using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Reglas simplificadas de derechos y cap holds. No pretende replicar el CBA.</summary>
public static class FreeAgencyRules
{
    public const int MAX_RFA_SERVICE_YEARS = 3;

    public static string TypeForService(int serviceYears, bool isTwoWay = false)
    {
        if (isTwoWay) return "UFA";
        return serviceYears <= MAX_RFA_SERVICE_YEARS ? "RFA" : "UFA";
    }

    public static long QualifyingOffer(long previousSalary, long minimumSalary)
    {
        long increase = (long)Math.Ceiling(Math.Max(0L, previousSalary) * 1.25d);
        return Math.Max(Math.Max(0L, minimumSalary), increase);
    }

    /// <summary>
    /// Hold de diseño: RFA = qualifying offer; UFA = 120% (sin Bird) o 150%
    /// (tres o más temporadas con el equipo). Las cifras se versionarán en F4-07.
    /// </summary>
    public static long CapHold(long previousSalary, int serviceYears, int seasonsWithTeam, long qualifyingOffer, long minimumSalary)
    {
        if (TypeForService(serviceYears) == "RFA")
            return Math.Max(minimumSalary, qualifyingOffer);

        double multiplier = seasonsWithTeam >= 3 ? 1.50d : 1.20d;
        return Math.Max(Math.Max(0L, minimumSalary), (long)Math.Ceiling(Math.Max(0L, previousSalary) * multiplier));
    }

    /// <summary>La nómina de cap excluye two-way e incluye cap holds vivos no duplicados.</summary>
    public static long EffectivePayroll(IEnumerable<PlayerData> players, IEnumerable<TeamCapHoldData> holds)
    {
        var roster = players?.ToList() ?? new List<PlayerData>();
        var signedIds = new HashSet<int>(roster.Where(p => p.team_id != 0 && p.contract_years > 0).Select(p => p.id));
        long salaries = roster.Where(p => p.is_two_way != 1 && p.contract_years > 0).Sum(p => Math.Max(0L, p.salary));
        long capHolds = (holds ?? Enumerable.Empty<TeamCapHoldData>())
            .Where(h => h.is_active == 1 && !signedIds.Contains(h.player_id))
            .Sum(h => Math.Max(0L, h.amount));
        return salaries + capHolds;
    }

    /// <summary>
    /// Regla de matching de la IA: conserva a una RFA de 80+ OVR, o iguala
    /// ofertas que no superen en más del 50% el qualifying offer. La legalidad
    /// contractual se valida aparte por el llamador.
    /// </summary>
    public static bool ShouldAIMatch(string freeAgentType, int overall, long offerSalary, long qualifyingOffer)
    {
        if (freeAgentType != "RFA" || offerSalary <= 0) return false;
        return overall >= 80 || (qualifyingOffer > 0 && offerSalary <= qualifyingOffer * 1.50d);
    }
}
