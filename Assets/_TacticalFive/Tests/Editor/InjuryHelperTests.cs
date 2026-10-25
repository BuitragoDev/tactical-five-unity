using NUnit.Framework;

/// <summary>
/// Reglas de la parte médica (E2): evolución diaria, alta forzada, recaída y progreso.
/// </summary>
public class InjuryHelperTests
{
    // ── EVOLUCIÓN DIARIA ────────────────────────────────

    [Test]
    public void EvolveDays_LastDayRecovers()
    {
        Assert.That(InjuryHelper.EvolveDays(1, 5, InjurySeverity.Leve, 0, 90, 0.5), Is.EqualTo(0));
    }

    [Test]
    public void EvolveDays_BaseOneDayPerDay()
    {
        // Sin médico y con un roll que no activa ni avance ni retroceso.
        Assert.That(InjuryHelper.EvolveDays(10, 20, InjurySeverity.Moderada, 0, 80, 0.5), Is.EqualTo(9));
    }

    [Test]
    public void EvolveDays_GoodMedicoSpeedsUpMinorInjuries()
    {
        int normal = InjuryHelper.EvolveDays(10, 20, InjurySeverity.Moderada, 0, 80, 0.9);
        int fast = InjuryHelper.EvolveDays(10, 20, InjurySeverity.Moderada, 5, 80, 0.9);
        Assert.That(fast, Is.LessThan(normal), "el médico de élite avanza más rápido");
    }

    [Test]
    public void EvolveDays_SetbackInSeriousInjuriesWithLowFisico()
    {
        // Lesión grave, físico bajo y mala suerte: la evolución retrocede un día (sin pasarse del pronóstico).
        int next = InjuryHelper.EvolveDays(10, 30, InjurySeverity.Grave, 0, 20, 0.01);
        Assert.That(next, Is.EqualTo(11));
    }

    [Test]
    public void EvolveDays_SetbackNeverExceedsTotal()
    {
        int next = InjuryHelper.EvolveDays(30, 30, InjurySeverity.Critica, 0, 10, 0.01);
        Assert.That(next, Is.LessThanOrEqualTo(30), "no puede superar el pronóstico original");
    }

    [Test]
    public void EvolveDays_SoftInjuriesNeverRegress()
    {
        int next = InjuryHelper.EvolveDays(5, 10, InjurySeverity.Leve, 0, 10, 0.01);
        Assert.That(next, Is.EqualTo(4), "las leves no retroceden");
    }

    [Test]
    public void EvolveDays_RecoveredStaysRecovered()
    {
        Assert.That(InjuryHelper.EvolveDays(0, 0, InjurySeverity.Leve, 5, 90, 0.9), Is.EqualTo(0));
    }

    // ── ALTA FORZADA Y RECAÍDA ──────────────────────────

    [Test]
    public void ForceReturnRisk_GrowsWithRemainingDaysAndSeverity()
    {
        int leve = InjuryHelper.ForceReturnRisk(2, InjurySeverity.Leve);
        int grave = InjuryHelper.ForceReturnRisk(2, InjurySeverity.Grave);
        int largo = InjuryHelper.ForceReturnRisk(30, InjurySeverity.Grave);

        Assert.That(grave, Is.GreaterThan(leve));
        Assert.That(largo, Is.GreaterThan(grave));
        Assert.That(largo, Is.LessThanOrEqualTo(90));
        Assert.That(InjuryHelper.ForceReturnRisk(0, InjurySeverity.Grave), Is.EqualTo(0));
    }

    [Test]
    public void RelapseChancePerGame_ZeroWithoutRisk()
    {
        Assert.That(InjuryHelper.RelapseChancePerGame(0, 90), Is.EqualTo(0f));
    }

    [Test]
    public void RelapseChancePerGame_HighRiskIsNotablyHigh()
    {
        // B2: un 90% nominal debe dar una probabilidad por partido alta (~0.54), no ~0.31.
        float chance = InjuryHelper.RelapseChancePerGame(90, 90);
        Assert.That(chance, Is.GreaterThanOrEqualTo(0.5f));
        Assert.That(chance, Is.LessThanOrEqualTo(0.75f));
    }

    [Test]
    public void RelapseChancePercent_RoundsCorrectly()
    {
        Assert.That(InjuryHelper.RelapseChancePercent(0, 90), Is.EqualTo(0));
        Assert.That(InjuryHelper.RelapseChancePercent(90, 90), Is.InRange(50, 75));
    }

    [Test]
    public void CanForceReturn_OnlyBetweenTwoAndSevenDays()
    {
        Assert.That(InjuryHelper.CanForceReturn(0), Is.False);
        Assert.That(InjuryHelper.CanForceReturn(1), Is.False, "con 1 día no compensa");
        Assert.That(InjuryHelper.CanForceReturn(2), Is.True);
        Assert.That(InjuryHelper.CanForceReturn(7), Is.True);
        Assert.That(InjuryHelper.CanForceReturn(8), Is.False, "más de 7 días = demasiado grave");
        Assert.That(InjuryHelper.CanForceReturn(90), Is.False);
    }

    [Test]
    public void RelapseChancePerGame_HigherWhenTired()
    {
        float fresh = InjuryHelper.RelapseChancePerGame(50, 90);
        float tired = InjuryHelper.RelapseChancePerGame(50, 30);
        Assert.That(fresh, Is.GreaterThan(0f));
        Assert.That(tired, Is.GreaterThan(fresh));
    }

    [Test]
    public void RiskDecay_RestingHealsFaster()
    {
        Assert.That(InjuryHelper.RiskDecayForDay(false), Is.GreaterThan(InjuryHelper.RiskDecayForDay(true)));
    }

    [Test]
    public void Relapse_PicksAWorseInjuryOfTheSameFamily()
    {
        var original = InjuryCatalog.ByName("Sobrecarga muscular");   // muscular, Leve
        var relapse = InjuryHelper.Relapse(original, 0.5);

        Assert.That(relapse, Is.Not.Null);
        Assert.That(relapse.severity, Is.GreaterThan(original.severity));
        Assert.That(relapse.family, Is.EqualTo("muscular"));
    }

    [Test]
    public void Relapse_BumpsTheInjuryWhenNothingWorseExists()
    {
        var original = InjuryCatalog.ByName("Rotura del ligamento cruzado anterior");
        var relapse = InjuryHelper.Relapse(original, 0.5);

        Assert.That(relapse.name, Is.EqualTo(original.name));
        Assert.That(relapse.maxDays, Is.GreaterThan(original.maxDays));
        Assert.That(relapse.severity, Is.EqualTo(InjurySeverity.Critica), "clampado en el máximo");
    }

    [Test]
    public void Relapse_HandlesNullOriginal()
    {
        Assert.That(InjuryHelper.Relapse(null, 0.3), Is.Not.Null);
    }

    // ── PROGRESO Y SORTEO DE DÍAS ───────────────────────

    [Test]
    public void RecoveryProgress_TracksTheEpisode()
    {
        Assert.That(InjuryHelper.RecoveryProgress(10, 10), Is.EqualTo(0));
        Assert.That(InjuryHelper.RecoveryProgress(5, 10), Is.EqualTo(50));
        Assert.That(InjuryHelper.RecoveryProgress(0, 10), Is.EqualTo(100));
        Assert.That(InjuryHelper.RecoveryProgress(0, 0), Is.EqualTo(100));
    }

    [Test]
    public void RollDays_StaysWithinTheRange()
    {
        var d = new InjuryDef { name = "X", family = "f", minDays = 4, maxDays = 8, weight = 1 };

        Assert.That(InjuryHelper.RollDays(d, 0.0), Is.EqualTo(4));
        Assert.That(InjuryHelper.RollDays(d, 0.999), Is.EqualTo(8));
        for (int i = 0; i <= 20; i++)
            Assert.That(InjuryHelper.RollDays(d, i / 20.0), Is.InRange(4, 8));
    }

    // ── RIESGO DE LESIÓN POR PARTIDO (F2-07) ────────────

    [Test]
    public void MatchRiskPerGame_NoMinutes_NoRisk()
    {
        // Quien no juega no se lesiona, por muy bajo que tenga el físico o el riesgo.
        Assert.That(InjuryHelper.MatchRiskPerGame(0, 99, 0), Is.EqualTo(0f));
        Assert.That(InjuryHelper.MatchRiskPerGame(0, 5, 90), Is.EqualTo(0f));
        Assert.That(InjuryHelper.MatchRiskPerGame(-3, 50, 50), Is.EqualTo(0f));
    }

    [Test]
    public void MatchRiskPerGame_IsProportionalToExposure()
    {
        // F3-06: 36 minutos = probabilidad base calibrada (20 %).
        Assert.That(InjuryHelper.MatchRiskPerGame(36, 90, 0),
            Is.EqualTo(InjuryHelper.INJURY_BASE_PER_GAME).Within(1e-6f));
        Assert.That(InjuryHelper.MatchRiskPerGame(18, 90, 0),
            Is.EqualTo(InjuryHelper.INJURY_BASE_PER_GAME / 2).Within(1e-6f));

        float r5 = InjuryHelper.MatchRiskPerGame(5, 90, 0);
        float r20 = InjuryHelper.MatchRiskPerGame(20, 90, 0);
        float r36 = InjuryHelper.MatchRiskPerGame(36, 90, 0);
        float r44 = InjuryHelper.MatchRiskPerGame(44, 90, 0);
        TestContext.WriteLine($"Riesgo por partido: 5 min {r5:P3} · 20 min {r20:P3} · 36 min {r36:P3} · 44 min {r44:P3}");

        Assert.That(r5, Is.LessThan(r20));
        Assert.That(r20, Is.LessThan(r36));
        Assert.That(r36, Is.LessThan(r44));
        Assert.That(r44, Is.GreaterThan(r5 * 8f), "40 minutos arriesgan mucho más que 5");
    }

    [Test]
    public void MatchRiskPerGame_LowFisico_IncreasesRisk()
    {
        float rested = InjuryHelper.MatchRiskPerGame(36, 90, 0);
        float tired = InjuryHelper.MatchRiskPerGame(36, 20, 0);
        Assert.That(tired, Is.GreaterThan(rested));
        // (30 - 20) * 0.15 = 1.5 → x2.5 sobre la base.
        Assert.That(tired, Is.EqualTo(rested * 2.5f).Within(1e-5f));
    }

    [Test]
    public void MatchRiskPerGame_RelapseRiskAddsToTheBase()
    {
        float healthy = InjuryHelper.MatchRiskPerGame(30, 80, 0);
        float risky = InjuryHelper.MatchRiskPerGame(30, 80, 60);
        Assert.That(risky, Is.GreaterThan(healthy));
        Assert.That(risky, Is.EqualTo(healthy + InjuryHelper.RelapseChancePerGame(60, 80)).Within(1e-6f));
    }

    [Test]
    public void MatchRiskPerGame_CanNeverExceedCertainty()
    {
        Assert.That(InjuryHelper.MatchRiskPerGame(48, 1, 100), Is.LessThanOrEqualTo(1f));
    }

    [Test]
    public void CanSustainMatchInjury_PreservesFiveHealthyPlayers()
    {
        Assert.That(InjuryHelper.CanSustainMatchInjury(4), Is.False);
        Assert.That(InjuryHelper.CanSustainMatchInjury(5), Is.False);
        Assert.That(InjuryHelper.CanSustainMatchInjury(6), Is.True);
    }
}
