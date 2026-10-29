using System.Linq;
using NUnit.Framework;

/// <summary>
/// Catálogo puro de la capa de ciudad (F7-05). Sin base de datos ni azar.
/// </summary>
public class CityCatalogTests
{
    [Test]
    public void Projects_HaveUniqueKeysAndValidConfiguration()
    {
        Assert.That(CityCatalog.Projects, Is.Not.Empty);
        Assert.That(CityCatalog.Projects.Select(p => p.key).Distinct().Count(),
            Is.EqualTo(CityCatalog.Projects.Length));

        foreach (var project in CityCatalog.Projects)
        {
            Assert.That(project.key, Is.Not.Empty);
            Assert.That(project.name, Is.Not.Empty);
            Assert.That(project.description, Is.Not.Empty);
            Assert.That(project.maxLevel, Is.GreaterThan(0), project.key);
            Assert.That(project.days, Is.GreaterThan(0), project.key);
            Assert.That(project.baseCost, Is.GreaterThan(0), project.key);
            Assert.That(project.costStep, Is.GreaterThanOrEqualTo(0), project.key);
        }
    }

    [Test]
    public void ProjectCatalog_ContainsTheFourCrossCuttingProjects()
    {
        Assert.That(CityCatalog.Project(CityCatalog.ProjectArena), Is.Not.Null);
        Assert.That(CityCatalog.Project(CityCatalog.ProjectPerformance), Is.Not.Null);
        Assert.That(CityCatalog.Project(CityCatalog.ProjectFan), Is.Not.Null);
        Assert.That(CityCatalog.Project(CityCatalog.ProjectMedia), Is.Not.Null);
    }

    [Test]
    public void ProjectCost_GrowsPerLevelAndStartsAtBaseCost()
    {
        var arena = CityCatalog.Project(CityCatalog.ProjectArena);
        Assert.That(CityCatalog.ProjectCost(arena, 1), Is.EqualTo(arena.baseCost));
        Assert.That(CityCatalog.ProjectCost(arena, 2),
            Is.GreaterThan(CityCatalog.ProjectCost(arena, 1)));
        Assert.That(CityCatalog.ProjectCost(null, 1), Is.EqualTo(0));
    }

    [Test]
    public void ImpactHelpers_AreClampedToProjectMaximums()
    {
        Assert.That(CityCatalog.AttendanceMultiplier(0), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(CityCatalog.AttendanceMultiplier(1), Is.EqualTo(1.04f).Within(0.0001f));
        Assert.That(CityCatalog.AttendanceMultiplier(2), Is.EqualTo(1.08f).Within(0.0001f));
        Assert.That(CityCatalog.AttendanceMultiplier(99), Is.EqualTo(1.08f).Within(0.0001f));

        Assert.That(CityCatalog.RecoveryBonus(0), Is.EqualTo(0));
        Assert.That(CityCatalog.RecoveryBonus(1), Is.EqualTo(2));
        Assert.That(CityCatalog.RecoveryBonus(2), Is.EqualTo(4));
        Assert.That(CityCatalog.RecoveryBonus(99), Is.EqualTo(4));
    }

    [Test]
    public void Events_HaveUniqueKeysAndTwoValidChoices()
    {
        Assert.That(CityCatalog.Events, Is.Not.Empty);
        Assert.That(CityCatalog.Events.Select(e => e.key).Distinct().Count(),
            Is.EqualTo(CityCatalog.Events.Length));

        foreach (var cityEvent in CityCatalog.Events)
        {
            Assert.That(cityEvent.key, Is.Not.Empty);
            Assert.That(cityEvent.title, Is.Not.Empty);
            Assert.That(cityEvent.description, Is.Not.Empty);
            Assert.That(cityEvent.choices, Has.Length.EqualTo(2), cityEvent.key);

            foreach (var choice in cityEvent.choices)
            {
                Assert.That(choice.label, Is.Not.Empty, cityEvent.key);
                Assert.That(choice.cost, Is.GreaterThanOrEqualTo(0), cityEvent.key);
                Assert.That(choice.fanConfidence != 0 || choice.morale != 0 || choice.fisico != 0,
                    Is.True, $"{cityEvent.key} debe tener algún efecto");
            }
        }
    }

    [Test]
    public void MoneyFormat_UsesDotThousandsAndTrailingDollar()
    {
        Assert.That(CityCatalog.FormatMoney(3_000_000), Is.EqualTo("3.000.000 $"));
        Assert.That(CityCatalog.FormatMoney(500_000), Is.EqualTo("500.000 $"));
    }

    [Test]
    public void EventEffectSummary_ShowsCostAndAtLeastOneEffect()
    {
        var choice = CityCatalog.Event("queja_vecinal").choices[0];
        string summary = CityCatalog.EventEffectSummary(choice);

        Assert.That(summary, Does.Contain("Coste: 500.000 $"));
        Assert.That(summary, Does.Contain("Afición"));
    }
}
