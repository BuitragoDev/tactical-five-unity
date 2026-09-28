/// <summary>
/// Plan de partido: táctica con la que un equipo afronta un partido.
/// - `pace`: -1 lento, 0 normal, +1 rápido.
/// - `offense`: 0 equilibrado, 1 interior, 2 exterior.
/// - `defense`: 0 conservadora, 1 equilibrada, 2 agresiva.
/// Lógica pura: la usan el simulador y la IA (derivada de su estrategia).
/// </summary>
public struct GamePlan
{
    public int pace;
    public int offense;
    public int defense;

    public static GamePlan Neutral => new GamePlan { pace = 0, offense = 0, defense = 1 };

    /// <summary>Plan por defecto de un equipo IA según su estrategia de GM.</summary>
    public static GamePlan FromStrategy(TeamStrategy strategy)
    {
        switch (strategy)
        {
            case TeamStrategy.Contend:
                // Los aspirantes van rápido, tiran de tres y defienden agresivo.
                return new GamePlan { pace = 1, offense = 2, defense = 2 };
            case TeamStrategy.Rebuild:
                // Los que reconstruyen juegan lento y cargan el juego interior.
                return new GamePlan { pace = -1, offense = 1, defense = 1 };
            default:
                return Neutral;
        }
    }

    /// <summary>Sesgo de probabilidad de tiro de tres según el enfoque ofensivo.</summary>
    public float ThreePointBias()
    {
        if (offense >= 2) return 0.10f;
        if (offense <= 0) return 0f;
        return offense == 1 ? -0.10f : 0f;
    }

    /// <summary>Delta de pérdidas que provoca esta defensa (positivo = fuerza más).</summary>
    public float ForcedTurnoverDelta()
    {
        if (defense >= 2) return 0.020f;   // agresiva
        if (defense <= 0) return -0.015f;  // conservadora
        return 0f;
    }

    /// <summary>Delta de faltas que provoca esta defensa (positivo = más faltas).</summary>
    public float FoulDelta()
    {
        if (defense >= 2) return 0.03f;
        if (defense <= 0) return -0.02f;
        return 0f;
    }

    /// <summary>Ajuste de posesiones por partido según el ritmo (se promedia con el rival).</summary>
    public static float PaceAdjustment(GamePlan home, GamePlan away) => (home.pace + away.pace) * 2f;
}
