using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

// B47: Día de partido: pipeline F8-03 (pre-lote, simulación, bookkeeping y transiciones).
public partial class DashboardController
{
    System.Collections.IEnumerator ProcessGameDayRoutine(bool fastSim = false, bool skipPreBatch = false, bool navigateToPreGame = false)
    {
        int gameDay = FindNextGameDay();
        Debug.Log($"[GameDay] ProcessGameDayRoutine started. gameDay={gameDay}");
        if (gameDay == 0)
        {
            Debug.Log("[Dashboard] No hay más partidos programados.");
            TryEscapeStuckPhase();
            if (!fastSim) HideLoading();
            yield break;
        }

        // En una simulación rápida reanudada tras el modal de alineación, el
        // pre-lote del día ya se commiteó antes del aborto; se evita re-ejecutarlo.
        bool effectiveSkipPreBatch = skipPreBatch || (fastSim && _fastSimSkipPreBatch);
        _fastSimSkipPreBatch = false;
        _dayAborted = false;

        var state = new GameDayRunState
        {
            gameDay = gameDay,
            fastSim = fastSim,
            effectiveSkipPreBatch = effectiveSkipPreBatch,
            navigateToPreGame = navigateToPreGame
        };

        var steps = new List<GameDayPipeline.Step>
        {
            new GameDayPipeline.Step(GameDayStep.PreBatch, () => RunPreBatch(state)),
            new GameDayPipeline.Step(GameDayStep.LineupValidation, () => RunLineupValidation(state)),
            new GameDayPipeline.Step(GameDayStep.PreGameGate, () => RunPreGameGate(state)),
            new GameDayPipeline.Step(GameDayStep.SimulationBookkeeping, () => RunSimulationBookkeeping(state)),
            new GameDayPipeline.Step(GameDayStep.PostDay, () => RunPostDay(state))
        };

        yield return GameDayPipeline.Run(steps,
            step => Debug.Log($"[GameDay] step={step} gameDay={state.gameDay}"),
            () => _dayAborted);
    }

    IEnumerator RunPreBatch(GameDayRunState st)
    {
        var db = DatabaseManager.Instance.Db;
        if (!st.effectiveSkipPreBatch)
        {
            // F7-05: bonus de recuperación del centro de rendimiento de la ciudad.
            int cityRecovery = 0;
            if (_manager != null && _season != null && _myTeam != null)
                cityRecovery = CityCatalog.RecoveryBonus(
                    DatabaseManager.Instance.GetCityProjectLevel(
                        _manager.id, _season.id, CityCatalog.ProjectPerformance));

            // Paso 1: lesiones y recuperaciones (background)
            var recoveryTask = DatabaseManager.Instance.RunInBackground(conn =>
            {
                conn.BeginTransaction();
                var recovered = new List<(int id, string first, string last)>();
                var recoveredFromIR = new List<int>();
                var rng = new System.Random();

                var teamsPlayToday = new HashSet<int>();
                var todayVenueByTeam = new Dictionary<int, int>();
                if (st.gameDay > 0)
                {
                    foreach (var g in conn.Table<GameData>().Where(g => g.game_day == st.gameDay).ToList())
                    {
                        teamsPlayToday.Add(g.home_team_id);
                        teamsPlayToday.Add(g.away_team_id);
                        todayVenueByTeam[g.home_team_id] = g.home_team_id;
                        todayVenueByTeam[g.away_team_id] = g.home_team_id;
                    }
                }

                // E2: reputación del jefe de servicios médicos por equipo (acelera la recuperación).
                var medicoByTeam = new Dictionary<int, int>();
                foreach (var e in conn.Table<EmployeeData>().Where(e => e.position == "MEDICO").ToList())
                    medicoByTeam[e.team_id] = e.reputation;

                string today = _season?.current_date ?? "";
                int seasonId = _season?.id ?? 0;

                var allTeams = conn.Table<TeamData>().ToList();
                var teamsById = allTeams.ToDictionary(t => t.id);

                // F5-03: viaje/descanso desde el último partido jugado. El desgaste
                // se aplica antes de simular el partido de hoy.
                var lastGameDayByTeam = new Dictionary<int, int>();
                var lastVenueByTeam = new Dictionary<int, int>();
                if (st.gameDay > 0)
                {
                    var previousGames = conn.Table<GameData>()
                        .Where(g => g.season_id == seasonId
                                 && g.game_day < st.gameDay
                                 && g.is_played == 1
                                 && (g.game_type == "regular" || g.game_type == "preseason"))
                        .ToList();
                    foreach (var g in previousGames)
                    {
                        UpdateLastVenue(g.home_team_id, g.game_day, g.home_team_id);
                        UpdateLastVenue(g.away_team_id, g.game_day, g.home_team_id);
                    }
                }

                void UpdateLastVenue(int teamId, int day, int venueId)
                {
                    if (!lastGameDayByTeam.TryGetValue(teamId, out int previous) || day >= previous)
                    {
                        lastGameDayByTeam[teamId] = day;
                        lastVenueByTeam[teamId] = venueId;
                    }
                }

                var travelFatigueByTeam = new Dictionary<int, int>();
                foreach (var team in allTeams)
                {
                    if (!teamsPlayToday.Contains(team.id)) continue;
                    if (!todayVenueByTeam.TryGetValue(team.id, out int venue)) continue;
                    if (!lastGameDayByTeam.TryGetValue(team.id, out int previousDay)) continue;
                    if (!lastVenueByTeam.TryGetValue(team.id, out int previousVenue)) continue;

                    int restDays = TravelHelper.RestDays(previousDay, st.gameDay);
                    int kilometers = TravelHelper.DistanceKilometers(previousVenue, venue, teamsById);
                    int cost = TravelHelper.FatigueCost(kilometers, restDays);
                    if (cost > 0) travelFatigueByTeam[team.id] = cost;
                }
                foreach (var team in allTeams)
                {
                    var players = conn.Table<PlayerData>().Where(p => p.team_id == team.id).ToList();
                    int medicoRep = medicoByTeam.TryGetValue(team.id, out var rep) ? rep : 0;
                    foreach (var p in players)
                    {
                        if (p.injury_days > 0)
                        {
                            var def = InjuryCatalog.ByName(p.injury_type);
                            var sev = def != null
                                ? def.severity
                                : InjuryCatalog.SeverityOf(p.injury_total_days > 0 ? p.injury_total_days : p.injury_days);

                            p.injury_days = InjuryHelper.EvolveDays(
                                p.injury_days, p.injury_total_days, sev, medicoRep, p.fisico, rng.NextDouble());

                            if (p.injury_days <= 0)
                            {
                                int episodeDays = p.injury_total_days > 0 ? p.injury_total_days : 1;

                                // Parte médico: se cierra el expediente en el historial.
                                conn.Insert(new InjuryHistoryData
                                {
                                    player_id = p.id,
                                    team_id = p.team_id,
                                    injury_type = p.injury_type,
                                    severity = InjuryCatalog.SeverityLabel(sev),
                                    days = episodeDays,
                                    season_id = seasonId,
                                    game_day = st.gameDay,
                                    date = today,
                                    outcome = "recuperado",
                                    treated = p.treated
                                });

                                p.injury_days = 0;
                                p.injury_total_days = 0;
                                p.recovery_risk = 0;
                                p.injury_type = "";
                                p.treated = 0;
                                if (p.is_on_ir == 1)
                                {
                                    p.is_on_ir = 0;
                                    if (p.team_id == _myTeam.id)
                                        recoveredFromIR.Add(p.id);
                                }
                                if (p.team_id == _myTeam.id)
                                    recovered.Add((p.id, p.first_name, p.last_name));
                            }
                            conn.Update(p);
                        }
                        else if (p.recovery_risk > 0)
                        {
                            // Alta forzada: descansar cura el riesgo; jugar lo mantiene vivo.
                            p.recovery_risk -= InjuryHelper.RiskDecayForDay(teamsPlayToday.Contains(team.id));
                            if (p.recovery_risk <= 0)
                            {
                                p.recovery_risk = 0;
                                p.injury_type = "";
                            }
                            conn.Update(p);
                        }
                        if (p.injury_days <= 0 && !teamsPlayToday.Contains(team.id))
                        {
                            // +10 en día de descanso: compensa el desgaste de la
                            // temporada sin que los titulares acaben hundidos (antes
                            // +8 dejaba un drenaje neto que hacía caer el acierto).
                            // F7-05: el centro de rendimiento añade 2 por nivel.
                            int restRecovery = 10 + (p.team_id == _myTeam.id ? cityRecovery : 0);
                            p.fisico = Mathf.Min(99, p.fisico + restRecovery);
                            conn.Update(p);
                        }
                        else if (p.injury_days <= 0
                                 && travelFatigueByTeam.TryGetValue(team.id, out int travelCost)
                                 && travelCost > 0)
                        {
                            // F5-03: el viaje largo resta físico antes de jugar; el
                            // desgaste por minutos lo aplica después GameSimulator.
                            p.fisico = Mathf.Max(0, p.fisico - travelCost);
                            conn.Update(p);
                        }
                    }
                }

                // Equipos IA: si recuperar del IR deja la plantilla por encima del
                // límite, liberan al peor jugador para no quedarse sobre el tope.
                foreach (var team in allTeams)
                {
                    if (team.id == _myTeam.id) continue;
                    var roster = conn.Table<PlayerData>().Where(p => p.team_id == team.id && p.is_on_ir == 0).ToList();
                    var standard = roster.Where(p => p.is_two_way == 0)
                        .OrderBy(p => p.GetCalculatedAverage()).ToList();
                    // SQLite-net solo puede traducir OrderBy por columnas. GetCalculatedAverage()
                    // es un método C#: primero materializamos y luego ordenamos en memoria.
                    var twoWay = conn.Table<PlayerData>().Where(p => p.team_id == team.id && p.is_two_way == 1)
                        .ToList()
                        .OrderBy(p => p.GetCalculatedAverage()).ToList();
                    while (standard.Count > TradeHelper.MAX_STANDARD_ROSTER || twoWay.Count > TradeHelper.MAX_TWO_WAY)
                    {
                        var worst = standard.Count > TradeHelper.MAX_STANDARD_ROSTER
                            ? standard[0]
                            : twoWay[0];
                        if (worst.is_two_way == 1) twoWay.RemoveAt(0);
                        else standard.RemoveAt(0);
                        worst.team_id = 0;
                        worst.contract_years = 0;
                        worst.guaranteed_years = 0;
                        worst.has_team_option = 0;
                        worst.has_player_option = 0;
                        worst.is_two_way = 0;
                        worst.is_on_ir = 0;
                        worst.g_league_assigned = 0;
                        conn.Update(worst);
                    }
                }

                conn.Commit();
                return (recovered, recoveredFromIR);
            });

            while (!recoveryTask.IsCompleted)
                yield return null;

            try
            {
                var recoveryResult = recoveryTask.Result;
                var recoveredPlayers = recoveryResult.recovered;
                var recoveredFromIR = recoveryResult.recoveredFromIR;

                if (recoveredPlayers.Count > 0)
                {
                    foreach (var (id, first, last) in recoveredPlayers)
                    {
                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = $"Recuperado: {first} {last}",
                            body = NewsTexts.Recuperacion($"{first} {last}", "lesión muscular", 0),
                            game_day = st.gameDay,
                            game_date = _season?.current_date ?? "",
                            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            is_read = 0,
                            category = NewsCategory.LESIONES,
                            news_type = NewsType.RECUPERACION
                        });
                        _pendingRecoveredIds.Add(id);
                    }
                }

                // Si un jugador del usuario sale del IR y la plantilla queda llena,
                // se muestra un modal para que decida a quién liberar.
                _pendingIRReleaseIds.Clear();
                int standardExcess = Mathf.Max(0,
                    DatabaseManager.Instance.GetRosterCount(_myTeam.id) - TradeHelper.MAX_STANDARD_ROSTER);
                var recoveredStandardIds = recoveredFromIR
                    .Where(id => DatabaseManager.Instance.GetPlayerById(id)?.is_two_way != 1)
                    .Take(standardExcess)
                    .ToList();
                _pendingIRReleaseIds.AddRange(recoveredStandardIds);
                _pendingIRReleaseCount = recoveredStandardIds.Count;
            }
            catch (System.Exception ex)
            {
                _pendingRecoveredIds.Clear();
                HandleDayError("Error en el lote pre-partido", "ERROR AL PROCESAR EL DÍA", "Ha ocurrido un error al procesar el día.", ex, st.fastSim);
                _dayAborted = true;
            yield break;
            }
            if (st.fastSim) yield return null;

            // Paso 2: scouts, entrenamiento y renovaciones
        // B38: la fase de simulación + bookkeeping es síncrona y se ejecuta dentro de una
        // única transacción en un solo frame (sin yields entre BeginTransaction y Commit).
        // El respiro de la UI en simulación rápida se da AQUÍ, antes de abrirla.
        if (st.fastSim) yield return null;

        db.BeginTransaction();
            try
            {
                ProcessScouts();
                ProcessTraining();
                ProcessRenovations();
                if (_season != null && _season.current_game_day > 0 && _season.current_game_day % 7 == 0)
                    ProcessGLeagueDevelopment();
                db.Commit();
            }
            catch (System.Exception ex)
            {
                db.Rollback();
                _pendingRecoveredIds.Clear();
                HandleDayError("Error en el lote pre-partido", "ERROR AL PROCESAR EL DÍA", "Ha ocurrido un error al procesar el día.", ex, st.fastSim);
                _dayAborted = true;
            yield break;
            }
            if (st.fastSim) yield return null;

            // Paso 3: traspasos AI, fichajes estrella y psicólogo (hilo de fondo)
            var aiTask = DatabaseManager.Instance.RunInBackgroundAsync(() =>
                ProcessAIMarket(st.gameDay));

            while (!aiTask.IsCompleted)
                yield return null;

            try
            {
                aiTask.GetAwaiter().GetResult();
            }
            catch (System.Exception ex)
            {
                _pendingRecoveredIds.Clear();
                HandleDayError("Error en el lote pre-partido", "ERROR AL PROCESAR EL DÍA", "Ha ocurrido un error al procesar el día.", ex, st.fastSim);
                _dayAborted = true;
            yield break;
            }
        }

        // El pre-lote de este día queda commiteado: si más adelante se vuelve a simular
        // el MISMO día (pantalla de previa → IR AL PARTIDO) no debe repetirse.
        if (!st.effectiveSkipPreBatch)
        {
            _preBatchGameDay = st.gameDay;
            _preBatchSeasonId = _season?.id ?? -1;
        }

        // Si hay jugadores recuperados y estamos en fast-sim, interrumpir
        // para mostrar el modal (mismo patrón que lesionados/vacíos).
        if (st.fastSim && _pendingRecoveredIds.Count > 0)
        {
            _fastSimAborted = true;
            _fastSimAbortRecovered = true;
            _dayAborted = true;
            yield break;
        }


    }

    IEnumerator RunLineupValidation(GameDayRunState st)
    {
        st.gamesToday = DatabaseManager.Instance.GetGamesByGameDay(_manager.id, st.gameDay);
        var gamesToday = st.gamesToday;

        // Los partidos G-League comparten game_day con la NBA pero nunca
        // disparan modales de convocatoria ni navegación de mi equipo: los ids
        // de filial viven en otro espacio y podrían coincidir numéricamente.
        bool myTeamPlays = gamesToday.Any(g =>
            !IsGLeagueGame(g) && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
        bool hasAllStar = gamesToday.Any(g => g.game_type == "allstar");
        st.myTeamPlays = myTeamPlays;
        st.hasAllStar = hasAllStar;

        if (myTeamPlays)
        {
            // Ensure lineup exists before simulation
            var lineupCheck = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
            if (lineupCheck.Count == 0)
            {
                var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
                DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, allPlayers);
            }

            // Check for injured players FIRST — most urgent before lineup decisions
            var injured = GetInjuredActiveLineupPlayers();
            if (injured.Count > 0)
            {
                if (st.fastSim)
                {
                    _fastSimAborted = true;
                    _fastSimAbortInjured = injured;
                    _dayAborted = true;
            yield break;
                }
                _injuredModalResolved = false;
                _injuredModalGoToQuinteto = false;
                ShowInjuredLineupModal(injured);
                yield return new WaitUntil(() => _injuredModalResolved);
                if (_injuredModalGoToQuinteto)
                {
                    HideLoading();
                    ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                    _dayAborted = true;
            yield break;
                }
            }

            // Check for tired players (load management) — skipped on fast‑sim re‑entry
            if (!st.effectiveSkipPreBatch && PlayerPrefs.GetInt("TF_LoadMgmt_Enabled", 0) == 1)
            {
                _loadMgmtIsB2B = IsMyTeamBackToBack(st.gameDay);
                _loadMgmtTiredPlayers = GetTiredActivePlayers();
                if (_loadMgmtIsB2B || _loadMgmtTiredPlayers.Count > 0)
                {
                    if (st.fastSim)
                    {
                        _fastSimAborted = true;
                        _dayAborted = true;
            yield break;
                    }
                    _loadMgmtModalResolved = false;
                    _loadMgmtModalGoToQuinteto = false;
                    ShowLoadManagementModal(_loadMgmtTiredPlayers, _loadMgmtIsB2B);
                    yield return new WaitUntil(() => _loadMgmtModalResolved);
                    _loadMgmtTiredPlayers.Clear();
                    _loadMgmtIsB2B = false;
                    if (_loadMgmtModalGoToQuinteto)
                    {
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        _dayAborted = true;
            yield break;
                    }
                }
            }

            // Check for empty starter slots (e.g. after a trade)
            var emptySlots = GetEmptyStarterSlots();
            if (emptySlots.Count > 0)
            {
                if (st.fastSim)
                {
                    _fastSimAborted = true;
                    _fastSimAbortEmptySlots = emptySlots;
                    _dayAborted = true;
            yield break;
                }
                _emptyLineupModalResolved = false;
                _emptyLineupGoToQuinteto = false;
                ShowEmptyLineupModal(emptySlots);
                yield return new WaitUntil(() => _emptyLineupModalResolved);
                if (_emptyLineupGoToQuinteto)
                {
                    HideLoading();
                    ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                    _dayAborted = true;
            yield break;
                }
            }

            // Check de ROTACIÓN: o todos los minutos están asignados (240) o la rotación
            // está en automático (el simulador reparte). Se comprueba al final porque los
            // auto-rellenos anteriores resetean los minutos a -1 (estado válido).
            var rotStatus = GetRotationStatus();
            if (!rotStatus.IsComplete)
            {
                if (st.fastSim)
                {
                    _fastSimAborted = true;
                    _fastSimAbortRotation = true;
                    _dayAborted = true;
            yield break;
                }
                _rotationModalResolved = false;
                _rotationModalGoToRotation = false;
                ShowRotationModal(rotStatus);
                yield return new WaitUntil(() => _rotationModalResolved);
                if (_rotationModalGoToRotation)
                {
                    HideLoading();
                    ScreenManager.Instance.GoTo(GameScreen.Rotation);
                    _dayAborted = true;
            yield break;
                }
            }
        }


    }

    IEnumerator RunPreGameGate(GameDayRunState st)
    {
        // ── PreGame: navegar ANTES de simular ──
        if (st.navigateToPreGame && st.myTeamPlays)
        {
            Refresh();
            GameSaveManager.UpdateSlotFromDatabase(DatabaseManager.Instance.ActiveSaveSlot);
            HideLoading();
            Debug.Log($"[Dashboard] Día {st.gameDay} — pre-lote listo → PreGame (sin simular)");
            ScreenManager.Instance.GoTo(GameScreen.PreGame);
            _dayAborted = true;
            yield break;
        }


    }

    IEnumerator RunSimulationBookkeeping(GameDayRunState st)
    {
        var db = DatabaseManager.Instance.Db;
        db.BeginTransaction();
        try
        {
            // SIM-08: limpieza INCONDICIONAL. En un día sin partidos previos que crea
            // clasificatorios, `st.gamesToday` se leyó vacío y quedaban resultados del día
            // anterior mezclados en el cache.
            GameResultCache.Clear();
            if (st.gamesToday.Count > 0)
                GameResultCache.LastGameDay = st.gameDay;
        }
        catch (System.Exception ex)
        {
            db.Rollback();
            GameResultCache.Clear();
            HandleDayError("Error simulando el día", "ERROR AL SIMULAR EL DÍA", "Ha ocurrido un error al simular el día.", ex, st.fastSim);
            _dayAborted = true;
            yield break;
        }

        foreach (var game in st.gamesToday)
        {
            try
            {
                ProcessSingleGame(game);
            }
            catch (System.Exception ex)
            {
                db.Rollback();
                GameResultCache.Clear();
                HandleDayError("Error simulando el día", "ERROR AL SIMULAR EL DÍA", "Ha ocurrido un error al simular el día.", ex, st.fastSim);
                _dayAborted = true;
            yield break;
            }
        }

        // Recalculate team chemistry for all teams involved today
        foreach (var game in st.gamesToday)
        {
            try
            {
                int chem = DatabaseManager.Instance.CalculateTeamChemistry(game.home_team_id, st.gameDay);
                DatabaseManager.Instance.UpdateTeamChemistry(game.home_team_id, chem);
                chem = DatabaseManager.Instance.CalculateTeamChemistry(game.away_team_id, st.gameDay);
                DatabaseManager.Instance.UpdateTeamChemistry(game.away_team_id, chem);
            }
            catch (System.Exception ex)
            {
                db.Rollback();
                GameResultCache.Clear();
                HandleDayError("Error simulando el día", "ERROR AL SIMULAR EL DÍA", "Ha ocurrido un error al simular el día.", ex, st.fastSim);
                _dayAborted = true;
            yield break;
            }
        }

        try
        {
            var nbaGamesToday = st.gamesToday.Where(g => !IsGLeagueGame(g)).ToList();
            if (nbaGamesToday.Count > 0)
            {
                QuickNewsGenerator.Generate(_manager, _myTeam, _season, nbaGamesToday, st.gameDay, _season?.current_date ?? "");

                // Hitos de legado (E1): antes de los hilos, para que la narrativa los vea.
                MilestoneService.EvaluateDay(_manager, _myTeam, _season, st.gameDay, _season?.current_date ?? "");

                // Motor de narrativa: abre/escala/cierra hilos (rachas) con noticias y efectos.
                StorylineService.ProcessDay(_manager, _myTeam, _season, st.gameDay, _season?.current_date ?? "");

                AchievementService.EvaluateGameDay(_manager.id, _myTeam.id, _season.id);
            }

        // F5-04: la NBA Cup avanza por datos, sin tocar `seasons.phase`.
        try
        {
            CupHelper.AdvanceIfNeeded(_season, DatabaseManager.Instance.GetAllTeams());
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Cup] Error avanzando la NBA Cup: {ex.Message}");
        }

        RunPhaseTransitions(st.gameDay);

        // ── G-League postseason (competición paralela; no toca seasons.phase) ──
        try
        {
            GLeaguePostSeason.AdvanceIfNeeded(_manager.id, _season, _myTeam?.id ?? 0);
        }
        catch (System.Exception exGl)
        {
            Debug.LogError($"[Dashboard] Error en postseason G-League: {exGl.Message}\n{exGl.StackTrace}");
        }

        // ── F7-05: proyectos y eventos de ciudad ──
        try
        {
            CityService.Advance(_manager, _myTeam, _season, st.gameDay);
        }
        catch (System.Exception exCity)
        {
            Debug.LogError($"[Dashboard] Error en la capa de ciudad: {exCity.Message}\n{exCity.StackTrace}");
        }

        UpdateManagerStats(st.gameDay);

        TeamEconomyService.ProcessMonthlyPayroll(st.gameDay, _manager, _myTeam, _season);
        TeamEconomyService.ProcessSubscriptionRevenue(st.gameDay, _manager, _myTeam, _season);

        if (_season.phase == "finished")
        {
            DatabaseManager.Instance.UpdateHistoricalPlayerStatsFromSeason(_season.id, _manager.id);
            DatabaseManager.Instance.SaveSeasonEndRecords(_season.id, _manager.id);
            AssignSeasonPoints();
            AchievementService.EvaluateSeasonEnd(_manager.id, _myTeam.id, _season.id);
        }

        RunCalendarAndAwards(st.gameDay);
            DatabaseManager.Instance.UpdateSeason(_season);
            db.Commit();

            // El día queda cerrado: el siguiente día necesita su propio pre-lote.
            _preBatchGameDay = int.MinValue;
            _preBatchSeasonId = -1;
        }
        catch (System.Exception ex)
        {
            db.Rollback();
            _showDeadlineWeekModal = false;
            GameResultCache.Clear();
            Debug.LogError($"[GameDay] Error simulando el día, revertido: {ex.Message}\n{ex.StackTrace}");
            if (st.fastSim)
            {
                _fastSimAborted = true;
                _dayAborted = true;
            yield break;
            }
            HideLoading();
            ShowOfferResultModal("ERROR AL SIMULAR EL DÍA", "Ha ocurrido un error al simular el día.\n\n" + ex.Message, -1);
            _dayAborted = true;
            yield break;
        }
        finally
        {
            // Red de seguridad: si la corrutina se aborta a mitad (StopAllCoroutines al
            // salir de la pantalla) Unity ejecuta este finally. Sin él, la transacción
            // quedaría abierta y bloquearía todas las escrituras de la sesión.
            if (db.IsInTransaction)
            {
                try { db.Rollback(); } catch { }
                GameResultCache.Clear();
                Debug.LogWarning("[GameDay] Día abortado a mitad de transacción: revertido.");
            }
        }


    }

    void RunPhaseTransitions(int gameDay)
    {
        if (_season.phase == "regular" || _season.phase == "preseason")
        {
            bool allRegularPlayed = !DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == _manager.id
                       && g.game_type == "regular"
                       && g.is_played == 0);
            if (allRegularPlayed)
            {
                // Evaluate April before transitioning (use May 1 so prevMonth = 4 / April)
                Debug.Log("[Dashboard] Regular season finished, evaluating April awards");
                EvaluateMonthlyAwards(new System.DateTime(_season.year_end, 5, 1));
                PlayoffsGenerator.GeneratePlayIn(_season, _manager.id);
                _season.phase = "playin";
                DatabaseManager.Instance.UpdateSeason(_season);
                Debug.Log("[Dashboard] Regular season finished → Play-In generated.");
            }
        }

        if (_season.phase == "playin")
        {
            // Match Django: create eliminator for each conference (idempotent)
            int eastElim = PlayoffsGenerator.CreatePlayInEliminator(_season, _manager.id, "East");
            int westElim = PlayoffsGenerator.CreatePlayInEliminator(_season, _manager.id, "West");

            // Match Django: if eliminator games were just created on the current day, simulate them now
            if (eastElim > 0 || westElim > 0)
            {
                var elimToday = DatabaseManager.Instance.GetGamesByGameDay(_manager.id, gameDay);
                if (elimToday.Count > 0)
                {
                    foreach (var elimGame in elimToday)
                    {
                        var homeP = GetActivePlayers(elimGame.home_team_id);
                        var awayP = GetActivePlayers(elimGame.away_team_id);
                        int elimHomeChem = DatabaseManager.Instance.GetTeamChemistry(elimGame.home_team_id);
                        int elimAwayChem = DatabaseManager.Instance.GetTeamChemistry(elimGame.away_team_id);
                        var elimResult = GameSimulator.SimulateGame(elimGame, homeP, awayP, elimHomeChem, elimAwayChem,
                            homePlan: PlanFor(elimGame.home_team_id), awayPlan: PlanFor(elimGame.away_team_id),
                            homeCoachBonus: CoachBonusFor(elimGame.home_team_id), awayCoachBonus: CoachBonusFor(elimGame.away_team_id));
                        DatabaseManager.Instance.UpdateGame(elimGame);
                        TeamEconomyService.ProcessGameFinances(elimGame, _manager, _myTeam, _season);
                        UpdatePlayersMoraleAfterGame(elimResult.home_stats, elimGame.home_team_id, elimGame.home_score > elimGame.away_score);
                        UpdatePlayersMoraleAfterGame(elimResult.away_stats, elimGame.away_team_id, elimGame.away_score > elimGame.home_score);
                    }
                    GameResultCache.LastGameDay = gameDay;
                    GameResultCache.SimulatedGameIds.AddRange(elimToday.Select(g => g.id));

                    // Recalculate chemistry for Play-In teams
                    foreach (var elimGame in elimToday)
                    {
                        int chem = DatabaseManager.Instance.CalculateTeamChemistry(elimGame.home_team_id, gameDay);
                        DatabaseManager.Instance.UpdateTeamChemistry(elimGame.home_team_id, chem);
                        chem = DatabaseManager.Instance.CalculateTeamChemistry(elimGame.away_team_id, gameDay);
                        DatabaseManager.Instance.UpdateTeamChemistry(elimGame.away_team_id, chem);
                    }
                }
            }

            // Check user's play-in elimination BEFORE allPlayInPlayed transition
            if (_myTeam != null)
            {
                bool hasPlayedPlayIn = DatabaseManager.Instance.Db.Table<GameData>()
                    .Any(g => g.manager_id == _manager.id
                           && g.game_type == "playin"
                           && g.is_played == 1
                           && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
                bool hasRemainingPlayIn = DatabaseManager.Instance.Db.Table<GameData>()
                    .Any(g => g.manager_id == _manager.id
                           && g.game_type == "playin"
                           && g.is_played == 0
                           && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));

                if (hasPlayedPlayIn && !hasRemainingPlayIn)
                {
                    var myPlayInGames = DatabaseManager.Instance.Db.Table<GameData>()
                        .Where(g => g.manager_id == _manager.id
                               && g.game_type == "playin"
                               && g.is_played == 1
                               && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id))
                        .OrderByDescending(g => g.game_day)
                        .ToList();

                    if (myPlayInGames.Count > 0)
                    {
                        var lastGame = myPlayInGames[0];
                        bool iWonLast = (lastGame.home_team_id == _myTeam.id && lastGame.home_score > lastGame.away_score)
                                      || (lastGame.away_team_id == _myTeam.id && lastGame.away_score > lastGame.home_score);

                        if (!iWonLast)
                        {
                            bool playInAlreadySent = DatabaseManager.Instance.Db.Table<MessageData>()
                                .Any(m => m.manager_id == _manager.id && m.news_type == NewsType.ELIMINACION_PLAYIN);
                            if (!playInAlreadySent)
                            {
                                DatabaseManager.Instance.AddMessage(new MessageData
                                {
                                    manager_id = _manager.id,
                                    sender_type = 1,
                                    sender_id = 0,
                                    title = "Eliminación en Play-In",
                                    body = NewsTexts.EliminacionPlayin(_myTeam.name, 0),
                                    game_day = gameDay,
                                    game_date = _season?.current_date ?? "",
                                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                    is_read = 0,
                                    category = NewsCategory.LIGA,
                                    news_type = NewsType.ELIMINACION_PLAYIN
                                });
                                Debug.Log($"[Dashboard] {_myTeam.name} eliminado de Play-In.");
                            }
                        }
                    }
                }
            }

            bool allPlayInPlayed = !DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == _manager.id
                       && g.game_type == "playin"
                       && g.is_played == 0);
            if (allPlayInPlayed)
            {
                int created = PlayoffsGenerator.GeneratePlayoffs(_season, _manager.id);
                if (created > 0)
                {
                    _season.phase = "playoff";
                    DatabaseManager.Instance.UpdateSeason(_season);
                    Debug.Log($"[Dashboard] Play-In finished → Playoffs generated ({created} games).");
                }
                else
                {
                    Debug.LogError("[Dashboard] allPlayInPlayed but GeneratePlayoffs returned 0 games!");
                }
            }
        }

        if (_season.phase == "playoff")
        {
            PlayoffsGenerator.AdvancePlayoffSeries(_season, _manager.id);

            bool allPlayoffPlayed = !DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == _manager.id
                       && g.game_type == "playoff"
                       && g.is_played == 0);
            if (allPlayoffPlayed)
            {
                _season.phase = "finished";
                DatabaseManager.Instance.UpdateSeason(_season);
                Debug.Log("[Dashboard] Playoffs finished → Season finished.");
            }
            else if (_myTeam != null)
            {
                bool hasPlayedPlayoffs = DatabaseManager.Instance.Db.Table<GameData>()
                    .Any(g => g.manager_id == _manager.id
                           && g.game_type == "playoff"
                           && g.is_played == 1
                           && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
                bool hasRemainingPlayoffs = DatabaseManager.Instance.Db.Table<GameData>()
                    .Any(g => g.manager_id == _manager.id
                           && g.game_type == "playoff"
                           && g.is_played == 0
                           && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));

                bool teamWonLastSeries = false;
                string lastSeriesLabel = "";
                if (hasPlayedPlayoffs && !hasRemainingPlayoffs)
                {
                    lastSeriesLabel = DatabaseManager.Instance.Db.Table<GameData>()
                        .Where(g => g.manager_id == _manager.id
                               && g.game_type == "playoff"
                               && g.is_played == 1
                               && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id))
                        .OrderByDescending(g => g.game_day)
                        .Select(g => g.series_label)
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(lastSeriesLabel))
                    {
                        int winner = PlayoffsGenerator.GetSeriesWinner(_season, _manager.id, lastSeriesLabel);
                        teamWonLastSeries = (winner == _myTeam.id);
                    }
                }

                if (hasPlayedPlayoffs && !hasRemainingPlayoffs && !teamWonLastSeries)
                {
                    bool playoffAlreadySent = DatabaseManager.Instance.Db.Table<MessageData>()
                        .Any(m => m.manager_id == _manager.id && m.news_type == NewsType.ELIMINACION_PLAYOFFS);
                    if (!playoffAlreadySent)
                    {
                        string rivalName = "";
                        int round = 0;
                        int rivalWins = 0;
                        int teamWins = 0;

                        if (!string.IsNullOrEmpty(lastSeriesLabel))
                        {
                            var seriesGames = DatabaseManager.Instance.Db.Table<GameData>()
                                .Where(g => g.manager_id == _manager.id
                                       && g.series_label == lastSeriesLabel
                                       && g.is_played == 1)
                                .ToList();

                            if (seriesGames.Count > 0)
                            {
                                int rivalId = seriesGames[0].home_team_id == _myTeam.id
                                    ? seriesGames[0].away_team_id
                                    : seriesGames[0].home_team_id;
                                rivalName = _allTeams?.Find(t => t.id == rivalId)?.name ?? "";

                                foreach (var sg in seriesGames)
                                {
                                    bool iLost = (sg.home_team_id == _myTeam.id && sg.home_score < sg.away_score)
                                              || (sg.away_team_id == _myTeam.id && sg.away_score < sg.home_score);
                                    if (iLost) rivalWins++;
                                    else teamWins++;
                                }
                            }

                            if (lastSeriesLabel.Contains("-r1-")) round = 1;
                            else if (lastSeriesLabel.Contains("-r2-")) round = 2;
                            else if (lastSeriesLabel.Contains("-r3-")) round = 3;
                            else round = 4;
                        }

                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = "Eliminación en Playoffs",
                            body = NewsTexts.EliminacionPlayoffs(_myTeam.name, rivalName, round, rivalWins, teamWins),
                            game_day = gameDay,
                            game_date = _season?.current_date ?? "",
                            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            is_read = 0,
                            category = NewsCategory.LIGA,
                            news_type = NewsType.ELIMINACION_PLAYOFFS
                        });
                        Debug.Log($"[Dashboard] {_myTeam.name} eliminado de Playoffs por {rivalName} en ronda {round}.");
                    }
                }
            }
        }

    }

    void RunCalendarAndAwards(int gameDay)
    {
        _season.current_game_day = gameDay;
        if (!string.IsNullOrEmpty(_season.current_date))
        {
            var cur = System.DateTime.Parse(_season.current_date);
            _season.current_date = cur.AddDays(1).ToString("yyyy-MM-dd");
            var newDate = System.DateTime.Parse(_season.current_date);
            if (newDate.Month == 2 && newDate.Day == 1 && newDate.Year == _season.year_end)
            {
                DatabaseManager.Instance.AddMessage(new MessageData
                {
                    manager_id = _manager.id,
                    sender_type = 2,
                    sender_id = 0,
                    title = "Última semana de traspasos",
                    body = NewsTexts.DeadlineSemana(),
                    game_day = _season.current_game_day,
                    game_date = _season.current_date,
                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    is_read = 0,
                    category = NewsCategory.TRASPASOS,
                    news_type = NewsType.DEADLINE_SEMANA
                });
                _showDeadlineWeekModal = true;
            }

            if (newDate.Month == 2 && newDate.Day == 12 && newDate.Year == _season.year_end)
            {
                DatabaseManager.Instance.AddMessage(new MessageData
                {
                    manager_id = _manager.id,
                    sender_type = 0,
                    sender_id = 0,
                    title = "Fecha límite de renovaciones",
                    body = "Hoy cierra el plazo para renovar contratos. A partir de mañana no podrás ofrecer renovaciones a tus jugadores hasta la próxima temporada.",
                    game_day = _season.current_game_day,
                    game_date = _season.current_date,
                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    is_read = 0,
                    category = NewsCategory.CLUB,
                    news_type = NewsType.RENOVACION
                });
            }

            // Monthly awards evaluation (1st of Dec through Apr)
            if (_season.phase == "regular" && newDate.Day == 1
                && (newDate.Month == 12 || newDate.Month <= 4))
            {
                Debug.Log($"[Dashboard] Trigger awards: newDate={newDate:yyyy-MM-dd}, phase={_season.phase}");
                EvaluateMonthlyAwards(newDate);
            }
        }
    }

    IEnumerator RunPostDay(GameDayRunState st)
    {
        _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        _teamRecordCache = null;

        if (_showDeadlineWeekModal)
        {
            _showDeadlineWeekModal = false;
            ShowTradeDeadlineModal(st.fastSim);
            yield return new WaitUntil(() => _firedOverlay.style.display == DisplayStyle.None);
        }

        if (st.fastSim)
        {
            // En modo simulación rápida no se navega ni se refresca por día:
            // el refresh se hace al final. Si hay ofertas maduradas o propuestas
            // de traspaso pendientes, se aborta el día para mostrarlas con la
            // simulación parada (el día ya está commiteado).
            var matured = DatabaseManager.Instance.GetMaturedUnprocessedOffers(_manager.id, _season.current_game_day);
            var pendingTrades = DatabaseManager.Instance.GetPendingTradeOffers(_manager.id);
            if (matured.Count > 0 || pendingTrades.Count > 0)
            {
                _fastSimAborted = true;
                _fastSimAbortOffers = true;
                _dayAborted = true;
            yield break;
            }
            _dayAborted = true;
            yield break;
        }

        Refresh();

        GameSaveManager.UpdateSlotFromDatabase(DatabaseManager.Instance.ActiveSaveSlot);

        HideLoading();

        if (st.gamesToday.Count == 0)
        {
            ShowPendingRecoveryModal();
            ShowPendingIRReleaseModal();
            ProcessMaturedOffers();
            Debug.Log($"[Dashboard] Día {st.gameDay} — sin partidos, continúa en Dashboard");
        }
        else if (st.myTeamPlays || st.hasAllStar)
        {
            if (st.hasAllStar)
            {
                Debug.Log($"[Dashboard] Día {st.gameDay} — All-Star Game → MatchDay");
                ScreenManager.Instance.GoTo(GameScreen.MatchDay);
            }
            else if (st.navigateToPreGame)
            {
                Debug.Log($"[Dashboard] Día {st.gameDay} — mi equipo juega → PreGame");
                ScreenManager.Instance.GoTo(GameScreen.PreGame);
            }
            else
            {
                Debug.Log($"[Dashboard] Día {st.gameDay} — mi equipo juega → MatchDay");
                ScreenManager.Instance.GoTo(GameScreen.MatchDay);
            }
        }
        else
        {
            Debug.Log($"[Dashboard] Día {st.gameDay} — {st.gamesToday.Count} partidos → GameResults");
            ScreenManager.Instance.GoTo(GameScreen.GameResults);
        }

    }

}
