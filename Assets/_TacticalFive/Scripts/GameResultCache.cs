using System.Collections.Generic;

public static class GameResultCache
{
    public static int LastGameDay { get; set; }

    // IDs de partidos simulados en el día actual
    public static List<int> SimulatedGameIds { get; set; } = new();

    // Flag para mostrar aviso de presupuesto en rojo al volver al Dashboard
    public static bool PendingBudgetWarning { get; set; }

    public static void Clear()
    {
        SimulatedGameIds.Clear();
    }
}
