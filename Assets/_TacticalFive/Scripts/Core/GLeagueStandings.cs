using System.Collections.Generic;
using System.Linq;

/// <summary>Fila de clasificación G-League.</summary>
public class GLeagueStandingRow
{
    public int teamId;
    public int wins;
    public int losses;
    public int pf;
    public int pa;
    public List<bool> results = new(); // cronológico: true=victoria

    public float WinPct => wins + losses > 0 ? (float)wins / (wins + losses) : 0f;
    public int Diff => pf - pa;
}

/// <summary>
/// Clasificación G-League calculada en memoria desde los partidos jugados
/// (misma filosofía que StandingsController para la NBA; nunca se persiste).
/// </summary>
public static class GLeagueStandings
{
    /// <summary>
    /// Calcula la tabla ordenada (Win% → dif → PF → id). Solo cuenta partidos
    /// jugados cuyo game_type sea el de liga regular G-League.
    /// </summary>
    public static List<GLeagueStandingRow> Compute(IEnumerable<GLeagueTeamData> teams, IEnumerable<GameData> games)
    {
        var data = teams.ToDictionary(
            t => t.id,
            _ => new GLeagueStandingRow());

        foreach (var g in games)
        {
            if (g.is_played != 1 || g.game_type != GLeagueScheduleGenerator.TYPE_REGULAR)
                continue;

            // Los partidos G-League guardan los ids de filial codificados (+offset)
            int homeId = GLeagueHelper.DecodeGlTeamId(g.home_team_id);
            int awayId = GLeagueHelper.DecodeGlTeamId(g.away_team_id);
            bool homeWon = g.home_score > g.away_score;

            if (data.TryGetValue(homeId, out var home))
            {
                home.wins += homeWon ? 1 : 0;
                home.losses += homeWon ? 0 : 1;
                home.pf += g.home_score;
                home.pa += g.away_score;
                home.results.Add(homeWon);
            }
            if (data.TryGetValue(awayId, out var away))
            {
                away.wins += homeWon ? 0 : 1;
                away.losses += homeWon ? 1 : 0;
                away.pf += g.away_score;
                away.pa += g.home_score;
                away.results.Add(!homeWon);
            }
        }

        return data
            .Select(kv => { kv.Value.teamId = kv.Key; return kv.Value; })
            .OrderByDescending(r => r.WinPct)
            .ThenByDescending(r => r.Diff)
            .ThenByDescending(r => r.pf)
            .ThenBy(r => r.teamId)
            .ToList();
    }

    /// <summary>Racha reciente como texto ("VVDL"); más antiguo → más reciente.</summary>
    public static string StreakText(GLeagueStandingRow row, int max = 5)
    {
        if (row == null || row.results.Count == 0) return "—";
        int take = System.Math.Min(max, row.results.Count);
        var last = row.results.Skip(row.results.Count - take);
        return string.Concat(last.Select(w => w ? "V" : "D"));
    }
}
