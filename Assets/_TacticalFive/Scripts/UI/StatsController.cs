using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class StatRow
{
    public int playerId;
    public string firstName;
    public string lastName;
    public string position;
    public string teamAbbrev;
    public int gp;
    public int totalPts;
    public int totalReb;
    public int totalAst;
    public int totalStl;
    public int totalBlk;
    public int totalFgm;
    public int totalFga;
    public int totalFg3m;
    public int totalFg3a;
    public int totalFtm;
    public int totalFta;
    public int totalTov;
    public float totalMin;
    public int totalVal;
    public int totalDd;
    public int totalTd;
    public float avgPts;
    public float avgReb;
    public float avgAst;
    public float avgStl;
    public float avgBlk;
    public float fgPct;
    public float fg3Pct;
    public float ftPct;
    public float avgMin;
    public float avgVal;
    public float avgTov;
}

public class StatsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private VisualElement _statsBody;
    private VisualElement _tableHeader;
    private Label _panelTitle;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;

    private Dictionary<string, Sprite> _logoSprites = new();
    private string _currentStat = "puntos";
    private string _currentMode = "season";
    private string _currentDisplay = "totals";

    private static readonly System.Globalization.CultureInfo _spanishCI = new("es-ES");

    private List<Button> _statTabs = new();
    private List<Button> _filterBtns = new();
    private List<Button> _modeBtns = new();

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        // Always reset to default filters when entering the screen
        _currentStat = "puntos";
        _currentMode = "season";
        _currentDisplay = "totals";

        CacheReferences();
        LoadSidebarIcons();
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _statsBody = _root.Q<VisualElement>("StatsBody");
        _tableHeader = _root.Q<VisualElement>("TableHeader");
        _panelTitle = _root.Q<Label>("PanelTitle");

        _statTabs.Clear();
        _filterBtns.Clear();
        _modeBtns.Clear();

        string[] tabNames = { "TabPuntos", "TabRebotes", "TabAsistencias", "TabRobos", "TabTapones",
                              "TabPctTC", "TabPct3P", "TabPctTL", "TabVal", "TabPerdidas",
                              "TabMinutos", "TabDD", "TabTD" };
        foreach (var name in tabNames)
        {
            var btn = _root.Q<Button>(name);
            if (btn != null) _statTabs.Add(btn);
        }
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos) _logoSprites[s.name] = s;

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

        string[] statKeys = { "puntos", "rebotes", "asistencias", "robos", "tapones",
                              "pcttc", "pct3p", "pcttl", "val", "perdidas",
                              "minutos", "dd", "td" };
        for (int i = 0; i < _statTabs.Count && i < statKeys.Length; i++)
        {
            int idx = i;
            _statTabs[i].RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowStats(statKeys[idx]); });
        }

        var btnSeason = _root.Q<Button>("BtnSeason");
        var btnHistorical = _root.Q<Button>("BtnHistorical");
        var btnMyTeam = _root.Q<Button>("BtnMyTeam");
        _filterBtns.Add(btnSeason);
        _filterBtns.Add(btnHistorical);
        _filterBtns.Add(btnMyTeam);
        btnSeason?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetMode("season"); });
        btnHistorical?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetMode("historical"); });
        btnMyTeam?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetMode("team"); });

        var btnTotals = _root.Q<Button>("BtnTotals");
        var btnAverages = _root.Q<Button>("BtnAverages");
        _modeBtns.Add(btnTotals);
        _modeBtns.Add(btnAverages);
        btnTotals?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetDisplay("totals"); });
        btnAverages?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetDisplay("averages"); });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetDefaultCursor();
            RegisterHandCursors();
        }
    }

    void RegisterHandCursors()
    {
        foreach (var btn in _root.Query<Button>(null, "nav-item").Build())
            CursorManager.Instance.RegisterHandCursor(btn);
        foreach (var btn in _root.Query<Button>(null, "nav-submenu-item").Build())
            CursorManager.Instance.RegisterHandCursor(btn);

        var cursorTargets = new[] { "BtnAction", "ConfigIcon",
            "NavDashboard", "NavRoster", "NavCalendar", "NavResults", "NavStandings",
            "NavPalmares", "NavPlayoffs", "NavStats", "NavMarket", "NavFinances",
            "NavArena", "NavMessages" };
        foreach (var name in cursorTargets)
        {
            var el = _root.Q<VisualElement>(name);
            if (el != null)
                CursorManager.Instance.RegisterHandCursor(el);
        }

        foreach (var btn in _statTabs)
            CursorManager.Instance.RegisterHandCursor(btn);

        var extraBtns = new[] { "BtnSeason", "BtnHistorical", "BtnMyTeam", "BtnTotals", "BtnAverages" };
        foreach (var name in extraBtns)
        {
            var el = _root.Q<Button>(name);
            if (el != null)
                CursorManager.Instance.RegisterHandCursor(el);
        }
    }

    void RegisterNavButtons()
    {
        var allSubmenus = new[]
        {
            _root.Q<VisualElement>("RosterSubmenu"),
            _root.Q<VisualElement>("PalmaresSubmenu"),
            _root.Q<VisualElement>("MarketSubmenu"),
            _root.Q<VisualElement>("FinanceSubmenu"),
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
        _root.Q<Button>("SubmenuVestuario")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Vestuario); });
        _root.Q<Button>("SubmenuEntrenamiento")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Training); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
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

    void Refresh()
    {
        RefreshHeader();

        // Dynamic season label
        if (_season != null)
        {
            var btnSeason = _root.Q<Button>("BtnSeason");
            if (btnSeason != null)
                btnSeason.text = $"TEMPORADA {_season.year_start}-{_season.year_end}";
        }

        // Team name on filter button
        if (_myTeam != null)
        {
            var btnMyTeam = _root.Q<Button>("BtnMyTeam");
            if (btnMyTeam != null)
                btnMyTeam.text = _myTeam.name.ToUpper();
        }

        ShowStats(_currentStat);
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
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

        _btnAction.text = "DASHBOARD";
    }

    void SetMode(string mode)
    {
        _currentMode = mode;
        foreach (var btn in _filterBtns)
        {
            btn.RemoveFromClassList("filter-btn--active");
        }
        if (mode == "season")
            _root.Q<Button>("BtnSeason")?.AddToClassList("filter-btn--active");
        else if (mode == "historical")
            _root.Q<Button>("BtnHistorical")?.AddToClassList("filter-btn--active");
        else
            _root.Q<Button>("BtnMyTeam")?.AddToClassList("filter-btn--active");
        ShowStats(_currentStat);
    }

    void SetDisplay(string display)
    {
        _currentDisplay = display;
        foreach (var btn in _modeBtns)
        {
            btn.RemoveFromClassList("mode-btn--active");
        }
        if (display == "totals")
            _root.Q<Button>("BtnTotals")?.AddToClassList("mode-btn--active");
        else
            _root.Q<Button>("BtnAverages")?.AddToClassList("mode-btn--active");
        ShowStats(_currentStat);
    }

    // ═══════════════════════════════════════════════════════
    // SHOW STATS — main entry point
    // ═══════════════════════════════════════════════════════
    void UpdateFilterVisuals()
    {
        // Stat tabs
        foreach (var btn in _statTabs)
            btn.RemoveFromClassList("stats-tab--active");
        var statTabName = _currentStat switch
        {
            "puntos" => "TabPuntos",
            "rebotes" => "TabRebotes",
            "asistencias" => "TabAsistencias",
            "robos" => "TabRobos",
            "tapones" => "TabTapones",
            "pcttc" => "TabPctTC",
            "pct3p" => "TabPct3P",
            "pcttl" => "TabPctTL",
            "val" => "TabVal",
            "perdidas" => "TabPerdidas",
            "minutos" => "TabMinutos",
            "dd" => "TabDD",
            "td" => "TabTD",
            _ => "TabPuntos"
        };
        _root.Q<Button>(statTabName)?.AddToClassList("stats-tab--active");

        // Time filters (season / historical / team)
        foreach (var btn in _filterBtns)
            btn.RemoveFromClassList("filter-btn--active");
        if (_currentMode == "season")
            _root.Q<Button>("BtnSeason")?.AddToClassList("filter-btn--active");
        else if (_currentMode == "historical")
            _root.Q<Button>("BtnHistorical")?.AddToClassList("filter-btn--active");
        else
            _root.Q<Button>("BtnMyTeam")?.AddToClassList("filter-btn--active");

        // Mode filters (totals / averages)
        foreach (var btn in _modeBtns)
            btn.RemoveFromClassList("mode-btn--active");
        if (_currentDisplay == "totals")
            _root.Q<Button>("BtnTotals")?.AddToClassList("mode-btn--active");
        else
            _root.Q<Button>("BtnAverages")?.AddToClassList("mode-btn--active");
    }

    void ShowStats(string stat)
    {
        _currentStat = stat;

        // Sync all filter button visuals
        UpdateFilterVisuals();

        // Panel title
        string statLabel = stat switch
        {
            "puntos" => "PUNTOS",
            "rebotes" => "REBOTES",
            "asistencias" => "ASISTENCIAS",
            "robos" => "ROBOS",
            "tapones" => "TAPONES",
            "pcttc" => "% TC",
            "pct3p" => "% 3P",
            "pcttl" => "% TL",
            "val" => "VAL",
            "perdidas" => "PÉRDIDAS",
            "minutos" => "MINUTOS",
            "dd" => "DOBLES-DOBLES",
            "td" => "TRIPLES-DOBLES",
            _ => "PUNTOS"
        };
        if (_panelTitle != null)
            _panelTitle.text = $"{statLabel} — LÍDERES DE LA LIGA";

        _statsBody.Clear();

        bool useAverages = _currentDisplay == "averages";
        var allPlayers = _allTeams.SelectMany(t => DatabaseManager.Instance.GetPlayersByTeam(t.id)).ToList();
        List<StatRow> playerAggs;

        if (_currentMode == "historical")
        {
            playerAggs = BuildHistoricalMergedStats(allPlayers);
        }
        else if (_currentMode == "team")
        {
            var teamPlayers = allPlayers.Where(p => p.team_id == _myTeam.id).ToList();
            playerAggs = BuildSeasonStats(teamPlayers);
        }
        else
        {
            playerAggs = BuildSeasonStats(allPlayers);
        }

        // Calculate averages for everyone
        foreach (var s in playerAggs)
        {
            int g = s.gp;
            s.avgPts = g > 0 ? (float)s.totalPts / g : 0;
            s.avgReb = g > 0 ? (float)s.totalReb / g : 0;
            s.avgAst = g > 0 ? (float)s.totalAst / g : 0;
            s.avgStl = g > 0 ? (float)s.totalStl / g : 0;
            s.avgBlk = g > 0 ? (float)s.totalBlk / g : 0;
            s.avgTov = g > 0 ? (float)s.totalTov / g : 0;
            s.avgMin = g > 0 ? s.totalMin / g : 0;
            s.avgVal = g > 0 ? (float)s.totalVal / g : 0;
            s.fgPct = s.totalFga > 0 ? (float)s.totalFgm / s.totalFga * 100f : 0;
            s.fg3Pct = s.totalFg3a > 0 ? (float)s.totalFg3m / s.totalFg3a * 100f : 0;
            s.ftPct = s.totalFta > 0 ? (float)s.totalFtm / s.totalFta * 100f : 0;
        }

        // Sort
        var sorted = SortStats(playerAggs, stat, useAverages);
        var top = sorted.Take(100).ToList();

        // Build dynamic header
        BuildDynamicHeader(stat, useAverages);

        // Render rows
        RenderDynamicRows(top, stat, useAverages, allPlayers);
    }

    // ═══════════════════════════════════════════════════════
    // BUILD SEASON STATS (aggregates from PlayerGameStats)
    // ═══════════════════════════════════════════════════════
    List<StatRow> BuildSeasonStats(List<PlayerData> allPlayers)
    {
        var allStats = new List<PlayerGameStats>();

        // Only count regular season games (same as Django)
        if (_season != null && _manager != null)
        {
            var games = DatabaseManager.Instance.GetSeasonGames(_manager.id, _season.id)
                .Where(g => g.game_type == "regular").ToList();
            var gameIds = new HashSet<int>(games.Select(g => g.id));
            foreach (var player in allPlayers)
            {
                var playerStats = DatabaseManager.Instance.GetPlayerGameStats(player.id)
                    .Where(s => gameIds.Contains(s.game_id))
                    .ToList();
                allStats.AddRange(playerStats);
            }
        }

        return allStats
            .GroupBy(s => s.player_id)
            .Select(g => new StatRow
            {
                playerId = g.Key,
                gp = g.Count(),
                totalPts = g.Sum(s => s.points),
                totalReb = g.Sum(s => s.rebounds),
                totalAst = g.Sum(s => s.assists),
                totalStl = g.Sum(s => s.steals),
                totalBlk = g.Sum(s => s.blocks),
                totalFgm = g.Sum(s => s.fgm),
                totalFga = g.Sum(s => s.fga),
                totalFg3m = g.Sum(s => s.fg3m),
                totalFg3a = g.Sum(s => s.fg3a),
                totalFtm = g.Sum(s => s.ftm),
                totalFta = g.Sum(s => s.fta),
                totalTov = g.Sum(s => s.turnovers),
                totalMin = g.Sum(s => s.minutes),
                totalVal = g.Sum(s => s.rating),
                totalDd = g.Sum(s => s.double_double),
                totalTd = g.Sum(s => s.triple_double),
            })
            .ToList();
    }

    // ═══════════════════════════════════════════════════════
    // BUILD HISTORICAL MERGED STATS (Django-style runtime merge)
    // ═══════════════════════════════════════════════════════
    List<StatRow> BuildHistoricalMergedStats(List<PlayerData> allPlayers)
    {
        // 1. Get season stats first
        var seasonStats = BuildSeasonStats(allPlayers);

        // 2. Get all historical stats
        var histData = DatabaseManager.Instance.GetAllHistoricalPlayerStats();

        // 3. Build dict keyed by (first, last)
        var histDict = new Dictionary<(string first, string last), HistoricalPlayerStatsData>();
        foreach (var h in histData)
        {
            var key = (h.first_name.ToLower(), h.last_name.ToLower());
            histDict[key] = h;
        }

        var result = new List<StatRow>();
        var mergedKeys = new HashSet<(string, string)>();

        // 4. Merge: for active players, add historical to season
        foreach (var s in seasonStats)
        {
            var player = allPlayers.FirstOrDefault(p => p.id == s.playerId);
            if (player == null) continue;

            var key = (player.first_name.ToLower(), player.last_name.ToLower());
            s.firstName = player.first_name;
            s.lastName = player.last_name;
            s.position = player.position;

            var team = _allTeams.Find(t => t.id == player.team_id);
            s.teamAbbrev = team?.abbreviation ?? "FA";

            if (histDict.TryGetValue(key, out var h))
            {
                s.gp += h.games;
                s.totalPts += h.total_points;
                s.totalReb += h.total_rebounds;
                s.totalAst += h.total_assists;
                s.totalStl += h.total_steals;
                s.totalBlk += h.total_blocks;
                s.totalFgm += h.total_fgm;
                s.totalFga += h.total_fga;
                s.totalFg3m += h.total_fg3m;
                s.totalFg3a += h.total_fg3a;
                s.totalFtm += h.total_ftm;
                s.totalFta += h.total_fta;
                s.totalTov += h.total_turnovers;
                s.totalMin += h.games * 36;  // Django estimates 36 min per historical game
                s.totalVal += h.total_rating;
                s.totalDd += h.total_double_doubles;
                s.totalTd += h.total_triple_doubles;
                mergedKeys.Add(key);
            }
            result.Add(s);
        }

        // 5. Add historical-only players not in current season
        foreach (var kvp in histDict)
        {
            if (!mergedKeys.Contains(kvp.Key))
            {
                var h = kvp.Value;
                result.Add(new StatRow
                {
                    playerId = 0,
                    firstName = h.first_name,
                    lastName = h.last_name,
                    position = h.position,
                    teamAbbrev = h.team_abbreviation ?? "FA",
                    gp = h.games,
                    totalPts = h.total_points,
                    totalReb = h.total_rebounds,
                    totalAst = h.total_assists,
                    totalStl = h.total_steals,
                    totalBlk = h.total_blocks,
                    totalFgm = h.total_fgm,
                    totalFga = h.total_fga,
                    totalFg3m = h.total_fg3m,
                    totalFg3a = h.total_fg3a,
                    totalFtm = h.total_ftm,
                    totalFta = h.total_fta,
                    totalTov = h.total_turnovers,
                    totalMin = h.games * 36,
                    totalVal = h.total_rating,
                    totalDd = h.total_double_doubles,
                    totalTd = h.total_triple_doubles,
                });
            }
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════
    // SORT STATS
    // ═══════════════════════════════════════════════════════
    List<StatRow> SortStats(List<StatRow> rows, string stat, bool useAverages)
    {
        return stat switch
        {
            "puntos" => useAverages
                ? rows.OrderByDescending(x => x.avgPts).ToList()
                : rows.OrderByDescending(x => x.totalPts).ToList(),
            "rebotes" => useAverages
                ? rows.OrderByDescending(x => x.avgReb).ToList()
                : rows.OrderByDescending(x => x.totalReb).ToList(),
            "asistencias" => useAverages
                ? rows.OrderByDescending(x => x.avgAst).ToList()
                : rows.OrderByDescending(x => x.totalAst).ToList(),
            "robos" => useAverages
                ? rows.OrderByDescending(x => x.avgStl).ToList()
                : rows.OrderByDescending(x => x.totalStl).ToList(),
            "tapones" => useAverages
                ? rows.OrderByDescending(x => x.avgBlk).ToList()
                : rows.OrderByDescending(x => x.totalBlk).ToList(),
            "pcttc" => rows.Where(x => x.totalFga >= 10).OrderByDescending(x => x.fgPct).ToList(),
            "pct3p" => rows.Where(x => x.totalFg3a >= 5).OrderByDescending(x => x.fg3Pct).ToList(),
            "pcttl" => rows.Where(x => x.totalFta >= 5).OrderByDescending(x => x.ftPct).ToList(),
            "val" => useAverages
                ? rows.OrderByDescending(x => x.avgVal).ToList()
                : rows.OrderByDescending(x => x.totalVal).ToList(),
            "perdidas" => useAverages
                ? rows.OrderByDescending(x => x.avgTov).ToList()
                : rows.OrderByDescending(x => x.totalTov).ToList(),
            "minutos" => useAverages
                ? rows.OrderByDescending(x => x.avgMin).ToList()
                : rows.OrderByDescending(x => x.totalMin).ToList(),
            "dd" => rows.OrderByDescending(x => x.totalDd).ToList(),
            "td" => rows.OrderByDescending(x => x.totalTd).ToList(),
            _ => rows.OrderByDescending(x => x.totalPts).ToList()
        };
    }

    // ═══════════════════════════════════════════════════════
    // DYNAMIC HEADER BUILDER
    // ═══════════════════════════════════════════════════════
    void BuildDynamicHeader(string stat, bool useAverages)
    {
        if (_tableHeader == null) return;
        _tableHeader.Clear();

        _tableHeader.Add(MakeHeaderCell("#", "col-rank", true));
        _tableHeader.Add(MakeHeaderCell("JUGADOR", "col-player-name", false));
        _tableHeader.Add(MakeHeaderCell("EQ", "col-team-abbrev", true));
        _tableHeader.Add(MakeHeaderCell("POS", "col-pos", false));
        _tableHeader.Add(MakeHeaderCell("PJ", "col-stat", false));

        string suffix = (useAverages && stat != "dd" && stat != "td") ? "/P" : "";

        switch (stat)
        {
            case "puntos":
                _tableHeader.Add(MakeHeaderCell($"PTOS{suffix}", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("TC%", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("3P%", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("TL%", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "rebotes":
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("ROF", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("RDF", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "asistencias":
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"PER{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "robos":
                _tableHeader.Add(MakeHeaderCell($"ROB{suffix}", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "tapones":
                _tableHeader.Add(MakeHeaderCell($"TAP{suffix}", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "pcttc":
                _tableHeader.Add(MakeHeaderCell("TC%", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("CONV", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "pct3p":
                _tableHeader.Add(MakeHeaderCell("3P%", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("CONV", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "pcttl":
                _tableHeader.Add(MakeHeaderCell("TL%", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("CONV", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "val":
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("TC%", "col-stat", false));
                break;
            case "perdidas":
                _tableHeader.Add(MakeHeaderCell($"PER{suffix}", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "minutos":
                _tableHeader.Add(MakeHeaderCell($"MIN{suffix}", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "dd":
                _tableHeader.Add(MakeHeaderCell("DD", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell("PTS", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("REB", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("AST", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("VAL", "col-stat", false));
                break;
            case "td":
                _tableHeader.Add(MakeHeaderCell("TD", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell("PTS", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("REB", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("AST", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("VAL", "col-stat", false));
                break;
        }
    }

    Label MakeHeaderCell(string text, string baseClass, bool isBold)
    {
        var lbl = new Label();
        lbl.AddToClassList(baseClass);
        lbl.AddToClassList("col-stat--header");
        if (isBold) lbl.AddToClassList("col-stat--bold");
        lbl.text = text;
        return lbl;
    }

    // ═══════════════════════════════════════════════════════
    // DYNAMIC ROW RENDERER
    // ═══════════════════════════════════════════════════════
    void RenderDynamicRows(List<StatRow> top, string stat, bool useAverages, List<PlayerData> allPlayers)
    {
        for (int i = 0; i < top.Count; i++)
        {
            var x = top[i];
            string playerName;
            string position;
            string teamAbbrev;
            bool isMyTeam = false;

            if (x.playerId <= 0)
            {
                playerName = $"{x.firstName} {x.lastName}";
                position = x.position;
                teamAbbrev = x.teamAbbrev ?? "FA";
            }
            else
            {
                var player = allPlayers.FirstOrDefault(p => p.id == x.playerId);
                if (player == null) continue;
                playerName = $"{player.first_name} {player.last_name}";
                position = player.position;
                var team = _allTeams.Find(t => t.id == player.team_id);
                teamAbbrev = team?.abbreviation ?? "FA";
                isMyTeam = team != null && team.id == _myTeam.id;
            }

            var row = new VisualElement();
            row.AddToClassList("stats-row");
            if (isMyTeam)
                row.AddToClassList("stats-row--my-team");

            // Rank with badge for top 3
            var rankContainer = new VisualElement();
            rankContainer.AddToClassList("col-rank");
            var rankLbl = new Label();
            rankLbl.text = (i + 1).ToString();
            if (i < 3)
            {
                rankLbl.AddToClassList("rank-badge-top");
            }
            rankLbl.AddToClassList("col-stat--bold");
            rankContainer.Add(rankLbl);

            var nameLbl = new Label();
            nameLbl.AddToClassList("col-player-name");
            nameLbl.text = playerName;

            var abbrevLbl = new Label();
            abbrevLbl.AddToClassList("col-team-abbrev");
            abbrevLbl.text = teamAbbrev;
            abbrevLbl.AddToClassList("col-stat--bold");

            var posLbl = new Label();
            posLbl.AddToClassList("col-pos");
            posLbl.text = position;

            var gpLbl = new Label();
            gpLbl.AddToClassList("col-stat");
            gpLbl.text = x.gp.ToString("N0", _spanishCI);

            // Helper to create a stat cell with optional bold + leader
            Label MakeCell(string value, bool isActiveStat, bool isLeader)
            {
                var lbl = new Label();
                lbl.AddToClassList("col-stat");
                lbl.text = value;
                if (isActiveStat) lbl.AddToClassList("col-stat--bold");
                if (isLeader) lbl.AddToClassList("col-stat--leader");
                return lbl;
            }

            row.Add(rankContainer);
            row.Add(nameLbl);
            row.Add(abbrevLbl);
            row.Add(posLbl);
            row.Add(gpLbl);

            // Render columns based on stat type
            switch (stat)
            {
                case "puntos":
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), true, i == 0));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.fgPct.ToString("N1", _spanishCI), false, false));
                    row.Add(MakeCell(x.fg3Pct.ToString("N1", _spanishCI), false, false));
                    row.Add(MakeCell(x.ftPct.ToString("N1", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "rebotes":
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), true, i == 0));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell("—", false, false)); // ROF not tracked
                    row.Add(MakeCell("—", false, false)); // RDF not tracked
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "asistencias":
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), true, i == 0));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgTov.ToString("N1", _spanishCI) : x.totalTov.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "robos":
                    row.Add(MakeCell(useAverages ? x.avgStl.ToString("N1", _spanishCI) : x.totalStl.ToString("N0", _spanishCI), true, i == 0));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "tapones":
                    row.Add(MakeCell(useAverages ? x.avgBlk.ToString("N1", _spanishCI) : x.totalBlk.ToString("N0", _spanishCI), true, i == 0));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "pcttc":
                    row.Add(MakeCell(x.fgPct.ToString("N1", _spanishCI), true, i == 0));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell($"{x.totalFgm}/{x.totalFga}", false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "pct3p":
                    row.Add(MakeCell(x.fg3Pct.ToString("N1", _spanishCI), true, i == 0));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell($"{x.totalFg3m}/{x.totalFg3a}", false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "pcttl":
                    row.Add(MakeCell(x.ftPct.ToString("N1", _spanishCI), true, i == 0));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell($"{x.totalFtm}/{x.totalFta}", false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "val":
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), true, i == 0));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.fgPct.ToString("N1", _spanishCI), false, false));
                    break;
                case "perdidas":
                    row.Add(MakeCell(useAverages ? x.avgTov.ToString("N1", _spanishCI) : x.totalTov.ToString("N0", _spanishCI), true, i == 0));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "minutos":
                    row.Add(MakeCell(useAverages ? x.avgMin.ToString("N1", _spanishCI) : x.totalMin.ToString("N0", _spanishCI), true, i == 0));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "dd":
                    row.Add(MakeCell(x.totalDd.ToString("N0", _spanishCI), true, i == 0));
                    row.Add(MakeCell(x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "td":
                    row.Add(MakeCell(x.totalTd.ToString("N0", _spanishCI), true, i == 0));
                    row.Add(MakeCell(x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
            }

            _statsBody.Add(row);
        }

        if (top.Count == 0)
        {
            var empty = new VisualElement();
            empty.AddToClassList("stats-empty");
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("stats-empty-label");
            emptyLbl.text = "No hay datos estadísticos disponibles todavía.\nJuega partidos para ver estadísticas.";
            empty.Add(emptyLbl);
            _statsBody.Add(empty);
        }
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
