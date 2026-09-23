using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Cálculo y ordenación de la clasificación de conferencia. Fuente única para
/// Dashboard, Standings, Play-In/Playoffs y los objetivos de temporada, de modo
/// que los emparejamientos de eliminatorias coincidan siempre con lo que se ve
/// en pantalla.
/// </summary>
public class StandingRow
{
    public int teamId;
    public int rank;
    public int wins;
    public int losses;
    public int pf;
    public int pa;
    public List<bool> games = new List<bool>();  // secuencia homeWon (para last-10)
}

public static class StandingsHelper
{
    /// <summary>
    /// Construye la clasificación de un conjunto de equipos (normalmente una
    /// conferencia) a partir de los partidos jugados. Orden canónico:
    /// %victorias DESC → derrotas ASC → victorias DESC → dif. puntos DESC →
    /// teamId ASC (desempate determinista).
    /// </summary>
    public static List<StandingRow> Build(IEnumerable<TeamData> teams, IEnumerable<GameData> games)
    {
        var data = teams.ToDictionary(t => t.id, t => new StandingRow { teamId = t.id });
        var ids = data.Keys.ToHashSet();

        if (games != null)
        {
            foreach (var g in games
                .Where(g => g.is_played == 1 && g.game_type == "regular")
                .OrderBy(g => g.game_day).ThenBy(g => g.id))
            {
                if (ids.Contains(g.home_team_id))
                {
                    bool homeWon = g.home_score > g.away_score;
                    var r = data[g.home_team_id];
                    r.wins += homeWon ? 1 : 0;
                    r.losses += homeWon ? 0 : 1;
                    r.pf += g.home_score;
                    r.pa += g.away_score;
                    r.games.Add(homeWon);
                }
                if (ids.Contains(g.away_team_id))
                {
                    bool awayWon = g.away_score > g.home_score;
                    var r = data[g.away_team_id];
                    r.wins += awayWon ? 1 : 0;
                    r.losses += awayWon ? 0 : 1;
                    r.pf += g.away_score;
                    r.pa += g.home_score;
                    r.games.Add(awayWon);
                }
            }
        }

        var rows = data.Values.ToList();
        rows.Sort(Compare);
        for (int i = 0; i < rows.Count; i++)
            rows[i].rank = i + 1;
        return rows;
    }

    public static int Compare(StandingRow a, StandingRow b)
    {
        float pctA = a.wins + a.losses > 0 ? (float)a.wins / (a.wins + a.losses) : 0f;
        float pctB = b.wins + b.losses > 0 ? (float)b.wins / (b.wins + b.losses) : 0f;
        if (pctB != pctA) return pctB.CompareTo(pctA);
        if (a.losses != b.losses) return a.losses.CompareTo(b.losses);
        if (b.wins != a.wins) return b.wins.CompareTo(a.wins);
        int diffA = a.pf - a.pa;
        int diffB = b.pf - b.pa;
        if (diffB != diffA) return diffB.CompareTo(diffA);
        return a.teamId.CompareTo(b.teamId);
    }

    /// <summary>
    /// Posición (1..N) de un equipo dentro de su conferencia. Devuelve 0 si no
    /// se puede determinar.
    /// </summary>
    public static int GetRank(int teamId, string conference, List<TeamData> teams, List<GameData> games)
    {
        if (teams == null) return 0;

        var confTeams = teams.Where(t => t.conference == conference).ToList();
        if (confTeams.Count == 0) return 0;

        var row = Build(confTeams, games).FirstOrDefault(r => r.teamId == teamId);
        return row?.rank ?? 0;
    }
}
