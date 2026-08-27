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

    // ── LIGA COMPLETA (filiales y prospectos) ────────────

    /// <summary>Offset reservado para los ids de prospectos al simular: evita
    /// colisiones con la tabla players en cualquier referencia cruzada.</summary>
    public const int PROSPECT_ID_OFFSET = 500000;

    /// <summary>
    /// Offset aplicado a los ids de filial almacenados en games.home_team_id /
    /// away_team_id. Los partidos G-League se guardan codificados (id + 1000)
    /// para que NINGUNA query histórica que compare contra ids de equipos NBA
    /// (1..30) pueda confundir un partido de la filial con uno del equipo matriz.
    /// </summary>
    public const int GAME_TEAM_ID_OFFSET = 1000;

    public static bool IsProspectId(int simId) => simId >= PROSPECT_ID_OFFSET;

    public static int ProspectRowId(int simId) => simId - PROSPECT_ID_OFFSET;

    public static int ProspectSimId(GLeaguePlayerData p) => PROSPECT_ID_OFFSET + p.id;

    /// <summary>Codifica un id de filial para almacenarlo en una fila GameData.</summary>
    public static int EncodeGlTeamId(int gleagueTeamId) => GAME_TEAM_ID_OFFSET + gleagueTeamId;

    /// <summary>Descodifica el id de equipo almacenado en un GameData:
    /// devuelve el id de filial si está codificado, o el valor tal cual.</summary>
    public static int DecodeGlTeamId(int storedId)
        => storedId >= GAME_TEAM_ID_OFFSET ? storedId - GAME_TEAM_ID_OFFSET : storedId;

    public static string PlayerName(PlayerData p) => $"{p.first_name} {p.last_name}";

    /// <summary>
    /// Limita la línea estadística individual de un partido G-League a valores
    /// realistas. El simulador premia mucho a un único jugador NBA cuando se
    /// enfrenta a plantillas de prospectos (OVR ~50) — un asignado de 80 OVR
    /// puede anotar 60-70 pts. Esto acota el box individual sin alterar el
    /// marcador del equipo ni los demás sub-sistemas.
    /// </summary>
    public static (int pts, int reb, int ast, int stl, int blk, int tov) ClampLine(
        int pts, int reb, int ast, int stl, int blk, int tov)
    {
        return (
            Mathf.Clamp(pts, 0, 42),
            Mathf.Clamp(reb, 0, 18),
            Mathf.Clamp(ast, 0, 14),
            Mathf.Clamp(stl, 0, 6),
            Mathf.Clamp(blk, 0, 6),
            Mathf.Clamp(tov, 0, 8)
        );
    }    /// <summary>Mapea un prospecto a PlayerData transitorio para el simulador.
    /// NUNCA se persiste: los prospectos viven en gleague_players.</summary>
    public static PlayerData ToSimPlayer(GLeaguePlayerData gp)
    {
        return new PlayerData
        {
            id = ProspectSimId(gp),
            team_id = gp.gleague_team_id,
            first_name = gp.first_name,
            last_name = gp.last_name,
            position = gp.position,
            age = gp.age,
            overall = gp.overall,
            potential = gp.potential,
            speed = gp.speed,
            shooting = gp.shooting,
            three_point = gp.three_point,
            passing = gp.passing,
            dribbling = gp.dribbling,
            defense = gp.defense,
            rebounding = gp.rebounding,
            athleticism = gp.athleticism,
            iq = gp.iq,
            steals = gp.steals,
            blocks = gp.blocks,
            morale = 60,
            fisico = 99,
            injury_days = 0
        };
    }

    /// <summary>
    /// Convocatoria de una filial: prospectos de su plantilla + jugadores NBA
    /// asignados y sanos del equipo matriz. Top 12 por overall (los 5 primeros
    /// salen de titulares en el simulador).
    /// </summary>
    public static List<PlayerData> BuildAffiliateLineup(
        GLeagueTeamData affiliate,
        List<GLeaguePlayerData> allProspects,
        Dictionary<int, List<PlayerData>> assignedByNbaTeam)
    {
        var pool = new List<PlayerData>();

        if (assignedByNbaTeam != null && affiliate.nba_team_id != 0
            && assignedByNbaTeam.TryGetValue(affiliate.nba_team_id, out var assigned))
        {
            foreach (var p in assigned)
            {
                if (p != null && p.injury_days == 0 && p.is_on_ir == 0)
                    pool.Add(p);
            }
        }

        foreach (var gp in allProspects)
        {
            if (gp.gleague_team_id == affiliate.id)
                pool.Add(ToSimPlayer(gp));
        }

        return pool.OrderByDescending(p => p.overall).Take(12).ToList();
    }
}