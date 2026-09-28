using NUnit.Framework;

/// <summary>
/// Esquema del asistente (Fase 3): bonus de calidad y mezcla con el plan del usuario.
/// </summary>
public class CoachSchemeTests
{
    [Test]
    public void RatingBonus_ScalesWithReputation()
    {
        Assert.That(CoachScheme.RatingBonus(5), Is.EqualTo(2));
        Assert.That(CoachScheme.RatingBonus(4), Is.EqualTo(1));
        Assert.That(CoachScheme.RatingBonus(3), Is.EqualTo(0));
        Assert.That(CoachScheme.RatingBonus(2), Is.EqualTo(-1));
        Assert.That(CoachScheme.RatingBonus(1), Is.EqualTo(-2));
    }

    [Test]
    public void Blend_WithoutScheme_ReturnsUserPlan()
    {
        var user = new GamePlan { pace = 1, offense = 2, defense = 2 };
        var coach = new EmployeeData { scheme_set = 0 };
        var p = CoachScheme.Blend(user, coach);
        Assert.That(p.pace, Is.EqualTo(1));
        Assert.That(p.offense, Is.EqualTo(2));
        Assert.That(p.defense, Is.EqualTo(2));
    }

    [Test]
    public void Blend_AveragesUserPlanAndCoachScheme()
    {
        var user = new GamePlan { pace = 1, offense = 2, defense = 0 };
        var coach = new EmployeeData { scheme_set = 1, scheme_pace = -1, scheme_offense = 0, scheme_defense = 2 };
        var p = CoachScheme.Blend(user, coach);
        Assert.That(p.pace, Is.EqualTo(0));      // (1 + -1)/2
        Assert.That(p.offense, Is.EqualTo(1));   // (2 + 0)/2
        Assert.That(p.defense, Is.EqualTo(1));   // (0 + 2)/2
    }

    [Test]
    public void AssignRandomScheme_MarksItAsSet()
    {
        var e = new EmployeeData();
        CoachScheme.AssignRandomScheme(e, new System.Random(42));
        Assert.That(e.scheme_set, Is.EqualTo(1), "El esquema queda marcado como asignado");
        Assert.That(CoachScheme.SchemeLabel(e), Is.Not.Empty);
    }
}
