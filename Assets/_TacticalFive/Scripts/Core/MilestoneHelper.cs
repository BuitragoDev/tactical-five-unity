using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Reglas puras de los hitos de legado (E1): qué hitos ha alcanzado un jugador con sus
/// totales de carrera y cuánto le falta para el siguiente. Sin DB.
/// </summary>
public static class MilestoneHelper
{
    /// <summary>Valor de carrera de la familia indicada.</summary>
    public static int ValueOf(MilestoneFamily family, in CareerTotals c) => family switch
    {
        MilestoneFamily.Points => c.points,
        MilestoneFamily.Rebounds => c.rebounds,
        MilestoneFamily.Assists => c.assists,
        MilestoneFamily.Steals => c.steals,
        MilestoneFamily.Blocks => c.blocks,
        MilestoneFamily.ThreePointers => c.threePointers,
        MilestoneFamily.Games => c.games,
        MilestoneFamily.DoubleDoubles => c.doubleDoubles,
        MilestoneFamily.TripleDoubles => c.tripleDoubles,
        MilestoneFamily.AllStar => c.allStars,
        MilestoneFamily.SeasonMvp => c.seasonMvps,
        MilestoneFamily.Rings => c.rings,
        _ => c.finalsMvps
    };

    /// <summary>
    /// Hitos alcanzados y **no registrados todavía** (los que ya están en `alreadyReached`
    /// se ignoran para no repetir la noticia).
    /// </summary>
    public static List<MilestoneDef> Reached(in CareerTotals c, ICollection<string> alreadyReached = null)
    {
        var result = new List<MilestoneDef>();
        foreach (var d in MilestoneCatalog.All)
        {
            if (alreadyReached != null && alreadyReached.Contains(d.key)) continue;
            if (ValueOf(d.family, c) >= d.threshold) result.Add(d);
        }
        return result.OrderBy(d => d.family).ThenBy(d => d.threshold).ToList();
    }

    /// <summary>Siguiente umbral de una familia con su progreso (o (null, 0, 0) si ya los tiene todos).</summary>
    public static (MilestoneDef next, int current, int percent) Next(in CareerTotals c, MilestoneFamily family)
    {
        int current = ValueOf(family, c);
        var next = MilestoneCatalog.OfFamily(family).FirstOrDefault(d => current < d.threshold);
        if (next == null) return (null, 0, 0);
        int percent = Mathf.Clamp(current * 100 / next.threshold, 0, 99);
        return (next, current, percent);
    }

    /// <summary>El hito más cercano de toda la carrera (mayor % de progreso) o null si no queda ninguno.</summary>
    public static (MilestoneDef next, int current, int percent)? NextOverall(in CareerTotals c)
    {
        (MilestoneDef, int, int)? best = null;
        foreach (MilestoneFamily f in System.Enum.GetValues(typeof(MilestoneFamily)))
        {
            var n = Next(c, f);
            if (n.next == null) continue;
            if (best == null || n.percent > best.Value.Item3) best = n;
        }
        return best;
    }

    /// <summary>Resumen corto de una lista de hitos ("20.000 puntos, 10× All-Star").</summary>
    public static string Summarize(IEnumerable<MilestoneDef> milestones)
        => milestones == null ? "" : string.Join(", ", milestones.Select(MilestoneCatalog.Label));
}
