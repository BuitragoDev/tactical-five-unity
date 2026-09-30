using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Catálogo de lesiones (E2): integridad de los datos y sorteo ponderado puro.
/// </summary>
public class InjuryCatalogTests
{
    [Test]
    public void Catalog_HasValidDefs()
    {
        Assert.That(InjuryCatalog.All, Is.Not.Empty);
        var names = new HashSet<string>();
        foreach (var d in InjuryCatalog.All)
        {
            Assert.That(names.Add(d.name), Is.True, $"lesión duplicada: {d.name}");
            Assert.That(d.weight, Is.GreaterThan(0), d.name);
            Assert.That(d.maxDays, Is.GreaterThanOrEqualTo(d.minDays), d.name);
            Assert.That(d.minDays, Is.GreaterThan(0), d.name);
            Assert.That(d.relapseRisk, Is.InRange(0, 100), d.name);
            Assert.That(string.IsNullOrEmpty(d.family), Is.False, d.name);
        }
        Assert.That(InjuryCatalog.TotalWeight, Is.EqualTo(InjuryCatalog.All.Sum(d => d.weight)));
    }

    [Test]
    public void Catalog_CoversAllSeverities()
    {
        foreach (InjurySeverity sev in System.Enum.GetValues(typeof(InjurySeverity)))
            Assert.That(InjuryCatalog.All.Any(d => d.severity == sev), Is.True, $"sin lesiones {sev}");
    }

    [Test]
    public void Pick_StaysInRangeAndRespectsBoundaries()
    {
        Assert.That(InjuryCatalog.Pick(0.0), Is.SameAs(InjuryCatalog.All[0]));
        Assert.That(InjuryCatalog.Pick(0.999999), Is.SameAs(InjuryCatalog.All.Last()));

        InjuryDef last = null;
        for (int i = 0; i <= 100; i++)
        {
            var picked = InjuryCatalog.Pick(i / 101.0);
            Assert.That(picked, Is.Not.Null, $"roll {i}");
            if (last != null)
                Assert.That(InjuryCatalog.All.IndexOf(picked),
                            Is.GreaterThanOrEqualTo(InjuryCatalog.All.IndexOf(last)), "el orden debe ser monótono");
            last = picked;
        }
    }

    [Test]
    public void Pick_FollowsWeights()
    {
        // El tramo [0, peso0/total) debe devolver siempre la primera lesión.
        double boundary = (InjuryCatalog.All[0].weight - 0.5) / InjuryCatalog.TotalWeight;
        Assert.That(InjuryCatalog.Pick(boundary), Is.SameAs(InjuryCatalog.All[0]));
    }

    [Test]
    public void ByName_FindsAndMisses()
    {
        var first = InjuryCatalog.All[0];
        Assert.That(InjuryCatalog.ByName(first.name), Is.SameAs(first));
        Assert.That(InjuryCatalog.ByName("Lesión inventada"), Is.Null);
        Assert.That(InjuryCatalog.ByName(""), Is.Null);
        Assert.That(InjuryCatalog.ByName(null), Is.Null);
    }

    [Test]
    public void SeverityOf_Thresholds()
    {
        Assert.That(InjuryCatalog.SeverityOf(1), Is.EqualTo(InjurySeverity.Leve));
        Assert.That(InjuryCatalog.SeverityOf(7), Is.EqualTo(InjurySeverity.Leve));
        Assert.That(InjuryCatalog.SeverityOf(8), Is.EqualTo(InjurySeverity.Moderada));
        Assert.That(InjuryCatalog.SeverityOf(21), Is.EqualTo(InjurySeverity.Moderada));
        Assert.That(InjuryCatalog.SeverityOf(22), Is.EqualTo(InjurySeverity.Grave));
        Assert.That(InjuryCatalog.SeverityOf(70), Is.EqualTo(InjurySeverity.Grave));
        Assert.That(InjuryCatalog.SeverityOf(71), Is.EqualTo(InjurySeverity.Critica));
    }

    [Test]
    public void SeverityLabel_AllValues()
    {
        Assert.That(InjuryCatalog.SeverityLabel(InjurySeverity.Leve), Is.EqualTo("LEVE"));
        Assert.That(InjuryCatalog.SeverityLabel(InjurySeverity.Critica), Is.EqualTo("CRÍTICA"));
    }

    [Test]
    public void Pronostico_FormatsRange()
    {
        var d = new InjuryDef { name = "X", family = "f", minDays = 3, maxDays = 7, weight = 1 };
        Assert.That(InjuryCatalog.Pronostico(d), Is.EqualTo("3-7 días"));
        Assert.That(InjuryCatalog.Pronostico(null), Is.EqualTo("—"));
    }

    [Test]
    public void WorsenCandidates_PrefersSameFamily()
    {
        var leve = InjuryCatalog.ByName("Sobrecarga muscular");   // muscular, Leve
        var candidates = InjuryCatalog.WorsenCandidates(leve);

        Assert.That(candidates, Is.Not.Empty);
        Assert.That(candidates.All(c => c.severity > leve.severity), Is.True);
        Assert.That(candidates.All(c => c.family == "muscular"), Is.True, "misma familia si hay peores");
    }

    [Test]
    public void WorsenCandidates_FallsBackToGlobalWhenFamilyMaxed()
    {
        // El LCA ya es lo más grave de su familia y del catálogo: no hay nada peor.
        var lca = InjuryCatalog.ByName("Rotura del ligamento cruzado anterior");
        Assert.That(InjuryCatalog.WorsenCandidates(lca), Is.Empty);
    }
}
