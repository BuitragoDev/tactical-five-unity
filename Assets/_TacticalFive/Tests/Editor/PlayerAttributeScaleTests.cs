using NUnit.Framework;

/// <summary>F1-05: rango legal de atributos y límites de generación/scouting.</summary>
public class PlayerAttributeScaleTests
{
    [Test]
    public void IsValid_UsesLegalPlayerRange_OneToNinetyNine()
    {
        Assert.That(PlayerAttributeScale.IsValid(0), Is.False);
        Assert.That(PlayerAttributeScale.IsValid(1), Is.True);
        Assert.That(PlayerAttributeScale.IsValid(6), Is.True); // valor bajo, pero válido
        Assert.That(PlayerAttributeScale.IsValid(25), Is.True);
        Assert.That(PlayerAttributeScale.IsValid(99), Is.True);
        Assert.That(PlayerAttributeScale.IsValid(100), Is.False);
    }

    [Test]
    public void ClampValid_ClampsOnlyToLegalScale()
    {
        Assert.That(PlayerAttributeScale.ClampValid(-5), Is.EqualTo(1));
        Assert.That(PlayerAttributeScale.ClampValid(6), Is.EqualTo(6));
        Assert.That(PlayerAttributeScale.ClampValid(100), Is.EqualTo(99));
    }

    [Test]
    public void ClampGenerated_KeepsItsSeparateThirtyPointFloor()
    {
        Assert.That(PlayerAttributeScale.ClampGenerated(20), Is.EqualTo(30));
        Assert.That(PlayerAttributeScale.ClampGenerated(45), Is.EqualTo(45));
        Assert.That(PlayerAttributeScale.ClampGenerated(100), Is.EqualTo(99));
    }

    [Test]
    public void ClampGenerated_WithPotential_UsesTheLowerCeiling()
    {
        Assert.That(PlayerAttributeScale.ClampGenerated(80, 65), Is.EqualTo(65));
        Assert.That(PlayerAttributeScale.ClampGenerated(20, 65), Is.EqualTo(30));
        Assert.That(PlayerAttributeScale.ClampGenerated(28, 28), Is.EqualTo(28));
    }

    [Test]
    public void ClampScoutingEstimate_KeepsItsSeparateTwentyFivePointFloor()
    {
        Assert.That(PlayerAttributeScale.ClampScoutingEstimate(20), Is.EqualTo(25));
        Assert.That(PlayerAttributeScale.ClampScoutingEstimate(45), Is.EqualTo(45));
        Assert.That(PlayerAttributeScale.ClampScoutingEstimate(100), Is.EqualTo(99));
    }
}
