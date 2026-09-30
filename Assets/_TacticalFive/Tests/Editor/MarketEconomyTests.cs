using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Economía de mercado (E3): asistencia elástica, tamaño de mercado, valor de contratos
/// y renegociación. Reglas puras.
/// </summary>
public class MarketEconomyTests
{
    // ── ASISTENCIA ──────────────────────────────────────

    [Test]
    public void EstimateHomeAttendance_NeverExceedsCapacity()
    {
        for (int i = 0; i <= 20; i++)
        {
            int att = MarketEconomy.EstimateHomeAttendance(
                capacity: 18000, winPct: 0.9f, rivalWinPct: 0.8f, rivalReputation: 5,
                marketSize: 100, teamReputation: 5, objectiveFactor: 1.0f, streakFactor: 1.10f,
                fanConfidence01: 1f, includeFanConfidence: true, ticketPrice: 30f,
                roll1: i / 20.0, roll2: i / 25.0);
            Assert.That(att, Is.InRange(0, 18000));
        }
    }

    [Test]
    public void MarketSize_RaisesAttendance()
    {
        int small = MarketEconomy.EstimateHomeAttendance(20000, 0.5f, 0.5f, 3, 30, 3, 1f, 1f, 0.5f, true, 45f, 0.5, 0.5);
        int big = MarketEconomy.EstimateHomeAttendance(20000, 0.5f, 0.5f, 3, 95, 3, 1f, 1f, 0.5f, true, 45f, 0.5, 0.5);
        Assert.That(big, Is.GreaterThan(small), "un gran mercado llena más");
    }

    [Test]
    public void Price_AboveFair_ReducesAttendance()
    {
        int cheap = MarketEconomy.EstimateHomeAttendance(20000, 0.6f, 0.5f, 3, 70, 4, 1f, 1f, 0.6f, true, 45f, 0.5, 0.5);
        int expensive = MarketEconomy.EstimateHomeAttendance(20000, 0.6f, 0.5f, 3, 70, 4, 1f, 1f, 0.6f, true, 400f, 0.5, 0.5);
        Assert.That(expensive, Is.LessThan(cheap), "subir el precio reduce la ocupación");
    }

    [Test]
    public void WinPct_RaisesAttendance()
    {
        int losing = MarketEconomy.EstimateHomeAttendance(20000, 0.1f, 0.5f, 3, 60, 3, 1f, 1f, 0.4f, true, 45f, 0.5, 0.5);
        int winning = MarketEconomy.EstimateHomeAttendance(20000, 0.9f, 0.5f, 3, 60, 3, 1f, 1f, 0.4f, true, 45f, 0.5, 0.5);
        Assert.That(winning, Is.GreaterThan(losing));
    }

    [Test]
    public void RivalReputation_RaisesAttendance()
    {
        int boring = MarketEconomy.EstimateHomeAttendance(20000, 0.5f, 0.4f, 1, 60, 3, 1f, 1f, 0.5f, true, 45f, 0.5, 0.5);
        int glamour = MarketEconomy.EstimateHomeAttendance(20000, 0.5f, 0.7f, 5, 60, 3, 1f, 1f, 0.5f, true, 45f, 0.5, 0.5);
        Assert.That(glamour, Is.GreaterThan(boring), "el rival estrella llena más");
    }

    [Test]
    public void FanConfidence_OnlyCountsForTheUser()
    {
        int away = MarketEconomy.EstimateHomeAttendance(20000, 0.5f, 0.5f, 3, 60, 3, 1f, 1f, 0.05f, false, 45f, 0.5, 0.5);
        int home = MarketEconomy.EstimateHomeAttendance(20000, 0.5f, 0.5f, 3, 60, 3, 1f, 1f, 1.0f, true, 45f, 0.5, 0.5);
        Assert.That(home, Is.GreaterThan(away), "la confianza de la afición solo aplica en casa del usuario");
    }

    [Test]
    public void AttendancePriceFactor_SaturatesAtTheTop()
    {
        // Muy barato: como máximo un 10 % más (no se puede pasar del aforo).
        Assert.That(MarketEconomy.AttendancePriceFactor(0f, 50f), Is.EqualTo(1.10f).Within(0.001f));
        // Precio justo: referencia 1.0.
        Assert.That(MarketEconomy.AttendancePriceFactor(50f, 50f), Is.EqualTo(1.0f).Within(0.001f));
        // Caro: cae.
        Assert.That(MarketEconomy.AttendancePriceFactor(150f, 50f), Is.LessThan(0.6f));
        Assert.That(MarketEconomy.AttendancePriceFactor(100000f, 50f), Is.GreaterThanOrEqualTo(0.15f));
    }

    [Test]
    public void FairTicketPrice_GrowsWithMarketAndReputation()
    {
        float small = MarketEconomy.FairTicketPrice(20, 1);
        float big = MarketEconomy.FairTicketPrice(95, 5);
        Assert.That(big, Is.GreaterThan(small));
        Assert.That(small, Is.GreaterThan(0f));
    }

    // ── CONTRATOS ───────────────────────────────────────

    [Test]
    public void DealValueMultiplier_Range()
    {
        float worst = MarketEconomy.DealValueMultiplier(0, 0);
        float best = MarketEconomy.DealValueMultiplier(5, 100);
        Assert.That(worst, Is.InRange(0.6f, 0.8f));
        Assert.That(best, Is.InRange(1.4f, 1.6f));
        Assert.That(best, Is.GreaterThan(worst));
    }

    [Test]
    public void SponsorAnnualValue_ScalesWithFame()
    {
        long small = MarketEconomy.SponsorAnnualValue(10_000_000, 1, 25, 0.5);
        long big = MarketEconomy.SponsorAnnualValue(10_000_000, 5, 95, 0.5);
        Assert.That(big, Is.GreaterThan(small));
        Assert.That(MarketEconomy.SponsorAnnualValue(0, 5, 95, 0.5), Is.EqualTo(0));
    }

    [Test]
    public void TvContractValue_Shapes()
    {
        var (fee, home) = MarketEconomy.TvContractValue(50_000_000, 2_000_000, 5, 95, 0.5);
        Assert.That(fee, Is.GreaterThan(50_000_000));
        Assert.That(home, Is.GreaterThan(2_000_000));
        var (fee0, home0) = MarketEconomy.TvContractValue(50_000_000, 2_000_000, 1, 20, 0.5);
        Assert.That(fee0, Is.LessThan(fee));
        Assert.That(home0, Is.LessThan(home));
    }

    [Test]
    public void ShouldRenewDeal_BigMarketsMoreLikely()
    {
        // Un gran mercado con prestigio renueva con casi cualquier roll; uno pequeño,
        // solo con rolls muy bajos.
        Assert.That(MarketEconomy.ShouldRenewDeal(5, 100, 0.5), Is.True);
        Assert.That(MarketEconomy.ShouldRenewDeal(1, 20, 0.99), Is.False);
    }

    [Test]
    public void MarketNormalized_ClampedTo01()
    {
        Assert.That(MarketEconomy.MarketNormalized(-40), Is.EqualTo(0f));
        Assert.That(MarketEconomy.MarketNormalized(100), Is.EqualTo(1f));
        Assert.That(MarketEconomy.MarketNormalized(50), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(MarketEconomy.MarketNormalized(200), Is.EqualTo(1f));
    }
}
