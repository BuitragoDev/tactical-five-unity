using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Hitos de legado (E1): umbrales, hitos alcanzados nuevos y progreso hacia el siguiente.
/// </summary>
public class MilestoneHelperTests
{
    static CareerTotals Career(int points = 0, int rebounds = 0, int assists = 0, int steals = 0,
                               int blocks = 0, int threes = 0, int games = 0, int dd = 0, int td = 0,
                               int allStars = 0, int mvps = 0, int rings = 0, int fmvps = 0)
        => new CareerTotals
        {
            points = points, rebounds = rebounds, assists = assists, steals = steals,
            blocks = blocks, threePointers = threes, games = games, doubleDoubles = dd,
            tripleDoubles = td, allStars = allStars, seasonMvps = mvps, rings = rings, finalsMvps = fmvps
        };

    [Test]
    public void Reached_ExactThresholdCounts()
    {
        var reached = MilestoneHelper.Reached(Career(points: 10000));
        Assert.That(reached.Exists(d => d.key == "points_10000"), Is.True, "10.000 exactos es hito");

        var below = MilestoneHelper.Reached(Career(points: 9999));
        Assert.That(below.Exists(d => d.key == "points_10000"), Is.False);
    }

    [Test]
    public void Reached_SkipsAlreadyRegistered()
    {
        var career = Career(points: 21000, rebounds: 5000);
        var already = new List<string> { "points_10000", "points_15000", "points_20000" };

        var reached = MilestoneHelper.Reached(career, already);

        Assert.That(reached.Exists(d => d.key == "points_10000"), Is.False, "ya registrado");
        Assert.That(reached.Exists(d => d.key == "points_20000"), Is.False, "ya registrado");
        Assert.That(reached.Exists(d => d.key == "rebounds_5000"), Is.True, "ese sí es nuevo");
    }

    [Test]
    public void Reached_CanReturnSeveralAtOnce()
    {
        var reached = MilestoneHelper.Reached(Career(allStars: 11, rings: 3, mvps: 3));

        Assert.That(reached.Exists(d => d.key == "allstar_3"), Is.True);
        Assert.That(reached.Exists(d => d.key == "allstar_5"), Is.True);
        Assert.That(reached.Exists(d => d.key == "allstar_10"), Is.True);
        Assert.That(reached.Exists(d => d.key == "rings_3"), Is.True);
        Assert.That(reached.Exists(d => d.key == "mvp_3"), Is.True);
    }

    [Test]
    public void Reached_EmptyCareer_ReturnsNothing()
    {
        Assert.That(MilestoneHelper.Reached(new CareerTotals()), Is.Empty);
    }

    [Test]
    public void Next_ReturnsThresholdAndProgress()
    {
        var next = MilestoneHelper.Next(Career(points: 18420), MilestoneFamily.Points);

        Assert.That(next.next, Is.Not.Null);
        Assert.That(next.next.key, Is.EqualTo("points_20000"));
        Assert.That(next.current, Is.EqualTo(18420));
        Assert.That(next.percent, Is.EqualTo(92), "18420/20000 = 92 %");
    }

    [Test]
    public void Next_NullWhenFamilyCompleted()
    {
        var next = MilestoneHelper.Next(Career(fmvps: 2), MilestoneFamily.FinalsMvp);
        Assert.That(next.next, Is.Null);
        Assert.That(next.percent, Is.EqualTo(0));
    }

    [Test]
    public void NextOverall_PicksTheClosestMilestone()
    {
        // Puntos al 92 % vs rebotes al 20 % → gana puntos.
        var next = MilestoneHelper.NextOverall(Career(points: 18420, rebounds: 1000));

        Assert.That(next, Is.Not.Null);
        Assert.That(next.Value.next.key, Is.EqualTo("points_20000"));
    }

    [Test]
    public void Catalog_KeysAreUniqueAndThresholdsAscend()
    {
        var keys = new HashSet<string>();
        foreach (var d in MilestoneCatalog.All)
        {
            Assert.That(keys.Add(d.key), Is.True, $"clave duplicada: {d.key}");
            Assert.That(MilestoneCatalog.Label(d), Is.Not.Empty);
            Assert.That(MilestoneCatalog.Icon(d), Does.StartWith("Icons/"));
        }

        foreach (MilestoneFamily f in System.Enum.GetValues(typeof(MilestoneFamily)))
        {
            var family = MilestoneCatalog.OfFamily(f);
            Assert.That(family, Is.Not.Empty, $"la familia {f} no tiene hitos");
            for (int i = 1; i < family.Count; i++)
                Assert.That(family[i].threshold, Is.GreaterThan(family[i - 1].threshold), $"{f} desordenada");
        }
    }

    [Test]
    public void LegendCareer_HasAllMilestones()
    {
        var legend = Career(points: 40000, rebounds: 12000, assists: 10000, steals: 2200, blocks: 1200,
                            threes: 2400, games: 1500, dd: 600, td: 100, allStars: 20, mvps: 4,
                            rings: 4, fmvps: 4);

        var reached = MilestoneHelper.Reached(legend);

        Assert.That(reached.Count, Is.EqualTo(MilestoneCatalog.All.Count), "una leyenda completa los tiene todos");
        Assert.That(MilestoneHelper.NextOverall(legend), Is.Null);
    }
}
