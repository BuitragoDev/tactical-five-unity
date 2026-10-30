using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager
{
    // ── HITOS DE CARRERA (E1) ─────────────────────────────

    public List<MilestoneData> GetPlayerMilestones(int playerId)
    {
        if (!EnsureDb() || playerId <= 0) return new List<MilestoneData>();
        return _db.Table<MilestoneData>()
                  .Where(m => m.player_id == playerId)
                  .OrderBy(m => m.season_id).ThenBy(m => m.id)
                  .ToList();
    }

    /// <summary>Claves de hito ya registradas por jugador (para no repetir la noticia).</summary>
    public Dictionary<int, List<string>> GetMilestoneKeysByPlayer()
    {
        var dict = new Dictionary<int, List<string>>();
        if (!EnsureDb()) return dict;
        foreach (var m in _db.Table<MilestoneData>().ToList())
        {
            if (!dict.TryGetValue(m.player_id, out var list))
            {
                list = new List<string>();
                dict[m.player_id] = list;
            }
            list.Add(m.milestone);
        }
        return dict;
    }

    /// <summary>Nº de hitos registrados por jugador (etiqueta de Palmarés).</summary>
    public Dictionary<int, int> GetMilestoneCountsByPlayer()
    {
        var dict = new Dictionary<int, int>();
        if (!EnsureDb()) return dict;
        foreach (var m in _db.Table<MilestoneData>().ToList())
            dict[m.player_id] = dict.TryGetValue(m.player_id, out var n) ? n + 1 : 1;
        return dict;
    }

    public void InsertMilestones(List<MilestoneData> milestones)
    {
        if (!EnsureDb() || milestones == null || milestones.Count == 0) return;
        _db.InsertAll(milestones);
    }

    /// <summary>Convocatorias All-Star de carrera: las de la partida + las del seed histórico.</summary>
    public int GetCareerAllStarCount(int playerId)
    {
        if (!EnsureDb() || playerId <= 0) return 0;
        int fromGame = _db.ExecuteScalar<int>(
            "SELECT COUNT(DISTINCT season_id) FROM all_star_roster WHERE player_id = ?", playerId);
        int fromSeed = _db.ExecuteScalar<int>(
            "SELECT COALESCE(SUM(appearances), 0) FROM allstar_appearances_seed WHERE player_id = ?", playerId);
        return fromGame + fromSeed;
    }

    public bool HasMilestone(int playerId, string milestone)
    {
        if (!EnsureDb()) return false;
        return _db.Table<MilestoneData>()
                  .Any(m => m.player_id == playerId && m.milestone == milestone);
    }

    /// <summary>
    /// Totales de carrera de cada jugador: histórico (`historical_player_stats`, por
    /// `player_id`) + temporada en curso (todos los tipos menos el All-Star, igual que el
    /// archivado) + honores (contadores de `players` y convocatorias All-Star de la partida
    /// más las del seed histórico).
    /// </summary>
    public Dictionary<int, CareerTotals> BuildCareerTotals(int managerId, int seasonId)
    {
        var result = new Dictionary<int, CareerTotals>();
        if (!EnsureDb()) return result;

        void Add(int playerId, int points, int rebounds, int assists, int steals, int blocks,
                 int threes, int games, int dd, int td)
        {
            var t = result.TryGetValue(playerId, out var cur) ? cur : new CareerTotals();
            t.points += points; t.rebounds += rebounds; t.assists += assists;
            t.steals += steals; t.blocks += blocks; t.threePointers += threes;
            t.games += games; t.doubleDoubles += dd; t.tripleDoubles += td;
            result[playerId] = t;
        }

        // Temporadas anteriores (las leyendas sembradas llevan player_id <= 0: se ignoran).
        foreach (var h in _db.Table<HistoricalPlayerStatsData>().ToList())
        {
            if (h.player_id <= 0) continue;
            Add(h.player_id, h.total_points, h.total_rebounds, h.total_assists, h.total_steals,
                h.total_blocks, h.total_fg3m, h.games, h.total_double_doubles, h.total_triple_doubles);
        }

        // Temporada en curso (todos los tipos de partido menos el All-Star, como el archivado).
        if (seasonId > 0)
        {
            var aggregates = _db.Query<SeasonStatsAggregate>(
                @"SELECT ps.player_id, COUNT(*) AS gp,
                         SUM(ps.points) AS total_points, SUM(ps.rebounds) AS total_rebounds,
                         SUM(ps.assists) AS total_assists, SUM(ps.steals) AS total_steals,
                         SUM(ps.blocks) AS total_blocks, SUM(ps.fgm) AS total_fgm,
                         SUM(ps.fga) AS total_fga, SUM(ps.fg3m) AS total_fg3m,
                         SUM(ps.fg3a) AS total_fg3a, SUM(ps.ftm) AS total_ftm,
                         SUM(ps.fta) AS total_fta, SUM(ps.turnovers) AS total_turnovers,
                         SUM(ps.minutes) AS total_minutes, SUM(ps.rating) AS total_rating,
                         SUM(ps.double_double) AS total_dd, SUM(ps.triple_double) AS total_td
                  FROM player_game_stats ps
                  JOIN games g ON ps.game_id = g.id
                  WHERE g.manager_id = ? AND g.season_id = ? AND g.is_played = 1
                        AND g.game_type = 'regular'
                  GROUP BY ps.player_id",
                managerId, seasonId);
            foreach (var a in aggregates)
                Add(a.player_id, a.total_points, a.total_rebounds, a.total_assists, a.total_steals,
                    a.total_blocks, a.total_fg3m, a.gp, a.total_dd, a.total_td);
        }

        // Honores y convocatorias All-Star.
        foreach (var p in _db.Query<PlayerHonorsRow>(
            "SELECT id, team_id, rings, finals_mvps, season_mvps FROM players"))
        {
            var t = result.TryGetValue(p.id, out var cur) ? cur : new CareerTotals();
            t.rings += p.rings;
            t.finalsMvps += p.finals_mvps;
            t.seasonMvps += p.season_mvps;
            result[p.id] = t;
        }

        foreach (var r in _db.Query<AllStarSeasonCountRow>(
            "SELECT player_id, COUNT(DISTINCT season_id) AS appearances FROM all_star_roster WHERE manager_id = ? GROUP BY player_id",
            managerId))
        {
            if (r.player_id <= 0) continue;
            var t = result.TryGetValue(r.player_id, out var cur) ? cur : new CareerTotals();
            t.allStars += r.appearances;
            result[r.player_id] = t;
        }

        foreach (var s in _db.Table<AllStarAppearanceSeed>().ToList())
        {
            if (s.player_id <= 0) continue;
            var t = result.TryGetValue(s.player_id, out var cur) ? cur : new CareerTotals();
            t.allStars += s.appearances;
            result[s.player_id] = t;
        }

        return result;
    }

    /// <summary>
    /// Línea base (una vez por partida): registra **sin noticias** los hitos que los jugadores
    /// ya cumplen al activar el sistema (p. ej. las leyendas del seed con 40.000 puntos).
    /// Devuelve true si acaba de hacerse (el evaluador del día no debe emitir noticias).
    /// </summary>
    public bool EnsureMilestonesBaselined(int managerId, int seasonId)
    {
        if (!EnsureDb() || IsMigrationApplied("milestones_baselined")) return false;

        var careers = BuildCareerTotals(managerId, seasonId);
        var rows = new List<MilestoneData>();
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        int currentDay = GetActiveSeason(managerId)?.current_game_day ?? 0;

        // Equipo actual de cada jugador en una sola query (evita N+1 en la línea base).
        var teamByPlayer = new Dictionary<int, int>();
        foreach (var p in _db.Query<PlayerHonorsRow>("SELECT id, team_id, 0 AS rings, 0 AS finals_mvps, 0 AS season_mvps FROM players"))
            teamByPlayer[p.id] = p.team_id;

        foreach (var kv in careers)
        {
            foreach (var d in MilestoneHelper.Reached(kv.Value))
            {
                rows.Add(new MilestoneData
                {
                    player_id = kv.Key,
                    milestone = d.key,
                    season_id = seasonId,
                    game_day = currentDay,
                    team_id = teamByPlayer.TryGetValue(kv.Key, out var tid) ? tid : 0,
                    created_at = now
                });
            }
        }

        if (rows.Count > 0) _db.InsertAll(rows);
        MarkMigrationApplied("milestones_baselined");
        Debug.Log($"[DB] Migration: hitos de carrera baselinados ({rows.Count} registros)");
        return true;
    }

    // ── HISTORICAL PLAYER STATS ───────────────────────────

    public List<HistoricalPlayerStatsData> GetAllHistoricalPlayerStats()
    {
        return _db.Table<HistoricalPlayerStatsData>()
                  .OrderByDescending(p => p.total_points)
                  .ToList();
    }

    /// <summary>Carrera acumulada de un jugador real, por su id estable (nunca por nombre).</summary>
    public HistoricalPlayerStatsData GetHistoricalPlayerStats(int playerId)
    {
        if (playerId <= 0) return null;
        return _db.Table<HistoricalPlayerStatsData>()
                  .Where(p => p.player_id == playerId)
                  .FirstOrDefault();
    }

    /// <summary>
    /// Fila por nombre: SOLO filas sin dueño real (leyendas del seeder u huérfanas
    /// antiguas). Los jugadores reales se buscan por id — ver <see cref="HistoricalStatsRules"/>.
    /// </summary>
    public HistoricalPlayerStatsData GetHistoricalPlayerStats(string firstName, string lastName)
    {
        return _db.Table<HistoricalPlayerStatsData>()
                  .Where(p => p.first_name == firstName && p.last_name == lastName
                           && p.player_id <= 0)
                  .FirstOrDefault();
    }

    /// <summary>
    /// Totales de liga regular de cada jugador en esta partida. Las temporadas cerradas
    /// viven en `player_season_stats`; la temporada en curso sigue en `player_game_stats`.
    /// La migración puede pedir ambas por separado porque no sabe si la temporada actual
    /// ya fue archivada en `historical_player_stats`.
    /// </summary>
    Dictionary<int, HistoricalPlayerStatsData> BuildCareerGameTotals(bool includeCurrentSeason)
    {
        var totals = new Dictionary<int, HistoricalPlayerStatsData>();

        void Add(HistoricalStatsAggregateRow r)
        {
            if (r == null || r.player_id <= 0) return;
            if (!totals.TryGetValue(r.player_id, out var t))
            {
                t = new HistoricalPlayerStatsData { player_id = r.player_id };
                totals[r.player_id] = t;
            }

            t.games += r.games;
            t.total_points += r.total_points;
            t.total_rebounds += r.total_rebounds;
            t.total_assists += r.total_assists;
            t.total_steals += r.total_steals;
            t.total_blocks += r.total_blocks;
            t.total_turnovers += r.total_turnovers;
            t.total_fgm += r.total_fgm;
            t.total_fga += r.total_fga;
            t.total_fg3m += r.total_fg3m;
            t.total_fg3a += r.total_fg3a;
            t.total_ftm += r.total_ftm;
            t.total_fta += r.total_fta;
            t.total_oreb += r.total_oreb;
            t.total_dreb += r.total_dreb;
            t.total_double_doubles += r.total_double_doubles;
            t.total_triple_doubles += r.total_triple_doubles;
            t.total_minutes += r.total_minutes;
            t.total_rating += r.total_rating;
        }

        var archived = _db.Query<HistoricalStatsAggregateRow>(@"
            SELECT player_id,
                   SUM(games) AS games,
                   SUM(total_points) AS total_points,
                   SUM(total_rebounds) AS total_rebounds,
                   SUM(total_assists) AS total_assists,
                   SUM(total_steals) AS total_steals,
                   SUM(total_blocks) AS total_blocks,
                   SUM(total_turnovers) AS total_turnovers,
                   SUM(total_fgm) AS total_fgm,
                   SUM(total_fga) AS total_fga,
                   SUM(total_fg3m) AS total_fg3m,
                   SUM(total_fg3a) AS total_fg3a,
                   SUM(total_ftm) AS total_ftm,
                   SUM(total_fta) AS total_fta,
                   SUM(total_oreb) AS total_oreb,
                   SUM(total_dreb) AS total_dreb,
                   0 AS total_double_doubles,
                   0 AS total_triple_doubles,
                   CAST(SUM(total_minutes) AS INTEGER) AS total_minutes,
                   SUM(total_rating) AS total_rating
            FROM player_season_stats
            GROUP BY player_id");
        foreach (var row in archived) Add(row);

        if (includeCurrentSeason)
        {
            var current = _db.Query<HistoricalStatsAggregateRow>(@"
                SELECT ps.player_id,
                       COUNT(*) AS games,
                       SUM(ps.points) AS total_points,
                       SUM(ps.rebounds) AS total_rebounds,
                       SUM(ps.assists) AS total_assists,
                       SUM(ps.steals) AS total_steals,
                       SUM(ps.blocks) AS total_blocks,
                       SUM(ps.turnovers) AS total_turnovers,
                       SUM(ps.fgm) AS total_fgm,
                       SUM(ps.fga) AS total_fga,
                       SUM(ps.fg3m) AS total_fg3m,
                       SUM(ps.fg3a) AS total_fg3a,
                       SUM(ps.ftm) AS total_ftm,
                       SUM(ps.fta) AS total_fta,
                       SUM(ps.oreb) AS total_oreb,
                       SUM(ps.dreb) AS total_dreb,
                       SUM(ps.double_double) AS total_double_doubles,
                       SUM(ps.triple_double) AS total_triple_doubles,
                       CAST(SUM(ps.minutes) AS INTEGER) AS total_minutes,
                       SUM(ps.rating) AS total_rating
                FROM player_game_stats ps
                JOIN games g ON g.id = ps.game_id
                WHERE g.is_played = 1 AND g.game_type = 'regular'
                GROUP BY ps.player_id");
            foreach (var row in current) Add(row);
        }

        return totals;
    }

    /// <summary>
    /// Migración one-time: sustituye la base sintética de las carreras sembradas por la
    /// carrera real del seeder, conservando las temporadas jugadas y actualizando las
    /// filas del Salón de la Fama ya creadas.
    /// </summary>
    void CanonicalizeSeededPlayerCareers(bool allowSeedAdoption)
    {
        if (_db.Table<HistoricalPlayerStatsData>().Count() == 0) return;

        // En el seed/template todos los jugadores son del seed y se puede adoptar su
        // carrera real. En una partida antigua no: un rookie generado puede compartir
        // nombre con una leyenda y no debe heredar su carrera.
        if (allowSeedAdoption)
            SeedActivePlayerCareers();

        var all = _db.Table<HistoricalPlayerStatsData>().ToList();

        var archivedTotals = BuildCareerGameTotals(includeCurrentSeason: false);
        var allTotals = BuildCareerGameTotals(includeCurrentSeason: true);
        var seedRows = all.Where(r => r.player_id <= 0).ToList();
        var ownedRows = all.Where(r => r.player_id > 0).ToList();

        int merged = 0;
        foreach (var owned in ownedRows)
        {
            archivedTotals.TryGetValue(owned.player_id, out var archived);
            allTotals.TryGetValue(owned.player_id, out var allGames);

            HistoricalPlayerStatsData played = null;
            if (SeededCareerRules.LooksSyntheticBaseline(owned, archived))
                played = archived;
            else if (SeededCareerRules.LooksSyntheticBaseline(owned, allGames))
                played = allGames;

            if (played == null) continue;

            var seedRow = SeededCareerRules.FindUniqueSeedRow(
                seedRows, owned.first_name, owned.last_name);
            if (seedRow == null) continue;

            var combined = SeededCareerRules.Merge(seedRow, played);
            combined.id = owned.id;
            combined.player_id = owned.player_id;
            combined.first_name = owned.first_name;
            combined.last_name = owned.last_name;
            combined.position = string.IsNullOrEmpty(owned.position) ? seedRow.position : owned.position;
            combined.overall = owned.overall;
            combined.team_name = owned.team_name;
            combined.team_abbreviation = owned.team_abbreviation;
            combined.team_logo = owned.team_logo;
            // La base sintética no aporta dobles-dobles ni triples-dobles: los de la
            // fila propia son exactamente los de los partidos jugados.
            combined.total_double_doubles = seedRow.total_double_doubles + owned.total_double_doubles;
            combined.total_triple_doubles = seedRow.total_triple_doubles + owned.total_triple_doubles;

            _db.Update(combined);
            _db.Delete(seedRow);
            seedRows.Remove(seedRow);
            UpdateHallOfFameCareerTotals(owned.player_id, combined);
            merged++;
        }

        // Filas del seeder con 0 partidos (rookies sin carrera NBA) que ya tienen dueño
        // en la partida: no aportan nada y duplicarían la fila en Estadísticas históricas.
        var playerNames = new HashSet<string>(
            _db.Table<PlayerData>().Select(p => SeededCareerRules.NameKey(p.first_name, p.last_name)));
        int removedEmpty = 0;
        foreach (var seedRow in seedRows.ToList())
        {
            if (seedRow.games != 0) continue;
            string key = SeededCareerRules.NameKey(seedRow.first_name, seedRow.last_name);
            bool hasOwnedRow = ownedRows.Any(r =>
                SeededCareerRules.NameKey(r.first_name, r.last_name) == key);
            if (!hasOwnedRow && !playerNames.Contains(key)) continue;

            _db.Delete(seedRow);
            seedRows.Remove(seedRow);
            removedEmpty++;
        }

        // Red de seguridad: un HOF sin fila de carrera propia se reconstruye desde el
        // seeder. No debería ocurrir en partidas normales, pero evita dejar una fila
        // histórica huérfana si una versión anterior borró la carrera.
        foreach (var hof in _db.Table<HallOfFameData>().Where(h => h.player_id > 0).ToList())
        {
            if (ownedRows.Any(r => r.player_id == hof.player_id)) continue;

            var seedRow = SeededCareerRules.FindUniqueSeedRow(
                seedRows, hof.first_name, hof.last_name);
            if (seedRow == null) continue;

            allTotals.TryGetValue(hof.player_id, out var played);
            var combined = SeededCareerRules.Merge(seedRow, played);
            combined.id = 0;
            combined.player_id = hof.player_id;
            combined.first_name = hof.first_name;
            combined.last_name = hof.last_name;
            combined.position = hof.position;
            combined.team_abbreviation = hof.team_abbreviation;

            _db.Insert(combined);
            _db.Delete(seedRow);
            seedRows.Remove(seedRow);
            UpdateHallOfFameCareerTotals(hof.player_id, combined);
            merged++;
        }

        if (merged > 0 || removedEmpty > 0)
            Debug.Log($"[DB] Carreras sembradas: {merged} unificadas con el histórico real, " +
                      $"{removedEmpty} filas vacías del seeder eliminadas.");
    }

    void UpdateHallOfFameCareerTotals(int playerId, HistoricalPlayerStatsData stats)
    {
        if (playerId <= 0 || stats == null) return;

        var hof = _db.Table<HallOfFameData>()
                     .FirstOrDefault(h => h.player_id == playerId);
        if (hof == null) return;

        hof.career_points = stats.total_points;
        hof.career_rebounds = stats.total_rebounds;
        hof.career_assists = stats.total_assists;
        hof.career_games = stats.games;
        if (!string.IsNullOrEmpty(stats.team_abbreviation))
            hof.team_abbreviation = stats.team_abbreviation;
        _db.Update(hof);
    }

    /// <summary>
    /// F6-01: rellena splits de tiro, pérdidas y OREB/DREB en filas archivadas
    /// usando los partidos que aún existan en `player_game_stats`. Las temporadas
    /// ya purgadas no se pueden reconstruir y se dejan como estén.
    /// </summary>
    public void BackfillPlayerSeasonShootingSplits()
    {
        _db.Execute(@"
            UPDATE player_season_stats SET
                total_fgm = (SELECT COALESCE(SUM(ps.fgm), 0) FROM player_game_stats ps
                             JOIN games g ON g.id = ps.game_id
                             WHERE g.season_id = player_season_stats.season_id
                               AND ps.player_id = player_season_stats.player_id
                               AND ps.team_id = player_season_stats.team_id
                               AND g.is_played = 1 AND g.game_type = 'regular'),
                total_fga = (SELECT COALESCE(SUM(ps.fga), 0) FROM player_game_stats ps
                             JOIN games g ON g.id = ps.game_id
                             WHERE g.season_id = player_season_stats.season_id
                               AND ps.player_id = player_season_stats.player_id
                               AND ps.team_id = player_season_stats.team_id
                               AND g.is_played = 1 AND g.game_type = 'regular'),
                total_fg3m = (SELECT COALESCE(SUM(ps.fg3m), 0) FROM player_game_stats ps
                              JOIN games g ON g.id = ps.game_id
                              WHERE g.season_id = player_season_stats.season_id
                                AND ps.player_id = player_season_stats.player_id
                                AND ps.team_id = player_season_stats.team_id
                                AND g.is_played = 1 AND g.game_type = 'regular'),
                total_fg3a = (SELECT COALESCE(SUM(ps.fg3a), 0) FROM player_game_stats ps
                              JOIN games g ON g.id = ps.game_id
                              WHERE g.season_id = player_season_stats.season_id
                                AND ps.player_id = player_season_stats.player_id
                                AND ps.team_id = player_season_stats.team_id
                                AND g.is_played = 1 AND g.game_type = 'regular'),
                total_ftm = (SELECT COALESCE(SUM(ps.ftm), 0) FROM player_game_stats ps
                             JOIN games g ON g.id = ps.game_id
                             WHERE g.season_id = player_season_stats.season_id
                               AND ps.player_id = player_season_stats.player_id
                               AND ps.team_id = player_season_stats.team_id
                               AND g.is_played = 1 AND g.game_type = 'regular'),
                total_fta = (SELECT COALESCE(SUM(ps.fta), 0) FROM player_game_stats ps
                             JOIN games g ON g.id = ps.game_id
                             WHERE g.season_id = player_season_stats.season_id
                               AND ps.player_id = player_season_stats.player_id
                               AND ps.team_id = player_season_stats.team_id
                               AND g.is_played = 1 AND g.game_type = 'regular'),
                total_turnovers = (SELECT COALESCE(SUM(ps.turnovers), 0) FROM player_game_stats ps
                                   JOIN games g ON g.id = ps.game_id
                                   WHERE g.season_id = player_season_stats.season_id
                                     AND ps.player_id = player_season_stats.player_id
                                     AND ps.team_id = player_season_stats.team_id
                                     AND g.is_played = 1 AND g.game_type = 'regular'),
                total_oreb = (SELECT COALESCE(SUM(ps.oreb), 0) FROM player_game_stats ps
                              JOIN games g ON g.id = ps.game_id
                              WHERE g.season_id = player_season_stats.season_id
                                AND ps.player_id = player_season_stats.player_id
                                AND ps.team_id = player_season_stats.team_id
                                AND g.is_played = 1 AND g.game_type = 'regular'),
                total_dreb = (SELECT COALESCE(SUM(ps.dreb), 0) FROM player_game_stats ps
                              JOIN games g ON g.id = ps.game_id
                              WHERE g.season_id = player_season_stats.season_id
                                AND ps.player_id = player_season_stats.player_id
                                AND ps.team_id = player_season_stats.team_id
                                AND g.is_played = 1 AND g.game_type = 'regular')
            WHERE EXISTS (
                SELECT 1 FROM player_game_stats ps
                JOIN games g ON g.id = ps.game_id
                WHERE g.season_id = player_season_stats.season_id
                  AND ps.player_id = player_season_stats.player_id
                  AND ps.team_id = player_season_stats.team_id
                  AND g.is_played = 1 AND g.game_type = 'regular')");
    }

    /// <summary>F6-03: agregados de liga por temporada (archivo + temporada en curso).</summary>
    public List<LeagueSeasonAggregateRow> GetLeagueSeasonAggregates(int managerId)
    {
        if (!EnsureDb()) return new List<LeagueSeasonAggregateRow>();

        return _db.Query<LeagueSeasonAggregateRow>(@"
            SELECT season_id, MAX(year_start) AS year_start, MAX(year_end) AS year_end,
                   SUM(games) AS games, SUM(total_minutes) AS total_minutes,
                   SUM(total_points) AS total_points, SUM(total_rebounds) AS total_rebounds,
                   SUM(total_assists) AS total_assists, SUM(total_steals) AS total_steals,
                   SUM(total_blocks) AS total_blocks,
                   SUM(total_fgm) AS total_fgm, SUM(total_fga) AS total_fga,
                   SUM(total_fg3m) AS total_fg3m, SUM(total_fg3a) AS total_fg3a,
                   SUM(total_ftm) AS total_ftm, SUM(total_fta) AS total_fta,
                   SUM(total_turnovers) AS total_turnovers,
                   SUM(total_oreb) AS total_oreb, SUM(total_dreb) AS total_dreb
            FROM (
                SELECT season_id, year_start, year_end, games, total_minutes,
                       total_points, total_rebounds, total_assists, total_steals,
                       total_blocks, total_fgm, total_fga, total_fg3m, total_fg3a,
                       total_ftm, total_fta, total_turnovers, total_oreb, total_dreb
                FROM player_season_stats

                UNION ALL

                SELECT g.season_id, s.year_start, s.year_end, COUNT(*),
                       SUM(ps.minutes), SUM(ps.points), SUM(ps.rebounds),
                       SUM(ps.assists), SUM(ps.steals), SUM(ps.blocks),
                       SUM(ps.fgm), SUM(ps.fga), SUM(ps.fg3m), SUM(ps.fg3a),
                       SUM(ps.ftm), SUM(ps.fta), SUM(ps.turnovers),
                       SUM(ps.oreb), SUM(ps.dreb)
                FROM player_game_stats ps
                JOIN games g ON ps.game_id = g.id
                JOIN seasons s ON g.season_id = s.id
                WHERE g.manager_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
                GROUP BY g.season_id
            )
            GROUP BY season_id
            ORDER BY year_start", managerId).ToList();
    }

    public List<PlayerCareerSeasonRow> GetPlayerCareerHistory(int playerId, int managerId)
    {
        if (!EnsureDb()) return new List<PlayerCareerSeasonRow>();

        // Archived past seasons + current unarchived season
        return _db.Query<PlayerCareerSeasonRow>(
            @"SELECT season_id, year_start, year_end, team_id,
                     team_abbreviation, team_name,
                     games, total_minutes, total_points,
                     total_rebounds, total_assists,
                     total_steals, total_blocks, total_rating,
                     total_fgm, total_fga, total_fg3m, total_fg3a,
                     total_ftm, total_fta, total_turnovers, total_oreb, total_dreb
              FROM player_season_stats
              WHERE player_id = ?

              UNION ALL

              SELECT g.season_id, s.year_start, s.year_end,
                     ps.team_id, t.abbreviation AS team_abbreviation, t.name AS team_name,
                     COUNT(*) AS games,
                     SUM(ps.minutes) AS total_minutes,
                     SUM(ps.points) AS total_points,
                     SUM(ps.rebounds) AS total_rebounds,
                     SUM(ps.assists) AS total_assists,
                     SUM(ps.steals) AS total_steals,
                     SUM(ps.blocks) AS total_blocks,
                     SUM(ps.rating) AS total_rating,
                     SUM(ps.fgm) AS total_fgm,
                     SUM(ps.fga) AS total_fga,
                     SUM(ps.fg3m) AS total_fg3m,
                     SUM(ps.fg3a) AS total_fg3a,
                     SUM(ps.ftm) AS total_ftm,
                     SUM(ps.fta) AS total_fta,
                     SUM(ps.turnovers) AS total_turnovers,
                     SUM(ps.oreb) AS total_oreb,
                     SUM(ps.dreb) AS total_dreb
              FROM player_game_stats ps
              JOIN games g ON ps.game_id = g.id
              JOIN seasons s ON g.season_id = s.id
              LEFT JOIN teams t ON ps.team_id = t.id
              WHERE ps.player_id = ? AND g.manager_id = ? AND g.is_played = 1
                    AND g.game_type = 'regular'
              GROUP BY g.season_id, ps.team_id

              ORDER BY season_id",
            playerId, playerId, managerId).ToList();
    }

    public List<PlayerAwardEntry> GetPlayerAwards(int playerId)
    {
        if (!EnsureDb()) return new List<PlayerAwardEntry>();

        var allRecords = _db.Query<SeasonAwardRow>(
            @"SELECT sr.season_id, s.year_start, s.year_end,
                     sr.season_mvp_id, sr.rookie_of_year_id, sr.finals_mvp_id,
                     sr.best_defender_id, sr.sixth_man_id, sr.most_improved_id,
                     sr.all_star_pg_id, sr.all_star_sg_id, sr.all_star_sf_id,
                     sr.all_star_pf_id, sr.all_star_c_id,
                     sr.first_team_pg, sr.first_team_sg, sr.first_team_sf,
                     sr.first_team_pf, sr.first_team_c,
                     sr.second_team_pg, sr.second_team_sg, sr.second_team_sf,
                     sr.second_team_pf, sr.second_team_c,
                     sr.champion_id
              FROM season_records sr
              JOIN seasons s ON sr.season_id = s.id
              ORDER BY sr.season_id").ToList();

        var awards = new List<PlayerAwardEntry>();

        foreach (var r in allRecords)
        {
            if (r.season_mvp_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "mvp" });
            if (r.rookie_of_year_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "roty" });
            if (r.finals_mvp_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "finals_mvp" });
            if (r.best_defender_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "dpoy" });
            if (r.sixth_man_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "sixth_man" });
            if (r.most_improved_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "mip" });
            if (r.champion_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "champion" });

            // First Team
            if (r.first_team_pg == playerId || r.first_team_sg == playerId ||
                r.first_team_sf == playerId || r.first_team_pf == playerId ||
                r.first_team_c == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "first_team" });

            // Second Team
            if (r.second_team_pg == playerId || r.second_team_sg == playerId ||
                r.second_team_sf == playerId || r.second_team_pf == playerId ||
                r.second_team_c == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "second_team" });
        }

        // Monthly awards (player / rookie of the month)
        var monthlyAwards = _db.Query<MonthlyAwardData>(
            "SELECT * FROM monthly_awards WHERE player_id = ? AND award_type IN ('player_month', 'rookie_month')",
            playerId).ToList();

        foreach (var m in monthlyAwards)
        {
            var season = _db.Find<SeasonData>(m.season_id);
            if (season != null)
                awards.Add(new PlayerAwardEntry { season_id = m.season_id, year_start = season.year_start, year_end = season.year_end, award_type = m.award_type });
        }

        // Convocatorias All-Star (tabla all_star_roster: ve a los 24, no solo al quinteto)
        foreach (int allStarSeasonId in GetAllStarSeasonIds(playerId))
        {
            var season = _db.Find<SeasonData>(allStarSeasonId);
            if (season != null)
                awards.Add(new PlayerAwardEntry
                {
                    season_id = allStarSeasonId,
                    year_start = season.year_start,
                    year_end = season.year_end,
                    award_type = "all_star"
                });
        }

        return awards;
    }

    public void SaveHistoricalPlayerStats(HistoricalPlayerStatsData stats)
    {
        // La fila a actualizar se decide por id de fila, por player_id o (solo si la fila
        // no tiene dueño real) por nombre. Nunca por nombre si hay player_id: era el bug
        // que hacía que un homónimo heredara la carrera de una leyenda.
        HistoricalPlayerStatsData existing = null;
        if (stats.id > 0)
            existing = _db.Find<HistoricalPlayerStatsData>(stats.id);
        else if (stats.player_id > 0)
            existing = GetHistoricalPlayerStats(stats.player_id);
        else
            existing = GetHistoricalPlayerStats(stats.first_name, stats.last_name);

        if (existing != null)
        {
            stats.id = existing.id;
            _db.Update(stats);
        }
        else
        {
            _db.Insert(stats);
        }
    }

    public void UpdateHistoricalPlayerStatsFromSeason(int seasonId, int managerId)
    {
        var seasonStats = _db.Query<HistoricalStatsAggregateRow>(
            @"SELECT ps.player_id,
                     COUNT(*) AS games,
                     SUM(ps.points) AS total_points,
                     SUM(ps.rebounds) AS total_rebounds,
                     SUM(ps.assists) AS total_assists,
                     SUM(ps.steals) AS total_steals,
                     SUM(ps.blocks) AS total_blocks,
                     SUM(ps.turnovers) AS total_turnovers,
                     SUM(ps.fgm) AS total_fgm,
                     SUM(ps.fga) AS total_fga,
                     SUM(ps.fg3m) AS total_fg3m,
                     SUM(ps.fg3a) AS total_fg3a,
                     SUM(ps.ftm) AS total_ftm,
                     SUM(ps.fta) AS total_fta,
                     SUM(ps.oreb) AS total_oreb,
                     SUM(ps.dreb) AS total_dreb,
                     SUM(ps.double_double) AS total_double_doubles,
                     SUM(ps.triple_double) AS total_triple_doubles,
                     CAST(SUM(ps.minutes) AS INTEGER) AS total_minutes,
                     SUM(ps.rating) AS total_rating
              FROM player_game_stats ps
              JOIN games g ON ps.game_id = g.id
              WHERE g.season_id = ? AND g.is_played = 1
                    AND g.game_type = 'regular'
              GROUP BY ps.player_id",
            seasonId);

        foreach (var ss in seasonStats)
        {
            var player = GetPlayerById(ss.player_id);
            if (player == null) continue;

            var hist = GetHistoricalPlayerStats(player.id);
            if (hist == null)
            {
                var team = GetTeamById(player.team_id);
                hist = new HistoricalPlayerStatsData
                {
                    player_id = player.id,
                    first_name = player.first_name,
                    last_name = player.last_name,
                    position = player.position,
                    overall = player.overall,
                    team_name = team?.name ?? "",
                    team_abbreviation = team?.abbreviation ?? "",
                    team_logo = team?.logo ?? ""
                };
            }

            hist.games += ss.games;
            hist.total_points += ss.total_points;
            hist.total_rebounds += ss.total_rebounds;
            hist.total_assists += ss.total_assists;
            hist.total_steals += ss.total_steals;
            hist.total_blocks += ss.total_blocks;
            hist.total_turnovers += ss.total_turnovers;
            hist.total_fgm += ss.total_fgm;
            hist.total_fga += ss.total_fga;
            hist.total_fg3m += ss.total_fg3m;
            hist.total_fg3a += ss.total_fg3a;
            hist.total_ftm += ss.total_ftm;
            hist.total_fta += ss.total_fta;
            hist.total_oreb += ss.total_oreb;
            hist.total_dreb += ss.total_dreb;
            hist.total_double_doubles += ss.total_double_doubles;
            hist.total_triple_doubles += ss.total_triple_doubles;
            hist.total_minutes += ss.total_minutes;
            hist.total_rating += ss.total_rating;

            var currentTeam = GetTeamById(player.team_id);
            if (currentTeam != null)
            {
                hist.team_name = currentTeam.name;
                hist.team_abbreviation = currentTeam.abbreviation;
                hist.team_logo = currentTeam.logo;
            }

            SaveHistoricalPlayerStats(hist);
        }
    }

    /// <summary>
    /// Decide si un jugador merece entrar en el Salón de la Fama según sus
    /// estadísticas históricas, anillos y Finales MVP (tanto pre-partida como simuladas).
    /// </summary>
    public bool WouldInduct(PlayerData p)
    {
        try
        {
            var hist = GetHistoricalPlayerStats(p.id);
            int pts = hist?.total_points ?? 0;
            int reb = hist?.total_rebounds ?? 0;
            int ast = hist?.total_assists ?? 0;
            int fmvps = Math.Max(p.finals_mvps,
                _db.Table<FinalsRecord>().Count(f => f.mvp == $"{p.first_name} {p.last_name}"));
            // E1: 10× All-Star o 3× MVP de temporada también inducen.
            return HallOfFameHelper.ShouldInduct(p.rings, fmvps, pts, reb, ast,
                                                 allStars: GetCareerAllStarCount(p.id),
                                                 seasonMvps: p.season_mvps);
        }
        catch { return false; }
    }

    /// <summary>
    /// Jugadores con 40+ años que se retirarán esta temporada y merecen entrar
    /// al Salón de la Fama (según WouldInduct).
    /// </summary>
    public List<PlayerData> GetRetiringHallOfFameMembers()
    {
        var candidates = _db.Table<PlayerData>()
            .Where(p => p.age >= 40)
            .ToList();
        return candidates
            .Where(p => WouldInduct(p))
            .OrderByDescending(p => p.age)
            .ToList();
    }

    /// <summary>
    /// Captura a un jugador retirado que cumpla los umbrales y lo inserta en el Salón de la Fama.
    /// Se llama antes de borrar al jugador de la tabla players.
    /// </summary>
    void TryInductIntoHallOfFame(PlayerData p, string seasonLabel)
    {
        try
        {
            if (!WouldInduct(p))
                return;

            var hist = GetHistoricalPlayerStats(p.id);
            int pts = hist?.total_points ?? 0;
            int reb = hist?.total_rebounds ?? 0;
            int ast = hist?.total_assists ?? 0;
            int games = hist?.games ?? 0;
            int fmvps = Math.Max(p.finals_mvps,
                _db.Table<FinalsRecord>().Count(f => f.mvp == $"{p.first_name} {p.last_name}"));

            var team = GetTeamById(p.team_id);
            _db.Insert(new HallOfFameData
            {
                player_id = p.id,
                first_name = p.first_name,
                last_name = p.last_name,
                position = p.position,
                team_abbreviation = team?.abbreviation ?? "",
                rings = p.rings,
                career_points = pts,
                career_rebounds = reb,
                career_assists = ast,
                career_games = games,
                finals_mvps = fmvps,
                induction_season = seasonLabel
            });
            Debug.Log($"[DB] {p.first_name} {p.last_name} inducido al Salón de la Fama ({seasonLabel}, {p.rings} anillos).");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Error induciendo al HOF a {p.first_name} {p.last_name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Determina si un jugador retirado merece que su equipo retire su dorsal.
    /// Criterio: HOF dinámico (mismo WouldInduct) O (>=10 temporadas en el equipo
    /// y números de carrera altos, usando los mismos umbrales del Salón de la Fama).
    /// Solo para retiros dinámicos ocurridos durante la partida.
    /// </summary>
    public bool ShouldRetireNumber(PlayerData p)
    {
        if (p == null || p.number <= 0) return false;
        if (WouldInduct(p)) return true;
        if (p.seasons_with_team < 10) return false;
        try
        {
            var hist = GetHistoricalPlayerStats(p.id);
            int pts = hist?.total_points ?? 0;
            int reb = hist?.total_rebounds ?? 0;
            int ast = hist?.total_assists ?? 0;
            int fmvps = Math.Max(p.finals_mvps,
                _db.Table<FinalsRecord>().Count(f => f.mvp == $"{p.first_name} {p.last_name}"));
            return HallOfFameHelper.ShouldInduct(p.rings, fmvps, pts, reb, ast);
        }
        catch { return false; }
    }

    /// <summary>
    /// Captura el dorsal retirado de un jugador antes de que sea borrado de la tabla
    /// players (se llama en StartNewSeason antes de _db.Delete(p)).
    /// </summary>
    void TryRetireNumber(PlayerData p, string seasonLabel)
    {
        try
        {
            if (!ShouldRetireNumber(p) || p.team_id <= 0) return;

            var hist = GetHistoricalPlayerStats(p.id);
            _db.Insert(new RetiredNumberData
            {
                team_id = p.team_id,
                number = p.number,
                player_id = p.id,
                first_name = p.first_name,
                last_name = p.last_name,
                position = p.position,
                rings = p.rings,
                career_points = hist?.total_points ?? 0,
                induction_season = seasonLabel
            });
            Debug.Log($"[DB] Dorsal {p.number} retirado por {p.first_name} {p.last_name} ({seasonLabel}, {p.rings} anillos).");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Error retirando dorsal de {p.first_name} {p.last_name}: {ex.Message}");
        }
    }

    public void SaveSeasonEndRecords(int seasonId, int managerId)
    {
        var season = GetActiveSeason(managerId);
        if (season == null) return;
        string seasonLabel = $"{season.year_start}-{season.year_end.ToString().Substring(2)}";
        Debug.Log($"[DB] Saving season-end records for {seasonLabel}...");

        // El MIP debe resolverse antes del rollover, cuando la temporada actual
        // aún está en player_game_stats y la anterior ya puede estar archivada.
        var mostImproved = GetMostImprovedPlayer(seasonId, managerId);
        var seasonRecord = _db.Table<SeasonRecord>()
            .Where(r => r.season_id == seasonId)
            .FirstOrDefault();
        if (seasonRecord == null)
        {
            seasonRecord = new SeasonRecord
            {
                season_id = seasonId,
                most_improved_id = mostImproved?.PlayerId ?? 0
            };
            _db.Insert(seasonRecord);
        }
        else if (mostImproved != null && seasonRecord.most_improved_id != mostImproved.PlayerId)
        {
            seasonRecord.most_improved_id = mostImproved.PlayerId;
            _db.Update(seasonRecord);
        }
        if (mostImproved != null)
            Debug.Log($"[DB] Most Improved saved: {mostImproved.PlayerName}");

        // ── Finals Record ──
        var finalsGames = _db.Table<GameData>()
            .Where(g => g.manager_id == managerId
                     && g.season_id == seasonId
                     && g.series_label == "playoff-r4-finals"
                     && g.is_played == 1)
            .ToList();

        if (finalsGames.Count > 0)
        {
            int teamA = finalsGames[0].home_team_id;
            int teamB = finalsGames[0].away_team_id;
            var winCount = new Dictionary<int, int>();
            foreach (var g in finalsGames)
            {
                int winner = g.home_score >= g.away_score ? g.home_team_id : g.away_team_id;
                winCount[winner] = winCount.GetValueOrDefault(winner, 0) + 1;
            }

            int champId = winCount.OrderByDescending(kv => kv.Value).First().Key;
            int finalistId = champId == teamA ? teamB : teamA;
            int champWins = winCount[champId];
            int finalistWins = winCount.GetValueOrDefault(finalistId, 0);

            var champTeam = GetTeamById(champId);
            var finalistTeam = GetTeamById(finalistId);

            // Copy Finals player stats to finals_player_stats table
            var finalsGameIds = finalsGames.Select(g => g.id).ToList();
            var allFinalsStats = new List<PlayerGameStats>();
            if (finalsGameIds.Count > 0)
            {
                _db.Execute("DELETE FROM finals_player_stats WHERE game_id IN (" +
                    string.Join(",", finalsGameIds) + ")");
                allFinalsStats = _db.Query<PlayerGameStats>(
                    "SELECT * FROM player_game_stats WHERE game_id IN (" +
                    string.Join(",", finalsGameIds) + ")");
                foreach (var ps in allFinalsStats)
                {
                    _db.Insert(new FinalsPlayerStatsData
                    {
                        game_id = ps.game_id,
                        player_id = ps.player_id,
                        team_id = ps.team_id,
                        minutes = ps.minutes,
                        points = ps.points,
                        fgm = ps.fgm,
                        fga = ps.fga,
                        fg3m = ps.fg3m,
                        fg3a = ps.fg3a,
                        ftm = ps.ftm,
                        fta = ps.fta,
                        oreb = ps.oreb,
                        dreb = ps.dreb,
                        rebounds = ps.rebounds,
                        assists = ps.assists,
                        steals = ps.steals,
                        blocks = ps.blocks,
                        turnovers = ps.turnovers,
                        pf = ps.pf,
                        rating = ps.rating,
                        double_double = ps.double_double,
                        triple_double = ps.triple_double
                    });
                }
            }

            // Finals MVP: player from champ team with best average rating
            string finalsMvp = "";
            var champStats = allFinalsStats.Where(s => s.team_id == champId).ToList();
            if (champStats.Count > 0)
            {
                var topPlayer = champStats
                    .GroupBy(s => s.player_id)
                    .Select(g => new { PlayerId = g.Key, AvgRating = g.Average(s => s.rating) })
                    .OrderByDescending(x => x.AvgRating)
                    .First();
                var mvpPlayer = GetPlayerById(topPlayer.PlayerId);
                if (mvpPlayer != null)
                {
                    finalsMvp = $"{mvpPlayer.first_name} {mvpPlayer.last_name}";

                    // Finals MVP +1 en el contador del jugador (como los anillos del campeón).
                    mvpPlayer.finals_mvps = mvpPlayer.finals_mvps + 1;
                    _db.Update(mvpPlayer);
                }

                // season_records: campeón, finalista y MVP de las Finales (antes solo
                // se escribía most_improved_id y la trayectoria del jugador salía vacía).
                seasonRecord.champion_id = champId;
                seasonRecord.finalist_id = finalistId;
                seasonRecord.finals_mvp_id = topPlayer.PlayerId;
                seasonRecord.finals_mvp_rating = (float)topPlayer.AvgRating;
            }

            _db.Insert(new FinalsRecord
            {
                season = seasonLabel,
                champ_name = champTeam?.name ?? "",
                champ_keyword = champTeam?.logo ?? "",
                finalist_name = finalistTeam?.name ?? "",
                finalist_keyword = finalistTeam?.logo ?? "",
                result = $"{champWins}-{finalistWins}",
                mvp = finalsMvp
            });
            Debug.Log($"[DB] FinalsRecord saved: {champTeam?.name} {champWins}-{finalistWins} over {finalistTeam?.name}");

            // Ring para todos los jugadores del equipo campeón (los que están en su plantilla al ganar).
            var champRosters = _db.Table<PlayerData>().Where(p => p.team_id == champId).ToList();
            foreach (var cp in champRosters)
            {
                cp.rings = cp.rings + 1;
                _db.Update(cp);
            }
            if (champRosters.Count > 0)
                Debug.Log($"[DB] Rings +{champRosters.Count} para la plantilla campeona {champTeam?.name}");

            // Finales jugadas: +1 para todos los jugadores de ambos finalistas (campeón y subcampeón).
            var finalsRosters = _db.Table<PlayerData>()
                .Where(p => p.team_id == champId || p.team_id == finalistId)
                .ToList();
            foreach (var fp in finalsRosters)
            {
                fp.finals_played = fp.finals_played + 1;
                _db.Update(fp);
            }
            if (finalsRosters.Count > 0)
                Debug.Log($"[DB] Finals played +{finalsRosters.Count} para los finalistas {champTeam?.name} y {finalistTeam?.name}");
        }

        // ── Season awards & All-NBA quintets (regular season) ──
        var seasonStats = _db.Query<HistoricalStatsAggregateRow>(
            @"SELECT ps.player_id,
                     COUNT(*) AS games,
                     SUM(ps.points) AS total_points,
                     SUM(ps.rebounds) AS total_rebounds,
                     SUM(ps.assists) AS total_assists,
                     SUM(ps.steals) AS total_steals,
                     SUM(ps.blocks) AS total_blocks,
                     SUM(ps.turnovers) AS total_turnovers,
                     SUM(ps.fgm) AS total_fgm,
                     SUM(ps.fga) AS total_fga,
                     SUM(ps.fg3m) AS total_fg3m,
                     SUM(ps.fg3a) AS total_fg3a,
                     SUM(ps.ftm) AS total_ftm,
                     SUM(ps.fta) AS total_fta,
                     SUM(ps.oreb) AS total_oreb,
                     SUM(ps.dreb) AS total_dreb,
                     SUM(ps.double_double) AS total_double_doubles,
                     SUM(ps.triple_double) AS total_triple_doubles,
                     CAST(SUM(ps.minutes) AS INTEGER) AS total_minutes,
                     SUM(ps.rating) AS total_rating
              FROM player_game_stats ps
              JOIN games g ON ps.game_id = g.id
              WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              GROUP BY ps.player_id",
            seasonId);

        if (seasonStats.Count > 0)
        {
            // MVP
            var mvpCandidates = seasonStats.Where(s => s.games >= 65).ToList();
            if (mvpCandidates.Count == 0) mvpCandidates = seasonStats;
            var topMvp = mvpCandidates.OrderByDescending(s => (double)s.total_rating / s.games).First();
            var mvpPlayer = GetPlayerById(topMvp.player_id);
            var mvpTeam = mvpPlayer != null ? GetTeamById(mvpPlayer.team_id) : null;
            string mvpName = mvpPlayer != null ? $"{mvpPlayer.first_name} {mvpPlayer.last_name}" : "";
            string mvpRatingStr = ((double)topMvp.total_rating / Math.Max(1, topMvp.games)).ToString("F1", CultureInfo.InvariantCulture);

            // MVP temporada regular: +1 en el contador del jugador.
            if (mvpPlayer != null)
            {
                mvpPlayer.season_mvps = mvpPlayer.season_mvps + 1;
                _db.Update(mvpPlayer);
            }

            seasonRecord.season_mvp_id = topMvp.player_id;
            seasonRecord.season_mvp_rating = (float)((double)topMvp.total_rating / Math.Max(1, topMvp.games));
            seasonRecord.season_mvp_games = topMvp.games;

            // Rookie of the Year
            var rookieCandidates = seasonStats
                .Where(s =>
                {
                    var p = GetPlayerById(s.player_id);
                    return p != null && p.is_rookie == 1;
                })
                .ToList();
            string rookieName = "", rookieTeamKeyword = "", rookieRatingStr = "";
            if (rookieCandidates.Count > 0)
            {
                var rookiesQualified = rookieCandidates.Where(r => r.games >= 65).ToList();
                if (rookiesQualified.Count == 0) rookiesQualified = rookieCandidates;
                var topRookie = rookiesQualified.OrderByDescending(r => (double)r.total_rating / r.games).First();
                var rookiePlayer = GetPlayerById(topRookie.player_id);
                var rookieTeam = rookiePlayer != null ? GetTeamById(rookiePlayer.team_id) : null;
                rookieName = rookiePlayer != null ? $"{rookiePlayer.first_name} {rookiePlayer.last_name}" : "";
                rookieTeamKeyword = rookieTeam?.logo ?? "";
                rookieRatingStr = ((double)topRookie.total_rating / Math.Max(1, topRookie.games)).ToString("F1", CultureInfo.InvariantCulture);

                seasonRecord.rookie_of_year_id = topRookie.player_id;
                seasonRecord.rookie_rating = (float)((double)topRookie.total_rating / Math.Max(1, topRookie.games));
                seasonRecord.rookie_games = topRookie.games;
            }

            _db.Insert(new AwardsRecord
            {
                season = seasonLabel,
                mvp = mvpName,
                mvp_team_keyword = mvpTeam?.logo ?? "",
                mvp_rating = mvpRatingStr,
                rookie = rookieName,
                rookie_team_keyword = rookieTeamKeyword,
                rookie_rating = rookieRatingStr
            });
            Debug.Log($"[DB] AwardsRecord saved: MVP={mvpName}, ROY={rookieName}");

            // All-NBA Quintets — both primary and secondary positions, no duplicate players
            string[] positions = { "PG", "SG", "SF", "PF", "C" };
            var posValues = new Dictionary<string, (string name, string team)>
            {
                { "PG", ("", "") }, { "SG", ("", "") }, { "SF", ("", "") },
                { "PF", ("", "") }, { "C",  ("", "") }
            };
            var posIds = new Dictionary<string, int>();
            var assigned = new HashSet<int>();

            foreach (string pos in positions)
            {
                var posPlayers = seasonStats
                    .Where(s =>
                    {
                        if (assigned.Contains(s.player_id)) return false;
                        var p = GetPlayerById(s.player_id);
                        return p != null && (p.position == pos || (!string.IsNullOrEmpty(p.secondary_position) && p.secondary_position == pos));
                    })
                    .ToList();
                if (posPlayers.Count == 0) continue;

                var qualified = posPlayers.Where(x => x.games >= 65).ToList();
                if (qualified.Count == 0) qualified = posPlayers;
                var best = qualified.OrderByDescending(x => (double)x.total_rating / x.games).First();
                assigned.Add(best.player_id);
                var player = GetPlayerById(best.player_id);
                var team = player != null ? GetTeamById(player.team_id) : null;
                string fullName = player != null ? $"{player.first_name} {player.last_name}" : "";
                string teamKw = team?.logo ?? "";
                posValues[pos] = (fullName, teamKw);
                posIds[pos] = best.player_id;
            }

            // Primer quinteto en season_records (para la trayectoria del jugador).
            seasonRecord.first_team_pg = posIds.GetValueOrDefault("PG");
            seasonRecord.first_team_sg = posIds.GetValueOrDefault("SG");
            seasonRecord.first_team_sf = posIds.GetValueOrDefault("SF");
            seasonRecord.first_team_pf = posIds.GetValueOrDefault("PF");
            seasonRecord.first_team_c  = posIds.GetValueOrDefault("C");

            // DPOY y Sexto Hombre también en season_records (SEA-11: la trayectoria del
            // jugador no los mostraba). All-Star y 2º quinteto siguen sin persistir.
            seasonRecord.best_defender_id = GetBestDefensivePlayer(seasonId, managerId)?.PlayerId ?? 0;
            seasonRecord.sixth_man_id = GetSixthMan(seasonId, managerId)?.PlayerId ?? 0;

            _db.Insert(new QuintetRecord
            {
                season = seasonLabel,
                pg = posValues["PG"].name,
                pg_team = posValues["PG"].team,
                sg = posValues["SG"].name,
                sg_team = posValues["SG"].team,
                sf = posValues["SF"].name,
                sf_team = posValues["SF"].team,
                pf = posValues["PF"].name,
                pf_team = posValues["PF"].team,
                c = posValues["C"].name,
                c_team = posValues["C"].team
            });
            Debug.Log($"[DB] QuintetRecord saved for {seasonLabel}");
        }

        // Persistir el season_records enriquecido (campeón, MVP, ROY, quinteto...).
        _db.Update(seasonRecord);

        Debug.Log($"[DB] Season-end records complete for {seasonLabel}");
    }
}
