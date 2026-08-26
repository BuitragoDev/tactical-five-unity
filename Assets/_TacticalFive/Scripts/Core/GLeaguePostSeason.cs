using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Postemporada G-League: eliminatoria directa por conferencias (QF → SF → CF)
/// más Gran Final entre campeones de conferencia. Cada ronda se genera cuando
/// la anterior está completa, fechada en el siguiente día con partidos
/// pendientes. Es una competición PARALELA: nunca modifica seasons.phase.
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

    const string LBL_QF = "gl-qf";
    const string LBL_SF = "gl-sf";
    const string LBL_CF = "gl-cf";
    const string LBL_FINAL = "gl-final";

    /// <summary>Punto de entrada diario. Debe llamarse DENTRO de la transacción
    /// del día, tras las transiciones de fase NBA.</summary>
    public static void AdvanceIfNeeded(int managerId, SeasonData season, int myNbaTeamId = 0)
    {
        if (season == null) return;
        var db = DatabaseManager.Instance;

        var glTeams = db.GetGLeagueTeams();
        if (glTeams.Count == 0) return;

        var glGames = db.GetAllGLeagueGames(managerId);

        // Sin competición GL esta temporada (slot antiguo con calendario ya
        // generado antes de esta feature): no avisar cada día.
        if (!glGames.Any()) return;

        bool regularDone = !glGames.Any(g => g.game_type == GLeagueScheduleGenerator.TYPE_REGULAR && g.is_played == 0);
        if (!regularDone) return;

        var poGames = glGames.Where(g => g.game_type == TYPE_PLAYOFF).ToList();

        // Sin bracket y liga regular terminada → generar cuartos
        if (poGames.Count == 0)
        {
            GenerateQuarterfinals(season, managerId, glTeams,
                glGames.Where(g => g.game_type == GLeagueScheduleGenerator.TYPE_REGULAR).ToList());
            return;
        }

        // Ronda en curso → esperar a que termine
        if (poGames.Any(g => g.is_played == 0)) return;

        // Bracket completo y final jugada → registrar campeón (una sola vez)
        var finalGame = poGames.FirstOrDefault(g => g.series_label == LBL_FINAL && g.is_played == 1);
        if (finalGame != null)
        {
            RecordChampionIfNeeded(season, managerId, finalGame, glTeams, myNbaTeamId);
            return;
        }

        // Avanzar la siguiente ronda pendiente
        var seeds = ComputeRegularSeeds(glTeams,
            glGames.Where(g => g.game_type == GLeagueScheduleGenerator.TYPE_REGULAR && g.is_played == 1).ToList());
        int nextDay = PickNextPendingDay(managerId, poGames.Max(g => g.game_day));

        switch (NextMissingRound(poGames))
        {
            case "sf":
                GenerateSemifinals(season, managerId, poGames, seeds, nextDay);
                break;
            case "cf":
                GenerateConferenceFinals(season, managerId, poGames, seeds, nextDay);
                break;
            case "final":
                GenerateGrandFinal(season, managerId, poGames, seeds, nextDay);
                break;
        }
    }

    static string NextMissingRound(List<GameData> poGames)
    {
        var qf = poGames.Where(g => g.series_label.StartsWith(LBL_QF)).ToList();
        if (qf.Count == 0 || qf.Any(g => g.is_played == 0)) return null;
        if (!poGames.Any(g => g.series_label.StartsWith(LBL_SF))) return "sf";

        var sf = poGames.Where(g => g.series_label.StartsWith(LBL_SF)).ToList();
        if (sf.Any(g => g.is_played == 0)) return null;
        if (!poGames.Any(g => g.series_label.StartsWith(LBL_CF))) return "cf";

        var cf = poGames.Where(g => g.series_label.StartsWith(LBL_CF)).ToList();
        if (cf.Any(g => g.is_played == 0)) return null;
        return "final";
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

    // ── GENERADORES DE RONDAS ────────────────────────────

    static void GenerateQuarterfinals(SeasonData season, int managerId,
        List<GLeagueTeamData> glTeams, List<GameData> regularGames)
    {
        var seeds = ComputeRegularSeeds(glTeams, regularGames);

        var games = new List<GameData>();
        foreach (var conf in new[] { "East", "West" })
        {
            var ranked = seeds.Where(kv => kv.Value.seed > 0
                    && glTeams.First(t => t.id == kv.Key).conference == conf)
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
                var lower  = ranked[pairs[i][1]];
                games.Add(MakeGame(season, managerId, higher.Key, lower.Key,
                    $"{LBL_QF}-{conf.ToLower()}-{i + 1}"));
            }
        }

        if (games.Count == 0) return;
        AssignDateAndSave(season, managerId, games, "cuartos de final");
    }

    static void GenerateSemifinals(SeasonData season, int managerId, List<GameData> poGames,
        Dictionary<int, (int seed, float winPct)> seeds, int gameDay)
    {
        var games = new List<GameData>();
        foreach (var conf in new[] { "East", "West" })
        {
            var qfWinners = new List<int>();
            for (int slot = 1; slot <= 4; slot++)
            {
                var g = poGames.FirstOrDefault(x => x.series_label == $"{LBL_QF}-{conf.ToLower()}-{slot}");
                if (g == null) return; // falta algo; no avanzar a medias
                qfWinners.Add(WinnerOf(g));
            }

            // SF-a: ganador qf1 vs ganador qf2 · SF-b: ganador qf3 vs ganador qf4
            games.Add(MakeGameBetterSeedHome(season, managerId, qfWinners[0], qfWinners[1],
                $"{LBL_SF}-{conf.ToLower()}-a", seeds));
            games.Add(MakeGameBetterSeedHome(season, managerId, qfWinners[2], qfWinners[3],
                $"{LBL_SF}-{conf.ToLower()}-b", seeds));
        }

        AssignDateAndSave(season, managerId, games, "semifinales", gameDay);
    }

    static void GenerateConferenceFinals(SeasonData season, int managerId, List<GameData> poGames,
        Dictionary<int, (int seed, float winPct)> seeds, int gameDay)
    {
        var games = new List<GameData>();
        foreach (var conf in new[] { "East", "West" })
        {
            var winners = new List<int>();
            foreach (var suffix in new[] { "a", "b" })
            {
                var g = poGames.FirstOrDefault(x => x.series_label == $"{LBL_SF}-{conf.ToLower()}-{suffix}");
                if (g == null) return;
                winners.Add(WinnerOf(g));
            }

            games.Add(MakeGameBetterSeedHome(season, managerId, winners[0], winners[1],
                $"{LBL_CF}-{conf.ToLower()}", seeds));
        }

        AssignDateAndSave(season, managerId, games, "finales de conferencia", gameDay);
    }

    static void GenerateGrandFinal(SeasonData season, int managerId, List<GameData> poGames,
        Dictionary<int, (int seed, float winPct)> seeds, int gameDay)
    {
        var eastChamp = WinnerOf(poGames.FirstOrDefault(g => g.series_label == $"{LBL_CF}-east"));
        var westChamp = WinnerOf(poGames.FirstOrDefault(g => g.series_label == $"{LBL_CF}-west"));
        if (eastChamp == 0 || westChamp == 0) return;

        var games = new List<GameData>
        {
            MakeGameBetterSeedHome(season, managerId, eastChamp, westChamp, LBL_FINAL, seeds)
        };
        AssignDateAndSave(season, managerId, games, "la Gran Final", gameDay);
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

    /// <summary>Fija día y fecha comunes para toda la ronda y persiste.</summary>
    static void AssignDateAndSave(SeasonData season, int managerId, List<GameData> games,
        string roundName, int forcedDay = -1)
    {
        if (games.Count == 0) return;

        int gameDay = forcedDay > 0
            ? forcedDay
            : PickNextPendingDay(managerId, LastScheduledGlDay(managerId));

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

    /// <summary>Ganador de una eliminatoria (ids de filial SIN codificar).</summary>
    static int WinnerOf(GameData g)
    {
        if (g == null) return 0;
        return g.home_score >= g.away_score
            ? GLeagueHelper.DecodeGlTeamId(g.home_team_id)
            : GLeagueHelper.DecodeGlTeamId(g.away_team_id);
    }

    static void RecordChampionIfNeeded(SeasonData season, int managerId, GameData finalGame,
        List<GLeagueTeamData> glTeams, int myNbaTeamId)
    {
        var db = DatabaseManager.Instance;
        if (db.GetGLeagueChampions(managerId).Any(c => c.season_id == season.id)) return;

        int championId = WinnerOf(finalGame);
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
                ? $"Tu filial {championTeam.name} se ha proclamado campeona de la G-League tras ganar la Gran Final por {finalGame.home_score}-{finalGame.away_score}. El proyecto de desarrollo da sus frutos."
                : $"{championTeam.name} ha ganado la Gran Final de la G-League por {finalGame.home_score}-{finalGame.away_score}.",
            game_day = season.current_game_day,
            game_date = season.current_date ?? "",
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        });
    }

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
}
