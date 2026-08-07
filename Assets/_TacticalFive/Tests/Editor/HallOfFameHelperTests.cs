using NUnit.Framework;

public class HallOfFameHelperTests
{
    [Test]
    public void ShouldInduct_AnyRing_ReturnsTrue()
    {
        Assert.That(HallOfFameHelper.ShouldInduct(rings: 1, finalsMvps: 0,
            careerPoints: 5000, careerRebounds: 2000, careerAssists: 1000), Is.True);
    }

    [Test]
    public void ShouldInduct_TwoRingsEvenWithoutStats_ReturnsTrue()
    {
        Assert.That(HallOfFameHelper.ShouldInduct(rings: 2, finalsMvps: 0,
            careerPoints: 0, careerRebounds: 0, careerAssists: 0), Is.True);
    }

    [Test]
    public void ShouldInduct_FinalsMvp_ReturnsTrue()
    {
        Assert.That(HallOfFameHelper.ShouldInduct(rings: 0, finalsMvps: 1,
            careerPoints: 8000, careerRebounds: 4000, careerAssists: 2000), Is.True);
    }

    [Test]
    public void ShouldInduct_PointsThreshold_ReturnsTrue()
    {
        Assert.That(HallOfFameHelper.ShouldInduct(rings: 0, finalsMvps: 0,
            careerPoints: HallOfFameHelper.CAREER_POINTS_THRESHOLD,
            careerRebounds: 0, careerAssists: 0), Is.True);
    }

    [Test]
    public void ShouldInduct_ReboundsThreshold_ReturnsTrue()
    {
        Assert.That(HallOfFameHelper.ShouldInduct(rings: 0, finalsMvps: 0,
            careerPoints: 0, careerRebounds: HallOfFameHelper.CAREER_REBOUNDS_THRESHOLD,
            careerAssists: 0), Is.True);
    }

    [Test]
    public void ShouldInduct_AssistsThreshold_ReturnsTrue()
    {
        Assert.That(HallOfFameHelper.ShouldInduct(rings: 0, finalsMvps: 0,
            careerPoints: 0, careerRebounds: 0,
            careerAssists: HallOfFameHelper.CAREER_ASSISTS_THRESHOLD), Is.True);
    }

    [Test]
    public void ShouldInduct_NoMilestones_ReturnsFalse()
    {
        Assert.That(HallOfFameHelper.ShouldInduct(rings: 0, finalsMvps: 0,
            careerPoints: 8000, careerRebounds: 3000, careerAssists: 2000), Is.False);
    }

    [Test]
    public void ShouldInduct_JustBelowThresholds_ReturnsFalse()
    {
        Assert.That(HallOfFameHelper.ShouldInduct(rings: 0, finalsMvps: 0,
            careerPoints: HallOfFameHelper.CAREER_POINTS_THRESHOLD - 1,
            careerRebounds: HallOfFameHelper.CAREER_REBOUNDS_THRESHOLD - 1,
            careerAssists: HallOfFameHelper.CAREER_ASSISTS_THRESHOLD - 1), Is.False);
    }

    [Test]
    public void ShouldInduct_AllZero_ReturnsFalse()
    {
        Assert.That(HallOfFameHelper.ShouldInduct(rings: 0, finalsMvps: 0,
            careerPoints: 0, careerRebounds: 0, careerAssists: 0), Is.False);
    }
}