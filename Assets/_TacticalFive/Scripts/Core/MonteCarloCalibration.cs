using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>Objetivo versionado de una métrica y sus márgenes de aceptación.</summary>
public sealed class MonteCarloCalibrationTarget
{
    public MonteCarloMetric metric { get; }
    public double referenceMean { get; }
    public double meanTolerance { get; }
    public double? teamSeasonP5Reference { get; }
    public double? teamSeasonP5Tolerance { get; }
    public double? teamSeasonP50Reference { get; }
    public double? teamSeasonP50Tolerance { get; }
    public double? teamSeasonP95Reference { get; }
    public double? teamSeasonP95Tolerance { get; }
    public double? teamMinReference { get; }
    public double? teamMaxReference { get; }
    public double? teamTolerance { get; }
    public string source { get; }
    public string scope { get; }

    public double MeanMin => referenceMean - meanTolerance;
    public double MeanMax => referenceMean + meanTolerance;

    public MonteCarloCalibrationTarget(MonteCarloMetric metric, double referenceMean,
        double meanTolerance, string source, string scope,
        double? teamSeasonP5Reference = null, double? teamSeasonP5Tolerance = null,
        double? teamSeasonP50Reference = null, double? teamSeasonP50Tolerance = null,
        double? teamSeasonP95Reference = null, double? teamSeasonP95Tolerance = null,
        double? teamMinReference = null, double? teamMaxReference = null,
        double? teamTolerance = null)
    {
        this.metric = metric;
        this.referenceMean = referenceMean;
        this.meanTolerance = meanTolerance;
        this.teamSeasonP5Reference = teamSeasonP5Reference;
        this.teamSeasonP5Tolerance = teamSeasonP5Tolerance;
        this.teamSeasonP50Reference = teamSeasonP50Reference;
        this.teamSeasonP50Tolerance = teamSeasonP50Tolerance;
        this.teamSeasonP95Reference = teamSeasonP95Reference;
        this.teamSeasonP95Tolerance = teamSeasonP95Tolerance;
        this.teamMinReference = teamMinReference;
        this.teamMaxReference = teamMaxReference;
        this.teamTolerance = teamTolerance;
        this.source = source;
        this.scope = scope;
    }
}

/// <summary>Violación de una banda de aceptación del perfil.</summary>
public sealed class MonteCarloCalibrationFailure
{
    public MonteCarloMetric metric;
    public string statistic;
    public double observed;
    public double minimum;
    public double maximum;

    public override string ToString()
        => $"{metric}.{statistic}: observado {F(observed)}, aceptado [{F(minimum)}, {F(maximum)}]";

    static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
}

/// <summary>
/// Perfil versionado a la temporada NBA 2024-25. v3 mantiene las bandas ofensivas y de
/// victorias de v2 y sustituye la puerta de lesiones por una de jugabilidad: 12–18
/// lesiones nuevas por equipo-temporada, disponibilidad para premios y un máximo de
/// partidos con parte médico. Las bandas son umbrales de ingeniería (referencia ±
/// tolerancia declarada), no intervalos de confianza.
/// </summary>
public static class MonteCarloCalibrationProfile
{
    public const string VERSION = "NBA-2024-25-v3";
    public const string TEAM_STATS_SOURCE = "Basketball-Reference, NBA League Averages — Per Game, 2024-25";
    public const string WINS_SOURCE = "Basketball-Reference, NBA 2024-25 Standings (30 equipos)";
    public const string INJURY_SOURCE = "Diseño del juego: disponibilidad para premios (65 partidos) y 12–18 lesiones nuevas por equipo-temporada";

    static readonly MonteCarloCalibrationTarget[] _targets =
    {
        // Referencia por equipo-partido: tolerancias absolutas en unidades de cada métrica;
        // en porcentajes son puntos porcentuales, no fracción relativa.
        new(MonteCarloMetric.Pace, 98.8, 1.5, TEAM_STATS_SOURCE, "media de ritmo por equipo-partido"),
        new(MonteCarloMetric.Points, 113.8, 3.5, TEAM_STATS_SOURCE, "media de puntos por equipo-partido",
            teamSeasonP5Reference: 105.235, teamSeasonP5Tolerance: 4.0,
            teamSeasonP50Reference: 114.05, teamSeasonP50Tolerance: 3.5,
            teamSeasonP95Reference: 121.295, teamSeasonP95Tolerance: 5.0,
            teamMinReference: 105.1, teamMaxReference: 121.9, teamTolerance: 6.0),
        new(MonteCarloMetric.FieldGoalPercent, 46.7, 1.5, TEAM_STATS_SOURCE, "media de FG% por equipo-partido",
            teamSeasonP5Reference: 43.79, teamSeasonP5Tolerance: 2.5,
            teamSeasonP50Reference: 46.9, teamSeasonP50Tolerance: 1.5,
            teamSeasonP95Reference: 48.965, teamSeasonP95Tolerance: 2.0,
            teamMinReference: 43.0, teamMaxReference: 50.6, teamTolerance: 2.1),
        new(MonteCarloMetric.ThreePointPercent, 36.0, 2.0, TEAM_STATS_SOURCE, "media de 3P% por equipo-partido",
            teamSeasonP5Reference: 33.68, teamSeasonP5Tolerance: 2.5,
            teamSeasonP50Reference: 36.4, teamSeasonP50Tolerance: 2.0,
            teamSeasonP95Reference: 38.075, teamSeasonP95Tolerance: 2.5,
            teamMinReference: 31.8, teamMaxReference: 38.7, teamTolerance: 3.0),
        new(MonteCarloMetric.FreeThrowAttempts, 21.7, 2.1, TEAM_STATS_SOURCE, "media de FTA por equipo-partido",
            teamSeasonP5Reference: 19.745, teamSeasonP5Tolerance: 2.5,
            teamSeasonP50Reference: 21.85, teamSeasonP50Tolerance: 2.0,
            teamSeasonP95Reference: 23.255, teamSeasonP95Tolerance: 2.5,
            teamMinReference: 19.1, teamMaxReference: 23.3, teamTolerance: 3.0),
        new(MonteCarloMetric.Turnovers, 14.3, 2.0, TEAM_STATS_SOURCE, "media de pérdidas por equipo-partido",
            teamSeasonP5Reference: 12.485, teamSeasonP5Tolerance: 2.0,
            teamSeasonP50Reference: 14.15, teamSeasonP50Tolerance: 2.0,
            teamSeasonP95Reference: 15.865, teamSeasonP95Tolerance: 2.0,
            teamMinReference: 11.7, teamMaxReference: 17.2, teamTolerance: 3.0),
        new(MonteCarloMetric.Assists, 26.5, 2.0, TEAM_STATS_SOURCE, "media de asistencias por equipo-partido",
            teamSeasonP5Reference: 23.245, teamSeasonP5Tolerance: 2.5,
            teamSeasonP50Reference: 26.25, teamSeasonP50Tolerance: 2.0,
            teamSeasonP95Reference: 29.42, teamSeasonP95Tolerance: 2.5,
            teamMinReference: 23.0, teamMaxReference: 31.0, teamTolerance: 4.0),
        new(MonteCarloMetric.Rebounds, 44.1, 3.0, TEAM_STATS_SOURCE, "media de rebotes por equipo-partido"),
        new(MonteCarloMetric.Fouls, 18.6, 2.0, TEAM_STATS_SOURCE, "media de faltas personales por equipo-partido"),

        // Distribución de temporada sobre los 30 equipos. Los percentiles usan la misma
        // interpolación lineal de MonteCarloRunner; tolerancia explícita de ±5 victorias.
        new(MonteCarloMetric.Wins, 41.0, 1.0, WINS_SOURCE, "victorias por equipo-temporada",
            teamSeasonP5Reference: 18.45, teamSeasonP5Tolerance: 5.0,
            teamSeasonP50Reference: 40.5, teamSeasonP50Tolerance: 5.0,
            teamSeasonP95Reference: 62.65, teamSeasonP95Tolerance: 5.0),

        // Puerta de jugabilidad (calibración de lesiones): no se copia el volumen bruto de la NBA
        // (273–536 man-games con plantillas de 17–20), porque el juego usa 15 jugadores y
        // exige 65 partidos para premios. Se prioriza que la rotación llegue a los premios.
        new(MonteCarloMetric.PlayerGamesMissed, 45.0, 20.0, INJURY_SOURCE,
            "partidos-jugador perdidos por equipo-temporada"),
        new(MonteCarloMetric.InjuryEvents, 15.0, 3.0, INJURY_SOURCE,
            "lesiones nuevas por equipo-temporada"),
        new(MonteCarloMetric.Players65Games, 11.0, 1.5, INJURY_SOURCE,
            "jugadores con ≥65 partidos por equipo-temporada (incluye DNPs)"),
        new(MonteCarloMetric.Rotation65Rate, 72.0, 10.0, INJURY_SOURCE,
            "porcentaje de jugadores con ≥20 partidos que alcanzan 65"),
        new(MonteCarloMetric.GamesWithInjuryRate, 17.0, 5.0, INJURY_SOURCE,
            "porcentaje de partidos del equipo con al menos una lesión nueva")
    };

    public static IReadOnlyList<MonteCarloCalibrationTarget> Targets => Array.AsReadOnly(_targets);
}

/// <summary>Evalúa un informe contra el perfil de aceptación versionado F3-03.</summary>
public static class MonteCarloCalibrationGate
{
    static readonly HashSet<MonteCarloMetric> F3_04_OFFENSE_METRICS = new()
    {
        MonteCarloMetric.Points,
        MonteCarloMetric.FieldGoalPercent,
        MonteCarloMetric.ThreePointPercent,
        MonteCarloMetric.FreeThrowAttempts,
        MonteCarloMetric.Turnovers,
        MonteCarloMetric.Assists
    };

    public static List<MonteCarloCalibrationFailure> Evaluate(MonteCarloReport report)
        => EvaluateTargets(report, MonteCarloCalibrationProfile.Targets);

    /// <summary>Gate F3-04 aislado: stats ofensivas, sin mezclar victorias ni lesiones.</summary>
    public static List<MonteCarloCalibrationFailure> EvaluateOffense(MonteCarloReport report)
        => EvaluateTargets(report, MonteCarloCalibrationProfile.Targets
            .Where(t => F3_04_OFFENSE_METRICS.Contains(t.metric)));

    public static void AssertOffenseAccepted(MonteCarloReport report)
    {
        var failures = EvaluateOffense(report);
        if (failures.Count > 0)
            throw new InvalidOperationException($"Gate ofensivo {MonteCarloCalibrationProfile.VERSION}: "
                + string.Join("; ", failures));
    }

    static List<MonteCarloCalibrationFailure> EvaluateTargets(MonteCarloReport report,
        IEnumerable<MonteCarloCalibrationTarget> targets)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        var failures = new List<MonteCarloCalibrationFailure>();
        foreach (var target in targets)
        {
            if (!report.metrics.TryGetValue(target.metric, out var summary) || summary == null || summary.samples == 0)
            {
                failures.Add(new MonteCarloCalibrationFailure
                {
                    metric = target.metric,
                    statistic = "mean",
                    observed = double.NaN,
                    minimum = target.MeanMin,
                    maximum = target.MeanMax
                });
                continue;
            }

            AddIfOutside(failures, target.metric, "mean", summary.mean, target.MeanMin, target.MeanMax);

            bool hasSeasonTargets = target.teamSeasonP5Reference.HasValue
                || target.teamSeasonP50Reference.HasValue || target.teamSeasonP95Reference.HasValue;
            if (!hasSeasonTargets) continue;
            if (!report.teamSeasonMetrics.TryGetValue(target.metric, out var seasonSummary)
                || seasonSummary == null || seasonSummary.samples == 0)
            {
                AddIfOutside(failures, target.metric, "team-season P5", double.NaN, 0, 0);
                continue;
            }
            if (target.teamSeasonP5Reference.HasValue && target.teamSeasonP5Tolerance.HasValue)
                AddIfOutside(failures, target.metric, "team-season P5", seasonSummary.p5,
                    target.teamSeasonP5Reference.Value - target.teamSeasonP5Tolerance.Value,
                    target.teamSeasonP5Reference.Value + target.teamSeasonP5Tolerance.Value);
            if (target.teamSeasonP50Reference.HasValue && target.teamSeasonP50Tolerance.HasValue)
                AddIfOutside(failures, target.metric, "team-season P50", seasonSummary.p50,
                    target.teamSeasonP50Reference.Value - target.teamSeasonP50Tolerance.Value,
                    target.teamSeasonP50Reference.Value + target.teamSeasonP50Tolerance.Value);
            if (target.teamSeasonP95Reference.HasValue && target.teamSeasonP95Tolerance.HasValue)
                AddIfOutside(failures, target.metric, "team-season P95", seasonSummary.p95,
                    target.teamSeasonP95Reference.Value - target.teamSeasonP95Tolerance.Value,
                    target.teamSeasonP95Reference.Value + target.teamSeasonP95Tolerance.Value);

            if (target.teamMinReference.HasValue && target.teamMaxReference.HasValue
                && target.teamTolerance.HasValue)
            {
                foreach (var team in report.teamBreakdowns)
                {
                    if (!team.metrics.TryGetValue(target.metric, out var teamMetric)
                        || teamMetric == null || teamMetric.samples == 0)
                    {
                        failures.Add(new MonteCarloCalibrationFailure
                        {
                            metric = target.metric,
                            statistic = $"team {team.teamId} sin muestra",
                            observed = double.NaN,
                            minimum = target.teamMinReference.Value - target.teamTolerance.Value,
                            maximum = target.teamMaxReference.Value + target.teamTolerance.Value
                        });
                        continue;
                    }
                    AddIfOutside(failures, target.metric, $"team {team.teamId} media",
                        teamMetric.mean,
                        target.teamMinReference.Value - target.teamTolerance.Value,
                        target.teamMaxReference.Value + target.teamTolerance.Value);
                }
            }
        }
        return failures;
    }

    public static bool IsAccepted(MonteCarloReport report) => Evaluate(report).Count == 0;

    /// <summary>Hace fallar el caller con todas las bandas incumplidas.</summary>
    public static void AssertAccepted(MonteCarloReport report)
    {
        var failures = Evaluate(report);
        if (failures.Count > 0)
            throw new InvalidOperationException($"Perfil {MonteCarloCalibrationProfile.VERSION}: "
                + string.Join("; ", failures));
    }

    internal static IEnumerable<string> ToMarkdownLines(MonteCarloReport report)
    {
        var failures = Evaluate(report);
        var offenseFailures = EvaluateOffense(report);
        var byMetric = failures.GroupBy(f => f.metric).ToDictionary(g => g.Key, g => g.ToList());
        yield return "";
        yield return $"## Gate de calibración `{MonteCarloCalibrationProfile.VERSION}`";
        yield return "";
        yield return "Las tolerancias son bandas de aceptación absolutas declaradas; los datos NBA son referencias de una temporada, no un objetivo de roster sintético por franquicia.";
        yield return "";
        yield return "| Métrica | Banda media | Media sim. | P5/P50/P95 equipo-temp. · min/max equipos | Gate | Fuente |";
        yield return "|---|---:|---:|---:|---|---|";

        foreach (var target in MonteCarloCalibrationProfile.Targets)
        {
            bool hasSummary = report.metrics.TryGetValue(target.metric, out var summary) && summary?.samples > 0;
            string observed = hasSummary ? F(summary.mean) : "sin muestras";
            string band = $"{F(target.MeanMin)}–{F(target.MeanMax)}";
            string seasonSummaryText = "—";
            if (target.teamSeasonP5Reference.HasValue
                && report.teamSeasonMetrics.TryGetValue(target.metric, out var seasonSummary)
                && seasonSummary != null)
                seasonSummaryText = $"{F(seasonSummary.p5)} / {F(seasonSummary.p50)} / {F(seasonSummary.p95)}";
            if (target.teamMinReference.HasValue && report.teamBreakdowns.Count > 0)
            {
                var teamMeans = report.teamBreakdowns
                    .Where(t => t.metrics.TryGetValue(target.metric, out var s) && s != null && s.samples > 0)
                    .Select(t => t.metrics[target.metric].mean)
                    .ToList();
                if (teamMeans.Count > 0)
                    seasonSummaryText += $" · {F(teamMeans.Min())}–{F(teamMeans.Max())}";
            }
            string status = !hasSummary ? "FALLO (sin N)"
                : byMetric.TryGetValue(target.metric, out var metricFailures)
                    ? "FALLO: " + string.Join(", ", metricFailures.Select(f => f.statistic))
                    : "OK";
            yield return $"| {target.metric} | {band} | {observed} | {seasonSummaryText} | {status} | {target.source} |";
        }

        yield return "";
        yield return offenseFailures.Count == 0
            ? "**Gate ofensivo F3-04: ACEPTADO.**"
            : $"**Gate ofensivo F3-04: FUERA DE RANGO ({offenseFailures.Count} hallazgos).**";
        yield return failures.Count == 0
            ? "**Resultado del gate: ACEPTADO.**"
            : $"**Resultado del gate: FUERA DE RANGO ({failures.Count} hallazgos).** F3-04/F3-06 deben calibrar los desvíos antes de convertir el banco en un gate obligatorio de integración.";
    }

    static void AddIfOutside(List<MonteCarloCalibrationFailure> failures, MonteCarloMetric metric,
        string statistic, double observed, double minimum, double maximum)
    {
        if (double.IsNaN(observed) || observed < minimum || observed > maximum)
            failures.Add(new MonteCarloCalibrationFailure
            {
                metric = metric,
                statistic = statistic,
                observed = observed,
                minimum = minimum,
                maximum = maximum
            });
    }

    static string F(double value) => double.IsNaN(value)
        ? "N/D"
        : value.ToString("F2", CultureInfo.InvariantCulture);
}

public sealed class MonteCarloOutcomeFailure
{
    public string metric;
    public double observed;
    public double minimum;
    public double maximum;

    public override string ToString()
        => $"{metric}: observado {observed.ToString("F3", CultureInfo.InvariantCulture)}, "
            + $"aceptado [{minimum.ToString("F3", CultureInfo.InvariantCulture)}, {maximum.ToString("F3", CultureInfo.InvariantCulture)}]";
}

/// <summary>
/// F3-05: umbrales de temporada. La dispersión de victorias usa NBA 2024-25
/// (desviación poblacional de 30 récords); la tasa OT usa 60 juegos OT / 1.230 partidos.
/// Correlación y extremos entre temporadas son tolerancias de ingeniería para el banco.
/// </summary>
public static class MonteCarloOutcomeGate
{
    public const int REQUIRED_SEASONS = 100;
    public const double NBA_2024_25_WIN_STANDARD_DEVIATION = 13.130118;
    public const double NBA_2024_25_OVERTIME_RATE_PERCENT = 100.0 * 60.0 / 1230.0;

    public static List<MonteCarloOutcomeFailure> Evaluate(MonteCarloReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        var failures = new List<MonteCarloOutcomeFailure>();
        if (report.seasons < REQUIRED_SEASONS)
            failures.Add(new MonteCarloOutcomeFailure
            {
                metric = "Temporadas simuladas",
                observed = report.seasons,
                minimum = REQUIRED_SEASONS,
                maximum = double.PositiveInfinity
            });

        if (report.teamSeasonMetrics.TryGetValue(MonteCarloMetric.Wins, out var wins))
            Check(failures, "Victorias medias/equipo-temporada", wins.mean, 40.5, 41.5);
        else Check(failures, "Victorias medias/equipo-temporada", double.NaN, 40.5, 41.5);

        CheckOutcomeSummary(failures, "Desv. típica de victorias por temporada",
            report.outcomes?.winStandardDeviation, NBA_2024_25_WIN_STANDARD_DEVIATION - 4.0,
            NBA_2024_25_WIN_STANDARD_DEVIATION + 4.0);
        CheckOutcomeSummary(failures, "Mínimo de victorias medio por temporada",
            report.outcomes?.minimumWins, 8.0, 22.0);
        CheckOutcomeSummary(failures, "Máximo de victorias medio por temporada",
            report.outcomes?.maximumWins, 62.0, 75.0);
        CheckOutcomeSummary(failures, "Correlación OVR↔victorias (Pearson)",
            report.outcomes?.ovrWinCorrelation, 0.60, 0.98);
        CheckOutcomeSummary(failures, "Partidos con OT (%)",
            report.outcomes?.overtimeRatePercent,
            NBA_2024_25_OVERTIME_RATE_PERCENT - 1.5,
            NBA_2024_25_OVERTIME_RATE_PERCENT + 1.5);
        return failures;
    }

    public static void AssertAccepted(MonteCarloReport report)
    {
        var failures = Evaluate(report);
        if (failures.Count > 0)
            throw new InvalidOperationException("Gate de resultados F3-05: " + string.Join("; ", failures));
    }

    internal static IEnumerable<string> ToMarkdownLines(MonteCarloReport report)
    {
        var failures = Evaluate(report);
        var failedMetrics = failures.Select(f => f.metric).ToHashSet();
        var rows = new List<string>();
        yield return "";
        yield return "## Gate de victorias, paridad y prórrogas F3-05";
        yield return "";
        yield return "| Métrica | N temporadas/muestras | Media | Rango aceptado | Gate |";
        yield return "|---|---:|---:|---:|---|";
        AddOutcomeRow("Victorias medias/equipo-temporada", report.teamSeasonMetrics.TryGetValue(MonteCarloMetric.Wins, out var wins)
            ? wins.samples : 0, wins?.mean ?? double.NaN, 40.5, 41.5, failedMetrics);
        AddOutcomeRow("Desv. típica de victorias por temporada", report.outcomes?.winStandardDeviation.samples ?? 0,
            report.outcomes?.winStandardDeviation.mean ?? double.NaN,
            NBA_2024_25_WIN_STANDARD_DEVIATION - 4, NBA_2024_25_WIN_STANDARD_DEVIATION + 4, failedMetrics);
        AddOutcomeRow("Mínimo de victorias medio por temporada", report.outcomes?.minimumWins.samples ?? 0,
            report.outcomes?.minimumWins.mean ?? double.NaN, 8, 22, failedMetrics);
        AddOutcomeRow("Máximo de victorias medio por temporada", report.outcomes?.maximumWins.samples ?? 0,
            report.outcomes?.maximumWins.mean ?? double.NaN, 62, 75, failedMetrics);
        AddOutcomeRow("Correlación Pearson OVR↔victorias", report.outcomes?.ovrWinCorrelation.samples ?? 0,
            report.outcomes?.ovrWinCorrelation.mean ?? double.NaN, 0.60, 0.98, failedMetrics);
        AddOutcomeRow("Partidos con OT (%)", report.outcomes?.overtimeRatePercent.samples ?? 0,
            report.outcomes?.overtimeRatePercent.mean ?? double.NaN,
            NBA_2024_25_OVERTIME_RATE_PERCENT - 1.5,
            NBA_2024_25_OVERTIME_RATE_PERCENT + 1.5, failedMetrics);
        foreach (string row in rows) yield return row;
        yield return "";
        yield return failures.Count == 0
            ? "**Gate de resultados F3-05: ACEPTADO.**"
            : $"**Gate de resultados F3-05: FUERA DE RANGO ({failures.Count} hallazgos).** Requiere al menos {REQUIRED_SEASONS} temporadas para aceptar la prueba estadística.";

        void AddOutcomeRow(string name, int samples, double mean, double min, double max,
            HashSet<string> failed)
        {
            rows.Add($"| {name} | {samples} | {F(mean)} | {F(min)}–{F(max)} | "
                + $"{(failed.Contains(name) ? "FALLO" : "OK")} |");
        }
    }

    static void CheckOutcomeSummary(List<MonteCarloOutcomeFailure> failures, string name,
        MonteCarloMetricSummary summary, double minimum, double maximum)
    {
        if (summary == null || summary.samples == 0)
        {
            Check(failures, name, double.NaN, minimum, maximum);
            return;
        }
        Check(failures, name, summary.mean, minimum, maximum);
    }

    static void Check(List<MonteCarloOutcomeFailure> failures, string name,
        double observed, double minimum, double maximum)
    {
        if (double.IsNaN(observed) || observed < minimum || observed > maximum)
            failures.Add(new MonteCarloOutcomeFailure
            {
                metric = name,
                observed = observed,
                minimum = minimum,
                maximum = maximum
            });
    }

    static string F(double value) => double.IsNaN(value)
        ? "N/D"
        : value.ToString("F2", CultureInfo.InvariantCulture);
}
