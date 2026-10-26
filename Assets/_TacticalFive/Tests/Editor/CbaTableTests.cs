using NUnit.Framework;

/// <summary>
/// F4-07: tabla CBA por temporada. Comprueba las cifras oficiales publicadas, el
/// orden de los umbrales, la monotonía y el marcado de las temporadas proyectadas.
/// </summary>
public class CbaTableTests
{
    [Test]
    public void OfficialSeasons_MatchPublishedFigures()
    {
        var s2024 = CbaTable.For(2024);
        Assert.That(s2024.SalaryCap, Is.EqualTo(140_588_000L));
        Assert.That(s2024.LuxuryTax, Is.EqualTo(170_814_000L));
        Assert.That(s2024.FirstApron, Is.EqualTo(178_132_000L));
        Assert.That(s2024.SecondApron, Is.EqualTo(188_931_000L));
        Assert.That(s2024.NonTaxpayerMle, Is.EqualTo(12_822_000L));
        Assert.That(s2024.TaxpayerMle, Is.EqualTo(5_168_000L));
        Assert.That(s2024.BiAnnual, Is.EqualTo(4_668_000L));
        Assert.That(s2024.MinimumSalary, Is.EqualTo(1_157_153L));
        Assert.That(s2024.MinimumSalary1, Is.EqualTo(1_862_265L));
        Assert.That(s2024.MinimumSalary2, Is.EqualTo(2_087_519L));
        Assert.That(s2024.MinimumSalary3Plus, Is.EqualTo(2_162_606L));
        Assert.That(s2024.TwoWaySalary, Is.EqualTo(578_577L));
        Assert.That(s2024.Verified, Is.True);

        var s2025 = CbaTable.For(2025);
        Assert.That(s2025.SalaryCap, Is.EqualTo(154_647_000L));
        Assert.That(s2025.LuxuryTax, Is.EqualTo(187_895_000L));
        Assert.That(s2025.FirstApron, Is.EqualTo(195_945_000L));
        Assert.That(s2025.SecondApron, Is.EqualTo(207_824_000L));
        Assert.That(s2025.NonTaxpayerMle, Is.EqualTo(14_104_000L));
        Assert.That(s2025.TaxpayerMle, Is.EqualTo(5_685_000L));
        Assert.That(s2025.BiAnnual, Is.EqualTo(5_134_000L));
        Assert.That(s2025.MinimumSalary, Is.EqualTo(1_272_870L));
        Assert.That(s2025.MinimumSalary1, Is.EqualTo(2_048_494L));
        Assert.That(s2025.MinimumSalary2, Is.EqualTo(2_296_274L));
        Assert.That(s2025.MinimumSalary3Plus, Is.EqualTo(2_378_870L));
        Assert.That(s2025.TwoWaySalary, Is.EqualTo(636_435L));
        Assert.That(s2025.Verified, Is.True);

        var s2026 = CbaTable.For(2026);
        Assert.That(s2026.SalaryCap, Is.EqualTo(164_961_000L));
        Assert.That(s2026.LuxuryTax, Is.EqualTo(200_428_000L));
        Assert.That(s2026.FirstApron, Is.EqualTo(209_015_000L));
        Assert.That(s2026.SecondApron, Is.EqualTo(221_686_000L));
        Assert.That(s2026.NonTaxpayerMle, Is.EqualTo(15_044_000L));
        Assert.That(s2026.TaxpayerMle, Is.EqualTo(6_064_000L));
        Assert.That(s2026.BiAnnual, Is.EqualTo(5_477_000L));
        Assert.That(s2026.MinimumSalary, Is.EqualTo(1_357_763L));
        Assert.That(s2026.MinimumSalary1, Is.EqualTo(2_185_116L));
        Assert.That(s2026.MinimumSalary2, Is.EqualTo(2_449_421L));
        Assert.That(s2026.MinimumSalary3Plus, Is.EqualTo(2_537_526L));
        Assert.That(s2026.TwoWaySalary, Is.EqualTo(678_882L));
        Assert.That(s2026.Verified, Is.True);
    }

    [Test]
    public void MinimumFor_UsesExperienceBands()
    {
        var s = CbaTable.For(2026);
        Assert.That(s.MinimumFor(0), Is.EqualTo(1_357_763L));
        Assert.That(s.MinimumFor(1), Is.EqualTo(2_185_116L));
        Assert.That(s.MinimumFor(2), Is.EqualTo(2_449_421L));
        Assert.That(s.MinimumFor(3), Is.EqualTo(2_537_526L));
        Assert.That(s.MinimumFor(10), Is.EqualTo(2_537_526L), "3+ agrupa a los veteranos");

        var settings = new LeagueSettingsData { season_start_year = 2025 };
        Assert.That(TradeHelper.MinimumSalary(settings, 0), Is.EqualTo(1_272_870L));
        Assert.That(TradeHelper.MinimumSalary(settings, 1), Is.EqualTo(2_048_494L));
        Assert.That(TradeHelper.MinimumSalary(settings, 2), Is.EqualTo(2_296_274L));
        Assert.That(TradeHelper.MinimumSalary(settings, 4), Is.EqualTo(2_378_870L));
    }

    [Test]
    public void Thresholds_AreOrderedAndMonotonicAcrossSeasons()
    {
        long previousCap = 0;
        for (int year = 2024; year <= 2040; year++)
        {
            var s = CbaTable.For(year);
            Assert.That(s.SalaryCap, Is.GreaterThan(0), $"cap {year}");
            Assert.That(s.SalaryCap, Is.LessThan(s.LuxuryTax), $"cap < tax {year}");
            Assert.That(s.LuxuryTax, Is.LessThan(s.FirstApron), $"tax < 1er apron {year}");
            Assert.That(s.FirstApron, Is.LessThan(s.SecondApron), $"1er < 2º apron {year}");
            Assert.That(s.NonTaxpayerMle, Is.GreaterThan(s.TaxpayerMle), $"NT-MLE > T-MLE {year}");
            Assert.That(s.TwoWaySalary, Is.GreaterThan(0), $"two-way {year}");
            Assert.That(s.MinimumSalary, Is.GreaterThan(0), $"mínimo 0 años {year}");
            Assert.That(s.MinimumSalary, Is.LessThan(s.MinimumSalary1), $"mínimo 0 < 1 año {year}");
            Assert.That(s.MinimumSalary1, Is.LessThan(s.MinimumSalary2), $"mínimo 1 < 2 años {year}");
            Assert.That(s.MinimumSalary2, Is.LessThan(s.MinimumSalary3Plus), $"mínimo 2 < 3+ años {year}");
            Assert.That(s.SalaryCap, Is.GreaterThanOrEqualTo(previousCap), $"cap no decrece {year}");
            previousCap = s.SalaryCap;
        }
    }

    [Test]
    public void CurrentSeason_MatchesTradeHelperFallbacks()
    {
        var s = CbaTable.For(CbaTable.CurrentSeasonStartYear);
        Assert.That(TradeHelper.SALARY_CAP, Is.EqualTo(s.SalaryCap));
        Assert.That(TradeHelper.LUXURY_TAX, Is.EqualTo(s.LuxuryTax));
        Assert.That(TradeHelper.FIRST_APRON, Is.EqualTo(s.FirstApron));
        Assert.That(TradeHelper.SECOND_APRON, Is.EqualTo(s.SecondApron));
        Assert.That(TradeHelper.NT_MLE, Is.EqualTo(s.NonTaxpayerMle));
        Assert.That(TradeHelper.T_MLE, Is.EqualTo(s.TaxpayerMle));
        Assert.That(TradeHelper.TWO_WAY_SALARY, Is.EqualTo(s.TwoWaySalary));
    }

    [Test]
    public void FutureSeasons_AreMarkedAsProjections()
    {
        Assert.That(CbaTable.For(2026).Verified, Is.True);
        Assert.That(CbaTable.For(2027).Verified, Is.False);
        Assert.That(CbaTable.For(2027).Source, Does.Contain("RealGM"));
        Assert.That(CbaTable.For(CbaTable.LastSeasonStartYear + 1).Verified, Is.False);
        Assert.That(CbaTable.For(CbaTable.LastSeasonStartYear + 1).Source, Does.Contain("Proyección del juego"));
    }

    [Test]
    public void BeyondTheTable_GrowsAtFivePercentPerSeason()
    {
        var last = CbaTable.For(CbaTable.LastSeasonStartYear);
        var next = CbaTable.For(CbaTable.LastSeasonStartYear + 1);
        double ratio = (double)next.SalaryCap / last.SalaryCap;
        Assert.That(ratio, Is.EqualTo(1.05).Within(0.001));
    }

    [Test]
    public void CapRatio_MatchesTheTable()
    {
        Assert.That(CbaTable.CapRatio(2025, 2025), Is.EqualTo(1.0).Within(0.000001));
        double ratio = CbaTable.CapRatio(2025, 2026);
        Assert.That(ratio, Is.EqualTo(164_961_000.0 / 154_647_000.0).Within(0.000001));
    }

    [Test]
    public void ApplyTo_WritesSeasonAndTheMinimumOfZeroYears()
    {
        var settings = new LeagueSettingsData
        {
            minimum_salary = 2_000_000,
            is_active = 1
        };

        CbaTable.ApplyTo(settings, 2027);
        var s2027 = CbaTable.For(2027);
        Assert.That(settings.salary_cap, Is.EqualTo(s2027.SalaryCap));
        Assert.That(settings.luxury_tax, Is.EqualTo(s2027.LuxuryTax));
        Assert.That(settings.apron, Is.EqualTo(s2027.FirstApron));
        Assert.That(settings.repeater_apron, Is.EqualTo(s2027.SecondApron));
        Assert.That(settings.mid_level, Is.EqualTo(s2027.NonTaxpayerMle));
        Assert.That(settings.taxpayer_mid_level, Is.EqualTo(s2027.TaxpayerMle));
        Assert.That(settings.bi_annual, Is.EqualTo(s2027.BiAnnual));
        Assert.That(settings.two_way_salary, Is.EqualTo(s2027.TwoWaySalary));
        Assert.That(settings.season_start_year, Is.EqualTo(2027));
        Assert.That(settings.cba_projected, Is.EqualTo(1));
        Assert.That(settings.cba_source, Is.Not.Empty);
        Assert.That(settings.minimum_salary, Is.EqualTo(s2027.MinimumSalary));

        CbaTable.ApplyTo(settings, 2026);
        Assert.That(settings.season_start_year, Is.EqualTo(2026));
        Assert.That(settings.cba_projected, Is.EqualTo(0));
    }

    [Test]
    public void InvalidSeason_DefaultsToTheCurrentOne()
    {
        Assert.That(CbaTable.For(0).StartYear, Is.EqualTo(CbaTable.CurrentSeasonStartYear));
        Assert.That(CbaTable.For(-10).StartYear, Is.EqualTo(CbaTable.CurrentSeasonStartYear));
    }
}
