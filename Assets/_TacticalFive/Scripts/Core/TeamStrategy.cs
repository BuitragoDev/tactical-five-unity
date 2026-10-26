using System.Linq;

/// <summary>
/// Estrategia de un GM IA, recalculada por ciclo de traspasos. Determina cómo
/// negocia (densidad de traspasos, objetivos) y su plan de partido por defecto.
/// </summary>
public enum TeamStrategy { Rebuild, Balanced, Contend }

/// <summary>Clasificación pura de la estrategia de un equipo, compartida entre UI e IA.</summary>
public static class TeamStrategyRules
{
    public static TeamStrategy Classify(int rank, int conferenceSize, System.Collections.Generic.List<PlayerData> roster)
    {
        roster ??= new System.Collections.Generic.List<PlayerData>();
        int stars = roster.Count(p => p.overall >= 85);
        float averageAge = roster.Count > 0 ? (float)roster.Average(p => p.age) : 30f;

        if ((rank > 0 && rank <= 4) || stars >= 2) return TeamStrategy.Contend;

        bool weakRecord = rank > 0 && conferenceSize > 0 && rank >= conferenceSize - 3;
        bool youngNoStars = averageAge <= 27f && stars == 0 && roster.Count(p => p.overall >= 80) <= 1;
        if (weakRecord || youngNoStars) return TeamStrategy.Rebuild;

        return TeamStrategy.Balanced;
    }
}
