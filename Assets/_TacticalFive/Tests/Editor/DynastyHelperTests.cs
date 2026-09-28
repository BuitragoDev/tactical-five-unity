using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Dinastías y prestigio de franquicia (derivados de `season_records`). Reglas puras.
/// </summary>
public class DynastyHelperTests
{
    static SeasonRecord Record(int seasonId, int champion, int finalist, int eastChamp = 0, int westChamp = 0)
        => new SeasonRecord
        {
            season_id = seasonId,
            champion_id = champion,
            finalist_id = finalist,
            east_champion_id = eastChamp,
            west_champion_id = westChamp
        };

    [Test]
    public void Prestige_WeightsTitlesFinalsAndConference()
    {
        Assert.That(DynastyHelper.Prestige(3, 5, 7), Is.EqualTo(3 * 3 + 5 * 2 + 7));
        Assert.That(DynastyHelper.Prestige(0, 0, 0), Is.EqualTo(0));
    }

    [Test]
    public void IsDynasty_TrueForThreeTitlesInWindow()
    {
        Assert.That(DynastyHelper.IsDynasty(new[] { 10, 12, 14 }), Is.True);
        Assert.That(DynastyHelper.IsDynasty(new[] { 10, 16, 17 }), Is.True, "de 10 a 17 son 8 temporadas (ventana completa)");
        Assert.That(DynastyHelper.IsDynasty(new[] { 10, 11 }), Is.False, "menos de 3 títulos");
        Assert.That(DynastyHelper.IsDynasty(new[] { 10, 17, 18 }), Is.False, "de 10 a 18 van 9 temporadas: fuera de la ventana");
    }

    [Test]
    public void IsDynasty_FalseWhenTitlesAreSpreadOut()
    {
        Assert.That(DynastyHelper.IsDynasty(new[] { 10, 25, 40 }), Is.False);
        Assert.That(DynastyHelper.IsDynasty(null), Is.False);
        Assert.That(DynastyHelper.IsDynasty(new[] { 10, 10, 11, 12 }), Is.True, "ids repetidos cuentan una vez: 10, 11 y 12");
    }

    [Test]
    public void Of_CountsTitlesFinalsAndConferenceTitles()
    {
        var records = new List<SeasonRecord>
        {
            Record(1, champion: 1, finalist: 2, eastChamp: 1),
            Record(2, champion: 1, finalist: 3, eastChamp: 1),
            Record(3, champion: 1, finalist: 4, eastChamp: 1),
            Record(10, champion: 1, finalist: 5, eastChamp: 1)
        };

        var p = DynastyHelper.Of(records, 1);

        Assert.That(p.titles, Is.EqualTo(4));
        Assert.That(p.finals, Is.EqualTo(4), "campeón o finalista en las cuatro");
        Assert.That(p.conferenceTitles, Is.EqualTo(4));
        Assert.That(p.firstTitleSeasonId, Is.EqualTo(1));
        Assert.That(p.lastTitleSeasonId, Is.EqualTo(10));
        Assert.That(p.prestige, Is.EqualTo(DynastyHelper.Prestige(4, 4, 4)));
        Assert.That(p.isDynasty, Is.True, "3 títulos seguidos (1, 2, 3) dentro de la ventana");
    }

    [Test]
    public void LeagueRanking_OrdersByPrestige()
    {
        var records = new List<SeasonRecord>
        {
            Record(1, champion: 1, finalist: 2, eastChamp: 1, westChamp: 2),
            Record(2, champion: 1, finalist: 3, eastChamp: 1),
            Record(3, champion: 2, finalist: 1, eastChamp: 2, westChamp: 1)
        };

        var ranking = DynastyHelper.LeagueRanking(records);

        Assert.That(ranking[0].teamId, Is.EqualTo(1));
        Assert.That(ranking[0].titles, Is.EqualTo(2));
        Assert.That(ranking.Select(r => r.teamId), Does.Contain(2));
        Assert.That(ranking.Select(r => r.teamId), Does.Contain(3));
        Assert.That(DynastyHelper.LeagueRanking(records, max: 1), Has.Count.EqualTo(1));
    }
}
