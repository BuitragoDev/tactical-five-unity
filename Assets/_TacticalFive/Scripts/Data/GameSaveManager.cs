using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using SQLite;

public static class GameSaveManager
{
    /// <summary>
    /// Directorio de guardado. En tests se sobrescribe con una carpeta temporal para
    /// poder probar el flujo de slots sin tocar las partidas del jugador.
    /// </summary>
    public static string BaseDirOverride { get; set; }

    private static string BaseDir => string.IsNullOrEmpty(BaseDirOverride)
        ? Path.Combine(Application.persistentDataPath, "TacticalFive", "saves")
        : BaseDirOverride;
    private static string MetaPath => Path.Combine(BaseDir, "saves.json");

    public static int FindNextAvailableSlot()
    {
        var existing = GetAllSlots();
        int slot = 1;
        while (existing.Any(s => s.slotNumber == slot && s.exists))
        {
            slot++;
        }
        return slot;
    }

    /// <summary>
    /// Elimina el archivo DB de un slot si no tiene metadatos válidos (partida huérfana).
    /// Si los metadatos no se pueden leer, NO borra nada (un fallo de lectura no debe
    /// interpretarse como "no hay ninguna partida").
    /// </summary>
    public static void CleanupOrphanDb(int slotNumber)
    {
        if (!TryGetAllSlots(out var metaSlots))
        {
            Debug.LogWarning($"[GameSaveManager] Metadatos ilegibles: se omite la limpieza del slot {slotNumber}");
            return;
        }

        var slot = metaSlots.FirstOrDefault(s => s.slotNumber == slotNumber);
        bool hasValidMeta = slot != null && slot.exists;
        if (!hasValidMeta)
        {
            DeleteDbFiles(slotNumber);
            Debug.Log($"[GameSaveManager] DB huérfana eliminada: slot {slotNumber}");
        }
    }

    /// <summary>Carpeta de partidas exportadas (backup portable en un .db).</summary>
    public static string ExportsDir => Path.Combine(BaseDir, "exports");

    /// <summary>Copia la partida del slot a la carpeta de exportaciones. Devuelve la
    /// ruta del archivo o null si el slot no existe.</summary>
    public static string ExportSlot(int slotNumber, string managerName, string teamName)
    {
        try
        {
            string db = GetSaveDbPath(slotNumber);
            if (!File.Exists(db)) return null;

            // Si es la partida abierta, volcar el WAL antes de copiar para que el
            // respaldo sea autocontenido.
            if (DatabaseManager.Instance != null && DatabaseManager.Instance.ActiveSaveSlot == slotNumber)
            {
                try { DatabaseManager.Instance.Db.ExecuteScalar<string>("PRAGMA wal_checkpoint(TRUNCATE)"); }
                catch { }
            }

            Directory.CreateDirectory(ExportsDir);
            string name = $"liga_{Sanitize(teamName)}_{Sanitize(managerName)}_{System.DateTime.Now:yyyyMMdd_HHmmss}.db";
            string dest = Path.Combine(ExportsDir, name);
            File.Copy(db, dest, true);

            // Sidecars por si el WAL no se pudo truncar.
            foreach (var side in new[] { "-wal", "-shm" })
                if (File.Exists(db + side))
                    File.Copy(db + side, dest + side, true);

            Debug.Log($"[GameSaveManager] Partida exportada: {dest}");
            return dest;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameSaveManager] Error exportando el slot {slotNumber}: {e.Message}");
            return null;
        }
    }

    /// <summary>Datos de una partida exportada, para pintarla con logo y fecha.</summary>
    public class ExportInfo
    {
        public string path;
        public string fileName;
        public string teamName;
        public string teamLogo;
        public string managerName;
        public System.DateTime date;
        public long sizeBytes;
    }

    private class ExportDbRow
    {
        public int team_id { get; set; }
        public string name { get; set; }          // nombre del equipo
        public string logo { get; set; }          // clave del logo
        public string manager_name { get; set; }
    }

    /// <summary>Partidas exportadas con sus datos; el equipo/manager se leen del
    /// propio .db (con la ruta y el nombre de archivo como respaldo).</summary>
    public static List<ExportInfo> ListExportInfos()
    {
        var list = new List<ExportInfo>();
        foreach (var path in ListExports())
        {
            var info = new ExportInfo
            {
                path = path,
                fileName = Path.GetFileName(path),
                date = File.GetLastWriteTime(path),
                sizeBytes = new FileInfo(path).Length
            };

            try
            {
                using var conn = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
                var row = conn.Query<ExportDbRow>(
                    @"SELECT m.team_id AS team_id, t.name AS name, t.logo AS logo, m.name AS manager_name
                      FROM managers m LEFT JOIN teams t ON t.id = m.team_id
                      ORDER BY m.id DESC LIMIT 1").FirstOrDefault();
                if (row != null)
                {
                    info.teamName = row.name;
                    info.teamLogo = row.logo;
                    info.managerName = row.manager_name;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSaveManager] No se pudo leer la cabecera de {info.fileName}: {e.Message}");
            }

            list.Add(info);
        }
        return list;
    }

    /// <summary>Borra una copia exportada (y sus sidecars).</summary>
    public static bool DeleteExport(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            File.Delete(path);
            foreach (var side in new[] { "-wal", "-shm" })
                if (File.Exists(path + side)) File.Delete(path + side);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameSaveManager] No se pudo borrar la copia {path}: {e.Message}");
            return false;
        }
    }

    /// <summary>Lista de partidas exportadas, de la más reciente a la más antigua.</summary>
    public static List<string> ListExports()
    {
        if (!Directory.Exists(ExportsDir)) return new List<string>();
        return Directory.GetFiles(ExportsDir, "liga_*.db")
            .OrderByDescending(File.GetLastWriteTime)
            .ToList();
    }

    /// <summary>Importa un archivo exportado al siguiente slot libre. Devuelve el slot
    /// (o -1 si no se pudo).</summary>
    public static int ImportSave(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return -1;

            EnsureBaseDir();
            int slot = FindNextAvailableSlot();
            string db = GetSaveDbPath(slot);
            File.Copy(filePath, db, true);

            foreach (var side in new[] { "-wal", "-shm" })
            {
                if (File.Exists(db + side)) File.Delete(db + side);
                if (File.Exists(filePath + side)) File.Copy(filePath + side, db + side, true);
            }

            Debug.Log($"[GameSaveManager] Partida importada al slot {slot}: {filePath}");
            return slot;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameSaveManager] Error importando {filePath}: {e.Message}");
            return -1;
        }
    }

    /// <summary>Nombre de archivo seguro (sin rutas ni caracteres raros).</summary>
    private static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "partida";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
            sb.Append(invalid.Contains(c) || c == ' ' ? '_' : c);
        return sb.ToString();
    }

    public static string GetSaveDbPath(int slotNumber)
    {
        return Path.Combine(BaseDir, $"save_{slotNumber}.db");
    }

    /// <summary>Borra la DB de un slot junto con sus sidecars de WAL.</summary>
    private static void DeleteDbFiles(int slotNumber)
    {
        string dbPath = GetSaveDbPath(slotNumber);
        string[] files = { dbPath, dbPath + "-wal", dbPath + "-shm" };
        foreach (var f in files)
        {
            if (!File.Exists(f)) continue;
            try { File.Delete(f); }
            catch (Exception e) { Debug.LogWarning($"[GameSaveManager] No se pudo borrar {f}: {e.Message}"); }
        }
    }

    /// <summary>
    /// Elimina todas las DBs huérfanas (archivos .db sin metadatos válidos).
    /// Llámalo al entrar a la pantalla de Cargar Partida.
    /// </summary>
    public static void CleanupAllOrphanDbs()
    {
        if (!Directory.Exists(BaseDir)) return;

        // Si no se pueden leer los metadatos, no se borra nada.
        if (!TryGetAllSlots(out var metaSlots))
        {
            Debug.LogWarning("[GameSaveManager] Metadatos ilegibles: se omite la limpieza de DBs huérfanas.");
            return;
        }

        var dbFiles = Directory.GetFiles(BaseDir, "save_*.db");
        foreach (var dbPath in dbFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(dbPath); // "save_N"
            if (!fileName.StartsWith("save_")) continue;
            if (!int.TryParse(fileName.Substring(5), out int slotNum)) continue;

            var slot = metaSlots.FirstOrDefault(s => s.slotNumber == slotNum);
            // Un slot es válido si los metadatos (fiables) lo marcan como existente.
            // NO se exige managerName: una partida legítima puede tenerlo vacío.
            bool hasValidMeta = slot != null && slot.exists;
            if (!hasValidMeta)
            {
                DeleteDbFiles(slotNum);
                Debug.Log($"[GameSaveManager] DB huérfana eliminada: slot {slotNum}");
            }
        }
    }

    /// <summary>
    /// Lee los slots desde saves.json.
    /// Devuelve false SOLO si existe un archivo de metadatos pero no se puede leer ni
    /// por sí mismo ni por su backup; en ese caso los llamadores NO deben borrar nada.
    /// </summary>
    public static bool TryGetAllSlots(out List<SaveSlotInfo> slots)
    {
        slots = new List<SaveSlotInfo>();
        EnsureBaseDir();

        string backup = MetaPath + ".bak";

        if (File.Exists(MetaPath))
        {
            if (TryReadMeta(MetaPath, out slots))
            {
                NormalizeSlots(slots);
                return true;
            }

            if (File.Exists(backup) && TryReadMeta(backup, out slots))
            {
                Debug.LogWarning("[GameSaveManager] saves.json ilegible: se usa saves.json.bak");
                NormalizeSlots(slots);
                return true;
            }

            Debug.LogError("[GameSaveManager] saves.json ilegible y sin backup válido. No se borrará ninguna DB.");
            return false;
        }

        // Sin archivo principal: el backup, si existe, es la única fuente.
        if (File.Exists(backup))
        {
            if (TryReadMeta(backup, out slots))
            {
                NormalizeSlots(slots);
                return true;
            }
            Debug.LogError("[GameSaveManager] saves.json.bak ilegible. No se borrará ninguna DB.");
            return false;
        }

        return true; // sin metadata y sin backup: lista vacía válida
    }

    public static List<SaveSlotInfo> GetAllSlots()
    {
        TryGetAllSlots(out var slots);
        return slots.OrderBy(s => s.slotNumber).ToList();
    }

    private static bool TryReadMeta(string path, out List<SaveSlotInfo> slots)
    {
        slots = new List<SaveSlotInfo>();
        try
        {
            string json = File.ReadAllText(path);
            var meta = JsonUtility.FromJson<SaveMeta>(json);
            if (meta?.slots == null) return false;
            slots = meta.slots.ToList();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameSaveManager] Error leyendo metadatos ({path}): {e.Message}");
            return false;
        }
    }

    // Metadatos desfasados: un slot marcado como existente cuyo .db ya no está.
    private static void NormalizeSlots(List<SaveSlotInfo> slots)
    {
        foreach (var slot in slots)
        {
            if (slot.exists && !File.Exists(GetSaveDbPath(slot.slotNumber)))
                slot.exists = false;
        }
    }

    public static SaveSlotInfo GetSlot(int slotNumber)
    {
        return GetAllSlots().FirstOrDefault(s => s.slotNumber == slotNumber);
    }

    public static void SaveSlotInfo(SaveSlotInfo slot)
    {
        EnsureBaseDir();

        List<SaveSlotInfo> all;
        if (!TryGetAllSlots(out all))
        {
            // Metadatos ilegibles: preservamos el archivo dañado y reconstruimos una
            // entrada por cada DB presente para no dejarlas huérfanas.
            PreserveCorruptMeta();
            all = RebuildSlotsFromDisk();
        }

        int idx = all.FindIndex(s => s.slotNumber == slot.slotNumber);
        if (idx >= 0) all[idx] = slot;
        else all.Add(slot);

        var meta = new SaveMeta { slots = all.ToArray() };
        WriteMetaAtomic(JsonUtility.ToJson(meta, true));
    }

    /// <summary>Escritura atómica de saves.json (tmp + replace, conservando .bak).</summary>
    private static void WriteMetaAtomic(string json)
    {
        string tmp = MetaPath + ".tmp";
        try
        {
            File.WriteAllText(tmp, json);
            if (File.Exists(MetaPath))
                File.Replace(tmp, MetaPath, MetaPath + ".bak", true);
            else
                File.Move(tmp, MetaPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameSaveManager] Escritura atómica no disponible ({e.Message}); usando fallback.");
            try
            {
                File.Copy(tmp, MetaPath, true);
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            catch (Exception e2)
            {
                Debug.LogError($"[GameSaveManager] No se pudo guardar saves.json: {e2.Message}");
            }
        }
    }

    /// <summary>Guarda el saves.json ilegible aparte para poder diagnosticarlo.</summary>
    private static void PreserveCorruptMeta()
    {
        try
        {
            string dest = MetaPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            File.Move(MetaPath, dest);
            Debug.LogWarning($"[GameSaveManager] saves.json ilegible preservado como {dest}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameSaveManager] No se pudo preservar saves.json: {e.Message}");
        }
    }

    /// <summary>Entradas mínimas para no perder de vista las DBs que existen en disco.</summary>
    private static List<SaveSlotInfo> RebuildSlotsFromDisk()
    {
        var list = new List<SaveSlotInfo>();
        if (!Directory.Exists(BaseDir)) return list;

        foreach (var dbPath in Directory.GetFiles(BaseDir, "save_*.db"))
        {
            string fileName = Path.GetFileNameWithoutExtension(dbPath);
            if (!fileName.StartsWith("save_")) continue;
            if (!int.TryParse(fileName.Substring(5), out int slotNum)) continue;

            list.Add(new SaveSlotInfo
            {
                slotNumber = slotNum,
                exists = true,
                managerName = "(recuperada)",
                lastPlayedRealDate = File.GetLastWriteTime(dbPath).ToString("dd/MM/yyyy HH:mm")
            });
        }
        Debug.Log($"[GameSaveManager] Metadatos reconstruidos desde disco: {list.Count} partida(s).");
        return list;
    }

    public static void DeleteSave(int slotNumber)
    {
        // Si es la partida cargada, cerramos la conexión antes de borrar el archivo:
        // en Linux un unlink con la conexión abierta deja escrituras huérfanas.
        if (DatabaseManager.Instance != null && DatabaseManager.Instance.ActiveSaveSlot == slotNumber)
        {
            if (!DatabaseManager.Instance.CloseActiveSlot())
            {
                Debug.LogError($"[GameSaveManager] No se borra la partida {slotNumber}: un lote de fondo sigue escribiendo.");
                return;
            }
        }

        DeleteDbFiles(slotNumber);

        string photoDir = Path.Combine(Application.persistentDataPath, "PlayerPhotos", slotNumber.ToString());
        if (Directory.Exists(photoDir))
        {
            try { Directory.Delete(photoDir, true); } catch { }
        }

        // Borrar también las imágenes de equipos personalizadas de esta partida.
        TeamImages.DeleteScope($"save_{slotNumber}");

        var slot = GetSlot(slotNumber);
        if (slot != null)
        {
            slot.exists = false;
            slot.managerName = null;
            slot.teamName = null;
            slot.teamLogo = null;
            slot.seasonYear = null;
            slot.currentDate = null;
            slot.lastPlayedRealDate = null;
            slot.currentGameDay = 0;
            slot.gameMode = null;
            SaveSlotInfo(slot);
        }
    }

    public static void UpdateSlotFromDatabase(int slotNumber)
    {
        var manager = DatabaseManager.Instance.GetActiveManager();
        var team = manager != null ? DatabaseManager.Instance.GetTeamById(manager.team_id) : null;
        var season = manager != null ? DatabaseManager.Instance.GetActiveSeason(manager.id) : null;

        var slot = GetSlot(slotNumber) ?? new SaveSlotInfo { slotNumber = slotNumber };
        slot.exists = true;
        slot.managerName = manager?.name ?? "Manager";
        slot.teamName = team?.name ?? "Sin equipo";
        slot.teamLogo = team?.logo ?? "";
        slot.seasonYear = season != null ? $"{season.year_start}-{season.year_end}" : "2026-2027";
        slot.currentGameDay = season?.current_game_day ?? 0;
        slot.gameMode = manager?.game_mode ?? "manager";
        slot.lastPlayedRealDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        // Calcular fecha in-game
        if (season != null && !string.IsNullOrEmpty(season.current_date))
        {
            slot.currentDate = DateTime.Parse(season.current_date).ToString("dd/MM/yyyy");
        }
        else if (season != null && season.current_game_day > 0)
        {
            var seasonStart = new DateTime(season.year_start, 10, 22);
            var gameDate = seasonStart.AddDays(season.current_game_day - 1);
            slot.currentDate = gameDate.ToString("dd/MM/yyyy");
        }
        else if (season != null)
        {
            var nextGame = DatabaseManager.Instance.GetNextGame(manager.id, team?.id ?? 0);
            slot.currentDate = nextGame != null ? nextGame.game_date : $"01/10/{season.year_start}";
        }
        else
        {
            slot.currentDate = "";
        }

        // ── LoadGame card data ──
        slot.teamId = team?.id ?? 0;
        slot.teamBudget = team?.budget ?? 0;
        slot.teamChemistry = team?.team_chemistry ?? 50;
        slot.managerStatus = manager != null && manager.is_fired == 1 ? "fired" : "active";

        // Payroll: sum of all player salaries on the team
        if (slot.teamId > 0)
        {
            var players = DatabaseManager.Instance.GetPlayersByTeam(slot.teamId);
            slot.teamPayroll = 0;
            foreach (var p in players)
                slot.teamPayroll += p.salary;
        }
        else
            slot.teamPayroll = 0;

        SaveSlotInfo(slot);
    }

    private static void EnsureBaseDir()
    {
        if (!Directory.Exists(BaseDir))
            Directory.CreateDirectory(BaseDir);
    }

    [Serializable]
    private class SaveMeta
    {
        public SaveSlotInfo[] slots;
    }
}
