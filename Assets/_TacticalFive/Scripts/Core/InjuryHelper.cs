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

    /// <summary>Máximo de días restantes que aún permite forzar el alta (más que eso = demasiado grave).</summary>
    public const int MAX_FORCE_RETURN_DAYS = 7;

    /// <summary>
    /// ¿Se puede forzar el alta con estos días restantes? Solo entre 2 y 7 días: con 0-1 no
    /// compensa (ya se recupera) y con más de 7 la lesión es demasiado grave para arriesgarse.
    /// </summary>
    public static bool CanForceReturn(int daysRemaining)
        => daysRemaining >= 2 && daysRemaining <= MAX_FORCE_RETURN_DAYS;

    /// <summary>Riesgo (0-100) que asume el jugador si se le da el alta antes de tiempo.</summary>
    public static int ForceReturnRisk(int remaining, InjurySeverity sev)
    {
        if (remaining <= 0) return 0;
        int risk = 20 + (int)sev * 15 + remaining * 2;
        return Math.Min(90, risk);
    }

    /// <summary>
    /// Probabilidad de recaída POR PARTIDO jugado (0-1) con un riesgo pendiente. Se mantiene
    /// por debajo del riesgo nominal (un 90% no implica caer seguro en cada partido), pero lo
    /// bastante alta para que se note. Con físico bajo (<50) aumenta un 50%.
    /// </summary>
    public static float RelapseChancePerGame(int risk, int fisico)
    {
        if (risk <= 0) return 0f;
        float baseChance = risk / 100f * 0.6f;
        if (fisico < 50) baseChance *= 1.5f;
        return Math.Min(0.75f, baseChance);
    }

    /// <summary>Probabilidad de recaída por partido en porcentaje (para el parte médico/UI).</summary>
    public static int RelapseChancePercent(int risk, int fisico)
        => (int)System.Math.Round(RelapseChancePerGame(risk, fisico) * 100f);

    // ── RIESGO DE LESIÓN POR PARTIDO (F2-07) ─────────────────────

    /// <summary>Minutos de exposición con los que se mantiene la probabilidad base.</summary>
    public const int INJURY_REFERENCE_MINUTES = 36;

    /// <summary>Probabilidad base de lesión en un partido a plena exposición (36 min).</summary>
    public const float INJURY_BASE_PER_GAME = 0.008f;

    /// <summary>
    /// Probabilidad (0-1) de lesionarse en UN partido. Escala con la **exposición**
    /// (minutos jugados, proporcional sobre 36), con el **físico bajo** (&lt; 30) y con el
    /// **riesgo de recaída** pendiente de un alta forzada. 0 minutos ⇒ 0: quien no juega
    /// no se lesiona. El sorteo es **postpartido**: no altera minutos ni resultado.
    /// </summary>
    public static float MatchRiskPerGame(int minutes, int fisico, int recoveryRisk)
    {
        if (minutes <= 0) return 0f;

        float chance = INJURY_BASE_PER_GAME * minutes / INJURY_REFERENCE_MINUTES;
        if (fisico < 30)
            chance *= 1f + (30 - fisico) * 0.15f;
        chance += RelapseChancePerGame(recoveryRisk, fisico);
        return Math.Min(1f, chance);
    }

    /// <summary>Riesgo de lesión por partido en porcentaje (parte médico/UI).</summary>
    public static int MatchRiskPercent(int minutes, int fisico, int recoveryRisk)
        => (int)System.Math.Round(MatchRiskPerGame(minutes, fisico, recoveryRisk) * 100f);

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
