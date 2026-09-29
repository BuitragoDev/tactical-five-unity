using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Reparto automático de los 240 minutos (botón AUTO y vista del estado "todo automático").
/// </summary>
public class RotationPlannerTests
{
    static readonly int[] Realistic = { 39, 39, 31, 31, 31, 21, 21, 15, 15, 15, 8, 8 };

    static int Sum(int[] v) { int s = 0; foreach (var x in v) s += x; return s; }

    [Test]
    public void AutoAssign_SumsExactlyTeamMinutes()
    {
        var mins = RotationPlanner.AutoAssign(Realistic);

        Assert.That(mins.Length, Is.EqualTo(Realistic.Length));
        Assert.That(Sum(mins), Is.EqualTo(RotationPlanner.TEAM_MINUTES), "el reparto debe cuadrar 240");
    }

    [Test]
    public void AutoAssign_RespectsFloorAndCap()
    {
        var mins = RotationPlanner.AutoAssign(Realistic);
        foreach (var m in mins)
        {
            Assert.That(m, Is.GreaterThanOrEqualTo(RotationPlanner.MIN_PER_PLAYER), "suelo de 8 min (igual que el simulador)");
            Assert.That(m, Is.LessThanOrEqualTo(RotationPlanner.MAX_PER_PLAYER));
        }

        // Pocos jugadores: se reparte hasta el tope de 48 por cabeza.
        var five = RotationPlanner.AutoAssign(new List<int> { 40, 35, 30, 25, 20 });
        Assert.That(Sum(five), Is.EqualTo(RotationPlanner.TEAM_MINUTES));
        foreach (var m in five) Assert.That(m, Is.EqualTo(RotationPlanner.MAX_PER_PLAYER));
    }

    [Test]
    public void AutoAssign_GivesMoreMinutesToHigherSuggestions()
    {
        var mins = RotationPlanner.AutoAssign(Realistic);

        Assert.That(mins[0], Is.GreaterThan(mins[11]), "el titular de 39 sugeridos juega más que el de 8");
        Assert.That(mins[0], Is.GreaterThanOrEqualTo(mins[5]));
    }

    [Test]
    public void AutoAssign_HandlesZeroAndEmptySuggestions()
    {
        var zeros = RotationPlanner.AutoAssign(new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        Assert.That(Sum(zeros), Is.EqualTo(RotationPlanner.TEAM_MINUTES), "sin sugerencias se reparte por igual");

        Assert.That(RotationPlanner.AutoAssign(new List<int>()), Is.Empty);
        Assert.That(RotationPlanner.AutoAssign(null), Is.Empty);
    }

    [Test]
    public void AutoAssign_DropsFloorWhenThereAreTooManyPlayers()
    {
        var many = new List<int>();
        for (int i = 0; i < 40; i++) many.Add(10);

        var mins = RotationPlanner.AutoAssign(many);

        Assert.That(mins.Length, Is.EqualTo(40));
        Assert.That(Sum(mins), Is.EqualTo(RotationPlanner.TEAM_MINUTES));
    }
}
