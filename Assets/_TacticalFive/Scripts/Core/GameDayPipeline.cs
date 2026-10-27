using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Pasos nombrados del día de partido (F8-03). El orden es explícito y se puede
/// probar sin arrancar Unity: el Dashboard inyecta las corrutinas reales.
/// </summary>
public enum GameDayStep
{
    PreBatch,
    LineupValidation,
    PreGameGate,
    SimulationBookkeeping,
    PostDay
}

/// <summary>Subpasos síncronos que comparten la transacción de simulación/bookkeeping.</summary>
public enum GameDaySubStep
{
    Simulation,
    Bookkeeping,
    PhaseTransitions,
    Calendar
}

public static class GameDayPipeline
{
    public static readonly GameDayStep[] DefaultOrder =
    {
        GameDayStep.PreBatch,
        GameDayStep.LineupValidation,
        GameDayStep.PreGameGate,
        GameDayStep.SimulationBookkeeping,
        GameDayStep.PostDay
    };

    public static readonly GameDaySubStep[] BookkeepingOrder =
    {
        GameDaySubStep.Simulation,
        GameDaySubStep.Bookkeeping,
        GameDaySubStep.PhaseTransitions,
        GameDaySubStep.Calendar
    };

    public sealed class Step
    {
        public readonly GameDayStep Id;
        public readonly Func<IEnumerator> Run;

        public Step(GameDayStep id, Func<IEnumerator> run)
        {
            Id = id;
            Run = run;
        }
    }

    /// <summary>
    /// Ejecuta los pasos en orden. `aborted` se consulta después de cada paso para
    /// detener el día si el paso abrió un modal, navegó o revirtió una transacción.
    /// </summary>
    public static IEnumerator Run(IReadOnlyList<Step> steps, Action<GameDayStep> onStep = null,
        Func<bool> aborted = null)
    {
        if (steps == null) yield break;

        foreach (var step in steps)
        {
            if (step?.Run == null) continue;
            onStep?.Invoke(step.Id);
            yield return step.Run();
            if (aborted != null && aborted()) yield break;
        }
    }
}
