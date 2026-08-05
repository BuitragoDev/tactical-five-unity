using NUnit.Framework;

public class AdvancedStatsHelperTests
{
    [Test]
    public void CalcEFG_NoThrees_EqualsStandardFG()
    {
        Assert.That(AdvancedStatsHelper.CalcEFG(10, 20, 0), Is.EqualTo(50f));
    }

    [Test]
    public void CalcEFG_WithThrees_WeightedCorrectly()
    {
        Assert.That(AdvancedStatsHelper.CalcEFG(10, 20, 4), Is.EqualTo(60f));
    }

    [Test]
    public void CalcEFG_AllThrees()
    {
        Assert.That(AdvancedStatsHelper.CalcEFG(6, 12, 6), Is.EqualTo(75f));
    }

    [Test]
    public void CalcEFG_ZeroFGA_ReturnsZero()
    {
        Assert.That(AdvancedStatsHelper.CalcEFG(0, 0, 0), Is.EqualTo(0f));
    }

    [Test]
    public void CalcTS_StandardLine()
    {
        var ts = AdvancedStatsHelper.CalcTS(30, 20, 5);
        Assert.That(ts, Is.GreaterThan(67f).And.LessThan(68f));
    }

    [Test]
    public void CalcTS_ZeroDenom_ReturnsZero()
    {
        Assert.That(AdvancedStatsHelper.CalcTS(0, 0, 0), Is.EqualTo(0f));
    }

    [Test]
    public void CalcTS_PureFoulShooter()
    {
        var ts = AdvancedStatsHelper.CalcTS(20, 0, 10);
        Assert.That(ts, Is.GreaterThan(0f));
    }

    [Test]
    public void CalcEff_PositiveLine()
    {
        var eff = AdvancedStatsHelper.CalcEff(
            pts: 25, reb: 10, ast: 5, stl: 2, blk: 1,
            fgm: 10, fga: 20, ftm: 4, fta: 5, tov: 3);
        Assert.That(eff, Is.EqualTo(23));
    }

    [Test]
    public void CalcEff_PoorShooting_Negative()
    {
        var eff = AdvancedStatsHelper.CalcEff(
            pts: 5, reb: 3, ast: 1, stl: 0, blk: 0,
            fgm: 2, fga: 15, ftm: 1, fta: 4, tov: 4);
        Assert.That(eff, Is.LessThan(0));
    }

    [Test]
    public void CalcPER_StandardPlayer()
    {
        var per = AdvancedStatsHelper.CalcPER(eff: 600f, minutes: 720f);
        Assert.That(per, Is.EqualTo(40f).Within(0.01f));
    }

    [Test]
    public void CalcPER_ZeroMinutes_ReturnsZero()
    {
        Assert.That(AdvancedStatsHelper.CalcPER(100f, 0f), Is.EqualTo(0f));
    }
}
