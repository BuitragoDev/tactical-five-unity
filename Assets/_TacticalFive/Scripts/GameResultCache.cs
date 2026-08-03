using System.Collections.Generic;

public static class GameResultCache
{
    public static int LastGameDay { get; set; }

    // IDs de partidos simulados en el día actual
    public static List<int> SimulatedGameIds { get; set; } = new();

    // Starter player IDs per game (first 5 in the active list)
    public static Dictionary<int, HashSet<int>> GameStarters { get; set; } = new();

    // Registro play-by-play por partido simulado (solo se llena si el modo está activo)
    public static Dictionary<int, List<GameSimulator.PlayByPlayEvent>> PlayByPlayLogs { get; set; } = new();

    // Flag para mostrar aviso de presupuesto en rojo al volver al Dashboard
    public static bool PendingBudgetWarning { get; set; }

    // Fecha objetivo de la simulación rápida solicitada desde el Calendario.
    // Se consume y resetea en DashboardController.OnEnable.
    public static System.DateTime? FastSimTargetDate { get; set; }

    public static void Clear()
    {
        SimulatedGameIds.Clear();
        GameStarters.Clear();
        PlayByPlayLogs.Clear();
    }
}
