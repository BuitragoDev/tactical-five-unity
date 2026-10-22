using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Valoración de traspasos de la IA (<see cref="TradeValueHelper"/>). Reglas puras: sin
/// base de datos, sin UnityEngine.Random.
/// </summary>
public class TradeValueHelperTests
{
    static PlayerData P(int overall, int age = 27, long salary = 0, int potential = 0)
    {
        return new PlayerData
        {
            overall = overall,
            age = age,
            salary = salary,
            potential = potential > 0 ? potential : overall
        };
    }

    static DraftPickData Pick1(int number = 16, int year = 0)
        => new DraftPickData { round = 1, pick_number = number, draft_year = year };

    static DraftPickData Pick2(int number = 45, int year = 0)
        => new DraftPickData { round = 2, pick_number = number, draft_year = year };

    // ── CURVA Y MODIFICADORES ─────────────────────────────────────────────────

    [Test]
    public void BaseValue_IncreasesWithOverall()
    {
        for (int ovr = 56; ovr <= 95; ovr++)
            Assert.That(TradeValueHelper.BaseValue(ovr),
                        Is.GreaterThanOrEqualTo(TradeValueHelper.BaseValue(ovr - 1)),
                        $"el valor debe crecer con la media ({ovr})");
    }

    [Test]
    public void BaseValue_TopEndIsMuchMoreValuableThanRolePlayer()
    {
        // Un 85 vale bastante más del doble que un 76: la curva no puede ser lineal.
        Assert.That(TradeValueHelper.BaseValue(85),
                    Is.GreaterThan(TradeValueHelper.BaseValue(76) * 2.0));
    }

    [Test]
    public void PlayerValue_YoungerPlayerIsWorthMore()
    {
        Assert.That(TradeValueHelper.PlayerValue(P(82, 22)), Is.GreaterThan(TradeValueHelper.PlayerValue(P(82, 30))));
        Assert.That(TradeValueHelper.PlayerValue(P(82, 30)), Is.GreaterThan(TradeValueHelper.PlayerValue(P(82, 35))));
    }

    [Test]
    public void PlayerValue_PotentialAddsValueOnlyForYoungPlayers()
    {
        Assert.That(TradeValueHelper.PlayerValue(P(78, 22, potential: 90)),
                    Is.GreaterThan(TradeValueHelper.PlayerValue(P(78, 22, potential: 78))));

        Assert.That(TradeValueHelper.PlayerValue(P(78, 31, potential: 90)),
                    Is.EqualTo(TradeValueHelper.PlayerValue(P(78, 31, potential: 78))));
    }

    [Test]
    public void PlayerValue_OverpaidContractLosesValue()
    {
        // Mismo jugador: cobrar muy por encima de mercado le resta valor de traspaso.
        Assert.That(TradeValueHelper.PlayerValue(P(82, 27, salary: 40_000_000L)),
                    Is.GreaterThan(TradeValueHelper.PlayerValue(P(82, 27, salary: 90_000_000L))));
    }

    [Test]
    public void ExpectedSalary_ReusesMarketValueByOvr()
    {
        long cap = TradeHelper.SALARY_CAP;
        Assert.That(TradeValueHelper.ExpectedSalary(85, cap), Is.EqualTo(TradeHelper.MarketValueByOvr(85, cap)));
        Assert.That(TradeValueHelper.ExpectedSalary(85, cap), Is.GreaterThan(TradeValueHelper.ExpectedSalary(78, cap)));
    }

    [Test]
    public void PickValue_FirstRoundWorthMoreThanSecond()
    {
        Assert.That(TradeValueHelper.PickValue(Pick1(1)),
                    Is.GreaterThan(TradeValueHelper.PickValue(Pick1(30))));
        Assert.That(TradeValueHelper.PickValue(Pick1(30)),
                    Is.GreaterThan(TradeValueHelper.PickValue(Pick2(45))));
    }

    [Test]
    public void Tolerance_ContendMayOverpayAndRebuildNeverDoes()
    {
        Assert.That(TradeValueHelper.Tolerance(TeamStrategy.Contend), Is.GreaterThan(TradeValueHelper.Tolerance(TeamStrategy.Balanced)));
        Assert.That(TradeValueHelper.Tolerance(TeamStrategy.Balanced), Is.GreaterThanOrEqualTo(TradeValueHelper.Tolerance(TeamStrategy.Rebuild)));
        Assert.That(TradeValueHelper.Tolerance(TeamStrategy.Rebuild), Is.EqualTo(1.0));
    }

    // ── OFERTAS DE LA IA (AI → usuario) ───────────────────────────────────────

    [Test]
    public void CheckOffer_TwoStarsForRolePlayer_IsRejected()
    {
        // El caso reportado: la IA daba un 84 (50 M) + un 85 (53 M) por un 76 (43,66 M).
        var target = P(76, 28, salary: 43_660_000L);
        var gives = new List<PlayerData> { P(84, 32, salary: 50_000_000L), P(85, 27, salary: 53_000_000L) };
        var receives = new List<PlayerData> { target };

        var check = TradeValueHelper.CheckOffer(TeamStrategy.Contend, gives, null, receives, null);

        Assert.That(check.Sane, Is.False, "dos estrellas por un rol no es un traspaso razonable");
        Assert.That(check.OvrGapOk, Is.False, "85 por 76 rompe la regla de diferencia de media");
        Assert.That(check.Outgoing, Is.GreaterThan(check.Incoming * 2.0), "el valor entregado es varias veces el recibido");
    }

    [Test]
    public void CheckOffer_TwoStarsForRolePlayerPlusFirstRounder_IsStillRejected()
    {
        var target = P(76, 28, salary: 43_660_000L);
        var gives = new List<PlayerData> { P(84, 32, salary: 50_000_000L), P(85, 27, salary: 53_000_000L) };
        var receives = new List<PlayerData> { target };
        var receivesPicks = new List<DraftPickData> { Pick1() };

        Assert.That(TradeValueHelper.CheckOffer(TeamStrategy.Contend, gives, null, receives, receivesPicks).Sane,
                    Is.False, "una ronda no compensa dos estrellas");
    }

    [Test]
    public void CheckOffer_FairOneForOne_IsSane()
    {
        // Un 84 por un 85 con salarios parecidos es un intercambio normal.
        var gives = new List<PlayerData> { P(84, 29, salary: 40_000_000L) };
        var receives = new List<PlayerData> { P(85, 29, salary: 41_000_000L) };

        Assert.That(TradeValueHelper.IsSane(TeamStrategy.Balanced, gives, null, receives, null), Is.True);
    }

    [Test]
    public void CheckOffer_GivingBetterPlayerForMuchWorseOne_IsRejected()
    {
        var gives = new List<PlayerData> { P(88, 32, salary: 45_000_000L) };
        var receives = new List<PlayerData> { P(76, 28, salary: 43_000_000L) };

        Assert.That(TradeValueHelper.IsSane(TeamStrategy.Contend, gives, null, receives,
                                            new List<DraftPickData> { Pick1() }), Is.False);
    }

    [Test]
    public void CheckOffer_RolePlayerForBetterPlayer_IsSane()
    {
        // Al contrario sí: la IA entrega un 78 y recibe un 84 (su objetivo).
        var gives = new List<PlayerData> { P(78, 26, salary: 20_000_000L) };
        var receives = new List<PlayerData> { P(84, 27, salary: 40_000_000L) };

        Assert.That(TradeValueHelper.CheckOffer(TeamStrategy.Contend, gives, null, receives, null).Sane, Is.True);
    }

    [Test]
    public void CheckOffer_LowballOfferIsNotSaneForTheOtherSide()
    {
        // Un 70 por una estrella: aunque "no da de más", es una oferta insultante.
        var gives = new List<PlayerData> { P(70, 30) };
        var receives = new List<PlayerData> { P(90, 27) };

        var check = TradeValueHelper.CheckOffer(TeamStrategy.Contend, gives, null, receives, null);
        Assert.That(check.Outgoing, Is.LessThan(check.Incoming * TradeValueHelper.MinOfferRatio));
    }

    [Test]
    public void CheckOffer_FirstRounderWidensTheOvrGapRule()
    {
        // Un 82 por un 76 (+6) sólo se admite si a cambio llega una primera ronda.
        var gives = new List<PlayerData> { P(82, 27) };
        var receives = new List<PlayerData> { P(76, 27) };

        Assert.That(TradeValueHelper.CheckOffer(TeamStrategy.Balanced, gives, null, receives, null).OvrGapOk,
                    Is.False);
        Assert.That(TradeValueHelper.CheckOffer(TeamStrategy.Balanced, gives, null, receives,
                                                new List<DraftPickData> { Pick1() }).OvrGapOk,
                    Is.True);
    }

    [Test]
    public void TargetOvrMargin_ContendAimsHigherThanRebuild()
    {
        Assert.That(TradeValueHelper.TargetOvrMargin(TeamStrategy.Contend),
                    Is.GreaterThan(TradeValueHelper.TargetOvrMargin(TeamStrategy.Balanced)));
        Assert.That(TradeValueHelper.TargetOvrMargin(TeamStrategy.Balanced),
                    Is.GreaterThanOrEqualTo(TradeValueHelper.TargetOvrMargin(TeamStrategy.Rebuild)));
        Assert.That(TradeValueHelper.TargetOvrMargin(TeamStrategy.Rebuild),
                    Is.EqualTo(TradeValueHelper.MaxOvrGap));
    }

    // ── TRASPASOS IA-IA ───────────────────────────────────────────────────────

    [Test]
    public void IsSaneBothWays_BalancedTrade_IsSane()
    {
        var aGives = new List<PlayerData> { P(81, 27) };
        var bGives = new List<PlayerData> { P(82, 27) };

        Assert.That(TradeValueHelper.IsSaneBothWays(TeamStrategy.Balanced, aGives, null,
                                                    TeamStrategy.Balanced, bGives, null), Is.True);
    }

    [Test]
    public void IsSaneBothWays_WithoutCompensation_IsRejected()
    {
        // A entrega un 76 y recibe un 82: para el otro equipo es un regalo.
        var aGives = new List<PlayerData> { P(76, 27) };
        var bGives = new List<PlayerData> { P(82, 27) };

        Assert.That(TradeValueHelper.IsSaneBothWays(TeamStrategy.Rebuild, aGives, null,
                                                    TeamStrategy.Balanced, bGives, null), Is.False);
    }

    [Test]
    public void IsSaneBothWays_WithCompensatingPick_IsSane()
    {
        var aGives = new List<PlayerData> { P(76, 27) };
        var bGives = new List<PlayerData> { P(82, 27) };
        var aPicks = new List<DraftPickData> { Pick1() };

        Assert.That(TradeValueHelper.IsSaneBothWays(TeamStrategy.Rebuild, aGives, aPicks,
                                                    TeamStrategy.Balanced, bGives, null), Is.True);
    }

    [Test]
    public void IsSaneBothWays_GivingStarForRolePlayer_IsRejectedEvenWithPick()
    {
        var aGives = new List<PlayerData> { P(89, 34, salary: 55_000_000L) };
        var bGives = new List<PlayerData> { P(76, 28, salary: 43_000_000L) };
        var bPicks = new List<DraftPickData> { Pick1() };

        Assert.That(TradeValueHelper.IsSaneBothWays(TeamStrategy.Contend, aGives, null,
                                                    TeamStrategy.Rebuild, bGives, bPicks), Is.False);
    }
}
