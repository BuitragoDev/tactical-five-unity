using System.Linq;
using NUnit.Framework;

/// <summary>
/// F5-05: camp post-draft. Reglas puras de elegibilidad, simulación acotada y
/// desarrollo de como máximo +1 atributo.
/// </summary>
public class SummerLeagueHelperTests
{
    static PlayerData Player(int id, int age = 21, int potential = 90)
    {
        var p = new PlayerData
        {
            id = id,
            first_name = "Rookie",
            last_name = id.ToString(),
            position = "SG",
            age = age,
            potential = potential,
            speed = 50,
            shooting = 50,
            three_point = 50,
            passing = 50,
            dribbling = 50,
            defense = 50,
            rebounding = 50,
            athleticism = 50,
            iq = 50,
            steals = 50,
            blocks = 50,
            contract_years = 4,
            guaranteed_years = 4,
            morale = 50,
            fisico = 99
        };
        p.RecalculateOverall();
        return p;
    }

    [Test]
    public void Eligibility_IncludesRookiesAndYoungSophomores()
    {
        var rookie = Player(1);
        rookie.is_rookie = 1;
        var sophomore = Player(2, age: 22);
        sophomore.is_rookie = 0;
        sophomore.years_of_service = 2;
        var veteran = Player(3, age: 27);
        veteran.years_of_service = 5;

        Assert.That(SummerLeagueHelper.IsEligible(rookie), Is.True);
        Assert.That(SummerLeagueHelper.IsEligible(sophomore), Is.True);
        Assert.That(SummerLeagueHelper.IsEligible(veteran), Is.False);

        rookie.injury_days = 3;
        Assert.That(SummerLeagueHelper.IsEligible(rookie), Is.False);
    }

    [Test]
    public void DefaultSelection_IsCappedAtTwelve()
    {
        var players = Enumerable.Range(1, 20).Select(i =>
        {
            var p = Player(i);
            p.is_rookie = 1;
            return p;
        }).ToList();

        Assert.That(SummerLeagueHelper.DefaultSelection(players), Has.Count.EqualTo(12));
    }

    [Test]
    public void Simulate_IsDeterministicAndProducesThreeGames()
    {
        var selected = new[] { Player(1), Player(2) };
        var first = SummerLeagueHelper.Simulate(selected, 1234);
        var second = SummerLeagueHelper.Simulate(selected, 1234);

        Assert.That(first, Has.Count.EqualTo(2));
        Assert.That(first.All(r => r.games == 3 && r.minutes > 0), Is.True);
        Assert.That(first.Select(r => (r.points, r.rebounds, r.assists, r.minutes)),
            Is.EqualTo(second.Select(r => (r.points, r.rebounds, r.assists, r.minutes))));
    }

    [Test]
    public void ApplyDevelopment_OnlyRaisesTheLowestAttributeByOne()
    {
        var p = Player(1, potential: 99);
        p.blocks = 20;
        p.RecalculateOverall();

        Assert.That(SummerLeagueHelper.ApplyDevelopment(p, 1), Is.True);
        Assert.That(p.blocks, Is.EqualTo(21));
    }

    [Test]
    public void ApplyDevelopment_StopsAtPotential()
    {
        var p = Player(1, potential: 90);
        p.potential = p.overall; // ya en su techo
        Assert.That(SummerLeagueHelper.ApplyDevelopment(p, 1), Is.False);
    }
}
