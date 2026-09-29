using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Reparto automático de los 240 minutos de un partido entre la convocatoria. Puro y
/// testeable: lo usan el botón AUTO de ROTACIÓN y la vista del estado "todo automático",
/// de forma que lo que se muestra coincide con lo que se juega cuando se fijan los valores.
/// </summary>
public static class RotationPlanner
{
    public const int TEAM_MINUTES = 240;
    public const int MAX_PER_PLAYER = 48;
    /// <summary>Suelo por jugador: el mismo que aplica el simulador a los minutos fijados.</summary>
    public const int MIN_PER_PLAYER = GameSimulator.MIN_FIXED_MINUTES;

    /// <summary>
    /// Reparte `total` minutos (240) proporcionalmente a las sugerencias de cada jugador,
    /// con suelo `MIN_PER_PLAYER` y tope `MAX_PER_PLAYER`. La suma es exactamente `total`
    /// cuando el reparto es posible; si no cabe el suelo (demasiados jugadores) se reparte
    /// proporcionalmente sin suelo.
    /// </summary>
    public static int[] AutoAssign(IReadOnlyList<int> suggestions, int total = TEAM_MINUTES)
    {
        int n = suggestions?.Count ?? 0;
        if (n == 0) return new int[0];

        var sugar = suggestions.Select(s => s > 0 ? s : 0).ToArray();
        float sum = sugar.Sum();
        if (sum <= 0f) sum = n;

        bool floorFits = MIN_PER_PLAYER * n <= total;
        int min = floorFits ? MIN_PER_PLAYER : 0;

        var mins = new int[n];
        var cap = new int[n];
        int assigned = 0;
        for (int i = 0; i < n; i++)
        {
            mins[i] = min;
            cap[i] = MAX_PER_PLAYER - min;
            assigned += min;
        }

        int extra = total - assigned;
        if (extra <= 0) return mins;   // sin margen (o suelo justo): se devuelve el mínimo

        // Reparto por mayores fracciones, respetando el tope por jugador.
        var remainders = new float[n];
        int distributed = 0;
        for (int i = 0; i < n; i++)
        {
            float raw = extra * (sugar[i] / sum);
            int whole = (int)raw;
            if (whole > cap[i]) whole = cap[i];
            mins[i] += whole;
            distributed += whole;
            remainders[i] = raw - (int)raw;
        }

        int leftover = total - (assigned + distributed);
        var order = Enumerable.Range(0, n).OrderByDescending(i => remainders[i]).ThenByDescending(i => sugar[i]).ToList();
        int guard = 0;
        while (leftover > 0 && guard < n * MAX_PER_PLAYER)
        {
            bool gave = false;
            foreach (int i in order)
            {
                if (leftover <= 0) break;
                if (mins[i] - min >= cap[i]) continue;
                mins[i]++;
                leftover--;
                gave = true;
            }
            if (!gave) break;   // todo el mundo en el tope
            guard++;
        }

        return mins;
    }
}
