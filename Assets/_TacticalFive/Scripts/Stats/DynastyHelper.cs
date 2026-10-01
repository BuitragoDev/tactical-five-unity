using System.Collections.Generic;
using System.Linq;

/// <summary>Prestigio histórico de una franquicia (derivado de `season_records`).</summary>
public class FranchisePrestige
{
    public int teamId;
    public int titles;
    public int finals;
    public int firstTitleSeasonId;
    public int lastTitleSeasonId;
    /// <summary>títulos×3 + finales×2.</summary>
    public int prestige;
    /// <summary>¿Dinastía? 3+ títulos dentro de la ventana (ver `DynastyHelper`).</summary>
    public bool isDynasty;
}

/// <summary>
/// Reglas puras de dinastía/prestigio (sin DB, testables). Todo se deriva de
/// `season_records` (campeón, finalista y campeones de conferencia por temporada),
/// así que no hace falta esquema nuevo ni se pierde al purgar `games`.
/// </summary>
public static class DynastyHelper
{
    public const int DYNASTY_TITLES = 3;   // títulos necesarios
    public const int DYNASTY_WINDOW = 8;   // ventana de temporadas para considerarlo dinastía

    public static int Prestige(int titles, int finals)
        => titles * 3 + finals * 2;

    /// <summary>¿3+ títulos en una ventana de 8 temporadas? (temporadas ordenables por id)</summary>
    public static bool IsDynasty(IEnumerable<int> titleSeasonIds)
    {
        if (titleSeasonIds == null) return false;
        var ids = titleSeasonIds.Distinct().OrderBy(x => x).ToList();
        if (ids.Count < DYNASTY_TITLES) return false;

        for (int i = 0; i + DYNASTY_TITLES - 1 < ids.Count; i++)
            if (ids[i + DYNASTY_TITLES - 1] - ids[i] <= DYNASTY_WINDOW - 1) return true;
        return false;
    }

    /// <summary>Prestigio de un equipo concreto a partir de todos los season_records.</summary>
    public static FranchisePrestige Of(IEnumerable<SeasonRecord> records, int teamId)
    {
        var list = (records ?? Enumerable.Empty<SeasonRecord>()).Where(r => r != null).ToList();
        var titleSeasons = list.Where(r => r.champion_id == teamId).Select(r => r.season_id).ToList();
        // "Finales" = veces campeón o subcampeón. El campeón de conferencia es siempre el
        // finalista, así que no se cuenta aparte (dato redundante).
        int finals = list.Count(r => r.champion_id == teamId || r.finalist_id == teamId);

        return new FranchisePrestige
        {
            teamId = teamId,
            titles = titleSeasons.Count,
            finals = finals,
            firstTitleSeasonId = titleSeasons.Count > 0 ? titleSeasons.Min() : 0,
            lastTitleSeasonId = titleSeasons.Count > 0 ? titleSeasons.Max() : 0,
            prestige = Prestige(titleSeasons.Count, finals),
            isDynasty = IsDynasty(titleSeasons)
        };
    }

    /// <summary>
    /// Ranking de prestigio de la liga (solo equipos con algún título, final o conferencia),
    /// ordenado por prestigio y títulos.
    /// </summary>
    public static List<FranchisePrestige> LeagueRanking(IEnumerable<SeasonRecord> records, int max = 0)
    {
        var list = (records ?? Enumerable.Empty<SeasonRecord>()).Where(r => r != null).ToList();

        var teamIds = new HashSet<int>();
        foreach (var r in list)
        {
            if (r.champion_id > 0) teamIds.Add(r.champion_id);
            if (r.finalist_id > 0) teamIds.Add(r.finalist_id);
        }

        var ranking = teamIds.Select(id => Of(list, id))
                             .OrderByDescending(p => p.prestige)
                             .ThenByDescending(p => p.titles)
                             .ThenBy(p => p.teamId)
                             .ToList();
        return max > 0 ? ranking.Take(max).ToList() : ranking;
    }
}
