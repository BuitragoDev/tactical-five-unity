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

    public static string GetSaveDbPath(int slotNumber)
    {
        return Path.Combine(BaseDir, $"save_{slotNumber}.db");
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

        // Sincronizar exists con archivo real y limpiar huérfanos
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
            else if (!slot.exists && fileExists)
            {
                slot.exists = true;
            }
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
        slot.seasonYear = season != null ? $"{season.year_start}-{season.year_end}" : "2025-2026";
        slot.currentGameDay = season?.current_game_day ?? 0;
        slot.gameMode = manager?.game_mode ?? "manager";
        slot.lastPlayedRealDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        // Calcular fecha in-game
        if (season != null && season.current_game_day > 0)
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
