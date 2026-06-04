using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class ResultsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private Button _btnPrevDate;
    private Button _btnNextDate;
    private Button _btnCalendar;
    private Label _currentDateLabel;
    private VisualElement _calendarPicker;
    private Button _btnCalPrev;
    private Button _btnCalNext;
    private Label _calMonthLabel;
    private VisualElement _calDays;
    private VisualElement _resultsBody;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;
    private List<GameData> _allGames;

    private Dictionary<string, Sprite> _logoSprites = new();
    private List<string> _gameDates = new();
    private int _currentDateIndex = 0;
    private bool _calendarOpen = false;
    private System.DateTime _calendarMonth;

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
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _btnPrevDate = _root.Q<Button>("BtnPrevDate");
        _btnNextDate = _root.Q<Button>("BtnNextDate");
        _btnCalendar = _root.Q<Button>("BtnCalendar");
        _currentDateLabel = _root.Q<Label>("CurrentDateLabel");
        _calendarPicker = _root.Q<VisualElement>("CalendarPicker");
        _btnCalPrev = _root.Q<Button>("BtnCalPrev");
        _btnCalNext = _root.Q<Button>("BtnCalNext");
        _calMonthLabel = _root.Q<Label>("CalMonthLabel");
        _calDays = _root.Q<VisualElement>("CalDays");
        _resultsBody = _root.Q<VisualElement>("ResultsBody");
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        if (_myTeam == null) return;
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams();

        _allGames = DatabaseManager.Instance.GetAllGames(_manager.id);
        if (_allGames == null) _allGames = new List<GameData>();

        _gameDates = _allGames
            .Select(g => g.game_date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var myPlayedGames = _allGames
            .Where(g => (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id) && g.is_played == 1)
            .OrderByDescending(g => g.game_date)
            .ToList();

        if (myPlayedGames.Count > 0)
        {
            _currentDateIndex = _gameDates.IndexOf(myPlayedGames[0].game_date);
            if (_currentDateIndex < 0) _currentDateIndex = 0;
        }
        else if (_gameDates.Count > 0)
        {
            _currentDateIndex = 0;
        }

        _calendarMonth = _season != null
            ? new System.DateTime(_season.year_start, 10, 1)
            : System.DateTime.Now;
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();
        _btnPrevDate?.RegisterCallback<ClickEvent>(_ => NavigateDate(-1));
        _btnNextDate?.RegisterCallback<ClickEvent>(_ => NavigateDate(1));
        _btnCalendar?.RegisterCallback<ClickEvent>(_ => ToggleCalendar());
        _btnCalPrev?.RegisterCallback<ClickEvent>(_ => ChangeCalendarMonth(-1));
        _btnCalNext?.RegisterCallback<ClickEvent>(_ => ChangeCalendarMonth(1));
        _btnAction?.RegisterCallback<ClickEvent>(_ => ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("BtnReset")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.MainMenu));
    }

    void RegisterNavButtons()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Roster));
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Calendar));
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Standings));
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Palmares));
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
    }

    void Refresh()
    {
        RefreshHeader();
        UpdateDateLabel();
        ShowResults();
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
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetNextGameDateString(_manager.id, _myTeam.id);
        }

        _btnAction.text = "← DASHBOARD";
    }

    void NavigateDate(int delta)
    {
        _currentDateIndex = Mathf.Clamp(_currentDateIndex + delta, 0, _gameDates.Count - 1);
        UpdateDateLabel();
        ShowResults();
    }

    void UpdateDateLabel()
    {
        if (_currentDateLabel == null) return;

        if (_gameDates.Count == 0)
        {
            _currentDateLabel.text = "SIN PARTIDOS";
            return;
        }

        try
        {
            var dt = System.DateTime.Parse(_gameDates[_currentDateIndex]);
            _currentDateLabel.text = $"{dt.Day} {GetMonthName(dt.Month).ToUpper()} {dt.Year}";
        }
        catch
        {
            _currentDateLabel.text = _gameDates[_currentDateIndex];
        }
    }

    void ToggleCalendar()
    {
        _calendarOpen = !_calendarOpen;
        _calendarPicker.style.display = _calendarOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if (_calendarOpen)
        {
            BuildCalendar();
        }
    }

    void ChangeCalendarMonth(int delta)
    {
        _calendarMonth = _calendarMonth.AddMonths(delta);
        BuildCalendar();
    }

    void BuildCalendar()
    {
        _calDays.Clear();
        _calMonthLabel.text = $"{MonthNames[_calendarMonth.Month]} {_calendarMonth.Year}".ToUpper();

        int year = _calendarMonth.Year;
        int month = _calendarMonth.Month;
        int daysInMonth = System.DateTime.DaysInMonth(year, month);
        int firstDayOfWeek = (int)new System.DateTime(year, month, 1).DayOfWeek;
        if (firstDayOfWeek == 0) firstDayOfWeek = 7;
        firstDayOfWeek--;

        int totalCells = firstDayOfWeek + daysInMonth;
        int rows = (totalCells + 6) / 7;

        for (int i = 0; i < rows * 7; i++)
        {
            var cell = new VisualElement();
            cell.AddToClassList("cal-day-cell");

            int dayNum = i - firstDayOfWeek + 1;
            if (dayNum < 1 || dayNum > daysInMonth)
            {
                cell.AddToClassList("cal-day-cell--empty");
            }
            else
            {
                var dateStr = $"{year}-{month:D2}-{dayNum:D2}";
                bool hasGames = _gameDates.Contains(dateStr);
                bool isMyGame = _allGames.Any(g => g.game_date == dateStr &&
                    (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
                bool isSelected = dateStr == _gameDates[_currentDateIndex];

                if (hasGames)
                {
                    cell.AddToClassList("cal-day-cell--has-games");
                    if (isMyGame) cell.AddToClassList("cal-day-cell--my-game");
                    if (isSelected) cell.AddToClassList("cal-day-cell--selected");

                    int capturedIndex = _gameDates.IndexOf(dateStr);
                    cell.RegisterCallback<ClickEvent>(_ => SelectCalendarDate(capturedIndex));
                }

                var numLbl = new Label();
                numLbl.AddToClassList("cal-day-number");
                numLbl.text = dayNum.ToString();
                cell.Add(numLbl);
            }

            _calDays.Add(cell);
        }
    }

    void SelectCalendarDate(int dateIndex)
    {
        if (dateIndex >= 0 && dateIndex < _gameDates.Count)
        {
            _currentDateIndex = dateIndex;
            UpdateDateLabel();
            ShowResults();
            ToggleCalendar();
        }
    }

    void ShowResults()
    {
        _resultsBody.Clear();

        if (_gameDates.Count == 0)
        {
            var noResults = new VisualElement();
            noResults.AddToClassList("no-results");
            var noLbl = new Label();
            noLbl.AddToClassList("no-results-text");
            noLbl.text = "NO HAY PARTIDOS PROGRAMADOS";
            noResults.Add(noLbl);
            _resultsBody.Add(noResults);
            return;
        }

        var currentDate = _gameDates[_currentDateIndex];
        var dayGames = _allGames.Where(g => g.game_date == currentDate).ToList();

        if (dayGames.Count == 0)
        {
            var noResults = new VisualElement();
            noResults.AddToClassList("no-results");
            var noLbl = new Label();
            noLbl.AddToClassList("no-results-text");
            noLbl.text = "NO HAY PARTIDOS EN ESTA FECHA";
            noResults.Add(noLbl);
            _resultsBody.Add(noResults);
            return;
        }

        foreach (var g in dayGames)
        {
            var card = CreateGameCard(g);
            _resultsBody.Add(card);
        }
    }

    VisualElement CreateGameCard(GameData game)
    {
        var card = new VisualElement();
        card.style.flexDirection = FlexDirection.Row;
        card.style.alignItems = Align.Center;
        card.style.backgroundColor = new Color(28f/255f, 33f/255f, 48f/255f);
        card.style.borderTopLeftRadius = 8;
        card.style.borderTopRightRadius = 8;
        card.style.borderBottomLeftRadius = 8;
        card.style.borderBottomRightRadius = 8;
        card.style.borderTopWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftWidth = 1;
        card.style.borderTopColor = new Color(42f/255f, 51f/255f, 71f/255f);
        card.style.paddingLeft = 24;
        card.style.paddingRight = 24;
        card.style.paddingTop = 16;
        card.style.paddingBottom = 16;
        card.style.marginBottom = 16;

        bool isMyGame = game.home_team_id == _myTeam.id || game.away_team_id == _myTeam.id;
        if (isMyGame)
        {
            card.style.borderTopColor = new Color(212f/255f, 160f/255f, 23f/255f);
            card.style.borderRightColor = new Color(212f/255f, 160f/255f, 23f/255f);
            card.style.borderBottomColor = new Color(212f/255f, 160f/255f, 23f/255f);
            card.style.borderLeftColor = new Color(212f/255f, 160f/255f, 23f/255f);
            card.style.backgroundColor = new Color(212f/255f, 160f/255f, 23f/255f, 0.04f);
        }
        if (game.is_played != 1)
        {
            card.style.opacity = 0.7f;
        }

        var home = _allTeams.Find(t => t.id == game.home_team_id);
        var away = _allTeams.Find(t => t.id == game.away_team_id);

        // Home team side (left)
        var homeSide = new VisualElement();
        homeSide.style.flexDirection = FlexDirection.Row;
        homeSide.style.alignItems = Align.Center;
        homeSide.style.justifyContent = Justify.FlexEnd;
        homeSide.style.flexGrow = 1;
        homeSide.style.flexBasis = 0;
        homeSide.style.paddingRight = 10;

        var homeName = new Label();
        homeName.AddToClassList("results-team-name");
        homeName.text = home?.name ?? "???";
        if (isMyGame && game.home_team_id == _myTeam.id)
            homeName.style.color = new Color(212f/255f, 160f/255f, 23f/255f);
        homeSide.Add(homeName);

        var homeLogo = new VisualElement();
        homeLogo.style.width = 40;
        homeLogo.style.height = 40;
        homeLogo.style.flexShrink = 0;
        homeLogo.style.marginLeft = 10;
        if (home != null && _logoSprites.TryGetValue(home.logo, out var hSprite))
            homeLogo.style.backgroundImage = new StyleBackground(hSprite);
        homeSide.Add(homeLogo);

        card.Add(homeSide);

        // Score box
        var scoreBox = new VisualElement();
        scoreBox.style.flexDirection = FlexDirection.Row;
        scoreBox.style.alignItems = Align.Center;
        scoreBox.style.justifyContent = Justify.Center;
        scoreBox.style.backgroundColor = new Color(0, 0, 0, 0.25f);
        scoreBox.style.borderTopLeftRadius = 4;
        scoreBox.style.borderTopRightRadius = 4;
        scoreBox.style.borderBottomLeftRadius = 4;
        scoreBox.style.borderBottomRightRadius = 4;
        scoreBox.style.paddingLeft = 16;
        scoreBox.style.paddingRight = 16;
        scoreBox.style.paddingTop = 6;
        scoreBox.style.paddingBottom = 6;
        scoreBox.style.minWidth = 100;
        scoreBox.style.marginLeft = 16;
        scoreBox.style.marginRight = 16;

        if (game.is_played == 1)
        {
            var hs = new Label();
            hs.AddToClassList("results-score-label");
            hs.style.marginRight = 8;
            hs.text = game.home_score.ToString();
            scoreBox.Add(hs);

            var sep = new Label();
            sep.AddToClassList("results-score-sep");
            sep.style.marginRight = 8;
            sep.text = "-";
            scoreBox.Add(sep);

            var as2 = new Label();
            as2.AddToClassList("results-score-label");
            as2.text = game.away_score.ToString();
            scoreBox.Add(as2);
        }
        else
        {
            var e1 = new Label();
            e1.AddToClassList("results-score-sep");
            e1.style.marginRight = 8;
            e1.text = "-";
            scoreBox.Add(e1);

            var sep = new Label();
            sep.AddToClassList("results-score-sep");
            sep.style.marginRight = 8;
            sep.text = "-";
            scoreBox.Add(sep);

            var e2 = new Label();
            e2.AddToClassList("results-score-sep");
            e2.text = "-";
            scoreBox.Add(e2);
        }

        card.Add(scoreBox);

        // Away team side (right)
        var awaySide = new VisualElement();
        awaySide.style.flexDirection = FlexDirection.Row;
        awaySide.style.alignItems = Align.Center;
        awaySide.style.justifyContent = Justify.FlexStart;
        awaySide.style.flexGrow = 1;
        awaySide.style.flexBasis = 0;
        awaySide.style.paddingLeft = 10;

        var awayLogo = new VisualElement();
        awayLogo.style.width = 40;
        awayLogo.style.height = 40;
        awayLogo.style.flexShrink = 0;
        awayLogo.style.marginRight = 10;
        if (away != null && _logoSprites.TryGetValue(away.logo, out var aSprite))
            awayLogo.style.backgroundImage = new StyleBackground(aSprite);
        awaySide.Add(awayLogo);

        var awayName = new Label();
        awayName.AddToClassList("results-team-name");
        awayName.text = away?.name ?? "???";
        if (isMyGame && game.away_team_id == _myTeam.id)
            awayName.style.color = new Color(212f/255f, 160f/255f, 23f/255f);
        awaySide.Add(awayName);

        card.Add(awaySide);

        return card;
    }

    string GetMonthName(int month)
    {
        var names = new[] { "", "Ene", "Feb", "Mar", "Abr", "May", "Jun",
                           "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
        return names[month];
    }
}
