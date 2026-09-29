using System.Collections.Generic;
using System.Linq;

/// <summary>Candidato a firmar a un jugador rescindido (datos del equipo ya leídos).</summary>
public class BuyoutCandidate
{
    public int teamId;
    public TeamStrategy strategy;
    public float teamAvg;
    public int samePosCount;
    /// <summary>Puede firmarle: hueco de plantilla y encaje en cap/excepciones (el mínimo siempre cabe).</summary>
    public bool hasRoom;
}

/// <summary>
/// Reglas puras del **mercado de buyout**: los jugadores rescindidos firman por el mínimo
/// y la IA les busca destino de inmediato. Prioriza contender &gt; balanced &gt; rebuild;
/// a igual estrategia, el equipo con menos jugadores en su posición (más necesidad) y,
/// si empatan, el de mejor media.
/// </summary>
public static class BuyoutMarketRules
{
    /// <summary>Duración del contrato de un rescindido: firma de mínimo de 1 año.</summary>
    public const int ContractYears = 1;

    /// <summary>
    /// Media mínima para que el mercado de buyout le busque destino **al día siguiente**.
    /// Por debajo, el jugador queda como agente libre normal (el mercado general decidirá
    /// si algún equipo con hueco le ficha más adelante).
    /// </summary>
    public const int MinOverall = 75;

    /// <summary>¿Merece un destino inmediato en el mercado de buyout?</summary>
    public static bool IsWanted(int overall) => overall >= MinOverall;

    public static int Priority(TeamStrategy strategy)
        => strategy == TeamStrategy.Contend ? 3
         : strategy == TeamStrategy.Balanced ? 2
         : 1;

    /// <summary>Mejor destino (teamId) o -1 si ninguno puede firmarle.</summary>
    public static int BestDestination(IReadOnlyList<BuyoutCandidate> candidates)
    {
        if (candidates == null) return -1;

        var best = candidates
            .Where(c => c != null && c.hasRoom)
            .OrderByDescending(c => Priority(c.strategy))
            .ThenBy(c => c.samePosCount)
            .ThenByDescending(c => c.teamAvg)
            .ThenBy(c => c.teamId)
            .FirstOrDefault();

        return best != null ? best.teamId : -1;
    }

    /// <summary>¿Puede la IA firmarle en esta jornada? (el mismo día de la rescisión o después)</summary>
    public static bool CanSignOn(int waivedGameDay, int currentGameDay)
        => waivedGameDay >= 0 && currentGameDay >= waivedGameDay;

    /// <summary>Salario que firma un rescindido: el mínimo de liga.</summary>
    public static long WaivedSalary(long minSalary)
        => minSalary > 0 ? minSalary : TradeHelper.MIN_SALARY;
}
