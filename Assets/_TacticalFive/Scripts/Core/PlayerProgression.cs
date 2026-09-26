/// <summary>
/// Progresión/declive de atributos en el offseason. Lógica pura y testeable:
/// combina edad, potencial, nivel actual, minutos de la temporada anterior y la
/// prioridad del atributo según la posición. Lo usan el rollover de temporada.
/// </summary>
public static class PlayerProgression
{
    /// <summary>Cambio base por grupo de edad.</summary>
    public static int AgeBase(int age)
    {
        if (age <= 22) return 4;    // crecimiento rápido
        if (age <= 27) return 1;    // prime temprano
        if (age <= 30) return 0;    // prime tardío
        if (age <= 34) return -3;   // declive suave
        return -5;                  // declive fuerte
    }

    /// <summary>
    /// Variación de un atributo en el offseason.
    /// </summary>
    /// <param name="age">Edad DESPUÉS de cumplir años.</param>
    /// <param name="potential">Techo del jugador (0 = desconocido).</param>
    /// <param name="overall">Overall actual (para detectar el techo).</param>
    /// <param name="mpg">Minutos por partido de la temporada anterior (-1 = sin datos).</param>
    /// <param name="isPriority">¿Es un atributo prioritario de su posición?</param>
    /// <param name="randomTweak">±1 aleatorio ya sorteado por el llamador.</param>
    public static int AttributeDelta(int age, int potential, int overall, int mpg,
                                     bool isPriority, int randomTweak)
    {
        int change = AgeBase(age);
        if (isPriority) change += 1;

        // Los minutos de la temporada anterior modulan el desarrollo de los jóvenes:
        // quien juega progresa; quien no juega, se estanca o retrocede.
        if (age <= 27 && mpg >= 0)
        {
            if (mpg >= 28) change += 1;
            else if (mpg < 12) change -= 1;
        }

        // El potencial acelera o frena la progresión temprana.
        if (age <= 24)
        {
            if (potential >= 88) change += 1;
            else if (potential > 0 && potential <= 70) change -= 1;
        }

        change += randomTweak;

        // Un jugador en (o por encima de) su techo no puede seguir subiendo.
        if (potential > 0 && overall >= potential && change > 0)
            change = 0;

        return change;
    }
}
