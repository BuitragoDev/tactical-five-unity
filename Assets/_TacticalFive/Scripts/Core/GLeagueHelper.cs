using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Reglas puras de la liga de desarrollo (G-League ligera): cuándo un jugador
/// puede ser asignado/recuperado, el tick de desarrollo semanal y la generación
/// de estadísticas procedimentales semanales.
/// </summary>
public static class GLeagueHelper
{
    /// <summary>Mínimo de jugadores activos (sanos, no IR, no G-League) que deben
    /// quedar en la plantilla NBA al asignar a un jugador a la liga de desarrollo.</summary>
    public const int MIN_ACTIVE_PLAYERS = 12;

    /// <summary>¿Puede asignarse a este jugador a la G-League?</summary>
    public static bool CanAssign(PlayerData p)
    {
        return p != null
            && p.team_id > 0
            && p.injury_days == 0
            && p.is_on_ir == 0
            && p.g_league_assigned == 0;
    }

    /// <summary>¿Puede recuperarse a este jugador de la G-League?</summary>
    public static bool CanRecall(PlayerData p)
    {
        return p != null && p.g_league_assigned == 1;
    }

    /// <summary>Comprueba si asignar al jugador deja al menos MIN_ACTIVE_PLAYERS activos.</summary>
    public static bool HasEnoughActive(List<PlayerData> roster)
    {
        int active = roster.Count(p => p.injury_days == 0 && p.is_on_ir == 0 && p.g_league_assigned == 0);
        return active - 1 >= MIN_ACTIVE_PLAYERS;
    }

    /// <summary>
    /// Tick semanal de desarrollo: +1 en un atributo aleatorio hasta el potencial.
    /// Usa UnityEngine.Random (main thread).
    /// </summary>
    public static bool ProcessDevelopmentTick(PlayerData p)
    {
        if (p == null || p.g_league_assigned != 1) return false;

        var attrs = new[]
        {
            "speed", "shooting", "three_point", "passing", "dribbling",
            "defense", "rebounding", "athleticism", "iq", "steals", "blocks"
        };
        var options = attrs.Where(a => GetAttr(p, a) < p.potential).ToList();
        if (options.Count == 0) return false;

        string pick = options[UnityEngine.Random.Range(0, options.Count)];
        SetAttr(p, pick, GetAttr(p, pick) + 1);
        return true;
    }

    /// <summary>Genera unas estadísticas procedimentales semanales para un jugador en G-League.</summary>
    public static (int pts, int reb, int ast, int stl, int blk, int tov, int rating) GenerateWeeklyStats(PlayerData p)
    {
        if (p == null) return (0, 0, 0, 0, 0, 0, 0);

        float ovr = p.GetCalculatedAverage();
        float gamesThisWeek = 2; // 2 partidos de desarrollo por semana

        int pts = (int)((ovr * 0.22f) * gamesThisWeek + UnityEngine.Random.Range(-3, 4));
        int reb = (int)((ovr * 0.09f) * gamesThisWeek + UnityEngine.Random.Range(-1, 2));
        int ast = (int)((ovr * 0.08f) * gamesThisWeek + UnityEngine.Random.Range(-1, 2));
        int stl = (int)(UnityEngine.Random.Range(1, 4));
        int blk = (int)(UnityEngine.Random.Range(0, 3));
        int tov = (int)(UnityEngine.Random.Range(2, 5));
        int rating = (int)UnityEngine.Random.Range(8, 16) + (int)(ovr * 0.2f);

        pts = Mathf.Max(0, pts);
        reb = Mathf.Max(0, reb);
        ast = Mathf.Max(0, ast);
        return (pts, reb, ast, stl, blk, tov, rating);
    }

    static int GetAttr(PlayerData p, string name)
    {
        switch (name)
        {
            case "speed": return p.speed;
            case "shooting": return p.shooting;
            case "three_point": return p.three_point;
            case "passing": return p.passing;
            case "dribbling": return p.dribbling;
            case "defense": return p.defense;
            case "rebounding": return p.rebounding;
            case "athleticism": return p.athleticism;
            case "iq": return p.iq;
            case "steals": return p.steals;
            case "blocks": return p.blocks;
            default: return 0;
        }
    }

    static void SetAttr(PlayerData p, string name, int value)
    {
        int v = Mathf.Min(p.potential, Mathf.Max(30, value));
        switch (name)
        {
            case "speed": p.speed = v; break;
            case "shooting": p.shooting = v; break;
            case "three_point": p.three_point = v; break;
            case "passing": p.passing = v; break;
            case "dribbling": p.dribbling = v; break;
            case "defense": p.defense = v; break;
            case "rebounding": p.rebounding = v; break;
            case "athleticism": p.athleticism = v; break;
            case "iq": p.iq = v; break;
            case "steals": p.steals = v; break;
            case "blocks": p.blocks = v; break;
        }
        // Recalcular overall tras la mejora (mantiene la invariante media de 11 atributos)
        p.overall = System.Math.Min(p.potential, p.GetCalculatedAverage());
    }
}