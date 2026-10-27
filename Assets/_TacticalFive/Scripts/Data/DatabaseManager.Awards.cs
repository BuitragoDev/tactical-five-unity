using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager
{
    // ── PALMARES ────────────────────────────────────────

    public List<FinalsRecord> GetFinalsRecords()
    {
        if (!EnsureDb()) return new List<FinalsRecord>();
        return _db.Table<FinalsRecord>().ToList();
    }

    public List<AwardsRecord> GetAwardsRecords()
    {
        if (!EnsureDb()) return new List<AwardsRecord>();
        return _db.Table<AwardsRecord>().ToList();
    }

    public List<QuintetRecord> GetQuintetRecords()
    {
        if (!EnsureDb()) return new List<QuintetRecord>();
        return _db.Table<QuintetRecord>().ToList();
    }

    public List<AllStarRecord> GetAllStarRecords(int managerId)
    {
        if (!EnsureDb()) return new List<AllStarRecord>();
        return _db.Table<AllStarRecord>()
                  .Where(a => a.manager_id == 0 || a.manager_id == managerId)
                  .ToList();
    }

    public void SaveAllStarRecord(AllStarRecord record)
    {
        if (!EnsureDb()) return;
        _db.Insert(record);
    }

    /// <summary>
    /// Guarda la convocatoria del All-Star de la temporada (una vez por temporada, idempotente:
    /// borra la previa de esa temporada). Titulares = primeros 5 de cada conferencia.
    /// </summary>
    public void SaveAllStarRoster(int managerId, int seasonId, List<PlayerData> east, List<PlayerData> west)
    {
        if (!EnsureDb()) return;
        _db.Execute("DELETE FROM all_star_roster WHERE manager_id = ? AND season_id = ?", managerId, seasonId);

        var rows = new List<AllStarRosterData>();
        void AddConference(List<PlayerData> players, string conference)
        {
            if (players == null) return;
            for (int i = 0; i < players.Count; i++)
                rows.Add(new AllStarRosterData
                {
                    manager_id = managerId,
                    season_id = seasonId,
                    conference = conference,
                    player_id = players[i].id,
                    is_starter = i < 5 ? 1 : 0
                });
        }
        AddConference(east, "East");
        AddConference(west, "West");
        if (rows.Count > 0) _db.InsertAll(rows);
    }

    /// <summary>Temporadas en las que un jugador fue convocado al All-Star (para la trayectoria).</summary>
    public List<int> GetAllStarSeasonIds(int playerId)
    {
        if (!EnsureDb() || playerId <= 0) return new List<int>();
        return _db.Query<int>(
            "SELECT DISTINCT season_id FROM all_star_roster WHERE player_id = ? ORDER BY season_id",
            playerId);
    }

    public class AllStarAppearanceEntry
    {
        public int player_id { get; set; }
        public string player_name { get; set; }
        public string team_logo { get; set; }
        public int appearances { get; set; }
    }

    public List<AllStarAppearanceEntry> GetAllStarAppearances(int managerId)
    {
        if (!EnsureDb()) return new List<AllStarAppearanceEntry>();

        var fromGames = _db.Query<AllStarAppearanceEntry>(@"
            SELECT p.id as player_id, p.first_name || ' ' || p.last_name as player_name,
                   t.logo as team_logo, COUNT(DISTINCT r.season_id) as appearances
            FROM all_star_roster r
            JOIN players p ON r.player_id = p.id
            JOIN teams t ON p.team_id = t.id
            WHERE r.manager_id = ?
            GROUP BY r.player_id", managerId);

        var fromSeed = _db.Table<AllStarAppearanceSeed>().ToList();

        var gameById = fromGames.ToDictionary(e => e.player_id);

        var result = new List<AllStarAppearanceEntry>();

        foreach (var s in fromSeed)
        {
            int totalApps = s.appearances;
            string displayName = s.player_name;
            string logo = "";

            if (s.player_id > 0 && gameById.TryGetValue(s.player_id, out var gameStats))
            {
                totalApps += gameStats.appearances;
                displayName = gameStats.player_name;
                logo = gameStats.team_logo ?? "";
                gameById.Remove(s.player_id);
            }
            else if (s.player_id > 0)
            {
                var player = _db.Table<PlayerData>().FirstOrDefault(p => p.id == s.player_id);
                if (player != null && player.team_id > 0)
                {
                    var team = _db.Table<TeamData>().FirstOrDefault(t => t.id == player.team_id);
                    if (team != null)
                        logo = team.logo;
                }
            }

            result.Add(new AllStarAppearanceEntry
            {
                player_name = displayName,
                appearances = totalApps,
                team_logo = logo
            });
        }

        foreach (var kv in gameById)
        {
            result.Add(new AllStarAppearanceEntry
            {
                player_name = kv.Value.player_name,
                appearances = kv.Value.appearances,
                team_logo = kv.Value.team_logo ?? ""
            });
        }

        return result.OrderByDescending(e => e.appearances).ToList();
    }

    // ── FINALS PLAYER STATS ───────────────────────────────

    public void SaveFinalsPlayerStats(FinalsPlayerStatsData stats)
    {
        _db.Insert(stats);
    }

    public List<FinalsPlayerStatsData> GetFinalsPlayerStats(int gameId)
    {
        return _db.Table<FinalsPlayerStatsData>()
                  .Where(s => s.game_id == gameId)
                  .OrderByDescending(s => s.points)
                  .ToList();
    }

    public List<FinalsPlayerStatsData> GetFinalsPlayerStatsByTeam(int gameId, int teamId)
    {
        return _db.Table<FinalsPlayerStatsData>()
                  .Where(s => s.game_id == gameId && s.team_id == teamId)
                  .OrderByDescending(s => s.points)
                  .ToList();
    }

    public FinalsMVPDetails GetFinalsMVPDetails(int seasonId, int managerId)
    {
        if (!EnsureDb()) return null;

        var finalsGames = _db.Table<GameData>()
            .Where(g => g.manager_id == managerId
                     && g.season_id == seasonId
                     && g.series_label == "playoff-r4-finals"
                     && g.is_played == 1)
            .ToList();

        if (finalsGames.Count == 0) return null;

        // Determine champion team
        var winCount = new Dictionary<int, int>();
        foreach (var g in finalsGames)
        {
            int winner = g.home_score >= g.away_score ? g.home_team_id : g.away_team_id;
            winCount[winner] = winCount.GetValueOrDefault(winner, 0) + 1;
        }
        int champId = winCount.OrderByDescending(kv => kv.Value).First().Key;

        // Get all finals player stats for champion team
        var finalsGameIds = finalsGames.Select(g => g.id).ToList();
        var champStats = new List<FinalsPlayerStatsData>();
        if (finalsGameIds.Count > 0)
        {
            champStats = _db.Query<FinalsPlayerStatsData>(
                "SELECT * FROM finals_player_stats WHERE game_id IN (" +
                string.Join(",", finalsGameIds) + ") AND team_id = " + champId);
        }

        if (champStats.Count == 0) return null;

        // Group by player, compute averages, pick best avg rating
        var topPlayer = champStats
            .GroupBy(s => s.player_id)
            .Select(g => new
            {
                PlayerId = g.Key,
                AvgRating = g.Average(s => s.rating),
                AvgPts = g.Average(s => s.points),
                AvgReb = g.Average(s => s.rebounds),
                AvgAst = g.Average(s => s.assists),
                AvgStl = g.Average(s => s.steals),
                AvgBlk = g.Average(s => s.blocks),
                GamesPlayed = g.Count()
            })
            .Where(x => x.GamesPlayed >= 2)
            .OrderByDescending(x => x.AvgRating)
            .FirstOrDefault();

        if (topPlayer == null) return null;

        var player = GetPlayerById(topPlayer.PlayerId);
        if (player == null) return null;

        var champTeam = GetTeamById(champId);

        return new FinalsMVPDetails
        {
            PlayerId = player.id,
            Photo = player.photo,
            PlayerName = $"{player.first_name} {player.last_name}",
            TeamName = champTeam?.name ?? "",
            AvgPts = (float)topPlayer.AvgPts,
            AvgReb = (float)topPlayer.AvgReb,
            AvgAst = (float)topPlayer.AvgAst,
            AvgStl = (float)topPlayer.AvgStl,
            AvgBlk = (float)topPlayer.AvgBlk
        };
    }

    // ── PLAYER AWARDS ────────────────────────────────────

    public PlayerAwardInfo GetRegularSeasonMVP(int seasonId, int managerId)
    {
        return QueryTopPlayer(seasonId, managerId, null, 65);
    }

    public PlayerAwardInfo GetRookieOfYear(int seasonId, int managerId)
    {
        return QueryTopPlayer(seasonId, managerId, true, 40);
    }

    public List<PlayerAwardInfo> GetAllStarTeam(int seasonId, int managerId)
    {
        return GetBestPerPosition(seasonId, managerId, null, 65);
    }

    public List<PlayerAwardInfo> GetAllRookieTeam(int seasonId, int managerId)
    {
        return GetBestPerPosition(seasonId, managerId, true, 40);
    }

    /// <summary>
    /// Los dos mejores quintetos de la temporada (informativo: **no se persiste** en
    /// `quintet_records` ni afecta a las apariciones de Palmarés).
    /// </summary>
    public List<List<PlayerAwardInfo>> GetAllStarTeams(int seasonId, int managerId, int teams = QuintetPicker.Teams)
    {
        // allowFallback: ningún puesto se queda vacío aunque no haya nadie con 65 partidos.
        return GetBestPerPositionTeams(seasonId, managerId, null, 65, teams, allowFallback: true);
    }

    /// <summary>Los dos mejores quintetos de rookies (informativo, no se persiste).</summary>
    public List<List<PlayerAwardInfo>> GetAllRookieTeams(int seasonId, int managerId, int teams = QuintetPicker.Teams)
    {
        // allowFallback: ningún puesto se queda vacío aunque no haya ningún rookie con 40
        // partidos en esa posición (p. ej. solo hay 6 pivots rookie en la liga).
        return GetBestPerPositionTeams(seasonId, managerId, true, 40, teams, allowFallback: true);
    }

    PlayerAwardInfo QueryTopPlayer(int seasonId, int managerId, bool? rookieOnly, int minGames)
    {
        if (!EnsureDb()) return null;
        string rookieFilter = rookieOnly == true ? "AND p.is_rookie = 1" : "";
        string sql = $@"
            SELECT p.id, p.photo, p.first_name, p.last_name, p.position, t.name AS team_name, t.logo AS team_logo,
                   COUNT(*) AS games,
                   AVG(ps.points) AS avg_pts,
                   AVG(ps.rebounds) AS avg_reb,
                   AVG(ps.assists) AS avg_ast,
                   AVG(ps.rating) AS avg_rating
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN players p ON ps.player_id = p.id
            JOIN teams t ON p.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.manager_id = ? {rookieFilter}
            GROUP BY ps.player_id
            HAVING games >= ?
            ORDER BY avg_rating DESC
            LIMIT 1";
        var row = _db.Query<PlayerAwardQueryRow>(sql, seasonId, managerId, minGames).FirstOrDefault();
        if (row == null) return null;
        return new PlayerAwardInfo
        {
            PlayerId = row.id,
            Photo = row.photo ?? "",
            PlayerName = $"{row.first_name} {row.last_name}",
            TeamName = row.team_name ?? "",
            TeamKeyword = row.team_logo ?? "",
            Position = row.position ?? "",
            AvgPts = (float)row.avg_pts,
            AvgReb = (float)row.avg_reb,
            AvgAst = (float)row.avg_ast,
            AvgRating = (float)row.avg_rating
        };
    }

    List<PlayerAwardInfo> GetBestPerPosition(int seasonId, int managerId, bool? rookieOnly, int minGames)
    {
        var teams = GetBestPerPositionTeams(seasonId, managerId, rookieOnly, minGames, 1);
        return teams.Count > 0 ? teams[0] : new List<PlayerAwardInfo>();
    }

    /// <summary>
    /// Mejores quintetos de la temporada (1º, 2º…): mismos mínimos de partidos que el 1º,
    /// sin repetir jugador y sin persistir nada. La selección vive en `QuintetPicker` (puro).
    ///
    /// Con `allowFallback`, si ningún candidato que llega al mínimo de partidos cubre una
    /// posición, el puesto se rellena con el mejor disponible de esa posición (con al menos
    /// un partido jugado) para que no quede ningún hueco en el quinteto.
    /// </summary>
    List<List<PlayerAwardInfo>> GetBestPerPositionTeams(int seasonId, int managerId, bool? rookieOnly,
                                                        int minGames, int teams, bool allowFallback = false)
    {
        if (!EnsureDb()) return new List<List<PlayerAwardInfo>>();
        string rookieFilter = rookieOnly == true ? "AND p.is_rookie = 1" : "";
        string sql = $@"
            SELECT p.id, p.photo, p.first_name, p.last_name, p.position, p.secondary_position,
                   t.name AS team_name, t.logo AS team_logo,
                   COUNT(*) AS games,
                   AVG(ps.points) AS avg_pts, AVG(ps.rebounds) AS avg_reb,
                   AVG(ps.assists) AS avg_ast, AVG(ps.rating) AS avg_rating
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN players p ON ps.player_id = p.id
            JOIN teams t ON p.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.manager_id = ? {rookieFilter}
            GROUP BY ps.player_id
            ORDER BY avg_rating DESC";
        var allRows = _db.Query<PlayerAwardQueryRow>(sql, seasonId, managerId);

        // El mínimo de partidos se aplica aquí (no en SQL) para poder conservar como
        // respaldo a los que no lo alcanzan: los que cumplen el mínimo rellenan los
        // puestos; los demás solo entran si queda algún puesto vacío.
        var qualified = new List<QuintetCandidate>();
        var backups = new List<QuintetCandidate>();
        foreach (var r in allRows)
        {
            var candidate = new QuintetCandidate
            {
                PlayerId = r.id,
                Photo = r.photo ?? "",
                PlayerName = $"{r.first_name} {r.last_name}",
                Position = r.position,
                SecondaryPosition = r.secondary_position,
                TeamName = r.team_name ?? "",
                TeamLogo = r.team_logo ?? "",
                AvgPts = (float)r.avg_pts,
                AvgReb = (float)r.avg_reb,
                AvgAst = (float)r.avg_ast,
                AvgRating = (float)r.avg_rating
            };
            if (r.games >= minGames) qualified.Add(candidate);
            else if (r.games >= 1) backups.Add(candidate);
        }

        // Selección pura (1º y 2º quinteto, sin repetir jugador).
        var picked = QuintetPicker.Pick(qualified, teams, allowFallback ? backups : null);

        var result = new List<List<PlayerAwardInfo>>();
        foreach (var team in picked)
        {
            var quintet = new List<PlayerAwardInfo>();
            foreach (var slot in team)
            {
                quintet.Add(new PlayerAwardInfo
                {
                    PlayerId = slot.player.PlayerId,
                    Photo = slot.player.Photo,
                    PlayerName = slot.player.PlayerName,
                    TeamName = slot.player.TeamName,
                    TeamKeyword = slot.player.TeamLogo,
                    Position = slot.position,
                    AvgPts = slot.player.AvgPts,
                    AvgReb = slot.player.AvgReb,
                    AvgAst = slot.player.AvgAst,
                    AvgRating = slot.player.AvgRating
                });
            }
            result.Add(quintet);
        }
        return result;
    }

    public PlayerAwardInfo GetBestDefensivePlayer(int seasonId, int managerId)
    {
        if (!EnsureDb()) return null;
        var row = _db.Query<PlayerAwardQueryRow>(@"
            SELECT p.id, p.photo, p.first_name, p.last_name, p.position, t.name AS team_name, t.logo AS team_logo,
                   COUNT(*) AS games,
                   AVG(ps.rating) AS avg_rating,
                   AVG(ps.steals) AS avg_pts,
                   AVG(ps.blocks) AS avg_reb,
                   0 AS avg_ast
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN players p ON ps.player_id = p.id
            JOIN teams t ON p.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.manager_id = ?
            GROUP BY ps.player_id
            HAVING games >= 55
            ORDER BY (AVG(ps.steals) + AVG(ps.blocks)) DESC
            LIMIT 1", seasonId, managerId).FirstOrDefault();
        if (row == null) return null;
        return new PlayerAwardInfo
        {
            PlayerId = row.id,
            Photo = row.photo ?? "",
            PlayerName = $"{row.first_name} {row.last_name}",
            TeamName = row.team_name ?? "",
            TeamKeyword = row.team_logo ?? "",
            Position = row.position ?? "",
            AvgPts = (float)row.avg_pts,
            AvgReb = (float)row.avg_reb,
            AvgAst = 0
        };
    }

    public PlayerAwardInfo GetSixthMan(int seasonId, int managerId)
    {
        if (!EnsureDb()) return null;
        string sql = $@"
            SELECT p.id, p.photo, p.first_name, p.last_name, p.position, t.name AS team_name, t.logo AS team_logo,
                   COUNT(*) AS games,
                   AVG(ps.points) AS avg_pts,
                   AVG(ps.rebounds) AS avg_reb,
                   AVG(ps.assists) AS avg_ast,
                   AVG(ps.rating) AS avg_rating
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN players p ON ps.player_id = p.id
            JOIN teams t ON p.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.manager_id = ?
            GROUP BY ps.player_id
            HAVING COUNT(*) >= 40 AND CAST(SUM(ps.minutes) AS REAL) / COUNT(*) < 28
            ORDER BY avg_rating DESC
            LIMIT 1";
        var row = _db.Query<PlayerAwardQueryRow>(sql, seasonId, managerId).FirstOrDefault();
        if (row == null) return null;
        return new PlayerAwardInfo
        {
            PlayerId = row.id,
            Photo = row.photo ?? "",
            PlayerName = $"{row.first_name} {row.last_name}",
            TeamName = row.team_name ?? "",
            TeamKeyword = row.team_logo ?? "",
            Position = row.position ?? "",
            AvgPts = (float)row.avg_pts,
            AvgReb = (float)row.avg_reb,
            AvgAst = (float)row.avg_ast
        };
    }

    public PlayerAwardInfo GetMostImprovedPlayer(int seasonId, int managerId)
    {
        if (!EnsureDb()) return null;

        var previousSeason = _db.Table<SeasonData>()
            .Where(s => s.manager_id == managerId && s.id < seasonId)
            .OrderByDescending(s => s.id)
            .FirstOrDefault();
        if (previousSeason == null) return null;

        int prevSeasonId = previousSeason.id;
        var curRatings = GetSeasonPlayerRatings(seasonId, managerId);
        var prevRatings = GetSeasonPlayerRatings(prevSeasonId, managerId);
        if (prevRatings.Count == 0) return null;

        PlayerAwardInfo best = null;
        float bestDelta = -999f;
        foreach (var cur in curRatings.Values)
        {
            if (!prevRatings.TryGetValue(cur.player_id, out var prev)) continue;
            if (prev.games < 40 || cur.games < 40) continue;
            float delta = (float)cur.avg_rating - (float)prev.avg_rating;
            var player = GetPlayerById(cur.player_id);
            if (player == null) continue;
            if (delta <= bestDelta) continue;
            bestDelta = delta;
            var team = GetTeamById(player.team_id);
            best = new PlayerAwardInfo
            {
                PlayerId = player.id,
                Photo = player.photo ?? "",
                PlayerName = $"{player.first_name} {player.last_name}",
                TeamName = team?.name ?? "",
                TeamKeyword = team?.logo ?? "",
                Position = player.position ?? "",
                AvgPts = (float)cur.avg_rating,
                AvgReb = (float)prev.avg_rating,
                AvgAst = bestDelta
            };
        }
        return best;
    }

    Dictionary<int, SeasonPlayerRatingRow> GetSeasonPlayerRatings(int seasonId, int managerId)
    {
        var result = new Dictionary<int, SeasonPlayerRatingRow>();
        if (!EnsureDb() || seasonId <= 0) return result;

        // La temporada activa todavía vive en player_game_stats.
        var rows = _db.Query<SeasonPlayerRatingRow>(@"
            SELECT ps.player_id, AVG(ps.rating) AS avg_rating, COUNT(*) AS games
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.manager_id = ?
            GROUP BY ps.player_id", seasonId, managerId);
        foreach (var r in rows) result[r.player_id] = r;

        if (result.Count > 0) return result;

        // Tras el rollover, player_game_stats y games se limpian. La copia
        // agregada por temporada es la fuente histórica equivalente.
        var archivedRows = _db.Query<SeasonPlayerRatingRow>(@"
            SELECT player_id,
                   CAST(total_rating AS REAL) / NULLIF(games, 0) AS avg_rating,
                   games
            FROM player_season_stats
            WHERE season_id = ? AND games > 0", seasonId);
        foreach (var r in archivedRows) result[r.player_id] = r;

        return result;
    }

    public CoachAwardInfo GetCoachOfTheYear(int seasonId)
    {
        if (!EnsureDb()) return null;

        // Premio basado en el récord de temporada: más victorias en la fase regular.
        var winRows = _db.Query<MonthlyManagerAwardRow>(@"
            SELECT t.id AS team_id, t.name AS team_name,
                   SUM(CASE WHEN (g.home_team_id = t.id AND g.home_score > g.away_score)
                             OR (g.away_team_id = t.id AND g.away_score > g.home_score) THEN 1 ELSE 0 END) AS wins,
                   COUNT(*) AS games
            FROM games g
            JOIN teams t ON t.id IN (g.home_team_id, g.away_team_id)
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
            GROUP BY t.id", seasonId);

        // Desempate: nº de premios de Entrenador del Mes (rank 1) por equipo.
        var monthRows = _db.Query<CoachMonthRow>(@"
            SELECT team_id, COUNT(*) AS mes_count
            FROM monthly_awards
            WHERE season_id = ? AND award_type = 'manager' AND rank = 1
            GROUP BY team_id", seasonId);
        var monthByTeam = new Dictionary<int, int>();
        foreach (var r in monthRows) monthByTeam[r.team_id] = r.mes_count;

        // Si aun así hay empate (mismas victorias y mismos meses), LINQ es estable
        // y FirstOrDefault devuelve uno arbitrario.
        var top = winRows
            .OrderByDescending(r => r.wins)
            .ThenByDescending(r => monthByTeam.TryGetValue(r.team_id, out int m) ? m : 0)
            .FirstOrDefault();
        if (top == null || top.team_id == 0) return null;

        var coach = _db.Table<CoachRankingData>()
            .FirstOrDefault(c => c.team_id == top.team_id && (c.status == "active" || c.status == "player"));
        if (coach == null)
            coach = _db.Table<CoachRankingData>().FirstOrDefault(c => c.team_id == top.team_id);

        var team = GetTeamById(top.team_id);
        int recordWins = top.wins;
        int recordLosses = top.games - top.wins;
        return new CoachAwardInfo
        {
            CoachName = coach?.name ?? "",
            TeamName = team?.name ?? "",
            TeamKeyword = team?.logo ?? "",
            RecordText = $"{recordWins}-{recordLosses}"
        };
    }

    // ── RECORDS TRACKING ──────────────────────────────────

    public List<HistoricalRecordData> GetAllHistoricalRecords()
    {
        if (!EnsureDb()) return new List<HistoricalRecordData>();
        return _db.Table<HistoricalRecordData>().ToList();
    }

    public List<TeamRecordData> GetTeamRecords(int teamId)
    {
        if (!EnsureDb()) return new List<TeamRecordData>();
        return _db.Table<TeamRecordData>()
                  .Where(r => r.team_id == teamId)
                  .ToList();
    }

    public List<SeasonRecord> GetAllSeasonRecords(int seasonId)
    {
        if (!EnsureDb()) return new List<SeasonRecord>();
        return _db.Table<SeasonRecord>()
                  .Where(r => r.season_id == seasonId)
                  .ToList();
    }

    /// <summary>Todos los season_records de la partida (para dinastías/prestigio histórico).</summary>
    public List<SeasonRecord> GetAllSeasonRecords()
    {
        if (!EnsureDb()) return new List<SeasonRecord>();
        return _db.Table<SeasonRecord>()
                  .OrderBy(r => r.season_id)
                  .ToList();
    }

    public HistoricalRecordData GetHistoricalRecord(string statType)
    {
        if (!EnsureDb()) return null;
        return _db.Table<HistoricalRecordData>()
                  .Where(r => r.stat_type == statType)
                  .FirstOrDefault();
    }

    public TeamRecordData GetTeamRecord(int teamId, string statType)
    {
        if (!EnsureDb()) return null;
        return _db.Table<TeamRecordData>()
                  .Where(r => r.team_id == teamId && r.stat_type == statType)
                  .FirstOrDefault();
    }

    public List<SeasonGameRecordData> GetCurrentSeasonRecords(int seasonId)
    {
        if (!EnsureDb()) return new List<SeasonGameRecordData>();
        var all = _db.Table<SeasonGameRecordData>()
                     .Where(r => r.season_id == seasonId)
                     .ToList();
        // Pick highest value per stat_type
        var result = new List<SeasonGameRecordData>();
        var seen = new HashSet<string>();
        foreach (var r in all.OrderByDescending(r => r.value))
        {
            if (seen.Add(r.stat_type))
                result.Add(r);
        }
        return result;
    }

    public SeasonGameRecordData GetSeasonGameRecord(int teamId, int seasonId, string statType)
    {
        if (!EnsureDb()) return null;
        return _db.Table<SeasonGameRecordData>()
                  .Where(r => r.team_id == teamId && r.season_id == seasonId && r.stat_type == statType)
                  .FirstOrDefault();
    }

    public List<MonthlyAwardData> EvaluateMonthlyAwards(int seasonId, string monthName,
        string startDate, string endDate, int managerId, int myTeamId)
    {
        if (!EnsureDb()) return new List<MonthlyAwardData>();

        Debug.Log($"[MonthlyAwards] Evaluando {monthName}: {startDate} → {endDate} (season {seasonId})");

        _db.Execute("DELETE FROM monthly_awards WHERE season_id = ? AND month_name = ?", seasonId, monthName);

        var results = new List<MonthlyAwardData>();

        // ── Manager of the Month ──
        var managerAwards = _db.Query<MonthlyManagerAwardRow>(@"
            SELECT t.id AS team_id, t.name AS team_name,
                   SUM(CASE WHEN (g.home_team_id = t.id AND g.home_score > g.away_score)
                             OR (g.away_team_id = t.id AND g.away_score > g.home_score) THEN 1 ELSE 0 END) AS wins,
                   COUNT(*) AS games,
                   SUM(CASE WHEN g.home_team_id = t.id THEN g.home_score - g.away_score
                            ELSE g.away_score - g.home_score END) AS diff
            FROM games g
            JOIN teams t ON t.id IN (g.home_team_id, g.away_team_id)
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.game_date >= ? AND g.game_date <= ?
            GROUP BY t.id
            HAVING games >= 5
            ORDER BY CAST(wins AS REAL) / games DESC, diff DESC
            LIMIT 3",
            seasonId, startDate, endDate);

        for (int i = 0; i < managerAwards.Count; i++)
        {
            var m = managerAwards[i];
            int? mgrId = m.team_id == myTeamId ? (int?)managerId : null;
            float winPct = m.games > 0 ? (float)m.wins / m.games : 0f;
            results.Add(new MonthlyAwardData
            {
                season_id = seasonId,
                month_name = monthName,
                award_type = "manager",
                rank = i + 1,
                manager_id = mgrId,
                team_id = m.team_id,
                team_name = m.team_name,
                player_name = m.team_name,
                value = winPct
            });
        }

        // ── Player of the Month ──
        var playerAwards = _db.Query<MonthlyPlayerAwardRow>(@"
            SELECT ps.player_id, p.first_name || ' ' || p.last_name AS player_name,
                   p.team_id, t.name AS team_name,
                   AVG(ps.rating) AS avg_rating, COUNT(*) AS games
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN players p ON ps.player_id = p.id
            LEFT JOIN teams t ON p.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.game_date >= ? AND g.game_date <= ?
            GROUP BY ps.player_id
            HAVING games >= 5
            ORDER BY avg_rating DESC
            LIMIT 3",
            seasonId, startDate, endDate);

        for (int i = 0; i < playerAwards.Count; i++)
        {
            var p = playerAwards[i];
            results.Add(new MonthlyAwardData
            {
                season_id = seasonId,
                month_name = monthName,
                award_type = "player",
                rank = i + 1,
                player_id = p.player_id,
                player_name = p.player_name,
                team_id = p.team_id,
                team_name = p.team_name,
                value = (float)p.avg_rating
            });
        }

        // ── Rookie of the Month ──
        var rookieAwards = _db.Query<MonthlyPlayerAwardRow>(@"
            SELECT ps.player_id, p.first_name || ' ' || p.last_name AS player_name,
                   p.team_id, t.name AS team_name,
                   AVG(ps.rating) AS avg_rating, COUNT(*) AS games
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN players p ON ps.player_id = p.id AND p.is_rookie = 1
            LEFT JOIN teams t ON p.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.game_date >= ? AND g.game_date <= ?
            GROUP BY ps.player_id
            ORDER BY avg_rating DESC
            LIMIT 3",
            seasonId, startDate, endDate);

        for (int i = 0; i < rookieAwards.Count; i++)
        {
            var r = rookieAwards[i];
            results.Add(new MonthlyAwardData
            {
                season_id = seasonId,
                month_name = monthName,
                award_type = "rookie",
                rank = i + 1,
                player_id = r.player_id,
                player_name = r.player_name,
                team_id = r.team_id,
                team_name = r.team_name,
                value = (float)r.avg_rating
            });
        }

        Debug.Log($"[MonthlyAwards] {monthName}: {managerAwards.Count} managers, {playerAwards.Count} players, {rookieAwards.Count} rookies → {results.Count} total");

        // Persist all
        foreach (var award in results)
            _db.Insert(award);

        // Manager of the Month rank 1 gets +10 coach ranking points
        var topManager = results.FirstOrDefault(r => r.award_type == "manager" && r.rank == 1 && r.team_id.HasValue);
        if (topManager != null)
        {
            var coach = _db.Table<CoachRankingData>().FirstOrDefault(c => c.team_id == topManager.team_id.Value);
            if (coach != null && coach.status != "historical")
            {
                coach.score += 10;
                _db.Update(coach);
            }
        }

        return results;
    }

    public List<MonthlyAwardData> GetMonthlyAwardsForSeason(int seasonId)
    {
        if (!EnsureDb()) return new List<MonthlyAwardData>();
        return _db.Query<MonthlyAwardData>(@"
            SELECT * FROM monthly_awards
            WHERE season_id = ?
            ORDER BY
                CASE month_name
                    WHEN 'noviembre' THEN 0
                    WHEN 'diciembre' THEN 1
                    WHEN 'enero' THEN 2
                    WHEN 'febrero' THEN 3
                    WHEN 'marzo' THEN 4
                    ELSE 5
                END,
                CASE award_type
                    WHEN 'manager' THEN 0
                    WHEN 'player' THEN 1
                    ELSE 2
                END,
                rank", seasonId).ToList();
    }

    public int CountManagerOfTheMonthWins(int managerId)
    {
        if (!EnsureDb()) return 0;
        return _db.Table<MonthlyAwardData>()
                  .Count(a => a.manager_id == managerId && a.award_type == "manager" && a.rank == 1);
    }

    public int CountPlayerOfTheMonthWins(int playerId)
    {
        if (!EnsureDb()) return 0;
        return _db.Table<MonthlyAwardData>()
                  .Count(a => a.player_id == playerId && a.award_type == "player" && a.rank == 1);
    }

    public int CountRookieOfTheMonthWins(int playerId)
    {
        if (!EnsureDb()) return 0;
        return _db.Table<MonthlyAwardData>()
                  .Count(a => a.player_id == playerId && a.award_type == "rookie" && a.rank == 1);
    }

    public void CheckAndUpdateRecords(GameData game, List<GameSimulator.PlayerStatSnapshot> playerStats, int teamId)
    {
        var team = GetTeamById(teamId);
        if (team == null) return;

        string[] statFields = { "points", "rebounds", "assists", "steals", "blocks", "fgm", "fg3m", "ftm", "turnovers" };
        bool brokeHistoricalRecord = false;

        foreach (var ps in playerStats)
        {
            var player = GetPlayerById(ps.player_id);
            if (player == null) continue;

            string playerName = $"{player.first_name} {player.last_name}";

            foreach (var stat in statFields)
            {
                int value = stat switch
                {
                    "rebounds" => ps.oreb + ps.dreb,
                    "points" => ps.points,
                    "assists" => ps.assists,
                    "steals" => ps.steals,
                    "blocks" => ps.blocks,
                    "fgm" => ps.fgm,
                    "fg3m" => ps.fg3m,
                    "ftm" => ps.ftm,
                    "turnovers" => ps.turnovers,
                    _ => 0
                };

                if (value <= 0) continue;

                // Historical Record
                var histRecord = GetHistoricalRecord(stat);
                if (histRecord == null || value > histRecord.value)
                {
                    brokeHistoricalRecord = true;
                    if (histRecord == null)
                    {
                        histRecord = new HistoricalRecordData
                        {
                            stat_type = stat,
                            player_name = playerName,
                            value = value,
                            game_date = game.game_date,
                            team_abbreviation = team.abbreviation
                        };
                        _db.Insert(histRecord);
                    }
                    else
                    {
                        histRecord.player_name = playerName;
                        histRecord.value = value;
                        histRecord.game_date = game.game_date;
                        histRecord.team_abbreviation = team.abbreviation;
                        _db.Update(histRecord);
                    }
                }

                // Team Record
                var teamRecord = GetTeamRecord(teamId, stat);
                if (teamRecord == null || value > teamRecord.value)
                {
                    if (teamRecord == null)
                    {
                        teamRecord = new TeamRecordData
                        {
                            team_id = teamId,
                            stat_type = stat,
                            player_name = playerName,
                            value = value,
                            game_date = game.game_date
                        };
                        _db.Insert(teamRecord);
                    }
                    else
                    {
                        teamRecord.player_name = playerName;
                        teamRecord.value = value;
                        teamRecord.game_date = game.game_date;
                        _db.Update(teamRecord);
                    }
                }

                // Season Game Record
                var season = GetActiveSeason(GetActiveManager()?.id ?? 0);
                if (season != null)
                {
                    var seasonRecord = GetSeasonGameRecord(teamId, season.id, stat);
                    if (seasonRecord == null || value > seasonRecord.value)
                    {
                        if (seasonRecord == null)
                        {
                            seasonRecord = new SeasonGameRecordData
                            {
                                team_id = teamId,
                                season_id = season.id,
                                stat_type = stat,
                                player_name = playerName,
                                value = value,
                                game_date = game.game_date
                            };
                            _db.Insert(seasonRecord);
                        }
                        else
                        {
                            seasonRecord.player_name = playerName;
                            seasonRecord.value = value;
                            seasonRecord.game_date = game.game_date;
                            _db.Update(seasonRecord);
                        }
                    }
                }
            }
        }

        // Logro del GM: romper un récord histórico con un jugador de tu equipo
        if (brokeHistoricalRecord)
        {
            var mgr = GetActiveManager();
            if (mgr != null && mgr.team_id == teamId)
            {
                var s = GetActiveSeason(mgr.id);
                AchievementService.EvaluateRecordBreak(mgr.id, teamId, s?.id ?? 0);
            }
        }
    }
}
