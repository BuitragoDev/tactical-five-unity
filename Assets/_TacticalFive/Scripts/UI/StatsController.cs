using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class StatsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private VisualElement _statsBody;
    private Label _panelTitle;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;

    private Dictionary<string, Sprite> _logoSprites = new();
    private string _currentStat = "puntos";
    private string _currentMode = "season";
    private string _currentDisplay = "totals";

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

        CacheReferences();
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _statsBody = _root.Q<VisualElement>("StatsBody");
        _panelTitle = _root.Q<Label>("PanelTitle");

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
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
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
        _btnAction?.RegisterCallback<ClickEvent>(_ => ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("BtnReset")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.MainMenu));

        string[] statKeys = { "puntos", "rebotes", "asistencias", "robos", "tapones",
                              "pcttc", "pct3p", "pcttl", "val", "perdidas",
                              "minutos", "dd", "td" };
        for (int i = 0; i < _statTabs.Count && i < statKeys.Length; i++)
        {
            int idx = i;
            _statTabs[i].RegisterCallback<ClickEvent>(_ => ShowStats(statKeys[idx]));
        }

        var btnSeason = _root.Q<Button>("BtnSeason");
        var btnHistorical = _root.Q<Button>("BtnHistorical");
        _filterBtns.Add(btnSeason);
        _filterBtns.Add(btnHistorical);
        btnSeason?.RegisterCallback<ClickEvent>(_ => SetMode("season"));
        btnHistorical?.RegisterCallback<ClickEvent>(_ => SetMode("historical"));

        var btnTotals = _root.Q<Button>("BtnTotals");
        var btnAverages = _root.Q<Button>("BtnAverages");
        _modeBtns.Add(btnTotals);
        _modeBtns.Add(btnAverages);
        btnTotals?.RegisterCallback<ClickEvent>(_ => SetDisplay("totals"));
        btnAverages?.RegisterCallback<ClickEvent>(_ => SetDisplay("averages"));
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
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Results));
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Playoffs));
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
        ShowStats(_currentStat);
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

    void SetMode(string mode)
    {
        _currentMode = mode;
        foreach (var btn in _filterBtns)
        {
            btn.RemoveFromClassList("filter-btn--active");
        }
        if (mode == "season")
            _root.Q<Button>("BtnSeason")?.AddToClassList("filter-btn--active");
        else
            _root.Q<Button>("BtnHistorical")?.AddToClassList("filter-btn--active");
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

    void ShowStats(string stat)
    {
        _currentStat = stat;

        foreach (var btn in _statTabs)
            btn.RemoveFromClassList("stats-tab--active");

        var statTabName = stat switch
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

        var allPlayers = _allTeams.SelectMany(t => DatabaseManager.Instance.GetPlayersByTeam(t.id)).ToList();
        var allStats = new List<PlayerGameStats>();

        if (_currentMode == "season" && _season != null)
        {
            var games = DatabaseManager.Instance.GetSeasonGames(_manager.id, _season.id)
                .Where(g => g.game_type == "regular").ToList();
            var gameIds = games.Select(g => g.id).ToHashSet();
            foreach (var player in allPlayers)
            {
                var playerStats = DatabaseManager.Instance.GetPlayerGameStats(player.id)
                    .Where(s => gameIds.Contains(s.game_id))
                    .ToList();
                allStats.AddRange(playerStats);
            }
        }
        else
        {
            foreach (var player in allPlayers)
            {
                allStats.AddRange(DatabaseManager.Instance.GetPlayerGameStats(player.id));
            }
        }

        var playerAggs = allStats
            .GroupBy(s => s.player_id)
            .Select(g =>
            {
                var first = g.First();
                int gp = g.Count();
                int totalPts = g.Sum(s => s.points);
                int totalReb = g.Sum(s => s.rebounds);
                int totalAst = g.Sum(s => s.assists);
                int totalStl = g.Sum(s => s.steals);
                int totalBlk = g.Sum(s => s.blocks);
                int totalFgm = g.Sum(s => s.fgm);
                int totalFga = g.Sum(s => s.fga);
                int totalFg3m = g.Sum(s => s.fg3m);
                int totalFg3a = g.Sum(s => s.fg3a);
                int totalFtm = g.Sum(s => s.ftm);
                int totalFta = g.Sum(s => s.fta);
                int totalTov = g.Sum(s => s.turnovers);
                float totalMin = g.Sum(s => s.minutes);
                int totalVal = g.Sum(s => s.rating);
                int totalDd = g.Sum(s => s.double_double);
                int totalTd = g.Sum(s => s.triple_double);

                float avgPts = gp > 0 ? (float)totalPts / gp : 0;
                float avgReb = gp > 0 ? (float)totalReb / gp : 0;
                float avgAst = gp > 0 ? (float)totalAst / gp : 0;
                float avgStl = gp > 0 ? (float)totalStl / gp : 0;
                float avgBlk = gp > 0 ? (float)totalBlk / gp : 0;
                float avgMin = gp > 0 ? totalMin / gp : 0;
                float avgVal = gp > 0 ? (float)totalVal / gp : 0;
                float avgTov = gp > 0 ? (float)totalTov / gp : 0;
                float fgPct = totalFga > 0 ? (float)totalFgm / totalFga * 100f : 0;
                float fg3Pct = totalFg3a > 0 ? (float)totalFg3m / totalFg3a * 100f : 0;
                float ftPct = totalFta > 0 ? (float)totalFtm / totalFta * 100f : 0;

                return new
                {
                    player_id = g.Key,
                    gp,
                    totalPts,
                    totalReb,
                    totalAst,
                    totalStl,
                    totalBlk,
                    totalFgm,
                    totalFga,
                    totalFg3m,
                    totalFg3a,
                    totalFtm,
                    totalFta,
                    totalTov,
                    totalMin,
                    totalVal,
                    totalDd,
                    totalTd,
                    avgPts,
                    avgReb,
                    avgAst,
                    avgStl,
                    avgBlk,
                    fgPct,
                    fg3Pct,
                    ftPct,
                    avgMin,
                    avgVal,
                    avgTov
                };
            })
            .ToList();

        bool useAverages = _currentDisplay == "averages";

        var sorted = stat switch
        {
            "puntos" => useAverages
                ? playerAggs.OrderByDescending(x => x.avgPts).ToList()
                : playerAggs.OrderByDescending(x => x.totalPts).ToList(),
            "rebotes" => useAverages
                ? playerAggs.OrderByDescending(x => x.avgReb).ToList()
                : playerAggs.OrderByDescending(x => x.totalReb).ToList(),
            "asistencias" => useAverages
                ? playerAggs.OrderByDescending(x => x.avgAst).ToList()
                : playerAggs.OrderByDescending(x => x.totalAst).ToList(),
            "robos" => useAverages
                ? playerAggs.OrderByDescending(x => x.avgStl).ToList()
                : playerAggs.OrderByDescending(x => x.totalStl).ToList(),
            "tapones" => useAverages
                ? playerAggs.OrderByDescending(x => x.avgBlk).ToList()
                : playerAggs.OrderByDescending(x => x.totalBlk).ToList(),
            "pcttc" => playerAggs.Where(x => x.totalFga >= 10).OrderByDescending(x => x.fgPct).ToList(),
            "pct3p" => playerAggs.Where(x => x.totalFg3a >= 5).OrderByDescending(x => x.fg3Pct).ToList(),
            "pcttl" => playerAggs.Where(x => x.totalFta >= 5).OrderByDescending(x => x.ftPct).ToList(),
            "val" => useAverages
                ? playerAggs.OrderByDescending(x => x.avgVal).ToList()
                : playerAggs.OrderByDescending(x => x.totalVal).ToList(),
            "perdidas" => useAverages
                ? playerAggs.OrderByDescending(x => x.avgTov).ToList()
                : playerAggs.OrderByDescending(x => x.totalTov).ToList(),
            "minutos" => useAverages
                ? playerAggs.OrderByDescending(x => x.avgMin).ToList()
                : playerAggs.OrderByDescending(x => x.totalMin).ToList(),
            "dd" => playerAggs.OrderByDescending(x => x.totalDd).ToList(),
            "td" => playerAggs.OrderByDescending(x => x.totalTd).ToList(),
            _ => playerAggs.OrderByDescending(x => x.totalPts).ToList()
        };

        var top = sorted.Take(100).ToList();

        for (int i = 0; i < top.Count; i++)
        {
            var x = top[i];
            var player = allPlayers.FirstOrDefault(p => p.id == x.player_id);
            if (player == null) continue;

            var team = _allTeams.Find(t => t.id == player.team_id);

            var row = new VisualElement();
            row.AddToClassList("stats-row");
            if (team != null && team.id == _myTeam.id)
                row.AddToClassList("stats-row--my-team");

            var rankLbl = new Label();
            rankLbl.AddToClassList("col-rank");
            rankLbl.text = (i + 1).ToString();

            var nameLbl = new Label();
            nameLbl.AddToClassList("col-player-name");
            nameLbl.text = $"{player.first_name} {player.last_name}";

            var abbrevLbl = new Label();
            abbrevLbl.AddToClassList("col-team-abbrev");
            abbrevLbl.text = team?.abbreviation ?? "FA";

            var posLbl = new Label();
            posLbl.AddToClassList("col-pos");
            posLbl.text = player.position;

            var gpLbl = new Label();
            gpLbl.AddToClassList("col-stat");
            gpLbl.text = x.gp.ToString();

            var ptsLbl = new Label();
            ptsLbl.AddToClassList("col-stat");
            ptsLbl.text = useAverages ? x.avgPts.ToString("F1") : x.totalPts.ToString();
            if (i == 0 && stat == "puntos") ptsLbl.AddToClassList("col-stat--leader");

            var rebLbl = new Label();
            rebLbl.AddToClassList("col-stat");
            rebLbl.text = useAverages ? x.avgReb.ToString("F1") : x.totalReb.ToString();
            if (i == 0 && stat == "rebotes") rebLbl.AddToClassList("col-stat--leader");

            var astLbl = new Label();
            astLbl.AddToClassList("col-stat");
            astLbl.text = useAverages ? x.avgAst.ToString("F1") : x.totalAst.ToString();
            if (i == 0 && stat == "asistencias") astLbl.AddToClassList("col-stat--leader");

            var fgLbl = new Label();
            fgLbl.AddToClassList("col-stat");
            fgLbl.text = x.fgPct.ToString("F1");
            if (i == 0 && stat == "pcttc") fgLbl.AddToClassList("col-stat--leader");

            var fg3Lbl = new Label();
            fg3Lbl.AddToClassList("col-stat");
            fg3Lbl.text = x.fg3Pct.ToString("F1");
            if (i == 0 && stat == "pct3p") fg3Lbl.AddToClassList("col-stat--leader");

            var ftLbl = new Label();
            ftLbl.AddToClassList("col-stat");
            ftLbl.text = x.ftPct.ToString("F1");
            if (i == 0 && stat == "pcttl") ftLbl.AddToClassList("col-stat--leader");

            var valLbl = new Label();
            valLbl.AddToClassList("col-stat");
            valLbl.text = useAverages ? x.avgVal.ToString("F1") : x.totalVal.ToString();
            if (i == 0 && stat == "val") valLbl.AddToClassList("col-stat--leader");

            row.Add(rankLbl);
            row.Add(nameLbl);
            row.Add(abbrevLbl);
            row.Add(posLbl);
            row.Add(gpLbl);
            row.Add(ptsLbl);
            row.Add(rebLbl);
            row.Add(astLbl);
            row.Add(fgLbl);
            row.Add(fg3Lbl);
            row.Add(ftLbl);
            row.Add(valLbl);

            var spacer = new VisualElement();
            spacer.AddToClassList("col-spacer");
            row.Add(spacer);

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
