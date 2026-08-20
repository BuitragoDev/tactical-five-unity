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

    public List<PlayerData> GetAllPlayers()
    {
        if (!EnsureDb()) return new List<PlayerData>();
        return _db.Table<PlayerData>()
                  .OrderByDescending(p => p.overall)
                  .ToList();
    }

    public Dictionary<int, long> GetTeamPayrolls()
    {
        if (!EnsureDb()) return new Dictionary<int, long>();
        return _db.Query<PlayerData>("SELECT team_id, salary FROM players WHERE team_id > 0")
                  .GroupBy(p => p.team_id)
                  .ToDictionary(g => g.Key, g => g.Sum(p => p.salary));
    }

    /// <summary>
    /// Asigna al jugador un dorsal del equipo: conserva el actual si está libre y no
    /// retirado; en caso contrario asigna el menor dorsal libre (1-99), evitando los
    /// retirados y los ya ocupados. Si no encuentra hueco usa 0.
    /// </summary>
    public void AssignJerseyNumber(PlayerData player, int teamId)
    {
        if (player == null) return;
        var used = _db.Table<PlayerData>()
                      .Where(p => p.team_id == teamId && p.id != player.id)
                      .ToList()
                      .Select(p => p.number)
                      .ToHashSet();
        foreach (var r in _db.Table<RetiredNumberData>()
                             .Where(r => r.team_id == teamId)
                             .ToList())
            used.Add(r.number);

        if (player.number > 0 && !used.Contains(player.number))
            return;

        for (int n = 1; n <= 99; n++)
        {
            if (used.Contains(n)) continue;
            player.number = n;
            return;
        }
        player.number = 0;
    }

    public List<RetiredNumberData> GetRetiredNumbers(int teamId)
    {
        if (!EnsureDb()) return new List<RetiredNumberData>();
        var all = _db.Table<RetiredNumberData>()
                     .Where(r => r.team_id == teamId)
                     .OrderBy(r => r.number)
                     .ToList();

        var activeIds = _db.Table<PlayerData>()
                           .Select(p => p.id)
                           .ToHashSet();

        return all.Where(r => r.player_id == 0 || !activeIds.Contains(r.player_id))
                  .ToList();
    }

    public List<PlayerData> GetRetiringPlayers()
    {
        var withTeam = _db.Table<PlayerData>()
            .Where(p => p.team_id != 0 && p.age >= 40)
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
