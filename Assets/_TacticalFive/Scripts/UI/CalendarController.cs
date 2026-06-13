using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class CalendarController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private Button _btnPrevMonth;
    private Button _btnNextMonth;
    private Label _currentMonth;
    private VisualElement _calendarDays;
    private Label _selectedDayTitle;
    private VisualElement _selectedDayGames;
    private Label _noGamesText;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;
    private List<GameData> _allGames;

    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSprites80 = new();
    private Dictionary<string, Sprite> _logoSprites32 = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();

    private System.DateTime _currentMonthDate;
    private System.DateTime? _selectedDate;
    private System.DateTime? _currentGameDate;

    private static readonly string[] MonthNames = {
        "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    };

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        CacheReferences();
        LoadSidebarIcons();
        LoadData();
        RegisterCallbacks();

        // Auto-select current game day
        AutoSelectCurrentDay();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _btnPrevMonth = _root.Q<Button>("BtnPrevMonth");
        _btnNextMonth = _root.Q<Button>("BtnNextMonth");
        _currentMonth = _root.Q<Label>("CurrentMonth");
        _calendarDays = _root.Q<VisualElement>("CalendarDays");
        _selectedDayTitle = _root.Q<Label>("SelectedDayTitle");
        _selectedDayGames = _root.Q<VisualElement>("SelectedDayGames");
        _noGamesText = _root.Q<Label>("NoGamesText");
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos80 = Resources.LoadAll<Sprite>("Teams/Logos/80x80");
        foreach (var s in logos80) _logoSprites80[s.name] = s;

        var logos32 = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _allGames = DatabaseManager.Instance.GetAllGames(_manager.id);
    }

    void AutoSelectCurrentDay()
    {
        // Try to find the current game day using next unplayed game
        GameData currentGame = null;
        if (_manager != null && _myTeam != null)
        {
            currentGame = DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id);
        }

        // Fallback: last played game
        if (currentGame == null && _manager != null && _myTeam != null)
        {
            currentGame = DatabaseManager.Instance.GetLastPlayedGame(_manager.id, _myTeam.id);
        }

        if (currentGame != null && !string.IsNullOrEmpty(currentGame.game_date))
        {
            if (System.DateTime.TryParse(currentGame.game_date, out var gameDate))
            {
                _currentGameDate = gameDate;
                _currentMonthDate = new System.DateTime(gameDate.Year, gameDate.Month, 1);
                _selectedDate = gameDate;
                // Pre-populate the sidebar
                var dayGames = _allGames.Where(g => g.game_date == currentGame.game_date).ToList();
                OnDaySelected(gameDate.Day, currentGame.game_date, dayGames, rebuildCalendar: false);
            }
            else
            {
                DefaultMonth();
            }
        }
        else
        {
            DefaultMonth();
        }
    }

    void DefaultMonth()
    {
        _currentGameDate = null;
        if (_season != null)
            _currentMonthDate = new System.DateTime(_season.year_start, 9, 1);
        else
            _currentMonthDate = System.DateTime.Now;
    }

    void RegisterCallbacks()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("RosterSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });
        _root.Q<Button>("SubmenuJugadores")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        _root.Q<Button>("SubmenuEmpleados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Employees); });
        _root.Q<Button>("SubmenuLesionados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Injured); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Palmares); });
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Records); });
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("MarketSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });

        _root.Q<Button>("SubmenuOfertas")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Market);
        });
        _root.Q<Button>("SubmenuCartera")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Cartera); });
        _root.Q<Button>("SubmenuHistorial")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Historial); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("FinanceSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });
        _root.Q<Button>("SubmenuDecisiones")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Finances);
        });
        _root.Q<Button>("SubmenuPrestamos")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Loans);
        });
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });

        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });

        _btnPrevMonth?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeMonth(-1); });
        _btnNextMonth?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeMonth(1); });
        _btnAction?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnActionClicked(); });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnPrevMonth);
            CursorManager.Instance.RegisterHandCursor(_btnNextMonth);
            CursorManager.Instance.RegisterHandCursor(_btnAction);
        }
    }

    void Refresh()
    {
        RefreshHeader();
        BuildCalendar();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites64.TryGetValue(_myTeam.logo, out var sprite))
            _root.Q<VisualElement>("HeaderTeamLogo").style.backgroundImage = new StyleBackground(sprite);

        _root.Q<Label>("HeaderTeamName").text = _myTeam.name.ToUpper();
        _root.Q<Label>("HeaderManagerName").text = $"Manager: {_manager.name}";
        var budgetLabel = _root.Q<Label>("HeaderBudget");
        budgetLabel.text = $"${_myTeam.budget / 1_000_000}M";
        budgetLabel.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long totalPayroll = players.Sum(p => p.salary);
        _root.Q<Label>("HeaderPayroll").text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? 155_000_000;
        long margin = salaryCap - totalPayroll;
        var marginLbl = _root.Q<Label>("HeaderMargin");
        marginLbl.text = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        marginLbl.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) marginLbl.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _root.Q<Label>("HeaderSeason").text = $"Temporada {_season.year_start}-{_season.year_end}";
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "DASHBOARD";
    }

    void ChangeMonth(int delta)
    {
        _currentMonthDate = _currentMonthDate.AddMonths(delta);
        _selectedDate = null;
        BuildCalendar();
        _selectedDayGames.Clear();
        _noGamesText.style.display = DisplayStyle.Flex;
        _selectedDayTitle.text = "";
    }

    void BuildCalendar()
    {
        _calendarDays.Clear();
        _currentMonth.text = $"{MonthNames[_currentMonthDate.Month]} {_currentMonthDate.Year}".ToUpper();

        int year = _currentMonthDate.Year;
        int month = _currentMonthDate.Month;
        int daysInMonth = System.DateTime.DaysInMonth(year, month);
        int firstDayOfWeek = (int)new System.DateTime(year, month, 1).DayOfWeek;
        if (firstDayOfWeek == 0) firstDayOfWeek = 7;
        firstDayOfWeek--;

        int totalCells = firstDayOfWeek + daysInMonth;
        int rows = (totalCells + 6) / 7;

        for (int i = 0; i < rows * 7; i++)
        {
            var cell = new VisualElement();
            cell.AddToClassList("calendar-day-cell");

            int dayNum = i - firstDayOfWeek + 1;
            if (dayNum < 1 || dayNum > daysInMonth)
            {
                cell.AddToClassList("calendar-day-cell--empty");
            }
            else
            {
                var dayStr = $"{year}-{month:D2}-{dayNum:D2}";
                var dayGames = _allGames.Where(g => g.game_date == dayStr).ToList();
                bool hasAllStar = dayGames.Any(g => g.game_type == "allstar");
                bool isMyGame = dayGames.Any(g => g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id) || hasAllStar;
                bool isToday = _currentGameDate.HasValue && _currentGameDate.Value.Year == year
                                && _currentGameDate.Value.Month == month
                                && _currentGameDate.Value.Day == dayNum;

                if (isToday) cell.AddToClassList("calendar-day-cell--today");
                if (isMyGame) cell.AddToClassList("calendar-day-cell--my-game");

                if (isMyGame)
                {
                    BuildMyGameCell(cell, dayNum, dayStr, dayGames);
                }
                else
                {
                    BuildNormalCell(cell, dayNum, dayStr, dayGames);
                }

                int capturedDay = dayNum;
                cell.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnDaySelected(capturedDay, dayStr, dayGames); });
                if (CursorManager.Instance != null)
                {
                    cell.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
                    cell.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
                }
            }

            _calendarDays.Add(cell);
        }
    }

    void BuildMyGameCell(VisualElement cell, int dayNum, string dayStr, List<GameData> dayGames)
    {
        cell.style.justifyContent = Justify.Center;
        cell.style.alignItems = Align.Center;
        cell.style.paddingTop = 0;
        cell.style.paddingBottom = 0;
        cell.style.paddingLeft = 0;
        cell.style.paddingRight = 0;

        // Day number at top-right
        var numLbl = new Label();
        numLbl.AddToClassList("calendar-day-number");
        numLbl.AddToClassList("calendar-day-number--top-right");
        numLbl.text = dayNum.ToString();
        cell.Add(numLbl);

        var myGame = dayGames.FirstOrDefault(g => g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id);
        if (myGame != null)
        {
            // Opponent logo centered, 80x80
            bool isHome = myGame.home_team_id == _myTeam.id;
            int oppId = isHome ? myGame.away_team_id : myGame.home_team_id;
            var oppTeam = _allTeams.Find(t => t.id == oppId);

            var logoContainer = new VisualElement();
            logoContainer.style.width = 80;
            logoContainer.style.height = 80;
            logoContainer.style.flexShrink = 0;
            logoContainer.style.alignSelf = Align.Center;
            if (oppTeam != null && _logoSprites80.TryGetValue(oppTeam.logo, out var sprite))
            {
                logoContainer.style.backgroundImage = new StyleBackground(sprite);
                logoContainer.style.unityBackgroundImageTintColor = new StyleColor(new Color(1, 1, 1, 1f));
            }
            cell.Add(logoContainer);
        }
        else if (dayGames.Any(g => g.game_type == "allstar"))
        {
            var logoContainer = new VisualElement();
            logoContainer.style.width = 80;
            logoContainer.style.height = 80;
            logoContainer.style.flexShrink = 0;
            logoContainer.style.alignSelf = Align.Center;
            if (_logoSprites80.TryGetValue("all-star-game", out var asSprite))
            {
                logoContainer.style.backgroundImage = new StyleBackground(asSprite);
                logoContainer.style.unityBackgroundImageTintColor = new StyleColor(new Color(1, 1, 1, 1f));
            }
            cell.Add(logoContainer);
        }
    }

    void BuildNormalCell(VisualElement cell, int dayNum, string dayStr, List<GameData> dayGames)
    {
        var numLbl = new Label();
        numLbl.AddToClassList("calendar-day-number");
        numLbl.AddToClassList("calendar-day-number--top-right");
        numLbl.text = dayNum.ToString();
        cell.Add(numLbl);
    }

    void OnDaySelected(int day, string dateStr, List<GameData> games, bool rebuildCalendar = true)
    {
        _selectedDate = new System.DateTime(_currentMonthDate.Year, _currentMonthDate.Month, day);
        _selectedDayTitle.text = $"{day} / {MonthNames[_currentMonthDate.Month].ToUpper()} / {_currentMonthDate.Year}";
        _selectedDayGames.Clear();

        if (games.Count == 0)
        {
            _noGamesText.style.display = DisplayStyle.Flex;
        }
        else
        {
            _noGamesText.style.display = DisplayStyle.None;
            foreach (var g in games)
            {
                var row = new VisualElement();
                row.AddToClassList("selected-game-row");
                bool isMyGame = g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id;
                if (isMyGame || g.game_type == "allstar") row.AddToClassList("selected-game-row--my-game");
                if (g.is_played == 1) row.AddToClassList("selected-game-row--played");

                if (g.game_type == "allstar")
                {
                    var teamsBlock = new VisualElement();
                    teamsBlock.AddToClassList("selected-game-teams");

                    var logo = new VisualElement();
                    logo.AddToClassList("selected-game-team-logo");
                    if (_logoSprites32.TryGetValue("all-star-game", out var asSprite))
                        logo.style.backgroundImage = new StyleBackground(asSprite);
                    teamsBlock.Add(logo);

                    var name = new Label();
                    name.AddToClassList("selected-game-team-name");
                    name.text = "ALL-STAR GAME";
                    teamsBlock.Add(name);

                    row.Add(teamsBlock);
                }
                else
                {
                    var home = _allTeams.Find(t => t.id == g.home_team_id);
                    var away = _allTeams.Find(t => t.id == g.away_team_id);

                    var teamsBlock = new VisualElement();
                    teamsBlock.AddToClassList("selected-game-teams");

                    var awayLogo = new VisualElement();
                    awayLogo.AddToClassList("selected-game-team-logo");
                    if (away != null && _logoSprites32.TryGetValue(away.logo, out var aSprite))
                        awayLogo.style.backgroundImage = new StyleBackground(aSprite);
                    teamsBlock.Add(awayLogo);

                    var awayName = new Label();
                    awayName.AddToClassList("selected-game-team-name");
                    awayName.text = away?.abbreviation ?? "???";
                    teamsBlock.Add(awayName);

                    var vs = new Label();
                    vs.AddToClassList("selected-game-vs");
                    vs.text = "@";
                    teamsBlock.Add(vs);

                    var homeLogo = new VisualElement();
                    homeLogo.AddToClassList("selected-game-team-logo");
                    if (home != null && _logoSprites32.TryGetValue(home.logo, out var hSprite))
                        homeLogo.style.backgroundImage = new StyleBackground(hSprite);
                    teamsBlock.Add(homeLogo);

                    var homeName = new Label();
                    homeName.AddToClassList("selected-game-team-name");
                    homeName.text = home?.abbreviation ?? "???";
                    teamsBlock.Add(homeName);

                    row.Add(teamsBlock);
                }

                if (g.is_played == 1)
                {
                    var score = new Label();
                    score.AddToClassList("selected-game-score");
                    score.text = $"{g.away_score} - {g.home_score}";
                    row.Add(score);
                }

                var type = new Label();
                type.AddToClassList("selected-game-type");
                type.text = g.game_type switch
                {
                    "preseason" => "AMISTOSO",
                    "regular" => "REGULAR",
                    "playin" => "PLAY-IN",
                    "playoff" => "PLAYOFF",
                    _ => g.game_type.ToUpper()
                };
                row.Add(type);

                _selectedDayGames.Add(row);
            }
        }

        if (rebuildCalendar)
            BuildCalendar();
    }

    void OnActionClicked()
    {
        ScreenManager.Instance.GoTo(GameScreen.Dashboard);
    }

    void LoadSidebarIcons()
    {
        var iconMap = new System.Collections.Generic.Dictionary<string, string>
        {
            {"NavDashboardIcon", "inicio"},
            {"NavRosterIcon", "plantilla"},
            {"NavCalendarIcon", "calendario"},
            {"NavStandingsIcon", "clasificacion"},
            {"NavPalmaresIcon", "palmares"},
            {"NavResultsIcon", "resultados"},
            {"NavPlayoffsIcon", "playoff"},
            {"NavStatsIcon", "estadisticas"},
            {"NavRecordsIcon", "records"},
            {"NavMarketIcon", "mercado"},
            {"NavFinancesIcon", "finanzas"},
            {"NavSponsorsIcon", "patrocinador"},
            {"NavTVIcon", "television"},
            {"NavArenaIcon", "pabellon"},
            {"NavMessagesIcon", "mensajes"},

        };

        foreach (var kv in iconMap)
        {
            var iconElem = _root.Q<VisualElement>(kv.Key);
            if (iconElem == null) continue;
            var tex = Resources.Load<Texture2D>($"Icons/{kv.Value}");
            if (tex != null)
                iconElem.style.backgroundImage = new StyleBackground(tex);
        }
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
