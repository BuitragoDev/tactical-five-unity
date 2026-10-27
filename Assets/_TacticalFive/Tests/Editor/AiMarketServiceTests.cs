using System;
using NUnit.Framework;

/// <summary>
/// F8-02: reglas de calendario e inyección de estrategia del mercado IA.
/// </summary>
public class AiMarketServiceTests
{
    [Test]
    public void TransferWindow_IsSeptemberFirstToFebruaryEighth()
    {
        Assert.That(AiMarketService.IsTransferWindow(new DateTime(2026, 8, 31), 2026, 2027), Is.False);
        Assert.That(AiMarketService.IsTransferWindow(new DateTime(2026, 9, 1), 2026, 2027), Is.True);
        Assert.That(AiMarketService.IsTransferWindow(new DateTime(2027, 2, 8), 2026, 2027), Is.True);
        Assert.That(AiMarketService.IsTransferWindow(new DateTime(2027, 2, 9), 2026, 2027), Is.False);
    }

    [Test]
    public void RenewalWindow_OpensOnFebruaryTwelfthAndStaysOpenAfter()
    {
        var before = new SeasonData { year_end = 2027, current_date = "2027-02-11" };
        var deadline = new SeasonData { year_end = 2027, current_date = "2027-02-12" };
        var after = new SeasonData { year_end = 2027, current_date = "2027-02-13" };

        Assert.That(AiMarketService.IsRenewalDue(before), Is.False);
        Assert.That(AiMarketService.IsRenewalDue(deadline), Is.True);
        Assert.That(AiMarketService.IsRenewalDue(after), Is.True);
        Assert.That(AiMarketService.IsRenewalDue(null), Is.False);
    }

    [Test]
    public void StrategyAndDeadline_AreInjected()
    {
        var service = new AiMarketService(
            null, null, null, new Random(7), null,
            id => id == 42 ? TeamStrategy.Contend : TeamStrategy.Rebuild,
            null,
            team => team?.id == 42,
            () => true);

        Assert.That(service.StrategyFor(42), Is.EqualTo(TeamStrategy.Contend));
        Assert.That(service.StrategyFor(7), Is.EqualTo(TeamStrategy.Rebuild));
        Assert.That(service.IsDeadlineWeekActive, Is.True);
    }
}
