using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager
{
    public void ClearAllRookies()
    {
        if (!EnsureDb()) return;
        _db.Execute("UPDATE players SET is_rookie = 0");
    }

    /// <summary>Estado persistido del draft de una temporada (null si no hay).</summary>
    public DraftStateData GetDraftState(int seasonId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<DraftStateData>().FirstOrDefault(d => d.season_id == seasonId);
    }

    /// <summary>Guarda el progreso del draft (upsert por temporada).</summary>
    public void SaveDraftState(int seasonId, int managerId, int currentPickIndex, bool completed)
    {
        if (!EnsureDb()) return;
        _db.InsertOrReplace(new DraftStateData
        {
            season_id = seasonId,
            manager_id = managerId,
            current_pick_index = currentPickIndex,
            completed = completed ? 1 : 0,
            updated_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    public void StartNewSeason(int oldSeasonId, int newTeamId, string gameMode, int managerId, int prevTeamId = 0)
    {
        _db.BeginTransaction();
        try
        {
        // 0. Cargar la temporada anterior. Su archivado histórico de carrera lo hace
        // el Dashboard al llegar a la fase "finished"; aquí NO se repite (duplicaba
        // los totales de carrera).
        var oldSeason = _db.Find<SeasonData>(oldSeasonId);

        // 0b. Archive per-season stats for career history
        _db.Execute("DELETE FROM player_season_stats WHERE season_id = ?", oldSeasonId);
        _db.Execute(@"INSERT INTO player_season_stats
            (player_id, season_id, year_start, year_end, team_id,
             team_abbreviation, team_name,
             games, total_minutes, total_points, total_rebounds, total_assists,
             total_steals, total_blocks, total_rating,
             total_fgm, total_fga, total_fg3m, total_fg3a,
             total_ftm, total_fta, total_turnovers, total_oreb, total_dreb)
            SELECT ps.player_id, g.season_id, s.year_start, s.year_end,
                   ps.team_id, t.abbreviation, t.name,
                   COUNT(*), SUM(ps.minutes), SUM(ps.points), SUM(ps.rebounds),
                   SUM(ps.assists), SUM(ps.steals), SUM(ps.blocks), SUM(ps.rating),
                   SUM(ps.fgm), SUM(ps.fga), SUM(ps.fg3m), SUM(ps.fg3a),
                   SUM(ps.ftm), SUM(ps.fta), SUM(ps.turnovers),
                   SUM(ps.oreb), SUM(ps.dreb)
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN seasons s ON g.season_id = s.id
            LEFT JOIN teams t ON ps.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1
                  AND g.game_type = 'regular'
            GROUP BY ps.player_id, ps.team_id", oldSeasonId, oldSeasonId);

        var allPlayers = _db.Table<PlayerData>().ToList();

        // 1. Retire players 40+ (capturando a los que entran al Salón de la Fama)
        string retireSeasonLabel = "";
        if (oldSeason != null)
            retireSeasonLabel = oldSeason.year_end.ToString();
        foreach (var p in allPlayers.Where(p => p.age >= 40))
        {
            RemoveCapHold(p.id);
            TryInductIntoHallOfFame(p, retireSeasonLabel);
            TryRetireNumber(p, retireSeasonLabel);
            _db.Delete(p);
        }

        // 2. Age + attribute changes (progression/regression by career phase)
        var remaining = _db.Table<PlayerData>().ToList();

        // Pre-compute team win% for player option decisions
        var teamWinPctCache = new Dictionary<int, float>();
        var teamOptionSettings = GetLeagueSettings();
        if (teamOptionSettings != null && oldSeasonId > 0)
        {
            var seasonGames = _db.Table<GameData>()
                .Where(g => g.season_id == oldSeasonId && g.is_played == 1)
                .ToList();
            var teamGames = new Dictionary<int, (int wins, int total)>();
            foreach (var g in seasonGames)
            {
                if (!teamGames.ContainsKey(g.home_team_id)) teamGames[g.home_team_id] = (0, 0);
                if (!teamGames.ContainsKey(g.away_team_id)) teamGames[g.away_team_id] = (0, 0);
                teamGames[g.home_team_id] = (teamGames[g.home_team_id].wins + (g.home_score > g.away_score ? 1 : 0), teamGames[g.home_team_id].total + 1);
                teamGames[g.away_team_id] = (teamGames[g.away_team_id].wins + (g.away_score > g.home_score ? 1 : 0), teamGames[g.away_team_id].total + 1);
            }
            foreach (var kv in teamGames)
                teamWinPctCache[kv.Key] = kv.Value.total > 0 ? (float)kv.Value.wins / kv.Value.total : 0.5f;
        }

        // Minutos por partido de la temporada anterior: modulan el desarrollo.
        var mpgByPlayer = new Dictionary<int, int>();
        foreach (var ss in _db.Table<PlayerSeasonStatRow>().Where(x => x.season_id == oldSeasonId))
            mpgByPlayer[ss.player_id] = ss.games > 0 ? (int)System.Math.Round(ss.total_minutes / ss.games) : -1;

        // Valor de mercado del tope para reajustar el precio pedido al expirar (una vez).
        var holdSettings = GetLeagueSettings();
        long rolloverCap = holdSettings?.salary_cap ?? TradeHelper.SALARY_CAP;

        foreach (var p in remaining)
        {
            // B41: un rookie en su primer año envejece y progresa como cualquier otro;
            // lo único que NO hace es decrementar su contrato de rookie (todavía).
            bool isFirstYearRookie = p.is_rookie == 1;
            int serviceTeamId = p.team_id;
            bool wasTwoWay = p.is_two_way == 1;
            if (serviceTeamId != 0 && p.team_id != newTeamId)
                p.years_of_service++;

            p.age += 1;

            // Position priority attributes (get +1 extra)
            var priorityAttrs = new HashSet<string>();
            switch (p.position)
            {
                case "PG":
                    priorityAttrs = new HashSet<string> { "passing", "dribbling", "speed", "iq", "three_point" };
                    break;
                case "SG":
                    priorityAttrs = new HashSet<string> { "shooting", "three_point", "speed", "dribbling", "steals" };
                    break;
                case "SF":
                    priorityAttrs = new HashSet<string> { "shooting", "defense", "athleticism", "speed", "rebounding" };
                    break;
                case "PF":
                    priorityAttrs = new HashSet<string> { "defense", "rebounding", "blocks", "athleticism" };
                    break;
                case "C":
                    priorityAttrs = new HashSet<string> { "rebounding", "blocks", "defense", "iq", "athleticism" };
                    break;
            }

            int GetPositionDeclineModifier(string pos, string attr, int age)
            {
                int mod = 0;

                // Declive atlético general ≥ 31
                if (age >= 31 && (attr == "speed" || attr == "athleticism"))
                {
                    mod -= 1;
                    // Declive severo ≥ 35: guards pierden más físico, bigs pierden defensa/blocks
                    if (age >= 35)
                    {
                        if (pos == "PG" || pos == "SG") mod -= 1;
                        else if (pos == "PF" || pos == "C")
                        {
                            if (attr == "blocks" || attr == "defense") mod -= 1;
                        }
                        else mod -= 1; // SF
                    }
                }

                // IQ veterano ≥ 28: guards y wings ganan IQ y tiro
                if (age >= 28)
                {
                    if (attr == "iq") mod += 1;
                    if (attr == "shooting" && (pos == "PG" || pos == "SG" || pos == "SF"))
                        mod += 1;
                }

                return mod;
            }

            int Apply(string name, int current)
            {
                int change = PlayerProgression.AttributeDelta(
                    p.age,
                    p.potential,
                    p.overall,
                    mpgByPlayer.TryGetValue(p.id, out var mpg) ? mpg : -1,
                    priorityAttrs.Contains(name),
                    Rng.Next(-1, 2));
                change += GetPositionDeclineModifier(p.position, name, p.age);
                return PlayerAttributeScale.ClampValid(current + change);
            }

            p.speed = Apply("speed", p.speed);
            p.shooting = Apply("shooting", p.shooting);
            p.three_point = Apply("three_point", p.three_point);
            p.passing = Apply("passing", p.passing);
            p.dribbling = Apply("dribbling", p.dribbling);
            p.defense = Apply("defense", p.defense);
            p.rebounding = Apply("rebounding", p.rebounding);
            p.athleticism = Apply("athleticism", p.athleticism);
            p.iq = Apply("iq", p.iq);
            p.steals = Apply("steals", p.steals);
            p.blocks = Apply("blocks", p.blocks);

            // Overall = media de atributos capada por potencial (fuente única).
            p.RecalculateOverall();

            // Save old team for seasons_with_team tracking
            int oldTeamId = p.team_id;

            // 2.5. Resolve contract options before decrement
            // Skip team options for the manager's team (already handled in NewSeason modal)
            if (p.has_team_option == 1 && p.guaranteed_years == 0 && p.contract_years > 0
                && p.team_id != newTeamId)
            {
                if (DecideTeamOption(p))
                {
                    p.contract_years += 1;
                    p.guaranteed_years = p.contract_years;
                }
                p.has_team_option = 0;
            }
            // Player options are always decided by the AI (player decides)
            if (p.has_player_option == 1 && p.guaranteed_years == 0 && p.contract_years > 0)
            {
                float teamPct = teamWinPctCache.TryGetValue(p.team_id, out float wp) ? wp : 0.5f;
                if (DecidePlayerOption(p, teamPct, teamOptionSettings))
                {
                    p.contract_years += 1;
                    p.guaranteed_years = p.contract_years;
                }
                p.has_player_option = 0;
            }

            // 3. Decrement contracts
            // El equipo del manager ya decrementó contratos y resolvió FAs en
            // NewSeasonController (CheckRosterAndStart) antes del control de
            // plantilla — no volver a restar el año.
            if (p.team_id == newTeamId)
            {
                _db.Update(p);
                continue;
            }
            // B41: el contrato de rookie no decrementa en su primer año (pero el jugador
            // ya envejeció y progresó arriba).
            if (RolloverRules.ShouldDecrementContract(isFirstYearRookie))
            {
                p.contract_years = System.Math.Max(0, p.contract_years - 1);
                p.guaranteed_years = System.Math.Max(0, p.guaranteed_years - 1);
            }
            if (p.contract_years <= 0)
            {
                p.contract_years = 0;
                p.guaranteed_years = 0;
                p.has_team_option = 0;
                p.has_player_option = 0;
                p.is_two_way = 0;
                p.is_on_ir = 0;
                p.g_league_assigned = 0;
                if (oldTeamId != 0 && p.last_team_id == 0)
                    p.last_team_id = oldTeamId; // conserva Bird rights/contexto de re-firma al ir a FA
                p.team_id = 0;

                if (oldTeamId != 0 && !wasTwoWay)
                    SaveCapHold(p, oldTeamId, oldSeasonId,
                        TradeHelper.MinimumSalary(holdSettings, p.years_of_service));

                // El precio pedido no se queda anclado al salario viejo: si está por debajo
                // de su valor de mercado (p. ej. fichó por el mínimo tras una rescisión), sube.
                long marketValue = TradeHelper.MarketValueByOvr(p.overall, rolloverCap);
                if (marketValue > p.salary) p.salary = marketValue;
            }

            // Track team changes for seasons_with_team
            if (p.team_id == 0)
            {
                // Free agent — keep current seasons_with_team
            }
            else if (oldTeamId == p.team_id)
            {
                p.seasons_with_team += 1;  // Same team
            }
            else
            {
                p.seasons_with_team = 1;   // New team (traded, or FA signed)
            }

            _db.Update(p);
        }

        // 3a. Mentoring: veteranos enseñan a jóvenes
        ApplyMentoring(remaining);

        // 3b. Decrement employee contracts
        var allEmployees = _db.Table<EmployeeData>().Where(e => e.team_id != 0).ToList();
        foreach (var emp in allEmployees)
        {
            emp.contract_years -= 1;
            if (emp.contract_years <= 0)
                _db.Delete(emp);
            else
                _db.Update(emp);
        }

        // 4. Decrement sponsor/TV contracts for all teams
        foreach (var team in GetAllTeams())
        {
            var settings = GetTeamSettings(team.id);
            if (settings == null) continue;

            bool changed = false;
            if (settings.sponsor_years_remaining > 0)
            {
                settings.sponsor_years_remaining -= 1;
                if (settings.sponsor_years_remaining <= 0)
                {
                    settings.sponsor_id = 0;
                    // Fire the sponsor so it becomes available again
                    var activeSponsor = GetActiveSponsor(team.id);
                    if (activeSponsor != null)
                        FireSponsor(activeSponsor);
                    RenewMarketDealsForTeam(team, settings);
                }
                changed = true;
            }
            if (settings.tv_years_remaining > 0)
            {
                settings.tv_years_remaining -= 1;
                if (settings.tv_years_remaining <= 0)
                {
                    settings.tv_channel_id = 0;
                    var activeChannel = GetActiveTVChannel(team.id);
                    if (activeChannel != null)
                        FireTVChannel(activeChannel);
                    RenewMarketDealsForTeam(team, settings);
                }
                changed = true;
            }
            if (changed)
                UpdateTeamSettings(settings);
        }

        // 4b. Archive manager career stats from the completed season
        if (prevTeamId > 0 && oldSeasonId > 0)
        {
            try
            {
                var seasonGames = _db.Table<GameData>()
                    .Where(g => g.manager_id == managerId && g.season_id == oldSeasonId && g.is_played == 1)
                    .ToList();

                if (seasonGames.Count > 0)
                {
                    var manager = _db.Find<ManagerData>(managerId);
                    if (manager != null)
                    {
                        // Regular season
                        var regGames = seasonGames
                            .Where(g => g.game_type == "regular"
                                && (g.home_team_id == prevTeamId || g.away_team_id == prevTeamId))
                            .ToList();
                        int regW = regGames.Count(g =>
                            (g.home_team_id == prevTeamId && g.home_score > g.away_score) ||
                            (g.away_team_id == prevTeamId && g.away_score > g.home_score));
                        manager.career_reg_wins += regW;
                        manager.career_reg_losses += regGames.Count - regW;

                        // Playoff / Play-In
                        var poGames = seasonGames
                            .Where(g => (g.game_type == "playoff" || g.game_type == "playin")
                                && (g.home_team_id == prevTeamId || g.away_team_id == prevTeamId))
                            .ToList();
                        int poW = poGames.Count(g =>
                            (g.home_team_id == prevTeamId && g.home_score > g.away_score) ||
                            (g.away_team_id == prevTeamId && g.away_score > g.home_score));
                        manager.career_po_wins += poW;
                        manager.career_po_losses += poGames.Count - poW;

                        // Championship
                        if (oldSeason != null)
                        {
                            string seasonLabel = $"{oldSeason.year_start}-{oldSeason.year_end.ToString().Substring(2)}";
                            var finalsRecord = _db.Table<FinalsRecord>()
                                .FirstOrDefault(f => f.season == seasonLabel);
                            if (finalsRecord != null)
                            {
                                var oldTeam = GetTeamById(prevTeamId);
                                if (oldTeam != null && finalsRecord.champ_name == oldTeam.name)
                                    manager.championships += 1;
                            }
                        }

                        manager.seasons_completed += 1;
                        _db.Update(manager);
                        Debug.Log($"[DB] Archived career stats for manager {managerId}: reg {manager.career_reg_wins}-{manager.career_reg_losses}, po {manager.career_po_wins}-{manager.career_po_losses}, rings {manager.championships}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DB] Error archiving manager career stats: {ex.Message}");
            }
        }

        // 5. Archive attendance summary before clearing
        try
        {
            var oldSeasonData = _db.Find<SeasonData>(oldSeasonId);
            if (oldSeasonData != null)
            {
                var gameIds = _db.Table<GameData>()
                    .Where(g => g.season_id == oldSeasonId && g.is_played == 1)
                    .Select(g => g.id)
                    .ToList();

                long totalAtt = 0;
                long totalCap = 0;
                foreach (var gid in gameIds)
                {
                    var att = _db.Table<GameAttendanceData>().Where(a => a.game_id == gid).FirstOrDefault();
                    if (att != null && att.attendance > 0)
                    {
                        totalAtt += att.attendance;
                        var game = _db.Table<GameData>().Where(g => g.id == gid).FirstOrDefault();
                        if (game != null)
                        {
                            var homeTeam = _db.Table<TeamData>().Where(t => t.id == game.home_team_id).FirstOrDefault();
                            if (homeTeam != null) totalCap += homeTeam.capacity;
                        }
                    }
                }

                float occPct = totalCap > 0 ? (float)totalAtt / totalCap * 100f : 0;
                _db.InsertOrReplace(new SeasonAttendanceSummaryData
                {
                    season_id = oldSeasonId,
                    manager_id = managerId,
                    total_attendance = (int)totalAtt,
                    total_capacity = (int)totalCap,
                    occupancy_pct = occPct
                });
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Error archiving attendance summary: {ex.Message}");
        }

        // Draft order projection captured before clearing games (for pick positions).
        var projectedDraftOrder = BuildDraftOrder(oldSeason != null ? (int?)oldSeason.id : null);

        // 5. Clear tables
        // Punto seguro: si se cambió de partida durante el rollover, abortar aquí
        // (rollback completo) antes de borrar datos.
        ThrowIfBatchCancelled();
        _db.Execute("DELETE FROM player_game_stats");
        _db.Execute("DELETE FROM finals_player_stats");
        _db.Execute("DELETE FROM games");
        _db.Execute("DELETE FROM messages");
        _db.Execute("DELETE FROM game_attendance");
        _db.Execute("DELETE FROM finance_records");

        // 5a. Ciclo de vida G-League (envejecimiento, retiro, progresión, re-seeding)
        AdvanceGLeagueLifecycle();

        // 5b. Trim AI teams to max 17
        TrimRostersToMaxSize(TradeHelper.MAX_STANDARD_ROSTER, newTeamId);

        // 6. Fill rosters to 15 for all teams (except the user's new team)
        var allTeams = GetAllTeams();
        var freeAgents = _db.Table<PlayerData>()
            .Where(p => p.team_id == 0 && p.age < 40)
            .OrderByDescending(p => p.overall)
            .ToList();

        var capSettings = GetLeagueSettings();
        long salaryCap = capSettings?.salary_cap ?? TradeHelper.SALARY_CAP;

        // Sort AI teams by average overall rating descending (best teams sign first)
        var teamsToFill = allTeams
            .Where(t => t.id != newTeamId)
            .Select(t =>
            {
                var r = GetPlayersByTeam(t.id);
                double avg = r.Count > 0 ? r.Average(p => (double)p.overall) : 0;
                return (team: t, avgOvr: avg);
            })
            .OrderByDescending(x => x.avgOvr)
            .Select(x => x.team)
            .ToList();

        const int targetRosterSize = 15;

        foreach (var team in teamsToFill)
        {
            var roster = GetPlayersByTeam(team.id);
            long payroll = GetEffectiveTeamPayroll(team.id);
            int rosterCount = GetRosterCount(team.id);
            int need = targetRosterSize - rosterCount;
            if (need <= 0) continue;

            var posCounts = new Dictionary<string, int>();
            foreach (string pos in new[] { "PG", "SG", "SF", "PF", "C" })
                posCounts[pos] = roster.Count(p => p.position == pos);

            for (int i = 0; i < need && freeAgents.Count > 0; i++)
            {
                string minPos = posCounts.OrderBy(kv => kv.Value).First().Key;

                PlayerData signed = null;
                foreach (var fa in freeAgents)
                {
                    bool maySign = fa.free_agent_type != "RFA" || fa.last_team_id == team.id;
                    if (maySign && fa.position == minPos)
                    {
                        signed = fa;
                        break;
                    }
                }
                if (signed == null)
                    signed = freeAgents.FirstOrDefault(fa => fa.free_agent_type != "RFA" || fa.last_team_id == team.id);

                if (signed != null)
                {
                    long hold = GetActiveCapHolds(team.id).Where(h => h.player_id == signed.id).Sum(h => h.amount);
                    payroll = Math.Max(0, payroll - hold);
                    long availableCap = salaryCap - payroll;
                    long faSalary = signed.salary;

                    long faMinimum = TradeHelper.MinimumSalary(capSettings, signed.years_of_service);
                    if (faSalary > availableCap)
                    {
                        faSalary = Math.Max(faMinimum, availableCap);
                    }
                    else if (faSalary < faMinimum)
                    {
                        faSalary = faMinimum;
                    }

                    signed.salary = faSalary;
                    signed.team_id = team.id;
                    signed.contract_years = 1;
                    signed.guaranteed_years = 1;
                    signed.seasons_with_team = 1;
                    signed.free_agent_type = "";
                    signed.qualifying_offer_salary = 0;
                    RemoveCapHold(signed.id);
                    _db.Update(signed);
                    freeAgents.Remove(signed);
                    payroll += faSalary;
                    posCounts[signed.position] = posCounts.GetValueOrDefault(signed.position) + 1;
                }
            }
        }

        // Seed relationships for user's team
        var userPlayers = GetPlayersByTeam(newTeamId);
        if (userPlayers.Count >= 2)
        {
            SeedTeamPersonalities(newTeamId, userPlayers);
            SeedTeamRelationships(newTeamId, userPlayers);
        }
        AutoSeedLineup(newTeamId, userPlayers);

        // 7. Topes de la nueva temporada desde la tabla CBA (F4-07). La tabla sustituye
        // el crecimiento ciego del +5 %; el mínimo sigue congelado en el suelo interno
        // hasta F4-08 (mínimo por experiencia).
        var leagueSettings = GetLeagueSettings();
        int nextSeasonStartYear = oldSeason != null
            ? oldSeason.year_start + 1
            : (leagueSettings != null && leagueSettings.season_start_year > 0
                ? leagueSettings.season_start_year + 1
                : CbaTable.CurrentSeasonStartYear + 1);
        if (leagueSettings != null)
        {
            CbaTable.ApplyTo(leagueSettings, nextSeasonStartYear);
            _db.Update(leagueSettings);
        }

        // 7b. El hard cap por uso del NT-MLE es ANUAL: se libera para todos los equipos
        // al empezar la temporada (antes quedaba activo de por vida).
        _db.Execute("UPDATE teams SET first_apron_hard_capped = 0 WHERE first_apron_hard_capped = 1");

        // 7c. Fijar el equipo del manager para la nueva temporada DENTRO de la
        // transacción: si el rollover falla, no queda cambiado a medias.
        var managerRow = _db.Find<ManagerData>(managerId);
        if (managerRow != null && managerRow.team_id != newTeamId)
        {
            managerRow.team_id = newTeamId;
            _db.Update(managerRow);
        }

        // 7d. Ofertas pendientes: su day_sent es de la temporada anterior y con el
        // contador a 0 madurarían casi un año tarde. Se normalizan (maduran en 7 días
        // de la nueva temporada) y se descartan las ofertas de traspaso obsoletas.
        try
        {
            _db.Execute("UPDATE offers SET day_sent = 0 WHERE processed = 0");
            _db.Execute("DELETE FROM trade_offers WHERE processed = 0");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[DB] No se pudieron normalizar las ofertas del rollover: {ex.Message}");
        }

        // 7e. Reset del estado transitorio de temporada: lesiones, fatiga, trade block,
        // química y confianza no deben viajar entre temporadas.
        _db.Execute("UPDATE players SET injury_days = 0, injury_type = '', treated = 0, fisico = 99, on_trade_block = 0, waived_game_day = -1");
        _db.Execute("UPDATE teams SET team_chemistry = 50");
        _db.Execute("UPDATE managers SET fan_confidence = 50");

        // 7f. Hilos narrativos: se cierran al acabar la temporada (el histórico se conserva).
        AbandonActiveStorylines(managerId);

        // 8. Deactivate old season
        if (oldSeason != null)
        {
            oldSeason.is_active = 0;
            _db.Update(oldSeason);
        }

        // 9. Assign random sponsors/TV to teams without one
        var availableSponsors = _db.Table<SponsorData>().Where(s => s.is_active == 1).ToList();
        var availableChannels = _db.Table<TvChannelData>().Where(c => c.is_active == 1).ToList();

        foreach (var team in allTeams)
        {
            var settings = GetTeamSettings(team.id);
            if (settings == null) continue;
            if (team.id == newTeamId) continue;

            if (settings.sponsor_id == 0 && availableSponsors.Count > 0)
            {
                var rngSp = new System.Random();
                var pick = availableSponsors[rngSp.Next(availableSponsors.Count)];
                SignSponsor(pick.id, 0, team.id);
                // Re-read available sponsors (the signed one is now assigned)
                availableSponsors = _db.Table<SponsorData>().Where(s => s.is_active == 1).ToList();
            }

            if (settings.tv_channel_id == 0 && availableChannels.Count > 0)
            {
                var rngTv = new System.Random();
                var pick = availableChannels[rngTv.Next(availableChannels.Count)];
                SignTVChannel(pick.id, 0, team.id);
                availableChannels = _db.Table<TvChannelData>().Where(c => c.is_active == 1).ToList();
            }
        }

        // 10. Create new season (preseason)
        int newYearStart = nextSeasonStartYear;
        var newSeason = new SeasonData
        {
            year_start = newYearStart,
            year_end = newYearStart + 1,
            is_active = 1,
            current_game_day = 0,
            current_date = $"{newYearStart}-09-05",
            game_mode = gameMode,
            phase = "preseason",
            manager_id = managerId,
            generated = 0
        };
        _db.Insert(newSeason);

        // 11b. Deuda muerta (buyouts con stretch): cobrar el tramo de la nueva
        // temporada. dead_money es persistente (no la borra el rollover).
        var deadRows = _db.Table<DeadMoneyData>().ToList();
        foreach (var dm in deadRows)
        {
            var deadTeam = _db.Find<TeamData>(dm.team_id);
            if (deadTeam != null)
            {
                deadTeam.budget -= dm.annual_amount;
                _db.Update(deadTeam);
                _db.Insert(new FinanceRecord
                {
                    team_id = dm.team_id,
                    season_id = newSeason.id,
                    record_type = FinanceRecord.TYPE_BUYOUT,
                    game_day = 0,
                    amount = dm.annual_amount,
                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            dm.years_remaining -= 1;
            if (dm.years_remaining <= 0)
                _db.Delete(dm);
            else
                _db.Update(dm);
        }

        // Rollover de picks: borra el año drafteado, proyecta el nuevo draft
        // actual y añade el año más lejano (los intermedios se conservan).
        RolloverDraftPicks(newSeason, oldSeason, projectedDraftOrder);
        _db.Commit();
        }
        catch (System.Exception ex)
        {
        _db.Rollback();
        Debug.LogError($"[DB] StartNewSeason error, rolled back: {ex.Message}\n{ex.StackTrace}");
        throw;
        }
    }

    public void TrimRostersToMaxSize(int maxSize, int excludeTeamId)
    {
        var allTeams = GetAllTeams();
        foreach (var team in allTeams)
        {
            if (team.id == excludeTeamId) continue;
            var roster = GetPlayersByTeam(team.id);
            var standard = roster.Where(p => p.is_two_way == 0 && p.is_on_ir == 0)
                .OrderBy(p => p.GetCalculatedAverage()).ToList();
            int excess = Mathf.Max(0, standard.Count - maxSize);
            for (int i = 0; i < excess; i++)
            {
                var p = standard[i];
                p.team_id = 0;
                p.contract_years = 0;
                p.guaranteed_years = 0;
                p.has_team_option = 0;
                p.has_player_option = 0;
                p.is_two_way = 0;
                p.is_on_ir = 0;
                p.g_league_assigned = 0;
                _db.Update(p);
            }

            var twoWay = roster.Where(p => p.is_two_way == 1)
                .OrderBy(p => p.GetCalculatedAverage()).ToList();
            excess = Mathf.Max(0, twoWay.Count - TradeHelper.MAX_TWO_WAY);
            for (int i = 0; i < excess; i++)
            {
                var p = twoWay[i];
                p.team_id = 0;
                p.contract_years = 0;
                p.guaranteed_years = 0;
                p.has_team_option = 0;
                p.has_player_option = 0;
                p.is_two_way = 0;
                p.is_on_ir = 0;
                p.g_league_assigned = 0;
                _db.Update(p);
            }
        }
    }
}
