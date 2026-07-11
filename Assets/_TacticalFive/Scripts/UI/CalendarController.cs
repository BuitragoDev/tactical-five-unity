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
    private System.DateTime? _currentGameDate;

    private static readonly string[] MonthNames = {
        "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    };
    // Config modal
    private VisualElement _configModalOverlay;
    private VisualElement _configModalBox;
    private Button _btnConfigCerrar;
    private CustomSlider _configSliderMaster;
    private CustomSlider _configSliderMusic;
    private CustomSlider _configSliderSFX;
    private Label _configLabelMaster;
    private Label _configLabelMusic;
    private Label _configLabelSFX;
    private Button _configBtnQualityLow;
    private Button _configBtnQualityMedium;
    private Button _configBtnQualityHigh;
    private Button _configBtnQualityUltra;

    // Config confirm modals
    private VisualElement _configMainMenuConfirmOverlay;
    private Button _configBtnMainMenu;
    private Button _configBtnMainMenuYes;
    private Button _configBtnMainMenuNo;
    private VisualElement _configExitConfirmOverlay;
    private Button _configBtnExit;
    private Button _configBtnExitYes;
    private Button _configBtnExitNo;



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
        InitConfigModal();

        // Auto-select current game day
        try { AutoSelectCurrentDay(); } catch (System.Exception ex) { Debug.LogWarning($"[Calendar] AutoSelectCurrentDay error: {ex.Message}"); }
        Refresh();
        CursorManager.Instance?.SetDefaultCursor();
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
        // Get actual current date from database (for "today" highlight)
        string currentDateStr = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        System.DateTime currentDate = default;
        bool hasCurrentDate = !string.IsNullOrEmpty(currentDateStr)
                              && System.DateTime.TryParseExact(currentDateStr, "dd/MM/yyyy",
                                  System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.None, out currentDate);
        if (hasCurrentDate)
            _currentGameDate = currentDate;

        // Select the next unplayed game (or fallback to current date)
        GameData nextGame = _manager != null && _myTeam != null
            ? DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id)
            : null;

        if (hasCurrentDate)
        {
            _currentMonthDate = new System.DateTime(currentDate.Year, currentDate.Month, 1);
            var dayGames = _allGames.Where(g => g.game_date == currentDateStr).ToList();
            OnDaySelected(currentDate.Day, currentDateStr, dayGames);
        }
        else if (nextGame != null && System.DateTime.TryParse(nextGame.game_date, out var gameDate))
        {
            _currentMonthDate = new System.DateTime(gameDate.Year, gameDate.Month, 1);
            var dayGames = _allGames.Where(g => g.game_date == nextGame.game_date).ToList();
            OnDaySelected(gameDate.Day, nextGame.game_date, dayGames);
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
        // Sidebar unificado
        SidebarController.Attach(_root, GameScreen.Calendar);
        HeaderController.Attach(_root);
        var allSubmenus = new[] {
            _root.Q<VisualElement>("RosterSubmenu"),
            _root.Q<VisualElement>("PalmaresSubmenu"),
            _root.Q<VisualElement>("MarketSubmenu"),
            _root.Q<VisualElement>("FinanceSubmenu")
        };

        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("RosterSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
        });
        _root.Q<Button>("SubmenuJugadores")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        _root.Q<Button>("SubmenuEmpleados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Employees); });
        _root.Q<Button>("SubmenuLesionados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Injured); });
        _root.Q<Button>("SubmenuQuinteto")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Quinteto); });

        _root.Q<Button>("SubmenuEntrenamiento")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Training); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("PalmaresSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
        });
        _root.Q<Button>("SubmenuPalmares")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Palmares); });
        _root.Q<Button>("SubmenuRecords")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Records); });
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });

        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("MarketSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
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
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
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
        _root.Q<Button>("SubmenuSponsors")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("SubmenuTV")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });

        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OpenConfigModal(); });

        _btnPrevMonth?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeMonth(-1); });
        _btnNextMonth?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeMonth(1); });
        _btnAction?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnActionClicked(); });

        if (CursorManager.Instance == null) return;
        var cursor = CursorManager.Instance;
        cursor.RegisterHandCursor(_root.Q<Button>("NavDashboard"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavRoster"));
        foreach (var btn in _root.Query<Button>(null, "nav-submenu-item").Build())
            cursor.RegisterHandCursor(btn);
        cursor.RegisterHandCursor(_root.Q<Button>("NavStandings"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavPalmares"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavResults"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavPlayoffs"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavStats"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavMarket"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavFinances"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavArena"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavMessages"));
        cursor.RegisterHandCursor(_root.Q<VisualElement>("ConfigIcon"));
        cursor.RegisterHandCursor(_btnAction);
        cursor.RegisterHandCursor(_btnPrevMonth);
        cursor.RegisterHandCursor(_btnNextMonth);
    }

    void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Calendar] RefreshHeader error: {ex.Message}"); }
        BuildCalendar();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;
        if (_root.Q<Label>("HeaderTeamName") == null) return;

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
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - totalPayroll;
        var marginLbl = _root.Q<Label>("HeaderMargin");
        string marginText = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        marginLbl.text = marginText;
        var chemLabel = _root.Q<Label>("HeaderChemistry");
        if (chemLabel != null)
        {
            chemLabel.text = $"{chemistry.ToString()}%";
            chemLabel.RemoveFromClassList("header-stat-value--gold");
            chemLabel.RemoveFromClassList("header-stat-value--negative");
            if (chemistry < 40)
                chemLabel.AddToClassList("header-stat-value--negative");
            else if (chemistry < 70)
                chemLabel.AddToClassList("header-stat-value--gold");
        }
        marginLbl.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) marginLbl.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _root.Q<Label>("HeaderSeason").text = $"Temporada {_season.year_start}-{_season.year_end}";
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "MENÚ PRINCIPAL";
    }

    void ChangeMonth(int delta)
    {
        _currentMonthDate = _currentMonthDate.AddMonths(delta);
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

    void OnDaySelected(int day, string dateStr, List<GameData> games)
    {
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
            {"NavMarketIcon", "mercado"},
            {"NavFinancesIcon", "finanzas"},
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
    void InitConfigModal()
    {
        _configModalOverlay = _root.Q<VisualElement>("ConfigModalOverlay");
        _configModalBox     = _root.Q<VisualElement>("ConfigModalBox");
        _btnConfigCerrar    = _root.Q<Button>("ConfigBtnCerrar");

        _configSliderMaster = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderMaster"),
            _root.Q<VisualElement>("ConfigFillMaster"),
            _root.Q<VisualElement>("ConfigDraggerMaster"));
        _configSliderMusic  = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderMusic"),
            _root.Q<VisualElement>("ConfigFillMusic"),
            _root.Q<VisualElement>("ConfigDraggerMusic"));
        _configSliderSFX    = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderSFX"),
            _root.Q<VisualElement>("ConfigFillSFX"),
            _root.Q<VisualElement>("ConfigDraggerSFX"));
        _configLabelMaster  = _root.Q<Label>("ConfigLabelMaster");
        _configLabelMusic   = _root.Q<Label>("ConfigLabelMusic");
        _configLabelSFX     = _root.Q<Label>("ConfigLabelSFX");
        _configBtnQualityLow    = _root.Q<Button>("ConfigBtnQualityLow");
        _configBtnQualityMedium = _root.Q<Button>("ConfigBtnQualityMedium");
        _configBtnQualityHigh   = _root.Q<Button>("ConfigBtnQualityHigh");
        _configBtnQualityUltra  = _root.Q<Button>("ConfigBtnQualityUltra");

        _configBtnMainMenu     = _root.Q<Button>("ConfigBtnMainMenu");
        _configBtnExit         = _root.Q<Button>("ConfigBtnExit");

        _configMainMenuConfirmOverlay = _root.Q<VisualElement>("ConfigMainMenuConfirmOverlay");
        _configBtnMainMenuYes = _root.Q<Button>("ConfigBtnMainMenuYes");
        _configBtnMainMenuNo  = _root.Q<Button>("ConfigBtnMainMenuNo");

        _configExitConfirmOverlay = _root.Q<VisualElement>("ConfigExitConfirmOverlay");
        _configBtnExitYes = _root.Q<Button>("ConfigBtnExitYes");
        _configBtnExitNo  = _root.Q<Button>("ConfigBtnExitNo");

        _configSliderMaster.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMasterVolume(v);
            UpdateConfigLabels();
        };
        _configSliderMusic.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMusicVolume(v);
            UpdateConfigLabels();
        };
        _configSliderSFX.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetSFXVolume(v);
            UpdateConfigLabels();
        };

        _configBtnQualityLow?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(0); });
        _configBtnQualityMedium?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(1); });
        _configBtnQualityHigh?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(2); });
        _configBtnQualityUltra?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(3); });

        _btnConfigCerrar?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseConfigModal(); });
        _configModalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configModalOverlay)
                { PlayClick(); CloseConfigModal(); }
        });

        _configBtnMainMenu?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenMainMenuConfirmModal(); });
        _configBtnExit?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenExitConfirmModal(); });

        _configBtnMainMenuYes?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        _configBtnMainMenuNo?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseMainMenuConfirmModal();
        });
        _configMainMenuConfirmOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configMainMenuConfirmOverlay)
                { PlayClick(); CloseMainMenuConfirmModal(); }
        });

        _configBtnExitYes?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            QuitGame();
        });
        _configBtnExitNo?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseExitConfirmModal();
        });
        _configExitConfirmOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configExitConfirmOverlay)
                { PlayClick(); CloseExitConfirmModal(); }
        });
    }

    void OpenConfigModal()
    {
        CursorManager.Instance?.SetDefaultCursor();
        var am = AudioManager.Instance;
        if (am != null)
        {
            _configSliderMaster.SetValueWithoutNotify(am.MasterVolume);
            _configSliderMusic.SetValueWithoutNotify(am.MusicVolume);
            _configSliderSFX.SetValueWithoutNotify(am.SFXVolume);
            UpdateConfigLabels();
        }
        int q = QualitySettings.GetQualityLevel();
        UpdateConfigQualityButtons(Mathf.Clamp(q, 0, 3));

        _configModalOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configModalOverlay.AddToClassList("modal-overlay--visible");
        _configModalBox.AddToClassList("modal-box--visible");
    }

    void CloseConfigModal()
    {
        _configModalOverlay.RemoveFromClassList("modal-overlay--visible");
        _configModalBox.RemoveFromClassList("modal-box--visible");
    }

    void UpdateConfigLabels()
    {
        var am = AudioManager.Instance;
        if (am == null) return;
        if (_configLabelMaster != null)
            _configLabelMaster.text = $"{Mathf.RoundToInt(am.MasterVolume * 100)}%";
        if (_configLabelMusic != null)
            _configLabelMusic.text  = $"{Mathf.RoundToInt(am.MusicVolume  * 100)}%";
        if (_configLabelSFX != null)
            _configLabelSFX.text    = $"{Mathf.RoundToInt(am.SFXVolume    * 100)}%";
    }

    void SelectConfigQuality(int index)
    {
        AudioManager.Instance?.SetQualityLevel(index);
        UpdateConfigQualityButtons(index);
    }

    void UpdateConfigQualityButtons(int activeIndex)
    {
        var buttons = new[] { _configBtnQualityLow, _configBtnQualityMedium, _configBtnQualityHigh, _configBtnQualityUltra };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].EnableInClassList("settings-quality-btn--active", i == activeIndex);
        }
    }

    void OpenMainMenuConfirmModal()
    {
        PlayClick();
        _configMainMenuConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configMainMenuConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseMainMenuConfirmModal()
    {
        _configMainMenuConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.RemoveFromClassList("modal-box--visible");
    }

    void OpenExitConfirmModal()
    {
        PlayClick();
        _configExitConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configExitConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseExitConfirmModal()
    {
        _configExitConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.RemoveFromClassList("modal-box--visible");
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }



    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
