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

    // ── HILOS POR JUGADOR (varios protagonistas a la vez) ───

    [Test]
    public void DetectSlumpPlayers_OpensOneHiloPerPlayer()
    {
        var a = new PlayerData { id = 1, first_name = "Ana", last_name = "Uno", role = PlayerRole.Estrella, overall = 90 };
        var b = new PlayerData { id = 2, first_name = "Bea", last_name = "Dos", role = PlayerRole.Titular, overall = 85 };
        var ctx = Ctx(a, b);
        ctx.SeasonPpg[1] = 30f; ctx.SeasonPpg[2] = 30f;   // umbral 14
        foreach (int id in new[] { 1, 2 })
        {
            AddGame(ctx, id, 8, 30f);
            AddGame(ctx, id, 9, 30f);
            AddGame(ctx, id, 10, 30f);
        }

        var def = StorylineCatalog.Find(StorylineType.Sequia);
        var t = StorylineCatalog.DetectSlumpPlayers(ctx, def, new StorylineSubjects());

        Assert.That(t, Has.Count.EqualTo(2), "Cada protagonista tiene su propio hilo");
        Assert.That(t.Select(x => x.subjectPlayerId).OrderBy(x => x), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(t.All(x => x.action == StorylineService.Action.Start), Is.True);
    }

    [Test]
    public void DetectSlumpPlayers_ClosesSubjectThatNoLongerQualifies()
    {
        var a = new PlayerData { id = 1, first_name = "Ana", last_name = "Uno", role = PlayerRole.Estrella, overall = 90 };
        var ctx = Ctx(a);
        ctx.SeasonPpg[1] = 30f;
        AddGame(ctx, 1, 8, 30f);
        AddGame(ctx, 1, 9, 30f);
        AddGame(ctx, 1, 10, 30f);

        var subjects = new StorylineSubjects
        {
            Actives = new List<StorylineData>
            {
                new StorylineData { id = 5, type = StorylineType.Sequia, subject_player_id = 9,
                                    title = "Sequía: Otro Jugador", resolved = 0 }
            }
        };

        var def = StorylineCatalog.Find(StorylineType.Sequia);
        var t = StorylineCatalog.DetectSlumpPlayers(ctx, def, subjects);

        Assert.That(t.Any(x => x.action == StorylineService.Action.Start && x.subjectPlayerId == 1), Is.True);
        Assert.That(t.Any(x => x.action == StorylineService.Action.Resolve && x.subjectPlayerId == 9), Is.True,
            "El hilo del protagonista que ya no cumple se cierra (y con su sujeto, no de equipo)");
    }

    [Test]
    public void FindPointsStreakCandidates_CapsAndOrdersByRelevance()
    {
        var bajo = new PlayerData { id = 1, role = PlayerRole.Banquillo, overall = 60 };
        var estrella = new PlayerData { id = 2, role = PlayerRole.Estrella, overall = 95 };
        var titular = new PlayerData { id = 3, role = PlayerRole.Titular, overall = 80 };
        var ctx = Ctx(bajo, estrella, titular);
        foreach (int id in new[] { 1, 2, 3 })
        {
            ctx.SeasonPpg[id] = 30f;
            for (int i = 0; i < 3; i++) AddGame(ctx, id, 5, 30f);
        }

        var cands = StorylineCatalog.FindPointsStreakCandidates(ctx, StorylineCatalog.COLD_POINTS_MIN,
            atLeast: false, minGames: 3, minAvgMinutes: StorylineCatalog.SLUMP_MIN_AVG_MINUTES, maxCandidates: 2);

        Assert.That(cands, Has.Count.EqualTo(2), "Se capan a los más relevantes");
        Assert.That(cands[0].player.id, Is.EqualTo(2));
        Assert.That(cands[1].player.id, Is.EqualTo(3));
    }

    // ── SEQUÍA: umbral relativo, minutos y relevancia ───────

    [Test]
    public void ColdThreshold_IsRelativeToSeasonAverage_WithFloor()
    {
        var p = new PlayerData { id = 1 };
        var ctx = Ctx(p);
        ctx.SeasonPpg[1] = 30f;   // 30 * 0.45 = 13.5 -> 14
        Assert.That(StorylineCatalog.ColdThreshold(ctx, p), Is.EqualTo(14));

        ctx.SeasonPpg[1] = 5f;    // 2.25 -> por debajo del suelo
        Assert.That(StorylineCatalog.ColdThreshold(ctx, p), Is.EqualTo(StorylineCatalog.COLD_POINTS_MIN));

        ctx.SeasonPpg.Clear();    // sin datos de temporada: suelo
        Assert.That(StorylineCatalog.ColdThreshold(ctx, p), Is.EqualTo(StorylineCatalog.COLD_POINTS_MIN));
    }

    [Test]
    public void DetectSlump_UsesRelativeThreshold()
    {
        // Anotador de 5 puntos por partido: 4-5-6 es su nivel normal, no una sequía
        // (su umbral es max(3, 45% de 5) = 3).
        var bajo = new PlayerData { id = 1, first_name = "Bajo", last_name = "Perfil",
                                    role = PlayerRole.Banquillo, overall = 60 };
        var ctx = Ctx(bajo);
        ctx.SeasonPpg[1] = 5f;
        AddGame(ctx, 1, 4, 25f);
        AddGame(ctx, 1, 5, 25f);
        AddGame(ctx, 1, 6, 25f);

        var def = StorylineCatalog.Find(StorylineType.Sequia);
        Assert.That(StorylineCatalog.DetectSlump(ctx, def, null), Is.Empty,
            "4-5-6 puntos no es sequía para un jugador que promedia 5");

        // El mismo perfil anotando la mitad de lo suyo (≤3) sí es sequía.
        var ctx2 = Ctx(new PlayerData { id = 1, role = PlayerRole.Banquillo, overall = 60 });
        ctx2.SeasonPpg[1] = 4f;                     // umbral = max(3, 2) = 3
        AddGame(ctx2, 1, 1, 25f);
        AddGame(ctx2, 1, 2, 25f);
        AddGame(ctx2, 1, 3, 25f);
        Assert.That(StorylineCatalog.DetectSlump(ctx2, def, null), Has.Count.EqualTo(1));
    }

    [Test]
    public void DetectSlump_IgnoresPlayersWhoBarelyPlay()
    {
        // 3 partidos fríos pero solo 8 minutos de media: es un problema de rol (Descontento).
        var suplente = new PlayerData { id = 1, role = PlayerRole.Banquillo, overall = 70 };
        var ctx = Ctx(suplente);
        ctx.SeasonPpg[1] = 30f;                     // umbral 14
        AddGame(ctx, 1, 2, 8f);
        AddGame(ctx, 1, 3, 8f);
        AddGame(ctx, 1, 4, 8f);

        var def = StorylineCatalog.Find(StorylineType.Sequia);
        Assert.That(StorylineCatalog.DetectSlump(ctx, def, null), Is.Empty);
    }

    [Test]
    public void DetectSlump_IgnoresUltimoRecurso()
    {
        var ultimo = new PlayerData { id = 1, role = PlayerRole.UltimoRecurso, overall = 65 };
        var ctx = Ctx(ultimo);
        ctx.SeasonPpg[1] = 30f;
        AddGame(ctx, 1, 2, 30f);
        AddGame(ctx, 1, 3, 30f);
        AddGame(ctx, 1, 4, 30f);

        var def = StorylineCatalog.Find(StorylineType.Sequia);
        Assert.That(StorylineCatalog.DetectSlump(ctx, def, null), Is.Empty);
    }

    [Test]
    public void DetectSlump_OpensForUnderperformingStar()
    {
        var estrella = new PlayerData { id = 1, first_name = "Estrella", last_name = "Uno",
                                        role = PlayerRole.Estrella, overall = 92 };
        var ctx = Ctx(estrella);
        ctx.SeasonPpg[1] = 30f;                     // umbral 14
        AddGame(ctx, 1, 10, 36f);
        AddGame(ctx, 1, 12, 35f);
        AddGame(ctx, 1, 9, 34f);

        var def = StorylineCatalog.Find(StorylineType.Sequia);
        var t = StorylineCatalog.DetectSlump(ctx, def, null);

        Assert.That(t, Has.Count.EqualTo(1));
        Assert.That(t[0].action, Is.EqualTo(StorylineService.Action.Start));
        Assert.That(t[0].subjectPlayerId, Is.EqualTo(1));
        Assert.That(t[0].summary, Does.Contain("14"), "El texto usa el umbral del jugador");
    }

    [Test]
    public void FindPointsStreak_TieBreakPrefersRelevantPlayer()
    {
        // Misma racha fría: se narra la del jugador más relevante (mayor OVR).
        var suplente = new PlayerData { id = 1, overall = 68, role = PlayerRole.Banquillo };
        var estrella = new PlayerData { id = 2, overall = 91, role = PlayerRole.Estrella };
        var ctx = Ctx(suplente, estrella);
        ctx.SeasonPpg[1] = 30f; ctx.SeasonPpg[2] = 30f;
        foreach (int id in new[] { 1, 2 })
        {
            AddGame(ctx, id, 5, 30f);
            AddGame(ctx, id, 6, 30f);
            AddGame(ctx, id, 7, 30f);
        }

        var best = StorylineCatalog.FindPointsStreak(ctx, StorylineCatalog.COLD_POINTS_MIN,
                                                     atLeast: false, minGames: 3,
                                                     minAvgMinutes: StorylineCatalog.SLUMP_MIN_AVG_MINUTES);
        Assert.That(best, Is.Not.Null);
        Assert.That(best.Value.player.id, Is.EqualTo(2));
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

    // ── REGRESIÓN: solo cuentan los partidos de MI equipo ──

    [Test]
    public void RegularGamesOf_KeepsOnlyMyTeamGames_InOrder()
    {
        var games = new List<GameData>
        {
            Game(3, 2, 3, 100, 90),   // otro equipo
            Game(1, 1, 2, 100, 90),   // mío
            Game(2, 1, 3, 90, 100),   // mío
            Game(4, 3, 4, 100, 90),   // otro equipo
            Game(5, 4, 1, 80, 100)    // mío
        };

        var mine = StorylineService.RegularGamesOf(games, 1);

        Assert.That(mine, Has.Count.EqualTo(3), "Solo los partidos de mi equipo (bug: contaba toda la liga)");
        Assert.That(mine[0].game_day, Is.EqualTo(1));
        Assert.That(mine[1].game_day, Is.EqualTo(2));
        Assert.That(mine[2].game_day, Is.EqualTo(5));
    }

    [Test]
    public void CurrentStreak_IgnoresGamesOfOtherTeams()
    {
        // Aunque la lista venga mezclada, la racha solo mira mis partidos.
        var games = new List<GameData>
        {
            Game(1, 1, 2, 100, 90),   // gano
            Game(2, 3, 4, 100, 90),   // partido ajeno (no debe romper nada)
            Game(3, 1, 2, 110, 99),   // gano
            Game(4, 4, 3, 100, 90)    // partido ajeno
        };
        Assert.That(StorylineService.CurrentStreak(games, 1), Is.EqualTo(2));
    }
}
