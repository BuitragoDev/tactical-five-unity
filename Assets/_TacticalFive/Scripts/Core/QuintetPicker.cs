using System.Collections.Generic;
using System.Linq;

/// <summary>Candidato a entrar en un quinteto de la temporada (datos ya leídos de la BD).</summary>
public class QuintetCandidate
{
    public int PlayerId;
    public string Photo;
    public string PlayerName;
    public string Position;            // posición primaria
    public string SecondaryPosition;   // posición secundaria (puede estar vacía)
    public string TeamName;
    public string TeamLogo;
    public float AvgPts;
    public float AvgReb;
    public float AvgAst;
    public float AvgRating;
}

/// <summary>Un puesto de un quinteto: la posición que cubre y el jugador elegido.</summary>
public class QuintetSlot
{
    public string position;
    public QuintetCandidate player;
}

/// <summary>
/// Selección de los mejores quintetos de la temporada (1º, 2º, …). Puro y testeable:
/// por cada posición (PG/SG/SF/PF/C, aceptando la secundaria) elige el mejor por
/// rating medio, **sin repetir jugador** entre posiciones ni entre quintetos.
///
/// Si ningún candidato de `candidates` cubre una posición, se busca en `fallbacks`
/// (los que no alcanzaron el mínimo de partidos): así ningún puesto del quinteto se
/// queda vacío. Los candidatos de respaldo solo se usan para los puestos vacíos, nunca
/// para desplazar a uno que sí cumple el mínimo.
/// </summary>
public static class QuintetPicker
{
    public static readonly string[] Positions = { "PG", "SG", "SF", "PF", "C" };

    /// <summary>Cuántos quintetos se calculan (1º y 2º).</summary>
    public const int Teams = 2;

    public static List<List<QuintetSlot>> Pick(IReadOnlyList<QuintetCandidate> candidates,
                                                int teams = Teams,
                                                IReadOnlyList<QuintetCandidate> fallbacks = null)
    {
        var result = new List<List<QuintetSlot>>();
        if ((candidates == null || candidates.Count == 0) && (fallbacks == null || fallbacks.Count == 0))
            return result;
        if (teams <= 0) return result;

        // Orden por rating (el desempate lo marca el orden original, como antes).
        var ordered = Order(candidates);
        var orderedFallback = Order(fallbacks);
        var used = new HashSet<int>();

        for (int t = 0; t < teams; t++)
        {
            var team = new List<QuintetSlot>();
            foreach (var pos in Positions)
            {
                // Fase 1: los que cumplen el mínimo de partidos.
                QuintetCandidate best = BestFor(ordered, pos, used);
                // Fase 2: el puesto se llenó con el mejor disponible aunque no llegue al mínimo.
                if (best == null) best = BestFor(orderedFallback, pos, used);
                if (best == null) continue;
                if (best.PlayerId != 0) used.Add(best.PlayerId);
                team.Add(new QuintetSlot { position = pos, player = best });
            }
            result.Add(team);
        }

        return result;
    }

    static List<QuintetCandidate> Order(IReadOnlyList<QuintetCandidate> candidates)
    {
        if (candidates == null) return new List<QuintetCandidate>();
        return candidates.Where(c => c != null)
                         .OrderByDescending(c => c.AvgRating)
                         .ToList();
    }

    /// <summary>Mejor candidato para una posición (primaria o secundaria) sin repetir jugador.</summary>
    static QuintetCandidate BestFor(List<QuintetCandidate> ordered, string pos, HashSet<int> used)
    {
        QuintetCandidate best = null;
        foreach (var c in ordered)
        {
            if (c.PlayerId != 0 && used.Contains(c.PlayerId)) continue;
            if (c.Position != pos && c.SecondaryPosition != pos) continue;
            if (best == null || c.AvgRating > best.AvgRating) best = c;
        }
        return best;
    }
}
