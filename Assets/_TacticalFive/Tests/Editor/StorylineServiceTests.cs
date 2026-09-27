using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Motor de narrativa (Fase 4): detección de rachas, escalones y transiciones del hilo.
/// </summary>
public class StorylineServiceTests
{
    static GameData Game(int day, int home, int away, int homeScore, int awayScore, bool played = true)
        => new GameData
        {
            id = day, game_day = day,
            home_team_id = home, away_team_id = away,
            home_score = homeScore, away_score = awayScore,
            is_played = played ? 1 : 0, game_type = "regular"
        };

    [Test]
    public void CurrentStreak_CountsTrailingWins()
    {
        var games = new List<GameData>
        {
            Game(1, 1, 2, 100, 90),   // gana
            Game(2, 2, 1, 95, 80),    // pierde
            Game(3, 1, 2, 110, 99)    // gana -> racha 1
        };
        Assert.That(StorylineService.CurrentStreak(games, 1), Is.EqualTo(1));
    }

    [Test]
    public void CurrentStreak_NegativeWhenLosing()
    {
        var games = new List<GameData>
        {
            Game(1, 1, 2, 100, 90),   // gana
            Game(2, 1, 2, 90, 100),   // pierde
            Game(3, 2, 1, 120, 100)   // pierde -> racha -2
        };
        Assert.That(StorylineService.CurrentStreak(games, 1), Is.EqualTo(-2));
    }

    [Test]
    public void Milestone_StepsEveryFive()
    {
        Assert.That(StorylineService.Milestone(4), Is.EqualTo(0));
        Assert.That(StorylineService.Milestone(5), Is.EqualTo(1));
        Assert.That(StorylineService.Milestone(-10), Is.EqualTo(2));
        Assert.That(StorylineService.Milestone(14), Is.EqualTo(2));
    }

    [Test]
    public void TypeFor_MapsTheSign()
    {
        Assert.That(StorylineService.TypeFor(3), Is.Null);
        Assert.That(StorylineService.TypeFor(5), Is.EqualTo(StorylineType.RachaPositiva));
        Assert.That(StorylineService.TypeFor(-5), Is.EqualTo(StorylineType.Bache));
    }

    [Test]
    public void Decide_OpensEscalatesAndCloses()
    {
        Assert.That(StorylineService.Decide(5, null, 0), Is.EqualTo(StorylineService.Action.Start));
        Assert.That(StorylineService.Decide(3, null, 0), Is.EqualTo(StorylineService.Action.None));
        Assert.That(StorylineService.Decide(10, StorylineType.RachaPositiva, 1), Is.EqualTo(StorylineService.Action.Escalate));
        Assert.That(StorylineService.Decide(0, StorylineType.RachaPositiva, 1), Is.EqualTo(StorylineService.Action.Resolve));
        Assert.That(StorylineService.Decide(-6, StorylineType.RachaPositiva, 1), Is.EqualTo(StorylineService.Action.Resolve));
    }

    [Test]
    public void Effects_AreSignedAndScaled()
    {
        var up = StorylineService.Effects(StorylineType.RachaPositiva, 2);
        var down = StorylineService.Effects(StorylineType.Bache, 2);

        Assert.That(up.morale, Is.GreaterThan(0));
        Assert.That(up.chemistry, Is.GreaterThan(0));
        Assert.That(up.fan, Is.GreaterThan(0));
        Assert.That(down.morale, Is.LessThan(0));
        Assert.That(down.fan, Is.LessThan(0));
        Assert.That(StorylineService.Effects(StorylineType.RachaPositiva, 3).fan,
                    Is.GreaterThan(StorylineService.Effects(StorylineType.RachaPositiva, 1).fan));
    }
}
