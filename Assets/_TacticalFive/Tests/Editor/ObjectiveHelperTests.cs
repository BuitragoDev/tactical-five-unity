using System.Collections.Generic;
using NUnit.Framework;

public class ObjectiveHelperTests
{
    [Test]
    public void IsObjectiveMet_RankZero_NeverMet()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Zona tranquila", 0), Is.False);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Campeonato", 0), Is.False);
        Assert.That(ObjectiveHelper.IsObjectiveMet(null, 0), Is.False);
    }

    [Test]
    public void IsObjectiveMet_ZonaTranquila_Threshold12()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Zona tranquila", 12), Is.True);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Zona tranquila", 13), Is.False);
    }

    [Test]
    public void IsObjectiveMet_PlayIn_Threshold10()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Play-In", 10), Is.True);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Play-In", 11), Is.False);
    }

    [Test]
    public void IsObjectiveMet_Playoffs_Threshold6()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Playoffs", 6), Is.True);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Playoffs", 7), Is.False);
    }

    [Test]
    public void IsObjectiveMet_Campeonato_Threshold2()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Campeonato", 1), Is.True);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Campeonato", 2), Is.True);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Campeonato", 3), Is.False);
    }

    [Test]
    public void IsObjectiveMet_UnknownObjective_NeverMet()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Inexistente", 1), Is.False);
    }

    // Un East con 2 equipos; B ganó 1:0.
    static List<TeamData> TwoEastTeams()
    {
        return new List<TeamData>
        {
            new TeamData { id = 1, conference = "East", name = "EastA" },
            new TeamData { id = 2, conference = "East", name = "EastB" },
            new TeamData { id = 3, conference = "West", name = "WestA" }
        };
    }

    static GameData Regular(int home, int away, int hs, int as_)
    {
        return new GameData
        {
            home_team_id = home,
            away_team_id = away,
            home_score = hs,
            away_score = as_,
            is_played = 1,
            game_type = "regular"
        };
    }

    [Test]
    public void GetConferenceRank_TopTeam_IsRank1()
    {
        var teams = TwoEastTeams();
        var games = new List<GameData>
        {
            Regular(1, 2, 110, 100)
        };

        Assert.That(ObjectiveHelper.GetConferenceRank(1, "East", teams, games), Is.EqualTo(1));
        Assert.That(ObjectiveHelper.GetConferenceRank(2, "East", teams, games), Is.EqualTo(2));
    }

    [Test]
    public void GetConferenceRank_WestTeam_MatchesWestRank()
    {
        var teams = TwoEastTeams();
        var games = new List<GameData>
        {
            Regular(1, 2, 110, 100),
            Regular(3, 1, 120, 90)
        };
        // El equipo 3 es el único del Oeste con partido jugado -> rank 1 en su conferencia.
        Assert.That(ObjectiveHelper.GetConferenceRank(3, "West", teams, games), Is.EqualTo(1));
    }

    [Test]
    public void GetConferenceRank_TeamNotInConference_ReturnsZero()
    {
        var teams = TwoEastTeams();
        var games = new List<GameData>();
        Assert.That(ObjectiveHelper.GetConferenceRank(999, "East", teams, games), Is.EqualTo(0));
    }

    [Test]
    public void GetConferenceRank_IgnoresNonRegularAndUnplayed()
    {
        var teams = TwoEastTeams();
        var games = new List<GameData>
        {
            Regular(1, 2, 110, 100),
            new GameData { home_team_id = 2, away_team_id = 1, home_score = 99, away_score = 98, is_played = 1, game_type = "playoff" },
            new GameData { home_team_id = 2, away_team_id = 1, home_score = 99, away_score = 98, is_played = 0, game_type = "regular" }
        };
        // Solo cuenta el partido regular jugado (EastA ganó) -> EastA rank 1.
        Assert.That(ObjectiveHelper.GetConferenceRank(1, "East", teams, games), Is.EqualTo(1));
        Assert.That(ObjectiveHelper.GetConferenceRank(2, "East", teams, games), Is.EqualTo(2));
    }
}