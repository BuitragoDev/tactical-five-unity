using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Calendario de la liga regular G-League: doble vuelta intra-conferencia
/// (28 partidos por filial) disputada SOLO en días que ya tienen partidos NBA,
/// entre el 1 de noviembre y el 20 de marzo, con pausa en la semana All-Star.
///
/// Cada "jornada" se construye por rotación circular (todos los equipos juegan
/// exactamente una vez, hay un descanso por conferencia al ser 15 equipos),
/// lo que garantiza que ningún filial dispute dos partidos el mismo día.
/// </summary>
public static class GLeagueScheduleGenerator
{
    public const string TYPE_REGULAR = "gleague";
    public const int GAMES_PER_TEAM = 28;   // 14 rivales intra-conferencia × 2 vueltas
    public const int MAX_GAMES_PER_DAY = 8; // una jornada completa son 7 partidos

    /// <summary>Genera y persiste el calendario. Devuelve el número de partidos creados.</summary>
    public static int GenerateSchedule(SeasonData season, List<GLeagueTeamData> teams)
    {
        // Días (1-based, convención games.game_day) con partidos NBA ya programados
        var nbaDays = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == season.manager_id && g.game_type == "regular")
            .Select(g => g.game_day)
            .Distinct()
            .ToList();

        var games = BuildSchedule(season, teams, nbaDays);
        DatabaseManager.Instance.SaveGLeagueGames(games);
        Debug.Log($"[GLeague] {games.Count} partidos de liga regular G-League generados.");
        return games.Count;
    }

    /// <summary>
    /// Puro y testeable: construye las filas GameData sin tocar la BD.
    /// `nbaGameDays` son valores game_day (1-based) con partido NBA.
    /// </summary>
    public static List<GameData> BuildSchedule(SeasonData season, List<GLeagueTeamData> teams, IEnumerable<int> nbaGameDays)
    {
        var seasonStart = new DateTime(season.year_start, 10, 22);
        var windowStart = new DateTime(season.year_start, 11, 1);
        var windowEnd   = new DateTime(season.year_end, 3, 20);

        // Días disponibles: con NBA, dentro de la ventana, fuera de la semana All-Star
        var availableDays = new List<int>();
        foreach (var day in nbaGameDays)
        {
            int idx = day - 1; // game_day es 1-based
            var date = seasonStart.AddDays(idx);
            if (date < windowStart || date > windowEnd) continue;
            if (IsAllStarBreak(date)) continue;
            availableDays.Add(idx);
        }
        availableDays.Sort();

        var east = teams.Where(t => t.conference == "East").OrderBy(t => t.id).ToList();
        var west = teams.Where(t => t.conference == "West").OrderBy(t => t.id).ToList();

        // Jornadas intercaladas Este/Oeste: cada fecha acoge una sola jornada
        // (~7 partidos), así ningún día supera MAX_GAMES_PER_DAY.
        var eastRounds = DoubleRoundRobin(east);
        var westRounds = DoubleRoundRobin(west);
        var rounds = new List<List<(int home, int away)>>();
        int halfLen = Math.Max(eastRounds.Count, westRounds.Count);
        for (int r = 0; r < halfLen; r++)
        {
            if (r < eastRounds.Count) rounds.Add(eastRounds[r]);
            if (r < westRounds.Count) rounds.Add(westRounds[r]);
        }

        // Fechas elegidas: repartir las jornadas a lo largo de la ventana disponible
        var chosenDays = PickSpreadDates(availableDays, rounds.Count);

        var games = new List<GameData>();
        var teamDaySet = teams.ToDictionary(t => t.id, _ => new HashSet<int>());
        var dayLoad = new Dictionary<int, int>();
        int pointer = 0;

        for (int r = 0; r < rounds.Count; r++)
        {
            var matchups = rounds[r];

            // Buscar fecha libre para la jornada completa (sin solapamientos de equipo)
            bool placed = false;
            for (int attempt = 0; attempt < Math.Max(1, chosenDays.Count); attempt++)
            {
                int day = chosenDays[pointer];
                bool free = matchups.All(m => !teamDaySet[m.home].Contains(day) && !teamDaySet[m.away].Contains(day))
                            && dayLoad.GetValueOrDefault(day, 0) + matchups.Count <= MAX_GAMES_PER_DAY;
                if (free)
                {
                    foreach (var (home, away) in matchups)
                    {
                        var gameDate = seasonStart.AddDays(day);
                        games.Add(new GameData
                        {
                            season_id    = season.id,
                            manager_id   = season.manager_id,
                            game_day     = day + 1,
                            game_date    = gameDate.ToString("yyyy-MM-dd"),
                            home_team_id = GLeagueHelper.EncodeGlTeamId(home),
                            away_team_id = GLeagueHelper.EncodeGlTeamId(away),
                            is_played    = 0,
                            game_type    = TYPE_REGULAR,
                            series_label = "",
                            home_score   = 0,
                            away_score   = 0,
                            is_home      = 0
                        });
                        teamDaySet[home].Add(day);
                        teamDaySet[away].Add(day);
                        dayLoad[day] = dayLoad.GetValueOrDefault(day, 0) + 1;
                    }
                    pointer = (pointer + 1) % chosenDays.Count;
                    placed = true;
                    break;
                }
                pointer = (pointer + 1) % Math.Max(1, chosenDays.Count);
            }

            if (!placed && chosenDays.Count > 0)
                Debug.LogWarning($"[GLeague] Jornada {r} sin fecha libre; partidos descartados.");
        }

        // Verificación: cada equipo debe tener GAMES_PER_TEAM partidos
        var counts = new Dictionary<int, int>();
        foreach (var t in teams) counts[t.id] = 0;
        foreach (var g in games)
        {
            counts[GLeagueHelper.DecodeGlTeamId(g.home_team_id)]++;
            counts[GLeagueHelper.DecodeGlTeamId(g.away_team_id)]++;
        }
        foreach (var t in teams)
        {
            if (counts[t.id] != GAMES_PER_TEAM)
                Debug.LogWarning($"[GLeague] {t.name} tiene {counts[t.id]} partidos, se esperaban {GAMES_PER_TEAM}");
        }

        return games;
    }

    static bool IsAllStarBreak(DateTime date)
        => date.Month == 2 && date.Day >= 8 && date.Day <= 14;

    /// <summary>
    /// Doble vuelta por rotación circular (método del círculo). Con N impar cada
    /// jornada tiene N/2 partidos y un equipo descansa; la segunda vuelta invierte
    /// la localía. Devuelve una jornada por ronda.
    /// </summary>
    public static List<List<(int home, int away)>> DoubleRoundRobin(List<GLeagueTeamData> teams)
    {
        var firstHalf = SingleRoundRobin(teams.Select(t => t.id).ToList());
        var result = new List<List<(int home, int away)>>(firstHalf);
        foreach (var round in firstHalf)
            result.Add(round.Select(m => (m.away, m.home)).ToList());
        return result;
    }

    /// <summary>
    /// Una vuelta por rotación circular (método del círculo). Con N impar se
    /// añade un dorsal ficticio (byedummy, -1) para que la rotación sea par:
    /// se generan N jornadas con N/2 partidos y un descanso por jornada. Así
    /// cada equipo juega contra los otros N-1 exactamente una vez.
    /// </summary>
    static List<List<(int home, int away)>> SingleRoundRobin(List<int> ids)
    {
        var rounds = new List<List<(int home, int away)>>();
        var rotation = new List<int>(ids);
        bool odd = rotation.Count % 2 == 1;
        if (odd) rotation.Add(-1);   // dorsal ficticio: la pareja con -1 es el descanso
        int m = rotation.Count;

        for (int r = 0; r < m - 1; r++)
        {
            var round = new List<(int home, int away)>();
            for (int i = 0; i < m / 2; i++)
            {
                int home = rotation[i];
                int away = rotation[m - 1 - i];
                if (home == -1 || away == -1) continue;   // jornada con bye
                // Alternar localía según la ronda para equilibrar
                round.Add((r + i) % 2 == 0 ? (home, away) : (away, home));
            }
            rounds.Add(round);

            // Rotación: el primero fijo, el resto gira
            int last = rotation[m - 1];
            rotation.RemoveAt(m - 1);
            rotation.Insert(1, last);
        }
        return rounds;
    }

    /// <summary>Selecciona `count` fechas repartidas uniformemente (orden preservado).
    /// Si hay menos fechas que jornadas, cicla.</summary>
    public static List<int> PickSpreadDates(List<int> sortedDays, int count)
    {
        var result = new List<int>();
        if (sortedDays.Count == 0) return result;
        if (sortedDays.Count <= count)
        {
            while (result.Count < count)
                result.AddRange(sortedDays);
            return result.Take(count).ToList();
        }

        double step = (double)(sortedDays.Count - 1) / Math.Max(1, count - 1);
        for (int i = 0; i < count; i++)
            result.Add(sortedDays[(int)Math.Round(i * step)]);
        return result;
    }
}
