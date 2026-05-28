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

    private System.DateTime _currentMonthDate;
    private System.DateTime? _selectedDate;

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
        LoadData();
        RegisterCallbacks();

        if (_season != null)
            _currentMonthDate = new System.DateTime(_season.year_start, 9, 1);
        else
            _currentMonthDate = System.DateTime.Now;

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

        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _allGames = DatabaseManager.Instance.GetAllGames(_manager.id);
    }

    void RegisterCallbacks()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Roster));
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

        _btnPrevMonth?.RegisterCallback<ClickEvent>(_ => ChangeMonth(-1));
        _btnNextMonth?.RegisterCallback<ClickEvent>(_ => ChangeMonth(1));
        _btnAction?.RegisterCallback<ClickEvent>(_ => OnActionClicked());
        _root.Q<Button>("BtnReset")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.MainMenu));

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

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _root.Q<VisualElement>("HeaderTeamLogo").style.backgroundImage = new StyleBackground(sprite);

        _root.Q<Label>("HeaderTeamName").text = _myTeam.name.ToUpper();
        _root.Q<Label>("HeaderManagerName").text = $"Manager: {_manager.name}";
        _root.Q<Label>("HeaderBudget").text = $"${_myTeam.budget / 1_000_000}M";

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
            var nextGame = DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id);
            _root.Q<Label>("HeaderDate").text = nextGame != null
                ? System.DateTime.Parse(nextGame.game_date).ToString("dd/MM/yyyy") : "";
        }

        _btnAction.text = "← DASHBOARD";
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
                bool isMyGame = dayGames.Any(g => g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id);
                bool isToday = dayStr == System.DateTime.Now.ToString("yyyy-MM-dd");
                bool isSelected = _selectedDate.HasValue && _selectedDate.Value.Day == dayNum &&
                                  _selectedDate.Value.Month == month && _selectedDate.Value.Year == year;

                if (isToday) cell.AddToClassList("calendar-day-cell--today");
                if (isSelected) cell.AddToClassList("calendar-day-cell--selected");
                if (isMyGame) cell.AddToClassList("calendar-day-cell--my-game");
                else if (dayGames.Count > 0) cell.AddToClassList("calendar-day-cell--has-game");

                var numLbl = new Label();
                numLbl.AddToClassList("calendar-day-number");
                numLbl.text = dayNum.ToString();
                cell.Add(numLbl);

                if (dayGames.Count > 0)
                {
                    var dotsRow = new VisualElement();
                    dotsRow.AddToClassList("calendar-day-games");

                    var myGame = dayGames.FirstOrDefault(g => g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id);
                    var otherGames = dayGames.Where(g => g.home_team_id != _myTeam.id && g.away_team_id != _myTeam.id).Take(2).ToList();

                    if (myGame != null)
                    {
                        var dot = new VisualElement();
                        dot.AddToClassList("calendar-game-dot");
                        dot.AddToClassList("calendar-game-dot--my-game");
                        dotsRow.Add(dot);
                    }

                    foreach (var g in otherGames)
                    {
                        var dot = new VisualElement();
                        dot.AddToClassList("calendar-game-dot");
                        dotsRow.Add(dot);
                    }

                    cell.Add(dotsRow);
                }

                int capturedDay = dayNum;
                cell.RegisterCallback<ClickEvent>(_ => OnDaySelected(capturedDay, dayStr, dayGames));
                if (CursorManager.Instance != null)
                {
                    cell.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
                    cell.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
                }
            }

            _calendarDays.Add(cell);
        }
    }

    void OnDaySelected(int day, string dateStr, List<GameData> games)
    {
        _selectedDate = new System.DateTime(_currentMonthDate.Year, _currentMonthDate.Month, day);
        _selectedDayTitle.text = $"{day} {MonthNames[_currentMonthDate.Month].ToUpper()}";
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
                if (isMyGame) row.AddToClassList("selected-game-row--my-game");
                if (g.is_played == 1) row.AddToClassList("selected-game-row--played");

                var home = _allTeams.Find(t => t.id == g.home_team_id);
                var away = _allTeams.Find(t => t.id == g.away_team_id);

                var teamsBlock = new VisualElement();
                teamsBlock.AddToClassList("selected-game-teams");

                var awayLogo = new VisualElement();
                awayLogo.AddToClassList("selected-game-team-logo");
                if (away != null && _logoSprites.TryGetValue(away.logo, out var aSprite))
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
                if (home != null && _logoSprites.TryGetValue(home.logo, out var hSprite))
                    homeLogo.style.backgroundImage = new StyleBackground(hSprite);
                teamsBlock.Add(homeLogo);

                var homeName = new Label();
                homeName.AddToClassList("selected-game-team-name");
                homeName.text = home?.abbreviation ?? "???";
                teamsBlock.Add(homeName);

                row.Add(teamsBlock);

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
                    "regular" => "LIGA",
                    "playin" => "PLAY-IN",
                    "playoff" => "PLAYOFF",
                    _ => g.game_type.ToUpper()
                };
                row.Add(type);

                _selectedDayGames.Add(row);
            }
        }

        BuildCalendar();
    }

    void OnActionClicked()
    {
        ScreenManager.Instance.GoTo(GameScreen.Dashboard);
    }
}
