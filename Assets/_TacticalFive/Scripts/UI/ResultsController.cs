using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class ResultsController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Results;

    // Header
    private VisualElement _resultsHeaderIcon;
    private VisualElement _resultsDateIcon;
    private Button _btnHoy;

    // Date nav
    private Button _btnPrevDate;
    private Button _btnNextDate;
    private Label _currentDateLabel;

    // Results
    private VisualElement _resultsBody;
    private VisualElement _resultsPaginator;

    // Match details modal
    private VisualElement _matchOverlay;
    private Label _matchSubtitle;
    private Label _matchTitle;
    private VisualElement _matchArenaImage;
    private Label _matchArenaName;
    private Label _matchArenaAttendance;
    private VisualElement _matchHomeCol;
    private VisualElement _matchAwayCol;
    private Button _btnMatchCloseFooter;
    private Sprite _defaultArenaSprite;
    private bool _defaultArenaLoaded;

    // Data
    private List<TeamData> _allTeams;
    private List<GameData> _allGames;
    private Dictionary<string, Sprite> _logoSprites64 = new();
    private Dictionary<string, Sprite> _logoSprites80 = new();
    private Dictionary<int, (int wins, int losses)> _teamRecords = new();
    private List<string> _gameDates = new();
    private int _currentDateIndex = 0;

    // Pagination
    private int _currentPage = 0;
    private const int ITEMS_PER_PAGE = 6; // 2 columnas x 3 filas

    // Icons
    private Texture2D _chevronTex;

    protected override void CacheReferences()
    {
        _resultsHeaderIcon = _root.Q<VisualElement>("ResultsScreenIcon");
        _resultsDateIcon = _root.Q<VisualElement>("ResultsDateIcon");
        _btnHoy = _root.Q<Button>("BtnHoy");

        _btnPrevDate = _root.Q<Button>("BtnPrevDate");
        _btnNextDate = _root.Q<Button>("BtnNextDate");
        _currentDateLabel = _root.Q<Label>("CurrentDateLabel");

        _resultsBody = _root.Q<VisualElement>("ResultsBody");
        _resultsPaginator = _root.Q<VisualElement>("ResultsPaginator");

        _matchOverlay = _root.Q<VisualElement>("MatchModalOverlay");
        _matchSubtitle = _root.Q<Label>("MatchModalSubtitle");
        _matchTitle = _root.Q<Label>("MatchModalTitle");
        _matchArenaImage = _root.Q<VisualElement>("MatchArenaImage");
        _matchArenaName = _root.Q<Label>("MatchArenaName");
        _matchArenaAttendance = _root.Q<Label>("MatchArenaAttendance");
        _matchHomeCol = _root.Q<VisualElement>("MatchHomeCol");
        _matchAwayCol = _root.Q<VisualElement>("MatchAwayCol");
        _btnMatchCloseFooter = _root.Q<Button>("BtnMatchCloseFooter");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var logos64 = TeamImages.Logos(64);
        foreach (var s in logos64) _logoSprites64[s.name] = s;

        var logos80 = TeamImages.Logos(80);
        foreach (var s in logos80) _logoSprites80[s.name] = s;

        _chevronTex = Resources.Load<Texture2D>("Icons/right_arrow");
        var headerIconTex = Resources.Load<Texture2D>("Icons/resultados128px");
        if (headerIconTex != null && _resultsHeaderIcon != null)
            _resultsHeaderIcon.style.backgroundImage = new StyleBackground(headerIconTex);
        var dateIconTex = Resources.Load<Texture2D>("Icons/calendario");
        if (dateIconTex != null && _resultsDateIcon != null)
            _resultsDateIcon.style.backgroundImage = new StyleBackground(dateIconTex);

        if (_myTeam == null) return;

        _allTeams = DatabaseManager.Instance.GetAllTeams();

        _allGames = (DatabaseManager.Instance.GetAllGames(_manager.id) ?? new List<GameData>())
            .Where(g => g.game_type != "gleague" && g.game_type != "gleague_playoff")
            .ToList();

        _gameDates = _allGames
            .Select(g => g.game_date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        BuildTeamRecords();

        // Arranca en la fecha actual de la partida.
        GoToToday(false);
    }

    void BuildTeamRecords()
    {
        _teamRecords.Clear();
        if (_allTeams != null)
        {
            foreach (var t in _allTeams)
                _teamRecords[t.id] = (0, 0);
        }

        foreach (var g in _allGames)
        {
            if (g.is_played != 1 || g.game_type != "regular") continue;
            if (g.home_score == g.away_score) continue;

            if (!_teamRecords.ContainsKey(g.home_team_id)) _teamRecords[g.home_team_id] = (0, 0);
            if (!_teamRecords.ContainsKey(g.away_team_id)) _teamRecords[g.away_team_id] = (0, 0);

            if (g.home_score > g.away_score)
            {
                _teamRecords[g.home_team_id] = (_teamRecords[g.home_team_id].wins + 1, _teamRecords[g.home_team_id].losses);
                _teamRecords[g.away_team_id] = (_teamRecords[g.away_team_id].wins, _teamRecords[g.away_team_id].losses + 1);
            }
            else
            {
                _teamRecords[g.home_team_id] = (_teamRecords[g.home_team_id].wins, _teamRecords[g.home_team_id].losses + 1);
                _teamRecords[g.away_team_id] = (_teamRecords[g.away_team_id].wins + 1, _teamRecords[g.away_team_id].losses);
            }
        }
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _btnPrevDate?.RegisterCallback<ClickEvent>(_ => { PlayClick(); NavigateDate(-1); });
        _btnNextDate?.RegisterCallback<ClickEvent>(_ => { PlayClick(); NavigateDate(1); });
        _btnHoy?.RegisterCallback<ClickEvent>(_ => { PlayClick(); GoToToday(true); });

        _btnMatchCloseFooter?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseMatchDetails(); });
        _matchOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _matchOverlay) CloseMatchDetails();
        });

        if (CursorManager.Instance == null) return;
        var cursor = CursorManager.Instance;
        cursor.RegisterHandCursor(_btnPrevDate);
        cursor.RegisterHandCursor(_btnNextDate);
        cursor.RegisterHandCursor(_btnHoy);
        cursor.RegisterHandCursor(_btnMatchCloseFooter);
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Results] RefreshHeader error: {ex.Message}"); }
        _matchOverlay?.RemoveFromClassList("match-modal-overlay--visible");
        UpdateDateLabel();
        ShowResults();
    }

    protected override void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;
        if (_root.Q<Label>("HeaderTeamName") == null) return;

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
            chemLabel.text = $"{chemistry}%";
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

    // ── NAVIGATION ──

    void NavigateDate(int delta)
    {
        if (_gameDates.Count == 0) return;
        int newIndex = Mathf.Clamp(_currentDateIndex + delta, 0, _gameDates.Count - 1);
        if (newIndex == _currentDateIndex) return;
        _currentDateIndex = newIndex;
        _currentPage = 0;
        UpdateDateLabel();
        ShowResults();
    }

    void GoToToday(bool refresh)
    {
        if (_gameDates.Count == 0 || _allGames == null) return;

        // "Hoy" = fecha actual de la partida (seasons.current_date), la misma que
        // muestra la cabecera y resalta el calendario.
        string today = _season?.current_date;
        int idx = !string.IsNullOrEmpty(today) ? _gameDates.IndexOf(today) : -1;

        if (idx < 0)
        {
            // La fecha actual no tiene jornada (día de descanso): usa la más cercana.
            if (!string.IsNullOrEmpty(today))
            {
                string before = null, after = null;
                foreach (var d in _gameDates)
                {
                    if (string.CompareOrdinal(d, today) <= 0) before = d;
                    else { after = d; break; }
                }
                string chosen = before ?? after ?? _gameDates[0];
                idx = _gameDates.IndexOf(chosen);
            }
            else idx = 0;
        }
        if (idx < 0) idx = 0;

        _currentDateIndex = idx;
        _currentPage = 0;

        if (!refresh) return;
        UpdateDateLabel();
        ShowResults();
    }

    void UpdateDateLabel()
    {
        if (_currentDateLabel == null) return;

        if (_gameDates.Count == 0)
        {
            _currentDateLabel.text = "SIN PARTIDOS";
            SetNavEnabled(false, false);
            return;
        }

        try
        {
            var dt = System.DateTime.Parse(_gameDates[_currentDateIndex]);
            _currentDateLabel.text = $"{dt.Day} {GetMonthName(dt.Month).ToUpper()} {dt.Year}";
        }
        catch
        {
            _currentDateLabel.text = _gameDates[_currentDateIndex];
        }

        SetNavEnabled(_currentDateIndex > 0, _currentDateIndex < _gameDates.Count - 1);
    }

    void SetNavEnabled(bool prev, bool next)
    {
        if (_btnPrevDate != null)
        {
            _btnPrevDate.SetEnabled(prev);
            _btnPrevDate.EnableInClassList("results-nav-btn--disabled", !prev);
        }
        if (_btnNextDate != null)
        {
            _btnNextDate.SetEnabled(next);
            _btnNextDate.EnableInClassList("results-nav-btn--disabled", !next);
        }
    }

    // ── RENDER ──

    void ShowResults()
    {
        _resultsBody.Clear();
        _resultsPaginator?.Clear();
        if (_resultsPaginator != null)
            _resultsPaginator.style.display = DisplayStyle.None;

        if (_gameDates.Count == 0)
        {
            AddEmptyState("NO HAY PARTIDOS PROGRAMADOS");
            return;
        }

        string currentDate = _gameDates[_currentDateIndex];
        var dayGames = _allGames
            .Where(g => g.game_date == currentDate)
            .OrderBy(g => g.id)
            .ToList();

        if (dayGames.Count == 0)
        {
            AddEmptyState("NO HAY PARTIDOS EN ESTA FECHA");
            return;
        }

        int totalPages = Mathf.CeilToInt((float)dayGames.Count / ITEMS_PER_PAGE);
        if (totalPages < 1) totalPages = 1;
        if (_currentPage >= totalPages) _currentPage = totalPages - 1;
        if (_currentPage < 0) _currentPage = 0;

        int start = _currentPage * ITEMS_PER_PAGE;
        int end = Mathf.Min(start + ITEMS_PER_PAGE, dayGames.Count);

        for (int i = start; i < end; i++)
            _resultsBody.Add(CreateGameCard(dayGames[i]));

        if (totalPages > 1)
            BuildPaginator(totalPages);
    }

    void AddEmptyState(string text)
    {
        var wrap = new VisualElement();
        wrap.AddToClassList("results-empty");
        var lbl = new Label();
        lbl.AddToClassList("results-empty-text");
        lbl.text = text;
        wrap.Add(lbl);
        _resultsBody.Add(wrap);
    }

    void BuildPaginator(int totalPages)
    {
        if (_resultsPaginator == null) return;
        _resultsPaginator.style.display = DisplayStyle.Flex;

        var btnPrev = new Button();
        btnPrev.AddToClassList("results-paginator-btn");
        btnPrev.text = "\u25C0";
        btnPrev.SetEnabled(_currentPage > 0);
        if (_currentPage == 0) btnPrev.AddToClassList("results-paginator-btn--disabled");
        int prevPage = _currentPage - 1;
        btnPrev.RegisterCallback<ClickEvent>(_ => { PlayClick(); GoToPage(prevPage); });
        RegisterHand(btnPrev);
        _resultsPaginator.Add(btnPrev);

        var lbl = new Label();
        lbl.AddToClassList("results-paginator-label");
        lbl.text = $"{_currentPage + 1} / {totalPages}";
        _resultsPaginator.Add(lbl);

        var btnNext = new Button();
        btnNext.AddToClassList("results-paginator-btn");
        btnNext.text = "\u25B6";
        btnNext.SetEnabled(_currentPage < totalPages - 1);
        if (_currentPage >= totalPages - 1) btnNext.AddToClassList("results-paginator-btn--disabled");
        int nextPage = _currentPage + 1;
        btnNext.RegisterCallback<ClickEvent>(_ => { PlayClick(); GoToPage(nextPage); });
        RegisterHand(btnNext);
        _resultsPaginator.Add(btnNext);
    }

    void GoToPage(int page)
    {
        _currentPage = page;
        ShowResults();
    }

    void RegisterHand(VisualElement el)
    {
        if (el == null || CursorManager.Instance == null) return;
        el.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
        el.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
    }

    // ── MODAL DETALLES DE PARTIDO ──

    void OpenMatchDetails(GameData game)
    {
        if (game == null || _matchOverlay == null) return;

        bool isAllStar = game.game_type == "allstar";
        var home = isAllStar ? null : _allTeams?.Find(t => t.id == game.home_team_id);
        var away = isAllStar ? null : _allTeams?.Find(t => t.id == game.away_team_id);

        string homeName = isAllStar ? "CONFERENCIA ESTE" : (home?.name?.ToUpper() ?? "LOCAL");
        string awayName = isAllStar ? "CONFERENCIA OESTE" : (away?.name?.ToUpper() ?? "VISITANTE");

        if (_matchSubtitle != null)
            _matchSubtitle.text = $"{GetGameTypeBadge(game.game_type)} · {FormatDate(game.game_date)} · FINALIZADO";
        if (_matchTitle != null)
            _matchTitle.text = $"{homeName} {game.home_score} - {game.away_score} {awayName}";

        // Pabellón
        if (_matchArenaImage != null)
        {
            var arenaSprite = GetArenaSprite(isAllStar ? null : home?.logo);
            if (arenaSprite != null)
                _matchArenaImage.style.backgroundImage = new StyleBackground(arenaSprite);
        }
        if (_matchArenaName != null)
            _matchArenaName.text = isAllStar ? "ALL-STAR ARENA" : (home?.arena?.ToUpper() ?? "PABELLÓN");

        if (_matchArenaAttendance != null)
        {
            int attendance = 0;
            var att = DatabaseManager.Instance.GetGameAttendance(game.id);
            if (att != null) attendance = att.attendance;
            else if (isAllStar) attendance = 50000;
            _matchArenaAttendance.text = attendance > 0
                ? $"{attendance:N0} espectadores"
                : "Asistencia no disponible";
        }

        // Estadísticas del partido
        var stats = DatabaseManager.Instance.GetGamePlayerStats(game.id) ?? new List<PlayerGameStats>();
        var homeStats = stats.Where(s => s.team_id == game.home_team_id).ToList();
        var awayStats = stats.Where(s => s.team_id == game.away_team_id).ToList();

        BuildTeamColumn(_matchHomeCol, home, game.home_score, game.away_score, homeStats,
            isAllStar ? "all-star-game" : null, homeName);
        BuildTeamColumn(_matchAwayCol, away, game.away_score, game.home_score, awayStats,
            isAllStar ? "all-star-game" : null, awayName);

        _matchOverlay.AddToClassList("match-modal-overlay--visible");
    }

    void CloseMatchDetails()
    {
        _matchOverlay?.RemoveFromClassList("match-modal-overlay--visible");
    }

    void BuildTeamColumn(VisualElement col, TeamData team, int myScore, int rivalScore,
        List<PlayerGameStats> stats, string logoOverride, string nameOverride)
    {
        if (col == null) return;
        col.Clear();

        var head = new VisualElement();
        head.AddToClassList("match-modal-team-head");

        var logo = new VisualElement();
        logo.AddToClassList("match-modal-team-logo");
        string logoSprite = !string.IsNullOrEmpty(team?.logo) ? team.logo : logoOverride;
        if (!string.IsNullOrEmpty(logoSprite))
        {
            if (_logoSprites80.TryGetValue(logoSprite, out var sprite80))
                logo.style.backgroundImage = new StyleBackground(sprite80);
            else if (_logoSprites64.TryGetValue(logoSprite, out var sprite64))
                logo.style.backgroundImage = new StyleBackground(sprite64);
        }
        head.Add(logo);

        var name = new Label();
        name.AddToClassList("match-modal-team-name");
        name.text = nameOverride ?? team?.name?.ToUpper() ?? "???";
        head.Add(name);

        var score = new Label();
        score.AddToClassList("match-modal-team-score");
        score.text = myScore.ToString();
        score.AddToClassList(myScore > rivalScore ? "match-modal-team-score--winner" : "match-modal-team-score--loser");
        head.Add(score);

        col.Add(head);

        AddLeaderSection(col, "ANOTADORES", stats, s => s.points);
        AddLeaderSection(col, "REBOTEADORES", stats, s => s.rebounds);
        AddLeaderSection(col, "ASISTENTES", stats, s => s.assists);
    }

    void AddLeaderSection(VisualElement col, string title, List<PlayerGameStats> stats,
        System.Func<PlayerGameStats, int> valFn)
    {
        var section = new VisualElement();
        section.AddToClassList("match-modal-leader-section");

        var titleLbl = new Label();
        titleLbl.AddToClassList("match-modal-leader-title");
        titleLbl.text = title;
        section.Add(titleLbl);

        var top = stats.OrderByDescending(valFn).Take(3).ToList();
        if (top.Count == 0)
        {
            var empty = new Label();
            empty.AddToClassList("match-modal-leader-empty");
            empty.text = "Sin datos";
            section.Add(empty);
        }
        else
        {
            for (int i = 0; i < top.Count; i++)
            {
                var s = top[i];
                var player = DatabaseManager.Instance.GetPlayerById(s.player_id);

                var row = new VisualElement();
                row.AddToClassList("match-modal-leader-row");

                var rank = new Label();
                rank.AddToClassList("match-modal-leader-rank");
                rank.text = $"{i + 1}";
                row.Add(rank);

                var nameLbl = new Label();
                nameLbl.AddToClassList("match-modal-leader-name");
                nameLbl.text = player != null ? $"{player.first_name} {player.last_name}" : "???";
                row.Add(nameLbl);

                var posLbl = new Label();
                posLbl.AddToClassList("match-modal-leader-pos");
                posLbl.text = PositionCodes.GetShort(player?.position);
                row.Add(posLbl);

                var valLbl = new Label();
                valLbl.AddToClassList("match-modal-leader-value");
                valLbl.text = valFn(s).ToString();
                row.Add(valLbl);

                section.Add(row);
            }
        }

        col.Add(section);
    }

    Sprite GetArenaSprite(string logo)
    {
        string key = logo;
        if (key == "sixers") key = "76ers"; // desajuste de nombre de archivo de arena
        if (!string.IsNullOrEmpty(key))
        {
            var sprite = TeamImages.Arena(key);
            if (sprite != null) return sprite;
        }

        if (!_defaultArenaLoaded)
        {
            _defaultArenaSprite = TeamImages.Arena("default");
            _defaultArenaLoaded = true;
        }
        return _defaultArenaSprite;
    }

    // ── CARD ──

    VisualElement CreateGameCard(GameData game)
    {
        bool isPlayed = game.is_played == 1;
        bool isMyGame = game.home_team_id == _myTeam.id || game.away_team_id == _myTeam.id;

        var card = new VisualElement();
        card.AddToClassList("results-card");
        if (isMyGame) card.AddToClassList("results-card--my-team");

        var inner = new VisualElement();
        inner.AddToClassList("results-card-inner");

        var main = new VisualElement();
        main.AddToClassList("results-card-main");

        // Top bar
        var top = new VisualElement();
        top.AddToClassList("results-card-top");

        var badge = new Label();
        badge.AddToClassList("results-card-badge");
        badge.text = GetGameTypeBadge(game.game_type);
        top.Add(badge);

        var dateLbl = new Label();
        dateLbl.AddToClassList("results-card-date");
        dateLbl.text = FormatDate(game.game_date);
        top.Add(dateLbl);

        var statusLbl = new Label();
        statusLbl.AddToClassList("results-card-status");
        if (isPlayed)
        {
            statusLbl.AddToClassList("results-card-status--played");
            statusLbl.text = "FINALIZADO";
        }
        else
        {
            statusLbl.AddToClassList("results-card-status--scheduled");
            statusLbl.text = "PROGRAMADO";
        }
        top.Add(statusLbl);

        // Body
        var body = new VisualElement();
        body.AddToClassList("results-card-body");

        bool isAllStar = game.game_type == "allstar";
        string allStarLogo = isAllStar ? "all-star-game" : null;
        string homeOverride = isAllStar ? "CONFERENCIA\nESTE" : null;
        string awayOverride = isAllStar ? "CONFERENCIA\nOESTE" : null;

        body.Add(BuildTeamSide(game.home_team_id, isPlayed, homeOverride, allStarLogo));
        body.Add(BuildScore(game, isPlayed));
        body.Add(BuildTeamSide(game.away_team_id, isPlayed, awayOverride, allStarLogo));

        main.Add(body);
        inner.Add(top);

        var content = new VisualElement();
        content.AddToClassList("results-card-content");
        content.Add(main);

        // Chevron + clic (solo partidos jugados)
        if (isPlayed)
        {
            if (_chevronTex != null)
            {
                var chevron = new VisualElement();
                chevron.AddToClassList("results-card-chevron");
                chevron.style.backgroundImage = new StyleBackground(_chevronTex);
                content.Add(chevron);
            }

            card.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenMatchDetails(game); });
            RegisterHand(card);
        }

        inner.Add(content);
        card.Add(inner);
        return card;
    }

    VisualElement BuildTeamSide(int teamId, bool isPlayed, string nameOverride = null, string logoOverride = null)
    {
        var side = new VisualElement();
        side.AddToClassList("results-card-team");

        var team = nameOverride == null ? _allTeams?.Find(t => t.id == teamId) : null;
        string logoSprite = logoOverride ?? team?.logo;

        var logo = new VisualElement();
        logo.AddToClassList("results-card-logo");
        if (!string.IsNullOrEmpty(logoSprite))
        {
            if (_logoSprites80.TryGetValue(logoSprite, out var sprite80))
                logo.style.backgroundImage = new StyleBackground(sprite80);
            else if (_logoSprites64.TryGetValue(logoSprite, out var sprite64))
                logo.style.backgroundImage = new StyleBackground(sprite64);
        }
        side.Add(logo);

        var name = new Label();
        name.AddToClassList("results-card-name");
        name.text = nameOverride ?? team?.name?.ToUpper() ?? "???";
        if (nameOverride == null && teamId == _myTeam.id) name.AddToClassList("results-card-name--mine");
        side.Add(name);

        var record = new Label();
        record.AddToClassList("results-card-record");
        record.text = (isPlayed && nameOverride == null) ? GetTeamRecord(teamId) : "—";
        side.Add(record);

        return side;
    }

    VisualElement BuildScore(GameData game, bool isPlayed)
    {
        var score = new VisualElement();
        score.AddToClassList("results-card-score");

        // Home column
        var homeCol = new VisualElement();
        homeCol.AddToClassList("results-card-score-col");
        var homeScore = new Label();
        homeScore.AddToClassList("results-card-score-label");
        homeScore.text = isPlayed ? game.home_score.ToString() : "-";
        homeCol.Add(homeScore);
        var homeLine = new VisualElement();
        homeLine.AddToClassList("results-card-score-line");
        if (isPlayed && game.home_score > game.away_score)
        {
            homeScore.AddToClassList("results-card-score-label--winner");
            homeLine.AddToClassList("results-card-score-line--visible");
        }
        homeCol.Add(homeLine);
        score.Add(homeCol);

        var sep = new Label();
        sep.AddToClassList("results-card-score-sep");
        sep.text = "-";
        score.Add(sep);

        // Away column
        var awayCol = new VisualElement();
        awayCol.AddToClassList("results-card-score-col");
        var awayScore = new Label();
        awayScore.AddToClassList("results-card-score-label");
        awayScore.text = isPlayed ? game.away_score.ToString() : "-";
        awayCol.Add(awayScore);
        var awayLine = new VisualElement();
        awayLine.AddToClassList("results-card-score-line");
        if (isPlayed && game.away_score > game.home_score)
        {
            awayScore.AddToClassList("results-card-score-label--winner");
            awayLine.AddToClassList("results-card-score-line--visible");
        }
        awayCol.Add(awayLine);
        score.Add(awayCol);

        return score;
    }

    // ── HELPERS ──

    string GetTeamRecord(int teamId)
    {
        if (_teamRecords.TryGetValue(teamId, out var rec))
            return $"{rec.wins}-{rec.losses}";
        return "0-0";
    }

    string GetGameTypeBadge(string gameType)
    {
        switch (gameType)
        {
            case "preseason": return "PRETEMPORADA";
            case "regular": return "LIGA REGULAR";
            case "playin": return "PLAY-IN";
            case "playoff": return "PLAYOFFS";
            case "allstar": return "ALL-STAR";
            case "gleague": return "G-LEAGUE";
            case "gleague_playoff": return "G-LEAGUE";
            default: return "LIGA REGULAR";
        }
    }

    string FormatDate(string date)
    {
        try
        {
            var dt = System.DateTime.Parse(date);
            return $"{dt.Day} {GetMonthName(dt.Month).ToUpper()} {dt.Year}";
        }
        catch
        {
            return date;
        }
    }

    string GetMonthName(int month)
    {
        var names = new[] { "", "Ene", "Feb", "Mar", "Abr", "May", "Jun",
                           "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
        return names[month];
    }
}
