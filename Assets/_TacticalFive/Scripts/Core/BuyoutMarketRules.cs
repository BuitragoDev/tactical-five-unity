using System.Collections.Generic;
using System.Linq;

/// <summary>Candidato a firmar a un jugador rescindido (datos del equipo ya leídos).</summary>
public class BuyoutCandidate
{
    public int teamId;
    public TeamStrategy strategy;
    public float teamAvg;
    public int samePosCount;
    /// <summary>Prestigio del equipo (1-5): atrae más a los jugadores.</summary>
    public int reputation;
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

    /// <summary>
    /// Cuántos equipos pujan de verdad por un jugador rescindido. Entre ellos se sortea el
    /// destino (ponderado por el puesto en el ranking), como en la realidad: el mejor
    /// candidato es el más probable, pero no el único.
    /// </summary>
    public const int SuitorCount = 3;

    /// <summary>Equipos candidatos ordenados de mejor a peor destino para el jugador.</summary>
    public static List<BuyoutCandidate> RankDestinations(IReadOnlyList<BuyoutCandidate> candidates)
    {
        if (candidates == null) return new List<BuyoutCandidate>();

        return candidates
            .Where(c => c != null && c.hasRoom)
            // El jugador quiere competir: contender > balanced > rebuild.
            .OrderByDescending(c => Priority(c.strategy))
            // Minutos disponibles: menos jugadores en su posición = más hueco.
            .ThenBy(c => c.samePosCount)
            // Proyecto: mejor plantilla y más prestigio atraen más.
            .ThenByDescending(c => c.teamAvg)
            .ThenByDescending(c => c.reputation)
            // Desempate estable (para que el ranking sea reproducible).
            .ThenBy(c => c.teamId)
            .ToList();
    }

    /// <summary>Mejor destino (teamId) o -1 si ninguno puede firmarle.</summary>
    public static int BestDestination(IReadOnlyList<BuyoutCandidate> candidates)
    {
        var ranked = RankDestinations(candidates);
        return ranked.Count > 0 ? ranked[0].teamId : -1;
    }

    /// <summary>
    /// Sorteo ponderado entre los `SuitorCount` mejores candidatos del ranking: el 1º tiene
    /// el doble de probabilidad que el 2º y el triple que el 3º. Devuelve el índice elegido
    /// (0 = mejor destino) o -1 si no hay candidatos. Puro (recibe el RNG del hilo de IA).
    /// </summary>
    public static int PickSuitor(int rankedCount, System.Random rng)
    {
        int n = System.Math.Min(SuitorCount, rankedCount);
        if (n <= 0) return -1;
        if (rng == null) return 0;
        if (n == 1) return 0;

        int total = n * (n + 1) / 2;            // 3, 2, 1 → 6
        int roll = rng.Next(1, total + 1);
        int acc = 0;
        for (int i = 0; i < n; i++)
        {
            acc += n - i;                        // peso del puesto i
            if (roll <= acc) return i;
        }
        return 0;
    }

    /// <summary>¿Puede la IA firmarle en esta jornada? (el mismo día de la rescisión o después)</summary>
    public static bool CanSignOn(int waivedGameDay, int currentGameDay)
        => waivedGameDay >= 0 && currentGameDay >= waivedGameDay;

    /// <summary>Salario que firma un rescindido: el mínimo de liga.</summary>
    public static long WaivedSalary(long minSalary)
        => minSalary > 0 ? minSalary : TradeHelper.MIN_SALARY;
}
