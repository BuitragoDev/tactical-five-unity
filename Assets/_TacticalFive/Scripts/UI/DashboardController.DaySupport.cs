using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

// B47: Soporte del día: fechas, lesiones, scouting, entrenamiento y estrategia IA.
public partial class DashboardController
{
    /// <summary>Índice de día de partido de una fecha (regla en <see cref="CalendarHelper"/>).</summary>
    static int DateToGameDay(SeasonData season, System.DateTime date)
    {
        return CalendarHelper.DateToGameDay(season.year_start, date);
    }

    /// <summary>
    /// Red de seguridad contra soft-locks de temporada. Se llama cuando no quedan
    /// partidos pendientes pero la fase no es terminal (calendario parcial, datos
    /// heredados de un guardado fallido, play-in sin seeds...): regenera el
    /// calendario si falta o avanza/cierra la fase para que la partida no se quede
    /// bloqueada para siempre.
    /// </summary>
    void TryEscapeStuckPhase()
    {
        if (_season == null || _manager == null) return;

        int Count(string type) => DatabaseManager.Instance.Db.Table<GameData>()
            .Count(g => g.manager_id == _manager.id && g.game_type == type);
        bool NoPending(string type) => !DatabaseManager.Instance.Db.Table<GameData>()
            .Any(g => g.manager_id == _manager.id && g.game_type == type && g.is_played == 0);

        // 1. Temporada regular sin calendario (fallo de guardado heredado): regenerar.
        if ((_season.phase == "preseason" || _season.phase == "regular") && Count("regular") == 0)
        {
            int count = ScheduleGenerator.GenerateSchedule(_season, DatabaseManager.Instance.GetAllTeams());
            if (count > 0)
            {
                try
                {
                    var glTeams = DatabaseManager.Instance.GetGLeagueTeams();
                    if (glTeams.Count > 0)
                        GLeagueScheduleGenerator.GenerateSchedule(_season, glTeams);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Dashboard] Error regenerando calendario G-League: {ex.Message}");
                }

                DatabaseManager.Instance.EnsureCupEdition(_season, DatabaseManager.Instance.GetAllTeams());
                _season.generated = 1;
                _season.phase = "regular";
                DatabaseManager.Instance.UpdateSeason(_season);
                Debug.LogWarning($"[Dashboard] Temporada sin calendario: regenerados {count} partidos de liga regular.");
            }
            else
            {
                Debug.LogError("[Dashboard] No se pudo regenerar el calendario de la temporada.");
            }
            return;
        }

        // 2. Regular completa pero sin transición hecha.
        if ((_season.phase == "preseason" || _season.phase == "regular") && NoPending("regular"))
        {
            PlayoffsGenerator.GeneratePlayIn(_season, _manager.id);
            _season.phase = "playin";
            DatabaseManager.Instance.UpdateSeason(_season);
            Debug.LogWarning("[Dashboard] Regular sin partidos pendientes: Play-In generado y fase avanzada.");
            return;
        }

        // 3. Play-In completo: generar playoffs o cerrar la temporada si no hay seeds.
        if (_season.phase == "playin" && NoPending("playin"))
        {
            int created = PlayoffsGenerator.GeneratePlayoffs(_season, _manager.id);
            if (created > 0)
            {
                _season.phase = "playoff";
                DatabaseManager.Instance.UpdateSeason(_season);
                Debug.LogWarning($"[Dashboard] Play-In sin partidos pendientes: playoffs generados ({created}).");
            }
            else
            {
                // Sin seeds suficientes no se puede construir el bracket: cerrar la
                // temporada es preferible a dejarla bloqueada para siempre.
                _season.phase = "finished";
                DatabaseManager.Instance.UpdateSeason(_season);
                Debug.LogError("[Dashboard] Play-In sin partidos y sin seeds suficientes: temporada cerrada como 'finished'.");
            }
            return;
        }

        // 4. Playoffs completos.
        if (_season.phase == "playoff" && NoPending("playoff"))
        {
            _season.phase = "finished";
            DatabaseManager.Instance.UpdateSeason(_season);
            Debug.LogWarning("[Dashboard] Playoffs sin partidos pendientes: temporada cerrada.");
        }
    }

    /// <summary>
    /// Día de partido pendiente: el que se simula al avanzar. 0 = no quedan partidos.
    /// Fuente única de verdad: la usa el botón de acción del Dashboard ("DÍA DE PARTIDO")
    /// y la pantalla de previa para saber si el partido que muestra es el de hoy.
    /// </summary>
    int FindNextGameDay()
    {
        return FindNextGameDay(_season, _manager.id);
    }

    /// <summary>
    /// Versión estática de <see cref="FindNextGameDay()"/> para poder consultarla desde
    /// otras pantallas (p. ej. PreGame) sin depender de la instancia del Dashboard.
    /// </summary>
    public static int FindNextGameDay(SeasonData season, int managerId)
    {
        if (season == null) return 0;

        bool anyUnplayed = DatabaseManager.Instance.Db.Table<GameData>()
            .Any(g => g.manager_id == managerId && g.is_played == 0);
        if (!anyUnplayed) return 0;

        // New system: use current_date for day-by-day advancement
        if (!string.IsNullOrEmpty(season.current_date))
        {
            var currentDate = System.DateTime.Parse(season.current_date);

            // Only skip the Feb break if there are no unplayed games on that date
            // (the All-Star game is scheduled during the break, Feb 13)
            bool hasUnplayedToday = DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == managerId
                       && g.game_date == season.current_date
                       && g.is_played == 0);

            if (!hasUnplayedToday && currentDate.Month == 2 && currentDate.Day >= 8 && currentDate.Day <= 14)
                currentDate = new System.DateTime(currentDate.Year, 2, 15);

            return DateToGameDay(season, currentDate);
        }

        // Fallback for old saves without current_date
        if (season.current_game_day == 0)
        {
            var nextPreseason = DatabaseManager.Instance.Db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                         && g.is_played == 0
                         && g.game_day < 0)
                .OrderByDescending(g => g.game_day)
                .FirstOrDefault();
            if (nextPreseason != null) return nextPreseason.game_day;
            return 1;
        }

        return season.current_game_day + 1;
    }

    List<PlayerData> ProcessInjuries()
    {
        var allPlayers = DatabaseManager.Instance.GetAllTeams().SelectMany(t =>
            DatabaseManager.Instance.GetPlayersByTeam(t.id)).ToList();
        var recovered = new List<PlayerData>();
        foreach (var p in allPlayers)
        {
            if (p.injury_days > 0)
            {
                p.injury_days--;
                if (p.injury_days <= 0)
                {
                    p.injury_days = 0;
                    p.injury_type = "";
                    p.treated = 0;
                    if (p.team_id == _myTeam.id)
                        recovered.Add(p);
                }
                DatabaseManager.Instance.UpdatePlayer(p);
            }
        }
        return recovered;
    }

    void ProcessFisicoRecovery()
    {
        if (_myTeam == null) return;
        var allPlayers = DatabaseManager.Instance.GetAllTeams().SelectMany(t =>
            DatabaseManager.Instance.GetPlayersByTeam(t.id)).ToList();
        foreach (var p in allPlayers)
        {
            if (p.injury_days > 0) continue;
            p.fisico = Mathf.Min(99, p.fisico + 8);
            DatabaseManager.Instance.UpdatePlayer(p);
        }
    }

    void ProcessScouts()
    {
        if (_season == null) return;
        var scouts = DatabaseManager.Instance.GetScoutsByTeam(_myTeam.id)
            .Where(s => s.completed == 0 && _season.current_game_day >= s.end_day)
            .ToList();
        foreach (var s in scouts)
        {
            s.completed = 1;
            DatabaseManager.Instance.UpdateScout(s);
            DatabaseManager.Instance.MarkPlayerScouted(_myTeam.id, s.player_id, s.end_day);
        }
    }

    void ProcessTraining()
    {
        if (_season == null) return;
        var trainings = DatabaseManager.Instance.GetTeamTraining(_myTeam.id)
            .Where(t => _season.current_game_day >= t.start_day + t.duration)
            .ToList();
        foreach (var t in trainings)
        {
            DatabaseManager.Instance.CompleteTrainingAndApply(t);
            var player = DatabaseManager.Instance.GetPlayerById(t.player_id);
            string attrName = t.attribute.Replace("_", " ").ToUpper();
            if (player != null)
            {
                DatabaseManager.Instance.AddMessage(new MessageData
                {
                    manager_id = _manager.id,
                    sender_type = 1,
                    sender_id = 0,
                    title = "Entrenamiento completado",
                    body = NewsTexts.Entrenamiento($"{player.first_name} {player.last_name}", attrName, 0, 0),
                    game_day = _season.current_game_day,
                    game_date = _season.current_date,
                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    is_read = 0,
                    category = NewsCategory.CLUB,
                    news_type = NewsType.ENTRENAMIENTO
                });
            }
        }
    }

    void ProcessRenovations()
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        foreach (var team in allTeams)
        {
            if (team.arena_renovation_end_day > 0 && _season.current_game_day >= team.arena_renovation_end_day)
            {
                ApplyRenovation(team);
            }
        }
    }

    void ProcessGLeagueDevelopment()
    {
        if (_season == null || _season.id <= 0) return;
        var assigned = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id)
            .Where(p => p.g_league_assigned == 1)
            .ToList();
        if (assigned.Count == 0) return;

        // Tick semanal de desarrollo: +1 atributo hasta el potencial. Las
        // estadísticas ya no son procedurales: se acumulan por partido real
        // simulado en ProcessGLeagueGame.
        foreach (var p in assigned)
        {
            if (p.injury_days > 0 || p.is_on_ir == 1) continue;

            if (GLeagueHelper.ProcessDevelopmentTick(p))
                DatabaseManager.Instance.UpdatePlayer(p);
        }
    }

    // ═══════════════════════════════════════════
    //  AI TRANSFERS
    // ═══════════════════════════════════════════


    bool IsDeadlineWeek()
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return false;
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return false;
        return date.Month == 2 && date.Day >= 1 && date.Day <= 8 && date.Year == _season.year_end;
    }

    bool IsTeamContender(TeamData team)
    {
        if (_allGames == null || _allGames.Count == 0) return false;
        var confTeams = _allTeams?.Where(t => t.conference == team.conference).ToList();
        if (confTeams == null || confTeams.Count == 0) return false;
        var standings = BuildStandings(confTeams);
        var top4 = standings.Take(4).ToList();
        return top4.Any(s => s.teamId == team.id);
    }

    // Clasifica la estrategia de un equipo a partir de su clasificación y plantilla.
    TeamStrategy GetTeamStrategy(TeamData team, List<StandingRow> confStandings, List<PlayerData> roster)
    {
        var row = confStandings?.FirstOrDefault(s => s.teamId == team.id);
        return TeamStrategyRules.Classify(row?.rank ?? 0, confStandings?.Count ?? 0, roster);
    }

    // Precalcula la estrategia de todos los equipos una vez por ciclo de traspaso.
    void BuildTeamStrategyCache()
    {
        _teamStrategyCache.Clear();
        if (_allTeams == null || _allTeams.Count == 0) return;

        var eastTeams = _allTeams.Where(t => t.conference == "East").ToList();
        var westTeams = _allTeams.Where(t => t.conference == "West").ToList();
        var eastStandings = BuildStandings(eastTeams);
        var westStandings = BuildStandings(westTeams);

        foreach (var team in _allTeams)
        {
            var standings = team.conference == "East" ? eastStandings : westStandings;
            var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            _teamStrategyCache[team.id] = GetTeamStrategy(team, standings, roster);
        }
    }

    TeamStrategy GetTeamStrategy(int teamId)
    {
        return _teamStrategyCache.TryGetValue(teamId, out var s) ? s : TeamStrategy.Balanced;
    }

    /// <summary>Plan de partido de un equipo: el del usuario es persistente; el de
    /// la IA se deriva de su estrategia de GM.</summary>
    GamePlan PlanFor(int teamId)
    {
        if (_myTeam != null && teamId == _myTeam.id)
        {
            var userPlan = new GamePlan { pace = _myTeam.plan_pace, offense = _myTeam.plan_offense, defense = _myTeam.plan_defense };
            var assistant = GetMyAssistant();
            return CoachScheme.Blend(userPlan, assistant);
        }
        return GamePlan.FromStrategy(GetTeamStrategy(teamId));
    }

    /// <summary>Asistente del equipo del usuario (aporta esquema y bonus).</summary>
    EmployeeData GetMyAssistant()
    {
        if (_myTeam == null) return null;
        return DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id)
            .FirstOrDefault(e => e.position == CoachScheme.POSITION && e.scheme_set == 1);
    }

    /// <summary>Bonus de rating del cuerpo técnico del equipo indicado.</summary>
    int CoachBonusFor(int teamId)
    {
        if (_myTeam == null || teamId != _myTeam.id) return 0;
        var assistant = GetMyAssistant();
        return assistant != null ? CoachScheme.RatingBonus(assistant.reputation) : 0;
    }

    void ProcessAIMarket(int gameDay)
    {
        // Se ejecuta en hilo de fondo; _db (ambient) apunta a la conexión de fondo.
        // El main thread espera la finalización, así que leer/mutar estado de instancia es seguro.
        var db = DatabaseManager.Instance.Db;
        db.BeginTransaction();
        try
        {
            // F8-02: el mercado IA vive en AiMarketService. El controller solo
            // inyecta estrategia/reloj y mantiene la transacción y el psicólogo.
            var market = new AiMarketService(
                _manager, _myTeam, _season, _aiRng, _teamTradeCooldown,
                id => GetTeamStrategy(id),
                BuildTeamStrategyCache,
                IsTeamContender,
                IsDeadlineWeek);

            market.ProcessBuyoutMarket(gameDay);
            market.ProcessOwnFreeAgentResignings(gameDay);
            market.ProcessAITransfers(gameDay);
            market.ProcessStarFreeAgentSignings(gameDay);

            if (!_airRenewalsProcessed && market.ShouldProcessRenewals())
            {
                market.ProcessAIRenewals();
                _airRenewalsProcessed = true;
            }

            ProcessPsychologistMorale();
            db.Commit();
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    HashSet<int> GetPendingSATIds()
        => AiMarketService.GetPendingSATIds(_manager, _myTeam);

    void ProcessPsychologistMorale()
    {
        var staff = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        var psicologo = staff.FirstOrDefault(e => e.position == "PSICOLOGO");

        int recovery = psicologo?.reputation switch
        {
            5 => 2,
            4 => 1,
            3 => 1,
            _ => 0
        };

        if (recovery <= 0) return;

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        foreach (var p in players)
        {
            if (p.morale >= 50) continue;
            int newMorale = Mathf.Min(50, p.morale + recovery);
            DatabaseManager.Instance.UpdatePlayerMorale(p.id, newMorale);
        }
    }

}
