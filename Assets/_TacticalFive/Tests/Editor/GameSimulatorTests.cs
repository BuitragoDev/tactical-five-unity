using NUnit.Framework;

public class GameSimulatorTests
{
    [Test]
    public void Rebounds_PrioritizeInteriorPositions()
    {
        Assert.That(
            GameSimulator.GetReboundPositionMultiplier("C", false),
            Is.GreaterThan(GameSimulator.GetReboundPositionMultiplier("PG", false)));
        Assert.That(
            GameSimulator.GetReboundPositionMultiplier("PF", true),
            Is.GreaterThan(GameSimulator.GetReboundPositionMultiplier("SG", true)));
    }

    [Test]
    public void Blocks_PrioritizeCentersAndPowerForwards()
    {
        Assert.That(
            GameSimulator.GetBlockPositionMultiplier("C"),
            Is.GreaterThan(GameSimulator.GetBlockPositionMultiplier("PG")));
        Assert.That(
            GameSimulator.GetBlockPositionMultiplier("PF"),
            Is.GreaterThan(GameSimulator.GetBlockPositionMultiplier("SG")));
    }

    [Test]
    public void UnknownPosition_UsesNeutralMultiplier()
    {
        Assert.That(GameSimulator.GetReboundPositionMultiplier("", false), Is.EqualTo(1f));
        Assert.That(GameSimulator.GetBlockPositionMultiplier(""), Is.EqualTo(1f));
    }

    [Test]
    public void StartersAndStars_ReceiveHigherMinuteTargets()
    {
        var star = new GameSimulator.PlayerStatSnapshot
        {
            role = PlayerRole.Estrella,
            overall = 90,
            starter = true
        };
        var bench = new GameSimulator.PlayerStatSnapshot
        {
            role = PlayerRole.Banquillo,
            overall = 72,
            starter = false
        };

        Assert.That(GameSimulator.GetTargetMinutes(star), Is.EqualTo(39));
        Assert.That(GameSimulator.GetTargetMinutes(bench), Is.EqualTo(15));
        Assert.That(GameSimulator.GetTargetMinutes(star), Is.GreaterThan(GameSimulator.GetTargetMinutes(bench)));
    }
}
