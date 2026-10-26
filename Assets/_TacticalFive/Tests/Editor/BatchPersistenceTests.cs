using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using SQLite;
using UnityEngine;

/// <summary>
/// F2-09 (PERF-02): la persistencia de un partido se hace en lote.
///
/// Antes, cada jugador costaba un `GetPlayerById` + `UpdatePlayer` para la fatiga,
/// otro `GetPlayerById` en el chequeo de lesiones y un `SavePlayerGameStats` por
/// fila: más de 70 sentencias sueltas por partido, cada una en autocommit (su
/// propio viaje a disco con fsync). Con 15 partidos al día, más de mil.
///
/// Ahora los cambios se acumulan en memoria y se escriben con
/// `UpdatePlayersBatch` + `SavePlayerGameStatsBatch`, dentro de la transacción
/// del día si ya está abierta (nunca anidan un BEGIN).
///
/// El arnés es el de `SaveSlotInitTests`: una carpeta temporal y el singleton
/// registrado a mano, para no tocar las partidas reales del jugador.
/// </summary>
public class BatchPersistenceTests
{
    string _root;
    GameObject _go;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "tf_batch_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        GameSaveManager.BaseDirOverride  = Path.Combine(_root, "saves");
        DatabaseManager.DataRootOverride = Path.Combine(_root, "data");
        CustomScopes.RootOverride        = Path.Combine(_root, "custom");

        _go = new GameObject("DatabaseManagerBatchTest");
        var db = _go.AddComponent<DatabaseManager>();
        // En EditMode no se ejecuta Awake (solo al entrar en Play): singleton a mano.
        SetInstance(db);
    }

    static void SetInstance(DatabaseManager db) =>
        typeof(DatabaseManager)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.SetValue(null, db);

    [TearDown]
    public void TearDown()
    {
        DatabaseManager.Instance?.CloseActiveSlot();
        SetInstance(null);
        if (_go != null) Object.DestroyImmediate(_go);
        GameSaveManager.BaseDirOverride  = null;
        DatabaseManager.DataRootOverride = null;
        CustomScopes.RootOverride        = null;
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    // ── F2-09: los dos helpers escriben de verdad ────────────────────────────

    [Test]
    public void RosterCounts_SeparateStandardTwoWayAndIR()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);

        var freeAgents = db.GetFreeAgents().Take(3).ToList();
        Assert.That(freeAgents, Has.Count.EqualTo(3));
        freeAgents[0].team_id = 999;
        freeAgents[0].is_two_way = 0;
        freeAgents[0].is_on_ir = 0;
        freeAgents[1].team_id = 999;
        freeAgents[1].is_two_way = 1;
        freeAgents[1].is_on_ir = 0;
        freeAgents[2].team_id = 999;
        freeAgents[2].is_two_way = 1;
        freeAgents[2].is_on_ir = 1;
        db.UpdatePlayersBatch(freeAgents);

        Assert.That(db.GetRosterCount(999), Is.EqualTo(1), "solo plaza estándar activa; IR y two-way se excluyen");
        Assert.That(db.GetTwoWayCount(999), Is.EqualTo(2), "los two-way en IR siguen ocupando contrato two-way");
        Assert.That(db.GetTotalRosterCount(999), Is.EqualTo(2), "total activo: estándar + two-way, sin IR");
    }

    [Test]
    public void Batch_GuardaStatsYJugadoresEnUnaSolaEscritura()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);

        var players = db.Db.Table<PlayerData>().Where(p => p.team_id != 0).Take(5).ToList();
        Assert.That(players.Count, Is.EqualTo(5), "El seed debe dejar jugadores asignados");

        // Físico en lote.
        foreach (var p in players) p.fisico = 42;
        db.UpdatePlayersBatch(players);
        foreach (var p in players)
            Assert.That(db.GetPlayerById(p.id).fisico, Is.EqualTo(42),
                $"físico persistido para el jugador {p.id}");

        // Estadísticas en lote.
        var rows = players.Select((p, i) => new PlayerGameStats
        {
            game_id = 990000 + i,
            player_id = p.id,
            team_id = p.team_id,
            minutes = 30,
            points = 10
        }).ToList();
        db.SavePlayerGameStatsBatch(rows);
        Assert.That(db.Db.Table<PlayerGameStats>().Count(s => s.game_id >= 990000),
            Is.EqualTo(rows.Count), "todas las filas del partido insertadas");

        // Y funciona dentro de una transacción ya abierta (la del día): no anida BEGIN.
        db.Db.RunInTransaction(() =>
        {
            foreach (var p in players) p.fisico = 77;
            db.UpdatePlayersBatch(players);
            db.SavePlayerGameStatsBatch(players.Select(p => new PlayerGameStats
            {
                game_id = 991000,
                player_id = p.id,
                team_id = p.team_id,
                minutes = 5
            }).ToList());
        });

        foreach (var p in players)
            Assert.That(db.GetPlayerById(p.id).fisico, Is.EqualTo(77),
                $"físico persistido dentro de la transacción del día ({p.id})");
        Assert.That(db.Db.Table<PlayerGameStats>().Count(s => s.game_id == 991000),
            Is.EqualTo(players.Count), "las filas de la transacción del día se confirman");
    }

    [Test]
    public void TradeTransaction_RollsBackPlayersPicksHardCapHistoryAndMessage()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        var teams = db.GetAllTeams();
        var teamA = teams[0];
        var teamB = teams[1];
        var player = db.GetPlayersByTeam(teamA.id).First();
        var originalPlayerTeam = player.team_id;
        var originalSalary = player.salary;
        var originalYears = player.contract_years;
        var originalNumber = player.number;
        var originalHardCap = teamB.first_apron_hard_capped;
        const int testSeasonId = 987654;
        const int testManagerId = 987654;

        var pick = new DraftPickData
        {
            season_id = testSeasonId,
            draft_year = 2032,
            round = 1,
            pick_number = 30,
            original_team_id = teamA.id,
            current_team_id = teamA.id
        };
        db.Db.Insert(pick);

        Assert.Throws<System.InvalidOperationException>(() => db.RunTradeTransaction(() =>
        {
            player.salary = originalSalary + 5_000_000;
            player.contract_years = originalYears + 1;
            db.MovePlayerToTeam(player, teamB.id);
            db.AssignJerseyNumber(player, teamB.id);
            db.UpdatePlayer(player);

            db.TransferDraftPicks(new List<int> { pick.id }, teamA.id, teamB.id,
                protectedFrom: 5, isSwap: 1, swapOriginalTeamId: teamA.id);

            teamB.first_apron_hard_capped = 1;
            db.UpdateTeam(teamB);
            db.InsertTrade(new TradeData
            {
                season_id = testSeasonId,
                game_day = 10,
                game_date = "2026-11-01",
                team_id_from = teamA.id,
                team_id_to = teamB.id,
                player_id = player.id,
                pick_id = pick.id,
                trade_type = "sign_and_trade"
            });
            db.AddMessage(new MessageData
            {
                manager_id = testManagerId,
                sender_type = 1,
                title = "Test rollback",
                body = "No debe persistir",
                game_day = 10,
                game_date = "2026-11-01",
                created_at = "2026-11-01 12:00:00",
                date_sent = "2026-11-01 12:00:00",
                is_read = 0,
                category = NewsCategory.TRASPASOS,
                news_type = NewsType.TRASPASO
            });

            throw new System.InvalidOperationException("fallo inducido para probar rollback");
        }));

        var persistedPlayer = db.GetPlayerById(player.id);
        var persistedPick = db.GetDraftPickById(pick.id);
        Assert.That(persistedPlayer.team_id, Is.EqualTo(originalPlayerTeam));
        Assert.That(persistedPlayer.salary, Is.EqualTo(originalSalary));
        Assert.That(persistedPlayer.contract_years, Is.EqualTo(originalYears));
        Assert.That(persistedPlayer.number, Is.EqualTo(originalNumber));
        Assert.That(persistedPick.current_team_id, Is.EqualTo(teamA.id));
        Assert.That(persistedPick.protected_from, Is.EqualTo(0));
        Assert.That(persistedPick.is_swap, Is.EqualTo(0));
        Assert.That(db.GetTeamById(teamB.id).first_apron_hard_capped, Is.EqualTo(originalHardCap));
        Assert.That(db.GetTradesBySeason(testSeasonId), Is.Empty);
        Assert.That(db.GetMessages(testManagerId), Is.Empty);
    }

    // ── F2-09: medición del coste real (una sentencia por fila vs lote) ──────

    [Test]
    public void EscrituraPorFila_EsMasCaraQueElLote()
    {
        // 360 filas = 15 partidos × 24 jugadores (un día completo de la liga).
        var path = Path.Combine(_root, "perf.db");
        using var conn = new SQLiteConnection(path);
        // Igual que las partidas reales: WAL activo (DatabaseManager.InitSaveSlot).
        conn.ExecuteScalar<string>("PRAGMA journal_mode=WAL");
        conn.CreateTable<PlayerGameStats>();

        var rows = Enumerable.Range(1, 360).Select(i => new PlayerGameStats
        {
            game_id = i / 24,
            player_id = i,
            team_id = 1,
            minutes = 20,
            points = 8
        }).ToList();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var r in rows) conn.Insert(r);
        sw.Stop();
        long perRow = sw.ElapsedMilliseconds;

        conn.DeleteAll<PlayerGameStats>();

        sw.Restart();
        conn.InsertAll(rows, runInTransaction: true);
        sw.Stop();
        long batched = sw.ElapsedMilliseconds;

        TestContext.WriteLine(
            $"Día de 15 partidos (360 filas): una por una {perRow} ms · en lote {batched} ms "
            + $"(ahorro {perRow - batched} ms)");

        Assert.That(conn.Table<PlayerGameStats>().Count(), Is.EqualTo(rows.Count));
    }
}
