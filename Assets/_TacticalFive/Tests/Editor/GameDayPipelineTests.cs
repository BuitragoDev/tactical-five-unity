using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// F8-03: el día tiene un orden explícito y el runner se detiene al abortar un paso.
/// </summary>
public class GameDayPipelineTests
{
    [Test]
    public void DefaultOrder_IsTheDeclaredDaySequence()
    {
        Assert.That(GameDayPipeline.DefaultOrder, Is.EqualTo(new[]
        {
            GameDayStep.PreBatch,
            GameDayStep.LineupValidation,
            GameDayStep.PreGameGate,
            GameDayStep.SimulationBookkeeping,
            GameDayStep.PostDay
        }));
    }

    [Test]
    public void BookkeepingOrder_IsSimulationThenBookkeepingThenTransitionsThenCalendar()
    {
        Assert.That(GameDayPipeline.BookkeepingOrder, Is.EqualTo(new[]
        {
            GameDaySubStep.Simulation,
            GameDaySubStep.Bookkeeping,
            GameDaySubStep.PhaseTransitions,
            GameDaySubStep.Calendar
        }));
    }

    [Test]
    public void Run_ExecutesStepsInOrderAndStopsWhenAborted()
    {
        var visited = new List<string>();
        var steps = new List<GameDayPipeline.Step>
        {
            new GameDayPipeline.Step(GameDayStep.PreBatch, () => Mark(visited, "pre")),
            new GameDayPipeline.Step(GameDayStep.LineupValidation, () => Mark(visited, "lineup")),
            new GameDayPipeline.Step(GameDayStep.PreGameGate, () => Mark(visited, "pregame")),
            new GameDayPipeline.Step(GameDayStep.SimulationBookkeeping, () => Mark(visited, "sim")),
            new GameDayPipeline.Step(GameDayStep.PostDay, () => Mark(visited, "post"))
        };

        Drive(GameDayPipeline.Run(steps, null, () => visited.Count >= 2));

        Assert.That(visited, Is.EqualTo(new[] { "pre", "lineup" }),
            "El runner debe detenerse después del paso que marca el aborto");
    }

    static IEnumerator Mark(List<string> visited, string value)
    {
        visited.Add(value);
        yield break;
    }

    static void Drive(IEnumerator routine)
    {
        while (routine.MoveNext())
        {
            if (routine.Current is IEnumerator nested)
                Drive(nested);
        }
    }
}
