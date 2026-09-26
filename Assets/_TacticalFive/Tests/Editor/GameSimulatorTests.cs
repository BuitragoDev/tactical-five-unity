using NUnit.Framework;
using System.Collections.Generic;

public class GameSimulatorTests
{
    [Test]
    public void Rebounds_PrioritizeInteriorPositions()
    {
        Assert.That(
            GameSimulator.GetReboundPositionMultiplier("C", false),
            Is.GreaterThan(GameSimulator.GetReboundPositionMultiplier("PG", false)));
        Assert.That(
            GameSimulator.GetReboundPositionMultiplier("PF", true),
            Is.GreaterThan(GameSimulator.GetReboundPositionMultiplier("SG", true)));
    }

    [Test]
    public void Blocks_PrioritizeCentersAndPowerForwards()
    {
        Assert.That(
            GameSimulator.GetBlockPositionMultiplier("C"),
            Is.GreaterThan(GameSimulator.GetBlockPositionMultiplier("PG")));
        Assert.That(
            GameSimulator.GetBlockPositionMultiplier("PF"),
            Is.GreaterThan(GameSimulator.GetBlockPositionMultiplier("SG")));
    }

    [Test]
    public void UnknownPosition_UsesNeutralMultiplier()
    {
        Assert.That(GameSimulator.GetReboundPositionMultiplier("", false), Is.EqualTo(1f));
        Assert.That(GameSimulator.GetBlockPositionMultiplier(""), Is.EqualTo(1f));
    }

    [Test]
    public void StartersAndStars_ReceiveHigherMinuteTargets()
    {
        var star = new GameSimulator.PlayerStatSnapshot
        {
            role = PlayerRole.Estrella,
            overall = 90,
            starter = true
        };
        var bench = new GameSimulator.PlayerStatSnapshot
        {
            role = PlayerRole.Banquillo,
            overall = 72,
            starter = false
        };

        Assert.That(GameSimulator.GetTargetMinutes(star), Is.EqualTo(39));
        Assert.That(GameSimulator.GetTargetMinutes(bench), Is.EqualTo(15));
        Assert.That(GameSimulator.GetTargetMinutes(star), Is.GreaterThan(GameSimulator.GetTargetMinutes(bench)));
    }

    // ── Rotación vinculante (pantalla ROTACIÓN) ─────────────────

    static GameSimulator.PlayerStatSnapshot Plan(string pos, string sec, PlayerRole role, int ovr, int plan, bool starter)
    {
        return new GameSimulator.PlayerStatSnapshot
        {
            position = pos,
            secondary_position = sec,
            role = role,
            overall = ovr,
            target_minutes = plan,
            fixed_minutes = plan >= 0,
            starter = starter
        };
    }

    // 12 convocados: planes 8/23/29/29/23 titulares y 16/16/15/15/39/15/15 banquillo.
    static List<GameSimulator.PlayerStatSnapshot> ConfiguredRoster()
    {
        return new List<GameSimulator.PlayerStatSnapshot>
        {
            Plan("PG", "SG", PlayerRole.Titular,   88,  8, true),
            Plan("PG", "SG", PlayerRole.Banquillo, 82, 23, true),
            Plan("SF", "PF", PlayerRole.Titular,   85, 29, true),
            Plan("PF", "C",  PlayerRole.Estrella,  93, 29, true),
            Plan("C",  "PF", PlayerRole.Titular,   83, 23, true),
            Plan("C",  "PF", PlayerRole.Banquillo, 80, 16, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 78, 16, false),
            Plan("PG", "SG", PlayerRole.Banquillo, 78, 15, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 77, 15, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 77, 39, false),
            Plan("SG", "SF", PlayerRole.Banquillo, 75, 15, false),
            Plan("C",  "PF", PlayerRole.Banquillo, 75, 15, false)
        };
    }

    [Test]
    public void StrictRotation_TakesOutPlayerOverPlan_AndBringsInPendingMinutes()
    {
        var ps = ConfiguredRoster();
        var on = new HashSet<int> { 0, 1, 2, 3, 4 };
        var quarterMinutes = new float[12];
        quarterMinutes[0] = 20f; // White: plan 8 -> se pasó 12

        var pick = GameSimulator.PickSubstitution(on, ps, quarterMinutes, strict: true);

        Assert.That(pick.outgoing, Is.EqualTo(0), "Debe salir el que más se pasa de su plan");
        Assert.That(pick.incoming, Is.EqualTo(9), "Debe entrar el de más minutos pendientes (Hugo, plan 39)");
    }

    [Test]
    public void StrictRotation_DoesNotReenterPlayerWhoExhaustedPlan()
    {
        var ps = ConfiguredRoster();
        // Todos los de banquillo ya jugaron su plan salvo el 9.
        foreach (int i in new[] { 5, 6, 7, 8, 10, 11 })
            ps[i].minutes = ps[i].target_minutes;
        ps[9].minutes = 0;

        var on = new HashSet<int> { 0, 1, 2, 3, 4 };
        var quarterMinutes = new float[12];
        quarterMinutes[0] = 20f;

        var pick = GameSimulator.PickSubstitution(on, ps, quarterMinutes, strict: true);

        Assert.That(pick.outgoing, Is.EqualTo(0));
        Assert.That(pick.incoming, Is.EqualTo(9), "No debe volver a entrar quien agotó su plan");
    }

    [Test]
    public void StrictRotation_NoChange_WhenNobodyHasPendingMinutes()
    {
        var ps = ConfiguredRoster();
        foreach (int i in new[] { 5, 6, 7, 8, 9, 10, 11 })
            ps[i].minutes = ps[i].target_minutes;

        var on = new HashSet<int> { 0, 1, 2, 3, 4 };
        var quarterMinutes = new float[12];
        quarterMinutes[0] = 20f;

        var pick = GameSimulator.PickSubstitution(on, ps, quarterMinutes, strict: true);

        Assert.That(pick.outgoing, Is.EqualTo(-1));
        Assert.That(pick.incoming, Is.EqualTo(-1));
    }

    [Test]
    public void NonStrictRotation_KeepsAiStarRule_WithGlobalMinutes()
    {
        var ps = new List<GameSimulator.PlayerStatSnapshot>
        {
            Plan("PF", "C",  PlayerRole.Estrella,  93, 39, true),  // estrella en pista
            Plan("PG", "SG", PlayerRole.Titular,   80, 23, true),
            Plan("SF", "PF", PlayerRole.Titular,   80, 29, true),
            Plan("C",  "PF", PlayerRole.Titular,   80, 23, true),
            Plan("SG", "SF", PlayerRole.Banquillo, 75, 15, true),
            Plan("C",  "PF", PlayerRole.Banquillo, 75, 16, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 75, 16, false),
            Plan("PG", "SG", PlayerRole.Banquillo, 75, 15, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 75, 15, false),
            Plan("SF", "PF", PlayerRole.Banquillo, 75, 39, false),
            Plan("SG", "SF", PlayerRole.Banquillo, 75, 15, false),
            Plan("C",  "PF", PlayerRole.Banquillo, 75, 15, false)
        };
        var on = new HashSet<int> { 0, 1, 2, 3, 4 };
        var quarterMinutes = new float[12];
        quarterMinutes[0] = 25f; // lleva 25' del cuarto, pero la IA no lo cuenta

        var ai = GameSimulator.PickSubstitution(on, ps, quarterMinutes, strict: false);
        Assert.That(ai.outgoing, Is.Not.EqualTo(0), "La IA mantiene la regla antigua (minutos globales)");

        var mine = GameSimulator.PickSubstitution(on, ps, quarterMinutes, strict: true);
        Assert.That(mine.outgoing, Is.EqualTo(0), "Con rotación vinculante sale el que se pasó del plan");
    }

    [Test]
    public void FixedPlans_GetMinimumFloor_AndIgnoreFatigue()
    {
        var players = new List<GameSimulator.PlayerStatSnapshot>
        {
            Plan("PG", "SG", PlayerRole.Titular,   88,  5, true),   // 0: 5 -> suelo 8
            Plan("SF", "PF", PlayerRole.Banquillo, 77, 39, false),  // 1: fijo, no lo toca la fatiga
            Plan("C",  "PF", PlayerRole.Banquillo, 75,  0, false),  // 2: 0 se respeta
            Plan("AP", "P",  PlayerRole.Banquillo, 74, 20, false),  // 3: fijo, sin cambios
            Plan("B",  "E",  PlayerRole.Banquillo, 73, 20, false),  // 4: fijo, sin cambios
            // 5+: banquillo. PrepareRotationMetadata fija starter = i < 5, así que el
            // automático tiene que estar en índice ≥ 5 para usar la heurística de suplente.
            Plan("SG", "SF", PlayerRole.Banquillo, 72, -1, false)   // 5: automático -> heurística
        };
        players[1].fisico = 10;
        players[5].fisico = 50;

        GameSimulator.PrepareRotationMetadata(players);

        Assert.That(players[0].target_minutes, Is.EqualTo(GameSimulator.MIN_FIXED_MINUTES));
        Assert.That(players[1].target_minutes, Is.EqualTo(39), "La fatiga no baja un plan configurado");
        Assert.That(players[2].target_minutes, Is.EqualTo(0));
        Assert.That(players[5].target_minutes, Is.EqualTo(15), "Sin configurar usa la heurística de suplente");
        Assert.That(players[5].fixed_minutes, Is.False);
        Assert.That(players[5].starter, Is.False);
        Assert.That(players[0].starter, Is.True);
    }
}
