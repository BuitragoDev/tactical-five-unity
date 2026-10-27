using NUnit.Framework;
using System.Linq;

public class MonteCarloRunnerTests
{
    static MonteCarloConfig SmallConfig(ulong seed = 8128) => new()
    {
        teams = 4,
        gamesPerTeam = 4,
        seasons = 2,
        seed = seed
    };

    [Test]
    public void Run_EmiteTodasLasMetricasConMuestrasYUnidadesCorrectas()
    {
        var report = MonteCarloRunner.Run(SmallConfig());

        Assert.That(report.gamesSimulated, Is.EqualTo(16)); // 4 equipos × 4 partidos ÷ 2 × 2 temporadas
        Assert.That(report.metrics[MonteCarloMetric.Pace].samples, Is.EqualTo(16));
        Assert.That(report.metrics[MonteCarloMetric.Points].samples, Is.EqualTo(32));
        Assert.That(report.metrics[MonteCarloMetric.Wins].samples, Is.EqualTo(8));
        Assert.That(report.metrics[MonteCarloMetric.PlayerDaysOut].samples, Is.EqualTo(8));
        Assert.That(report.metrics[MonteCarloMetric.PlayerGamesMissed].samples, Is.EqualTo(8));
        Assert.That(report.metrics[MonteCarloMetric.InjuryEvents].samples, Is.EqualTo(8));
        Assert.That(report.metrics[MonteCarloMetric.Players65Games].samples, Is.EqualTo(8));
        Assert.That(report.metrics[MonteCarloMetric.Rotation65Rate].samples, Is.EqualTo(8));
        Assert.That(report.metrics[MonteCarloMetric.GamesWithInjuryRate].samples, Is.EqualTo(8));
        Assert.That(report.teamSeasonMetrics[MonteCarloMetric.Points].samples, Is.EqualTo(8));
        Assert.That(report.teamSeasonMetrics[MonteCarloMetric.ThreePointPercent].samples, Is.EqualTo(8));
        Assert.That(report.metrics.ContainsKey(MonteCarloMetric.FieldGoalPercent), Is.True);
        Assert.That(report.metrics.ContainsKey(MonteCarloMetric.ThreePointPercent), Is.True);
        Assert.That(report.metrics[MonteCarloMetric.PlayerDaysOut].mean, Is.GreaterThanOrEqualTo(0));

        string markdown = report.ToMarkdown();
        Assert.That(markdown, Does.Contain("FG%"));
        Assert.That(markdown, Does.Contain("3P%"));
        Assert.That(markdown, Does.Contain("Días-jugador de baja"));
        Assert.That(markdown, Does.Contain("Desv. típica"));
        Assert.That(markdown, Does.Contain("P95"));
        Assert.That(markdown, Does.Contain($"Gate de calibración `{MonteCarloCalibrationProfile.VERSION}`"));
        Assert.That(markdown, Does.Contain("Distribución agregada por equipo-temporada"));
    }

    [Test]
    public void Run_MismaConfiguracionYSemilla_ProduceElMismoInforme()
    {
        var first = MonteCarloRunner.Run(SmallConfig(0xCA11B4A7UL));
        var second = MonteCarloRunner.Run(SmallConfig(0xCA11B4A7UL));

        Assert.That(second.ToMarkdown(), Is.EqualTo(first.ToMarkdown()));
    }

    [TestCase(1)]
    [TestCase(3)]
    public void Run_RechazaUnNumeroImparDeEquipos(int teams)
    {
        var config = SmallConfig();
        config.teams = teams;
        Assert.Throws<System.ArgumentOutOfRangeException>(() => MonteCarloRunner.Run(config));
    }

    [Test]
    public void Run_TemporadaCompletaDe30Equipos_CuadraElCalendario()
    {
        var report = MonteCarloRunner.Run(new MonteCarloConfig
        {
            teams = 30,
            gamesPerTeam = 82,
            seasons = 1,
            seed = 20261024UL
        });

        Assert.That(report.gamesSimulated, Is.EqualTo(1230));
        Assert.That(report.metrics[MonteCarloMetric.Wins].samples, Is.EqualTo(30));
        Assert.That(report.metrics[MonteCarloMetric.Wins].mean, Is.EqualTo(41).Within(0.001));
        Assert.That(report.metrics[MonteCarloMetric.PlayerDaysOut].samples, Is.EqualTo(30));
        TestContext.WriteLine(report.ToMarkdown());
        Assert.That(MonteCarloCalibrationGate.EvaluateOffense(report), Is.Empty,
            "F3-04: PTS/FG%/3P%/FTA/TO/AST deben pasar por promedio y colas equipo-temporada");
    }

    // 100 temporadas ≈ 200 s de ejecución: el timeout por defecto (180 s) no basta.
    [Test]
    [Timeout(600000)]
    public void Run_CienTemporadas_CalibraVictoriasParidadOVRYProrrogas()
    {
        var report = MonteCarloRunner.Run(new MonteCarloConfig
        {
            teams = 30,
            gamesPerTeam = 82,
            seasons = 100,
            seed = 20261024UL
        });

        Assert.That(report.gamesSimulated, Is.EqualTo(123000));
        Assert.That(report.outcomes.overtimeRatePercent.samples, Is.EqualTo(100));
        Assert.That(report.outcomes.ovrWinCorrelation.samples, Is.EqualTo(100));
        TestContext.WriteLine($"100 temporadas: victorias media={report.teamSeasonMetrics[MonteCarloMetric.Wins].mean:F2}, "
            + $"desv={report.outcomes.winStandardDeviation.mean:F2}, "
            + $"min={report.outcomes.minimumWins.mean:F2}, max={report.outcomes.maximumWins.mean:F2}, "
            + $"r(OVR,W)={report.outcomes.ovrWinCorrelation.mean:F3}, "
            + $"OT={report.outcomes.overtimeRatePercent.mean:F2}% "
            + $"({report.gamesWithOvertime}/{report.gamesSimulated})");
        var gamesMissed = report.teamSeasonMetrics[MonteCarloMetric.PlayerGamesMissed];
        TestContext.WriteLine($"Bajas por equipo-temporada: media={gamesMissed.mean:F2}, "
            + $"P5={gamesMissed.p5:F2}, P50={gamesMissed.p50:F2}, P95={gamesMissed.p95:F2}; "
            + $"mínimo roster sano={report.minimumHealthyPlayers}");
        var injuryEvents = report.teamSeasonMetrics[MonteCarloMetric.InjuryEvents];
        var players65 = report.teamSeasonMetrics[MonteCarloMetric.Players65Games];
        var rotation65 = report.teamSeasonMetrics[MonteCarloMetric.Rotation65Rate];
        var injuryGameRate = report.teamSeasonMetrics[MonteCarloMetric.GamesWithInjuryRate];
        TestContext.WriteLine($"Lesiones: {injuryEvents.mean:F2}/equipo-temporada; "
            + $"jugadores con ≥65 partidos={players65.mean:F2}/15; "
            + $"rotación con ≥65={rotation65.mean:F2}%; "
            + $"partidos con lesión nueva={injuryGameRate.mean:F2}%");
        TestContext.WriteLine($"Mínimo roster sano={report.minimumHealthyPlayers}; "
            + string.Join(" · ", report.injuryDurations.OrderBy(kv => kv.Key)
                .Select(kv => $"{InjuryCatalog.SeverityLabel(kv.Key)} {kv.Value.events} episodios, "
                    + $"media {kv.Value.meanDays:F2}d (catálogo {kv.Value.catalogMeanDays:F2}d)")));

        var failures = MonteCarloOutcomeGate.Evaluate(report);
        Assert.That(failures, Is.Empty, string.Join("; ", failures));

        Assert.That(report.minimumHealthyPlayers, Is.GreaterThanOrEqualTo(InjuryHelper.MIN_HEALTHY_PLAYERS),
            "El laboratorio nunca debe dejar a una franquicia sin cinco jugadores sanos");
        Assert.That(report.teamBreakdowns.All(t => t.minimumHealthyPlayers >= InjuryHelper.MIN_HEALTHY_PLAYERS),
            Is.True, "Cada equipo debe conservar al menos cinco jugadores sanos durante las temporadas");

        foreach (var severity in System.Enum.GetValues(typeof(InjurySeverity)).Cast<InjurySeverity>())
        {
            Assert.That(report.injuryDurations.TryGetValue(severity, out var duration), Is.True,
                $"Sin muestra para gravedad {severity}");
            var catalog = InjuryCatalog.All.Where(d => d.severity == severity).ToArray();
            double tolerance = severity switch
            {
                InjurySeverity.Leve => 1.0,
                InjurySeverity.Moderada => 2.0,
                InjurySeverity.Grave => 5.0,
                _ => 20.0
            };
            Assert.That(duration.events, Is.GreaterThan(100), $"Muestra insuficiente para {severity}");
            Assert.That(duration.meanDays, Is.EqualTo(duration.catalogMeanDays).Within(tolerance),
                $"Duración media de {severity} alejada de la mezcla ponderada del catálogo");
            Assert.That(duration.observedMinDays, Is.GreaterThanOrEqualTo(catalog.Min(d => d.minDays)));
            Assert.That(duration.observedMaxDays, Is.LessThanOrEqualTo(catalog.Max(d => d.maxDays)));
        }

        var calibrationFailures = MonteCarloCalibrationGate.Evaluate(report);
        Assert.That(calibrationFailures, Is.Empty,
            "F3-04/B47: calibración ofensiva y disponibilidad por lesiones deben quedar dentro del perfil. "
            + string.Join("; ", calibrationFailures));
    }
}
