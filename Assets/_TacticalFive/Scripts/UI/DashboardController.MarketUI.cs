using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

// B47: Mercado: ofertas de traspaso, S&T, renovación y bookkeeping postpartido.
public partial class DashboardController
{
    // ── AI RENEWALS ────────────────────────────────────────────────

    void ShowNextPendingTradeOffer()
    {
        var pending = DatabaseManager.Instance.GetPendingTradeOffers(_manager.id);
        if (pending.Count == 0)
        {
            _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            Refresh();
            return;
        }

        var offer = pending[0];
        var myRoster = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var aiRoster = DatabaseManager.Instance.GetPlayersByTeam(offer.team_id_from);

        var wantedIds = offer.GetWantedPlayerIds();
        var offeredIds = offer.GetOfferedPlayerIds();
        var wantedPickIds = offer.GetWantedPickIds();
        var offeredPickIds = offer.GetOfferedPickIds();

        var ourPlayers = wantedIds
            .Select(id => myRoster.FirstOrDefault(p => p.id == id))
            .Where(p => p != null)
            .ToList();

        // S&T: si la IA pide un FA propio tuyo (no está en el roster), se firma y se traspasa
        var ourSATFAs = new List<PlayerData>();
        var freeAgents = DatabaseManager.Instance.GetFreeAgents().ToList();
        var unaccounted = wantedIds.Where(id => ourPlayers.All(p => p.id != id)).ToList();
        foreach (var id in unaccounted)
        {
            var fa = freeAgents.FirstOrDefault(p => p.id == id && DatabaseManager.Instance.IsOwnRecentFA(p, _myTeam.id));
            if (fa != null)
            {
                ourSATFAs.Add(fa);
                ourPlayers.Add(fa);
            }
        }

        var theirPlayers = offeredIds
            .Select(id => aiRoster.FirstOrDefault(p => p.id == id))
            .Where(p => p != null)
            .ToList();

        var ourPicks = wantedPickIds
            .Select(id => DatabaseManager.Instance.GetDraftPickById(id))
            .Where(p => p != null && p.current_team_id == _myTeam.id)
            .ToList();
        var theirPicks = offeredPickIds
            .Select(id => DatabaseManager.Instance.GetDraftPickById(id))
            .Where(p => p != null && p.current_team_id == offer.team_id_from)
            .ToList();

        if (ourPlayers.Count != wantedIds.Count || theirPlayers.Count != offeredIds.Count
            || ourPicks.Count != wantedPickIds.Count || theirPicks.Count != offeredPickIds.Count)
        {
            DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 2);
            ShowNextPendingTradeOffer();
            return;
        }

        // Revalidación al mostrarla: la IA validó el traspaso cuando lo generó, pero la
        // oferta puede quedar pendiente varios días y desde entonces la nómina, la
        // plantilla o el hard cap cambian. Sin esto se enseñaban ofertas que solo
        // fallaban al aceptarlas. Las obsoletas se descartan sin molestar al jugador.
        var staleErrors = ValidatePendingTradeOffer(offer, ourPlayers, theirPlayers, ourSATFAs);
        if (staleErrors.Count > 0)
        {
            DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 2);
            Debug.LogWarning($"[AITrades] Oferta de {offer.team_id_from} descartada por obsoleta: {string.Join(" | ", staleErrors)}");
            ShowNextPendingTradeOffer();
            return;
        }

        ShowTradeOfferModal(offer, ourPlayers, theirPlayers, ourPicks, theirPicks, ourSATFAs);
    }

    /// <summary>
    /// Revalida una oferta de traspaso de la IA con la plantilla y la nómina ACTUALES.
    /// Equivale a la validación que hace la IA al generarla (<see cref="TradeHelper.ValidateTrade"/>),
    /// con el mismo reparto de lados que al aceptar: A = equipo IA (envía), B = mi equipo (recibe).
    /// Devuelve la lista de errores (vacía = la oferta sigue siendo válida).
    /// </summary>
    List<string> ValidatePendingTradeOffer(TradeOfferData offer, List<PlayerData> ourPlayers,
                                           List<PlayerData> theirPlayers, List<PlayerData> ourSATFAs)
    {
        var aiTeam = _allTeams?.FirstOrDefault(t => t.id == offer.team_id_from)
                     ?? DatabaseManager.Instance.GetTeamById(offer.team_id_from);

        // S&T: los FA propios que se firmarían viajan con su nuevo contrato (Bird rights).
        var signSalaries = new Dictionary<int, long>();
        if (ourSATFAs != null && ourSATFAs.Count > 0)
        {
            var lsSat = DatabaseManager.Instance.GetLeagueSettings();
            long myPaySat = DatabaseManager.Instance.GetEffectiveTeamPayroll(_myTeam.id);
            foreach (var fa in ourSATFAs)
            {
                var bd = RosterController.GetMaxOfferBreakdown(fa, lsSat, myPaySat, true);
                long market = DatabaseManager.Instance.EstimateMarketSalary(fa.GetCalculatedAverage(),
                    lsSat != null ? lsSat.salary_cap : TradeHelper.SALARY_CAP);
                signSalaries[fa.id] = bd.birdMax > 0 ? bd.birdMax : market;
            }
        }

        var leagueChk = DatabaseManager.Instance.GetLeagueSettings();
        long firstApronChk = leagueChk?.apron > 0 ? leagueChk.apron : TradeHelper.FIRST_APRON;
        long secondApronChk = leagueChk?.repeater_apron > 0 ? leagueChk.repeater_apron : TradeHelper.SECOND_APRON;
        long luxuryTaxChk = leagueChk?.luxury_tax > 0 ? leagueChk.luxury_tax : TradeHelper.LUXURY_TAX;

        return TradeHelper.ValidateTrade(
            theirPlayers, ourPlayers,
            DatabaseManager.Instance.GetRosterCount(offer.team_id_from),
            DatabaseManager.Instance.GetRosterCount(_myTeam.id),
            _myTeam.name, DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id).Sum(p => p.salary),
            aiTeam != null ? aiTeam.name : "IA",
            DatabaseManager.Instance.GetPlayersByTeam(offer.team_id_from).Sum(p => p.salary),
            aiTeam != null && aiTeam.first_apron_hard_capped == 1,
            _myTeam.first_apron_hard_capped == 1,
            firstApronChk, secondApronChk, luxuryTaxChk,
            null,
            signSalaries.Count > 0 ? signSalaries : null,
            teamATwoWayCount: DatabaseManager.Instance.GetTwoWayCount(offer.team_id_from),
            teamBTwoWayCount: DatabaseManager.Instance.GetTwoWayCount(_myTeam.id),
            teamATotalActiveRoster: DatabaseManager.Instance.GetTotalRosterCount(offer.team_id_from),
            teamBTotalActiveRoster: DatabaseManager.Instance.GetTotalRosterCount(_myTeam.id));
    }

    void ShowTradeOfferModal(TradeOfferData offer, List<PlayerData> ourPlayers, List<PlayerData> theirPlayers,
                              List<DraftPickData> ourPicks = null, List<DraftPickData> theirPicks = null,
                              List<PlayerData> ourSATFAs = null)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var aiTeam = _allTeams.FirstOrDefault(t => t.id == offer.team_id_from);
        string teamName = aiTeam != null ? aiTeam.name : "?";
        var teamAbbrs = DatabaseManager.Instance.GetAllTeams().ToDictionary(t => t.id, t => t.abbreviation);

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.width = 1050;
        box.style.borderTopColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderRightColor = new StyleColor(new Color32(212, 160, 23, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");

        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.propuesta_de_intercambio"));
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        header.Add(title);
        box.Add(header);

        // ── Teams row ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        body.AddToClassList("trade-offer-body");

        var teamsRow = new VisualElement();
        teamsRow.AddToClassList("trade-modal-teams");

        // Pre-compute totals for cap room calculation
        long ourSalaryTotal = ourPlayers.Sum(p => p.salary);
        long theirSalaryTotal = theirPlayers.Sum(p => p.salary);

        // Left: your team (what you send)
        var leftPanel = BuildTradeTeamPanel(_myTeam, ourPlayers, ourPicks, teamAbbrs, isSending: true, otherSideSalary: theirSalaryTotal);
        teamsRow.Add(leftPanel);

        // Swap icon
        var swapIcon = new VisualElement();
        swapIcon.AddToClassList("trade-modal-swap");
        var mercadoSprite = Resources.Load<Sprite>("Icons/swap_blue");
        if (mercadoSprite != null)
            swapIcon.style.backgroundImage = new StyleBackground(mercadoSprite);
        teamsRow.Add(swapIcon);

        // Right: their team (what you receive)
        var rightPanel = BuildTradeTeamPanel(aiTeam, theirPlayers, theirPicks, teamAbbrs, isSending: false, otherSideSalary: ourSalaryTotal);
        teamsRow.Add(rightPanel);

        body.Add(teamsRow);

        // ── Analysis section ──
        var analysisSection = BuildTradeAnalysis(ourPlayers, theirPlayers, aiTeam);
        body.Add(analysisSection);
        box.Add(body);

        // ── Buttons ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var rejectBtn = new Button();
        rejectBtn.text = LocalizationService.T("ui.code.dashboard_controller.rechazar_oferta");
        rejectBtn.AddToClassList("btn-modal-confirm");
        rejectBtn.AddToClassList("btn-modal-confirm--danger");
        rejectBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 2);
            _firedOverlay.style.display = DisplayStyle.None;
            if (_fastSimOfferPauseActive)
            {
                _tradeOfferModalResolved = true;
                _tradeOfferAccepted = false;
            }
            else
                ShowNextPendingTradeOffer();
        });
        actions.Add(rejectBtn);

        var acceptBtn = new Button();
        acceptBtn.text = LocalizationService.T("ui.code.dashboard_controller.aceptar_intercambio");
        acceptBtn.AddToClassList("btn-modal-confirm");
        acceptBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // ── Revalidación obligatoria antes de mutar ──
            // La oferta se generó cuando la plantilla/nómina eran otras; entre medias
            // el jugador pudo fichar/cortar. Sin revalidar se pueden crear plantillas
            // >17 o traspasos que incumplen el salary matching / apron.
            {
                var errorsChk = ValidatePendingTradeOffer(offer, ourPlayers, theirPlayers, ourSATFAs);

                if (errorsChk.Count > 0)
                {
                    DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 2);
                    _firedOverlay.style.display = DisplayStyle.None;
                    // El callback resuelve la espera de la simulación rápida: sin él la
                    // corrutina se quedaba en el WaitUntil y el juego quedaba bloqueado.
                    ShowOfferResultModal("TRASPASO NO VÁLIDO",
                        "La oferta ya no es válida con tu plantilla actual:\n\n" + string.Join("\n", errorsChk), -1,
                        onFastSimResolved: goToQuinteto =>
                        {
                            _tradeOfferModalResolved = true;
                            _tradeOfferAccepted = false;
                            _tradeOfferGoToQuinteto = goToQuinteto;
                        });
                    return;
                }
            }

            // S&T: firmar los FA propios antes de traspazarlos a la IA
            if (ourSATFAs != null && ourSATFAs.Count > 0)
            {
                var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
                foreach (var fa in ourSATFAs)
                {
                    long myPayroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(_myTeam.id, fa.id);
                    var breakdown = RosterController.GetMaxOfferBreakdown(fa, leagueSettings, myPayroll, true);
                    long market = DatabaseManager.Instance.EstimateMarketSalary(fa.GetCalculatedAverage(), leagueSettings != null ? leagueSettings.salary_cap : TradeHelper.SALARY_CAP);
                    long signSalary = breakdown.birdMax > 0 ? breakdown.birdMax : market;
                    int years = fa.age > 35 ? 1 : fa.age > 32 ? 2 : fa.age > 28 ? 3 : fa.age > 25 ? 4 : 5;
                    fa.team_id = _myTeam.id;
                    fa.last_team_id = _myTeam.id;
                    fa.salary = signSalary;
                    fa.contract_years = years;
                    fa.guaranteed_years = years;
                    fa.seasons_with_team = 1;
                    DatabaseManager.Instance.AssignJerseyNumber(fa, _myTeam.id);
                    DatabaseManager.Instance.UpdatePlayer(fa);
                    DatabaseManager.Instance.InsertTrade(new TradeData
                    {
                        season_id = _season?.id ?? 0,
                        game_day = _season?.current_game_day ?? 0,
                        game_date = _season?.current_date ?? now,
                        team_id_from = 0,
                        team_id_to = _myTeam.id,
                        player_id = fa.id,
                        trade_type = "free_agent"
                    });
                }
                if (_myTeam.first_apron_hard_capped == 0)
                {
                    _myTeam.first_apron_hard_capped = 1;
                    DatabaseManager.Instance.UpdateTeam(_myTeam);
                }
            }

            foreach (var p in ourPlayers)
            {
                DatabaseManager.Instance.MovePlayerToTeam(p, offer.team_id_from);
                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = _season?.id ?? 0,
                    game_day = _season?.current_game_day ?? 0,
                    game_date = _season?.current_date ?? now,
                    team_id_from = _myTeam.id,
                    team_id_to = offer.team_id_from,
                    player_id = p.id,
                    trade_type = (ourSATFAs != null && ourSATFAs.Any(x => x.id == p.id)) ? "sign_and_trade" : "trade"
                });
            }

            foreach (var p in theirPlayers)
            {
                DatabaseManager.Instance.MovePlayerToTeam(p, _myTeam.id);
                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = _season?.id ?? 0,
                    game_day = _season?.current_game_day ?? 0,
                    game_date = _season?.current_date ?? now,
                    team_id_from = offer.team_id_from,
                    team_id_to = _myTeam.id,
                    player_id = p.id,
                    trade_type = "trade"
                });
            }

            foreach (var p in ourPlayers)
            {
                DatabaseManager.Instance.AssignJerseyNumber(p, offer.team_id_from);
                DatabaseManager.Instance.UpdatePlayer(p);
            }

            foreach (var p in theirPlayers)
            {
                DatabaseManager.Instance.AssignJerseyNumber(p, _myTeam.id);
                DatabaseManager.Instance.UpdatePlayer(p);
            }

            // Apply projected chemistry from the analysis modal
            DatabaseManager.Instance.UpdateTeamChemistry(_myTeam.id, _pendingTradeChem);
            _myTeam.team_chemistry = _pendingTradeChem;
            var headerChem = _root.Q<Label>("HeaderChemistry");
            if (headerChem != null)
            {
                headerChem.text = $"{_pendingTradeChem}%";
                headerChem.RemoveFromClassList("header-stat-value--gold");
                headerChem.RemoveFromClassList("header-stat-value--negative");
                if (_pendingTradeChem < 40)
                    headerChem.AddToClassList("header-stat-value--negative");
                else if (_pendingTradeChem < 70)
                    headerChem.AddToClassList("header-stat-value--gold");
            }

            // Update header payroll & margin
            var leagueSet = DatabaseManager.Instance.GetLeagueSettings();
            long cap = leagueSet?.salary_cap ?? TradeHelper.SALARY_CAP;
            long prevPayroll = _players.Sum(x => x.salary);
            long salIn = theirPlayers.Sum(p => p.salary);
            long salOut = ourPlayers.Sum(p => p.salary);
            long newTotalPayroll = prevPayroll - salOut + salIn;
            long newMargin = cap - newTotalPayroll;
            _headerPayroll.text = $"${newTotalPayroll / 1_000_000}M";
            string newMarginText = newMargin >= 0
                ? $"+${newMargin / 1_000_000}M"
                : $"-${Mathf.Abs((int)(newMargin / 1_000_000))}M";
            _headerMargin.text = newMarginText;
            _headerMargin.RemoveFromClassList("header-stat-value--negative");
            if (newMargin < 0)
                _headerMargin.AddToClassList("header-stat-value--negative");

            // Transfer picks
            if (ourPicks != null && ourPicks.Count > 0)
            {
                var ids = ourPicks.Select(p => p.id).ToList();
                DatabaseManager.Instance.TransferDraftPicks(ids, _myTeam.id, offer.team_id_from);
                foreach (var pk in ourPicks)
                {
                    DatabaseManager.Instance.InsertTrade(new TradeData
                    {
                        season_id = _season?.id ?? 0,
                        game_day = _season?.current_game_day ?? 0,
                        game_date = _season?.current_date ?? now,
                        team_id_from = _myTeam.id,
                        team_id_to = offer.team_id_from,
                        player_id = 0,
                        pick_id = pk.id,
                        trade_type = "pick_trade"
                    });
                }
            }
            if (theirPicks != null && theirPicks.Count > 0)
            {
                var ids = theirPicks.Select(p => p.id).ToList();
                DatabaseManager.Instance.TransferDraftPicks(ids, offer.team_id_from, _myTeam.id);
                foreach (var pk in theirPicks)
                {
                    DatabaseManager.Instance.InsertTrade(new TradeData
                    {
                        season_id = _season?.id ?? 0,
                        game_day = _season?.current_game_day ?? 0,
                        game_date = _season?.current_date ?? now,
                        team_id_from = offer.team_id_from,
                        team_id_to = _myTeam.id,
                        player_id = 0,
                        pick_id = pk.id,
                        trade_type = "pick_trade"
                    });
                }
            }

            var ourNames = string.Join(", ", ourPlayers.Select(p => $"{p.first_name} {p.last_name}"));
            var theirNames = string.Join(", ", theirPlayers.Select(p => $"{p.first_name} {p.last_name}"));
            var ourPicksText = ourPicks != null && ourPicks.Count > 0
                ? " y " + string.Join(", ", ourPicks.Select(p => $"R{p.round} {(teamAbbrs.TryGetValue(p.original_team_id, out var a3) ? a3 : "???")}")) : "";
            var theirPicksText = theirPicks != null && theirPicks.Count > 0
                ? " y " + string.Join(", ", theirPicks.Select(p => $"R{p.round} {(teamAbbrs.TryGetValue(p.original_team_id, out var a4) ? a4 : "???")}")) : "";

            var satMsg = (ourSATFAs != null && ourSATFAs.Count > 0)
                ? $" (Sign & Trade: {string.Join(", ", ourSATFAs.Select(x => $"{x.first_name} {x.last_name}"))} firmado(s) con Bird rights y traspasado(s))."
                : "";
            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 1,
                sender_id = 0,
                title = "Intercambio aceptado",
                body = NewsTexts.Intercambio(teamName, ourNames + ourPicksText, _myTeam.name, theirNames + theirPicksText, ""),
                game_day = _season.current_game_day,
                game_date = now,
                created_at = now,
                date_sent = now,
                is_read = 0,
                category = NewsCategory.TRASPASOS,
                news_type = NewsType.INTERCAMBIO
            });

            DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 1);
            _firedOverlay.style.display = DisplayStyle.None;
            if (_fastSimOfferPauseActive)
            {
                _tradeOfferModalResolved = true;
                _tradeOfferAccepted = true;
            }
            else
                ShowNextPendingTradeOffer();
        });
        actions.Add(acceptBtn);
        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(rejectBtn);
            CursorManager.Instance.RegisterHandCursor(acceptBtn);
        }
    }

    // ═══════════════════════════════════════════
    //  TRADE OFFER MODAL — BUILDERS
    // ═══════════════════════════════════════════

    VisualElement BuildTradeTeamPanel(TeamData team, List<PlayerData> players,
                                       List<DraftPickData> picks, Dictionary<int, string> teamAbbrs,
                                       bool isSending, long otherSideSalary = 0)
    {
        var panel = new VisualElement();
        panel.AddToClassList("trade-modal-team-panel");

        // Team header: logo + name + action label
        var teamHeader = new VisualElement();
        teamHeader.AddToClassList("trade-modal-team-header");

        var logo = new VisualElement();
        logo.AddToClassList("trade-modal-team-logo");
        if (team != null && !string.IsNullOrEmpty(team.logo))
            SetTeamLogo(logo, team.logo, "64x64");
        teamHeader.Add(logo);

        var teamInfo = new VisualElement();
        teamInfo.AddToClassList("trade-modal-team-info");

        var nameLbl = new Label(team != null ? team.name.ToUpper() : "?");
        nameLbl.AddToClassList("trade-modal-team-name");
        teamInfo.Add(nameLbl);

        var actionLbl = new Label(LocalizationService.T("ui.code.dashboard_controller.envia"));
        actionLbl.AddToClassList("trade-modal-team-action");
        teamInfo.Add(actionLbl);

        teamHeader.Add(teamInfo);
        panel.Add(teamHeader);

        // Player cards
        foreach (var p in players)
        {
            var card = BuildTradePlayerCard(p);
            panel.Add(card);
        }

        // Picks
        if (picks != null && picks.Count > 0)
        {
            var picksSection = new VisualElement();
            picksSection.AddToClassList("trade-modal-picks-section");
            var allTeams = DatabaseManager.Instance.GetAllTeams();
            foreach (var pk in picks.OrderBy(p => p.draft_year).ThenBy(p => p.round))
            {
                var origTeam = allTeams.FirstOrDefault(t => t.id == pk.original_team_id);
                string teamName = origTeam != null ? origTeam.name : "???";
                string pickText = $"Ronda {pk.round} {pk.draft_year} - {teamName}";
                // Si el pick cambió de dueño, indicar también el equipo actual (la
                // posición la marca el equipo original).
                if (pk.current_team_id != pk.original_team_id)
                {
                    var curTeam = allTeams.FirstOrDefault(t => t.id == pk.current_team_id);
                    if (curTeam != null) pickText += $" ({curTeam.name})";
                }
                var pickLbl = new Label(pickText);
                pickLbl.AddToClassList("trade-modal-picks-label");
                picksSection.Add(pickLbl);
            }
            panel.Add(picksSection);
        }

        // Salary summary bar
        var salaryBar = new VisualElement();
        salaryBar.AddToClassList("trade-modal-salary-bar");

        long totalSalary = players.Sum(p => p.salary);
        var salaryItem = new VisualElement();
        salaryItem.AddToClassList("trade-modal-salary-item");

        var salaryLabel = new Label(isSending ? "SALARIO TOTAL SALIENTE" : "SALARIO TOTAL ENTRANTE");
        salaryLabel.AddToClassList("trade-modal-salary-label");
        salaryItem.Add(salaryLabel);

        var salaryValue = new Label(FormatMoneyShort(totalSalary));
        salaryValue.AddToClassList("trade-modal-salary-value");
        salaryValue.AddToClassList("trade-modal-salary-value--positive");
        salaryItem.Add(salaryValue);
        salaryBar.Add(salaryItem);

        // Cap room resultante — each team's own cap room
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;

        if (isSending)
        {
            // My team's cap room after sending these players and receiving the other side
            long myCurrentPayroll = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id).Sum(x => x.salary);
            long myCapRoom = (salaryCap - myCurrentPayroll) + totalSalary - otherSideSalary;
            long capRoomAfter = myCapRoom;

            var capItem = new VisualElement();
            capItem.AddToClassList("trade-modal-salary-item");

            var capLabel = new Label(LocalizationService.T("ui.code.dashboard_controller.cap_room_resultante"));
            capLabel.AddToClassList("trade-modal-salary-label");
            capItem.Add(capLabel);

            string capText = (capRoomAfter >= 0 ? "+" : "-") + FormatMoneyShort(System.Math.Abs(capRoomAfter));
            var capValue = new Label(capText);
            capValue.AddToClassList("trade-modal-salary-value");
            capValue.AddToClassList(capRoomAfter >= 0
                ? "trade-modal-salary-value--positive"
                : "trade-modal-salary-value--negative");
            capItem.Add(capValue);
            salaryBar.Add(capItem);
        }
        else
        {
            // AI team's cap room after receiving these players and sending the other side
            long aiCurrentPayroll = DatabaseManager.Instance.GetPlayersByTeam(team.id).Sum(x => x.salary);
            long aiCapRoom = (salaryCap - aiCurrentPayroll) + totalSalary - otherSideSalary;
            long capRoomAfter = aiCapRoom;

            var capItem = new VisualElement();
            capItem.AddToClassList("trade-modal-salary-item");

            var capLabel = new Label(LocalizationService.T("ui.code.dashboard_controller.cap_room_resultante"));
            capLabel.AddToClassList("trade-modal-salary-label");
            capItem.Add(capLabel);

            string capText = (capRoomAfter >= 0 ? "+" : "-") + FormatMoneyShort(System.Math.Abs(capRoomAfter));
            var capValue = new Label(capText);
            capValue.AddToClassList("trade-modal-salary-value");
            capValue.AddToClassList(capRoomAfter >= 0
                ? "trade-modal-salary-value--positive"
                : "trade-modal-salary-value--negative");
            capItem.Add(capValue);
            salaryBar.Add(capItem);
        }

        panel.Add(salaryBar);

        return panel;
    }

    VisualElement BuildTradePlayerCard(PlayerData p)
    {
        var card = new VisualElement();
        card.AddToClassList("trade-modal-player-card");

        // Photo
        var photo = new VisualElement();
        photo.AddToClassList("trade-modal-player-photo");
        Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
        if (tex != null)
            photo.style.backgroundImage = new StyleBackground(tex);
        card.Add(photo);

        // Info
        var info = new VisualElement();
        info.AddToClassList("trade-modal-player-info");

        var nameLbl = new Label($"{p.first_name} {p.last_name}");
        nameLbl.AddToClassList("trade-modal-player-name");
        info.Add(nameLbl);

        string stars = p.overall >= 85 ? " ★★★" : p.overall >= 75 ? " ★★" : p.overall >= 65 ? " ★" : "";
        var metaLbl = new Label($"{PositionCodes.GetShort(p.position)} | {p.age} años");
        metaLbl.AddToClassList("trade-modal-player-meta");
        info.Add(metaLbl);

        string yearsText = p.contract_years == 1 ? "1 AÑO RESTANTE" : $"{p.contract_years} AÑOS RESTANTES";
        var contractLbl = new Label($"{SalaryStr(p.salary)}  ·  {yearsText}");
        contractLbl.AddToClassList("trade-modal-player-contract");
        info.Add(contractLbl);

        card.Add(info);

        // OVR column
        var ovrCol = new VisualElement();
        ovrCol.AddToClassList("trade-modal-player-ovr-col");

        var ovrLbl = new Label(p.overall.ToString());
        ovrLbl.AddToClassList("trade-modal-player-ovr");
        if (p.overall >= 80)
            ovrLbl.style.color = new StyleColor(new Color32(39, 174, 96, 255));
        else if (p.overall >= 60)
            ovrLbl.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        else
            ovrLbl.style.color = new StyleColor(new Color32(192, 57, 43, 255));
        ovrCol.Add(ovrLbl);

        var ovrSub = new Label(LocalizationService.T("ui.code.dashboard_controller.med"));
        ovrSub.AddToClassList("trade-modal-player-ovr-label");
        ovrCol.Add(ovrSub);

        card.Add(ovrCol);

        return card;
    }

    VisualElement BuildTradeAnalysis(List<PlayerData> ourPlayers, List<PlayerData> theirPlayers, TeamData aiTeam)
    {
        var section = new VisualElement();
        section.AddToClassList("trade-modal-analysis");

        var titleLbl = new Label(LocalizationService.T("ui.code.dashboard_controller.analisis_de_la_operacion"));
        titleLbl.AddToClassList("trade-modal-analysis-title");
        section.Add(titleLbl);

        var row = new VisualElement();
        row.AddToClassList("trade-modal-analysis-row");

        // ── 1. Impacto de Equipo ──
        var impactCard = new VisualElement();
        impactCard.AddToClassList("trade-modal-analysis-card");

        var impactLabel = new Label(LocalizationService.T("ui.code.dashboard_controller.impacto_de_equipo"));
        impactLabel.AddToClassList("trade-modal-analysis-label");
        impactCard.Add(impactLabel);

        // Compute net OVR change
        float ourAvg = ourPlayers.Count > 0 ? (float)ourPlayers.Average(p => p.overall) : 0;
        float theirAvg = theirPlayers.Count > 0 ? (float)theirPlayers.Average(p => p.overall) : 0;
        int netImpact = Mathf.RoundToInt(theirAvg - ourAvg);
        // Factor in roster depth change
        int depthDelta = theirPlayers.Count - ourPlayers.Count;

        string impactText = (netImpact >= 0 ? "+" : "") + netImpact.ToString();
        var impactValue = new Label(impactText);
        impactValue.AddToClassList("trade-modal-analysis-value");
        impactValue.AddToClassList(netImpact > 0
            ? "trade-modal-analysis-value--positive"
            : netImpact < 0
                ? "trade-modal-analysis-value--negative"
                : "trade-modal-analysis-value--neutral");
        impactCard.Add(impactValue);

        string impactDesc = netImpact > 0
            ? "Mejora la calidad de la plantilla"
            : netImpact < 0
                ? "Pierdes calidad media"
                : "Calidad similar";
        if (depthDelta > 0) impactDesc += " y profundidad";
        else if (depthDelta < 0) impactDesc += " pero pierdes profundidad";

        var impactDescLbl = new Label(impactDesc);
        impactDescLbl.AddToClassList("trade-modal-analysis-desc");
        impactCard.Add(impactDescLbl);

        row.Add(impactCard);

        // ── 2. Química del Equipo ──
        var chemCard = new VisualElement();
        chemCard.AddToClassList("trade-modal-analysis-card");

        var chemLabel = new Label(LocalizationService.T("ui.code.dashboard_controller.quimica_del_equipo"));
        chemLabel.AddToClassList("trade-modal-analysis-label");
        chemCard.Add(chemLabel);

        int currentChem = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        float avgMoraleIn = theirPlayers.Count > 0 ? (float)theirPlayers.Average(p => p.morale) : 50;
        float avgMoraleOut = ourPlayers.Count > 0 ? (float)ourPlayers.Average(p => p.morale) : 50;
        int chemShift = Mathf.RoundToInt((avgMoraleIn - avgMoraleOut) * 0.15f);
        int projectedChem = Mathf.Clamp(currentChem + chemShift, 0, 100);
        _pendingTradeChem = projectedChem;

        var chemCircle = new VisualElement();
        chemCircle.AddToClassList("trade-modal-chem-circle");
        var chemValue = new Label($"{projectedChem}%");
        chemValue.AddToClassList("trade-modal-chem-value");
        chemCircle.Add(chemValue);
        chemCard.Add(chemCircle);

        string chemDesc;
        if (chemShift > 2) chemDesc = "Mejora notable";
        else if (chemShift > 0) chemDesc = "Ligera mejora";
        else if (chemShift == 0) chemDesc = "Sin cambios significativos";
        else if (chemShift > -3) chemDesc = "Ligero deterioro";
        else chemDesc = "Deterioro notable";

        var chemDescLbl = new Label(chemDesc);
        chemDescLbl.AddToClassList("trade-modal-analysis-desc");
        chemCard.Add(chemDescLbl);

        row.Add(chemCard);

        // ── 3. Flexibilidad Futura ──
        var flexCard = new VisualElement();
        flexCard.AddToClassList("trade-modal-analysis-card");

        var flexLabel = new Label(LocalizationService.T("ui.code.dashboard_controller.flexibilidad_futura"));
        flexLabel.AddToClassList("trade-modal-analysis-label");
        flexCard.Add(flexLabel);

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long currentPayroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(_myTeam.id);
        long salaryIn = theirPlayers.Sum(p => p.salary);
        long salaryOut = ourPlayers.Sum(p => p.salary);
        long newPayroll = currentPayroll - salaryOut + salaryIn;
        long currentCapRoom = salaryCap - currentPayroll;
        long newCapRoom = salaryCap - newPayroll;

        int flexScore = 0;
        if (newCapRoom > currentCapRoom) flexScore = 1;
        else if (newCapRoom < currentCapRoom) flexScore = -1;

        string flexText = (flexScore >= 0 ? "+" : "") + flexScore.ToString();
        var flexValue = new Label(flexText);
        flexValue.AddToClassList("trade-modal-analysis-value");
        flexValue.AddToClassList(flexScore > 0
            ? "trade-modal-analysis-value--positive"
            : flexScore < 0
                ? "trade-modal-analysis-value--negative"
                : "trade-modal-analysis-value--neutral");
        flexCard.Add(flexValue);

        long capDiff = newCapRoom - currentCapRoom;
        string flexDesc = capDiff > 0
            ? $"Ganas {FormatMoneyShort(capDiff)} de espacio salarial"
            : capDiff < 0
                ? $"Pierdes {FormatMoneyShort(-capDiff)} de espacio salarial"
                : "Sin cambio en espacio salarial";

        var flexDescLbl = new Label(flexDesc);
        flexDescLbl.AddToClassList("trade-modal-analysis-desc");
        flexCard.Add(flexDescLbl);

        row.Add(flexCard);

        section.Add(row);
        return section;
    }

    string FormatMoneyShort(long amount)
    {
        float millions = amount / 1_000_000f;
        return $"${millions:F2}M";
    }

    string SalaryStr(long salary)
    {
        return salary.ToString("N0").Replace(',', '.') + " $";
    }

    void ApplyRenovation(TeamData team)
    {
        var info = GetRenovationInfo(team.arena_renovation_type);
        if (info.name == "") return;

        if (team.capacity + info.capacityBonus > 50000) return;

        team.capacity += info.capacityBonus;
        team.arena_renovation_count++;

        // Increase facilities every 3 renovations
        if (team.arena_renovation_count >= 3 && team.facilities < 5)
        {
            team.facilities++;
            team.arena_renovation_count = 0;
        }

        // Deduct cost
        if (team.arena_renovation_cost > 0)
        {
            team.budget -= team.arena_renovation_cost;
            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = team.id,
                season_id = _season.id,
                record_type = FinanceRecord.TYPE_RENOVATION,
                game_day = _season.current_game_day,
                amount = team.arena_renovation_cost
            });
        }

        DatabaseManager.Instance.UpdateTeamBudget(team.id, team.budget);
        SyncMyTeamBudget(team);

        // Update team with renovation reset
        var dbTeam = DatabaseManager.Instance.GetTeamById(team.id);
        dbTeam.capacity = team.capacity;
        dbTeam.facilities = team.facilities;
        dbTeam.arena_renovation_count = team.arena_renovation_count;
        dbTeam.arena_renovation_end_day = 0;
        dbTeam.arena_renovation_type = "";
        dbTeam.arena_renovation_cost = 0;
        DatabaseManager.Instance.UpdateTeam(dbTeam);

        // Send message
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"Remodelación completada: {info.name}",
            body = NewsTexts.RemodelacionCompletada(_myTeam.name, info.name),
            game_day = _season.current_game_day,
            game_date = System.DateTime.Parse(_season.year_start + "-10-22").AddDays(_season.current_game_day - 1).ToString("yyyy-MM-dd"),
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = NewsType.REMODELACION_COMPLETADA
        });
    }

    (string name, int capacityBonus, long cost) GetRenovationInfo(string type)
    {
        return type switch
        {
            "general_seats" => ("Ampliar Grada General", 3000, 10_000_000),
            "tribune" => ("Ampliar Tribuna", 2000, 20_000_000),
            "vip_seats" => ("Ampliar Grada VIP", 1000, 35_000_000),
            _ => ("", 0, 0)
        };
    }

    void UpdatePlayersMoraleAfterGame(List<GameSimulator.PlayerStatSnapshot> stats, int teamId, bool teamWon)
    {
        // Get last 10 games for this team to calculate win%
        var teamGames = DatabaseManager.Instance.GetStandingsGames(_manager.id)
            .Where(g => (g.home_team_id == teamId || g.away_team_id == teamId) && g.is_played == 1)
            .OrderByDescending(g => g.game_day).Take(10).ToList();
        int winsInLast10 = teamGames.Count(g =>
            (g.home_team_id == teamId && g.home_score > g.away_score) ||
            (g.away_team_id == teamId && g.away_score > g.home_score));
        float winPct = teamGames.Count > 0 ? (float)winsInLast10 / teamGames.Count : 0.5f;

        bool isMyTeam = teamId == _myTeam.id;
        int gameDay = _season?.current_game_day ?? 0;
        string gameDate = _season?.current_date ?? "";

        int GetRoleDelta(PlayerRole role, float ratio)
        {
            return role switch
            {
                PlayerRole.Estrella => ratio >= 1.0f ? 2 : ratio >= 0.7f ? 1 : ratio >= 0.4f ? -1 : ratio > 0f ? -2 : -3,
                PlayerRole.Titular => ratio >= 1.0f ? 2 : ratio >= 0.7f ? 1 : ratio >= 0.4f ? 0 : ratio > 0f ? -1 : -2,
                PlayerRole.Banquillo => ratio >= 1.0f ? 2 : ratio >= 0.7f ? 1 : ratio >= 0.4f ? 0 : ratio > 0f ? 0 : -1,
                _ => ratio >= 1.0f ? 2 : ratio >= 0.7f ? 1 : 0
            };
        }

        float ExpectedMinForRole(PlayerRole role) => role switch
        {
            PlayerRole.Estrella => 40f,
            PlayerRole.Titular => 28f,
            PlayerRole.Banquillo => 10f,
            _ => 3f
        };

        void ProcessPlayer(PlayerData player, float minutes, float? avgRating, float expectedMinutes)
        {
            // Minutos esperados: los configurados en ROTACIÓN (target del snapshot) o,
            // si no hay, la tabla fija por rol.
            float expectedMin = expectedMinutes > 0f ? expectedMinutes : ExpectedMinForRole(player.role);
            float ratio = expectedMin > 0f ? minutes / expectedMin : 1f;
            int roleDelta = GetRoleDelta(player.role, ratio);
            if (teamWon) roleDelta = Mathf.Min(roleDelta + 1, 2);

            int formDelta = avgRating.HasValue
                ? (avgRating.Value >= 28f ? 2
                    : avgRating.Value >= 20f ? 1
                    : avgRating.Value >= 15f ? 0
                    : avgRating.Value >= 10f ? -1
                    : -2)
                : 0;

            int streakDelta = winPct >= 0.7f ? 1 : winPct <= 0.3f ? -1 : 0;
            int contractDelta = player.contract_years == 1 ? -1 : 0;
            int injuryDelta = player.injury_days > 0 ? -2 : 0;

            int totalDelta = Mathf.Clamp(roleDelta + formDelta + streakDelta + contractDelta + injuryDelta, -3, 3);
            int newMorale = Mathf.Clamp(player.morale + totalDelta, 0, 100);
            DatabaseManager.Instance.UpdatePlayerMorale(player.id, newMorale);

            // Generate complaint messages for my team players with very low morale
            if (isMyTeam && player.role != PlayerRole.UltimoRecurso && newMorale < 20)
            {
                int minGameDay = gameDay - 10;
                var recentMsg = DatabaseManager.Instance.Db.Table<MessageData>()
                    .FirstOrDefault(m => m.sender_id == player.id && m.game_day > minGameDay);
                if (recentMsg == null)
                {
                    string title, body;
                    if (newMorale < 10)
                    {
                        title = $"Queja: {player.first_name} {player.last_name}";
                        body = $"{player.first_name} {player.last_name} está furioso por la falta de minutos. Exige un traspaso inmediato.";
                    }
                    else
                    {
                        title = $"Preocupación: {player.first_name} {player.last_name}";
                        body = $"{player.first_name} {player.last_name} no está conforme con los minutos que está jugando. Espera tener más protagonismo teniendo en cuenta el rol que se le prometió.";
                    }
                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = player.id,
                        title = title,
                        body = newMorale < 10
                            ? NewsTexts.Queja(_myTeam.name, $"{player.first_name} {player.last_name} la falta de minutos")
                            : NewsTexts.Preocupacion(_myTeam.name, $"{player.first_name} {player.last_name} la falta de minutos"),
                        game_day = gameDay,
                        game_date = gameDate,
                        created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        is_read = 0,
                        category = NewsCategory.PRENSA,
                        news_type = newMorale < 10 ? NewsType.QUEJA : NewsType.PREOCUPACION
                    });
                }
            }
        }

        // ── Active players (12 que jugaron) ──
        var activeIds = new HashSet<int>();
        foreach (var ps in stats)
        {
            var player = DatabaseManager.Instance.GetPlayerById(ps.player_id);
            if (player == null) continue;
            activeIds.Add(player.id);

            var last5Stats = DatabaseManager.Instance.GetPlayerGameStats(player.id)
                .Where(s => s.minutes > 0)
                .OrderByDescending(s => s.game_id).Take(5).ToList();
            float avgRating = last5Stats.Count > 0 ? (float)last5Stats.Average(s => s.rating) : 15f;

            ProcessPlayer(player, ps.minutes, avgRating, ps.target_minutes);
        }

        // ── Inactive healthy players (no convocados pero sanos) ──
        var allTeamPlayers = DatabaseManager.Instance.GetPlayersByTeam(teamId);
        foreach (var player in allTeamPlayers)
        {
            if (player.injury_days > 0) continue;
            if (activeIds.Contains(player.id)) continue;
            ProcessPlayer(player, 0f, null, 0f);
        }
    }

    void UpdateFanConfidence(GameData game, GameSimulator.GameResult result)
    {
        bool isHome = game.home_team_id == _myTeam.id;
        int myScore = isHome ? result.home_score : result.away_score;
        int rivalScore = isHome ? result.away_score : result.home_score;
        bool won = myScore > rivalScore;
        int margin = Mathf.Abs(myScore - rivalScore);

        int change = 0;
        if (won)
        {
            change = isHome ? 4 : 2;
            if (margin <= 5) change += 1;  // close exciting win
            if (margin >= 20) change += 1; // dominant win
        }
        else
        {
            change = isHome ? -3 : -2;
            if (margin <= 5) change -= 1;  // close painful loss
            if (margin >= 20) change -= 1; // embarrassing blowout
        }

        _manager.fan_confidence = Mathf.Clamp(_manager.fan_confidence + change, 0, 100);
        DatabaseManager.Instance.SaveManager(_manager);
    }

    void CreateGameResultMessage(GameData game, GameSimulator.GameResult result)
    {
        bool isHome = game.home_team_id == _myTeam.id;
        int myScore = isHome ? result.home_score : result.away_score;
        int rivalScore = isHome ? result.away_score : result.home_score;
        bool won = myScore > rivalScore;

        // Rival name
        var rival = DatabaseManager.Instance.GetTeamById(isHome ? game.away_team_id : game.home_team_id);
        string rivalName = rival?.name ?? "Rival";

        // Home team and attendance (always shown)
        var homeTeam = DatabaseManager.Instance.GetTeamById(game.home_team_id);
        string arenaName = homeTeam?.arena ?? "Pabellón";

        // Read saved attendance from database (calculated by TeamEconomyService.ProcessGameFinances)
        var attendanceData = DatabaseManager.Instance.GetGameAttendance(game.id);
        int attendance = attendanceData?.attendance ?? 0;

        // Fallback: calculate if missing (e.g., older saves or preseason games)
        if (attendance == 0 && homeTeam != null)
        {
            attendance = TeamEconomyService.CalculateAttendance(game, homeTeam, _manager, _season);
        }

        // MVP of my team (most points)
        var myStats = isHome ? result.home_stats : result.away_stats;
        var mvp = myStats.OrderByDescending(s => s.points).FirstOrDefault();
        string mvpName = mvp != null ? $"{mvp.name}" : "";

        // Top scorer of the game
        var homeScorers = result.home_stats;
        var awayScorers = result.away_stats;
        var allStats = (homeScorers != null ? homeScorers.AsEnumerable() : Enumerable.Empty<GameSimulator.PlayerStatSnapshot>())
            .Concat(awayScorers != null ? awayScorers.AsEnumerable() : Enumerable.Empty<GameSimulator.PlayerStatSnapshot>());
        var topScorer = allStats.OrderByDescending(s => s.points).FirstOrDefault();
        string topScorerName = topScorer?.name ?? mvpName;
        int topScorerPts = topScorer?.points ?? 0;

        string body = NewsTexts.Resultado(_myTeam.name, myScore, rivalName, rivalScore,
            game.game_date ?? "", topScorerName, topScorerPts);
        body += $"\nPabellón: {arenaName} — Asistencia: {attendance:N0} espectadores.";
        if (!string.IsNullOrEmpty(mvpName))
            body += $"\nMVP del partido: {mvpName}.";

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"Resultado: {rivalName} ({myScore}-{rivalScore})",
            body = body,
            game_day = game.game_day,
            game_date = game.game_date,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = NewsType.RESULTADO
        });
    }

    void ProcessGameInjuries(GameSimulator.GameResult result, string gameDate)
    {
        if (result.injuries == null) return;
        foreach (var inj in result.injuries)
        {
            // Avisar solo de las lesiones de jugadores de mi equipo.
            {
                var player = DatabaseManager.Instance.GetPlayerById(inj.player_id);
                if (player != null && player.team_id == _myTeam.id)
                {
                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Lesión: {player.first_name} {player.last_name}",
                        body = NewsTexts.Lesion($"{player.first_name} {player.last_name}", inj.type, inj.days, false),
                        game_day = _season.current_game_day,
                        game_date = gameDate,
                        created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        is_read = 0,
                        category = NewsCategory.LESIONES,
                        news_type = NewsType.LESION
                    });
                }
            }
        }
    }

    void UpdateManagerStats(int gameDay)
    {
        if (_season == null) return;

        var db = DatabaseManager.Instance.Db;
        var myGames = db.Table<GameData>()
            .Where(g => g.manager_id == _manager.id
                     && g.season_id == _season.id
                     && g.game_type != "preseason"
                     // Excluir la G-League: sus ids de filial pueden coincidir
                     // numéricamente con el id de mi equipo NBA.
                     && g.game_type != GLeagueScheduleGenerator.TYPE_REGULAR
                     && g.game_type != GLeaguePostSeason.TYPE_PLAYOFF
                     && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id)
                     && g.is_played == 1)
            .OrderBy(g => g.game_day)
            .ThenBy(g => g.id)
            .ToList();

        // Las relaciones solo cambian tras un partido de mi equipo; si hoy mi
        // equipo no jugó (descanso, play-in/playoff de otros equipos o ya
        // eliminado), no se deben aplicar cambios.
        if (!myGames.Any(g => g.game_day == gameDay))
            return;

        // Trust: based on win percentage
        int wins = myGames.Count(g =>
            (g.home_team_id == _myTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == _myTeam.id && g.away_score > g.home_score));
        int losses = myGames.Count - wins;

        float winPct = myGames.Count > 0 ? (float)wins / myGames.Count : 0.5f;
        int trustChange = winPct > 0.5f ? 2 : winPct > 0.4f ? 1 : winPct < 0.2f ? -2 : -1;
        _manager.trust = Mathf.Clamp(_manager.trust + trustChange, 0, 100);

        // Morale: based on recent form (last 5 games)
        var last5 = myGames.OrderByDescending(g => g.game_day).Take(5).ToList();
        int recentWins = last5.Count(g =>
            (g.home_team_id == _myTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == _myTeam.id && g.away_score > g.home_score));
        int moraleChange = recentWins >= 4 ? 3 : recentWins >= 3 ? 1 : recentWins <= 1 ? -2 : 0;
        _manager.morale = Mathf.Clamp(_manager.morale + moraleChange, 0, 100);

        DatabaseManager.Instance.SaveManager(_manager);
    }

}
