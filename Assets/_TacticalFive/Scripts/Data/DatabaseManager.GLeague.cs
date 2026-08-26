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
}
