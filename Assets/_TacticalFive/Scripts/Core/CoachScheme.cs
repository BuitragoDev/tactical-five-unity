using UnityEngine;

/// <summary>
/// Esquema táctico del asistente (posición ASISTENTE del staff). El asistente aporta
/// un estilo de juego propio que modula el plan del usuario y un bonus/malus al
/// rendimiento del equipo según su reputación. Lógica pura y testeable.
/// </summary>
public static class CoachScheme
{
    /// <summary>Posición del staff que aporta esquema táctico.</summary>
    public const string POSITION = "ASISTENTE";

    /// <summary>Bonus al rating del equipo por reputación del asistente (1..5).</summary>
    public static int RatingBonus(int reputation) => Mathf.Clamp(reputation - 3, -2, 2);

    /// <summary>Asigna un esquema aleatorio coherente a un candidato/asistente.</summary>
    public static void AssignRandomScheme(EmployeeData e, System.Random rng)
    {
        if (e == null || rng == null) return;
        int r = rng.Next(0, 6);
        switch (r)
        {
            case 0: e.scheme_pace = 1; e.scheme_offense = 2; e.scheme_defense = 2; break; // run & gun
            case 1: e.scheme_pace = -1; e.scheme_offense = 1; e.scheme_defense = 1; break; // control
            case 2: e.scheme_pace = 0; e.scheme_offense = 2; e.scheme_defense = 1; break; // triples
            case 3: e.scheme_pace = 0; e.scheme_offense = 1; e.scheme_defense = 2; break; // interior + presión
            case 4: e.scheme_pace = -1; e.scheme_offense = 1; e.scheme_defense = 0; break; // defensivo
            default: e.scheme_pace = 0; e.scheme_offense = 0; e.scheme_defense = 1; break; // equilibrado
        }
        e.scheme_set = 1;
    }

    /// <summary>Plan del equipo: media entre lo que pide el usuario y lo que propone el
    /// asistente (el esquema matiza la instrucción, no la anula).</summary>
    public static GamePlan Blend(GamePlan userPlan, EmployeeData coach)
    {
        if (coach == null || coach.scheme_set != 1) return userPlan;
        return new GamePlan
        {
            pace = Mathf.RoundToInt((userPlan.pace + coach.scheme_pace) * 0.5f),
            offense = Mathf.RoundToInt((userPlan.offense + coach.scheme_offense) * 0.5f),
            defense = Mathf.RoundToInt((userPlan.defense + coach.scheme_defense) * 0.5f)
        };
    }

    public static string SchemeLabel(EmployeeData coach)
    {
        if (coach == null || coach.scheme_set != 1) return "";
        string pace = coach.scheme_pace > 0 ? "RÁPIDO" : coach.scheme_pace < 0 ? "LENTO" : "NORMAL";
        string off = coach.scheme_offense >= 2 ? "EXTERIOR" : coach.scheme_offense == 1 ? "INTERIOR" : "EQUILIBRADO";
        string def = coach.scheme_defense >= 2 ? "AGRESIVA" : coach.scheme_defense == 0 ? "CONSERVADORA" : "EQUILIBRADA";
        return $"{pace} · {off} · {def}";
    }
}
