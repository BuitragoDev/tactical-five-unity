using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Concursos de mates y triples (E5). Simulación con azar controlado (System.Random con
/// semilla fija) y comprobación de rangos y estructura.
/// </summary>
public class AllStarContestTests
{
    // ── MATES ───────────────────────────────────────────

    [Test]
    public void Dunk_Skill_ScalesWithAttributes()
    {
        float low = SlamDunkContest.Skill(50, 50, 190);
        float high = SlamDunkContest.Skill(95, 95, 210);
        Assert.That(high, Is.GreaterThan(low));
        Assert.That(low, Is.InRange(0.15f, 1f));
        Assert.That(high, Is.InRange(0.15f, 1f));
    }

    [Test]
    public void Dunk_Attempt_MadeScoresWithinFifty()
    {
        var (made, pts) = SlamDunkContest.Attempt(difficulty: 1, baseScore: 30, skill: 1f,
                                                  roll: 0.0, judgeRoll: 1.0);
        Assert.That(made, Is.True);
        Assert.That(pts, Is.InRange(0, SlamDunkContest.MAX_SCORE));
    }

    [Test]
    public void Dunk_Attempt_MissScoresAtMostHalf()
    {
        var (made, pts) = SlamDunkContest.Attempt(difficulty: 5, baseScore: 48, skill: 0.15f,
                                                  roll: 0.99, judgeRoll: 1.0);
        Assert.That(made, Is.False);
        Assert.That(pts, Is.InRange(0, 24));   // 48 * 1.0 * 0.5
    }

    [Test]
    public void Dunk_HighSkillScoresMoreOverManyRolls()
    {
        int ProScore(float skill)
        {
            var rng = new Random(1234);
            var field = new List<(int, string, string, float)> { (1, "A", "x", skill) };
            return SlamDunkContest.Run(field, rng).First().bestScore;
        }
        Assert.That(ProScore(0.95f), Is.GreaterThanOrEqualTo(ProScore(0.2f)));
    }

    [Test]
    public void Dunk_Run_RanksAllParticipants()
    {
        var rng = new Random(7);
        var field = Enumerable.Range(1, 8).Select(i => (i, $"Jug{i}", "logo", 0.4f + i * 0.05f));
        var results = SlamDunkContest.Run(field, rng);

        Assert.That(results.Count, Is.EqualTo(8));
        Assert.That(results.Select(r => r.rank).OrderBy(x => x), Is.EqualTo(Enumerable.Range(1, 8)));
        Assert.That(results[0].bestScore, Is.GreaterThanOrEqualTo(results.Last().bestScore));
        Assert.That(results.All(r => r.bestScore >= 0), Is.True);
    }

    // ── TRIPLES ─────────────────────────────────────────

    [Test]
    public void Three_ShotMade_HalfCourtIsHarder()
    {
        int normal = 0, half = 0;
        var rng = new Random(99);
        for (int i = 0; i < 2000; i++)
        {
            if (ThreePointContest.ShotMade(85, false, rng.NextDouble())) normal++;
            if (ThreePointContest.ShotMade(85, true, rng.NextDouble())) half++;
        }
        Assert.That(normal, Is.GreaterThan(half));
    }

    [Test]
    public void Three_RunRound_WithinMax()
    {
        var rng = new Random(3);
        for (int i = 0; i < 50; i++)
        {
            int pts = ThreePointContest.RunRound(90, rng);
            Assert.That(pts, Is.InRange(0, ThreePointContest.MAX_SCORE));
        }
    }

    [Test]
    public void Three_HighShooterScoresMore()
    {
        int Score(int three)
        {
            var rng = new Random(42);
            long total = 0;
            for (int i = 0; i < 40; i++) total += ThreePointContest.RunRound(three, rng);
            return (int)(total / 40);
        }
        Assert.That(Score(95), Is.GreaterThan(Score(40)));
    }

    [Test]
    public void Three_Run_HasChampionAndRanks()
    {
        var rng = new Random(11);
        var field = Enumerable.Range(1, 8).Select(i => (i, $"Jug{i}", "logo", 60 + i * 4));
        var results = ThreePointContest.Run(field, rng);

        Assert.That(results.Count, Is.EqualTo(8));
        Assert.That(results.Count(r => r.champion), Is.EqualTo(1));
        Assert.That(results[0].champion, Is.True, "el champion es el primero del ranking");
        Assert.That(results.Select(r => r.rank).OrderBy(x => x), Is.EqualTo(Enumerable.Range(1, 8)));
        // Todos juegan la clasificatoria; solo los 3 finalistas añaden la ronda final.
        Assert.That(results.All(r => r.roundScores.Count >= 1), Is.True, "todos puntúan la clasificatoria");
        Assert.That(results.Count(r => r.roundScores.Count == 2), Is.EqualTo(3), "3 finalistas");
    }
}
