using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class StandingsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private Button _tabEast;
    private Button _tabWest;
    private VisualElement _standingsBody;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;
    private List<GameData> _allGames;

    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSprites32 = new();
    private Dictionary<string, Sprite> _logoSprites80 = new();
    private string _currentFilter = "East";

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
        _tabEast = _root.Q<Button>("TabEast");
        _tabWest = _root.Q<Button>("TabWest");
        _standingsBody = _root.Q<VisualElement>("StandingsBody");
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos32 = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        var logos80 = Resources.LoadAll<Sprite>("Teams/Logos/80x80");
        foreach (var s in logos80) _logoSprites80[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);

        // Predeterminar conferencia del equipo
        if (_myTeam != null)
            _currentFilter = _myTeam.conference;
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();
        _tabEast?.RegisterCallback<ClickEvent>(_ => ShowStandings("East"));
        _tabWest?.RegisterCallback<ClickEvent>(_ => ShowStandings("West"));
        _btnAction?.RegisterCallback<ClickEvent>(_ => ScreenManager.Instance.GoTo(GameScreen.Dashboard));
    }

    void RegisterNavButtons()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Roster));
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Calendar));
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
        _root.Q<Button>("NavConfig")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Settings));
    }

    void Refresh()
    {
        RefreshHeader();
        ShowStandings(_currentFilter);
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites80.TryGetValue(_myTeam.logo, out var sprite))
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

        _btnAction.text = "DASHBOARD";
    }

    void ShowStandings(string filter)
    {
        _currentFilter = filter;

        _tabEast.RemoveFromClassList("standings-tab--active");
        _tabWest.RemoveFromClassList("standings-tab--active");

        if (filter == "East") _tabEast.AddToClassList("standings-tab--active");
        else _tabWest.AddToClassList("standings-tab--active");

        var filtered = _allTeams.FindAll(t => t.conference == filter);
        var standings = BuildStandings(filtered);
        _standingsBody.Clear();

        foreach (var row in standings)
        {
            var team = _allTeams.Find(t => t.id == row.teamId);
            var rowElem = CreateStandingsRow(row, team);
            _standingsBody.Add(rowElem);
        }
    }

    List<StandingRow> BuildStandings(List<TeamData> teams)
    {
        var data = teams.ToDictionary(t => t.id, t => new StandingRow
        {
            teamId = t.id,
            wins = 0,
            losses = 0,
            pf = 0,
            pa = 0,
            games = new List<bool>()
        });

        foreach (var g in _allGames)
        {
            if (data.ContainsKey(g.home_team_id))
            {
                bool homeWon = g.home_score > g.away_score;
                data[g.home_team_id].wins += homeWon ? 1 : 0;
                data[g.home_team_id].losses += homeWon ? 0 : 1;
                data[g.home_team_id].pf += g.home_score;
                data[g.home_team_id].pa += g.away_score;
                data[g.home_team_id].games.Add(homeWon);
            }
            if (data.ContainsKey(g.away_team_id))
            {
                bool awayWon = g.away_score > g.home_score;
                data[g.away_team_id].wins += awayWon ? 1 : 0;
                data[g.away_team_id].losses += awayWon ? 0 : 1;
                data[g.away_team_id].pf += g.away_score;
                data[g.away_team_id].pa += g.home_score;
                data[g.away_team_id].games.Add(awayWon);
            }
        }

        var rows = data.Values.ToList();
        rows.Sort((a, b) =>
        {
            float pctA = a.wins + a.losses > 0 ? (float)a.wins / (a.wins + a.losses) : 0;
            float pctB = b.wins + b.losses > 0 ? (float)b.wins / (b.wins + b.losses) : 0;
            if (pctB != pctA) return pctB.CompareTo(pctA);
            int diffA = a.pf - a.pa;
            int diffB = b.pf - b.pa;
            if (diffB != diffA) return diffB.CompareTo(diffA);
            return b.wins.CompareTo(a.wins);
        });

        for (int i = 0; i < rows.Count; i++)
            rows[i].rank = i + 1;

        return rows;
    }

    VisualElement CreateStandingsRow(StandingRow row, TeamData team)
    {
        var elem = new VisualElement();
        elem.AddToClassList("standings-row");

        bool isMyTeam = team != null && team.id == _myTeam.id;
        if (isMyTeam) elem.AddToClassList("standings-row--my-team");
        else if (row.rank <= 6) elem.AddToClassList("standings-row--playoff");
        else if (row.rank <= 10) elem.AddToClassList("standings-row--playin");
        else elem.AddToClassList("standings-row--lottery");

        int gp = row.wins + row.losses;
        float pct = gp > 0 ? (float)row.wins / gp : 0f;
        var streak = CalcStreak(row.games);
        var last10 = CalcLast10(row.games);
        int diff = row.pf - row.pa;

        var rankLbl = new Label();
        rankLbl.AddToClassList("col-rank");
        rankLbl.text = row.rank.ToString();

        var logoElem = new VisualElement();
        logoElem.AddToClassList("col-team-logo");
        if (team != null) SetTeamLogo(logoElem, team.logo);

        var nameLbl = new Label();
        nameLbl.AddToClassList("col-team-name");
        nameLbl.text = team?.name.ToUpper() ?? "???";

        var gpLbl = new Label();
        gpLbl.AddToClassList("col-stat");
        gpLbl.text = gp.ToString();

        var wLbl = new Label();
        wLbl.AddToClassList("col-stat");
        wLbl.AddToClassList("col-wins");
        wLbl.text = row.wins.ToString();

        var lLbl = new Label();
        lLbl.AddToClassList("col-stat");
        lLbl.AddToClassList("col-losses");
        lLbl.text = row.losses.ToString();

        var pctLbl = new Label();
        pctLbl.AddToClassList("col-stat");
        pctLbl.text = pct.ToString("F3");

        var diffLbl = new Label();
        diffLbl.AddToClassList("col-diff");
        diffLbl.text = diff > 0 ? $"+{diff}" : diff.ToString();
        diffLbl.style.color = diff > 0
            ? new StyleColor(new Color(0.15f, 0.68f, 0.38f))
            : new StyleColor(new Color(0.75f, 0.22f, 0.17f));

        var streakLbl = new Label();
        streakLbl.AddToClassList("col-streak");
        streakLbl.text = streak.text;
        streakLbl.AddToClassList(streak.type == "win" ? "streak-win" :
                                  streak.type == "loss" ? "streak-loss" : "streak-none");

        var last10Lbl = new Label();
        last10Lbl.AddToClassList("col-last10");
        last10Lbl.text = last10;

        elem.Add(rankLbl);
        elem.Add(logoElem);
        elem.Add(nameLbl);
        elem.Add(gpLbl);
        elem.Add(wLbl);
        elem.Add(lLbl);
        elem.Add(pctLbl);
        elem.Add(diffLbl);
        elem.Add(streakLbl);
        elem.Add(last10Lbl);

        return elem;
    }

    (string text, string type) CalcStreak(List<bool> games)
    {
        if (games == null || games.Count == 0) return ("-", "none");
        bool last = games[games.Count - 1];
        int count = 0;
        for (int i = games.Count - 1; i >= 0; i--)
        {
            if (games[i] == last) count++;
            else break;
        }
        return last ? ($"{count}V", "win") : ($"{count}D", "loss");
    }

    string CalcLast10(List<bool> games)
    {
        if (games == null || games.Count == 0) return "-";
        var last10 = games.Skip(Mathf.Max(0, games.Count - 10)).ToList();
        int wins = last10.Count(g => g);
        return $"{wins}-{last10.Count - wins}";
    }

    void SetTeamLogo(VisualElement elem, string logoName)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;
        if (_logoSprites32.TryGetValue(logoName, out var sprite))
            elem.style.backgroundImage = new StyleBackground(sprite);
        else if (_logoSprites.TryGetValue(logoName, out var fallback))
            elem.style.backgroundImage = new StyleBackground(fallback);
    }

    class StandingRow
    {
        public int teamId;
        public int rank;
        public int wins;
        public int losses;
        public int pf;
        public int pa;
        public List<bool> games;
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
}
