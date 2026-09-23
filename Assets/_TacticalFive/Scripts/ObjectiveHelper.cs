using System.Collections.Generic;

/// <summary>
/// Lógica pura de los objetivos de temporada de los equipos y del ranking de
/// conferencia, extraída para reutilizarse en Dashboard/Manager y en el cese
/// por objetivo de final de temporada. Sin dependencias de UI ni de la base.
/// </summary>
public static class ObjectiveHelper
{
    /// <summary>
    /// Devuelve si un objetivo se considera cumplido según la posición en
    /// su conferencia (rank 1 = primero). rank <= 0 (sin dato) nunca cumple.
    /// </summary>
    public static bool IsObjectiveMet(string objective, int rank)
    {
        if (rank <= 0) return false;

        switch (objective)
        {
            case "Zona tranquila":
                return rank <= 12;   // 11+ = no entrar en nada
            case "Play-In":
                return rank <= 10;   // 1-10 = al menos play-in
            case "Playoffs":
                return rank <= 6;    // 1-6 = posición de playoffs
            case "Campeonato":
                return rank <= 2;    // 1-2 = top directo / contender
            default:
                return false;
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