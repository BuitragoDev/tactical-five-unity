using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public static class ScheduleGenerator
{
    // Genera los 82 partidos por equipo para la temporada regular
    public static int GenerateSchedule(SeasonData season, List<TeamData> teams)
    {
        var east = teams.Where(t => t.conference == "East").OrderBy(t => t.division).ThenBy(t => t.id).ToList();
        var west = teams.Where(t => t.conference == "West").OrderBy(t => t.division).ThenBy(t => t.id).ToList();

        var eastDivs = east.GroupBy(t => t.division).ToDictionary(g => g.Key, g => g.ToList());
        var westDivs = west.GroupBy(t => t.division).ToDictionary(g => g.Key, g => g.ToList());

        var matchups = new List<(int home, int away)>();

        // Partidos dentro de cada conferencia entre divisiones
        foreach (var divDict in new[] { eastDivs, westDivs })
        {
            var divNames = divDict.Keys.OrderBy(k => k).ToList();

            // Partidos dentro de la misma división: 4 por pareja, 2 en cada campo.
            foreach (var div in divDict.Values)
            {
                for (int i = 0; i < div.Count; i++)
                for (int j = i + 1; j < div.Count; j++)
                {
                    matchups.Add((div[i].id, div[j].id));
                    matchups.Add((div[i].id, div[j].id));
                    matchups.Add((div[j].id, div[i].id));
                    matchups.Add((div[j].id, div[i].id));
                }
            }

            // Partidos entre divisiones de la misma conferencia: 3 o 4 por pareja,
            // con localías equilibradas por construcción.
            for (int i = 0; i < divNames.Count; i++)
            for (int j = i + 1; j < divNames.Count; j++)
                AddDivPairGames(matchups, divDict[divNames[i]], divDict[divNames[j]]);
        }

        // Partidos entre conferencias: 2 por pareja, uno en cada campo.
        foreach (var t1 in east)
        foreach (var t2 in west)
        {
            matchups.Add((t1.id, t2.id));
            matchups.Add((t2.id, t1.id));
        }

        // Verificar 82 partidos y 41 localías por equipo.
        var gameCount = new Dictionary<int, int>();
        var homeCount = new Dictionary<int, int>();
        var awayCount = new Dictionary<int, int>();
        foreach (var t in teams)
        {
            gameCount[t.id] = 0;
            homeCount[t.id] = 0;
            awayCount[t.id] = 0;
        }
        foreach (var (h, a) in matchups)
        {
            gameCount[h]++;
            gameCount[a]++;
            homeCount[h]++;
            awayCount[a]++;
        }
        foreach (var t in teams)
        {
            if (gameCount[t.id] != 82)
                Debug.LogWarning($"[Schedule] {t.name} tiene {gameCount[t.id]} partidos, se esperaban 82");
            if (homeCount[t.id] != 41 || awayCount[t.id] != 41)
                Debug.LogWarning($"[Schedule] {t.name} tiene {homeCount[t.id]}-{awayCount[t.id]} en casa/fuera, se esperaba 41-41");
        }

        // Distribuir partidos en días
        var seasonStart = new DateTime(season.year_start, 10, 22);
        var seasonEnd   = new DateTime(season.year_end,   4, 15);
        int totalDays   = (int)(seasonEnd - seasonStart).TotalDays + 1;

        // Shuffle
        matchups = matchups.OrderBy(_ => Random.value).ToList();

        var teamDaySet      = teams.ToDictionary(t => t.id, _ => new HashSet<int>());
        var dayGamesCount   = new Dictionary<int, int>();
        var scheduled       = new List<(int day, int home, int away)>();
        var unplaced        = new List<(int home, int away)>();

        // Cada partido es una entrada individual: no se deduplica por pareja
        // orientada (dos partidos idénticos son dos partidos distintos).
        foreach (var (homePk, awayPk) in matchups)
        {
            int day = FindDay(homePk, awayPk, totalDays, teamDaySet, dayGamesCount, seasonStart);
            if (day >= 0)
            {
                scheduled.Add((day, homePk, awayPk));
                teamDaySet[homePk].Add(day);
                teamDaySet[awayPk].Add(day);
                dayGamesCount[day] = dayGamesCount.GetValueOrDefault(day, 0) + 1;
            }
            else
            {
                unplaced.Add((homePk, awayPk));
            }
        }

        // Partidos que el greedy no pudo colocar: forzar en el primer día libre.
        // Si aun así no caben, se registran y la generación falla (no se guarda).
        var stillUnplaced = new List<(int home, int away)>();
        foreach (var (homePk, awayPk) in unplaced)
        {
            bool placed = false;
            for (int d = 0; d < totalDays; d++)
            {
                var date = seasonStart.AddDays(d);
                if (date.Month == 2 && date.Day >= 8 && date.Day <= 14)
                    continue;

                if (!teamDaySet[homePk].Contains(d) && !teamDaySet[awayPk].Contains(d))
                {
                    scheduled.Add((d, homePk, awayPk));
                    teamDaySet[homePk].Add(d);
                    teamDaySet[awayPk].Add(d);
                    dayGamesCount[d] = dayGamesCount.GetValueOrDefault(d, 0) + 1;
                    placed = true;
                    break;
                }
            }
            if (!placed) stillUnplaced.Add((homePk, awayPk));
        }

        if (unplaced.Count > 0)
        {
            Debug.LogWarning($"[Schedule] El greedy no pudo colocar {unplaced.Count} partido(s); " +
                             $"{stillUnplaced.Count} siguen sin día tras el respaldo.");
        }
        if (stillUnplaced.Count > 0)
        {
            foreach (var (homePk, awayPk) in stillUnplaced.Take(20))
                Debug.LogError($"[Schedule] Partido sin programar: {homePk} vs {awayPk}");
            Debug.LogError("[Schedule] Calendario incompleto: no se guardará ni se marcará la temporada como generada.");
            return -1;
        }

        // Validación final por construcción: 1.230 partidos, 82 por equipo y 41-41.
        int expectedGames = teams.Count * 82 / 2;
        if (scheduled.Count != expectedGames)
        {
            Debug.LogError($"[Schedule] Se programaron {scheduled.Count} partidos; se esperaban {expectedGames}.");
            return -1;
        }

        var finalCount = teams.ToDictionary(t => t.id, _ => 0);
        var finalHome = teams.ToDictionary(t => t.id, _ => 0);
        var finalAway = teams.ToDictionary(t => t.id, _ => 0);
        foreach (var (_, homePk, awayPk) in scheduled)
        {
            finalCount[homePk]++;
            finalCount[awayPk]++;
            finalHome[homePk]++;
            finalAway[awayPk]++;
        }
        bool invalid = false;
        foreach (var t in teams)
        {
            if (finalCount[t.id] != 82 || finalHome[t.id] != 41 || finalAway[t.id] != 41)
            {
                Debug.LogError($"[Schedule] {t.name}: {finalCount[t.id]} partidos, " +
                               $"{finalHome[t.id]}-{finalAway[t.id]} casa/fuera; se esperaba 82 y 41-41.");
                invalid = true;
            }
        }
        if (invalid)
        {
            Debug.LogError("[Schedule] Calendario inválido: no se guarda ni se marca la temporada como generada.");
            return -1;
        }

        // Crear objetos GameData y guardar en BD
        var games = new List<GameData>();
        foreach (var (dayIdx, homePk, awayPk) in scheduled)
        {
            var gameDate = seasonStart.AddDays(dayIdx);
            games.Add(new GameData
            {
                season_id    = season.id,
                manager_id   = season.manager_id,
                game_day     = dayIdx + 1,
                game_date    = gameDate.ToString("yyyy-MM-dd"),
                home_team_id = homePk,
                away_team_id = awayPk,
                is_played    = 0,
                game_type    = "regular",
                series_label = "",
                home_score   = 0,
                away_score   = 0,
                is_home      = 0
            });
        }

        // Si el guardado falla, devolvemos -1 para que el llamador NO marque la
        // temporada como generada (de lo contrario quedaría sin partidos).
        if (!DatabaseManager.Instance.SaveRegularSeasonGames(games))
        {
            Debug.LogError("[Schedule] No se pudo guardar el calendario de temporada regular.");
            return -1;
        }
        Debug.Log($"[Schedule] {games.Count} partidos de temporada regular generados.");

        // All-Star Game (sábado de la segunda semana de febrero)
        var feb8 = new DateTime(season.year_start + 1, 2, 8);
        int daysToSat = ((int)DayOfWeek.Saturday - (int)feb8.DayOfWeek + 7) % 7;
        var allStarDate = feb8.AddDays(daysToSat);
        int allStarDay = (int)(allStarDate - seasonStart).TotalDays;

        var allStarGame = new GameData
        {
            season_id    = season.id,
            manager_id   = season.manager_id,
            game_day     = allStarDay + 1,
            game_date    = allStarDate.ToString("yyyy-MM-dd"),
            home_team_id = -1,
            away_team_id = -2,
            is_played    = 0,
            game_type    = "allstar",
            series_label = "",
            home_score   = 0,
            away_score   = 0,
            is_home      = 0
        };
        DatabaseManager.Instance.SaveRegularSeasonGames(new List<GameData> { allStarGame });
        Debug.Log($"[Schedule] All-Star Game creado: {allStarDate:yyyy-MM-dd}");

        return games.Count;
    }

    /// <summary>
    /// Cruces entre dos divisiones de la misma conferencia: 3 o 4 partidos por pareja.
    /// Los de 4 se reparten 2-2; los de 3 se orientan para que cada equipo sume
    /// exactamente 9 localías en el cruce (18 partidos), sin depender del azar.
    /// </summary>
    static void AddDivPairGames(List<(int home, int away)> matchups,
        List<TeamData> teamsA, List<TeamData> teamsB)
    {
        int n = Math.Min(teamsA.Count, teamsB.Count);
        if (n <= 0) return;

        for (int i = 0; i < teamsA.Count; i++)
        for (int j = 0; j < teamsB.Count; j++)
        {
            int a = teamsA[i].id;
            int b = teamsB[j].id;
            int offset = ((j - i) % n + n) % n;

            if (offset < n - 2)
            {
                // 4 partidos: dos en cada campo.
                matchups.Add((a, b));
                matchups.Add((a, b));
                matchups.Add((b, a));
                matchups.Add((b, a));
            }
            else if (offset == n - 2)
            {
                // 3 partidos: A recibe dos en casa y B uno.
                matchups.Add((a, b));
                matchups.Add((a, b));
                matchups.Add((b, a));
            }
            else
            {
                // 3 partidos: B recibe dos en casa y A uno.
                matchups.Add((a, b));
                matchups.Add((b, a));
                matchups.Add((b, a));
            }
        }
    }

    static int FindDay(int homePk, int awayPk, int totalDays,
        Dictionary<int, HashSet<int>> teamDaySet,
        Dictionary<int, int> dayGamesCount,
        DateTime seasonStart)
    {
        int bestDay   = -1;
        int bestScore = -1000;

        for (int d = 0; d < totalDays; d++)
        {
            var date = seasonStart.AddDays(d);
            if (date.Month == 2 && date.Day >= 8 && date.Day <= 14)
                continue;

            if (teamDaySet[homePk].Contains(d) || teamDaySet[awayPk].Contains(d))
                continue;
            if (dayGamesCount.GetValueOrDefault(d, 0) >= 15)
                continue;

            int hWeek = TeamWeekCount(homePk, d, teamDaySet);
            int aWeek = TeamWeekCount(awayPk, d, teamDaySet);
            if (hWeek >= 5 || aWeek >= 5) continue;

            int score = 100;
            if (HasB2B(homePk, d, teamDaySet)) score -= 50;
            if (HasB2B(awayPk, d, teamDaySet)) score -= 50;
            if (hWeek >= 4 || aWeek >= 4) score -= 30;
            score -= dayGamesCount.GetValueOrDefault(d, 0) * 2;

            if (score > bestScore) { bestScore = score; bestDay = d; }
        }
        return bestDay;
    }

    static int TeamWeekCount(int teamPk, int dayIdx, Dictionary<int, HashSet<int>> teamDaySet)
    {
        int week = dayIdx / 7;
        return teamDaySet[teamPk].Count(d => d / 7 == week);
    }

    static bool HasB2B(int teamPk, int dayIdx, Dictionary<int, HashSet<int>> teamDaySet)
        => teamDaySet[teamPk].Contains(dayIdx - 1) || teamDaySet[teamPk].Contains(dayIdx + 1);
}