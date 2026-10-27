using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Mercado de la IA (F8-02): fichajes, renovaciones, buyout, S&amp;T, traspasos y
/// ofertas al usuario. La estrategia de cada equipo y el reloj del mercado se
/// inyectan desde el controller para mantener este servicio testeable y sin
/// depender de la pantalla.
/// </summary>
public sealed class AiMarketService
{
    readonly ManagerData _manager;
    readonly TeamData _myTeam;
    readonly SeasonData _season;
    readonly System.Random _aiRng;
    readonly Dictionary<int, int> _teamTradeCooldown;
    readonly Func<int, TeamStrategy> _strategyProvider;
    readonly Action _rebuildStrategies;
    readonly Func<TeamData, bool> _isContenderProvider;
    readonly Func<bool> _isDeadlineWeekProvider;

    public AiMarketService(ManagerData manager, TeamData myTeam, SeasonData season,
        System.Random aiRng, Dictionary<int, int> teamTradeCooldown,
        Func<int, TeamStrategy> strategyProvider, Action rebuildStrategies,
        Func<TeamData, bool> isContenderProvider, Func<bool> isDeadlineWeekProvider)
    {
        _manager = manager;
        _myTeam = myTeam;
        _season = season;
        _aiRng = aiRng;
        _teamTradeCooldown = teamTradeCooldown ?? new Dictionary<int, int>();
        _strategyProvider = strategyProvider;
        _rebuildStrategies = rebuildStrategies;
        _isContenderProvider = isContenderProvider;
        _isDeadlineWeekProvider = isDeadlineWeekProvider;
    }

    public static bool IsRenewalDue(SeasonData season)
    {
        if (season == null || string.IsNullOrEmpty(season.current_date)) return false;
        if (!DateTime.TryParse(season.current_date, out var date)) return false;
        return (date.Month == 2 && date.Day == 12 && date.Year == season.year_end)
            || date > new DateTime(season.year_end, 2, 12);
    }

    /// <summary>Ventana de traspasos: 1 de septiembre → 8 de febrero.</summary>
    public static bool IsTransferWindow(DateTime date, int yearStart, int yearEnd)
        => date >= new DateTime(yearStart, 9, 1) && date <= new DateTime(yearEnd, 2, 8);

    public TeamStrategy StrategyFor(int teamId) => GetTeamStrategy(teamId);
    public bool IsDeadlineWeekActive => IsDeadlineWeek();

    public bool ShouldProcessRenewals() => IsRenewalDue(_season);

    TeamStrategy GetTeamStrategy(int teamId)
        => _strategyProvider != null ? _strategyProvider(teamId) : TeamStrategy.Balanced;

    void BuildTeamStrategyCache() => _rebuildStrategies?.Invoke();

    bool IsDeadlineWeek() => _isDeadlineWeekProvider != null && _isDeadlineWeekProvider();

    bool IsTeamContender(TeamData team)
        => _isContenderProvider != null && _isContenderProvider(team);

    public void ProcessAITransfers(int gameDay)
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return;

        // Only run every ~10 game days (or 3-5 during deadline week)
        int lastDay = _season.last_ai_trade_day;
        int cooldownDays = IsDeadlineWeek() ? _aiRng.Next(3, 6) : 10;
        Debug.Log($"[AITrades] ProcessAITransfers called. gameDay={gameDay} lastDay={lastDay} diff={gameDay - lastDay}");
        if (gameDay - lastDay < cooldownDays) return;

        // Check if in transfer window (1 Sep year_start to 8 Feb year_end)
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return;
        if (!IsTransferWindow(date, _season.year_start, _season.year_end))
        {
            Debug.Log($"[AITrades] Outside transfer window: date={_season.current_date} " +
                      $"window={new System.DateTime(_season.year_start, 9, 1):dd/MMM} - " +
                      $"{new System.DateTime(_season.year_end, 2, 8):dd/MMM}");
            return;
        }

        _season.last_ai_trade_day = gameDay;
        Debug.Log($"[AITrades] Starting cycle. Date={_season.current_date} gameDay={gameDay}");

        BuildTeamStrategyCache();

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var seasonId = _season.id;
        var freeAgentsAll = DatabaseManager.Instance.GetFreeAgents();
        var tradedThisSeason = DatabaseManager.Instance.GetPlayersTradedThisSeason(seasonId);

        int teamsAttempted = 0, teamsTraded = 0, teamsFAd = 0;
        const int maxTrades = 3;

        foreach (var team in allTeams.OrderBy(_ => _aiRng.Next(0, 1000)))
        {
            if (team.id == _myTeam.id) continue;

            if (teamsTraded >= maxTrades) break;

            var strategy = GetTeamStrategy(team.id);

            // Densidad por estrategia: contender y rebuild se mueven más que balanced
            float moveChance = strategy switch
            {
                TeamStrategy.Contend => 0.45f,
                TeamStrategy.Rebuild => 0.40f,
                _ => 0.25f
            };
            if ((float)_aiRng.NextDouble() > moveChance) continue;

            // Cooldown por equipo según estrategia
            int teamCooldown = strategy switch
            {
                TeamStrategy.Contend => IsDeadlineWeek() ? 3 : 6,
                TeamStrategy.Rebuild => IsDeadlineWeek() ? 3 : 8,
                _ => IsDeadlineWeek() ? 5 : 15
            };
            int lastTeamTrade = _teamTradeCooldown.TryGetValue(team.id, out var lt) ? lt : -10000;
            if (gameDay - lastTeamTrade < teamCooldown) continue;
            _teamTradeCooldown[team.id] = gameDay;

            var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            if (roster.Count < 10) continue;

            var weakestPos = GetWeakestPosition(roster);
            if (weakestPos == null) continue;

            teamsAttempted++;

            // Rebuild: primero intenta vender veteranos (fire sale) para obtener jóvenes/picks
            if (strategy == TeamStrategy.Rebuild && TrySellVeteran(team, roster, seasonId, gameDay, tradedThisSeason))
            {
                teamsTraded++;
                continue;
            }

            // If roster < 12, sign free agent directly without trying trade
            if (roster.Count < 12)
            {
                Debug.Log($"[AITrades] {team.name} roster={roster.Count} < 12, direct FA at {weakestPos}");
                TrySignFreeAgent(team, roster, weakestPos, freeAgentsAll, seasonId, gameDay);
                teamsFAd++;
                continue;
            }

            bool traded = TryFindAITrade(team, roster, weakestPos, seasonId, gameDay, tradedThisSeason);

            if (traded)
            {
                teamsTraded++;
            }
            else if ((float)_aiRng.NextDouble() < 0.3f)
            {
                Debug.Log($"[AITrades] {team.name} trade failed, trying FA at {weakestPos}");
                TrySignFreeAgent(team, roster, weakestPos, freeAgentsAll, seasonId, gameDay);
                teamsFAd++;
            }
        }

        Debug.Log($"[AITrades] Cycle complete: {teamsAttempted} teams attempted, {teamsTraded} trades, {teamsFAd} FA signings");
        GenerateAITradeOffersForPlayer(gameDay, seasonId, tradedThisSeason);
    }

    // Fire sale de un equipo en reconstrucción: ofrece veteranos (edad >= 30, salario alto)
    // a cambio de jugadores jóvenes o picks de equipos contender/balanced.
    // EXENTO a propósito del filtro de valor (TradeValueHelper): en un fire sale el rebuild
    // regala talento a cambio de juventud/rondas; es su plan, no un error de valoración.
    bool TrySellVeteran(TeamData seller, List<PlayerData> sellerRoster, int seasonId, int gameDay, HashSet<int> tradedThisSeason)
    {
        var vets = sellerRoster
            .Where(p => p.injury_days == 0 && p.age >= 30 && p.overall < 87
                        && p.salary >= TradeHelper.MIN_SALARY
                        && !tradedThisSeason.Contains(p.id) && p.is_rookie == 0)
            .OrderByDescending(p => p.salary)
            .ToList();
        if (vets.Count == 0) return false;

        var buyers = DatabaseManager.Instance.GetAllTeams()
            .Where(t => t.id != _myTeam.id && t.id != seller.id)
            .OrderBy(_ => _aiRng.Next(0, 1000))
            .ToList();

        foreach (var vet in vets)
        {
            foreach (var buyer in buyers)
            {
                if (GetTeamStrategy(buyer.id) == TeamStrategy.Rebuild) continue;

                var buyerRoster = DatabaseManager.Instance.GetPlayersByTeam(buyer.id);
                if (buyerRoster.Count < 12) continue;

                // El comprador paga con jóvenes (edad < 25) baratos o con picks
                var young = buyerRoster
                    .Where(p => p.injury_days == 0 && p.age < 25 && p.salary <= vet.salary * 0.8f
                                && !tradedThisSeason.Contains(p.id) && p.is_rookie == 0)
                    .OrderBy(p => p.salary)
                    .ToList();
                if (young.Count == 0) continue;

                var candidate = young[0];
                var youngPack = new List<PlayerData> { candidate };
                if (young.Count >= 2 && (float)_aiRng.NextDouble() < 0.4f)
                    youngPack.Add(young[1]);

                var futurePicks = GetFuturePicksForTeam(buyer.id, true);
                var picks = new List<DraftPickData>();
                if (futurePicks.Count > 0 && (float)_aiRng.NextDouble() < 0.5f)
                {
                    picks.Add(futurePicks[0]);
                    if (futurePicks.Count > 1 && (float)_aiRng.NextDouble() < 0.4f)
                        picks.Add(futurePicks[1]);
                }

                if (TryExecuteTrade(seller, sellerRoster, buyer, buyerRoster,
                        new List<PlayerData> { vet },
                        youngPack,
                        seasonId, gameDay,
                        aSelectedPicks: null,
                        bSelectedPicks: picks))
                {
                    tradedThisSeason.Add(vet.id);
                    foreach (var yp in youngPack) tradedThisSeason.Add(yp.id);
                    Debug.Log($"[AITrades] {seller.name} vende a {vet.first_name} {vet.last_name} ({vet.age} años) a {buyer.name} por jóvenes/picks (rebuild fire sale)");
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Mercado de buyout: los jugadores rescindidos que siguen libres reciben destino de la
    /// IA en cuanto pasa la jornada de su rescisión. Firman por el mínimo y se genera noticia.
    /// Se ejecuta en el lote de fondo (conexión ambient), antes de los demás pases de FA.
    /// </summary>
    public void ProcessBuyoutMarket(int gameDay)
    {
        if (_season == null || _myTeam == null) return;

        // Solo los jugadores de 75+ de media tienen destino inmediato: un rescindido de
        // 68 se queda en agentes libres y decide el mercado general.
        var waived = DatabaseManager.Instance.GetFreeAgents()
            .Where(p => BuyoutMarketRules.CanSignOn(p.waived_game_day, gameDay)
                     && BuyoutMarketRules.IsWanted(p.GetCalculatedAverage()))
            .OrderByDescending(p => p.GetCalculatedAverage())
            .ToList();
        if (waived.Count == 0) return;

        var settings = DatabaseManager.Instance.GetLeagueSettings();
        var teams = DatabaseManager.Instance.GetAllTeams().Where(t => t.id != _myTeam.id).ToList();
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (var p in waived)
        {
            long minSalary = BuyoutMarketRules.WaivedSalary(
                TradeHelper.MinimumSalary(settings, p.years_of_service));
            var candidates = new List<BuyoutCandidate>();
            foreach (var t in teams)
            {
                var roster = DatabaseManager.Instance.GetPlayersByTeam(t.id);
                long payroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(t.id);
                bool hasRoom = DatabaseManager.Instance.GetRosterCount(t.id) < TradeHelper.MAX_STANDARD_ROSTER
                            && TradeHelper.CanAISignExternal(payroll, minSalary, settings, p.years_of_service);

                candidates.Add(new BuyoutCandidate
                {
                    teamId = t.id,
                    strategy = GetTeamStrategy(t.id),
                    teamAvg = roster.Count > 0 ? (float)roster.Average(x => x.GetCalculatedAverage()) : 0f,
                    samePosCount = roster.Count(x => x.position == p.position),
                    reputation = t.reputation,
                    hasRoom = hasRoom
                });
            }

            // Entre los mejores postores (contender > balanced > rebuild, con hueco en su
            // posición y mejor proyecto/prestigio) se sortea el destino ponderadamente: el
            // mejor candidato es el más probable, pero no siempre el elegido.
            var ranked = BuyoutMarketRules.RankDestinations(candidates);
            int pick = BuyoutMarketRules.PickSuitor(ranked.Count, _aiRng);
            if (pick < 0) continue;

            var team = teams.FirstOrDefault(t => t.id == ranked[pick].teamId);
            if (team == null) continue;

            p.team_id = team.id;
            p.last_team_id = team.id;
            p.salary = minSalary;
            // Firma de mínimo de 1 año (mercado de buyout): al expirar, su equipo puede
            // renovarle por su valor real o pierde sus derechos al abrirse el mercado.
            p.contract_years = BuyoutMarketRules.ContractYears;
            p.guaranteed_years = BuyoutMarketRules.ContractYears;
            p.has_team_option = 0;
            p.has_player_option = 0;
            p.waived_game_day = -1;
            DatabaseManager.Instance.AssignJerseyNumber(p, team.id);
            DatabaseManager.Instance.UpdatePlayer(p);

            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = _season.id,
                game_day = gameDay,
                game_date = _season.current_date,
                team_id_from = 0,
                team_id_to = team.id,
                player_id = p.id,
                trade_type = "free_agent"
            });

            string playerName = $"{p.first_name} {p.last_name}";
            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 2,
                sender_id = 0,
                title = $"Mercado de buyout: {playerName} firma con {team.name}",
                body = NewsTexts.MercadoBuyout(playerName, team.name),
                game_day = gameDay,
                game_date = _season.current_date,
                created_at = now,
                date_sent = now,
                is_read = 0,
                category = NewsCategory.LIGA,
                news_type = NewsType.FICHAJE
            });

            Debug.Log($"[BuyoutMarket] {team.name} firma a {playerName} (OVR {p.GetCalculatedAverage()}) por el mínimo " +
                      $"[postor {pick + 1}/{ranked.Count}]");
        }
    }

    /// <summary>
    /// Re-firma de **agentes libres propios con Bird rights**: un FA reciente cuyo último
    /// equipo conserva sus derechos (expiró contrato o declinó su opción) puede volver a él
    /// con un contrato acorde a su valor de mercado, aunque el equipo esté por encima del
    /// tope (límite del 2º apron, como las renovaciones). Es lo que permite que un jugador
    /// que firmó por el mínimo tras una rescisión cobre después lo que le corresponde.
    /// Se ejecuta en el lote de fondo, antes de los demás pases de FA.
    /// </summary>
    public void ProcessOwnFreeAgentResignings(int gameDay)
    {
        if (_season == null || _myTeam == null) return;

        var settings = DatabaseManager.Instance.GetLeagueSettings();
        long cap = settings?.salary_cap ?? TradeHelper.SALARY_CAP;

        var candidates = DatabaseManager.Instance.GetFreeAgents()
            .Where(p => p.last_team_id > 0 && p.last_team_id != _myTeam.id && p.seasons_with_team > 0)
            .OrderByDescending(p => p.GetCalculatedAverage())
            .ToList();
        if (candidates.Count == 0) return;

        Dictionary<int, int> gamesByPlayer = null;

        foreach (var p in candidates)
        {
            if (p.renewal_cooldown_day > gameDay) continue;

            var team = DatabaseManager.Instance.GetTeamById(p.last_team_id);
            if (team == null) continue;
            if (DatabaseManager.Instance.GetRosterCount(team.id) >= TradeHelper.MAX_STANDARD_ROSTER) continue;
            if (CalculateTeamRenewalScore(p, team, cap, settings) < 50f) continue;

            var offer = CalculateAIRenewalOffer(p, team, cap, settings);
                long payroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(team.id, p.id);

            // Como en las renovaciones: nunca dejar al equipo por encima del 2º apron.
            if (!TradeHelper.CanAIRenew(payroll + offer.salary, settings))
            {
                p.renewal_cooldown_day = gameDay + 15;
                DatabaseManager.Instance.UpdatePlayer(p);
                continue;
            }

            gamesByPlayer ??= DatabaseManager.Instance.GetGamesPlayedByPlayerInSeason(_manager.id, _season.id);
            int gamesPlayed = gamesByPlayer.GetValueOrDefault(p.id, 0);
            int teamChem = DatabaseManager.Instance.GetTeamChemistry(team.id);
            float acceptScore = RosterController.CalculateAcceptScore(
                p, offer.salary, offer.years, gamesPlayed, teamChem);

            if (_aiRng.Next(1, 101) > acceptScore)
            {
                p.renewal_cooldown_day = gameDay + 15;
                DatabaseManager.Instance.UpdatePlayer(p);
                continue;
            }

            p.team_id = team.id;
            p.last_team_id = team.id;
            p.salary = offer.salary;
            p.contract_years = offer.years;
            p.guaranteed_years = offer.teamOption ? offer.years - 1 : offer.years;
            p.has_team_option = offer.teamOption ? 1 : 0;
            p.has_player_option = 0;
            p.waived_game_day = -1;
            p.renewal_cooldown_day = gameDay + 365;
            DatabaseManager.Instance.AssignJerseyNumber(p, team.id);
            DatabaseManager.Instance.UpdatePlayer(p);

            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = _season.id,
                game_day = gameDay,
                game_date = _season.current_date,
                team_id_from = 0,
                team_id_to = team.id,
                player_id = p.id,
                trade_type = "free_agent"
            });

            if (p.overall >= 90) AddAIRenewalNotification(p, team, true, offer);
            Debug.Log($"[OwnFA] {team.name} re-firma a {p.first_name} {p.last_name} " +
                      $"(OVR {p.overall}) por {offer.salary:N0} $ / {offer.years} año(s) con Bird rights");
        }
    }

    public void ProcessStarFreeAgentSignings(int gameDay)
    {
        if (_season == null) return;

        var freeAgents = DatabaseManager.Instance.GetFreeAgents();
        var stars = freeAgents
            .Where(p => p.GetCalculatedAverage() > 80)
            .OrderByDescending(p => p.GetCalculatedAverage())
            .ToList();
        if (stars.Count == 0) return;

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        var allTeams = DatabaseManager.Instance.GetAllTeams()
            .Where(t => t.id != _myTeam.id)
            .ToList();

        var teamAvgs = new Dictionary<int, float>();
        var teamStrategies = new Dictionary<int, TeamStrategy>();
        foreach (var team in allTeams)
        {
            var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            teamAvgs[team.id] = roster.Count > 0 ? (float)roster.Average(p => p.GetCalculatedAverage()) : 0f;
            teamStrategies[team.id] = GetTeamStrategy(team.id);
        }
        // Prioridad de destino: contender > balanced > rebuild (los rebuild tankean y
        // preservan el pick alto del draft).
        var teamsByPriority = allTeams
            .OrderByDescending(t => teamStrategies[t.id] == TeamStrategy.Contend ? 3
                                   : teamStrategies[t.id] == TeamStrategy.Balanced ? 2 : 1)
            .ThenByDescending(t => teamAvgs[t.id])
            .ToList();

        var pendingFAIds = DatabaseManager.Instance.GetPendingFAPlayerIds(_manager.id);
        var pendingSATIds = GetPendingSATIds(_manager, _myTeam);

        foreach (var star in stars)
        {
            if (pendingFAIds.Contains(star.id))
            {
                Debug.Log($"[StarFA] Skip {star.first_name} {star.last_name} — user has pending offer");
                continue;
            }

            if (pendingSATIds.Contains(star.id))
            {
                Debug.Log($"[StarFA] Skip {star.first_name} {star.last_name} — user has pending sign-and-trade offer");
                continue;
            }

            bool signed = false;
            foreach (var team in teamsByPriority)
            {
                var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
                if (DatabaseManager.Instance.GetRosterCount(team.id) >= TradeHelper.MAX_STANDARD_ROSTER) continue;

                // Un equipo en reconstrucción no ficha estrellas top (tankea para el draft)
                if (star.GetCalculatedAverage() >= 85 && teamStrategies[team.id] == TeamStrategy.Rebuild) continue;

                long payroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(team.id);
                bool fromBuyout = star.waived_game_day >= 0;

                // Salario nuevo: un rescindido firma por el mínimo (mercado de buyout); el
                // resto, su precio + un pequeño incremento (como el pase general). Antes se
                // firmaba con el salario viejo y el equipo nuevo heredaba esa nómina.
                long starMinimum = TradeHelper.MinimumSalary(leagueSettings, star.years_of_service);
                long newSalary = fromBuyout
                    ? BuyoutMarketRules.WaivedSalary(starMinimum)
                    : star.salary + 2_000_000;
                if (newSalary < starMinimum) newSalary = starMinimum;

                int years = fromBuyout ? BuyoutMarketRules.ContractYears
                          : star.age > 35 ? 1 : star.age > 32 ? 2 : star.age > 28 ? 3 : star.age > 25 ? 4 : 5;
                TeamData signingTeam = team;
                bool matchedRFA = false;
                if (!fromBuyout && star.free_agent_type == "RFA" && star.last_team_id > 0
                    && star.last_team_id != team.id
                    && FreeAgencyRules.ShouldAIMatch(star.free_agent_type, star.GetCalculatedAverage(),
                        newSalary, star.qualifying_offer_salary))
                {
                    var rightsTeam = DatabaseManager.Instance.GetTeamById(star.last_team_id);
                    if (rightsTeam != null
                        && DatabaseManager.Instance.GetRosterCount(rightsTeam.id) < TradeHelper.MAX_STANDARD_ROSTER)
                    {
                        long rightsPayroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(rightsTeam.id, star.id);
                        long matchedPayroll = rightsPayroll + newSalary;
                        bool withinApron = TradeHelper.CanAIRenew(matchedPayroll, leagueSettings);
                        if (rightsTeam.first_apron_hard_capped == 1)
                        {
                            long firstApron = leagueSettings?.apron > 0 ? leagueSettings.apron : TradeHelper.FIRST_APRON;
                            withinApron &= matchedPayroll <= firstApron;
                        }
                        if (withinApron)
                        {
                            signingTeam = rightsTeam;
                            payroll = rightsPayroll;
                            matchedRFA = true;
                        }
                    }
                }

                // La IA ficha con sus excepciones (espacio → MLE → mínimo), no solo con
                // espacio de tope: antes ninguna estrella por encima del cap podía firmar.
                if (!matchedRFA && !TradeHelper.CanAISignExternal(payroll, newSalary, leagueSettings, star.years_of_service)) continue;

                star.team_id = signingTeam.id;
                star.last_team_id = signingTeam.id;
                star.salary = newSalary;
                // El rescindido firma 1 año (y al expirar su equipo puede renovarle por su
                // valor real, o re-firmarle con Bird rights).
                star.contract_years = years;
                star.guaranteed_years = years;
                star.has_team_option = 0;
                star.has_player_option = 0;
                star.waived_game_day = -1;
                star.free_agent_type = "";
                star.qualifying_offer_salary = 0;
                DatabaseManager.Instance.AssignJerseyNumber(star, signingTeam.id);
                DatabaseManager.Instance.UpdatePlayer(star);

                if (matchedRFA && signingTeam.id == _myTeam.id)
                {
                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Oferta RFA igualada: {star.first_name} {star.last_name}",
                        body = $"Tu franquicia ha igualado la oferta de {newSalary:N0} $ por {star.first_name} {star.last_name} y conserva sus derechos.",
                        game_day = gameDay,
                        game_date = _season.current_date,
                        created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        is_read = 0,
                        category = NewsCategory.TRASPASOS,
                        news_type = NewsType.FICHAJE
                    });
                }

                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = _season.id,
                    game_day = gameDay,
                    game_date = _season.current_date,
                    team_id_from = 0,
                    team_id_to = signingTeam.id,
                    player_id = star.id,
                    trade_type = "free_agent"
                });

                Debug.Log($"[StarFA] {signingTeam.name} {(matchedRFA ? "matched" : "signed")} star {star.first_name} {star.last_name} (OVR {star.GetCalculatedAverage()})");
                signed = true;
                break;
            }

            if (!signed)
            {
                Debug.Log($"[StarFA] No team could sign {star.first_name} {star.last_name} (OVR {star.GetCalculatedAverage()}) — stays in FA");
            }
        }
    }

    public void ProcessAIRenewals()
    {
        Debug.Log("[AIRenewals] Processing AI contract renewals...");

        var db = DatabaseManager.Instance.Db;
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long cap = leagueSettings.salary_cap;
        int renewedCount = 0, lostCount = 0;

        // Una sola query agregada en vez de una por jugador (N+1).
        var gamesByPlayer = DatabaseManager.Instance.GetGamesPlayedByPlayerInSeason(_manager.id, _season.id);

        foreach (var team in allTeams)
        {
            if (team.id == _myTeam.id) continue;

            // Nómina en curso del equipo (se actualiza al renovar).
            long teamPayroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(team.id);

            var expiringPlayers = db.Query<PlayerData>(
                "SELECT * FROM players WHERE team_id = ? AND contract_years = 1 AND is_two_way = 0 AND is_on_ir = 0",
                team.id);

            foreach (var player in expiringPlayers)
            {
                float teamScore = CalculateTeamRenewalScore(player, team, cap, leagueSettings);
                if (teamScore < 50f) continue;

                var offer = CalculateAIRenewalOffer(player, team, cap, leagueSettings);

                // No renovar si deja al equipo por encima del 2º apron (evita que las
                // nóminas IA se disparen temporada a temporada).
                long projected = teamPayroll - player.salary + offer.salary;
                if (!TradeHelper.CanAIRenew(projected, leagueSettings))
                {
                    player.renewal_cooldown_day = _season.current_game_day + 15;
                    db.Update(player);
                    lostCount++;
                    if (player.overall >= 90)
                        AddAIRenewalNotification(player, team, false, offer);
                    continue;
                }

                int gamesPlayed = gamesByPlayer.GetValueOrDefault(player.id, 0);
                int teamChem = DatabaseManager.Instance.GetTeamChemistry(team.id);
                float acceptScore = RosterController.CalculateAcceptScore(
                    player, offer.salary, offer.years, gamesPlayed, teamChem);

                bool accepted = _aiRng.Next(1, 101) <= acceptScore;

                if (accepted)
                {
                    player.salary = offer.salary;
                    player.contract_years = offer.years;
                    player.guaranteed_years = offer.teamOption ? offer.years - 1 : offer.years;
                    player.has_team_option = offer.teamOption ? 1 : 0;
                    player.has_player_option = 0;
                    player.renewal_cooldown_day = _season.current_game_day + 365;
                    db.Update(player);
                    teamPayroll = projected;
                    renewedCount++;

                    if (player.overall >= 90)
                        AddAIRenewalNotification(player, team, true, offer);
                }
                else
                {
                    player.renewal_cooldown_day = _season.current_game_day + 15;
                    db.Update(player);
                    lostCount++;

                    if (player.overall >= 90)
                        AddAIRenewalNotification(player, team, false, offer);
                }
            }
        }

        Debug.Log($"[AIRenewals] Done. Renewed: {renewedCount}, Lost: {lostCount}");
    }

    float CalculateTeamRenewalScore(PlayerData player, TeamData team, long cap,
        LeagueSettingsData settings)
    {
        float score = 0f;

        // OVR (40%)
        if (player.overall >= 85) score += 40f;
        else if (player.overall >= 80) score += 32f;
        else if (player.overall >= 75) score += 24f;
        else if (player.overall >= 70) score += 16f;
        else score += 8f;

        // Rol (20%)
        if (player.role == PlayerRole.Estrella) score += 20f;
        else if (player.role == PlayerRole.Titular) score += 15f;
        else if (player.role == PlayerRole.Banquillo) score += 5f;

        // Edad (15%) — equipos prefieren jóvenes
        if (player.age <= 25) score += 15f;
        else if (player.age <= 30) score += 10f;
        else if (player.age <= 34) score += 5f;

        // Salario vs valor estimado (15%)
        long estimatedValue = EstimateMarketValue(player, cap, settings);
        if (player.salary < estimatedValue) score += 15f;
        else if (player.salary <= estimatedValue * 1.2f) score += 8f;

        // Estrategia del equipo (10%)
        var strategy = GetTeamStrategy(team.id);
        if (strategy == TeamStrategy.Contend && player.overall >= 75) score += 10f;
        else if (strategy == TeamStrategy.Rebuild && player.overall < 75 && player.age <= 28) score += 10f;
        else score += 5f;

        return Mathf.Clamp(score, 0f, 100f);
    }

    long EstimateMarketValue(PlayerData player, long cap, LeagueSettingsData settings)
    {
        // Umbrales por OVR en una sola fuente (TradeHelper); por debajo, el salario actual.
        long byOvr = TradeHelper.MarketValueByOvr(player.overall, cap);
        if (byOvr > 0) return byOvr;
        long minimum = TradeHelper.MinimumSalary(settings, player.years_of_service);
        if (player.overall >= 70) return (long)Mathf.Max(player.salary * 110 / 100, minimum);
        return (long)Mathf.Max(player.salary * 105 / 100, minimum);
    }

    (long salary, int years, bool teamOption) CalculateAIRenewalOffer(
        PlayerData player, TeamData team, long cap, LeagueSettingsData settings)
    {
        long maxByExp;
        int experience = player.age - 22;
        if (experience <= 6) maxByExp = cap / 4;
        else if (experience <= 9) maxByExp = (long)(cap * 0.30);
        else maxByExp = (long)(cap * 0.35);

        long marketValue = EstimateMarketValue(player, cap, settings);
        long salary = (long)Mathf.Min(maxByExp, marketValue);
        long minimum = TradeHelper.MinimumSalary(settings, player.years_of_service);

        // Muy por debajo de su valor de mercado (p. ej. fichó por el mínimo tras una
        // rescisión): el equipo le renueva a su valor real (para una estrella, su máximo
        // por experiencia), sin el tope no-Bird del 120 % sobre el salario mínimo.
        if (marketValue >= player.salary * 3)
        {
            salary = (long)(Mathf.Round(salary / 100_000f) * 100_000);
            if (salary < minimum) salary = minimum;

            int yearsLow;
            if (player.age <= 25) yearsLow = 5;
            else if (player.age <= 28) yearsLow = 4;
            else if (player.age <= 32) yearsLow = 3;
            else if (player.age <= 35) yearsLow = 2;
            else yearsLow = 1;
            return (salary, yearsLow, player.age >= 30);
        }

        if (player.seasons_with_team >= 3)
        {
            salary = (long)Mathf.Min(salary, maxByExp);
        }
        else if (player.seasons_with_team == 2)
        {
            long earlyBirdMax = (long)Mathf.Max(player.salary * 175 / 100, (long)(cap * 1.05));
            salary = (long)Mathf.Min(salary, earlyBirdMax);
        }
        else
        {
            long noBirdMax = player.salary * 120 / 100;
            salary = (long)Mathf.Min(salary, noBirdMax);
        }

        salary = (long)(Mathf.Round(salary / 100_000f) * 100_000);
        if (salary < minimum) salary = minimum;

        int years;
        if (player.age <= 25) years = 5;
        else if (player.age <= 28) years = 4;
        else if (player.age <= 32) years = 3;
        else if (player.age <= 35) years = 2;
        else years = 1;

        bool teamOption = player.age >= 30;

        return (salary, years, teamOption);
    }

    void AddAIRenewalNotification(PlayerData player, TeamData team, bool accepted,
        (long salary, int years, bool teamOption) offer)
    {
        string playerName = $"{player.first_name} {player.last_name}";
        string teamName = team.name;
        string salaryStr = $"${offer.salary:N0}";
        string yearsText = offer.years == 1 ? "1 temporada" : $"{offer.years} temporadas";
        string optionText = offer.teamOption ? " (con opción de equipo)" : "";

        if (accepted)
        {
            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 2,
                sender_id = 0,
                title = $"Renovación estrella: {playerName}",
                body = $"{teamName} ha renovado a {playerName} (OVR {player.overall}) por {yearsText} a {salaryStr}/año{optionText}.",
                game_day = _season.current_game_day,
                game_date = _season.current_date,
                created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = 0,
                category = NewsCategory.TRASPASOS,
                news_type = NewsType.RENOVACION
            });
        }
        else
        {
            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 2,
                sender_id = 0,
                title = $"Estrella sin renovar: {playerName}",
                body = $"{playerName} (OVR {player.overall}) no ha aceptado la renovación de {teamName}. El jugador saldrá a mercado libre al final de temporada.",
                game_day = _season.current_game_day,
                game_date = _season.current_date,
                created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = 0,
                category = NewsCategory.TRASPASOS,
                news_type = NewsType.OFERTA_RECHAZADA
            });
        }
    }

    string GetWeakestPosition(List<PlayerData> roster)
    {
        if (roster == null || roster.Count == 0) return null;

        var positions = PositionCodes.Order;
        string weakest = null;
        float lowestAvg = float.MaxValue;

        foreach (var pos in positions)
        {
            var atPos = roster.Where(p => p.position == pos && p.injury_days == 0).ToList();
            if (atPos.Count == 0) return pos;
            float avg = (float)atPos.Average(p => p.overall);
            if (avg < lowestAvg)
            {
                lowestAvg = avg;
                weakest = pos;
            }
        }
        return weakest;
    }

    bool TryFindAITrade(TeamData teamA, List<PlayerData> rosterA, string targetPos,
                        int seasonId, int gameDay, HashSet<int> tradedThisSeason)
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams()
            .Where(t => t.id != _myTeam.id && t.id != teamA.id)
            .OrderBy(_ => _aiRng.Next(0, 1000))
            .ToList();

        // Rondas disponibles del equipo que propone (una sola consulta para todo el ciclo).
        var aPickPool = GetFuturePicksForTeam(teamA.id, true);

        foreach (var teamB in allTeams)
        {
            var rosterB = DatabaseManager.Instance.GetPlayersByTeam(teamB.id);

            // Los contenders buscan mejoras más fuertes (sin tope de 86) y pueden incluir picks
            var teamAStrategy = GetTeamStrategy(teamA.id);
            int targetCap = teamAStrategy == TeamStrategy.Contend ? 90 : 86;

            var target = rosterB
                .Where(p => p.position == targetPos && p.injury_days == 0 && p.is_rookie == 0)
                .OrderByDescending(p => p.overall)
                .FirstOrDefault(p => p.overall <= targetCap);
            if (target == null) continue;

            var candidates = rosterA
                .Where(p => p.injury_days == 0 && p.id != target.id
                            && !tradedThisSeason.Contains(p.id) && p.is_rookie == 0
                            && rosterA.Count(r => r.position == p.position) >= (
                                p.position == targetPos ? 1 : 2))
                .OrderBy(p => p.overall)
                .ToList();
            if (candidates.Count == 0) continue;

            // Protección universal: estrellas (OVR >= 87) nunca se ofrecen.
            // Contender además protege jóvenes prometedores (edad < 26 y OVR >= 82).
            List<PlayerData> tradeableCandidates = candidates
                .Where(p => p.overall < 87).ToList();
            if (teamAStrategy == TeamStrategy.Contend)
                tradeableCandidates = tradeableCandidates.Where(p => !(p.age < 26 && p.overall >= 82)).ToList();

            if (tradeableCandidates.Count == 0) continue;

            // Los contender suelen añadir un pick futuro como "sweetener" (R1 + a veces R2)
            List<DraftPickData> teamAPicks = null;
            if (teamAStrategy == TeamStrategy.Contend && target.overall > 84 && aPickPool.Count > 0)
            {
                teamAPicks = aPickPool
                    .Take((float)_aiRng.NextDouble() < 0.4f && aPickPool.Count > 1 ? 2 : 1)
                    .ToList();
            }

            // Try 1-for-1 (de peor a mejor jugador): sólo si el traspaso es razonable para
            // los DOS equipos (nadie regala talento sin compensación).
            foreach (var c in tradeableCandidates)
            {
                var aGives = new List<PlayerData> { c };
                var aPicks = teamAPicks;
                if (!IsAITradeFair(teamA, aGives, teamB, new List<PlayerData> { target }, ref aPicks, aPickPool)) continue;
                if (TryExecuteTrade(teamA, rosterA, teamB, rosterB,
                        aGives,
                        new List<PlayerData> { target },
                        seasonId, gameDay,
                        aSelectedPicks: aPicks.Count > 0 ? aPicks : null))
                {
                    tradedThisSeason.Add(c.id);
                    tradedThisSeason.Add(target.id);
                    return true;
                }
            }

            // Try 2-for-1
            for (int i = 0; i < tradeableCandidates.Count; i++)
            {
                for (int j = i + 1; j < tradeableCandidates.Count; j++)
                {
                    var aGives = new List<PlayerData> { tradeableCandidates[i], tradeableCandidates[j] };
                    var aPicks = teamAPicks;
                    if (!IsAITradeFair(teamA, aGives, teamB, new List<PlayerData> { target }, ref aPicks, aPickPool)) continue;
                    if (TryExecuteTrade(teamA, rosterA, teamB, rosterB,
                            aGives,
                            new List<PlayerData> { target },
                            seasonId, gameDay,
                            aSelectedPicks: aPicks.Count > 0 ? aPicks : null))
                    {
                        tradedThisSeason.Add(tradeableCandidates[i].id);
                        tradedThisSeason.Add(tradeableCandidates[j].id);
                        tradedThisSeason.Add(target.id);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// ¿El traspaso IA-IA es razonable para LOS DOS equipos? (Ninguno entrega mucho más
    /// valor del que recibe ni un jugador claramente mejor que el que se lleva.) Si tal
    /// cual no cuadra, el equipo que lo propone intenta compensar con una ronda; si aun
    /// así no cuadra, no hay trato. El fire-sale de <see cref="TrySellVeteran"/> queda
    /// fuera a propósito: ahí un rebuild SÍ regala veteranos por jóvenes/picks.
    /// </summary>
    bool IsAITradeFair(TeamData teamA, List<PlayerData> aGives, TeamData teamB, List<PlayerData> bGives,
                       ref List<DraftPickData> aPicks, List<DraftPickData> pickPool)
    {
        long cap = GetValueCap();
        int draftYear = _season?.year_end ?? 0;
        var strategyA = GetTeamStrategy(teamA.id);
        var strategyB = GetTeamStrategy(teamB.id);

        var picks = aPicks != null ? new List<DraftPickData>(aPicks) : new List<DraftPickData>();

        bool Fair(List<DraftPickData> current) =>
            TradeValueHelper.IsSaneBothWays(strategyA, aGives, current, strategyB, bGives, null, cap, draftYear);

        if (Fair(picks)) { aPicks = picks; return true; }

        // El "sweetener" de contender puede romper el equilibrio: primero sin él.
        if (picks.Count > 0)
        {
            picks.Clear();
            if (Fair(picks)) { aPicks = picks; return true; }
        }

        // Compensar con una ronda del que propone el traspaso.
        if (pickPool != null)
        {
            foreach (var pk in pickPool)
            {
                if (picks.Any(x => x.id == pk.id)) continue;
                picks.Add(pk);
                if (Fair(picks)) { aPicks = picks; return true; }
                picks.Remove(pk);
            }
        }
        return false;
    }

    List<DraftPickData> GetFuturePicksForTeam(int teamId, bool includeRound2)
    {
        var all = DatabaseManager.Instance.GetDraftPicksForTeam(teamId);
        if (all.Count == 0) return all;

        // La IA ofrece un año aleatorio de entre los que posee el equipo.
        var years = all.Select(p => p.draft_year).Distinct().ToList();
        int chosenYear = years[_aiRng.Next(years.Count)];
        var rounded = all.Where(p => p.draft_year == chosenYear).ToList();

        var r1 = rounded.Where(p => p.round == 1).Take(1).ToList();
        if (!includeRound2)
            return r1;
        return r1.Concat(rounded.Where(p => p.round == 2).Take(1)).ToList();
    }

    bool TryExecuteTrade(TeamData teamA, List<PlayerData> rosterA,
                         TeamData teamB, List<PlayerData> rosterB,
                         List<PlayerData> aSelected, List<PlayerData> bSelected,
                         int seasonId, int gameDay,
                         List<DraftPickData> aSelectedPicks = null,
                         List<DraftPickData> bSelectedPicks = null)
    {
        var aPayroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(teamA.id);
        var bPayroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(teamB.id);
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long firstApron = leagueSettings?.apron > 0 ? leagueSettings.apron : TradeHelper.FIRST_APRON;
        long secondApron = leagueSettings?.repeater_apron > 0 ? leagueSettings.repeater_apron : TradeHelper.SECOND_APRON;
        long luxuryTax = leagueSettings?.luxury_tax > 0 ? leagueSettings.luxury_tax : TradeHelper.LUXURY_TAX;
        var errors = TradeHelper.ValidateTrade(
            aSelected, bSelected,
            DatabaseManager.Instance.GetRosterCount(teamA.id),
            DatabaseManager.Instance.GetRosterCount(teamB.id),
            teamB.name, bPayroll,
            teamA.name, aPayroll,
            teamA.first_apron_hard_capped == 1,
            teamB.first_apron_hard_capped == 1,
            firstApron, secondApron, luxuryTax,
            teamATwoWayCount: DatabaseManager.Instance.GetTwoWayCount(teamA.id),
            teamBTwoWayCount: DatabaseManager.Instance.GetTwoWayCount(teamB.id),
            teamATotalActiveRoster: DatabaseManager.Instance.GetTotalRosterCount(teamA.id),
            teamBTotalActiveRoster: DatabaseManager.Instance.GetTotalRosterCount(teamB.id));

        if (errors.Count > 0) return false;

        foreach (var p in aSelected)
        {
            p.team_id = teamB.id;
            p.g_league_assigned = 0;
            p.is_on_ir = 0;
            p.on_trade_block = 0;
            DatabaseManager.Instance.UpdatePlayer(p);
            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = seasonId,
                game_day = gameDay,
                game_date = _season.current_date,
                team_id_from = teamA.id,
                team_id_to = teamB.id,
                player_id = p.id,
                trade_type = "trade",
                partner_player_id = bSelected.First().id
            });
        }

        foreach (var p in bSelected)
        {
            p.team_id = teamA.id;
            p.g_league_assigned = 0;
            p.is_on_ir = 0;
            p.on_trade_block = 0;
            DatabaseManager.Instance.UpdatePlayer(p);
            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = seasonId,
                game_day = gameDay,
                game_date = _season.current_date,
                team_id_from = teamB.id,
                team_id_to = teamA.id,
                player_id = p.id,
                trade_type = "trade",
                partner_player_id = aSelected.First().id
            });
        }

        foreach (var p in aSelected)
        {
            DatabaseManager.Instance.AssignJerseyNumber(p, teamB.id);
            DatabaseManager.Instance.UpdatePlayer(p);
        }
        foreach (var p in bSelected)
        {
            DatabaseManager.Instance.AssignJerseyNumber(p, teamA.id);
            DatabaseManager.Instance.UpdatePlayer(p);
        }

        // Transfer picks
        if (aSelectedPicks != null && aSelectedPicks.Count > 0)
        {
            var ids = aSelectedPicks.Select(p => p.id).ToList();
            DatabaseManager.Instance.TransferDraftPicks(ids, teamA.id, teamB.id);
            foreach (var pk in aSelectedPicks)
            {
                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = seasonId,
                    game_day = gameDay,
                    game_date = _season.current_date,
                    team_id_from = teamA.id,
                    team_id_to = teamB.id,
                    player_id = 0,
                    pick_id = pk.id,
                    trade_type = "pick_trade"
                });
            }
        }
        if (bSelectedPicks != null && bSelectedPicks.Count > 0)
        {
            var ids = bSelectedPicks.Select(p => p.id).ToList();
            DatabaseManager.Instance.TransferDraftPicks(ids, teamB.id, teamA.id);
            foreach (var pk in bSelectedPicks)
            {
                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = seasonId,
                    game_day = gameDay,
                    game_date = _season.current_date,
                    team_id_from = teamB.id,
                    team_id_to = teamA.id,
                    player_id = 0,
                    pick_id = pk.id,
                    trade_type = "pick_trade"
                });
            }
        }

        var aNames = string.Join(", ", aSelected.Select(p => $"{p.first_name} {p.last_name}"));
        var bNames = string.Join(", ", bSelected.Select(p => $"{p.first_name} {p.last_name}"));
        var aPicksLog = aSelectedPicks != null && aSelectedPicks.Count > 0
            ? " + " + string.Join(", ", aSelectedPicks.Select(p => $"R{p.round}#{p.pick_number}")) : "";
        var bPicksLog = bSelectedPicks != null && bSelectedPicks.Count > 0
            ? " + " + string.Join(", ", bSelectedPicks.Select(p => $"R{p.round}#{p.pick_number}")) : "";
        Debug.Log($"[AI Trade] {teamA.name} ↔ {teamB.name}: {aNames}{aPicksLog} for {bNames}{bPicksLog}");
        return true;
    }

    void TrySignFreeAgent(TeamData team, List<PlayerData> roster, string targetPos,
                          List<PlayerData> freeAgents, int seasonId, int gameDay)
    {
        int rosterCount = DatabaseManager.Instance.GetRosterCount(team.id);
        int twoWayCount = DatabaseManager.Instance.GetTwoWayCount(team.id);
        if (rosterCount >= TradeHelper.MAX_STANDARD_ROSTER && twoWayCount >= TradeHelper.MAX_TWO_WAY) return;

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long aiPayroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(team.id);

        var pendingFAIds = DatabaseManager.Instance.GetPendingFAPlayerIds(_manager.id);
        var pendingSATIds = GetPendingSATIds(_manager, _myTeam);

        var candidates = freeAgents
            .Where(p => p.free_agent_type != "RFA" && p.position == targetPos && p.salary <= team.budget && !pendingFAIds.Contains(p.id) && !pendingSATIds.Contains(p.id))
            .OrderByDescending(p => p.overall)
            .ToList();

        if (candidates.Count == 0)
        {
            // Try any position if no match at targetPos
            candidates = freeAgents
                .Where(p => p.free_agent_type != "RFA" && p.salary <= team.budget && !pendingFAIds.Contains(p.id) && !pendingSATIds.Contains(p.id))
                .OrderByDescending(p => p.overall)
                .ToList();
        }

        foreach (var player in candidates)
        {
            // Al completar las 16 plazas estándar, la IA solo puede usar plazas two-way.
            bool signAsTwoWay = rosterCount >= TradeHelper.MAX_STANDARD_ROSTER;
            if (signAsTwoWay)
            {
                if (twoWayCount >= TradeHelper.MAX_TWO_WAY) return;
                if (!TradeHelper.IsEligibleForTwoWay(player)) continue;
            }

            int chance = (int)Mathf.Clamp(team.reputation * 20 - player.overall * 0.5f + 30, 5, 95);
            if (_aiRng.Next(0, 100) >= chance) continue;

            // Los two-way no computan contra el cap (igual que para el usuario).
            long minSalary = TradeHelper.MinimumSalary(leagueSettings, player.years_of_service);
            long newSalary = signAsTwoWay ? TradeHelper.TwoWaySalary(leagueSettings)
                           : player.waived_game_day >= 0 ? minSalary      // mercado de buyout
                           : player.salary + 2_000_000;
            if (!signAsTwoWay && newSalary < minSalary) newSalary = minSalary;
            if (!signAsTwoWay && !TradeHelper.CanAISignExternal(aiPayroll, newSalary, leagueSettings, player.years_of_service))
                continue; // no cabe en el cap/excepciones: probar otro candidato

            player.team_id = team.id;
            player.last_team_id = team.id;
            if (signAsTwoWay)
            {
                player.is_two_way = 1;
                player.salary = TradeHelper.TwoWaySalary(leagueSettings);
                player.contract_years = 2;
                player.guaranteed_years = 2;
            }
            else
            {
                int years = player.waived_game_day >= 0 ? BuyoutMarketRules.ContractYears
                          : player.age > 35 ? 1 : player.age > 32 ? 2 : player.age > 28 ? 3 : player.age > 25 ? 4 : 5;
                player.salary = newSalary;
                player.contract_years = years;
                player.guaranteed_years = years;
                player.waived_game_day = -1;
                aiPayroll += newSalary;
            }
            DatabaseManager.Instance.AssignJerseyNumber(player, team.id);
            DatabaseManager.Instance.UpdatePlayer(player);

            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = seasonId,
                game_day = gameDay,
                game_date = _season.current_date,
                team_id_from = 0,
                team_id_to = team.id,
                player_id = player.id,
                trade_type = signAsTwoWay ? "free_agent_two_way" : "free_agent"
            });

            Debug.Log($"[AI FA] {team.name} signed {player.first_name} {player.last_name} ({player.position}, {player.overall} OVR){(signAsTwoWay ? " — TWO-WAY" : "")}");
            break;
        }
    }

    // ═══════════════════════════════════════════
    //  AI TRADE OFFERS TO PLAYER
    // ═══════════════════════════════════════════

    public static HashSet<int> GetPendingSATIds(ManagerData manager, TeamData myTeam)
    {
        var result = new HashSet<int>();
        var pending = DatabaseManager.Instance.GetPendingTradeOffers(manager.id);
        var freeAgents = DatabaseManager.Instance.GetFreeAgents()
            .Where(p => DatabaseManager.Instance.IsOwnRecentFA(p, myTeam.id))
            .ToDictionary(p => p.id, p => p);
        foreach (var offer in pending)
        {
            foreach (var id in offer.GetWantedPlayerIds())
            {
                if (freeAgents.ContainsKey(id))
                    result.Add(id);
            }
        }
        return result;
    }

    void GenerateAITradeOffersForPlayer(int gameDay, int seasonId, HashSet<int> tradedThisSeason)
    {
        var existingPending = DatabaseManager.Instance.GetPendingTradeOffers(_manager.id);
        if (existingPending.Count > 0) return;

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var targetedIds = new HashSet<int>();

        foreach (var aiTeam in allTeams.OrderBy(_ => _aiRng.Next(0, 1000)))
        {
            if (aiTeam.id == _myTeam.id) continue;
            if ((float)_aiRng.NextDouble() > 0.50f) continue;

            var aiRoster = DatabaseManager.Instance.GetPlayersByTeam(aiTeam.id);
            if (aiRoster.Count < 11) continue;

            var userRoster = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            if (DatabaseManager.Instance.GetTotalRosterCount(_myTeam.id) < 10) continue;

            var userHealthy = userRoster.Where(p => p.injury_days == 0).ToList();
            if (userHealthy.Count == 0) continue;

            // S&T de IA: proponer traspasar un FA reciente del usuario (Bird rights)
            if ((float)_aiRng.NextDouble() > 0.55f)
            {
                var userFAs = DatabaseManager.Instance.GetFreeAgents()
                    .Where(p => DatabaseManager.Instance.IsOwnRecentFA(p, _myTeam.id) && p.GetCalculatedAverage() > 75)
                    .OrderByDescending(p => p.GetCalculatedAverage())
                    .ToList();
                var faTarget = userFAs.FirstOrDefault(p => !targetedIds.Contains(p.id));
                if (faTarget != null)
                {
                    targetedIds.Add(faTarget.id);
                    var aiHealthyForSA = aiRoster
                        .Where(p => p.injury_days == 0 && p.overall < 87
                                    && !tradedThisSeason.Contains(p.id) && p.is_rookie == 0)
                        .ToList();
                    if (aiHealthyForSA.Count > 0)
                    {
                        long cap = GetValueCap();
                        int draftYear = _season?.year_end ?? 0;
                        double faValue = TradeValueHelper.PlayerValue(faTarget, cap);

                        // Valor: nunca ofrecer a alguien claramente mejor que el FA pedido
                        // y buscar el paquete de valor más parecido (antes: salario parecido,
                        // que no dice nada del talento).
                        var faPack = aiHealthyForSA
                            .Where(p => p.overall <= faTarget.overall + TradeValueHelper.MaxOvrGap)
                            .OrderBy(p => Math.Abs(TradeValueHelper.PlayerValue(p, cap) - faValue))
                            .Take(2)
                            .ToList();
                        if (faPack.Count > 0)
                        {
                            var futurePicks = GetFuturePicksForTeam(aiTeam.id, true);
                            var saPicks = new List<DraftPickData>();
                            if (futurePicks.Count > 0 && (float)_aiRng.NextDouble() > 0.4f)
                                saPicks.Add(futurePicks[0]);

                            var saCheck = TradeValueHelper.CheckOffer(GetTeamStrategy(aiTeam.id), faPack, saPicks,
                                new List<PlayerData> { faTarget }, null, cap, draftYear);
                            if (saCheck.Sane && saCheck.Outgoing >= saCheck.Incoming * TradeValueHelper.MinOfferRatio)
                            {
                                DatabaseManager.Instance.AddTradeOffer(new TradeOfferData
                                {
                                    manager_id = _manager.id,
                                    team_id_from = aiTeam.id,
                                    player_ids_out = TradeOfferData.JoinIds(new List<int> { faTarget.id }),
                                    player_ids_in = TradeOfferData.JoinIds(faPack.Select(p => p.id).ToList()),
                                    pick_ids_out = TradeOfferData.JoinIds(new List<int>()),
                                    pick_ids_in = TradeOfferData.JoinIds(saPicks.Select(pk => pk.id).ToList()),
                                    day_sent = gameDay,
                                    processed = 0
                                });
                                Debug.Log($"[AITrades] {aiTeam.name} propone S&T por {faTarget.first_name} {faTarget.last_name} (FA propio del usuario)");
                                break;
                            }
                            Debug.Log($"[AITrades] {aiTeam.name} descarta el S&T por {faTarget.last_name}: {saCheck.Reason ?? "valor desequilibrado"}");
                        }
                    }
                }
            }

            var aiStrategy = GetTeamStrategy(aiTeam.id);
            var target = PickTradeTarget(userHealthy, aiRoster, targetedIds, aiStrategy);
            if (target == null) continue;

            targetedIds.Add(target.id);

            var aiHealthy = aiRoster
                .Where(p => p.injury_days == 0 && p.id != target.id)
                .ToList();
            if (aiHealthy.Count == 0) continue;

            var offerPack = BuildOfferPackage(aiHealthy, aiRoster.Count, target, userRoster, aiTeam, aiStrategy, tradedThisSeason);
            if (offerPack == null) continue;

            long valueCap = GetValueCap();
            int valueDraftYear = _season?.year_end ?? 0;
            double valueTolerance = TradeValueHelper.Tolerance(aiStrategy);
            bool isContender = IsTeamContender(aiTeam);

            var offeredPicks = new List<DraftPickData>();
            var wantedPicks = new List<DraftPickData>();

            // Compensación por VALOR (antes se comparaba sólo el mejor OVR del paquete, lo
            // que dejaba pasar "dos estrellas por un rol" e incluso pedía una ronda tuya).
            double incomingValue = TradeValueHelper.PlayerValue(target, valueCap);
            double outgoingValue = TradeValueHelper.PlayersValue(offerPack, valueCap);

            // La IA se queda corta: paga la diferencia con rondas (nunca por encima de lo
            // que su estrategia tolera).
            if (outgoingValue < incomingValue * 0.95)
            {
                bool deepGap = incomingValue - outgoingValue > outgoingValue * 0.5;
                foreach (var pk in GetFuturePicksForTeam(aiTeam.id, deepGap))
                {
                    double pickValue = TradeValueHelper.PickValue(pk, valueDraftYear);
                    if (offeredPicks.Any(x => x.id == pk.id)) continue;
                    if (outgoingValue + pickValue > incomingValue * valueTolerance) break;
                    offeredPicks.Add(pk);
                    outgoingValue += pickValue;
                }
            }
            // La IA entrega más valor (dentro de la tolerancia): pide una ronda tuya como
            // compensación. Nunca las dos cosas a la vez.
            else if (outgoingValue > incomingValue * 1.02)
            {
                var askPicks = GetFuturePicksForTeam(_myTeam.id, false);
                if (askPicks.Count > 0 && (float)_aiRng.NextDouble() < 0.5f)
                {
                    wantedPicks.Add(askPicks[0]);
                    incomingValue += TradeValueHelper.PickValue(askPicks[0], valueDraftYear);
                }
            }

            // Deadline: los contenders presionan y añaden su R1 como incentivo, si sigue
            // cuadrando el valor.
            if (IsDeadlineWeek() && isContender)
            {
                foreach (var pk in GetFuturePicksForTeam(aiTeam.id, true))
                {
                    if (offeredPicks.Any(x => x.id == pk.id)) continue;
                    double pickValue = TradeValueHelper.PickValue(pk, valueDraftYear);
                    if (outgoingValue + pickValue > incomingValue * valueTolerance) break;
                    offeredPicks.Add(pk);
                    outgoingValue += pickValue;
                }
            }

            // Puerta de valor: ni regalar talento (CheckOffer: nada mejor que el objetivo
            // ni entregar más valor del tolerado) ni plantear una oferta insultante.
            var offerCheck = TradeValueHelper.CheckOffer(aiStrategy, offerPack, offeredPicks,
                new List<PlayerData> { target }, wantedPicks, valueCap, valueDraftYear);
            if (!offerCheck.Sane || offerCheck.Outgoing < offerCheck.Incoming * TradeValueHelper.MinOfferRatio)
            {
                Debug.Log($"[AITrades] {aiTeam.name} descarta ofertar por {target.last_name} "
                        + $"({target.overall}): {offerCheck.Reason ?? "oferta demasiado baja"} "
                        + $"[da {offerCheck.Outgoing:0.0} / recibe {offerCheck.Incoming:0.0}]");
                continue;
            }

            var wantIds = new List<int> { target.id };
            var offeredIds = offerPack.Select(p => p.id).ToList();
            DatabaseManager.Instance.AddTradeOffer(new TradeOfferData
            {
                manager_id = _manager.id,
                team_id_from = aiTeam.id,
                player_ids_out = TradeOfferData.JoinIds(wantIds),
                player_ids_in = TradeOfferData.JoinIds(offeredIds),
                pick_ids_out = TradeOfferData.JoinIds(wantedPicks.Select(pk => pk.id).ToList()),
                pick_ids_in = TradeOfferData.JoinIds(offeredPicks.Select(pk => pk.id).ToList()),
                day_sent = gameDay,
                processed = 0
            });
            break;
        }
    }

    PlayerData PickTradeTarget(List<PlayerData> userHealthy, List<PlayerData> aiRoster,
                                HashSet<int> excludedIds, TeamStrategy aiStrategy = TeamStrategy.Balanced)
    {
        var roll = (float)_aiRng.NextDouble();

        // Excluir rookies y jugadores ya excluidos
        var tradeable = userHealthy
            .Where(p => !excludedIds.Contains(p.id) && p.is_rookie == 0)
            .ToList();
        if (tradeable.Count == 0) return null;

        // La IA sólo persigue a quien PUEDE pagar: nunca objetivos muy por encima de su
        // mejor jugador traspasable (no se plantea pedir una estrella para regalar un rol).
        // Los contenders tienen algo más de margen porque completan el pago con rondas.
        int bestTradeable = aiRoster
            .Where(p => p.overall < 87 && p.is_rookie == 0)
            .Select(p => p.overall)
            .DefaultIfEmpty(0)
            .Max();
        if (bestTradeable > 0)
        {
            tradeable = tradeable
                .Where(p => p.overall <= bestTradeable + TradeValueHelper.TargetOvrMargin(aiStrategy))
                .ToList();
            if (tradeable.Count == 0) return null;
        }

        // Trade block: si el usuario ha puesto jugadores en el mercado, la IA
        // los prioriza con un 60 % de probabilidad (siempre que encajen en la
        // estrategia del equipo AI).
        var onBlock = tradeable
            .Where(p => p.on_trade_block == 1)
            .ToList();
        if (onBlock.Count > 0 && _aiRng.NextDouble() < 0.60f)
        {
            var validBlock = onBlock
                .Where(p =>
                    aiStrategy == TeamStrategy.Contend ? p.overall >= 75 :
                    aiStrategy == TeamStrategy.Rebuild ? (p.age <= 28 || p.overall >= 70) : true)
                .ToList();
            if (validBlock.Count > 0)
                return validBlock[_aiRng.Next(validBlock.Count)];
        }

        // Los contenders persiguen a tus mejores jugadores (estrellas)
        if (aiStrategy == TeamStrategy.Contend && roll < 0.75f)
        {
            var starCandidates = tradeable
                .Where(p => p.overall >= 83)
                .OrderByDescending(p => p.overall)
                .ToList();
            if (starCandidates.Count > 0)
                return starCandidates[_aiRng.Next(0, Mathf.Min(2, starCandidates.Count))];
        }

        // Los rebuild buscan activos jóvenes (los explotan o los roban baratos)
        if (aiStrategy == TeamStrategy.Rebuild && roll < 0.30f)
        {
            var youngCandidates = tradeable
                .Where(p => p.age <= 26)
                .OrderByDescending(p => p.overall)
                .ToList();
            if (youngCandidates.Count > 0)
                return youngCandidates[_aiRng.Next(0, Mathf.Min(2, youngCandidates.Count))];
        }

        if (roll < 0.35f)
        {
            var aiWeakPos = GetWeakestPosition(aiRoster);
            if (aiWeakPos != null)
            {
                var candidates = tradeable
                    .Where(p => p.position == aiWeakPos)
                    .OrderByDescending(p => p.overall)
                    .ToList();
                if (candidates.Count > 0)
                    return candidates[_aiRng.Next(0, Mathf.Min(3, candidates.Count))];
            }
        }

        if (roll < 0.60f)
        {
            var candidates = tradeable
                .OrderByDescending(p => p.overall)
                .ToList();
            if (candidates.Count > 0)
                return candidates[_aiRng.Next(0, Mathf.Min(3, candidates.Count))];
        }

        if (roll < 0.80f)
        {
            var candidates = tradeable
                .OrderByDescending(p => p.salary)
                .ToList();
            if (candidates.Count > 0)
                return candidates[_aiRng.Next(0, Mathf.Min(3, candidates.Count))];
        }

        return tradeable[_aiRng.Next(0, tradeable.Count)];
    }

    List<PlayerData> BuildOfferPackage(List<PlayerData> aiAvailable, int aiRosterCount,
                                        PlayerData target, List<PlayerData> userRoster, TeamData aiTeam,
                                        TeamStrategy aiStrategy, HashSet<int> tradedThisSeason)
    {
        long userPayroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(_myTeam.id);
        var fullAiRoster = DatabaseManager.Instance.GetPlayersByTeam(aiTeam.id);
        long aiPayroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(aiTeam.id);

        bool aiHardCapped = aiTeam.first_apron_hard_capped == 1;
        bool userHardCapped = _myTeam.first_apron_hard_capped == 1;

        // Valor: el paquete ha de poder pagar al objetivo y no puede regalar más de lo
        // que la estrategia permite (ver TradeValueHelper).
        long cap = GetValueCap();
        double targetValue = TradeValueHelper.PlayerValue(target, cap);
        double tolerance = TradeValueHelper.Tolerance(aiStrategy);

        // Protección universal: estrellas (OVR >= 87) nunca se ofrecen.
        // Contender además protege jóvenes prometedores (edad < 26 y OVR >= 82).
        // Rebuild solo ofrece veteranos (age >= 25).
        // Nadie claramente mejor que el objetivo: un 84/85 no se entrega por un 76 por
        // mucho que los salarios encajen (MaxOvrGap).
        var tradeable = aiAvailable.Where(p => !tradedThisSeason.Contains(p.id)
                                              && p.is_rookie == 0 && p.overall < 87
                                              && p.overall <= target.overall + TradeValueHelper.MaxOvrGap).ToList();
        if (aiStrategy == TeamStrategy.Contend)
            tradeable = tradeable.Where(p => !(p.age < 26 && p.overall >= 82)).ToList();
        if (aiStrategy == TeamStrategy.Rebuild)
            tradeable = tradeable.Where(p => p.age >= 25).ToList();
        if (tradeable.Count == 0) return null;

        bool CapOk(List<PlayerData> pack) =>
            TradeHelper.ValidateTrade(pack, new List<PlayerData> { target },
                DatabaseManager.Instance.GetRosterCount(aiTeam.id),
                DatabaseManager.Instance.GetRosterCount(_myTeam.id), _myTeam.name, userPayroll,
                aiTeam.name, aiPayroll, aiHardCapped, userHardCapped,
                teamATwoWayCount: DatabaseManager.Instance.GetTwoWayCount(aiTeam.id),
                teamBTwoWayCount: DatabaseManager.Instance.GetTwoWayCount(_myTeam.id),
                teamATotalActiveRoster: DatabaseManager.Instance.GetTotalRosterCount(aiTeam.id),
                teamBTotalActiveRoster: DatabaseManager.Instance.GetTotalRosterCount(_myTeam.id)).Count == 0;

        // 1-por-1: el valor más parecido al del objetivo (antes se ordenaba por salario,
        // que no dice nada del talento), y nunca por encima de lo que la estrategia tolera.
        foreach (var p in tradeable.OrderBy(p => Math.Abs(TradeValueHelper.PlayerValue(p, cap) - targetValue)))
        {
            if (TradeValueHelper.PlayerValue(p, cap) > targetValue * tolerance) continue;
            var pack = new List<PlayerData> { p };
            if (CapOk(pack)) return pack;
        }

        // 2-por-1: combinaciones ordenadas por cercanía de valor (profundidad por calidad).
        var pairs = new List<(double distance, List<PlayerData> pack)>();
        for (int i = 0; i < tradeable.Count; i++)
        {
            for (int j = i + 1; j < tradeable.Count; j++)
            {
                double v = TradeValueHelper.PlayerValue(tradeable[i], cap)
                         + TradeValueHelper.PlayerValue(tradeable[j], cap);
                if (v > targetValue * tolerance) continue;
                pairs.Add((Math.Abs(v - targetValue), new List<PlayerData> { tradeable[i], tradeable[j] }));
            }
        }
        foreach (var (_, pack) in pairs.OrderBy(x => x.distance))
            if (CapOk(pack)) return pack;

        // 3-por-1 (sólo si la plantilla lo aguanta), mismo criterio de valor.
        if (aiRosterCount - 3 >= 10)
        {
            var triples = new List<(double distance, List<PlayerData> pack)>();
            for (int i = 0; i < tradeable.Count; i++)
            {
                for (int j = i + 1; j < tradeable.Count; j++)
                {
                    for (int k = j + 1; k < tradeable.Count; k++)
                    {
                        double v = TradeValueHelper.PlayerValue(tradeable[i], cap)
                                 + TradeValueHelper.PlayerValue(tradeable[j], cap)
                                 + TradeValueHelper.PlayerValue(tradeable[k], cap);
                        if (v > targetValue * tolerance) continue;
                        triples.Add((Math.Abs(v - targetValue),
                            new List<PlayerData> { tradeable[i], tradeable[j], tradeable[k] }));
                    }
                }
            }
            foreach (var (_, pack) in triples.OrderBy(x => x.distance))
                if (CapOk(pack)) return pack;
        }

        return null;
    }

    /// <summary>
    /// Cap salarial para la valoración de traspasos (crece +5 %/año; nunca usar la
    /// constante a pelo para no desincronizarse con la partida).
    /// </summary>
    long GetValueCap()
    {
        var ls = DatabaseManager.Instance.GetLeagueSettings();
        return ls != null && ls.salary_cap > 0 ? ls.salary_cap : TradeHelper.SALARY_CAP;
    }
}
