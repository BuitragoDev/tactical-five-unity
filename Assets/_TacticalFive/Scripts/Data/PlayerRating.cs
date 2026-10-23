/// <summary>
/// FUENTE ÚNICA del cálculo de `overall`: media de los 11 atributos redondeada,
/// capada por el potencial. Cualquier sitio que derive un overall de los atributos
/// (seed, agentes libres, G-League, migraciones, draft, progresión, herramientas del
/// editor) debe llamar aquí. No dupliques la fórmula en otro archivo.
/// </summary>
public static class PlayerRating
{
    /// <summary>Número de atributos que componen el overall.</summary>
    public const int ATTRIBUTE_COUNT = 11;

    /// <summary>
    /// Overall a partir de la suma de los 11 atributos y el potencial.
    /// El potencial (> 0) actúa de techo: nunca se muestra un overall por encima de él.
    /// </summary>
    public static int FromSum(int attributeSum, int potential)
    {
        int avg = (int)System.Math.Round(attributeSum / (double)ATTRIBUTE_COUNT);
        return potential > 0 && potential < avg ? potential : avg;
    }

    /// <summary>
    /// Overall a partir de los 11 atributos, en el orden que usan `PlayerData`,
    /// `GLeaguePlayerData` y el seed.
    /// </summary>
    public static int FromAttributes(int speed, int shooting, int three_point, int passing, int dribbling,
                                     int defense, int rebounding, int athleticism, int iq, int steals,
                                     int blocks, int potential)
    {
        int sum = speed + shooting + three_point + passing + dribbling + defense
                + rebounding + athleticism + iq + steals + blocks;
        return FromSum(sum, potential);
    }
}
