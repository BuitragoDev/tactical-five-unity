using NUnit.Framework;

/// <summary>
/// Invariante del overall (SEA-08/09 de la auditoría): debe ser la media redondeada
/// de los 11 atributos, capada por el potencial, y recalcularse al mutar atributos.
/// </summary>
public class PlayerDataTests
{
    static PlayerData Uniform(int value, int potential = 99) => new PlayerData
    {
        speed = value, shooting = value, three_point = value, passing = value, dribbling = value,
        defense = value, rebounding = value, athleticism = value, iq = value, steals = value,
        blocks = value, potential = potential
    };

    [Test]
    public void GetCalculatedAverage_RoundsAttributes()
    {
        var p = Uniform(80, 99);
        p.blocks = 81; // (10*80 + 81) / 11 = 80.09 -> 80
        Assert.That(p.GetCalculatedAverage(), Is.EqualTo(80));
    }

    [Test]
    public void GetCalculatedAverage_IsCappedByPotential()
    {
        Assert.That(Uniform(90, 70).GetCalculatedAverage(), Is.EqualTo(70));
    }

    [Test]
    public void RecalculateOverall_FollowsAttributeChanges()
    {
        var p = Uniform(80, 99);
        p.overall = 80;
        p.shooting = 99; // (10*80 + 99) / 11 = 81.7 -> 82
        p.RecalculateOverall();
        Assert.That(p.overall, Is.EqualTo(82));
    }

    [Test]
    public void RecalculateOverall_RespectsPotentialCap()
    {
        var p = Uniform(80, 81);
        p.shooting = 99;
        p.RecalculateOverall();
        Assert.That(p.overall, Is.EqualTo(81), "El overall nunca supera el potencial");
    }
}
