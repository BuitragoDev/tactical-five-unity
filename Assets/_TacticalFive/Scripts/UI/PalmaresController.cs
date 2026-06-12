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

    private VisualElement _titlesRankingBody;
    private VisualElement _finalsHistoryBody;
    private VisualElement _mvpRankingBody;
    private VisualElement _awardsHistoryBody;
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
        SetupScrollViews();
        LoadSidebarIcons();
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void SetupScrollViews()
    {
        var scrolls = new[] { "TitlesRankingScroll", "FinalsHistoryScroll", "MVPRankingScroll", "AwardsHistoryScroll", "QuintetAppearancesScroll", "QuintetHistoryScroll" };
        foreach (var name in scrolls)
        {
            var sv = _root.Q<ScrollView>(name);
            if (sv != null)
                sv.contentContainer.style.flexGrow = 0;
        }
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
        {
            PlayClick();
            var btn = _root.Q<Button>("NavRoster");
            if (btn != null)
                btn.EnableInClassList("nav-item--expanded", !btn.ClassListContains("nav-item--expanded"));
        });
        _root.Q<Button>("SubmenuJugadores")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<Button>("NavRoster")?.RemoveFromClassList("nav-item--expanded");
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        _root.Q<Button>("SubmenuEmpleados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); });
        _root.Q<Button>("SubmenuLesionados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); });
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

    // ══ DATA ══════════════════════════════════════════════

    TeamData FindTeam(string keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return null;
        return _allTeams?.Find(t =>
            t.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(t.logo, keyword, System.StringComparison.OrdinalIgnoreCase));
    }

    // ══ EQUIPOS TAB ══════════════════════════════════════

    void BuildEquiposTab()
    {
        var finalsData = DatabaseManager.Instance.GetFinalsRecords();
        finalsData.Reverse();
        BuildTitlesRanking(finalsData);
        BuildFinalsHistory(finalsData);
    }

    void BuildTitlesRanking(List<FinalsRecord> finalsData)
    {
        _titlesRankingBody.Clear();

        var champCounts = new Dictionary<string, (int count, TeamData team)>();

        foreach (var f in finalsData)
        {
            var team = FindTeam(f.champ_keyword);
            string key = team?.name ?? f.champ_name;
            if (!champCounts.ContainsKey(key))
                champCounts[key] = (0, team);
            var cur = champCounts[key];
            champCounts[key] = (cur.count + 1, cur.team);
        }

        var sorted = champCounts.OrderByDescending(kv => kv.Value.count).ToList();

        if (sorted.Count == 0)
        {
            var emptyLbl = new Label { text = "Aún no hay campeonatos registrados" };
            emptyLbl.AddToClassList("no-data-cell");
            _titlesRankingBody.Add(emptyLbl);
            return;
        }

        for (int i = 0; i < sorted.Count; i++)
        {
            var kv = sorted[i];
            var row = new VisualElement();
            row.AddToClassList("champ-row");

            var rankLbl = new Label { text = (i + 1).ToString() };
            rankLbl.AddToClassList("champ-rank");
            row.Add(rankLbl);

            var logo = new VisualElement();
            logo.AddToClassList("champ-logo");
            if (kv.Value.team != null && _logoSprites32.TryGetValue(kv.Value.team.logo, out var sp))
                logo.style.backgroundImage = new StyleBackground(sp);
            row.Add(logo);

            var nameLbl = new Label { text = kv.Key };
            nameLbl.AddToClassList("champ-name");
            row.Add(nameLbl);

            var countLbl = new Label { text = kv.Value.count.ToString() };
            countLbl.AddToClassList("champ-count");
            row.Add(countLbl);

            _titlesRankingBody.Add(row);
        }
    }

    void BuildFinalsHistory(List<FinalsRecord> finalsData)
    {
        _finalsHistoryBody.Clear();

        foreach (var f in finalsData)
        {
            var row = new VisualElement();
            row.AddToClassList("palmares-data-row");

            var seasonLbl = new Label { text = f.season };
            seasonLbl.AddToClassList("td-season");
            row.Add(seasonLbl);

            row.Add(CreateCellWithLogo(FindTeam(f.champ_keyword), "td-champ", f.champ_name));
            row.Add(CreateCellWithLogo(FindTeam(f.finalist_keyword), "td-finalist", f.finalist_name));

            var resultLbl = new Label { text = f.result };
            resultLbl.AddToClassList("td-result");
            row.Add(resultLbl);

            var mvpLbl = new Label { text = f.mvp };
            mvpLbl.AddToClassList("td-mvp");
            row.Add(mvpLbl);

            _finalsHistoryBody.Add(row);
        }
    }

    // ══ JUGADORES TAB ════════════════════════════════════

    void BuildJugadoresTab()
    {
        var awardsData = DatabaseManager.Instance.GetAwardsRecords();
        awardsData.Reverse();
        BuildMVPRanking(awardsData);
        BuildAwardsHistory(awardsData);
    }

    void BuildMVPRanking(List<AwardsRecord> awardsData)
    {
        _mvpRankingBody.Clear();

        var mvpCounts = new Dictionary<string, (int count, string teamKeyword)>();

        foreach (var a in awardsData)
        {
            if (!mvpCounts.ContainsKey(a.mvp))
                mvpCounts[a.mvp] = (0, a.mvp_team_keyword);
            var cur = mvpCounts[a.mvp];
            mvpCounts[a.mvp] = (cur.count + 1, cur.teamKeyword);
        }

        var sorted = mvpCounts.OrderByDescending(kv => kv.Value.count).ToList();

        if (sorted.Count == 0)
        {
            var emptyLbl = new Label { text = "Aún no hay MVPs registrados" };
            emptyLbl.AddToClassList("no-data-cell");
            _mvpRankingBody.Add(emptyLbl);
            return;
        }

        for (int i = 0; i < sorted.Count; i++)
        {
            var kv = sorted[i];
            var row = new VisualElement();
            row.AddToClassList("champ-row");

            var rankLbl = new Label { text = (i + 1).ToString() };
            rankLbl.AddToClassList("champ-rank");
            row.Add(rankLbl);

            var team = string.IsNullOrEmpty(kv.Value.teamKeyword) ? null : FindTeam(kv.Value.teamKeyword);
            var logo = new VisualElement();
            logo.AddToClassList("champ-logo");
            if (team != null && _logoSprites32.TryGetValue(team.logo, out var sp))
                logo.style.backgroundImage = new StyleBackground(sp);
            row.Add(logo);

            var nameLbl = new Label { text = kv.Key };
            nameLbl.AddToClassList("champ-name");
            row.Add(nameLbl);

            var countLbl = new Label { text = kv.Value.count.ToString() };
            countLbl.AddToClassList("champ-count");
            row.Add(countLbl);

            _mvpRankingBody.Add(row);
        }
    }

    void BuildAwardsHistory(List<AwardsRecord> awardsData)
    {
        _awardsHistoryBody.Clear();

        foreach (var a in awardsData)
        {
            var row = new VisualElement();
            row.AddToClassList("palmares-data-row");

            var seasonLbl = new Label { text = a.season };
            seasonLbl.AddToClassList("td-season");
            row.Add(seasonLbl);

            row.Add(CreatePlayerCell(a.mvp, string.IsNullOrEmpty(a.mvp_team_keyword) ? null : FindTeam(a.mvp_team_keyword), "td-mvp"));

            var mvpRatingLbl = new Label { text = a.mvp_rating };
            mvpRatingLbl.AddToClassList("td-rating");
            row.Add(mvpRatingLbl);

            row.Add(CreatePlayerCell(a.rookie, string.IsNullOrEmpty(a.rookie_team_keyword) ? null : FindTeam(a.rookie_team_keyword), "td-rookie"));

            var rookieRatingLbl = new Label { text = a.rookie_rating };
            rookieRatingLbl.AddToClassList("td-rating");
            row.Add(rookieRatingLbl);

            _awardsHistoryBody.Add(row);
        }
    }

    // ══ QUINTETOS TAB ════════════════════════════════════

    void BuildQuintetosTab()
    {
        var quintetData = DatabaseManager.Instance.GetQuintetRecords();
        quintetData.Reverse();
        BuildQuintetAppearances(quintetData);
        BuildQuintetHistory(quintetData);
    }

    void BuildQuintetAppearances(List<QuintetRecord> quintetData)
    {
        _quintetAppearancesBody.Clear();

        var appearanceCounts = new Dictionary<string, (int count, string teamKeyword)>();

        foreach (var q in quintetData)
        {
            var players = new[] {
                (q.pg, q.pg_team), (q.sg, q.sg_team),
                (q.sf, q.sf_team), (q.pf, q.pf_team), (q.c, q.c_team)
            };
            foreach (var (name, team) in players)
            {
                if (!appearanceCounts.ContainsKey(name))
                    appearanceCounts[name] = (0, team);
                var cur = appearanceCounts[name];
                appearanceCounts[name] = (cur.count + 1, cur.teamKeyword);
            }
        }

        var sorted = appearanceCounts.OrderByDescending(kv => kv.Value.count).ThenBy(kv => kv.Key).ToList();

        if (sorted.Count == 0)
        {
            var emptyLbl = new Label { text = "Aún no hay quintetos registrados" };
            emptyLbl.AddToClassList("no-data-cell");
            _quintetAppearancesBody.Add(emptyLbl);
            return;
        }

        for (int i = 0; i < sorted.Count; i++)
        {
            var kv = sorted[i];
            var row = new VisualElement();
            row.AddToClassList("champ-row");

            var rankLbl = new Label { text = (i + 1).ToString() };
            rankLbl.AddToClassList("champ-rank");
            row.Add(rankLbl);

            var team = string.IsNullOrEmpty(kv.Value.teamKeyword) ? null : FindTeam(kv.Value.teamKeyword);
            var logo = new VisualElement();
            logo.AddToClassList("champ-logo");
            if (team != null && _logoSprites32.TryGetValue(team.logo, out var sp))
                logo.style.backgroundImage = new StyleBackground(sp);
            row.Add(logo);

            var nameLbl = new Label { text = kv.Key };
            nameLbl.AddToClassList("champ-name");
            row.Add(nameLbl);

            var countLbl = new Label { text = kv.Value.count.ToString() };
            countLbl.AddToClassList("champ-count");
            row.Add(countLbl);

            _quintetAppearancesBody.Add(row);
        }
    }

    void BuildQuintetHistory(List<QuintetRecord> quintetData)
    {
        _quintetHistoryBody.Clear();

        foreach (var q in quintetData)
        {
            var row = new VisualElement();
            row.AddToClassList("palmares-data-row");

            var seasonLbl = new Label { text = q.season };
            seasonLbl.AddToClassList("td-season");
            row.Add(seasonLbl);

            row.Add(CreatePlayerCell(q.pg, string.IsNullOrEmpty(q.pg_team) ? null : FindTeam(q.pg_team), "td-quintet-pos"));
            row.Add(CreatePlayerCell(q.sg, string.IsNullOrEmpty(q.sg_team) ? null : FindTeam(q.sg_team), "td-quintet-pos"));
            row.Add(CreatePlayerCell(q.sf, string.IsNullOrEmpty(q.sf_team) ? null : FindTeam(q.sf_team), "td-quintet-pos"));
            row.Add(CreatePlayerCell(q.pf, string.IsNullOrEmpty(q.pf_team) ? null : FindTeam(q.pf_team), "td-quintet-pos"));
            row.Add(CreatePlayerCell(q.c, string.IsNullOrEmpty(q.c_team) ? null : FindTeam(q.c_team), "td-quintet-pos"));

            _quintetHistoryBody.Add(row);
        }
    }

    // ══ HELPERS ═══════════════════════════════════════════

    VisualElement CreateCellWithLogo(TeamData team, string cellClass, string fallbackName)
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
        nameLbl.text = fallbackName;
        cell.Add(nameLbl);

        return cell;
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

    void LoadSidebarIcons()
    {
        var iconMap = new Dictionary<string, string>
        {
            {"NavDashboardIcon", "inicio"}, {"NavRosterIcon", "plantilla"},
            {"NavCalendarIcon", "calendario"}, {"NavStandingsIcon", "clasificacion"},
            {"NavPalmaresIcon", "palmares"}, {"NavResultsIcon", "resultados"},
            {"NavPlayoffsIcon", "playoff"}, {"NavStatsIcon", "estadisticas"},
            {"NavRecordsIcon", "records"}, {"NavMarketIcon", "mercado"},
            {"NavFinancesIcon", "finanzas"}, {"NavSponsorsIcon", "patrocinador"},
            {"NavTVIcon", "television"}, {"NavArenaIcon", "pabellon"},
            {"NavMessagesIcon", "mensajes"}, {"NavConfigIcon", "configuracion"}
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
