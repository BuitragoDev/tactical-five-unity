using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// F5-04: grupos y clasificación de la NBA Cup. Reglas puras; la eliminatoria
/// con BD se prueba en la suite de integración.
/// </summary>
public class CupHelperTests
{
    static TeamData Team(int id, string conference = "East", string division = "Atlántico")
        => new TeamData
        {
            id = id,
            abbreviation = "T" + id,
            name = "Team " + id,
            city = "City " + id,
            conference = conference,
            division = division,
            arena = "Arena " + id
        };

    static GameData Game(int home, int away, int day, int homeScore = 0, int awayScore = 0,
        string label = "", string type = "regular")
        => new GameData
        {
            home_team_id = home,
            away_team_id = away,
            game_day = day,
            game_date = "2026-11-01",
            game_type = type,
            series_label = label,
            is_played = homeScore > 0 || awayScore > 0 ? 1 : 0,
            home_score = homeScore,
            away_score = awayScore
        };

    [Test]
    public void GroupPairings_AreTenUniqueGamesWithTwoHomeEach()
    {
        var pairings = CupHelper.GroupPairings();
        Assert.That(pairings.Count, Is.EqualTo(10));
        Assert.That(pairings.Select(p => (p.homeIndex, p.awayIndex)).Distinct().Count(), Is.EqualTo(10));

        for (int team = 0; team < 5; team++)
        {
            Assert.That(pairings.Count(p => p.homeIndex == team), Is.EqualTo(2), $"localías {team}");
            Assert.That(pairings.Count(p => p.awayIndex == team), Is.EqualTo(2), $"visitas {team}");
        }
    }

    [Test]
    public void MarkGroupGames_MarksFourPerTeamAndTwoHome()
    {
        var teams = Enumerable.Range(1, 5).Select(i => Team(i)).ToList();
        var games = new List<GameData>();
        int day = 1;
        for (int i = 0; i < teams.Count; i++)
        for (int j = i + 1; j < teams.Count; j++)
        {
            // Cuatro partidos por pareja: 2 en cada campo.
            for (int k = 0; k < 2; k++)
            {
                games.Add(Game(teams[i].id, teams[j].id, day++));
                games.Add(Game(teams[j].id, teams[i].id, day++));
            }
        }

        int marked = CupHelper.MarkGroupGames(games, teams);
        Assert.That(marked, Is.EqualTo(10));

        foreach (var team in teams)
        {
            var own = games.Where(g => (g.home_team_id == team.id || g.away_team_id == team.id)
                                 && g.series_label.StartsWith(CupHelper.GroupLabelPrefix)).ToList();
            Assert.That(own.Count, Is.EqualTo(4), $"{team.id}: 4 partidos de grupo");
            Assert.That(own.Count(g => g.home_team_id == team.id), Is.EqualTo(2), $"{team.id}: 2 en casa");
            Assert.That(own.Count(g => g.away_team_id == team.id), Is.EqualTo(2), $"{team.id}: 2 fuera");
            Assert.That(own.All(g => g.series_label.StartsWith(CupHelper.GroupLabelPrefix)), Is.True);
        }
    }

    [Test]
    public void ComputeStandings_SortsByWinsThenDiff()
    {
        var teams = Enumerable.Range(1, 5).Select(i => Team(i)).ToList();
        var games = new List<GameData>
        {
            Game(1, 2, 1, 100, 90, CupHelper.GroupLabelPrefix + "G"),
            Game(1, 3, 2, 110, 80, CupHelper.GroupLabelPrefix + "G"),
            Game(2, 3, 3, 95, 100, CupHelper.GroupLabelPrefix + "G"),
            Game(2, 4, 4, 120, 100, CupHelper.GroupLabelPrefix + "G"),
        };

        var rows = CupHelper.ComputeStandings(teams, games);
        var group = rows.Where(r => r.group_name == CupHelper.GroupName(teams[0])).ToList();
        Assert.That(group, Has.Count.EqualTo(5));
        Assert.That(group[0].team_id, Is.EqualTo(1), "el primero gana sus dos partidos");
        Assert.That(group[0].wins, Is.EqualTo(2));
        Assert.That(group[0].point_diff, Is.EqualTo(40));
        Assert.That(group[0].rank, Is.EqualTo(1));
        Assert.That(group[1].team_id, Is.EqualTo(2).Or.EqualTo(3), "desempate por diferencia");
    }

    [Test]
    public void QualifiedTeamIds_TakesSixWinnersAndOneWildcardPerConference()
    {
        var standings = new List<TournamentStandingData>();
        for (int group = 0; group < 6; group++)
        {
            string conf = group < 3 ? "East" : "West";
            for (int rank = 1; rank <= 5; rank++)
            {
                standings.Add(new TournamentStandingData
                {
                    group_name = $"{conf} G{group}",
                    conference = conf,
                    team_id = group * 10 + rank,
                    rank = rank,
                    wins = 4 - rank,
                    losses = rank - 1,
                    point_diff = (5 - rank) * 10,
                    points_for = 400 - rank
                });
            }
        }

        var qualified = CupHelper.QualifiedTeamIds(standings);
        Assert.That(qualified, Has.Count.EqualTo(8));
        Assert.That(qualified.Distinct().Count(), Is.EqualTo(8));
        Assert.That(qualified, Does.Contain(1), "ganador del grupo 0");
        Assert.That(qualified, Does.Contain(2), "mejor segundo del Este");
        Assert.That(qualified, Does.Contain(32), "mejor segundo del Oeste");
    }
}
