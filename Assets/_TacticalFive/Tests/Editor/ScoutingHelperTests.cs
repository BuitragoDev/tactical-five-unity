using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Scouting de draft (Fase 3): la evaluación tiene un margen de error que depende de
/// la reputación de los ojeadores; el valor real se revela al elegir el pick.
/// </summary>
public class ScoutingHelperTests
{
    [Test]
    public void MaxError_DecreasesWithBetterScouts()
    {
        Assert.That(ScoutingHelper.MaxError(1), Is.EqualTo(5));
        Assert.That(ScoutingHelper.MaxError(3), Is.EqualTo(3));
        Assert.That(ScoutingHelper.MaxError(5), Is.EqualTo(1));
    }

    [Test]
    public void ErrorFor_NeverExceedsTheRange()
    {
        for (int rep = 1; rep <= 5; rep++)
        {
            int max = ScoutingHelper.MaxError(rep);
            for (int r1 = -max; r1 <= max; r1++)
            for (int r2 = -max; r2 <= max; r2++)
            {
                int e = ScoutingHelper.ErrorFor(rep, r1, r2);
                Assert.That(System.Math.Abs(e), Is.LessThanOrEqualTo(max));
            }
        }
    }

    [Test]
    public void EstimateAndReveal_AreInverse()
    {
        int trueValue = 72;
        int error = 4;
        int estimate = ScoutingHelper.Estimate(trueValue, error);
        Assert.That(estimate, Is.EqualTo(76));
        Assert.That(ScoutingHelper.Reveal(estimate, error), Is.EqualTo(trueValue));
    }

    [Test]
    public void Estimate_ClampsToValidRange()
    {
        Assert.That(ScoutingHelper.Estimate(97, 5), Is.LessThanOrEqualTo(99));
        Assert.That(ScoutingHelper.Reveal(27, 5), Is.GreaterThanOrEqualTo(25));
    }

    [Test]
    public void ScoutReputation_TakesTheBestScout()
    {
        var staff = new List<EmployeeData>
        {
            new EmployeeData { position = "OJEADOR", reputation = 2 },
            new EmployeeData { position = "OJEADOR", reputation = 4 },
            new EmployeeData { position = "MEDICO", reputation = 5 }
        };
        Assert.That(ScoutingHelper.ScoutReputation(staff), Is.EqualTo(4));
    }

    [Test]
    public void ScoutReputation_WithoutScouts_IsOne()
    {
        Assert.That(ScoutingHelper.ScoutReputation(null), Is.EqualTo(1));
        Assert.That(ScoutingHelper.ScoutReputation(new List<EmployeeData>()), Is.EqualTo(1));
    }
}
