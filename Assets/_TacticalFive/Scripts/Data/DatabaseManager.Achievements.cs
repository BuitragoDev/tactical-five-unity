using UnityEngine;
using System.Collections.Generic;
using SQLite;
using System.Linq;

public partial class DatabaseManager
{
    // ── GM ACHIEVEMENTS ───────────────────────────────────

    public List<GmAchievementData> GetAchievements(int managerId)
    {
        return _db.Table<GmAchievementData>()
                  .Where(a => a.manager_id == managerId)
                  .OrderByDescending(a => a.id)
                  .ToList();
    }

    public bool IsAchievementUnlocked(int managerId, string type)
    {
        return _db.Table<GmAchievementData>()
                  .Any(a => a.manager_id == managerId && a.type == type);
    }

    /// <summary>Desbloquea un logro de forma idempotente. Devuelve true si es nuevo.</summary>
    public bool UnlockAchievement(int managerId, string type, int? seasonId = null, string seasonLabel = null)
    {
        if (IsAchievementUnlocked(managerId, type)) return false;

        _db.Execute("INSERT OR IGNORE INTO gm_achievements(manager_id, type, season_id, season_label, unlocked_at) VALUES (?, ?, ?, ?, ?)",
            managerId, type, seasonId, seasonLabel, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return IsAchievementUnlocked(managerId, type);
    }

    public int CountAchievements(int managerId)
    {
        return _db.Table<GmAchievementData>().Count(a => a.manager_id == managerId);
    }
}
