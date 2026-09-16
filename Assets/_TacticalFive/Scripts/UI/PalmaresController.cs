using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class PalmaresController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Palmares;
    private Button _tabEquipos;
    private Button _tabJugadores;
    private Button _tabQuintetos;
    private Button _tabAllStar;
    private Button _tabSalonFama;
    private VisualElement _tabContentEquipos;
    private VisualElement _tabContentJugadores;
    private VisualElement _tabContentQuintetos;
    private VisualElement _tabContentAllStar;
    private VisualElement _tabContentSalonFama;

    // ── Pagination state ──
    const int PAGE_SIZE = 10;

    // Titles ranking
    private VisualElement _titlesRankingBody;
    private List<FinalsRecord> _titlesRankingData;
    private int _titlesRankingPage;

    // Finals history
    private VisualElement _finalsHistoryBody;
    private List<FinalsRecord> _finalsHistoryData;
    private int _finalsHistoryPage;

    // MVP ranking
    private VisualElement _mvpRankingBody;
    private List<AwardsRecord> _mvpRankingData;
    private int _mvpRankingPage;

    // Awards history
    private VisualElement _awardsHistoryBody;
    private List<AwardsRecord> _awardsHistoryData;
    private int _awardsHistoryPage;

    // Quintet appearances
    private VisualElement _quintetAppearancesBody;
    private List<QuintetRecord> _quintetAppearancesData;
    private int _quintetAppearancesPage;

    // Quintet history
    private VisualElement _quintetHistoryBody;
    private List<QuintetRecord> _quintetHistoryData;
    private int _quintetHistoryPage;

    // All-Star appearances
    private VisualElement _allStarAppearancesBody;
    private List<AllStarAppearancesData> _allStarAppearancesData;
    private int _allStarAppearancesPage;

    // All-Star history
    private VisualElement _allStarHistoryBody;
    private List<AllStarRecord> _allStarHistoryData;
    private int _allStarHistoryPage;

    // Hall of Fame
    private VisualElement _hoFBody;
    private List<HallOfFameData> _hoFData;
    private int _hoFPage;

    // Paginator elements
    private Button _titlesRankingPrev, _titlesRankingNext;
    private Label _titlesRankingPageInfo;
    private Button _finalsHistoryPrev, _finalsHistoryNext;
    private Label _finalsHistoryPageInfo;
    private Button _mvpRankingPrev, _mvpRankingNext;
    private Label _mvpRankingPageInfo;
    private Button _awardsHistoryPrev, _awardsHistoryNext;
    private Label _awardsHistoryPageInfo;
    private Button _quintetAppearancesPrev, _quintetAppearancesNext;
    private Label _quintetAppearancesPageInfo;
    private Button _quintetHistoryPrev, _quintetHistoryNext;
    private Label _quintetHistoryPageInfo;
    private Button _allStarAppearancesPrev, _allStarAppearancesNext;
    private Label _allStarAppearancesPageInfo;
    private Button _allStarHistoryPrev, _allStarHistoryNext;
    private Label _allStarHistoryPageInfo;
    private Button _hoFPrev, _hoFNext;
    private Label _hoFPageInfo;

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
        _tabJugadores = _root.Q<Button>("TabJugadores");
        _tabQuintetos = _root.Q<Button>("TabQuintetos");
        _tabAllStar = _root.Q<Button>("TabAllStar");
        _tabSalonFama = _root.Q<Button>("TabSalonFama");

        _tabContentEquipos = _root.Q<VisualElement>("TabContentEquipos");
        _tabContentJugadores = _root.Q<VisualElement>("TabContentJugadores");
        _tabContentQuintetos = _root.Q<VisualElement>("TabContentQuintetos");
        _tabContentAllStar = _root.Q<VisualElement>("TabContentAllStar");
        _tabContentSalonFama = _root.Q<VisualElement>("TabContentSalonFama");

        _titlesRankingBody = _root.Q<VisualElement>("TitlesRankingBody");
        _finalsHistoryBody = _root.Q<VisualElement>("FinalsHistoryBody");
        _mvpRankingBody = _root.Q<VisualElement>("MVPRankingBody");
        _awardsHistoryBody = _root.Q<VisualElement>("AwardsHistoryBody");
        _quintetAppearancesBody = _root.Q<VisualElement>("QuintetAppearancesBody");
        _quintetHistoryBody = _root.Q<VisualElement>("QuintetHistoryBody");
        _allStarAppearancesBody = _root.Q<VisualElement>("AllStarAppearancesBody");
        _allStarHistoryBody = _root.Q<VisualElement>("AllStarHistoryBody");
        _hoFBody = _root.Q<VisualElement>("HallOfFameBody");

        _titlesRankingPrev = _root.Q<Button>("TitlesRankingPrev");
        _titlesRankingNext = _root.Q<Button>("TitlesRankingNext");
        _titlesRankingPageInfo = _root.Q<Label>("TitlesRankingPageInfo");
        _finalsHistoryPrev = _root.Q<Button>("FinalsHistoryPrev");
        _finalsHistoryNext = _root.Q<Button>("FinalsHistoryNext");
        _finalsHistoryPageInfo = _root.Q<Label>("FinalsHistoryPageInfo");
        _mvpRankingPrev = _root.Q<Button>("MVPRankingPrev");
        _mvpRankingNext = _root.Q<Button>("MVPRankingNext");
        _mvpRankingPageInfo = _root.Q<Label>("MVPRankingPageInfo");
        _awardsHistoryPrev = _root.Q<Button>("AwardsHistoryPrev");
        _awardsHistoryNext = _root.Q<Button>("AwardsHistoryNext");
        _awardsHistoryPageInfo = _root.Q<Label>("AwardsHistoryPageInfo");
        _quintetAppearancesPrev = _root.Q<Button>("QuintetAppearancesPrev");
        _quintetAppearancesNext = _root.Q<Button>("QuintetAppearancesNext");
        _quintetAppearancesPageInfo = _root.Q<Label>("QuintetAppearancesPageInfo");
        _quintetHistoryPrev = _root.Q<Button>("QuintetHistoryPrev");
        _quintetHistoryNext = _root.Q<Button>("QuintetHistoryNext");
        _quintetHistoryPageInfo = _root.Q<Label>("QuintetHistoryPageInfo");
        _allStarAppearancesPrev = _root.Q<Button>("AllStarAppearancesPrev");
        _allStarAppearancesNext = _root.Q<Button>("AllStarAppearancesNext");
        _allStarAppearancesPageInfo = _root.Q<Label>("AllStarAppearancesPageInfo");
        _allStarHistoryPrev = _root.Q<Button>("AllStarHistoryPrev");
        _allStarHistoryNext = _root.Q<Button>("AllStarHistoryNext");
        _allStarHistoryPageInfo = _root.Q<Label>("AllStarHistoryPageInfo");
        _hoFPrev = _root.Q<Button>("HallOfFamePrev");
        _hoFNext = _root.Q<Button>("HallOfFameNext");
        _hoFPageInfo = _root.Q<Label>("HallOfFamePageInfo");
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

        var logos32 = Resources.LoadAll<Sprite>("Teams/Logos/32x32");
        foreach (var s in logos32) _logoSprites32[s.name] = s;

        var logos64 = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _seasonRecords = DatabaseManager.Instance.GetAllSeasonRecords(_season?.id ?? 0);
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _tabEquipos?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("equipos"); });
        _tabJugadores?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("jugadores"); });
        _tabQuintetos?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("quintetos"); });
        _tabAllStar?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("allstar"); });
        _tabSalonFama?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("salonfama"); });

        _titlesRankingPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _titlesRankingPage--; RenderTitlesRanking(); });
        _titlesRankingNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _titlesRankingPage++; RenderTitlesRanking(); });
        _finalsHistoryPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _finalsHistoryPage--; RenderFinalsHistory(); });
        _finalsHistoryNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _finalsHistoryPage++; RenderFinalsHistory(); });
        _mvpRankingPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _mvpRankingPage--; RenderMVPRanking(); });
        _mvpRankingNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _mvpRankingPage++; RenderMVPRanking(); });
        _awardsHistoryPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _awardsHistoryPage--; RenderAwardsHistory(); });
        _awardsHistoryNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _awardsHistoryPage++; RenderAwardsHistory(); });
        _quintetAppearancesPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _quintetAppearancesPage--; RenderQuintetAppearances(); });
        _quintetAppearancesNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _quintetAppearancesPage++; RenderQuintetAppearances(); });
        _quintetHistoryPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _quintetHistoryPage--; RenderQuintetHistory(); });
        _quintetHistoryNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _quintetHistoryPage++; RenderQuintetHistory(); });
        _allStarAppearancesPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _allStarAppearancesPage--; RenderAllStarAppearances(); });
        _allStarAppearancesNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _allStarAppearancesPage++; RenderAllStarAppearances(); });
        _allStarHistoryPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _allStarHistoryPage--; RenderAllStarHistory(); });
        _allStarHistoryNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _allStarHistoryPage++; RenderAllStarHistory(); });
        _hoFPrev?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _hoFPage--; RenderHoF(); });
        _hoFNext?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _hoFPage++; RenderHoF(); });
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Palmares] RefreshHeader error: {ex.Message}"); }
        ShowTab("equipos");
    }

    void ShowTab(string tab)
    {
        _tabEquipos.RemoveFromClassList("palmares-tab--active");
        _tabJugadores.RemoveFromClassList("palmares-tab--active");
        _tabQuintetos.RemoveFromClassList("palmares-tab--active");
        _tabAllStar.RemoveFromClassList("palmares-tab--active");
        _tabSalonFama.RemoveFromClassList("palmares-tab--active");

        _tabContentEquipos.style.display = DisplayStyle.None;
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

    // ══ PAGINATION HELPERS ════════════════════════════════

    void UpdatePaginator(Button prev, Button next, Label pageInfo, int currentPage, int totalCount)
    {
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalCount / PAGE_SIZE));
        int displayPage = Mathf.Clamp(currentPage + 1, 1, totalPages);
        pageInfo.text = $"{displayPage} / {totalPages}";
        prev.SetEnabled(currentPage > 0);
        next.SetEnabled(currentPage < totalPages - 1);
    }

    List<T> GetPage<T>(List<T> data, int page)
    {
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)data.Count / PAGE_SIZE));
        int p = Mathf.Clamp(page, 0, totalPages - 1);
        return data.Skip(p * PAGE_SIZE).Take(PAGE_SIZE).ToList();
    }

    void SetPaginatorVisibility(VisualElement paginator, bool hasData)
    {
        if (paginator != null)
            paginator.style.display = hasData ? DisplayStyle.Flex : DisplayStyle.None;
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
        _titlesRankingData = finalsData;
        _titlesRankingPage = 0;
        _finalsHistoryData = finalsData;
        _finalsHistoryPage = 0;
        RenderTitlesRanking();
        RenderFinalsHistory();
    }

    void RenderTitlesRanking()
    {
        var sorted = new List<KeyValuePair<string, (int count, TeamData team)>>();
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
        sorted = champCounts.OrderByDescending(kv => kv.Value.count).ToList();

        _titlesRankingBody.Clear();
        SetPaginatorVisibility(_root.Q<VisualElement>("TitlesRankingPaginator"), sorted.Count > 0);

        if (sorted.Count == 0)
        {
            var emptyLbl = new Label { text = "Aún no hay campeonatos registrados" };
            emptyLbl.AddToClassList("no-data-cell");
            _titlesRankingBody.Add(emptyLbl);
            UpdatePaginator(_titlesRankingPrev, _titlesRankingNext, _titlesRankingPageInfo, 0, 0);
            return;
        }

        var page = GetPage(sorted, _titlesRankingPage);
        int startIdx = _titlesRankingPage * PAGE_SIZE;
        for (int i = 0; i < page.Count; i++)
        {
            var kv = page[i];
            var row = new VisualElement();
            row.AddToClassList("champ-row");

            var rankLbl = new Label { text = (startIdx + i + 1).ToString() };
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
        UpdatePaginator(_titlesRankingPrev, _titlesRankingNext, _titlesRankingPageInfo, _titlesRankingPage, sorted.Count);
    }

    void RenderFinalsHistory()
    {
        _finalsHistoryBody.Clear();
        SetPaginatorVisibility(_root.Q<VisualElement>("FinalsHistoryPaginator"), _finalsHistoryData.Count > 0);

        if (_finalsHistoryData.Count == 0)
        {
            UpdatePaginator(_finalsHistoryPrev, _finalsHistoryNext, _finalsHistoryPageInfo, 0, 0);
            return;
        }

        var page = GetPage(_finalsHistoryData, _finalsHistoryPage);
        int startIdx = _finalsHistoryPage * PAGE_SIZE;
        foreach (var f in page)
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
        UpdatePaginator(_finalsHistoryPrev, _finalsHistoryNext, _finalsHistoryPageInfo, _finalsHistoryPage, _finalsHistoryData.Count);
    }

    // ══ JUGADORES TAB ════════════════════════════════════

    void BuildJugadoresTab()
    {
        var awardsData = DatabaseManager.Instance.GetAwardsRecords();
        awardsData.Reverse();
        _mvpRankingData = awardsData;
        _mvpRankingPage = 0;
        _awardsHistoryData = awardsData;
        _awardsHistoryPage = 0;
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
        SetPaginatorVisibility(_root.Q<VisualElement>("MVPRankingPaginator"), sorted.Count > 0);

        if (sorted.Count == 0)
        {
            var emptyLbl = new Label { text = "Aún no hay MVPs registrados" };
            emptyLbl.AddToClassList("no-data-cell");
            _mvpRankingBody.Add(emptyLbl);
            UpdatePaginator(_mvpRankingPrev, _mvpRankingNext, _mvpRankingPageInfo, 0, 0);
            return;
        }

        var page = GetPage(sorted, _mvpRankingPage);
        int startIdx = _mvpRankingPage * PAGE_SIZE;
        for (int i = 0; i < page.Count; i++)
        {
            var kv = page[i];
            var row = new VisualElement();
            row.AddToClassList("champ-row");

            var rankLbl = new Label { text = (startIdx + i + 1).ToString() };
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
        UpdatePaginator(_mvpRankingPrev, _mvpRankingNext, _mvpRankingPageInfo, _mvpRankingPage, sorted.Count);
    }

    void RenderAwardsHistory()
    {
        _awardsHistoryBody.Clear();
        SetPaginatorVisibility(_root.Q<VisualElement>("AwardsHistoryPaginator"), _awardsHistoryData.Count > 0);

        if (_awardsHistoryData.Count == 0)
        {
            UpdatePaginator(_awardsHistoryPrev, _awardsHistoryNext, _awardsHistoryPageInfo, 0, 0);
            return;
        }

        var page = GetPage(_awardsHistoryData, _awardsHistoryPage);
        foreach (var a in page)
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
        UpdatePaginator(_awardsHistoryPrev, _awardsHistoryNext, _awardsHistoryPageInfo, _awardsHistoryPage, _awardsHistoryData.Count);
    }

    // ══ QUINTETOS TAB ════════════════════════════════════

    void BuildQuintetosTab()
    {
        var quintetData = DatabaseManager.Instance.GetQuintetRecords();
        quintetData.Reverse();
        _quintetAppearancesData = quintetData;
        _quintetAppearancesPage = 0;
        _quintetHistoryData = quintetData;
        _quintetHistoryPage = 0;
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
        SetPaginatorVisibility(_root.Q<VisualElement>("QuintetAppearancesPaginator"), sorted.Count > 0);

        if (sorted.Count == 0)
        {
            var emptyLbl = new Label { text = "Aún no hay quintetos registrados" };
            emptyLbl.AddToClassList("no-data-cell");
            _quintetAppearancesBody.Add(emptyLbl);
            UpdatePaginator(_quintetAppearancesPrev, _quintetAppearancesNext, _quintetAppearancesPageInfo, 0, 0);
            return;
        }

        var page = GetPage(sorted, _quintetAppearancesPage);
        int startIdx = _quintetAppearancesPage * PAGE_SIZE;
        for (int i = 0; i < page.Count; i++)
        {
            var kv = page[i];
            var row = new VisualElement();
            row.AddToClassList("champ-row");

            var rankLbl = new Label { text = (startIdx + i + 1).ToString() };
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
        UpdatePaginator(_quintetAppearancesPrev, _quintetAppearancesNext, _quintetAppearancesPageInfo, _quintetAppearancesPage, sorted.Count);
    }

    void RenderQuintetHistory()
    {
        _quintetHistoryBody.Clear();
        SetPaginatorVisibility(_root.Q<VisualElement>("QuintetHistoryPaginator"), _quintetHistoryData.Count > 0);

        if (_quintetHistoryData.Count == 0)
        {
            UpdatePaginator(_quintetHistoryPrev, _quintetHistoryNext, _quintetHistoryPageInfo, 0, 0);
            return;
        }

        var page = GetPage(_quintetHistoryData, _quintetHistoryPage);
        foreach (var q in page)
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
        UpdatePaginator(_quintetHistoryPrev, _quintetHistoryNext, _quintetHistoryPageInfo, _quintetHistoryPage, _quintetHistoryData.Count);
    }

    // ══ ALL-STAR TAB ══════════════════════════════════════

    void BuildAllStarTab()
    {
        var allStarData = DatabaseManager.Instance.GetAllStarRecords(_manager.id);
        allStarData.Sort((a, b) => b.season.CompareTo(a.season));
        _allStarHistoryData = allStarData;
        _allStarHistoryPage = 0;
        _allStarAppearancesData = null;
        _allStarAppearancesPage = 0;
        RenderAllStarAppearances();
        RenderAllStarHistory();
    }

    void RenderAllStarAppearances()
    {
        _allStarAppearancesData = DatabaseManager.Instance.GetAllStarAppearances(_manager.id);
        _allStarAppearancesBody.Clear();
        SetPaginatorVisibility(_root.Q<VisualElement>("AllStarAppearancesPaginator"), _allStarAppearancesData.Count > 0);

        if (_allStarAppearancesData.Count == 0)
        {
            var emptyLbl = new Label { text = "Aún no hay All-Stars registrados" };
            emptyLbl.AddToClassList("no-data-cell");
            _allStarAppearancesBody.Add(emptyLbl);
            UpdatePaginator(_allStarAppearancesPrev, _allStarAppearancesNext, _allStarAppearancesPageInfo, 0, 0);
            return;
        }

        var page = GetPage(_allStarAppearancesData, _allStarAppearancesPage);
        int startIdx = _allStarAppearancesPage * PAGE_SIZE;
        for (int i = 0; i < page.Count; i++)
        {
            var a = page[i];
            var row = new VisualElement();
            row.AddToClassList("champ-row");

            var rankLbl = new Label { text = (startIdx + i + 1).ToString() };
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
        UpdatePaginator(_allStarAppearancesPrev, _allStarAppearancesNext, _allStarAppearancesPageInfo, _allStarAppearancesPage, _allStarAppearancesData.Count);
    }

    void RenderAllStarHistory()
    {
        _allStarHistoryBody.Clear();
        SetPaginatorVisibility(_root.Q<VisualElement>("AllStarHistoryPaginator"), _allStarHistoryData.Count > 0);

        if (_allStarHistoryData.Count == 0)
        {
            UpdatePaginator(_allStarHistoryPrev, _allStarHistoryNext, _allStarHistoryPageInfo, 0, 0);
            return;
        }

        _logoSprites32.TryGetValue("all-star-game", out var allStarSprite);

        var page = GetPage(_allStarHistoryData, _allStarHistoryPage);
        foreach (var a in page)
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
        UpdatePaginator(_allStarHistoryPrev, _allStarHistoryNext, _allStarHistoryPageInfo, _allStarHistoryPage, _allStarHistoryData.Count);
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
        _hoFPage = 0;
        RenderHoF();
    }

    void RenderHoF()
    {
        _hoFBody.Clear();
        SetPaginatorVisibility(_root.Q<VisualElement>("HallOfFamePaginator"), _hoFData.Count > 0);

        if (_hoFData.Count == 0)
        {
            var emptyLbl = new Label { text = "Todavía no hay miembros en el Salón de la Fama" };
            emptyLbl.AddToClassList("no-data-cell");
            _hoFBody.Add(emptyLbl);
            UpdatePaginator(_hoFPrev, _hoFNext, _hoFPageInfo, 0, 0);
            return;
        }

        var page = GetPage(_hoFData, _hoFPage);
        int startIdx = _hoFPage * PAGE_SIZE;
        for (int i = 0; i < page.Count; i++)
        {
            var m = page[i];
            var row = new VisualElement();
            row.AddToClassList("palmares-data-row");

            row.Add(MakeHoFCell((startIdx + i + 1).ToString(), "td-hof-rank", true));
            row.Add(MakeHoFCell(m.FullName, "td-hof-name", false));
            row.Add(MakeHoFCell(PositionCodes.GetName(m.position), "td-hof-pos", false));
            row.Add(MakeHoFCell(GetHoFTeamName(m), "td-hof-team", false));
            row.Add(MakeHoFCell(m.rings.ToString(), "td-hof-rings", false));
            row.Add(MakeHoFCell(m.finals_mvps.ToString(), "td-hof-fmvp", false));
            row.Add(MakeHoFCell(FormatThousands(m.career_points), "td-hof-points", true));
            row.Add(MakeHoFCell(FormatThousands(m.career_rebounds), "td-hof-rebounds", true));
            row.Add(MakeHoFCell(FormatThousands(m.career_assists), "td-hof-assists", true));
            row.Add(MakeHoFCell(m.induction_season, "td-hof-season", false));

            _hoFBody.Add(row);
        }
        UpdatePaginator(_hoFPrev, _hoFNext, _hoFPageInfo, _hoFPage, _hoFData.Count);
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

    Label MakeHoFCell(string text, string cssClass, bool right)
    {
        var lbl = new Label { text = text };
        lbl.AddToClassList(cssClass);
        if (right) lbl.style.unityTextAlign = TextAnchor.MiddleRight;
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
}
