using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

public class GameSimulatorTests
{
    [Test]
    public void Rebounds_PrioritizeInteriorPositions()
    {
        Assert.That(
            GameSimulator.GetReboundPositionMultiplier("C", false),
            Is.GreaterThan(GameSimulator.GetReboundPositionMultiplier("PG", false)));
        Assert.That(
            GameSimulator.GetReboundPositionMultiplier("PF", true),
            Is.GreaterThan(GameSimulator.GetReboundPositionMultiplier("SG", true)));
    }

    [Test]
    public void Blocks_PrioritizeCentersAndPowerForwards()
    {
        Assert.That(
            GameSimulator.GetBlockPositionMultiplier("C"),
            Is.GreaterThan(GameSimulator.GetBlockPositionMultiplier("PG")));
        Assert.That(
            GameSimulator.GetBlockPositionMultiplier("PF"),
            Is.GreaterThan(GameSimulator.GetBlockPositionMultiplier("SG")));
    }

    [Test]
    public void UnknownPosition_UsesNeutralMultiplier()
    {
        Assert.That(GameSimulator.GetReboundPositionMultiplier("", false), Is.EqualTo(1f));
        Assert.That(GameSimulator.GetBlockPositionMultiplier(""), Is.EqualTo(1f));
    }

    [Test]
    public void StartersAndStars_ReceiveHigherMinuteTargets()
    {
        var star = new GameSimulator.PlayerStatSnapshot
        {
            role = PlayerRole.Estrella,
            overall = 90,
            starter = true
        };
        var bench = new GameSimulator.PlayerStatSnapshot
        {
            role = PlayerRole.Banquillo,
            overall = 72,
            starter = false
        };

        // Tabla nueva: estrella (OVR>=90) titular 40; banquillo de media baja 12.
        Assert.That(GameSimulator.GetTargetMinutes(star), Is.EqualTo(40));
        Assert.That(GameSimulator.GetTargetMinutes(bench), Is.EqualTo(12));
        Assert.That(GameSimulator.GetTargetMinutes(star), Is.GreaterThan(GameSimulator.GetTargetMinutes(bench)));

        // Tramos por media y fondo de banquillo.
        Assert.That(GameSimulator.GetTargetMinutes(new GameSimulator.PlayerStatSnapshot
        { role = PlayerRole.Titular, overall = 86, starter = true }), Is.EqualTo(34));
        Assert.That(GameSimulator.GetTargetMinutes(new GameSimulator.PlayerStatSnapshot
        { role = PlayerRole.Titular, overall = 82, starter = false }), Is.EqualTo(22));
        Assert.That(GameSimulator.GetTargetMinutes(new GameSimulator.PlayerStatSnapshot
        { role = PlayerRole.Banquillo, overall = 76, starter = true }), Is.EqualTo(26));
        Assert.That(GameSimulator.GetTargetMinutes(new GameSimulator.PlayerStatSnapshot
        { role = PlayerRole.UltimoRecurso, overall = 95, starter = true }),
            Is.EqualTo(GameSimulator.MIN_FIXED_MINUTES), "último recurso = suelo, aunque tenga media");
    }

    [Test]
    public void GetTargetMinutes_GLeagueScalesTheSameTableDown()
    {
        var star = new GameSimulator.PlayerStatSnapshot { role = PlayerRole.Estrella, overall = 92, starter = true };
        Assert.That(GameSimulator.GetTargetMinutes(star, glLeague: true), Is.EqualTo(30), "40 × 0.75");
        Assert.That(GameSimulator.GetTargetMinutes(star), Is.EqualTo(40));

        var last = new GameSimulator.PlayerStatSnapshot { role = PlayerRole.UltimoRecurso, overall = 80, starter = false };
        Assert.That(GameSimulator.GetTargetMinutes(last, glLeague: true),
            Is.EqualTo(GameSimulator.MIN_FIXED_MINUTES), "el fondo nunca baja del suelo");
    }

    [Test]
    public void FixedPlans_RespectTheTwoMinuteFloor()
    {
        var players = new List<GameSimulator.PlayerStatSnapshot>
        {
            Plan("PG", "SG", PlayerRole.Titular, 85, 1, true),   // 0: 1 -> suelo 2
            Plan("SG", "SF", PlayerRole.Titular, 85, 3, true)    // 1: 3 -> se respeta (ya no sube a 8)
        };

        GameSimulator.PrepareRotationMetadata(players);

        Assert.That(players[0].target_minutes, Is.EqualTo(GameSimulator.MIN_FIXED_MINUTES));
        Assert.That(players[1].target_minutes, Is.EqualTo(3));
    }

    // ── Rotación vinculante (pantalla ROTACIÓN) ─────────────────

    static GameSimulator.PlayerStatSnapshot Plan(string pos, string sec, PlayerRole role, int ovr, int plan, bool starter)
    {
        return new GameSimulator.PlayerStatSnapshot
        {
            position = pos,
            secondary_position = sec,
            role = role,
            overall = ovr,
            target_minutes = plan,
            fixed_minutes = plan >= 0,
            starter = starter
        };
    }

    // 12 convocados: planes 8/23/29/29/23 titulares y 16/16/15/15/39/15/15 banquillo.
    static List<GameSimulator.PlayerStatSnapshot> ConfiguredRoster()
    {
        return new List<GameSimulator.PlayerStatSnapshot>
        {
            Plan("PG", "SG", PlayerRole.Titular,   88,  8, true),
            Plan("PG", "SG", PlayerRole.Banquillo, 82, 23, true),
            Plan("SF", "PF", PlayerRole.Titular,   85, 29, true),
            Plan("PF", "C",  PlayerRole.Estrella,  93, 29, true),
            Plan("C",  "PF", PlayerRole.Titular,   83, 23, true),
            Plan("C",  "PF", PlayerRole.Banquillo, 80, 16, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 78, 16, false),
            Plan("PG", "SG", PlayerRole.Banquillo, 78, 15, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 77, 15, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 77, 39, false),
            Plan("SG", "SF", PlayerRole.Banquillo, 75, 15, false),
            Plan("C",  "PF", PlayerRole.Banquillo, 75, 15, false)
        };
    }

    [Test]
    public void StrictRotation_TakesOutPlayerOverPlan_AndBringsInPendingMinutes()
    {
        var ps = ConfiguredRoster();
        var on = new HashSet<int> { 0, 1, 2, 3, 4 };
        var quarterMinutes = new float[12];
        quarterMinutes[0] = 20f; // White: plan 8 -> se pasó 12

        var pick = GameSimulator.PickSubstitution(on, ps, quarterMinutes, strict: true);

        Assert.That(pick.outgoing, Is.EqualTo(0), "Debe salir el que más se pasa de su plan");
        Assert.That(pick.incoming, Is.EqualTo(9), "Debe entrar el de más minutos pendientes (Hugo, plan 39)");
    }

    [Test]
    public void StrictRotation_DoesNotReenterPlayerWhoExhaustedPlan()
    {
        var ps = ConfiguredRoster();
        // Todos los de banquillo ya jugaron su plan salvo el 9.
        foreach (int i in new[] { 5, 6, 7, 8, 10, 11 })
            ps[i].minutes = ps[i].target_minutes;
        ps[9].minutes = 0;

        var on = new HashSet<int> { 0, 1, 2, 3, 4 };
        var quarterMinutes = new float[12];
        quarterMinutes[0] = 20f;

        var pick = GameSimulator.PickSubstitution(on, ps, quarterMinutes, strict: true);

        Assert.That(pick.outgoing, Is.EqualTo(0));
        Assert.That(pick.incoming, Is.EqualTo(9), "No debe volver a entrar quien agotó su plan");
    }

    [Test]
    public void StrictRotation_NoChange_WhenNobodyHasPendingMinutes()
    {
        var ps = ConfiguredRoster();
        foreach (int i in new[] { 5, 6, 7, 8, 9, 10, 11 })
            ps[i].minutes = ps[i].target_minutes;

        var on = new HashSet<int> { 0, 1, 2, 3, 4 };
        var quarterMinutes = new float[12];
        quarterMinutes[0] = 20f;

        var pick = GameSimulator.PickSubstitution(on, ps, quarterMinutes, strict: true);

        Assert.That(pick.outgoing, Is.EqualTo(-1));
        Assert.That(pick.incoming, Is.EqualTo(-1));
    }

    [Test]
    public void NonStrictRotation_KeepsAiStarRule_WithGlobalMinutes()
    {
        var ps = new List<GameSimulator.PlayerStatSnapshot>
        {
            Plan("PF", "C",  PlayerRole.Estrella,  93, 39, true),  // estrella en pista
            Plan("PG", "SG", PlayerRole.Titular,   80, 23, true),
            Plan("SF", "PF", PlayerRole.Titular,   80, 29, true),
            Plan("C",  "PF", PlayerRole.Titular,   80, 23, true),
            Plan("SG", "SF", PlayerRole.Banquillo, 75, 15, true),
            Plan("C",  "PF", PlayerRole.Banquillo, 75, 16, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 75, 16, false),
            Plan("PG", "SG", PlayerRole.Banquillo, 75, 15, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 75, 15, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 75, 39, false),
            Plan("SG", "SF", PlayerRole.Banquillo, 75, 15, false),
            Plan("C",  "PF", PlayerRole.Banquillo, 75, 15, false)
        };
        var on = new HashSet<int> { 0, 1, 2, 3, 4 };
        var quarterMinutes = new float[12];
        quarterMinutes[0] = 25f; // lleva 25' del cuarto, pero la IA no lo cuenta

        var ai = GameSimulator.PickSubstitution(on, ps, quarterMinutes, strict: false);
        Assert.That(ai.outgoing, Is.Not.EqualTo(0), "La IA mantiene la regla antigua (minutos globales)");

        var mine = GameSimulator.PickSubstitution(on, ps, quarterMinutes, strict: true);
        Assert.That(mine.outgoing, Is.EqualTo(0), "Con rotación vinculante sale el que se pasó del plan");
    }

    [Test]
    public void FixedPlans_GetMinimumFloor_AndIgnoreFatigue()
    {
        var players = new List<GameSimulator.PlayerStatSnapshot>
        {
            Plan("PG", "SG", PlayerRole.Titular,   88,  1, true),   // 0: 1 -> suelo 2
            Plan("SF", "PF", PlayerRole.Banquillo, 77, 39, false),  // 1: fijo, no lo toca la fatiga
            Plan("C",  "PF", PlayerRole.Banquillo, 75,  0, false),  // 2: 0 se respeta
            Plan("AP", "P",  PlayerRole.Banquillo, 74, 20, false),  // 3: fijo, sin cambios
            Plan("B",  "E",  PlayerRole.Banquillo, 73, 20, false),  // 4: fijo, sin cambios
            // 5+: banquillo. PrepareRotationMetadata fija starter = i < 5, así que el
            // automático tiene que estar en índice ≥ 5 para usar la heurística de suplente.
            Plan("SG", "SF", PlayerRole.Banquillo, 72, -1, false)   // 5: automático -> heurística
        };
        players[1].fisico = 10;
        players[5].fisico = 50;

        GameSimulator.PrepareRotationMetadata(players);

        Assert.That(players[0].target_minutes, Is.EqualTo(GameSimulator.MIN_FIXED_MINUTES));
        Assert.That(players[1].target_minutes, Is.EqualTo(39), "La fatiga no baja un plan configurado");
        Assert.That(players[2].target_minutes, Is.EqualTo(0));
        Assert.That(players[5].target_minutes, Is.EqualTo(12), "Sin configurar usa la heurística de suplente");
        Assert.That(players[5].fixed_minutes, Is.False);
        Assert.That(players[5].starter, Is.False);
        Assert.That(players[0].starter, Is.True);
    }

    // ── Integridad de resultados y rotación (Fase 2) ─────────────

    static List<PlayerData> AutoRoster(int baseId, int size = 12)
    {
        var list = new List<PlayerData>();
        for (int i = 0; i < size; i++)
        {
            list.Add(new PlayerData
            {
                id = baseId + i,
                first_name = "J" + i,
                last_name = "Test",
                position = PositionCodes.Order[i % PositionCodes.Order.Length],
                secondary_position = PositionCodes.Order[(i + 2) % PositionCodes.Order.Length],
                role = i < 1 ? PlayerRole.Estrella : i < 5 ? PlayerRole.Titular : PlayerRole.Banquillo,
                overall = 80, speed = 80, shooting = 80, three_point = 80, passing = 80,
                dribbling = 80, defense = 80, rebounding = 80, athleticism = 80, iq = 80,
                steals = 80, blocks = 80, fisico = 99, morale = 50, rotation_minutes = -1
            });
        }
        return list;
    }

    static GameData FakeGame() => new GameData
    {
        id = 1, season_id = 1, manager_id = 1, game_day = 1, game_date = "2026-10-22",
        home_team_id = 1, away_team_id = 2, game_type = "regular"
    };

    [Test]
    public void SimulateGame_ShortRoster_NeverEndsInTie()
    {
        // F2-06: un roster de 1 jugador se simula de verdad (no hay marcador
        // sintético) y el partido nunca puede terminar empatado.
        var home = AutoRoster(1, 1);
        var away = AutoRoster(100, 1);
        for (int i = 0; i < 100; i++)
        {
            var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
            Assert.That(r.home_score, Is.Not.EqualTo(r.away_score),
                $"Empate en la iteración {i}: {r.home_score}-{r.away_score}");
            Assert.That(r.shortRoster, Is.True);
            Assert.That(r.walkover, Is.False);
            // El marcador sale de las acciones reales de los jugadores disponibles.
            Assert.That(r.home_stats.Sum(p => p.points), Is.EqualTo(r.home_score));
            Assert.That(r.away_stats.Sum(p => p.points), Is.EqualTo(r.away_score));
        }
    }

    [Test]
    public void SimulateGame_ShortRoster_ProducesRealBoxScore()
    {
        // Con 1–4 sanos se juega con los que hay: marcador y box score coherentes.
        var home = AutoRoster(1, 3);
        var away = AutoRoster(100, 4);
        int totalHome = 0, totalAway = 0;

        for (int i = 0; i < 20; i++)
        {
            var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
            totalHome += r.home_score;
            totalAway += r.away_score;

            Assert.That(r.shortRoster, Is.True);
            Assert.That(r.walkover, Is.False);
            Assert.That(r.home_stats.Sum(p => p.points), Is.EqualTo(r.home_score));
            Assert.That(r.away_stats.Sum(p => p.points), Is.EqualTo(r.away_score));
            Assert.That(r.home_stats.Sum(p => p.fga), Is.GreaterThan(0),
                "El equipo corto también tira a canasta");
            Assert.That(r.away_stats.Sum(p => p.fga), Is.GreaterThan(0));
            Assert.That(r.home_stats.Max(p => p.minutes), Is.GreaterThan(0));
        }

        TestContext.WriteLine($"Marcador medio con 3 vs 4 jugadores: {totalHome / 20}-{totalAway / 20}");
    }

    [Test]
    public void SimulateGame_NoHealthyPlayers_IsAForfeit()
    {
        // Sin ningún jugador sano no se puede simular: incomparecencia 0–20 (regla
        // NBA), sin estadísticas inventadas.
        var home = AutoRoster(1, 0);
        var away = AutoRoster(100);

        var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
        Assert.That(r.walkover, Is.True);
        Assert.That(r.shortRoster, Is.True);
        Assert.That(r.home_score, Is.EqualTo(0));
        Assert.That(r.away_score, Is.EqualTo(20));
        Assert.That(r.home_stats, Is.Empty);
        Assert.That(r.away_stats.All(p => p.minutes == 0 && p.points == 0), Is.True,
            "Nadie acumula estadísticas en una incomparecencia");
        Assert.That(r.quarters.Sum(q => q.home), Is.EqualTo(0), "Los cuartos cuadran con el marcador");
        Assert.That(r.quarters.Sum(q => q.away), Is.EqualTo(20));

        // Todos lesionados también es incomparecencia para el visitante.
        var injured = AutoRoster(100);
        foreach (var p in injured) p.injury_days = 2;
        var r2 = GameSimulator.SimulateGame(FakeGame(), AutoRoster(1), injured, persistToDb: false);
        Assert.That(r2.walkover, Is.True);
        Assert.That(r2.home_score, Is.EqualTo(20));
        Assert.That(r2.away_score, Is.EqualTo(0));
        Assert.That(r2.quarters.Sum(q => q.home), Is.EqualTo(20));
        Assert.That(r2.quarters.Sum(q => q.away), Is.EqualTo(0));
    }

    [Test]
    public void SimulateGame_FullRosters_AreNotFlaggedAsShort()
    {
        var home = AutoRoster(1);
        var away = AutoRoster(100);
        var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
        Assert.That(r.shortRoster, Is.False);
        Assert.That(r.walkover, Is.False);
    }

    // ── Captura de play-by-play bajo demanda (F2-08) ─────────

    [Test]
    public void SimulateGame_WithoutCapture_DoesNotBuildTheChronicle()
    {
        var r = GameSimulator.SimulateGame(FakeGame(), AutoRoster(1), AutoRoster(100), persistToDb: false);

        Assert.That(r.playByPlay, Is.Null, "En directo no se construye la crónica");
        Assert.That(r.home_stats.Sum(p => p.points), Is.EqualTo(r.home_score));
        Assert.That(r.away_stats.Sum(p => p.points), Is.EqualTo(r.away_score));
    }

    [Test]
    public void SimulateGame_WithCapture_BuildsTheChronicle()
    {
        var r = GameSimulator.SimulateGame(FakeGame(), AutoRoster(1), AutoRoster(100), persistToDb: false,
            capturePlayByPlay: true);

        Assert.That(r.playByPlay, Is.Not.Null);
        Assert.That(r.playByPlay, Is.Not.Empty);
        Assert.That(r.playByPlay[r.playByPlay.Count - 1].text, Is.EqualTo("¡Fin del partido!"));
        Assert.That(r.playByPlay.Any(e => e.deltas != null && e.deltas.Count > 0), Is.True,
            "Las jugadas llevan deltas de estadísticas");
        // La crónica no altera el partido.
        Assert.That(r.home_stats.Sum(p => p.points), Is.EqualTo(r.home_score));
        Assert.That(r.away_stats.Sum(p => p.points), Is.EqualTo(r.away_score));
    }

    [Test]
    public void SimulateGame_CaptureCost_IsMeasured()
    {
        // Medición de un día completo de la liga (15 partidos) en cada modo. No se
        // afirma sobre el tiempo (depende de la máquina): se publica para el plan.
        var home = AutoRoster(1);
        var away = AutoRoster(100);
        var sw = new System.Diagnostics.Stopwatch();

        // Calentamiento (JIT) para que la comparación no mida la primera compilación.
        for (int i = 0; i < 3; i++)
        {
            GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
            GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false, capturePlayByPlay: true);
        }

        long direct = long.MaxValue, capture = long.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            sw.Restart();
            for (int i = 0; i < 15; i++)
                GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
            sw.Stop();
            direct = System.Math.Min(direct, sw.ElapsedMilliseconds);

            sw.Restart();
            for (int i = 0; i < 15; i++)
                GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false, capturePlayByPlay: true);
            sw.Stop();
            capture = System.Math.Min(capture, sw.ElapsedMilliseconds);
        }

        TestContext.WriteLine($"Día de 15 partidos (mejor de 3): directa {direct} ms · con crónica {capture} ms "
            + $"({capture / 15.0:F1} ms/partido con captura)");
    }

    // ── Aleatoriedad inyectable y reproducible (F3-01) ─────────

    static string ResultFingerprint(GameSimulator.GameResult r)
    {
        string Players(IEnumerable<GameSimulator.PlayerStatSnapshot> players) => string.Join(";",
            players.OrderBy(p => p.player_id).Select(p =>
                $"{p.player_id}:{p.minutes:F3}:{p.points}:{p.fgm}:{p.fga}:{p.fg3m}:{p.fg3a}:"
                + $"{p.ftm}:{p.fta}:{p.oreb}:{p.dreb}:{p.assists}:{p.steals}:{p.blocks}:"
                + $"{p.turnovers}:{p.pf}:{p.target_minutes}"));

        string log = r.playByPlay == null
            ? "sin-crónica"
            : string.Join(";", r.playByPlay.Select(e =>
                $"{e.quarter}:{e.text}:{e.homeScore}:{e.awayScore}:{e.timeElapsed:F3}"));
        return $"{r.home_score}-{r.away_score}|{string.Join(",", r.quarters.Select(q => $"{q.home}-{q.away}"))}|"
            + $"{Players(r.home_stats)}|{Players(r.away_stats)}|{log}";
    }

    [Test]
    public void SimulateGame_MismaSemillaYEntradas_ProduceElMismoResultado()
    {
        const ulong seed = 0x5EED2026UL;
        var first = GameSimulator.SimulateGame(FakeGame(), AutoRoster(1), AutoRoster(100),
            persistToDb: false, capturePlayByPlay: true, random: new SeededRandomSource(seed));
        var second = GameSimulator.SimulateGame(FakeGame(), AutoRoster(1), AutoRoster(100),
            persistToDb: false, capturePlayByPlay: true, random: new SeededRandomSource(seed));

        Assert.That(ResultFingerprint(second), Is.EqualTo(ResultFingerprint(first)),
            "misma semilla + mismas entradas deben reproducir marcador, box score, cuartos y crónica");
    }

    [Test]
    public void SeededRandomSource_RepiteLaSecuenciaIncluyendoLosRangos()
    {
        var first = new SeededRandomSource(123456789UL);
        var second = new SeededRandomSource(123456789UL);

        for (int i = 0; i < 100; i++)
        {
            Assert.That(second.Value, Is.EqualTo(first.Value));
            Assert.That(second.Range(-7, 19), Is.EqualTo(first.Range(-7, 19)));
            Assert.That(second.Range(-2.5f, 3.5f), Is.EqualTo(first.Range(-2.5f, 3.5f)));
        }
    }

    // ── Rotación estratégica automática (F2-11) ──────────────

    static GameSimulator.PlayerStatSnapshot AutoSnap(int id, PlayerRole role, int ovr, int fisico = 100)
        => new()
        {
            player_id = id,
            name = "J" + id,
            position = "PG",
            secondary_position = "SG",
            role = role,
            overall = ovr,
            fisico = fisico,
            target_minutes = -1,
            fixed_minutes = false
        };

    /// <summary>12 convocados tipo: 1 estrella, 4 titulares, 6 banquillo y un último recurso.</summary>
    static List<GameSimulator.PlayerStatSnapshot> AutoTeam()
    {
        var list = new List<GameSimulator.PlayerStatSnapshot> { AutoSnap(1, PlayerRole.Estrella, 93) };
        for (int i = 2; i <= 5; i++) list.Add(AutoSnap(i, PlayerRole.Titular, 85));
        for (int i = 6; i <= 11; i++) list.Add(AutoSnap(i, PlayerRole.Banquillo, 78));
        list.Add(AutoSnap(12, PlayerRole.UltimoRecurso, 70));
        return list;
    }

    [Test]
    public void RotacionAutomatica_ReparteExactamenteLos240()
    {
        var ps = AutoTeam();
        GameSimulator.PrepareRotationMetadata(ps);

        Assert.That(ps.Sum(p => p.target_minutes), Is.EqualTo(240),
            "el reparto automático cuadra los 240 minutos del partido");
        Assert.That(ps.TrueForAll(p => p.planned), "el plan es vinculante para el simulador");
        Assert.That(ps.TrueForAll(p => p.target_minutes > 0), "nadie se queda sin minutos por defecto");
    }

    [Test]
    public void RotacionAutomatica_RespetaLaPromesaDelRol()
    {
        var ps = AutoTeam();
        GameSimulator.PrepareRotationMetadata(ps);

        var estrella = ps[0];
        var titular = ps[1];
        var banquillo = ps[5];
        var ultimo = ps[11];

        Assert.That(estrella.target_minutes, Is.GreaterThan(titular.target_minutes));
        Assert.That(titular.target_minutes, Is.GreaterThan(banquillo.target_minutes));
        Assert.That(banquillo.target_minutes, Is.GreaterThanOrEqualTo(ultimo.target_minutes));
        Assert.That(estrella.target_minutes, Is.GreaterThanOrEqualTo(32),
            "una estrella no baja de 32 minutos");
        Assert.That(ultimo.target_minutes, Is.LessThanOrEqualTo(4),
            "el último recurso apenas juega");
    }

    [Test]
    public void RotacionAutomatica_CuentaLaFormaFisica()
    {
        var fresco = AutoSnap(1, PlayerRole.Titular, 85, fisico: 100);
        var cansado = AutoSnap(2, PlayerRole.Titular, 85, fisico: 20);

        Assert.That(GameSimulator.GetAutomaticMinutes(cansado),
            Is.LessThan(GameSimulator.GetAutomaticMinutes(fresco)),
            "el cansancio baja el ideal del jugador");

        var ps = new List<GameSimulator.PlayerStatSnapshot> { fresco, cansado };
        for (int i = 3; i <= 12; i++) ps.Add(AutoSnap(i, PlayerRole.Banquillo, 78, fisico: 100));
        GameSimulator.PrepareRotationMetadata(ps);

        Assert.That(ps[1].target_minutes, Is.LessThan(ps[0].target_minutes),
            "el cansado recibe menos minutos del reparto");
    }

    [Test]
    public void RotacionAutomatica_GLeagueMantieneLaHeuristica()
    {
        var ps = AutoTeam();
        GameSimulator.PrepareRotationMetadata(ps, glLeague: true);

        Assert.That(ps.TrueForAll(p => !p.planned), "la G-League no usa el plan estratégico");
        Assert.That(ps[0].target_minutes,
            Is.EqualTo(GameSimulator.GetTargetMinutes(ps[0], glLeague: true)),
            "la G-League conserva la heurística anterior");
    }

    [Test]
    public void RotacionAutomatica_ElPartidoRespetaElPlan()
    {
        var home = AutoRoster(1);
        var away = AutoRoster(100);
        var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);

        float total = r.home_stats.Sum(p => p.minutes);
        // 5 en pista × 48 minutos = 240 (265 con prórroga). La fase final añade
        // hasta 3 × (5 jugadores × 12/44 min por posesión) de jugador-minutos; si un
        // equipo se queda con cuatro por faltas, la suma baja en la proporción jugada.
        float lateExtensionAllowance = r.possessions.lateExtensionPossessions * 5f * 12f / 44f;
        bool plausible = Enumerable.Range(0, 11)
            .Any(ot => System.Math.Abs(total - (240f + 25f * ot)) <= 3f + lateExtensionAllowance);
        Assert.That(plausible, Is.True, $"minutos jugados: {total}");

        var estrella = r.home_stats.First(s => s.player_id == 1);
        var fondo = r.home_stats.First(s => s.player_id == 11);
        Assert.That(estrella.minutes, Is.GreaterThan(fondo.minutes),
            "la estrella juega más que el fondo del banquillo");
    }

    [Test]
    public void RotacionAutomatica_Medicion()
    {
        var home = AutoRoster(1);
        var away = AutoRoster(100);

        var ps = home.Select(p => new GameSimulator.PlayerStatSnapshot
        {
            player_id = p.id,
            role = p.role,
            overall = p.overall,
            fisico = p.fisico,
            target_minutes = p.rotation_minutes,
            fixed_minutes = p.rotation_minutes >= 0
        }).ToList();
        GameSimulator.PrepareRotationMetadata(ps);
        // Lo que pedía la heurística antigua (OVR a secas) frente al reparto de 240.
        int antes = ps.Sum(p => GameSimulator.GetTargetMinutes(p));

        float minTotal = float.MaxValue, maxTotal = float.MinValue;
        var avgByRole = new Dictionary<PlayerRole, float>();
        const int games = 10;
        for (int g = 0; g < games; g++)
        {
            var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
            float total = r.home_stats.Sum(s => s.minutes);
            minTotal = System.Math.Min(minTotal, total);
            maxTotal = System.Math.Max(maxTotal, total);
            foreach (var s in r.home_stats)
                avgByRole[s.role] = avgByRole.TryGetValue(s.role, out var v) ? v + s.minutes : s.minutes;
        }

        string Avg(PlayerRole role)
        {
            int count = home.Count(p => p.role == role);
            float total = avgByRole.TryGetValue(role, out var v) ? v : 0f;
            return (count > 0 ? total / games / count : 0f).ToString("F1");
        }

        TestContext.WriteLine(
            $"Plan de 240 por jugador: estrella {Avg(PlayerRole.Estrella)} · titular {Avg(PlayerRole.Titular)} · "
            + $"banquillo {Avg(PlayerRole.Banquillo)} (media de {games} partidos) | "
            + $"antes la IA pedía {antes} min de 240 | suma jugada {minTotal:F0}–{maxTotal:F0}");
    }

    [Test]
    public void SimulateGame_FullRosters_AlwaysProducesWinner()
    {
        var home = AutoRoster(1);
        var away = AutoRoster(100);
        for (int i = 0; i < 60; i++)
        {
            var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
            Assert.That(r.home_score, Is.Not.EqualTo(r.away_score));
        }
    }

    [Test]
    public void PrepareRotationMetadata_AllZeroPlans_FallsBackToAutomatic()
    {
        var ps = new List<GameSimulator.PlayerStatSnapshot>
        {
            Plan("PG", "SG", PlayerRole.Estrella,   90, 0, true),
            Plan("SG", "SF", PlayerRole.Titular,    80, 0, true),
            Plan("SF", "PF", PlayerRole.Banquillo,  75, 0, false),
            Plan("PF", "C",  PlayerRole.Banquillo,  75, 0, false),
            Plan("C",  "PF", PlayerRole.Banquillo,  75, 0, false),
            Plan("PG", "SG", PlayerRole.Banquillo,  75, 0, false)
        };

        GameSimulator.PrepareRotationMetadata(ps);

        Assert.That(ps.TrueForAll(p => !p.fixed_minutes), "Con todo a 0 la rotación vuelve a automática");
        Assert.That(ps.TrueForAll(p => p.target_minutes > 0), "La heurística debe asignar minutos");
    }

    [Test]
    public void InitialOnCourt_Strict_SkipsZeroMinuteStarter()
    {
        var ps = new List<GameSimulator.PlayerStatSnapshot>
        {
            Plan("PG", "SG", PlayerRole.Estrella,  90,  0, true),  // titular con plan 0: no debe empezar
            Plan("SG", "SF", PlayerRole.Titular,   85, 30, true),
            Plan("SF", "PF", PlayerRole.Titular,   84, 30, true),
            Plan("PF", "C",  PlayerRole.Titular,   83, 30, true),
            Plan("C",  "PF", PlayerRole.Titular,   82, 30, true),
            Plan("PG", "SG", PlayerRole.Banquillo, 78, 20, false)
        };

        var on = GameSimulator.InitialOnCourt(ps, strict: true);

        Assert.That(on.Count, Is.EqualTo(5));
        Assert.That(on.Contains(0), Is.False, "Un titular con plan 0 no debe empezar");
        Assert.That(on.Contains(5), Is.True, "Debe entrar el siguiente con minutos");
    }

    [Test]
    public void InitialOnCourt_NonStrict_TakesFirstFive()
    {
        var ps = new List<GameSimulator.PlayerStatSnapshot>();
        for (int i = 0; i < 8; i++)
            ps.Add(Plan("PG", "SG", PlayerRole.Banquillo, 80, -1, i < 5));

        var on = GameSimulator.InitialOnCourt(ps, strict: false);

        Assert.That(on, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }));
    }

    // ── Posesiones y rebote ofensivo (F2-01) ─────────────────────

    static GameSimulator.PossessionOutcome OffRebound() =>
        new() { pts = 0, desc = "fallo con rebote ofensivo", offensiveRebound = true };

    static GameSimulator.PossessionOutcome PossessionEnds(int pts = 0) =>
        new() { pts = pts, desc = "posesión terminada", offensiveRebound = false };

    [Test]
    public void RunPossessionChain_OffensiveRebound_KeepsTheSameTeamAttacking()
    {
        // Guion: fallo con rebote ofensivo y, después, canasta del mismo equipo.
        var script = new Queue<GameSimulator.PossessionOutcome>(
            new[] { OffRebound(), PossessionEnds(2) });
        var attackers = new List<string>();
        int points = 0;

        int attacks = GameSimulator.RunPossessionChain(() =>
        {
            var outcome = script.Dequeue();
            attackers.Add("equipo_ofensivo");
            points += outcome.pts;
            return outcome;
        });

        Assert.That(attacks, Is.EqualTo(2), "El rebote ofensivo da una segunda oportunidad en la misma posesión");
        Assert.That(points, Is.EqualTo(2));
        Assert.That(script, Is.Empty, "La posesión termina con la canasta");
        Assert.That(attackers, Is.All.EqualTo("equipo_ofensivo"), "Ambos ataques son del mismo equipo");
    }

    [Test]
    public void RunPossessionChain_DefensiveRebound_EndsThePossession()
    {
        var script = new Queue<GameSimulator.PossessionOutcome>(
            new[] { PossessionEnds(), PossessionEnds(3) });

        int attacks = GameSimulator.RunPossessionChain(() => script.Dequeue());

        Assert.That(attacks, Is.EqualTo(1), "Sin rebote ofensivo la posesión acaba y cambia el balón");
        Assert.That(script.Count, Is.EqualTo(1), "No debe consumirse el ataque siguiente");
    }

    [Test]
    public void RunPossessionChain_StopsAtTheAttackCap()
    {
        int calls = 0;

        int attacks = GameSimulator.RunPossessionChain(() =>
        {
            calls++;
            return OffRebound();
        });

        Assert.That(attacks, Is.EqualTo(GameSimulator.MAX_ATTACKS_PER_POSSESSION));
        Assert.That(calls, Is.EqualTo(GameSimulator.MAX_ATTACKS_PER_POSSESSION),
            "El tope evita una posesión sin fin por rebotes ofensivos encadenados");
    }

    [Test]
    public void SimulateGame_CountsPossessionsAndAttacksSeparately()
    {
        var home = AutoRoster(1);
        var away = AutoRoster(100);

        var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
        var pos = r.possessions;

        Assert.That(pos, Is.Not.Null);
        Assert.That(pos.homePossessions + pos.awayPossessions, Is.GreaterThan(0));
        Assert.That(pos.homeAttacks, Is.GreaterThanOrEqualTo(pos.homePossessions),
            "Cada posesión usa al menos un ataque");
        Assert.That(pos.awayAttacks, Is.GreaterThanOrEqualTo(pos.awayPossessions));
        Assert.That(pos.homeAttacks, Is.LessThanOrEqualTo(pos.homePossessions * GameSimulator.MAX_ATTACKS_PER_POSSESSION));
        Assert.That(pos.awayAttacks, Is.LessThanOrEqualTo(pos.awayPossessions * GameSimulator.MAX_ATTACKS_PER_POSSESSION));
        Assert.That(pos.HomeSecondChanceAttacks + pos.AwaySecondChanceAttacks, Is.GreaterThan(0),
            "Un partido con ~200 posesiones deja segundas oportunidades por rebote ofensivo");
    }

    // ── Alternancia de posesiones (F2-02) ────────────────────────

    [Test]
    public void PossessionState_BeginPeriod_AlternatesTheTipOff()
    {
        var state = new GameSimulator.PossessionState();

        state.BeginPeriod(1);
        Assert.That(state.HomeOnOffense, Is.True, "Q1 saca el local");
        state.BeginPeriod(2);
        Assert.That(state.HomeOnOffense, Is.False, "Q2 saca el visitante");
        state.BeginPeriod(3);
        Assert.That(state.HomeOnOffense, Is.True, "Q3 vuelve a sacar el local");
        state.BeginPeriod(4);
        Assert.That(state.HomeOnOffense, Is.False, "Q4 saca el visitante");
        state.BeginPeriod(5);
        Assert.That(state.HomeOnOffense, Is.True, "La primera prórroga sigue la alternancia");
        state.BeginPeriod(6);
        Assert.That(state.HomeOnOffense, Is.False);
    }

    [Test]
    public void PossessionState_NextPossession_HandsTheBallToTheOtherTeam()
    {
        // Dentro del periodo no hay azar: cada posesión cerrada cambia el atacante.
        var state = new GameSimulator.PossessionState();
        state.BeginPeriod(1);

        var attackers = new List<bool> { state.HomeOnOffense };
        for (int i = 0; i < 5; i++)
        {
            state.NextPossession();
            attackers.Add(state.HomeOnOffense);
        }

        Assert.That(attackers, Is.EqualTo(new[] { true, false, true, false, true, false }),
            "El ataque alterna posesión a posesión sin ninguna moneda");
    }

    [Test]
    public void SimulateGame_PossessionsAreBalanced_BetweenBothTeams()
    {
        var home = AutoRoster(1);
        var away = AutoRoster(100);
        int total = 0, maxDiff = 0;

        for (int i = 0; i < 10; i++)
        {
            var pos = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false).possessions;

            int diff = Math.Abs(pos.homePossessions - pos.awayPossessions);
            maxDiff = Math.Max(maxDiff, diff);
            Assert.That(diff, Is.LessThanOrEqualTo(pos.lateExtensionPossessions),
                $"Diferencia mayor que las posesiones de fase final: {pos.homePossessions} vs {pos.awayPossessions} "
                + $"(fase final: {pos.lateExtensionPossessions})");
            total += pos.homePossessions + pos.awayPossessions;
        }

        int avg = total / 10;
        TestContext.WriteLine($"Posesiones por partido (media de 10): {avg} · diferencia máxima: {maxDiff} "
            + "(la fase final puede desequilibrarlas hasta en 3)");
        Assert.That(avg, Is.InRange(150, 260), $"Posesiones por partido fuera de rango: {avg}");
    }

    [Test]
    public void SimulateGame_FaseFinal_AlargaSoloLosPartidosAbiertos()
    {
        // F3-05: al cerrar el 4.º cuarto con el partido abierto (1-6 puntos de marcador)
        // la fase final añade posesiones alternando al que va por detrás y al líder; si
        // empata, el partido va a prórroga. Nunca corre en un partido ya decidido.
        var home = AutoRoster(1);
        var away = AutoRoster(100);
        int conFaseFinal = 0, conProrroga = 0;

        for (int i = 1; i <= 120; i++)
        {
            var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false,
                random: new SeededRandomSource((ulong)i));

            int regHome = 0, regAway = 0;
            for (int q = 0; q < 4 && q < r.quarters.Count; q++)
            {
                regHome += r.quarters[q].home;
                regAway += r.quarters[q].away;
            }
            int margin = Math.Abs(regHome - regAway);
            int extra = r.possessions.lateExtensionPossessions;

            if (extra > 0)
            {
                conFaseFinal++;
                // Solo con el partido abierto: al terminar la fase el margen no puede
                // haberse alejado más allá de 6 puntos + un tiro (máx. 3).
                Assert.That(margin, Is.LessThanOrEqualTo(GameSimulator.LATE_EXTENSION_MAX_MARGIN + 3),
                    $"Fase final en un partido decidido (partida {i}): {regHome}-{regAway}");
                Assert.That(extra,
                    Is.LessThanOrEqualTo(GameSimulator.LATE_EXTENSION_MAX_POSS),
                    $"Demasiadas posesiones extra en la partida {i}: {extra}");
            }

            if (r.quarters.Count > 4)
            {
                conProrroga++;
                Assert.That(regHome, Is.EqualTo(regAway),
                    $"Prórroga sin empate al final del reglamento (partida {i})");
            }
        }

        TestContext.WriteLine($"Fase final en {conFaseFinal}/120 partidos · prórrogas: {conProrroga}/120");
        Assert.That(conFaseFinal, Is.GreaterThan(0), "La fase final no se ha activado en ningún partido");
        Assert.That(conProrroga, Is.GreaterThan(0), "Ningún partido llegó a prórroga");
    }

    [Test]
    public void SimulateGame_PointsPerPossession_StaysInRange()
    {
        var home = AutoRoster(1);
        var away = AutoRoster(100);
        int points = 0, possessions = 0;

        for (int i = 0; i < 30; i++)
        {
            var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
            points += r.home_score + r.away_score;
            possessions += r.possessions.homePossessions + r.possessions.awayPossessions;
        }

        float ppp = (float)points / possessions;
        TestContext.WriteLine($"PPP: {ppp:0.000} ({points} pts / {possessions} pos)");
        Assert.That(ppp, Is.InRange(0.85f, 1.35f),
            $"Puntos por posesión fuera de rango realista: {ppp:0.000} ({points} pts / {possessions} pos)");
    }

    [Test]
    public void SimulateGame_SecondChanceAttacks_DoNotExceedOffensiveRebounds()
    {
        var home = AutoRoster(1);
        var away = AutoRoster(100);

        var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
        int oreb = r.home_stats.Sum(p => p.oreb) + r.away_stats.Sum(p => p.oreb);
        var pos = r.possessions;

        Assert.That(pos.HomeSecondChanceAttacks + pos.AwaySecondChanceAttacks, Is.LessThanOrEqualTo(oreb),
            "Cada segunda oportunidad sale de un rebote ofensivo contabilizado en el box score");
    }

    // ── Faltas, tiros libres y expulsión (F2-03) ─────────────────

    static GameSimulator.PlayerStatSnapshot Snap(string name, string pos, string sec, int ovr, int plan = 20)
        => new()
        {
            name = name,
            position = pos,
            secondary_position = sec,
            overall = ovr,
            target_minutes = plan,
            fixed_minutes = false
        };

    [Test]
    public void DefenseFouls_GivesFreeThrowsFromTheFourthTeamFoul()
    {
        var state = new GameSimulator.FoulState();
        state.BeginPeriod(1);
        var defense = new GameSimulator.DefenseFouls(state, defenseIsHome: true, clutch: false);

        var bonus = new List<bool>();
        for (int i = 0; i < 5; i++) bonus.Add(defense.RegisterFoul());

        Assert.That(bonus, Is.EqualTo(new[] { false, false, false, true, true }),
            "La 4.ª falta de equipo del periodo manda al rival a la línea");
        Assert.That(state.TeamFouls(true), Is.EqualTo(5));
        Assert.That(state.TeamFouls(false), Is.EqualTo(0), "Las faltas se cuentan por equipo");
    }

    [Test]
    public void DefenseFouls_InClutchTheBonusArrivesOnTheSecondFoul()
    {
        var state = new GameSimulator.FoulState();
        state.BeginPeriod(4);
        var defense = new GameSimulator.DefenseFouls(state, defenseIsHome: false, clutch: true);

        Assert.That(defense.RegisterFoul(), Is.False);
        Assert.That(defense.RegisterFoul(), Is.True, "En los últimos 2 minutos la 2.ª falta ya da libres");
    }

    [Test]
    public void FoulState_TeamFouls_ResetEveryPeriod()
    {
        var state = new GameSimulator.FoulState();
        state.BeginPeriod(1);
        for (int i = 0; i < 6; i++) state.AddTeamFoul(false);

        state.BeginPeriod(2);

        Assert.That(state.TeamFouls(false), Is.EqualTo(0));
        Assert.That(state.TeamFouls(true), Is.EqualTo(0));
    }

    [Test]
    public void IsClutch_CoversTheLastTwoMinutesOfTheFourthAndOvertime()
    {
        Assert.That(GameSimulator.IsClutch(3, 11.9f), Is.False);
        Assert.That(GameSimulator.IsClutch(4, 9.5f), Is.False);
        Assert.That(GameSimulator.IsClutch(4, 10f), Is.True);
        Assert.That(GameSimulator.IsClutch(5, 2.9f), Is.False);
        Assert.That(GameSimulator.IsClutch(5, 3f), Is.True);
    }

    [Test]
    public void ShootFreeThrows_LastMissLeavesTheBallLive()
    {
        var shooter = new GameSimulator.PlayerStatSnapshot { name = "Tirador", overall = 70 };

        for (int i = 0; i < 200; i++)
        {
            int before = shooter.ftm;
            int made = GameSimulator.ShootFreeThrows(shooter, 1, out bool live);

            Assert.That(made, Is.EqualTo(shooter.ftm - before));
            Assert.That(live, Is.EqualTo(made == 0), "Con un solo libre, solo queda vivo si se falla");
        }
        Assert.That(shooter.fta, Is.EqualTo(200), "Cada intento cuenta en el box score");
    }

    [Test]
    public void ShootFreeThrows_ThreeShots_NeverLeaveTheBallLiveIfAllGoIn()
    {
        var shooter = new GameSimulator.PlayerStatSnapshot { name = "Tirador", overall = 95 };

        for (int i = 0; i < 100; i++)
        {
            int made = GameSimulator.ShootFreeThrows(shooter, 3, out bool live);
            Assert.That(made, Is.InRange(0, 3));
            if (made == 3) Assert.That(live, Is.False, "Si el último entra, el balón muere");
        }
    }

    [Test]
    public void EjectFouledOut_SixFoulsBringTheBestBenchReplacement()
    {
        var ps = new List<GameSimulator.PlayerStatSnapshot>
        {
            Snap("Base expulsado", "PG", "SG", 80, 30),
            Snap("Escolta", "SG", "PG", 80),
            Snap("Alero", "SF", "PF", 80),
            Snap("Ala-pívot", "PF", "C", 80),
            Snap("Pívot", "C", "PF", 80),
            Snap("Base suplente", "PG", "SG", 70),
            Snap("Alero suplente", "SF", "PF", 88)
        };
        ps[0].pf = GameSimulator.FoulState.PERSONAL_FOUL_LIMIT;
        var on = new HashSet<int> { 0, 1, 2, 3, 4 };

        var ejected = GameSimulator.EjectFouledOut(on, ps);

        Assert.That(ejected, Is.EqualTo(new[] { "Base expulsado" }));
        Assert.That(on.Contains(0), Is.False, "Quien llega a seis faltas deja la pista");
        Assert.That(on.Count, Is.EqualTo(5), "Entra un suplente");
        Assert.That(on.Contains(5), Is.True, "Gana el encaje posicional, no el mejor OVR");
    }

    [Test]
    public void EjectFouledOut_WithoutBench_LeavesTheTeamWithFour()
    {
        var ps = new List<GameSimulator.PlayerStatSnapshot>
        {
            Snap("Uno", "PG", "SG", 80),
            Snap("Dos", "SG", "PG", 80),
            Snap("Tres", "SF", "PF", 80),
            Snap("Cuatro", "PF", "C", 80),
            Snap("Cinco", "C", "PF", 80)
        };
        ps[3].pf = GameSimulator.FoulState.PERSONAL_FOUL_LIMIT;
        var on = new HashSet<int> { 0, 1, 2, 3, 4 };

        var ejected = GameSimulator.EjectFouledOut(on, ps);

        Assert.That(ejected.Count, Is.EqualTo(1));
        Assert.That(on.Count, Is.EqualTo(4), "Sin banca no hay relevo: el equipo sigue con cuatro");
    }

    [Test]
    public void FoulWeight_PrefersInteriorsAndDiscouragesFoulTrouble()
    {
        float center = GameSimulator.FoulWeight(Snap("Pívot", "C", "PF", 80));
        float guard = GameSimulator.FoulWeight(Snap("Base", "PG", "SG", 80));
        var inTrouble = Snap("Pívot", "C", "PF", 80);
        inTrouble.pf = 5;
        var fouledOut = Snap("Pívot", "C", "PF", 80);
        fouledOut.pf = GameSimulator.FoulState.PERSONAL_FOUL_LIMIT;

        Assert.That(center, Is.GreaterThan(guard), "Los interiores cometen más faltas");
        Assert.That(GameSimulator.FoulWeight(inTrouble), Is.LessThan(center),
            "Quien está en problemas de faltas defiende con más cuidado");
        Assert.That(GameSimulator.FoulWeight(fouledOut), Is.EqualTo(0f), "El expulsado ya no puede faltar");
    }

    [Test]
    public void SimulateGame_NobodyExceedsSixPersonalFouls()
    {
        var home = AutoRoster(1);
        var away = AutoRoster(100);
        int fta = 0, pf = 0, foulOuts = 0;

        for (int i = 0; i < 10; i++)
        {
            var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
            var all = r.home_stats.Concat(r.away_stats).ToList();

            Assert.That(all.TrueForAll(p => p.pf <= GameSimulator.FoulState.PERSONAL_FOUL_LIMIT),
                "Al llegar a la sexta falta el jugador sale de la pista");
            Assert.That(all.TrueForAll(p => p.fta >= p.ftm), "No se puede anotar más libres de los intentados");

            foulOuts += all.Count(p => p.pf == GameSimulator.FoulState.PERSONAL_FOUL_LIMIT);
            fta += r.home_team_stats.fta + r.away_team_stats.fta;
            pf += r.home_team_stats.pf + r.away_team_stats.pf;
        }

        TestContext.WriteLine($"TL intentados por partido: {fta / 10} · faltas personales: {pf / 10} · expulsiones: {foulOuts}");
        Assert.That(fta, Is.GreaterThan(0), "Debe haber tiros libres");
        Assert.That(pf, Is.GreaterThan(0), "Debe haber faltas personales");
    }

    // ── Atributos con efecto propio (F2-04) ──────────────────────

    static List<PlayerData> RosterWith(int baseId, Action<PlayerData> tweak, int size = 12)
    {
        var roster = AutoRoster(baseId, size);
        foreach (var p in roster) tweak(p);
        return roster;
    }

    static float TwoPointPct(GameSimulator.GameResult r)
    {
        var stats = r.home_stats.Concat(r.away_stats).ToList();
        float made = stats.Sum(p => p.fgm - p.fg3m);
        float attempted = stats.Sum(p => p.fga - p.fg3a);
        return attempted <= 0 ? 0f : made / attempted;
    }

    static List<GameSimulator.PlayerStatSnapshot> SnapRoster(int speed)
    {
        var list = new List<GameSimulator.PlayerStatSnapshot>();
        for (int i = 0; i < 5; i++)
        {
            var snap = Snap("J" + i, "PG", "SG", 80);
            snap.speed = speed;
            list.Add(snap);
        }
        return list;
    }

    [Test]
    public void SpeedPaceDelta_MovesTheTempo()
    {
        var fast = SnapRoster(90);
        var slow = SnapRoster(50);
        var neutral = SnapRoster(70);

        Assert.That(GameSimulator.SpeedPaceDelta(fast, fast), Is.EqualTo(3f).Within(0.001f));
        Assert.That(GameSimulator.SpeedPaceDelta(slow, slow), Is.EqualTo(-3f).Within(0.001f));
        Assert.That(GameSimulator.SpeedPaceDelta(neutral, neutral), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void SimulateGame_FasterTeams_PlayMorePossessions()
    {
        var fastHome = RosterWith(1, p => p.speed = 90);
        var fastAway = RosterWith(100, p => p.speed = 90);
        var slowHome = RosterWith(1, p => p.speed = 45);
        var slowAway = RosterWith(100, p => p.speed = 45);
        int fast = 0, slow = 0;

        for (int i = 0; i < 20; i++)
        {
            fast += GameSimulator.SimulateGame(FakeGame(), fastHome, fastAway, persistToDb: false).possessions.homePossessions;
            slow += GameSimulator.SimulateGame(FakeGame(), slowHome, slowAway, persistToDb: false).possessions.homePossessions;
        }

        TestContext.WriteLine($"Posesiones por equipo: velocidad 90 = {fast / 20f:0.0} · velocidad 45 = {slow / 20f:0.0}");
        Assert.That(fast, Is.GreaterThan(slow), "La velocidad sube el ritmo: más posesiones");
    }

    [Test]
    public void TurnoverChance_DropsWithBallHandling()
    {
        float slick = GameSimulator.TurnoverChance(80, 80, ballSecurity: 90f);
        float neutral = GameSimulator.TurnoverChance(80, 80, ballSecurity: 70f);
        float sloppy = GameSimulator.TurnoverChance(80, 80, ballSecurity: 50f);

        Assert.That(slick, Is.LessThan(neutral));
        Assert.That(sloppy, Is.GreaterThan(neutral));
        Assert.That(GameSimulator.TurnoverChance(80, 80, 0f), Is.LessThanOrEqualTo(0.30f),
            "La probabilidad queda acotada");
    }

    [Test]
    public void BallSecurity_UsesTheBallHandlers()
    {
        var court = new List<GameSimulator.PlayerStatSnapshot>
        {
            Snap("Base", "PG", "SG", 80), Snap("Escolta", "SG", "PG", 80),
            Snap("Alero", "SF", "PF", 80), Snap("Ala-pívot", "PF", "C", 80), Snap("Pívot", "C", "PF", 80)
        };
        court[0].dribbling = 95; court[1].dribbling = 85; court[2].dribbling = 75;
        court[3].dribbling = 10; court[4].dribbling = 10;

        Assert.That(GameSimulator.BallSecurity(court), Is.EqualTo(85f).Within(0.01f),
            "El bote de los interiores no protege el balón");
    }

    [Test]
    public void SimulateGame_GoodBallHandling_LosesFewerTurnovers()
    {
        var slick = RosterWith(1, p => p.dribbling = 95);
        var sloppy = RosterWith(1, p => p.dribbling = 45);
        var away = AutoRoster(100);
        int slickTo = 0, sloppyTo = 0;

        for (int i = 0; i < 20; i++)
        {
            slickTo += GameSimulator.SimulateGame(FakeGame(), slick, away, persistToDb: false).home_team_stats.turnovers;
            sloppyTo += GameSimulator.SimulateGame(FakeGame(), sloppy, away, persistToDb: false).home_team_stats.turnovers;
        }

        TestContext.WriteLine($"Pérdidas: bote 95 = {slickTo / 20f:0.0} · bote 45 = {sloppyTo / 20f:0.0}");
        Assert.That(slickTo, Is.LessThan(sloppyTo), "Mejor bote = menos pérdidas");
    }

    [Test]
    public void Athleticism_ImprovesFinishingAndReboundWeight()
    {
        Assert.That(GameSimulator.FinishingBonus(90), Is.GreaterThan(GameSimulator.FinishingBonus(70)));
        Assert.That(GameSimulator.FinishingBonus(50), Is.LessThan(0f));
        Assert.That(GameSimulator.ReboundAthleticismFactor(95),
            Is.GreaterThan(GameSimulator.ReboundAthleticismFactor(60)));
        Assert.That(GameSimulator.ReboundAthleticismFactor(1), Is.GreaterThanOrEqualTo(0.7f));
        Assert.That(GameSimulator.ReboundAthleticismFactor(99), Is.LessThanOrEqualTo(1.4f));
    }

    [Test]
    public void SimulateGame_MoreAthleticism_FinishesBetterAtTheRim()
    {
        var athletic = RosterWith(1, p => p.athleticism = 95);
        var heavy = RosterWith(1, p => p.athleticism = 40);
        var away = AutoRoster(100);
        float athleticPct = 0f, heavyPct = 0f;

        for (int i = 0; i < 30; i++)
        {
            athleticPct += TwoPointPct(GameSimulator.SimulateGame(FakeGame(), athletic, away, persistToDb: false));
            heavyPct += TwoPointPct(GameSimulator.SimulateGame(FakeGame(), heavy, away, persistToDb: false));
        }

        TestContext.WriteLine($"TC de dos: atletismo 95 = {athleticPct / 30f:0.000} · atletismo 40 = {heavyPct / 30f:0.000}");
        Assert.That(athleticPct, Is.GreaterThan(heavyPct), "El atletismo mejora la finalización");
    }

    [Test]
    public void ShotType_HighIqShootsMoreFromHisStrength()
    {
        var goodShooter3 = new GameSimulator.PlayerStatSnapshot
        { position = "SG", three_point = 90, shooting = 55, iq = 95, fisico = 99 };
        var sameShooterLowIq = new GameSimulator.PlayerStatSnapshot
        { position = "SG", three_point = 90, shooting = 55, iq = 20, fisico = 99 };

        int highIq = 0, lowIq = 0;
        var highIqRandom = new SeededRandomSource(1111);
        var lowIqRandom = new SeededRandomSource(2222);
        for (int i = 0; i < 2000; i++)
        {
            if (GameSimulator.ShotType(goodShooter3, random: highIqRandom) == "3") highIq++;
            if (GameSimulator.ShotType(sameShooterLowIq, random: lowIqRandom) == "3") lowIq++;
        }

        Assert.That(highIq, Is.GreaterThan(lowIq),
            "Con el triple como punto fuerte, el IQ alto insiste más desde fuera");

        var badShooter3 = new GameSimulator.PlayerStatSnapshot
        { position = "SG", three_point = 55, shooting = 90, iq = 95, fisico = 99 };
        var badShooter3LowIq = new GameSimulator.PlayerStatSnapshot
        { position = "SG", three_point = 55, shooting = 90, iq = 20, fisico = 99 };

        int smart = 0, dumb = 0;
        var smartRandom = new SeededRandomSource(3333);
        var dumbRandom = new SeededRandomSource(4444);
        for (int i = 0; i < 2000; i++)
        {
            if (GameSimulator.ShotType(badShooter3, random: smartRandom) == "3") smart++;
            if (GameSimulator.ShotType(badShooter3LowIq, random: dumbRandom) == "3") dumb++;
        }

        Assert.That(smart, Is.LessThan(dumb),
            "Con el triple como debilidad, el IQ alto busca más el tiro de dos");
    }

    [Test]
    public void SimulateGame_SmartTeams_TakeTheShotThatSuitsThem()
    {
        void Shape(PlayerData p) { p.three_point = 92; p.shooting = 55; }
        var smart = RosterWith(1, p => { Shape(p); p.iq = 95; });
        var careless = RosterWith(1, p => { Shape(p); p.iq = 20; });
        var away = AutoRoster(100);
        int smartThrees = 0, carelessThrees = 0;

        for (int i = 0; i < 30; i++)
        {
            // Semilla fija por partido: la comparación no depende del azar global.
            smartThrees += GameSimulator.SimulateGame(FakeGame(), smart, away,
                persistToDb: false, random: new SeededRandomSource((ulong)(7000 + i))).home_team_stats.fg3a;
            carelessThrees += GameSimulator.SimulateGame(FakeGame(), careless, away,
                persistToDb: false, random: new SeededRandomSource((ulong)(9000 + i))).home_team_stats.fg3a;
        }

        TestContext.WriteLine($"Triples intentados: IQ 95 = {smartThrees / 30f:0.0} · IQ 20 = {carelessThrees / 30f:0.0}");
        Assert.That(smartThrees, Is.GreaterThan(carelessThrees),
            "El IQ alto explota el punto fuerte del tirador");
    }

    // ── Sin pisos artificiales de estadísticas (F2-05) ──────────

    [Test]
    public void SimulateGame_NoStatisticIsRaisedToAnEliteFloor()
    {
        // Plantillas con atributos elite: si existiera el suelo, ningún jugador con
        // ≥ 20 minutos podría acabar por debajo de su mínimo garantizado.
        void Elite(PlayerData p)
        {
            p.passing = 99; p.rebounding = 99; p.steals = 99; p.blocks = 99;
        }
        var home = RosterWith(1, Elite);
        var away = RosterWith(100, Elite);
        int assistsBelow = 0, reboundsBelow = 0, stealsBelow = 0, blocksBelow = 0;

        for (int i = 0; i < 20; i++)
        {
            var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);
            foreach (var p in r.home_stats.Concat(r.away_stats))
            {
                if (p.minutes < 20) continue;
                int share = (int)Math.Round(p.minutes / 48f * 10f);
                if (p.assists < share) assistsBelow++;
                if (p.steals < (int)Math.Round(p.minutes / 48f * 3f)) stealsBelow++;
                if (p.blocks < (int)Math.Round(p.minutes / 48f * 3f
                        * GameSimulator.GetBlockPositionMultiplier(p.position))) blocksBelow++;
                if (p.oreb + p.dreb < (int)Math.Round(p.minutes / 48f * 10f
                        * GameSimulator.GetReboundPositionMultiplier(p.position, false))) reboundsBelow++;
            }
        }

        TestContext.WriteLine($"Por debajo del antiguo suelo: asist {assistsBelow} · rebote {reboundsBelow} · robo {stealsBelow} · tapón {blocksBelow}");
        Assert.That(assistsBelow, Is.GreaterThan(0), "Nadie recibe asistencias regaladas por su pase");
        Assert.That(reboundsBelow, Is.GreaterThan(0), "Nadie recibe rebotes regalados por su atributo");
        Assert.That(stealsBelow, Is.GreaterThan(0), "Nadie recibe robos regalados");
        Assert.That(blocksBelow, Is.GreaterThan(0), "Nadie recibe tapones regalados");
    }

    [Test]
    public void SimulateGame_BoxScoreOnlyCountsRealActions()
    {
        var home = RosterWith(1, p => { p.passing = 99; p.rebounding = 99; p.steals = 99; p.blocks = 99; });
        var away = AutoRoster(100);

        for (int i = 0; i < 10; i++)
        {
            var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);

            foreach (var (team, rival) in new[] { (r.home_stats, r.away_stats), (r.away_stats, r.home_stats) })
            {
                Assert.That(team.Sum(p => p.assists), Is.LessThanOrEqualTo(team.Sum(p => p.fgm)),
                    "No puede haber más asistencias que canastas");
                Assert.That(team.Sum(p => p.blocks), Is.LessThanOrEqualTo(rival.Sum(p => p.fga - p.fgm)),
                    "No puede haber más tapones que fallos del rival");
                Assert.That(team.Sum(p => p.steals), Is.LessThanOrEqualTo(rival.Sum(p => p.turnovers)),
                    "No puede haber más robos que pérdidas del rival");
            }

            int rebounds = r.home_stats.Concat(r.away_stats).Sum(p => p.oreb + p.dreb);
            int misses = r.home_stats.Concat(r.away_stats)
                .Sum(p => (p.fga - p.fgm) + (p.fta - p.ftm));
            Assert.That(rebounds, Is.LessThanOrEqualTo(misses), "Cada rebote sale de un fallo real");
        }
    }

    [Test]
    public void SimulateGame_ScoreMatchesShotBreakdown()
    {
        var home = AutoRoster(1);
        var away = AutoRoster(100);

        var r = GameSimulator.SimulateGame(FakeGame(), home, away, persistToDb: false);

        foreach (var (stats, score) in new[] { (r.home_stats, r.home_score), (r.away_stats, r.away_score) })
        {
            int two = stats.Sum(p => p.fgm - p.fg3m);
            int three = stats.Sum(p => p.fg3m);
            int ft = stats.Sum(p => p.ftm);
            Assert.That(2 * two + 3 * three + ft, Is.EqualTo(score),
                "El marcador debe ser la suma de las acciones del box score");
        }
    }
}
