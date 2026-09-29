using System.Collections.Generic;
using System.Linq;

/// <summary>Un convocado para calcular el estado de la rotación.</summary>
public class RotationSlot
{
    public int playerId;
    /// <summary>`team_lineup.rotation_minutes` (-1 = automático).</summary>
    public int minutes;
    /// <summary>Disponible para jugar (ni lesionado ni en G-League).</summary>
    public bool playable;
    /// <summary>Minutos que sugeriría el simulador (heurística por rol/OVR/slot).</summary>
    public int suggestion;
}

/// <summary>Estado del reparto de minutos de la rotación.</summary>
public class RotationStatus
{
    /// <summary>Minutos efectivos de los convocados jugables.</summary>
    public int total;
    /// <summary>Ningún convocado jugable tiene plan positivo: el simulador reparte 240.</summary>
    public bool allAuto;
    /// <summary>Algún valor fuera de rango (-1/0..48).</summary>
    public bool hasInvalid;
    public int playableCount;

    /// <summary>Minutos que faltan (&gt;0) o sobran (&lt;0) para llegar a 240.</summary>
    public int Missing => RotationPlanner.TEAM_MINUTES - total;

    /// <summary>La rotación está lista: sin jugables, en automático, o 240 exactos y válidos.</summary>
    public bool IsComplete => playableCount == 0 || allAuto || (!hasInvalid && total == RotationPlanner.TEAM_MINUTES);
}

/// <summary>
/// Reparto automático de los 240 minutos de un partido entre la convocatoria. Puro y
/// testeable: lo usan el botón AUTO de ROTACIÓN y la vista del estado "todo automático",
/// de forma que lo que se muestra coincide con lo que se juega cuando se fijan los valores.
/// </summary>
public static class RotationPlanner
{    public const int TEAM_MINUTES = 240;
    public const int MAX_PER_PLAYER = 48;
    /// <summary>
    /// Tamaño de la rotación: solo los 10 mejores objetivos reparten los 240 minutos (como
    /// una rotación NBA real). El resto de convocados se queda en el suelo.
    /// </summary>
    public const int ROTATION_SIZE = 10;
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

        // Rotación corta: los ROTATION_SIZE mejores objetivos reparten los minutos; el resto
        // se queda en el suelo (no entran en el reparto).
        int rotationSize = System.Math.Min(ROTATION_SIZE, n);
        var inRotation = Enumerable.Range(0, n)
            .OrderByDescending(i => sugar[i]).ThenBy(i => i)
            .Take(rotationSize)
            .ToHashSet();

        var mins = new int[n];
        var cap = new int[n];
        int assigned = 0;
        for (int i = 0; i < n; i++)
        {
            mins[i] = min;
            cap[i] = MAX_PER_PLAYER - min;
            assigned += min;
        }

        // Los que quedan fuera de la rotación solo suman su suelo; los que entran reparten
        // el resto (manteniendo la suma exacta en `total`).
        int pool = total - min * n;               // minutos a repartir entre la rotación
        if (pool <= 0) return mins;

        float rotSum = 0f;
        foreach (int i in inRotation) rotSum += sugar[i];
        if (rotSum <= 0f) rotSum = rotationSize;

        var remainders = new float[n];
        int distributed = 0;
        foreach (int i in inRotation)
        {
            float raw = pool * (sugar[i] / rotSum);
            int whole = (int)raw;
            if (whole > cap[i]) whole = cap[i];
            mins[i] += whole;
            distributed += whole;
            remainders[i] = raw - (int)raw;
        }

        int leftover = total - (assigned + distributed);
        var order = Enumerable.Range(0, n).Where(i => inRotation.Contains(i))
                              .OrderByDescending(i => remainders[i]).ThenByDescending(i => sugar[i]).ToList();
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
            if (!gave) break;   // toda la rotación en el tope
            guard++;
        }

        return mins;
    }

    /// <summary>
    /// Evalúa el reparto actual: total de los convocados jugables, si está todo en
    /// automático y si hay valores inválidos. Es la misma cuenta que muestra la pantalla
    /// ROTACIÓN (fijos tal cual; los `-1` con su sugerencia salvo si todo es automático).
    /// </summary>
    public static RotationStatus Evaluate(IReadOnlyList<RotationSlot> slots)
    {
        var st = new RotationStatus();
        if (slots == null) return st;

        var playable = slots.Where(s => s != null && s.playable).ToList();
        st.playableCount = playable.Count;
        if (playable.Count == 0) return st;

        st.hasInvalid = playable.Any(s => s.minutes < -1 || s.minutes > MAX_PER_PLAYER);
        st.allAuto = playable.All(s => s.minutes <= 0);
        if (st.allAuto)
        {
            st.total = TEAM_MINUTES;   // el simulador reparte los 240 automáticamente
            return st;
        }

        int total = 0;
        foreach (var s in playable)
            total += s.minutes >= 0 ? System.Math.Min(s.minutes, MAX_PER_PLAYER) : System.Math.Max(0, s.suggestion);
        st.total = total;
        return st;
    }
}
