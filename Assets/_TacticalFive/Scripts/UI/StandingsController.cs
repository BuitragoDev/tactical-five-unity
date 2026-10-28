using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class StandingsController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Standings;
    private Button _tabEast;
    private Button _tabWest;
    private Button _cupTabEast, _cupTabWest, _cupTabKnockout;
    private VisualElement _standingsHeaderRow;
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
    private string _cupFilter = "East";
    protected override void CacheReferences()
    {
        _tabEast = _root.Q<Button>("TabEast");
        _tabWest = _root.Q<Button>("TabWest");
        _cupTabEast = _root.Q<Button>("CupTabEast");
        _cupTabWest = _root.Q<Button>("CupTabWest");
        _cupTabKnockout = _root.Q<Button>("CupTabKnockout");
        _standingsHeaderRow = _root.Q<VisualElement>("StandingsHeaderRow");
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
    }
    protected override void LoadData()
    {
        base.LoadData();

        var logos = TeamImages.Logos(200);
        foreach (var s in logos) _logoSprites[s.name] = s;

        var logos32 = TeamImages.Logos(32);
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        var logos64 = TeamImages.Logos(64);
        foreach (var s in logos64) _logoSprites64[s.name] = s;
        // ── Screen header icon ──
        var screenIcon = _root.Q<VisualElement>("StandingsScreenIcon");
        if (screenIcon != null)
        {
            var iconTex = Resources.Load<Texture2D>("Icons/clasificacion_icon");
            if (iconTex != null)
                screenIcon.style.backgroundImage = new StyleBackground(iconTex);
        }

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
        _cupTabEast?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowStandings("CupEast"); });
        _cupTabWest?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowStandings("CupWest"); });
        _cupTabKnockout?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowStandings("CupKnockout"); });
    }
    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Standings] RefreshHeader error: {ex.Message}"); }
        ShowStandings(_currentFilter);
        RefreshStatCards();
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
            unitLbl.text = LocalizationService.T("ui.code.standings_controller.pts_p");

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
        _root.Q<Label>("HeaderManagerName").text = LocalizationService.T("ui.common.manager", _manager.name);
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
            _root.Q<Label>("HeaderSeason").text = LocalizationService.T("ui.common.season", _season.year_start, _season.year_end);
            _root.Q<Label>("HeaderDate").text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = LocalizationService.T("ui.code.standings_controller.menu_principal");
    }

    void ShowStandings(string filter)
    {
        _currentFilter = filter;

        _tabEast.RemoveFromClassList("standings-tab--active");
        _tabWest.RemoveFromClassList("standings-tab--active");
        _cupTabEast?.RemoveFromClassList("standings-tab--active");
        _cupTabWest?.RemoveFromClassList("standings-tab--active");
        _cupTabKnockout?.RemoveFromClassList("standings-tab--active");

        if (filter == "East" || filter == "West")
        {
            if (filter == "East") _tabEast.AddToClassList("standings-tab--active");
            else _tabWest.AddToClassList("standings-tab--active");

            if (_standingsHeaderRow != null)
                _standingsHeaderRow.style.display = DisplayStyle.Flex;

            var subtitleLabel = _root.Q<Label>("StandingsSubtitle");
            if (subtitleLabel != null)
                subtitleLabel.text = filter == "East" ? "CONFERENCIA ESTE" : "CONFERENCIA OESTE";

            var filtered = _allTeams.FindAll(t => t.conference == filter);
            var standings = BuildStandings(filtered);
            _standingsBody.Clear();

            foreach (var row in standings)
            {
                var team = _allTeams.Find(t => t.id == row.teamId);
                _standingsBody.Add(CreateStandingsRow(row, team));
            }
            return;
        }

        _cupFilter = filter == "CupWest" ? "West"
                   : filter == "CupKnockout" ? "Knockout"
                   : "East";
        if (_cupFilter == "East") _cupTabEast?.AddToClassList("standings-tab--active");
        else if (_cupFilter == "West") _cupTabWest?.AddToClassList("standings-tab--active");
        else _cupTabKnockout?.AddToClassList("standings-tab--active");

        ShowCupStandings();
    }

    void ShowCupStandings()
    {
        _standingsBody.Clear();
        var subtitle = _root.Q<Label>("StandingsSubtitle");
        if (_season == null)
        {
            _standingsBody.Add(new Label(LocalizationService.T("ui.code.standings_controller.nba_cup_no_disponible")));
            return;
        }

        var edition = DatabaseManager.Instance.GetCupEdition(_season.id);
        if (edition == null)
        {
            if (subtitle != null) subtitle.text = LocalizationService.T("ui.code.standings_controller.nba_cup_aun_no_generada");
            _standingsBody.Add(new Label(LocalizationService.T("ui.code.standings_controller.la_nba_cup_se_genera_al_crear_el_calenda_268b289e")));
            return;
        }

        string championText = "";
        if (edition.champion_team_id > 0)
        {
            var champion = _allTeams.Find(t => t.id == edition.champion_team_id);
            championText = $" · CAMPEÓN: {champion?.name.ToUpper() ?? "???"}";
        }
        if (subtitle != null)
            subtitle.text = $"NBA CUP {edition.year_start}-{edition.year_start + 1}{championText}";

        if (_cupFilter == "Knockout")
        {
            if (_standingsHeaderRow != null)
                _standingsHeaderRow.style.display = DisplayStyle.None;
            ShowCupKnockout(edition);
            return;
        }

        if (_standingsHeaderRow != null)
            _standingsHeaderRow.style.display = DisplayStyle.Flex;

        var standings = DatabaseManager.Instance.GetCupStandings(edition.id);
        if (standings.Count == 0)
        {
            var groupGames = DatabaseManager.Instance.GetCupGames(_season.id, _manager.id)
                .Where(g => (g.series_label ?? "").StartsWith(CupHelper.GroupLabelPrefix))
                .ToList();
            standings = CupHelper.ComputeStandings(_allTeams, groupGames);
        }

        var conferenceStandings = standings.Where(s => s.conference == _cupFilter).ToList();
        if (conferenceStandings.Count == 0)
        {
            _standingsBody.Add(new Label(LocalizationService.T("ui.standings.cup_standings_empty")));
            return;
        }

        foreach (var group in conferenceStandings.GroupBy(s => s.group_name).OrderBy(g => g.Key))
        {
            var header = new Label(group.Key.ToUpper());
            header.AddToClassList("standings-cup-group");
            _standingsBody.Add(header);

            foreach (var row in group.OrderBy(r => r.rank))
            {
                var standing = new StandingRow
                {
                    teamId = row.team_id,
                    rank = row.rank,
                    wins = row.wins,
                    losses = row.losses,
                    pf = row.points_for,
                    pa = row.points_against
                };
                var team = _allTeams.Find(t => t.id == row.team_id);
                _standingsBody.Add(CreateStandingsRow(standing, team, true));
            }
        }
    }

    void ShowCupKnockout(TournamentEditionData edition)
    {
        var games = DatabaseManager.Instance.GetCupGames(_season.id, _manager.id)
            .Where(IsCupKnockoutGame)
            .OrderBy(g => g.game_day)
            .ThenBy(g => g.id)
            .ToList();

        if (games.Count == 0)
        {
            string key = edition != null && edition.status == "group"
                ? "ui.standings.cup_knockout_pending"
                : "ui.standings.cup_knockout_empty";
            var empty = new Label(LocalizationService.T(key));
            empty.AddToClassList("standings-cup-message");
            _standingsBody.Add(empty);
            return;
        }

        AddCupKnockoutHeader();
        var stages = new (string prefix, string key, bool final)[]
        {
            (CupHelper.QfPrefix, "ui.standings.cup_round_qf", false),
            (CupHelper.SfPrefix, "ui.standings.cup_round_sf", false),
            (CupHelper.FinalLabel, "ui.standings.cup_round_final", true),
        };
        foreach (var (prefix, key, final) in stages)
        {
            var stageGames = final
                ? games.Where(g => g.series_label == CupHelper.FinalLabel).ToList()
                : games.Where(g => (g.series_label ?? "").StartsWith(prefix)).ToList();
            if (stageGames.Count == 0) continue;

            var stageLabel = new Label(LocalizationService.T(key));
            stageLabel.AddToClassList("standings-cup-group");
            _standingsBody.Add(stageLabel);

            foreach (var game in stageGames)
                AddCupKnockoutRow(game);
        }
    }

    static bool IsCupKnockoutGame(GameData game)
    {
        string label = game.series_label ?? "";
        return label.StartsWith(CupHelper.QfPrefix)
            || label.StartsWith(CupHelper.SfPrefix)
            || label == CupHelper.FinalLabel;
    }

    void AddCupKnockoutHeader()
    {
        var row = new VisualElement();
        row.AddToClassList("standings-cup-ko-header");
        row.Add(MakeCupKnockoutLabel(LocalizationService.T("ui.standings.cup_ko_home"), "standings-cup-ko-team"));
        row.Add(MakeCupKnockoutLabel(LocalizationService.T("ui.standings.cup_ko_score"), "standings-cup-ko-score"));
        row.Add(MakeCupKnockoutLabel(LocalizationService.T("ui.standings.cup_ko_away"), "standings-cup-ko-team"));
        _standingsBody.Add(row);
    }

    void AddCupKnockoutRow(GameData game)
    {
        var home = _allTeams.Find(t => t.id == game.home_team_id);
        var away = _allTeams.Find(t => t.id == game.away_team_id);
        var row = new VisualElement();
        row.AddToClassList("standings-cup-ko-row");

        row.Add(MakeCupKnockoutLabel(home?.name.ToUpper() ?? "—", "standings-cup-ko-team"));
        string score = game.is_played == 1 ? $"{game.home_score} - {game.away_score}" : "—";
        row.Add(MakeCupKnockoutLabel(score, "standings-cup-ko-score"));
        row.Add(MakeCupKnockoutLabel(away?.name.ToUpper() ?? "—", "standings-cup-ko-team"));
        _standingsBody.Add(row);
    }

    static Label MakeCupKnockoutLabel(string text, string className)
    {
        var label = new Label(text);
        label.AddToClassList(className);
        return label;
    }

    List<StandingRow> BuildStandings(List<TeamData> teams)
    {
        return StandingsHelper.Build(teams, _allGames);
    }

    VisualElement CreateStandingsRow(StandingRow row, TeamData team, bool cupRow = false)
    {
        var elem = new VisualElement();
        elem.AddToClassList("standings-row");
        if (cupRow) elem.AddToClassList("standings-row--cup");

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

    // StandingRow vive en Scripts/Core/StandingsHelper.cs (compartida).
}
