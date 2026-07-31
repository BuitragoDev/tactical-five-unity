using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager
{
    // ── SEASON ────────────────────────────────────────────
    public SeasonData CreateSeason(int managerId, string gameMode)
    {
        // Find the max year_start from existing seasons, default to 2025
        var lastSeason = _db.Table<SeasonData>()
            .OrderByDescending(s => s.year_start)
            .FirstOrDefault();
        int yearStart = lastSeason != null ? lastSeason.year_start + 1 : 2026;

        var season = new SeasonData
        {
            year_start = yearStart,
            year_end = yearStart + 1,
            is_active = 1,
            current_game_day = 0,
            game_mode = gameMode,
            phase = "preseason",
            manager_id = managerId,
            generated = 0,
            current_date = $"{yearStart}-09-05"
        };
        _db.Insert(season);

        // Seed draft picks for this new season. First-ever season of the manager
        // has no previousSeasonId (falls back to overall+reputation ordering).
        int? prevSeasonId = lastSeason != null ? (int?)lastSeason.id : null;
        SeedDraftPicks(season.id, managerId, prevSeasonId);

        return season;
    }

    public SeasonData GetActiveSeason(int managerId)
    {
        return _db.Table<SeasonData>()
                .Where(s => s.manager_id == managerId && s.is_active == 1)
                .FirstOrDefault();
    }

    public int GetCurrentDay(int managerId)
    {
        var season = GetActiveSeason(managerId);
        if (season == null) return 0;
        return season.current_game_day;
    }

    // ── GAMES ─────────────────────────────────────────────

    public List<GameData> GetAllGames(int managerId)
    {
        if (!EnsureDb()) return new List<GameData>();
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId)
                  .OrderBy(g => g.game_day)
                  .ToList();
    }

    public List<GameData> GetSeasonGames(int managerId, int seasonId)
    {
        if (!EnsureDb()) return new List<GameData>();
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId && g.season_id == seasonId)
                  .OrderBy(g => g.game_day)
                  .ToList();
    }

    public List<GameData> GetUpcomingGames(int managerId, int currentDay)
    {
        if (!EnsureDb()) return new List<GameData>();
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId && g.game_day > currentDay && g.is_played == 0)
                  .OrderBy(g => g.game_day)
                  .ToList();
    }

    public void SavePreseasonGames(List<GameData> games)
    {
        // Borrar amistosos anteriores de este manager/temporada
        if (games.Count == 0) return;
        int managerId = games[0].manager_id;
        var existing = _db.Table<GameData>()
                        .Where(g => g.manager_id == managerId
                                && g.game_type == "preseason")
                        .ToList();
        foreach (var g in existing)
            _db.Delete(g);

        foreach (var g in games)
            _db.Insert(g);
    }

    public List<GameData> GetPreseasonGames(int managerId)
    {
        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.game_type == "preseason")
                .OrderBy(g => g.game_day)
                .ToList();
    }

    public void SaveRegularSeasonGames(List<GameData> games)
    {
        // Insertar en lotes para mejor rendimiento
        _db.BeginTransaction();
        try
        {
            foreach (var g in games)
                _db.Insert(g);
            _db.Commit();
            Debug.Log($"[DB] {games.Count} partidos de liga regular guardados.");
        }
        catch (Exception e)
        {
            _db.Rollback();
            Debug.LogError($"[DB] Error guardando partidos: {e.Message}");
        }
    }

    public GameData GetNextGame(int managerId, int teamId)
    {
        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.is_played == 0
                        && (g.home_team_id == teamId || g.away_team_id == teamId))
                .OrderBy(g => g.game_date)
                .FirstOrDefault();
    }

    public string GetCurrentDateString(int managerId)
    {
        var season = GetActiveSeason(managerId);
        if (season == null) return "";

        if (!string.IsNullOrEmpty(season.current_date))
            return System.DateTime.Parse(season.current_date).ToString("dd/MM/yyyy");

        if (season.current_game_day == 0)
        {
            var firstPre = _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                         && g.game_type == "preseason")
                .OrderByDescending(g => g.game_day)
                .FirstOrDefault();
            if (firstPre != null)
                return System.DateTime.Parse(firstPre.game_date).ToString("dd/MM/yyyy");
            return new System.DateTime(season.year_start, 10, 22).ToString("dd/MM/yyyy");
        }

        if (season.current_game_day < 0)
        {
            var lastGame = _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                         && g.is_played == 1
                         && g.game_day == season.current_game_day)
                .FirstOrDefault();
            if (lastGame != null)
                return System.DateTime.Parse(lastGame.game_date).ToString("dd/MM/yyyy");
        }

        var seasonStart = new System.DateTime(season.year_start, 10, 22);
        return seasonStart.AddDays(season.current_game_day - 1).ToString("dd/MM/yyyy");
    }

    public GameData GetLastPlayedGame(int managerId, int teamId)
    {
        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.is_played == 1
                        && (g.home_team_id == teamId || g.away_team_id == teamId))
                .OrderByDescending(g => g.game_day)
                .FirstOrDefault();
    }

    public List<GameData> GetGamesOnDay(int managerId, int gameDay)
    {
        // Calcular la fecha correspondiente al día
        var season = GetActiveSeason(managerId);
        if (season == null) return new List<GameData>();

        var date = new System.DateTime(season.year_start, 10, 22)
                    .AddDays(gameDay - 1)
                    .ToString("yyyy-MM-dd");

        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.game_date == date
                        && g.is_played == 0
                        && g.game_type == "regular")
                .ToList();
    }

    public List<GameData> GetGamesOnDate(int managerId, string date)
    {
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId
                           && g.game_date == date
                           && g.is_played == 0)
                  .ToList();
    }

    public List<GameData> GetGamesByGameDay(int managerId, int gameDay)
    {
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId
                           && g.game_day == gameDay
                           && g.is_played == 0)
                  .ToList();
    }

    public List<GameData> GetAllGamesByGameDay(int managerId, int gameDay)
    {
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId
                           && g.game_day == gameDay)
                  .ToList();
    }

    public List<GameData> GetStandingsGames(int managerId)
    {
        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.game_type == "regular"
                        && g.is_played == 1)
                .OrderBy(g => g.game_day)
                .ThenBy(g => g.id)
                .ToList();
    }

    public void UpdateSeason(SeasonData season)
    {
        _db.Update(season);
    }

    public void UpdateGame(GameData game)
    {
        _db.Update(game);
    }

    // ── PLAYOFFS ───────────────────────────────────────────

    public void SavePlayInGames(List<GameData> games)
    {
        if (games.Count == 0) return;
        _db.BeginTransaction();
        try
        {
            foreach (var g in games)
                _db.Insert(g);
            _db.Commit();
            Debug.Log($"[DB] {games.Count} partidos Play-In guardados.");
        }
        catch (Exception e)
        {
            _db.Rollback();
            Debug.LogError($"[DB] Error guardando partidos Play-In: {e.Message}");
        }
    }

    public void SavePlayoffGames(List<GameData> games)
    {
        if (games.Count == 0) return;
        _db.BeginTransaction();
        try
        {
            foreach (var g in games)
                _db.Insert(g);
            _db.Commit();
            Debug.Log($"[DB] {games.Count} partidos de Playoff guardados.");
        }
        catch (Exception e)
        {
            _db.Rollback();
            Debug.LogError($"[DB] Error guardando partidos de Playoff: {e.Message}");
        }
    }

    public List<GameData> GetPlayInGames(int managerId)
    {
        if (!EnsureDb()) return new List<GameData>();
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId && g.game_type == "playin")
                  .OrderBy(g => g.game_day)
                  .ToList();
    }

    public List<GameData> GetPlayoffGames(int managerId)
    {
        if (!EnsureDb()) return new List<GameData>();
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId && g.game_type == "playoff")
                  .OrderBy(g => g.game_day)
                  .ToList();
    }

}
