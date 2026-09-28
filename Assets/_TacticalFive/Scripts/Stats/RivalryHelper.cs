using System.Collections.Generic;
using System.Linq;

/// <summary>Resumen de la rivalidad con otro equipo a partir de las series de playoffs.</summary>
public class RivalryEntry
{
    public int opponentId;
    public int series;          // enfrentamientos totales
    public int wins;
    public int losses;
    public int finalsMeetings;  // cruces en las Finales
    public int lastSeasonId;
    public string lastStage;    // r1..r4
    public bool lastWon;
    public int lastGameDay;
    public int intensity;       // para ordenar / elegir el rival principal
}

/// <summary>
/// Reglas puras de rivalidad (sin DB, testables). Se alimenta de `playoff_series`, que se
/// escribe al cerrarse cada serie porque `games` se purga en el rollover.
/// </summary>
public static class RivalryHelper
{
    /// <summary>
    /// Rivales de un equipo, ordenados por intensidad. Intensidad pondera enfrentamientos,
    /// cruces en Finales, recencia (si se pasa la temporada actual) y equilibrio del balance
    /// (una rivalidad igualada es más "caliente" que un 4-0).
    /// </summary>
    public static List<RivalryEntry> RivalsOf(IEnumerable<PlayoffSeriesData> allSeries, int teamId,
                                              int currentSeasonId = 0)
    {
        var map = new Dictionary<int, RivalryEntry>();
        if (allSeries == null) return new List<RivalryEntry>();

        foreach (var s in allSeries)
        {
            if (s == null || string.IsNullOrEmpty(s.label)) continue;
            if (!s.label.StartsWith("playoff-")) continue;          // el play-in no es una serie
            if (s.high_team_id != teamId && s.low_team_id != teamId) continue;

            int opponent = s.high_team_id == teamId ? s.low_team_id : s.high_team_id;
            if (opponent == teamId) continue;

            if (!map.TryGetValue(opponent, out var e))
            {
                e = new RivalryEntry { opponentId = opponent };
                map[opponent] = e;
            }

            bool won = s.winner_id == teamId;
            e.series++;
            if (won) e.wins++; else e.losses++;
            if (s.stage == "r4") e.finalsMeetings++;

            if (s.season_id > e.lastSeasonId ||
                (s.season_id == e.lastSeasonId && s.game_day >= e.lastGameDay))
            {
                e.lastSeasonId = s.season_id;
                e.lastStage = s.stage;
                e.lastWon = won;
                e.lastGameDay = s.game_day;
            }
        }

        foreach (var e in map.Values)
        {
            int balance = System.Math.Abs(e.wins - e.losses);
            e.intensity = e.series * 10 + e.finalsMeetings * 20 - balance * 2;
            if (currentSeasonId > 0 && currentSeasonId - e.lastSeasonId <= 3)
                e.intensity += 15;                                   // rivalidad reciente
        }

        return map.Values
                  .OrderByDescending(e => e.intensity)
                  .ThenByDescending(e => e.series)
                  .ThenBy(e => e.opponentId)
                  .ToList();
    }

    /// <summary>El rival principal (o null si no hay cruces de playoffs).</summary>
    public static RivalryEntry MainRival(IEnumerable<PlayoffSeriesData> allSeries, int teamId, int currentSeasonId = 0)
        => RivalsOf(allSeries, teamId, currentSeasonId).FirstOrDefault();

    /// <summary>Enfrentamientos previos con un rival concreto (más reciente primero).</summary>
    public static List<PlayoffSeriesData> MeetingsWith(IEnumerable<PlayoffSeriesData> allSeries, int teamId, int opponentId)
        => (allSeries ?? Enumerable.Empty<PlayoffSeriesData>())
              .Where(s => s != null
                       && ((s.high_team_id == teamId && s.low_team_id == opponentId)
                        || (s.high_team_id == opponentId && s.low_team_id == teamId)))
              .OrderByDescending(s => s.season_id).ThenByDescending(s => s.game_day)
              .ToList();

    /// <summary>¿Hay historia previa de playoffs con ese rival?</summary>
    public static bool HasHistory(IEnumerable<PlayoffSeriesData> allSeries, int teamId, int opponentId)
        => MeetingsWith(allSeries, teamId, opponentId).Count > 0;

    /// <summary>
    /// ¿El último cruce fue una eliminación (perdimos)? Base del hilo de revancha.
    /// Devuelve la serie más reciente en la que el rival eliminó a mi equipo (o null).
    /// </summary>
    public static PlayoffSeriesData LastEliminationBy(IEnumerable<PlayoffSeriesData> allSeries, int teamId, int opponentId)
        => MeetingsWith(allSeries, teamId, opponentId).FirstOrDefault(s => s.loser_id == teamId);
}
