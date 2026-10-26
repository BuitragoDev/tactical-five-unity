using NUnit.Framework;

/// <summary>
/// F5-03: distancias, descanso y desgaste de viaje. Son reglas puras: no tocan BD,
/// Unity ni el calendario; el scheduler y la UI las consumen.
/// </summary>
public class TravelHelperTests
{
    static TeamData Team(int id, string abbr) => new TeamData
    {
        id = id,
        abbreviation = abbr,
        name = abbr,
        city = abbr,
        conference = id <= 15 ? "East" : "West",
        division = "Div",
        arena = abbr + " Arena"
    };

    [Test]
    public void DistanceMiles_UsesCityCoordinates()
    {
        var boston = Team(1, "BOS");
        var newYork = Team(2, "NYK");
        var miami = Team(3, "MIA");
        var portland = Team(4, "PRT");

        Assert.That(TravelHelper.DistanceMiles(boston, newYork), Is.InRange(150, 250));
        Assert.That(TravelHelper.DistanceMiles(miami, portland), Is.GreaterThan(2_000));
        Assert.That(TravelHelper.DistanceMiles(boston, boston), Is.Zero);
    }

    [Test]
    public void RestDays_CountsFullDaysBetweenGames()
    {
        Assert.That(TravelHelper.RestDays(10, 11), Is.Zero, "back-to-back");
        Assert.That(TravelHelper.RestDays(10, 12), Is.EqualTo(1));
        Assert.That(TravelHelper.RestDays(10, 13), Is.EqualTo(2));
    }

    [Test]
    public void ImpossibleBackToBack_OnlyWhenNoRestAndLongTravel()
    {
        Assert.That(TravelHelper.IsImpossibleBackToBack(0, 1, 2_000), Is.True, "el día 0 es válido");
        Assert.That(TravelHelper.IsImpossibleBackToBack(10, 11, 1_000), Is.False);
        Assert.That(TravelHelper.IsImpossibleBackToBack(10, 11, 2_000), Is.True);
        Assert.That(TravelHelper.IsImpossibleBackToBack(10, 12, 2_000), Is.False);
        Assert.That(TravelHelper.IsImpossibleBackToBack(-1, 1, 2_000), Is.False, "sin partido previo");
    }

    [Test]
    public void FatigueCost_GrowsWithDistanceAndShrinksWithRest()
    {
        Assert.That(TravelHelper.FatigueCost(100, 0), Is.Zero, "tramo corto sin coste");
        Assert.That(TravelHelper.FatigueCost(400, 0), Is.EqualTo(1));
        Assert.That(TravelHelper.FatigueCost(900, 0), Is.EqualTo(2));
        Assert.That(TravelHelper.FatigueCost(2_000, 0), Is.EqualTo(3));
        Assert.That(TravelHelper.FatigueCost(2_000, 1), Is.EqualTo(2));
        Assert.That(TravelHelper.FatigueCost(2_000, 2), Is.EqualTo(1));
        Assert.That(TravelHelper.FatigueCost(2_000, 3), Is.Zero);
        Assert.That(TravelHelper.FatigueCost(900, 2), Is.Zero);
    }

    [Test]
    public void GameTravelMiles_IsZeroAtHome()
    {
        var teams = new System.Collections.Generic.Dictionary<int, TeamData>
        {
            [1] = Team(1, "BOS"),
            [2] = Team(2, "NYK")
        };

        Assert.That(TravelHelper.GameTravelMiles(2, 2, teams), Is.Zero);
        Assert.That(TravelHelper.GameTravelMiles(2, 1, teams), Is.GreaterThan(0));
    }

    [Test]
    public void Describe_IncludesDistanceAndRest()
    {
        Assert.That(TravelHelper.Describe(1_200, 2), Does.Contain("mi").And.Contain("2 días"));
        Assert.That(TravelHelper.Describe(0, 1), Does.Contain("1 día"));
    }
}
