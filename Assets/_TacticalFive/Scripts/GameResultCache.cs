using System.Collections.Generic;

public static class GameResultCache
{
    public static int LastGameDay { get; set; }

    // IDs de partidos simulados en el día actual
    public static List<int> SimulatedGameIds { get; set; } = new();

    public static void Clear()
    {
        SimulatedGameIds.Clear();
    }
}
