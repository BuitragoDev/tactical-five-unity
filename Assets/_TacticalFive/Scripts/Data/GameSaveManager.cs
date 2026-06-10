using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public static class GameSaveManager
{
    private static string BaseDir => Path.Combine(Application.persistentDataPath, "TacticalFive", "saves");
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
    /// </summary>
    public static void CleanupOrphanDb(int slotNumber)
    {
        string dbPath = GetSaveDbPath(slotNumber);
        var slot = GetSlot(slotNumber);
        bool hasValidMeta = slot != null && slot.exists;
        if (!hasValidMeta && File.Exists(dbPath))
        {
            try { File.Delete(dbPath); } catch { }
            Debug.Log($"[GameSaveManager] DB huérfana eliminada: slot {slotNumber}");
        }
    }

    public static string GetSaveDbPath(int slotNumber)
    {
        return Path.Combine(BaseDir, $"save_{slotNumber}.db");
    }

    /// <summary>
    /// Elimina todas las DBs huérfanas (archivos .db sin metadatos válidos).
    /// Llámalo al entrar a la pantalla de Cargar Partida.
    /// </summary>
    public static void CleanupAllOrphanDbs()
    {
        if (!Directory.Exists(BaseDir)) return;
        var metaSlots = GetAllSlots();
        var dbFiles = Directory.GetFiles(BaseDir, "save_*.db");
        foreach (var dbPath in dbFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(dbPath); // "save_N"
            if (!fileName.StartsWith("save_")) continue;
            if (!int.TryParse(fileName.Substring(5), out int slotNum)) continue;

            var slot = metaSlots.FirstOrDefault(s => s.slotNumber == slotNum);
            bool hasValidMeta = slot != null && slot.exists && !string.IsNullOrEmpty(slot.managerName);
            if (!hasValidMeta)
            {
                try { File.Delete(dbPath); } catch { }
                Debug.Log($"[GameSaveManager] DB huérfana eliminada: slot {slotNum}");
            }
        }
    }

    public static List<SaveSlotInfo> GetAllSlots()
    {
        EnsureBaseDir();
        var slots = new List<SaveSlotInfo>();

        if (File.Exists(MetaPath))
        {
            try
            {
                string json = File.ReadAllText(MetaPath);
                var meta = JsonUtility.FromJson<SaveMeta>(json);
                if (meta?.slots != null)
                    slots = meta.slots.ToList();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSaveManager] Error leyendo metadatos: {e.Message}");
            }
        }

        // Limpiar metadatos de slots cuyo archivo DB ya no existe
        var result = new List<SaveSlotInfo>();
        foreach (var slot in slots)
        {
            string dbPath = GetSaveDbPath(slot.slotNumber);
            bool fileExists = File.Exists(dbPath);
            if (slot.exists && !fileExists)
            {
                // Metadatos desfasados → limpiar
                slot.exists = false;
            }
            // NOTA: NUNCA forzamos exists=true solo porque haya un archivo .db.
            // Un .db sin metadatos válidos es una partida abandonada (huérfana).
            result.Add(slot);
        }

        return result.OrderBy(s => s.slotNumber).ToList();
    }

    public static SaveSlotInfo GetSlot(int slotNumber)
    {
        return GetAllSlots().FirstOrDefault(s => s.slotNumber == slotNumber);
    }

    public static void SaveSlotInfo(SaveSlotInfo slot)
    {
        EnsureBaseDir();
        var all = GetAllSlots();
        int idx = all.FindIndex(s => s.slotNumber == slot.slotNumber);
        if (idx >= 0) all[idx] = slot;
        else all.Add(slot);

        var meta = new SaveMeta { slots = all.ToArray() };
        string json = JsonUtility.ToJson(meta, true);
        File.WriteAllText(MetaPath, json);
    }

    public static void DeleteSave(int slotNumber)
    {
        string dbPath = GetSaveDbPath(slotNumber);
        if (File.Exists(dbPath))
        {
            try { File.Delete(dbPath); } catch { }
        }

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
