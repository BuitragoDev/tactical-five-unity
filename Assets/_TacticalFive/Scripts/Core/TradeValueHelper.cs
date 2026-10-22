using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Valoración de traspasos para la IA: cuánto vale un jugador y una ronda del draft en
/// unidades comparables. Es la fuente única del "¿es razonable este intercambio?" y la
/// usan tanto las ofertas que la IA te hace como los traspasos IA-IA.
///
/// Sin esto, <see cref="TradeHelper.ValidateTrade"/> sólo mira salarios y la IA llegaba a
/// ofrecer dos estrellas (84 + 85) por un jugador de media 76 si los salarios cuadraban.
///
/// Reglas:
///  - <see cref="BaseValue"/>: curva por OVR (cada punto por encima de 80 vale mucho más).
///  - Edad y potencial: los jóvenes valen más (proyección); los veteranos, menos.
///  - Contrato: barato respecto a su valor de mercado suma; caro resta.
///  - Rondas: mismas unidades (reutiliza <see cref="TradeHelper.PickBonus"/>).
///  - <see cref="CheckOffer"/>: un equipo nunca debe ENTREGAR mucho más valor del que
///    recibe (tolerancia por estrategia) ni dar un jugador claramente mejor que el que
///    recibe (<see cref="MaxOvrGap"/>).
///  - <see cref="IsSaneBothWays"/>: los traspasos IA-IA se validan por los dos lados, con
///    una banda más ancha (<see cref="BilateralTolerance"/>) porque las dos partes negocian.
///  - <see cref="TargetOvrMargin"/>: hasta dónde puede aspirar cada estrategia (la IA no
///    persigue lo que no puede pagar).
/// Todo es puro (sin DB ni UnityEngine.Random): testeable en EditMode.
/// </summary>
public static class TradeValueHelper
{
    /// <summary>
    /// Diferencia máxima de media (OVR) entre el mejor jugador que un equipo entrega y el
    /// mejor que recibe. Evita el clásico "dos estrellas por un rol": ningún GM entrega a
    /// un 85 por un 76, por mucho que los salarios encajen. Si a cambio llegan primeras
    /// rondas el margen sube (ver <see cref="CheckOffer"/>): "rol + R1 por un titular
    /// mejor" sí es un traspaso real.
    /// </summary>
    public const int MaxOvrGap = 3;

    /// <summary>
    /// Por debajo de esta fracción del valor recibido la oferta es un insulto (lowball) y
    /// la IA ni la plantea: no pierde el tiempo proponiendo un 70 por una estrella.
    /// </summary>
    public const double MinOfferRatio = 0.60;

    // Curva de valor por OVR (unidades arbitrarias, comparables con las rondas).
    static readonly (int overall, double value)[] Curve =
    {
        (55, 0.4), (60, 0.8), (65, 1.5), (68, 2.4), (70, 3.5), (72, 5.0),
        (74, 6.3), (75, 7.0), (76, 8.5), (78, 11.0), (80, 15.0), (82, 21.0),
        (84, 28.0), (86, 38.0), (88, 50.0), (90, 65.0), (92, 85.0), (95, 130.0),
    };

    // ── VALOR BASE ────────────────────────────────────────────────────────────

    /// <summary>Valor base por media (interpolado en la curva; extremos fijados).</summary>
    public static double BaseValue(int overall)
    {
        if (overall <= Curve[0].overall) return Curve[0].value;
        var last = Curve[Curve.Length - 1];
        if (overall >= last.overall) return last.value;

        for (int i = 1; i < Curve.Length; i++)
        {
            var lo = Curve[i - 1];
            var hi = Curve[i];
            if (overall > hi.overall) continue;
            double t = (overall - lo.overall) / (double)(hi.overall - lo.overall);
            return lo.value + (hi.value - lo.value) * t;
        }
        return last.value;
    }

    /// <summary>Modificador por edad: proyección en jóvenes, declive en veteranos.</summary>
    public static double AgeFactor(int age)
    {
        if (age <= 0) return 1.0;
        if (age <= 21) return 1.25;
        if (age <= 24) return 1.15;
        if (age <= 27) return 1.05;
        if (age <= 30) return 1.00;
        if (age <= 33) return 0.85;
        if (age <= 35) return 0.70;
        return 0.50;
    }

    /// <summary>Margen de crecimiento (potential − overall), sólo para jugadores ≤ 24 años.</summary>
    public static double PotentialFactor(PlayerData p)
    {
        if (p == null || p.age > 24) return 1.0;
        int headroom = Math.Max(0, p.potential - p.overall);
        return 1.0 + Math.Min(headroom, 10) * 0.03;   // hasta +30 %
    }

    /// <summary>
    /// Salario "de mercado" esperado para una media. Reutiliza
    /// <see cref="TradeHelper.MarketValueByOvr"/> (la fuente única de la economía) y añade
    /// los tramos bajos que esa función no cubre (devuelve 0 por debajo de 75).
    /// </summary>
    public static long ExpectedSalary(int overall, long cap)
    {
        if (cap <= 0) cap = TradeHelper.SALARY_CAP;
        long market = TradeHelper.MarketValueByOvr(overall, cap);
        if (market > 0) return market;
        if (overall >= 72) return (long)(cap * 0.18);
        if (overall >= 68) return (long)(cap * 0.12);
        if (overall >= 64) return (long)(cap * 0.07);
        return (long)(cap * 0.03);
    }

    /// <summary>
    /// Ajuste por contrato, en los mismos puntos que la curva: un jugador que cobra por
    /// debajo de su valor de mercado suma (activo barato), uno sobrepagado resta.
    /// </summary>
    public static double ContractAdjustment(PlayerData p, long cap)
    {
        if (p == null || p.salary <= 0) return 0;   // sin contrato (FA): no ajustamos
        if (cap <= 0) cap = TradeHelper.SALARY_CAP;
        long expected = ExpectedSalary(p.overall, cap);
        if (expected <= 0) return 0;
        double surplusPct = (expected - p.salary) / (double)cap;
        return Math.Max(-6.0, Math.Min(10.0, surplusPct * 100.0));
    }

    /// <summary>Valor de traspaso de un jugador (curva × edad × potencial + contrato).</summary>
    public static double PlayerValue(PlayerData p, long salaryCap = 0)
    {
        if (p == null) return 0;
        if (salaryCap <= 0) salaryCap = TradeHelper.SALARY_CAP;
        double value = BaseValue(p.overall) * AgeFactor(p.age) * PotentialFactor(p);
        value += ContractAdjustment(p, salaryCap);
        return Math.Max(0.1, value);
    }

    /// <summary>Valor de una ronda, en las mismas unidades que <see cref="PlayerValue"/>.</summary>
    public static double PickValue(DraftPickData pk, int currentDraftYear = 0)
        => pk == null ? 0 : TradeHelper.PickBonus(pk, currentDraftYear) * 0.8;

    public static double PlayersValue(IEnumerable<PlayerData> players, long salaryCap = 0)
        => players == null ? 0 : players.Sum(p => PlayerValue(p, salaryCap));

    public static double PicksValue(IEnumerable<DraftPickData> picks, int currentDraftYear = 0)
        => picks == null ? 0 : picks.Sum(pk => PickValue(pk, currentDraftYear));

    /// <summary>Cuánto valor extra puede regalar un equipo según su plan.</summary>
    public static double Tolerance(TeamStrategy strategy)
    {
        switch (strategy)
        {
            case TeamStrategy.Contend: return 1.20;   // sobrepaga si tapa su posición débil
            case TeamStrategy.Balanced: return 1.05;
            default: return 1.00;                     // Rebuild no regala nada
        }
    }

    /// <summary>
    /// Margen de media que la IA puede aspirar a conseguir por encima de lo que es capaz
    /// de ofrecer en jugadores. Los contenders aspiran más alto porque completan el pago
    /// con rondas; un rebuild, en cambio, no persigue lo que no puede pagar.
    /// </summary>
    public static int TargetOvrMargin(TeamStrategy strategy)
    {
        switch (strategy)
        {
            case TeamStrategy.Contend: return 6;
            case TeamStrategy.Balanced: return 4;
            default: return MaxOvrGap;
        }
    }

    /// <summary>
    /// Banda de razonabilidad de un traspaso IA-IA, donde las dos partes negocian y ambas
    /// aceptan un intercambio algo desequilibrado (se compensa con agregado de valor):
    /// más ancha que <see cref="Tolerance"/>, que mide cuánto puede SOBREPAGAR un equipo.
    /// </summary>
    public const double BilateralTolerance = 1.20;

    // ── VALIDACIÓN DE LA OFERTA ───────────────────────────────────────────────

    public struct OfferCheck
    {
        /// <summary>Valor de lo que RECIBE el equipo analizado.</summary>
        public double Incoming;
        /// <summary>Valor de lo que ENTREGA el equipo analizado.</summary>
        public double Outgoing;
        public int BestIncomingOvr;
        public int BestOutgoingOvr;
        public double Tolerance;
        public bool ValueOk;
        public bool OvrGapOk;
        /// <summary>La oferta es razonable para el equipo analizado.</summary>
        public bool Sane;
        public string Reason;

        /// <summary>Fracción del valor recibido que se entrega (&gt; 1 = pierde valor).</summary>
        public double Ratio => Incoming <= 0 ? (Outgoing <= 0 ? 1.0 : double.MaxValue) : Outgoing / Incoming;
    }

    /// <summary>
    /// ¿Es razonable que `quien entrega` dé `givesPlayers` + `givesPicks` a cambio de
    /// `receivesPlayers` + `receivesPicks`? Se analiza el lado de UN equipo; para un
    /// traspaso IA-IA hay que llamarlo con los dos lados.
    /// </summary>
    public static OfferCheck CheckOffer(
        TeamStrategy strategy,
        IEnumerable<PlayerData> givesPlayers, IEnumerable<DraftPickData> givesPicks,
        IEnumerable<PlayerData> receivesPlayers, IEnumerable<DraftPickData> receivesPicks,
        long salaryCap = 0, int currentDraftYear = 0)
        => CheckOfferWithTolerance(Tolerance(strategy), givesPlayers, givesPicks,
                                   receivesPlayers, receivesPicks, salaryCap, currentDraftYear);

    static OfferCheck CheckOfferWithTolerance(
        double tolerance,
        IEnumerable<PlayerData> givesPlayers, IEnumerable<DraftPickData> givesPicks,
        IEnumerable<PlayerData> receivesPlayers, IEnumerable<DraftPickData> receivesPicks,
        long salaryCap, int currentDraftYear)
    {
        var gives = givesPlayers?.ToList() ?? new List<PlayerData>();
        var receives = receivesPlayers?.ToList() ?? new List<PlayerData>();

        double outgoing = PlayersValue(gives, salaryCap) + PicksValue(givesPicks, currentDraftYear);
        double incoming = PlayersValue(receives, salaryCap) + PicksValue(receivesPicks, currentDraftYear);

        int bestOut = gives.Count > 0 ? gives.Max(p => p.overall) : 0;
        int bestIn = receives.Count > 0 ? receives.Max(p => p.overall) : 0;

        bool valueOk = outgoing <= incoming * tolerance + 1e-6;

        // La regla de media admite holgura si a cambio llegan primeras rondas: "jugador de
        // rol + R1 por un titular mejor" es un traspaso real (hasta 2 rondas, +6 de margen).
        int incomingFirstRounders = receivesPicks?.Count(pk => pk != null && pk.round == 1) ?? 0;
        int allowedGap = MaxOvrGap + 3 * Math.Min(incomingFirstRounders, 2);
        bool ovrGapOk = bestOut <= bestIn + allowedGap;

        string reason = null;
        if (!ovrGapOk)
            reason = $"entregaría un {bestOut} por un {bestIn}";
        else if (!valueOk)
            reason = $"entregaría {outgoing:0.0} de valor y recibiría {incoming:0.0}";

        return new OfferCheck
        {
            Incoming = incoming,
            Outgoing = outgoing,
            BestIncomingOvr = bestIn,
            BestOutgoingOvr = bestOut,
            Tolerance = tolerance,
            ValueOk = valueOk,
            OvrGapOk = ovrGapOk,
            Sane = valueOk && ovrGapOk,
            Reason = reason
        };
    }

    public static bool IsSane(TeamStrategy strategy,
                              IEnumerable<PlayerData> givesPlayers, IEnumerable<DraftPickData> givesPicks,
                              IEnumerable<PlayerData> receivesPlayers, IEnumerable<DraftPickData> receivesPicks,
                              long salaryCap = 0, int currentDraftYear = 0)
        => CheckOffer(strategy, givesPlayers, givesPicks, receivesPlayers, receivesPicks,
                      salaryCap, currentDraftYear).Sane;

    /// <summary>
    /// Traspaso IA-IA: ha de ser razonable para LOS DOS equipos (banda bilateral). Se
    /// analiza desde el lado de cada uno: lo que da A frente a lo que recibe A, y lo
    /// mismo con B.
    /// </summary>
    public static bool IsSaneBothWays(
        TeamStrategy strategyA,
        List<PlayerData> aGives, List<DraftPickData> aPicks,
        TeamStrategy strategyB,
        List<PlayerData> bGives, List<DraftPickData> bPicks,
        long salaryCap = 0, int currentDraftYear = 0)
    {
        bool aSane = CheckOfferWithTolerance(BilateralTolerance, aGives, aPicks, bGives, bPicks,
                                             salaryCap, currentDraftYear).Sane;
        if (!aSane) return false;
        return CheckOfferWithTolerance(BilateralTolerance, bGives, bPicks, aGives, aPicks,
                                       salaryCap, currentDraftYear).Sane;
    }
}
