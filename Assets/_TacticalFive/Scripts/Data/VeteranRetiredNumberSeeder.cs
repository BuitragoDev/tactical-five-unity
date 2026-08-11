using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Dorsales que merecen estar retirados en franquicias históricas de
/// veteranos activos que ya no juegan en esos equipos.
/// Se combina con RetiredNumberSeeder (leyendas HOF fallecidas/retiradas).
/// </summary>
public static class VeteranRetiredNumberSeeder
{
    public static List<(string FirstName, string LastName, string Abbreviation, int Number)> Data => new()
    {
        ("LeBron", "James", "CLE", 23),
        ("LeBron", "James", "MIA", 6),
        ("LeBron", "James", "LAL", 23),
        ("Kevin", "Durant", "OKC", 35),
        ("Kevin", "Durant", "GSW", 35),
        ("Giannis", "Antetokounmpo", "MIL", 34),
        ("Klay", "Thompson", "GSW", 11),
        ("Kyrie", "Irving", "CLE", 2),
        ("James", "Harden", "HOU", 13),
        ("Kawhi", "Leonard", "SAS", 2),
        ("Pascal", "Siakam", "TOR", 43),
        ("Al", "Horford", "BOS", 42),
        ("Anthony", "Davis", "LAL", 3),
        ("Jrue", "Holiday", "MIL", 21),
        ("Jrue", "Holiday", "BOS", 4),
        ("Kyle", "Lowry", "TOR", 7),
        ("Russell", "Westbrook", "OKC", 0),
    };
}