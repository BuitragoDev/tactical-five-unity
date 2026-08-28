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

    public GLeaguePlayerData GetGLeaguePlayerById(int id)
    {
        return _db.Table<GLeaguePlayerData>().Where(p => p.id == id).FirstOrDefault();
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

        // Si ya hay una transacción abierta (p.ej. el día de partido), no abrir
        // una anidada: sqlite-net lanza InvalidOperationException y la generación
        // de playoffs de G-League fallaría silenciosamente. Inserta directamente.
        if (_db.IsInTransaction)
        {
            foreach (var g in games)
                _db.Insert(g);
            Debug.Log($"[DB] {games.Count} partidos G-League guardados (tx existente).");
            return;
        }

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

    /// <summary>Todos los partidos de la Gran Final G-League de la temporada
    /// (última ronda, mejor de 3: comparten series_label='gl-final').</summary>
    public List<GameData> GetGLFinalSeries(int managerId, int seasonId)
    {
        return _db.Query<GameData>(
            "SELECT * FROM games WHERE manager_id=? AND season_id=? AND game_type='gleague_playoff' AND series_label='gl-final' ORDER BY game_day, id",
            managerId, seasonId);
    }

    public GLSeasonMVPRow GetGLSeasonMVP(int managerId, int seasonId)
    {
        // Unificar prospectos y NBA asignados en una sola query para elegir
        // al jugador con mayor valoración media, sin priorizar un tipo sobre otro.
        return _db.Query<GLSeasonMVPRow>(@"
            SELECT * FROM (
                SELECT gs.player_id, gp.first_name, gp.last_name, gp.position,
                       gt.name as team_name, gt.logo as team_logo,
                       gs.games,
                       CAST(gs.rating AS REAL)/gs.games as avg_rating,
                       CAST(gs.points AS REAL)/gs.games as avg_pts,
                       CAST(gs.rebounds AS REAL)/gs.games as avg_reb,
                       CAST(gs.assists AS REAL)/gs.games as avg_ast,
                       CAST(gs.steals AS REAL)/gs.games as avg_stl,
                       CAST(gs.blocks AS REAL)/gs.games as avg_blk
                FROM gleague_season_stats gs
                JOIN gleague_players gp ON gs.player_id = gp.id + 500000
                JOIN gleague_teams gt ON gp.gleague_team_id = gt.id
                WHERE gs.season_id=? AND gs.games > 15 AND gs.player_id >= 500000
                UNION ALL
                SELECT gs.player_id, p.first_name, p.last_name, p.position,
                       gt.name as team_name, gt.logo as team_logo,
                       gs.games,
                       CAST(gs.rating AS REAL)/gs.games as avg_rating,
                       CAST(gs.points AS REAL)/gs.games as avg_pts,
                       CAST(gs.rebounds AS REAL)/gs.games as avg_reb,
                       CAST(gs.assists AS REAL)/gs.games as avg_ast,
                       CAST(gs.steals AS REAL)/gs.games as avg_stl,
                       CAST(gs.blocks AS REAL)/gs.games as avg_blk
                FROM gleague_season_stats gs
                JOIN players p ON gs.player_id = p.id
                JOIN gleague_teams gt ON gt.nba_team_id = p.team_id
                WHERE gs.season_id=? AND gs.games > 15 AND gs.player_id < 500000
            ) ORDER BY avg_rating DESC LIMIT 1", seasonId, seasonId).FirstOrDefault();
    }

    // ── CICLO DE VIDA ANUAL G-LEAGUE ─────────────────────

    /// <summary>
    /// Ejecuta el ciclo de vida anual de la G-League al inicio de cada nueva temporada:
    /// 1. Limpia stats de temporada anterior
    /// 2. Envejece prospectos (+1 año)
    /// 3. Retira prospectos >= 26 años
    /// 4. Progresa atributos de prospectos jóvenes
    /// 5. Rellena equipos con < 12 prospectos
    /// Llamar dentro de la transacción de StartNewSeason.
    /// </summary>
    internal void AdvanceGLeagueLifecycle()
    {
        // 1. Limpiar stats de temporada anterior
        _db.Execute("DELETE FROM gleague_season_stats");
        _db.Execute("DELETE FROM gleague_champions");
        Debug.Log("[DB] G-League: stats y campeones de temporada anterior eliminados.");

        // 2. Envejecer prospectos (+1 año)
        var allProspects = _db.Table<GLeaguePlayerData>().ToList();
        foreach (var p in allProspects)
            p.age += 1;
        _db.UpdateAll(allProspects);
        Debug.Log($"[DB] G-League: {allProspects.Count} prospectos envejecidos +1 año.");

        // 3. Retirar prospectos >= 26 años
        var retired = allProspects.Where(p => p.age >= 26).ToList();
        if (retired.Count > 0)
        {
            foreach (var p in retired)
                _db.Delete(p);
            Debug.Log($"[DB] G-League: {retired.Count} prospectos retirados (>= 26 años).");
        }

        // 4. Progresión de atributos para jóvenes
        var active = _db.Table<GLeaguePlayerData>().ToList();
        int progressed = 0;
        foreach (var p in active)
        {
            int attrsToImprove = p.age <= 22 ? 2 : p.age <= 25 ? 1 : 0;
            if (attrsToImprove <= 0) continue;

            var attrNames = new[] { "speed", "shooting", "three_point", "passing", "dribbling",
                                    "defense", "rebounding", "athleticism", "iq", "steals", "blocks" };

            for (int i = 0; i < attrsToImprove; i++)
            {
                string attr = attrNames[GLeagueSeeder.Rng.Next(0, attrNames.Length)];
                int current = attr switch
                {
                    "speed" => p.speed,
                    "shooting" => p.shooting,
                    "three_point" => p.three_point,
                    "passing" => p.passing,
                    "dribbling" => p.dribbling,
                    "defense" => p.defense,
                    "rebounding" => p.rebounding,
                    "athleticism" => p.athleticism,
                    "iq" => p.iq,
                    "steals" => p.steals,
                    "blocks" => p.blocks,
                    _ => 0
                };
                int newVal = Mathf.Min(current + 1, p.potential);
                switch (attr)
                {
                    case "speed": p.speed = newVal; break;
                    case "shooting": p.shooting = newVal; break;
                    case "three_point": p.three_point = newVal; break;
                    case "passing": p.passing = newVal; break;
                    case "dribbling": p.dribbling = newVal; break;
                    case "defense": p.defense = newVal; break;
                    case "rebounding": p.rebounding = newVal; break;
                    case "athleticism": p.athleticism = newVal; break;
                    case "iq": p.iq = newVal; break;
                    case "steals": p.steals = newVal; break;
                    case "blocks": p.blocks = newVal; break;
                }
            }

            // Recalcular overall
            int sum = p.speed + p.shooting + p.three_point + p.passing + p.dribbling
                    + p.defense + p.rebounding + p.athleticism + p.iq + p.steals + p.blocks;
            p.overall = Mathf.Min(p.potential, sum / 11);
            progressed++;
        }
        if (progressed > 0)
        {
            _db.UpdateAll(active);
            Debug.Log($"[DB] G-League: {progressed} prospectos con atributos mejorados.");
        }

        // 5. Rellenar equipos con < 12 prospectos
        var glTeams = _db.Table<GLeagueTeamData>().ToList();
        int totalAdded = 0;
        foreach (var team in glTeams)
        {
            int count = _db.Table<GLeaguePlayerData>()
                .Count(gp => gp.gleague_team_id == team.id);
            int need = GLeagueSeeder.PLAYERS_PER_TEAM - count;
            if (need <= 0) continue;

            var newProspects = GLeagueSeeder.GenerateProspects(team.id);
            _db.InsertAll(newProspects.Take(need));
            totalAdded += need;
        }
        if (totalAdded > 0)
            Debug.Log($"[DB] G-League: {totalAdded} nuevos prospectos generados para equipos incompletos.");
    }
}
