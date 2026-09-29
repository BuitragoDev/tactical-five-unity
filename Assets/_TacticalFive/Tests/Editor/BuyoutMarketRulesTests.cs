using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Mercado de buyout: los rescindidos firman por el mínimo y la IA les busca destino
/// (contender &gt; balanced &gt; rebuild; a igual estrategia, el que más le necesita).
/// </summary>
public class BuyoutMarketRulesTests
{
    static BuyoutCandidate Team(int id, TeamStrategy strategy, int samePos = 0, float avg = 70f, bool room = true)
        => new BuyoutCandidate { teamId = id, strategy = strategy, samePosCount = samePos, teamAvg = avg, hasRoom = room };

    [Test]
    public void BestDestination_PrefersContender()
    {
        var dest = BuyoutMarketRules.BestDestination(new List<BuyoutCandidate>
        {
            Team(1, TeamStrategy.Balanced, samePos: 0, avg: 85f),
            Team(2, TeamStrategy.Contend,  samePos: 3, avg: 75f),
            Team(3, TeamStrategy.Rebuild,  samePos: 0, avg: 90f)
        });

        Assert.That(dest, Is.EqualTo(2), "el contender gana aunque tenga más gente en la posición");
    }

    [Test]
    public void BestDestination_TieBreakByNeedThenStrength()
    {
        var byNeed = BuyoutMarketRules.BestDestination(new List<BuyoutCandidate>
        {
            Team(1, TeamStrategy.Balanced, samePos: 4, avg: 80f),
            Team(2, TeamStrategy.Balanced, samePos: 1, avg: 70f)
        });
        Assert.That(byNeed, Is.EqualTo(2), "a igual estrategia, el que menos gente tiene en la posición");

        var byAvg = BuyoutMarketRules.BestDestination(new List<BuyoutCandidate>
        {
            Team(1, TeamStrategy.Balanced, samePos: 2, avg: 70f),
            Team(2, TeamStrategy.Balanced, samePos: 2, avg: 82f)
        });
        Assert.That(byAvg, Is.EqualTo(2), "y si empatan, el de mejor media");
    }

    [Test]
    public void BestDestination_IgnoresTeamsWithoutRoom()
    {
        var dest = BuyoutMarketRules.BestDestination(new List<BuyoutCandidate>
        {
            Team(1, TeamStrategy.Contend, room: false),
            Team(2, TeamStrategy.Rebuild, room: false)
        });
        Assert.That(dest, Is.EqualTo(-1));

        Assert.That(BuyoutMarketRules.BestDestination(null), Is.EqualTo(-1));
        Assert.That(BuyoutMarketRules.BestDestination(new List<BuyoutCandidate>()), Is.EqualTo(-1));
    }

    [Test]
    public void CanSignOn_FromTheWaivedDayOnwards()
    {
        Assert.That(BuyoutMarketRules.CanSignOn(waivedGameDay: 120, currentGameDay: 119), Is.False);
        Assert.That(BuyoutMarketRules.CanSignOn(120, 120), Is.True);
        Assert.That(BuyoutMarketRules.CanSignOn(120, 130), Is.True);
        Assert.That(BuyoutMarketRules.CanSignOn(-1, 130), Is.False, "no fue rescindido");
    }

    [Test]
    public void ContractYears_IsOneYear()
    {
        Assert.That(BuyoutMarketRules.ContractYears, Is.EqualTo(1),
            "el rescindido firma el mínimo por 1 año; luego su equipo renueva o pierde los derechos");
    }

    [Test]
    public void WaivedSalary_IsTheLeagueMinimum()
    {
        Assert.That(BuyoutMarketRules.WaivedSalary(2_100_000), Is.EqualTo(2_100_000));
        Assert.That(BuyoutMarketRules.WaivedSalary(0), Is.EqualTo(TradeHelper.MIN_SALARY), "sin ajuste usa el mínimo base");
    }
}
