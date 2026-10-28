using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class PalmaresController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Palmares;
    private Button _tabEquipos;
    private Button _tabCup;
    private Button _tabJugadores;
    private Button _tabQuintetos;
    private Button _tabAllStar;
    private Button _tabSalonFama;
    private VisualElement _tabContentEquipos;
    private VisualElement _tabContentCup;
    private VisualElement _tabContentJugadores;
    private VisualElement _tabContentQuintetos;
    private VisualElement _tabContentAllStar;
    private VisualElement _tabContentSalonFama;

    // ── Data ──
    private VisualElement _titlesRankingBody;
    private List<FinalsRecord> _titlesRankingData;
    private VisualElement _finalsHistoryBody;
    private List<FinalsRecord> _finalsHistoryData;
    private VisualElement _cupTitlesRankingBody;
    private List<TournamentEditionData> _cupEditions;
    private VisualElement _cupFinalsHistoryBody;
    private VisualElement _mvpRankingBody;
    private List<AwardsRecord> _mvpRankingData;
    private VisualElement _awardsHistoryBody;
    private List<AwardsRecord> _awardsHistoryData;
    private VisualElement _quintetAppearancesBody;
    private List<QuintetRecord> _quintetAppearancesData;
    private VisualElement _quintetHistoryBody;
    private List<QuintetRecord> _quintetHistoryData;
    private VisualElement _allStarAppearancesBody;
    private List<DatabaseManager.AllStarAppearanceEntry> _allStarAppearancesData;
    private VisualElement _allStarHistoryBody;
    private List<AllStarRecord> _allStarHistoryData;
    private VisualElement _hoFBody;
    private List<HallOfFameData> _hoFData;
    private Dictionary<int, int> _milestoneCounts = new();

    // ── Scrollbar elements (viewport, track, thumb per list) ──
    private VisualElement _titlesRankingViewport, _titlesRankingTrack, _titlesRankingThumb;
    private VisualElement _finalsHistoryViewport, _finalsHistoryTrack, _finalsHistoryThumb;
    private VisualElement _cupTitlesRankingViewport, _cupTitlesRankingTrack, _cupTitlesRankingThumb;
    private VisualElement _cupFinalsHistoryViewport, _cupFinalsHistoryTrack, _cupFinalsHistoryThumb;
    private VisualElement _mvpRankingViewport, _mvpRankingTrack, _mvpRankingThumb;
    private VisualElement _awardsHistoryViewport, _awardsHistoryTrack, _awardsHistoryThumb;
    private VisualElement _quintetAppearancesViewport, _quintetAppearancesTrack, _quintetAppearancesThumb;
    private VisualElement _quintetHistoryViewport, _quintetHistoryTrack, _quintetHistoryThumb;
    private VisualElement _allStarAppearancesViewport, _allStarAppearancesTrack, _allStarAppearancesThumb;
    private VisualElement _allStarHistoryViewport, _allStarHistoryTrack, _allStarHistoryThumb;
    private VisualElement _hoFViewport, _hoFTrack, _hoFThumb;

    private ScrollBar _titlesRankingBar, _finalsHistoryBar;
    private ScrollBar _cupTitlesRankingBar, _cupFinalsHistoryBar;
    private ScrollBar _mvpRankingBar, _awardsHistoryBar;
    private ScrollBar _quintetAppearancesBar, _quintetHistoryBar, _allStarAppearancesBar, _allStarHistoryBar, _hoFBar;

    private List<TeamData> _allTeams;
    private List<SeasonRecord> _seasonRecords;
    private Dictionary<string, Sprite> _logoSprites32 = new();
    private Dictionary<string, Sprite> _logoSprites64 = new();

    protected override void OnEnable()
    {
        base.OnEnable();
    }

    protected override void CacheReferences()
    {
        _tabEquipos = _root.Q<Button>("TabEquipos");
        _tabCup = _root.Q<Button>("TabCup");
        _tabJugadores = _root.Q<Button>("TabJugadores");
        _tabQuintetos = _root.Q<Button>("TabQuintetos");
        _tabAllStar = _root.Q<Button>("TabAllStar");
        _tabSalonFama = _root.Q<Button>("TabSalonFama");



        _tabContentEquipos = _root.Q<VisualElement>("TabContentEquipos");
        _tabContentCup = _root.Q<VisualElement>("TabContentCup");
        _tabContentJugadores = _root.Q<VisualElement>("TabContentJugadores");
        _tabContentQuintetos = _root.Q<VisualElement>("TabContentQuintetos");
        _tabContentAllStar = _root.Q<VisualElement>("TabContentAllStar");
        _tabContentSalonFama = _root.Q<VisualElement>("TabContentSalonFama");




        _titlesRankingBody = _root.Q<VisualElement>("TitlesRankingBody");
        _finalsHistoryBody = _root.Q<VisualElement>("FinalsHistoryBody");
        _cupTitlesRankingBody = _root.Q<VisualElement>("CupTitlesRankingBody");
        _cupFinalsHistoryBody = _root.Q<VisualElement>("CupFinalsHistoryBody");
        _mvpRankingBody = _root.Q<VisualElement>("MVPRankingBody");
        _awardsHistoryBody = _root.Q<VisualElement>("AwardsHistoryBody");
        _quintetAppearancesBody = _root.Q<VisualElement>("QuintetAppearancesBody");
        _quintetHistoryBody = _root.Q<VisualElement>("QuintetHistoryBody");
        _allStarAppearancesBody = _root.Q<VisualElement>("AllStarAppearancesBody");
        _allStarHistoryBody = _root.Q<VisualElement>("AllStarHistoryBody");
        _hoFBody = _root.Q<VisualElement>("HallOfFameBody");

        // Scrollbar elements
        _titlesRankingViewport = _root.Q<VisualElement>("TitlesRankingViewport");
        _titlesRankingTrack = _root.Q<VisualElement>("TitlesRankingTrack");
        _titlesRankingThumb = _root.Q<VisualElement>("TitlesRankingThumb");
        _finalsHistoryViewport = _root.Q<VisualElement>("FinalsHistoryViewport");
        _finalsHistoryTrack = _root.Q<VisualElement>("FinalsHistoryTrack");
        _finalsHistoryThumb = _root.Q<VisualElement>("FinalsHistoryThumb");
        _cupTitlesRankingViewport = _root.Q<VisualElement>("CupTitlesRankingViewport");
        _cupTitlesRankingTrack = _root.Q<VisualElement>("CupTitlesRankingTrack");
        _cupTitlesRankingThumb = _root.Q<VisualElement>("CupTitlesRankingThumb");
        _cupFinalsHistoryViewport = _root.Q<VisualElement>("CupFinalsHistoryViewport");
        _cupFinalsHistoryTrack = _root.Q<VisualElement>("CupFinalsHistoryTrack");
        _cupFinalsHistoryThumb = _root.Q<VisualElement>("CupFinalsHistoryThumb");
        _mvpRankingViewport = _root.Q<VisualElement>("MVPRankingViewport");
        _mvpRankingTrack = _root.Q<VisualElement>("MVPRankingTrack");
        _mvpRankingThumb = _root.Q<VisualElement>("MVPRankingThumb");
        _awardsHistoryViewport = _root.Q<VisualElement>("AwardsHistoryViewport");
        _awardsHistoryTrack = _root.Q<VisualElement>("AwardsHistoryTrack");
        _awardsHistoryThumb = _root.Q<VisualElement>("AwardsHistoryThumb");
        _quintetAppearancesViewport = _root.Q<VisualElement>("QuintetAppearancesViewport");
        _quintetAppearancesTrack = _root.Q<VisualElement>("QuintetAppearancesTrack");
        _quintetAppearancesThumb = _root.Q<VisualElement>("QuintetAppearancesThumb");
        _quintetHistoryViewport = _root.Q<VisualElement>("QuintetHistoryViewport");
        _quintetHistoryTrack = _root.Q<VisualElement>("QuintetHistoryTrack");
        _quintetHistoryThumb = _root.Q<VisualElement>("QuintetHistoryThumb");
        _allStarAppearancesViewport = _root.Q<VisualElement>("AllStarAppearancesViewport");
        _allStarAppearancesTrack = _root.Q<VisualElement>("AllStarAppearancesTrack");
        _allStarAppearancesThumb = _root.Q<VisualElement>("AllStarAppearancesThumb");
        _allStarHistoryViewport = _root.Q<VisualElement>("AllStarHistoryViewport");
        _allStarHistoryTrack = _root.Q<VisualElement>("AllStarHistoryTrack");
        _allStarHistoryThumb = _root.Q<VisualElement>("AllStarHistoryThumb");
        _hoFViewport = _root.Q<VisualElement>("HallOfFameViewport");
        _hoFTrack = _root.Q<VisualElement>("HallOfFameTrack");
        _hoFThumb = _root.Q<VisualElement>("HallOfFameThumb");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var screenIcon = _root.Q<VisualElement>("PalmaresScreenIcon");
        if (screenIcon != null)
        {
            var iconTex = Resources.Load<Texture2D>("Icons/palmares_icon");
            if (iconTex != null) screenIcon.style.backgroundImage = new StyleBackground(iconTex);
        }

        var logos32 = TeamImages.Logos(32);
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        var logos64 = TeamImages.Logos(64);
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _seasonRecords = DatabaseManager.Instance.GetAllSeasonRecords(_season?.id ?? 0);
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _tabEquipos?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("equipos"); });
        _tabCup?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("cup"); });
        _tabJugadores?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("jugadores"); });
        _tabQuintetos?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("quintetos"); });
        _tabAllStar?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("allstar"); });
        _tabSalonFama?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("salonfama"); });


    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Palmares] RefreshHeader error: {ex.Message}"); }
        ShowTab("equipos");
    }

    void ShowTab(string tab)
    {
        _tabEquipos.RemoveFromClassList("palmares-tab--active");
        _tabCup.RemoveFromClassList("palmares-tab--active");
        _tabJugadores.RemoveFromClassList("palmares-tab--active");
        _tabQuintetos.RemoveFromClassList("palmares-tab--active");
        _tabAllStar.RemoveFromClassList("palmares-tab--active");
        _tabSalonFama.RemoveFromClassList("palmares-tab--active");



        _tabContentEquipos.style.display = DisplayStyle.None;
        _tabContentCup.style.display = DisplayStyle.None;
        _tabContentJugadores.style.display = DisplayStyle.None;
        _tabContentQuintetos.style.display = DisplayStyle.None;
        _tabContentAllStar.style.display = DisplayStyle.None;
        _tabContentSalonFama.style.display = DisplayStyle.None;



        switch (tab)
        {
            case "equipos":
                _tabEquipos.AddToClassList("palmares-tab--active");
                _tabContentEquipos.style.display = DisplayStyle.Flex;
                BuildEquiposTab();
                break;
            case "cup":
                _tabCup.AddToClassList("palmares-tab--active");
                _tabContentCup.style.display = DisplayStyle.Flex;
                BuildCupTab();
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
            case "allstar":
                _tabAllStar.AddToClassList("palmares-tab--active");
                _tabContentAllStar.style.display = DisplayStyle.Flex;
                BuildAllStarTab();
                break;
            case "salonfama":
                _tabSalonFama.AddToClassList("palmares-tab--active");
                _tabContentSalonFama.style.display = DisplayStyle.Flex;
                BuildSalonFamaTab();
                break;
        }
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

        _btnAction.text = LocalizationService.T("ui.code.palmares_controller.menu_principal");
    }

    // ══ DATA ══════════════════════════════════════════════

    TeamData FindTeam(string keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return null;
        return _allTeams?.Find(t =>
            t.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(t.logo, keyword, System.StringComparison.OrdinalIgnoreCase));
    }

    TeamData FindTeamById(int teamId)
        => teamId > 0 ? _allTeams?.Find(t => t.id == teamId) : null;

    // ══ EQUIPOS TAB ══════════════════════════════════════

    void BuildEquiposTab()
    {
        var finalsData = DatabaseManager.Instance.GetFinalsRecords();
        finalsData.Reverse();
        _titlesRankingData = finalsData;
        _finalsHistoryData = finalsData;
        RenderTitlesRanking();
        RenderFinalsHistory();
    }

    void RenderTitlesRanking()
    {
        var champCounts = new Dictionary<string, (int count, TeamData team)>();
        foreach (var f in _titlesRankingData)
        {
            var team = FindTeam(f.champ_keyword);
            string key = team?.name ?? f.champ_name;
            if (!champCounts.ContainsKey(key))
                champCounts[key] = (0, team);
            var cur = champCounts[key];
            champCounts[key] = (cur.count + 1, cur.team);
        }
        var sorted = champCounts.OrderByDescending(kv => kv.Value.count).ToList();

        _titlesRankingBody.Clear();

        if (sorted.Count == 0)
        {
            var emptyLbl = new Label { text = LocalizationService.T("ui.code.palmares_controller.aun_no_hay_campeonatos_registrados") };
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
        _titlesRankingBar = BindScrollBar(_titlesRankingViewport, _titlesRankingBody, _titlesRankingTrack, _titlesRankingThumb);
    }

    void RenderFinalsHistory()
    {
        _finalsHistoryBody.Clear();

        if (_finalsHistoryData.Count == 0) return;

        foreach (var f in _finalsHistoryData)
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
        _finalsHistoryBar = BindScrollBar(_finalsHistoryViewport, _finalsHistoryBody, _finalsHistoryTrack, _finalsHistoryThumb);
    }

    // ══ CAMPEONES NBA CUP TAB ═══════════════════════════

    void BuildCupTab()
    {
        _cupEditions = DatabaseManager.Instance.GetCupEditions(_manager.id);
        RenderCupTitlesRanking();
        RenderCupFinalsHistory();
    }

    void RenderCupTitlesRanking()
    {
        var champCounts = new Dictionary<int, (int count, TeamData team)>();
        foreach (var edition in _cupEditions)
        {
            if (edition.champion_team_id <= 0) continue;
            if (!champCounts.ContainsKey(edition.champion_team_id))
                champCounts[edition.champion_team_id] = (0, FindTeamById(edition.champion_team_id));
            var cur = champCounts[edition.champion_team_id];
            champCounts[edition.champion_team_id] = (cur.count + 1, cur.team);
        }
        var sorted = champCounts
            .OrderByDescending(kv => kv.Value.count)
            .ThenBy(kv => kv.Value.team?.name ?? "")
            .ToList();

        _cupTitlesRankingBody.Clear();

        if (sorted.Count == 0)
        {
            var emptyLbl = new Label { text = LocalizationService.T("ui.code.palmares_controller.aun_no_hay_campeonatos_registrados") };
            emptyLbl.AddToClassList("no-data-cell");
            _cupTitlesRankingBody.Add(emptyLbl);
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

            var nameLbl = new Label { text = kv.Value.team?.name ?? "—" };
            nameLbl.AddToClassList("champ-name");
            row.Add(nameLbl);

            var countLbl = new Label { text = kv.Value.count.ToString() };
            countLbl.AddToClassList("champ-count");
            row.Add(countLbl);

            _cupTitlesRankingBody.Add(row);
        }
        _cupTitlesRankingBar = BindScrollBar(_cupTitlesRankingViewport, _cupTitlesRankingBody,
            _cupTitlesRankingTrack, _cupTitlesRankingThumb);
    }

    void RenderCupFinalsHistory()
    {
        _cupFinalsHistoryBody.Clear();

        if (_cupEditions.Count == 0) return;

        foreach (var edition in _cupEditions.OrderByDescending(e => e.year_start))
        {
            var row = new VisualElement();
            row.AddToClassList("palmares-data-row");

            var seasonLbl = new Label { text = $"{edition.year_start}-{(edition.year_start + 1) % 100:00}" };
            seasonLbl.AddToClassList("td-season");
            row.Add(seasonLbl);

            var champion = FindTeamById(edition.champion_team_id);
            row.Add(CreateCellWithLogo(champion, "td-champ", champion?.name ?? "—"));

            var finalist = FindTeamById(edition.finalist_team_id);
            row.Add(CreateCellWithLogo(finalist, "td-finalist", finalist?.name ?? "—"));

            var resultLbl = new Label
            {
                text = string.IsNullOrWhiteSpace(edition.final_score) ? "—" : edition.final_score
            };
            resultLbl.AddToClassList("td-result");
            row.Add(resultLbl);

            var mvp = edition.mvp_player_id > 0
                ? DatabaseManager.Instance.GetPlayerById(edition.mvp_player_id)
                : null;
            var mvpLbl = new Label
            {
                text = mvp != null ? $"{mvp.first_name} {mvp.last_name}" : "—"
            };
            mvpLbl.AddToClassList("td-mvp");
            row.Add(mvpLbl);

            _cupFinalsHistoryBody.Add(row);
        }
        _cupFinalsHistoryBar = BindScrollBar(_cupFinalsHistoryViewport, _cupFinalsHistoryBody,
            _cupFinalsHistoryTrack, _cupFinalsHistoryThumb);
    }

    // ══ JUGADORES TAB ════════════════════════════════════

    void BuildJugadoresTab()
    {
        var awardsData = DatabaseManager.Instance.GetAwardsRecords();
        awardsData.Reverse();
        _mvpRankingData = awardsData;
        _awardsHistoryData = awardsData;
        RenderMVPRanking();
        RenderAwardsHistory();
    }

    void RenderMVPRanking()
    {
        var mvpCounts = new Dictionary<string, (int count, string teamKeyword)>();
        foreach (var a in _mvpRankingData)
        {
            if (!mvpCounts.ContainsKey(a.mvp))
                mvpCounts[a.mvp] = (0, a.mvp_team_keyword);
            var cur = mvpCounts[a.mvp];
            mvpCounts[a.mvp] = (cur.count + 1, cur.teamKeyword);
        }
        var sorted = mvpCounts.OrderByDescending(kv => kv.Value.count).ToList();

        _mvpRankingBody.Clear();

        if (sorted.Count == 0)
        {
            var emptyLbl = new Label { text = LocalizationService.T("ui.code.palmares_controller.aun_no_hay_mvps_registrados") };
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
        _mvpRankingBar = BindScrollBar(_mvpRankingViewport, _mvpRankingBody, _mvpRankingTrack, _mvpRankingThumb);
    }

    void RenderAwardsHistory()
    {
        _awardsHistoryBody.Clear();

        if (_awardsHistoryData.Count == 0) return;

        foreach (var a in _awardsHistoryData)
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
        _awardsHistoryBar = BindScrollBar(_awardsHistoryViewport, _awardsHistoryBody, _awardsHistoryTrack, _awardsHistoryThumb);
    }

    // ══ QUINTETOS TAB ════════════════════════════════════

    void BuildQuintetosTab()
    {
        var quintetData = DatabaseManager.Instance.GetQuintetRecords();
        quintetData.Reverse();
        _quintetAppearancesData = quintetData;
        _quintetHistoryData = quintetData;
        RenderQuintetAppearances();
        RenderQuintetHistory();
    }

    void RenderQuintetAppearances()
    {
        var appearanceCounts = new Dictionary<string, (int count, string teamKeyword)>();
        foreach (var q in _quintetAppearancesData)
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

        _quintetAppearancesBody.Clear();

        if (sorted.Count == 0)
        {
            var emptyLbl = new Label { text = LocalizationService.T("ui.code.palmares_controller.aun_no_hay_quintetos_registrados") };
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
        _quintetAppearancesBar = BindScrollBar(_quintetAppearancesViewport, _quintetAppearancesBody, _quintetAppearancesTrack, _quintetAppearancesThumb);
    }

    void RenderQuintetHistory()
    {
        _quintetHistoryBody.Clear();

        if (_quintetHistoryData.Count == 0) return;

        foreach (var q in _quintetHistoryData)
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
        _quintetHistoryBar = BindScrollBar(_quintetHistoryViewport, _quintetHistoryBody, _quintetHistoryTrack, _quintetHistoryThumb);
    }

    // ══ ALL-STAR TAB ══════════════════════════════════════

    void BuildAllStarTab()
    {
        var allStarData = DatabaseManager.Instance.GetAllStarRecords(_manager.id);
        allStarData.Sort((a, b) => b.season.CompareTo(a.season));
        _allStarHistoryData = allStarData;
        _allStarAppearancesData = null;
        RenderAllStarAppearances();
        RenderAllStarHistory();
    }

    void RenderAllStarAppearances()
    {
        _allStarAppearancesData = DatabaseManager.Instance.GetAllStarAppearances(_manager.id);
        _allStarAppearancesBody.Clear();

        if (_allStarAppearancesData.Count == 0)
        {
            var emptyLbl = new Label { text = LocalizationService.T("ui.code.palmares_controller.aun_no_hay_all_stars_registrados") };
            emptyLbl.AddToClassList("no-data-cell");
            _allStarAppearancesBody.Add(emptyLbl);
            return;
        }

        for (int i = 0; i < _allStarAppearancesData.Count; i++)
        {
            var a = _allStarAppearancesData[i];
            var row = new VisualElement();
            row.AddToClassList("champ-row");

            var rankLbl = new Label { text = (i + 1).ToString() };
            rankLbl.AddToClassList("champ-rank");
            row.Add(rankLbl);

            var team = _allTeams.Find(t => t.logo == a.team_logo);
            var logo = new VisualElement();
            logo.AddToClassList("champ-logo");
            if (team != null && _logoSprites32.TryGetValue(team.logo, out var sp))
                logo.style.backgroundImage = new StyleBackground(sp);
            row.Add(logo);

            var nameLbl = new Label { text = a.player_name };
            nameLbl.AddToClassList("champ-name");
            row.Add(nameLbl);

            var countLbl = new Label { text = a.appearances.ToString() };
            countLbl.AddToClassList("champ-count");
            row.Add(countLbl);

            _allStarAppearancesBody.Add(row);
        }
        _allStarAppearancesBar = BindScrollBar(_allStarAppearancesViewport, _allStarAppearancesBody, _allStarAppearancesTrack, _allStarAppearancesThumb);
    }

    void RenderAllStarHistory()
    {
        _allStarHistoryBody.Clear();

        if (_allStarHistoryData.Count == 0) return;

        _logoSprites32.TryGetValue("all-star-game", out var allStarSprite);

        foreach (var a in _allStarHistoryData)
        {
            var row = new VisualElement();
            row.AddToClassList("palmares-data-row");

            bool eastWon = a.east_score > a.west_score;

            var seasonLbl = new Label { text = a.season };
            seasonLbl.AddToClassList("td-season");
            row.Add(seasonLbl);

            row.Add(CreateAllStarCell(eastWon ? "Conferencia Este" : "Conferencia Oeste", "td-winner", allStarSprite));
            row.Add(CreateAllStarCell(eastWon ? "Conferencia Oeste" : "Conferencia Este", "td-loser", allStarSprite));

            var resultLbl = new Label { text = eastWon ? $"{a.east_score}-{a.west_score}" : $"{a.west_score}-{a.east_score}" };
            resultLbl.AddToClassList("td-result");
            row.Add(resultLbl);

            var mvpLbl = new Label { text = a.mvp };
            mvpLbl.AddToClassList("td-mvp");
            row.Add(mvpLbl);

            _allStarHistoryBody.Add(row);
        }
        _allStarHistoryBar = BindScrollBar(_allStarHistoryViewport, _allStarHistoryBody, _allStarHistoryTrack, _allStarHistoryThumb);
    }

    VisualElement CreateAllStarCell(string text, string cellClass, Sprite logo)
    {
        var cell = new VisualElement();
        cell.AddToClassList("cell-with-logo");

        var logoEl = new VisualElement();
        logoEl.AddToClassList("mini-logo");
        if (logo != null)
            logoEl.style.backgroundImage = new StyleBackground(logo);
        cell.Add(logoEl);

        var nameLbl = new Label();
        nameLbl.AddToClassList(cellClass);
        nameLbl.text = text;
        cell.Add(nameLbl);

        return cell;
    }

    // ══ SALÓN DE LA FAMA TAB ════════════════════════════

    void BuildSalonFamaTab()
    {
        var members = DatabaseManager.Instance.GetHoFMembers();
        members.Sort((a, b) =>
        {
            int bySeason = string.Compare(b.induction_season, a.induction_season, System.StringComparison.Ordinal);
            if (bySeason != 0) return bySeason;
            int byRings = b.rings.CompareTo(a.rings);
            if (byRings != 0) return byRings;
            int byPts = b.career_points.CompareTo(a.career_points);
            return byPts != 0 ? byPts : string.Compare(a.FullName, b.FullName, System.StringComparison.Ordinal);
        });
        _hoFData = members;
        _milestoneCounts = DatabaseManager.Instance.GetMilestoneCountsByPlayer();
        RenderHoF();
    }

    void RenderHoF()
    {
        _hoFBody.Clear();

        if (_hoFData.Count == 0)
        {
            var emptyLbl = new Label { text = LocalizationService.T("ui.code.palmares_controller.todavia_no_hay_miembros_en_el_salon_de_l_8ecdbf02") };
            emptyLbl.AddToClassList("no-data-cell");
            _hoFBody.Add(emptyLbl);
            return;
        }

        for (int i = 0; i < _hoFData.Count; i++)
        {
            var m = _hoFData[i];
            var row = new VisualElement();
            row.AddToClassList("palmares-data-row");

            row.Add(MakeHoFCell((i + 1).ToString(), "td-hof-rank"));
            var nameCell = MakeHoFCell(m.FullName, "td-hof-name");
            // Etiqueta ligera de legado: nº de hitos de carrera registrados (E1).
            if (m.player_id > 0 && _milestoneCounts.TryGetValue(m.player_id, out var mCount) && mCount > 0)
                nameCell.tooltip = $"{mCount} hito(s) de carrera";
            row.Add(nameCell);
            row.Add(MakeHoFCell(PositionCodes.GetName(m.position), "td-hof-pos"));
            row.Add(MakeHoFCell(GetHoFTeamName(m), "td-hof-team"));
            row.Add(MakeHoFCell(m.rings.ToString(), "td-hof-rings"));
            row.Add(MakeHoFCell(m.finals_mvps.ToString(), "td-hof-fmvp"));
            row.Add(MakeHoFCell(FormatThousands(m.career_points), "td-hof-points"));
            row.Add(MakeHoFCell(FormatThousands(m.career_rebounds), "td-hof-rebounds"));
            row.Add(MakeHoFCell(FormatThousands(m.career_assists), "td-hof-assists"));
            row.Add(MakeHoFCell(m.induction_season, "td-hof-season"));

            _hoFBody.Add(row);
        }
        _hoFBar = BindScrollBar(_hoFViewport, _hoFBody, _hoFTrack, _hoFThumb);
    }

    static readonly System.Globalization.CultureInfo _hoFCulture = new("es-ES");

    string GetHoFTeamName(HallOfFameData m)
    {
        if (!string.IsNullOrEmpty(m.team_abbreviation) && _allTeams != null)
        {
            var team = _allTeams.Find(t =>
                string.Equals(t.abbreviation, m.team_abbreviation, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.logo, m.team_abbreviation, System.StringComparison.OrdinalIgnoreCase));
            if (team != null) return team.name;
        }
        return m.team_abbreviation;
    }

    string FormatThousands(int value)
    {
        return value.ToString("N0", _hoFCulture);
    }

    Label MakeHoFCell(string text, string cssClass)
    {
        var lbl = new Label { text = text };
        lbl.AddToClassList(cssClass);
        return lbl;
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

    // ══ SCROLLBAR ════════════════════════════════════════

    class ScrollBar : CustomScrollbar.Handle { }

    ScrollBar BindScrollBar(VisualElement viewport, VisualElement content,
                                VisualElement track, VisualElement thumb)
        => CustomScrollbar.Bind<ScrollBar>(viewport, content, track, thumb);






}
