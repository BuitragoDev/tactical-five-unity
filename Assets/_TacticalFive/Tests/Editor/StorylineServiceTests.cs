using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Motor de narrativa (Fase 4): rachas, escalones y detectores de hilos (puros).
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

    static StorylineContext Ctx(params PlayerData[] players)
        => new StorylineContext
        {
            manager = new ManagerData { id = 1 },
            myTeam = new TeamData { id = 1, name = "Test Team" },
            season = new SeasonData { id = 1 },
            gameDay = 10,
            gameDate = "2026-12-01",
            Roster = players.ToList()
        };

    // Los partidos se guardan del más reciente al más antiguo (como arma el contexto).
    static void AddGame(StorylineContext ctx, int playerId, int points, float minutes)
    {
        if (!ctx.RecentByPlayer.TryGetValue(playerId, out var list))
        {
            list = new List<PlayerGameStats>();
            ctx.RecentByPlayer[playerId] = list;
        }
        list.Add(new PlayerGameStats { player_id = playerId, points = points, minutes = minutes });
    }

    // ── RACHAS (puras) ──────────────────────────────────────

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
            Game(1, 1, 2, 100, 90),
            Game(2, 1, 2, 90, 100),
            Game(3, 2, 1, 120, 100)
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
    public void Effects_AreSignedAndScaled()
    {
        var up = StorylineService.Effects(StorylineType.RachaPositiva, 2);
        var down = StorylineService.Effects(StorylineType.Bache, 2);

        Assert.That(up.morale, Is.GreaterThan(0));
        Assert.That(up.chemistry, Is.GreaterThan(0));
        Assert.That(up.fan, Is.GreaterThan(0));
        Assert.That(down.morale, Is.LessThan(0));
        Assert.That(down.fan, Is.LessThan(0));
    }

    // ── DETECTOR DE RACHAS ──────────────────────────────────

    [Test]
    public void DetectStreak_OpensAtTheThreshold_AndEscalatesAndCloses()
    {
        var def = StorylineCatalog.Find(StorylineType.RachaPositiva);

        var ctx = Ctx();
        ctx.Streak = 5;
        var start = StorylineCatalog.DetectStreak(ctx, def, null);
        Assert.That(start, Has.Count.EqualTo(1));
        Assert.That(start[0].action, Is.EqualTo(StorylineService.Action.Start));
        Assert.That(start[0].chemistry, Is.GreaterThan(0));

        var active = new StorylineData { type = StorylineType.RachaPositiva, intensity = 1 };
        ctx.Streak = 10;
        Assert.That(StorylineCatalog.DetectStreak(ctx, def, active)[0].action,
                    Is.EqualTo(StorylineService.Action.Escalate));

        ctx.Streak = -1;
        var closed = StorylineCatalog.DetectStreak(ctx, def, active);
        Assert.That(closed[0].action, Is.EqualTo(StorylineService.Action.Resolve));
        Assert.That(closed[0].resolution, Is.EqualTo(StorylineResolution.Broken));
    }

    [Test]
    public void DetectStreak_DoesNothingBelowTheThreshold()
    {
        var def = StorylineCatalog.Find(StorylineType.RachaPositiva);
        var ctx = Ctx();
        ctx.Streak = 4;
        Assert.That(StorylineCatalog.DetectStreak(ctx, def, null), Is.Empty);
    }

    // ── DETECTORES DE JUGADOR ───────────────────────────────

    [Test]
    public void DetectHotHand_OpensWithThreeGamesOverTheThreshold()
    {
        var p = new PlayerData { id = 1, first_name = "Ana", last_name = "Ruiz" };
        var ctx = Ctx(p);
        AddGame(ctx, 1, 30, 35f);
        AddGame(ctx, 1, 28, 34f);
        AddGame(ctx, 1, 27, 33f);

        var def = StorylineCatalog.Find(StorylineType.JugadorCaliente);
        var t = StorylineCatalog.DetectHotHand(ctx, def, null);

        Assert.That(t, Has.Count.EqualTo(1));
        Assert.That(t[0].action, Is.EqualTo(StorylineService.Action.Start));
        Assert.That(t[0].subjectPlayerId, Is.EqualTo(1));
        Assert.That(t[0].morale, Is.GreaterThan(0));
    }

    [Test]
    public void FindPointsStreak_StopsAtTheFirstMiss()
    {
        var p = new PlayerData { id = 1 };
        var ctx = Ctx(p);
        AddGame(ctx, 1, 30, 35f);   // más reciente: cumple
        AddGame(ctx, 1, 12, 30f);   // rompe la racha

        Assert.That(StorylineCatalog.FindPointsStreak(ctx, StorylineCatalog.HOT_POINTS, true, 3), Is.Null);
    }

    [Test]
    public void FindUnderusedPlayer_DetectsLowMoraleAndMinutes()
    {
        var p = new PlayerData { id = 2, role = PlayerRole.Titular, morale = 20, rotation_minutes = -1 };
        var ctx = Ctx(p);
        AddGame(ctx, 2, 5, 8f);
        AddGame(ctx, 2, 4, 10f);
        AddGame(ctx, 2, 6, 9f);

        var found = StorylineCatalog.FindUnderusedPlayer(ctx);

        Assert.That(found.HasValue, Is.True);
        Assert.That(found.Value.player.id, Is.EqualTo(2));
    }

    [Test]
    public void FindUnderusedPlayer_IgnoresHappyPlayers()
    {
        var p = new PlayerData { id = 3, role = PlayerRole.Titular, morale = 70 };
        var ctx = Ctx(p);
        AddGame(ctx, 3, 5, 8f);
        AddGame(ctx, 3, 4, 10f);
        AddGame(ctx, 3, 6, 9f);

        Assert.That(StorylineCatalog.FindUnderusedPlayer(ctx), Is.Null);
    }

    [Test]
    public void ExpectedMinutes_FollowsTheRole()
    {
        Assert.That(StorylineCatalog.ExpectedMinutes(PlayerRole.Estrella), Is.GreaterThan(
                    StorylineCatalog.ExpectedMinutes(PlayerRole.Banquillo)));
        Assert.That(StorylineCatalog.ExpectedMinutes(PlayerRole.Banquillo), Is.GreaterThan(
                    StorylineCatalog.ExpectedMinutes(PlayerRole.UltimoRecurso)));
    }

    // ── DETECTORES FASE C/E ────────────────────────────────

    [Test]
    public void DetectTradeRumor_OpensForMarketPlayer_AndClosesWhenOut()
    {
        var p = new PlayerData { id = 5, first_name = "Rui", last_name = "Diaz", on_trade_block = 1, overall = 80 };
        var ctx = Ctx(p);
        var def = StorylineCatalog.Find(StorylineType.RumorTraspaso);

        var open = StorylineCatalog.DetectTradeRumor(ctx, def, null);
        Assert.That(open, Has.Count.EqualTo(1));
        Assert.That(open[0].action, Is.EqualTo(StorylineService.Action.Start));
        Assert.That(open[0].subjectPlayerId, Is.EqualTo(5));

        // El jugador sale del equipo -> el hilo se resuelve
        var active = new StorylineData { type = StorylineType.RumorTraspaso, subject_player_id = 5, intensity = 1 };
        ctx.Roster.Clear();
        var closed = StorylineCatalog.DetectTradeRumor(ctx, def, active);
        Assert.That(closed[0].action, Is.EqualTo(StorylineService.Action.Resolve));
        Assert.That(closed[0].resolution, Is.EqualTo(StorylineResolution.Completed));
    }

    [Test]
    public void DetectStarInjury_OpensAndResolvesOnReturn()
    {
        var star = new PlayerData { id = 9, first_name = "Nikola", last_name = "P", overall = 92, injury_days = 12 };
        var ctx = Ctx(star);
        var def = StorylineCatalog.Find(StorylineType.BajaEstrella);

        var open = StorylineCatalog.DetectStarInjury(ctx, def, null);
        Assert.That(open, Has.Count.EqualTo(1));
        Assert.That(open[0].action, Is.EqualTo(StorylineService.Action.Start));

        star.injury_days = 0;
        var active = new StorylineData { type = StorylineType.BajaEstrella, subject_player_id = 9, intensity = 1 };
        var closed = StorylineCatalog.DetectStarInjury(ctx, def, active);
        Assert.That(closed[0].action, Is.EqualTo(StorylineService.Action.Resolve));
        Assert.That(closed[0].resolution, Is.EqualTo(StorylineResolution.Completed));
    }

    [Test]
    public void DetectGmPressure_OpensWhenFanAndTrustAreLow()
    {
        var ctx = Ctx();
        ctx.manager.fan_confidence = 25;
        ctx.manager.trust = 40;
        var def = StorylineCatalog.Find(StorylineType.PresionGM);

        var open = StorylineCatalog.DetectGmPressure(ctx, def, null);
        Assert.That(open, Has.Count.EqualTo(1));
        Assert.That(open[0].action, Is.EqualTo(StorylineService.Action.Start));

        ctx.manager.fan_confidence = 60;
        ctx.manager.trust = 60;
        var active = new StorylineData { type = StorylineType.PresionGM, intensity = 1 };
        var closed = StorylineCatalog.DetectGmPressure(ctx, def, active);
        Assert.That(closed[0].action, Is.EqualTo(StorylineService.Action.Resolve));
    }

    [Test]
    public void DetectPlayoffRace_OpensLateAndResolvesWithDirectPlayoff()
    {
        var ctx = Ctx();
        ctx.GamesRemaining = 20;
        ctx.ConferenceRank = 9;
        var def = StorylineCatalog.Find(StorylineType.CazaPlayoffs);

        var open = StorylineCatalog.DetectPlayoffRace(ctx, def, null);
        Assert.That(open, Has.Count.EqualTo(1));
        Assert.That(open[0].action, Is.EqualTo(StorylineService.Action.Start));

        ctx.ConferenceRank = 4;
        var active = new StorylineData { type = StorylineType.CazaPlayoffs, intensity = 1 };
        var closed = StorylineCatalog.DetectPlayoffRace(ctx, def, active);
        Assert.That(closed[0].action, Is.EqualTo(StorylineService.Action.Resolve));
        Assert.That(closed[0].resolution, Is.EqualTo(StorylineResolution.Completed));
    }

    [Test]
    public void DetectEconomicCrisis_OpensWithNegativeBudget_AndClosesWhenFixed()
    {
        var ctx = Ctx();
        ctx.myTeam.budget = -5_000_000;
        var def = StorylineCatalog.Find(StorylineType.CrisisEconomica);

        var open = StorylineCatalog.DetectEconomicCrisis(ctx, def, null);
        Assert.That(open, Has.Count.EqualTo(1));
        Assert.That(open[0].action, Is.EqualTo(StorylineService.Action.Start));

        ctx.myTeam.budget = 1_000_000;
        ctx.manager.budget_red_warnings = 0;
        var active = new StorylineData { type = StorylineType.CrisisEconomica, intensity = 1 };
        var closed = StorylineCatalog.DetectEconomicCrisis(ctx, def, active);
        Assert.That(closed[0].action, Is.EqualTo(StorylineService.Action.Resolve));
        Assert.That(closed[0].resolution, Is.EqualTo(StorylineResolution.Completed));
    }
}
