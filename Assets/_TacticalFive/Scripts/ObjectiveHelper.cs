using System.Collections.Generic;

/// <summary>
/// Lógica pura de los objetivos de temporada de los equipos y del ranking de
/// conferencia, extraída para reutilizarse en Dashboard/Manager y en el cese
/// por objetivo de final de temporada. Sin dependencias de UI ni de la base.
/// </summary>
public static class ObjectiveHelper
{
    // Objetivos posibles, de menos a más exigente.
    public const string ZONA_TRANQUILA = "Zona tranquila";
    public const string PLAY_IN = "Play-In";
    public const string PLAYOFFS = "Playoffs";
    public const string CAMPEONATO = "Campeonato";

    /// <summary>
    /// Devuelve si un objetivo se considera cumplido según la posición en
    /// su conferencia (rank 1 = primero). rank <= 0 (sin dato) nunca cumple.
    /// </summary>
    public static bool IsObjectiveMet(string objective, int rank)
    {
        if (rank <= 0) return false;

        switch (objective)
        {
            case ZONA_TRANQUILA:
                return rank <= 12;   // 11+ = no entrar en nada
            case PLAY_IN:
                return rank <= 10;   // 1-10 = al menos play-in
            case PLAYOFFS:
                return rank <= 6;    // 1-6 = posición de playoffs
            case CAMPEONATO:
                return rank <= 2;    // 1-2 = top directo / contender
            default:
                return false;
        }
    }

    /// <summary>
    /// Objetivo base que pide el dueño según la fuerza de la plantilla
    /// (puesto 1..N de la liga por media de `overall`). Con 30 equipos:
    /// 1-5 Campeonato, 6-12 Playoffs, 13-22 Play-In, 23-30 Zona tranquila.
    /// Devuelve null si no hay dato (puesto &lt;= 0).
    /// </summary>
    public static string ObjectiveForStrengthRank(int strengthRank)
    {
        if (strengthRank <= 0) return null;
        if (strengthRank <= 5) return CAMPEONATO;
        if (strengthRank <= 12) return PLAYOFFS;
        if (strengthRank <= 22) return PLAY_IN;
        return ZONA_TRANQUILA;
    }

    /// <summary>
    /// Objetivo de la temporada que empieza: base por fuerza de plantilla, con el
    /// resultado de la temporada cerrada como ajuste.
    /// - Campeón: defiende el título (Campeonato), aunque la plantilla se debilite.
    /// - Finalista: como mínimo Playoffs.
    /// - Si cumplió el objetivo anterior, el dueño no baja el listón.
    /// - Si lo falló, se queda en la base de plantilla (la exigencia se ajusta a la
    ///   realidad del roster, sin dobles castigos).
    /// Devuelve null si no hay datos para decidir (se conserva el objetivo actual).
    /// </summary>
    public static string NextObjective(string current, int conferenceRank, int strengthRank,
                                       bool champion, bool finalist)
    {
        string baseObjective = ObjectiveForStrengthRank(strengthRank);
        if (baseObjective == null) return null;
        if (champion) return CAMPEONATO;

        string next = baseObjective;
        if (finalist) next = Higher(next, PLAYOFFS);
        if (IsObjectiveMet(current, conferenceRank)) next = Higher(next, current);
        return next;
    }

    /// <summary>El más exigente de los dos objetivos (null = sin dato).</summary>
    public static string Higher(string a, string b)
    {
        int levelA = Level(a);
        int levelB = Level(b);
        if (levelA < 0) return levelB < 0 ? null : b;
        if (levelB < 0) return a;
        return levelA >= levelB ? a : b;
    }

    /// <summary>Escalón del objetivo (0 Zona tranquila … 3 Campeonato; -1 desconocido).</summary>
    public static int Level(string objective)
    {
        switch (objective)
        {
            case ZONA_TRANQUILA: return 0;
            case PLAY_IN:        return 1;
            case PLAYOFFS:       return 2;
            case CAMPEONATO:     return 3;
            default:             return -1;
        }
    }

    /// <summary>
    /// Calcula la posición (1..N) de un equipo dentro de su conferencia a
    /// partir de los partidos de liga ya jugados (solo "regular" y "is_played").
    /// Devuelve 0 si no se puede determinar.
    /// </summary>
    public static int GetConferenceRank(
        int teamId,
        string conference,
        List<TeamData> teams,
        List<GameData> games)
    {
        return StandingsHelper.GetRank(teamId, conference, teams, games);
    }
}