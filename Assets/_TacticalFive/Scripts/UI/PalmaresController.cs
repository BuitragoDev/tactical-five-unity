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
    private Button _tabRivalidades;
    private VisualElement _tabContentRivalidades;
    private VisualElement _dinastiasBody, _rivalidadesBody;
    private CustomScrollbar.Handle _dinastiasBar, _rivalidadesBar;
    private VisualElement _tabContentEquipos;
    private VisualElement _tabContentJugadores;
    private VisualElement _tabContentQuintetos;
    private VisualElement _tabContentAllStar;
    private VisualElement _tabContentSalonFama;

    // ── Data ──
    private VisualElement _titlesRankingBody;
    private List<FinalsRecord> _titlesRankingData;
    private VisualElement _finalsHistoryBody;
    private List<FinalsRecord> _finalsHistoryData;
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
    private VisualElement _mvpRankingViewport, _mvpRankingTrack, _mvpRankingThumb;
    private VisualElement _awardsHistoryViewport, _awardsHistoryTrack, _awardsHistoryThumb;
    private VisualElement _quintetAppearancesViewport, _quintetAppearancesTrack, _quintetAppearancesThumb;
    private VisualElement _quintetHistoryViewport, _quintetHistoryTrack, _quintetHistoryThumb;
    private VisualElement _allStarAppearancesViewport, _allStarAppearancesTrack, _allStarAppearancesThumb;
    private VisualElement _allStarHistoryViewport, _allStarHistoryTrack, _allStarHistoryThumb;
    private VisualElement _hoFViewport, _hoFTrack, _hoFThumb;

    private ScrollBar _titlesRankingBar, _finalsHistoryBar, _mvpRankingBar, _awardsHistoryBar;
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
        _tabJugadores = _root.Q<Button>("TabJugadores");
        _tabQuintetos = _root.Q<Button>("TabQuintetos");
        _tabAllStar = _root.Q<Button>("TabAllStar");
        _tabSalonFama = _root.Q<Button>("TabSalonFama");

        _tabRivalidades = _root.Q<Button>("TabRivalidades");

        _tabContentEquipos = _root.Q<VisualElement>("TabContentEquipos");
        _tabContentJugadores = _root.Q<VisualElement>("TabContentJugadores");
        _tabContentQuintetos = _root.Q<VisualElement>("TabContentQuintetos");
        _tabContentAllStar = _root.Q<VisualElement>("TabContentAllStar");
        _tabContentSalonFama = _root.Q<VisualElement>("TabContentSalonFama");

        _tabContentRivalidades = _root.Q<VisualElement>("TabContentRivalidades");
        _dinastiasBody = _root.Q<VisualElement>("DinastiasBody");
        _rivalidadesBody = _root.Q<VisualElement>("RivalidadesBody");

        _titlesRankingBody = _root.Q<VisualElement>("TitlesRankingBody");
        _finalsHistoryBody = _root.Q<VisualElement>("FinalsHistoryBody");
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
        _tabJugadores?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("jugadores"); });
        _tabQuintetos?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("quintetos"); });
        _tabAllStar?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("allstar"); });
        _tabSalonFama?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("salonfama"); });

        _tabRivalidades?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("rivalidades"); });
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

        _tabRivalidades?.RemoveFromClassList("palmares-tab--active");

        _tabContentEquipos.style.display = DisplayStyle.None;
        _tabContentJugadores.style.display = DisplayStyle.None;
        _tabContentQuintetos.style.display = DisplayStyle.None;
        _tabContentAllStar.style.display = DisplayStyle.None;
        _tabContentSalonFama.style.display = DisplayStyle.None;

        if (_tabContentRivalidades != null) _tabContentRivalidades.style.display = DisplayStyle.None;

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
            case "rivalidades":
                _tabRivalidades.AddToClassList("palmares-tab--active");
                _tabContentRivalidades.style.display = DisplayStyle.Flex;
                BuildRivalidadesTab();
                break;
        }
    }

    // ── PESTAÑA RIVALIDADES (dinastías + series de playoffs) ──

    void BuildRivalidadesTab()
    {
        if (_dinastiasBody == null || _rivalidadesBody == null || _manager == null || _myTeam == null) return;
        _dinastiasBody.Clear();
        _rivalidadesBody.Clear();

        BuildDinastiasList();
        BuildRivalidadesList();

        _dinastiasBar = CustomScrollbar.Bind(
            _root.Q<VisualElement>("DinastiasViewport"), _dinastiasBody,
            _root.Q<VisualElement>("DinastiasTrack"), _root.Q<VisualElement>("DinastiasThumb"));
        _rivalidadesBar = CustomScrollbar.Bind(
            _root.Q<VisualElement>("RivalidadesViewport"), _rivalidadesBody,
            _root.Q<VisualElement>("RivalidadesTrack"), _root.Q<VisualElement>("RivalidadesThumb"));
    }

    /// <summary>Ranking de prestigio de la liga (títulos/finales/conferencias por equipo).</summary>
    void BuildDinastiasList()
    {
        var ranking = DynastyHelper.LeagueRanking(DatabaseManager.Instance.GetAllSeasonRecords(), 12);
        if (ranking.Count == 0)
        {
            _dinastiasBody.Add(EmptyRivalLabel("Todavía no hay títulos registrados."));
            return;
        }

        foreach (var p in ranking)
        {
            var team = _allTeams?.FirstOrDefault(t => t.id == p.teamId);

            var row = new VisualElement();
            row.AddToClassList("rival-row");

            var logo = new VisualElement();
            logo.AddToClassList("rival-logo");
            if (team != null && _logoSprites32.TryGetValue(team.logo, out var sp))
                logo.style.backgroundImage = new StyleBackground(sp);
            row.Add(logo);

            var col = new VisualElement();
            col.AddToClassList("rival-col");

            var name = new Label(team?.name ?? $"Equipo {p.teamId}");
            name.AddToClassList("rival-title");
            col.Add(name);

            var meta = new Label($"{p.titles} títulos · {p.finals} finales · {p.conferenceTitles} conferencias");
            meta.AddToClassList("rival-meta");
            col.Add(meta);
            row.Add(col);

            var badge = new Label(p.isDynasty ? "DINASTÍA" : $"PRESTIGIO {p.prestige}");
            badge.AddToClassList("rival-badge");
            if (p.isDynasty) badge.AddToClassList("rival-badge--hot");
            row.Add(badge);

            _dinastiasBody.Add(row);
        }
    }

    /// <summary>Rivales de mi equipo en playoffs (balance de series y último cruce).</summary>
    void BuildRivalidadesList()
    {
        var series = DatabaseManager.Instance.GetPlayoffSeriesOfTeam(_manager.id, _myTeam.id);
        var rivals = RivalryHelper.RivalsOf(series, _myTeam.id, _season?.id ?? 0);

        if (rivals.Count == 0)
        {
            _rivalidadesBody.Add(EmptyRivalLabel("Sin cruces de playoffs todavía: las rivalidades aparecerán aquí."));
            return;
        }

        var seasons = DatabaseManager.Instance.GetAllSeasons();

        foreach (var r in rivals)
        {
            var team = _allTeams?.FirstOrDefault(t => t.id == r.opponentId);

            var row = new VisualElement();
            row.AddToClassList("rival-row");

            var logo = new VisualElement();
            logo.AddToClassList("rival-logo");
            if (team != null && _logoSprites32.TryGetValue(team.logo, out var sp))
                logo.style.backgroundImage = new StyleBackground(sp);
            row.Add(logo);

            var col = new VisualElement();
            col.AddToClassList("rival-col");

            var name = new Label(team?.name ?? $"Equipo {r.opponentId}");
            name.AddToClassList("rival-title");
            col.Add(name);

            string lastYear = "";
            var lastSeason = seasons?.FirstOrDefault(x => x.id == r.lastSeasonId);
            if (lastSeason != null)
                lastYear = $" {lastSeason.year_start}-{lastSeason.year_end % 100:D2}";

            var meta = new Label(
                $"{r.series} series · {r.wins}-{r.losses}"
                + (string.IsNullOrEmpty(r.lastStage) ? "" : $" · último: {StageLabel(r.lastStage)}{lastYear} ({RivalryResultLabel(r.lastWon)})"));
            meta.AddToClassList("rival-meta");
            col.Add(meta);
            row.Add(col);

            var badge = new Label(IntensityLabel(r.intensity));
            badge.AddToClassList("rival-badge");
            if (r.intensity >= 30) badge.AddToClassList("rival-badge--hot");
            row.Add(badge);

            _rivalidadesBody.Add(row);
        }
    }

    static string StageLabel(string stage) => stage switch
    {
        "r1" => "1ª ronda",
        "r2" => "2ª ronda",
        "r3" => "final de conferencia",
        "r4" => "FINALES",
        _ => stage ?? ""
    };

    static string RivalryResultLabel(bool won) => won ? "ganamos" : "eliminados";

    static string IntensityLabel(int intensity)
        => intensity >= 30 ? "RIVALIDAD FUERTE" : intensity >= 15 ? "RIVALIDAD" : "CRUCE";

    static Label EmptyRivalLabel(string text)
    {
        var lbl = new Label(text);
        lbl.AddToClassList("rival-empty");
        return lbl;
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
            var emptyLbl = new Label { text = "Aún no hay All-Stars registrados" };
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
            var emptyLbl = new Label { text = "Todavía no hay miembros en el Salón de la Fama" };
            emptyLbl.AddToClassList("no-data-cell");
            _hoFBody.Add(emptyLbl);
            return;
        }

        for (int i = 0; i < _hoFData.Count; i++)
        {
            var m = _hoFData[i];
            var row = new VisualElement();
            row.AddToClassList("palmares-data-row");

            row.Add(MakeHoFCell((i + 1).ToString(), "td-hof-rank", true));
            var nameCell = MakeHoFCell(m.FullName, "td-hof-name", false);
            // Etiqueta ligera de legado: nº de hitos de carrera registrados (E1).
            if (m.player_id > 0 && _milestoneCounts.TryGetValue(m.player_id, out var mCount) && mCount > 0)
                nameCell.tooltip = $"{mCount} hito(s) de carrera";
            row.Add(nameCell);
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

    // ══ SCROLLBAR ════════════════════════════════════════

    class ScrollBar : IScrollBarHandle
    {
        public VisualElement Viewport;
        public VisualElement Content;
        public VisualElement Track;
        public VisualElement Thumb;
        public float ScrollY;
        public bool Dragging;
        public float StartPointerY;
        public float StartScrollY;
    }

    ScrollBar BindScrollBar(VisualElement viewport, VisualElement content,
                            VisualElement track, VisualElement thumb)
    {
        if (viewport == null || content == null || track == null || thumb == null) return null;

        // Ya enlazada en esta visita (el viewport persiste): no registrar los
        // handlers otra vez, que se acumulaban al reconstruir la lista.
        if (ScrollBarBinder.TryGet<ScrollBar>(viewport, out var bound)) return bound;


        viewport.style.overflow = Overflow.Hidden;
        viewport.style.flexDirection = FlexDirection.Column;

        var bar = new ScrollBar
        {
            Viewport = viewport,
            Content = content,
            Track = track,
            Thumb = thumb
        };

        viewport.RegisterCallback<WheelEvent>(evt =>
        {
            float max = GetScrollMax(bar);
            if (max <= 0f) return;
            bar.ScrollY = Mathf.Clamp(bar.ScrollY + evt.delta.y * 30f, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerDownEvent>(evt =>
        {
            bar.Dragging = true;
            bar.StartPointerY = evt.position.y;
            bar.StartScrollY = bar.ScrollY;
            thumb.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!bar.Dragging) return;
            float max = GetScrollMax(bar);
            float travel = track.layout.height - thumb.layout.height;
            if (max <= 0f || travel <= 0f) return;
            float delta = evt.position.y - bar.StartPointerY;
            bar.ScrollY = Mathf.Clamp(bar.StartScrollY + delta / travel * max, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (bar.Dragging)
            {
                bar.Dragging = false;
                thumb.ReleasePointer(evt.pointerId);
            }
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerCaptureOutEvent>(_ => bar.Dragging = false);

        track.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (bar.Dragging) return;
            float max = GetScrollMax(bar);
            if (max <= 0f) return;
            float page = viewport.layout.height;
            float y = evt.localPosition.y;
            float thumbTop = thumb.layout.y;
            float thumbBottom = thumbTop + thumb.layout.height;
            float target = y < thumbTop ? bar.ScrollY - page
                         : (y > thumbBottom ? bar.ScrollY + page : bar.ScrollY);
            bar.ScrollY = Mathf.Clamp(target, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        CursorManager.Instance?.RegisterHandCursor(thumb);
        CursorManager.Instance?.RegisterHandCursor(track);

        content.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));
        viewport.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));

        ApplyScroll(bar);
        ScrollBarBinder.Remember(viewport, bar);
        return bar;
    }

    static float GetScrollMax(ScrollBar bar)
    {
        if (bar == null || bar.Content == null || bar.Viewport == null) return 0f;
        float contentHeight = bar.Content.layout.height;
        float viewportHeight = bar.Viewport.layout.height;
        return Mathf.Max(0f, contentHeight - viewportHeight);
    }

    void ClampAndApply(ScrollBar bar)
    {
        if (bar == null) return;
        bar.ScrollY = Mathf.Clamp(bar.ScrollY, 0f, GetScrollMax(bar));
        ApplyScroll(bar);
    }

    void ApplyScroll(ScrollBar bar)
    {
        if (bar == null || bar.Content == null) return;

        bar.Content.style.top = -bar.ScrollY;

        float trackHeight = bar.Track.layout.height;
        float contentHeight = bar.Content.layout.height;
        float viewportHeight = bar.Viewport.layout.height;
        if (trackHeight <= 0f || contentHeight <= 0f) return;

        float max = Mathf.Max(0f, contentHeight - viewportHeight);
        float thumbHeight = max <= 0f ? trackHeight : Mathf.Max(36f, trackHeight * viewportHeight / contentHeight);
        thumbHeight = Mathf.Min(thumbHeight, trackHeight);

        float travel = trackHeight - thumbHeight;
        float t = max <= 0f ? 0f : Mathf.Clamp01(bar.ScrollY / max);

        bar.Thumb.style.height = thumbHeight;
        bar.Thumb.style.top = travel * t;
    }
}
