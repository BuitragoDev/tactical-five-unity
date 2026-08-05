using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class StandingsController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Standings;
    private Button _tabEast;
    private Button _tabWest;
    private VisualElement _standingsBody;

    // Right panel stat cards
    private VisualElement _cardBestAttackLogo;
    private VisualElement _cardBestDefenseLogo;
    private VisualElement _cardBestStreakLogo;
    private VisualElement _cardWorstStreakLogo;
    private Label _cardBestAttackTeam;
    private Label _cardBestDefenseTeam;
    private Label _cardBestStreakTeam;
    private Label _cardWorstStreakTeam;
    private Label _cardBestAttackValue;
    private Label _cardBestDefenseValue;
    private Label _cardBestStreakValue;
    private Label _cardWorstStreakValue;
    private List<TeamData> _allTeams;
    private List<GameData> _allGames;
    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _logoSprites32 = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();
    private string _currentFilter = "East";
    private VisualElement _rankChart;
    private VisualElement _rankChartXLabels;
    private VisualElement _rankEvoContent;
    private Button _rankEvoToggle;
    private bool _rankEvoExpanded;
    protected override void CacheReferences()
    {
        _tabEast = _root.Q<Button>("TabEast");
        _tabWest = _root.Q<Button>("TabWest");
        _standingsBody = _root.Q<VisualElement>("StandingsBody");

        _cardBestAttackLogo = _root.Q<VisualElement>("CardBestAttackLogo");
        _cardBestDefenseLogo = _root.Q<VisualElement>("CardBestDefenseLogo");
        _cardBestStreakLogo = _root.Q<VisualElement>("CardBestStreakLogo");
        _cardWorstStreakLogo = _root.Q<VisualElement>("CardWorstStreakLogo");
        _cardBestAttackTeam = _root.Q<Label>("CardBestAttackTeam");
        _cardBestDefenseTeam = _root.Q<Label>("CardBestDefenseTeam");
        _cardBestStreakTeam = _root.Q<Label>("CardBestStreakTeam");
        _cardWorstStreakTeam = _root.Q<Label>("CardWorstStreakTeam");
        _cardBestAttackValue = _root.Q<Label>("CardBestAttackValue");
        _cardBestDefenseValue = _root.Q<Label>("CardBestDefenseValue");
        _cardBestStreakValue = _root.Q<Label>("CardBestStreakValue");
        _cardWorstStreakValue = _root.Q<Label>("CardWorstStreakValue");

        _rankChart = _root.Q<VisualElement>("RankChart");
        _rankChartXLabels = _root.Q<VisualElement>("RankChartXLabels");
        _rankEvoContent = _root.Q<VisualElement>("RankEvoContent");
        _rankEvoToggle = _root.Q<Button>("RankEvoToggle");
    }
    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos32 = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        
        

        
        
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);

        // Predeterminar conferencia del equipo
        if (_myTeam != null)
            _currentFilter = _myTeam.conference;
    }
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _tabEast?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowStandings("East"); });
        _tabWest?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowStandings("West"); });
        _rankEvoToggle?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ToggleRankEvolution(); });
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Standings] RefreshHeader error: {ex.Message}"); }
        ShowStandings(_currentFilter);
        RefreshStatCards();
        BuildRankEvolution();
    }

    void RefreshStatCards()
    {
        if (_allTeams == null || _allTeams.Count == 0) return;
        // Build standings for ALL teams (no conference filter)
        var allStandings = BuildStandings(_allTeams);

        // Best attack: highest points per game
        var bestAttack = allStandings.OrderByDescending(s =>
        {
            int gp = s.wins + s.losses;
            return gp > 0 ? (float)s.pf / gp : 0f;
        }).FirstOrDefault();

        // Best defense: lowest points against per game
        var bestDefense = allStandings.OrderBy(s =>
        {
            int gp = s.wins + s.losses;
            return gp > 0 ? (float)s.pa / gp : float.MaxValue;
        }).FirstOrDefault();

        // Best/worst current streak
        StandingRow bestStreakTeam = null;
        StandingRow worstStreakTeam = null;
        int bestStreakCount = 0;
        int worstStreakCount = 0;

        foreach (var row in allStandings)
        {
            var (count, type) = GetCurrentStreak(row.games);
            if (type == "win" && count > bestStreakCount)
            {
                bestStreakCount = count;
                bestStreakTeam = row;
            }
            if (type == "loss" && count > worstStreakCount)
            {
                worstStreakCount = count;
                worstStreakTeam = row;
            }
        }

        // Fill cards
        FillStatCardWithUnit(_cardBestAttackLogo, _cardBestAttackTeam, _cardBestAttackValue,
            bestAttack, bestAttack != null ? GetAvgPoints(bestAttack) : "-", true);

        FillStatCardWithUnit(_cardBestDefenseLogo, _cardBestDefenseTeam, _cardBestDefenseValue,
            bestDefense, bestDefense != null ? GetAvgPointsAgainst(bestDefense) : "-", true);

        FillStatCard(_cardBestStreakLogo, _cardBestStreakTeam, _cardBestStreakValue,
            bestStreakTeam, bestStreakTeam != null ? $"{bestStreakCount}V" : "-");

        FillStatCard(_cardWorstStreakLogo, _cardWorstStreakTeam, _cardWorstStreakValue,
            worstStreakTeam, worstStreakTeam != null ? $"{worstStreakCount}D" : "-");
    }

    void FillStatCard(VisualElement logoElem, Label teamLbl, Label valueLbl, StandingRow row, string valueText)
    {
        FillStatCardWithUnit(logoElem, teamLbl, valueLbl, row, valueText, false);
    }

    void FillStatCardWithUnit(VisualElement logoElem, Label teamLbl, Label valueLbl, StandingRow row, string valueText, bool showUnit)
    {
        if (row == null)
        {
            teamLbl.text = "";
            valueLbl.text = valueText;
            return;
        }

        var team = _allTeams.Find(t => t.id == row.teamId);
        if (team != null)
        {
            teamLbl.text = team.name.ToUpper();
            if (logoElem != null && _logoSprites64.TryGetValue(team.logo, out var sprite))
                logoElem.style.backgroundImage = new StyleBackground(sprite);
        }
        valueLbl.text = valueText;

        var parent = valueLbl.parent;
        var oldRow = parent?.Q<VisualElement>(valueLbl.name + "_row");
        if (oldRow != null) oldRow.RemoveFromHierarchy();

        if (showUnit)
        {
            var rowContainer = new VisualElement();
            rowContainer.name = valueLbl.name + "_row";
            rowContainer.style.flexDirection = FlexDirection.Row;
            rowContainer.style.alignItems = Align.Center;
            rowContainer.style.justifyContent = Justify.Center;

            valueLbl.RemoveFromHierarchy();

            var unitLbl = new Label();
            unitLbl.AddToClassList("stat-card-unit");
            unitLbl.text = "PTS/P.";

            rowContainer.Add(valueLbl);
            rowContainer.Add(unitLbl);
            parent?.Add(rowContainer);
        }
    }

    string GetAvgPoints(StandingRow row)
    {
        int gp = row.wins + row.losses;
        if (gp == 0) return "-";
        return (row.pf / (float)gp).ToString("F1");
    }

    string GetAvgPointsAgainst(StandingRow row)
    {
        int gp = row.wins + row.losses;
        if (gp == 0) return "-";
        return (row.pa / (float)gp).ToString("F1");
    }

    (int count, string type) GetCurrentStreak(List<bool> games)
    {
        if (games == null || games.Count == 0) return (0, "none");
        bool last = games[games.Count - 1];
        int count = 0;
        for (int i = games.Count - 1; i >= 0; i--)
        {
            if (games[i] == last) count++;
            else break;
        }
        return (count, last ? "win" : "loss");
    }
    protected override void RefreshHeader()
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
            if (a.losses != b.losses) return a.losses.CompareTo(b.losses);
            if (b.wins != a.wins) return b.wins.CompareTo(a.wins);
            int diffA = a.pf - a.pa;
            int diffB = b.pf - b.pa;
            return diffB.CompareTo(diffA);
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

    void ToggleRankEvolution()
    {
        _rankEvoExpanded = !_rankEvoExpanded;
        if (_rankEvoExpanded)
        {
            _rankEvoContent.RemoveFromClassList("rank-evo-content--hidden");
            _rankEvoContent.style.display = DisplayStyle.Flex;
        }
        else
        {
            _rankEvoContent.AddToClassList("rank-evo-content--hidden");
            _rankEvoContent.style.display = DisplayStyle.None;
        }
    }

    void BuildRankEvolution()
    {
        if (_rankChart == null || _myTeam == null || _allGames == null || _allGames.Count == 0) return;

        _rankChart.Clear();
        _rankChartXLabels.Clear();

        var myConferenceTeams = _allTeams
            .Where(t => t.conference == _myTeam.conference)
            .Select(t => t.id)
            .ToHashSet();

        var gamesByDay = _allGames
            .GroupBy(g => g.game_day)
            .OrderBy(g => g.Key)
            .ToList();

        const float chartHeight = 220f;
        const int maxRank = 15;

        for (int r = 1; r <= maxRank; r++)
        {
            if (r == 1 || r == 5 || r == 10 || r == 15)
            {
                var gridLine = new VisualElement();
                gridLine.AddToClassList("rank-chart-line");
                float yPos = (r - 1) / (float)(maxRank - 1) * chartHeight;
                gridLine.style.top = new StyleLength(new Length(yPos, LengthUnit.Pixel));
                _rankChart.Add(gridLine);

                var yLabel = new Label(r.ToString());
                yLabel.AddToClassList("rank-chart-y-label");
                yLabel.style.top = new StyleLength(new Length(yPos - 7f, LengthUnit.Pixel));
                _rankChart.Add(yLabel);
            }
        }

        var confStandings = new Dictionary<int, (int wins, int losses, int pf, int pa)>();
        foreach (var t in _allTeams)
            if (myConferenceTeams.Contains(t.id))
                confStandings[t.id] = (0, 0, 0, 0);

        int totalDays = gamesByDay.Count;
        if (totalDays <= 1) return;

        for (int di = 0; di < totalDays; di++)
        {
            var dayGroup = gamesByDay[di];

            foreach (var g in dayGroup)
            {
                if (confStandings.ContainsKey(g.home_team_id))
                {
                    bool homeWon = g.home_score > g.away_score;
                    var s = confStandings[g.home_team_id];
                    confStandings[g.home_team_id] = (
                        s.wins + (homeWon ? 1 : 0),
                        s.losses + (homeWon ? 0 : 1),
                        s.pf + g.home_score,
                        s.pa + g.away_score);
                }
                if (confStandings.ContainsKey(g.away_team_id))
                {
                    bool awayWon = g.away_score > g.home_score;
                    var s = confStandings[g.away_team_id];
                    confStandings[g.away_team_id] = (
                        s.wins + (awayWon ? 1 : 0),
                        s.losses + (awayWon ? 0 : 1),
                        s.pf + g.away_score,
                        s.pa + g.home_score);
                }
            }

            var sorted = confStandings
                .Select(kv => (id: kv.Key, w: kv.Value.wins, l: kv.Value.losses, pf: kv.Value.pf, pa: kv.Value.pa))
                .OrderByDescending(x => x.w + x.l > 0 ? (float)x.w / (x.w + x.l) : 0f)
                .ThenBy(x => x.l)
                .ThenByDescending(x => x.w)
                .ThenByDescending(x => x.pf - x.pa)
                .ToList();

            int myRank = sorted.FindIndex(x => x.id == _myTeam.id) + 1;

            var bar = new VisualElement();
            bar.AddToClassList("rank-chart-bar");
            if (myRank <= 6)
                bar.AddToClassList("rank-chart-bar--playoff");
            else if (myRank <= 10)
                bar.AddToClassList("rank-chart-bar--playin");
            else
                bar.AddToClassList("rank-chart-bar--lottery");

            float xPct = di / (float)(totalDays - 1) * 100f;
            float yPos = (myRank - 1) / (float)(maxRank - 1) * chartHeight;

            bar.style.left = new StyleLength(new Length(xPct, LengthUnit.Percent));
            bar.style.top = new StyleLength(new Length(yPos, LengthUnit.Pixel));
            bar.style.height = new StyleLength(new Length(3f, LengthUnit.Pixel));
            _rankChart.Add(bar);
        }

        int step = totalDays <= 10 ? 1
            : totalDays <= 20 ? 2
            : totalDays <= 41 ? 5
            : 10;

        for (int di = 0; di < totalDays; di++)
        {
            int dayNum = gamesByDay[di].Key;
            if (dayNum % step == 1 || di == 0 || di == totalDays - 1)
            {
                var xLabel = new Label(dayNum.ToString());
                xLabel.AddToClassList("rank-chart-x-label");
                float xPct = di / (float)(totalDays - 1) * 100f;
                xLabel.style.left = new StyleLength(new Length(xPct, LengthUnit.Percent));
                _rankChartXLabels.Add(xLabel);
            }
        }
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
}
