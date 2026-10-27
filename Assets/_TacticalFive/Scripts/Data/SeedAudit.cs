using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

    public sealed class SeedAuditException : InvalidOperationException
    {
        public string UserSummary { get; }
        public string FullReport { get; }

        public SeedAuditException(string message, string userSummary, string fullReport)
            : base(message)
        {
            UserSummary = userSummary;
            FullReport = fullReport;
        }
    }

    const int EXPECTED_TEAMS      = 30;
    const int MIN_ROSTER_ACTIVE   = 14;
    const int MIN_HEIGHT_CM       = 160;
    const int MAX_HEIGHT_CM       = 240;
    const int MIN_WEIGHT_KG       = 55;
    const int MAX_WEIGHT_KG       = 180;
    const int MIN_AGE             = 16;
    const int MAX_AGE             = 50;

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
        public int AssignedPlayerCount { get; set; }
        public int FreeAgentCount       { get; set; }
        public int StandardPlayerCount  { get; set; }
        public int TwoWayPlayerCount    { get; set; }
        public int LowAttributeCount    { get; set; }
        /// <summary>Contratos asignados anteriores a F4-08 por debajo del mínimo de su experiencia.</summary>
        public int LegacySalaryCount    { get; set; }
        public readonly List<string> RosterSummaries = new List<string>();

        /// <summary>Equipos sembrados fuera de la referencia 14-18. Es un dato de
        /// diagnóstico: el seed de plantillas lo mantiene el usuario a mano.</summary>
        public int RosterOutOfRangeCount { get; set; }
        public readonly List<string> RosterOutOfRange = new List<string>();

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

        /// <summary>
        /// Bloquea el uso de una plantilla cuando incumple invariantes del juego. Los
        /// desvíos meramente informativos (p. ej. tamaños 14-18) no son hallazgos y no
        /// impiden crear una partida.
        /// </summary>
        public void ThrowIfInvalid(string source)
        {
            if (Errors == 0) return;

            string full = Text("Validación del seed");
            string summary = Summary(source, maxExamples: 2);
            throw new SeedAuditException(
                $"Seed inválido en {source}: {Errors} error(es). No se puede crear ni clonar una partida.\n{full}",
                summary,
                full);
        }

        /// <summary>Resumen pequeño para UI: conteo por regla y como máximo dos ejemplos.</summary>
        public string Summary(string source, int maxExamples = 2)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"No se pudo validar {source}: {Errors} errores.");

            foreach (var group in Findings.Where(f => f.Severity == Error)
                         .GroupBy(f => f.Rule)
                         .OrderBy(g => g.Key, StringComparer.Ordinal))
                sb.AppendLine($"• {group.Key}: {group.Count()}");

            var examples = Findings.Where(f => f.Severity == Error).Take(Math.Max(0, maxExamples)).ToList();
            if (examples.Count > 0)
            {
                sb.AppendLine("Ejemplos:");
                foreach (var finding in examples)
                    sb.AppendLine($"• {finding}");
            }

            sb.Append("El informe completo está en la consola de Unity.");
            return sb.ToString();
        }

        /// <summary>Informe legible: errores primero, luego avisos; máximo de ejemplos por regla.</summary>
        public string Text(string header = null)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(header)) sb.AppendLine(header);

            sb.AppendLine($"Datos auditados: {TeamCount} equipos · {PlayerCount} jugadores · {ProspectCount} prospectos G-League.");
            sb.AppendLine($"Seed NBA: {AssignedPlayerCount} asignados ({StandardPlayerCount} estándar + {TwoWayPlayerCount} two-way) · {FreeAgentCount} agentes libres.");
            sb.AppendLine($"Atributos <{PlayerAttributeScale.LOW_VALUE_INFO_THRESHOLD}: {LowAttributeCount} (válidos; solo informativo).");
            if (LegacySalaryCount > 0)
                sb.AppendLine($"Contratos legacy por debajo del mínimo de su experiencia (F4-08): {LegacySalaryCount} " +
                              "(firmados bajo el suelo interno anterior; se respetan hasta su renovación).");
            sb.AppendLine($"Plantillas fuera del rango activo {MIN_ROSTER_ACTIVE}-{TradeHelper.MAX_ROSTER} o con más de {TradeHelper.MAX_STANDARD_ROSTER} plazas estándar: {RosterOutOfRangeCount} " +
                          "(informativo; el seed de plantillas lo mantiene el usuario).");
            sb.AppendLine($"Resultado: {Errors} errores · {Warnings} avisos · {Findings.Count} hallazgos.");
            sb.AppendLine("Plantillas: " + string.Join(" · ", RosterSummaries));

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
        List<GLeaguePlayerData> glProspects,
        bool includeGLeague = true)
    {
        teams = teams ?? new List<TeamData>();
        players = players ?? new List<PlayerData>();
        glTeams = glTeams ?? new List<GLeagueTeamData>();
        glProspects = glProspects ?? new List<GLeaguePlayerData>();

        var assignedPlayers = players.Where(p => p.team_id != 0).ToList();
        var report = new Report
        {
            TeamCount = teams.Count,
            PlayerCount = players.Count,
            ProspectCount = includeGLeague ? glProspects.Count : 0,
            AssignedPlayerCount = assignedPlayers.Count,
            FreeAgentCount = players.Count(p => p.team_id == 0),
            StandardPlayerCount = assignedPlayers.Count(p => p.is_two_way != 1),
            TwoWayPlayerCount = assignedPlayers.Count(p => p.is_two_way == 1)
        };

        CheckTeams(teams, report);
        CheckPlayerIntegrity(teams, players, settings, report);
        CheckRosters(teams, players, report);
        CheckLeagueSettings(settings, report);
        if (includeGLeague)
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

    static void CheckPlayerIntegrity(List<TeamData> teams, List<PlayerData> players,
        LeagueSettingsData settings, Report r)
    {
        var teamIds = new HashSet<int>(teams.Select(t => t.id));
        var seen = new HashSet<int>();
        long universalMinimum = TradeHelper.MinimumSalary(settings, 0);

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
                if (!PlayerAttributeScale.IsValid(value))
                    r.Add("PLAYER-ATTR-RANGE", Error, who,
                        $"{name}={value} (fuera de {PlayerAttributeScale.MIN_VALUE}-{PlayerAttributeScale.MAX_VALUE}).");
                else if (value < PlayerAttributeScale.LOW_VALUE_INFO_THRESHOLD)
                    r.LowAttributeCount++;
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

            if ((p.has_team_option != 0 && p.has_team_option != 1)
                || (p.has_player_option != 0 && p.has_player_option != 1))
                r.Add("PLAYER-CONTRACT", Error, who,
                    $"Opciones inválidas: TO={p.has_team_option}, PO={p.has_player_option} (deben ser 0/1).");
            else if (p.has_team_option == 1 && p.has_player_option == 1)
                r.Add("PLAYER-CONTRACT", Error, who, "TO y PO son mutuamente excluyentes.");
            else if ((p.has_team_option == 1 || p.has_player_option == 1)
                     && p.guaranteed_years != Math.Max(0, p.contract_years - 1))
                r.Add("PLAYER-CONTRACT", Error, who,
                    $"Con TO/PO, guaranteed_years debe ser max(0, contract_years-1): " +
                    $"{p.guaranteed_years} vs {Math.Max(0, p.contract_years - 1)}.");

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

            // ── Invariante del overall (ROST-01 / F0-04) ──
            int canonical = p.GetCalculatedAverage();
            if (p.overall != canonical)
                r.Add("PLAYER-OVR", Error, who,
                    $"overall={p.overall} pero la media de sus 11 atributos (capada por potencial) es {canonical}.");

            // ── Suelo salarial universal (F4-08) ──
            // Solo los contratos asignados están sujetos al suelo. Los agentes libres
            // guardan un precio pedido, no un contrato firmado.
            if (p.team_id != 0 && p.salary > 0 && p.salary < universalMinimum
                && p.is_rookie == 0 && p.is_two_way == 0)
                r.Add("PLAYER-SALARY-FLOOR", Error, who,
                    $"{p.salary:N0} $ por debajo del mínimo universal {universalMinimum:N0} $ sin ser rookie ni two-way.");

            // Contratos firmados antes de F4-08: se conservan (no se reescriben nóminas
            // del usuario) pero el mínimo de su experiencia se aplicará al renovar.
            long applicableMinimum = TradeHelper.MinimumSalary(settings, p.years_of_service);
            if (p.team_id != 0 && p.salary > 0 && p.salary < applicableMinimum
                && p.is_rookie == 0 && p.is_two_way == 0)
                r.LegacySalaryCount++;
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
                r.RosterSummaries.Add($"{t.abbreviation}: 0 activos (0 estándar + 0 two-way)");
                continue;
            }

            int activeCount = roster.Count(p => p.is_on_ir == 0);
            int activeTwoWay = roster.Count(p => p.is_on_ir == 0 && p.is_two_way == 1);
            int standardCount = activeCount - activeTwoWay;
            int irCount = roster.Count(p => p.is_on_ir != 0);
            r.RosterSummaries.Add($"{t.abbreviation}: {activeCount} activos ({standardCount} estándar + {activeTwoWay} two-way)" +
                                  (irCount > 0 ? $", {irCount} IR" : ""));

            // El tamaño de plantilla NO se impone: el seed de plantillas lo mantiene el
            // usuario a mano y 14-18 es solo la referencia de juego. Se publica como dato
            // (RosterOutOfRange) para que el desvío no pase desapercibido.
            if (activeCount < MIN_ROSTER_ACTIVE || activeCount > TradeHelper.MAX_ROSTER
                || standardCount > TradeHelper.MAX_STANDARD_ROSTER)
            {
                r.RosterOutOfRangeCount++;
                r.RosterOutOfRange.Add(
                    $"{t.abbreviation}: {activeCount} activos ({standardCount}/{TradeHelper.MAX_STANDARD_ROSTER} estándar + {activeTwoWay}/{TradeHelper.MAX_TWO_WAY} two-way; {irCount} IR fuera)");
            }

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
                if (!PlayerAttributeScale.IsValid(value))
                    r.Add("GLEAGUE-ATTR", Error, who,
                        $"{name}={value} (fuera de {PlayerAttributeScale.MIN_VALUE}-{PlayerAttributeScale.MAX_VALUE}).");

            int canonical = p.GetCalculatedAverage();
            if (p.overall != canonical)
                r.Add("GLEAGUE-OVR", Error, who,
                    $"overall={p.overall} pero la media de sus atributos es {canonical}.");
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
