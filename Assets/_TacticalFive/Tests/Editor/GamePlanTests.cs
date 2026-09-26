using NUnit.Framework;

/// <summary>
/// Plan de partido (Fase 3): efectos tácticos puros usados por el simulador y la IA.
/// </summary>
public class GamePlanTests
{
    [Test]
    public void Neutral_HasNoEffects()
    {
        var n = GamePlan.Neutral;
        Assert.That(n.ThreePointBias(), Is.EqualTo(0f));
        Assert.That(n.ForcedTurnoverDelta(), Is.EqualTo(0f));
        Assert.That(n.FoulDelta(), Is.EqualTo(0f));
    }

    [Test]
    public void Exterior_ShootsMoreThrees_Interior_Fewer()
    {
        var exterior = new GamePlan { offense = 2 };
        var interior = new GamePlan { offense = 1 };
        Assert.That(exterior.ThreePointBias(), Is.GreaterThan(0f));
        Assert.That(interior.ThreePointBias(), Is.LessThan(0f));
    }

    [Test]
    public void Agresiva_ForcesMoreTurnoversAndFouls_Conservadora_Fewer()
    {
        var agresiva = new GamePlan { defense = 2 };
        var conservadora = new GamePlan { defense = 0 };
        Assert.That(agresiva.ForcedTurnoverDelta(), Is.GreaterThan(0f));
        Assert.That(agresiva.FoulDelta(), Is.GreaterThan(0f));
        Assert.That(conservadora.ForcedTurnoverDelta(), Is.LessThan(0f));
        Assert.That(conservadora.FoulDelta(), Is.LessThan(0f));
    }

    [Test]
    public void FromStrategy_Contend_IsFastAndAggressive()
    {
        var p = GamePlan.FromStrategy(TeamStrategy.Contend);
        Assert.That(p.pace, Is.EqualTo(1));
        Assert.That(p.offense, Is.EqualTo(2));
        Assert.That(p.defense, Is.EqualTo(2));
    }

    [Test]
    public void FromStrategy_Rebuild_IsSlowAndInside()
    {
        var p = GamePlan.FromStrategy(TeamStrategy.Rebuild);
        Assert.That(p.pace, Is.EqualTo(-1));
        Assert.That(p.offense, Is.EqualTo(1));
    }

    [Test]
    public void PaceAdjustment_AveragesBothTeams()
    {
        var normal = GamePlan.Neutral;
        Assert.That(GamePlan.PaceAdjustment(normal, normal), Is.EqualTo(0f));
        Assert.That(GamePlan.PaceAdjustment(new GamePlan { pace = 1 }, new GamePlan { pace = 1 }), Is.EqualTo(4f));
        Assert.That(GamePlan.PaceAdjustment(new GamePlan { pace = 1 }, new GamePlan { pace = -1 }), Is.EqualTo(0f));
    }
}
