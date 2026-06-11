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
            for (int i = 0; i < divNames.Count; i++)
            for (int j = i + 1; j < divNames.Count; j++)
                matchups.AddRange(DivPairGames(divDict[divNames[i]], divDict[divNames[j]]));

            // Partidos dentro de la misma división (4 veces)
            foreach (var div in divDict.Values)
            {
                for (int i = 0; i < div.Count; i++)
                for (int j = i + 1; j < div.Count; j++)
                    for (int k = 0; k < 4; k++)
                        matchups.Add(Random.value < 0.5f
                            ? (div[i].id, div[j].id)
                            : (div[j].id, div[i].id));
            }
        }

        // Partidos entre conferencias (2 veces cada cruce)
        foreach (var t1 in east)
        foreach (var t2 in west)
            for (int k = 0; k < 2; k++)
                matchups.Add(Random.value < 0.5f
                    ? (t1.id, t2.id)
                    : (t2.id, t1.id));

        // Verificar 82 partidos por equipo
        var gameCount = new Dictionary<int, int>();
        foreach (var t in teams) gameCount[t.id] = 0;
        foreach (var (h, a) in matchups)
        {
            gameCount[h]++;
            gameCount[a]++;
        }
        foreach (var t in teams)
        {
            if (gameCount[t.id] != 82)
                Debug.LogWarning($"[Schedule] {t.name} tiene {gameCount[t.id]} partidos, se esperaban 82");
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
        var scheduledPairs  = new HashSet<(int, int)>();

        foreach (var (homePk, awayPk) in matchups)
        {
            int day = FindDay(homePk, awayPk, totalDays, teamDaySet, dayGamesCount, seasonStart);
            if (day >= 0)
            {
                scheduled.Add((day, homePk, awayPk));
                scheduledPairs.Add((homePk, awayPk));
                teamDaySet[homePk].Add(day);
                teamDaySet[awayPk].Add(day);
                dayGamesCount[day] = dayGamesCount.GetValueOrDefault(day, 0) + 1;
            }
        }

        // Partidos no programados — forzar en primer día libre
        var unscheduled = matchups.Where(m => !scheduledPairs.Contains(m)).ToList();
        foreach (var (homePk, awayPk) in unscheduled)
        {
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
                    break;
                }
            }
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

        DatabaseManager.Instance.SaveRegularSeasonGames(games);
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
            home_team_id = 0,
            away_team_id = 0,
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

    static List<(int, int)> DivPairGames(List<TeamData> teamsA, List<TeamData> teamsB)
    {
        var result = new List<(int, int)>();
        int n = teamsA.Count;
        for (int i = 0; i < teamsA.Count; i++)
        for (int j = 0; j < teamsB.Count; j++)
        {
            int offset = ((j - i) % n + n) % n;
            int count  = offset < 3 ? 4 : 3;
            for (int k = 0; k < count; k++)
                result.Add(Random.value < 0.5f
                    ? (teamsA[i].id, teamsB[j].id)
                    : (teamsB[j].id, teamsA[i].id));
        }
        return result;
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