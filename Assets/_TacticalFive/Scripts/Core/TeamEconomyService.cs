using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Economía de la franquicia (F8-01). Agrupa las reglas y la persistencia que antes
/// vivían en DashboardController: asistencia, taquilla, patrocinio/TV del partido,
/// nómina mensual, empleados, luxury tax y abonos. El controller solo decide cuándo
/// se ejecutan y pinta el resultado.
/// </summary>
public static class TeamEconomyService
{
    public static readonly int[] PayrollDays = { 1, 16, 31, 46, 61, 76, 91, 106, 121, 136, 151, 166 };

    // Reparto fijo de derechos de liga/televisión nacional. Se paga junto a la
    // nómina para que la caja no dependa solo de los partidos en casa.
    public const long LeagueDistributionAnnual = 60_000_000;
    public static long LeagueDistributionShare => MonthlyShare(LeagueDistributionAnnual);

    // ── REGLAS PURAS ──────────────────────────────────────

    public static bool IsPayrollDay(int gameDay) => PayrollDays.Contains(gameDay);

    public static bool IsSubscriptionWindow(int gameDay) => gameDay >= 10 && gameDay <= 12;

    public static long MonthlyShare(long annualAmount) => annualAmount / 12;

    public static long TicketRevenue(int attendance, int ticketPrice, float multiplier)
        => (long)(attendance * ticketPrice * multiplier);

    public static float ArenaTicketMultiplier(int pabellonReputation)
    {
        return pabellonReputation switch
        {
            5 => 1.20f,
            4 => 1.15f,
            3 => 1.10f,
            2 => 1.06f,
            1 => 1.03f,
            _ => 1.0f
        };
    }

    public static float EarlySeasonWinMultiplier(int wins) => 1.0f + wins * 0.05f;

    public static float SubscriptionReputationMultiplier(int reputation)
        => 0.7f + (reputation / 5f) * 0.6f;

    public static float SubscriptionMarketMultiplier(int marketSize)
        => 0.90f + MarketEconomy.MarketNormalized(marketSize) * 0.25f;

    /// <summary>Abonados estimados; el random entra como factor 0.85–1.15 desde fuera.</summary>
    public static int SubscriptionDemand(int capacity, float subscriptionPrice, int earlyWins,
        int reputation, int marketSize, float randomFactor)
    {
        if (capacity <= 0) return 0;
        float baseRatio = 0.5f;
        float priceFactor = (2000f - subscriptionPrice) / 10000f;
        int demand = (int)(capacity * (baseRatio + priceFactor)
            * EarlySeasonWinMultiplier(earlyWins)
            * SubscriptionReputationMultiplier(reputation)
            * SubscriptionMarketMultiplier(marketSize)
            * randomFactor);
        return Mathf.Clamp(demand, 0, capacity);
    }

    public static int ObjectiveTargetPosition(string objective)
    {
        if (objective == "Campeonato") return 1;
        if (objective == "Playoffs") return 6;
        if (objective == "Play-In") return 10;
        return 0;
    }

    // ── ASISTENCIA ───────────────────────────────────────

    /// <summary>
    /// Asistencia (E3): demanda elástica según mercado, rival, racha y precio. Los
    /// rolls se generan aquí, en el main thread, y se pasan a MarketEconomy.
    /// </summary>
    public static int CalculateAttendance(GameData game, TeamData homeTeam,
        ManagerData manager, SeasonData season)
    {
        if (game == null || homeTeam == null || homeTeam.capacity <= 0
            || manager == null) return 0;

        bool myTeamIsHome = homeTeam.id == manager.team_id;
        var db = DatabaseManager.Instance;
        var allGames = db.GetStandingsGames(manager.id);

        var (homeWins, homePlayed) = TeamRecord(allGames, homeTeam.id);
        float homeWinPct = homePlayed > 0 ? (float)homeWins / homePlayed : 0.5f;

        var rival = db.GetTeamById(
            game.home_team_id == homeTeam.id ? game.away_team_id : game.home_team_id);
        var (rivalWins, rivalPlayed) = TeamRecord(allGames, rival != null ? rival.id : 0);
        float rivalWinPct = rivalPlayed > 0 ? (float)rivalWins / rivalPlayed : 0.5f;

        float objectiveFactor = GetObjectiveComplianceFactor(homeTeam, allGames, db);
        float streakFactor = GetAttendanceStreakFactor(allGames, homeTeam.id);

        int ticketPrice = 50;
        var finSettings = db.GetTeamSettings(homeTeam.id);
        if (finSettings != null) ticketPrice = (int)finSettings.ticket_price;

        int attendance = MarketEconomy.EstimateHomeAttendance(
            homeTeam.capacity,
            homeWinPct,
            rivalWinPct,
            rival != null ? rival.reputation : 3,
            homeTeam.market_size,
            homeTeam.reputation,
            objectiveFactor,
            streakFactor,
            manager.fan_confidence / 100f,
            myTeamIsHome,
            ticketPrice,
            UnityEngine.Random.value,
            UnityEngine.Random.value);

        // F7-05: el entorno del pabellón mejora la asistencia de los partidos del usuario.
        if (myTeamIsHome && season != null)
        {
            int level = db.GetCityProjectLevel(manager.id, season.id, CityCatalog.ProjectArena);
            attendance = Mathf.RoundToInt(attendance * CityCatalog.AttendanceMultiplier(level));
        }

        return Mathf.Clamp(attendance, 0, homeTeam.capacity);
    }

    // ── FINANZAS DEL PARTIDO ──────────────────────────────

    public static void ProcessGameFinances(GameData game, ManagerData manager,
        TeamData myTeam, SeasonData season)
    {
        if (game == null || manager == null || season == null) return;

        var db = DatabaseManager.Instance;
        var homeTeam = db.GetTeamById(game.home_team_id);
        if (homeTeam == null) return;

        var finSettings = db.GetTeamSettings(homeTeam.id);
        const int defaultTicketPrice = 50;
        int ticketPrice = finSettings != null ? (int)finSettings.ticket_price : defaultTicketPrice;

        int attendance = CalculateAttendance(game, homeTeam, manager, season);
        float ticketMultiplier = homeTeam.id == myTeam?.id
            ? GetArenaTicketMultiplier(myTeam)
            : 1.0f;
        long ticketRevenue = TicketRevenue(attendance, ticketPrice, ticketMultiplier);

        // La asistencia se guarda siempre, aunque falten ajustes financieros.
        db.SaveGameAttendance(new GameAttendanceData
        {
            game_id = game.id,
            attendance = attendance,
            ticket_price = ticketPrice,
            revenue = ticketRevenue
        });

        if (ticketRevenue > 0)
            ApplyIncome(homeTeam, myTeam, season, game.game_day,
                FinanceRecord.TYPE_TICKET, ticketRevenue);

        // Sin ajustes configurados no hay patrocinio/TV que procesar.
        if (finSettings == null) return;

        if (finSettings.sponsor_id > 0 && finSettings.sponsor_years_remaining > 0)
        {
            var sponsor = db.GetSponsorById(finSettings.sponsor_id);
            if (sponsor != null)
            {
                long sponsorIncome = ContractMarket.SponsorHomeGameIncome(
                    sponsor.home_game_income, homeTeam.reputation, homeTeam.market_size);
                ApplyIncome(homeTeam, myTeam, season, game.game_day,
                    FinanceRecord.TYPE_SPONSORSHIP, sponsorIncome);
            }
        }

        if (finSettings.tv_channel_id > 0 && finSettings.tv_years_remaining > 0)
        {
            var tv = db.GetTVChannelById(finSettings.tv_channel_id);
            if (tv != null)
            {
                long tvIncome = ContractMarket.SponsorHomeGameIncome(
                    tv.home_game_income, homeTeam.reputation, homeTeam.market_size);
                ApplyIncome(homeTeam, myTeam, season, game.game_day,
                    FinanceRecord.TYPE_TV, tvIncome);
            }
        }
    }

    // ── NÓMINA MENSUAL ────────────────────────────────────

    public static void ProcessMonthlyPayroll(int gameDay, ManagerData manager,
        TeamData myTeam, SeasonData season)
    {
        if (myTeam == null || season == null || manager == null) return;
        if (!IsPayrollDay(gameDay)) return;

        foreach (var team in DatabaseManager.Instance.GetAllTeams())
        {
            ProcessTeamPlayerPayroll(team, gameDay, manager, myTeam, season);
            ProcessTeamLuxuryTax(team, gameDay, myTeam, season);
            ProcessTeamLeagueDistribution(team, gameDay, myTeam, season);
        }

        ProcessEmployeePayroll(gameDay, myTeam, season);
    }

    static void ProcessTeamPlayerPayroll(TeamData team, int gameDay, ManagerData manager,
        TeamData myTeam, SeasonData season)
    {
        var db = DatabaseManager.Instance;
        var players = db.GetPlayersByTeam(team.id);

        // Solo se paga una vez por ciclo.
        if (db.GetFinanceRecord(team.id, season.id, FinanceRecord.TYPE_SALARIES, gameDay) != null)
            return;

        long monthlyPayroll = MonthlyShare(players.Sum(p => p.salary));
        ApplyExpense(team, myTeam, season, gameDay, FinanceRecord.TYPE_SALARIES, monthlyPayroll);

        if (team.id != myTeam.id) return;

        try
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var gameDate = DateTime.Parse(season.year_start + "-10-22").AddDays(gameDay - 1);
            var msg = new MessageData
            {
                manager_id = manager.id,
                sender_type = 1,
                sender_id = 0,
                title = LocalizationService.T("ui.code.team_economy_service.pago_de_nominas"),
                body = NewsTexts.Nomina(myTeam.name, monthlyPayroll, 0),
                game_day = gameDay,
                game_date = gameDate.ToString("yyyy-MM-dd"),
                created_at = now,
                date_sent = now,
                is_read = 0,
                category = NewsCategory.CLUB,
                news_type = NewsType.NOMINA
            };
            db.AddMessage(msg);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Payroll] Error creating message: {ex.Message}\n{ex.StackTrace}");
        }
    }

    static void ProcessTeamLeagueDistribution(TeamData team, int gameDay,
        TeamData myTeam, SeasonData season)
    {
        var db = DatabaseManager.Instance;
        if (db.GetFinanceRecord(team.id, season.id, FinanceRecord.TYPE_LEAGUE, gameDay) != null)
            return;

        ApplyIncome(team, myTeam, season, gameDay,
            FinanceRecord.TYPE_LEAGUE, LeagueDistributionShare);
    }

    static void ProcessEmployeePayroll(int gameDay, TeamData myTeam, SeasonData season)
    {
        var db = DatabaseManager.Instance;
        if (db.GetFinanceRecord(myTeam.id, season.id,
                FinanceRecord.TYPE_EMPLOYEE_SALARY, gameDay) != null)
            return;

        var employees = db.GetEmployeesByTeam(myTeam.id);
        long monthlyEmployeePayroll = MonthlyShare(employees.Sum(e => e.salary));
        if (monthlyEmployeePayroll <= 0) return;

        ApplyExpense(myTeam, myTeam, season, gameDay,
            FinanceRecord.TYPE_EMPLOYEE_SALARY, monthlyEmployeePayroll);
    }

    static void ProcessTeamLuxuryTax(TeamData team, int gameDay, TeamData myTeam, SeasonData season)
    {
        var db = DatabaseManager.Instance;
        if (db.GetFinanceRecord(team.id, season.id, FinanceRecord.TYPE_TAX, gameDay) != null)
            return;

        var leagueSettings = db.GetLeagueSettings();
        long taxThreshold = leagueSettings?.luxury_tax ?? TradeHelper.LUXURY_TAX;
        var players = db.GetPlayersByTeam(team.id);
        long annualTax = TradeHelper.CalculateLuxuryTax(
            players.Sum(p => p.salary), taxThreshold);
        if (annualTax <= 0) return;

        ApplyExpense(team, myTeam, season, gameDay,
            FinanceRecord.TYPE_TAX, MonthlyShare(annualTax), stampCreatedAt: true);
    }

    // ── ABONOS ────────────────────────────────────────────

    public static void ProcessSubscriptionRevenue(int gameDay, ManagerData manager,
        TeamData myTeam, SeasonData season)
    {
        if (manager == null || myTeam == null || season == null) return;
        if (!IsSubscriptionWindow(gameDay)) return;

        var db = DatabaseManager.Instance;
        if (db.GetFinanceRecord(myTeam.id, season.id,
                FinanceRecord.TYPE_SUBSCRIPTION, 0) != null)
            return;

        var finSettings = db.GetTeamSettings(myTeam.id);
        if (finSettings == null) return;

        var earlyGames = db.GetStandingsGames(manager.id)
            .Where(g => (g.home_team_id == myTeam.id || g.away_team_id == myTeam.id)
                     && g.game_day <= 4)
            .ToList();
        int wins = earlyGames.Count(g =>
            (g.home_team_id == myTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == myTeam.id && g.away_score > g.home_score));

        float randomFactor = 0.85f + UnityEngine.Random.value * 0.30f;
        int subscribers = SubscriptionDemand(
            myTeam.capacity,
            finSettings.subscription_price,
            wins,
            myTeam.reputation,
            myTeam.market_size,
            randomFactor);

        long amount = (long)(subscribers * finSettings.subscription_price);
        if (amount <= 0) return;

        ApplyIncome(myTeam, myTeam, season, gameDay,
            FinanceRecord.TYPE_SUBSCRIPTION, amount);

        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        db.AddMessage(new MessageData
        {
            manager_id = manager.id,
            sender_type = 1,
            sender_id = 0,
            title = LocalizationService.T("ui.code.team_economy_service.campana_de_abonos_cerrada"),
            body = NewsTexts.Abonos(myTeam.name, subscribers, amount),
            game_day = gameDay,
            game_date = DateTime.Parse(season.year_start + "-10-22")
                .AddDays(gameDay - 1).ToString("yyyy-MM-dd"),
            created_at = now,
            date_sent = now,
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = NewsType.ABONOS
        });
    }

    // ── PERSISTENCIA AUXILIAR ─────────────────────────────

    static void ApplyIncome(TeamData team, TeamData myTeam, SeasonData season,
        int gameDay, int recordType, long amount)
    {
        team.budget += amount;
        DatabaseManager.Instance.UpdateTeamBudget(team.id, team.budget);
        SyncBudget(team, myTeam);
        DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
        {
            team_id = team.id,
            season_id = season.id,
            record_type = recordType,
            game_day = gameDay,
            amount = amount
        });
    }

    static void ApplyExpense(TeamData team, TeamData myTeam, SeasonData season,
        int gameDay, int recordType, long amount, bool stampCreatedAt = false)
    {
        team.budget -= amount;
        DatabaseManager.Instance.UpdateTeamBudget(team.id, team.budget);
        SyncBudget(team, myTeam);
        var record = new FinanceRecord
        {
            team_id = team.id,
            season_id = season.id,
            record_type = recordType,
            game_day = gameDay,
            amount = amount
        };
        if (stampCreatedAt) record.created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        DatabaseManager.Instance.AddFinanceRecord(record);
    }

    static void SyncBudget(TeamData team, TeamData myTeam)
    {
        if (team != null && myTeam != null && team.id == myTeam.id)
            myTeam.budget = team.budget;
    }

    static float GetArenaTicketMultiplier(TeamData myTeam)
    {
        if (myTeam == null) return 1.0f;
        var staff = DatabaseManager.Instance.GetEmployeesByTeam(myTeam.id);
        var pabellon = staff.FirstOrDefault(e => e.position == "PABELLON");
        return ArenaTicketMultiplier(pabellon?.reputation ?? 0);
    }

    // ── ASISTENCIA: HELPERS ───────────────────────────────

    static (int wins, int played) TeamRecord(List<GameData> allGames, int teamId)
    {
        int wins = 0, played = 0;
        foreach (var g in allGames)
        {
            if (g.home_team_id == teamId)
            {
                played++;
                if (g.home_score > g.away_score) wins++;
            }
            else if (g.away_team_id == teamId)
            {
                played++;
                if (g.away_score > g.home_score) wins++;
            }
        }
        return (wins, played);
    }

    static float GetAttendanceStreakFactor(List<GameData> allGames, int teamId)
    {
        var last10 = allGames
            .Where(g => g.home_team_id == teamId || g.away_team_id == teamId)
            .OrderByDescending(g => g.game_day).ThenByDescending(g => g.id)
            .Take(10).ToList();
        if (last10.Count == 0) return 1.0f;
        int wins = last10.Count(g =>
            (g.home_team_id == teamId && g.home_score > g.away_score) ||
            (g.away_team_id == teamId && g.away_score > g.home_score));
        float recent = (float)wins / last10.Count;
        return Mathf.Clamp(0.85f + recent * 0.25f, 0.85f, 1.10f);
    }

    static float GetObjectiveComplianceFactor(TeamData homeTeam, List<GameData> teamGames,
        DatabaseManager db)
    {
        if (string.IsNullOrEmpty(homeTeam.objective)) return 1.0f;
        if (homeTeam.objective == "Zona tranquila") return 1.0f;

        int targetPos = ObjectiveTargetPosition(homeTeam.objective);
        if (targetPos <= 0) return 1.0f;

        var conferenceTeams = db.GetAllTeams()
            .Where(t => t.conference == homeTeam.conference)
            .ToList();

        var ranked = conferenceTeams.Select(t =>
        {
            var tGames = teamGames.Where(g => g.home_team_id == t.id || g.away_team_id == t.id).ToList();
            int w = tGames.Count(g =>
                (g.home_team_id == t.id && g.home_score > g.away_score) ||
                (g.away_team_id == t.id && g.away_score > g.home_score));
            int l = tGames.Count - w;
            return new { Team = t, Wins = w, Losses = l, Pct = tGames.Count > 0 ? (float)w / tGames.Count : 0f };
        })
        .OrderByDescending(x => x.Pct)
        .ThenBy(x => x.Losses)
        .ThenByDescending(x => x.Wins)
        .ToList();

        int currentPos = ranked.FindIndex(x => x.Team.id == homeTeam.id) + 1;
        if (currentPos <= 0) return 1.0f;

        int gap = currentPos - targetPos;
        if (gap <= 0) return 1.0f;

        // 1 posición por debajo → 0,90; 10+ → 0,30.
        return Mathf.Clamp(1.0f - gap * 0.06f, 0.30f, 1.0f);
    }
}
