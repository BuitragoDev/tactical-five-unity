using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Scouting de draft: lo que se ve de un prospecto es una ESTIMACIÓN. Cuanto peores
/// son los ojeadores, mayor el margen de error, así que un prospecto puede salir
/// mejor (boom) o peor (bust) de lo que parecía. Lógica pura y testeable.
/// </summary>
public static class ScoutingHelper
{
    /// <summary>Posición del staff que da precisión de scouting.</summary>
    public const string SCOUT_POSITION = "OJEADOR";

    /// <summary>Error máximo (puntos) según reputación de los ojeadores (1..5):
    /// reputación 1 → ±5, 5 → ±1.</summary>
    public static int MaxError(int scoutReputation) => Mathf.Clamp(6 - scoutReputation, 1, 5);

    /// <summary>
    /// Error firmado de la evaluación. Distribución triangular: los errores pequeños
    /// son habituales y los grandes raros. `roll1`/`roll2` son dos tiradas uniformes
    /// en [-max, max] que aporta el llamador (así el helper es puro y testeable).
    /// </summary>
    public static int ErrorFor(int scoutReputation, int roll1, int roll2)
    {
        int max = MaxError(scoutReputation);
        int a = Mathf.Clamp(roll1, -max, max);
        int b = Mathf.Clamp(roll2, -max, max);
        return (a + b) / 2;
    }

    /// <summary>
    /// Valor mostrado por el ojeador a partir del valor real. El suelo 25 es el límite
    /// de esta estimación, no el mínimo legal de un atributo (`PlayerAttributeScale`).
    /// </summary>
    public static int Estimate(int trueValue, int error) =>
        PlayerAttributeScale.ClampScoutingEstimate(trueValue + error);

    /// <summary>Valor real a partir de la estimación y el error guardado.</summary>
    public static int Reveal(int estimate, int error) =>
        PlayerAttributeScale.ClampScoutingEstimate(estimate - error);

    /// <summary>Reputación del mejor ojeador del equipo (1 si no hay ninguno).</summary>
    public static int ScoutReputation(List<EmployeeData> staff)
    {
        if (staff == null) return 1;
        int best = 0;
        foreach (var e in staff)
            if (e != null && e.position == SCOUT_POSITION && e.reputation > best)
                best = e.reputation;
        return best > 0 ? best : 1;
    }
}
