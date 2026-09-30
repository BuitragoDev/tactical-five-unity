using System;
using UnityEngine;

/// <summary>
/// Economía de mercado (E3). Reglas puras para la asistencia elástica, el tamaño de mercado,
/// el atractivo del rival y el valor de un contrato de patrocinio/TV según fama.
/// Todo recibe el `roll`/valores de azar desde fuera para poder testearse.
/// </summary>
public static class MarketEconomy
{
    // ── ASISTENCIA ────────────────────────────────────────

    /// <summary>
    /// Tamaño de mercado normalizado (0-1). Mercados grandes llenan más el pabellón y pagan
    /// más por sus derechos, incluso con malos resultados.
    /// </summary>
    public static float MarketNormalized(int marketSize) => Mathf.Clamp01(marketSize / 100f);

    /// <summary>Factor de atractivo del rival que visita (0-1 según su reputación).</summary>
    public static float RivalDrawFactor(int rivalReputation) => Mathf.Clamp01(rivalReputation / 5f);

    /// <summary>
    /// Demanda de entradas (0-1.25) para un partido en casa.
    /// - reputación del equipo (fama)
    /// - tamaño de mercado (0-1)
    /// - rendimiento (winPct 0-1)
    /// - confianza de la afición solo si el partido es del usuario (evita acoplamiento global)
    /// - gancho del rival + su récord
    /// - cumplimiento del objetivo de temporada (factor externo)
    /// - bonus de racha (caliente/fría) y ruido aleatorio
    /// </summary>
    public static float Demand01(float winPct, float rivalWinPct, int rivalReputation, int marketSize,
                                 float objectiveFactor, float streakFactor, float fanConfidence01,
                                 bool includeFanConfidence, double roll)
    {
        float demand = 0.34f
            + winPct * 0.20f
            + MarketNormalized(marketSize) * 0.10f
            + RivalDrawFactor(rivalReputation) * 0.09f
            + rivalWinPct * 0.05f;

        if (includeFanConfidence) demand += Mathf.Clamp01(fanConfidence01) * 0.13f;

        demand *= Mathf.Clamp(objectiveFactor, 0.30f, 1.0f);
        demand *= Mathf.Clamp(streakFactor, 0.85f, 1.10f);

        float noise = 0.90f + (float)roll * 0.20f;   // ±10 %
        return Mathf.Clamp(demand * noise, 0.05f, 1.25f);
    }

    /// <summary>
    /// Factor de precio con saturación al alza: subir el precio reduce la ocupación.
    /// Un precio "justo" de referencia llena el pabellón (1.0); el tramo barato apenas
    /// aumenta el aforo (no se puede superar el límite) y el tramo caro castiga de forma
    /// progresiva. `fairPrice` se deriva del tamaño de mercado.
    /// </summary>
    public static float AttendancePriceFactor(float ticketPrice, float fairPrice)
    {
        if (fairPrice <= 1f) fairPrice = 1f;
        if (ticketPrice <= fairPrice)
        {
            float below = (fairPrice - ticketPrice) / fairPrice;   // 0..1
            return Mathf.Lerp(1.0f, 1.10f, Mathf.Clamp01(below));
        }

        float above = (ticketPrice - fairPrice) / fairPrice;
        return Mathf.Clamp(Mathf.Exp(-above * 0.55f), 0.15f, 1.0f);
    }

    /// <summary>Precio de referencia de una entrada según el mercado y la reputación del equipo.</summary>
    public static float FairTicketPrice(int marketSize, int teamReputation, float basePrice = 45f)
    {
        float marketMult = 0.80f + MarketNormalized(marketSize) * 0.50f;   // 0.80x - 1.30x
        float repMult = 0.90f + Mathf.Clamp01(teamReputation / 5f) * 0.25f;// 0.90x - 1.15x
        return Mathf.Max(10f, basePrice * marketMult * repMult);
    }

    /// <summary>
    /// Ocupación final (0-1) del pabellón para un partido en casa. `roll1`/`roll2` ∈ [0,1).
    /// Devuelve directamente el aforo estimado para `capacity`.
    /// </summary>
    public static int EstimateHomeAttendance(int capacity, float winPct, float rivalWinPct,
                                             int rivalReputation, int marketSize, int teamReputation,
                                             float objectiveFactor, float streakFactor,
                                             float fanConfidence01, bool includeFanConfidence,
                                             float ticketPrice, double roll1, double roll2)
    {
        if (capacity <= 0) return 0;
        float demand = Demand01(winPct, rivalWinPct, rivalReputation, marketSize, objectiveFactor,
                                streakFactor, fanConfidence01, includeFanConfidence, roll1);
        float fair = FairTicketPrice(marketSize, teamReputation);
        float priceFactor = AttendancePriceFactor(ticketPrice, fair);
        float occupancy = Mathf.Clamp01(demand * priceFactor);
        int count = (int)Mathf.Round(capacity * occupancy);
        return Mathf.Clamp(count, 0, capacity);
    }

    // ── PATROCINIO Y TV ───────────────────────────────────

    /// <summary>
    /// Multiplicador de valor de un contrato según fama del equipo (reputación) y tamaño de
    /// mercado. Va de ~0.7x (pequeño y poco conocido) a ~1.5x (gran mercado con prestigio).
    /// </summary>
    public static float DealValueMultiplier(int teamReputation, int marketSize)
    {
        float rep = Mathf.Clamp01(teamReputation / 5f);
        float market = MarketNormalized(marketSize);
        return 0.70f + rep * 0.50f + market * 0.30f;
    }

    /// <summary>Valor anual de patrocinio a partir de un valor base y la fama. `roll` ∈ [0,1).</summary>
    public static long SponsorAnnualValue(long baseValue, int teamReputation, int marketSize, double roll)
    {
        float variance = 0.90f + (float)roll * 0.20f;
        long value = (long)(baseValue * DealValueMultiplier(teamReputation, marketSize) * variance);
        return Math.Max(0, value);
    }

    /// <summary>Valor de un contrato de TV (fee + variable por partido en casa) según la fama.</summary>
    public static (long fee, long homeGameIncome) TvContractValue(long baseFee, long baseHomeIncome,
                                                                  int teamReputation, int marketSize, double roll)
    {
        float variance = 0.90f + (float)roll * 0.20f;
        float mult = DealValueMultiplier(teamReputation, marketSize) * variance;
        return ((long)Math.Max(0, baseFee * mult), (long)Math.Max(0, baseHomeIncome * mult));
    }

    // ── RENEGOCIACIÓN DE CONTRATOS ────────────────────────

    /// <summary>
    /// ¿Renueva el equipo su patrocinio/TV al expirar? En función del atractivo de mercado:
    /// los grandes mercados casi siempre encuentran comprador; los pequeños pueden quedarse
    /// sin él. `roll` ∈ [0,1).
    /// </summary>
    public static bool ShouldRenewDeal(int teamReputation, int marketSize, double roll)
    {
        float chance = 0.45f + MarketNormalized(marketSize) * 0.35f + Mathf.Clamp01(teamReputation / 5f) * 0.20f;
        return roll < Mathf.Clamp(chance, 0.10f, 0.95f);
    }
}
