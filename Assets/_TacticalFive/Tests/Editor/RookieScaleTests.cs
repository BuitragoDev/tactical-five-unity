using NUnit.Framework;

/// <summary>
/// F4-06: escala salarial de rookie por puesto de draft y temporada. Reglas puras,
/// sin base de datos ni UnityEngine.Random.
/// </summary>
public class RookieScaleTests
{
    [Test]
    public void FirstRoundSalary_DecreasesStrictlyWithPick_ForEverySeason()
    {
        foreach (int season in new[] { RookieScale.BaseSeasonStartYear, 2026, 2030 })
        {
            long previous = long.MaxValue;
            for (int pick = 1; pick <= 30; pick++)
            {
                long salary = RookieScale.SalaryFor(pick, season);
                Assert.That(salary, Is.LessThan(previous),
                    $"pick {pick} de la temporada {season} debe cobrar menos que el anterior");
                Assert.That(salary, Is.GreaterThan(0));
                previous = salary;
            }
        }
    }

    [Test]
    public void SecondRoundSalary_DecreasesStrictlyAndStaysBelowFirstRound()
    {
        long pick30 = RookieScale.SalaryFor(30, RookieScale.BaseSeasonStartYear);
        long previous = pick30;
        for (int pick = 31; pick <= RookieScale.TotalPicks; pick++)
        {
            long salary = RookieScale.SalaryFor(pick, RookieScale.BaseSeasonStartYear);
            Assert.That(salary, Is.LessThan(previous), $"pick {pick} debe cobrar menos que el {pick - 1}");
            previous = salary;
        }
    }

    [Test]
    public void Salary_IsDeterministicForSamePickAndSeason()
    {
        for (int pick = 1; pick <= RookieScale.TotalPicks; pick++)
        {
            long first = RookieScale.SalaryFor(pick, 2027);
            long second = RookieScale.SalaryFor(pick, 2027);
            Assert.That(second, Is.EqualTo(first), $"el pick {pick} debe tener un salario fijo");
        }
    }

    [Test]
    public void Salary_GrowsWithSeasonAtTheConfiguredRate()
    {
        long basePick1 = RookieScale.SalaryFor(1, RookieScale.BaseSeasonStartYear);
        long nextSeasonPick1 = RookieScale.SalaryFor(1, RookieScale.BaseSeasonStartYear + 1);
        long tenYears = RookieScale.SalaryFor(1, RookieScale.BaseSeasonStartYear + 10);

        Assert.That(nextSeasonPick1, Is.GreaterThan(basePick1));
        Assert.That(nextSeasonPick1, Is.InRange(
            (long)(basePick1 * 1.04), (long)(basePick1 * 1.06)));
        Assert.That(tenYears, Is.GreaterThan(nextSeasonPick1));
    }

    [Test]
    public void PreviousSeasons_KeepTheBaseTableWithoutDiscount()
    {
        Assert.That(RookieScale.SalaryFor(5, RookieScale.BaseSeasonStartYear - 3),
            Is.EqualTo(RookieScale.SalaryFor(5, RookieScale.BaseSeasonStartYear)));
    }

    [Test]
    public void TwoWayContract_UsesTheFixedLeagueSalary_RegardlessOfPickAndSeason()
    {
        for (int pick = 1; pick <= RookieScale.TotalPicks; pick++)
            for (int season = 2025; season <= 2029; season++)
                Assert.That(RookieScale.SalaryFor(pick, season, isTwoWay: true),
                    Is.EqualTo(TradeHelper.TWO_WAY_SALARY));
    }

    [Test]
    public void OutOfRangePicks_AreClampedToTheAvailableScale()
    {
        Assert.That(RookieScale.SalaryFor(0, 2025), Is.EqualTo(RookieScale.SalaryFor(1, 2025)));
        Assert.That(RookieScale.SalaryFor(-5, 2025), Is.EqualTo(RookieScale.SalaryFor(1, 2025)));
        Assert.That(RookieScale.SalaryFor(61, 2025), Is.EqualTo(RookieScale.SalaryFor(60, 2025)));
    }

    [Test]
    public void SalaryIsRoundedToTenThousand()
    {
        for (int pick = 1; pick <= RookieScale.TotalPicks; pick++)
        {
            long salary = RookieScale.SalaryFor(pick, 2026);
            Assert.That(salary % 10_000, Is.Zero, $"el pick {pick} debe quedar redondeado a 10.000");
        }
    }
}
