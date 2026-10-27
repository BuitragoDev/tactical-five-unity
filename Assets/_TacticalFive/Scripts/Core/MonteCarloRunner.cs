using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>Medidas que publica el laboratorio Monte Carlo.</summary>
public enum MonteCarloMetric
{
    Pace,
    Points,
    FieldGoalPercent,
    ThreePointPercent,
    FreeThrowAttempts,
    Turnovers,
    Assists,
    Rebounds,
    Fouls,
    Wins,
    PlayerDaysOut,
    PlayerGamesMissed,
    InjuryEvents,
    Players65Games,
    Rotation65Rate,
    GamesWithInjuryRate,
    FieldGoalsMade,
    FieldGoalAttempts,
    ThreePointAttempts,
    OffensiveRebounds,
    DefensiveRebounds,
    Blocks,
    OvertimeRate,
    OvertimePeriods,
    GameMargin,
    CloseGameRate
}

/// <summary>Resumen de una distribución de observaciones del laboratorio.</summary>
public sealed class MonteCarloMetricSummary
{
    public int samples;
    public double mean;
    /// <summary>Desviación típica poblacional (denominador N).</summary>
    public double standardDeviation;
    public double p5, p50, p95;
}

/// <summary>Configuración de temporada sintética. No lee ni escribe partidas.</summary>
public sealed class MonteCarloConfig
{
    public int seasons = 1;
    public int teams = 30;
    public int gamesPerTeam = 82;
    public ulong seed = 1;

    /// <summary>OVR medio del equipo más débil de la liga sintética (F3-05).</summary>
    public const double SYNTHETIC_BASE_OVR = 72.08;
    /// <summary>Diferencia de OVR entre el peor y el mejor equipo sintético. Calibrada
    /// para que victorias y puntos por equipo reproduzcan la paridad de la NBA 2024-25.
    /// Calibración de lesiones: 9,5 mantiene la cola alta de puntos dentro del perfil con menos lesiones.</summary>
    public const double SYNTHETIC_OVR_SPREAD = 9.5;
}

/// <summary>Resultado estadístico de una ejecución de laboratorio.</summary>
public sealed class MonteCarloReport
{
    public ulong seed;
    public int seasons;
    public int teams;
    public int gamesPerTeam;
    public int gamesSimulated;
    public int gamesWithOvertime;
    public int overtimePeriods;
    /// <summary>Mínimo de jugadores sanos en cualquier equipo antes de un partido.</summary>
    public int minimumHealthyPlayers;
    public double overtimeRatePercent => gamesSimulated > 0 ? 100.0 * gamesWithOvertime / gamesSimulated : 0;
    public Dictionary<MonteCarloMetric, MonteCarloMetricSummary> metrics = new();
    /// <summary>Distribuciones agregadas por equipo-temporada (una observación por equipo).</summary>
    public Dictionary<MonteCarloMetric, MonteCarloMetricSummary> teamSeasonMetrics = new();
    /// <summary>Desglose por equipo sintético para detectar desviaciones persistentes.</summary>
    public List<MonteCarloTeamSummary> teamBreakdowns = new();
    public MonteCarloOutcomeSummary outcomes = new();
    /// <summary>Duraciones observadas agrupadas por gravedad durante la ejecución.</summary>
    public Dictionary<InjurySeverity, MonteCarloInjurySeveritySummary> injuryDurations = new();
    /// <summary>Diagnóstico temporal de F3-05: recuento de márgenes finales 0..12.</summary>
    public Dictionary<int, int> marginHistogram = new();

    public string ToMarkdown()
    {
        var lines = new List<string>
        {
            "# Informe Monte Carlo — Tactical Five",
            "",
            $"- Semilla: `{seed}`",
            $"- Temporadas sintéticas: {seasons}",
            $"- Equipos: {teams}",
            $"- Partidos por equipo: {gamesPerTeam}",
            $"- Partidos simulados: {gamesSimulated}",
            $"- Partidos con prórroga: {gamesWithOvertime} ({F(overtimeRatePercent)} %); periodos OT: {overtimePeriods}",
            $"- Mínimo de jugadores sanos disponibles: {minimumHealthyPlayers}",
            "- Calendario: round-robin circular sintético, repetido hasta alcanzar los partidos por equipo.",
            "- Partidos sin persistencia; entre rondas hay un descanso y un día de partido, y las bajas cuentan días-jugador realmente no disponibles (truncados al final de temporada).",
            "",
            "| Métrica | Unidad de muestra | N | Media | Desv. típica | P5 | P50 | P95 |",
            "|---|---|---:|---:|---:|---:|---:|---:|"
        };

        foreach (MonteCarloMetric metric in Enum.GetValues(typeof(MonteCarloMetric)))
        {
            if (!metrics.TryGetValue(metric, out var value)) continue;
            lines.Add($"| {DisplayName(metric)} | {SampleUnit(metric)} | {value.samples} "
                + $"| {F(value.mean)} | {F(value.standardDeviation)} | {F(value.p5)} "
                + $"| {F(value.p50)} | {F(value.p95)} |");
        }
        lines.Add("");
        lines.Add("## Distribución agregada por equipo-temporada");
        lines.Add("");
        lines.Add("| Métrica | N equipos-temporada | Media | Desv. típica | P5 | P50 | P95 |");
        lines.Add("|---|---:|---:|---:|---:|---:|---:|");
        foreach (MonteCarloMetric metric in Enum.GetValues(typeof(MonteCarloMetric)))
        {
            if (!teamSeasonMetrics.TryGetValue(metric, out var value)) continue;
            lines.Add($"| {DisplayName(metric)} | {value.samples} | {F(value.mean)} "
                + $"| {F(value.standardDeviation)} | {F(value.p5)} | {F(value.p50)} | {F(value.p95)} |");
        }
        if (teamBreakdowns.Count > 0)
        {
            lines.Add("");
            lines.Add("## Consistencia por equipo sintético");
            lines.Add("");
            lines.Add("| Equipo | OVR | PTS | FG% | FGA | 3PA | FTA | TO | AST | OREB/DREB | W | Games missed | Mín. sanos |");
            lines.Add("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var team in teamBreakdowns.OrderBy(t => t.teamId))
            {
                double V(MonteCarloMetric m) => team.metrics.TryGetValue(m, out var s) ? s.mean : 0;
                lines.Add($"| {team.teamId} | {F(team.averageOverall)} | {F(V(MonteCarloMetric.Points))} "
                    + $"| {F(V(MonteCarloMetric.FieldGoalPercent))} | {F(V(MonteCarloMetric.FieldGoalAttempts))} "
                    + $"| {F(V(MonteCarloMetric.ThreePointAttempts))} | {F(V(MonteCarloMetric.FreeThrowAttempts))} "
                    + $"| {F(V(MonteCarloMetric.Turnovers))} | {F(V(MonteCarloMetric.Assists))} "
                    + $"| {F(V(MonteCarloMetric.OffensiveRebounds))}/{F(V(MonteCarloMetric.DefensiveRebounds))} "
                    + $"| {F(V(MonteCarloMetric.Wins))} | {F(V(MonteCarloMetric.PlayerGamesMissed))} "
                    + $"| {team.minimumHealthyPlayers} |");
            }
        }
        lines.AddRange(MonteCarloCalibrationGate.ToMarkdownLines(this));
        lines.AddRange(MonteCarloOutcomeGate.ToMarkdownLines(this));
        if (injuryDurations.Count > 0)
        {
            lines.Add("");
            lines.Add("## Duración de lesiones por gravedad");
            lines.Add("");
            lines.Add("| Gravedad | Episodios | Media observada (días) | Media esperada del catálogo | Rango catálogo | Rango observado |");
            lines.Add("|---|---:|---:|---:|---:|---:|");
            foreach (var severity in Enum.GetValues(typeof(InjurySeverity)).Cast<InjurySeverity>())
            {
                if (!injuryDurations.TryGetValue(severity, out var s)) continue;
                lines.Add($"| {InjuryCatalog.SeverityLabel(severity)} | {s.events} | {F(s.meanDays)} "
                    + $"| {F(s.catalogMeanDays)} | {s.catalogMinDays}–{s.catalogMaxDays} "
                    + $"| {s.observedMinDays}–{s.observedMaxDays} |");
            }
        }
        if (marginHistogram.Count > 0)
        {
            lines.Add("");
            lines.Add("## Histograma de márgenes finales (diagnóstico)");
            lines.Add("");
            lines.Add("| Margen | Partidos | % |");
            lines.Add("|---:|---:|---:|");
            for (int m = 0; m <= 12; m++)
            {
                marginHistogram.TryGetValue(m, out int count);
                lines.Add($"| {m} | {count} | {F(gamesSimulated > 0 ? 100.0 * count / gamesSimulated : 0)} |");
            }
        }
        return string.Join("\n", lines);
    }

    static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    static string DisplayName(MonteCarloMetric metric) => metric switch
    {
        MonteCarloMetric.Pace => "Ritmo",
        MonteCarloMetric.Points => "Puntos",
        MonteCarloMetric.FieldGoalPercent => "FG%",
        MonteCarloMetric.ThreePointPercent => "3P%",
        MonteCarloMetric.FreeThrowAttempts => "Tiros libres intentados",
        MonteCarloMetric.Turnovers => "Pérdidas",
        MonteCarloMetric.Assists => "Asistencias",
        MonteCarloMetric.Rebounds => "Rebotes",
        MonteCarloMetric.Fouls => "Faltas personales",
        MonteCarloMetric.Wins => "Victorias",
        MonteCarloMetric.PlayerDaysOut => "Días-jugador de baja",
        MonteCarloMetric.PlayerGamesMissed => "Partidos-jugador perdidos por lesión",
        MonteCarloMetric.InjuryEvents => "Lesiones nuevas",
        MonteCarloMetric.Players65Games => "Jugadores con ≥65 partidos",
        MonteCarloMetric.Rotation65Rate => "Rotación con ≥65 partidos (%)",
        MonteCarloMetric.GamesWithInjuryRate => "Partidos del equipo con lesión nueva (%)",
        MonteCarloMetric.FieldGoalsMade => "Canastas de campo",
        MonteCarloMetric.FieldGoalAttempts => "Tiros de campo intentados",
        MonteCarloMetric.ThreePointAttempts => "Triples intentados",
        MonteCarloMetric.OffensiveRebounds => "Rebotes ofensivos",
        MonteCarloMetric.DefensiveRebounds => "Rebotes defensivos",
        MonteCarloMetric.Blocks => "Tapones",
        MonteCarloMetric.OvertimeRate => "Partidos que llegan a prórroga (%)",
        MonteCarloMetric.OvertimePeriods => "Periodos de prórroga por partido",
        MonteCarloMetric.GameMargin => "Margen final (valor absoluto)",
        MonteCarloMetric.CloseGameRate => "Partidos decididos por 5 puntos o menos (%)",
        _ => metric.ToString()
    };

    static string SampleUnit(MonteCarloMetric metric) => metric switch
    {
        MonteCarloMetric.Pace => "equipo-partido (posesiones)",
        MonteCarloMetric.Points => "equipo-partido",
        MonteCarloMetric.FieldGoalPercent or MonteCarloMetric.ThreePointPercent => "equipo-partido (%)",
        MonteCarloMetric.FreeThrowAttempts or MonteCarloMetric.Turnovers
            or MonteCarloMetric.Assists or MonteCarloMetric.Rebounds or MonteCarloMetric.Fouls
            => "equipo-partido",
        MonteCarloMetric.Wins => "equipo-temporada",
        MonteCarloMetric.PlayerDaysOut => "equipo-temporada (días-jugador realmente indisponibles)",
        MonteCarloMetric.PlayerGamesMissed => "equipo-temporada (partidos de roster perdidos)",
        MonteCarloMetric.InjuryEvents => "equipo-temporada (episodios nuevos)",
        MonteCarloMetric.Players65Games => "equipo-temporada (jugadores de 15)",
        MonteCarloMetric.Rotation65Rate => "equipo-temporada (%)",
        MonteCarloMetric.GamesWithInjuryRate => "equipo-temporada (%)",
        MonteCarloMetric.FieldGoalsMade or MonteCarloMetric.FieldGoalAttempts
            or MonteCarloMetric.ThreePointAttempts or MonteCarloMetric.OffensiveRebounds
            or MonteCarloMetric.DefensiveRebounds or MonteCarloMetric.Blocks => "equipo-partido",
        MonteCarloMetric.OvertimeRate => "partido (0/100)",
        MonteCarloMetric.OvertimePeriods => "partido (periodos OT)",
        MonteCarloMetric.GameMargin => "partido (puntos)",
        MonteCarloMetric.CloseGameRate => "partido (0/100)",
        _ => "muestra"
    };
}

/// <summary>Medidas por temporada para validar récords, paridad, correlación y OT.</summary>
public sealed class MonteCarloOutcomeSummary
{
    public MonteCarloMetricSummary winStandardDeviation = new();
    public MonteCarloMetricSummary minimumWins = new();
    public MonteCarloMetricSummary maximumWins = new();
    public MonteCarloMetricSummary ovrWinCorrelation = new();
    public MonteCarloMetricSummary overtimeRatePercent = new();
    public MonteCarloMetricSummary overtimePeriodsPerGamePercent = new();
}

public sealed class MonteCarloInjurySeveritySummary
{
    public int events;
    public double meanDays;
    public int observedMinDays;
    public int observedMaxDays;
    public double catalogMeanDays;
    public int catalogMinDays;
    public int catalogMaxDays;
}

public sealed class MonteCarloTeamSummary
{
    public int teamId;
    public int seasons;
    public double averageOverall;
    public int minimumHealthyPlayers;
    public Dictionary<MonteCarloMetric, MonteCarloMetricSummary> metrics = new();
}

/// <summary>
/// Banco Monte Carlo autónomo (F3-02). Genera una liga sintética en memoria, juega
/// temporadas con `persistToDb:false` y una semilla inyectada, y devuelve distribuciones
/// de partido, victorias y días de lesión. No consulta `DatabaseManager`, no toca UI y no
/// escribe en la partida del usuario.
/// </summary>
public static class MonteCarloRunner
{
    static readonly int[] RosterOvrOffsets = { 7, 6, 5, 4, 3, 2, 1, 0, -1, -2, -3, -4, -5, -6, -7 };
    static readonly string[] Positions = { "PG", "SG", "SF", "PF", "C" };

    sealed class MetricSamples
    {
        readonly List<double> _values = new();

        public void Add(double value) => _values.Add(value);

        public MonteCarloMetricSummary Summarize()
        {
            if (_values.Count == 0) return new MonteCarloMetricSummary();
            double mean = _values.Average();
            double variance = _values.Sum(v => (v - mean) * (v - mean)) / _values.Count;
            var sorted = _values.OrderBy(v => v).ToArray();
            return new MonteCarloMetricSummary
            {
                samples = sorted.Length,
                mean = mean,
                standardDeviation = Math.Sqrt(variance),
                p5 = Percentile(sorted, 0.05),
                p50 = Percentile(sorted, 0.50),
                p95 = Percentile(sorted, 0.95)
            };
        }

        static double Percentile(double[] sorted, double percentile)
        {
            if (sorted.Length == 1) return sorted[0];
            double index = (sorted.Length - 1) * percentile;
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper) return sorted[lower];
            double fraction = index - lower;
            return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
        }
    }

    sealed class TeamState
    {
        public int id;
        public List<PlayerData> players;
        public int wins;
        public int playerDaysOut;
        public int playerGamesMissed;
        public int injuryEvents;
        public int gamesWithInjury;
        public readonly Dictionary<int, int> gamesPlayedByPlayer = new();
        public TeamSeasonTotals seasonTotals = new();
    }

    sealed class InjuryDurationAccumulator
    {
        public int events;
        public int totalDays;
        public int observedMinDays = int.MaxValue;
        public int observedMaxDays;

        public void Add(int days)
        {
            events++;
            totalDays += days;
            observedMinDays = Math.Min(observedMinDays, days);
            observedMaxDays = Math.Max(observedMaxDays, days);
        }
    }

    sealed class TeamSeasonTotals
    {
        public int games;
        public int points, fgm, fga, fg3m, fg3a, fta, turnovers, assists, rebounds, oreb, dreb, blocks, fouls;
        public int possessions;
        public int overtimeGames, overtimePeriods;

        public void Add(GameSimulator.TeamStats stats, int teamPoints, int teamPossessions, int otPeriods)
        {
            games++;
            points += teamPoints;
            fgm += stats.fgm;
            fga += stats.fga;
            fg3m += stats.fg3m;
            fg3a += stats.fg3a;
            fta += stats.fta;
            turnovers += stats.turnovers;
            assists += stats.assists;
            rebounds += stats.reb;
            oreb += stats.oreb;
            dreb += stats.dreb;
            blocks += stats.blocks;
            fouls += stats.pf;
            possessions += teamPossessions;
            if (otPeriods > 0) overtimeGames++;
            overtimePeriods += otPeriods;
        }

        public double Value(MonteCarloMetric metric)
        {
            if (games <= 0) return 0;
            return metric switch
            {
                MonteCarloMetric.Pace => (double)possessions / games,
                MonteCarloMetric.Points => (double)points / games,
                MonteCarloMetric.FieldGoalPercent => fga > 0 ? 100.0 * fgm / fga : 0,
                MonteCarloMetric.ThreePointPercent => fg3a > 0 ? 100.0 * fg3m / fg3a : 0,
                MonteCarloMetric.FreeThrowAttempts => (double)fta / games,
                MonteCarloMetric.Turnovers => (double)turnovers / games,
                MonteCarloMetric.Assists => (double)assists / games,
                MonteCarloMetric.Rebounds => (double)rebounds / games,
                MonteCarloMetric.Fouls => (double)fouls / games,
                MonteCarloMetric.FieldGoalsMade => (double)fgm / games,
                MonteCarloMetric.FieldGoalAttempts => (double)fga / games,
                MonteCarloMetric.ThreePointAttempts => (double)fg3a / games,
                MonteCarloMetric.OffensiveRebounds => (double)oreb / games,
                MonteCarloMetric.DefensiveRebounds => (double)dreb / games,
                MonteCarloMetric.Blocks => (double)blocks / games,
                MonteCarloMetric.OvertimeRate => 100.0 * overtimeGames / games,
                MonteCarloMetric.OvertimePeriods => (double)overtimePeriods / games,
                _ => 0
            };
        }
    }

    sealed class TeamGamePair
    {
        public int first;
        public int second;
    }

    public static MonteCarloReport Run(MonteCarloConfig config)
    {
        Validate(config);
        var samples = Enum.GetValues(typeof(MonteCarloMetric))
            .Cast<MonteCarloMetric>()
            .ToDictionary(metric => metric, _ => new MetricSamples());
        var teamSeasonSamples = Enum.GetValues(typeof(MonteCarloMetric))
            .Cast<MonteCarloMetric>()
            .ToDictionary(metric => metric, _ => new MetricSamples());
        var perTeamSamples = Enumerable.Range(1, config.teams).ToDictionary(
            teamId => teamId,
            _ => Enum.GetValues(typeof(MonteCarloMetric)).Cast<MonteCarloMetric>()
                .ToDictionary(metric => metric, __ => new MetricSamples()));
        var perTeamMinimumHealthy = Enumerable.Range(1, config.teams).ToDictionary(teamId => teamId, _ => 12);
        var outcomeSamples = new MonteCarloOutcomeSummary();
        var seasonWinStdDev = new MetricSamples();
        var seasonMinWins = new MetricSamples();
        var seasonMaxWins = new MetricSamples();
        var seasonOvrWinCorrelation = new MetricSamples();
        var seasonOvertimeRate = new MetricSamples();
        var seasonOvertimePeriodsPerGame = new MetricSamples();
        var teamOverallById = new Dictionary<int, double>();
        var injuryDurationAccumulators = Enum.GetValues(typeof(InjurySeverity)).Cast<InjurySeverity>()
            .ToDictionary(severity => severity, _ => new InjuryDurationAccumulator());
        var random = new SeededRandomSource(config.seed);
        int gameId = 1;
        int gamesWithOvertime = 0;
        int overtimePeriods = 0;
        int minimumHealthyPlayers = int.MaxValue;
        var marginHistogram = new Dictionary<int, int>();

        for (int season = 0; season < config.seasons; season++)
        {
            var teams = Enumerable.Range(0, config.teams)
                .Select(index => CreateTeam(index + 1, config.teams))
                .ToArray();
            int seasonOvertimeGames = 0;
            int seasonOvertimePeriods = 0;
            foreach (var team in teams)
                teamOverallById[team.id] = team.players.Average(p => p.overall);
            var rounds = BuildRoundRobin(config.teams);

            for (int roundIndex = 0; roundIndex < config.gamesPerTeam; roundIndex++)
            {
                // Las rondas se espacian dos días: un descanso (+8 físico) y el siguiente
                // día de partido. Así las lesiones evolucionan en días calendario y se
                // miden las bajas reales dentro de la temporada, no la duración potencial.
                if (roundIndex > 0)
                {
                    foreach (var team in teams)
                        foreach (var player in team.players)
                        {
                            AdvanceCalendarDay(player, team, restDay: true);
                            AdvanceCalendarDay(player, team, restDay: false);
                        }
                }

                int cycle = roundIndex / rounds.Count;
                var round = rounds[roundIndex % rounds.Count];
                foreach (var pairing in round)
                {
                    int homeIndex = (cycle % 2 == 0) ? pairing.first : pairing.second;
                    int awayIndex = (cycle % 2 == 0) ? pairing.second : pairing.first;
                    var home = teams[homeIndex];
                    var away = teams[awayIndex];
                    home.playerGamesMissed += home.players.Count(p => p.injury_days > 0);
                    away.playerGamesMissed += away.players.Count(p => p.injury_days > 0);
                    int homeHealthy = home.players.Count(p => p.injury_days <= 0);
                    int awayHealthy = away.players.Count(p => p.injury_days <= 0);
                    perTeamMinimumHealthy[home.id] = Math.Min(perTeamMinimumHealthy[home.id], homeHealthy);
                    perTeamMinimumHealthy[away.id] = Math.Min(perTeamMinimumHealthy[away.id], awayHealthy);
                    minimumHealthyPlayers = Math.Min(minimumHealthyPlayers, Math.Min(homeHealthy, awayHealthy));
                    var game = new GameData
                    {
                        id = gameId++,
                        season_id = season + 1,
                        manager_id = 0,
                        game_day = roundIndex * 2 + 1,
                        game_date = "2000-01-01",
                        home_team_id = home.id,
                        away_team_id = away.id,
                        game_type = "regular"
                    };

                    var result = GameSimulator.SimulateGame(game, home.players, away.players,
                        persistToDb: false,
                        random: random,
                        simulatePlayerStateWhenNotPersisting: true);

                    // Disponibilidad para premios: son los partidos con minutos reales,
                    // el mismo criterio que player_game_stats (minutes > 0).
                    foreach (var ps in result.home_stats)
                        if (ps.minutes > 0)
                            home.gamesPlayedByPlayer[ps.player_id] =
                                home.gamesPlayedByPlayer.TryGetValue(ps.player_id, out int hp) ? hp + 1 : 1;
                    foreach (var ps in result.away_stats)
                        if (ps.minutes > 0)
                            away.gamesPlayedByPlayer[ps.player_id] =
                                away.gamesPlayedByPlayer.TryGetValue(ps.player_id, out int ap) ? ap + 1 : 1;

                    int homeInjuries = result.injuries.Count(i => home.players.Any(p => p.id == i.player_id));
                    int awayInjuries = result.injuries.Count(i => away.players.Any(p => p.id == i.player_id));
                    home.injuryEvents += homeInjuries;
                    away.injuryEvents += awayInjuries;
                    if (homeInjuries > 0) home.gamesWithInjury++;
                    if (awayInjuries > 0) away.gamesWithInjury++;

                    foreach (var injury in result.injuries)
                    {
                        var definition = InjuryCatalog.ByName(injury.type);
                        if (definition != null)
                            injuryDurationAccumulators[definition.severity].Add(injury.days);
                    }

                    int gameOvertimePeriods = Math.Max(0, result.quarters.Count - 4);
                    if (gameOvertimePeriods > 0)
                    {
                        gamesWithOvertime++;
                        seasonOvertimeGames++;
                    }
                    overtimePeriods += gameOvertimePeriods;
                    seasonOvertimePeriods += gameOvertimePeriods;
                    double overtimeFlag = gameOvertimePeriods > 0 ? 100.0 : 0.0;
                    samples[MonteCarloMetric.OvertimeRate].Add(overtimeFlag);
                    samples[MonteCarloMetric.OvertimePeriods].Add(gameOvertimePeriods);

                    // Paridad (F3-05): margen final absoluto y cuota de partidos igualados.
                    int finalMargin = Math.Abs(result.home_score - result.away_score);
                    samples[MonteCarloMetric.GameMargin].Add(finalMargin);
                    samples[MonteCarloMetric.CloseGameRate].Add(finalMargin <= 5 ? 100.0 : 0.0);
                    if (finalMargin <= 12)
                    {
                        marginHistogram.TryGetValue(finalMargin, out int bucket);
                        marginHistogram[finalMargin] = bucket + 1;
                    }

                    samples[MonteCarloMetric.Pace].Add(
                        (result.possessions.homePossessions + result.possessions.awayPossessions) / 2.0);
                    AddTeamGameSamples(samples, result.home_team_stats, result.home_score);
                    AddTeamGameSamples(samples, result.away_team_stats, result.away_score);
                    home.seasonTotals.Add(result.home_team_stats, result.home_score,
                        result.possessions.homePossessions, gameOvertimePeriods);
                    away.seasonTotals.Add(result.away_team_stats, result.away_score,
                        result.possessions.awayPossessions, gameOvertimePeriods);

                    if (result.home_score > result.away_score) home.wins++;
                    else away.wins++;

                }
            }

            foreach (var team in teams)
            {
                samples[MonteCarloMetric.Wins].Add(team.wins);
                samples[MonteCarloMetric.PlayerDaysOut].Add(team.playerDaysOut);
                samples[MonteCarloMetric.PlayerGamesMissed].Add(team.playerGamesMissed);
                samples[MonteCarloMetric.InjuryEvents].Add(team.injuryEvents);
                samples[MonteCarloMetric.Players65Games].Add(CountPlayers65(team));
                samples[MonteCarloMetric.Rotation65Rate].Add(Rotation65Rate(team));
                samples[MonteCarloMetric.GamesWithInjuryRate].Add(GamesWithInjuryRate(team));
                foreach (var (metric, values) in perTeamSamples[team.id])
                    values.Add(TeamSeasonValue(team, metric));
                foreach (MonteCarloMetric metric in Enum.GetValues(typeof(MonteCarloMetric)))
                {
                    // Margen y partidos igualados son medidas de partido, no de equipo-temporada.
                    if (metric is MonteCarloMetric.GameMargin or MonteCarloMetric.CloseGameRate) continue;
                    double value = metric switch
                    {
                        MonteCarloMetric.Wins => team.wins,
                        MonteCarloMetric.PlayerDaysOut => team.playerDaysOut,
                        MonteCarloMetric.PlayerGamesMissed => team.playerGamesMissed,
                        MonteCarloMetric.InjuryEvents => team.injuryEvents,
                        MonteCarloMetric.Players65Games => CountPlayers65(team),
                        MonteCarloMetric.Rotation65Rate => Rotation65Rate(team),
                        MonteCarloMetric.GamesWithInjuryRate => GamesWithInjuryRate(team),
                        _ => team.seasonTotals.Value(metric)
                    };
                    teamSeasonSamples[metric].Add(value);
                }
            }

            int[] seasonWins = teams.Select(t => t.wins).ToArray();
            seasonWinStdDev.Add(PopulationStandardDeviation(seasonWins.Select(w => (double)w)));
            seasonMinWins.Add(seasonWins.Min());
            seasonMaxWins.Add(seasonWins.Max());
            seasonOvrWinCorrelation.Add(PearsonCorrelation(
                teams.Select(t => teamOverallById[t.id]).ToArray(),
                seasonWins.Select(w => (double)w).ToArray()));
            int seasonGames = config.teams * config.gamesPerTeam / 2;
            seasonOvertimeRate.Add(100.0 * seasonOvertimeGames / seasonGames);
            seasonOvertimePeriodsPerGame.Add(100.0 * seasonOvertimePeriods / seasonGames);
        }

        var report = new MonteCarloReport
        {
            seed = config.seed,
            seasons = config.seasons,
            teams = config.teams,
            gamesPerTeam = config.gamesPerTeam,
            gamesSimulated = gameId - 1,
            gamesWithOvertime = gamesWithOvertime,
            overtimePeriods = overtimePeriods,
            minimumHealthyPlayers = minimumHealthyPlayers == int.MaxValue ? 0 : minimumHealthyPlayers,
            marginHistogram = marginHistogram,
            outcomes = new MonteCarloOutcomeSummary
            {
                winStandardDeviation = seasonWinStdDev.Summarize(),
                minimumWins = seasonMinWins.Summarize(),
                maximumWins = seasonMaxWins.Summarize(),
                ovrWinCorrelation = seasonOvrWinCorrelation.Summarize(),
                overtimeRatePercent = seasonOvertimeRate.Summarize(),
                overtimePeriodsPerGamePercent = seasonOvertimePeriodsPerGame.Summarize()
            }
        };
        foreach (var (metric, values) in samples)
            report.metrics[metric] = values.Summarize();
        foreach (var (metric, values) in teamSeasonSamples)
            report.teamSeasonMetrics[metric] = values.Summarize();
        foreach (var (severity, accumulator) in injuryDurationAccumulators)
        {
            if (accumulator.events == 0) continue;
            var definitions = InjuryCatalog.All.Where(d => d.severity == severity).ToArray();
            int totalWeight = definitions.Sum(d => d.weight);
            report.injuryDurations[severity] = new MonteCarloInjurySeveritySummary
            {
                events = accumulator.events,
                meanDays = (double)accumulator.totalDays / accumulator.events,
                observedMinDays = accumulator.observedMinDays,
                observedMaxDays = accumulator.observedMaxDays,
                catalogMeanDays = totalWeight > 0
                    ? definitions.Sum(d => (d.minDays + d.maxDays) * 0.5 * d.weight) / totalWeight
                    : 0,
                catalogMinDays = definitions.Length > 0 ? definitions.Min(d => d.minDays) : 0,
                catalogMaxDays = definitions.Length > 0 ? definitions.Max(d => d.maxDays) : 0
            };
        }
        foreach (var (teamId, values) in perTeamSamples)
        {
            var teamSummary = new MonteCarloTeamSummary
            {
                teamId = teamId,
                seasons = config.seasons,
                averageOverall = teamOverallById[teamId],
                minimumHealthyPlayers = perTeamMinimumHealthy[teamId]
            };
            foreach (var (metric, metricSamples) in values)
                teamSummary.metrics[metric] = metricSamples.Summarize();
            report.teamBreakdowns.Add(teamSummary);
        }
        return report;
    }

    static double TeamSeasonValue(TeamState team, MonteCarloMetric metric) => metric switch
    {
        MonteCarloMetric.Wins => team.wins,
        MonteCarloMetric.PlayerDaysOut => team.playerDaysOut,
        MonteCarloMetric.PlayerGamesMissed => team.playerGamesMissed,
        MonteCarloMetric.InjuryEvents => team.injuryEvents,
        MonteCarloMetric.Players65Games => CountPlayers65(team),
        MonteCarloMetric.Rotation65Rate => Rotation65Rate(team),
        MonteCarloMetric.GamesWithInjuryRate => GamesWithInjuryRate(team),
        _ => team.seasonTotals.Value(metric)
    };

    static int CountPlayers65(TeamState team)
        => team.gamesPlayedByPlayer.Values.Count(games => games >= 65);

    static double Rotation65Rate(TeamState team)
    {
        int rotationPlayers = team.gamesPlayedByPlayer.Values.Count(games => games >= 20);
        if (rotationPlayers == 0) return 0;
        int qualified = team.gamesPlayedByPlayer.Values.Count(games => games >= 65);
        return 100.0 * qualified / rotationPlayers;
    }

    static double GamesWithInjuryRate(TeamState team)
        => team.seasonTotals.games > 0
            ? 100.0 * team.gamesWithInjury / team.seasonTotals.games
            : 0;

    static double PopulationStandardDeviation(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return 0;
        double mean = list.Average();
        return Math.Sqrt(list.Sum(v => (v - mean) * (v - mean)) / list.Count);
    }

    static double PearsonCorrelation(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        if (x == null || y == null || x.Count != y.Count || x.Count < 2) return 0;
        double meanX = x.Average(), meanY = y.Average();
        double covariance = 0, varianceX = 0, varianceY = 0;
        for (int i = 0; i < x.Count; i++)
        {
            double dx = x[i] - meanX, dy = y[i] - meanY;
            covariance += dx * dy;
            varianceX += dx * dx;
            varianceY += dy * dy;
        }
        double denominator = Math.Sqrt(varianceX * varianceY);
        return denominator <= 0 ? 0 : covariance / denominator;
    }

    static void Validate(MonteCarloConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (config.seasons < 1) throw new ArgumentOutOfRangeException(nameof(config.seasons), "Debe simularse al menos una temporada.");
        if (config.teams < 2 || config.teams % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(config.teams), "La liga sintética requiere un número par de equipos (mínimo 2).");
        if (config.gamesPerTeam < 1)
            throw new ArgumentOutOfRangeException(nameof(config.gamesPerTeam), "Cada equipo debe jugar al menos un partido.");
    }

    static TeamState CreateTeam(int teamId, int teamCount)
    {
        // Los offsets suman 0. La liga sintética conserva variación entre equipos, pero
        // su dispersión está calibrada a la paridad de la NBA 2024-25 (F3-05): los
        // equipos sintéticos deben repartir victorias y puntos como los reales, no como
        // el seed. No pretende clonar franquicias. 15 jugadores (roster NBA de referencia), sin DB.
        double fraction = teamCount <= 1 ? 0.0 : (teamId - 1) / (double)(teamCount - 1);
        float teamBase = (float)(MonteCarloConfig.SYNTHETIC_BASE_OVR
            + MonteCarloConfig.SYNTHETIC_OVR_SPREAD * Math.Pow(fraction, 1.5));
        var players = new List<PlayerData>(RosterOvrOffsets.Length);
        for (int i = 0; i < RosterOvrOffsets.Length; i++)
        {
            int targetOverall = Math.Max(55, Math.Min(95, (int)Math.Round(teamBase + RosterOvrOffsets[i])));
            string position = Positions[i % Positions.Length];
            int attributeShift = (i % 3 - 1) * 3;
            int speed = ClampAttribute(targetOverall + attributeShift);
            int shooting = ClampAttribute(targetOverall + (i % 4 - 1) * 2);
            int threePoint = ClampAttribute(targetOverall + (i % 5 - 2) * 2);
            int passing = ClampAttribute(targetOverall + attributeShift);
            int dribbling = ClampAttribute(targetOverall + attributeShift);
            int defense = ClampAttribute(targetOverall + attributeShift);
            int rebounding = ClampAttribute(targetOverall + attributeShift);
            int athleticism = ClampAttribute(targetOverall + attributeShift);
            int iq = ClampAttribute(targetOverall + attributeShift);
            int steals = ClampAttribute(targetOverall + attributeShift);
            int blocks = ClampAttribute(targetOverall + attributeShift);
            int potential = Math.Min(99, targetOverall + 8);
            int overall = PlayerRating.FromAttributes(speed, shooting, threePoint, passing,
                dribbling, defense, rebounding, athleticism, iq, steals, blocks, potential);
            var player = new PlayerData
            {
                id = teamId * 1000 + i + 1,
                team_id = teamId,
                first_name = $"J{teamId}",
                last_name = $"P{i + 1}",
                position = position,
                secondary_position = Positions[(i + 1) % Positions.Length],
                age = 24 + i % 8,
                overall = overall,
                potential = potential,
                speed = speed,
                shooting = shooting,
                three_point = threePoint,
                passing = passing,
                dribbling = dribbling,
                defense = defense,
                rebounding = rebounding,
                athleticism = athleticism,
                iq = iq,
                steals = steals,
                blocks = blocks,
                role = PlayerRoleRules.FromOverall(overall),
                morale = 50,
                fisico = 99,
                rotation_minutes = -1,
                injury_days = 0,
                recovery_risk = 0
            };
            players.Add(player);
        }

        return new TeamState { id = teamId, players = players };
    }

    static int ClampAttribute(int value) => Math.Max(PlayerAttributeScale.MIN_VALUE, Math.Min(PlayerAttributeScale.MAX_VALUE, value));

    static List<List<TeamGamePair>> BuildRoundRobin(int teamCount)
    {
        var rotation = Enumerable.Range(0, teamCount).ToList();
        var rounds = new List<List<TeamGamePair>>(teamCount - 1);
        for (int roundIndex = 0; roundIndex < teamCount - 1; roundIndex++)
        {
            var round = new List<TeamGamePair>(teamCount / 2);
            for (int i = 0; i < teamCount / 2; i++)
                round.Add(new TeamGamePair { first = rotation[i], second = rotation[teamCount - 1 - i] });
            rounds.Add(round);

            // Método del círculo: el primer equipo queda fijo; los demás rotan una plaza.
            int last = rotation[^1];
            for (int i = teamCount - 1; i > 1; i--) rotation[i] = rotation[i - 1];
            rotation[1] = last;
        }
        return rounds;
    }

    static void AdvanceCalendarDay(PlayerData player, TeamState team, bool restDay)
    {
        if (restDay) player.fisico = Math.Min(99, player.fisico + 8);
        if (player.injury_days > 0)
        {
            player.injury_days = Math.Max(0, player.injury_days - 1);
            if (player.injury_days > 0) team.playerDaysOut++;
            else
            {
                player.injury_type = null;
                player.injury_total_days = 0;
                player.treated = 0;
            }
        }
    }

    static void AddTeamGameSamples(Dictionary<MonteCarloMetric, MetricSamples> samples,
        GameSimulator.TeamStats stats, int points)
    {
        samples[MonteCarloMetric.Points].Add(points);
        if (stats.fga > 0)
            samples[MonteCarloMetric.FieldGoalPercent].Add(100.0 * stats.fgm / stats.fga);
        if (stats.fg3a > 0)
            samples[MonteCarloMetric.ThreePointPercent].Add(100.0 * stats.fg3m / stats.fg3a);
        samples[MonteCarloMetric.FreeThrowAttempts].Add(stats.fta);
        samples[MonteCarloMetric.Turnovers].Add(stats.turnovers);
        samples[MonteCarloMetric.Assists].Add(stats.assists);
        samples[MonteCarloMetric.Rebounds].Add(stats.reb);
        samples[MonteCarloMetric.Fouls].Add(stats.pf);
        samples[MonteCarloMetric.FieldGoalsMade].Add(stats.fgm);
        samples[MonteCarloMetric.FieldGoalAttempts].Add(stats.fga);
        samples[MonteCarloMetric.ThreePointAttempts].Add(stats.fg3a);
        samples[MonteCarloMetric.OffensiveRebounds].Add(stats.oreb);
        samples[MonteCarloMetric.DefensiveRebounds].Add(stats.dreb);
        samples[MonteCarloMetric.Blocks].Add(stats.blocks);
    }
}
