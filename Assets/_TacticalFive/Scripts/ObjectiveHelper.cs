using System.Collections.Generic;
using System.Linq;

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
        if (teams == null || games == null) return 0;

        var confTeams = teams.Where(t => t.conference == conference).ToList();
        if (confTeams.Count == 0) return 0;

var standings = new List<(TeamData team, int wins, int losses)>();
        foreach (var team in confTeams)
        {
            var tg = games
                .Where(g => g.is_played == 1
                            && g.game_type == "regular"
                            && (g.home_team_id == team.id || g.away_team_id == team.id))
                .ToList();
            int w = tg.Count(g =>
                (g.home_team_id == team.id && g.home_score > g.away_score) ||
                (g.away_team_id == team.id && g.away_score > g.home_score));
            standings.Add((team, w, tg.Count - w));
        }

        standings.Sort((a, b) =>
        {
            float pctA = a.wins + a.losses > 0 ? (float)a.wins / (a.wins + a.losses) : 0f;
            float pctB = b.wins + b.losses > 0 ? (float)b.wins / (b.wins + b.losses) : 0f;
            if (pctB != pctA) return pctB.CompareTo(pctA);
            if (a.losses != b.losses) return a.losses.CompareTo(b.losses);
            return b.wins.CompareTo(a.wins);
        });

        for (int i = 0; i < standings.Count; i++)
            if (standings[i].team.id == teamId) return i + 1;

        return 0;
    }
}