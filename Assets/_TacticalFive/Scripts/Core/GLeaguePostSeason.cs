using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Postemporada G-League: eliminatoria por conferencias (QF → SF → CF) más
/// Gran Final entre campeones de liga. Cada serie es al MEJOR DE 3 (el primero
/// que llegue a 2 victorias avanza; si queda 1-1 se disputa el 3º partido).
/// Todos los partidos de una serie comparten series_label; se generan de forma
/// incremental (partido 1, luego 2 si hace falta, luego 3 si queda 1-1),
/// fechados en el siguiente día con actividad. Competición PARALELA: nunca
/// modifica seasons.phase.
///
/// Convenciones de series_label:
///   gl-qf-east-1 … gl-qf-east-4   (cuartos; 1v8, 4v5, 2v7, 3v6)
///   gl-sf-east-a / gl-sf-east-b   (semis: ganadores qf1 vs qf2, qf3 vs qf4)
///   gl-cf-east / gl-cf-west       (final de conferencia)
///   gl-final                      (gran final Este vs Oeste)
/// </summary>
public static class GLeaguePostSeason
{
    public const string TYPE_PLAYOFF = "gleague_playoff";
    public const int TEAMS_PER_CONFERENCE = 8;
    public const int SERIES_WIN_NEEDED = 2; // best of 3 → primero a 2
    public const int SERIES_MAX_GAMES = 3;

    const string LBL_QF = "gl-qf";
    const string LBL_SF = "gl-sf";
    const string LBL_CF = "gl-cf";
    const string LBL_FINAL = "gl-final";

    static readonly string[] RoundOrder = { LBL_QF, LBL_SF, LBL_CF, LBL_FINAL };

    /// <summary>Punto de entrada diario. Debe llamarse DENTRO de la transacción
    /// del día, tras las transiciones de fase NBA.</summary>
    public static void AdvanceIfNeeded(int managerId, SeasonData season, int myNbaTeamId = 0)
    {
        if (season == null) return;
        var db = DatabaseManager.Instance;

        var glTeams = db.GetGLeagueTeams();
        if (glTeams.Count == 0) return;

        var glGames = db.GetAllGLeagueGames(managerId);

        // Sin competición GL esta temporada: no avisar cada día.
        if (!glGames.Any()) return;

        bool regularDone = !glGames.Any(g => g.game_type == GLeagueScheduleGenerator.TYPE_REGULAR && g.is_played == 0);
        if (!regularDone) return;

        var poGames = glGames.Where(g => g.game_type == TYPE_PLAYOFF).ToList();
        var regularGames = glGames.Where(g => g.game_type == GLeagueScheduleGenerator.TYPE_REGULAR).ToList();

        // Gran Final decidida → registrar campeón (idempotente).
        var finalSeries = SeriesGames(poGames, LBL_FINAL);
        if (finalSeries.Any() && SeriesDecided(finalSeries))
        {
            RecordChampionIfNeeded(season, managerId, finalSeries, glTeams, myNbaTeamId);
            return;
        }

        // Sin bracket y liga regular terminada → crear cuartos de final.
        if (poGames.Count == 0)
        {
            GenerateRoundNextGames(season, managerId, glTeams, poGames, regularGames, LBL_QF);
            return;
        }

        // Avanzar la ronda en curso (generar el siguiente partido pendiente).
        string round = NextRoundToAdvance(poGames);
        if (round == null) return;
        GenerateRoundNextGames(season, managerId, glTeams, poGames, regularGames, round);
    }

    /// <summary>Ronda que debe avanzarse: la primera con series sin decidir, o la
    /// siguiente por crear cuando la actual ya está cerrada. null si no hay nada
    /// pendiente (la Gran Final ya está decidida).</summary>
    static string NextRoundToAdvance(List<GameData> poGames)
    {
        foreach (var prefix in RoundOrder)
        {
            if (!poGames.Any(g => g.series_label.StartsWith(prefix)))
                return prefix; // ronda aún no creada
            if (!RoundDecided(poGames, prefix))
                return prefix; // ronda en curso sin decidir
        }
        return null;
    }

    static bool RoundDecided(List<GameData> poGames, string prefix)
    {
        var round = poGames.Where(g => g.series_label.StartsWith(prefix)).ToList();
        if (round.Count == 0) return false;
        return round.GroupBy(g => g.series_label).All(sg => SeriesDecided(sg.ToList()));
    }

    // ── GENERACIÓN DE PARTIDOS DE UNA RONDA ──────────────

    /// <summary>Genera el siguiente partido de cada serie de la ronda que lo
    /// necesite (no creada → partido 1; en curso y sin decidir → partido 2/3).</summary>
    static void GenerateRoundNextGames(SeasonData season, int managerId,
        List<GLeagueTeamData> glTeams, List<GameData> poGames, List<GameData> regularGames, string roundPrefix)
    {
        var seeds = ComputeRegularSeeds(glTeams,
            regularGames.Where(g => g.is_played == 1).ToList());

        var matchups = BuildRoundMatchups(roundPrefix, glTeams, poGames, regularGames);
        if (matchups.Count == 0) return;

        var newGames = new List<GameData>();
        foreach (var (a, b, label) in matchups)
        {
            var g = MakeNextSeriesGame(season, managerId, a, b, label, seeds, poGames);
            if (g != null) newGames.Add(g);
        }

        if (newGames.Count > 0)
            AssignDayAndSave(season, managerId, newGames, RoundLabel(roundPrefix));
    }

    /// <summary>Emparejamientos (a, b ordenados por mejor seed como posible local,
    /// label de serie) de una ronda, a partir de los ganadores de la ronda previa.</summary>
    static List<(int a, int b, string label)> BuildRoundMatchups(string roundPrefix,
        List<GLeagueTeamData> glTeams, List<GameData> poGames, List<GameData> regularGames)
    {
        var result = new List<(int, int, string)>();

        if (roundPrefix == LBL_QF)
        {
            var seeds = ComputeRegularSeeds(glTeams, regularGames.Where(g => g.is_played == 1).ToList());

            foreach (var conf in new[] { "East", "West" })
            {
                var confTeams = glTeams.Where(t => t.conference == conf).ToList();
                var ranked = seeds.Where(kv => kv.Value.seed > 0 && confTeams.Any(t => t.id == kv.Key))
                    .OrderBy(kv => kv.Value.seed)
                    .Take(TEAMS_PER_CONFERENCE)
                    .ToList();

                if (ranked.Count < TEAMS_PER_CONFERENCE)
                {
                    Debug.LogWarning($"[GLeague] {conf}: solo {ranked.Count} equipos elegibles para playoffs.");
                    continue;
                }

                int[][] pairs =
                {
                    new[] { 0, 7 }, // 1 v 8
                    new[] { 3, 4 }, // 4 v 5
                    new[] { 1, 6 }, // 2 v 7
                    new[] { 2, 5 }, // 3 v 6
                };

                for (int i = 0; i < pairs.Length; i++)
                {
                    var higher = ranked[pairs[i][0]];
                    var lower = ranked[pairs[i][1]];
                    result.Add((higher.Key, lower.Key, $"{LBL_QF}-{conf.ToLower()}-{i + 1}"));
                }
            }
            return result;
        }

        if (roundPrefix == LBL_SF)
        {
            foreach (var conf in new[] { "East", "West" })
            {
                for (int band = 1; band <= 2; band++)
                {
                    var w1 = SeriesWinner(SeriesGames(poGames, $"{LBL_QF}-{conf.ToLower()}-{(band - 1) * 2 + 1}"));
                    var w2 = SeriesWinner(SeriesGames(poGames, $"{LBL_QF}-{conf.ToLower()}-{(band - 1) * 2 + 2}"));
                    if (w1 == 0 || w2 == 0) continue;
                    string suffix = band == 1 ? "a" : "b";
                    result.Add((w1, w2, $"{LBL_SF}-{conf.ToLower()}-{suffix}"));
                }
            }
            return result;
        }

        if (roundPrefix == LBL_CF)
        {
            foreach (var conf in new[] { "East", "West" })
            {
                var w1 = SeriesWinner(SeriesGames(poGames, $"{LBL_SF}-{conf.ToLower()}-a"));
                var w2 = SeriesWinner(SeriesGames(poGames, $"{LBL_SF}-{conf.ToLower()}-b"));
                if (w1 == 0 || w2 == 0) continue;
                result.Add((w1, w2, $"{LBL_CF}-{conf.ToLower()}"));
            }
            return result;
        }

        if (roundPrefix == LBL_FINAL)
        {
            var east = SeriesWinner(SeriesGames(poGames, $"{LBL_CF}-east"));
            var west = SeriesWinner(SeriesGames(poGames, $"{LBL_CF}-west"));
            if (east == 0 || west == 0) return result;
            result.Add((east, west, LBL_FINAL));
        }

        return result;
    }

    /// <summary>Devuelve el siguiente partido de la serie (o null si no procede).
    /// El mejor seed es local en los partidos impares (1 y 3); el peor en el 2.</summary>
    static GameData MakeNextSeriesGame(SeasonData season, int managerId, int a, int b,
        string label, Dictionary<int, (int seed, float winPct)> seeds, List<GameData> poGames)
    {
        var series = SeriesGames(poGames, label);

        // Serie no creada → partido 1 (local el mejor seed).
        if (series.Count == 0)
            return MakeGameBetterSeedHome(season, managerId, a, b, label, seeds);

        if (SeriesDecided(series)) return null; // serie resuelta; no más partidos

        int homeId = GLeagueHelper.DecodeGlTeamId(series[0].home_team_id); // mejor seed (local del G1)
        int awayId = GLeagueHelper.DecodeGlTeamId(series[0].away_team_id);
        int nextNum = series.Count + 1;

        int home = (nextNum % 2 == 1) ? homeId : awayId;   // 1,3 → mejor; 2 → peor
        int away = (nextNum % 2 == 1) ? awayId : homeId;
        return MakeGame(season, managerId, home, away, label);
    }

    // ── LÓGICA DE SERIES (mejor de 3) ────────────────────

    static List<GameData> SeriesGames(List<GameData> poGames, string label)
        => poGames.Where(g => g.series_label == label).ToList();

    /// <summary>true si alguna de las dos filiales ya tiene SERIES_WIN_NEEDED
    /// victorias (o se han jugado los máximos partidos).</summary>
    static bool SeriesDecided(List<GameData> series)
    {
        if (series.Count == 0) return false;
        int homeId = GLeagueHelper.DecodeGlTeamId(series[0].home_team_id);
        int awayId = GLeagueHelper.DecodeGlTeamId(series[0].away_team_id);
        return DecideWinner(series, homeId, awayId) != 0;
    }

    /// <summary>Equipo ganador de la serie (id de filial sin codificar), o 0.</summary>
    static int SeriesWinner(List<GameData> series)
    {
        if (series == null || series.Count == 0) return 0;
        int homeId = GLeagueHelper.DecodeGlTeamId(series[0].home_team_id);
        int awayId = GLeagueHelper.DecodeGlTeamId(series[0].away_team_id);
        return DecideWinner(series, homeId, awayId);
    }

    static int DecideWinner(List<GameData> series, int homeId, int awayId)
    {
        int wh = 0, wa = 0;
        foreach (var g in series)
        {
            if (g.is_played != 1) continue;
            int w = WinnerOf(g);
            if (w == homeId) wh++;
            else if (w == awayId) wa++;
        }
        if (wh >= SERIES_WIN_NEEDED) return homeId;
        if (wa >= SERIES_WIN_NEEDED) return awayId;
        return 0;
    }

    /// <summary>Parcial de la serie como "2-1" (victorias del equipo local y
    /// visitante de esta serie).</summary>
    public static string SeriesResult(List<GameData> series)
    {
        if (series == null || series.Count == 0) return "0-0";
        int homeId = GLeagueHelper.DecodeGlTeamId(series[0].home_team_id);
        int awayId = GLeagueHelper.DecodeGlTeamId(series[0].away_team_id);
        int wh = 0, wa = 0;
        foreach (var g in series)
        {
            if (g.is_played != 1) continue;
            int w = WinnerOf(g);
            if (w == homeId) wh++;
            else if (w == awayId) wa++;
        }
        return $"{wh}-{wa}";
    }

    /// <summary>Parcial de la serie "victorias del equipo dado - victorias del rival".</summary>
    public static string SeriesScoreForTeam(List<GameData> series, int teamId)
    {
        if (series == null || series.Count == 0) return "0-0";
        int homeId = GLeagueHelper.DecodeGlTeamId(series[0].home_team_id);
        int awayId = GLeagueHelper.DecodeGlTeamId(series[0].away_team_id);
        int otherId = homeId == teamId ? awayId : homeId;
        int wins = 0, otherWins = 0;
        foreach (var g in series)
        {
            if (g.is_played != 1) continue;
            int w = WinnerOf(g);
            if (w == teamId) wins++;
            else if (w == otherId) otherWins++;
        }
        return $"{wins}-{otherWins}";
    }

    /// <summary>Primer día futuro con partidos pendientes (cualquier competición)
    /// para enganchar la ronda al pipeline diario; fallback día+1.</summary>
    public static int PickNextPendingDay(int managerId, int minDayExclusive)
    {
        var days = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.is_played == 0 && g.game_day > minDayExclusive)
            .Select(g => g.game_day)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
        return days.Count > 0 ? days[0] : minDayExclusive + 1;
    }

    // ── UTILIDADES ───────────────────────────────────────

    static GameData MakeGame(SeasonData season, int managerId, int homeTeamId, int awayTeamId, string label)
    {
        return new GameData
        {
            season_id    = season.id,
            manager_id   = season.manager_id,
            game_day     = 0,
            game_date    = "",
            home_team_id = GLeagueHelper.EncodeGlTeamId(homeTeamId),
            away_team_id = GLeagueHelper.EncodeGlTeamId(awayTeamId),
            is_played    = 0,
            game_type    = TYPE_PLAYOFF,
            series_label = label,
            home_score   = 0,
            away_score   = 0,
            is_home      = 0
        };
    }

    static GameData MakeGameBetterSeedHome(SeasonData season, int managerId, int teamA, int teamB,
        string label, Dictionary<int, (int seed, float winPct)> seeds)
    {
        float pctA = seeds.TryGetValue(teamA, out var sa) ? sa.winPct : 0f;
        float pctB = seeds.TryGetValue(teamB, out var sb) ? sb.winPct : 0f;
        // Local para el mejor registro de liga regular; desempate estable por id
        bool aHome = pctA > pctB || (Mathf.Approximately(pctA, pctB) && teamA <= teamB);
        return MakeGame(season, managerId, aHome ? teamA : teamB, aHome ? teamB : teamA, label);
    }

    /// <summary>Persiste una tanda de partidos, fechados todos en el siguiente día
    /// libre.</summary>
    static void AssignDayAndSave(SeasonData season, int managerId, List<GameData> games, string roundName)
    {
        if (games.Count == 0) return;

        int gameDay = PickNextPendingDay(managerId, LastScheduledGlDay(managerId));
        var seasonStart = new DateTime(season.year_start, 10, 22);
        string dateStr = seasonStart.AddDays(gameDay - 1).ToString("yyyy-MM-dd");

        foreach (var g in games)
        {
            g.game_day = gameDay;
            g.game_date = dateStr;
        }

        DatabaseManager.Instance.SaveGLeagueGames(games);
        Debug.Log($"[GLeague] {roundName} generados ({games.Count} partidos) el {dateStr}.");
    }

    static int LastScheduledGlDay(int managerId)
    {
        var glDays = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId
                     && (g.game_type == GLeagueScheduleGenerator.TYPE_REGULAR || g.game_type == TYPE_PLAYOFF))
            .Select(g => g.game_day)
            .ToList();
        return glDays.Count > 0 ? glDays.Max() : 0;
    }

    static string RoundLabel(string prefix)
    {
        switch (prefix)
        {
            case LBL_QF: return "cuartos de final";
            case LBL_SF: return "semifinales";
            case LBL_CF: return "finales de conferencia";
            case LBL_FINAL: return "la Gran Final";
            default: return prefix;
        }
    }

    /// <summary>Ganador de una eliminatoria (ids de filial SIN codificar).</summary>
    static int WinnerOf(GameData g)
    {
        if (g == null) return 0;
        return g.home_score >= g.away_score
            ? GLeagueHelper.DecodeGlTeamId(g.home_team_id)
            : GLeagueHelper.DecodeGlTeamId(g.away_team_id);
    }

    static void RecordChampionIfNeeded(SeasonData season, int managerId, List<GameData> finalSeries,
        List<GLeagueTeamData> glTeams, int myNbaTeamId)
    {
        var db = DatabaseManager.Instance;
        if (db.GetGLeagueChampions(managerId).Any(c => c.season_id == season.id)) return;

        int championId = SeriesWinner(finalSeries);
        var championTeam = glTeams.FirstOrDefault(t => t.id == championId);
        if (championTeam == null) return;

        db.SaveGLeagueChampion(new GLeagueChampionData
        {
            manager_id = managerId,
            season_id = season.id,
            season = $"{season.year_end}",
            gleague_team_id = championTeam.id,
            team_name = championTeam.name
        });

        string partial = SeriesResult(finalSeries);
        // "2-1" → victorias del campeón y del finalista
        int champWins = CountTeamWins(finalSeries, championId);
        int finalistId = GLeagueHelper.DecodeGlTeamId(finalSeries[0].home_team_id) == championId
            ? GLeagueHelper.DecodeGlTeamId(finalSeries[0].away_team_id)
            : GLeagueHelper.DecodeGlTeamId(finalSeries[0].home_team_id);
        int finalistWins = CountTeamWins(finalSeries, finalistId);
        string score = $"{champWins}-{finalistWins}";

        var finalistTeam = glTeams.FirstOrDefault(t => t.id == finalistId);
        string finalistName = finalistTeam?.name ?? "su rival";

        bool isMyAffiliate = myNbaTeamId > 0 && championTeam.nba_team_id == myNbaTeamId;
        db.AddMessage(new MessageData
        {
            manager_id = managerId,
            sender_type = 2,
            sender_id = 0,
            title = isMyAffiliate
                ? $"¡{championTeam.name}, tu filial, campeón de la G-League!"
                : $"{championTeam.name} campeón de la G-League",
            body = isMyAffiliate
                ? $"Tu filial {championTeam.name} se ha proclamado campeona de la G-League tras vencer a {finalistName} en la Gran Final por {score}. El proyecto de desarrollo da sus frutos."
                : $"{championTeam.name} ha vencido a {finalistName} en la Gran Final de la G-League por {score}.",
            game_day = season.current_game_day,
            game_date = season.current_date ?? "",
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        });
    }

    static int CountTeamWins(List<GameData> series, int teamId)
        => series.Count(g => g.is_played == 1 && WinnerOf(g) == teamId);

    /// <summary>Diccionario teamId → (seed dentro de su conferencia, Win% global).</summary>
    public static Dictionary<int, (int seed, float winPct)> ComputeRegularSeeds(
        List<GLeagueTeamData> glTeams, List<GameData> regularGames)
    {
        var result = new Dictionary<int, (int seed, float winPct)>();
        foreach (var conf in new[] { "East", "West" })
        {
            var confTeams = glTeams.Where(t => t.conference == conf).ToList();
            var table = GLeagueStandings.Compute(confTeams, regularGames);
            for (int i = 0; i < table.Count; i++)
                result[table[i].teamId] = (i + 1, table[i].WinPct);
        }
        return result;
    }

    // ── BACKFILL / AUTO-COMPLETADO ───────────────────────

    /// <summary>
    /// Genera y simula la postemporada completa de G-League en una sola pasada
    /// para que SIEMPRE exista un campeón al terminar la liga regular, sin
    /// depender de que el bucle diario llegue a procesar cada ronda. Red de
    /// seguridad (p.ej. al abrir SeasonSummary). NO toca seasons.phase.
    /// </summary>
    public static void CompletePostSeason(SeasonData season, int managerId)
    {
        if (season == null) return;
        var db = DatabaseManager.Instance;

        var glTeams = db.GetGLeagueTeams();
        if (glTeams.Count == 0) return;

        var glGames = db.GetAllGLeagueGames(managerId);
        if (!glGames.Any()) return;

        bool regularDone = !glGames.Any(g => g.game_type == GLeagueScheduleGenerator.TYPE_REGULAR && g.is_played == 0);
        if (!regularDone) return;

        if (db.GetGLeagueChampions(managerId).Any(c => c.season_id == season.id)) return;

        var regularGames = glGames.Where(g => g.game_type == GLeagueScheduleGenerator.TYPE_REGULAR).ToList();
        var allProspects = db.GetAllGLeaguePlayers();
        var assignedByNba = db.GetGLeagueAssignedByTeam();

        int guard = 0;
        while (guard++ < 40)
        {
            var poGames = db.GetAllGLeagueGames(managerId).Where(g => g.game_type == TYPE_PLAYOFF).ToList();

            var finalSeries = SeriesGames(poGames, LBL_FINAL);
            if (finalSeries.Any() && SeriesDecided(finalSeries))
            {
                RecordChampionIfNeeded(season, managerId, finalSeries, glTeams, 0);
                return;
            }

            if (poGames.Count == 0)
                GenerateRoundNextGames(season, managerId, glTeams, poGames, regularGames, LBL_QF);
            else
            {
                string round = NextRoundToAdvance(poGames);
                if (round == null) return;
                GenerateRoundNextGames(season, managerId, glTeams, poGames, regularGames, round);
            }

            var pending = db.GetAllGLeagueGames(managerId)
                .Where(g => g.game_type == TYPE_PLAYOFF && g.is_played == 0)
                .ToList();
            if (pending.Count > 0)
                SimulatePlayoffGames(season, managerId, pending, allProspects, assignedByNba);
        }
    }

    /// <summary>Simula una lista de partidos de playoffs G-League (sin tocar stats
    /// NBA, sin acumular gleague_season_stats — solo liga regular).</summary>
    static void SimulatePlayoffGames(SeasonData season, int managerId, List<GameData> games,
        List<GLeaguePlayerData> allProspects, Dictionary<int, List<PlayerData>> assignedByNba)
    {
        var db = DatabaseManager.Instance;
        foreach (var game in games)
        {
            var homeTeam = db.GetGLeagueTeam(GLeagueHelper.DecodeGlTeamId(game.home_team_id));
            var awayTeam = db.GetGLeagueTeam(GLeagueHelper.DecodeGlTeamId(game.away_team_id));
            if (homeTeam == null || awayTeam == null)
            {
                game.is_played = 1;
                db.UpdateGame(game);
                continue;
            }

            var homePlayers = GLeagueHelper.BuildAffiliateLineup(homeTeam, allProspects, assignedByNba);
            var awayPlayers = GLeagueHelper.BuildAffiliateLineup(awayTeam, allProspects, assignedByNba);

            var result = GameSimulator.SimulateGame(game, homePlayers, awayPlayers, 50, 50, false,
                persistToDb: false, glLeague: true);
            db.UpdateGame(game);
        }
    }
}
