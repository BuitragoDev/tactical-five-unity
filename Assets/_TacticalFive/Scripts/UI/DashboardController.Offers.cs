using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

// B47: Ofertas maduradas, contraofertas de agente y modales de resultado.
public partial class DashboardController
{
    void ProcessMaturedOffers()
    {
        if (_season == null || _manager == null || _myTeam == null)
        {
            Debug.Log($"[Dashboard] ProcessMaturedOffers: skip null refs season={_season != null} manager={_manager != null} myTeam={_myTeam != null}");
            return;
        }

        Debug.Log($"[Dashboard] ProcessMaturedOffers: checking offers manager={_manager.id} current_day={_season.current_game_day}");
        var offers = DatabaseManager.Instance.GetMaturedUnprocessedOffers(_manager.id, _season.current_game_day);
        Debug.Log($"[Dashboard] ProcessMaturedOffers: found {offers.Count} matured offers");
        if (offers.Count == 0)
        {
            ShowNextPendingTradeOffer();
            return;
        }

        string resultSummary = "";
        int acceptedCount = 0;
        int rejectedCount = 0;
        string nowStr = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        bool hasRenewal = false;
        bool hasSigning = false;

        bool batchOk = false;
        string batchErrorMsg = null;
        var db = DatabaseManager.Instance.Db;
        db.BeginTransaction();
        try
        {
        bool agentCounterShown = false; // solo una contraoferta por tanda (el resto, al inbox)
        foreach (var offer in offers)
        {
            var player = DatabaseManager.Instance.GetPlayerById(offer.player_id);
            if (player == null)
            {
                DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                continue;
            }

            int gamesPlayed = DatabaseManager.Instance.GetPlayerGamesPlayedInSeason(player.id, _season.id);
            int teamChem = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
            float acceptScore = RosterController.CalculateAcceptScore(player, offer.offer_salary, offer.offer_years, gamesPlayed, teamChem);
            bool accepted = UnityEngine.Random.Range(1, 101) <= acceptScore;

            string playerName = $"{player.first_name} {player.last_name}";
            string salaryText = $"${offer.offer_salary / 1_000_000}M/año";
            string yearsText = FormatContractYears(offer);

            string FormatContractYears(OfferData o)
            {
                bool hasOpt = o.has_team_option == 1 || o.has_player_option == 1;
                if (!hasOpt) return $"{o.offer_years} año{(o.offer_years != 1 ? "s" : "")}";
                string optLabel = o.has_team_option == 1 ? "Team Option" : "Player Option";
                if (o.guaranteed_years == 0)
                    return $"{o.offer_years} año{(o.offer_years != 1 ? "s" : "")} ({optLabel})";
                return $"{o.guaranteed_years} año{(o.guaranteed_years != 1 ? "s" : "")} + {optLabel}";
            }

            if (offer.offer_type == 1)
            {
                // FREE AGENT SIGNING
                hasSigning = true;

                // Check if player is still a free agent
                if (player.team_id != 0)
                {
                    var signingTeam = DatabaseManager.Instance.GetTeamById(player.team_id);
                    string teamName = signingTeam?.name ?? "otro equipo";
                    rejectedCount++;
                    resultSummary += $"\u2717 {playerName}: FICHAJE CANCELADO \u2014 El jugador fich\u00f3 por {teamName}.\n";
                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Fichaje cancelado: {playerName}",
                        body = NewsTexts.FichajeCancelado(playerName, teamName),
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0,
                        category = NewsCategory.CLUB,
                        news_type = NewsType.FICHAJE_CANCELADO
                    });
                    DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                    continue;
                }

                if (accepted)
                {
                    if (TryAIMatchRestrictedOffer(player, offer, nowStr, out string matchedBy))
                    {
                        rejectedCount++;
                        resultSummary += $"✗ {playerName}: OFERTA IGUALADA por {matchedBy} — el jugador permanece con su equipo anterior.\n";
                        DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                        continue;
                    }

                    // Verificar por separado plazas estándar y contratos two-way.
                    var roster = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
                    int rosterCount = DatabaseManager.Instance.GetRosterCount(_myTeam.id);
                    bool isNewSigning = offer.offer_type == 1;
                    bool becomesStandard = offer.is_two_way == 0
                        && (isNewSigning || player.is_two_way == 1);
                    bool becomesTwoWay = offer.is_two_way == 1
                        && (isNewSigning || player.is_two_way != 1);
                    bool noStandardSlot = becomesStandard
                        && rosterCount >= TradeHelper.MAX_STANDARD_ROSTER;
                    bool noTwoWaySlot = becomesTwoWay
                        && DatabaseManager.Instance.GetTwoWayCount(_myTeam.id) >= TradeHelper.MAX_TWO_WAY;
                    if (noStandardSlot || noTwoWaySlot)
                    {
                        rejectedCount++;
                        player.renewal_cooldown_day = _season.current_game_day + 14;
                        DatabaseManager.Instance.UpdatePlayer(player);

                        resultSummary += $"✗ {playerName}: FICHAJE RECHAZADO (plantilla completa) — {salaryText} · {yearsText}\n";

                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = $"Fichaje rechazado: {playerName}",
                            body = NewsTexts.FichajeRechazado(playerName, _myTeam.name,
                                noTwoWaySlot ? $"las plazas two-way están completas ({TradeHelper.MAX_TWO_WAY})"
                                : $"las plazas estándar están completas ({TradeHelper.MAX_STANDARD_ROSTER})"),
                            game_day = _season.current_game_day,
                            game_date = nowStr,
                            created_at = nowStr,
                            date_sent = nowStr,
                            is_read = 0,
                            category = NewsCategory.CLUB,
                            news_type = NewsType.FICHAJE_RECHAZADO
                        });
                        DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                        continue;
                    }

                    // Verificar espacio salarial / excepciones (FA externo → sin Bird Rights)
                    long totalPayroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(_myTeam.id);
                    var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
                    long firstApron = leagueSettings?.apron > 0 ? leagueSettings.apron : TradeHelper.FIRST_APRON;
                    long secondApron = leagueSettings?.repeater_apron > 0 ? leagueSettings.repeater_apron : TradeHelper.SECOND_APRON;
                    long ntMle = leagueSettings?.mid_level > 0 ? leagueSettings.mid_level : TradeHelper.NT_MLE;
                    long tMle = leagueSettings?.taxpayer_mid_level > 0 ? leagueSettings.taxpayer_mid_level : TradeHelper.T_MLE;
                    long minSalary = TradeHelper.MinimumSalary(leagueSettings, player.years_of_service);
                    bool offerLegal = true;
                    string illegalReason = "";

                    if (leagueSettings != null)
                    {
                        bool proManagerOnly = _manager.game_mode == "promanager";
                        bool ownRecentFA = DatabaseManager.Instance.IsOwnRecentFA(player, _myTeam.id);
                        if (offer.is_two_way == 1)
                        {
                            // El límite de 2 two-way se valida en la maduración: la UI
                            // solo lo comprueba al abrir la oferta y se podían colar.
                            int currentTwoWayCount = DatabaseManager.Instance.GetTwoWayCount(_myTeam.id);
                            int existingTwoWay = player.team_id == _myTeam.id && player.is_two_way == 1 ? 1 : 0;
                            offerLegal = currentTwoWayCount - existingTwoWay < TradeHelper.MAX_TWO_WAY
                                      && TradeHelper.IsEligibleForTwoWay(player);
                            illegalReason = "ya tienes 2 contratos two-way (o el jugador no es elegible)";
                        }
                        else if (offer.offer_salary < minSalary)
                        {
                            offerLegal = false;
                            illegalReason = $"por debajo del mínimo de {player.years_of_service} año(s) de servicio (${minSalary:N0})";
                        }
                        else if (ownRecentFA)
                        {
                            // FA propio reciente: se mantienen los Bird Rights (la UI ya
                            // ofertaba con este máximo y la maduración los rechazaba).
                            var bd = RosterController.GetMaxOfferBreakdown(player, leagueSettings, totalPayroll, true, proManagerOnly);
                            long birdMax = bd.birdMax > 0 ? bd.birdMax : bd.capSpaceMax;
                            offerLegal = offer.offer_salary <= birdMax;
                            illegalReason = $"supera el máximo con Bird Rights (${birdMax:N0})";
                        }
                        else if (totalPayroll <= leagueSettings.salary_cap)
                        {
                            offerLegal = totalPayroll + offer.offer_salary <= leagueSettings.salary_cap;
                            illegalReason = "sin espacio salarial";
                        }
                        else if (proManagerOnly && totalPayroll <= firstApron)
                        {
                            offerLegal = offer.offer_salary <= tMle;
                            illegalReason = $"supera Mid-Level Exception (Taxpayer) (${tMle:N0}) — MODO PRO sin No-Taxpayer";
                        }
                        else if (totalPayroll <= firstApron)
                        {
                            offerLegal = offer.offer_salary <= ntMle;
                            illegalReason = $"supera Mid-Level Exception (No Taxpayer) (${ntMle:N0})";
                        }
                        else if (totalPayroll <= secondApron)
                        {
                            offerLegal = offer.offer_salary <= tMle;
                            illegalReason = $"supera Mid-Level Exception (Taxpayer) (${tMle:N0})";
                        }
                        else
                        {
                            offerLegal = offer.offer_salary <= minSalary;
                            illegalReason = $"supera salario mínimo (${minSalary:N0})";
                        }
                    }

                    if (!offerLegal)
                    {
                        rejectedCount++;
                        player.renewal_cooldown_day = _season.current_game_day + 14;
                        DatabaseManager.Instance.UpdatePlayer(player);

                        resultSummary += $"✗ {playerName}: FICHAJE RECHAZADO ({illegalReason}) — {salaryText} · {yearsText}\n";

                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = $"Fichaje rechazado: {playerName}",
                            body = NewsTexts.FichajeRechazado(playerName, _myTeam.name, illegalReason),
                            game_day = _season.current_game_day,
                            game_date = nowStr,
                            created_at = nowStr,
                            date_sent = nowStr,
                            is_read = 0,
                            category = NewsCategory.CLUB,
                            news_type = NewsType.FICHAJE_RECHAZADO
                        });
                        DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                        continue;
                    }

                    acceptedCount++;
                    player.team_id = _myTeam.id;
                    player.last_team_id = _myTeam.id;
                    player.salary = offer.offer_salary;
                    player.contract_years = offer.offer_years;
                    player.guaranteed_years = offer.guaranteed_years;
                    player.has_team_option = offer.has_team_option;
                    player.has_player_option = offer.has_player_option;
                    player.is_two_way = offer.is_two_way;
                    player.waived_game_day = -1;
                    player.seasons_with_team = 1;
                    DatabaseManager.Instance.AssignJerseyNumber(player, _myTeam.id);
                    DatabaseManager.Instance.UpdatePlayer(player);

                    AchievementService.EvaluateSignStarFA(_manager.id, _myTeam.id, _season?.id ?? 0, player);

                    DatabaseManager.Instance.InsertTrade(new TradeData
                    {
                        season_id = _season?.id ?? 0,
                        game_day = _season?.current_game_day ?? 0,
                        game_date = _season?.current_date ?? nowStr,
                        team_id_from = 0,
                        team_id_to = _myTeam.id,
                        player_id = player.id,
                        trade_type = "free_agent"
                    });

                    resultSummary += $"✓ {playerName}: FICHAJE REALIZADO — {salaryText} · {yearsText}\n";

                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Fichaje: {playerName}",
                        body = NewsTexts.Fichaje(playerName, _myTeam.name, (int)offer.offer_salary, offer.offer_years),
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0,
                        category = NewsCategory.CLUB,
                        news_type = NewsType.FICHAJE
                    });

                    // Activación del hard cap si se usó NT-MLE (>cap, ≤1er apron).
                    // En ProManager la NT-MLE no existe → el hard cap por NT-MLE no se activa.
                    if (_manager.game_mode != "promanager"
                        && _myTeam.first_apron_hard_capped == 0
                        && leagueSettings != null
                        && totalPayroll > leagueSettings.salary_cap
                        && totalPayroll <= firstApron)
                    {
                        _myTeam.first_apron_hard_capped = 1;
                        DatabaseManager.Instance.UpdateTeam(_myTeam);
                        string hardCapMsg = $"El fichaje de {playerName} se ha realizado usando la Mid-Level Exception (No Taxpayer). Tu equipo queda sujeto al hard cap del primer apron (${firstApron:N0}).";
                        resultSummary += $"\n⚠ {hardCapMsg}\n";
                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = "Hard cap activado",
                            body = NewsTexts.HardCap(playerName, _myTeam.name),
                            game_day = _season.current_game_day,
                            game_date = nowStr,
                            created_at = nowStr,
                            date_sent = nowStr,
                            is_read = 0,
                            category = NewsCategory.TRASPASOS,
                            news_type = NewsType.HARD_CAP
                        });
                    }
                }
                else
                {
                    rejectedCount++;
                    player.renewal_cooldown_day = _season.current_game_day + 14;
                    DatabaseManager.Instance.UpdatePlayer(player);

                    resultSummary += $"✗ {playerName}: FICHAJE RECHAZADO — {salaryText} · {yearsText}\n";

                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Fichaje rechazado: {playerName}",
                        body = NewsTexts.FichajeRechazado(playerName, _myTeam.name, $"oferta de {salaryText} durante {yearsText}") +
                               " Podrás intentarlo de nuevo dentro de 14 días.",
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0,
                        category = NewsCategory.CLUB,
                        news_type = NewsType.FICHAJE_RECHAZADO
                    });

                    // Contraoferta del agente (si queda margen legal y no es two-way).
                    // El máximo legal se recalcula aquí: las variables de cap del bloque
                    // anterior viven en otro ámbito.
                    if (offer.is_two_way != 1 && !agentCounterShown)
                    {
                        var lsNegFA = DatabaseManager.Instance.GetLeagueSettings();
                        if (lsNegFA != null)
                        {
                            long payrollFA = DatabaseManager.Instance.GetEffectiveTeamPayroll(_myTeam.id);
                            long capFA = lsNegFA.salary_cap;
                            long apron1FA = lsNegFA.apron > 0 ? lsNegFA.apron : TradeHelper.FIRST_APRON;
                            long apron2FA = lsNegFA.repeater_apron > 0 ? lsNegFA.repeater_apron : TradeHelper.SECOND_APRON;
                            long ntMleFA = lsNegFA.mid_level > 0 ? lsNegFA.mid_level : TradeHelper.NT_MLE;
                            long tMleFA = lsNegFA.taxpayer_mid_level > 0 ? lsNegFA.taxpayer_mid_level : TradeHelper.T_MLE;
                            long minFA = TradeHelper.MinimumSalary(lsNegFA, player.years_of_service);
                            bool proFA = _manager.game_mode == "promanager";

                            long legalMax = payrollFA <= capFA ? Math.Max(0, capFA - payrollFA)
                                          : (proFA && payrollFA <= apron1FA) ? tMleFA
                                          : payrollFA <= apron1FA ? ntMleFA
                                          : payrollFA <= apron2FA ? tMleFA
                                          : minFA;

                            long counter = NegotiationHelper.CounterSalary(offer.offer_salary, (int)acceptScore, legalMax);
                            if (counter > offer.offer_salary)
                            {
                                agentCounterShown = true;
                                ShowAgentCounterModal(player, counter, offer.offer_years, isRenewal: false);
                            }
                        }
                    }
                }
            }
            else
            {
                // RENEWAL (offer_type == 0)
                hasRenewal = true;
                if (offer.is_two_way != 1)
                {
                    long renewalMinimum = TradeHelper.MinimumSalary(
                        DatabaseManager.Instance.GetLeagueSettings(), player.years_of_service);
                    if (offer.offer_salary < renewalMinimum)
                        accepted = false;
                }
                if (accepted)
                {
                    acceptedCount++;
                    player.salary = offer.is_two_way == 1
                        ? TradeHelper.TwoWaySalary(DatabaseManager.Instance.GetLeagueSettings())
                        : offer.offer_salary;
                    player.contract_years = offer.offer_years;
                    player.guaranteed_years = offer.guaranteed_years;
                    player.has_team_option = offer.has_team_option;
                    player.has_player_option = offer.has_player_option;
                    player.is_two_way = offer.is_two_way;
                    // Re-firma de un FA propio reciente (declinó su opción): devolverlo al equipo
                    if (player.team_id == 0 && DatabaseManager.Instance.IsOwnRecentFA(player, _myTeam.id))
                    {
                        player.team_id = _myTeam.id;
                        player.last_team_id = _myTeam.id;
                        DatabaseManager.Instance.AssignJerseyNumber(player, _myTeam.id);
                    }
                    // Si como FA reciente firmó con otro equipo mientras maduraba, cancelar
                    else if (player.team_id != 0 && player.team_id != _myTeam.id)
                    {
                        rejectedCount++;
                        player.renewal_cooldown_day = _season.current_game_day + 15;
                        DatabaseManager.Instance.UpdatePlayer(player);
                        resultSummary += $"✗ {playerName}: RE-FIRMA CANCELADA — el jugador fichó por {DatabaseManager.Instance.GetTeamById(player.team_id)?.name ?? "otro equipo"}.\n";
                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = $"Re-firma cancelada: {playerName}",
                            body = NewsTexts.RefirmaCancelada(playerName, _myTeam.name),
                            game_day = _season.current_game_day,
                            game_date = nowStr,
                            created_at = nowStr,
                            date_sent = nowStr,
                            is_read = 0,
                            category = NewsCategory.CLUB,
                            news_type = NewsType.REFIRMA_CANCELADA
                        });
                        DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                        continue;
                    }
                    player.renewal_cooldown_day = _season.current_game_day + 365;
                    DatabaseManager.Instance.UpdatePlayer(player);

                    string reSignTag = player.team_id == _myTeam.id && player.last_team_id == _myTeam.id ? " RE-FIRMADO" : "";
                    resultSummary += $"✓ {playerName}: CONTRATO RENOVADO{reSignTag} — {salaryText} · {yearsText}\n";

                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Contrato renovado: {playerName}",
                        body = NewsTexts.Renovacion(playerName, _myTeam.name, (int)offer.offer_salary, offer.offer_years),
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0,
                        category = NewsCategory.CLUB,
                        news_type = NewsType.RENOVACION
                    });
                }
                else
                {
                    rejectedCount++;
                    player.renewal_cooldown_day = _season.current_game_day + 15;
                    DatabaseManager.Instance.UpdatePlayer(player);

                    resultSummary += $"✗ {playerName}: OFERTA RECHAZADA — {salaryText} · {yearsText}\n";

                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Oferta rechazada: {playerName}",
                        body = NewsTexts.OfertaRechazada(playerName, _myTeam.name),
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0,
                        category = NewsCategory.CLUB,
                        news_type = NewsType.OFERTA_RECHAZADA
                    });

                    // Contraoferta del agente (la re-firma propia puede pasar del cap,
                    // así que el techo es el valor de mercado del jugador).
                    if (offer.is_two_way != 1 && !agentCounterShown)
                    {
                        var lsNeg = DatabaseManager.Instance.GetLeagueSettings();
                        long capNeg = lsNeg != null ? lsNeg.salary_cap : TradeHelper.SALARY_CAP;
                        long market = DatabaseManager.Instance.EstimateMarketSalary(player.GetCalculatedAverage(), capNeg);
                        long legalMax = Math.Max(offer.offer_salary, market);
                        long counter = NegotiationHelper.CounterSalary(offer.offer_salary, (int)acceptScore, legalMax);
                        if (counter > offer.offer_salary)
                        {
                            agentCounterShown = true;
                            ShowAgentCounterModal(player, counter, offer.offer_years, isRenewal: true);
                        }
                    }
                }
            }

            DatabaseManager.Instance.MarkOfferProcessed(offer.id);
        }
        db.Commit();
        batchOk = true;
        }
        catch (System.Exception ex)
        {
        db.Rollback();
        batchErrorMsg = ex.Message;
        Debug.LogError($"[Dashboard] ProcessMaturedOffers error, batch rolled back: {ex.Message}\n{ex.StackTrace}");
        }

        if (!batchOk)
        {
            ShowOfferResultModal("ERROR AL PROCESAR OFERTAS", "Ha ocurrido un error al procesar las ofertas.\n\n" + batchErrorMsg, -1);
            return;
        }

        string title;
        int resultType;
        if (rejectedCount == 0)
        {
            if (acceptedCount == 1)
            {
                title = hasSigning && !hasRenewal ? "FICHAJE REALIZADO" : hasRenewal && !hasSigning ? "CONTRATO RENOVADO" : "OFERTA ACEPTADA";
            }
            else
            {
                title = hasSigning && !hasRenewal ? "FICHAJES REALIZADOS" : hasRenewal && !hasSigning ? "CONTRATOS RENOVADOS" : "OFERTAS ACEPTADAS";
            }
            resultType = 1;
        }
        else if (acceptedCount == 0)
        {
            if (rejectedCount == 1)
            {
                title = hasSigning && !hasRenewal ? "FICHAJE RECHAZADO" : hasRenewal && !hasSigning ? "OFERTA RECHAZADA" : "OFERTAS RECHAZADAS";
            }
            else
            {
                title = hasSigning && !hasRenewal ? "FICHAJES RECHAZADOS" : "OFERTAS RECHAZADAS";
            }
            resultType = -1;
        }
        else
        {
            title = "OFERTAS RESUELTAS";
            resultType = 0;
        }

        string text = acceptedCount + rejectedCount > 1
            ? $"Se han procesado {acceptedCount + rejectedCount} ofertas.\n\n{resultSummary}"
            : resultSummary;

        ShowOfferResultModal(title, text, resultType);
    }

    bool TryAIMatchRestrictedOffer(PlayerData player, OfferData offer, string nowStr, out string matchedBy)
    {
        matchedBy = null;
        if (player == null || offer == null || offer.is_two_way == 1
            || player.free_agent_type != "RFA" || player.last_team_id <= 0
            || player.last_team_id == _myTeam.id
            || !FreeAgencyRules.ShouldAIMatch(player.free_agent_type, player.GetCalculatedAverage(),
                offer.offer_salary, player.qualifying_offer_salary))
            return false;

        var rightsTeam = DatabaseManager.Instance.GetTeamById(player.last_team_id);
        if (rightsTeam == null || DatabaseManager.Instance.GetRosterCount(rightsTeam.id) >= TradeHelper.MAX_STANDARD_ROSTER)
            return false;

        var settings = DatabaseManager.Instance.GetLeagueSettings();
        long payrollWithoutHold = DatabaseManager.Instance.GetEffectiveTeamPayroll(rightsTeam.id, player.id);
        long projectedPayroll = payrollWithoutHold + offer.offer_salary;
        if (!TradeHelper.CanAIRenew(projectedPayroll, settings)) return false;
        if (rightsTeam.first_apron_hard_capped == 1)
        {
            long firstApron = settings?.apron > 0 ? settings.apron : TradeHelper.FIRST_APRON;
            if (projectedPayroll > firstApron) return false;
        }

        player.team_id = rightsTeam.id;
        player.last_team_id = rightsTeam.id;
        player.salary = offer.offer_salary;
        player.contract_years = offer.offer_years;
        player.guaranteed_years = offer.guaranteed_years;
        player.has_team_option = offer.has_team_option;
        player.has_player_option = offer.has_player_option;
        player.is_two_way = 0;
        player.free_agent_type = "";
        player.qualifying_offer_salary = 0;
        player.waived_game_day = -1;
        DatabaseManager.Instance.AssignJerseyNumber(player, rightsTeam.id);
        DatabaseManager.Instance.UpdatePlayer(player);
        DatabaseManager.Instance.InsertTrade(new TradeData
        {
            season_id = _season?.id ?? 0,
            game_day = _season?.current_game_day ?? 0,
            game_date = _season?.current_date ?? nowStr,
            team_id_from = 0,
            team_id_to = rightsTeam.id,
            player_id = player.id,
            trade_type = "free_agent"
        });
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"Oferta igualada: {player.first_name} {player.last_name}",
            body = $"{rightsTeam.name} ha ejercido sus derechos sobre {player.first_name} {player.last_name} e igualado tu oferta de {offer.offer_salary:N0} $ por {offer.offer_years} año(s).",
            game_day = _season?.current_game_day ?? 0,
            game_date = nowStr,
            created_at = nowStr,
            date_sent = nowStr,
            is_read = 0,
            category = NewsCategory.TRASPASOS,
            news_type = NewsType.FICHAJE_CANCELADO
        });
        matchedBy = rightsTeam.name;
        return true;
    }

    // ── NEGOCIACIÓN CON EL AGENTE ───────────────────────────────────
    /// <summary>Contraoferta del agente tras un rechazo por dinero: aceptarla ficha
    /// (o renueva) con las nuevas condiciones; retirarse deja al jugador como estaba.</summary>
    void ShowAgentCounterModal(PlayerData player, long counterSalary, int years, bool isRenewal)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.width = 760;
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.el_agente_pide_mas"));
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        var text = new Label();
        text.AddToClassList("modal-text");
        text.text = $"{player.first_name} {player.last_name} rechazó tu oferta, pero su agente cerraría por " +
                    $"${counterSalary:N0} durante {years} año{(years != 1 ? "s" : "")}.\n\n¿Aceptas las nuevas condiciones?";
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.marginTop = 18;

        var accept = new Button { text = LocalizationService.T("ui.code.dashboard_controller.aceptar") };
        accept.AddToClassList("btn-modal-confirm");
        accept.RegisterCallback<ClickEvent>(_ => { PlayClick(); AcceptAgentCounter(player, counterSalary, years, isRenewal); });
        CursorManager.Instance?.RegisterHandCursor(accept);
        actions.Add(accept);

        var reject = new Button { text = LocalizationService.T("ui.code.dashboard_controller.retirarse") };
        reject.AddToClassList("btn-modal-confirm");
        reject.AddToClassList("btn-modal-confirm--danger");
        reject.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
            Refresh();
        });
        CursorManager.Instance?.RegisterHandCursor(reject);
        actions.Add(reject);

        box.Add(actions);
    }

    void AcceptAgentCounter(PlayerData player, long counterSalary, int years, bool isRenewal)
    {
        _firedOverlay.style.display = DisplayStyle.None;
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string playerName = $"{player.first_name} {player.last_name}";

        if (!isRenewal && DatabaseManager.Instance.GetRosterCount(_myTeam.id) >= TradeHelper.MAX_STANDARD_ROSTER)
        {
            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 0,
                sender_id = 0,
                title = "Plantilla completa",
                body = $"No se pudo fichar a {playerName}: las plazas estándar están al máximo ({TradeHelper.MAX_STANDARD_ROSTER}).",
                game_day = _season.current_game_day,
                game_date = _season.current_date,
                created_at = now,
                date_sent = now,
                is_read = 0,
                category = NewsCategory.CLUB,
                news_type = NewsType.FICHAJE_RECHAZADO
            });
            Refresh();
            return;
        }

        player.salary = counterSalary;
        player.contract_years = years;
        player.guaranteed_years = years;
        player.has_team_option = 0;
        player.has_player_option = 0;

        if (!isRenewal)
        {
            player.is_two_way = 0;
            DatabaseManager.Instance.MovePlayerToTeam(player, _myTeam.id);
            DatabaseManager.Instance.AssignJerseyNumber(player, _myTeam.id);
            DatabaseManager.Instance.UpdatePlayer(player);
            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = _season?.id ?? 0,
                game_day = _season?.current_game_day ?? 0,
                game_date = _season?.current_date ?? now,
                team_id_from = 0,
                team_id_to = _myTeam.id,
                player_id = player.id,
                trade_type = "free_agent"
            });
            AchievementService.EvaluateSignStarFA(_manager.id, _myTeam.id, _season?.id ?? 0, player);
        }
        else
        {
            // Re-firma de un FA propio reciente: devolverlo al equipo.
            if (player.team_id == 0 && DatabaseManager.Instance.IsOwnRecentFA(player, _myTeam.id))
            {
                DatabaseManager.Instance.MovePlayerToTeam(player, _myTeam.id);
                DatabaseManager.Instance.AssignJerseyNumber(player, _myTeam.id);
            }
            DatabaseManager.Instance.UpdatePlayer(player);
        }

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"{(isRenewal ? "Contrato renovado" : "Fichaje")}: {playerName}",
            body = isRenewal
                ? NewsTexts.Renovacion(playerName, _myTeam.name, (int)counterSalary, years)
                : NewsTexts.Fichaje(playerName, _myTeam.name, (int)counterSalary, years),
            game_day = _season.current_game_day,
            game_date = _season.current_date,
            created_at = now,
            date_sent = now,
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = isRenewal ? NewsType.RENOVACION : NewsType.FICHAJE
        });

        Debug.Log($"[Negotiation] Contraoferta aceptada: {playerName} ${counterSalary:N0} x {years}");
        Refresh();
    }

    /// <summary>
    /// Modal de resultado de ofertas/errores. `onFastSimResolved` lo invocan los botones
    /// de la simulación rápida para liberar la espera de la corrutina correspondiente:
    /// sin él, cerrar el modal dejaba la simulación bloqueada para siempre.
    /// </summary>
    void ShowOfferResultModal(string title, string text, int resultType,
                              System.Action<bool> onFastSimResolved = null)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        // ── Box ──
        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.width = 800;
        Color32 borderColor = resultType == 1 ? new Color32(39, 174, 96, 255)
            : resultType == 0 ? new Color32(212, 160, 23, 255)
            : new Color32(192, 57, 43, 255);
        box.style.borderTopColor = new StyleColor(borderColor);
        box.style.borderBottomColor = new StyleColor(borderColor);
        box.style.borderLeftColor = new StyleColor(borderColor);
        box.style.borderRightColor = new StyleColor(borderColor);
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        string headerMod = resultType == 1 ? "fired-modal-header--green"
            : resultType == 0 ? "fired-modal-header--warning"
            : "";
        header.AddToClassList(headerMod);

        // El título lo decide quien llama ("TRASPASO NO VÁLIDO", "ERROR AL SIMULAR EL
        // DÍA"...). Antes se ignoraba y siempre se veía "OFERTAS RECHAZADAS".
        string subtitleText = !string.IsNullOrEmpty(title) ? title
            : resultType == 1 ? "OFERTAS ACEPTADAS"
            : resultType == 0 ? "HAY OFERTAS RECHAZADAS"
            : "OFERTAS RECHAZADAS";
        var subtitle = new Label(subtitleText);
        subtitle.AddToClassList("fired-modal-title");
        subtitle.style.color = new StyleColor(borderColor);
        header.Add(subtitle);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // ── Operation list ──
        var opList = new VisualElement();
        opList.AddToClassList("offer-result-modal-op-list");
        foreach (var rawLine in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            string line = rawLine.TrimStart();
            string badgeText = null;
            string cardMod = "neutral";
            string badgeMod = null;

            if (line.StartsWith("\u2713"))
            {
                line = line.Substring(1).TrimStart();
                badgeText = "ACEPTADA";
                cardMod = "positive";
                badgeMod = "positive";
            }
            else if (line.StartsWith("\u2717"))
            {
                line = line.Substring(1).TrimStart();
                badgeText = "RECHAZADA";
                cardMod = "negative";
                badgeMod = "negative";
            }
            else if (line.StartsWith("\u26A0"))
            {
                line = line.Substring(1).TrimStart();
                badgeText = "AVISO";
                cardMod = "warning";
                badgeMod = "warning";
            }

            var card = new VisualElement();
            card.AddToClassList("offer-result-modal-op-card");
            card.AddToClassList($"offer-result-modal-op-card--{cardMod}");

            var lineLabel = new Label(line);
            lineLabel.AddToClassList("offer-result-modal-op-text");
            card.Add(lineLabel);

            if (badgeText != null)
            {
                var badge = new VisualElement();
                badge.AddToClassList("offer-result-modal-op-badge");
                badge.AddToClassList($"offer-result-modal-op-badge--{badgeMod}");

                var badgeLbl = new Label(badgeText);
                badgeLbl.AddToClassList("offer-result-modal-op-badge-text");
                badgeLbl.AddToClassList($"offer-result-modal-op-badge-text--{badgeMod}");
                badge.Add(badgeLbl);
                card.Add(badge);
            }

            opList.Add(card);
        }
        body.Add(opList);
        box.Add(body);

        // ── Buttons ──
        if (_fastSimRunning && _fastSimOfferPauseActive)
        {
            var actions = new VisualElement();
            actions.AddToClassList("fired-modal-actions");

            var goToQuintetoBtn = new Button();
            goToQuintetoBtn.text = LocalizationService.T("ui.code.dashboard_controller.ir_a_quinteto");
            goToQuintetoBtn.AddToClassList("btn-modal-confirm");
            goToQuintetoBtn.AddToClassList("btn-modal-confirm--gold");
            goToQuintetoBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                _firedOverlay.style.display = DisplayStyle.None;
                _offerModalGoToQuinteto = true;
                _offerModalResolved = true;
                onFastSimResolved?.Invoke(true);
            });
            actions.Add(goToQuintetoBtn);

            var continueBtn = new Button();
            continueBtn.text = LocalizationService.T("ui.code.dashboard_controller.seguir_simulando");
            continueBtn.AddToClassList("btn-modal-cancel");
            continueBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                _firedOverlay.style.display = DisplayStyle.None;
                _offerModalGoToQuinteto = false;
                _offerModalResolved = true;
                onFastSimResolved?.Invoke(false);
            });
            actions.Add(continueBtn);

            box.Add(actions);

            if (CursorManager.Instance != null)
            {
                CursorManager.Instance.RegisterHandCursor(goToQuintetoBtn);
                CursorManager.Instance.RegisterHandCursor(continueBtn);
            }
        }
        else
        {
            var actions = new VisualElement();
            actions.AddToClassList("fired-modal-actions");

            var btn = new Button();
            btn.text = LocalizationService.T("ui.code.dashboard_controller.cerrar");
            btn.AddToClassList("btn-modal-cancel");
            btn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                _firedOverlay.style.display = DisplayStyle.None;
                // Aunque no haya simulación en curso, quien pasó un callback debe quedar
                // liberado (nunca dejar una espera sin resolver).
                onFastSimResolved?.Invoke(false);
                ShowNextPendingTradeOffer();
            });
            actions.Add(btn);

            box.Add(actions);

            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(btn);
        }
    }

    void ShowBudgetWarningModal(int num, bool fastSim = false)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderRightColor = new StyleColor(new Color32(212, 160, 23, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.aviso_financiero"));
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        var text = new Label(
            $"El presupuesto del club está en números rojos.\n\n" +
            $"Aviso {num} de 3."
        );
        text.AddToClassList("fired-modal-text");
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");
        var btn = new Button();
        btn.text = fastSim ? "IR A FINANZAS" : "CERRAR";
        btn.AddToClassList(fastSim ? "btn-modal-confirm" : "btn-modal-cancel");
        if (fastSim) btn.AddToClassList("btn-modal-confirm--gold");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            if (fastSim)
            {
                _fastSimStopRequested = true;
                HideLoading();
                _firedOverlay.style.display = DisplayStyle.None;
                ScreenManager.Instance.GoTo(GameScreen.Finances);
            }
            else
            {
                _firedOverlay.style.display = DisplayStyle.None;
            }
        });
        actions.Add(btn);
        box.Add(actions);
    }

    void ShowBudgetFiredModal()
    {
        MarkManagerFired();

        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.despido"));
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        var text = new Label(
            "La directiva ha decidido prescindir de tus servicios\n" +
            "debido a la mala gestión financiera del club."
        );
        text.AddToClassList("fired-modal-text");
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");
        var btn = new Button();
        btn.text = LocalizationService.T("ui.code.dashboard_controller.ir_al_menu_principal");
        btn.AddToClassList("btn-modal-confirm");
        btn.AddToClassList("btn-modal-confirm--danger");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        actions.Add(btn);
        box.Add(actions);
    }

}
