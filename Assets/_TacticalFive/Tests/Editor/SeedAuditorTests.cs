using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Motor de auditoría del seed. 100% puro (no toca Unity ni la BD): recibe las listas
/// ya cargadas y devuelve un informe con hallazgos clasificados.
///
/// Se eligió un motor separado del test para que F1-06 (validador de plantilla antes de
/// clonar el template) pueda reutilizarlo sin duplicar reglas.
///
/// Severidades:
///   ERROR → invariante ya acordada; su incumplimiento rompe el modelo (falla el test).
///   WARN  → deuda catalogada en Docs/AUDITORIA_2026-10-23.md con su ítem del plan
///           (Docs/MEJORAS_AUDITORIA_2026-10-23.md). Hoy no rompe, pero se vigila para
///           que no aparezcan avisos nuevos sin triaje.
/// </summary>
public static class SeedAudit
{
    public const string Error   = "ERROR";
    public const string Warning = "WARN";

    const int EXPECTED_TEAMS      = 30;
    const int MIN_ROSTER_STANDARD = 14;   // decisión pendiente D1/F1-02: hoy solo avisa
    const int MIN_HEIGHT_CM       = 160;
    const int MAX_HEIGHT_CM       = 240;
    const int MIN_WEIGHT_KG       = 55;
    const int MAX_WEIGHT_KG       = 180;
    const int MIN_AGE             = 16;
    const int MAX_AGE             = 50;
    const int LOW_ATTRIBUTE       = 25;   // por debajo de la escala del resto (F1-05)

    static readonly HashSet<string> ValidPositions = new HashSet<string> { "PG", "SG", "SF", "PF", "C" };

    public class Finding
    {
        public string Rule    { get; set; }
        public string Severity{ get; set; }
        public string Subject { get; set; }
        public string Detail  { get; set; }
        /// <summary>Ítem del plan de mejoras al que pertenece la deuda (vacío si es un error activo).</summary>
        public string PlanRef { get; set; }

        public override string ToString() =>
            $"{Subject} · {Detail}" + (string.IsNullOrEmpty(PlanRef) ? "" : $"  → {PlanRef}");
    }

    public class Report
    {
        public const int MaxExamplesPerRule = 6;

        public readonly List<Finding> Findings = new List<Finding>();
        public int TeamCount      { get; set; }
        public int PlayerCount    { get; set; }
        public int ProspectCount  { get; set; }

        public int Errors   => Findings.Count(f => f.Severity == Error);
        public int Warnings => Findings.Count(f => f.Severity == Warning);

        public void Add(string rule, string severity, string subject, string detail, string planRef = null)
        {
            Findings.Add(new Finding
            {
                Rule = rule, Severity = severity, Subject = subject, Detail = detail, PlanRef = planRef
            });
        }

        public IEnumerable<Finding> ByRule(string rule) => Findings.Where(f => f.Rule == rule);

        /// <summary>Informe legible: errores primero, luego avisos; máximo de ejemplos por regla.</summary>
        public string Text(string header = null)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(header)) sb.AppendLine(header);

            sb.AppendLine($"Datos auditados: {TeamCount} equipos · {PlayerCount} jugadores · {ProspectCount} prospectos G-League.");
            sb.AppendLine($"Resultado: {Errors} errores · {Warnings} avisos · {Findings.Count} hallazgos.");

            if (Findings.Count == 0)
            {
                sb.AppendLine("Sin hallazgos.");
                return sb.ToString();
            }

            var groups = Findings
                .GroupBy(f => new { f.Severity, f.Rule })
                .OrderBy(g => g.Key.Severity == Error ? 0 : 1)
                .ThenBy(g => g.Key.Rule);

            foreach (var g in groups)
            {
                sb.AppendLine();
                sb.AppendLine($"── {g.Key.Severity} · {g.Key.Rule} ({g.Count()})");
                foreach (var f in g.Take(MaxExamplesPerRule))
                    sb.AppendLine("   " + f);
                if (g.Count() > MaxExamplesPerRule)
                    sb.AppendLine($"   … y {g.Count() - MaxExamplesPerRule} más con la misma regla.");
            }

            return sb.ToString();
        }
    }

    // ── PUNTO DE ENTRADA ─────────────────────────────────

    public static Report Run(
        List<TeamData> teams,
        List<PlayerData> players,
        LeagueSettingsData settings,
        List<GLeagueTeamData> glTeams,
        List<GLeaguePlayerData> glProspects)
    {
        var report = new Report
        {
            TeamCount = teams?.Count ?? 0,
            PlayerCount = players?.Count ?? 0,
            ProspectCount = glProspects?.Count ?? 0
        };

        teams = teams ?? new List<TeamData>();
        players = players ?? new List<PlayerData>();
        glTeams = glTeams ?? new List<GLeagueTeamData>();
        glProspects = glProspects ?? new List<GLeaguePlayerData>();

        CheckTeams(teams, report);
        CheckPlayerIntegrity(teams, players, report);
        CheckRosters(teams, players, report);
        CheckLeagueSettings(settings, report);
        CheckGLeague(glTeams, glProspects, report);

        return report;
    }

    // ── EQUIPOS ──────────────────────────────────────────

    static void CheckTeams(List<TeamData> teams, Report r)
    {
        if (teams.Count != EXPECTED_TEAMS)
            r.Add("TEAM-COUNT", Error, "Liga", $"Hay {teams.Count} equipos; se esperan {EXPECTED_TEAMS}.");

        foreach (var dup in teams.GroupBy(t => t.abbreviation ?? "").Where(g => g.Count() > 1))
            r.Add("TEAM-ABBR", Error, $"Abreviatura '{dup.Key}'", $"Repetida en {dup.Count()} equipos.");

        foreach (var t in teams)
        {
            string who = $"{t.abbreviation} {t.name}".Trim();

            if (string.IsNullOrWhiteSpace(t.abbreviation))
                r.Add("TEAM-ABBR", Error, who, "Abreviatura vacía.");

            if (t.conference != "East" && t.conference != "West")
                r.Add("TEAM-CONF", Error, who, $"Conferencia '{t.conference}' (solo East/West).");

            if (string.IsNullOrWhiteSpace(t.division))
                r.Add("TEAM-DIV", Error, who, "División vacía.");

            if (t.capacity <= 0)
                r.Add("TEAM-CAPACITY", Error, who, $"Capacidad de pabellón {t.capacity}.");

            if (t.budget <= 0)
                r.Add("TEAM-BUDGET", Error, who, $"Presupuesto {t.budget}.");

            if (!InRange(t.attack, 0, 100) || !InRange(t.defense, 0, 100) || !InRange(t.overall, 0, 100))
                r.Add("TEAM-RATING", Error, who,
                    $"attack={t.attack} defense={t.defense} overall={t.overall} (fuera de 0-100).");
        }
    }

    // ── JUGADORES: INTEGRIDAD ────────────────────────────

    static void CheckPlayerIntegrity(List<TeamData> teams, List<PlayerData> players, Report r)
    {
        var teamIds = new HashSet<int>(teams.Select(t => t.id));
        var seen = new HashSet<int>();

        foreach (var p in players)
        {
            string who = $"#{p.id} {p.first_name} {p.last_name}".Trim();

            if (!seen.Add(p.id))
                r.Add("PLAYER-ID", Error, who, "Id de jugador repetido.");

            if (p.team_id != 0 && !teamIds.Contains(p.team_id))
                r.Add("PLAYER-TEAM", Error, who, $"team_id {p.team_id} no existe en 'teams'.");

            if (string.IsNullOrWhiteSpace(p.first_name) || string.IsNullOrWhiteSpace(p.last_name))
                r.Add("PLAYER-NAME", Error, who, "Nombre o apellido vacío.");

            if (!ValidPositions.Contains(p.position ?? ""))
                r.Add("PLAYER-POSITION", Error, who, $"Posición '{p.position}' no válida.");

            if (!string.IsNullOrEmpty(p.secondary_position) && !ValidPositions.Contains(p.secondary_position))
                r.Add("PLAYER-POSITION", Error, who, $"Posición secundaria '{p.secondary_position}' no válida.");

            // Escala de los 11 atributos que componen el overall.
            foreach (var (name, value) in Attributes(p))
            {
                if (!InRange(value, 1, 99))
                    r.Add("PLAYER-ATTR-RANGE", Error, who, $"{name}={value} (fuera de 1-99).");
                else if (value < LOW_ATTRIBUTE)
                    r.Add("PLAYER-ATTR-LOWOUT", Warning, who,
                        $"{name}={value} (muy por debajo de la escala del resto del seed).", "F1-05");
            }

            if (!InRange(p.potential, 1, 99))
                r.Add("PLAYER-POTENTIAL", Error, who, $"potential={p.potential} (fuera de 1-99).");
            else if (p.potential < p.overall)
                r.Add("PLAYER-POTENTIAL", Error, who, $"potential={p.potential} < overall={p.overall}.");

            if (p.salary < 0)
                r.Add("PLAYER-SALARY", Error, who, $"Salario negativo {p.salary}.");

            if (p.contract_years < 1)
                r.Add("PLAYER-CONTRACT", Error, who, $"contract_years={p.contract_years} (mínimo 1).");

            if (p.guaranteed_years < 0 || p.guaranteed_years > p.contract_years)
                r.Add("PLAYER-CONTRACT", Error, who,
                    $"guaranteed_years={p.guaranteed_years} fuera de [0, contract_years={p.contract_years}].");

            if (!InRange(p.age, MIN_AGE, MAX_AGE))
                r.Add("PLAYER-BODY", Error, who, $"Edad {p.age} (fuera de {MIN_AGE}-{MAX_AGE}).");

            if (!InRange(p.height_cm, MIN_HEIGHT_CM, MAX_HEIGHT_CM))
                r.Add("PLAYER-BODY", Error, who, $"Altura {p.height_cm} cm (fuera de {MIN_HEIGHT_CM}-{MAX_HEIGHT_CM}).");

            if (!InRange(p.weight_kg, MIN_WEIGHT_KG, MAX_WEIGHT_KG))
                r.Add("PLAYER-BODY", Error, who, $"Peso {p.weight_kg} kg (fuera de {MIN_WEIGHT_KG}-{MAX_WEIGHT_KG}).");

            if (!InRange(p.morale, 0, 100))
                r.Add("PLAYER-STATE", Error, who, $"Moral {p.morale} (fuera de 0-100).");

            if (!InRange(p.fisico, 0, 100))
                r.Add("PLAYER-STATE", Error, who, $"Físico {p.fisico} (fuera de 0-100).");

            if (p.injury_days < 0)
                r.Add("PLAYER-STATE", Error, who, $"injury_days={p.injury_days}.");

            if (p.is_two_way != 0 && p.is_two_way != 1)
                r.Add("PLAYER-STATE", Error, who, $"is_two_way={p.is_two_way} (debe ser 0/1).");

            // ── Invariante del overall (ROST-01) ──
            // Hoy es WARN porque el seeder trunca y el modelo redondea: se corrige en F0-04,
            // momento en el que esta regla pasa a Error.
            int canonical = p.GetCalculatedAverage();
            if (p.overall != canonical)
                r.Add("PLAYER-OVR", Warning, who,
                    $"overall={p.overall} pero la media de sus 11 atributos (capada por potencial) es {canonical}.",
                    "F0-04");

            // ── Suelo salarial (F1-04) ──
            if (p.salary > 0 && p.salary < TradeHelper.MIN_SALARY && p.is_rookie == 0 && p.is_two_way == 0)
                r.Add("PLAYER-SALARY-FLOOR", Warning, who,
                    $"{p.salary:N0} $ por debajo del mínimo {TradeHelper.MIN_SALARY:N0} $ sin ser rookie ni two-way.",
                    "F1-04");
        }
    }

    // ── JUGADORES: PLANTILLAS ────────────────────────────

    static void CheckRosters(List<TeamData> teams, List<PlayerData> players, Report r)
    {
        var byTeam = players.Where(p => p.team_id != 0)
                            .GroupBy(p => p.team_id)
                            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var t in teams)
        {
            string who = $"{t.abbreviation} {t.name}".Trim();
            if (!byTeam.TryGetValue(t.id, out var roster) || roster.Count == 0)
            {
                r.Add("ROSTER-EMPTY", Error, who, "Equipo sin ningún jugador asignado.");
                continue;
            }

            if (roster.Count < MIN_ROSTER_STANDARD || roster.Count > TradeHelper.MAX_ROSTER)
                r.Add("ROSTER-SIZE", Warning, who,
                    $"{roster.Count} jugadores (fuera de {MIN_ROSTER_STANDARD}-{TradeHelper.MAX_ROSTER}).", "F1-02");

            int twoWay = roster.Count(p => p.is_two_way == 1);
            if (twoWay > TradeHelper.MAX_TWO_WAY)
                r.Add("ROSTER-TWO-WAY", Error, who,
                    $"{twoWay} contratos two-way (máximo {TradeHelper.MAX_TWO_WAY}).");
        }
    }

    // ── CONVENIO (CAP / APRONS) ──────────────────────────

    static void CheckLeagueSettings(LeagueSettingsData s, Report r)
    {
        if (s == null)
        {
            r.Add("LEAGUE-SETTINGS", Error, "league_settings", "No hay ninguna fila activa.");
            return;
        }

        if (!(s.salary_cap > 0 && s.salary_cap < s.luxury_tax && s.luxury_tax < s.apron && s.apron < s.repeater_apron))
            r.Add("LEAGUE-SETTINGS", Error, "league_settings",
                $"Orden roto: cap={s.salary_cap:N0} tax={s.luxury_tax:N0} apron1={s.apron:N0} apron2={s.repeater_apron:N0}.");

        if (s.mid_level <= 0 || s.taxpayer_mid_level <= 0 || s.bi_annual <= 0 || s.minimum_salary <= 0 || s.two_way_salary <= 0)
            r.Add("LEAGUE-SETTINGS", Error, "league_settings",
                $"Excepciones a cero: NT-MLE={s.mid_level:N0} T-MLE={s.taxpayer_mid_level:N0} " +
                $"bi-anual={s.bi_annual:N0} mínimo={s.minimum_salary:N0} two-way={s.two_way_salary:N0}.");

        if (s.taxpayer_mid_level > s.mid_level)
            r.Add("LEAGUE-SETTINGS", Error, "league_settings",
                $"La Taxpayer MLE ({s.taxpayer_mid_level:N0}) no puede superar la No-Taxpayer ({s.mid_level:N0}).");
    }

    // ── G-LEAGUE ─────────────────────────────────────────

    static void CheckGLeague(List<GLeagueTeamData> teams, List<GLeaguePlayerData> prospects, Report r)
    {
        if (teams.Count != EXPECTED_TEAMS)
            r.Add("GLEAGUE-TEAMS", Error, "G-League", $"Hay {teams.Count} filiales; se esperan {EXPECTED_TEAMS}.");

        foreach (var t in teams)
        {
            int n = prospects.Count(p => p.gleague_team_id == t.id);
            if (n != GLeagueSeeder.PLAYERS_PER_TEAM)
                r.Add("GLEAGUE-ROSTER", Error, t.name,
                    $"{n} prospectos (esperados {GLeagueSeeder.PLAYERS_PER_TEAM}).");
        }

        foreach (var p in prospects)
        {
            string who = $"#{p.id} {p.first_name} {p.last_name}".Trim();

            foreach (var (name, value) in Attributes(p))
                if (!InRange(value, 1, 99))
                    r.Add("GLEAGUE-ATTR", Error, who, $"{name}={value} (fuera de 1-99).");

            int canonical = p.GetCalculatedAverage();
            if (p.overall != canonical)
                r.Add("GLEAGUE-OVR", Warning, who,
                    $"overall={p.overall} pero la media de sus atributos es {canonical}.", "F0-04");
        }
    }

    // ── HELPERS ──────────────────────────────────────────

    static bool InRange(int value, int min, int max) => value >= min && value <= max;

    static IEnumerable<(string name, int value)> Attributes(PlayerData p)
    {
        yield return ("speed", p.speed);
        yield return ("shooting", p.shooting);
        yield return ("three_point", p.three_point);
        yield return ("passing", p.passing);
        yield return ("dribbling", p.dribbling);
        yield return ("defense", p.defense);
        yield return ("rebounding", p.rebounding);
        yield return ("athleticism", p.athleticism);
        yield return ("iq", p.iq);
        yield return ("steals", p.steals);
        yield return ("blocks", p.blocks);
    }

    static IEnumerable<(string name, int value)> Attributes(GLeaguePlayerData p)
    {
        yield return ("speed", p.speed);
        yield return ("shooting", p.shooting);
        yield return ("three_point", p.three_point);
        yield return ("passing", p.passing);
        yield return ("dribbling", p.dribbling);
        yield return ("defense", p.defense);
        yield return ("rebounding", p.rebounding);
        yield return ("athleticism", p.athleticism);
        yield return ("iq", p.iq);
        yield return ("steals", p.steals);
        yield return ("blocks", p.blocks);
    }
}

/// <summary>
/// F0-01 · Auditor del seed (solo lectura).
///
/// Problema que cubre: el estado inicial se revisó a mano y no había ninguna comprobación
/// automática, así que inconsistencias como el OV truncado o plantillas de 18 jugadores
/// podían volver a colarse sin que nadie se enterara.
///
/// Cómo funciona: siembra una partida nueva en una carpeta temporal (los tres overrides de
/// test, igual que SaveSlotInitTests), carga el resultado y lo audita. No toca partidas ni
/// imágenes del jugador, y no escribe nada fuera de la carpeta temporal.
/// </summary>
public class SeedAuditorTests
{
    /// <summary>Deudas ya catalogadas en Docs/AUDITORIA_2026-10-23.md. Un aviso fuera de
    /// esta lista falla: hay que corregirlo o catalogarlo con su ítem del plan.</summary>
    static readonly string[] KnownWarnings =
    {
        "ROSTER-SIZE",         // F1-02 · DEN/NYK con 18, PRT con 13
        "PLAYER-SALARY-FLOOR", // F1-04 · salarios por debajo del mínimo interno
        "PLAYER-ATTR-LOWOUT"   // F1-05 · atributos por debajo de 25
    };

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
