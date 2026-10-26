using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using SQLite;
using UnityEngine;

/// <summary>
/// Regresión del alta de partida en una instalación nueva (máquina recién formateada,
/// sin `template.db`, que hasta ahora solo generaba el Editor).
///
/// `InitSaveSlot` sembraba los datos estáticos dentro de su propia transacción y
/// `SeedPlayers`/`SeedFreeAgents` abrían otra: sqlite-net lanza
/// InvalidOperationException al anidar, así que el alta devolvía false y en el menú
/// "no pasaba nada" al pulsar Liga Manager.
///
/// Todo el trabajo se hace en una carpeta temporal (los tres overrides de test), de
/// modo que no toca ni las partidas ni las imágenes del jugador.
/// </summary>
public class SaveSlotInitTests
{
    string _root;
    int _editedWeight;
    GameObject _go;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "tf_slotinit_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        GameSaveManager.BaseDirOverride  = Path.Combine(_root, "saves");
        DatabaseManager.DataRootOverride = Path.Combine(_root, "data");
        CustomScopes.RootOverride        = Path.Combine(_root, "custom");

        _go = new GameObject("DatabaseManagerTest");
        var db = _go.AddComponent<DatabaseManager>();
        // En EditMode no se ejecuta Awake (solo al entrar en Play), así que el
        // singleton se registra a mano para que los helpers internos lo encuentren.
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

    // ── SAV-02: partida nueva SIN template.db ────────────────────────────────

    [Test]
    public void InitSaveSlot_SinTemplate_CreaLaPartidaCompleta()
    {
        var db = DatabaseManager.Instance;
        Assert.That(File.Exists(db.TemplateDbPath), Is.False, "El escenario del bug es NO tener template.db");

        bool ok = db.InitSaveSlot(1);

        Assert.That(ok, Is.True, $"Debe poder crear la partida sin template ({db.LastInitError})");
        Assert.That(db.GetAllTeams().Count, Is.EqualTo(30), "Deben quedar los 30 equipos");
        Assert.That(db.Db.Table<PlayerData>().Count(), Is.GreaterThan(0), "Deben quedar los jugadores sembrados");
        Assert.That(db.GetGLeagueTeams().Count, Is.EqualTo(30), "Deben quedar las 30 filiales G-League");
        Assert.That(File.Exists(db.TemplateDbPath), Is.True,
            "El template debe generarse al vuelo para las siguientes partidas");
    }

    [Test]
    public void InitSaveSlot_SeedLeagueSettings_UsaLaTablaCba()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);

        var settings = db.GetLeagueSettings();
        var cba = CbaTable.For(CbaTable.CurrentSeasonStartYear);

        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.salary_cap, Is.EqualTo(cba.SalaryCap));
        Assert.That(settings.luxury_tax, Is.EqualTo(cba.LuxuryTax));
        Assert.That(settings.apron, Is.EqualTo(cba.FirstApron));
        Assert.That(settings.repeater_apron, Is.EqualTo(cba.SecondApron));
        Assert.That(settings.mid_level, Is.EqualTo(cba.NonTaxpayerMle));
        Assert.That(settings.taxpayer_mid_level, Is.EqualTo(cba.TaxpayerMle));
        Assert.That(settings.bi_annual, Is.EqualTo(cba.BiAnnual));
        Assert.That(settings.two_way_salary, Is.EqualTo(cba.TwoWaySalary));
        Assert.That(settings.season_start_year, Is.EqualTo(CbaTable.CurrentSeasonStartYear));
        Assert.That(settings.cba_projected, Is.Zero);
        Assert.That(settings.minimum_salary, Is.EqualTo(TradeHelper.MIN_SALARY),
            "F4-08 siembra el mínimo oficial de 0 años y las bandas se consultan en CbaTable.");
    }

    // ── SAV-03: la segunda partida reutiliza el template ────────────────────

    [Test]
    public void InitSaveSlot_SegundaPartida_ClonaDelTemplate()
    {
        var db = DatabaseManager.Instance;

        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        Assert.That(db.InitSaveSlot(2), Is.True, db.LastInitError);

        Assert.That(db.GetAllTeams().Count, Is.EqualTo(30));
        Assert.That(db.Db.Table<PlayerData>().Count(), Is.GreaterThan(0), "El clon debe traer los jugadores");
        Assert.That(db.Db.Table<LeagueSettingsData>().Count(), Is.GreaterThan(0), "El clon debe traer el convenio");
    }

    [Test]
    public void EnsureTemplateDb_TemplateInvalido_SeReconstruyeYConservaBackup()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        CorruptTemplateSalary(db);
        db.CloseActiveSlot();

        Assert.DoesNotThrow(() => db.EnsureTemplateDb());

        Assert.That(File.Exists(db.TemplateDbPath + ".bak"), Is.True,
            "Debe conservar el template descartado para diagnóstico/recuperación.");
        using var repaired = new SQLiteConnection(db.TemplateDbPath);
        Assert.That(repaired.Table<PlayerData>().First().salary, Is.GreaterThanOrEqualTo(0));
        Assert.That(repaired.Table<DbMetaData>().First(m => m.key == "seed_fingerprint").value,
            Is.EqualTo(SeedBaseline.Fingerprint));
    }

    [Test]
    public void InitSaveSlot_TemplateInvalido_SeReparaAntesDeAbrirElSlot()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        CorruptTemplateSalary(db);
        db.CloseActiveSlot();

        bool ok = db.InitSaveSlot(2);

        Assert.That(ok, Is.True, db.LastInitError);
        Assert.That(db.GetAllTeams().Count, Is.EqualTo(30));
        Assert.That(db.Db.Table<PlayerData>().Count(), Is.GreaterThan(0));
    }

    [Test]
    public void EnsureTemplateDb_SinSelloDeSeed_SeReconstruye()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        using (var template = new SQLiteConnection(db.TemplateDbPath))
            template.DeleteAll<DbMetaData>();
        db.CloseActiveSlot();

        Assert.DoesNotThrow(() => db.EnsureTemplateDb());

        using var repaired = new SQLiteConnection(db.TemplateDbPath);
        Assert.That(repaired.Table<DbMetaData>().First(m => m.key == "seed_fingerprint").value,
            Is.EqualTo(SeedBaseline.Fingerprint));
    }

    [Test]
    public void EnsureTemplateDb_GLeagueNoClonada_NoBloqueaNiReconstruyeTemplateValido()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        db.CloseActiveSlot();
        using (var template = new SQLiteConnection(db.TemplateDbPath))
        {
            var prospect = template.Table<GLeaguePlayerData>().First();
            prospect.overall = 1;
            template.Update(prospect);
        }

        Assert.DoesNotThrow(() => db.EnsureTemplateDb());

        using var stillPresent = new SQLiteConnection(db.TemplateDbPath);
        Assert.That(stillPresent.Table<GLeaguePlayerData>().First().overall, Is.EqualTo(1),
            "La G-League se siembra por slot y no forma parte de los datos clonados del template.");
    }

    [Test]
    public void EnsureTemplateDb_TemplateValidoConEdicionValida_SeReutiliza()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        db.CloseActiveSlot();
        using (var template = new SQLiteConnection(db.TemplateDbPath))
        {
            var player = template.Table<PlayerData>().First();
            player.weight_kg += 1;
            _editedWeight = player.weight_kg;
            template.Update(player);
        }

        db.EnsureTemplateDb();

        using var reused = new SQLiteConnection(db.TemplateDbPath);
        Assert.That(reused.Table<PlayerData>().First().weight_kg, Is.EqualTo(_editedWeight),
            "Las ediciones válidas del Editor no se deben perder por una reconstrucción automática.");
    }

    [Test]
    public void InitSaveSlot_SaveAntiguo_RecalculaOverallConLaFormulaV2()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        var player = db.Db.Table<PlayerData>().First();
        int expected = player.GetCalculatedAverage();
        player.overall = expected == 99 ? 98 : expected + 1;
        db.Db.Update(player);
        db.Db.Execute("DELETE FROM schema_migrations WHERE name = ?", "overall_recalc_v2");
        db.CloseActiveSlot();

        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);

        var repaired = db.Db.Table<PlayerData>().First(p => p.id == player.id);
        Assert.That(repaired.overall, Is.EqualTo(expected));
        Assert.That(db.IsMigrationApplied("overall_recalc_v2"), Is.True);
    }

    static void CorruptTemplateSalary(DatabaseManager db)
    {
        using var template = new SQLiteConnection(db.TemplateDbPath);
        var player = template.Table<PlayerData>().First();
        player.salary = -1;
        template.Update(player);
    }

    [Test]
    public void EnsureTemplateDb_SelloAntiguo_ReconstruyeElTemplate()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        int seededWeight = db.Db.Table<PlayerData>().First().weight_kg;
        db.CloseActiveSlot();
        using (var template = new SQLiteConnection(db.TemplateDbPath))
        {
            var player = template.Table<PlayerData>().First();
            player.weight_kg = seededWeight + 1;
            template.Update(player);
            template.InsertOrReplace(new DbMetaData
            {
                key = "seed_fingerprint",
                value = "sello-de-una-version-anterior"
            });
        }

        Assert.DoesNotThrow(() => db.EnsureTemplateDb());

        using var rebuilt = new SQLiteConnection(db.TemplateDbPath);
        Assert.That(rebuilt.Table<DbMetaData>().First(m => m.key == "seed_fingerprint").value,
            Is.EqualTo(SeedBaseline.Fingerprint), "El sello debe quedar actualizado tras reconstruir.");
        Assert.That(rebuilt.Table<PlayerData>().First().weight_kg, Is.EqualTo(seededWeight),
            "Un sello obsoleto fuerza la reconstrucción desde el seed actual (la edición se descarta).");
    }

    [Test]
    public void RebuildTemplateDb_PublicaAtomicamenteYConservaBackup()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        db.CloseActiveSlot();

        Assert.DoesNotThrow(() => db.RebuildTemplateDb());

        Assert.That(File.Exists(db.TemplateDbPath + ".bak"), Is.True,
            "El reset del Editor debe conservar el template anterior como backup.");
        Assert.That(File.Exists(db.TemplateDbPath + ".tmp"), Is.False,
            "El temporal no debe sobrevivir a una publicación correcta.");
        using var published = new SQLiteConnection(db.TemplateDbPath);
        Assert.That(published.Table<TeamData>().Count(), Is.EqualTo(30));
        Assert.That(published.Table<DbMetaData>().First(m => m.key == "seed_fingerprint").value,
            Is.EqualTo(SeedBaseline.Fingerprint));
    }

    [Test]
    public void PlayerSeasonStats_HasShootingSplitColumns()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);

        foreach (string column in new[]
                 {
                     "total_fgm", "total_fga", "total_fg3m", "total_fg3a",
                     "total_ftm", "total_fta", "total_turnovers", "total_oreb", "total_dreb"
                 })
        {
            int exists = db.Db.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM pragma_table_info('player_season_stats') WHERE name = ?", column);
            Assert.That(exists, Is.EqualTo(1), $"Falta {column} en player_season_stats");
        }
    }

    [Test]
    public void BackfillPlayerSeasonShootingSplits_UsaGameStatsDisponibles()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);

        var game = new GameData
        {
            season_id = 777,
            manager_id = 1,
            game_day = 1,
            game_date = "2026-10-22",
            home_team_id = 1,
            away_team_id = 2,
            is_played = 1,
            game_type = "regular",
            series_label = ""
        };
        db.Db.Insert(game);
        db.Db.Insert(new PlayerGameStats
        {
            game_id = game.id,
            player_id = 999,
            team_id = 1,
            minutes = 30,
            points = 25,
            fgm = 9,
            fga = 18,
            fg3m = 4,
            fg3a = 9,
            ftm = 3,
            fta = 4,
            oreb = 2,
            dreb = 5,
            rebounds = 7,
            assists = 6,
            steals = 1,
            blocks = 1,
            turnovers = 3,
            rating = 24
        });
        db.Db.Insert(new PlayerSeasonStatRow
        {
            player_id = 999,
            season_id = 777,
            year_start = 2026,
            year_end = 2027,
            team_id = 1,
            team_abbreviation = "TST",
            team_name = "Test",
            games = 1,
            total_minutes = 30,
            total_points = 25,
            total_rebounds = 7,
            total_assists = 6,
            total_steals = 1,
            total_blocks = 1,
            total_rating = 24
        });

        db.BackfillPlayerSeasonShootingSplits();

        var row = db.Db.Table<PlayerSeasonStatRow>()
            .First(r => r.player_id == 999 && r.season_id == 777 && r.team_id == 1);
        Assert.That(row.total_fgm, Is.EqualTo(9));
        Assert.That(row.total_fga, Is.EqualTo(18));
        Assert.That(row.total_fg3m, Is.EqualTo(4));
        Assert.That(row.total_fg3a, Is.EqualTo(9));
        Assert.That(row.total_ftm, Is.EqualTo(3));
        Assert.That(row.total_fta, Is.EqualTo(4));
        Assert.That(row.total_turnovers, Is.EqualTo(3));
        Assert.That(row.total_oreb, Is.EqualTo(2));
        Assert.That(row.total_dreb, Is.EqualTo(5));
    }

    [Test]
    public void RebuildTemplateDb_RutaResuelta_NoConsultaPersistentDataPathEnBackground()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        db.CloseActiveSlot();

        // Reproduce el reset del Editor: el path se resuelve en el hilo principal
        // y el rebuild corre en un hilo de fondo sin tocar Application.*.
        string templatePath = db.TemplateDbPath;
        System.Exception error = null;
        var task = System.Threading.Tasks.Task.Run(() =>
        {
            try { db.RebuildTemplateDb(templatePath); }
            catch (System.Exception ex) { error = ex; }
        });

        Assert.That(task.Wait(System.TimeSpan.FromSeconds(60)), Is.True, "El rebuild no terminó a tiempo.");
        Assert.That(error, Is.Null, error?.ToString());
        Assert.That(File.Exists(templatePath + ".bak"), Is.True);
        Assert.That(File.Exists(templatePath + ".tmp"), Is.False);
    }

    [Test]
    public void SeedBaseline_Fingerprint_CoincideConElSeedReciénGenerado()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);

        string actual = SeedBaseline.Compute(db.Db);
        Assert.That(actual, Is.EqualTo(SeedBaseline.Fingerprint),
            $"Si cambió intencionadamente el seed, actualiza SeedBaseline.Fingerprint a: {actual}");
    }

    // ── SAV-04: los seeders no abren transacciones anidadas ─────────────────

    [Test]
    public void SeedFreeAgents_DentroDeUnaTransaccion_NoLanza()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);

        // La partida nueva se siembra dentro de _db.RunInTransaction: los seeders
        // deben participar en ella, no abrir otra (sqlite-net lanza al anidar).
        db.Db.DeleteAll<PlayerData>();

        Assert.DoesNotThrow(() => db.Db.RunInTransaction(() => db.SeedFreeAgents()));
        Assert.That(db.Db.Table<PlayerData>().Count(), Is.GreaterThan(0));
    }
}
