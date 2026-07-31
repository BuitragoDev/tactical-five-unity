using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class CalendarController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Calendar;

    protected override void OnBtnActionClicked()
    {
        OnActionClicked();
    }
    private Button _btnPrevMonth;
    private Button _btnNextMonth;
    private Label _currentMonth;
    private VisualElement _calendarDays;
    private Label _selectedDayTitle;
    private VisualElement _selectedDayGames;
    private Label _noGamesText;
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
    protected override void CacheReferences()
    {
        _btnPrevMonth = _root.Q<Button>("BtnPrevMonth");
        _btnNextMonth = _root.Q<Button>("BtnNextMonth");
        _currentMonth = _root.Q<Label>("CurrentMonth");
        _calendarDays = _root.Q<VisualElement>("CalendarDays");
        _selectedDayTitle = _root.Q<Label>("SelectedDayTitle");
        _selectedDayGames = _root.Q<VisualElement>("SelectedDayGames");
        _noGamesText = _root.Q<Label>("NoGamesText");
    }
    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos80 = Resources.LoadAll<Sprite>("Teams/Logos/80x80");
        foreach (var s in logos80) _logoSprites80[s.name] = s;

        var logos32 = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        
        

        
        
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
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _btnPrevMonth?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeMonth(-1); });
        _btnNextMonth?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangeMonth(1); });
        if (CursorManager.Instance == null) return;
        var cursor = CursorManager.Instance;
        cursor.RegisterHandCursor(_btnPrevMonth);
        cursor.RegisterHandCursor(_btnNextMonth);
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Calendar] RefreshHeader error: {ex.Message}"); }
        try { AutoSelectCurrentDay(); }
        catch (System.Exception ex) { Debug.LogWarning($"[Calendar] AutoSelectCurrentDay error: {ex.Message}"); }
        BuildCalendar();
    }
    protected override void RefreshHeader()
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
}
