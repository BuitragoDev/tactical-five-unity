using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

public partial class DatabaseManager : MonoBehaviour
{
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

    public string TemplateDbPath =>
        Path.Combine(Application.persistentDataPath, "TacticalFive", "template.db");

    public int ActiveSaveSlot => _activeSaveSlot;

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
        try
        {
            // No cambiar de conexión/partida mientras un lote de fondo sigue escribiendo:
            // lo cancelamos y esperamos a que termine (rollback limpio si aborta).
            CancelBackgroundBatches();
            WaitForBackgroundBatch();

            if (_dbField != null)
            {
                try { _dbField.Close(); } catch { }
                _dbField = null;
            }

            _activeSaveSlot = slotNumber;
            _isTemplateSession = false;
            // Las imágenes personalizadas se guardan por ámbito (save_{n}).
            TeamImages.SetScope($"save_{slotNumber}");

            // Crear directorio de guardados si no existe
            string dir = Path.GetDirectoryName(DbPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _dbField = new SQLiteConnection(DbPath);
            _db.ExecuteScalar<string>("PRAGMA journal_mode=WAL");
            // CreateTables/RunMigrations ya fallan si el archivo está corrupto: es
            // nuestra detección temprana de DB ilegible.
            CreateTables();
            RunMigrations();

            bool hasExistingData = _db.Table<TeamData>().Count() > 0;
            bool isNewGame = !hasExistingData;

            if (isNewGame)
            {
                // Clon + seed en una sola transacción: un fallo no puede dejar la
                // partida a medias (tablas sin jugadores, etc.).
                _db.RunInTransaction(() =>
                {
                    if (File.Exists(TemplateDbPath))
                        CloneFromTemplate();
                    SeedStaticDataIfNeeded();
                });
                MarkMigrationApplied("slot_initialized");
            }
            else if (!IsMigrationApplied("slot_initialized"))
            {
                // Partida anterior al marcador: completar lo que falte y marcarla.
                SeedStaticDataIfNeeded();
                MarkMigrationApplied("slot_initialized");
            }

            // Las partidas existentes (creadas antes de la G-League) también
            // necesitan sus filiales y prospectos.
            EnsureGLeagueSeeded();

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
    public void CloseActiveSlot()
    {
        CancelBackgroundBatches();
        WaitForBackgroundBatch();
        if (_dbField != null)
        {
            try { _dbField.Close(); } catch { }
            _dbField = null;
        }
        _activeSaveSlot = 0;
        _isTemplateSession = false;
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

    /// <summary>Espera (con tope) a que termine un lote de fondo en curso. Evita cambiar
    /// de conexión/partida mientras una tarea sigue escribiendo.</summary>
    public static void WaitForBackgroundBatch(int timeoutMs = 10000)
    {
        var t = _runningBatch;
        if (t == null || t.IsCompleted) return;
        Debug.LogWarning("[DB] Esperando a que termine un lote de fondo antes de continuar...");
        try { t.Wait(timeoutMs); }
        catch (Exception e) { Debug.LogError($"[DB] El lote de fondo terminó con error: {e.Message}"); }
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

    public Task<T> RunInBackground<T>(Func<SQLiteConnection, T> work)
    {
        // Single-flight: si ya hay un lote en curso, esperar a que termine
        // (evita que dos lotes compartan estado/RNG y compitan por el lock de escritura).
        WaitForBackgroundBatch();
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
        WaitForBackgroundBatch();
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
        WaitForBackgroundBatch();
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
        WaitForBackgroundBatch();
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
        if (File.Exists(TemplateDbPath)) return;

        EnsureTemplateDirectory(TemplateDbPath);
        BuildTemplateFile(TemplateDbPath);
        Debug.Log($"[DB] Template database created: {TemplateDbPath}");
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
        var template = new SQLiteConnection(TemplateDbPath);
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
        template.Close();
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
        try { _db.Execute("ALTER TABLE players ADD COLUMN college TEXT DEFAULT ''"); } catch { }
        _db.CreateTable<EmployeeData>();
        _db.CreateTable<ScoutData>();
        _db.CreateTable<PlayerGameStats>();
        _db.CreateTable<FinanceRecord>();
        _db.CreateTable<DeadMoneyData>();
        _db.CreateTable<DraftStateData>();
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
    }

    void RunMigrations()
    {
        // Atómico: si una migración lanza, no queda un esquema a medias. La versión
        // se marca solo tras aplicar todas correctamente.
        _db.RunInTransaction(() => RunMigrationsCore());
        _db.Execute("PRAGMA user_version = " + SCHEMA_VERSION);
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

    void RunMigrationsCore()
    {
        // Registro de migraciones one-time dentro del DB (los flags viven con el slot;
        // borrar el slot reinicia el estado)
        _db.Execute("CREATE TABLE IF NOT EXISTS schema_migrations (name TEXT PRIMARY KEY, applied_at TEXT NOT NULL)");

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
                int sum = p.speed + p.shooting + p.three_point + p.passing + p.dribbling
                        + p.defense + p.rebounding + p.athleticism + p.iq + p.steals + p.blocks;
                int calc = (int)System.Math.Round(sum / 11f);
                if (calc > p.potential)
                    calc = p.potential;
                if (p.overall != calc)
                {
                    p.overall = calc;
                    _db.Update(p);
                }
            }
            MarkMigrationApplied("overall_recalc");
            Debug.Log("[DB] Migration: recalculated overall for all players");
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
            Debug.LogWarning($"[DB] Migration: secondary_position data migration failed: {ex.Message}");
        }

        // Add guaranteed_years/has_team_option/has_player_option to players if missing
        // (cada columna se comprueba por separado: un ALTER fallido no bloquea los demás)
        var playerColsOpt = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool addedGuaranteed = AddColumnIfMissing("players", "guaranteed_years", "guaranteed_years INTEGER DEFAULT 0");
        AddColumnIfMissing("players", "has_team_option", "has_team_option INTEGER DEFAULT 0");
        AddColumnIfMissing("players", "has_player_option", "has_player_option INTEGER DEFAULT 0");
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
            Debug.LogError($"[DB] Migration error for trade_offers: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for draft_picks.draft_year: {ex.Message}");
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
                Debug.LogError($"[DB] Migration error resetting draft_picks: {ex.Message}");
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
                Debug.LogError($"[DB] Migration error for draft_picks_multiyear: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for seasons: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for trades.pick_id: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for first_apron_hard_capped: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for manager career columns: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for fisico: {ex.Message}");
        }

        // Plan de partido en teams (táctica persistente del equipo)
        AddColumnIfMissing("teams", "plan_pace", "plan_pace INTEGER DEFAULT 0");
        AddColumnIfMissing("teams", "plan_offense", "plan_offense INTEGER DEFAULT 0");
        AddColumnIfMissing("teams", "plan_defense", "plan_defense INTEGER DEFAULT 1");

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
            Debug.LogError($"[DB] Migration error for on_trade_block: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for rings: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for finals_mvps: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for finals_played: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for season_mvps: {ex.Message}");
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
                "total_rating INTEGER" +
            ")");
            _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerSeasonStats_PlayerId ON player_season_stats(player_id)");
            _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerSeasonStats_SeasonId ON player_season_stats(season_id)");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Migration error for player_season_stats: {ex.Message}");
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
                Debug.LogError($"[DB] Migration error cleaning player_season_stats: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for monthly_awards: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for taxpayer_mid_level: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for two_way_salary: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for is_on_ir/is_two_way/g_league_assigned: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for offers.is_two_way: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for gleague_players.photo: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for employees.nationality: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for scouted_players: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error backfilling scouted_players: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for gleague_season_stats.gleague_team_id: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for season_attendance_summary: {ex.Message}");
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
            Debug.LogError($"[DB] Migration error for messages.category/news_type: {ex.Message}");
        }
    }

    bool IsMigrationApplied(string name)
    {
        try
        {
            return _db.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE name = ?", name) > 0;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] IsMigrationApplied({name}) error: {ex.Message}");
            return false;
        }
    }

    void MarkMigrationApplied(string name)
    {
        try
        {
            _db.Execute("INSERT OR IGNORE INTO schema_migrations (name, applied_at) VALUES (?, ?)",
                name, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] MarkMigrationApplied({name}) error: {ex.Message}");
        }
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

        foreach (var (firstName, lastName, number) in RetiredNumberSeeder.Data)
        {
            var legend = hof.FirstOrDefault(x => x.first_name == firstName && x.last_name == lastName);
            if (legend == null)
            {
                Debug.LogWarning($"[DB] Leyenda {firstName} {lastName} no encontrada en HallOfFameSeeder");
                continue;
            }

            if (!teams.TryGetValue(legend.team_abbreviation, out var teamId))
            {
                Debug.LogWarning($"[DB] Equipo {legend.team_abbreviation} no encontrado para retirar dorsal {number} de {firstName} {lastName}");
                continue;
            }

            _db.Insert(new RetiredNumberData
            {
                team_id = teamId,
                number = number,
                player_id = 0,
                first_name = firstName,
                last_name = lastName,
                position = legend.position,
                rings = legend.rings,
                career_points = legend.career_points,
                induction_season = legend.induction_season
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
            var hist = match != null ? GetHistoricalPlayerStats(match.first_name, match.last_name) : null;

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
