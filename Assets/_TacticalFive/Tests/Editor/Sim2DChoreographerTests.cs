using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Tests puros del coreógrafo de la vista Visual 2D (F0/F1). No dependen de
/// DatabaseManager ni de UnityEngine.Random: comprueban spacing, emparejamientos,
/// tiros libres 1/2/3, fuera de banda, marcador por pasos y determinismo.
/// </summary>
public class Sim2DChoreographerTests
{
    static Sim2DChoreographer.PlayerInfo P(int id, string position, int teamId = 1)
    {
        return new Sim2DChoreographer.PlayerInfo
        {
            Id = id,
            TeamId = teamId,
            Position = position,
            Name = $"J{id}",
            Overall = 80
        };
    }

    static List<Sim2DChoreographer.PlayerInfo> Offense() => new()
    {
        P(1, "PG"), P(2, "SG"), P(3, "SF"), P(4, "PF"), P(5, "C")
    };

    static List<Sim2DChoreographer.PlayerInfo> Defense() => new()
    {
        P(11, "PG", 2), P(12, "SG", 2), P(13, "SF", 2), P(14, "PF", 2), P(15, "C", 2)
    };

    static GameSimulator.StatDelta D(int id, string stat, float amount = 1f)
    {
        return new GameSimulator.StatDelta { player_id = id, stat = stat, amount = amount };
    }

    static GameSimulator.PlayByPlayEvent Ev(params GameSimulator.StatDelta[] deltas)
    {
        return new GameSimulator.PlayByPlayEvent
        {
            quarter = 1,
            deltas = deltas.ToList(),
            homeOnOffense = true,
            homeScore = 0,
            awayScore = 0,
            timeElapsed = 1f
        };
    }

    static Sim2DChoreographer.Script Build(
        GameSimulator.PlayByPlayEvent ev,
        Sim2DChoreographer.InboundKind inbound = Sim2DChoreographer.InboundKind.Baseline,
        bool attackRight = true,
        int eventIndex = 0,
        int previousHome = 0,
        int previousAway = 0,
        GameSimulator.PlayByPlayEvent previous = null)
    {
        return Sim2DChoreographer.Build(ev, previous, Offense(), Defense(), attackRight,
            inbound, 0.5f, eventIndex, 1, previousHome, previousAway, true);
    }

    [Test]
    public void Build_MadeThree_AddsThreePointsAndKeepsSpacing()
    {
        var ev = Ev(D(3, "fga"), D(3, "fg3a"), D(3, "fgm"), D(3, "pts", 3), D(1, "ast"));
        ev.homeScore = 3;

        var script = Build(ev);

        Assert.That(script, Is.Not.Null);
        Assert.That(script.ScoreSteps.Count, Is.EqualTo(1));
        Assert.That(script.ScoreSteps[0].Home, Is.EqualTo(3));
        Assert.That(script.ScoreSteps[0].Away, Is.EqualTo(0));
        Assert.That(script.ActionIds, Does.Contain(3));

        var finals = new List<UnityEngine.Vector2>();
        foreach (int id in Offense().Select(p => p.Id))
        {
            var path = script.PlayerPaths.FirstOrDefault(p => p.PlayerId == id);
            Assert.That(path, Is.Not.Null, $"Sin recorrido para el jugador {id}");
            var last = path.Points[path.Points.Count - 1];
            finals.Add(new UnityEngine.Vector2(last.X, last.Y));
        }

        // Los cinco atacantes ocupan zonas distintas; ninguno se queda apilado en el centro.
        int distinct = finals.Select(v => $"{v.x:F3},{v.y:F3}").Distinct().Count();
        Assert.That(distinct, Is.EqualTo(5));
        Assert.That(finals.Any(v =>
            UnityEngine.Vector2.Distance(v, new UnityEngine.Vector2(0.5f, 0.5f)) > 0.2f), Is.True);
    }

    [Test]
    public void Build_PostShooter_DoesNotOverlapCenter()
    {
        var ev = Ev(D(4, "fga"), D(4, "fgm"), D(4, "pts", 2));

        var script = Build(ev);

        var pf = script.PlayerPaths.First(p => p.PlayerId == 4).Points.Last();
        var center = script.PlayerPaths.First(p => p.PlayerId == 5).Points.Last();
        var a = new UnityEngine.Vector2(pf.X, pf.Y);
        var b = new UnityEngine.Vector2(center.X, center.Y);
        Assert.That(UnityEngine.Vector2.Distance(a, b), Is.GreaterThan(0.02f));
    }

    [Test]
    public void Build_OffBallActions_AppearAcrossEvents()
    {
        var seen = new HashSet<Sim2DChoreographer.OffBallActionKind>();
        for (int i = 0; i < 40; i++)
        {
            var ev = Ev(D(1, "fga"), D(1, "fgm"), D(1, "pts", 2));
            var script = Build(ev, eventIndex: i);
            if (script.OffBallKind != Sim2DChoreographer.OffBallActionKind.None)
                seen.Add(script.OffBallKind);
        }

        Assert.That(seen, Does.Contain(Sim2DChoreographer.OffBallActionKind.Screen));
        Assert.That(seen, Does.Contain(Sim2DChoreographer.OffBallActionKind.Cut));
        Assert.That(seen, Does.Contain(Sim2DChoreographer.OffBallActionKind.PostUp));
    }

    [Test]
    public void Build_OffBallAction_HasMatchingCue()
    {
        for (int i = 0; i < 40; i++)
        {
            var ev = Ev(D(1, "fga"), D(1, "fgm"), D(1, "pts", 2));
            var script = Build(ev, eventIndex: i);
            if (script.OffBallKind == Sim2DChoreographer.OffBallActionKind.None) continue;

            string expected = script.OffBallKind switch
            {
                Sim2DChoreographer.OffBallActionKind.Screen => "Bloqueo",
                Sim2DChoreographer.OffBallActionKind.Cut => "corta",
                Sim2DChoreographer.OffBallActionKind.PostUp => "poste",
                _ => null
            };
            Assert.That(script.Cues.Any(c => c.Text != null && c.Text.Contains(expected)),
                Is.True, $"Falta el comentario de {script.OffBallKind} en el evento {i}");
        }
    }

    [Test]
    public void Build_OffBallAction_IsDeterministic()
    {
        var ev = Ev(D(1, "fga"), D(1, "fgm"), D(1, "pts", 2));

        var a = Build(ev, eventIndex: 7);
        var b = Build(ev, eventIndex: 7);

        Assert.That(b.OffBallKind, Is.EqualTo(a.OffBallKind));
        Assert.That(b.OffBallActorId, Is.EqualTo(a.OffBallActorId));
        Assert.That(string.Join("|", b.Cues.Select(c => c.Text)),
            Is.EqualTo(string.Join("|", a.Cues.Select(c => c.Text))));
    }

    [Test]
    public void Build_OffBallAction_DoesNotOverlapShooter()
    {
        for (int i = 0; i < 40; i++)
        {
            var ev = Ev(D(1, "fga"), D(1, "fgm"), D(1, "pts", 2));
            var script = Build(ev, eventIndex: i);
            if (script.OffBallKind == Sim2DChoreographer.OffBallActionKind.None) continue;

            var actor = script.PlayerPaths.First(p => p.PlayerId == script.OffBallActorId);
            var shooter = script.PlayerPaths.First(p => p.PlayerId == 1);
            var a = actor.Points[actor.Points.Count - 1];
            var s = shooter.Points[shooter.Points.Count - 1];
            var pa = new UnityEngine.Vector2(a.X, a.Y);
            var ps = new UnityEngine.Vector2(s.X, s.Y);
            Assert.That(UnityEngine.Vector2.Distance(pa, ps), Is.GreaterThan(0.02f),
                $"El actor {script.OffBallActorId} se solapa con el tirador en el evento {i}");
        }
    }

    [Test]
    public void Build_IsDeterministic()
    {
        var ev = Ev(D(1, "fga"), D(1, "fgm"), D(1, "pts", 2));

        var a = Build(ev);
        var b = Build(ev);

        Assert.That(b.BallPath.Count, Is.EqualTo(a.BallPath.Count));
        for (int i = 0; i < a.BallPath.Count; i++)
        {
            Assert.That(b.BallPath[i].Time, Is.EqualTo(a.BallPath[i].Time).Within(0.0001f));
            Assert.That(b.BallPath[i].X, Is.EqualTo(a.BallPath[i].X).Within(0.0001f));
            Assert.That(b.BallPath[i].Y, Is.EqualTo(a.BallPath[i].Y).Within(0.0001f));
        }
        Assert.That(string.Join("|", b.Cues.Select(c => c.Text)),
            Is.EqualTo(string.Join("|", a.Cues.Select(c => c.Text))));
        Assert.That(b.ScoreSteps.Count, Is.EqualTo(a.ScoreSteps.Count));
    }

    [Test]
    public void AssignMatchups_CoversSamePosition()
    {
        var map = Sim2DChoreographer.AssignMatchups(Offense(), Defense());

        Assert.That(map.Count, Is.EqualTo(5));
        foreach (var defender in Defense())
        {
            Assert.That(map.ContainsKey(defender.Id), Is.True);
            Assert.That(map[defender.Id].Position, Is.EqualTo(defender.Position));
        }
    }

    [Test]
    public void Build_ThreeFreeThrows_ShowsEachAttempt()
    {
        var ev = Ev(D(1, "fta", 3), D(1, "ftm", 2));
        ev.freeThrowResults = new List<bool> { true, false, true };
        ev.freeThrowShooterId = 1;
        ev.homeScore = 2;

        var script = Build(ev, Sim2DChoreographer.InboundKind.None);

        Assert.That(script, Is.Not.Null);
        Assert.That(script.IsFreeThrow, Is.True);
        Assert.That(script.ScoreSteps.Count, Is.EqualTo(2));
        Assert.That(script.ScoreSteps[0].Home, Is.EqualTo(1));
        Assert.That(script.ScoreSteps[1].Home, Is.EqualTo(2));

        int attempts = script.Cues.Count(c => c.Kind == Sim2DChoreographer.CueKind.FreeThrow
            && c.Text != null && c.Text.ToLowerInvariant().Contains("libre"));
        int made = script.Cues.Count(c => c.Text == "Dentro.");
        int missed = script.Cues.Count(c => c.Text == "Falla.");
        Assert.That(attempts, Is.EqualTo(3));
        Assert.That(made, Is.EqualTo(2));
        Assert.That(missed, Is.EqualTo(1));
    }

    [Test]
    public void Build_TwoFreeThrows_ShowsTwoAttempts()
    {
        var ev = Ev(D(1, "fta", 2), D(1, "ftm", 1));
        ev.freeThrowResults = new List<bool> { true, false };
        ev.freeThrowShooterId = 1;
        ev.homeScore = 1;

        var script = Build(ev, Sim2DChoreographer.InboundKind.None);

        Assert.That(script.ScoreSteps.Count, Is.EqualTo(1));
        Assert.That(script.Cues.Count(c => c.Kind == Sim2DChoreographer.CueKind.FreeThrow
            && c.Text != null && c.Text.ToLowerInvariant().Contains("libre")), Is.EqualTo(2));
    }

    [Test]
    public void Build_AndOne_AddsFieldGoalAndFreeThrow()
    {
        var ev = Ev(D(1, "fga"), D(1, "fgm"), D(1, "fta"), D(1, "ftm"));
        ev.freeThrowResults = new List<bool> { true };
        ev.freeThrowShooterId = 1;
        ev.homeScore = 3;

        var script = Build(ev);

        Assert.That(script.ScoreSteps.Count, Is.EqualTo(2));
        Assert.That(script.ScoreSteps[0].Home, Is.EqualTo(2));
        Assert.That(script.ScoreSteps[1].Home, Is.EqualTo(3));
        Assert.That(script.Cues.Any(c => c.Kind == Sim2DChoreographer.CueKind.Shot), Is.True);
        Assert.That(script.Cues.Any(c => c.Kind == Sim2DChoreographer.CueKind.FreeThrow
            && c.Text != null && c.Text.ToLowerInvariant().Contains("libre")), Is.True);
    }

    [Test]
    public void Build_OutOfBounds_AddsTurnoverCueAndNoScore()
    {
        var ev = Ev(D(1, "to"));
        ev.turnoverType = "out_of_bounds";

        var script = Build(ev);

        Assert.That(script, Is.Not.Null);
        Assert.That(script.ScoreSteps, Is.Empty);
        Assert.That(script.Cues.Any(c => c.Kind == Sim2DChoreographer.CueKind.Turnover
            && c.Text != null && c.Text.Contains("fuera")), Is.True);
        var lastBall = script.BallPath[script.BallPath.Count - 1];
        Assert.That(lastBall.X > 0.94f || lastBall.Y < 0.03f || lastBall.Y > 0.97f, Is.True);
    }

    [Test]
    public void Build_SidelineTurnover_EndsOnSideline()
    {
        var ev = Ev(D(1, "to"));
        ev.turnoverType = "sideline";

        var script = Build(ev);

        var lastBall = script.BallPath[script.BallPath.Count - 1];
        Assert.That(lastBall.Y < 0.03f || lastBall.Y > 0.97f, Is.True);
    }

    [Test]
    public void Build_MissWithDefensiveRebound_HasReboundCueAndNoScore()
    {
        var ev = Ev(D(1, "fga"), D(12, "dreb"));

        var script = Build(ev);

        Assert.That(script.ScoreSteps, Is.Empty);
        Assert.That(script.Cues.Any(c => c.Kind == Sim2DChoreographer.CueKind.Rebound), Is.True);
        Assert.That(script.Cues.Any(c => c.Kind == Sim2DChoreographer.CueKind.Shot), Is.True);
    }

    [Test]
    public void Build_CuesAreOrderedAndShotComesBeforeOutcome()
    {
        var ev = Ev(D(1, "fga"), D(1, "fgm"), D(1, "pts", 2));

        var script = Build(ev);

        for (int i = 1; i < script.Cues.Count; i++)
            Assert.That(script.Cues[i].Time, Is.GreaterThanOrEqualTo(script.Cues[i - 1].Time));

        int shot = script.Cues.FindIndex(c => c.Kind == Sim2DChoreographer.CueKind.Shot);
        int outcome = script.Cues.FindIndex(c => c.Kind == Sim2DChoreographer.CueKind.Outcome);
        Assert.That(shot, Is.GreaterThanOrEqualTo(0));
        Assert.That(outcome, Is.GreaterThan(shot));
    }

    [Test]
    public void Build_TipOff_StartsAtCenterCourt()
    {
        var ev = Ev(D(1, "fga"), D(1, "fgm"), D(1, "pts", 2));

        var script = Build(ev, Sim2DChoreographer.InboundKind.TipOff);

        Assert.That(script.BallPath[0].X, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(script.BallPath[0].Y, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void Build_WithoutDeltas_ReturnsNull()
    {
        var ev = new GameSimulator.PlayByPlayEvent
        {
            deltas = new List<GameSimulator.StatDelta>()
        };

        Assert.That(Build(ev), Is.Null);
    }
}
