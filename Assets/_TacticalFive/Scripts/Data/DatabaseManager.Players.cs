using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager
{
    // ── PLAYERS ───────────────────────────────────────────

    /// <summary>
    /// Devuelve el siguiente id de jugador de forma monótona. No reutiliza ids aunque
    /// se borre el jugador de id máximo (los premios/HOF/retirados guardan player_id).
    /// </summary>
    public int AllocatePlayerId()
    {
        if (!EnsureDb()) return 0;
        _db.Execute("CREATE TABLE IF NOT EXISTS id_counters (name TEXT PRIMARY KEY, value INTEGER NOT NULL)");
        int current = _db.ExecuteScalar<int>("SELECT COALESCE(MAX(value), 0) FROM id_counters WHERE name = 'player'");
        int next = current > 0
            ? current + 1
            : _db.ExecuteScalar<int>("SELECT COALESCE(MAX(id), 0) + 1 FROM players");
        _db.Execute("INSERT OR REPLACE INTO id_counters (name, value) VALUES ('player', ?)", next);
        return next;
    }

    /// <summary>
    /// Mueve un jugador a otro equipo en un traspaso: actualiza team_id y last_team_id
    /// (los Bird rights y el tipo de contrato two-way viajan con el jugador) y limpia
    /// los flags temporales de IR/G-League/trade block.
    /// </summary>
    public void MovePlayerToTeam(PlayerData p, int newTeamId)
    {
        int oldTeamId = p.team_id;

        p.team_id = newTeamId;
        p.last_team_id = newTeamId;
        p.is_on_ir = 0;
        p.g_league_assigned = 0;
        p.on_trade_block = 0;
        p.waived_game_day = -1;   // ya tiene equipo: deja de ser un FA del mercado de buyout
        UpdatePlayer(p);

        // Si cambia de equipo, sus hilos narrativos (solo existen del equipo del usuario)
        // se cierran al instante; el siguiente día se vuelven a comprobar por seguridad.
        if (oldTeamId != newTeamId)
            StorylineService.CloseSubjectStorylines(p.id);
    }

    public List<PlayerData> GetFreeAgents()
    {
        if (!EnsureDb()) return new List<PlayerData>();
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id == 0)
                  .OrderByDescending(p => p.overall)
                  .ToList();
    }

    public PlayerData GetPlayer(int id)
    {
        return _db.Table<PlayerData>().FirstOrDefault(p => p.id == id);
    }

    public List<PlayerData> GetPlayersByTeam(int teamId)
    {
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id == teamId)
                  .OrderByDescending(p => p.overall)
                  .ToList();
    }

    /// <summary>Plazas estándar ocupadas: excluye IR y contratos two-way.</summary>
    public int GetRosterCount(int teamId)
    {
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id == teamId && p.is_on_ir == 0 && p.is_two_way == 0)
                  .Count();
    }

    /// <summary>Contratos two-way asignados; los lesionados conservan su plaza two-way.</summary>
    public int GetTwoWayCount(int teamId)
    {
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id == teamId && p.is_two_way == 1)
                  .Count();
    }

    /// <summary>Jugadores activos fuera de IR, sumando plazas estándar y two-way.</summary>
    public int GetTotalRosterCount(int teamId)
    {
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id == teamId && p.is_on_ir == 0)
                  .Count();
    }

    /// <summary>Activa/desactiva la reserva de lesionados (IR) del jugador.</summary>
    public void SetOnIR(PlayerData player, bool onIR)
    {
        if (player == null) return;
        player.is_on_ir = onIR ? 1 : 0;
        UpdatePlayer(player);
    }

    // ── PARTE MÉDICO (E2) ─────────────────────────────────

    /// <summary>
    /// Cierra el expediente de una lesión y lo guarda en el historial médico del jugador.
    /// `outcome`: recuperado | alta_forzada | recaida.
    /// </summary>
    public void RecordInjuryHistory(PlayerData p, string outcome, int days,
                                    string injuryType = null, string severity = null,
                                    int seasonId = 0, int gameDay = 0, string date = null)
    {
        if (p == null || !EnsureDb()) return;
        string diag = injuryType ?? p.injury_type ?? "";
        var def = InjuryCatalog.ByName(diag);
        string sevLabel = severity ?? InjuryCatalog.SeverityLabel(def != null ? def.severity : InjuryCatalog.SeverityOf(days));

        _db.Insert(new InjuryHistoryData
        {
            player_id = p.id,
            team_id = p.team_id,
            injury_type = diag,
            severity = sevLabel,
            days = days,
            season_id = seasonId,
            game_day = gameDay,
            date = string.IsNullOrEmpty(date) ? GetCurrentDateString(0) : date,
            outcome = outcome,
            treated = p.treated
        });
    }

    /// <summary>
    /// Alta forzada (E2): devuelve al jugador antes de tiempo. Cierra el expediente en el
    /// historial como "alta_forzada" y le deja un riesgo de recaída proporcional a los días
    /// que le faltaban. Si estaba en la reserva de lesionados, sale de ella.
    /// </summary>
    public void ForceReturnFromInjury(PlayerData p, int seasonId = 0, int gameDay = 0, string date = null)
    {
        if (p == null || p.injury_days <= 0) return;

        // Regla: solo se puede forzar entre 2 y 7 días restantes (ver InjuryHelper).
        if (!InjuryHelper.CanForceReturn(p.injury_days)) return;

        var def = InjuryCatalog.ByName(p.injury_type);
        var sev = def != null
            ? def.severity
            : InjuryCatalog.SeverityOf(p.injury_total_days > 0 ? p.injury_total_days : p.injury_days);
        int episodeDays = p.injury_total_days > 0 ? p.injury_total_days : p.injury_days;

        RecordInjuryHistory(p, "alta_forzada", episodeDays, p.injury_type,
                            InjuryCatalog.SeverityLabel(sev), seasonId, gameDay, date);

        p.recovery_risk = InjuryHelper.ForceReturnRisk(p.injury_days, sev);
        p.injury_days = 0;
        p.injury_total_days = 0;
        p.treated = 0;
        if (p.is_on_ir == 1) p.is_on_ir = 0;
        UpdatePlayer(p);
    }

    /// <summary>Historial médico de un jugador, de la lesión más reciente a la más antigua.</summary>
    public List<InjuryHistoryData> GetInjuryHistory(int playerId, int limit = 6)
    {
        if (!EnsureDb()) return new List<InjuryHistoryData>();
        return _db.Table<InjuryHistoryData>()
                  .Where(h => h.player_id == playerId)
                  .OrderByDescending(h => h.id)
                  .Take(limit)
                  .ToList();
    }

    /// <summary>Nº de lesiones registradas por jugador (para el parte médico del perfil).</summary>
    public Dictionary<int, int> GetInjuryCounts()
    {
        if (!EnsureDb()) return new Dictionary<int, int>();
        return _db.Query<InjuryHistoryData>("SELECT player_id FROM injury_history")
                  .GroupBy(h => h.player_id)
                  .ToDictionary(g => g.Key, g => g.Count());
    }

    public List<PlayerData> GetAllPlayers()
    {
        if (!EnsureDb()) return new List<PlayerData>();
        return _db.Table<PlayerData>()
                  .OrderByDescending(p => p.overall)
                  .ToList();
    }

    public Dictionary<int, long> GetTeamPayrolls()
    {
        if (!EnsureDb()) return new Dictionary<int, long>();
        return _db.Query<PlayerData>("SELECT team_id, salary FROM players WHERE team_id > 0")
                  .GroupBy(p => p.team_id)
                  .ToDictionary(g => g.Key, g => g.Sum(p => p.salary));
    }

    /// <summary>
    /// Asigna al jugador un dorsal del equipo: conserva el actual si está libre y no
    /// retirado; en caso contrario asigna el menor dorsal libre (1-99), evitando los
    /// retirados y los ya ocupados. Si no encuentra hueco usa 0.
    /// </summary>
    public void AssignJerseyNumber(PlayerData player, int teamId)
    {
        if (player == null) return;
        var used = _db.Table<PlayerData>()
                      .Where(p => p.team_id == teamId && p.id != player.id)
                      .ToList()
                      .Select(p => p.number)
                      .ToHashSet();
        foreach (var r in _db.Table<RetiredNumberData>()
                             .Where(r => r.team_id == teamId)
                             .ToList())
            used.Add(r.number);

        if (player.number > 0 && !used.Contains(player.number))
            return;

        for (int n = 1; n <= 99; n++)
        {
            if (used.Contains(n)) continue;
            player.number = n;
            return;
        }
        player.number = 0;
    }

    public List<RetiredNumberData> GetRetiredNumbers(int teamId)
    {
        if (!EnsureDb()) return new List<RetiredNumberData>();
        var all = _db.Table<RetiredNumberData>()
                     .Where(r => r.team_id == teamId)
                     .OrderBy(r => r.number)
                     .ToList();

        var activeIds = _db.Table<PlayerData>()
                           .Select(p => p.id)
                           .ToHashSet();

        return all.Where(r => r.player_id == 0 || !activeIds.Contains(r.player_id))
                  .ToList();
    }

    public List<PlayerData> GetRetiringPlayers()
    {
        var withTeam = _db.Table<PlayerData>()
            .Where(p => p.team_id != 0 && p.age >= 40)
            .ToList();
        var freeAgents = _db.Table<PlayerData>()
            .Where(p => p.team_id == 0 && p.age >= 40)
            .ToList();
        var result = new List<PlayerData>();
        result.AddRange(withTeam);
        result.AddRange(freeAgents);
        result.Sort((a, b) => b.age.CompareTo(a.age));
        return result;
    }

    public List<PlayerData> GetExpiringPlayers()
    {
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id != 0 && p.contract_years == 1 && p.age < 35)
                  .OrderByDescending(p => p.salary)
                  .ToList();
    }

    public void UpdatePlayer(PlayerData player)
    {
        if (!EnsureDb()) return;
        _db.Update(player);
    }

    /// <summary>
    /// F2-09: actualiza en lote los jugadores que cambiaron tras un partido (físico,
    /// lesión) en una sola transacción. Si el llamador ya tiene una abierta (la del
    /// día) escribe dentro de ella; nunca anida un <c>BEGIN</c>.
    /// </summary>
    public void UpdatePlayersBatch(IList<PlayerData> players)
    {
        if (!EnsureDb() || players == null || players.Count == 0) return;

        bool own = !_db.IsInTransaction;
        if (own) _db.BeginTransaction();
        try
        {
            _db.UpdateAll(players, runInTransaction: false);
            if (own) _db.Commit();
        }
        catch
        {
            if (own) _db.Rollback();
            throw;
        }
    }

    public List<PlayerData> GetTopPlayersByStat(int managerId, string stat, int count = 1)
    {
        if (!EnsureDb()) return new List<PlayerData>();
        var season = GetActiveSeason(managerId);
        if (season == null) return new List<PlayerData>();

        string col = stat switch
        {
            "rebounds" => "rebounds",
            "assists" => "assists",
            "steals" => "steals",
            "blocks" => "blocks",
            "minutes" => "minutes",
            "rating" => "rating",
            _ => "points",
        };

        var rows = _db.Query<PlayerStatTotalRow>(
            $@"SELECT ps.player_id, SUM(ps.{col}) AS total
               FROM player_game_stats ps
               JOIN games g ON ps.game_id = g.id
               WHERE g.manager_id = ?
                 AND g.is_played = 1
                 AND g.game_type = 'regular'
               GROUP BY ps.player_id
               ORDER BY total DESC
               LIMIT ?",
            managerId, count);

        var result = new List<PlayerData>();
        foreach (var r in rows)
        {
            var player = _db.Table<PlayerData>().Where(p => p.id == r.player_id).FirstOrDefault();
            if (player != null) result.Add(player);
        }
        return result;
    }

    // ── G-LEAGUE ─────────────────────────────────────────

    /// <summary>Asigna o recupera a un jugador de la G-League. Al asignar,
    /// lo saca de la convocatoria activa (slot 0/1) moviéndolo a inactivos.</summary>
    public void SetGLeagueAssignment(PlayerData player, bool assigned)
    {
        if (player == null) return;
        player.g_league_assigned = assigned ? 1 : 0;
        UpdatePlayer(player);

        if (assigned)
        {
            var cur = GetPlayerLineupSlot(player.id);
            if (cur != null && cur.slot <= 1)
            {
                int nextInact = GetInactive(player.team_id)
                    .Select(l => l.slot_index).DefaultIfEmpty(-1).Max() + 1;
                SetPlayerSlot(player.id, player.team_id, 2, nextInact);
            }
        }
    }

    public GLeagueSeasonStat GetGLeagueStats(int playerId, int seasonId)
    {
        return _db.Table<GLeagueSeasonStat>()
                  .Where(s => s.player_id == playerId && s.season_id == seasonId)
                  .FirstOrDefault();
    }

    public List<GLeagueSeasonStat> GetTeamGLeagueStats(int teamId, int seasonId)
    {
        var playerIds = GetPlayersByTeam(teamId).Select(p => p.id).ToList();
        return _db.Table<GLeagueSeasonStat>()
                  .Where(s => s.season_id == seasonId && playerIds.Contains(s.player_id))
                  .ToList();
    }

}
