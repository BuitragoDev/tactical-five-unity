using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class PalmaresController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private Button _tabEquipos;
    private Button _tabJugadores;
    private Button _tabQuintetos;

    private VisualElement _tabContentEquipos;
    private VisualElement _tabContentJugadores;
    private VisualElement _tabContentQuintetos;

    // Equipos
    private VisualElement _titlesRankingBody;
    private VisualElement _finalsHistoryBody;

    // Jugadores
    private VisualElement _mvpRankingBody;
    private VisualElement _awardsHistoryBody;

    // Quintetos
    private VisualElement _quintetAppearancesBody;
    private VisualElement _quintetHistoryBody;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;
    private List<SeasonRecord> _seasonRecords;

    private Dictionary<string, Sprite> _logoSprites32 = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();

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

        _tabEquipos = _root.Q<Button>("TabEquipos");
        _tabJugadores = _root.Q<Button>("TabJugadores");
        _tabQuintetos = _root.Q<Button>("TabQuintetos");

        _tabContentEquipos = _root.Q<VisualElement>("TabContentEquipos");
        _tabContentJugadores = _root.Q<VisualElement>("TabContentJugadores");
        _tabContentQuintetos = _root.Q<VisualElement>("TabContentQuintetos");

        _titlesRankingBody = _root.Q<VisualElement>("TitlesRankingBody");
        _finalsHistoryBody = _root.Q<VisualElement>("FinalsHistoryBody");

        _mvpRankingBody = _root.Q<VisualElement>("MVPRankingBody");
        _awardsHistoryBody = _root.Q<VisualElement>("AwardsHistoryBody");

        _quintetAppearancesBody = _root.Q<VisualElement>("QuintetAppearancesBody");
        _quintetHistoryBody = _root.Q<VisualElement>("QuintetHistoryBody");
    }

    void LoadData()
    {
        var logos32 = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _seasonRecords = DatabaseManager.Instance.GetAllSeasonRecords(_season?.id ?? 0);
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();

        _tabEquipos?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("equipos"); });
        _tabJugadores?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("jugadores"); });
        _tabQuintetos?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("quintetos"); });

        _btnAction?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
    }

    void RegisterNavButtons()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Roster); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Records); });
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Market); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Finances); });
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<Button>("NavConfig")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });
    }

    void Refresh()
    {
        RefreshHeader();
        ShowTab("equipos");
    }

    void ShowTab(string tab)
    {
        _tabEquipos.RemoveFromClassList("palmares-tab--active");
        _tabJugadores.RemoveFromClassList("palmares-tab--active");
        _tabQuintetos.RemoveFromClassList("palmares-tab--active");

        _tabContentEquipos.style.display = DisplayStyle.None;
        _tabContentJugadores.style.display = DisplayStyle.None;
        _tabContentQuintetos.style.display = DisplayStyle.None;

        switch (tab)
        {
            case "equipos":
                _tabEquipos.AddToClassList("palmares-tab--active");
                _tabContentEquipos.style.display = DisplayStyle.Flex;
                BuildEquiposTab();
                break;
            case "jugadores":
                _tabJugadores.AddToClassList("palmares-tab--active");
                _tabContentJugadores.style.display = DisplayStyle.Flex;
                BuildJugadoresTab();
                break;
            case "quintetos":
                _tabQuintetos.AddToClassList("palmares-tab--active");
                _tabContentQuintetos.style.display = DisplayStyle.Flex;
                BuildQuintetosTab();
                break;
        }
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites64.TryGetValue(_myTeam.logo, out var sprite))
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
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "DASHBOARD";
    }

    // ══ HELPERS ═══════════════════════════════════════════

    string GetSeasonString(int seasonId)
    {
        return $"{2025}-{2026}";
    }

    // ══ EQUIPOS TAB ══════════════════════════════════════

    void BuildEquiposTab()
    {
        BuildTitlesRanking();
        BuildFinalsHistory();
    }

    void BuildTitlesRanking()
    {
        _titlesRankingBody.Clear();

        // Test data: simulate team championship counts
        var testData = new List<(TeamData team, int count)>();
        var bulls = _allTeams?.Find(t => t.name.Contains("Bulls"));
        var lakers = _allTeams?.Find(t => t.name.Contains("Lakers"));
        var celtics = _allTeams?.Find(t => t.name.Contains("Celtics"));

        if (bulls != null) testData.Add((bulls, 6));
        if (lakers != null) testData.Add((lakers, 17));
        if (celtics != null) testData.Add((celtics, 18));

        // Sort by count descending
        testData = testData.OrderByDescending(t => t.count).ToList();

        if (testData.Count == 0)
        {
            var noData = new Label();
            noData.AddToClassList("no-data-cell");
            noData.text = "Aún no hay campeonatos registrados";
            _titlesRankingBody.Add(noData);
            return;
        }

        for (int i = 0; i < testData.Count; i++)
        {
            var (team, count) = testData[i];
            _titlesRankingBody.Add(CreateChampRow(i + 1, team, count));
        }
    }

    VisualElement CreateChampRow(int rank, TeamData team, int count)
    {
        var row = new VisualElement();
        row.AddToClassList("champ-row");

        var rankLbl = new Label();
        rankLbl.AddToClassList("champ-rank");
        rankLbl.text = rank.ToString();

        var logo = new VisualElement();
        logo.AddToClassList("champ-logo");
        if (team != null && _logoSprites32.TryGetValue(team.logo, out var sp))
            logo.style.backgroundImage = new StyleBackground(sp);

        var nameLbl = new Label();
        nameLbl.AddToClassList("champ-name");
        nameLbl.text = team?.name ?? "???";

        var countLbl = new Label();
        countLbl.AddToClassList("champ-count");
        countLbl.text = count.ToString();

        row.Add(rankLbl);
        row.Add(logo);
        row.Add(nameLbl);
        row.Add(countLbl);

        return row;
    }

    void BuildFinalsHistory()
    {
        _finalsHistoryBody.Clear();

        // Test data
        var bulls = _allTeams?.Find(t => t.name.Contains("Bulls"));
        var lakers = _allTeams?.Find(t => t.name.Contains("Lakers"));
        var celtics = _allTeams?.Find(t => t.name.Contains("Celtics"));

        AddFinalsRow("2024-2025", bulls, lakers, "4-2", "Michael Jordan");
        AddFinalsRow("2023-2024", lakers, celtics, "4-3", "LeBron James");
    }

    void AddFinalsRow(string season, TeamData champ, TeamData finalist, string result, string mvpName)
    {
        var row = new VisualElement();
        row.AddToClassList("palmares-data-row");

        // Season
        var seasonLbl = new Label();
        seasonLbl.AddToClassList("td-season");
        seasonLbl.text = season;
        row.Add(seasonLbl);

        // Champion (cell-with-logo)
        row.Add(CreateCellWithLogo(champ, "td-champ"));

        // Finalist (cell-with-logo)
        row.Add(CreateCellWithLogo(finalist, "td-finalist"));

        // Result
        var resultLbl = new Label();
        resultLbl.AddToClassList("td-result");
        resultLbl.text = result;
        row.Add(resultLbl);

        // MVP
        var mvpLbl = new Label();
        mvpLbl.AddToClassList("td-mvp");
        mvpLbl.text = mvpName;
        row.Add(mvpLbl);

        _finalsHistoryBody.Add(row);
    }

    VisualElement CreateCellWithLogo(TeamData team, string cellClass)
    {
        var cell = new VisualElement();
        cell.AddToClassList("cell-with-logo");

        var logo = new VisualElement();
        logo.AddToClassList("mini-logo");
        if (team != null && _logoSprites32.TryGetValue(team.logo, out var sp))
            logo.style.backgroundImage = new StyleBackground(sp);
        cell.Add(logo);

        var nameLbl = new Label();
        nameLbl.AddToClassList(cellClass);
        nameLbl.text = team?.name ?? "-";
        cell.Add(nameLbl);

        return cell;
    }

    // ══ JUGADORES TAB ════════════════════════════════════

    void BuildJugadoresTab()
    {
        BuildMVPRanking();
        BuildAwardsHistory();
    }

    void BuildMVPRanking()
    {
        _mvpRankingBody.Clear();

        // Test data
        var testMvps = new List<(string name, TeamData team, int count)>();
        var bulls = _allTeams?.Find(t => t.name.Contains("Bulls"));
        var lakers = _allTeams?.Find(t => t.name.Contains("Lakers"));

        testMvps.Add(("Michael Jordan", bulls, 5));
        testMvps.Add(("LeBron James", lakers, 4));

        testMvps = testMvps.OrderByDescending(m => m.count).ToList();

        for (int i = 0; i < testMvps.Count; i++)
        {
            var (name, team, count) = testMvps[i];
            var row = new VisualElement();
            row.AddToClassList("champ-row");

            var rankLbl = new Label();
            rankLbl.AddToClassList("champ-rank");
            rankLbl.text = (i + 1).ToString();
            row.Add(rankLbl);

            var logo = new VisualElement();
            logo.AddToClassList("champ-logo");
            if (team != null && _logoSprites32.TryGetValue(team.logo, out var sp))
                logo.style.backgroundImage = new StyleBackground(sp);
            row.Add(logo);

            var nameLbl = new Label();
            nameLbl.AddToClassList("champ-name");
            nameLbl.text = name;
            row.Add(nameLbl);

            var countLbl = new Label();
            countLbl.AddToClassList("champ-count");
            countLbl.text = count.ToString();
            row.Add(countLbl);

            _mvpRankingBody.Add(row);
        }

        if (testMvps.Count == 0)
        {
            var noData = new Label();
            noData.AddToClassList("no-data-cell");
            noData.text = "Aún no hay MVPs registrados";
            _mvpRankingBody.Add(noData);
        }
    }

    void BuildAwardsHistory()
    {
        _awardsHistoryBody.Clear();

        var bulls = _allTeams?.Find(t => t.name.Contains("Bulls"));
        var lakers = _allTeams?.Find(t => t.name.Contains("Lakers"));

        AddAwardsRow("2024-2025", "Michael Jordan", bulls, "29.5", "Magic Johnson", lakers, "12.3");
        AddAwardsRow("2023-2024", "LeBron James", lakers, "27.8", "Larry Bird", null, "11.2");
    }

    void AddAwardsRow(string season, string mvpName, TeamData mvpTeam, string mvpRating,
                      string rookieName, TeamData rookieTeam, string rookieRating)
    {
        var row = new VisualElement();
        row.AddToClassList("palmares-data-row");

        var seasonLbl = new Label();
        seasonLbl.AddToClassList("td-season");
        seasonLbl.text = season;
        row.Add(seasonLbl);

        // MVP cell
        row.Add(CreatePlayerCell(mvpName, mvpTeam, "td-mvp"));

        // MVP rating
        var mvpRatingLbl = new Label();
        mvpRatingLbl.AddToClassList("td-rating");
        mvpRatingLbl.text = mvpRating;
        row.Add(mvpRatingLbl);

        // Rookie cell
        row.Add(CreatePlayerCell(rookieName, rookieTeam, "td-rookie"));

        // Rookie rating
        var rookieRatingLbl = new Label();
        rookieRatingLbl.AddToClassList("td-rating");
        rookieRatingLbl.text = rookieRating;
        row.Add(rookieRatingLbl);

        _awardsHistoryBody.Add(row);
    }

    VisualElement CreatePlayerCell(string playerName, TeamData team, string cellClass)
    {
        var cell = new VisualElement();
        cell.AddToClassList("cell-with-logo");

        var logo = new VisualElement();
        logo.AddToClassList("mini-logo");
        if (team != null && _logoSprites32.TryGetValue(team.logo, out var sp))
            logo.style.backgroundImage = new StyleBackground(sp);
        cell.Add(logo);

        var nameLbl = new Label();
        nameLbl.AddToClassList(cellClass);
        nameLbl.text = playerName;
        cell.Add(nameLbl);

        return cell;
    }

    // ══ QUINTETOS TAB ════════════════════════════════════

    void BuildQuintetosTab()
    {
        BuildQuintetAppearances();
        BuildQuintetHistory();
    }

    void BuildQuintetAppearances()
    {
        _quintetAppearancesBody.Clear();

        var testAppearances = new List<(string name, TeamData team, int count)>();
        var bulls = _allTeams?.Find(t => t.name.Contains("Bulls"));
        var lakers = _allTeams?.Find(t => t.name.Contains("Lakers"));

        testAppearances.Add(("Michael Jordan", bulls, 11));
        testAppearances.Add(("LeBron James", lakers, 13));

        testAppearances = testAppearances.OrderByDescending(a => a.count).ToList();

        for (int i = 0; i < testAppearances.Count; i++)
        {
            var (name, team, count) = testAppearances[i];
            var row = new VisualElement();
            row.AddToClassList("champ-row");

            var rankLbl = new Label();
            rankLbl.AddToClassList("champ-rank");
            rankLbl.text = (i + 1).ToString();
            row.Add(rankLbl);

            var logo = new VisualElement();
            logo.AddToClassList("champ-logo");
            if (team != null && _logoSprites32.TryGetValue(team.logo, out var sp))
                logo.style.backgroundImage = new StyleBackground(sp);
            row.Add(logo);

            var nameLbl = new Label();
            nameLbl.AddToClassList("champ-name");
            nameLbl.text = name;
            row.Add(nameLbl);

            var countLbl = new Label();
            countLbl.AddToClassList("champ-count");
            countLbl.text = count.ToString();
            row.Add(countLbl);

            _quintetAppearancesBody.Add(row);
        }

        if (testAppearances.Count == 0)
        {
            var noData = new Label();
            noData.AddToClassList("no-data-cell");
            noData.text = "Aún no hay quintetos registrados";
            _quintetAppearancesBody.Add(noData);
        }
    }

    void BuildQuintetHistory()
    {
        _quintetHistoryBody.Clear();

        var bulls = _allTeams?.Find(t => t.name.Contains("Bulls"));
        var lakers = _allTeams?.Find(t => t.name.Contains("Lakers"));
        var celtics = _allTeams?.Find(t => t.name.Contains("Celtics"));

        AddQuintetRow("2024-2025",
            ("Michael Jordan", bulls), ("Magic Johnson", lakers),
            ("Larry Bird", celtics), ("Tim Duncan", null), ("Shaquille O'Neal", lakers));
        AddQuintetRow("2023-2024",
            ("LeBron James", lakers), ("Kobe Bryant", lakers),
            ("Kevin Durant", null), ("Karl Malone", null), ("Hakeem Olajuwon", null));
    }

    void AddQuintetRow(string season,
        (string name, TeamData team) pg, (string name, TeamData team) sg,
        (string name, TeamData team) sf, (string name, TeamData team) pf,
        (string name, TeamData team) c)
    {
        var row = new VisualElement();
        row.AddToClassList("palmares-data-row");

        var seasonLbl = new Label();
        seasonLbl.AddToClassList("td-season");
        seasonLbl.text = season;
        row.Add(seasonLbl);

        row.Add(CreatePlayerCell(pg.name, pg.team, "td-quintet-pos"));
        row.Add(CreatePlayerCell(sg.name, sg.team, "td-quintet-pos"));
        row.Add(CreatePlayerCell(sf.name, sf.team, "td-quintet-pos"));
        row.Add(CreatePlayerCell(pf.name, pf.team, "td-quintet-pos"));
        row.Add(CreatePlayerCell(c.name, c.team, "td-quintet-pos"));

        _quintetHistoryBody.Add(row);
    }

    // ══ SIDEBAR / MISC ═══════════════════════════════════

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
            {"NavConfigIcon", "configuracion"}
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
