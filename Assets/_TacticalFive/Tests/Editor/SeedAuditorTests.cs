using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// F0-01 · Auditor del seed (solo lectura).
///
/// Prueba el auditor runtime de SeedAudit.cs sobre una partida aislada y datos sintéticos.
/// El tamaño del roster se imprime como dato informativo; las invariantes de seed fallan.
///
/// Cómo funciona: siembra una partida nueva en una carpeta temporal (los tres overrides de
/// test, igual que SaveSlotInitTests), carga el resultado y lo audita. No toca partidas ni
/// imágenes del jugador, y no escribe nada fuera de la carpeta temporal.
/// </summary>
public class SeedAuditorTests
{
    /// <summary>Deudas ya catalogadas en Docs/AUDITORIA_2026-10-23.md. Un aviso fuera de
    /// esta lista falla: hay que corregirlo o catalogarlo con su ítem del plan.</summary>
    static readonly string[] KnownWarnings = { };

    string _root;
    GameObject _go;
    SeedAudit.Report _report;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "tf_seedaudit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        GameSaveManager.BaseDirOverride  = Path.Combine(_root, "saves");
        DatabaseManager.DataRootOverride = Path.Combine(_root, "data");
        CustomScopes.RootOverride        = Path.Combine(_root, "custom");

        _go = new GameObject("DatabaseManagerSeedAudit");
        var db = _go.AddComponent<DatabaseManager>();
        // En EditMode no se ejecuta Awake: el singleton se registra a mano.
        SetInstance(db);

        Assert.That(db.InitSaveSlot(1), Is.True,
            "No se pudo sembrar la partida de auditoría: " + db.LastInitError);

        var teams = db.GetAllTeams();
        var players = db.Db.Table<PlayerData>().ToList();
        var settings = db.GetLeagueSettings();
        var glTeams = db.GetGLeagueTeams();
        var glProspects = db.Db.Table<GLeaguePlayerData>().ToList();

        _report = SeedAudit.Run(teams, players, settings, glTeams, glProspects);

        TestContext.WriteLine(_report.Text("AUDITORÍA DEL SEED"));
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        DatabaseManager.Instance?.CloseActiveSlot();
        SetInstance(null);
        if (_go != null) UnityEngine.Object.DestroyImmediate(_go);

        GameSaveManager.BaseDirOverride  = null;
        DatabaseManager.DataRootOverride = null;
        CustomScopes.RootOverride        = null;

        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    static void SetInstance(DatabaseManager db) =>
        typeof(DatabaseManager)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.SetValue(null, db);

    // ── EL AUDITOR VE DATOS REALES ───────────────────────

    [Test]
    public void Auditor_NoPasaEnVacio()
    {
        Assert.That(_report.TeamCount, Is.EqualTo(30), "Deben auditarse los 30 equipos sembrados");
        Assert.That(_report.PlayerCount, Is.GreaterThan(500),
            "Deben auditarse los cientos de jugadores del seed (plantillas + agentes libres)");
        Assert.That(_report.ProspectCount, Is.GreaterThan(300), "Deben auditarse los prospectos G-League");
        Assert.That(_report.AssignedPlayerCount + _report.FreeAgentCount, Is.EqualTo(_report.PlayerCount),
            "El resumen debe distinguir jugadores asignados de agentes libres sin perder registros.");
        Assert.That(_report.StandardPlayerCount + _report.TwoWayPlayerCount, Is.EqualTo(_report.AssignedPlayerCount),
            "El resumen debe distinguir plazas estándar de contratos two-way.");
    }

    [Test]
    public void SeedAudit_BloqueaErroresPeroNoDatosInformativos()
    {
        var report = new SeedAudit.Report { RosterOutOfRangeCount = 1 };

        Assert.DoesNotThrow(() => report.ThrowIfInvalid("test informativo"),
            "El tamaño fuera del rango de referencia es informativo, no bloqueante.");

        report.Add("PLAYER-SALARY", SeedAudit.Error, "#1 Jugador", "Salario negativo.");
        var ex = Assert.Throws<SeedAudit.SeedAuditException>(() => report.ThrowIfInvalid("template.db"));

        Assert.That(ex.Message, Does.Contain("Seed inválido en template.db"));
        Assert.That(ex.Message, Does.Contain("PLAYER-SALARY"));
        Assert.That(ex.UserSummary, Does.Contain("PLAYER-SALARY: 1"));
        Assert.That(ex.UserSummary, Does.Contain("informe completo está en la consola"));
    }

    [Test]
    public void AtributosBajosDentroDeEscala_SoloSeInforman()
    {
        Assert.That(_report.LowAttributeCount, Is.GreaterThan(0),
            "El informe debe mostrar los atributos bajos como dato diagnóstico.");
        Assert.That(_report.ByRule("PLAYER-ATTR-LOWOUT"), Is.Empty,
            "Un atributo entre 1 y 24 es válido; no debe tratarse como aviso ni error.");
    }

    [Test]
    public void ContratoEmbiid_ReflejaCapHitMaximo2026_27()
    {
        var embiid = DatabaseManager.Instance.GetPlayerById(356);

        Assert.That(embiid, Is.Not.Null);
        Assert.That(embiid.salary, Is.EqualTo(58_100_000));
        Assert.That(embiid.contract_years, Is.EqualTo(3));
        Assert.That(embiid.guaranteed_years, Is.EqualTo(2));
        Assert.That(embiid.has_player_option, Is.EqualTo(1));
        Assert.That(embiid.has_team_option, Is.Zero);
    }

    [Test]
    public void HoustonPayroll_QuedaBajoApronInternoYReal2026_27()
    {
        var houston = DatabaseManager.Instance.GetAllTeams().Single(t => t.abbreviation == "HOU");
        long payroll = DatabaseManager.Instance.GetPlayersByTeam(houston.id).Sum(p => p.salary);

        Assert.That(payroll, Is.LessThanOrEqualTo(TradeHelper.SECOND_APRON),
            "La nómina del seed HOU debe quedar bajo el segundo apron configurado en el juego.");
        Assert.That(payroll, Is.LessThanOrEqualTo(221_686_000),
            "Para la referencia 2026-27, la nómina del seed también debe quedar bajo el segundo apron NBA oficial.");
        TestContext.WriteLine($"HOU seed payroll 2026-27: {payroll:N0} $; apron juego {TradeHelper.SECOND_APRON:N0} $; apron NBA 221.686.000 $.");
    }

    // ── INVARIANTES (deben estar en cero) ────────────────

    [TestCase("TEAM-COUNT")]
    [TestCase("TEAM-ABBR")]
    [TestCase("TEAM-CONF")]
    [TestCase("TEAM-DIV")]
    [TestCase("TEAM-CAPACITY")]
    [TestCase("TEAM-BUDGET")]
    [TestCase("TEAM-RATING")]
    [TestCase("PLAYER-ID")]
    [TestCase("PLAYER-TEAM")]
    [TestCase("PLAYER-NAME")]
    [TestCase("PLAYER-POSITION")]
    [TestCase("PLAYER-ATTR-RANGE")]
    [TestCase("PLAYER-OVR")]
    [TestCase("PLAYER-POTENTIAL")]
    [TestCase("PLAYER-SALARY")]
    [TestCase("PLAYER-SALARY-FLOOR")]
    [TestCase("PLAYER-CONTRACT")]
    [TestCase("PLAYER-BODY")]
    [TestCase("PLAYER-STATE")]
    [TestCase("ROSTER-EMPTY")]
    [TestCase("ROSTER-TWO-WAY")]
    [TestCase("LEAGUE-SETTINGS")]
    [TestCase("GLEAGUE-TEAMS")]
    [TestCase("GLEAGUE-ROSTER")]
    [TestCase("GLEAGUE-ATTR")]
    [TestCase("GLEAGUE-OVR")]
    public void Regla_SinErrores(string rule)
    {
        var errors = _report.ByRule(rule).Where(f => f.Severity == SeedAudit.Error).ToList();
        Assert.That(errors, Is.Empty,
            $"La regla {rule} tiene {errors.Count} incumplimiento(s):\n" + _report.Text());
    }

    [Test]
    public void PlantillasFueraDeRango_SoloSeInforman()
    {
        Assert.That(_report.ByRule("ROSTER-SIZE"), Is.Empty,
            "El tamaño de plantilla del seed lo mantiene el usuario: no es un hallazgo del auditor.");
        Assert.That(_report.RosterOutOfRangeCount, Is.EqualTo(_report.RosterOutOfRange.Count),
            "El contador informativo debe coincidir con las plantillas enumeradas.");
    }

    /// <summary>El comportamiento no depende del seed: una plantilla corta se informa,
    /// nunca se marca como aviso. Así el test sigue siendo válido cuando el usuario
    /// ajuste ATL/DEN/PRT.</summary>
    [Test]
    public void PlantillaCorta_SeInformaSinAviso()
    {
        var teams = new List<TeamData> { SyntheticTeam(1, "AAA") };
        var players = Enumerable.Range(1, 12).Select(i => SyntheticPlayer(i, 1)).ToList();

        var report = SeedAudit.Run(teams, players, null, null, null);

        Assert.That(report.ByRule("ROSTER-SIZE"), Is.Empty, "12 activos no debe generar un hallazgo.");
        Assert.That(report.RosterOutOfRangeCount, Is.EqualTo(1), "Debe informarse el equipo fuera de 14-18.");
        Assert.That(report.RosterOutOfRange.Single(), Does.StartWith("AAA"), "El dato debe identificar al equipo.");
    }

    [Test]
    public void PlantillaEnRango_NoSeInforma()
    {
        var teams = new List<TeamData> { SyntheticTeam(1, "BBB") };
        var players = Enumerable.Range(1, 15).Select(i => SyntheticPlayer(i, 1)).ToList();

        var report = SeedAudit.Run(teams, players, null, null, null);

        Assert.That(report.ByRule("ROSTER-SIZE"), Is.Empty);
        Assert.That(report.RosterOutOfRangeCount, Is.Zero);
    }

    [Test]
    public void PlazasEstandarPorEncimaDe16_SeInformanAunqueElTotalSea18()
    {
        var teams = new List<TeamData> { SyntheticTeam(1, "CCC") };
        var players = Enumerable.Range(1, 18).Select(i => SyntheticPlayer(i, 1)).ToList();
        players[17].is_two_way = 1;

        var report = SeedAudit.Run(teams, players, null, null, null);

        Assert.That(report.RosterOutOfRangeCount, Is.EqualTo(1));
        Assert.That(report.RosterOutOfRange.Single(), Does.Contain("17/16 estándar"));
    }

    [Test]
    public void PlantillaDieciseisEstandarDosTwoWay_EsValida()
    {
        var teams = new List<TeamData> { SyntheticTeam(1, "DDD") };
        var players = Enumerable.Range(1, 18).Select(i => SyntheticPlayer(i, 1)).ToList();
        players[16].is_two_way = 1;
        players[17].is_two_way = 1;

        var report = SeedAudit.Run(teams, players, null, null, null);

        Assert.That(report.RosterOutOfRangeCount, Is.Zero);
    }

    static TeamData SyntheticTeam(int id, string abbreviation) => new TeamData
    {
        id = id, name = "Equipo " + abbreviation, abbreviation = abbreviation,
        conference = "East", division = "Atlántico",
        capacity = 18000, budget = 150_000_000,
        attack = 70, defense = 70, overall = 70
    };

    static PlayerData SyntheticPlayer(int id, int teamId) => new PlayerData
    {
        id = id, team_id = teamId,
        first_name = "Jugador", last_name = id.ToString(),
        position = "PG", age = 25, height_cm = 190, weight_kg = 82,
        speed = 60, shooting = 60, three_point = 60, passing = 60, dribbling = 60,
        defense = 60, rebounding = 60, athleticism = 60, iq = 60, steals = 60, blocks = 60,
        potential = 70, salary = 2_000_000, contract_years = 1, guaranteed_years = 0,
        morale = 50, fisico = 100
    };

    // ── DEUDA CATALOGADA (se vigila, no rompe) ───────────

    [Test]
    public void Avisos_TodosCatalogados()
    {
        var unknown = _report.Findings
            .Where(f => f.Severity == SeedAudit.Warning)
            .Select(f => f.Rule)
            .Distinct()
            .Where(rule => !KnownWarnings.Contains(rule))
            .OrderBy(rule => rule)
            .ToList();

        Assert.That(unknown, Is.Empty,
            "Hay avisos sin catalogar. Triaje: se corrigen o se añaden a KnownWarnings con su ítem del plan.\n" +
            _report.Text());
    }

    [Test]
    public void Informe_SeImprimeEnElLog()
    {
        // El informe ya se escribe en OneTimeSetUp; este test existe para que el runner
        // muestre el desglose completo (y para que el conteo quede en el log de CI).
        TestContext.WriteLine(_report.Text("AUDITORÍA DEL SEED (detalle)"));
        Assert.That(_report.Errors, Is.Zero, _report.Text("Errores activos"));
    }
}
