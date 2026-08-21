using System.Collections.Generic;
using NUnit.Framework;

public class GLeagueHelperTests
{
    static PlayerData P(int injuryDays = 0, int onIR = 0, int glAssigned = 0, int teamId = 1)
    {
        return new PlayerData
        {
            team_id = teamId,
            injury_days = injuryDays,
            is_on_ir = onIR,
            g_league_assigned = glAssigned
        };
    }

    // ── Elegibilidad two-way (TradeHelper) ────────────────

    [Test]
    public void IsEligibleForTwoWay_AgeThreshold()
    {
        Assert.That(TradeHelper.IsEligibleForTwoWay(new PlayerData { age = 23 }), Is.True);
        Assert.That(TradeHelper.IsEligibleForTwoWay(new PlayerData { age = 21 }), Is.True);
        Assert.That(TradeHelper.IsEligibleForTwoWay(new PlayerData { age = 24 }), Is.False);
    }

    [Test]
    public void IsEligibleForIR_MinDays()
    {
        Assert.That(TradeHelper.IsEligibleForIR(new PlayerData { injury_days = 90 }), Is.True);
        Assert.That(TradeHelper.IsEligibleForIR(new PlayerData { injury_days = 110 }), Is.True);
        Assert.That(TradeHelper.IsEligibleForIR(new PlayerData { injury_days = 89 }), Is.False);
    }

    // ── GLeagueHelper ─────────────────────────────────────

    [Test]
    public void CanAssign_OnlyHealthyNonIRNonGL()
    {
        Assert.That(GLeagueHelper.CanAssign(P()), Is.True);
        Assert.That(GLeagueHelper.CanAssign(P(injuryDays: 1)), Is.False);
        Assert.That(GLeagueHelper.CanAssign(P(onIR: 1)), Is.False);
        Assert.That(GLeagueHelper.CanAssign(P(glAssigned: 1)), Is.False);
        Assert.That(GLeagueHelper.CanAssign(P(teamId: 0)), Is.False);
        Assert.That(GLeagueHelper.CanAssign(null), Is.False);
    }

    [Test]
    public void CanRecall_OnlyAssigned()
    {
        Assert.That(GLeagueHelper.CanRecall(P(glAssigned: 1)), Is.True);
        Assert.That(GLeagueHelper.CanRecall(P()), Is.False);
        Assert.That(GLeagueHelper.CanRecall(null), Is.False);
    }

    [Test]
    public void HasEnoughActive_RequiresTwelveAfterAssignment()
    {
        var thirteen = new List<PlayerData>();
        for (int i = 0; i < 13; i++) thirteen.Add(P());
        Assert.That(GLeagueHelper.HasEnoughActive(thirteen), Is.True);

        var twelve = new List<PlayerData>();
        for (int i = 0; i < 12; i++) twelve.Add(P());
        Assert.That(GLeagueHelper.HasEnoughActive(twelve), Is.False);
    }

    [Test]
    public void HasEnoughActive_IgnoresInjuredIRAndGL()
    {
        var roster = new List<PlayerData>();
        for (int i = 0; i < 11; i++) roster.Add(P());
        roster.Add(P(injuryDays: 5));
        roster.Add(P(onIR: 1));
        roster.Add(P(glAssigned: 1));
        // Solo 11 activos: 11-1=10 < 12 → false
        Assert.That(GLeagueHelper.HasEnoughActive(roster), Is.False);
    }

    [Test]
    public void ProcessDevelopmentTick_RespectsPotentialAndRecalculatesOverall()
    {
        var p = new PlayerData
        {
            team_id = 1,
            g_league_assigned = 1,
            potential = 60,
            speed = 50, shooting = 50, three_point = 50, passing = 50,
            dribbling = 50, defense = 50, rebounding = 50, athleticism = 50,
            iq = 50, steals = 50, blocks = 50
        };

        for (int i = 0; i < 200; i++)
            GLeagueHelper.ProcessDevelopmentTick(p);

        Assert.That(p.speed, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.shooting, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.three_point, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.passing, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.dribbling, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.defense, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.rebounding, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.athleticism, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.iq, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.steals, Is.LessThanOrEqualTo(p.potential));
        Assert.That(p.blocks, Is.LessThanOrEqualTo(p.potential));

        // overall se mantiene consistente con la media de 11 atributos capada por potential
        Assert.That(p.overall, Is.EqualTo(System.Math.Min(p.potential, p.GetCalculatedAverage())));
    }

    [Test]
    public void ProcessDevelopmentTick_OnlyAppliesToAssignedPlayers()
    {
        var notAssigned = new PlayerData
        {
            team_id = 1,
            g_league_assigned = 0,
            potential = 60,
            speed = 50, shooting = 50, three_point = 50, passing = 50,
            dribbling = 50, defense = 50, rebounding = 50, athleticism = 50,
            iq = 50, steals = 50, blocks = 50
        };
        Assert.That(GLeagueHelper.ProcessDevelopmentTick(notAssigned), Is.False);
        Assert.That(notAssigned.speed, Is.EqualTo(50));
    }
}
