using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class DashboardController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerSeason;
    private Label _headerDate;
    private Button _btnAction;

    // Último partido
    private Label _noLastGame;
    private VisualElement _lastGameResult;
    private VisualElement _lastHomeLog;
    private VisualElement _lastAwayLog;
    private Label _lastHomeName;
    private Label _lastAwayName;
    private Label _lastHomeScore;
    private Label _lastAwayScore;
    private Label _lastResultBadge;
    private Label _lastGameDate;
    // Meta partidos
    private Label _lastGameLocation;
    private Label _lastGameArena;
    private Label _lastGameType;


    // Próximo partido
    private Label _noNextGame;
    private VisualElement _nextGameResult;
    private VisualElement _nextHomeLog;
    private VisualElement _nextAwayLog;
    private Label _nextHomeName;
    private Label _nextAwayName;
    private Label _nextGameDate;
    private Label _nextGameLocation;
    private Label _nextGameArena;
    private Label _nextGameType;

    // Clasificación
    private Button _tabEast;
    private Button _tabWest;
    private VisualElement _standingsBody;
    private string _currentConf = "West";

    // Relaciones
    private VisualElement _barTrust;
    private VisualElement _barMorale;
    private VisualElement _barPressure;
    private Label _valTrust;
    private Label _valMorale;
    private Label _valPressure;

    // Datos
    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;
    private List<GameData> _allGames;
    private List<PlayerData> _players = new();

    // Sprites
    private Dictionary<string, Sprite> _logoSprites = new();

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        // Forzar root a ocupar toda la pantalla
        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        CacheReferences();
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        // Header
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _btnAction = _root.Q<Button>("BtnAction");

        // Último partido
        _noLastGame = _root.Q<Label>("NoLastGame");
        _lastGameResult = _root.Q<VisualElement>("LastGameResult");
        _lastHomeLog = _root.Q<VisualElement>("LastHomeLog");
        _lastAwayLog = _root.Q<VisualElement>("LastAwayLog");
        _lastHomeName = _root.Q<Label>("LastHomeName");
        _lastAwayName = _root.Q<Label>("LastAwayName");
        _lastHomeScore = _root.Q<Label>("LastHomeScore");
        _lastAwayScore = _root.Q<Label>("LastAwayScore");
        _lastResultBadge = _root.Q<Label>("LastResultBadge");
        _lastGameDate = _root.Q<Label>("LastGameDate");
        _lastGameLocation = _root.Q<Label>("LastGameLocation");
        _lastGameArena = _root.Q<Label>("LastGameArena");
        _lastGameType = _root.Q<Label>("LastGameType");

        // Próximo partido
        _noNextGame = _root.Q<Label>("NoNextGame");
        _nextGameResult = _root.Q<VisualElement>("NextGameResult");
        _nextHomeLog = _root.Q<VisualElement>("NextHomeLog");
        _nextAwayLog = _root.Q<VisualElement>("NextAwayLog");
        _nextHomeName = _root.Q<Label>("NextHomeName");
        _nextAwayName = _root.Q<Label>("NextAwayName");
        _nextGameDate = _root.Q<Label>("NextGameDate");
        _nextGameLocation = _root.Q<Label>("NextGameLocation");
        _nextGameArena = _root.Q<Label>("NextGameArena");
        _nextGameType = _root.Q<Label>("NextGameType");

        // Clasificación
        _tabEast = _root.Q<Button>("TabEast");
        _tabWest = _root.Q<Button>("TabWest");
        _standingsBody = _root.Q<VisualElement>("StandingsBody");

        // Relaciones
        _barTrust = _root.Q<VisualElement>("BarTrust");
        _barMorale = _root.Q<VisualElement>("BarMorale");
        _barPressure = _root.Q<VisualElement>("BarPressure");
        _valTrust = _root.Q<Label>("ValTrust");
        _valMorale = _root.Q<Label>("ValMorale");
        _valPressure = _root.Q<Label>("ValPressure");
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);

        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
    }

    void RegisterCallbacks()
    {
        _btnAction?.RegisterCallback<ClickEvent>(_ => OnActionClicked());

        _tabEast?.RegisterCallback<ClickEvent>(_ => ShowStandings("East"));
        _tabWest?.RegisterCallback<ClickEvent>(_ => ShowStandings("West"));

        // Sidebar navigation
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Roster));
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Calendar));
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Standings));
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Palmares));
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Results));
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Playoffs));
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Stats));
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Records));
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Market));
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Finances));
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Sponsors));
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.TV));
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Arena));
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Messages));

        _root.Q<Button>("BtnReset")?.RegisterCallback<ClickEvent>(_ => OnReset());

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnAction);
            CursorManager.Instance.RegisterHandCursor(_tabEast);
            CursorManager.Instance.RegisterHandCursor(_tabWest);
        }
    }

    // ── REFRESH COMPLETO ─────────────────────────────────

    void Refresh()
    {
        RefreshHeader();
        RefreshLastGame();
        RefreshNextGame();
        RefreshActionButton();
        ShowStandings(_currentConf);
        RefreshBoard();
        RefreshPlayerStats();
    }

    // ── HEADER ───────────────────────────────────────────

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerBudget.text = $"${_myTeam.budget / 1_000_000}M";

        // Masa salarial real
        long totalPayroll = _players.Sum(p => p.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        // Margen salarial real = Cap - Masa salarial
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? 155_000_000;
        long margin = salaryCap - totalPayroll;

        _headerMargin.text = margin >= 0
            ? $"+${margin / 1_000_000}M"
            : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";

        _headerMargin.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0)
            _headerMargin.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = GetCurrentDateString();
        }
    }

    string GetCurrentDateString()
    {
        if (_season == null) return "";
        try
        {
            if (_season.current_game_day <= 0)
            {
                var nextGame = DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id);
                if (nextGame != null)
                    return System.DateTime.Parse(nextGame.game_date).ToString("dd/MM/yyyy");
                return new System.DateTime(_season.year_end, 4, 15).ToString("dd/MM/yyyy");
            }

            var seasonStart = new System.DateTime(_season.year_start, 10, 22);
            return seasonStart.AddDays(_season.current_game_day - 1).ToString("dd/MM/yyyy");
        }
        catch { return ""; }
    }

    // ── BOTÓN ACCIÓN ─────────────────────────────────────

    void RefreshActionButton()
    {
        if (_season == null || _myTeam == null)
        {
            SetActionBtn("CONTINUAR →", "");
            return;
        }

        int nextDay = FindNextGameDay();
        if (nextDay == 0)
        {
            SetActionBtn("CONTINUAR →", "");
            return;
        }

        var gamesOnNextDay = DatabaseManager.Instance.GetGamesByGameDay(_manager.id, nextDay);

        bool myTeamPlays = gamesOnNextDay.Any(g =>
            g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id);

        if (myTeamPlays)
        {
            SetActionBtn("DÍA DE PARTIDO →", "btn-action--match");
            return;
        }

        if (gamesOnNextDay.Count > 0)
        {
            SetActionBtn("SIMULAR PARTIDOS →", "btn-action--simulate");
            return;
        }

        SetActionBtn("CONTINUAR →", "");
    }

    void SetActionBtn(string text, string extraClass)
    {
        _btnAction.text = text;
        _btnAction.RemoveFromClassList("btn-action--match");
        _btnAction.RemoveFromClassList("btn-action--simulate");
        if (!string.IsNullOrEmpty(extraClass))
            _btnAction.AddToClassList(extraClass);
    }

    void OnActionClicked()
    {
        if (_season == null) return;

        // Find the next game day with unplayed games
        int gameDay = FindNextGameDay();
        if (gameDay == 0)
        {
            Debug.Log("[Dashboard] No hay más partidos programados.");
            return;
        }

        // Process injuries (decrease injury days)
        ProcessInjuries();

        // Process renovations
        ProcessRenovations();

        var gamesToday = DatabaseManager.Instance.GetGamesByGameDay(_manager.id, gameDay);

        bool myTeamPlays = gamesToday.Any(g =>
            g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id);

        if (gamesToday.Count > 0)
        {
            GameResultCache.Clear();
            GameResultCache.LastGameDay = gameDay;

            foreach (var game in gamesToday)
            {
                var homePlayers = DatabaseManager.Instance.GetPlayersByTeam(game.home_team_id);
                var awayPlayers = DatabaseManager.Instance.GetPlayersByTeam(game.away_team_id);
                var result = GameSimulator.SimulateGame(game, homePlayers, awayPlayers);
                DatabaseManager.Instance.UpdateGame(game);
                GameResultCache.SimulatedGameIds.Add(game.id);

                // Process finances for this game
                ProcessGameFinances(game, result);

                // Process injuries from this game
                ProcessGameInjuries(result, game.game_date);
            }

            // Update manager stats
            UpdateManagerStats(gameDay);
        }

        // Process monthly payroll (1st of each month ~ game day 1, 31, 61, 91, 121, 151, 181)
        ProcessMonthlyPayroll(gameDay);

        // Process subscription revenue (around game day 11 = Nov 1)
        ProcessSubscriptionRevenue(gameDay);

        // Update historical player stats at end of season
        if (_season.phase == "finished")
        {
            DatabaseManager.Instance.UpdateHistoricalPlayerStatsFromSeason(_season.id, _manager.id);
        }

        // Update current_game_day to the day we just played
        _season.current_game_day = gameDay;
        DatabaseManager.Instance.UpdateSeason(_season);

        _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        Refresh();

        // Actualizar metadatos de guardado
        GameSaveManager.UpdateSlotFromDatabase(DatabaseManager.Instance.ActiveSaveSlot);

        if (myTeamPlays)
        {
            Debug.Log($"[Dashboard] Día {gameDay} — mi equipo juega → MatchDay");
            ScreenManager.Instance.GoTo(GameScreen.MatchDay);
        }
        else
        {
            Debug.Log($"[Dashboard] Día {gameDay} — {gamesToday.Count} partidos → GameResults");
            ScreenManager.Instance.GoTo(GameScreen.GameResults);
        }
    }

    int FindNextGameDay()
    {
        // Search from current day onwards for the next day with unplayed games
        var allUnplayed = DatabaseManager.Instance.GetAllGames(_manager.id)
            .Where(g => g.is_played == 0)
            .ToList();

        if (allUnplayed.Count == 0) return 0;

        // Sort: negatives first (descending: -1, -2, -3), then positives (ascending: 1, 2, 3)
        var preseason = allUnplayed.Where(g => g.game_day < 0).OrderByDescending(g => g.game_day).ToList();
        var regular   = allUnplayed.Where(g => g.game_day > 0).OrderBy(g => g.game_day).ToList();
        var sorted = preseason.Concat(regular).ToList();

        // If current_game_day is 0 or we haven't played any game yet, return the closest to 0
        if (_season.current_game_day == 0)
            return sorted.First().game_day;

        // Find the current game's position and return the next one
        var current = sorted.FindIndex(g => g.game_day == _season.current_game_day);
        if (current >= 0 && current < sorted.Count - 1)
            return sorted[current + 1].game_day;

        // If current game not found or is the last, return the closest unplayed to 0
        return sorted.First().game_day;
    }

    void ProcessInjuries()
    {
        var allPlayers = DatabaseManager.Instance.GetAllTeams().SelectMany(t =>
            DatabaseManager.Instance.GetPlayersByTeam(t.id)).ToList();
        foreach (var p in allPlayers)
        {
            if (p.injury_days > 0)
            {
                p.injury_days--;
                if (p.injury_days <= 0)
                {
                    p.injury_days = 0;
                    p.injury_type = "";
                }
                DatabaseManager.Instance.UpdatePlayer(p);
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

    void ApplyRenovation(TeamData team)
    {
        var info = GetRenovationInfo(team.arena_renovation_type);
        if (info.name == "") return;

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
            title = $"Remodelación completada: {info.name}",
            body = $"La remodelación \"{info.name}\" ha finalizado. Se han añadido {info.capacityBonus} asientos. Coste total: ${team.arena_renovation_cost:N0}.",
            game_day = _season.current_game_day,
            game_date = System.DateTime.Parse(_season.year_start + "-10-22").AddDays(_season.current_game_day - 1).ToString("yyyy-MM-dd"),
            is_read = 0
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

    void ProcessGameFinances(GameData game, GameSimulator.GameResult result)
    {
        // Only process for home team
        var homeTeam = DatabaseManager.Instance.GetTeamById(game.home_team_id);
        if (homeTeam == null) return;

        var finSettings = DatabaseManager.Instance.GetTeamSettings(homeTeam.id);
        if (finSettings == null) return;

        // Calculate attendance
        var teamGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        var homeTeamGames = teamGames.Where(g => g.home_team_id == homeTeam.id || g.away_team_id == homeTeam.id).ToList();
        int wins = homeTeamGames.Count(g =>
            (g.home_team_id == homeTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == homeTeam.id && g.away_score > g.home_score));
        int totalPlayed = homeTeamGames.Count;
        float winPct = totalPlayed > 0 ? (float)wins / totalPlayed : 0.5f;

        float baseAttendance = homeTeam.capacity * (0.55f + winPct * 0.40f);
        float randomFactor = 0.92f + UnityEngine.Random.value * 0.16f;
        int attendance = (int)Mathf.Min(homeTeam.capacity, baseAttendance * randomFactor);

        long ticketRevenue = (long)(attendance * finSettings.ticket_price);
        homeTeam.budget += ticketRevenue;
        DatabaseManager.Instance.UpdateTeamBudget(homeTeam.id, homeTeam.budget);

        DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
        {
            team_id = homeTeam.id,
            season_id = _season.id,
            record_type = FinanceRecord.TYPE_TICKET,
            game_day = game.game_day,
            amount = ticketRevenue
        });

        // Save attendance
        DatabaseManager.Instance.SaveGameAttendance(new GameAttendanceData
        {
            game_id = game.id,
            attendance = attendance,
            ticket_price = (int)finSettings.ticket_price,
            revenue = ticketRevenue
        });

        // Sponsor home game income
        if (finSettings.sponsor_id > 0 && finSettings.sponsor_years_remaining > 0)
        {
            var sponsor = DatabaseManager.Instance.GetSponsorById(finSettings.sponsor_id);
            if (sponsor != null)
            {
                homeTeam.budget += sponsor.home_game_income;
                DatabaseManager.Instance.UpdateTeamBudget(homeTeam.id, homeTeam.budget);
                DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
                {
                    team_id = homeTeam.id,
                    season_id = _season.id,
                    record_type = FinanceRecord.TYPE_SPONSORSHIP,
                    game_day = game.game_day,
                    amount = sponsor.home_game_income
                });
            }
        }

        // TV home game income
        if (finSettings.tv_channel_id > 0 && finSettings.tv_years_remaining > 0)
        {
            var tv = DatabaseManager.Instance.GetTVChannelById(finSettings.tv_channel_id);
            if (tv != null)
            {
                homeTeam.budget += tv.home_game_income;
                DatabaseManager.Instance.UpdateTeamBudget(homeTeam.id, homeTeam.budget);
                DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
                {
                    team_id = homeTeam.id,
                    season_id = _season.id,
                    record_type = FinanceRecord.TYPE_TV,
                    game_day = game.game_day,
                    amount = tv.home_game_income
                });
            }
        }
    }

    void ProcessGameInjuries(GameSimulator.GameResult result, string gameDate)
    {
        if (result.injuries == null) return;
        foreach (var inj in result.injuries)
        {
            if (inj.player_id == _myTeam.id || true) // Notify for all injuries affecting my team
            {
                var player = DatabaseManager.Instance.GetPlayerById(inj.player_id);
                if (player != null && player.team_id == _myTeam.id)
                {
                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        title = $"Lesión: {player.first_name} {player.last_name}",
                        body = $"{player.first_name} {player.last_name} ha sufrido {inj.type}. Estará de baja {inj.days} días.",
                        game_day = _season.current_game_day,
                        game_date = gameDate,
                        is_read = 0
                    });
                }
            }
        }
    }

    void UpdateManagerStats(int gameDay)
    {
        var teamGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        var myGames = teamGames.Where(g => g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id).ToList();

        int wins = myGames.Count(g =>
            (g.home_team_id == _myTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == _myTeam.id && g.away_score > g.home_score));
        int losses = myGames.Count - wins;

        // Trust: based on win percentage
        float winPct = myGames.Count > 0 ? (float)wins / myGames.Count : 0.5f;
        int trustChange = winPct > 0.6f ? 2 : winPct > 0.5f ? 1 : winPct < 0.4f ? -3 : -1;
        _manager.trust = Mathf.Clamp(_manager.trust + trustChange, 0, 100);

        // Morale: based on recent form (last 5 games)
        var last5 = myGames.OrderByDescending(g => g.game_day).Take(5).ToList();
        int recentWins = last5.Count(g =>
            (g.home_team_id == _myTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == _myTeam.id && g.away_score > g.home_score));
        int moraleChange = recentWins >= 4 ? 3 : recentWins >= 3 ? 1 : recentWins <= 1 ? -2 : 0;
        _manager.morale = Mathf.Clamp(_manager.morale + moraleChange, 0, 100);

        // Pressure: inverse of trust
        int pressureChange = trustChange < 0 ? 2 : trustChange > 0 ? -1 : 0;
        _manager.pressure = Mathf.Clamp(_manager.pressure + pressureChange, 0, 100);

        DatabaseManager.Instance.SaveManager(_manager);
    }

    void ProcessMonthlyPayroll(int gameDay)
    {
        // Payroll on game days ~1, 31, 61, 91, 121, 151, 181 (1st of each month)
        int[] payrollDays = { 1, 31, 61, 91, 121, 151, 181 };
        if (!payrollDays.Contains(gameDay)) return;

        // Check if already paid this cycle
        var existingPayroll = DatabaseManager.Instance.GetFinanceRecord(_myTeam.id, _season.id, FinanceRecord.TYPE_SALARIES, gameDay);
        if (existingPayroll != null) return;

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long monthlyPayroll = players.Sum(p => p.salary) / 12; // Monthly = annual / 12

        _myTeam.budget -= monthlyPayroll;
        DatabaseManager.Instance.UpdateTeamBudget(_myTeam.id, _myTeam.budget);

        DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
        {
            team_id = _myTeam.id,
            season_id = _season.id,
            record_type = FinanceRecord.TYPE_SALARIES,
            game_day = gameDay,
            amount = monthlyPayroll
        });

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            title = "Pago de nóminas",
            body = $"Se han pagado las nóminas del mes por un total de ${monthlyPayroll:N0}.",
            game_day = gameDay,
            game_date = System.DateTime.Parse(_season.year_start + "-10-22").AddDays(gameDay - 1).ToString("yyyy-MM-dd"),
            is_read = 0
        });
    }

    void ProcessSubscriptionRevenue(int gameDay)
    {
        // Process around game day 11 (Nov 1)
        if (gameDay < 10 || gameDay > 12) return;

        var existing = DatabaseManager.Instance.GetFinanceRecord(_myTeam.id, _season.id, FinanceRecord.TYPE_SUBSCRIPTION, 0);
        if (existing != null) return;

        var finSettings = DatabaseManager.Instance.GetTeamSettings(_myTeam.id);
        if (finSettings == null) return;

        // Early season performance
        var earlyGames = DatabaseManager.Instance.GetStandingsGames(_manager.id)
            .Where(g => (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id) && g.game_day <= 4)
            .ToList();
        int wins = earlyGames.Count(g =>
            (g.home_team_id == _myTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == _myTeam.id && g.away_score > g.home_score));

        float performanceMult = 1.0f + (wins * 0.05f);
        float randomFactor = 0.85f + UnityEngine.Random.value * 0.30f;

        float baseRatio = 0.5f;
        float priceFactor = (2000 - finSettings.subscription_price) / 10000f;
        int numSubscribers = (int)(_myTeam.capacity * (baseRatio + priceFactor) * performanceMult * randomFactor);
        numSubscribers = (int)Mathf.Clamp(numSubscribers, 0, _myTeam.capacity);

        long subAmount = numSubscribers * finSettings.subscription_price;

        if (subAmount > 0)
        {
            _myTeam.budget += subAmount;
            DatabaseManager.Instance.UpdateTeamBudget(_myTeam.id, _myTeam.budget);

            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = _myTeam.id,
                season_id = _season.id,
                record_type = FinanceRecord.TYPE_SUBSCRIPTION,
                game_day = gameDay,
                amount = subAmount
            });

            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                title = "Ingresos por abonos",
                body = $"Se han vendido {numSubscribers:N0} abonos esta temporada obteniendo un ingreso total de ${subAmount:N0}.",
                game_day = gameDay,
                game_date = System.DateTime.Parse(_season.year_start + "-10-22").AddDays(gameDay - 1).ToString("yyyy-MM-dd"),
                is_read = 0
            });
        }
    }

    // ── ÚLTIMO PARTIDO ───────────────────────────────────

    void RefreshLastGame()
    {
        var last = DatabaseManager.Instance.GetLastPlayedGame(_manager.id, _myTeam.id);

        if (last == null)
        {
            _noLastGame.style.display = DisplayStyle.Flex;
            _lastGameResult.style.display = DisplayStyle.None;
            _lastGameDate.text = "";
            return;
        }

        _noLastGame.style.display = DisplayStyle.None;
        _lastGameResult.style.display = DisplayStyle.Flex;

        var home = _allTeams.Find(t => t.id == last.home_team_id);
        var away = _allTeams.Find(t => t.id == last.away_team_id);

        SetTeamLogo(_lastHomeLog, home?.logo);
        SetTeamLogo(_lastAwayLog, away?.logo);
        _lastHomeName.text = home?.name.ToUpper() ?? "";
        _lastAwayName.text = away?.name.ToUpper() ?? "";
        _lastHomeScore.text = last.home_score.ToString();
        _lastAwayScore.text = last.away_score.ToString();
        try
        {
            _lastGameDate.text = System.DateTime.Parse(last.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            _lastGameDate.text = last.game_date;
        }

        // Badge victoria/derrota
        bool myTeamIsHome = last.home_team_id == _myTeam.id;
        int myScore = myTeamIsHome ? last.home_score : last.away_score;
        int oppScore = myTeamIsHome ? last.away_score : last.home_score;
        bool won = myScore > oppScore;

        _lastResultBadge.text = won ? "VICTORIA" : "DERROTA";
        _lastResultBadge.RemoveFromClassList("badge-win");
        _lastResultBadge.RemoveFromClassList("badge-loss");
        _lastResultBadge.AddToClassList(won ? "badge-win" : "badge-loss");

        // Meta
        bool lastIsHome = last.home_team_id == _myTeam.id;
        var lastHomeTeam = _allTeams.Find(t => t.id == last.home_team_id);

        _lastGameLocation.text = lastIsHome ? "🏠  LOCAL" : "✈  VISITANTE";
        _lastGameLocation.RemoveFromClassList("game-meta-location--home");
        _lastGameLocation.RemoveFromClassList("game-meta-location--away");
        _lastGameLocation.AddToClassList(lastIsHome ? "game-meta-location--home" : "game-meta-location--away");

        _lastGameArena.text = lastHomeTeam != null
            ? DatabaseManager.Instance.GetTeamById(lastHomeTeam.id)?.arena ?? ""
            : "";

        _lastGameType.text = last.game_type switch
        {
            "preseason" => "AMISTOSO",
            "regular" => "TEMPORADA REGULAR",
            "playin" => "PLAY-IN",
            "playoff" => "PLAYOFFS",
            _ => last.game_type.ToUpper()
        };
    }

    // ── PRÓXIMO PARTIDO ──────────────────────────────────

    void RefreshNextGame()
    {
        var next = DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id);

        if (next == null)
        {
            _noNextGame.style.display = DisplayStyle.Flex;
            _nextGameResult.style.display = DisplayStyle.None;
            _nextGameDate.text = "";
            return;
        }

        _noNextGame.style.display = DisplayStyle.None;
        _nextGameResult.style.display = DisplayStyle.Flex;

        var home = _allTeams.Find(t => t.id == next.home_team_id);
        var away = _allTeams.Find(t => t.id == next.away_team_id);

        SetTeamLogo(_nextHomeLog, home?.logo);
        SetTeamLogo(_nextAwayLog, away?.logo);
        _nextHomeName.text = home?.name.ToUpper() ?? "";
        _nextAwayName.text = away?.name.ToUpper() ?? "";
        try
        {
            _nextGameDate.text = System.DateTime.Parse(next.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            _nextGameDate.text = next.game_date;
        }

        // Meta
        bool nextIsHome = next.home_team_id == _myTeam.id;
        var nextHomeTeam = _allTeams.Find(t => t.id == next.home_team_id);

        _nextGameLocation.text = nextIsHome ? "🏠  LOCAL" : "✈  VISITANTE";
        _nextGameLocation.RemoveFromClassList("game-meta-location--home");
        _nextGameLocation.RemoveFromClassList("game-meta-location--away");
        _nextGameLocation.AddToClassList(nextIsHome ? "game-meta-location--home" : "game-meta-location--away");

        _nextGameArena.text = nextHomeTeam != null
            ? DatabaseManager.Instance.GetTeamById(nextHomeTeam.id)?.arena ?? ""
            : "";

        _nextGameType.text = next.game_type switch
        {
            "preseason" => "AMISTOSO",
            "regular" => "TEMPORADA REGULAR",
            "playin" => "PLAY-IN",
            "playoff" => "PLAYOFFS",
            _ => next.game_type.ToUpper()
        };
    }

    // ── CLASIFICACIÓN ────────────────────────────────────

    void ShowStandings(string conf)
    {
        _currentConf = conf;

        _tabEast.RemoveFromClassList("conf-tab--active");
        _tabWest.RemoveFromClassList("conf-tab--active");
        if (conf == "East") _tabEast.AddToClassList("conf-tab--active");
        else _tabWest.AddToClassList("conf-tab--active");

        var confTeams = _allTeams.Where(t => t.conference == conf).ToList();
        var standings = BuildStandings(confTeams);

        _standingsBody.Clear();

        foreach (var row in standings)
        {
            var team = _allTeams.Find(t => t.id == row.teamId);
            var rowElem = CreateStandingsRow(row, team);
            _standingsBody.Add(rowElem);
        }
    }

    List<StandingRow> BuildStandings(List<TeamData> confTeams)
    {
        var data = confTeams.ToDictionary(t => t.id, t => new StandingRow
        {
            teamId = t.id,
            wins = 0,
            losses = 0,
            games = new List<bool>()
        });

        var confIds = confTeams.Select(t => t.id).ToHashSet();

        foreach (var g in _allGames)
        {
            if (confIds.Contains(g.home_team_id))
            {
                bool homeWon = g.home_score > g.away_score;
                data[g.home_team_id].wins += homeWon ? 1 : 0;
                data[g.home_team_id].losses += homeWon ? 0 : 1;
                data[g.home_team_id].games.Add(homeWon);
            }
            if (confIds.Contains(g.away_team_id))
            {
                bool awayWon = g.away_score > g.home_score;
                data[g.away_team_id].wins += awayWon ? 1 : 0;
                data[g.away_team_id].losses += awayWon ? 0 : 1;
                data[g.away_team_id].games.Add(awayWon);
            }
        }

        var rows = data.Values.ToList();
        rows.Sort((a, b) =>
        {
            float pctA = a.wins + a.losses > 0 ? (float)a.wins / (a.wins + a.losses) : 0;
            float pctB = b.wins + b.losses > 0 ? (float)b.wins / (b.wins + b.losses) : 0;
            if (pctB != pctA) return pctB.CompareTo(pctA);
            if (a.losses != b.losses) return a.losses.CompareTo(b.losses);
            return b.wins.CompareTo(a.wins);
        });

        for (int i = 0; i < rows.Count; i++)
            rows[i].rank = i + 1;

        return rows;
    }

    VisualElement CreateStandingsRow(StandingRow row, TeamData team)
    {
        var elem = new VisualElement();
        elem.AddToClassList("standings-row");

        bool isMyTeam = team != null && team.id == _myTeam.id;
        if (isMyTeam) elem.AddToClassList("standings-row--my-team");
        else if (row.rank <= 6) elem.AddToClassList("standings-row--playoff");
        else if (row.rank <= 10) elem.AddToClassList("standings-row--playin");

        int gp = row.wins + row.losses;
        float pct = gp > 0 ? (float)row.wins / gp : 0f;
        var streak = CalcStreak(row.games);

        // Rank
        var rankLbl = new Label();
        rankLbl.AddToClassList("col-rank");
        rankLbl.text = row.rank.ToString();

        // Logo
        var logoElem = new VisualElement();
        logoElem.AddToClassList("col-team-logo");
        if (team != null) SetTeamLogo(logoElem, team.logo);

        // Nombre
        var nameLbl = new Label();
        nameLbl.AddToClassList("col-team-name");
        nameLbl.text = team?.name.ToUpper() ?? "???";

        // PJ
        var gpLbl = new Label();
        gpLbl.AddToClassList("col-stat");
        gpLbl.text = gp.ToString();

        // Victorias
        var wLbl = new Label();
        wLbl.AddToClassList("col-stat");
        wLbl.AddToClassList("col-wins");
        wLbl.text = row.wins.ToString();

        // Derrotas
        var lLbl = new Label();
        lLbl.AddToClassList("col-stat");
        lLbl.AddToClassList("col-losses");
        lLbl.text = row.losses.ToString();

        // %
        var pctLbl = new Label();
        pctLbl.AddToClassList("col-stat");
        pctLbl.text = pct.ToString("F3");

        // Racha
        var streakLbl = new Label();
        streakLbl.AddToClassList("col-streak");
        streakLbl.text = streak.text;
        streakLbl.AddToClassList(streak.type == "win" ? "streak-win" :
                                  streak.type == "loss" ? "streak-loss" : "streak-none");

        elem.Add(rankLbl);
        elem.Add(logoElem);
        elem.Add(nameLbl);
        elem.Add(gpLbl);
        elem.Add(wLbl);
        elem.Add(lLbl);
        elem.Add(pctLbl);
        elem.Add(streakLbl);

        return elem;
    }

    (string text, string type) CalcStreak(List<bool> games)
    {
        if (games == null || games.Count == 0) return ("-", "none");
        bool last = games[games.Count - 1];
        int count = 0;
        for (int i = games.Count - 1; i >= 0; i--)
        {
            if (games[i] == last) count++;
            else break;
        }
        return last ? ($"{count}V", "win") : ($"{count}D", "loss");
    }

    // ── ESTADISTICAS ───────────────────────────────────────
    void RefreshPlayerStats()
    {
        // Máximo anotador — mayor atributo de tiro
        var scorer = _players.OrderByDescending(p => p.shooting).FirstOrDefault();
        SetStatCard("StatScorer", "StatScorerName", "StatScorerGames",
            scorer?.shooting.ToString() ?? "--",
            scorer != null ? $"{scorer.first_name} {scorer.last_name}" : "",
            "");

        // Máximo rebotador — mayor atributo de rebote
        var rebounder = _players.OrderByDescending(p => p.rebounding).FirstOrDefault();
        SetStatCard("StatRebounder", "StatRebounderName", "StatRebounderGames",
            rebounder?.rebounding.ToString() ?? "--",
            rebounder != null ? $"{rebounder.first_name} {rebounder.last_name}" : "",
            "");

        // Máximo asistente — mayor atributo de pase
        var assister = _players.OrderByDescending(p => p.passing).FirstOrDefault();
        SetStatCard("StatAssister", "StatAssisterName", "StatAssisterGames",
            assister?.passing.ToString() ?? "--",
            assister != null ? $"{assister.first_name} {assister.last_name}" : "",
            "");

        // Mejor valoración — mayor overall
        var rated = _players.OrderByDescending(p => p.overall).FirstOrDefault();
        SetStatCard("StatRated", "StatRatedName", "StatRatedGames",
            rated?.overall.ToString() ?? "--",
            rated != null ? $"{rated.first_name} {rated.last_name}" : "",
            "");
    }

    void SetStatCard(string valName, string playerName, string gamesName,
                     string val, string player, string games)
    {
        var valLbl = _root.Q<Label>(valName);
        var playerLbl = _root.Q<Label>(playerName);
        var gamesLbl = _root.Q<Label>(gamesName);

        if (valLbl != null) valLbl.text = val;
        if (playerLbl != null) playerLbl.text = player;
        if (gamesLbl != null) gamesLbl.text = games;
    }

    // ── RELACIONES ───────────────────────────────────────

    void RefreshBoard()
    {
        if (_manager == null) return;
        SetBar(_barTrust, _valTrust, _manager.trust);
        SetBar(_barMorale, _valMorale, _manager.morale);
        SetBar(_barPressure, _valPressure, _manager.pressure);
    }

    void SetBar(VisualElement bar, Label val, int value)
    {
        if (bar == null || val == null) return;
        float pct = Mathf.Clamp01(value / 100f);
        bar.style.width = new StyleLength(new Length(pct * 100f, LengthUnit.Percent));

        bar.RemoveFromClassList("board-bar-fill--green");
        bar.RemoveFromClassList("board-bar-fill--gold");
        bar.RemoveFromClassList("board-bar-fill--red");
        bar.AddToClassList(value >= 80 ? "board-bar-fill--green" :
                           value >= 50 ? "board-bar-fill--gold" : "board-bar-fill--red");

        val.text = $"{value}%";
    }

    // ── HELPERS ──────────────────────────────────────────

    void SetTeamLogo(VisualElement elem, string logoName)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;
        if (_logoSprites.TryGetValue(logoName, out var sprite))
            elem.style.backgroundImage = new StyleBackground(sprite);
    }

    // ── RESET ────────────────────────────────────────────

    void OnReset()
    {
        Debug.Log("[Dashboard] Reiniciar partida — pendiente de implementar");
        ScreenManager.Instance.GoTo(GameScreen.MainMenu);
    }

    // ── CLASE AUXILIAR ───────────────────────────────────

    class StandingRow
    {
        public int teamId;
        public int rank;
        public int wins;
        public int losses;
        public List<bool> games;
    }
}
