using System;

/// <summary>
/// Reglas puras de la parte médica (E2): evolución diaria con el médico de plantilla,
/// alta forzada (volver antes de tiempo), riesgo de recaída y progreso de recuperación.
/// Sin estado ni aleatoriedad propia: todo lo que depende del azar recibe el `roll`.
/// </summary>
public static class InjuryHelper
{
    /// <summary>
    /// Evolución de un día de recuperación. Devuelve los días restantes (0 = recuperado).
    /// El médico con buena reputación acelera; las lesiones graves con físico bajo pueden
    /// estancarse/retroceder un día (nunca por encima del pronóstico original).
    /// </summary>
    public static int EvolveDays(int remaining, int totalDays, InjurySeverity sev, int medicoRep, int fisico, double roll)
    {
        if (remaining <= 0) return 0;

        int next = remaining - 1;

        // El buen médico de plantilla añade días de avance en las lesiones menores.
        bool fast = (sev == InjurySeverity.Leve || sev == InjurySeverity.Moderada)
                    && ((medicoRep >= 5 && roll > 0.55) || (medicoRep >= 4 && roll > 0.72));
        if (fast) next--;

        // Retroceso: solo en graves/críticas, con físico bajo y lejos del alta.
        int cap = totalDays > 0 ? totalDays : remaining + 1;
        if (remaining > 2 && sev >= InjurySeverity.Grave && fisico < 40 && roll < 0.08 && !fast)
            next = Math.Min(cap, remaining + 1);

        return Math.Max(0, next);
    }

    /// <summary>Riesgo (0-100) que asume el jugador si se le da el alta antes de tiempo.</summary>
    public static int ForceReturnRisk(int remaining, InjurySeverity sev)
    {
        if (remaining <= 0) return 0;
        int risk = 20 + (int)sev * 15 + remaining * 2;
        return Math.Min(90, risk);
    }

    /// <summary>Probabilidad extra de lesión por partido jugado con riesgo de recaída pendiente.</summary>
    public static float RelapseChancePerGame(int risk, int fisico)
    {
        if (risk <= 0) return 0f;
        float baseChance = risk / 100f * 0.35f;
        if (fisico < 50) baseChance *= 1.5f;
        return baseChance;
    }

    /// <summary>Cuánto baja el riesgo cada día: descansar lo cura; jugar lo mantiene.</summary>
    public static int RiskDecayForDay(bool playedToday) => playedToday ? 10 : 25;

    /// <summary>Progreso de recuperación 0-100 (para la barra del parte médico).</summary>
    public static int RecoveryProgress(int remaining, int totalDays)
    {
        if (totalDays <= 0) return remaining <= 0 ? 100 : 0;
        if (remaining <= 0) return 100;
        float done = (totalDays - remaining) / (float)totalDays;
        return Math.Max(0, Math.Min(100, (int)Math.Round(done * 100)));
    }

    /// <summary>
    /// Lesión resultante de una recaída: misma familia y más grave; si no hay ninguna peor,
    /// se agrava la misma un 25 % y sube un grado. `roll` ∈ [0,1).
    /// </summary>
    public static InjuryDef Relapse(InjuryDef original, double roll)
    {
        var candidates = InjuryCatalog.WorsenCandidates(original);
        if (candidates.Count > 0)
        {
            int total = 0;
            foreach (var c in candidates) total += c.weight;
            double r = roll * total;
            double cum = 0;
            foreach (var c in candidates)
            {
                cum += c.weight;
                if (r <= cum) return c;
            }
            return candidates[0];
        }

        var fallback = original ?? InjuryCatalog.All[0];
        var bumped = new InjuryDef
        {
            name = fallback.name,
            family = fallback.family,
            minDays = (int)Math.Round(fallback.minDays * 1.25),
            maxDays = (int)Math.Round(fallback.maxDays * 1.25),
            weight = fallback.weight,
            severity = (InjurySeverity)Math.Min(3, (int)fallback.severity + 1),
            relapseRisk = fallback.relapseRisk
        };
        return bumped;
    }

    /// <summary>Días de baja de una lesión sorteada a partir de su rango. `roll` ∈ [0,1).</summary>
    public static int RollDays(InjuryDef d, double roll)
    {
        if (d == null) return 0;
        int span = d.maxDays - d.minDays + 1;
        if (span < 1) return d.minDays;
        int idx = (int)(roll * span);
        if (idx < 0) idx = 0;
        if (idx >= span) idx = span - 1;
        return d.minDays + idx;
    }
}
