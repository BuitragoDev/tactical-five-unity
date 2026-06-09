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

    // ══ DATA ══════════════════════════════════════════════

    TeamData FindTeam(string keyword)
    {
        return _allTeams?.Find(t => t.name.Contains(keyword));
    }

    // ══ EQUIPOS TAB ══════════════════════════════════════

    struct FinalsRow
    {
        public string season;
        public string champName;
        public string champKeyword;
        public string finalistName;
        public string finalistKeyword;
        public string result;
        public string mvp;
    }

    List<FinalsRow> GetFinalsData()
    {
        return new List<FinalsRow>
        {
            new FinalsRow { season = "1999-00", champName = "Los Angeles Lakers",   champKeyword = "Lakers",    finalistName = "Indiana Pacers",  finalistKeyword = "Pacers",   result = "4-2", mvp = "Shaquille O'Neal" },
            new FinalsRow { season = "2000-01", champName = "Los Angeles Lakers",   champKeyword = "Lakers",    finalistName = "Philadelphia 76ers", finalistKeyword = "76ers",  result = "4-1", mvp = "Shaquille O'Neal" },
            new FinalsRow { season = "2001-02", champName = "Los Angeles Lakers",   champKeyword = "Lakers",    finalistName = "New Jersey Nets", finalistKeyword = "Nets",     result = "4-0", mvp = "Shaquille O'Neal" },
            new FinalsRow { season = "2002-03", champName = "San Antonio Spurs",    champKeyword = "Spurs",     finalistName = "New Jersey Nets", finalistKeyword = "Nets",     result = "4-2", mvp = "Tim Duncan" },
            new FinalsRow { season = "2003-04", champName = "Detroit Pistons",      champKeyword = "Pistons",   finalistName = "Los Angeles Lakers", finalistKeyword = "Lakers", result = "4-1", mvp = "Chauncey Billups" },
            new FinalsRow { season = "2004-05", champName = "San Antonio Spurs",    champKeyword = "Spurs",     finalistName = "Detroit Pistons", finalistKeyword = "Pistons",  result = "4-3", mvp = "Tim Duncan" },
            new FinalsRow { season = "2005-06", champName = "Miami Heat",           champKeyword = "Heat",      finalistName = "Dallas Mavericks", finalistKeyword = "Mavericks", result = "4-2", mvp = "Dwyane Wade" },
            new FinalsRow { season = "2006-07", champName = "San Antonio Spurs",    champKeyword = "Spurs",     finalistName = "Cleveland Cavaliers", finalistKeyword = "Cavaliers", result = "4-0", mvp = "Tony Parker" },
            new FinalsRow { season = "2007-08", champName = "Boston Celtics",       champKeyword = "Celtics",   finalistName = "Los Angeles Lakers", finalistKeyword = "Lakers",  result = "4-2", mvp = "Paul Pierce" },
            new FinalsRow { season = "2008-09", champName = "Los Angeles Lakers",   champKeyword = "Lakers",    finalistName = "Orlando Magic",  finalistKeyword = "Magic",    result = "4-1", mvp = "Kobe Bryant" },
            new FinalsRow { season = "2009-10", champName = "Los Angeles Lakers",   champKeyword = "Lakers",    finalistName = "Boston Celtics", finalistKeyword = "Celtics",  result = "4-3", mvp = "Kobe Bryant" },
            new FinalsRow { season = "2010-11", champName = "Dallas Mavericks",     champKeyword = "Mavericks", finalistName = "Miami Heat",     finalistKeyword = "Heat",     result = "4-2", mvp = "Dirk Nowitzki" },
            new FinalsRow { season = "2011-12", champName = "Miami Heat",           champKeyword = "Heat",      finalistName = "Oklahoma City Thunder", finalistKeyword = "Thunder", result = "4-1", mvp = "LeBron James" },
            new FinalsRow { season = "2012-13", champName = "Miami Heat",           champKeyword = "Heat",      finalistName = "San Antonio Spurs", finalistKeyword = "Spurs",   result = "4-3", mvp = "LeBron James" },
            new FinalsRow { season = "2013-14", champName = "San Antonio Spurs",    champKeyword = "Spurs",     finalistName = "Miami Heat",     finalistKeyword = "Heat",     result = "4-1", mvp = "Kawhi Leonard" },
            new FinalsRow { season = "2014-15", champName = "Golden State Warriors", champKeyword = "Warriors", finalistName = "Cleveland Cavaliers", finalistKeyword = "Cavaliers", result = "4-2", mvp = "Andre Iguodala" },
            new FinalsRow { season = "2015-16", champName = "Cleveland Cavaliers",  champKeyword = "Cavaliers", finalistName = "Golden State Warriors", finalistKeyword = "Warriors", result = "4-3", mvp = "LeBron James" },
            new FinalsRow { season = "2016-17", champName = "Golden State Warriors", champKeyword = "Warriors", finalistName = "Cleveland Cavaliers", finalistKeyword = "Cavaliers", result = "4-1", mvp = "Kevin Durant" },
            new FinalsRow { season = "2017-18", champName = "Golden State Warriors", champKeyword = "Warriors", finalistName = "Cleveland Cavaliers", finalistKeyword = "Cavaliers", result = "4-0", mvp = "Kevin Durant" },
            new FinalsRow { season = "2018-19", champName = "Toronto Raptors",      champKeyword = "Raptors",   finalistName = "Golden State Warriors", finalistKeyword = "Warriors", result = "4-2", mvp = "Kawhi Leonard" },
            new FinalsRow { season = "2019-20", champName = "Los Angeles Lakers",   champKeyword = "Lakers",    finalistName = "Miami Heat",     finalistKeyword = "Heat",     result = "4-2", mvp = "LeBron James" },
            new FinalsRow { season = "2020-21", champName = "Milwaukee Bucks",      champKeyword = "Bucks",     finalistName = "Phoenix Suns",   finalistKeyword = "Suns",     result = "4-2", mvp = "Giannis Antetokounmpo" },
            new FinalsRow { season = "2021-22", champName = "Golden State Warriors", champKeyword = "Warriors", finalistName = "Boston Celtics", finalistKeyword = "Celtics",  result = "4-2", mvp = "Stephen Curry" },
            new FinalsRow { season = "2022-23", champName = "Denver Nuggets",       champKeyword = "Nuggets",   finalistName = "Miami Heat",     finalistKeyword = "Heat",     result = "4-1", mvp = "Nikola Jokic" },
            new FinalsRow { season = "2023-24", champName = "Boston Celtics",       champKeyword = "Celtics",   finalistName = "Dallas Mavericks", finalistKeyword = "Mavericks", result = "4-1", mvp = "Jaylen Brown" },
            new FinalsRow { season = "2024-25", champName = "Oklahoma City Thunder", champKeyword = "Thunder",  finalistName = "Indiana Pacers",  finalistKeyword = "Pacers",   result = "4-3", mvp = "Shai Gilgeous-Alexander" },
        };
    }

    void BuildEquiposTab()
    {
        BuildTitlesRanking();
        BuildFinalsHistory();
    }

    void BuildTitlesRanking()
    {
        _titlesRankingBody.Clear();

        var finalsData = GetFinalsData();
        var champCounts = new Dictionary<string, (int count, TeamData team)>();

        foreach (var f in finalsData)
        {
            var team = FindTeam(f.champKeyword);
            string key = team?.name ?? f.champName;
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

    void BuildFinalsHistory()
    {
        _finalsHistoryBody.Clear();

        var finalsData = GetFinalsData();
        finalsData.Reverse();

        foreach (var f in finalsData)
        {
            var row = new VisualElement();
            row.AddToClassList("palmares-data-row");

            var seasonLbl = new Label { text = f.season };
            seasonLbl.AddToClassList("td-season");
            row.Add(seasonLbl);

            row.Add(CreateCellWithLogo(FindTeam(f.champKeyword), "td-champ", f.champName));
            row.Add(CreateCellWithLogo(FindTeam(f.finalistKeyword), "td-finalist", f.finalistName));

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

    struct AwardsRow
    {
        public string season;
        public string mvp;
        public string mvpTeamKeyword;
        public string mvpRating;
        public string rookie;
        public string rookieTeamKeyword;
        public string rookieRating;
    }

    List<AwardsRow> GetAwardsData()
    {
        return new List<AwardsRow>
        {
            new AwardsRow { season = "1999-00", mvp = "Shaquille O'Neal", mvpTeamKeyword = "Lakers", mvpRating = "29.7", rookie = "Elton Brand / Steve Francis", rookieTeamKeyword = "", rookieRating = "20.1 / 18.0" },
            new AwardsRow { season = "2000-01", mvp = "Allen Iverson", mvpTeamKeyword = "76ers", mvpRating = "31.1", rookie = "Mike Miller", rookieTeamKeyword = "Magic", rookieRating = "11.9" },
            new AwardsRow { season = "2001-02", mvp = "Tim Duncan", mvpTeamKeyword = "Spurs", mvpRating = "25.5", rookie = "Pau Gasol", rookieTeamKeyword = "Grizzlies", rookieRating = "17.6" },
            new AwardsRow { season = "2002-03", mvp = "Tim Duncan", mvpTeamKeyword = "Spurs", mvpRating = "23.3", rookie = "Amar'e Stoudemire", rookieTeamKeyword = "Suns", rookieRating = "13.5" },
            new AwardsRow { season = "2003-04", mvp = "Kevin Garnett", mvpTeamKeyword = "Timberwolves", mvpRating = "24.2", rookie = "LeBron James", rookieTeamKeyword = "Cavaliers", rookieRating = "20.9" },
            new AwardsRow { season = "2004-05", mvp = "Steve Nash", mvpTeamKeyword = "Suns", mvpRating = "15.5", rookie = "Emeka Okafor", rookieTeamKeyword = "Hornets", rookieRating = "15.1" },
            new AwardsRow { season = "2005-06", mvp = "Steve Nash", mvpTeamKeyword = "Suns", mvpRating = "18.8", rookie = "Chris Paul", rookieTeamKeyword = "Hornets", rookieRating = "16.1" },
            new AwardsRow { season = "2006-07", mvp = "Dirk Nowitzki", mvpTeamKeyword = "Mavericks", mvpRating = "24.6", rookie = "Brandon Roy", rookieTeamKeyword = "Trail Blazers", rookieRating = "16.8" },
            new AwardsRow { season = "2007-08", mvp = "Kobe Bryant", mvpTeamKeyword = "Lakers", mvpRating = "28.3", rookie = "Kevin Durant", rookieTeamKeyword = "Thunder", rookieRating = "20.3" },
            new AwardsRow { season = "2008-09", mvp = "LeBron James", mvpTeamKeyword = "Cavaliers", mvpRating = "28.4", rookie = "Derrick Rose", rookieTeamKeyword = "Bulls", rookieRating = "16.8" },
            new AwardsRow { season = "2009-10", mvp = "LeBron James", mvpTeamKeyword = "Cavaliers", mvpRating = "29.7", rookie = "Tyreke Evans", rookieTeamKeyword = "Kings", rookieRating = "20.1" },
            new AwardsRow { season = "2010-11", mvp = "Derrick Rose", mvpTeamKeyword = "Bulls", mvpRating = "25.0", rookie = "Blake Griffin", rookieTeamKeyword = "Clippers", rookieRating = "22.5" },
            new AwardsRow { season = "2011-12", mvp = "LeBron James", mvpTeamKeyword = "Heat", mvpRating = "27.1", rookie = "Kyrie Irving", rookieTeamKeyword = "Cavaliers", rookieRating = "18.5" },
            new AwardsRow { season = "2012-13", mvp = "LeBron James", mvpTeamKeyword = "Heat", mvpRating = "26.8", rookie = "Damian Lillard", rookieTeamKeyword = "Trail Blazers", rookieRating = "19.0" },
            new AwardsRow { season = "2013-14", mvp = "Kevin Durant", mvpTeamKeyword = "Thunder", mvpRating = "32.0", rookie = "Michael Carter-Williams", rookieTeamKeyword = "76ers", rookieRating = "16.7" },
            new AwardsRow { season = "2014-15", mvp = "Stephen Curry", mvpTeamKeyword = "Warriors", mvpRating = "23.8", rookie = "Andrew Wiggins", rookieTeamKeyword = "Timberwolves", rookieRating = "16.9" },
            new AwardsRow { season = "2015-16", mvp = "Stephen Curry", mvpTeamKeyword = "Warriors", mvpRating = "30.1", rookie = "Karl-Anthony Towns", rookieTeamKeyword = "Timberwolves", rookieRating = "18.3" },
            new AwardsRow { season = "2016-17", mvp = "Russell Westbrook", mvpTeamKeyword = "Thunder", mvpRating = "31.6", rookie = "Malcolm Brogdon", rookieTeamKeyword = "Bucks", rookieRating = "10.2" },
            new AwardsRow { season = "2017-18", mvp = "James Harden", mvpTeamKeyword = "Rockets", mvpRating = "30.4", rookie = "Ben Simmons", rookieTeamKeyword = "76ers", rookieRating = "15.8" },
            new AwardsRow { season = "2018-19", mvp = "Giannis Antetokounmpo", mvpTeamKeyword = "Bucks", mvpRating = "27.7", rookie = "Luka Doncic", rookieTeamKeyword = "Mavericks", rookieRating = "21.2" },
            new AwardsRow { season = "2019-20", mvp = "Giannis Antetokounmpo", mvpTeamKeyword = "Bucks", mvpRating = "29.5", rookie = "Ja Morant", rookieTeamKeyword = "Grizzlies", rookieRating = "17.8" },
            new AwardsRow { season = "2020-21", mvp = "Nikola Jokic", mvpTeamKeyword = "Nuggets", mvpRating = "26.4", rookie = "LaMelo Ball", rookieTeamKeyword = "Hornets", rookieRating = "15.7" },
            new AwardsRow { season = "2021-22", mvp = "Nikola Jokic", mvpTeamKeyword = "Nuggets", mvpRating = "27.1", rookie = "Scottie Barnes", rookieTeamKeyword = "Raptors", rookieRating = "15.3" },
            new AwardsRow { season = "2022-23", mvp = "Joel Embiid", mvpTeamKeyword = "76ers", mvpRating = "33.1", rookie = "Paolo Banchero", rookieTeamKeyword = "Magic", rookieRating = "20.0" },
            new AwardsRow { season = "2023-24", mvp = "Nikola Jokic", mvpTeamKeyword = "Nuggets", mvpRating = "26.4", rookie = "Victor Wembanyama", rookieTeamKeyword = "Spurs", rookieRating = "21.4" },
            new AwardsRow { season = "2024-25", mvp = "Shai Gilgeous-Alexander", mvpTeamKeyword = "Thunder", mvpRating = "32.7", rookie = "Stephon Castle", rookieTeamKeyword = "Spurs", rookieRating = "14.7" },
        };
    }

    void BuildJugadoresTab()
    {
        BuildMVPRanking();
        BuildAwardsHistory();
    }

    void BuildMVPRanking()
    {
        _mvpRankingBody.Clear();

        var awardsData = GetAwardsData();
        var mvpCounts = new Dictionary<string, (int count, string teamKeyword)>();

        foreach (var a in awardsData)
        {
            if (!mvpCounts.ContainsKey(a.mvp))
                mvpCounts[a.mvp] = (0, a.mvpTeamKeyword);
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

    void BuildAwardsHistory()
    {
        _awardsHistoryBody.Clear();

        var awardsData = GetAwardsData();
        awardsData.Reverse();

        foreach (var a in awardsData)
        {
            var row = new VisualElement();
            row.AddToClassList("palmares-data-row");

            var seasonLbl = new Label { text = a.season };
            seasonLbl.AddToClassList("td-season");
            row.Add(seasonLbl);

            row.Add(CreatePlayerCell(a.mvp, string.IsNullOrEmpty(a.mvpTeamKeyword) ? null : FindTeam(a.mvpTeamKeyword), "td-mvp"));

            var mvpRatingLbl = new Label { text = a.mvpRating };
            mvpRatingLbl.AddToClassList("td-rating");
            row.Add(mvpRatingLbl);

            row.Add(CreatePlayerCell(a.rookie, string.IsNullOrEmpty(a.rookieTeamKeyword) ? null : FindTeam(a.rookieTeamKeyword), "td-rookie"));

            var rookieRatingLbl = new Label { text = a.rookieRating };
            rookieRatingLbl.AddToClassList("td-rating");
            row.Add(rookieRatingLbl);

            _awardsHistoryBody.Add(row);
        }
    }

    // ══ QUINTETOS TAB ════════════════════════════════════

    struct QuintetRow
    {
        public string season;
        public string pg;  public string pgTeam;
        public string sg;  public string sgTeam;
        public string sf;  public string sfTeam;
        public string pf;  public string pfTeam;
        public string c;   public string cTeam;
    }

    List<QuintetRow> GetQuintetData()
    {
        return new List<QuintetRow>
        {
            new QuintetRow { season = "1999-00", pg = "Jason Kidd", pgTeam = "Suns", sg = "Gary Payton", sgTeam = "Thunder", sf = "Kevin Garnett", sfTeam = "Timberwolves", pf = "Tim Duncan", pfTeam = "Spurs", c = "Shaquille O'Neal", cTeam = "Lakers" },
            new QuintetRow { season = "2000-01", pg = "Allen Iverson", pgTeam = "76ers", sg = "Jason Kidd", sgTeam = "Suns", sf = "Chris Webber", sfTeam = "Kings", pf = "Tim Duncan", pfTeam = "Spurs", c = "Shaquille O'Neal", cTeam = "Lakers" },
            new QuintetRow { season = "2001-02", pg = "Jason Kidd", pgTeam = "Nets", sg = "Kobe Bryant", sgTeam = "Lakers", sf = "Tracy McGrady", sfTeam = "Magic", pf = "Tim Duncan", pfTeam = "Spurs", c = "Shaquille O'Neal", cTeam = "Lakers" },
            new QuintetRow { season = "2002-03", pg = "Tracy McGrady", pgTeam = "Magic", sg = "Kobe Bryant", sgTeam = "Lakers", sf = "Kevin Garnett", sfTeam = "Timberwolves", pf = "Tim Duncan", pfTeam = "Spurs", c = "Shaquille O'Neal", cTeam = "Lakers" },
            new QuintetRow { season = "2003-04", pg = "Jason Kidd", pgTeam = "Nets", sg = "Kobe Bryant", sgTeam = "Lakers", sf = "Kevin Garnett", sfTeam = "Timberwolves", pf = "Tim Duncan", pfTeam = "Spurs", c = "Shaquille O'Neal", cTeam = "Lakers" },
            new QuintetRow { season = "2004-05", pg = "Steve Nash", pgTeam = "Suns", sg = "Allen Iverson", sgTeam = "76ers", sf = "Dirk Nowitzki", sfTeam = "Mavericks", pf = "Tim Duncan", pfTeam = "Spurs", c = "Shaquille O'Neal", cTeam = "Heat" },
            new QuintetRow { season = "2005-06", pg = "Steve Nash", pgTeam = "Suns", sg = "Kobe Bryant", sgTeam = "Lakers", sf = "LeBron James", sfTeam = "Cavaliers", pf = "Dirk Nowitzki", pfTeam = "Mavericks", c = "Shaquille O'Neal", cTeam = "Heat" },
            new QuintetRow { season = "2006-07", pg = "Steve Nash", pgTeam = "Suns", sg = "Kobe Bryant", sgTeam = "Lakers", sf = "Dirk Nowitzki", sfTeam = "Mavericks", pf = "Tim Duncan", pfTeam = "Spurs", c = "Amar'e Stoudemire", cTeam = "Suns" },
            new QuintetRow { season = "2007-08", pg = "Chris Paul", pgTeam = "Hornets", sg = "Kobe Bryant", sgTeam = "Lakers", sf = "LeBron James", sfTeam = "Cavaliers", pf = "Kevin Garnett", pfTeam = "Celtics", c = "Dwight Howard", cTeam = "Magic" },
            new QuintetRow { season = "2008-09", pg = "Dwyane Wade", pgTeam = "Heat", sg = "Kobe Bryant", sgTeam = "Lakers", sf = "LeBron James", sfTeam = "Cavaliers", pf = "Dirk Nowitzki", pfTeam = "Mavericks", c = "Dwight Howard", cTeam = "Magic" },
            new QuintetRow { season = "2009-10", pg = "Dwyane Wade", pgTeam = "Heat", sg = "Kobe Bryant", sgTeam = "Lakers", sf = "LeBron James", sfTeam = "Cavaliers", pf = "Kevin Durant", pfTeam = "Thunder", c = "Dwight Howard", cTeam = "Magic" },
            new QuintetRow { season = "2010-11", pg = "Derrick Rose", pgTeam = "Bulls", sg = "Kobe Bryant", sgTeam = "Lakers", sf = "LeBron James", sfTeam = "Heat", pf = "Kevin Durant", pfTeam = "Thunder", c = "Dwight Howard", cTeam = "Magic" },
            new QuintetRow { season = "2011-12", pg = "Chris Paul", pgTeam = "Clippers", sg = "Kobe Bryant", sgTeam = "Lakers", sf = "LeBron James", sfTeam = "Heat", pf = "Kevin Durant", pfTeam = "Thunder", c = "Dwight Howard", cTeam = "Magic" },
            new QuintetRow { season = "2012-13", pg = "Chris Paul", pgTeam = "Clippers", sg = "Kobe Bryant", sgTeam = "Lakers", sf = "LeBron James", sfTeam = "Heat", pf = "Kevin Durant", pfTeam = "Thunder", c = "Tim Duncan", cTeam = "Spurs" },
            new QuintetRow { season = "2013-14", pg = "Chris Paul", pgTeam = "Clippers", sg = "James Harden", sgTeam = "Rockets", sf = "LeBron James", sfTeam = "Heat", pf = "Kevin Durant", pfTeam = "Thunder", c = "Joakim Noah", cTeam = "Bulls" },
            new QuintetRow { season = "2014-15", pg = "Stephen Curry", pgTeam = "Warriors", sg = "James Harden", sgTeam = "Rockets", sf = "LeBron James", sfTeam = "Cavaliers", pf = "Anthony Davis", pfTeam = "Pelicans", c = "Marc Gasol", cTeam = "Grizzlies" },
            new QuintetRow { season = "2015-16", pg = "Stephen Curry", pgTeam = "Warriors", sg = "Russell Westbrook", sgTeam = "Thunder", sf = "LeBron James", sfTeam = "Cavaliers", pf = "Kawhi Leonard", pfTeam = "Spurs", c = "DeAndre Jordan", cTeam = "Clippers" },
            new QuintetRow { season = "2016-17", pg = "Russell Westbrook", pgTeam = "Thunder", sg = "James Harden", sgTeam = "Rockets", sf = "LeBron James", sfTeam = "Cavaliers", pf = "Kawhi Leonard", pfTeam = "Spurs", c = "Anthony Davis", cTeam = "Pelicans" },
            new QuintetRow { season = "2017-18", pg = "Damian Lillard", pgTeam = "Trail Blazers", sg = "James Harden", sgTeam = "Rockets", sf = "LeBron James", sfTeam = "Cavaliers", pf = "Kevin Durant", pfTeam = "Warriors", c = "Anthony Davis", cTeam = "Pelicans" },
            new QuintetRow { season = "2018-19", pg = "Stephen Curry", pgTeam = "Warriors", sg = "James Harden", sgTeam = "Rockets", sf = "Giannis Antetokounmpo", sfTeam = "Bucks", pf = "Paul George", pfTeam = "Thunder", c = "Nikola Jokic", cTeam = "Nuggets" },
            new QuintetRow { season = "2019-20", pg = "Luka Doncic", pgTeam = "Mavericks", sg = "James Harden", sgTeam = "Rockets", sf = "LeBron James", sfTeam = "Lakers", pf = "Giannis Antetokounmpo", pfTeam = "Bucks", c = "Anthony Davis", cTeam = "Lakers" },
            new QuintetRow { season = "2020-21", pg = "Stephen Curry", pgTeam = "Warriors", sg = "Luka Doncic", sgTeam = "Mavericks", sf = "Kawhi Leonard", sfTeam = "Clippers", pf = "Giannis Antetokounmpo", pfTeam = "Bucks", c = "Nikola Jokic", cTeam = "Nuggets" },
            new QuintetRow { season = "2021-22", pg = "Luka Doncic", pgTeam = "Mavericks", sg = "Devin Booker", sgTeam = "Suns", sf = "Jayson Tatum", sfTeam = "Celtics", pf = "Giannis Antetokounmpo", pfTeam = "Bucks", c = "Nikola Jokic", cTeam = "Nuggets" },
            new QuintetRow { season = "2022-23", pg = "Luka Doncic", pgTeam = "Mavericks", sg = "Shai Gilgeous-Alexander", sgTeam = "Thunder", sf = "Jayson Tatum", sfTeam = "Celtics", pf = "Giannis Antetokounmpo", pfTeam = "Bucks", c = "Joel Embiid", cTeam = "76ers" },
            new QuintetRow { season = "2023-24", pg = "Luka Doncic", pgTeam = "Mavericks", sg = "Shai Gilgeous-Alexander", sgTeam = "Thunder", sf = "Jayson Tatum", sfTeam = "Celtics", pf = "Giannis Antetokounmpo", pfTeam = "Bucks", c = "Nikola Jokic", cTeam = "Nuggets" },
            new QuintetRow { season = "2024-25", pg = "Donovan Mitchell", pgTeam = "Cavaliers", sg = "Shai Gilgeous-Alexander", sgTeam = "Thunder", sf = "Jayson Tatum", sfTeam = "Celtics", pf = "Giannis Antetokounmpo", pfTeam = "Bucks", c = "Nikola Jokic", cTeam = "Nuggets" },
        };
    }

    void BuildQuintetosTab()
    {
        BuildQuintetAppearances();
        BuildQuintetHistory();
    }

    void BuildQuintetAppearances()
    {
        _quintetAppearancesBody.Clear();

        var quintetData = GetQuintetData();
        var appearanceCounts = new Dictionary<string, (int count, string teamKeyword)>();

        foreach (var q in quintetData)
        {
            var players = new[] {
                (q.pg, q.pgTeam), (q.sg, q.sgTeam),
                (q.sf, q.sfTeam), (q.pf, q.pfTeam), (q.c, q.cTeam)
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

    void BuildQuintetHistory()
    {
        _quintetHistoryBody.Clear();

        var quintetData = GetQuintetData();
        quintetData.Reverse();

        foreach (var q in quintetData)
        {
            var row = new VisualElement();
            row.AddToClassList("palmares-data-row");

            var seasonLbl = new Label { text = q.season };
            seasonLbl.AddToClassList("td-season");
            row.Add(seasonLbl);

            row.Add(CreatePlayerCell(q.pg, string.IsNullOrEmpty(q.pgTeam) ? null : FindTeam(q.pgTeam), "td-quintet-pos"));
            row.Add(CreatePlayerCell(q.sg, string.IsNullOrEmpty(q.sgTeam) ? null : FindTeam(q.sgTeam), "td-quintet-pos"));
            row.Add(CreatePlayerCell(q.sf, string.IsNullOrEmpty(q.sfTeam) ? null : FindTeam(q.sfTeam), "td-quintet-pos"));
            row.Add(CreatePlayerCell(q.pf, string.IsNullOrEmpty(q.pfTeam) ? null : FindTeam(q.pfTeam), "td-quintet-pos"));
            row.Add(CreatePlayerCell(q.c, string.IsNullOrEmpty(q.cTeam) ? null : FindTeam(q.cTeam), "td-quintet-pos"));

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
