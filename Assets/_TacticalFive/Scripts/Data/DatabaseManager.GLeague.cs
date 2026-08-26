using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public partial class DatabaseManager
{
    // ── G-LEAGUE ─────────────────────────────────────────

    /// <summary>Sembra filiales y prospectos G-League si las tablas están vacías.
    /// Idempotente; se llama desde SeedStaticDataIfNeeded y desde InitSaveSlot
    /// (para partidas existentes creadas antes de esta feature).</summary>
    internal void EnsureGLeagueSeeded()
    {
        if (_db.Table<GLeagueTeamData>().Count() == 0)
            SeedGLeagueTeams();

        if (_db.Table<GLeaguePlayerData>().Count() == 0)
            SeedGLeagueProspects();
    }

    void SeedGLeagueTeams()
    {
        var nbaByAbbr = _db.Table<TeamData>().ToDictionary(t => t.abbreviation, t => t.id);
        int inserted = 0;

        foreach (var (name, nbaAbbr, conference, logo) in GLeagueSeeder.Teams)
        {
            if (!nbaByAbbr.TryGetValue(nbaAbbr, out var nbaTeamId))
            {
                Debug.LogWarning($"[DB] Franquicia NBA '{nbaAbbr}' no encontrada para la filial {name}");
                continue;
            }

            string abbr = string.Concat(name.Split(' ').Where(w => w.Length > 0).Select(w => w[0])).ToUpper();
            _db.Insert(new GLeagueTeamData
            {
                name = name,
                abbreviation = abbr,
                conference = conference,
                logo = logo,
                nba_team_id = nbaTeamId
            });
            inserted++;
        }

        Debug.Log($"[DB] {inserted} filiales G-League insertadas.");
    }

    void SeedGLeagueProspects()
    {
        var glTeams = _db.Table<GLeagueTeamData>().ToList();
        var all = new List<GLeaguePlayerData>();
        foreach (var t in glTeams)
            all.AddRange(GLeagueSeeder.GenerateProspects(t.id));

        _db.InsertAll(all);
        Debug.Log($"[DB] {all.Count} prospectos G-League generados para {glTeams.Count} filiales.");
    }

    // ── EQUIPOS ──────────────────────────────────────────

    public List<GLeagueTeamData> GetGLeagueTeams()
    {
        return _db.Table<GLeagueTeamData>()
                  .OrderBy(t => t.conference)
                  .ThenBy(t => t.name)
                  .ToList();
    }

    public GLeagueTeamData GetGLeagueTeam(int id)
    {
        return _db.Table<GLeagueTeamData>().FirstOrDefault(t => t.id == id);
    }

    public GLeagueTeamData GetGLeagueTeamByNbaTeam(int nbaTeamId)
    {
        return _db.Table<GLeagueTeamData>().FirstOrDefault(t => t.nba_team_id == nbaTeamId);
    }

    // ── PROSPECTOS ───────────────────────────────────────

    public List<GLeaguePlayerData> GetGLeaguePlayersByTeam(int gleagueTeamId)
    {
        return _db.Table<GLeaguePlayerData>()
                  .Where(p => p.gleague_team_id == gleagueTeamId)
                  .OrderByDescending(p => p.overall)
                  .ToList();
    }

    public List<GLeaguePlayerData> GetAllGLeaguePlayers()
    {
        return _db.Table<GLeaguePlayerData>().ToList();
    }

    /// <summary>Jugadores NBA asignados a G-League, agrupados por team_id matriz.</summary>
    public Dictionary<int, List<PlayerData>> GetGLeagueAssignedByTeam()
    {
        return _db.Table<PlayerData>()
                  .Where(p => p.g_league_assigned == 1)
                  .ToList()
                  .GroupBy(p => p.team_id)
                  .ToDictionary(g => g.Key, g => g.ToList());
    }

    // ── PARTIDOS ─────────────────────────────────────────

    /// <summary>Todos los partidos G-League (liga regular + playoffs) del manager.</summary>
    public List<GameData> GetAllGLeagueGames(int managerId)
    {
        return _db.Query<GameData>(
            "SELECT * FROM games WHERE manager_id = ? AND game_type IN ('gleague','gleague_playoff') ORDER BY game_day, id",
            managerId);
    }

    public void SaveGLeagueGames(List<GameData> games)
    {
        if (games.Count == 0) return;
        _db.BeginTransaction();
        try
        {
            foreach (var g in games)
                _db.Insert(g);
            _db.Commit();
            Debug.Log($"[DB] {games.Count} partidos G-League guardados.");
        }
        catch (System.Exception e)
        {
            _db.Rollback();
            Debug.LogError($"[DB] Error guardando partidos G-League: {e.Message}");
        }
    }

    // ── ESTADÍSTICAS ─────────────────────────────────────

    public List<GLeagueSeasonStat> GetAllGLeagueSeasonStats(int seasonId)
    {
        return _db.Table<GLeagueSeasonStat>()
                  .Where(s => s.season_id == seasonId)
                  .ToList();
    }

    /// <summary>Acumula el box score de UN partido disputado en la G-League (games += 1).</summary>
    public void AddGLeagueGameStat(int playerId, int seasonId, int points, int rebounds, int assists,
                                   int steals, int blocks, int turnovers, int rating)
    {
        var stat = GetGLeagueStats(playerId, seasonId);
        if (stat == null)
        {
            _db.Insert(new GLeagueSeasonStat
            {
                player_id = playerId,
                season_id = seasonId,
                games = 1,
                points = points,
                rebounds = rebounds,
                assists = assists,
                steals = steals,
                blocks = blocks,
                turnovers = turnovers,
                rating = rating
            });
        }
        else
        {
            stat.games += 1;
            stat.points += points;
            stat.rebounds += rebounds;
            stat.assists += assists;
            stat.steals += steals;
            stat.blocks += blocks;
            stat.turnovers += turnovers;
            stat.rating += rating;
            _db.Update(stat);
        }
    }

    // ── CAMPEONES ────────────────────────────────────────

    public void SaveGLeagueChampion(GLeagueChampionData champion)
    {
        bool exists = _db.Table<GLeagueChampionData>()
                         .Any(c => c.manager_id == champion.manager_id && c.season_id == champion.season_id);
        if (exists) return;
        _db.Insert(champion);
    }

    public List<GLeagueChampionData> GetGLeagueChampions(int managerId)
    {
        return _db.Table<GLeagueChampionData>()
                  .Where(c => c.manager_id == managerId)
                  .OrderBy(c => c.season_id)
                  .ToList();
    }

    // ── RESUMEN DE TEMPORADA ─────────────────────────────

    public GameData GetGLFinalGame(int managerId, int seasonId)
    {
        return _db.Query<GameData>(
            "SELECT * FROM games WHERE manager_id=? AND season_id=? AND game_type='gleague_playoff' AND series_label='gl-final' AND is_played=1 LIMIT 1",
            managerId, seasonId).FirstOrDefault();
    }

    public GLSeasonMVPRow GetGLSeasonMVP(int managerId, int seasonId)
    {
        // Primero: prospectos (player_id >= 500000, offset en gleague_season_stats)
        var prospectMVP = _db.Query<GLSeasonMVPRow>(@"
            SELECT gs.player_id, gp.first_name, gp.last_name, gp.position,
                   gt.name as team_name, gt.logo as team_logo,
                   gs.games,
                   CAST(gs.rating AS REAL)/gs.games as avg_rating,
                   CAST(gs.points AS REAL)/gs.games as avg_pts,
                   CAST(gs.rebounds AS REAL)/gs.games as avg_reb,
                   CAST(gs.assists AS REAL)/gs.games as avg_ast
            FROM gleague_season_stats gs
            JOIN gleague_players gp ON gs.player_id = gp.id + 500000
            JOIN gleague_teams gt ON gp.gleague_team_id = gt.id
            WHERE gs.season_id=? AND gs.games > 15 AND gs.player_id >= 500000
            ORDER BY CAST(gs.rating AS REAL)/gs.games DESC LIMIT 1", seasonId).FirstOrDefault();

        if (prospectMVP != null) return prospectMVP;

        // Segundo: jugadores NBA asignados (player_id < 500000)
        return _db.Query<GLSeasonMVPRow>(@"
            SELECT gs.player_id, p.first_name, p.last_name, p.position,
                   t.name as team_name, t.logo as team_logo,
                   gs.games,
                   CAST(gs.rating AS REAL)/gs.games as avg_rating,
                   CAST(gs.points AS REAL)/gs.games as avg_pts,
                   CAST(gs.rebounds AS REAL)/gs.games as avg_reb,
                   CAST(gs.assists AS REAL)/gs.games as avg_ast
            FROM gleague_season_stats gs
            JOIN players p ON gs.player_id = p.id
            JOIN teams t ON p.team_id = t.id
            WHERE gs.season_id=? AND gs.games > 15 AND gs.player_id < 500000
            ORDER BY CAST(gs.rating AS REAL)/gs.games DESC LIMIT 1", seasonId).FirstOrDefault();
    }
}
