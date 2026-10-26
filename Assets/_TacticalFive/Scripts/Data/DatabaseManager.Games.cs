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
        // Solo puede haber una temporada activa por manager: desactivar las previas
        // (evita que GetActiveSeason devuelva una fila arbitraria).
        _db.Execute("UPDATE seasons SET is_active = 0 WHERE manager_id = ? AND is_active = 1", managerId);

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

        // Seed draft picks: current draft year (positions) + future years (no position).
        int? prevSeasonId = lastSeason != null ? (int?)lastSeason.id : null;
        SeedDraftPicksMultiYear(season, prevSeasonId);

        return season;
    }

    public SeasonData GetActiveSeason(int managerId)
    {
        var season = _db.Table<SeasonData>()
                .Where(s => s.manager_id == managerId && s.is_active == 1)
                .OrderByDescending(s => s.id)
                .FirstOrDefault();
        EnsureSeasonDate(season, managerId);
        return season;
    }

    /// <summary>Último aviso de fecha desincronizada (evita repetir el log).</summary>
    static string _lastSeasonDateWarning;

    /// <summary>
    /// El calendario avanza un día por día y `game_day` es el índice de día relativo
    /// al 22/10, así que la fecha de la temporada **debe** ser siempre la del día
    /// `current_game_day + 1` (día 8 → 30/10).
    ///
    /// Si la columna se ha quedado desincronizada (partidas viejas, saltos manuales),
    /// se corrige **en memoria**: de lo contrario el "día pendiente" se calcularía
    /// sobre una fecha equivocada y el botón "DÍA DE PARTIDO" del Dashboard (y el
    /// "IR AL PARTIDO" de la previa) no aparecerían. No se escribe aquí: la base de
    /// datos se repara sola en el siguiente commit del día, porque el Dashboard
    /// persiste su objeto ya saneado.
    ///
    /// La pretemporada manda: sus amistosos tienen días negativos y su propia fecha.
    /// </summary>
    void EnsureSeasonDate(SeasonData season, int managerId)
    {
        if (season == null) return;
        if (season.phase == "preseason") return;

        string expected = CalendarHelper.GameDayToDate(season.year_start, season.current_game_day + 1)
                                .ToString("yyyy-MM-dd");
        if (season.current_date == expected) return;

        // Si quedan amistosos de pretemporada sin jugar, su fecha es la buena.
        bool pendingPreseason = _db.Table<GameData>()
                .Any(g => g.manager_id == managerId
                       && g.game_type == "preseason"
                       && g.is_played == 0);
        if (pendingPreseason) return;

        // Aviso una sola vez por inconsistencia (GetActiveSeason se llama mucho).
        string warning = $"[DB] Fecha de temporada desincronizada ({season.current_date} → {expected}); corregida en memoria.";
        if (_lastSeasonDateWarning != warning)
        {
            _lastSeasonDateWarning = warning;
            Debug.LogWarning(warning);
        }
        season.current_date = expected;
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

        // Atómico: borrado + inserción no puede quedar a medias.
        _db.RunInTransaction(() =>
        {
            var existing = _db.Table<GameData>()
                            .Where(g => g.manager_id == managerId
                                    && g.game_type == "preseason")
                            .ToList();
            foreach (var g in existing)
                _db.Delete(g);

            foreach (var g in games)
                _db.Insert(g);
        });
    }

    public List<GameData> GetPreseasonGames(int managerId)
    {
        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.game_type == "preseason")
                .OrderBy(g => g.game_day)
                .ToList();
    }

    /// <summary>Guarda los partidos de liga regular. Devuelve false si no se pudieron
    /// persistir (el llamador NO debe marcar la temporada como generada).</summary>
    public bool SaveRegularSeasonGames(List<GameData> games)
    {
        try
        {
            _db.RunInTransaction(() =>
            {
                foreach (var g in games)
                    _db.Insert(g);
            });
            Debug.Log($"[DB] {games.Count} partidos de liga regular guardados.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DB] Error guardando partidos: {e.Message}");
            return false;
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

    /// <summary>Calendario regular de un equipo, ordenado por jornada (F5-03).</summary>
    public List<GameData> GetTeamSchedule(int managerId, int teamId)
    {
        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.game_type == "regular"
                        && (g.home_team_id == teamId || g.away_team_id == teamId))
                .OrderBy(g => g.game_day)
                .ToList();
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

    public List<GameData> GetStandingsGames(int managerId, int seasonId = 0)
    {
        var q = _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.game_type == "regular"
                        && g.is_played == 1);
        if (seasonId > 0)
            q = q.Where(g => g.season_id == seasonId);
        return q.OrderBy(g => g.game_day)
                .ThenBy(g => g.id)
                .ToList();
    }

    /// <summary>Últimos `limit` mensajes del manager (vista previa del dashboard).</summary>
    public List<MessageData> GetRecentMessages(int managerId, int limit)
    {
        if (!EnsureDb() || limit <= 0) return new List<MessageData>();
        return _db.Query<MessageData>(
            "SELECT * FROM messages WHERE manager_id = ? ORDER BY id DESC LIMIT ?",
            managerId, limit).ToList();
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
        _db.RunInTransaction(() =>
        {
            foreach (var g in games)
                _db.Insert(g);
        });
        Debug.Log($"[DB] {games.Count} partidos Play-In guardados.");
    }

    public void SavePlayoffGames(List<GameData> games)
    {
        if (games.Count == 0) return;
        _db.RunInTransaction(() =>
        {
            foreach (var g in games)
                _db.Insert(g);
        });
        Debug.Log($"[DB] {games.Count} partidos de Playoff guardados.");
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

    // ── SERIES DE PLAYOFFS (rivalidades/dinastías) ──────────────

    /// <summary>
    /// Registra una serie de playoffs al completarse. **Idempotente** por
    /// (manager_id, season_id, label): el bucle diario reprocesa las series ya cerradas.
    /// </summary>
    public void RecordPlayoffSeries(PlayoffSeriesData series)
    {
        if (!EnsureDb() || series == null) return;
        bool exists = _db.Table<PlayoffSeriesData>()
                          .Any(s => s.manager_id == series.manager_id
                                 && s.season_id == series.season_id
                                 && s.label == series.label);
        if (exists) return;
        _db.Insert(series);
    }

    /// <summary>Todas las series resueltas de la partida (cronológico).</summary>
    public List<PlayoffSeriesData> GetPlayoffSeries(int managerId)
    {
        if (!EnsureDb()) return new List<PlayoffSeriesData>();
        return _db.Table<PlayoffSeriesData>()
                  .Where(s => s.manager_id == managerId)
                  .OrderBy(s => s.season_id).ThenBy(s => s.id)
                  .ToList();
    }

    /// <summary>Series resueltas en las que participó un equipo (más recientes primero).</summary>
    public List<PlayoffSeriesData> GetPlayoffSeriesOfTeam(int managerId, int teamId)
    {
        if (!EnsureDb()) return new List<PlayoffSeriesData>();
        return _db.Table<PlayoffSeriesData>()
                  .Where(s => s.manager_id == managerId
                           && (s.high_team_id == teamId || s.low_team_id == teamId))
                  .OrderByDescending(s => s.season_id).ThenByDescending(s => s.id)
                  .ToList();
    }

}
