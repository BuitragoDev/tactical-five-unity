using System.Collections.Generic;
using NUnit.Framework;

public class TradeHelperTests
{
    static PlayerData Player(long salary, int overall = 80, int age = 25)
    {
        return new PlayerData { salary = salary, overall = overall, age = age };
    }

    [Test]
    public void CalculateLuxuryTax_UnderThreshold_ReturnsZero()
    {
        Assert.That(TradeHelper.CalculateLuxuryTax(200_000_000L), Is.EqualTo(0L));
        Assert.That(TradeHelper.CalculateLuxuryTax(TradeHelper.LUXURY_TAX), Is.EqualTo(0L));
    }

    [Test]
    public void CalculateLuxuryTax_ProgressiveBrackets()
    {
        // F4-07: usa el umbral vigente para no acoplar el test a una temporada concreta.
        Assert.That(TradeHelper.CalculateLuxuryTax(TradeHelper.LUXURY_TAX + 10_000_000L),
            Is.EqualTo(16_250_000L));
        Assert.That(TradeHelper.CalculateLuxuryTax(TradeHelper.LUXURY_TAX + 20_000_000L),
            Is.EqualTo(45_000_000L));
        Assert.That(TradeHelper.CalculateLuxuryTax(TradeHelper.LUXURY_TAX + 25_000_000L),
            Is.EqualTo(63_750_000L));
    }

    [Test]
    public void CalculateLuxuryTax_RespectsCustomThreshold()
    {
        Assert.That(TradeHelper.CalculateLuxuryTax(210_000_000L, 200_000_000L), Is.EqualTo(16_250_000L));
    }

    [Test]
    public void PickBonus_FirstOverallPick_IsMaxBonus()
    {
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 1, pick_number = 1 }), Is.EqualTo(19));
    }

    [Test]
    public void PickBonus_LastFirstRoundPick_IsBaseBonus()
    {
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 1, pick_number = 30 }), Is.EqualTo(10));
    }

    [Test]
    public void PickBonus_SecondRound()
    {
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 2, pick_number = 31 }), Is.EqualTo(10));
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 2, pick_number = 60 }), Is.EqualTo(5));
    }

    [Test]
    public void PickBonus_NullAndLateRound()
    {
        Assert.That(TradeHelper.PickBonus(null), Is.EqualTo(0));
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 3, pick_number = 5 }), Is.EqualTo(3));
    }

    [Test]
    public void PickBonus_ProtectedPick_IsPenalized()
    {
        // Pick 1 de R1 sin proteger vale 19; top-4 protegido pierde 8.
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 1, pick_number = 1, protected_from = 4 }), Is.EqualTo(11));
        // Pick 30 de R1 base 10; top-5 protegido -> 1 (mínimo).
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 1, pick_number = 30, protected_from = 5 }), Is.EqualTo(1));
    }

    [Test]
    public void PickBonus_ProtectedPick_NeverBelowOne()
    {
        // Protección máxima no deja el bonus en 0 ni negativo.
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 1, pick_number = 1, protected_from = 30 }), Is.GreaterThanOrEqualTo(1));
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 2, pick_number = 60, protected_from = 14 }), Is.EqualTo(1));
    }

    [Test]
    public void PickBonus_FutureYear_IsDiscounted()
    {
        // R1 slot 1 vale 19 en el año actual y pierde 2 por cada año futuro.
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 1, pick_number = 1, draft_year = 2027 }, 2027), Is.EqualTo(19));
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 1, pick_number = 1, draft_year = 2028 }, 2027), Is.EqualTo(17));
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 1, pick_number = 1, draft_year = 2030 }, 2027), Is.EqualTo(13));
    }

    [Test]
    public void PickBonus_FutureYearNoPosition_UsesFlatBase()
    {
        // Años futuros sin posición (pick_number = 0): base plana R1 = 14, R2 = 7,
        // menos el descuento por año.
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 1, pick_number = 0, draft_year = 2028 }, 2027), Is.EqualTo(12));
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 2, pick_number = 0, draft_year = 2028 }, 2027), Is.EqualTo(6));
    }

    [Test]
    public void ValidateTrade_FairTrade_HasNoErrors()
    {
        var a = new List<PlayerData> { Player(10_000_000) };
        var b = new List<PlayerData> { Player(10_000_000) };
        var errors = TradeHelper.ValidateTrade(a, b, 12, 12, "Bulls", 100_000_000L, "Lakers", 100_000_000L);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateTrade_BelowMinRoster_ReportsError()
    {
        var a = new List<PlayerData>
        {
            Player(10_000_000), Player(10_000_000), Player(10_000_000),
            Player(10_000_000), Player(10_000_000)
        };
        var errors = TradeHelper.ValidateTrade(a, new List<PlayerData>(), 12, 12, "Bulls", 100_000_000L, "Lakers", 100_000_000L);
        Assert.That(errors, Has.Some.Contains("mínimo 10"));
    }

    [Test]
    public void ValidateTrade_AboveMaxRoster_ReportsError()
    {
        var b = new List<PlayerData> { Player(10_000_000), Player(10_000_000), Player(10_000_000) };
        var errors = TradeHelper.ValidateTrade(new List<PlayerData>(), b, 15, 15, "Bulls", 100_000_000L, "Lakers", 100_000_000L);
        Assert.That(errors, Has.Some.Contains("máximo 15"));
    }

    [Test]
    public void ValidateTrade_AcceptingTwoForOne_AtMaxRoster_ReportsError()
    {
        // Revalidación al aceptar una oferta IA estando en el máximo de plantilla:
        // un 2x1 dejaría al equipo que acepta con 18 jugadores.
        var aiSent = new List<PlayerData> { Player(10_000_000), Player(8_000_000) };
        var ourSent = new List<PlayerData> { Player(10_000_000) };
        var errors = TradeHelper.ValidateTrade(aiSent, ourSent, 12, 15, "Nosotros", 100_000_000L, "IA", 100_000_000L,
            teamBTwoWayCount: 2);
        Assert.That(errors, Has.Some.Contains("máximo 15"));
    }

    [Test]
    public void ValidateTrade_TwoWayIncomingUsesSeparateLimitAndDoesNotConsumeStandardSlot()
    {
        var standardOut = new List<PlayerData> { Player(TradeHelper.TWO_WAY_SALARY) };
        var twoWayIn = new List<PlayerData> { new PlayerData { salary = TradeHelper.TWO_WAY_SALARY, is_two_way = 1 } };

        var errors = TradeHelper.ValidateTrade(standardOut, twoWayIn,
            15, 12, "Equipo con plazas completas", 100_000_000L, "Rival", 100_000_000L,
            teamATwoWayCount: 1, teamBTwoWayCount: 1);

        Assert.That(errors, Is.Empty,
            "el jugador estándar saliente libera esa plaza y el entrante two-way consume solo el cupo two-way");
    }

    [Test]
    public void ValidateTrade_TwoWayLimitRejectsThirdContractEvenWithStandardRoom()
    {
        var twoWayIn = new List<PlayerData> { new PlayerData { salary = TradeHelper.TWO_WAY_SALARY, is_two_way = 1 } };

        var errors = TradeHelper.ValidateTrade(new List<PlayerData>(), twoWayIn,
            10, 10, "Equipo A", 100_000_000L, "Equipo B", 100_000_000L,
            teamATwoWayCount: TradeHelper.MAX_TWO_WAY, teamBTwoWayCount: 1);

        Assert.That(errors, Has.Some.Contains("contratos two-way (máximo 2)"));
    }

    [Test]
    public void ValidateTrade_HardCap_RejectsExceedingFirstApronAndSalaryGain()
    {
        var a = new List<PlayerData> { Player(10_000_000), Player(10_000_000) };
        var b = new List<PlayerData> { Player(5_000_000) };
        long payrollBelowApron = TradeHelper.FIRST_APRON - 5_000_000L;
        var errors = TradeHelper.ValidateTrade(a, b, 12, 12, "Bulls", payrollBelowApron, "Lakers", 100_000_000L, false, true);
        Assert.That(errors, Has.Some.Contains("hard cap del primer apron"));
        Assert.That(errors, Has.Some.Contains($"${5_000_000:N0}"));
        Assert.That(errors, Has.None.Contains("agregar salarios"),
            "el hard cap no convierte el conteo de jugadores entrantes en una regla de agregación del segundo apron");
    }

    [Test]
    public void ValidateTrade_SecondApron_AllowsReceivingSeveralForOneOutgoing()
    {
        var teamASends = new List<PlayerData> { Player(5_000_000), Player(5_000_000) };
        var teamBSends = new List<PlayerData> { Player(10_000_000) };

        var errors = TradeHelper.ValidateTrade(teamASends, teamBSends,
            12, 12, "Segundo apron", 250_000_000L, "Equipo A", 100_000_000L);

        Assert.That(errors, Is.Empty,
            "se permiten varios jugadores entrantes si el equipo del segundo apron no agrega salarios salientes");
    }

    [Test]
    public void ValidateTrade_SecondApron_RejectsAggregatingSeveralOutgoingForOneIncoming()
    {
        var teamASends = new List<PlayerData> { Player(20_000_000) };
        var teamBSends = new List<PlayerData> { Player(10_000_000), Player(10_000_000) };

        var errors = TradeHelper.ValidateTrade(teamASends, teamBSends,
            12, 12, "Segundo apron", 250_000_000L, "Equipo A", 100_000_000L);

        Assert.That(errors, Has.Some.Contains("agregar salarios de 2 jugadores salientes para recibir 1"));
        Assert.That(errors, Does.Not.Contain("no puede recibir múltiples"));
    }

    [Test]
    public void ValidateTrade_BetweenAprons_AllowsMultipleOutgoingWhenMatchingFits()
    {
        var teamASends = new List<PlayerData> { Player(20_000_000) };
        var teamBSends = new List<PlayerData> { Player(10_000_000), Player(10_000_000) };
        long payroll = TradeHelper.FIRST_APRON + 1_000_000;

        var errors = TradeHelper.ValidateTrade(teamASends, teamBSends,
            12, 12, "Entre aprons", payroll, "Equipo A", 100_000_000L);

        Assert.That(errors, Is.Empty,
            "la regla de agregación del segundo apron no se aplica a un equipo entre aprons");
    }

    [Test]
    public void ValidateTrade_HardCapped_AllowsSeveralIncomingWithinTheApron()
    {
        var teamASends = new List<PlayerData> { Player(5_000_000), Player(5_000_000) };
        var teamBSends = new List<PlayerData> { Player(10_000_000) };

        var errors = TradeHelper.ValidateTrade(teamASends, teamBSends,
            12, 12, "Hard cap", 100_000_000L, "Equipo A", 100_000_000L,
            teamBHardCapped: true);

        Assert.That(errors, Is.Empty,
            "el hard cap impone el límite de nómina proyectada, no una prohibición de varios entrantes");
    }

    [Test]
    public void ValidateTrade_FirstApron_ErrorShowsTheExactLimitApplied()
    {
        const long salaryOut = 10_000_000;
        const long salaryIn = 11_000_001;
        long projectedPayroll = TradeHelper.FIRST_APRON + 1;
        long currentPayroll = projectedPayroll - salaryIn + salaryOut;
        var incoming = new List<PlayerData> { Player(salaryIn) };
        var outgoing = new List<PlayerData> { Player(salaryOut) };

        var errors = TradeHelper.ValidateTrade(incoming, outgoing, 12, 12, "Bulls", currentPayroll);

        Assert.That(errors, Has.Some.Contains($"${11_000_000:N0}"));
        Assert.That(errors, Has.Some.Contains("primer apron"));
    }

    [TestCase(10_000_000L, TradeHelper.SECOND_APRON + 1, false, 10_000_000L)]
    [TestCase(10_000_000L, TradeHelper.FIRST_APRON + 1, false, 11_000_000L)]
    [TestCase(5_000_000L, 100_000_000L, false, 10_250_000L)]
    public void ValidateTrade_SalaryMatchingBoundaries_AreExactAndMessageMatches(
        long salaryOut, long projectedPayroll, bool hardCap, long expectedLimit)
    {
        Assert.That(TradeHelper.GetMaxIncomingSalary(salaryOut, projectedPayroll, hardCap), Is.EqualTo(expectedLimit));

        // Just below and exactly at the limit are accepted; one dollar above is rejected.
        Assert.That(ErrorsForIncoming(salaryOut, projectedPayroll, hardCap, expectedLimit - 1), Is.Empty);
        Assert.That(ErrorsForIncoming(salaryOut, projectedPayroll, hardCap, expectedLimit), Is.Empty);

        var over = ErrorsForIncoming(salaryOut, projectedPayroll, hardCap, expectedLimit + 1);
        Assert.That(over, Has.Some.Contains($"${expectedLimit:N0}"));
    }

    [Test]
    public void GetMaxIncomingSalary_BelowFirstApron_UsesSingleCBAAdjustmentPerBracket()
    {
        Assert.That(TradeHelper.GetMaxIncomingSalary(7_499_999L, 100_000_000L), Is.EqualTo(15_249_998L));
        Assert.That(TradeHelper.GetMaxIncomingSalary(7_500_000L, 100_000_000L), Is.EqualTo(15_000_000L));
        Assert.That(TradeHelper.GetMaxIncomingSalary(28_999_999L, 100_000_000L), Is.EqualTo(36_499_999L));
        Assert.That(TradeHelper.GetMaxIncomingSalary(29_000_000L, 100_000_000L), Is.EqualTo(36_500_000L));
    }

    static List<string> ErrorsForIncoming(long salaryOut, long projectedPayroll, bool hardCap, long salaryIn)
    {
        // Mantener fija la nómina proyectada para probar cada tramo y solo variar el
        // salario entrante alrededor de su límite. Se valida el lado de Bulls (B).
        long currentPayroll = projectedPayroll - salaryIn + salaryOut;
        var incoming = new List<PlayerData> { Player(salaryIn) };
        var outgoing = new List<PlayerData> { Player(salaryOut) };
        return TradeHelper.ValidateTrade(incoming, outgoing, 12, 12, "Bulls", currentPayroll,
            teamBHardCapped: hardCap);
    }

    [Test]
    public void ValidateTrade_BelowApron_AppliesSalaryMatchingRule()
    {
        var a = new List<PlayerData> { Player(11_000_000) };
        var b = new List<PlayerData> { Player(5_000_000) };
        var errors = TradeHelper.ValidateTrade(a, b, 12, 12, "Bulls", 100_000_000L, "Lakers", 100_000_000L);
        Assert.That(errors, Has.Some.Contains("no puede recibir más de"));
    }

    [Test]
    public void ValidateTrade_SignAndTrade_UsingNewSignedSalary()
    {
        // FA propio firmado a 25M (teamA envia solo el FA via S&T) contra 25M de B.
        // Sin la firma el salario entrante seria el de su contrato externa; con el
        // nuevo salario de 25M el matching se valida sobre ese valor (25M, no 8M).
        var a = new List<PlayerData> { Player(8_000_000, 85, 26) };
        var b = new List<PlayerData> { Player(25_000_000, 78, 27) };
        var sign = new Dictionary<int, long> { [a[0].id] = 25_000_000L };
        var errors = TradeHelper.ValidateTrade(a, b, 12, 12, "Bulls", 100_000_000L, "Lakers", 100_000_000L,
            false, false, TradeHelper.FIRST_APRON, TradeHelper.SECOND_APRON, TradeHelper.LUXURY_TAX, sign);
        // A envia 25M (firmado) por 25M -> matching correcto en ambos lados.
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateTrade_SignAndTrade_RevalidatesAgainstTheSignedSalary()
    {
        var ownFA = new List<PlayerData> { Player(8_000_000, 85, 26) };
        var otherPlayer = new List<PlayerData> { Player(15_000_000, 78, 27) };
        var newSalary = new Dictionary<int, long> { [ownFA[0].id] = 25_000_000L };

        var beforeSigning = TradeHelper.ValidateTrade(ownFA, otherPlayer, 12, 12,
            "Bulls", 100_000_000L, "Lakers", 100_000_000L);
        var afterSigning = TradeHelper.ValidateTrade(ownFA, otherPlayer, 12, 12,
            "Bulls", 100_000_000L, "Lakers", 100_000_000L,
            teamASignSalaries: newSalary);

        Assert.That(beforeSigning, Is.Empty, "el salario FA previo cabe en el matching");
        Assert.That(afterSigning, Has.Some.Contains("Bulls no puede recibir más de"),
            "la revalidación debe usar los 25M del nuevo contrato, no los 8M del FA");
    }

    [Test]
    public void ValidateTrade_SignAndTrade_FACountsForReceiverRosterNotForSigner()
    {
        // A (12 jugadores) hace S&T enviando SOLO el FA firmado; B (12) envía 1 jugador.
        // El FA no estaba en la plantilla de A -> A no pierde rostro, B lo gana.
        // A: 12 - 0 (FA no contaba) + 1 (recibe de B) = 13 (ok, no mín/máx).
        var a = new List<PlayerData> { Player(5_000_000, 82, 26) };
        var b = new List<PlayerData> { Player(5_000_000, 80, 27) };
        var signA = new Dictionary<int, long> { [a[0].id] = 10_000_000L };
        var errors = TradeHelper.ValidateTrade(a, b, 12, 12, "Bulls", 100_000_000L, "Lakers", 100_000_000L,
            false, false, TradeHelper.FIRST_APRON, TradeHelper.SECOND_APRON, TradeHelper.LUXURY_TAX, signA);
        // Sin error de roster (el FA no resta del equipo que firma).
        Assert.That(errors, Does.Not.Contain("mínimo 10"));
        Assert.That(errors, Does.Not.Contain("máximo 17"));
    }

    // ── Fase 3: modelo de cap para la IA (ECO-06) ────────────────

    static LeagueSettingsData Settings(long cap, long apron1, long apron2, long ntMle, long tMle, long min)
        => new LeagueSettingsData
        {
            salary_cap = cap,
            apron = apron1,
            repeater_apron = apron2,
            mid_level = ntMle,
            taxpayer_mid_level = tMle,
            minimum_salary = min
        };

    [Test]
    public void CanAISignExternal_UnderCap_Allows()
    {
        var s = Settings(100_000_000, 120_000_000, 130_000_000, 10_000_000, 5_000_000, 2_000_000);
        Assert.That(TradeHelper.CanAISignExternal(80_000_000, 15_000_000, s), Is.True);
    }

    [Test]
    public void CanAISignExternal_RejectsBelowExperienceMinimum()
    {
        var s = Settings(100_000_000, 120_000_000, 130_000_000, 10_000_000, 5_000_000, 2_000_000);
        Assert.That(TradeHelper.CanAISignExternal(0, 1_000_000, s), Is.False, "bajo el mínimo de 0 años");
        Assert.That(TradeHelper.CanAISignExternal(0, 1_357_763, s), Is.True, "el mínimo de 0 años");
        Assert.That(TradeHelper.CanAISignExternal(0, 2_000_000, s, yearsOfService: 1), Is.False,
            "bajo el mínimo de 1 año");
    }

    [Test]
    public void CanAISignExternal_OverCap_AllowsOnlyMLE()
    {
        var s = Settings(100_000_000, 120_000_000, 130_000_000, 10_000_000, 5_000_000, 2_000_000);
        Assert.That(TradeHelper.CanAISignExternal(105_000_000, 9_000_000, s), Is.True, "cabe en el NT-MLE");
        Assert.That(TradeHelper.CanAISignExternal(105_000_000, 11_000_000, s), Is.False, "supera el NT-MLE");
    }

    [Test]
    public void CanAISignExternal_OverSecondApron_OnlyMinimum()
    {
        var s = Settings(100_000_000, 120_000_000, 130_000_000, 10_000_000, 5_000_000, 2_000_000);
        Assert.That(TradeHelper.CanAISignExternal(135_000_000, 1_357_763, s), Is.True, "el mínimo de 0 años sí");
        Assert.That(TradeHelper.CanAISignExternal(135_000_000, 3_000_000, s), Is.False, "por encima del 2º apron solo el mínimo");
        Assert.That(TradeHelper.CanAISignExternal(135_000_000, 2_185_116, s, yearsOfService: 1), Is.True,
            "un jugador de 1 año puede firmar exactamente su mínimo");
        Assert.That(TradeHelper.CanAISignExternal(135_000_000, 2_000_000, s, yearsOfService: 1), Is.False,
            "2 M no llega al mínimo de 1 año");
    }

    [Test]
    public void CanAIRenew_RespectsSecondApron()
    {
        var s = Settings(100_000_000, 120_000_000, 130_000_000, 10_000_000, 5_000_000, 2_000_000);
        Assert.That(TradeHelper.CanAIRenew(125_000_000, s), Is.True);
        Assert.That(TradeHelper.CanAIRenew(131_000_000, s), Is.False);
    }

    // ── ECO-13: la oferta se ajusta al mínimo y al máximo legal ──

    [Test]
    public void TryClampOffer_AppliesFloorThenCap()
    {
        Assert.That(TradeHelper.TryClampOffer(500_000, 1_000_000, 5_000_000, out var low), Is.True);
        Assert.That(low, Is.EqualTo(1_000_000), "Sube al mínimo");

        Assert.That(TradeHelper.TryClampOffer(9_000_000, 1_000_000, 5_000_000, out var high), Is.True);
        Assert.That(high, Is.EqualTo(5_000_000), "Baja al máximo legal (antes el `else if` lo dejaba pasar)");

        Assert.That(TradeHelper.TryClampOffer(3_000_000, 1_000_000, 5_000_000, out var ok), Is.True);
        Assert.That(ok, Is.EqualTo(3_000_000));
    }

    [Test]
    public void TryClampOffer_RejectsWhenMaxBelowFloor()
    {
        Assert.That(TradeHelper.TryClampOffer(2_000_000, 1_000_000, 800_000, out _), Is.False,
            "Sin rango legal no hay oferta posible");
    }

    // ── ECO-14: two-way solo para ≤ 23 años ──

    [Test]
    public void IsEligibleForTwoWayAge_OnlyUpTo23()
    {
        Assert.That(TradeHelper.IsEligibleForTwoWayAge(19), Is.True);
        Assert.That(TradeHelper.IsEligibleForTwoWayAge(23), Is.True);
        Assert.That(TradeHelper.IsEligibleForTwoWayAge(24), Is.False);
    }

    // ── VALOR DE MERCADO Y RENOVACIÓN A MERCADO ─────────────

    [Test]
    public void MarketValueByOvr_UsesCapFractions()
    {
        long cap = 140_000_000;
        Assert.That(TradeHelper.MarketValueByOvr(90, cap), Is.EqualTo((long)(cap * 0.35)));
        Assert.That(TradeHelper.MarketValueByOvr(85, cap), Is.EqualTo((long)(cap * 0.35)));
        Assert.That(TradeHelper.MarketValueByOvr(80, cap), Is.EqualTo((long)(cap * 0.30)));
        Assert.That(TradeHelper.MarketValueByOvr(75, cap), Is.EqualTo((long)(cap * 0.25)));
        Assert.That(TradeHelper.MarketValueByOvr(74, cap), Is.EqualTo(0), "por debajo de 75 no hay valor por OVR");
        Assert.That(TradeHelper.MarketValueByOvr(90, 0), Is.EqualTo(0));
    }

    [Test]
    public void ShouldRenewAtMarket_OnlyWhenFarBelowMarket()
    {
        // Firmó por el mínimo tras una rescisión: 49M vs 2M → renovación a mercado.
        Assert.That(TradeHelper.ShouldRenewAtMarket(49_000_000, 2_000_000), Is.True);
        // Un jugador pagado cerca de su valor mantiene los topes Bird.
        Assert.That(TradeHelper.ShouldRenewAtMarket(30_000_000, 25_000_000), Is.False);
        Assert.That(TradeHelper.ShouldRenewAtMarket(0, 2_000_000), Is.False);
        Assert.That(TradeHelper.ShouldRenewAtMarket(10_000_000, 0), Is.False, "sin salario actual no aplica");
    }
}
