using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager
{
    // ── EQUIPOS ────────────────────────────────────────

    public List<TeamData> GetAllTeams()
    {
        if (!EnsureDb()) return new List<TeamData>();
        return _db.Table<TeamData>().ToList();
    }

    public List<TeamData> GetTeamsByConference(string conference)
    {
        if (!EnsureDb()) return new List<TeamData>();
        return _db.Table<TeamData>()
                  .Where(t => t.conference == conference)
                  .ToList();
    }

    public TeamData GetTeamById(int id)
    {
        if (!EnsureDb()) return null;
        return _db.Table<TeamData>()
                  .Where(t => t.id == id)
                  .FirstOrDefault();
    }

    public PlayerData GetPlayerById(int id)
    {
        if (!EnsureDb()) return null;
        return _db.Table<PlayerData>()
                  .Where(p => p.id == id)
                  .FirstOrDefault();
    }

    public TeamSettingsData GetTeamSettings(int teamId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<TeamSettingsData>()
                  .Where(t => t.team_id == teamId)
                  .FirstOrDefault();
    }

    public void UpdateTeamSettings(TeamSettingsData settings)
    {
        if (!EnsureDb()) return;
        _db.Update(settings);
    }

    public void SaveTeamSettings(TeamSettingsData settings)
    {
        if (!EnsureDb()) return;
        if (settings.team_id <= 0) return;
        _db.InsertOrReplace(settings);
    }

    public void UpdateTeamBudget(int teamId, long newBudget)
    {
        if (!EnsureDb()) return;
        var team = GetTeamById(teamId);
        if (team != null)
        {
            team.budget = newBudget;
            _db.Update(team);
        }
    }

    public void UpdateTeam(TeamData team)
    {
        if (!EnsureDb()) return;
        _db.Update(team);
    }

    // ── CHEMISTRY HELPERS ──────────────────────────────────
    public int GetTeamChemistry(int teamId)
    {
        var team = GetTeamById(teamId);
        return team?.team_chemistry ?? 50;
    }

    public void UpdateTeamChemistry(int teamId, int chemistry)
    {
        var team = GetTeamById(teamId);
        if (team == null) return;
        team.team_chemistry = Mathf.Clamp(chemistry, 0, 100);
        UpdateTeam(team);
    }

    public void UpdatePlayerMorale(int playerId, int morale)
    {
        var player = _db.Table<PlayerData>().FirstOrDefault(p => p.id == playerId);
        if (player == null) return;
        player.morale = Mathf.Clamp(morale, 0, 100);
        _db.Update(player);
    }

    public void UpdatePlayerRole(int playerId, PlayerRole role)
    {
        var player = _db.Table<PlayerData>().FirstOrDefault(p => p.id == playerId);
        if (player == null) return;
        player.role = role;
        _db.Update(player);
    }

    public int GetPlayerMorale(int playerId)
    {
        var player = _db.Table<PlayerData>().FirstOrDefault(p => p.id == playerId);
        return player?.morale ?? 50;
    }

    public int CalculateTeamChemistry(int teamId, int currentGameDay)
    {
        var players = GetPlayersByTeam(teamId);
        if (players.Count == 0) return 50;

        int avgMorale = (int)players.Average(p => p.morale);

        // Roster stability: check if any trade involving this team in last 30 days
        int stability = 1;
        int tradeThreshold = currentGameDay - 30;
        var recentTrades = _db.Table<TradeData>()
            .Where(t => (t.team_id_from == teamId || t.team_id_to == teamId)
                     && t.game_day > tradeThreshold)
            .ToList();
        if (recentTrades.Count > 0)
            stability = 0;

        var team = GetTeamById(teamId);
        int facilities = team?.facilities ?? 3;
        int facilitiesBonus = Mathf.RoundToInt((facilities / 5f) * 3);

        return Mathf.Clamp(avgMorale + stability * 2 + facilitiesBonus, 0, 100);
    }

    // ── END CHEMISTRY HELPERS ──────────────────────────────

    // ── TRAINING HELPERS ───────────────────────────────────
    public List<TrainingData> GetTeamTraining(int teamId)
    {
        return _db.Table<TrainingData>()
                  .Where(t => t.team_id == teamId && t.completed == 0)
                  .ToList();
    }

    public TrainingData GetPlayerActiveTraining(int playerId)
    {
        return _db.Table<TrainingData>()
                  .Where(t => t.player_id == playerId && t.completed == 0)
                  .FirstOrDefault();
    }

    public void InsertTraining(TrainingData training)
    {
        _db.Insert(training);
    }

    public void CompleteTraining(int id)
    {
        var t = _db.Table<TrainingData>().FirstOrDefault(x => x.id == id);
        if (t != null)
        {
            t.completed = 1;
            _db.Update(t);
        }
    }

    public void CompleteTrainingAndApply(TrainingData t)
    {
        ApplyTrainingEffect(t);
        t.completed = 1;
        _db.Update(t);
    }

    void ApplyTrainingEffect(TrainingData t)
    {
        var player = GetPlayerById(t.player_id);
        if (player == null) return;

        var prop = typeof(PlayerData).GetProperty(t.attribute);
        if (prop == null) return;

        int val = (int)prop.GetValue(player);
        val = Mathf.Min(val + 2, 99);
        prop.SetValue(player, val);

        // Recalculate overall as average of all attributes, capped by potential
        int sum = player.shooting + player.three_point + player.passing + player.dribbling
                + player.defense + player.rebounding + player.speed + player.athleticism
                + player.iq + player.steals + player.blocks;
        player.overall = (int)System.Math.Round(sum / 11f);
        if (player.overall > player.potential)
            player.overall = player.potential;

        _db.Update(player);
    }
    // ── END TRAINING HELPERS ───────────────────────────────

    // Los 5 peores equipos por overall real (media de jugadores)
    public List<TeamData> GetWorstTeams(int count = 5)
    {
        var all = _db.Table<TeamData>().ToList();
        var teamAvgs = new Dictionary<int, double>();
        foreach (var team in all)
        {
            var players = _db.Table<PlayerData>().Where(p => p.team_id == team.id).ToList();
            teamAvgs[team.id] = players.Count > 0
                ? players.Average(p => (double)p.overall)
                : team.overall;
        }
        all.Sort((a, b) => teamAvgs[a.id].CompareTo(teamAvgs[b.id]));
        return all.GetRange(0, Mathf.Min(count, all.Count));
    }

}
