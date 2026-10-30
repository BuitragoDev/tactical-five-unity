using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

public enum BackgroundBatchWaitResult
{
    NoBatch,
    Completed,
    Faulted,
    TimedOut
}

public class BackgroundBatchBusyException : System.Exception
{
    public BackgroundBatchBusyException(string message) : base(message) { }
}

public partial class DatabaseManager : MonoBehaviour
{
    /// <summary>
    /// Índices compuestos de F8-05. Se declaran aquí para poder auditarlos con
    /// `EXPLAIN QUERY PLAN` sin depender del orden de llamadas de CreateTables.
    /// </summary>
    public static readonly string[] QueryIndexDefinitions =
    {
        "CREATE INDEX IF NOT EXISTS IX_Games_Manager_Season_Type_Played_Day ON games(manager_id, season_id, game_type, is_played, game_day, id)",
        "CREATE INDEX IF NOT EXISTS IX_Games_Manager_Day_Played ON games(manager_id, game_day, is_played)",
        "CREATE INDEX IF NOT EXISTS IX_PlayerGameStats_Team_Game ON player_game_stats(team_id, game_id)",
        "CREATE INDEX IF NOT EXISTS IX_PlayerSeasonStats_Season_Team ON player_season_stats(season_id, team_id)",
        "CREATE INDEX IF NOT EXISTS IX_PlayerSeasonStats_Player_Season ON player_season_stats(player_id, season_id)",
        "CREATE INDEX IF NOT EXISTS IX_FinanceRecords_Team_Season_Type_Day ON finance_records(team_id, season_id, record_type, game_day)",
        "CREATE INDEX IF NOT EXISTS IX_Offers_Manager_Processed_Day ON offers(manager_id, processed, day_sent)",
        "CREATE INDEX IF NOT EXISTS IX_TradeOffers_Manager_Processed ON trade_offers(manager_id, processed)",
        "CREATE INDEX IF NOT EXISTS IX_DraftPicks_CurrentTeam_Year ON draft_picks(current_team_id, draft_year)",
        "CREATE INDEX IF NOT EXISTS IX_DraftPicks_DraftYear ON draft_picks(draft_year)",
        "CREATE INDEX IF NOT EXISTS IX_Lineup_Team ON team_lineup(team_id)",
        "CREATE INDEX IF NOT EXISTS IX_SeasonRecords_Season ON season_records(season_id)",
        "CREATE INDEX IF NOT EXISTS IX_MonthlyAwards_Season ON monthly_awards(season_id)",
        "CREATE INDEX IF NOT EXISTS IX_TeamCapHolds_Team_Season_Active ON team_cap_holds(team_id, season_id, is_active)"
    };

    public static DatabaseManager Instance { get; private set; }

    private SQLiteConnection _dbField;
    private static readonly AsyncLocal<SQLiteConnection> _ambientDb = new();

    /// <summary>
    /// Conexión activa. En el hilo principal devuelve _dbField; mientras un
    /// RunInBackgroundAsync() corre en un hilo de fondo, devuelve la conexión
    /// propia de ese hilo para que todos los helpers (que usan `_db`) escriban
    /// en ella sin tocar la conexión principal.
    /// </summary>
    private SQLiteConnection _db
    {
        get => _ambientDb.Value ?? _dbField;
        set => _dbField = value;
    }

    public SQLiteConnection Db => _db;
    private int _activeSaveSlot = 0;
    private bool _isTemplateSession = false;

    /// <summary>
    /// Raíz de datos alternativa (tests). Vacío = `persistentDataPath/TacticalFive`,
    /// que es donde viven `template.db` y las carpetas de imágenes personalizadas.
    /// </summary>
    public static string DataRootOverride { get; set; }

    private static string DataRoot => string.IsNullOrEmpty(DataRootOverride)
        ? Path.Combine(Application.persistentDataPath, "TacticalFive")
        : DataRootOverride;

    public string TemplateDbPath => Path.Combine(DataRoot, "template.db");

    public int ActiveSaveSlot => _activeSaveSlot;

    /// <summary>
    /// Motivo del último fallo de <see cref="InitSaveSlot"/> (null si fue bien).
    /// La pantalla de menú lo muestra al jugador en lugar de fallar en silencio.
    /// </summary>
    public string LastInitError { get; private set; }

    /// <summary>Último error de migración; se mantiene separado para diagnóstico.</summary>
    public string LastMigrationError { get; private set; }

    /// <summary>Último error observado al esperar un lote de fondo.</summary>
    public static string LastBackgroundBatchError { get; private set; }

    private string DbPath => GameSaveManager.GetSaveDbPath(_activeSaveSlot);

    private const int SCHEMA_VERSION = 2;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // No inicializar automáticamente — el flujo de guardado decide cuándo y qué slot abrir
    }

    /// <summary>
    /// Abre/crea el slot indicado. Devuelve false si la DB no se pudo inicializar
    /// (corrupta, bloqueada, sólo lectura...): en ese caso la sesión queda limpia
    /// y el llamador debe avisar y abortar.
    /// </summary>
    public bool InitSaveSlot(int slotNumber)
    {
        LastInitError = null;
        LastMigrationError = null;
        try
        {
            // No cambiar de conexión/partida mientras un lote de fondo sigue escribiendo:
            // lo cancelamos y esperamos a que termine (rollback limpio si aborta).
            CancelBackgroundBatches();
            var batchWait = WaitForBackgroundBatch();
            if (batchWait != BackgroundBatchWaitResult.NoBatch
                && batchWait != BackgroundBatchWaitResult.Completed)
            {
                LastInitError = batchWait == BackgroundBatchWaitResult.TimedOut
                    ? "No se puede cambiar de partida: un lote de fondo sigue activo. Inténtalo de nuevo."
                    : $"No se puede cambiar de partida: el lote de fondo terminó con error ({LastBackgroundBatchError}).";
                return false;
            }

            if (_dbField != null)
            {
                try { _dbField.Close(); } catch { }
                _dbField = null;
            }

            _activeSaveSlot = slotNumber;
            _isTemplateSession = false;
            string savePath = GameSaveManager.GetSaveDbPath(slotNumber);
            bool isNewGame = !HasSeededTeams(savePath);

            // El template se valida/reconstruye antes de abrir o crear el archivo del
            // slot. Un template viejo no debe dejar una save vacía como residuo.
            if (isNewGame)
                EnsureTemplateDb();

            // Las imágenes personalizadas se guardan por ámbito (save_{n}).
            TeamImages.SetScope($"save_{slotNumber}");

            // Crear directorio de guardados si no existe
            string dir = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _dbField = new SQLiteConnection(savePath);
            _db.ExecuteScalar<string>("PRAGMA journal_mode=WAL");
            // CreateTables/RunMigrations ya fallan si el archivo está corrupto: es
            // nuestra detección temprana de DB ilegible.
            CreateTables();
            RunMigrations();

            if (isNewGame)
            {
                // Clon + seed en una sola transacción: un fallo no puede dejar la
                // partida a medias (tablas sin jugadores, etc.).
                _db.RunInTransaction(() =>
                {
                    if (File.Exists(TemplateDbPath))
                        CloneFromTemplate();
                    SeedStaticDataIfNeeded();
                    ValidateSeedDatabase(_db, $"partida nueva (slot {slotNumber})");
                });
                MarkMigrationApplied("slot_initialized");
            }
            else if (!IsMigrationApplied("slot_initialized"))
            {
                // Partida anterior al marcador: completar lo que falte y marcarla.
                SeedStaticDataIfNeeded();
                MarkMigrationApplied("slot_initialized");
            }

            if (!IsMigrationApplied("service_years_legacy_estimate"))
            {
                _db.RunInTransaction(EstimateLegacyServiceYears);
                MarkMigrationApplied("service_years_legacy_estimate");
            }

            // F4-08: los agentes libres también necesitan años de servicio para
            // aplicar el mínimo correcto. La migración anterior solo miraba
            // jugadores con equipo o último equipo.
            if (!IsMigrationApplied("service_years_free_agents"))
            {
                _db.RunInTransaction(EstimateLegacyServiceYears);
                MarkMigrationApplied("service_years_free_agents");
            }

            // Las partidas existentes (creadas antes de la G-League) también
            // necesitan sus filiales y prospectos.
            EnsureGLeagueSeeded();

            // Economía: cualquier partida antigua debe tener settings para todos los
            // equipos, aunque el jugador nunca haya abierto Finanzas.
            EnsureTeamSettingsForAllTeams();

            // Imágenes personalizadas por partida: una partida nueva hace snapshot de
            // las imágenes del template (conserva las que había al empezar). Una partida
            // existente anterior a este sistema recibe su snapshot una sola vez.
            string scope = $"save_{slotNumber}";
            if (isNewGame)
            {
                TeamImages.DeleteScope(scope);
                TeamImages.CopyCustom("template", scope);
            }
            else if (!TeamImages.HasCustom(scope))
            {
                TeamImages.CopyCustom("template", scope);
            }

            Debug.Log($"[DB] Save slot {slotNumber} inicializado: {DbPath}");
            return true;
        }
        catch (Exception e)
        {
            var auditError = e as SeedAudit.SeedAuditException;
            LastInitError = auditError != null
                ? auditError.UserSummary
                : (e.Message.Length > 1200 ? e.Message.Substring(0, 1200) + "…" : e.Message);
            Debug.LogError($"[DB] Error inicializando el slot {slotNumber}: {e.Message}\n{e.StackTrace}");
            if (_dbField != null)
            {
                try { _dbField.Close(); } catch { }
                _dbField = null;
            }
            _activeSaveSlot = 0;
            return false;
        }
    }

    /// <summary>Cierra la conexión del slot activo (p. ej. antes de borrar la partida).</summary>
    public bool CloseActiveSlot()
    {
        CancelBackgroundBatches();
        var batchWait = WaitForBackgroundBatch();
        if (batchWait == BackgroundBatchWaitResult.TimedOut
            || batchWait == BackgroundBatchWaitResult.Faulted)
        {
            Debug.LogError($"[DB] No se cierra el slot: {LastBackgroundBatchError}");
            return false;
        }

        if (_dbField != null)
        {
            try { _dbField.Close(); } catch { }
            _dbField = null;
        }
        _activeSaveSlot = 0;
        _isTemplateSession = false;
        return true;
    }

    private static Task _runningBatch;
    private static CancellationTokenSource _batchCts;

    /// <summary>Token del lote de fondo en curso (None si no hay ninguno). Los métodos
    /// largos pueden consultarlo para abortar de forma limpia.</summary>
    public static CancellationToken BackgroundToken => _batchCts?.Token ?? CancellationToken.None;

    /// <summary>Cancela el lote de fondo en curso (p. ej. al cambiar de partida).</summary>
    public static void CancelBackgroundBatches()
    {
        try { _batchCts?.Cancel(); } catch { }
    }

    /// <summary>Lanza si el lote de fondo en curso fue cancelado. Úsalo en los puntos
    /// seguros de una operación larga para abortar y hacer rollback.</summary>
    public static void ThrowIfBatchCancelled()
    {
        var token = BackgroundToken;
        if (token.CanBeCanceled && token.IsCancellationRequested)
            throw new OperationCanceledException("Operación cancelada (se cambió de partida).");
    }

    /// <summary>
    /// Espera (con tope) a que termine un lote de fondo y devuelve qué ocurrió.
    /// El llamador decide si continúa, vuelve a esperar o aborta el cambio de slot.
    /// Un timeout NO inicia una segunda conexión sobre el mismo archivo.
    /// </summary>
    public static BackgroundBatchWaitResult WaitForBackgroundBatch(int timeoutMs = 10000)
    {
        var t = _runningBatch;
        if (t == null) return BackgroundBatchWaitResult.NoBatch;

        if (!t.IsCompleted)
        {
            Debug.LogWarning("[DB] Esperando a que termine un lote de fondo antes de continuar...");
            try
            {
                if (!t.Wait(timeoutMs))
                {
                    LastBackgroundBatchError = $"el lote sigue activo tras {timeoutMs} ms";
                    Debug.LogError($"[DB] Timeout esperando el lote de fondo ({timeoutMs} ms).");
                    return BackgroundBatchWaitResult.TimedOut;
                }
            }
            catch (Exception e) when (IsCancellation(e))
            {
                // Cancelar es una salida limpia: el lote ya no escribe.
                ClearTrackedBatch(t);
                return BackgroundBatchWaitResult.Completed;
            }
            catch (Exception e)
            {
                LastBackgroundBatchError = e.GetBaseException().Message;
                Debug.LogError($"[DB] El lote de fondo terminó con error: {LastBackgroundBatchError}");
                ClearTrackedBatch(t);
                return BackgroundBatchWaitResult.Faulted;
            }
        }

        try
        {
            t.GetAwaiter().GetResult();
            ClearTrackedBatch(t);
            return BackgroundBatchWaitResult.Completed;
        }
        catch (Exception e) when (IsCancellation(e))
        {
            ClearTrackedBatch(t);
            return BackgroundBatchWaitResult.Completed;
        }
        catch (Exception e)
        {
            LastBackgroundBatchError = e.GetBaseException().Message;
            Debug.LogError($"[DB] El lote de fondo terminó con error: {LastBackgroundBatchError}");
            ClearTrackedBatch(t);
            return BackgroundBatchWaitResult.Faulted;
        }
    }

    static bool IsCancellation(Exception exception)
    {
        if (exception is OperationCanceledException) return true;
        if (exception is AggregateException aggregate)
            return aggregate.Flatten().InnerExceptions.All(e => e is OperationCanceledException);
        return exception?.GetBaseException() is OperationCanceledException;
    }

    static void ClearTrackedBatch(Task task)
    {
        if (!ReferenceEquals(_runningBatch, task)) return;
        _runningBatch = null;
        _batchCts?.Dispose();
        _batchCts = null;
    }

    /// <summary>Registra el lote en curso para poder esperarlo/cancelarlo.</summary>
    private static Task TrackBatch(Task task, CancellationTokenSource cts)
    {
        _runningBatch = task;
        _batchCts = cts;
        return task;
    }

    /// <summary>Registra el lote en curso (sobrecarga para Task&lt;T&gt;).</summary>
    private static Task<T> TrackBatch<T>(Task<T> task, CancellationTokenSource cts)
    {
        _runningBatch = task;
        _batchCts = cts;
        return task;
    }

    static Task AbortedBatchTask()
        => Task.FromException(new BackgroundBatchBusyException(
            $"No se inicia un lote de fondo: {LastBackgroundBatchError ?? "hay otro lote activo"}."));

    static Task<T> AbortedBatchTask<T>()
        => Task.FromException<T>(new BackgroundBatchBusyException(
            $"No se inicia un lote de fondo: {LastBackgroundBatchError ?? "hay otro lote activo"}."));

    static bool CanStartBackgroundBatch()
    {
        var result = WaitForBackgroundBatch();
        return result == BackgroundBatchWaitResult.NoBatch
            || result == BackgroundBatchWaitResult.Completed;
    }

    public Task<T> RunInBackground<T>(Func<SQLiteConnection, T> work)
    {
        // Single-flight: si ya hay un lote en curso, esperar a que termine
        // (evita que dos lotes compartan estado/RNG y compitan por el lock de escritura).
        if (!CanStartBackgroundBatch()) return AbortedBatchTask<T>();
        string path = DbPath;
        var task = Task.Run(() =>
        {
            using var conn = new SQLiteConnection(path);
            return work(conn);
        });
        return TrackBatch(task, new CancellationTokenSource());
    }

    public Task RunInBackground(Action<SQLiteConnection> work)
    {
        // Single-flight: si ya hay un lote en curso, esperar a que termine
        // (evita que dos lotes compartan estado/RNG y compitan por el lock de escritura).
        if (!CanStartBackgroundBatch()) return AbortedBatchTask();
        string path = DbPath;
        var task = Task.Run(() =>
        {
            using var conn = new SQLiteConnection(path);
            work(conn);
        });
        return TrackBatch(task, new CancellationTokenSource());
    }

    /// <summary>
    /// Ejecuta `work` en un hilo de fondo usando una conexión SQLite propia.
    /// Mientras `work` corre, cualquier helper que acceda a `_db` (o Db) resuelve
    /// a esa conexión de fondo, de modo que operaciones pesadas como StartNewSeason
    /// escriben en un hilo secundario sin bloquear la UI.
    /// </summary>
    public Task RunInBackgroundAsync(Action work)
    {
        // Single-flight: si ya hay un lote en curso, esperar a que termine
        // (evita que dos lotes compartan estado/RNG y compitan por el lock de escritura).
        if (!CanStartBackgroundBatch()) return AbortedBatchTask();
        string path = DbPath;
        var task = Task.Run(() =>
        {
            using var conn = new SQLiteConnection(path);
            conn.ExecuteScalar<string>("PRAGMA journal_mode=WAL");
            var prev = _ambientDb.Value;
            _ambientDb.Value = conn;
            try { work(); }
            finally { _ambientDb.Value = prev; }
        });
        return TrackBatch(task, new CancellationTokenSource());
    }

    public Task<T> RunInBackgroundAsync<T>(Func<T> work)
    {
        // Single-flight: si ya hay un lote en curso, esperar a que termine
        // (evita que dos lotes compartan estado/RNG y compitan por el lock de escritura).
        if (!CanStartBackgroundBatch()) return AbortedBatchTask<T>();
        string path = DbPath;
        var task = Task.Run(() =>
        {
            using var conn = new SQLiteConnection(path);
            conn.ExecuteScalar<string>("PRAGMA journal_mode=WAL");
            var prev = _ambientDb.Value;
            _ambientDb.Value = conn;
            try { return work(); }
            finally { _ambientDb.Value = prev; }
        });
        return TrackBatch(task, new CancellationTokenSource());
    }

    public void EnsureTemplateDb()
    {
        lock (_templateLock)
        {
            if (File.Exists(TemplateDbPath) && new FileInfo(TemplateDbPath).Length > 1024)
            {
                try
                {
                    if (_isTemplateSession)
                        CloseTemplateSession();

                    using (var existing = new SQLiteConnection(TemplateDbPath))
                    {
                        ValidateSeedDatabase(existing, "template existente", includeGLeague: false);
                        string fingerprint = GetMeta(existing, "seed_fingerprint");
                        if (string.Equals(fingerprint, SeedBaseline.Fingerprint, StringComparison.Ordinal))
                            return;
                    }

                    Debug.LogWarning("[DB] Template obsoleto (sin sello o de otra versión del seed); se reconstruirá.");
                }
                catch (Exception ex)
                {
                    var auditError = ex as SeedAudit.SeedAuditException;
                    Debug.LogWarning("[DB] Template inválido/corrupto; se reconstruirá. " +
                                     (auditError != null ? auditError.UserSummary : ex.Message));
                    if (auditError != null)
                        Debug.Log("[DB] Informe completo del template obsoleto:\n" + auditError.FullReport);
                }
            }

            EnsureTemplateDirectory(TemplateDbPath);
            string tmp = TemplateDbPath + ".tmp";
            TryDeleteFile(tmp);
            BuildTemplateFile(tmp);
            PublishTemplateFile(tmp, TemplateDbPath);
            Debug.Log($"[DB] Template database created/refreshed: {TemplateDbPath}");
        }
    }

    private static readonly object _templateLock = new();

    [ThreadStatic] private static System.Random _threadRng;
    private static System.Random Rng => _threadRng ??= new System.Random();

    public void BuildTemplateDatabaseInBackground(string dbPath)
    {
        lock (_templateLock)
        {
            EnsureTemplateDirectory(dbPath);
            BuildTemplateFile(dbPath);
            Debug.Log($"[DB] Template database created (background): {dbPath}");
        }
    }

    /// <summary>
    /// Reconstruye el template con el seed actual y lo publica de forma atómica: se
    /// genera en `.tmp`, se valida y solo entonces sustituye al definitivo (que queda
    /// como `.bak`). Si la construcción falla, el template anterior sigue intacto.
    /// Lo usa el reset del Editor, que antes borraba el template antes de construir.
    /// </summary>
    public void RebuildTemplateDb()
        => RebuildTemplateDb(TemplateDbPath);

    /// <summary>
    /// Variante para hilos de fondo: recibe la ruta ya resuelta en el hilo principal.
    /// `Application.persistentDataPath` no se puede consultar fuera del main thread.
    /// </summary>
    public void RebuildTemplateDb(string databasePath)
    {
        if (string.IsNullOrEmpty(databasePath))
            throw new System.ArgumentException("databasePath vacío", nameof(databasePath));

        lock (_templateLock)
        {
            EnsureTemplateDirectory(databasePath);
            string tmp = databasePath + ".tmp";
            TryDeleteFile(tmp);
            BuildTemplateFile(tmp);
            PublishTemplateFile(tmp, databasePath);
            Debug.Log($"[DB] Template database rebuilt: {databasePath}");
        }
    }

    /// <summary>
    /// Construye una DB de plantilla en `dbPath` con una conexión LOCAL.
    /// Nunca toca `_dbField` (que puede estar en uso por el hilo principal): instala
    /// la conexión local como "ambient" mientras corre, de modo que los helpers
    /// (CreateTables/RunMigrations/Seed) escriban en ella. La conexión se cierra (y
    /// el ambient se restaura) incluso si algo lanza.
    /// </summary>
    private void BuildTemplateFile(string dbPath)
    {
        using var conn = new SQLiteConnection(dbPath);
        var prev = _ambientDb.Value;
        _ambientDb.Value = conn;
        try
        {
            CreateTables();
            RunMigrations();
            SeedStaticDataIfNeeded();

            // El auditor es el gate real: un seed inválido no se publica. La huella
            // se comprueba después para que nunca enmascare un error de datos.
            ValidateSeedDatabase(conn, $"template generado ({dbPath})", includeGLeague: false);

            string fingerprint = SeedBaseline.Compute(conn);
            SetMeta(conn, "seed_fingerprint", fingerprint);
            // La huella solo detecta "el seed cambió desde la última revisión": si no
            // coincide, el template se reconstruye con el seed actual y se avisa. El
            // EditMode (SeedBaseline tests) es quien exige actualizar la constante.
            if (!string.Equals(fingerprint, SeedBaseline.Fingerprint, StringComparison.Ordinal))
                Debug.LogWarning(
                    "[DB] El seed cambió respecto a SeedBaseline.Fingerprint; el template se " +
                    $"reconstruyó con el seed actual. Actualiza la constante tras revisarlo: {fingerprint}");
        }
        finally
        {
            _ambientDb.Value = prev;
        }
    }

    public void InitTemplateSession()
    {
        EnsureTemplateDirectory(TemplateDbPath);
        // El Editor trabaja sobre el template: sus imágenes custom viven en el
        // ámbito "template" (las partidas nuevas hacen snapshot al empezar).
        TeamImages.SetScope("template");
        if (_dbField != null)
        {
            try { _dbField.Close(); } catch { }
            _dbField = null;
        }
        _dbField = new SQLiteConnection(TemplateDbPath);
        CreateTables();
        RunMigrations();
        _isTemplateSession = true;
        Debug.Log("[DB] Template session started");
    }

    private static void EnsureTemplateDirectory(string databasePath)
    {
        string directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    /// <summary>Audita una base sembrada antes de publicarla o clonarla.</summary>
    void ValidateSeedDatabase(SQLiteConnection database, string source)
        => ValidateSeedDatabase(database, source, includeGLeague: true);

    void ValidateSeedDatabase(SQLiteConnection database, string source, bool includeGLeague)
    {
        SeedAudit.Report report;
        try
        {
            report = SeedAudit.Run(
                database.Table<TeamData>().ToList(),
                database.Table<PlayerData>().ToList(),
                database.Table<LeagueSettingsData>().FirstOrDefault(),
                includeGLeague ? database.Table<GLeagueTeamData>().ToList() : null,
                includeGLeague ? database.Table<GLeaguePlayerData>().ToList() : null,
                includeGLeague);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"No se pudo auditar el seed de {source}: {ex.Message}", ex);
        }

        report.ThrowIfInvalid(source);
    }

    private static bool HasSeededTeams(string databasePath)
    {
        if (!File.Exists(databasePath) || new FileInfo(databasePath).Length == 0)
            return false;

        using var probe = new SQLiteConnection(databasePath);
        bool hasTable = probe.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='teams'") > 0;
        return hasTable && probe.Table<TeamData>().Count() > 0;
    }

    private static string GetMeta(SQLiteConnection database, string key)
    {
        bool hasTable = database.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='db_meta'") > 0;
        if (!hasTable) return null;
        return database.Table<DbMetaData>().Where(m => m.key == key).Select(m => m.value).FirstOrDefault();
    }

    private static void SetMeta(SQLiteConnection database, string key, string value)
    {
        database.CreateTable<DbMetaData>();
        var row = database.Table<DbMetaData>().FirstOrDefault(m => m.key == key);
        if (row == null)
            database.Insert(new DbMetaData { key = key, value = value });
        else
        {
            row.value = value;
            database.Update(row);
        }
    }

    private void PublishTemplateFile(string temporaryPath, string databasePath)
    {
        string backupPath = databasePath + ".bak";
        string walPath = databasePath + "-wal";
        string shmPath = databasePath + "-shm";
        string backupWalPath = backupPath + "-wal";
        string backupShmPath = backupPath + "-shm";
        bool hadWal = File.Exists(walPath);
        bool hadShm = File.Exists(shmPath);

        if (!File.Exists(databasePath))
        {
            File.Move(temporaryPath, databasePath);
            return;
        }

        // Conserva el template previo para diagnóstico/recuperación. La publicación
        // del temporal sobre el destino usa replace atómico en el mismo filesystem.
        File.Copy(databasePath, backupPath, true);
        if (hadWal) File.Copy(walPath, backupWalPath, true);
        if (hadShm) File.Copy(shmPath, backupShmPath, true);
        try
        {
            TryDeleteFile(walPath);
            TryDeleteFile(shmPath);
            File.Replace(temporaryPath, databasePath, null);
        }
        catch
        {
            if (hadWal && File.Exists(backupWalPath)) File.Copy(backupWalPath, walPath, true);
            if (hadShm && File.Exists(backupShmPath)) File.Copy(backupShmPath, shmPath, true);
            throw;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public void CloseTemplateSession()
    {
        if (!_isTemplateSession) return;
        if (_dbField != null)
        {
            try { _dbField.Close(); } catch { }
            _dbField = null;
        }
        _isTemplateSession = false;
        Debug.Log("[DB] Template session closed");
    }

    void CloneFromTemplate()
    {
        using var template = new SQLiteConnection(TemplateDbPath);
        ValidateSeedDatabase(template, $"template antes de clonar ({TemplateDbPath})", includeGLeague: false);

        template.CreateTable<TradeData>();
        template.CreateTable<TrainingData>();
        template.CreateTable<PlayerPersonalityData>();
        template.CreateTable<PlayerRelationshipData>();
        template.CreateTable<LineupData>();
        template.CreateTable<TradeOfferData>();
        template.CreateTable<DraftPickData>();
        _db.InsertAll(template.Table<TeamData>().ToList());
        _db.InsertAll(template.Table<PlayerData>().ToList());
        _db.InsertAll(template.Table<LeagueSettingsData>().ToList());
        _db.InsertAll(template.Table<SponsorData>().ToList());
        _db.InsertAll(template.Table<TvChannelData>().ToList());
        _db.InsertAll(template.Table<HistoricalRecordData>().ToList());
        _db.InsertAll(template.Table<TeamRecordData>().ToList());
        _db.InsertAll(template.Table<HistoricalPlayerStatsData>().ToList());
        _db.InsertAll(template.Table<FinalsRecord>().ToList());
        _db.InsertAll(template.Table<AwardsRecord>().ToList());
        _db.InsertAll(template.Table<QuintetRecord>().ToList());
        _db.InsertAll(template.Table<AllStarRecord>().ToList());
        _db.InsertAll(template.Table<AllStarAppearanceSeed>().ToList());
        _db.InsertAll(template.Table<TradeData>().ToList());
        _db.InsertAll(template.Table<HallOfFameData>().ToList());
        _db.InsertAll(template.Table<DbMetaData>().ToList());
        Debug.Log("[DB] Static data cloned from template");
    }

    void CreateTables()
    {
        _db.CreateTable<TeamData>();
        _db.CreateTable<LoanData>();
        _db.CreateTable<ManagerData>();
        _db.CreateTable<LeagueSettingsData>();
        _db.CreateTable<GameData>();
        _db.CreateTable<SeasonData>();
        _db.CreateTable<PlayerData>();
        _db.CreateTable<TeamCapHoldData>();
        try { _db.Execute("ALTER TABLE players ADD COLUMN college TEXT DEFAULT ''"); } catch { }
        _db.CreateTable<EmployeeData>();
        _db.CreateTable<ScoutData>();
        _db.CreateTable<PlayerGameStats>();
        _db.CreateTable<FinanceRecord>();
        _db.CreateTable<DeadMoneyData>();
        _db.CreateTable<DraftStateData>();
        _db.CreateTable<StorylineData>();
        _db.CreateTable<SeasonRecord>();
        _db.CreateTable<MessageData>();
        _db.CreateTable<SponsorData>();
        _db.CreateTable<TvChannelData>();
        _db.CreateTable<TeamSettingsData>();
        _db.CreateTable<HistoricalRecordData>();
        _db.CreateTable<TeamRecordData>();
        _db.CreateTable<SeasonGameRecordData>();
        _db.CreateTable<HistoricalPlayerStatsData>();
        _db.CreateTable<GameAttendanceData>();
        _db.CreateTable<FinalsPlayerStatsData>();
        _db.CreateTable<FinalsRecord>();
        _db.CreateTable<AwardsRecord>();
        _db.CreateTable<QuintetRecord>();
        _db.CreateTable<TradeData>();
        _db.CreateTable<TrainingData>();
        _db.CreateTable<PlayerPersonalityData>();
        _db.CreateTable<PlayerRelationshipData>();
        _db.CreateTable<LineupData>();
        _db.CreateTable<OfferData>();
        _db.CreateTable<TradeOfferData>();
        _db.CreateTable<DraftPickData>();
        _db.CreateTable<CoachRankingData>();
        _db.CreateTable<AllStarRecord>();
        _db.CreateTable<PlayerSeasonStatRow>();
        _db.CreateTable<AllStarAppearanceSeed>();
        _db.CreateTable<MonthlyAwardData>();
        // Migración: el índice antiguo era UNIQUE solo sobre manager_id (creaba
        // UNIQUE(manager_id)); se elimina para que CreateTable genere el índice
        // compuesto UNIQUE(manager_id, type) definido en GmAchievementData.
        try { _db.Execute("DROP INDEX IF EXISTS \"IX_Achievements_Manager_Type\""); } catch { }
        _db.CreateTable<GmAchievementData>();
        _db.CreateTable<HallOfFameData>();
        _db.CreateTable<RetiredNumberData>();
        _db.CreateTable<GLeagueSeasonStat>();
        _db.CreateTable<GLeagueTeamData>();
        _db.CreateTable<GLeaguePlayerData>();
        _db.CreateTable<GLeagueChampionData>();
        _db.CreateTable<SeasonAttendanceSummaryData>();
        _db.CreateTable<PlayoffSeriesData>();
        _db.CreateTable<AllStarRosterData>();
        _db.CreateTable<MilestoneData>();
        _db.CreateTable<InjuryHistoryData>();
        _db.CreateTable<TournamentEditionData>();
        _db.CreateTable<TournamentGroupData>();
        _db.CreateTable<TournamentStandingData>();
        _db.CreateTable<SummerLeagueStatData>();
        _db.CreateTable<CityProjectData>();
        _db.CreateTable<CityEventData>();
        _db.CreateTable<DbMetaData>();
        _db.CreateTable<SchemaMigrationAttemptData>();
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_GLeagueStats_PlayerId ON gleague_season_stats(player_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_GLeagueStats_SeasonId ON gleague_season_stats(season_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_GLeaguePlayers_TeamId ON gleague_players(gleague_team_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_GLeagueChampions_Manager ON gleague_champions(manager_id, season_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_Games_Standings ON games(manager_id, game_type, is_played, game_day)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerGameStats_GameId ON player_game_stats(game_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerGameStats_PlayerId ON player_game_stats(player_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerGameStats_TeamId ON player_game_stats(team_id)");
        // Índices que faltaban: las consultas por equipo/temporada/bandeja eran
        // full scans y se notan a lo largo de muchas temporadas.
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_Players_TeamId ON players(team_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_Games_SeasonId ON games(season_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_Messages_ManagerId ON messages(manager_id, id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_TournamentEditions_Season ON tournament_editions(manager_id, season_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_TournamentGroups_Edition ON tournament_groups(edition_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_TournamentStandings_Edition ON tournament_standings(edition_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_SummerLeague_Season ON summer_league_stats(manager_id, season_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_CityProjects_Season ON city_projects(manager_id, season_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_CityEvents_Season ON city_events(manager_id, season_id)");
        foreach (var indexSql in QueryIndexDefinitions)
            _db.Execute(indexSql);
    }

    void RunMigrations()
    {
        LastMigrationError = null;
        try
        {
            // Atómico: una migración obligatoria que falle revierte todo el bloque.
            _db.RunInTransaction(() => RunMigrationsCore());
            _db.Execute("PRAGMA user_version = " + SCHEMA_VERSION);
            RecordMigrationAttempt("run_migrations", "ok", null);
        }
        catch (System.Exception ex)
        {
            string name = (ex as DatabaseMigrationException)?.MigrationName ?? "run_migrations";
            LastMigrationError = ex.Message;
            try { if (_db.IsInTransaction) _db.Rollback(); } catch { }
            RecordMigrationAttempt(name, "failed", ex.Message);
            throw;
        }
    }

    void RecordMigrationAttempt(string name, string status, string error)
    {
        try
        {
            _db.Insert(new SchemaMigrationAttemptData
            {
                name = name,
                status = status,
                error = error,
                attempted_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
        catch (System.Exception logEx)
        {
            Debug.LogError($"[DB] No se pudo registrar el intento de migración {name}: {logEx.Message}");
        }
    }

    /// <summary>Añade una columna solo si no existe (idempotente y segura por columna).</summary>
    private bool AddColumnIfMissing(string table, string column, string ddl)
    {
        var cols = _db.Query<ColumnInfo>($"PRAGMA table_info({table})");
        if (cols.Any(c => c.name == column)) return false;
        _db.Execute($"ALTER TABLE {table} ADD COLUMN {ddl}");
        Debug.Log($"[DB] Migration: added {column} to {table}");
        return true;
    }

    void EstimateLegacyServiceYears()
    {
        // Los saves/seed anteriores no guardaban service time. Esta estimación
        // por edad/antigüedad evita atribuir cero años a toda la plantilla o a los
        // agentes libres; no sustituye un historial CBA real.
        _db.Execute("UPDATE players SET years_of_service = MAX(COALESCE(seasons_with_team, 0), MAX(0, age - 19)) WHERE years_of_service = 0 AND is_rookie = 0");
    }

    void RunMigrationsCore()
    {
        // Registro de migraciones one-time dentro del DB (los flags viven con el slot;
        // borrar el slot reinicia el estado)
        _db.Execute("CREATE TABLE IF NOT EXISTS schema_migrations (name TEXT PRIMARY KEY, applied_at TEXT NOT NULL)");

        AddColumnIfMissing("players", "years_of_service", "years_of_service INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing("players", "free_agent_type", "free_agent_type TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing("players", "qualifying_offer_salary", "qualifying_offer_salary INTEGER NOT NULL DEFAULT 0");

        // Add fan_confidence to managers if missing
        var managerCols = _db.Query<ColumnInfo>("PRAGMA table_info(managers)");
        bool hasFanConfidence = managerCols.Any(c => c.name == "fan_confidence");
        if (!hasFanConfidence)
        {
            _db.Execute("ALTER TABLE managers ADD COLUMN fan_confidence INTEGER DEFAULT 50");
            Debug.Log("[DB] Migration: added fan_confidence to managers");
        }

        // Add objective to teams if missing
        var teamCols = _db.Query<ColumnInfo>("PRAGMA table_info(teams)");
        bool hasObjective = teamCols.Any(c => c.name == "objective");
        if (!hasObjective)
        {
            _db.Execute("ALTER TABLE teams ADD COLUMN objective TEXT");
            Debug.Log("[DB] Migration: added objective to teams");
        }

        // Add renewal_cooldown_day to players if missing
        var playerCols = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool hasRenewalCooldown = playerCols.Any(c => c.name == "renewal_cooldown_day");
        if (!hasRenewalCooldown)
        {
            _db.Execute("ALTER TABLE players ADD COLUMN renewal_cooldown_day INTEGER DEFAULT 0");
            Debug.Log("[DB] Migration: added renewal_cooldown_day to players");
        }

        // Add number (dorsal) to players if missing
        var playerColsNum = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool hasNumber = playerColsNum.Any(c => c.name == "number");
        if (!hasNumber)
        {
            _db.Execute("ALTER TABLE players ADD COLUMN number INTEGER DEFAULT 0");
            Debug.Log("[DB] Migration: added number to players");
        }

        // Add budget_red_warnings to managers if missing
        var managerCols2 = _db.Query<ColumnInfo>("PRAGMA table_info(managers)");
        bool hasBudgetWarnings = managerCols2.Any(c => c.name == "budget_red_warnings");
        if (!hasBudgetWarnings)
        {
            _db.Execute("ALTER TABLE managers ADD COLUMN budget_red_warnings INTEGER DEFAULT 0");
            Debug.Log("[DB] Migration: added budget_red_warnings to managers");
        }

        // Add morale to players if missing
        var playerCols2 = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool hasMorale = playerCols2.Any(c => c.name == "morale");
        if (!hasMorale)
        {
            _db.Execute("ALTER TABLE players ADD COLUMN morale INTEGER DEFAULT 50");
            Debug.Log("[DB] Migration: added morale to players");
        }

        // Add role to players if missing
        var playerCols5 = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool hasRole = playerCols5.Any(c => c.name == "role");
        if (!hasRole)
        {
            _db.Execute("ALTER TABLE players ADD COLUMN role INTEGER DEFAULT 3");
            Debug.Log("[DB] Migration: added role to players");
        }

        // Recalculate overall for all players from their 11 attributes (fix seed data mismatch)
        if (!IsMigrationApplied("overall_recalc"))
        {
            var allPlayers = _db.Table<PlayerData>().ToList();
            foreach (var p in allPlayers)
            {
                int calc = p.GetCalculatedAverage();   // fuente única: PlayerRating
                if (p.overall != calc)
                {
                    p.overall = calc;
                    _db.Update(p);
                }
            }
            MarkMigrationApplied("overall_recalc");
            Debug.Log("[DB] Migration: recalculated overall for all players");
        }

        // La fórmula canónica se corrigió después de que `overall_recalc` pudiera
        // quedar aplicada con truncamiento. Esta segunda migración vuelve a derivar
        // el valor para saves existentes; solo se marca si todo el lote termina bien.
        if (!IsMigrationApplied("overall_recalc_v2"))
        {
            var allPlayers = _db.Table<PlayerData>().ToList();
            foreach (var p in allPlayers)
            {
                int calc = p.GetCalculatedAverage();
                if (p.overall == calc) continue;
                p.overall = calc;
                _db.Update(p);
            }
            MarkMigrationApplied("overall_recalc_v2");
            Debug.Log("[DB] Migration: canonical overall recalculated for all players (v2)");
        }

        // Los prospectos G-League nacían con overall = potential / 11 (fórmula mal
        // escrita en GLeagueSeeder): las partidas viejas hay que corregirlas una vez.
        if (!IsMigrationApplied("gleague_overall_recalc"))
        {
            var prospects = _db.Table<GLeaguePlayerData>().ToList();
            int fixedProspects = 0;
            foreach (var p in prospects)
            {
                int calc = p.GetCalculatedAverage();   // fuente única: PlayerRating
                if (p.overall != calc)
                {
                    p.overall = calc;
                    _db.Update(p);
                    fixedProspects++;
                }
            }
            MarkMigrationApplied("gleague_overall_recalc");
            if (fixedProspects > 0)
                Debug.Log($"[DB] Migration: recalculated overall for {fixedProspects} G-League prospects");
        }

        // Add slot_index to team_lineup if missing
        var lineupCols = _db.Query<ColumnInfo>("PRAGMA table_info(team_lineup)");
        bool hasSlotIndex = lineupCols.Any(c => c.name == "slot_index");
        if (!hasSlotIndex)
        {
            _db.Execute("ALTER TABLE team_lineup ADD COLUMN slot_index INTEGER DEFAULT -1");
            Debug.Log("[DB] Migration: added slot_index to team_lineup");
        }

        // Add rotation_minutes to team_lineup if missing (pantalla ROTACIÓN)
        if (!lineupCols.Any(c => c.name == "rotation_minutes"))
        {
            _db.Execute("ALTER TABLE team_lineup ADD COLUMN rotation_minutes INTEGER DEFAULT -1");
            Debug.Log("[DB] Migration: added rotation_minutes to team_lineup");
        }

        // Add photo to players if missing
        var playerCols3 = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool hasPhoto = playerCols3.Any(c => c.name == "photo");
        if (!hasPhoto)
        {
            _db.Execute("ALTER TABLE players ADD COLUMN photo TEXT DEFAULT ''");
            Debug.Log("[DB] Migration: added photo to players");
        }

        // Add secondary_position to players if missing
        var playerCols4 = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool hasSecondaryPos = playerCols4.Any(c => c.name == "secondary_position");
        if (!hasSecondaryPos)
        {
            _db.Execute("ALTER TABLE players ADD COLUMN secondary_position TEXT DEFAULT ''");
            Debug.Log("[DB] Migration: added secondary_position to players");
        }

        // Migrate existing players: assign adjacent secondary position
        try
        {
            int migrated = _db.Execute(@"
                UPDATE players
                SET secondary_position = CASE position
                    WHEN 'PG' THEN 'SG'
                    WHEN 'SG' THEN CASE WHEN height_cm < 198 THEN 'PG' ELSE 'SF' END
                    WHEN 'SF' THEN 'PF'
                    WHEN 'PF' THEN 'C'
                    WHEN 'C'  THEN 'PF'
                    ELSE ''
                END
                WHERE secondary_position IS NULL OR secondary_position = ''");
            if (migrated > 0)
                Debug.Log($"[DB] Migration: assigned adjacent secondary_position to {migrated} players");
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("players_secondary_position", ex);
        }

        // Add guaranteed_years/has_team_option/has_player_option to players if missing
        // (cada columna se comprueba por separado: un ALTER fallido no bloquea los demás)
        var playerColsOpt = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool addedGuaranteed = AddColumnIfMissing("players", "guaranteed_years", "guaranteed_years INTEGER DEFAULT 0");
        AddColumnIfMissing("players", "has_team_option", "has_team_option INTEGER DEFAULT 0");
        AddColumnIfMissing("players", "has_player_option", "has_player_option INTEGER DEFAULT 0");
        AddColumnIfMissing("players", "waived_game_day", "waived_game_day INTEGER DEFAULT -1");
        AddColumnIfMissing("players", "injury_total_days", "injury_total_days INTEGER DEFAULT 0");
        AddColumnIfMissing("players", "recovery_risk", "recovery_risk INTEGER DEFAULT 0");
        if (addedGuaranteed)
            _db.Execute("UPDATE players SET guaranteed_years = contract_years WHERE contract_years > 0");

        // Add last_team_id to players if missing
        if (!playerColsOpt.Any(c => c.name == "last_team_id"))
        {
            _db.Execute("ALTER TABLE players ADD COLUMN last_team_id INTEGER DEFAULT 0");
            _db.Execute("UPDATE players SET last_team_id = team_id WHERE team_id != 0");
            Debug.Log("[DB] Migration: added last_team_id to players");
        }

        // Add is_fired to managers if missing
        var managerColsFired = _db.Query<ColumnInfo>("PRAGMA table_info(managers)");
        if (managerColsFired.Count > 0 && !managerColsFired.Any(c => c.name == "is_fired"))
        {
            _db.Execute("ALTER TABLE managers ADD COLUMN is_fired INTEGER DEFAULT 0");
            Debug.Log("[DB] Migration: added is_fired to managers");
        }

        // Add guaranteed_years/has_team_option/has_player_option to offers if missing
        bool addedOfferGuaranteed = AddColumnIfMissing("offers", "guaranteed_years", "guaranteed_years INTEGER DEFAULT 0");
        AddColumnIfMissing("offers", "has_team_option", "has_team_option INTEGER DEFAULT 0");
        AddColumnIfMissing("offers", "has_player_option", "has_player_option INTEGER DEFAULT 0");
        if (addedOfferGuaranteed)
            _db.Execute("UPDATE offers SET guaranteed_years = offer_years WHERE guaranteed_years = 0");

        // Migrate trade_offers to multi-player schema
        try
        {
            var toCols = _db.Query<ColumnInfo>("PRAGMA table_info(trade_offers)");
            if (toCols.Count > 0 && !toCols.Any(c => c.name == "player_ids_out"))
            {
                _db.Execute("ALTER TABLE trade_offers ADD COLUMN player_ids_out TEXT DEFAULT ''");
                _db.Execute("ALTER TABLE trade_offers ADD COLUMN player_ids_in TEXT DEFAULT ''");
                Debug.Log("[DB] Migration: added player_ids_out/player_ids_in to trade_offers");
            }
            if (toCols.Count > 0 && !toCols.Any(c => c.name == "pick_ids_out"))
            {
                _db.Execute("ALTER TABLE trade_offers ADD COLUMN pick_ids_out TEXT DEFAULT ''");
                _db.Execute("ALTER TABLE trade_offers ADD COLUMN pick_ids_in TEXT DEFAULT ''");
                Debug.Log("[DB] Migration: added pick_ids_out/pick_ids_in to trade_offers");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("trade_offers_multifield", ex);
        }

        // Add draft_year to draft_picks (multi-year future picks)
        try
        {
            var dpCols = _db.Query<ColumnInfo>("PRAGMA table_info(draft_picks)");
            if (dpCols.Count > 0 && !dpCols.Any(c => c.name == "draft_year"))
            {
                _db.Execute("ALTER TABLE draft_picks ADD COLUMN draft_year INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added draft_year to draft_picks");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("draft_picks_draft_year", ex);
        }

        // Reset draft_picks: clear any legacy picks (seeded by overall at
        // season start) and re-seed picks for the current manager's active
        // season based on the previous season's standings (or overall+rep
        // fallback if there is no previous season).
        if (!IsMigrationApplied("draft_picks_reset"))
        {
            try
            {
                int deleted = _db.Execute("DELETE FROM draft_picks");

                var mgr = GetActiveManager();
                if (mgr != null)
                {
                    var activeSeason = _db.Table<SeasonData>()
                        .Where(s => s.manager_id == mgr.id && s.is_active == 1)
                        .OrderByDescending(s => s.year_start)
                        .FirstOrDefault();
                    if (activeSeason != null)
                    {
                        int? prevSeasonId = _db.Table<SeasonData>()
                            .Where(s => s.manager_id == mgr.id && s.id != activeSeason.id)
                            .OrderByDescending(s => s.year_start)
                            .Select(s => (int?)s.id)
                            .FirstOrDefault();
                        SeedDraftPicksMultiYear(activeSeason, prevSeasonId);
                    }
                }

                MarkMigrationApplied("draft_picks_reset");
                Debug.LogWarning($"[DB] Migration: cleared {deleted} legacy draft_picks and re-seeded for current manager.");
            }
            catch (System.Exception ex)
            {
                throw new DatabaseMigrationException("draft_picks_reset", ex);
            }
        }

        // Multi-year picks: re-seed the active season with current + future years.
        // (Los saves antiguos con un solo año se migran aquí.)
        if (!IsMigrationApplied("draft_picks_multiyear"))
        {
            try
            {
                _db.Execute("DELETE FROM draft_picks");

                var mgr = GetActiveManager();
                if (mgr != null)
                {
                    var activeSeason = _db.Table<SeasonData>()
                        .Where(s => s.manager_id == mgr.id && s.is_active == 1)
                        .OrderByDescending(s => s.year_start)
                        .FirstOrDefault();
                    if (activeSeason != null)
                    {
                        int? prevSeasonId = _db.Table<SeasonData>()
                            .Where(s => s.manager_id == mgr.id && s.id != activeSeason.id)
                            .OrderByDescending(s => s.year_start)
                            .Select(s => (int?)s.id)
                            .FirstOrDefault();
                        SeedDraftPicksMultiYear(activeSeason, prevSeasonId);
                    }
                }

                MarkMigrationApplied("draft_picks_multiyear");
                Debug.LogWarning("[DB] Migration: re-seeded draft_picks for 4 years.");
            }
            catch (System.Exception ex)
            {
                throw new DatabaseMigrationException("draft_picks_multiyear", ex);
            }
        }

        // Add last_ai_trade_day to seasons
        try
        {
            var sCols = _db.Query<ColumnInfo>("PRAGMA table_info(seasons)");
            if (sCols.Count > 0 && !sCols.Any(c => c.name == "last_ai_trade_day"))
            {
                _db.Execute("ALTER TABLE seasons ADD COLUMN last_ai_trade_day INTEGER DEFAULT -999");
                Debug.Log("[DB] Migration: added last_ai_trade_day to seasons");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("seasons_columns", ex);
        }

        // Add pick_id to trades
        try
        {
            var tCols = _db.Query<ColumnInfo>("PRAGMA table_info(trades)");
            if (tCols.Count > 0 && !tCols.Any(c => c.name == "pick_id"))
            {
                _db.Execute("ALTER TABLE trades ADD COLUMN pick_id INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added pick_id to trades");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("trades_pick_id", ex);
        }

        // Add first_apron_hard_capped to teams
        try
        {
            var tCols = _db.Query<ColumnInfo>("PRAGMA table_info(teams)");
            if (tCols.Count > 0 && !tCols.Any(c => c.name == "first_apron_hard_capped"))
            {
                _db.Execute("ALTER TABLE teams ADD COLUMN first_apron_hard_capped INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added first_apron_hard_capped to teams");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("teams_first_apron_hard_capped", ex);
        }

        // Add career stat columns to managers
        try
        {
            var mgrCols = _db.Query<ColumnInfo>("PRAGMA table_info(managers)");
            if (mgrCols.Count > 0)
            {
                if (!mgrCols.Any(c => c.name == "career_reg_wins"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN career_reg_wins INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added career_reg_wins to managers");
                }
                if (!mgrCols.Any(c => c.name == "career_reg_losses"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN career_reg_losses INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added career_reg_losses to managers");
                }
                if (!mgrCols.Any(c => c.name == "career_po_wins"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN career_po_wins INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added career_po_wins to managers");
                }
                if (!mgrCols.Any(c => c.name == "career_po_losses"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN career_po_losses INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added career_po_losses to managers");
                }
                if (!mgrCols.Any(c => c.name == "championships"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN championships INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added championships to managers");
                }
                if (!mgrCols.Any(c => c.name == "seasons_completed"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN seasons_completed INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added seasons_completed to managers");
                }
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("managers_career_columns", ex);
        }

        // Add fisico to players if missing
        try
        {
            var playerColsFisico = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
            if (!playerColsFisico.Any(c => c.name == "fisico"))
            {
                _db.Execute("ALTER TABLE players ADD COLUMN fisico INTEGER DEFAULT 99");
                Debug.Log("[DB] Migration: added fisico to players");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("players_fisico", ex);
        }

        // Storylines: columnas nuevas (sujeto, resolución, resumen y cooldown)
        AddColumnIfMissing("storylines", "subject_player_id", "subject_player_id INTEGER DEFAULT 0");
        AddColumnIfMissing("storylines", "resolution", "resolution TEXT DEFAULT ''");
        AddColumnIfMissing("storylines", "summary", "summary TEXT DEFAULT ''");
        AddColumnIfMissing("storylines", "last_news_game_day", "last_news_game_day INTEGER DEFAULT 0");

        // Esquema táctico del asistente (empleados)
        AddColumnIfMissing("employees", "scheme_pace", "scheme_pace INTEGER DEFAULT 0");
        AddColumnIfMissing("employees", "scheme_offense", "scheme_offense INTEGER DEFAULT 0");
        AddColumnIfMissing("employees", "scheme_defense", "scheme_defense INTEGER DEFAULT 1");
        AddColumnIfMissing("employees", "scheme_set", "scheme_set INTEGER DEFAULT 0");

        // Plan de partido en teams (táctica persistente del equipo)
        AddColumnIfMissing("teams", "plan_pace", "plan_pace INTEGER DEFAULT 0");
        AddColumnIfMissing("teams", "plan_offense", "plan_offense INTEGER DEFAULT 0");
        AddColumnIfMissing("teams", "plan_defense", "plan_defense INTEGER DEFAULT 1");

        // Palmarés de la NBA Cup: finalista y marcador de la final.
        bool cupFinalistAdded = AddColumnIfMissing("tournament_editions", "finalist_team_id", "finalist_team_id INTEGER NOT NULL DEFAULT 0");
        bool cupScoreAdded = AddColumnIfMissing("tournament_editions", "final_score", "final_score TEXT NOT NULL DEFAULT ''");
        if (cupFinalistAdded || cupScoreAdded)
            BackfillCupFinalDetails();

        // El ranking de entrenadores nunca debe mostrar puntuaciones negativas.
        if (!IsMigrationApplied("coach_scores_non_negative"))
        {
            try
            {
                _db.Execute("UPDATE coach_rankings SET score = 0 WHERE score < 0");
                MarkMigrationApplied("coach_scores_non_negative");
                Debug.Log("[DB] Migration: coach ranking scores clamped to >= 0");
            }
            catch (System.Exception ex)
            {
                throw new DatabaseMigrationException("coach_scores_non_negative", ex);
            }
        }

        // E3: tamaño de mercado (economía). Las partidas antiguas no lo traen: se rellena por
        // reputación (proxy) para que la asistencia/patrocinios no arranquen a cero.
        if (AddColumnIfMissing("teams", "market_size", "market_size INTEGER DEFAULT 0"))
            _db.Execute("UPDATE teams SET market_size = MIN(100, 28 + reputation * 14) WHERE market_size = 0");

        // Add on_trade_block to players if missing
        try
        {
            var playerColsBlock = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
            if (!playerColsBlock.Any(c => c.name == "on_trade_block"))
            {
                _db.Execute("ALTER TABLE players ADD COLUMN on_trade_block INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added on_trade_block to players");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("players_on_trade_block", ex);
        }

        // Los jugadores con rol "Último recurso" pero media digna (74+) reciben su rol por
        // media: el draft no asignaba rol y todos los rookies quedaban en Último recurso
        // (con la tabla nueva eso son 2 minutos de rotación).
        if (!IsMigrationApplied("role_from_overall"))
        {
            var sinRol = _db.Table<PlayerData>()
                            .Where(p => p.role == PlayerRole.UltimoRecurso && p.overall >= 74)
                            .ToList();
            foreach (var p in sinRol)
            {
                p.role = PlayerRoleRules.FromOverall(p.overall);
                _db.Update(p);
            }
            MarkMigrationApplied("role_from_overall");
            Debug.Log($"[DB] Migration: rol por media asignado a {sinRol.Count} jugadores");
        }

        // E2: las lesiones anteriores al parte médico no guardaban la duración total; se
        // rellena con los días restantes para que el progreso arranque en un valor razonable.
        if (!IsMigrationApplied("injury_totals_backfill"))
        {
            _db.Execute("UPDATE players SET injury_total_days = injury_days WHERE injury_days > 0 AND injury_total_days = 0");
            MarkMigrationApplied("injury_totals_backfill");
            Debug.Log("[DB] Migration: duración total de lesiones en curso rellenada");
        }

        // Limpieza one-time: las filas de player_game_stats con 0 minutos (DNPs)
        // inflaban los partidos jugados y diluían las medias. Ya no se persisten
        // (GameSimulator) y aquí se borran las existentes.
        if (!IsMigrationApplied("no_dnp_game_stats"))
        {
            _db.Execute("DELETE FROM player_game_stats WHERE minutes = 0");
            MarkMigrationApplied("no_dnp_game_stats");
            Debug.Log("[DB] Migration: eliminadas las filas de player_game_stats con 0 minutos");
        }

        // SEA-06: el histórico de carrera se agregaba por NOMBRE COMPLETO. Como los pools
        // de nombres incluyen nombres de leyendas, un rookie generado con el mismo nombre
        // heredaba sus totales, inducciones al HOF y dorsales. Ahora la clave es player_id:
        //   -1 = leyenda del seeder, 0 = fila antigua sin dueño, >0 = jugador real.
        try
        {
            AddColumnIfMissing("historical_player_stats", "player_id",
                "player_id INTEGER NOT NULL DEFAULT 0");
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("historical_player_stats_player_id", ex);
        }

        if (!IsMigrationApplied("historical_stats_keyed_by_player_id"))
        {
            // 1) Marcar las filas del seeder como leyendas (intocables).
            int legends = 0;
            foreach (var d in HistoricalPlayerStatsSeeder.Data)
            {
                legends += _db.Execute(
                    "UPDATE historical_player_stats SET player_id = ? WHERE player_id = 0 AND first_name = ? AND last_name = ?",
                    HistoricalStatsRules.LEGEND_ID, d.first, d.last);
            }

            // 2) Adoptar las filas huérfanas cuyo nombre sea de UN único jugador actual
            //    (así las carreras de partidas antiguas no se pierden). Los homónimos se
            //    quedan sin dueño: esa fila es una mezcla y no se puede repartir.
            var orphans = _db.Table<HistoricalPlayerStatsData>().Where(r => r.player_id == 0).ToList();
            var playersByName = _db.Table<PlayerData>().ToList()
                .GroupBy(p => (p.first_name, p.last_name))
                .ToDictionary(g => g.Key, g => g.Select(p => p.id).ToList());
            var adoptionPlan = HistoricalStatsRules.BuildAdoptionPlan(orphans, playersByName);
            int adopted = 0;
            foreach (var r in orphans)
            {
                if (!adoptionPlan.TryGetValue(r.id, out int playerId)) continue;
                r.player_id = playerId;
                _db.Update(r);
                adopted++;
            }

            MarkMigrationApplied("historical_stats_keyed_by_player_id");
            Debug.Log($"[DB] Migration: histórico clave player_id ({legends} leyendas marcadas, {adopted} carreras adoptadas)");
        }

        // Asignar un esquema a los asistentes creados antes de esta feature.
        if (!IsMigrationApplied("coach_schemes_assigned"))
        {
            var coaches = _db.Table<EmployeeData>().Where(e => e.position == CoachScheme.POSITION && e.scheme_set == 0).ToList();
            foreach (var c in coaches)
                CoachScheme.AssignRandomScheme(c, Rng);
            if (coaches.Count > 0) _db.UpdateAll(coaches);
            MarkMigrationApplied("coach_schemes_assigned");
            Debug.Log($"[DB] Migration: esquema asignado a {coaches.Count} asistentes");
        }

        // Reclasificar los registros de préstamo antiguos (TYPE_LOAN): el dinero
        // recibido pasa a ingreso y las devoluciones a gasto con importe positivo.
        if (!IsMigrationApplied("loan_records_reclassified"))
        {
            int loanIn = _db.Execute("UPDATE finance_records SET record_type = ? WHERE record_type = ? AND amount > 0",
                FinanceRecord.TYPE_LOAN_IN, FinanceRecord.TYPE_LOAN);
            int loanRep = _db.Execute("UPDATE finance_records SET record_type = ?, amount = -amount WHERE record_type = ? AND amount < 0",
                FinanceRecord.TYPE_LOAN_REPAY, FinanceRecord.TYPE_LOAN);
            MarkMigrationApplied("loan_records_reclassified");
            Debug.Log($"[DB] Migration: reclasificados {loanIn} préstamos y {loanRep} devoluciones");
        }

        // Add rings to players if missing (championships won by the player)
        try
        {
            var playerColsRings = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
            if (!playerColsRings.Any(c => c.name == "rings"))
            {
                _db.Execute("ALTER TABLE players ADD COLUMN rings INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added rings to players");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("players_rings", ex);
        }

        // Add finals_mvps to players if missing (Bill Russell Finals MVP awards won)
        try
        {
            var playerColsFmVp = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
            if (!playerColsFmVp.Any(c => c.name == "finals_mvps"))
            {
                _db.Execute("ALTER TABLE players ADD COLUMN finals_mvps INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added finals_mvps to players");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("players_finals_mvps", ex);
        }

        // Add finals_played to players if missing (finals games played by the player)
        try
        {
            var playerColsFp = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
            if (!playerColsFp.Any(c => c.name == "finals_played"))
            {
                _db.Execute("ALTER TABLE players ADD COLUMN finals_played INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added finals_played to players");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("players_finals_played", ex);
        }

        // Add season_mvps to players if missing (regular season MVP awards won)
        try
        {
            var playerColsSmv = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
            if (!playerColsSmv.Any(c => c.name == "season_mvps"))
            {
                _db.Execute("ALTER TABLE players ADD COLUMN season_mvps INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added season_mvps to players");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("players_season_mvps", ex);
        }

        // Create player_season_stats table if missing (for career history across seasons)
        try
        {
            _db.Execute("CREATE TABLE IF NOT EXISTS player_season_stats (" +
                "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "player_id INTEGER, " +
                "season_id INTEGER, " +
                "year_start INTEGER, " +
                "year_end INTEGER, " +
                "team_id INTEGER, " +
                "team_abbreviation TEXT, " +
                "team_name TEXT, " +
                "games INTEGER, " +
                "total_minutes REAL, " +
                "total_points INTEGER, " +
                "total_rebounds INTEGER, " +
                "total_assists INTEGER, " +
                "total_steals INTEGER, " +
                "total_blocks INTEGER, " +
                "total_rating INTEGER, " +
                "total_fgm INTEGER, " +
                "total_fga INTEGER, " +
                "total_fg3m INTEGER, " +
                "total_fg3a INTEGER, " +
                "total_ftm INTEGER, " +
                "total_fta INTEGER, " +
                "total_turnovers INTEGER, " +
                "total_oreb INTEGER, " +
                "total_dreb INTEGER" +
            ")");
            _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerSeasonStats_PlayerId ON player_season_stats(player_id)");
            _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerSeasonStats_SeasonId ON player_season_stats(season_id)");

            // F6-01: columnas aditivas para partidas creadas antes de los splits.
            AddColumnIfMissing("player_season_stats", "total_fgm", "total_fgm INTEGER DEFAULT 0");
            AddColumnIfMissing("player_season_stats", "total_fga", "total_fga INTEGER DEFAULT 0");
            AddColumnIfMissing("player_season_stats", "total_fg3m", "total_fg3m INTEGER DEFAULT 0");
            AddColumnIfMissing("player_season_stats", "total_fg3a", "total_fg3a INTEGER DEFAULT 0");
            AddColumnIfMissing("player_season_stats", "total_ftm", "total_ftm INTEGER DEFAULT 0");
            AddColumnIfMissing("player_season_stats", "total_fta", "total_fta INTEGER DEFAULT 0");
            AddColumnIfMissing("player_season_stats", "total_turnovers", "total_turnovers INTEGER DEFAULT 0");
            AddColumnIfMissing("player_season_stats", "total_oreb", "total_oreb INTEGER DEFAULT 0");
            AddColumnIfMissing("player_season_stats", "total_dreb", "total_dreb INTEGER DEFAULT 0");
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("player_season_stats_table", ex);
        }

        // F6-01: backfill de los splits para filas archivadas mientras sus
        // player_game_stats/games todavía existan. Las temporadas ya purgadas
        // no se pueden reconstruir; quedan a 0 y la UI mostrará el dato no disponible.
        if (!IsMigrationApplied("player_season_stats_shooting_splits"))
        {
            try
            {
                BackfillPlayerSeasonShootingSplits();
                MarkMigrationApplied("player_season_stats_shooting_splits");
                Debug.Log("[DB] Migration: shooting splits backfilled in player_season_stats");
            }
            catch (System.Exception ex)
            {
                throw new DatabaseMigrationException("player_season_stats_shooting_splits", ex);
            }
        }

        // Cleanup: el All-Star se guardaba en player_game_stats con ids de equipo
        // centinela (-1/-2), generando filas fantasma (team_name NULL) en el
        // histórico de carrera. Se eliminan de los saves ya archivados.
        if (!IsMigrationApplied("player_season_stats_exclude_allstar"))
        {
            try
            {
                int removed = _db.Execute(
                    "DELETE FROM player_season_stats WHERE team_id <= 0 OR team_name IS NULL");
                MarkMigrationApplied("player_season_stats_exclude_allstar");
                Debug.Log($"[DB] Migration: removed {removed} phantom player_season_stats rows (All-Star)");
            }
            catch (System.Exception ex)
            {
                throw new DatabaseMigrationException("player_season_stats_cleanup", ex);
            }
        }

        // Create monthly_awards table if missing (for monthly awards)
        try
        {
            _db.Execute("CREATE TABLE IF NOT EXISTS monthly_awards (" +
                "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "season_id INTEGER, " +
                "month_name TEXT, " +
                "award_type TEXT, " +
                "rank INTEGER, " +
                "manager_id INTEGER, " +
                "player_id INTEGER, " +
                "team_id INTEGER, " +
                "team_name TEXT, " +
                "player_name TEXT, " +
                "value REAL" +
            ")");
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("monthly_awards_table", ex);
        }

        // Add taxpayer_mid_level to league_settings if missing
        try
        {
            var lsCols = _db.Query<ColumnInfo>("PRAGMA table_info(league_settings)");
            if (!lsCols.Any(c => c.name == "taxpayer_mid_level"))
            {
                _db.Execute("ALTER TABLE league_settings ADD COLUMN taxpayer_mid_level INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added taxpayer_mid_level to league_settings");
            }
            _db.Execute($"UPDATE league_settings SET taxpayer_mid_level = {TradeHelper.T_MLE} WHERE taxpayer_mid_level = 0 OR taxpayer_mid_level IS NULL");
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("league_settings_taxpayer_mid_level", ex);
        }

        // Add two_way_salary to league_settings if missing
        try
        {
            var lsCols2 = _db.Query<ColumnInfo>("PRAGMA table_info(league_settings)");
            if (!lsCols2.Any(c => c.name == "two_way_salary"))
            {
                _db.Execute("ALTER TABLE league_settings ADD COLUMN two_way_salary INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added two_way_salary to league_settings");
            }
            _db.Execute($"UPDATE league_settings SET two_way_salary = {TradeHelper.TWO_WAY_SALARY} WHERE two_way_salary = 0 OR two_way_salary IS NULL");
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("league_settings_two_way_salary", ex);
        }

        // F4-07: temporada y procedencia de las cifras CBA en league_settings.
        try
        {
            var lsCols3 = _db.Query<ColumnInfo>("PRAGMA table_info(league_settings)");
            if (!lsCols3.Any(c => c.name == "season_start_year"))
            {
                _db.Execute("ALTER TABLE league_settings ADD COLUMN season_start_year INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added season_start_year to league_settings");
            }
            if (!lsCols3.Any(c => c.name == "cba_projected"))
            {
                _db.Execute("ALTER TABLE league_settings ADD COLUMN cba_projected INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added cba_projected to league_settings");
            }
            if (!lsCols3.Any(c => c.name == "cba_source"))
            {
                _db.Execute("ALTER TABLE league_settings ADD COLUMN cba_source TEXT DEFAULT ''");
                Debug.Log("[DB] Migration: added cba_source to league_settings");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("league_settings_cba_columns", ex);
        }

        // F4-07: sincroniza una partida existente con la fila CBA de su temporada
        // activa. Las partidas nuevas reciben la tabla desde SeedLeagueSettings.
        if (!IsMigrationApplied("cba_table_by_season"))
        {
            try
            {
                var activeSeason = _db.Table<SeasonData>()
                    .Where(s => s.is_active == 1)
                    .OrderByDescending(s => s.id)
                    .FirstOrDefault();
                var cbaSettings = GetLeagueSettings();
                int cbaYear = activeSeason != null
                    ? activeSeason.year_start
                    : CbaTable.CurrentSeasonStartYear;
                if (cbaSettings != null)
                {
                    CbaTable.ApplyTo(cbaSettings, cbaYear);
                    _db.Update(cbaSettings);
                }
                MarkMigrationApplied("cba_table_by_season");
                Debug.Log($"[DB] Migration: CBA table applied to season {cbaYear}-{cbaYear + 1}");
            }
            catch (System.Exception ex)
            {
                throw new DatabaseMigrationException("cba_table_by_season", ex);
            }
        }

        // F4-08: las partidas creadas antes del mínimo por experiencia guardaban
        // 2 M como `minimum_salary`. Se sincroniza con el mínimo oficial de 0 años.
        if (!IsMigrationApplied("minimum_salary_by_experience"))
        {
            try
            {
                var minSettings = GetLeagueSettings();
                if (minSettings != null)
                {
                    int minYear = minSettings.season_start_year > 0
                        ? minSettings.season_start_year
                        : CbaTable.CurrentSeasonStartYear;
                    minSettings.minimum_salary = CbaTable.For(minYear).MinimumSalary;
                    _db.Update(minSettings);
                }
                MarkMigrationApplied("minimum_salary_by_experience");
            }
            catch (System.Exception ex)
            {
                throw new DatabaseMigrationException("minimum_salary_by_experience", ex);
            }
        }

        // Add is_on_ir / is_two_way / g_league_assigned to players if missing
        try
        {
            var pCols2 = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
            if (!pCols2.Any(c => c.name == "is_on_ir"))
            {
                _db.Execute("ALTER TABLE players ADD COLUMN is_on_ir INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added is_on_ir to players");
            }
            if (!pCols2.Any(c => c.name == "is_two_way"))
            {
                _db.Execute("ALTER TABLE players ADD COLUMN is_two_way INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added is_two_way to players");
            }
            if (!pCols2.Any(c => c.name == "g_league_assigned"))
            {
                _db.Execute("ALTER TABLE players ADD COLUMN g_league_assigned INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added g_league_assigned to players");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("players_ir_two_way_gleague", ex);
        }

        // Add is_two_way to offers if missing
        try
        {
            var oCols = _db.Query<ColumnInfo>("PRAGMA table_info(offers)");
            if (!oCols.Any(c => c.name == "is_two_way"))
            {
                _db.Execute("ALTER TABLE offers ADD COLUMN is_two_way INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added is_two_way to offers");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("offers_is_two_way", ex);
        }

        // Add photo to gleague_players if missing
        try
        {
            var glCols = _db.Query<ColumnInfo>("PRAGMA table_info(gleague_players)");
            if (!glCols.Any(c => c.name == "photo"))
            {
                _db.Execute("ALTER TABLE gleague_players ADD COLUMN photo TEXT DEFAULT ''");
                Debug.Log("[DB] Migration: added photo to gleague_players");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("gleague_players_photo", ex);
        }

        // employees.nationality
        try
        {
            var empCols = _db.Query<ColumnInfo>("PRAGMA table_info(employees)");
            if (!empCols.Any(c => c.name == "nationality"))
            {
                _db.Execute("ALTER TABLE employees ADD COLUMN nationality TEXT DEFAULT ''");
                Debug.Log("[DB] Migration: added nationality to employees");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("employees_nationality", ex);
        }

        // scouted_players (conocimiento persistente de ojeadores)
        try
        {
            _db.Execute(@"
                CREATE TABLE IF NOT EXISTS scouted_players (
                    team_id INTEGER NOT NULL,
                    player_id INTEGER NOT NULL,
                    scouted_day INTEGER DEFAULT 0,
                    PRIMARY KEY (team_id, player_id)
                )");
            Debug.Log("[DB] Migration: ensured scouted_players table");
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("scouted_players_table", ex);
        }

        // Backfill: migrar ojeos ya completados de la tabla `scouts` a `scouted_players`
        // (idempotente gracias a INSERT OR IGNORE + PK compuesta).
        try
        {
            _db.Execute(@"
                INSERT OR IGNORE INTO scouted_players (team_id, player_id, scouted_day)
                SELECT team_id, player_id, end_day FROM scouts WHERE completed = 1");
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("scouted_players_backfill", ex);
        }

        // Add gleague_team_id to gleague_season_stats if missing
        try
        {
            var glStatCols = _db.Query<ColumnInfo>("PRAGMA table_info(gleague_season_stats)");
            if (!glStatCols.Any(c => c.name == "gleague_team_id"))
            {
                _db.Execute("ALTER TABLE gleague_season_stats ADD COLUMN gleague_team_id INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added gleague_team_id to gleague_season_stats");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("gleague_season_stats_team_id", ex);
        }

        // season_attendance_summary (resumen de asistencia por temporada)
        try
        {
            _db.Execute(@"
                CREATE TABLE IF NOT EXISTS season_attendance_summary (
                    season_id INTEGER PRIMARY KEY,
                    manager_id INTEGER NOT NULL,
                    total_attendance INTEGER DEFAULT 0,
                    total_capacity INTEGER DEFAULT 0,
                    occupancy_pct REAL DEFAULT 0
                )");
            Debug.Log("[DB] Migration: ensured season_attendance_summary table");
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("season_attendance_summary", ex);
        }

        // messages.category + news_type (noticias explícitas)
        try
        {
            var msgCols = _db.Query<ColumnInfo>("PRAGMA table_info(messages)");
            if (!msgCols.Any(c => c.name == "category"))
            {
                _db.Execute("ALTER TABLE messages ADD COLUMN category TEXT DEFAULT ''");
                Debug.Log("[DB] Migration: added category to messages");
            }
            if (!msgCols.Any(c => c.name == "news_type"))
            {
                _db.Execute("ALTER TABLE messages ADD COLUMN news_type TEXT DEFAULT ''");
                Debug.Log("[DB] Migration: added news_type to messages");
            }
        }
        catch (System.Exception ex)
        {
            throw new DatabaseMigrationException("messages_category_news_type", ex);
        }
    }

    public bool IsMigrationApplied(string name)
    {
        return _db.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE name = ?", name) > 0;
    }

    public void MarkMigrationApplied(string name)
    {
        _db.Execute("INSERT OR IGNORE INTO schema_migrations (name, applied_at) VALUES (?, ?)",
            name, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        RecordMigrationAttempt(name, "ok", null);
    }

    /// <summary>
    /// Pre-condiciones del rollover de temporada (opciones de jugador, decremento de
    /// contratos del equipo del usuario): se aplican en la UI antes de la transacción
    /// de StartNewSeason. Estos marcadores evitan aplicarlas dos veces si el rollover
    /// falla y el jugador reintenta. `phase`: "opts" | "dec".
    /// </summary>
    public bool IsRolloverPreApplied(int seasonId, string phase) =>
        IsMigrationApplied($"rollover_{phase}_{seasonId}");

    public void MarkRolloverPreApplied(int seasonId, string phase) =>
        MarkMigrationApplied($"rollover_{phase}_{seasonId}");

    class ColumnInfo
    {
        public int cid { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public int notnull { get; set; }
        public string dflt_value { get; set; }
        public int pk { get; set; }
    }

    void SeedStaticDataIfNeeded()
    {
        if (_db.Table<TeamData>().Count() == 0)
            SeedTeams();

        if (_db.Table<LeagueSettingsData>().Count() == 0)
            SeedLeagueSettings();

        if (_db.Table<PlayerData>().Count() == 0)
        {
            SeedPlayers();
            SeedFreeAgents();
        }

        if (_db.Table<SponsorData>().Count() == 0)
            SeedSponsors();

        if (_db.Table<TvChannelData>().Count() == 0)
            SeedTvChannels();
        else
        {
            // Detect old TV data (all initial_income == 0) and re-seed
            var tvChannels = _db.Table<TvChannelData>().ToList();
            if (tvChannels.Count > 0 && tvChannels.All(c => c.initial_income == 0))
            {
                _db.DeleteAll<TvChannelData>();
                SeedTvChannels();
            }
        }

        if (_db.Table<HistoricalRecordData>().Count() == 0)
            SeedHistoricalRecords();

        if (_db.Table<TeamRecordData>().Count() == 0)
            SeedTeamRecords();

        if (_db.Table<HistoricalPlayerStatsData>().Count() == 0)
            SeedHistoricalPlayerStats();
        else
        {
            // Detect old historical stats (all total_turnovers == 0) and re-seed
            var histStats = _db.Table<HistoricalPlayerStatsData>().ToList();
            if (histStats.Count > 0 && histStats.All(s => s.total_turnovers == 0))
            {
                _db.DeleteAll<HistoricalPlayerStatsData>();
                SeedHistoricalPlayerStats();
            }
        }

        if (_db.Table<FinalsRecord>().Count() == 0)
            SeedPalmaresData();

        if (_db.Table<AllStarRecord>().Count() == 0)
            SeedAllStarData();

        if (_db.Table<CoachRankingData>().Count() == 0)
            SeedCoachRankings();

        if (_db.Table<HallOfFameData>().Count() == 0)
            SeedHallOfFame();

        if (_db.Table<RetiredNumberData>().Count() == 0)
        {
            SeedRetiredNumbers();
            SeedVeteranRetiredNumbers();
        }

        EnsureGLeagueSeeded();

        // Los settings económicos también son datos estáticos: cada equipo debe tener
        // precios de entrada/abono desde el inicio de la partida.
        EnsureTeamSettingsForAllTeams();

        SeedActivePlayerCareers();
    }

    public void SeedHallOfFame()
    {
        foreach (var h in HallOfFameSeeder.Data)
        {
            h.player_id = 0;
            _db.Insert(h);
        }
        Debug.Log($"[DB] {HallOfFameSeeder.Data.Count} leyendas insertadas en el Salón de la Fama.");
    }

    public void SeedRetiredNumbers()
    {
        var teams = GetAllTeams().ToDictionary(t => t.abbreviation, t => t.id);
        var hof = HallOfFameSeeder.Data;

        foreach (var (firstName, lastName, abbr, number) in RetiredNumberSeeder.Data)
        {
            // El equipo va explícito en el seeder (no depende del HOF). Del HOF solo se toman
            // los datos de vitrina (posición/títulos/puntos) si la leyenda está registrada.
            var legend = hof.FirstOrDefault(x => x.first_name == firstName && x.last_name == lastName);

            if (!teams.TryGetValue(abbr, out var teamId))
            {
                Debug.LogWarning($"[DB] Equipo {abbr} no encontrado para retirar dorsal {number} de {firstName} {lastName}");
                continue;
            }

            _db.Insert(new RetiredNumberData
            {
                team_id = teamId,
                number = number,
                player_id = 0,
                first_name = firstName,
                last_name = lastName,
                position = legend?.position ?? "",
                rings = legend?.rings ?? 0,
                career_points = legend?.career_points ?? 0,
                induction_season = legend?.induction_season ?? ""
            });
        }
        Debug.Log($"[DB] Dorsales iniciales de leyendas HOF retirados sembrados.");
    }

    public void SeedVeteranRetiredNumbers()
    {
        var teams = GetAllTeams().ToDictionary(t => t.abbreviation, t => t.id);
        var players = _db.Table<PlayerData>().ToList();
        var playerByName = players.ToLookup(p => (p.first_name, p.last_name));

        foreach (var (firstName, lastName, abbr, number) in VeteranRetiredNumberSeeder.Data)
        {
            if (!teams.TryGetValue(abbr, out var teamId))
            {
                Debug.LogWarning($"[DB] Equipo {abbr} no encontrado para dorsal veterano de {firstName} {lastName}");
                continue;
            }

            var match = playerByName[(firstName, lastName)].FirstOrDefault();
            var hist = match != null ? GetHistoricalPlayerStats(match.id) : null;

            _db.Insert(new RetiredNumberData
            {
                team_id = teamId,
                number = number,
                player_id = match?.id ?? 0,
                first_name = firstName,
                last_name = lastName,
                position = match?.position ?? "",
                rings = match?.rings ?? 0,
                career_points = hist?.total_points ?? 0,
                induction_season = ""
            });
        }
        Debug.Log($"[DB] Dorsales de veteranos activos sembrados en franquicias históricas.");
    }

    public List<SeasonData> GetAllSeasons()
    {
        if (!EnsureDb()) return new List<SeasonData>();
        return _db.Table<SeasonData>().ToList();
    }

    public List<HallOfFameData> GetHoFMembers()
    {
        if (!EnsureDb()) return new List<HallOfFameData>();
        return _db.Table<HallOfFameData>().ToList();
    }

    /// <summary>Valida las relaciones declaradas en <see cref="ReferentialIntegrity"/>.</summary>
    public List<ReferentialIntegrity.Orphan> ValidateReferentialIntegrity()
    {
        if (!EnsureDb()) return new List<ReferentialIntegrity.Orphan>();
        return ReferentialIntegrity.Validate(_db);
    }

    public bool EnsureDb()
    {
        if (_db == null)
        {
            Debug.LogError("[DB] No hay base de datos activa. Llama InitSaveSlot() primero.");
            return false;
        }
        return true;
    }


    void OnDestroy()
    {
        _db?.Close();
    }
}
