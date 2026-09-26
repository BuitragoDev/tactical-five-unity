using NUnit.Framework;

/// <summary>
/// Negociación con el agente (Fase 3): la contraoferta sube la oferta sin superar
/// nunca el máximo legal del equipo.
/// </summary>
public class NegotiationHelperTests
{
    [Test]
    public void CounterSalary_IsAboveTheOffer_AndWithinTheLegalMax()
    {
        long counter = NegotiationHelper.CounterSalary(10_000_000, 30, 20_000_000);
        Assert.That(counter, Is.GreaterThan(10_000_000));
        Assert.That(counter, Is.LessThanOrEqualTo(20_000_000));
        Assert.That(counter, Is.EqualTo(13_800_000));
    }

    [Test]
    public void CounterSalary_CloserAcceptance_AsksForLess()
    {
        long near = NegotiationHelper.CounterSalary(10_000_000, 95, 50_000_000);
        long far = NegotiationHelper.CounterSalary(10_000_000, 30, 50_000_000);
        Assert.That(near, Is.LessThan(far));
    }

    [Test]
    public void CounterSalary_AtTheLegalCeiling_ReturnsZero()
    {
        Assert.That(NegotiationHelper.CounterSalary(10_000_000, 50, 10_000_000), Is.EqualTo(0L));
        Assert.That(NegotiationHelper.CounterSalary(10_000_000, 50, 9_000_000), Is.EqualTo(0L));
    }

    [Test]
    public void CounterSalary_IsAlwaysRoundedTo100k()
    {
        long counter = NegotiationHelper.CounterSalary(10_050_000, 95, 50_000_000);
        Assert.That(counter % 100_000, Is.EqualTo(0));
        Assert.That(NegotiationHelper.RoundTo100k(13_849_999), Is.EqualTo(13_800_000));
    }
}
