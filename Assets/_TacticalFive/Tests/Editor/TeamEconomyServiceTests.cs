using NUnit.Framework;

/// <summary>
/// F8-01: reglas puras de la economía de franquicia extraídas del DashboardController.
/// </summary>
public class TeamEconomyServiceTests
{
    [Test]
    public void PayrollSchedule_HasTwelveBiweeklyPayments()
    {
        Assert.That(TeamEconomyService.PayrollDays, Has.Length.EqualTo(12));
        Assert.That(TeamEconomyService.PayrollDays[0], Is.EqualTo(1));
        Assert.That(TeamEconomyService.PayrollDays[^1], Is.EqualTo(166));

        for (int i = 1; i < TeamEconomyService.PayrollDays.Length; i++)
            Assert.That(TeamEconomyService.PayrollDays[i] - TeamEconomyService.PayrollDays[i - 1],
                Is.EqualTo(15), "Los plazos de nómina deben ser quincenales");

        Assert.That(TeamEconomyService.IsPayrollDay(1), Is.True);
        Assert.That(TeamEconomyService.IsPayrollDay(16), Is.True);
        Assert.That(TeamEconomyService.IsPayrollDay(2), Is.False);
    }

    [Test]
    public void MonthlyShare_DividesAnnualAmountByTwelve()
    {
        Assert.That(TeamEconomyService.MonthlyShare(12_000_000), Is.EqualTo(1_000_000));
        Assert.That(TeamEconomyService.MonthlyShare(11), Is.EqualTo(0));
        Assert.That(TeamEconomyService.MonthlyShare(0), Is.EqualTo(0));
    }

    [Test]
    public void LeagueDistribution_IsSixtyMillionPerSeasonInTwelveShares()
    {
        Assert.That(TeamEconomyService.LeagueDistributionAnnual, Is.EqualTo(60_000_000));
        Assert.That(TeamEconomyService.LeagueDistributionShare, Is.EqualTo(5_000_000));
    }

    [Test]
    public void ArenaTicketMultiplier_ScalesWithPabellonReputation()
    {
        Assert.That(TeamEconomyService.ArenaTicketMultiplier(0), Is.EqualTo(1.0f));
        Assert.That(TeamEconomyService.ArenaTicketMultiplier(1), Is.EqualTo(1.03f));
        Assert.That(TeamEconomyService.ArenaTicketMultiplier(5), Is.EqualTo(1.20f));
        Assert.That(TeamEconomyService.ArenaTicketMultiplier(5),
            Is.GreaterThan(TeamEconomyService.ArenaTicketMultiplier(3)));
    }

    [Test]
    public void TicketRevenue_MultipliesAttendancePriceAndArenaFactor()
    {
        Assert.That(TeamEconomyService.TicketRevenue(1_000, 50, 1.0f), Is.EqualTo(50_000));
        Assert.That(TeamEconomyService.TicketRevenue(1_000, 50, 1.2f), Is.EqualTo(60_000));
        Assert.That(TeamEconomyService.TicketRevenue(0, 50, 1.2f), Is.EqualTo(0));
    }

    [Test]
    public void SubscriptionWindow_IsAroundDayEleven()
    {
        Assert.That(TeamEconomyService.IsSubscriptionWindow(9), Is.False);
        Assert.That(TeamEconomyService.IsSubscriptionWindow(10), Is.True);
        Assert.That(TeamEconomyService.IsSubscriptionWindow(12), Is.True);
        Assert.That(TeamEconomyService.IsSubscriptionWindow(13), Is.False);
    }

    [Test]
    public void SubscriptionDemand_RespondsToPriceWinsReputationAndMarket()
    {
        int baseDemand = TeamEconomyService.SubscriptionDemand(
            20_000, 2_000, 0, 3, 50, 1.0f);

        Assert.That(TeamEconomyService.SubscriptionDemand(20_000, 500, 0, 3, 50, 1.0f),
            Is.GreaterThan(baseDemand), "Un abono más barato debe vender más");
        Assert.That(TeamEconomyService.SubscriptionDemand(20_000, 2_000, 4, 3, 50, 1.0f),
            Is.GreaterThan(baseDemand), "Ganar al inicio debe ayudar");
        Assert.That(TeamEconomyService.SubscriptionDemand(20_000, 2_000, 0, 5, 50, 1.0f),
            Is.GreaterThan(baseDemand), "Más reputación debe ayudar");
        Assert.That(TeamEconomyService.SubscriptionDemand(20_000, 2_000, 0, 3, 100, 1.0f),
            Is.GreaterThan(baseDemand), "Un mercado mayor debe ayudar");
    }

    [Test]
    public void SubscriptionDemand_IsClampedToCapacityAndHandlesZero()
    {
        Assert.That(TeamEconomyService.SubscriptionDemand(0, 2_000, 0, 3, 50, 1.0f), Is.EqualTo(0));
        Assert.That(TeamEconomyService.SubscriptionDemand(100, 0, 82, 5, 100, 1.15f),
            Is.LessThanOrEqualTo(100));
    }

    [Test]
    public void ObjectiveTargetPosition_MapsObjectives()
    {
        Assert.That(TeamEconomyService.ObjectiveTargetPosition("Campeonato"), Is.EqualTo(1));
        Assert.That(TeamEconomyService.ObjectiveTargetPosition("Playoffs"), Is.EqualTo(6));
        Assert.That(TeamEconomyService.ObjectiveTargetPosition("Play-In"), Is.EqualTo(10));
        Assert.That(TeamEconomyService.ObjectiveTargetPosition("Zona tranquila"), Is.EqualTo(0));
        Assert.That(TeamEconomyService.ObjectiveTargetPosition(null), Is.EqualTo(0));
    }

    [Test]
    public void CalculateAttendance_ReturnsZeroWithoutCapacity()
    {
        var game = new GameData { home_team_id = 1, away_team_id = 2 };
        var team = new TeamData { id = 1, capacity = 0 };

        Assert.That(TeamEconomyService.CalculateAttendance(game, team, null, null), Is.EqualTo(0));
    }
}
