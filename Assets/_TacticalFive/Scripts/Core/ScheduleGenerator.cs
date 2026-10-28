using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public static class ScheduleGenerator
{
    // Reintentos del reparto greedy: cada intento usa otra mezcla aleatoria y
    // mantiene las mismas reglas de descanso.
    const int MAX_ARRANGE_ATTEMPTS = 24;

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

        // F5-04: separar los 60 partidos de grupo de la NBA Cup antes de repartir
        // el resto del calendario. Se jugarán en las noches fijas de noviembre.
        bool fullLeague = teams.Count == 30
            && teams.GroupBy(t => new { t.conference, t.division }).All(g => g.Count() == 5);
        var cupGames = new List<(int home, int away, string group)>();
        if (fullLeague)
        {
            cupGames = TakeCupGames(matchups, teams);
            if (cupGames.Count != 60)
            {
                Debug.LogError($"[Schedule] NBA Cup: se seleccionaron {cupGames.Count} partidos de grupo; se esperaban 60.");
                return -1;
            }
        }

        var teamsById       = teams.ToDictionary(t => t.id);
        var cupDays         = CupHelper.GroupPlayDays(season);
        // Las noches de grupo solo bloquean a los equipos que juegan la Cup
        // (ya están en teamDaySet); el resto puede jugar partido regular.
        // Las eliminatorias sí bloquean el calendario regular completo.
        var blockedDayIndexes = new HashSet<int>(CupHelper.BlockedRegularDays(season).Select(d => d - 1));
        foreach (int groupDay in cupDays)
            blockedDayIndexes.Remove(groupDay - 1);
        var cupAssignments  = fullLeague ? AssignCupGames(cupGames, cupDays, teams) : new List<(int day, int home, int away, string group)>();
        if (fullLeague && cupAssignments.Count != 60)
        {
            Debug.LogError($"[Schedule] NBA Cup: no se pudieron colocar los 60 partidos en las noches de grupo.");
            return -1;
        }

        var cupGameLabels = new Dictionary<(int day, int home, int away), string>();
        foreach (var (dayIdx, homePk, awayPk, groupName) in cupAssignments)
            cupGameLabels[(dayIdx, homePk, awayPk)] = groupName;

        // El reparto greedy puede dejar uno o dos partidos sin hueco por una mala
        // mezcla inicial. Se reintenta con otra mezcla aleatoria sin relajar las
        // reglas de descanso; solo se rinde si ninguna ordenación cabe.
        List<(int day, int home, int away)> scheduled = new List<(int day, int home, int away)>();
        List<(int home, int away)> stillUnplaced = new List<(int home, int away)>();
        int attempts = fullLeague ? MAX_ARRANGE_ATTEMPTS : 1;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            var arrangement = TryArrangeRegularGames(
                matchups, cupAssignments, teams, totalDays, seasonStart, blockedDayIndexes);
            scheduled = arrangement.scheduled;
            stillUnplaced = arrangement.unplaced;
            if (stillUnplaced.Count == 0) break;
        }

        if (stillUnplaced.Count > 0 && fullLeague)
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

            var teamGames = scheduled
                .Where(g => g.home == t.id || g.away == t.id)
                .OrderBy(g => g.day)
                .ToList();
            int consecutive = 1;
            for (int i = 1; i < teamGames.Count; i++)
            {
                var previous = teamGames[i - 1];
                var current = teamGames[i];
                consecutive = current.day == previous.day + 1 ? consecutive + 1 : 1;
                if (consecutive > 2)
                {
                    Debug.LogError($"[Schedule] {t.name}: {consecutive} días consecutivos de partido; no se guarda el calendario.");
                    invalid = true;
                }

                int kilometers = TravelHelper.DistanceKilometers(previous.home, current.home, teamsById);
                if (TravelHelper.IsImpossibleBackToBack(previous.day, current.day, kilometers))
                {
                    Debug.LogError($"[Schedule] {t.name}: back-to-back {previous.day}->{current.day} " +
                                   $"de {kilometers:N0} km; no se guarda el calendario.");
                    invalid = true;
                }
            }

            // Máximo 4 partidos por semana natural (lunes-domingo).
            foreach (var week in teamGames.GroupBy(g => CalendarWeekKey(seasonStart, g.day)))
            {
                if (week.Count() > 4)
                {
                    Debug.LogError($"[Schedule] {t.name}: {week.Count()} partidos en una semana natural; no se guarda el calendario.");
                    invalid = true;
                    break;
                }
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
                series_label = cupGameLabels.TryGetValue((dayIdx, homePk, awayPk), out string groupName)
                    ? CupHelper.GroupLabelPrefix + groupName
                    : "",
                home_score   = 0,
                away_score   = 0,
                is_home      = 0
            });
        }

        // Los partidos de grupo ya se han colocado en sus noches de Cup.
        Debug.Log($"[Schedule] NBA Cup: {cupGameLabels.Count} partidos de grupo programados.");

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

    static List<(int home, int away, string group)> TakeCupGames(
        List<(int home, int away)> matchups, List<TeamData> teams)
    {
        var result = new List<(int home, int away, string group)>();
        foreach (var division in teams.GroupBy(t => new { t.conference, t.division }))
        {
            var clubs = division.OrderBy(t => t.id).ToList();
            if (clubs.Count != 5) continue;
            string groupName = CupHelper.GroupName(clubs[0]);

            foreach (var (homeIndex, awayIndex) in CupHelper.GroupPairings())
            {
                int index = matchups.FindIndex(m =>
                    m.home == clubs[homeIndex].id && m.away == clubs[awayIndex].id);
                if (index < 0)
                {
                    Debug.LogError($"[Schedule] NBA Cup: falta el partido {clubs[homeIndex].abbreviation}-" +
                                   $"{clubs[awayIndex].abbreviation} en {groupName}.");
                    return result;
                }
                var game = matchups[index];
                matchups.RemoveAt(index);
                result.Add((game.home, game.away, groupName));
            }
        }
        return result;
    }

    static List<(int day, int home, int away, string group)> AssignCupGames(
        List<(int home, int away, string group)> cupGames,
        List<int> cupDays, List<TeamData> teams)
    {
        var result = new List<(int day, int home, int away, string group)>();
        var teamNights = teams.ToDictionary(t => t.id, _ => new HashSet<int>());
        var nightCounts = new int[cupDays.Count];

        foreach (var game in cupGames)
        {
            int chosen = -1;
            for (int n = 0; n < cupDays.Count; n++)
            {
                if (teamNights[game.home].Contains(n) || teamNights[game.away].Contains(n))
                    continue;
                if (nightCounts[n] >= 15) continue;
                chosen = n;
                break;
            }
            if (chosen < 0)
            {
                Debug.LogError($"[Schedule] NBA Cup: no hay noche para {game.home}-{game.away}.");
                return result;
            }

            teamNights[game.home].Add(chosen);
            teamNights[game.away].Add(chosen);
            nightCounts[chosen]++;
            result.Add((cupDays[chosen] - 1, game.home, game.away, game.group));
        }
        return result;
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

    static (List<(int day, int home, int away)> scheduled, List<(int home, int away)> unplaced)
    TryArrangeRegularGames(
        List<(int home, int away)> matchups,
        List<(int day, int home, int away, string group)> cupAssignments,
        List<TeamData> teams,
        int totalDays,
        DateTime seasonStart,
        HashSet<int> blockedDayIndexes)
    {
        var teamDaySet      = teams.ToDictionary(t => t.id, _ => new HashSet<int>());
        var dayGamesCount   = new Dictionary<int, int>();
        var scheduled       = new List<(int day, int home, int away)>();
        var unplaced        = new List<(int home, int away)>();
        var teamsById       = teams.ToDictionary(t => t.id);
        var lastGameDay     = teams.ToDictionary(t => t.id, _ => -1);
        var lastVenue       = teams.ToDictionary(t => t.id, _ => 0);
        var venueByTeamDay  = new Dictionary<(int team, int day), int>();

        foreach (var (dayIdx, homePk, awayPk, _) in cupAssignments)
        {
            scheduled.Add((dayIdx, homePk, awayPk));
            teamDaySet[homePk].Add(dayIdx);
            teamDaySet[awayPk].Add(dayIdx);
            dayGamesCount[dayIdx] = dayGamesCount.GetValueOrDefault(dayIdx, 0) + 1;
            RecordGame(homePk, awayPk, dayIdx, lastGameDay, lastVenue, venueByTeamDay);
        }

        // Cada partido es una entrada individual: no se deduplica por pareja
        // orientada (dos partidos idénticos son dos partidos distintos).
        foreach (var (homePk, awayPk) in matchups.OrderBy(_ => Random.value))
        {
            int day = FindDay(homePk, awayPk, totalDays, teamDaySet, dayGamesCount,
                seasonStart, teamsById, lastGameDay, lastVenue, venueByTeamDay, blockedDayIndexes);
            if (day >= 0)
            {
                scheduled.Add((day, homePk, awayPk));
                teamDaySet[homePk].Add(day);
                teamDaySet[awayPk].Add(day);
                dayGamesCount[day] = dayGamesCount.GetValueOrDefault(day, 0) + 1;
                RecordGame(homePk, awayPk, day, lastGameDay, lastVenue, venueByTeamDay);
            }
            else
            {
                unplaced.Add((homePk, awayPk));
            }
        }

        var stillUnplaced = new List<(int home, int away)>();
        foreach (var (homePk, awayPk) in unplaced)
        {
            bool placed = false;
            for (int d = 0; d < totalDays; d++)
            {
                var date = seasonStart.AddDays(d);
                if (date.Month == 2 && date.Day >= 8 && date.Day <= 14)
                    continue;
                if (blockedDayIndexes.Contains(d))
                    continue;

                if (!teamDaySet[homePk].Contains(d) && !teamDaySet[awayPk].Contains(d)
                    && CanPlayOnDay(homePk, d, teamDaySet)
                    && CanPlayOnDay(awayPk, d, teamDaySet)
                    && !ImpossibleBackToBack(homePk, d, homePk, teamsById, venueByTeamDay)
                    && !ImpossibleBackToBack(awayPk, d, homePk, teamsById, venueByTeamDay))
                {
                    scheduled.Add((d, homePk, awayPk));
                    teamDaySet[homePk].Add(d);
                    teamDaySet[awayPk].Add(d);
                    dayGamesCount[d] = dayGamesCount.GetValueOrDefault(d, 0) + 1;
                    RecordGame(homePk, awayPk, d, lastGameDay, lastVenue, venueByTeamDay);
                    placed = true;
                    break;
                }
            }
            if (!placed) stillUnplaced.Add((homePk, awayPk));
        }

        // El reparto anterior garantiza 4 por bloque de temporada; las semanas
        // naturales (lunes-domingo) pueden quedar desalineadas. Mover un partido
        // regular a otra semana corrige esos picos sin tocar los partidos de Cup.
        RepairActualWeekOverloads(scheduled, cupAssignments, teams, totalDays, seasonStart, blockedDayIndexes);

        return (scheduled, stillUnplaced);
    }

    static void RepairActualWeekOverloads(
        List<(int day, int home, int away)> scheduled,
        List<(int day, int home, int away, string group)> cupAssignments,
        List<TeamData> teams,
        int totalDays,
        DateTime seasonStart,
        HashSet<int> blockedDayIndexes)
    {
        var cupKeys = new HashSet<(int day, int home, int away)>(
            cupAssignments.Select(c => (c.day, c.home, c.away)));
        var teamDays = teams.ToDictionary(t => t.id, _ => new HashSet<int>());
        var dayGamesCount = new Dictionary<int, int>();
        var venueByTeamDay = new Dictionary<(int team, int day), int>();
        var teamsById = teams.ToDictionary(t => t.id);

        void AddGame((int day, int home, int away) game)
        {
            scheduled.Add(game);
            teamDays[game.home].Add(game.day);
            teamDays[game.away].Add(game.day);
            dayGamesCount[game.day] = dayGamesCount.GetValueOrDefault(game.day, 0) + 1;
            venueByTeamDay[(game.home, game.day)] = game.home;
            venueByTeamDay[(game.away, game.day)] = game.home;
        }

        void RemoveGame((int day, int home, int away) game)
        {
            scheduled.Remove(game);
            teamDays[game.home].Remove(game.day);
            teamDays[game.away].Remove(game.day);
            dayGamesCount[game.day] = Math.Max(0, dayGamesCount.GetValueOrDefault(game.day, 1) - 1);
            venueByTeamDay.Remove((game.home, game.day));
            venueByTeamDay.Remove((game.away, game.day));
        }

        var initial = scheduled.ToList();
        scheduled.Clear();
        foreach (var game in initial)
            AddGame(game);

        for (int guard = 0; guard < 400; guard++)
        {
            bool moved = false;
            foreach (var team in teams)
            {
                int overloadedWeek = -1;
                foreach (var week in teamDays[team.id].GroupBy(d => CalendarWeekKey(seasonStart, d)))
                {
                    if (week.Count() > 4) { overloadedWeek = week.Key; break; }
                }
                if (overloadedWeek < 0) continue;

                var candidates = scheduled
                    .Where(g => (g.home == team.id || g.away == team.id)
                                && CalendarWeekKey(seasonStart, g.day) == overloadedWeek
                                && !cupKeys.Contains((g.day, g.home, g.away)))
                    .ToList();

                foreach (var game in candidates)
                {
                    RemoveGame(game);
                    bool placed = false;
                    for (int day = 0; day < totalDays; day++)
                    {
                        if (CalendarWeekKey(seasonStart, day) == overloadedWeek) continue;
                        var date = seasonStart.AddDays(day);
                        if (date.Month == 2 && date.Day >= 8 && date.Day <= 14) continue;
                        if (blockedDayIndexes.Contains(day)) continue;
                        if (dayGamesCount.GetValueOrDefault(day, 0) >= 15) continue;
                        if (teamDays[game.home].Contains(day) || teamDays[game.away].Contains(day)) continue;
                        if (WouldCreateThreeInARow(game.home, day, teamDays)
                            || WouldCreateThreeInARow(game.away, day, teamDays)) continue;
                        if (GamesInActualWeek(game.home, day, teamDays, seasonStart) >= 4
                            || GamesInActualWeek(game.away, day, teamDays, seasonStart) >= 4) continue;
                        if (ImpossibleBackToBack(game.home, day, game.home, teamsById, venueByTeamDay)
                            || ImpossibleBackToBack(game.away, day, game.home, teamsById, venueByTeamDay)) continue;

                        AddGame((day, game.home, game.away));
                        placed = true;
                        moved = true;
                        break;
                    }
                    if (placed) break;
                    AddGame(game);
                }
                if (moved) break;
            }
            if (!moved) break;
        }
    }

    static int FindDay(int homePk, int awayPk, int totalDays,
        Dictionary<int, HashSet<int>> teamDaySet,
        Dictionary<int, int> dayGamesCount,
        DateTime seasonStart,
        IReadOnlyDictionary<int, TeamData> teamsById,
        Dictionary<int, int> lastGameDay,
        Dictionary<int, int> lastVenue,
        Dictionary<(int team, int day), int> venueByTeamDay,
        HashSet<int> blockedDays = null)
    {
        int bestDay   = -1;
        int bestScore = -1000;

        for (int d = 0; d < totalDays; d++)
        {
            var date = seasonStart.AddDays(d);
            if (date.Month == 2 && date.Day >= 8 && date.Day <= 14)
                continue;
            if (blockedDays != null && blockedDays.Contains(d))
                continue;

            if (teamDaySet[homePk].Contains(d) || teamDaySet[awayPk].Contains(d))
                continue;
            if (dayGamesCount.GetValueOrDefault(d, 0) >= 15)
                continue;

            // F5-03: no programar un back-to-back que cruce el país.
            if (ImpossibleBackToBack(homePk, d, homePk, teamsById, venueByTeamDay)
                || ImpossibleBackToBack(awayPk, d, homePk, teamsById, venueByTeamDay))
                continue;

            // Descanso: máximo 2 días consecutivos y 4 partidos por bloque de 7 días.
            // La reparación final alinea además las semanas naturales lunes-domingo.
            if (!CanPlayOnDay(homePk, d, teamDaySet)
                || !CanPlayOnDay(awayPk, d, teamDaySet))
                continue;

            int hWeek = GamesInCalendarWeek(homePk, d, teamDaySet);
            int aWeek = GamesInCalendarWeek(awayPk, d, teamDaySet);

            int homeTravel = TravelMiles(homePk, d, homePk, teamsById, lastGameDay, lastVenue);
            int awayTravel = TravelMiles(awayPk, d, homePk, teamsById, lastGameDay, lastVenue);
            bool homeB2B = IsBackToBack(homePk, d, lastGameDay);
            bool awayB2B = IsBackToBack(awayPk, d, lastGameDay);

            int score = 100;
            if (homeB2B) score -= 50;
            if (awayB2B) score -= 50;
            if (hWeek >= 3 || aWeek >= 3) score -= 30;
            score -= dayGamesCount.GetValueOrDefault(d, 0) * 2;
            score -= Math.Min(25, homeTravel / 100);
            score -= Math.Min(25, awayTravel / 100);
            if (homeB2B) score -= Math.Min(20, homeTravel / 60);
            if (awayB2B) score -= Math.Min(20, awayTravel / 60);

            if (score > bestScore) { bestScore = score; bestDay = d; }
        }
        return bestDay;
    }

    static void RecordGame(int homePk, int awayPk, int day,
        Dictionary<int, int> lastGameDay, Dictionary<int, int> lastVenue,
        Dictionary<(int team, int day), int> venueByTeamDay)
    {
        // La sede del partido es siempre el pabellón del local.
        venueByTeamDay[(homePk, day)] = homePk;
        venueByTeamDay[(awayPk, day)] = homePk;

        // El calendario se construye fuera de orden: conservar la jornada más
        // reciente para que la puntuación de viaje use el partido correcto.
        if (day >= lastGameDay[homePk])
        {
            lastGameDay[homePk] = day;
            lastVenue[homePk] = homePk;
        }
        if (day >= lastGameDay[awayPk])
        {
            lastGameDay[awayPk] = day;
            lastVenue[awayPk] = homePk;
        }
    }

    static bool IsBackToBack(int teamPk, int day, Dictionary<int, int> lastGameDay)
        => lastGameDay.TryGetValue(teamPk, out int previous) && previous >= 0 && day == previous + 1;

    static int TravelMiles(int teamPk, int day, int venueTeamId,
        IReadOnlyDictionary<int, TeamData> teamsById,
        Dictionary<int, int> lastGameDay, Dictionary<int, int> lastVenue)
    {
        if (!lastGameDay.TryGetValue(teamPk, out int previous) || previous < 0) return 0;
        if (!lastVenue.TryGetValue(teamPk, out int previousVenue) || previousVenue <= 0) return 0;
        return TravelHelper.DistanceKilometers(previousVenue, venueTeamId, teamsById);
    }

    static bool ImpossibleBackToBack(int teamPk, int day, int venueTeamId,
        IReadOnlyDictionary<int, TeamData> teamsById,
        Dictionary<(int team, int day), int> venueByTeamDay)
    {
        // El calendario se construye fuera de orden cronológico: hay que mirar
        // tanto el día anterior como el siguiente ya programados.
        if (day > 0 && venueByTeamDay.TryGetValue((teamPk, day - 1), out int previousVenue))
        {
            int kilometers = TravelHelper.DistanceKilometers(previousVenue, venueTeamId, teamsById);
            if (TravelHelper.IsImpossibleBackToBack(day - 1, day, kilometers)) return true;
        }
        if (venueByTeamDay.TryGetValue((teamPk, day + 1), out int nextVenue))
        {
            int kilometers = TravelHelper.DistanceKilometers(venueTeamId, nextVenue, teamsById);
            if (TravelHelper.IsImpossibleBackToBack(day, day + 1, kilometers)) return true;
        }
        return false;
    }

    static bool CanPlayOnDay(int teamPk, int dayIdx, Dictionary<int, HashSet<int>> teamDaySet)
    {
        if (teamDaySet[teamPk].Contains(dayIdx)) return false;
        if (WouldCreateThreeInARow(teamPk, dayIdx, teamDaySet)) return false;
        return GamesInCalendarWeek(teamPk, dayIdx, teamDaySet) < 4;
    }

    static bool WouldCreateThreeInARow(int teamPk, int dayIdx,
        Dictionary<int, HashSet<int>> teamDaySet)
    {
        var days = teamDaySet[teamPk];
        int before = 0;
        for (int d = dayIdx - 1; days.Contains(d); d--) before++;
        int after = 0;
        for (int d = dayIdx + 1; days.Contains(d); d++) after++;
        return before + after + 1 > 2;
    }

    static int GamesInCalendarWeek(int teamPk, int dayIdx, Dictionary<int, HashSet<int>> teamDaySet)
    {
        int week = dayIdx / 7;
        return teamDaySet[teamPk].Count(d => d / 7 == week);
    }

    static int GamesInActualWeek(int teamPk, int dayIdx,
        Dictionary<int, HashSet<int>> teamDaySet, DateTime seasonStart)
    {
        int week = CalendarWeekKey(seasonStart, dayIdx);
        return teamDaySet[teamPk].Count(d => CalendarWeekKey(seasonStart, d) == week);
    }

    static int CalendarWeekKey(DateTime seasonStart, int dayIdx)
        => (dayIdx + ((int)seasonStart.DayOfWeek + 6) % 7) / 7;

    static bool HasB2B(int teamPk, int dayIdx, Dictionary<int, HashSet<int>> teamDaySet)
        => teamDaySet[teamPk].Contains(dayIdx - 1) || teamDaySet[teamPk].Contains(dayIdx + 1);
}