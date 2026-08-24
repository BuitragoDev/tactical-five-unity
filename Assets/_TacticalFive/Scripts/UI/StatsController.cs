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
    public float efgPct;
    public float tsPct;
    public float per;
}
    public class StatsController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Stats;
    private VisualElement _statsBody;
    private VisualElement _tableHeader;
    private Label _panelTitle;
    private List<TeamData> _allTeams;
    private Dictionary<string, Sprite> _logoSprites = new();
    private string _currentStat = "puntos";
    private string _currentMode = "season";
    private string _currentDisplay = "totals";
    private static readonly System.Globalization.CultureInfo _spanishCI = CreateDotCulture();
    private static System.Globalization.CultureInfo CreateDotCulture()
    {
        var ci = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
        ci.NumberFormat.NumberGroupSeparator = ".";
        ci.NumberFormat.NumberDecimalSeparator = ".";
        return ci;
    }
    private List<Button> _statTabs = new();
    private List<Button> _filterBtns = new();
    private List<Button> _modeBtns = new();
    private Button _previousPageBtn;
    private Button _nextPageBtn;
    private Button _firstPageBtn;
    private Button _lastPageBtn;
    private Label _pageLabel;
    private List<StatRow> _currentRows = new();
    private int _currentPage;
    private string _pagedStat = "";
    private bool _pagedAverages;
    private List<PlayerData> _pagedAllPlayers = new();
    private const int PAGE_SIZE = 10;
    protected override void OnEnable()
    {
        base.OnEnable();
        _currentStat = "puntos";
        _currentMode = "season";
        _currentDisplay = "totals";
    }
    protected override void CacheReferences()
    {
        _statsBody = _root.Q<VisualElement>("StatsBody");
        _tableHeader = _root.Q<VisualElement>("TableHeader");
        _panelTitle = _root.Q<Label>("PanelTitle");

        _statTabs.Clear();
        _filterBtns.Clear();
        _modeBtns.Clear();

        string[] tabNames = { "TabPuntos", "TabRebotes", "TabAsistencias", "TabRobos", "TabTapones",
                              "TabPctTC", "TabPct3P", "TabPctTL", "TabEFG", "TabTS", "TabPER",
                              "TabVal", "TabPerdidas", "TabMinutos", "TabDD", "TabTD" };
        foreach (var name in tabNames)
        {
            var btn = _root.Q<Button>(name);
            if (btn != null) _statTabs.Add(btn);
        }

        _previousPageBtn = _root.Q<Button>("BtnPreviousPage");
        _nextPageBtn = _root.Q<Button>("BtnNextPage");
        _firstPageBtn = _root.Q<Button>("BtnFirstPage");
        _lastPageBtn = _root.Q<Button>("BtnLastPage");
        _pageLabel = _root.Q<Label>("PageLabel");

        var previousIcon = _root.Q<Image>("PreviousPageIcon");
        var nextIcon = _root.Q<Image>("NextPageIcon");
        var previousSprite = Resources.Load<Sprite>("Icons/left_arrow");
        var nextSprite = Resources.Load<Sprite>("Icons/right_arrow");
        SetPageIcon(previousIcon, previousSprite);
        SetPageIcon(nextIcon, nextSprite);
        SetPageIcon(_root.Q<Image>("FirstPageIcon1"), previousSprite);
        SetPageIcon(_root.Q<Image>("FirstPageIcon2"), previousSprite);
        SetPageIcon(_root.Q<Image>("LastPageIcon1"), nextSprite);
        SetPageIcon(_root.Q<Image>("LastPageIcon2"), nextSprite);
    }

    void SetPageIcon(Image image, Sprite sprite)
    {
        if (image != null && sprite != null)
            image.style.backgroundImage = new StyleBackground(sprite);
    }
    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos) _logoSprites[s.name] = s;

        
        

        
        
        _allTeams = DatabaseManager.Instance.GetAllTeams();
    }
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        string[] statKeys = { "puntos", "rebotes", "asistencias", "robos", "tapones",
                              "pcttc", "pct3p", "pcttl", "efgpct", "tspct", "per",
                              "val", "perdidas", "minutos", "dd", "td" };
        for (int i = 0; i < _statTabs.Count && i < statKeys.Length; i++)
        {
            int idx = i;
            _statTabs[i].RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowStats(statKeys[idx]); });
        }
        var btnSeason = _root.Q<Button>("BtnSeason");
        var btnHistorical = _root.Q<Button>("BtnHistorical");
        var btnMyTeam = _root.Q<Button>("BtnMyTeam");
        var btnRookies = _root.Q<Button>("BtnRookies");
        _filterBtns.Add(btnSeason);
        _filterBtns.Add(btnHistorical);
        _filterBtns.Add(btnMyTeam);
        _filterBtns.Add(btnRookies);
        btnSeason?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetMode("season"); });
        btnHistorical?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetMode("historical"); });
        btnMyTeam?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetMode("team"); });
        btnRookies?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetMode("rookies"); });
        var btnTotals = _root.Q<Button>("BtnTotals");
        var btnAverages = _root.Q<Button>("BtnAverages");
        _modeBtns.Add(btnTotals);
        _modeBtns.Add(btnAverages);
        btnTotals?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetDisplay("totals"); });
        btnAverages?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetDisplay("averages"); });
        _previousPageBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangePage(-1); });
        _nextPageBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangePage(1); });
        _firstPageBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangePage(-10); });
        _lastPageBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangePage(10); });
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Stats] RefreshHeader error: {ex.Message}"); }

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
    protected override void RefreshHeader()
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

        _btnAction.text = "MENÚ PRINCIPAL";
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
        else if (mode == "rookies")
            _root.Q<Button>("BtnRookies")?.AddToClassList("filter-btn--active");
        else
            _root.Q<Button>("BtnMyTeam")?.AddToClassList("filter-btn--active");

        // Histórico: solo se muestran TOTALES; PROMEDIOS queda deshabilitado
        var btnAverages = _root.Q<Button>("BtnAverages");
        bool isHistorical = mode == "historical";
        if (btnAverages != null)
            btnAverages.SetEnabled(!isHistorical);
        if (isHistorical)
            _currentDisplay = "totals";

        ShowStats(_currentStat);
    }

    void SetDisplay(string display)
    {
        // En histórico no se permiten promedios (botón deshabilitado, pero por seguridad)
        if (_currentMode == "historical" && display == "averages") return;

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
            "efgpct" => "TabEFG",
            "tspct" => "TabTS",
            "per" => "TabPER",
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
        else if (_currentMode == "rookies")
            _root.Q<Button>("BtnRookies")?.AddToClassList("filter-btn--active");
        else
            _root.Q<Button>("BtnMyTeam")?.AddToClassList("filter-btn--active");

        // Mode filters (totals / averages)
        foreach (var btn in _modeBtns)
            btn.RemoveFromClassList("mode-btn--active");
        if (_currentDisplay == "totals")
            _root.Q<Button>("BtnTotals")?.AddToClassList("mode-btn--active");
        else
            _root.Q<Button>("BtnAverages")?.AddToClassList("mode-btn--active");

        // Histórico: PROMEDIOS deshabilitado y se muestra siempre TOTALES
        bool isHistorical = _currentMode == "historical";
        var averagesBtn = _root.Q<Button>("BtnAverages");
        if (averagesBtn != null)
            averagesBtn.SetEnabled(!isHistorical);
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
            "pcttc" => "PORCENTAJE TIROS DE CAMPO",
            "pct3p" => "PORCENTAJE TRIPLES",
            "pcttl" => "PORCENTAJE TIROS LIBRES",
            "efgpct" => "PORCENTAJE EFECTIVO DE CAMPO",
            "tspct" => "PORCENTAJE REAL DE TIRO",
            "per" => "EFICIENCIA POR MINUTO",
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
        else if (_currentMode == "rookies")
        {
            var rookiePlayers = allPlayers.Where(p => p.is_rookie == 1).ToList();
            playerAggs = BuildSeasonStats(rookiePlayers);
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
            s.efgPct = AdvancedStatsHelper.CalcEFG(s.totalFgm, s.totalFga, s.totalFg3m);
            s.tsPct = AdvancedStatsHelper.CalcTS(s.totalPts, s.totalFga, s.totalFta);
            var eff = AdvancedStatsHelper.CalcEff(s.totalPts, s.totalReb, s.totalAst, s.totalStl, s.totalBlk,
                                                  s.totalFgm, s.totalFga, s.totalFtm, s.totalFta, s.totalTov);
            s.per = AdvancedStatsHelper.CalcPER(eff, s.totalMin);
        }

        // Sort
        var sorted = SortStats(playerAggs, stat, useAverages);

        // Guardar contexto para el paginado
        _currentRows = sorted;
        _pagedStat = stat;
        _pagedAverages = useAverages;
        _pagedAllPlayers = allPlayers;
        _currentPage = 0;

        // Build dynamic header
        BuildDynamicHeader(stat, useAverages);

        // Render current page
        RenderCurrentPage();
    }

    void ChangePage(int direction)
    {
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(_currentRows.Count / (float)PAGE_SIZE));
        _currentPage = Mathf.Clamp(_currentPage + direction, 0, pageCount - 1);
        RenderCurrentPage();
    }

    void RenderCurrentPage()
    {
        if (_statsBody == null) return;

        int pageCount = Mathf.Max(1, Mathf.CeilToInt(_currentRows.Count / (float)PAGE_SIZE));
        _currentPage = Mathf.Clamp(_currentPage, 0, pageCount - 1);
        var pageRows = _currentRows
            .Skip(_currentPage * PAGE_SIZE)
            .Take(PAGE_SIZE)
            .ToList();

        _statsBody.Clear();
        RenderDynamicRows(pageRows, _pagedStat, _pagedAverages, _pagedAllPlayers, _currentPage * PAGE_SIZE);

        if (_pageLabel != null)
            _pageLabel.text = $"{_currentPage + 1} de {pageCount}";
        _previousPageBtn?.SetEnabled(_currentPage > 0);
        _nextPageBtn?.SetEnabled(_currentPage < pageCount - 1);
        _firstPageBtn?.SetEnabled(_currentPage > 0);
        _lastPageBtn?.SetEnabled(_currentPage < pageCount - 1);
    }

    // ═══════════════════════════════════════════════════════
    // BUILD SEASON STATS (aggregates from PlayerGameStats)
    // ═══════════════════════════════════════════════════════
    List<StatRow> BuildSeasonStats(List<PlayerData> players)
    {
        if (_season == null || _manager == null)
            return new List<StatRow>();
        if (players == null || players.Count == 0)
            return new List<StatRow>();

        // Agregación en SQL (JOIN player_game_stats + games, GROUP BY), sin N+1
        var aggregates = DatabaseManager.Instance.GetSeasonPlayerStatsAggregates(_manager.id, _season.id);
        if (aggregates.Count == 0) return new List<StatRow>();

        var ids = new HashSet<int>(players.Select(p => p.id));
        var rows = new List<StatRow>(aggregates.Count);
        foreach (var a in aggregates)
        {
            if (!ids.Contains(a.player_id)) continue;
            rows.Add(new StatRow
            {
                playerId = a.player_id,
                gp = a.gp,
                totalPts = a.total_points,
                totalReb = a.total_rebounds,
                totalAst = a.total_assists,
                totalStl = a.total_steals,
                totalBlk = a.total_blocks,
                totalFgm = a.total_fgm,
                totalFga = a.total_fga,
                totalFg3m = a.total_fg3m,
                totalFg3a = a.total_fg3a,
                totalFtm = a.total_ftm,
                totalFta = a.total_fta,
                totalTov = a.total_turnovers,
                totalMin = (float)a.total_minutes,
                totalVal = a.total_rating,
                totalDd = a.total_dd,
                totalTd = a.total_td,
            });
        }
        return rows;
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
            "efgpct" => rows.Where(x => x.totalFga >= 10).OrderByDescending(x => x.efgPct).ToList(),
            "tspct" => rows.Where(x => x.totalFga >= 10 && x.totalFta >= 5).OrderByDescending(x => x.tsPct).ToList(),
            "per" => rows.OrderByDescending(x => x.per).ToList(),
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
            case "efgpct":
                _tableHeader.Add(MakeHeaderCell("EFG%", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell("TC%", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "tspct":
                _tableHeader.Add(MakeHeaderCell("TS%", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"VAL{suffix}", "col-stat", false));
                break;
            case "per":
                _tableHeader.Add(MakeHeaderCell("PER", "col-stat", true));
                _tableHeader.Add(MakeHeaderCell($"PTS{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"REB{suffix}", "col-stat", false));
                _tableHeader.Add(MakeHeaderCell($"AST{suffix}", "col-stat", false));
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
                _tableHeader.Add(MakeHeaderCell($"TO{suffix}", "col-stat", true));
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
    void RenderDynamicRows(List<StatRow> top, string stat, bool useAverages, List<PlayerData> allPlayers, int rankOffset = 0)
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

            int globalRank = rankOffset + i + 1;
            bool isLeader = rankOffset == 0 && i == 0;

            // Rank with badge for top 3
            var rankContainer = new VisualElement();
            rankContainer.AddToClassList("col-rank");
            var rankLbl = new Label();
            rankLbl.text = globalRank.ToString();
            if (globalRank < 4)
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
            posLbl.text = PositionCodes.GetShort(position);

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
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.fgPct.ToString("N1", System.Globalization.CultureInfo.InvariantCulture), false, false));
                    row.Add(MakeCell(x.fg3Pct.ToString("N1", System.Globalization.CultureInfo.InvariantCulture), false, false));
                    row.Add(MakeCell(x.ftPct.ToString("N1", System.Globalization.CultureInfo.InvariantCulture), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "rebotes":
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell("—", false, false)); // ROF not tracked
                    row.Add(MakeCell("—", false, false)); // RDF not tracked
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "asistencias":
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgTov.ToString("N1", _spanishCI) : x.totalTov.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "robos":
                    row.Add(MakeCell(useAverages ? x.avgStl.ToString("N1", _spanishCI) : x.totalStl.ToString("N0", _spanishCI), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "tapones":
                    row.Add(MakeCell(useAverages ? x.avgBlk.ToString("N1", _spanishCI) : x.totalBlk.ToString("N0", _spanishCI), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "pcttc":
                    row.Add(MakeCell(x.fgPct.ToString("N1", System.Globalization.CultureInfo.InvariantCulture), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell($"{x.totalFgm}/{x.totalFga}", false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "pct3p":
                    row.Add(MakeCell(x.fg3Pct.ToString("N1", System.Globalization.CultureInfo.InvariantCulture), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell($"{x.totalFg3m}/{x.totalFg3a}", false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "pcttl":
                    row.Add(MakeCell(x.ftPct.ToString("N1", System.Globalization.CultureInfo.InvariantCulture), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell($"{x.totalFtm}/{x.totalFta}", false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "efgpct":
                    row.Add(MakeCell(x.efgPct.ToString("N1", System.Globalization.CultureInfo.InvariantCulture), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.fgPct.ToString("N1", System.Globalization.CultureInfo.InvariantCulture), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "tspct":
                    row.Add(MakeCell(x.tsPct.ToString("N1", System.Globalization.CultureInfo.InvariantCulture), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "per":
                    row.Add(MakeCell(x.per.ToString("N1", _spanishCI), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "val":
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.fgPct.ToString("N1", System.Globalization.CultureInfo.InvariantCulture), false, false));
                    break;
                case "perdidas":
                    row.Add(MakeCell(useAverages ? x.avgTov.ToString("N1", _spanishCI) : x.totalTov.ToString("N0", _spanishCI), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "minutos":
                    row.Add(MakeCell(useAverages ? x.avgMin.ToString("N1", _spanishCI) : x.totalMin.ToString("N0", _spanishCI), true, isLeader));
                    row.Add(MakeCell(useAverages ? x.avgPts.ToString("N1", _spanishCI) : x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgReb.ToString("N1", _spanishCI) : x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgAst.ToString("N1", _spanishCI) : x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(useAverages ? x.avgVal.ToString("N1", _spanishCI) : x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "dd":
                    row.Add(MakeCell(x.totalDd.ToString("N0", _spanishCI), true, isLeader));
                    row.Add(MakeCell(x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
                case "td":
                    row.Add(MakeCell(x.totalTd.ToString("N0", _spanishCI), true, isLeader));
                    row.Add(MakeCell(x.totalPts.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalReb.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalAst.ToString("N0", _spanishCI), false, false));
                    row.Add(MakeCell(x.totalVal.ToString("N0", _spanishCI), false, false));
                    break;
            }

            if (x.playerId > 0)
            {
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    PlayClick();
                    ScreenManager.SelectedPlayerId = x.playerId;
                    ScreenManager.Instance.GoTo(GameScreen.PlayerProfile);
                });
                CursorManager.Instance?.RegisterHandCursor(row);
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
}
