using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager
{
    // ── PLAYERS ───────────────────────────────────────────

    public List<PlayerData> GetFreeAgents()
    {
        if (!EnsureDb()) return new List<PlayerData>();
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id == 0)
                  .OrderByDescending(p => p.overall)
                  .ToList();
    }

    public PlayerData GetPlayer(int id)
    {
        return _db.Table<PlayerData>().FirstOrDefault(p => p.id == id);
    }

    public List<PlayerData> GetPlayersByTeam(int teamId)
    {
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id == teamId)
                  .OrderByDescending(p => p.overall)
                  .ToList();
    }

    public List<PlayerData> GetRetiringPlayers()
    {
        var withTeam = _db.Table<PlayerData>()
            .Where(p => p.team_id != 0 && p.contract_years <= 1 && p.age >= 40)
            .ToList();
        var freeAgents = _db.Table<PlayerData>()
            .Where(p => p.team_id == 0 && p.age >= 40)
            .ToList();
        var result = new List<PlayerData>();
        result.AddRange(withTeam);
        result.AddRange(freeAgents);
        result.Sort((a, b) => b.age.CompareTo(a.age));
        return result;
    }

    public List<PlayerData> GetExpiringPlayers()
    {
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id != 0 && p.contract_years == 1 && p.age < 35)
                  .OrderByDescending(p => p.salary)
                  .ToList();
    }

    public void UpdatePlayer(PlayerData player)
    {
        if (!EnsureDb()) return;
        _db.Update(player);
    }

    public List<PlayerData> GetTopPlayersByStat(int managerId, string stat, int count = 1)
    {
        if (!EnsureDb()) return new List<PlayerData>();
        var season = GetActiveSeason(managerId);
        if (season == null) return new List<PlayerData>();

        string col = stat switch
        {
            "rebounds" => "rebounds",
            "assists" => "assists",
            "steals" => "steals",
            "blocks" => "blocks",
            "minutes" => "minutes",
            "rating" => "rating",
            _ => "points",
        };

        var rows = _db.Query<PlayerStatTotalRow>(
            $@"SELECT ps.player_id, SUM(ps.{col}) AS total
               FROM player_game_stats ps
               JOIN games g ON ps.game_id = g.id
               WHERE g.manager_id = ?
                 AND g.is_played = 1
                 AND g.game_type = 'regular'
               GROUP BY ps.player_id
               ORDER BY total DESC
               LIMIT ?",
            managerId, count);

        var result = new List<PlayerData>();
        foreach (var r in rows)
        {
            var player = _db.Table<PlayerData>().Where(p => p.id == r.player_id).FirstOrDefault();
            if (player != null) result.Add(player);
        }
        return result;
    }

}
