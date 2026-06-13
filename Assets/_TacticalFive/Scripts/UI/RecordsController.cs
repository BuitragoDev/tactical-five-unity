using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class RecordsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private Button _tabTeam;
    private Button _tabSeason;
    private Button _tabHistorical;
    private VisualElement _recordsBody;
    private VisualElement _headerTeamCol;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;

    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();

    private string _currentFilter = "team";

    private static readonly Dictionary<string, string> StatLabels = new()
    {
        { "points", "PUNTOS" },
        { "rebounds", "REBOTES" },
        { "assists", "ASISTENCIAS" },
        { "steals", "ROBOS" },
        { "blocks", "TAPONES" },
        { "fgm", "TIROS" },
        { "fg3m", "TRIPLES" },
        { "ftm", "TIROS LIBRES" },
        { "turnovers", "PÉRDIDAS" }
    };

    private static readonly string[] StatOrder = {
        "points", "rebounds", "assists", "steals", "blocks",
        "fgm", "fg3m", "ftm", "turnovers"
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
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _tabTeam = _root.Q<Button>("TabTeam");
        _tabSeason = _root.Q<Button>("TabSeason");
        _tabHistorical = _root.Q<Button>("TabHistorical");
        _recordsBody = _root.Q<VisualElement>("RecordsBody");
        _headerTeamCol = _root.Q<VisualElement>("HeaderTeamCol");
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams();
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

        _tabTeam?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetFilter("team"); });
        _tabSeason?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetFilter("season"); });
        _tabHistorical?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetFilter("historical"); });
    }

    void RegisterNavButtons()
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
        _root.Q<Button>("SubmenuEntrenamiento")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Training); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("PalmaresSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
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
        _root.Q<Button>("SubmenuSponsors")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("SubmenuTV")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });
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

    void SetFilter(string filter)
    {
        _currentFilter = filter;
        _tabTeam.RemoveFromClassList("records-tab--active");
        _tabSeason.RemoveFromClassList("records-tab--active");
        _tabHistorical.RemoveFromClassList("records-tab--active");

        if (filter == "team") _tabTeam.AddToClassList("records-tab--active");
        else if (filter == "season") _tabSeason.AddToClassList("records-tab--active");
        else if (filter == "historical") _tabHistorical.AddToClassList("records-tab--active");

        Refresh();
    }

    void Refresh()
    {
        RefreshHeader();
        BuildRecords();
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
        string marginText = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        marginLbl.text = marginText;
        var chemLabel = _root.Q<Label>("HeaderChemistry");
        if (chemLabel != null)
        {
            chemLabel.text = chemistry.ToString();
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

        _btnAction.text = "DASHBOARD";
    }

    void BuildRecords()
    {
        _recordsBody.Clear();

        _headerTeamCol.style.display = DisplayStyle.Flex;

        if (_currentFilter == "team")
            BuildTeamRecords();
        else if (_currentFilter == "season")
            BuildSeasonRecords();
        else
            BuildHistoricalRecords();
    }

    void BuildTeamRecords()
    {
        if (_myTeam == null) return;
        var records = DatabaseManager.Instance.GetTeamRecords(_myTeam.id);
        var byStat = records.ToDictionary(r => r.stat_type, r => r);

        int count = 0;
        foreach (var stat in StatOrder)
        {
            if (!byStat.TryGetValue(stat, out var rec)) continue;
            var row = CreateRow(stat, rec.player_name, rec.value.ToString(), rec.game_date, null);
            _recordsBody.Add(row);
            count++;
        }

        if (count == 0) ShowEmpty("No hay récords del equipo todavía.");
    }

    void BuildSeasonRecords()
    {
        if (_season == null) return;
        var records = DatabaseManager.Instance.GetCurrentSeasonRecords(_season.id);
        var byStat = records.ToDictionary(r => r.stat_type, r => r);

        int count = 0;
        foreach (var stat in StatOrder)
        {
            if (!byStat.TryGetValue(stat, out var rec)) continue;
            var team = _allTeams?.Find(t => t.id == rec.team_id);
            var row = CreateRow(stat, rec.player_name, rec.value.ToString(), rec.game_date, team);
            _recordsBody.Add(row);
            count++;
        }

        if (count == 0) ShowEmpty("No hay récords de temporada todavía. Juega partidos para ver récords.");
    }

    void BuildHistoricalRecords()
    {
        var records = DatabaseManager.Instance.GetAllHistoricalRecords();
        var byStat = records.ToDictionary(r => r.stat_type, r => r);

        foreach (var stat in StatOrder)
        {
            if (!byStat.TryGetValue(stat, out var rec)) continue;
            var team = _allTeams?.Find(t => t.abbreviation == rec.team_abbreviation);
            var row = CreateRow(stat, rec.player_name, rec.value.ToString(), rec.game_date, team);
            _recordsBody.Add(row);
        }
    }

    VisualElement CreateRow(string statType, string playerName, string value, string gameDate, TeamData team)
    {
        var row = new VisualElement();
        row.AddToClassList("record-row");

        var statLbl = new Label();
        statLbl.AddToClassList("record-stat");
        statLbl.text = StatLabels.TryGetValue(statType, out var label) ? label : statType;
        row.Add(statLbl);

        var valLbl = new Label();
        valLbl.AddToClassList("record-value");
        valLbl.text = value;
        row.Add(valLbl);

        var playerLbl = new Label();
        playerLbl.AddToClassList("record-player");
        playerLbl.text = playerName;
        row.Add(playerLbl);

        {
            var teamLbl = new Label();
            teamLbl.AddToClassList("record-team");
            teamLbl.text = team?.name ?? _myTeam?.name ?? "";
            row.Add(teamLbl);
        }

        var dateLbl = new Label();
        dateLbl.AddToClassList("record-date");
        try
        {
            var dt = System.DateTime.Parse(gameDate);
            dateLbl.text = dt.ToString("dd/MM/yyyy");
        }
        catch
        {
            dateLbl.text = gameDate;
        }
        row.Add(dateLbl);

        return row;
    }

    void ShowEmpty(string message)
    {
        var empty = new VisualElement();
        empty.AddToClassList("records-empty");
        var lbl = new Label();
        lbl.AddToClassList("records-empty-label");
        lbl.text = message;
        empty.Add(lbl);
        _recordsBody.Add(empty);
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
