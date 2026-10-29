using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

// B47: Simulación rápida, resumen, carga y avisos de presupuesto.
public partial class DashboardController
{
    // ── SIMULACIÓN RÁPIDA ─────────────────────────────────

    System.Collections.IEnumerator FastSimRoutine(System.DateTime target)
    {
        if (_season == null)
        {
            HideLoading();
            yield break;
        }

        _fastSimAborted = false;
        _fastSimErrorMessage = null;
        _fastSimSkipPreBatch = false;
        _fastSimAutoFixAttempts = 0;
        _fastSimAbortEmptySlots = new List<int>();
        _fastSimAbortInjured = new List<(LineupData, PlayerData)>();
        _fastSimAbortOffers = false;
        _fastSimAbortRecovered = false;
        _fastSimOfferPauseActive = false;
        _offerModalGoToQuinteto = false;
        _tradeOfferModalResolved = true;
        _tradeOfferAccepted = false;
        _tradeOfferGoToQuinteto = false;

        ShowLoading();

        // Durante la simulación el botón de acción permite detenerla.
        _fastSimRunning = true;
        _fastSimStopRequested = false;
        SetActionBtn("DETENER SIMULACIÓN", "");
        _btnAction.SetEnabled(true);
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(_btnAction);
        SetSidebarNavigationEnabled(false);

        _fastSimStartDate = _season?.current_date;

        while (_season.phase != "finished")
        {
            if (_fastSimStopRequested) break;

            string curDate = _season.current_date;
            if (string.IsNullOrEmpty(curDate)
                || !System.DateTime.TryParse(curDate, out System.DateTime cur)
                || cur > target)
                break;

            if (FindNextGameDay() == 0)
                break;

            if (IsFeb7OfYearEnd() && !_deadlineDayModalShown)
            {
                _deadlineDayModalShown = true;
                _deadlineDayActive = true;
                ShowDeadlineDayModal(true);
                yield return new WaitUntil(() => _firedOverlay.style.display == DisplayStyle.None);
                _deadlineDayActive = false;
            }

            yield return ProcessGameDayRoutine(fastSim: true);

            if (IsFeb12OfYearEnd() && !_renewalDeadlineModalShown)
            {
                _renewalDeadlineModalShown = true;
                _renewalDeadlineResolved = false;
                ShowRenewalDeadlineModal(true);
                yield return new WaitUntil(() => _renewalDeadlineResolved);
            }

            if (_pendingIRReleaseCount > 0)
            {
                _irReleaseModalResolved = false;
                ShowPendingIRReleaseModal();
                yield return new WaitUntil(() => _irReleaseModalResolved);
                if (_pendingIRReleaseCount > 0)
                {
                    _fastSimAborted = true;
                    break;
                }
            }

            if (_fastSimAborted)
            {
                bool autoFixed = false;
                if (_fastSimAbortInjured.Count > 0)
                {
                    _injuredModalResolved = false;
                    _injuredModalGoToQuinteto = false;
                    ShowInjuredLineupModal(_fastSimAbortInjured);
                    yield return new WaitUntil(() => _injuredModalResolved);
                    if (_injuredModalGoToQuinteto)
                    {
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        yield break;
                    }
                    autoFixed = true;
                }
                else if (_fastSimAbortRecovered)
                {
                    _recoveryModalResolved = false;
                    _recoveryModalGoToQuinteto = false;
                    ShowPendingRecoveryModal();
                    yield return new WaitUntil(() => _recoveryModalResolved);
                    if (_recoveryModalGoToQuinteto)
                    {
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        yield break;
                    }
                    _fastSimAborted = false;
                    _fastSimAbortRecovered = false;
                    continue;
                }
                else if (_fastSimAbortEmptySlots.Count > 0)
                {
                    _emptyLineupModalResolved = false;
                    _emptyLineupGoToQuinteto = false;
                    ShowEmptyLineupModal(_fastSimAbortEmptySlots);
                    yield return new WaitUntil(() => _emptyLineupModalResolved);
                    if (_emptyLineupGoToQuinteto)
                    {
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        yield break;
                    }
                    autoFixed = true;
                }
                else if (_fastSimAbortRotation)
                {
                    _rotationModalResolved = false;
                    _rotationModalGoToRotation = false;
                    ShowRotationModal(GetRotationStatus());
                    yield return new WaitUntil(() => _rotationModalResolved);
                    if (_rotationModalGoToRotation)
                    {
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Rotation);
                        yield break;
                    }
                    _fastSimAbortRotation = false;
                    autoFixed = true;
                }
                else if (_loadMgmtIsB2B || _loadMgmtTiredPlayers.Count > 0)
                {
                    _loadMgmtModalResolved = false;
                    _loadMgmtModalGoToQuinteto = false;
                    ShowLoadManagementModal(_loadMgmtTiredPlayers, _loadMgmtIsB2B);
                    yield return new WaitUntil(() => _loadMgmtModalResolved);
                    _loadMgmtTiredPlayers.Clear();
                    _loadMgmtIsB2B = false;
                    if (_loadMgmtModalGoToQuinteto)
                    {
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        yield break;
                    }
                    autoFixed = true;
                }
                else if (_fastSimAbortOffers)
                {
                    _fastSimOfferPauseActive = true;

                    if (DatabaseManager.Instance.GetMaturedUnprocessedOffers(_manager.id, _season.current_game_day).Count > 0)
                    {
                        _offerModalResolved = false;
                        _offerModalGoToQuinteto = false;
                        ProcessMaturedOffers();
                        yield return new WaitUntil(() => _offerModalResolved);
                        _firedOverlay.style.display = DisplayStyle.None;
                        if (_offerModalGoToQuinteto)
                        {
                            _fastSimOfferPauseActive = false;
                            _fastSimAbortOffers = false;
                            _fastSimRunning = false;
                            SetSidebarNavigationEnabled(true);
                            HideLoading();
                            ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                            yield break;
                        }
                    }

                    bool tradeAccepted = false;
                    while (DatabaseManager.Instance.GetPendingTradeOffers(_manager.id).Count > 0)
                    {
                        _tradeOfferModalResolved = false;
                        _tradeOfferAccepted = false;
                        _tradeOfferGoToQuinteto = false;
                        ShowNextPendingTradeOffer();
                        // Sin modal no hay nada que esperar: se corta el bucle (antes un
                        // `continue` podía dejarlo girando en vacío).
                        if (_firedOverlay.style.display != DisplayStyle.Flex) break;
                        yield return new WaitUntil(() => _tradeOfferModalResolved);
                        if (_tradeOfferAccepted)
                        {
                            tradeAccepted = true;
                            break;
                        }
                        if (_tradeOfferGoToQuinteto) break;
                    }

                    _fastSimOfferPauseActive = false;
                    _fastSimAbortOffers = false;

                    // Aviso de una oferta que dejó de ser válida: el jugador eligió
                    // IR A QUINTETO para arreglar la plantilla.
                    if (_tradeOfferGoToQuinteto)
                    {
                        _tradeOfferGoToQuinteto = false;
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        yield break;
                    }

                    if (tradeAccepted)
                    {
                        _fastSimAborted = true;
                        break;
                    }
                    _fastSimAborted = false;
                    continue;
                }

                _fastSimAbortEmptySlots.Clear();
                _fastSimAbortInjured.Clear();

                if (autoFixed)
                {
                    if (++_fastSimAutoFixAttempts > 3)
                    {
                        Debug.LogWarning("[Dashboard] No se pudo completar la alineación en varios intentos. Se detiene la simulación.");
                        break;
                    }
                    _fastSimAborted = false;
                    _fastSimSkipPreBatch = true;
                    continue;
                }
                break;
            }
            _fastSimAutoFixAttempts = 0;

            // ── Aviso financiero / despido durante FastSim ──
            if (GameResultCache.PendingBudgetWarning)
            {
                _fastSimRunning = false;
                SetSidebarNavigationEnabled(true);
                CheckBudgetWarning(true);
                yield return new WaitUntil(() => _firedOverlay.style.display == DisplayStyle.None);
                if (_season == null) yield break;
                _fastSimRunning = true;
                SetSidebarNavigationEnabled(false);
                SetActionBtn("DETENER SIMULACIÓN", "");
            }

            // ── Despido anticipado durante FastSim ──
            if (_myTeam != null && (_season.phase == "playin" || _season.phase == "playoff"))
            {
                string phaseType = _season.phase == "playin" ? "playin" : "playoff";
                bool teamHasPhaseGames = DatabaseManager.Instance.Db.Table<GameData>()
                    .Any(g => g.manager_id == _manager.id && g.game_type == phaseType
                           && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
                if (!teamHasPhaseGames && ShowFiredModalIfNeeded())
                {
                    _fastSimRunning = false;
                    SetSidebarNavigationEnabled(true);
                    yield return new WaitUntil(() => _firedOverlay.style.display == DisplayStyle.None);
                    yield break;
                }
            }

            // Refrescar cabecera (fecha) y paneles del Dashboard tras cada día
            _myTeam = DatabaseManager.Instance.GetTeamById(_myTeam.id);
            _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        _teamRecordCache = null;
            Refresh();

            // Renderizar un frame por día para que el spinner de la cabecera
            // gire visiblemente durante la simulación rápida.
            yield return null;
        }

        _fastSimRunning = false;
        SetSidebarNavigationEnabled(true);
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _myTeam = DatabaseManager.Instance.GetTeamById(_myTeam.id);
        _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        _teamRecordCache = null;
        Refresh();
        GameSaveManager.UpdateSlotFromDatabase(DatabaseManager.Instance.ActiveSaveSlot);
        HideLoading();

        if (_fastSimErrorMessage != null)
        {
            // La simulación se detuvo por un error: avisar en vez de salir en silencio.
            ShowOfferResultModal("ERROR EN LA SIMULACIÓN",
                "La simulación se detuvo por un error y el último día no se aplicó.\n\n" + _fastSimErrorMessage, -1);
        }
        else if (!_fastSimAborted && !_fastSimStopRequested)
        {
            // Calcular W/L del período simulado
            string endDate = _season?.current_date ?? _fastSimTarget?.ToString("yyyy-MM-dd") ?? "";
            int wins = 0, losses = 0;
            if (_allGames != null && _myTeam != null)
            {
                var periodGames = _allGames
                    .Where(g => !string.IsNullOrEmpty(g.game_date)
                            && string.Compare(g.game_date, _fastSimStartDate, System.StringComparison.Ordinal) >= 0
                            && string.Compare(g.game_date, endDate, System.StringComparison.Ordinal) <= 0
                            && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id))
                    .ToList();
                wins = periodGames.Count(g =>
                    (g.home_team_id == _myTeam.id && g.home_score > g.away_score)
                    || (g.away_team_id == _myTeam.id && g.away_score > g.home_score));
                losses = periodGames.Count - wins;
            }
            ShowFastSimSummary(_fastSimStartDate, endDate, wins, losses);
        }
    }

    void ShowFastSimSummary(string startDate, string endDate, int wins, int losses)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(42, 95, 201, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(42, 95, 201, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(42, 95, 201, 255));
        box.style.borderRightColor = new StyleColor(new Color32(42, 95, 201, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--blue");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.simulacion_completada"));
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(42, 95, 201, 255));
        header.Add(title);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // Fechas
        var datesRow = new VisualElement();
        datesRow.AddToClassList("fastsim-modal-dates");

        var dateIcon = new VisualElement();
        dateIcon.AddToClassList("fastsim-modal-dates-icon");
        var dateIconTex = Resources.Load<Texture2D>("Icons/calendario");
        if (dateIconTex != null)
            dateIcon.style.backgroundImage = new StyleBackground(dateIconTex);
        datesRow.Add(dateIcon);

        string fmtStart = FormatDateShort(startDate);
        string fmtEnd = FormatDateShort(endDate);
        var dateText = new Label($"{fmtStart}  →  {fmtEnd}");
        dateText.AddToClassList("fastsim-modal-dates-text");
        datesRow.Add(dateText);
        body.Add(datesRow);

        // Stats: W / L
        var statsRow = new VisualElement();
        statsRow.AddToClassList("fastsim-modal-stats");

        var winCol = new VisualElement();
        winCol.AddToClassList("fastsim-modal-stat-col");
        var winNum = new Label(wins.ToString());
        winNum.AddToClassList("fastsim-modal-stat-num");
        winNum.AddToClassList("fastsim-modal-stat-num--win");
        winCol.Add(winNum);
        var winLabel = new Label(LocalizationService.T("ui.code.dashboard_controller.ganados"));
        winLabel.AddToClassList("fastsim-modal-stat-label");
        winCol.Add(winLabel);
        statsRow.Add(winCol);

        var sep = new Label("-");
        sep.AddToClassList("fastsim-modal-stat-sep");
        statsRow.Add(sep);

        var lossCol = new VisualElement();
        lossCol.AddToClassList("fastsim-modal-stat-col");
        var lossNum = new Label(losses.ToString());
        lossNum.AddToClassList("fastsim-modal-stat-num");
        lossNum.AddToClassList("fastsim-modal-stat-num--loss");
        lossCol.Add(lossNum);
        var lossLabel = new Label(LocalizationService.T("ui.code.dashboard_controller.perdidos"));
        lossLabel.AddToClassList("fastsim-modal-stat-label");
        lossCol.Add(lossLabel);
        statsRow.Add(lossCol);

        body.Add(statsRow);

        // Detail text
        int total = wins + losses;
        string detail = total > 0
            ? $"{total} partidos simulados · {wins} ganados · {losses} perdidos"
            : "Sin partidos jugados en este período";
        var detailLbl = new Label(detail);
        detailLbl.AddToClassList("fastsim-modal-detail");
        body.Add(detailLbl);

        box.Add(body);

        // ── Button ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");
        var btn = new Button();
        btn.text = LocalizationService.T("ui.code.dashboard_controller.cerrar");
        btn.AddToClassList("btn-modal-confirm");
        btn.AddToClassList("btn-modal-confirm--blue");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(btn);
        box.Add(actions);

        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(btn);
    }

    string FormatDateShort(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "—";
        if (System.DateTime.TryParse(isoDate, out System.DateTime dt))
            return dt.ToString("dd/MM/yyyy");
        return isoDate;
    }

    void ShowLoading()
    {
        if (_isLoading) return;
        _isLoading = true;
        _btnAction.SetEnabled(false);
        _loadingSpinner.style.display = DisplayStyle.Flex;

        // Bloquear navegación (sidebar + config → Menú principal) mientras corre.
        SetSidebarNavigationEnabled(false);
        ScreenManager.NavigationLocked = true;

        _spinScheduler = _root.schedule.Execute(() =>
        {
            float angle;
            if (_loadingSpinner != null)
            {
                var current = _loadingSpinner.style.rotate;
                angle = current.value.angle.value + 15f;
                if (angle >= 360f) angle -= 360f;
                _loadingSpinner.style.rotate = new Rotate(Angle.Degrees(angle));
            }
            if (_matchTransitionSpinner != null)
            {
                var currentTransition = _matchTransitionSpinner.style.rotate;
                float transitionAngle = currentTransition.value.angle.value + 15f;
                if (transitionAngle >= 360f) transitionAngle -= 360f;
                _matchTransitionSpinner.style.rotate = new Rotate(Angle.Degrees(transitionAngle));
            }
        }).Every(30);
    }

    void HideLoading()
    {
        _spinScheduler?.Pause();
        _spinScheduler = null;
        _loadingSpinner.style.display = DisplayStyle.None;
        HideMatchTransitionOverlay();
        _btnAction.SetEnabled(true);
        _isLoading = false;
        SetSidebarNavigationEnabled(true);
        ScreenManager.NavigationLocked = false;
    }

    void CheckBudgetAfterGame()
    {
        if (_myTeam.budget < 0)
        {
            _manager.budget_red_warnings++;
            GameResultCache.PendingBudgetWarning = true;
        }
        else
        {
            _manager.budget_red_warnings = 0;
        }
        DatabaseManager.Instance.SaveManager(_manager);
    }

    /// <summary>
    /// Sincroniza el presupuesto en memoria de _myTeam con el de la copia de DB.
    /// Necesario porque muchos puntos mutan TeamData leído de la DB (no _myTeam):
    /// sin esto la cabecera y CheckBudgetAfterGame trabajan con datos obsoletos
    /// durante una sesión de Dashboard (sobre todo en FastSim, donde no hay OnEnable).
    /// </summary>
    void SyncMyTeamBudget(TeamData team)
    {
        if (team != null && _myTeam != null && team.id == _myTeam.id)
            _myTeam.budget = team.budget;
    }

    void CheckBudgetWarning(bool fastSim = false)
    {
        if (!GameResultCache.PendingBudgetWarning) return;
        GameResultCache.PendingBudgetWarning = false;

        // ProManager: cese por presupuesto más exigente (2 avisos en rojo vs 3).
        int threshold = _manager.game_mode == "promanager" ? 2 : 3;
        if (_manager.budget_red_warnings >= threshold)
            ShowBudgetFiredModal();
        else
            ShowBudgetWarningModal(_manager.budget_red_warnings, fastSim);
    }

}
