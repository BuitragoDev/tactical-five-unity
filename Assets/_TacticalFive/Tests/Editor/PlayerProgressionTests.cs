using NUnit.Framework;

/// <summary>
/// Progresión/declive de atributos (Fase 3): el desarrollo depende de edad,
/// potencial, minutos jugados y prioridad posicional.
/// </summary>
public class PlayerProgressionTests
{
    [Test]
    public void AttributeDelta_YoungHighPotentialHighMinutes_GrowsFast()
    {
        // 19 años: base 4 + prioridad 1 + 30 mpg 1 + potencial 90 1 = 7
        Assert.That(PlayerProgression.AttributeDelta(19, 90, 60, 30, true, 0), Is.EqualTo(7));
    }

    [Test]
    public void AttributeDelta_AtPotentialCeiling_DoesNotGrow()
    {
        // Cerca del techo: aunque la base sea positiva, no puede subir.
        Assert.That(PlayerProgression.AttributeDelta(27, 80, 80, 30, false, 1), Is.EqualTo(0));
    }

    [Test]
    public void AttributeDelta_Veteran_Declines()
    {
        Assert.That(PlayerProgression.AttributeDelta(36, 70, 70, 20, false, 0), Is.EqualTo(-5));
    }

    [Test]
    public void AttributeDelta_LowPotentialYoung_ProgressesSlower()
    {
        // 20 años, poco potencial y pocos minutos: 4 - 1 (mpg) - 1 (potencial) = 2
        Assert.That(PlayerProgression.AttributeDelta(20, 65, 50, 10, false, 0), Is.EqualTo(2));
    }

    [Test]
    public void AttributeDelta_WithoutMinutesData_SkipsMinutesFactor()
    {
        Assert.That(PlayerProgression.AttributeDelta(20, 80, 50, -1, false, 0), Is.EqualTo(4));
    }

    [Test]
    public void AgeBase_FollowsCareerCurve()
    {
        Assert.That(PlayerProgression.AgeBase(19), Is.GreaterThan(PlayerProgression.AgeBase(25)));
        Assert.That(PlayerProgression.AgeBase(25), Is.GreaterThan(PlayerProgression.AgeBase(40)));
        Assert.That(PlayerProgression.AgeBase(40), Is.LessThan(0));
    }
}
