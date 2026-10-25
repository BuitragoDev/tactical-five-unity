/// <summary>
/// Escala legal de los 11 atributos y límites propios de los procesos que generan
/// estimaciones. Un atributo bajo es válido si está entre MIN_VALUE y MAX_VALUE;
/// GENERATION_FLOOR y SCOUTING_ESTIMATE_FLOOR son reglas de esos procesos, no el
/// mínimo legal del jugador ni una razón para elevar ratings del seed.
/// </summary>
public static class PlayerAttributeScale
{
    public const int MIN_VALUE = 1;
    public const int MAX_VALUE = 99;

    // Suelo usado al crear perfiles de draft/G-League; no aplica a ratings manuales.
    public const int GENERATION_FLOOR = 30;

    // La estimación de scouting nunca revela ni muestra menos de 25.
    public const int SCOUTING_ESTIMATE_FLOOR = 25;

    // Umbral informativo para resumir valores bajos sin considerarlos un defecto.
    public const int LOW_VALUE_INFO_THRESHOLD = 25;

    public static bool IsValid(int value) => value >= MIN_VALUE && value <= MAX_VALUE;

    public static int ClampValid(int value) =>
        System.Math.Max(MIN_VALUE, System.Math.Min(MAX_VALUE, value));

    public static int ClampGenerated(int value) =>
        System.Math.Max(GENERATION_FLOOR, System.Math.Min(MAX_VALUE, value));

    public static int ClampGenerated(int value, int potential)
    {
        int upper = potential > 0 ? System.Math.Min(MAX_VALUE, potential) : MAX_VALUE;
        int floor = upper >= GENERATION_FLOOR ? GENERATION_FLOOR : MIN_VALUE;
        return System.Math.Max(floor, System.Math.Min(upper, value));
    }

    public static int ClampScoutingEstimate(int value) =>
        System.Math.Max(SCOUTING_ESTIMATE_FLOOR, System.Math.Min(MAX_VALUE, value));
}
