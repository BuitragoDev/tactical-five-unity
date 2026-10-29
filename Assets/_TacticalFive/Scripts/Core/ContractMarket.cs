/// <summary>
/// Reglas puras de la renovación de patrocinio/TV al expirar (E3). El equipo busca
/// sustituto si su mercado lo permite; si no, se queda con una oferta menor o sin acuerdo.
/// Todo recibe el azar desde fuera para poder testearse.
/// </summary>
public static class ContractMarket
{
    /// <summary>Precio pedido en la renovación: el base de la oferta escalado por fama y mercado.</summary>
    public static (long fee, long homeIncome) RenewalPrice(long baseFee, long baseHomeIncome,
                                                           int teamReputation, int marketSize, double roll)
        => MarketEconomy.TvContractValue(baseFee, baseHomeIncome, teamReputation, marketSize, roll);

    /// <summary>
    /// ¿Encuentra el equipo un patrocinador/TV al expirar? Un gran mercado con prestigio
    /// casi siempre sí; uno pequeño puede quedarse sin acuerdo. `roll` ∈ [0,1).
    /// </summary>
    public static bool FindsReplacement(int teamReputation, int marketSize, double roll)
        => MarketEconomy.ShouldRenewDeal(teamReputation, marketSize, roll);

    // Rebalanceo de economía: los contratos deben sostener una franquicia, no solo
    // complementar la taquilla. Se aplican al firmar y a cada partido en casa.
    public const float SponsorInitialMultiplier = 1.5f;
    public const float HomeGameIncomeMultiplier = 1.25f;

    public static long SponsorInitialIncome(long baseInitialIncome)
        => (long)System.Math.Max(0, baseInitialIncome * SponsorInitialMultiplier);

    public static long TvInitialIncome(long baseInitialIncome)
        => (long)System.Math.Max(0, baseInitialIncome * SponsorInitialMultiplier);

    /// <summary>
    /// Valor de un contrato de patrocinio a partir de la oferta base y el atractivo del club
    /// (reputación + mercado). Se usa para escalar el home_game_income que paga cada partido.
    /// </summary>
    public static long SponsorHomeGameIncome(long baseHomeIncome, int teamReputation, int marketSize)
        => (long)System.Math.Max(0, baseHomeIncome * HomeGameIncomeMultiplier
                                    * MarketEconomy.DealValueMultiplier(teamReputation, marketSize));
}
