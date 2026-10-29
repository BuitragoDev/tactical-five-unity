using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Mercado de buyout: los rescindidos firman por el mínimo y la IA les busca destino
/// (contender &gt; balanced &gt; rebuild; a igual estrategia, el que más le necesita).
/// </summary>
public class BuyoutMarketRulesTests
{
    static BuyoutCandidate Team(int id, TeamStrategy strategy, int samePos = 0, float avg = 70f,
                                bool room = true, int reputation = 3, long margin = 0)
        => new BuyoutCandidate { teamId = id, strategy = strategy, samePosCount = samePos,
                                 teamAvg = avg, hasRoom = room, reputation = reputation,
                                 salaryMargin = margin };

    static PlayerData Player(int id, int overall, int age = 27, int potential = 0,
                             long salary = 2_000_000, int years = 1,
                             PlayerRole role = PlayerRole.UltimoRecurso)
        => new PlayerData
        {
            id = id,
            overall = overall,
            age = age,
            potential = potential > 0 ? potential : overall,
            salary = salary,
            contract_years = years,
            role = role
        };

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
    public void IsWanted_OnlyFrom75Overall()
    {
        Assert.That(BuyoutMarketRules.IsWanted(90), Is.True);
        Assert.That(BuyoutMarketRules.IsWanted(75), Is.True, "el umbral entra");
        Assert.That(BuyoutMarketRules.IsWanted(74), Is.False, "por debajo se queda en agentes libres");
        Assert.That(BuyoutMarketRules.IsWanted(68), Is.False);
        Assert.That(BuyoutMarketRules.MinOverall, Is.EqualTo(75));
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

    [Test]
    public void RankDestinations_TieBreakByPrestigeThenId()
    {
        var ranked = BuyoutMarketRules.RankDestinations(new List<BuyoutCandidate>
        {
            Team(1, TeamStrategy.Balanced, samePos: 1, avg: 76f, reputation: 2),
            Team(2, TeamStrategy.Balanced, samePos: 1, avg: 76f, reputation: 5),
            Team(3, TeamStrategy.Balanced, samePos: 1, avg: 76f, reputation: 5)
        });

        Assert.That(ranked[0].teamId, Is.EqualTo(2), "a igual estrategia/necesidad/plantilla, más prestigio");
        Assert.That(ranked[1].teamId, Is.EqualTo(3), "y a igual prestigio, el id más bajo");
        Assert.That(ranked[2].teamId, Is.EqualTo(1));
    }

    [Test]
    public void PickSuitor_FavorsTheBestButNotAlways()
    {
        var rng = new System.Random(12345);
        int[] picks = new int[3];
        for (int i = 0; i < 3000; i++) picks[BuyoutMarketRules.PickSuitor(3, rng)]++;

        Assert.That(picks[0], Is.GreaterThan(picks[1]), "el mejor destino es el más probable");
        Assert.That(picks[1], Is.GreaterThan(picks[2]));
        Assert.That(picks[2], Is.GreaterThan(0), "pero el tercero también puede salir");
    }

    [Test]
    public void PickSuitor_HandlesFewOrNoCandidates()
    {
        var rng = new System.Random(1);
        Assert.That(BuyoutMarketRules.PickSuitor(0, rng), Is.EqualTo(-1));
        Assert.That(BuyoutMarketRules.PickSuitor(1, rng), Is.EqualTo(0), "con un solo postor no hay sorteo");
        Assert.That(BuyoutMarketRules.PickSuitor(2, rng), Is.InRange(0, 1));
    }

    [Test]
    public void RankDestinations_PrioritizesCapMargin()
    {
        var ranked = BuyoutMarketRules.RankDestinations(new List<BuyoutCandidate>
        {
            Team(1, TeamStrategy.Contend, margin: 5_000_000),
            Team(2, TeamStrategy.Rebuild, margin: 30_000_000)
        });

        Assert.That(ranked[0].teamId, Is.EqualTo(2),
            "con hueco, el mayor margen salarial debe pasar por delante de la estrategia");
    }

    [Test]
    public void CanCutFor_RequiresAStarAndAClearUpgrade()
    {
        var incoming = Player(1, 90, salary: 2_000_000, years: 1);
        var lowBench = Player(2, 70, age: 27, salary: 2_000_000, years: 1);

        Assert.That(BuyoutMarketRules.CanCutFor(incoming, lowBench, 140_000_000), Is.True,
            "una estrella puede reemplazar a un jugador de banquillo");
        Assert.That(BuyoutMarketRules.CanCutFor(Player(3, 80), Player(4, 78), 140_000_000),
            Is.False, "un 80 no debe cortar a un 78: no hay mejora clara");
        Assert.That(BuyoutMarketRules.CanCutFor(incoming,
            Player(5, 70, role: PlayerRole.Titular), 140_000_000), Is.False,
            "no se corta a un titular para meter a una estrella");
        Assert.That(BuyoutMarketRules.CanCutFor(Player(6, 79), lowBench, 140_000_000),
            Is.False, "por debajo de 80 no se abre plantilla");
    }

    [Test]
    public void SelectCutCandidate_PrefersExpiringAndLowValue()
    {
        var incoming = Player(1, 92, salary: 2_000_000);
        var expiring = Player(2, 68, age: 24, salary: 2_000_000, years: 1);
        var longDeal = Player(3, 72, age: 26, salary: 3_000_000, years: 3);

        var cut = BuyoutMarketRules.SelectCutCandidate(
            new List<PlayerData> { longDeal, expiring }, incoming, 140_000_000);

        Assert.That(cut, Is.SameAs(expiring),
            "se prefiere el contrato de 1 año y el menor valor de plantilla");
    }
}
