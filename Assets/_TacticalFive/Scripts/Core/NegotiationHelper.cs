using UnityEngine;

/// <summary>
/// Negociación con el agente de un jugador. Cuando una oferta se rechaza por dinero,
/// el agente hace una contraoferta: un escalón proporcional a lo lejos que quedó la
/// aceptación, redondeado a 100k y nunca por encima del máximo legal del equipo.
/// Lógica pura y testeable.
/// </summary>
public static class NegotiationHelper
{
    /// <summary>
    /// Contraoferta del agente. Devuelve 0 si no hay margen (la oferta ya estaba en el
    /// techo legal o por encima).
    /// </summary>
    /// <param name="offeredSalary">Salario que ofreciste y fue rechazado.</param>
    /// <param name="acceptScore">Puntuación de aceptación (1..100) que no llegó.</param>
    /// <param name="legalMax">Salario máximo que el equipo puede ofrecer legalmente.</param>
    public static long CounterSalary(long offeredSalary, int acceptScore, long legalMax)
    {
        if (legalMax <= offeredSalary) return 0;

        // Cuanto más lejos quedó la aceptación, mayor el salto (11%..38%).
        int gap = Mathf.Clamp(100 - acceptScore, 5, 60);
        long step = offeredSalary * (8 + gap / 2) / 100;

        long target = RoundTo100k(offeredSalary + step);
        if (target <= offeredSalary) target = RoundTo100k(offeredSalary + 100_000);
        return System.Math.Min(target, legalMax);
    }

    public static long RoundTo100k(long value) => (value / 100_000) * 100_000;
}
