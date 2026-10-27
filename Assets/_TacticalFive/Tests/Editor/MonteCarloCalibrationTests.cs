using System.Linq;
using NUnit.Framework;

public class MonteCarloCalibrationTests
{
    static MonteCarloReport ReportAtReferenceValues()
    {
        var report = new MonteCarloReport();
        var team = new MonteCarloTeamSummary { teamId = 1, seasons = 1, averageOverall = 77 };
        foreach (var target in MonteCarloCalibrationProfile.Targets)
        {
            report.metrics[target.metric] = new MonteCarloMetricSummary
            {
                samples = 30,
                mean = target.referenceMean,
                p5 = target.referenceMean - 2,
                p50 = target.referenceMean,
                p95 = target.referenceMean + 2
            };
            report.teamSeasonMetrics[target.metric] = new MonteCarloMetricSummary
            {
                samples = 30,
                mean = target.referenceMean,
                p5 = target.teamSeasonP5Reference ?? target.referenceMean - 2,
                p50 = target.teamSeasonP50Reference ?? target.referenceMean,
                p95 = target.teamSeasonP95Reference ?? target.referenceMean + 2
            };
            team.metrics[target.metric] = new MonteCarloMetricSummary
            {
                samples = 1,
                mean = target.referenceMean
            };
        }
        report.teamBreakdowns.Add(team);
        return report;
    }

    [Test]
    public void Perfil2024_25_CubreLasMetricasConUnidadComparableYFuente()
    {
        Assert.That(MonteCarloCalibrationProfile.VERSION, Is.EqualTo("NBA-2024-25-v3"));
        Assert.That(MonteCarloCalibrationProfile.Targets.Count, Is.EqualTo(15));
        Assert.That(MonteCarloCalibrationProfile.Targets.All(t => !string.IsNullOrWhiteSpace(t.source)), Is.True);
        Assert.That(MonteCarloCalibrationProfile.Targets.All(t => t.meanTolerance > 0), Is.True);
        Assert.That(MonteCarloCalibrationProfile.Targets.Any(t => t.metric == MonteCarloMetric.PlayerGamesMissed),
            Is.True, "la disponibilidad se mide en partidos-jugador perdidos");
        Assert.That(MonteCarloCalibrationProfile.Targets.Any(t => t.metric == MonteCarloMetric.InjuryEvents),
            Is.True, "la frecuencia de lesiones es una puerta propia");
        Assert.That(MonteCarloCalibrationProfile.Targets.Any(t => t.metric == MonteCarloMetric.Players65Games),
            Is.True, "la disponibilidad para premios es una puerta propia");
    }

    [Test]
    public void Gate_AceptaValoresEnLosLimitesIncluidos()
    {
        var report = ReportAtReferenceValues();
        foreach (var target in MonteCarloCalibrationProfile.Targets)
        {
            report.metrics[target.metric].mean = target.MeanMin;
            if (target.teamSeasonP5Reference.HasValue)
            {
                report.teamSeasonMetrics[target.metric].p5 = target.teamSeasonP5Reference.Value
                    - target.teamSeasonP5Tolerance.Value;
                report.teamSeasonMetrics[target.metric].p50 = target.teamSeasonP50Reference.Value
                    - target.teamSeasonP50Tolerance.Value;
                report.teamSeasonMetrics[target.metric].p95 = target.teamSeasonP95Reference.Value
                    - target.teamSeasonP95Tolerance.Value;
            }
        }

        Assert.That(MonteCarloCalibrationGate.Evaluate(report), Is.Empty);
        Assert.That(MonteCarloCalibrationGate.IsAccepted(report), Is.True);
    }

    [Test]
    public void Gate_RechazaUnaMediaFueraDeRangoYExplicaLaBanda()
    {
        var report = ReportAtReferenceValues();
        var points = MonteCarloCalibrationProfile.Targets.Single(t => t.metric == MonteCarloMetric.Points);
        report.metrics[MonteCarloMetric.Points].mean = points.MeanMax + 0.01;

        var failures = MonteCarloCalibrationGate.Evaluate(report);
        Assert.That(failures.Count, Is.EqualTo(1));
        Assert.That(failures[0].metric, Is.EqualTo(MonteCarloMetric.Points));
        Assert.That(failures[0].statistic, Is.EqualTo("mean"));
        Assert.That(failures[0].ToString(), Does.Contain("aceptado"));
        Assert.Throws<System.InvalidOperationException>(() => MonteCarloCalibrationGate.AssertAccepted(report));
    }

    [Test]
    public void Gate_RechazaColasDeVictoriasFueraDeRangoAunqueLaMediaSeaCorrecta()
    {
        var report = ReportAtReferenceValues();
        report.teamSeasonMetrics[MonteCarloMetric.Wins].p5 = 0;

        var failure = MonteCarloCalibrationGate.Evaluate(report)
            .Single(f => f.metric == MonteCarloMetric.Wins && f.statistic == "team-season P5");
        Assert.That(failure.statistic, Is.EqualTo("team-season P5"));
    }

    [Test]
    public void Gate_DetectaUnEquipoSistematicamenteFueraDeLaDistribucionNBA()
    {
        var report = ReportAtReferenceValues();
        var points = MonteCarloCalibrationProfile.Targets.Single(t => t.metric == MonteCarloMetric.Points);
        report.teamBreakdowns[0].metrics[MonteCarloMetric.Points].mean =
            points.teamMaxReference.Value + points.teamTolerance.Value + 0.01;

        var failure = MonteCarloCalibrationGate.EvaluateOffense(report)
            .Single(f => f.metric == MonteCarloMetric.Points && f.statistic == "team 1 media");
        Assert.That(failure.observed, Is.GreaterThan(failure.maximum));
    }

}
