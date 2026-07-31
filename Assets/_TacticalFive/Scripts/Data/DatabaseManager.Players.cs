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

        var allGames = _db.Table<GameData>()
                          .Where(g => g.manager_id == season.manager_id
                                   && g.is_played == 1
                                   && g.game_type == "regular")
                          .ToList();

        var totals = new Dictionary<int, double>();
        foreach (var game in allGames)
        {
            var stats = GetGamePlayerStats(game.id);
            foreach (var s in stats)
            {
                double value;
                switch (stat)
                {
                    case "rebounds": value = s.rebounds; break;
                    case "assists":  value = s.assists;  break;
                    case "steals":   value = s.steals;   break;
                    case "blocks":   value = s.blocks;   break;
                    case "minutes":  value = s.minutes;  break;
                    case "rating":   value = s.rating;   break;
                    default:         value = s.points;   break;
                }
                totals[s.player_id] = totals.GetValueOrDefault(s.player_id, 0) + value;
            }
        }

        var sorted = totals.OrderByDescending(kvp => kvp.Value).Take(count).ToList();
        var result = new List<PlayerData>();
        foreach (var kvp in sorted)
        {
            var player = _db.Table<PlayerData>().Where(p => p.id == kvp.Key).FirstOrDefault();
            if (player != null) result.Add(player);
        }
        return result;
    }

}
