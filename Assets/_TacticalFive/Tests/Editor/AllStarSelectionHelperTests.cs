using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Selección del All-Star (E5): puntuación, 12 por conferencia (5+7) y campo de concursos.
/// </summary>
public class AllStarSelectionHelperTests
{
    static PlayerData P(int id, string pos, int overall) => new PlayerData
    {
        id = id, position = pos, overall = overall, team_id = 1,
        first_name = "Jug", last_name = id.ToString()
    };

    static List<PlayerData> StandardPool()
    {
        var list = new List<PlayerData>();
        int id = 1;
        foreach (var pos in PositionCodes.Order)
            for (int i = 0; i < 4; i++)   // 4 por posición = 20 candidatos
                list.Add(P(id++, pos, 90 - i * 3));
        return list;
    }

    [Test]
    public void AllStarScore_WeighsRatingAndOverall()
    {
        float great = AllStarSelectionHelper.AllStarScore(25, 60, 95);
        float weak = AllStarSelectionHelper.AllStarScore(5, 60, 70);
        Assert.That(great, Is.GreaterThan(weak));
    }

    [Test]
    public void AllStarScore_PenalizesTinySamples()
    {
        float many = AllStarSelectionHelper.AllStarScore(20, 60, 85);
        float few = AllStarSelectionHelper.AllStarScore(20, 2, 85);
        Assert.That(many, Is.GreaterThan(few), "pocos partidos no deben premiar igual");
    }

    [Test]
    public void Select_ReturnsTwelveWithFiveStarters()
    {
        var ranked = AllStarSelectionHelper.Rank(StandardPool(), p => (20, 60));
        var roster = AllStarSelectionHelper.Select(ranked);

        Assert.That(roster.Count, Is.EqualTo(AllStarSelectionHelper.ROSTER_SIZE));

        // Los 5 primeros son los titulares (uno por posición, los mejores).
        var starters = roster.Take(AllStarSelectionHelper.STARTERS).ToList();
        Assert.That(starters.Select(s => s.position).Distinct().Count(), Is.EqualTo(5),
            "un titular por posición");
    }

    [Test]
    public void Select_NoDuplicates()
    {
        var ranked = AllStarSelectionHelper.Rank(StandardPool(), p => (20, 60));
        var roster = AllStarSelectionHelper.Select(ranked);
        Assert.That(roster.Select(r => r.player_id).Distinct().Count(), Is.EqualTo(roster.Count));
    }

    [Test]
    public void Select_TwoPerPosition()
    {
        var ranked = AllStarSelectionHelper.Rank(StandardPool(), p => (20, 60));
        var roster = AllStarSelectionHelper.Select(ranked);

        foreach (var pos in PositionCodes.Order)
            Assert.That(roster.Count(r => r.position == pos), Is.EqualTo(2), $"pos {pos}");
    }

    [Test]
    public void Select_HandlesSmallPool()
    {
        var pool = new List<PlayerData> { P(1, "PG", 80), P(2, "SG", 79), P(3, "SF", 78) };
        var roster = AllStarSelectionHelper.Select(AllStarSelectionHelper.Rank(pool, p => (10, 30)));
        Assert.That(roster.Count, Is.EqualTo(3), "con menos de 12, devuelve los que hay");
    }

    [Test]
    public void Rank_IgnoresFreeAgents()
    {
        var pool = StandardPool();
        pool.Add(new PlayerData { id = 999, position = "PG", overall = 99, team_id = 0, first_name = "FA", last_name = "X" });
        var ranked = AllStarSelectionHelper.Rank(pool, p => (30, 60));
        Assert.That(ranked.Any(r => r.player_id == 999), Is.False, "los agentes libres no son All-Stars");
    }

    [Test]
    public void ContestField_PicksTopByAttribute()
    {
        var rank = new List<AllStarCandidate>();
        for (int i = 0; i < 12; i++)
            rank.Add(new AllStarCandidate { player_id = i, overall = i * 5 });

        var field = AllStarSelectionHelper.ContestField(rank, c => c.overall, 8);
        Assert.That(field.Count, Is.EqualTo(8));
        Assert.That(field[0].player_id, Is.EqualTo(11), "el mejor atributo encabeza");
    }
}
