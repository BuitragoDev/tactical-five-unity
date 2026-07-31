using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
    public class DashboardController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Dashboard;

    protected override void OnBtnActionClicked()
    {
        OnActionClicked();
    }

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerSeason;
    private Label _headerDate;
    private VisualElement _loadingSpinner;
    private IVisualElementScheduledItem _spinScheduler;
    private bool _isLoading;

    // Último partido
    private Label _noLastGame;
    private VisualElement _lastGameResult;
    private VisualElement _lastHomeLog;
    private VisualElement _lastAwayLog;
    private Label _lastHomeName;
    private Label _lastAwayName;
    private Label _lastHomeScore;
    private Label _lastAwayScore;
    private Label _lastResultBadge;
    private Label _lastGameDate;
    // Meta partidos
    private Label _lastGameLocation;
    private Label _lastGameArena;
    private Label _lastGameType;

    // Próximo partido
    private Label _noNextGame;
    private VisualElement _nextGameResult;
    private VisualElement _nextHomeLog;
    private VisualElement _nextAwayLog;
    private Label _nextHomeName;
    private Label _nextAwayName;
    private Label _nextGameDate;
    private Label _nextGameLocation;
    private Label _nextGameArena;
    private Label _nextGameType;

    // Clasificación
    private Button _tabEast;
    private Button _tabWest;
    private VisualElement _standingsBody;
    private string _currentConf = "West";

    // Relaciones
    private VisualElement _barTrust;
    private VisualElement _barMorale;
    private VisualElement _barFanConfidence;
    private Label _valTrust;
    private Label _valMorale;
    private Label _valFanConfidence;

    // Estadísticas del equipo
    private Label _teamObjective;
    private VisualElement _teamObjectiveStatus;
    private VisualElement _teamStatsLogo;
    private Label _teamOverallLabel;
    private Label _teamChemistryLabel;
    private VisualElement _teamChemistryRing;
    private Label _teamChemistryRingVal;
    private VisualElement _teamChemistryIcon;
    private Label _teamOverallRingVal;
    private Label _teamArenaName;
    private Label _teamArenaCapacity;
    private VisualElement _teamReputationStars;
    private VisualElement _messagesBody;

    // Datos
    private List<TeamData> _allTeams;
    private List<GameData> _allGames;
    private List<PlayerData> _players = new();

    // Sprites
    private Dictionary<string, Sprite> _logoSprites = new();

    // Fired modal
    private VisualElement _firedOverlay;

    // Injured lineup modal
    private bool _injuredModalResolved;
    private bool _injuredModalGoToQuinteto;
    private static List<int> _pendingRecoveredIds = new();

    // Empty lineup modal
    private bool _emptyLineupModalResolved;
    private bool _emptyLineupGoToQuinteto;
    protected override void OnEnable()
    {
        base.OnEnable();
        _firedOverlay = new VisualElement();
        _firedOverlay.AddToClassList("fired-modal-overlay");
        _root.Add(_firedOverlay);
        AudioManager.Instance?.PlayMusic("backgroundMenu");
        SetupPlayerCoach();
        CheckBudgetWarning();
        ProcessMaturedOffers();
        ShowPendingRecoveryModal();
        CheckTradeDeadlineModal();
    }

    void Update()
    {
        if (IsAnyModalOpen()) return;
        if (Input.GetKeyDown(KeyCode.S))
        {
            PlayClick();
            OnActionClicked();
        }
    }

    bool IsAnyModalOpen()
    {
        if (_firedOverlay != null && _firedOverlay.style.display == DisplayStyle.Flex) return true;
        if (_configModalOverlay != null && _configModalOverlay.ClassListContains("modal-overlay--visible")) return true;
        if (_configMainMenuConfirmOverlay != null && _configMainMenuConfirmOverlay.ClassListContains("modal-overlay--visible")) return true;
        if (_configExitConfirmOverlay != null && _configExitConfirmOverlay.ClassListContains("modal-overlay--visible")) return true;
        return false;
    }
    protected override void CacheReferences()
    {
        // Header
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _loadingSpinner = _root.Q<VisualElement>("LoadingSpinner");
        _loadingSpinner.style.display = DisplayStyle.None;

        // Último partido
        _noLastGame = _root.Q<Label>("NoLastGame");
        _lastGameResult = _root.Q<VisualElement>("LastGameResult");
        _lastHomeLog = _root.Q<VisualElement>("LastHomeLog");
        _lastAwayLog = _root.Q<VisualElement>("LastAwayLog");
        _lastHomeName = _root.Q<Label>("LastHomeName");
        _lastAwayName = _root.Q<Label>("LastAwayName");
        _lastHomeScore = _root.Q<Label>("LastHomeScore");
        _lastAwayScore = _root.Q<Label>("LastAwayScore");
        _lastResultBadge = _root.Q<Label>("LastResultBadge");
        _lastGameDate = _root.Q<Label>("LastGameDate");
        _lastGameLocation = _root.Q<Label>("LastGameLocation");
        _lastGameArena = _root.Q<Label>("LastGameArena");
        _lastGameType = _root.Q<Label>("LastGameType");

        // Próximo partido
        _noNextGame = _root.Q<Label>("NoNextGame");
        _nextGameResult = _root.Q<VisualElement>("NextGameResult");
        _nextHomeLog = _root.Q<VisualElement>("NextHomeLog");
        _nextAwayLog = _root.Q<VisualElement>("NextAwayLog");
        _nextHomeName = _root.Q<Label>("NextHomeName");
        _nextAwayName = _root.Q<Label>("NextAwayName");
        _nextGameDate = _root.Q<Label>("NextGameDate");
        _nextGameLocation = _root.Q<Label>("NextGameLocation");
        _nextGameArena = _root.Q<Label>("NextGameArena");
        _nextGameType = _root.Q<Label>("NextGameType");

        // Clasificación
        _tabEast = _root.Q<Button>("TabEast");
        _tabWest = _root.Q<Button>("TabWest");
        _standingsBody = _root.Q<VisualElement>("StandingsBody");

        // Estadísticas del equipo
        _teamObjective = _root.Q<Label>("TeamObjective");
        _teamObjectiveStatus = _root.Q<VisualElement>("TeamObjectiveStatus");
        _teamStatsLogo = _root.Q<VisualElement>("TeamStatsLogo");
        _teamOverallLabel = _root.Q<Label>("TeamOverallLabel");
        _teamChemistryLabel = _root.Q<Label>("TeamChemistryLabel");
        _teamChemistryRing = _root.Q<VisualElement>("TeamChemistryRing");
        _teamChemistryRingVal = _root.Q<Label>("TeamChemistryRingVal");
        _teamChemistryIcon = _root.Q<VisualElement>("TeamChemistryIcon");
        _teamOverallRingVal = _root.Q<Label>("TeamOverallRingVal");
        _teamArenaName = _root.Q<Label>("TeamArenaName");
        _teamArenaCapacity = _root.Q<Label>("TeamArenaCapacity");
        _teamReputationStars = _root.Q<VisualElement>("TeamReputationStars");

        // Relaciones
        _barTrust = _root.Q<VisualElement>("CircleTrust");
        _barMorale = _root.Q<VisualElement>("CircleMorale");
        _barFanConfidence = _root.Q<VisualElement>("CircleFanConfidence");
        _valTrust = _root.Q<Label>("ValTrust");
        _valMorale = _root.Q<Label>("ValMorale");
        _valFanConfidence = _root.Q<Label>("ValFanConfidence");

        // Mensajes
        _messagesBody = _root.Q<VisualElement>("MessagesBody");
    }
    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        // Panel "more" buttons
        var moreTex = Resources.Load<Texture2D>("Icons/mas");
        if (moreTex != null)
        {
            var moreBtns = new[] { "BtnLastGameMore", "BtnNextGameMore", "BtnStandingsMore", "BtnPlayerStatsMore" };
            foreach (var name in moreBtns)
            {
                var el = _root.Q<VisualElement>(name);
                if (el != null)
                    el.style.backgroundImage = new StyleBackground(moreTex);
            }
        }

        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);

        // Clasificación: mostrar por defecto la conferencia de mi equipo
        if (_myTeam != null)
            _currentConf = _myTeam.conference == "East" ? "East" : "West";

        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
    }
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        var container = _root.childCount > 0 ? _root[0] : _root;
        if (container != null)
        {
            var topHeader = container.Q<VisualElement>("TopHeader");
            if (topHeader != null)
            {
                topHeader.RemoveFromHierarchy();
                var bodyRow = new VisualElement();
                bodyRow.style.flexDirection = FlexDirection.Row;
                bodyRow.style.flexGrow = 1;
                bodyRow.style.minHeight = 0;
                while (container.childCount > 0)
                    bodyRow.Add(container[0]);
                container.Add(topHeader);
                container.Add(bodyRow);
                container.style.flexDirection = FlexDirection.Column;
            }
        }
        _tabEast?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowStandings("East"); });
        _tabWest?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowStandings("West"); });
        var moreActions = new (string name, GameScreen screen)[]
        {
            ("BtnLastGameMore", GameScreen.Results),
            ("BtnNextGameMore", GameScreen.Results),
            ("BtnStandingsMore", GameScreen.Standings),
            ("BtnPlayerStatsMore", GameScreen.Stats),
        };
        foreach (var (name, screen) in moreActions)
        {
            var btn = _root.Q<VisualElement>(name);
            if (btn == null) continue;
            btn.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(screen); });
        }
    }

    // ── REFRESH COMPLETO ─────────────────────────────────

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Dashboard] RefreshHeader error: {ex.Message}"); }
        RefreshLastGame();
        RefreshNextGame();
        RefreshActionButton();
        ShowStandings(_currentConf);
        RefreshTeamStats();
        RefreshBoard();
        RefreshPlayerStats();
        RefreshMessages();
    }

    // ── HEADER ───────────────────────────────────────────

    protected override void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        SetTeamLogo(_headerTeamLogo, _myTeam.logo, "64x64");

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerBudget.text = $"${_myTeam.budget / 1_000_000}M";
        _headerBudget.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        // Masa salarial real (jugadores + empleados)
        var teamEmployees = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        long totalPayroll = _players.Sum(p => p.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        // Margen salarial = Cap - solo jugadores
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - _players.Sum(p => p.salary);

        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        string marginText = margin >= 0
            ? $"+${margin / 1_000_000}M"
            : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        _headerMargin.text = marginText;
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

        _headerMargin.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0)
            _headerMargin.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = GetCurrentDateString();
        }
    }

    string GetCurrentDateString()
    {
        if (_season == null) return "";
        if (!string.IsNullOrEmpty(_season.current_date))
            return System.DateTime.Parse(_season.current_date).ToString("dd/MM/yyyy");
        try
        {
            if (_season.current_game_day == 0)
            {
                var firstPre = DatabaseManager.Instance.Db.Table<GameData>()
                    .Where(g => g.manager_id == _manager.id
                             && g.game_type == "preseason")
                    .OrderByDescending(g => g.game_day)
                    .FirstOrDefault();
                if (firstPre != null)
                    return System.DateTime.Parse(firstPre.game_date).ToString("dd/MM/yyyy");
                return new System.DateTime(_season.year_start, 10, 22).ToString("dd/MM/yyyy");
            }

            if (_season.current_game_day < 0)
            {
                var lastGame = DatabaseManager.Instance.Db.Table<GameData>()
                    .Where(g => g.manager_id == _manager.id
                             && g.is_played == 1
                             && g.game_day == _season.current_game_day)
                    .FirstOrDefault();
                if (lastGame != null)
                    return System.DateTime.Parse(lastGame.game_date).ToString("dd/MM/yyyy");
            }

            var seasonStart = new System.DateTime(_season.year_start, 10, 22);
            return seasonStart.AddDays(_season.current_game_day - 1).ToString("dd/MM/yyyy");
        }
        catch { return ""; }
    }

    // ── BOTÓN ACCIÓN ─────────────────────────────────────

    void RefreshActionButton()
    {
        if (_season == null || _myTeam == null)
        {
            SetActionBtn("CONTINUAR", "");
            return;
        }

        int nextDay = FindNextGameDay();
        if (nextDay == 0)
        {
            SetActionBtn("CONTINUAR", "");
            return;
        }

        var gamesOnNextDay = DatabaseManager.Instance.GetGamesByGameDay(_manager.id, nextDay);

        bool myTeamPlays = gamesOnNextDay.Any(g =>
            g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id);
        bool hasAllStar = gamesOnNextDay.Any(g => g.game_type == "allstar");

        if (myTeamPlays || hasAllStar)
        {
            SetActionBtn("DÍA DE PARTIDO", "btn-action--match");
            return;
        }

        if (gamesOnNextDay.Count > 0)
        {
            SetActionBtn("SIMULAR PARTIDOS", "btn-action--simulate");
            return;
        }

        SetActionBtn("CONTINUAR", "");
    }

    void SetActionBtn(string text, string extraClass)
    {
        _btnAction.text = text;
        _btnAction.RemoveFromClassList("btn-action--match");
        _btnAction.RemoveFromClassList("btn-action--simulate");
        if (!string.IsNullOrEmpty(extraClass))
            _btnAction.AddToClassList(extraClass);
    }

    void OnActionClicked()
    {
        if (_season == null || _isLoading) return;

        if (_season.phase == "finished")
        {
            if (_manager.trust < 10)
            {
                ShowFiredModal();
            }
            else
            {
                ScreenManager.Instance.GoTo(GameScreen.SeasonSummary);
            }
            return;
        }

        ShowLoading();
        StartCoroutine(ProcessGameDayRoutine());
    }

    List<PlayerData> GetActivePlayers(int teamId)
    {
        if (teamId == _myTeam.id)
        {
            var lineup = DatabaseManager.Instance.GetTeamLineup(teamId);
            if (lineup.Count > 0)
            {
                var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(teamId);
                var lineupByPlayer = lineup.ToDictionary(l => l.player_id);

                var startersWithIdx = new List<(PlayerData p, int sortIdx)>();
                var bench = new List<PlayerData>();

                foreach (var p in allPlayers)
                {
                    if (p.injury_days > 0) continue;
                    if (!lineupByPlayer.TryGetValue(p.id, out var ls)) continue;
                    if (ls.slot == 0)
                        startersWithIdx.Add((p, ls.slot_index >= 0 ? ls.slot_index : 999));
                    else if (ls.slot == 1)
                        bench.Add(p);
                }

                var orderedStarters = startersWithIdx.OrderBy(s => s.sortIdx).Select(s => s.p).ToList();
                var active = orderedStarters.Concat(bench).Take(12).ToList();
                if (active.Count > 0) return active;
            }
        }

        return DatabaseManager.Instance.GetPlayersByTeam(teamId)
            .Where(p => p.injury_days == 0)
            .OrderByDescending(p => p.overall)
            .Take(12)
            .ToList();
    }

    System.Collections.IEnumerator ProcessGameDayRoutine()
    {
        int gameDay = FindNextGameDay();
        Debug.Log($"[GameDay] ProcessGameDayRoutine started. gameDay={gameDay}");
        if (gameDay == 0)
        {
            Debug.Log("[Dashboard] No hay más partidos programados.");
            HideLoading();
            yield break;
        }

        var db = DatabaseManager.Instance.Db;

        db.BeginTransaction();
        try
        {
            var recoveredPlayers = ProcessInjuries();
            ProcessFisicoRecovery();
            if (recoveredPlayers.Count > 0)
            {
                foreach (var p in recoveredPlayers)
                {
                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Recuperado: {p.first_name} {p.last_name}",
                        body = $"{p.first_name} {p.last_name} se ha recuperado de su lesión y vuelve a estar disponible.",
                        game_day = gameDay,
                        game_date = _season?.current_date ?? "",
                        created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        is_read = 0
                    });
                    _pendingRecoveredIds.Add(p.id);
                }
            }

            ProcessScouts();
            ProcessTraining();
            ProcessRenovations();
            ProcessAITransfers(gameDay);
            ProcessStarFreeAgentSignings(gameDay);
            ProcessPsychologistMorale();
            db.Commit();
        }
        catch (System.Exception ex)
        {
            db.Rollback();
            _pendingRecoveredIds.Clear();
            Debug.LogError($"[GameDay] Error en el lote pre-partido, revertido: {ex.Message}\n{ex.StackTrace}");
            HideLoading();
            ShowOfferResultModal("ERROR AL PROCESAR EL DÍA", "Ha ocurrido un error al procesar el día.\n\n" + ex.Message, -1);
            yield break;
        }

        var gamesToday = DatabaseManager.Instance.GetGamesByGameDay(_manager.id, gameDay);

        bool myTeamPlays = gamesToday.Any(g =>
            g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id);
        bool hasAllStar = gamesToday.Any(g => g.game_type == "allstar");

        if (myTeamPlays)
        {
            // Ensure lineup exists before simulation
            var lineupCheck = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
            if (lineupCheck.Count == 0)
            {
                var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
                DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, allPlayers);
            }

            // Check for empty starter slots (e.g. after a trade)
            var emptySlots = GetEmptyStarterSlots();
            if (emptySlots.Count > 0)
            {
                _emptyLineupModalResolved = false;
                _emptyLineupGoToQuinteto = false;
                ShowEmptyLineupModal(emptySlots);
                yield return new WaitUntil(() => _emptyLineupModalResolved);
                if (_emptyLineupGoToQuinteto)
                {
                    HideLoading();
                    ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                    yield break;
                }
            }

            var injured = GetInjuredActiveLineupPlayers();
            if (injured.Count > 0)
            {
                _injuredModalResolved = false;
                _injuredModalGoToQuinteto = false;
                ShowInjuredLineupModal(injured);
                yield return new WaitUntil(() => _injuredModalResolved);
                if (_injuredModalGoToQuinteto)
                {
                    HideLoading();
                    ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                    yield break;
                }
            }
        }

        db.BeginTransaction();
        try
        {
        if (gamesToday.Count > 0)
        {
            GameResultCache.Clear();
            GameResultCache.LastGameDay = gameDay;

            foreach (var game in gamesToday)
            {
                List<PlayerData> homePlayers, awayPlayers;

                if (game.game_type == "allstar")
                {
                    homePlayers = BuildAllStarRoster("East");
                    awayPlayers = BuildAllStarRoster("West");
                }
                else
                {
                    homePlayers = GetActivePlayers(game.home_team_id);
                    awayPlayers = GetActivePlayers(game.away_team_id);
                }

                int homeChem = DatabaseManager.Instance.GetTeamChemistry(game.home_team_id);
                int awayChem = DatabaseManager.Instance.GetTeamChemistry(game.away_team_id);
                bool isMyHomeGame = game.home_team_id == _myTeam.id;

                var homeStarters = new HashSet<int>(homePlayers.Take(5).Select(p => p.id));
                var awayStarters = new HashSet<int>(awayPlayers.Take(5).Select(p => p.id));
                GameResultCache.GameStarters[game.id] = new HashSet<int>(homeStarters.Concat(awayStarters));

                var result = GameSimulator.SimulateGame(game, homePlayers, awayPlayers, homeChem, awayChem, isMyHomeGame);
                DatabaseManager.Instance.UpdateGame(game);
                GameResultCache.SimulatedGameIds.Add(game.id);

                if (game.game_type == "allstar")
                {
                    var allStats = result.home_stats.Concat(result.away_stats).ToList();
                    var mvpStats = allStats.OrderByDescending(s => s.rating).First();
                    var record = new AllStarRecord
                    {
                        manager_id = _manager.id,
                        season = $"{_season.year_end}",
                        east_score = game.home_score,
                        west_score = game.away_score,
                        mvp = mvpStats.name,
                        mvp_player_id = mvpStats.player_id
                    };
                    DatabaseManager.Instance.SaveAllStarRecord(record);
                }

                if (game.game_type != "allstar")
                    ProcessGameFinances(game, result);

                ProcessGameInjuries(result, game.game_date);

                bool myTeamInThisGame = game.home_team_id == _myTeam.id || game.away_team_id == _myTeam.id;
                if (myTeamInThisGame)
                {
                    CreateGameResultMessage(game, result);
                    UpdateFanConfidence(game, result);
                    CheckBudgetAfterGame();
                }

                // Recalculate morale for all players in this game
                bool homeWon = game.home_score > game.away_score;
                UpdatePlayersMoraleAfterGame(result.home_stats, game.home_team_id, homeWon);
                UpdatePlayersMoraleAfterGame(result.away_stats, game.away_team_id, !homeWon);

                // Evolve relationships for teams in this game
                DatabaseManager.Instance.UpdateRelationshipsAfterGame(
                    game.home_team_id, game.id, homeWon,
                    result.home_stats.Where(s => s.minutes > 0).Select(s => s.player_id).ToList());
                DatabaseManager.Instance.UpdateRelationshipsAfterGame(
                    game.away_team_id, game.id, !homeWon,
                    result.away_stats.Where(s => s.minutes > 0).Select(s => s.player_id).ToList());
            }

            // Recalculate team chemistry for all teams involved today
            foreach (var game in gamesToday)
            {
                int chem = DatabaseManager.Instance.CalculateTeamChemistry(game.home_team_id, gameDay);
                DatabaseManager.Instance.UpdateTeamChemistry(game.home_team_id, chem);
                chem = DatabaseManager.Instance.CalculateTeamChemistry(game.away_team_id, gameDay);
                DatabaseManager.Instance.UpdateTeamChemistry(game.away_team_id, chem);
            }

            UpdateManagerStats(gameDay);

            QuickNewsGenerator.Generate(_manager, _myTeam, _season, gamesToday, gameDay, _season?.current_date ?? "");
        }

        // ── Phase transitions ──
        if (_season.phase == "regular" || _season.phase == "preseason")
        {
            bool allRegularPlayed = !DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == _manager.id
                       && g.game_type == "regular"
                       && g.is_played == 0);
            if (allRegularPlayed)
            {
                // Evaluate April before transitioning (use May 1 so prevMonth = 4 / April)
                Debug.Log("[Dashboard] Regular season finished, evaluating April awards");
                EvaluateMonthlyAwards(new System.DateTime(_season.year_end, 5, 1));
                PlayoffsGenerator.GeneratePlayIn(_season, _manager.id);
                _season.phase = "playin";
                DatabaseManager.Instance.UpdateSeason(_season);
                Debug.Log("[Dashboard] Regular season finished → Play-In generated.");
            }
        }

        if (_season.phase == "playin")
        {
            // Match Django: create eliminator for each conference (idempotent)
            int eastElim = PlayoffsGenerator.CreatePlayInEliminator(_season, _manager.id, "East");
            int westElim = PlayoffsGenerator.CreatePlayInEliminator(_season, _manager.id, "West");

            // Match Django: if eliminator games were just created on the current day, simulate them now
            if (eastElim > 0 || westElim > 0)
            {
                var elimToday = DatabaseManager.Instance.GetGamesByGameDay(_manager.id, gameDay);
                if (elimToday.Count > 0)
                {
                    foreach (var elimGame in elimToday)
                    {
                        var homeP = GetActivePlayers(elimGame.home_team_id);
                        var awayP = GetActivePlayers(elimGame.away_team_id);
                        int elimHomeChem = DatabaseManager.Instance.GetTeamChemistry(elimGame.home_team_id);
                        int elimAwayChem = DatabaseManager.Instance.GetTeamChemistry(elimGame.away_team_id);
                        var elimResult = GameSimulator.SimulateGame(elimGame, homeP, awayP, elimHomeChem, elimAwayChem, elimGame.home_team_id == _myTeam.id);
                        DatabaseManager.Instance.UpdateGame(elimGame);
                        ProcessGameFinances(elimGame, elimResult);
                        UpdatePlayersMoraleAfterGame(elimResult.home_stats, elimGame.home_team_id, elimGame.home_score > elimGame.away_score);
                        UpdatePlayersMoraleAfterGame(elimResult.away_stats, elimGame.away_team_id, elimGame.away_score > elimGame.home_score);
                    }
                    GameResultCache.LastGameDay = gameDay;
                    GameResultCache.SimulatedGameIds.AddRange(elimToday.Select(g => g.id));

                    // Recalculate chemistry for Play-In teams
                    foreach (var elimGame in elimToday)
                    {
                        int chem = DatabaseManager.Instance.CalculateTeamChemistry(elimGame.home_team_id, gameDay);
                        DatabaseManager.Instance.UpdateTeamChemistry(elimGame.home_team_id, chem);
                        chem = DatabaseManager.Instance.CalculateTeamChemistry(elimGame.away_team_id, gameDay);
                        DatabaseManager.Instance.UpdateTeamChemistry(elimGame.away_team_id, chem);
                    }
                }
            }

            bool allPlayInPlayed = !DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == _manager.id
                       && g.game_type == "playin"
                       && g.is_played == 0);
            if (allPlayInPlayed)
            {
                int created = PlayoffsGenerator.GeneratePlayoffs(_season, _manager.id);
                if (created > 0)
                {
                    _season.phase = "playoff";
                    DatabaseManager.Instance.UpdateSeason(_season);
                    Debug.Log($"[Dashboard] Play-In finished → Playoffs generated ({created} games).");
                }
                else
                {
                    Debug.LogError("[Dashboard] allPlayInPlayed but GeneratePlayoffs returned 0 games!");
                }
            }
        }

        if (_season.phase == "playoff")
        {
            PlayoffsGenerator.AdvancePlayoffSeries(_season, _manager.id);

            bool allPlayoffPlayed = !DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == _manager.id
                       && g.game_type == "playoff"
                       && g.is_played == 0);
            if (allPlayoffPlayed)
            {
                _season.phase = "finished";
                DatabaseManager.Instance.UpdateSeason(_season);
                Debug.Log("[Dashboard] Playoffs finished → Season finished.");
            }
        }

        ProcessMonthlyPayroll(gameDay);
        ProcessSubscriptionRevenue(gameDay);

        if (_season.phase == "finished")
        {
            DatabaseManager.Instance.UpdateHistoricalPlayerStatsFromSeason(_season.id, _manager.id);
            DatabaseManager.Instance.SaveSeasonEndRecords(_season.id, _manager.id);
            AssignSeasonPoints();
        }

        _season.current_game_day = gameDay;
        if (!string.IsNullOrEmpty(_season.current_date))
        {
            var cur = System.DateTime.Parse(_season.current_date);
            _season.current_date = cur.AddDays(1).ToString("yyyy-MM-dd");
            var newDate = System.DateTime.Parse(_season.current_date);
            if (newDate.Month == 2 && newDate.Day == 1 && newDate.Year == _season.year_end)
            {
                DatabaseManager.Instance.AddMessage(new MessageData
                {
                    manager_id = _manager.id,
                    title = "Última semana de traspasos",
                    body = "El período de traspasos finaliza el 8 de febrero. Aún estás a tiempo de realizar operaciones.",
                    game_day = _season.current_game_day,
                    game_date = _season.current_date,
                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    is_read = 0
                });
                ShowTradeDeadlineModal();
            }

            // Monthly awards evaluation (1st of Dec through Apr)
            if (_season.phase == "regular" && newDate.Day == 1
                && (newDate.Month == 12 || newDate.Month <= 4))
            {
                Debug.Log($"[Dashboard] Trigger awards: newDate={newDate:yyyy-MM-dd}, phase={_season.phase}");
                EvaluateMonthlyAwards(newDate);
            }
        }
            DatabaseManager.Instance.UpdateSeason(_season);
            db.Commit();
        }
        catch (System.Exception ex)
        {
            db.Rollback();
            GameResultCache.Clear();
            Debug.LogError($"[GameDay] Error simulando el día, revertido: {ex.Message}\n{ex.StackTrace}");
            HideLoading();
            ShowOfferResultModal("ERROR AL SIMULAR EL DÍA", "Ha ocurrido un error al simular el día.\n\n" + ex.Message, -1);
            yield break;
        }

        _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        Refresh();

        GameSaveManager.UpdateSlotFromDatabase(DatabaseManager.Instance.ActiveSaveSlot);

        HideLoading();

        if (gamesToday.Count == 0)
        {
            ShowPendingRecoveryModal();
            ProcessMaturedOffers();
            Debug.Log($"[Dashboard] Día {gameDay} — sin partidos, continúa en Dashboard");
        }
        else if (myTeamPlays || hasAllStar)
        {
            if (hasAllStar)
                Debug.Log($"[Dashboard] Día {gameDay} — All-Star Game → MatchDay");
            else
                Debug.Log($"[Dashboard] Día {gameDay} — mi equipo juega → MatchDay");
            ScreenManager.Instance.GoTo(GameScreen.MatchDay);
        }
        else
        {
            Debug.Log($"[Dashboard] Día {gameDay} — {gamesToday.Count} partidos → GameResults");
            ScreenManager.Instance.GoTo(GameScreen.GameResults);
        }
    }

    void ShowLoading()
    {
        if (_isLoading) return;
        _isLoading = true;
        _btnAction.SetEnabled(false);
        _loadingSpinner.style.display = DisplayStyle.Flex;

        _spinScheduler = _root.schedule.Execute(() =>
        {
            if (_loadingSpinner == null) return;
            var current = _loadingSpinner.style.rotate;
            float angle = current.value.angle.value + 15f;
            if (angle >= 360f) angle -= 360f;
            _loadingSpinner.style.rotate = new Rotate(Angle.Degrees(angle));
        }).Every(30);
    }

    void HideLoading()
    {
        _spinScheduler?.Pause();
        _spinScheduler = null;
        _loadingSpinner.style.display = DisplayStyle.None;
        _btnAction.SetEnabled(true);
        _isLoading = false;
    }

    void CheckBudgetAfterGame()
    {
        if (_myTeam.budget < 0)
        {
            _manager.budget_red_warnings++;
            GameResultCache.PendingBudgetWarning = true;
        }
        else
        {
            _manager.budget_red_warnings = 0;
        }
        DatabaseManager.Instance.SaveManager(_manager);
    }

    void CheckBudgetWarning()
    {
        if (!GameResultCache.PendingBudgetWarning) return;
        GameResultCache.PendingBudgetWarning = false;

        if (_manager.budget_red_warnings >= 3)
            ShowBudgetFiredModal();
        else
            ShowBudgetWarningModal(_manager.budget_red_warnings);
    }

    void ProcessMaturedOffers()
    {
        if (_season == null || _manager == null || _myTeam == null)
        {
            Debug.Log($"[Dashboard] ProcessMaturedOffers: skip null refs season={_season != null} manager={_manager != null} myTeam={_myTeam != null}");
            return;
        }

        Debug.Log($"[Dashboard] ProcessMaturedOffers: checking offers manager={_manager.id} current_day={_season.current_game_day}");
        var offers = DatabaseManager.Instance.GetMaturedUnprocessedOffers(_manager.id, _season.current_game_day);
        Debug.Log($"[Dashboard] ProcessMaturedOffers: found {offers.Count} matured offers");
        if (offers.Count == 0)
        {
            ShowNextPendingTradeOffer();
            return;
        }

        string resultSummary = "";
        int acceptedCount = 0;
        int rejectedCount = 0;
        string nowStr = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        bool hasRenewal = false;
        bool hasSigning = false;
        int batchSigningsAccepted = 0;

        bool batchOk = false;
        string batchErrorMsg = null;
        var db = DatabaseManager.Instance.Db;
        db.BeginTransaction();
        try
        {
        foreach (var offer in offers)
        {
            var player = DatabaseManager.Instance.GetPlayerById(offer.player_id);
            if (player == null) continue;

            int gamesPlayed = DatabaseManager.Instance.GetPlayerGamesPlayedInSeason(player.id, _season.id);
            int teamChem = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
            float acceptScore = RosterController.CalculateAcceptScore(player, offer.offer_salary, offer.offer_years, gamesPlayed, teamChem);
            bool accepted = Random.Range(1, 101) <= acceptScore;

            string playerName = $"{player.first_name} {player.last_name}";
            string salaryText = $"${offer.offer_salary / 1_000_000}M/año";
            string yearsText = $"{offer.offer_years} año{(offer.offer_years > 1 ? "s" : "")}";

            if (offer.offer_type == 1)
            {
                // FREE AGENT SIGNING
                hasSigning = true;

                // Check if player is still a free agent
                if (player.team_id != 0)
                {
                    var signingTeam = DatabaseManager.Instance.GetTeamById(player.team_id);
                    string teamName = signingTeam?.name ?? "otro equipo";
                    rejectedCount++;
                    resultSummary += $"\u2717 {playerName}: FICHAJE CANCELADO \u2014 El jugador fich\u00f3 por {teamName}.\n";
                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Fichaje cancelado: {playerName}",
                        body = $"Tu oferta por {playerName} ha sido cancelada porque el jugador ha fichado por {teamName}.",
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0
                    });
                    DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                    continue;
                }

                if (accepted)
                {
                    // Verificar límite de plantilla
                    var roster = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
                    if (roster.Count + batchSigningsAccepted >= TradeHelper.MAX_ROSTER)
                    {
                        rejectedCount++;
                        player.renewal_cooldown_day = _season.current_game_day + 14;
                        DatabaseManager.Instance.UpdatePlayer(player);

                        resultSummary += $"✗ {playerName}: FICHAJE RECHAZADO (plantilla completa) — {salaryText} · {yearsText}\n";

                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = $"Fichaje rechazado: {playerName}",
                            body = $"Tu oferta a {playerName} ha sido rechazada porque tu plantilla está completa ({TradeHelper.MAX_ROSTER} jugadores).",
                            game_day = _season.current_game_day,
                            game_date = nowStr,
                            created_at = nowStr,
                            date_sent = nowStr,
                            is_read = 0
                        });
                        DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                        continue;
                    }

                    // Verificar espacio salarial / excepciones (FA externo → sin Bird Rights)
                    long totalPayroll = roster.Sum(p => p.salary);
                    var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
                    long firstApron = leagueSettings?.apron > 0 ? leagueSettings.apron : TradeHelper.FIRST_APRON;
                    long secondApron = leagueSettings?.repeater_apron > 0 ? leagueSettings.repeater_apron : TradeHelper.SECOND_APRON;
                    long ntMle = leagueSettings?.mid_level > 0 ? leagueSettings.mid_level : TradeHelper.NT_MLE;
                    long tMle = leagueSettings?.taxpayer_mid_level > 0 ? leagueSettings.taxpayer_mid_level : TradeHelper.T_MLE;
                    long minSalary = leagueSettings?.minimum_salary > 0 ? leagueSettings.minimum_salary : TradeHelper.MIN_SALARY;
                    bool offerLegal = true;
                    string illegalReason = "";

                    if (leagueSettings != null)
                    {
                        if (totalPayroll <= leagueSettings.salary_cap)
                        {
                            offerLegal = totalPayroll + offer.offer_salary <= leagueSettings.salary_cap;
                            illegalReason = "sin espacio salarial";
                        }
                        else if (totalPayroll <= firstApron)
                        {
                            offerLegal = offer.offer_salary <= ntMle;
                            illegalReason = $"supera Mid-Level Exception (No Taxpayer) (${ntMle:N0})";
                        }
                        else if (totalPayroll <= secondApron)
                        {
                            offerLegal = offer.offer_salary <= tMle;
                            illegalReason = $"supera Mid-Level Exception (Taxpayer) (${tMle:N0})";
                        }
                        else
                        {
                            offerLegal = offer.offer_salary <= minSalary;
                            illegalReason = $"supera salario mínimo (${minSalary:N0})";
                        }
                    }

                    if (!offerLegal)
                    {
                        rejectedCount++;
                        player.renewal_cooldown_day = _season.current_game_day + 14;
                        DatabaseManager.Instance.UpdatePlayer(player);

                        resultSummary += $"✗ {playerName}: FICHAJE RECHAZADO ({illegalReason}) — {salaryText} · {yearsText}\n";

                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = $"Fichaje rechazado: {playerName}",
                            body = $"Tu oferta a {playerName} ha sido rechazada ({illegalReason}).",
                            game_day = _season.current_game_day,
                            game_date = nowStr,
                            created_at = nowStr,
                            date_sent = nowStr,
                            is_read = 0
                        });
                        DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                        continue;
                    }

                    acceptedCount++;
                    batchSigningsAccepted++;
                    player.team_id = _myTeam.id;
                    player.salary = offer.offer_salary;
                    player.contract_years = offer.offer_years;
                    player.seasons_with_team = 1;
                    DatabaseManager.Instance.UpdatePlayer(player);

                    DatabaseManager.Instance.InsertTrade(new TradeData
                    {
                        season_id = _season?.id ?? 0,
                        game_day = _season?.current_game_day ?? 0,
                        game_date = _season?.current_date ?? nowStr,
                        team_id_from = 0,
                        team_id_to = _myTeam.id,
                        player_id = player.id,
                        trade_type = "free_agent"
                    });

                    resultSummary += $"✓ {playerName}: FICHAJE REALIZADO — {salaryText} · {yearsText}\n";

                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Fichaje: {playerName}",
                        body = $"{playerName} ha firmado con tu equipo. Contrato: {salaryText} durante {yearsText}.",
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0
                    });

                    // Activación del hard cap si se usó NT-MLE (>cap, ≤1er apron)
                    if (_myTeam.first_apron_hard_capped == 0
                        && leagueSettings != null
                        && totalPayroll > leagueSettings.salary_cap
                        && totalPayroll <= firstApron)
                    {
                        _myTeam.first_apron_hard_capped = 1;
                        DatabaseManager.Instance.UpdateTeam(_myTeam);
                        string hardCapMsg = $"El fichaje de {playerName} se ha realizado usando la Mid-Level Exception (No Taxpayer). Tu equipo queda sujeto al hard cap del primer apron (${firstApron:N0}).";
                        resultSummary += $"\n⚠ {hardCapMsg}\n";
                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = "Hard cap activado",
                            body = hardCapMsg,
                            game_day = _season.current_game_day,
                            game_date = nowStr,
                            created_at = nowStr,
                            date_sent = nowStr,
                            is_read = 0
                        });
                    }
                }
                else
                {
                    rejectedCount++;
                    player.renewal_cooldown_day = _season.current_game_day + 14;
                    DatabaseManager.Instance.UpdatePlayer(player);

                    resultSummary += $"✗ {playerName}: FICHAJE RECHAZADO — {salaryText} · {yearsText}\n";

                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Fichaje rechazado: {playerName}",
                        body = $"{playerName} ha rechazado tu oferta de {salaryText} durante {yearsText}. Podrás intentarlo de nuevo dentro de 14 días.",
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0
                    });
                }
            }
            else
            {
                // RENEWAL (offer_type == 0)
                hasRenewal = true;
                if (accepted)
                {
                    acceptedCount++;
                    player.salary = offer.offer_salary;
                    player.contract_years = offer.offer_years;
                    player.renewal_cooldown_day = _season.current_game_day + 365;
                    DatabaseManager.Instance.UpdatePlayer(player);

                    resultSummary += $"✓ {playerName}: CONTRATO RENOVADO — {salaryText} · {yearsText}\n";

                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 0,
                        sender_id = 0,
                        title = $"Contrato renovado: {playerName}",
                        body = $"{playerName} ha aceptado tu oferta de renovación. Nuevo contrato: {salaryText} durante {yearsText}.",
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0
                    });
                }
                else
                {
                    rejectedCount++;
                    player.renewal_cooldown_day = _season.current_game_day + 15;
                    DatabaseManager.Instance.UpdatePlayer(player);

                    resultSummary += $"✗ {playerName}: OFERTA RECHAZADA — {salaryText} · {yearsText}\n";

                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 0,
                        sender_id = 0,
                        title = $"Oferta rechazada: {playerName}",
                        body = $"{playerName} ha rechazado tu oferta de {salaryText} durante {yearsText}.",
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0
                    });
                }
            }

            DatabaseManager.Instance.MarkOfferProcessed(offer.id);
        }
        db.Commit();
        batchOk = true;
        }
        catch (System.Exception ex)
        {
        db.Rollback();
        batchErrorMsg = ex.Message;
        Debug.LogError($"[Dashboard] ProcessMaturedOffers error, batch rolled back: {ex.Message}\n{ex.StackTrace}");
        }

        if (!batchOk)
        {
            ShowOfferResultModal("ERROR AL PROCESAR OFERTAS", "Ha ocurrido un error al procesar las ofertas.\n\n" + batchErrorMsg, -1);
            return;
        }

        string title;
        int resultType;
        if (rejectedCount == 0)
        {
            if (acceptedCount == 1)
            {
                title = hasSigning && !hasRenewal ? "FICHAJE REALIZADO" : hasRenewal && !hasSigning ? "CONTRATO RENOVADO" : "OFERTA ACEPTADA";
            }
            else
            {
                title = hasSigning && !hasRenewal ? "FICHAJES REALIZADOS" : hasRenewal && !hasSigning ? "CONTRATOS RENOVADOS" : "OFERTAS ACEPTADAS";
            }
            resultType = 1;
        }
        else if (acceptedCount == 0)
        {
            if (rejectedCount == 1)
            {
                title = hasSigning && !hasRenewal ? "FICHAJE RECHAZADO" : hasRenewal && !hasSigning ? "OFERTA RECHAZADA" : "OFERTAS RECHAZADAS";
            }
            else
            {
                title = hasSigning && !hasRenewal ? "FICHAJES RECHAZADOS" : "OFERTAS RECHAZADAS";
            }
            resultType = -1;
        }
        else
        {
            title = "OFERTAS RESUELTAS";
            resultType = 0;
        }

        string text = acceptedCount + rejectedCount > 1
            ? $"Se han procesado {acceptedCount + rejectedCount} ofertas.\n\n{resultSummary}"
            : resultSummary;

        ShowOfferResultModal(title, text, resultType);
    }

    void ShowOfferResultModal(string title, string text, int resultType)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box");
        if (resultType == 0)
            box.AddToClassList("fired-modal-box--warning");
        else if (resultType == 1)
            box.AddToClassList("fired-modal-box--positive");
        _firedOverlay.Add(box);

        var icon = new VisualElement();
        icon.AddToClassList("fired-modal-icon");
        var iconTex = Resources.Load<Texture2D>("Icons/boton-i-64px");
        if (iconTex != null)
            icon.style.backgroundImage = new StyleBackground(iconTex);
        box.Add(icon);

        var titleLabel = new Label(title);
        titleLabel.AddToClassList("fired-modal-title");
        if (resultType == 0)
            titleLabel.AddToClassList("fired-modal-title--warning");
        else if (resultType == 1)
            titleLabel.AddToClassList("fired-modal-title--positive");
        box.Add(titleLabel);

        var textLabel = new Label(text);
        textLabel.AddToClassList("fired-modal-text");
        textLabel.style.whiteSpace = WhiteSpace.Normal;
        box.Add(textLabel);

        var btn = new Button();
        btn.text = "CONTINUAR";
        btn.AddToClassList("fired-modal-btn");
        if (resultType == 0)
            btn.AddToClassList("fired-modal-btn--warning");
        else if (resultType == 1)
            btn.AddToClassList("fired-modal-btn--positive");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
            ShowNextPendingTradeOffer();
        });
        box.Add(btn);

        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(btn);
    }

    void ShowBudgetWarningModal(int num)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box");
        box.AddToClassList("fired-modal-box--warning");
        _firedOverlay.Add(box);

        var icon = new VisualElement();
        icon.AddToClassList("fired-modal-icon");
        box.Add(icon);

        var title = new Label("AVISO FINANCIERO");
        title.AddToClassList("fired-modal-title");
        title.AddToClassList("fired-modal-title--warning");
        box.Add(title);

        var text = new Label(
            $"El presupuesto del club está en números rojos.\n\n" +
            $"Aviso {num} de 3."
        );
        text.AddToClassList("fired-modal-text");
        box.Add(text);

        var btn = new Button();
        btn.text = "CONTINUAR";
        btn.AddToClassList("fired-modal-btn");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
        });
        box.Add(btn);
    }

    void ShowBudgetFiredModal()
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box");
        _firedOverlay.Add(box);

        var icon = new VisualElement();
        icon.AddToClassList("fired-modal-icon");
        box.Add(icon);

        var title = new Label("DESPIDO");
        title.AddToClassList("fired-modal-title");
        box.Add(title);

        var text = new Label(
            "La directiva ha decidido prescindir de tus servicios\n" +
            "debido a la mala gestión financiera del club."
        );
        text.AddToClassList("fired-modal-text");
        box.Add(text);

        var btn = new Button();
        btn.text = "IR AL MENÚ PRINCIPAL";
        btn.AddToClassList("fired-modal-btn");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        box.Add(btn);
    }

    List<int> GetEmptyStarterSlots()
    {
        var lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        if (lineup.Count == 0) return new List<int>();

        var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var playerMap = allPlayers.ToDictionary(p => p.id);

        var filledSlots = new HashSet<int>();
        foreach (var l in lineup)
            if (l.slot == 0 && playerMap.ContainsKey(l.player_id))
                filledSlots.Add(l.slot_index);

        var emptySlots = new List<int>();
        for (int i = 0; i < 5; i++)
            if (!filledSlots.Contains(i))
                emptySlots.Add(i);
        return emptySlots;
    }

    List<(LineupData li, PlayerData p)> GetInjuredActiveLineupPlayers()
    {
        var lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        if (lineup.Count == 0) return new List<(LineupData, PlayerData)>();

        var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var playerMap = allPlayers.ToDictionary(p => p.id);

        return lineup
            .Where(l => l.slot <= 1)
            .Select(l => (li: l, p: playerMap.GetValueOrDefault(l.player_id)))
            .Where(x => x.p != null && x.p.injury_days > 0)
            .ToList();
    }

    void ShowEmptyLineupModal(List<int> emptySlots)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box");
        box.AddToClassList("fired-modal-box--warning");
        _firedOverlay.Add(box);

        var title = new Label("5 TITULAR INCOMPLETO");
        title.AddToClassList("fired-modal-title");
        title.AddToClassList("fired-modal-title--warning");
        box.Add(title);

        var text = new Label("Hay puestos del quinteto titular sin jugador asignado. Revisa la convocatoria antes del partido:");
        text.AddToClassList("fired-modal-text");
        box.Add(text);

        var posNames = new[] { "Base", "Escolta", "Alero", "Ala-Pívot", "Pívot" };
        var list = new VisualElement();
        list.AddToClassList("injured-modal-list");
        foreach (var si in emptySlots)
        {
            var row = new Label(posNames[si]);
            row.AddToClassList("injured-modal-player-row");
            list.Add(row);
        }
        box.Add(list);

        var btnGroup = new VisualElement();
        btnGroup.AddToClassList("injured-modal-btn-group");

        var manualBtn = new Button();
        manualBtn.text = "REVISAR MANUALMENTE";
        manualBtn.AddToClassList("injured-modal-btn");
        manualBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _emptyLineupGoToQuinteto = true;
            _emptyLineupModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        btnGroup.Add(manualBtn);

        var autoBtn = new Button();
        autoBtn.text = "AUTO-RELLENAR";
        autoBtn.AddToClassList("injured-modal-btn");
        autoBtn.AddToClassList("injured-modal-btn--primary");
        autoBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            var injuredIds = new HashSet<int>(allPlayers
                .Where(p => p.injury_days > 0)
                .Select(p => p.id));
            DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, allPlayers, injuredIds);
            _emptyLineupModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        btnGroup.Add(autoBtn);

        box.Add(btnGroup);
    }

    void ShowInjuredLineupModal(List<(LineupData li, PlayerData p)> injured)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box");
        box.AddToClassList("fired-modal-box--warning");
        _firedOverlay.Add(box);

        var title = new Label("JUGADORES LESIONADOS");
        title.AddToClassList("fired-modal-title");
        title.AddToClassList("fired-modal-title--warning");
        box.Add(title);

        var text = new Label("Tienes jugadores lesionados convocados.\nResuelve antes del partido:");
        text.AddToClassList("injured-modal-text");
        box.Add(text);

        var list = new VisualElement();
        list.AddToClassList("injured-modal-list");
        foreach (var (li, p) in injured)
        {
            var slotLabel = li.slot == 0 ? "TITULAR" : "BANQUILLO";
            var row = new Label($"{p.first_name} {p.last_name} — {PositionCodes.GetShort(p.position)} ({slotLabel})");
            row.AddToClassList("injured-modal-player-row");
            list.Add(row);
        }
        box.Add(list);

        var btnGroup = new VisualElement();
        btnGroup.AddToClassList("injured-modal-btn-group");

        var manualBtn = new Button();
        manualBtn.text = "CAMBIAR MANUALMENTE";
        manualBtn.AddToClassList("injured-modal-btn");
        manualBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _injuredModalGoToQuinteto = true;
            _injuredModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        btnGroup.Add(manualBtn);

        var autoBtn = new Button();
        autoBtn.text = "CAMBIAR AUTOMÁTICAMENTE";
        autoBtn.AddToClassList("injured-modal-btn");
        autoBtn.AddToClassList("injured-modal-btn--primary");
        autoBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            AutoFixInjuredLineup();
            _injuredModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        btnGroup.Add(autoBtn);

        box.Add(btnGroup);
    }

    void ShowPendingRecoveryModal()
    {
        if (_pendingRecoveredIds.Count == 0) return;

        var players = new List<PlayerData>();
        foreach (int id in _pendingRecoveredIds)
        {
            var p = DatabaseManager.Instance.GetPlayerById(id);
            if (p != null) players.Add(p);
        }
        if (players.Count == 0)
        {
            _pendingRecoveredIds.Clear();
            return;
        }

        _pendingRecoveredIds.Clear();
        ShowRecoveredModal(players);
    }

    void ShowRecoveredModal(List<PlayerData> recovered)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box");
        box.AddToClassList("fired-modal-box--positive");
        _firedOverlay.Add(box);

        var title = new Label("JUGADOR(ES) RECUPERADO(S)");
        title.AddToClassList("fired-modal-title");
        title.AddToClassList("fired-modal-title--positive");
        box.Add(title);

        var text = new Label("Los siguientes jugadores se han recuperado de sus lesiones y vuelven a estar disponibles:");
        text.AddToClassList("injured-modal-text");
        box.Add(text);

        var list = new VisualElement();
        list.AddToClassList("injured-modal-list");
        foreach (var p in recovered)
        {
            var row = new Label($"{p.first_name} {p.last_name} — {PositionCodes.GetShort(p.position)}");
            row.AddToClassList("injured-modal-player-row");
            list.Add(row);
        }
        box.Add(list);

        var btnGroup = new VisualElement();
        btnGroup.AddToClassList("injured-modal-btn-group");

        var goBtn = new Button();
        goBtn.text = "IR A QUINTETO";
        goBtn.AddToClassList("injured-modal-btn");
        goBtn.style.backgroundColor = new StyleColor(new Color32(42, 95, 201, 255));
        goBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
            ScreenManager.Instance.GoTo(GameScreen.Quinteto);
        });
        btnGroup.Add(goBtn);

        var closeBtn = new Button();
        closeBtn.text = "CERRAR";
        closeBtn.AddToClassList("injured-modal-btn");
        closeBtn.style.marginLeft = 8;
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
        });
        btnGroup.Add(closeBtn);

        box.Add(btnGroup);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(goBtn);
            CursorManager.Instance.RegisterHandCursor(closeBtn);
        }
    }

    void CheckTradeDeadlineModal()
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return;
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return;
        if (date.Month == 2 && date.Day == 1 && date.Year == _season.year_end)
        {
            // Check if the message was already sent this season to avoid repeating the modal
            var existing = DatabaseManager.Instance.Db.Table<MessageData>()
                .FirstOrDefault(m => m.manager_id == _manager.id
                                  && m.title == "Última semana de traspasos"
                                  && m.game_date == _season.current_date);
            if (existing != null) return;

            ShowTradeDeadlineModal();
        }
    }

    void ShowTradeDeadlineModal()
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box");
        box.AddToClassList("fired-modal-box--warning");
        _firedOverlay.Add(box);

        var title = new Label("CIERRE DE MERCADO");
        title.AddToClassList("fired-modal-title");
        title.AddToClassList("fired-modal-title--warning");
        box.Add(title);

        var text = new Label("El período de traspasos finaliza el 8 de febrero. Aún estás a tiempo de realizar operaciones.");
        text.AddToClassList("fired-modal-text");
        box.Add(text);

        var btnGroup = new VisualElement();
        btnGroup.AddToClassList("injured-modal-btn-group");

        var marketBtn = new Button();
        marketBtn.text = "IR AL MERCADO";
        marketBtn.AddToClassList("injured-modal-btn");
        marketBtn.style.backgroundColor = new StyleColor(new Color32(42, 95, 201, 255));
        marketBtn.style.borderTopColor = new StyleColor(new Color32(42, 95, 201, 255));
        marketBtn.style.borderBottomColor = new StyleColor(new Color32(42, 95, 201, 255));
        marketBtn.style.borderLeftColor = new StyleColor(new Color32(42, 95, 201, 255));
        marketBtn.style.borderRightColor = new StyleColor(new Color32(42, 95, 201, 255));
        marketBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
            ScreenManager.Instance.GoTo(GameScreen.Market);
        });
        btnGroup.Add(marketBtn);

        var closeBtn = new Button();
        closeBtn.text = "CERRAR";
        closeBtn.AddToClassList("injured-modal-btn");
        closeBtn.style.marginLeft = 8;
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
        });
        btnGroup.Add(closeBtn);

        box.Add(btnGroup);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(marketBtn);
            CursorManager.Instance.RegisterHandCursor(closeBtn);
        }
    }

    void AutoFixInjuredLineup()
    {
        var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var playerMap = allPlayers.ToDictionary(p => p.id);
        var lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        if (lineup.Count == 0) return;

        // Find injured players in active slots
        var injuredActive = lineup.Where(l => l.slot <= 1 && playerMap.TryGetValue(l.player_id, out var p) && p.injury_days > 0).ToList();
        if (injuredActive.Count == 0) return;

        var lineupIds = new HashSet<int>(lineup.Select(l => l.player_id));

        // Separate lineup by slot
        var benchLineup = lineup.Where(l => l.slot == 1).ToList();
        var inactiveLineup = lineup.Where(l => l.slot == 2).ToList();

        // Non-injured bench players (available for promotion)
        var benchPool = benchLineup
            .Where(l => playerMap.TryGetValue(l.player_id, out var p) && p.injury_days == 0)
            .Select(l => playerMap[l.player_id])
            .ToList();

        // Non-injured inactive players (available for bench backfill)
        var inactivePool = inactiveLineup
            .Where(l => playerMap.TryGetValue(l.player_id, out var p) && p.injury_days == 0)
            .Select(l => playerMap[l.player_id])
            .ToList();

        // Unassigned non-injured players
        var unassignedPool = allPlayers
            .Where(p => p.injury_days == 0 && !lineupIds.Contains(p.id))
            .OrderByDescending(p => p.overall)
            .ToList();

        var usedInFix = new HashSet<int>();
        var benchVacated = new List<int>(); // bench slot_indices that need backfill

        // ── 1. Fix injured starters ──
        var injuredStarters = injuredActive.Where(l => l.slot == 0).OrderBy(l => l.slot_index).ToList();
        int nextInactiveIdx = inactiveLineup.DefaultIfEmpty().Max(l => l?.slot_index ?? -1) + 1;
        foreach (var li in injuredStarters)
        {
            var injuredPlayer = playerMap[li.player_id];
            DatabaseManager.Instance.SetPlayerSlot(li.player_id, _myTeam.id, 2, nextInactiveIdx++);
            lineupIds.Remove(li.player_id);
            usedInFix.Add(li.player_id);

            // Try to promote from bench first, then inactive, then unassigned
            var replacement = PickBestReplacement(injuredPlayer.position, benchPool, usedInFix, true)
                          ?? PickBestReplacement(injuredPlayer.position, inactivePool, usedInFix, true)
                          ?? PickBestReplacement(injuredPlayer.position, unassignedPool, usedInFix, true);

            if (replacement != null)
            {
                usedInFix.Add(replacement.id);
                // Check if replacement came from bench
                var oldBenchLi = benchLineup.FirstOrDefault(l => l.player_id == replacement.id);
                if (oldBenchLi != null)
                    benchVacated.Add(oldBenchLi.slot_index);
                DatabaseManager.Instance.SetPlayerSlot(replacement.id, _myTeam.id, 0, li.slot_index);
                lineupIds.Add(replacement.id);
            }
        }

        // ── 2. Fix injured bench players ──
        if (inactiveLineup.Count > 0)
            nextInactiveIdx = inactiveLineup.Max(l => l.slot_index) + 1;

        var injuredBench = injuredActive.Where(l => l.slot == 1).OrderBy(l => l.slot_index).ToList();
        foreach (var li in injuredBench)
        {
            var injuredPlayer = playerMap[li.player_id];
            DatabaseManager.Instance.SetPlayerSlot(li.player_id, _myTeam.id, 2, nextInactiveIdx++);
            lineupIds.Remove(li.player_id);
            usedInFix.Add(li.player_id);

            // Fill bench from inactive first, then unassigned
            var replacement = inactivePool.FirstOrDefault(c => !usedInFix.Contains(c.id))
                          ?? unassignedPool.FirstOrDefault(c => !usedInFix.Contains(c.id));

            if (replacement != null)
            {
                usedInFix.Add(replacement.id);
                DatabaseManager.Instance.SetPlayerSlot(replacement.id, _myTeam.id, 1, li.slot_index);
                lineupIds.Add(replacement.id);
            }
        }

        // ── 3. Backfill bench slots vacated by promotions ──
        foreach (var vacatedIdx in benchVacated)
        {
            var replacement = inactivePool.FirstOrDefault(c => !usedInFix.Contains(c.id))
                          ?? unassignedPool.FirstOrDefault(c => !usedInFix.Contains(c.id));

            if (replacement != null)
            {
                usedInFix.Add(replacement.id);
                DatabaseManager.Instance.SetPlayerSlot(replacement.id, _myTeam.id, 1, vacatedIdx);
                lineupIds.Add(replacement.id);
            }
        }
    }

    PlayerData PickBestReplacement(string injuredPos, List<PlayerData> pool, HashSet<int> used, bool preferExact)
    {
        var posOrder = PositionCodes.Order;
        int injuredIdx = System.Array.IndexOf(posOrder, injuredPos);
        var nearby = new List<string>();
        if (injuredIdx > 0) nearby.Add(posOrder[injuredIdx - 1]);
        if (injuredIdx < posOrder.Length - 1) nearby.Add(posOrder[injuredIdx + 1]);

        var available = pool.Where(c => !used.Contains(c.id)).ToList();

        if (preferExact)
        {
            var exact = available.Where(c => c.position == injuredPos).OrderByDescending(c => c.overall).FirstOrDefault();
            if (exact != null) return exact;
        }

        foreach (var nearPos in nearby)
        {
            var match = available.Where(c => c.position == nearPos).OrderByDescending(c => c.overall).FirstOrDefault();
            if (match != null) return match;
        }

        return available.OrderByDescending(c => c.overall).FirstOrDefault();
    }

    void ShowFiredModal()
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box");
        _firedOverlay.Add(box);

        var icon = new VisualElement();
        icon.AddToClassList("fired-modal-icon");
        box.Add(icon);

        var title = new Label("DESPIDO");
        title.AddToClassList("fired-modal-title");
        box.Add(title);

        var text = new Label(
            "La directiva ha decidido prescindir de tus servicios.\n\n" +
            "Los resultados de la temporada no han cumplido las expectativas mínimas."
        );
        text.AddToClassList("fired-modal-text");
        box.Add(text);

        var btn = new Button();
        btn.text = "IR AL MENÚ PRINCIPAL";
        btn.AddToClassList("fired-modal-btn");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        box.Add(btn);
    }

    int DateToGameDay(System.DateTime date)
    {
        var seasonStart = new System.DateTime(_season.year_start, 10, 22);
        if (date >= seasonStart)
            return (int)(date - seasonStart).TotalDays + 1;
        else
            return -(int)(seasonStart - date).TotalDays;
    }

    int FindNextGameDay()
    {
        bool anyUnplayed = DatabaseManager.Instance.Db.Table<GameData>()
            .Any(g => g.manager_id == _manager.id && g.is_played == 0);
        if (!anyUnplayed) return 0;

        // New system: use current_date for day-by-day advancement
        if (!string.IsNullOrEmpty(_season.current_date))
        {
            var currentDate = System.DateTime.Parse(_season.current_date);

            // Only skip the Feb break if there are no unplayed games on that date
            // (the All-Star game is scheduled during the break, Feb 13)
            bool hasUnplayedToday = DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == _manager.id
                       && g.game_date == _season.current_date
                       && g.is_played == 0);

            if (!hasUnplayedToday && currentDate.Month == 2 && currentDate.Day >= 8 && currentDate.Day <= 14)
                currentDate = new System.DateTime(currentDate.Year, 2, 15);

            return DateToGameDay(currentDate);
        }

        // Fallback for old saves without current_date
        if (_season.current_game_day == 0)
        {
            var nextPreseason = DatabaseManager.Instance.Db.Table<GameData>()
                .Where(g => g.manager_id == _manager.id
                         && g.is_played == 0
                         && g.game_day < 0)
                .OrderByDescending(g => g.game_day)
                .FirstOrDefault();
            if (nextPreseason != null) return nextPreseason.game_day;
            return 1;
        }

        return _season.current_game_day + 1;
    }

    List<PlayerData> ProcessInjuries()
    {
        var allPlayers = DatabaseManager.Instance.GetAllTeams().SelectMany(t =>
            DatabaseManager.Instance.GetPlayersByTeam(t.id)).ToList();
        var recovered = new List<PlayerData>();
        foreach (var p in allPlayers)
        {
            if (p.injury_days > 0)
            {
                p.injury_days--;
                if (p.injury_days <= 0)
                {
                    p.injury_days = 0;
                    p.injury_type = "";
                    p.treated = 0;
                    if (p.team_id == _myTeam.id)
                        recovered.Add(p);
                }
                DatabaseManager.Instance.UpdatePlayer(p);
            }
        }
        return recovered;
    }

    void ProcessFisicoRecovery()
    {
        if (_myTeam == null) return;
        var allPlayers = DatabaseManager.Instance.GetAllTeams().SelectMany(t =>
            DatabaseManager.Instance.GetPlayersByTeam(t.id)).ToList();
        foreach (var p in allPlayers)
        {
            if (p.injury_days > 0) continue;
            p.fisico = Mathf.Min(99, p.fisico + 8);
            DatabaseManager.Instance.UpdatePlayer(p);
        }
    }

    void ProcessScouts()
    {
        if (_season == null) return;
        var scouts = DatabaseManager.Instance.GetScoutsByTeam(_myTeam.id)
            .Where(s => s.completed == 0 && _season.current_game_day >= s.end_day)
            .ToList();
        foreach (var s in scouts)
        {
            s.completed = 1;
            DatabaseManager.Instance.UpdateScout(s);
        }
    }

    void ProcessTraining()
    {
        if (_season == null) return;
        var trainings = DatabaseManager.Instance.GetTeamTraining(_myTeam.id)
            .Where(t => _season.current_game_day >= t.start_day + t.duration)
            .ToList();
        foreach (var t in trainings)
        {
            DatabaseManager.Instance.CompleteTrainingAndApply(t);
            var player = DatabaseManager.Instance.GetPlayerById(t.player_id);
            string attrName = t.attribute.Replace("_", " ").ToUpper();
            if (player != null)
            {
                DatabaseManager.Instance.AddMessage(new MessageData
                {
                    manager_id = _manager.id,
                    sender_type = 0,
                    sender_id = 0,
                    title = "Entrenamiento completado",
                    body = $"{player.first_name} {player.last_name} ha completado el entrenamiento de {attrName} y ha mejorado su rendimiento.",
                    game_day = _season.current_game_day,
                    game_date = _season.current_date,
                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    is_read = 0
                });
            }
        }
    }

    void ProcessRenovations()
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        foreach (var team in allTeams)
        {
            if (team.arena_renovation_end_day > 0 && _season.current_game_day >= team.arena_renovation_end_day)
            {
                ApplyRenovation(team);
            }
        }
    }

    // ═══════════════════════════════════════════
    //  AI TRANSFERS
    // ═══════════════════════════════════════════

    void ProcessAITransfers(int gameDay)
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return;

        // Only run every ~10 game days
        int lastDay = _season.last_ai_trade_day;
        Debug.Log($"[AITrades] ProcessAITransfers called. gameDay={gameDay} lastDay={lastDay} diff={gameDay - lastDay}");
        if (gameDay - lastDay < 15) return;

        // Check if in transfer window (1 Sep year_start to 8 Feb year_end)
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return;
        var openDate = new System.DateTime(_season.year_start, 9, 1);
        var closeDate = new System.DateTime(_season.year_end, 2, 8);
        if (date < openDate || date > closeDate)
        {
            Debug.Log($"[AITrades] Outside transfer window: date={_season.current_date} window={openDate:dd/MMM} - {closeDate:dd/MMM}");
            return;
        }

        _season.last_ai_trade_day = gameDay;
        Debug.Log($"[AITrades] Starting cycle. Date={_season.current_date} gameDay={gameDay}");

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var seasonId = _season.id;
        var freeAgentsAll = DatabaseManager.Instance.GetFreeAgents();

        int teamsAttempted = 0, teamsTraded = 0, teamsFAd = 0;
        const int maxTrades = 2;

        foreach (var team in allTeams)
        {
            if (team.id == _myTeam.id) continue;

            if (teamsTraded >= maxTrades) break;

            if (Random.Range(0f, 1f) > 0.3f) continue;

            var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            if (roster.Count < 10) continue;

            var weakestPos = GetWeakestPosition(roster);
            if (weakestPos == null) continue;

            teamsAttempted++;

            // If roster < 12, sign free agent directly without trying trade
            if (roster.Count < 12)
            {
                Debug.Log($"[AITrades] {team.name} roster={roster.Count} < 12, direct FA at {weakestPos}");
                TrySignFreeAgent(team, roster, weakestPos, freeAgentsAll, seasonId, gameDay);
                teamsFAd++;
                continue;
            }

            bool traded = TryFindAITrade(team, roster, weakestPos, seasonId, gameDay);

            if (traded)
            {
                teamsTraded++;
            }
            else if (Random.Range(0f, 1f) < 0.3f)
            {
                Debug.Log($"[AITrades] {team.name} trade failed, trying FA at {weakestPos}");
                TrySignFreeAgent(team, roster, weakestPos, freeAgentsAll, seasonId, gameDay);
                teamsFAd++;
            }
        }

        Debug.Log($"[AITrades] Cycle complete: {teamsAttempted} teams attempted, {teamsTraded} trades, {teamsFAd} FA signings");
        GenerateAITradeOffersForPlayer(gameDay, seasonId);
    }

    void ProcessStarFreeAgentSignings(int gameDay)
    {
        if (_season == null) return;

        var freeAgents = DatabaseManager.Instance.GetFreeAgents();
        var stars = freeAgents
            .Where(p => p.GetCalculatedAverage() > 80)
            .OrderByDescending(p => p.GetCalculatedAverage())
            .ToList();
        if (stars.Count == 0) return;

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        var allTeams = DatabaseManager.Instance.GetAllTeams()
            .Where(t => t.id != _myTeam.id)
            .ToList();

        var teamAvgs = new Dictionary<int, float>();
        foreach (var team in allTeams)
        {
            var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            teamAvgs[team.id] = roster.Count > 0 ? (float)roster.Average(p => p.GetCalculatedAverage()) : 0f;
        }
        var teamsByAvg = allTeams.OrderByDescending(t => teamAvgs[t.id]).ToList();

        var pendingFAIds = DatabaseManager.Instance.GetPendingFAPlayerIds(_manager.id);

        foreach (var star in stars)
        {
            if (pendingFAIds.Contains(star.id))
            {
                Debug.Log($"[StarFA] Skip {star.first_name} {star.last_name} — user has pending offer");
                continue;
            }

            bool signed = false;
            foreach (var team in teamsByAvg)
            {
                var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
                if (roster.Count >= TradeHelper.MAX_ROSTER) continue;

                long payroll = roster.Sum(p => p.salary);
                if (salaryCap - payroll < star.salary) continue;

                star.team_id = team.id;
                int years = star.age > 35 ? 1 : star.age > 32 ? 2 : star.age > 28 ? 3 : star.age > 25 ? 4 : 5;
                star.contract_years = years;
                DatabaseManager.Instance.UpdatePlayer(star);

                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = _season.id,
                    game_day = gameDay,
                    game_date = _season.current_date,
                    team_id_from = 0,
                    team_id_to = team.id,
                    player_id = star.id,
                    trade_type = "free_agent"
                });

                Debug.Log($"[StarFA] {team.name} signed star {star.first_name} {star.last_name} (OVR {star.GetCalculatedAverage()})");
                signed = true;
                break;
            }

            if (!signed)
            {
                Debug.Log($"[StarFA] No team could sign {star.first_name} {star.last_name} (OVR {star.GetCalculatedAverage()}) — stays in FA");
            }
        }
    }

    void ProcessPsychologistMorale()
    {
        var staff = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        var psicologo = staff.FirstOrDefault(e => e.position == "PSICOLOGO");

        int recovery = psicologo?.reputation switch
        {
            5 => 2,
            4 => 1,
            3 => 1,
            _ => 0
        };

        if (recovery <= 0) return;

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        foreach (var p in players)
        {
            if (p.morale >= 50) continue;
            int newMorale = Mathf.Min(50, p.morale + recovery);
            DatabaseManager.Instance.UpdatePlayerMorale(p.id, newMorale);
        }
    }

    float GetArenaTicketMultiplier()
    {
        var staff = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        var pabellon = staff.FirstOrDefault(e => e.position == "PABELLON");
        return pabellon?.reputation switch
        {
            5 => 1.20f,
            4 => 1.15f,
            3 => 1.10f,
            2 => 1.06f,
            1 => 1.03f,
            _ => 1.0f
        };
    }

    string GetWeakestPosition(List<PlayerData> roster)
    {
        if (roster == null || roster.Count == 0) return null;

        var positions = PositionCodes.Order;
        string weakest = null;
        float lowestAvg = float.MaxValue;

        foreach (var pos in positions)
        {
            var atPos = roster.Where(p => p.position == pos && p.injury_days == 0).ToList();
            if (atPos.Count == 0) return pos;
            float avg = (float)atPos.Average(p => p.overall);
            if (avg < lowestAvg)
            {
                lowestAvg = avg;
                weakest = pos;
            }
        }
        return weakest;
    }

    bool TryFindAITrade(TeamData teamA, List<PlayerData> rosterA, string targetPos,
                        int seasonId, int gameDay)
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams()
            .Where(t => t.id != _myTeam.id && t.id != teamA.id)
            .OrderBy(_ => Random.Range(0, 1000))
            .ToList();

        foreach (var teamB in allTeams)
        {
            var rosterB = DatabaseManager.Instance.GetPlayersByTeam(teamB.id);

            var target = rosterB
                .Where(p => p.position == targetPos && p.injury_days == 0)
                .OrderByDescending(p => p.overall)
                .FirstOrDefault(p => p.overall <= 86);
            if (target == null) continue;

            var candidates = rosterA
                .Where(p => p.injury_days == 0 && p.id != target.id
                            && rosterA.Count(r => r.position == p.position) >= (
                                p.position == targetPos ? 1 : 2))
                .OrderBy(p => p.overall)
                .ToList();
            if (candidates.Count == 0) continue;

            // Try 1-for-1
            foreach (var c in candidates)
            {
                if (TryExecuteTrade(teamA, rosterA, teamB, rosterB,
                        new List<PlayerData> { c },
                        new List<PlayerData> { target },
                        seasonId, gameDay))
                    return true;
            }

            // Try 2-for-1
            for (int i = 0; i < candidates.Count; i++)
            {
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    if (TryExecuteTrade(teamA, rosterA, teamB, rosterB,
                            new List<PlayerData> { candidates[i], candidates[j] },
                            new List<PlayerData> { target },
                            seasonId, gameDay))
                        return true;
                }
            }
        }
        return false;
    }

    bool TryExecuteTrade(TeamData teamA, List<PlayerData> rosterA,
                         TeamData teamB, List<PlayerData> rosterB,
                         List<PlayerData> aSelected, List<PlayerData> bSelected,
                         int seasonId, int gameDay,
                         List<DraftPickData> aSelectedPicks = null,
                         List<DraftPickData> bSelectedPicks = null)
    {
        var aPayroll = rosterA.Sum(p => p.salary);
        var bPayroll = rosterB.Sum(p => p.salary);
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long firstApron = leagueSettings?.apron > 0 ? leagueSettings.apron : TradeHelper.FIRST_APRON;
        long secondApron = leagueSettings?.repeater_apron > 0 ? leagueSettings.repeater_apron : TradeHelper.SECOND_APRON;
        long luxuryTax = leagueSettings?.luxury_tax > 0 ? leagueSettings.luxury_tax : TradeHelper.LUXURY_TAX;
        var errors = TradeHelper.ValidateTrade(
            aSelected, bSelected,
            rosterA.Count, rosterB.Count,
            teamB.name, bPayroll,
            teamA.name, aPayroll,
            teamA.first_apron_hard_capped == 1,
            teamB.first_apron_hard_capped == 1,
            firstApron, secondApron, luxuryTax);

        if (errors.Count > 0) return false;

        foreach (var p in aSelected)
        {
            p.team_id = teamB.id;
            DatabaseManager.Instance.UpdatePlayer(p);
            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = seasonId,
                game_day = gameDay,
                game_date = _season.current_date,
                team_id_from = teamA.id,
                team_id_to = teamB.id,
                player_id = p.id,
                trade_type = "trade",
                partner_player_id = bSelected.First().id
            });
        }

        foreach (var p in bSelected)
        {
            p.team_id = teamA.id;
            DatabaseManager.Instance.UpdatePlayer(p);
            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = seasonId,
                game_day = gameDay,
                game_date = _season.current_date,
                team_id_from = teamB.id,
                team_id_to = teamA.id,
                player_id = p.id,
                trade_type = "trade",
                partner_player_id = aSelected.First().id
            });
        }

        // Transfer picks
        if (aSelectedPicks != null && aSelectedPicks.Count > 0)
        {
            var ids = aSelectedPicks.Select(p => p.id).ToList();
            DatabaseManager.Instance.TransferDraftPicks(ids, teamA.id, teamB.id);
            foreach (var pk in aSelectedPicks)
            {
                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = seasonId,
                    game_day = gameDay,
                    game_date = _season.current_date,
                    team_id_from = teamA.id,
                    team_id_to = teamB.id,
                    player_id = 0,
                    pick_id = pk.id,
                    trade_type = "pick_trade"
                });
            }
        }
        if (bSelectedPicks != null && bSelectedPicks.Count > 0)
        {
            var ids = bSelectedPicks.Select(p => p.id).ToList();
            DatabaseManager.Instance.TransferDraftPicks(ids, teamB.id, teamA.id);
            foreach (var pk in bSelectedPicks)
            {
                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = seasonId,
                    game_day = gameDay,
                    game_date = _season.current_date,
                    team_id_from = teamB.id,
                    team_id_to = teamA.id,
                    player_id = 0,
                    pick_id = pk.id,
                    trade_type = "pick_trade"
                });
            }
        }

        var aNames = string.Join(", ", aSelected.Select(p => $"{p.first_name} {p.last_name}"));
        var bNames = string.Join(", ", bSelected.Select(p => $"{p.first_name} {p.last_name}"));
        var aPicksLog = aSelectedPicks != null && aSelectedPicks.Count > 0
            ? " + " + string.Join(", ", aSelectedPicks.Select(p => $"R{p.round}#{p.pick_number}")) : "";
        var bPicksLog = bSelectedPicks != null && bSelectedPicks.Count > 0
            ? " + " + string.Join(", ", bSelectedPicks.Select(p => $"R{p.round}#{p.pick_number}")) : "";
        Debug.Log($"[AI Trade] {teamA.name} ↔ {teamB.name}: {aNames}{aPicksLog} for {bNames}{bPicksLog}");
        return true;
    }

    void TrySignFreeAgent(TeamData team, List<PlayerData> roster, string targetPos,
                          List<PlayerData> freeAgents, int seasonId, int gameDay)
    {
        if (roster.Count >= TradeHelper.MAX_ROSTER) return;

        var pendingFAIds = DatabaseManager.Instance.GetPendingFAPlayerIds(_manager.id);

        var candidates = freeAgents
            .Where(p => p.position == targetPos && p.salary <= team.budget && !pendingFAIds.Contains(p.id))
            .OrderByDescending(p => p.overall)
            .ToList();

        if (candidates.Count == 0)
        {
            // Try any position if no match at targetPos
            candidates = freeAgents
                .Where(p => p.salary <= team.budget && !pendingFAIds.Contains(p.id))
                .OrderByDescending(p => p.overall)
                .ToList();
        }

        foreach (var player in candidates)
        {
            int chance = (int)Mathf.Clamp(team.reputation * 20 - player.overall * 0.5f + 30, 5, 95);
            if (Random.Range(0, 100) >= chance) continue;

            player.team_id = team.id;
            int years = player.age > 35 ? 1 : player.age > 32 ? 2 : player.age > 28 ? 3 : player.age > 25 ? 4 : 5;
            player.salary += 2_000_000;
            player.contract_years = years;
            DatabaseManager.Instance.UpdatePlayer(player);

            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = seasonId,
                game_day = gameDay,
                game_date = _season.current_date,
                team_id_from = 0,
                team_id_to = team.id,
                player_id = player.id,
                trade_type = "free_agent"
            });

            Debug.Log($"[AI FA] {team.name} signed {player.first_name} {player.last_name} ({player.position}, {player.overall} OVR)");
            break;
        }
    }

    // ═══════════════════════════════════════════
    //  AI TRADE OFFERS TO PLAYER
    // ═══════════════════════════════════════════

    void GenerateAITradeOffersForPlayer(int gameDay, int seasonId)
    {
        var existingPending = DatabaseManager.Instance.GetPendingTradeOffers(_manager.id);
        if (existingPending.Count > 0) return;

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var targetedIds = new HashSet<int>();

        foreach (var aiTeam in allTeams.OrderBy(_ => Random.Range(0, 1000)))
        {
            if (aiTeam.id == _myTeam.id) continue;
            if (Random.Range(0f, 1f) > 0.50f) continue;

            var aiRoster = DatabaseManager.Instance.GetPlayersByTeam(aiTeam.id);
            if (aiRoster.Count < 11) continue;

            var userRoster = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            if (userRoster.Count < 10 || userRoster.Count >= TradeHelper.MAX_ROSTER) continue;

            var userHealthy = userRoster.Where(p => p.injury_days == 0).ToList();
            if (userHealthy.Count == 0) continue;

            var target = PickTradeTarget(userHealthy, aiRoster, targetedIds);
            if (target == null) continue;

            targetedIds.Add(target.id);

            var aiHealthy = aiRoster
                .Where(p => p.injury_days == 0 && p.id != target.id)
                .ToList();
            if (aiHealthy.Count == 0) continue;

            var offerPack = BuildOfferPackage(aiHealthy, aiRoster.Count, target, userRoster, aiTeam);
            if (offerPack == null) continue;

            // If salary gap is large, try sweetening with a draft pick
            long packSalary = offerPack.Sum(p => p.salary);
            List<int> offeredPickIds = new List<int>();
            if (target.salary > packSalary * 1.5f)
            {
                var aiPicks = DatabaseManager.Instance.GetDraftPicksForTeam(aiTeam.id);
                var futurePick = aiPicks
                    .OrderByDescending(p => p.season_id)
                    .ThenBy(p => p.round)
                    .ThenBy(p => p.pick_number)
                    .FirstOrDefault();
                if (futurePick != null)
                    offeredPickIds.Add(futurePick.id);
            }

            var wantedIds = new List<int> { target.id };
            var offeredIds = offerPack.Select(p => p.id).ToList();
            DatabaseManager.Instance.AddTradeOffer(new TradeOfferData
            {
                manager_id = _manager.id,
                team_id_from = aiTeam.id,
                player_ids_out = TradeOfferData.JoinIds(wantedIds),
                player_ids_in = TradeOfferData.JoinIds(offeredIds),
                pick_ids_out = TradeOfferData.JoinIds(new List<int>()),
                pick_ids_in = TradeOfferData.JoinIds(offeredPickIds),
                day_sent = gameDay,
                processed = 0
            });
            break;
        }
    }

    PlayerData PickTradeTarget(List<PlayerData> userHealthy, List<PlayerData> aiRoster,
                                HashSet<int> excludedIds)
    {
        var roll = Random.Range(0f, 1f);

        if (roll < 0.35f)
        {
            var aiWeakPos = GetWeakestPosition(aiRoster);
            if (aiWeakPos != null)
            {
                var candidates = userHealthy
                    .Where(p => p.position == aiWeakPos && !excludedIds.Contains(p.id))
                    .OrderByDescending(p => p.overall)
                    .ToList();
                if (candidates.Count > 0)
                    return candidates[Random.Range(0, Mathf.Min(3, candidates.Count))];
            }
        }

        if (roll < 0.60f)
        {
            var candidates = userHealthy
                .Where(p => !excludedIds.Contains(p.id))
                .OrderByDescending(p => p.overall)
                .ToList();
            if (candidates.Count > 0)
                return candidates[Random.Range(0, Mathf.Min(3, candidates.Count))];
        }

        if (roll < 0.80f)
        {
            var candidates = userHealthy
                .Where(p => !excludedIds.Contains(p.id))
                .OrderByDescending(p => p.salary)
                .ToList();
            if (candidates.Count > 0)
                return candidates[Random.Range(0, Mathf.Min(3, candidates.Count))];
        }

        var randomCandidates = userHealthy
            .Where(p => !excludedIds.Contains(p.id))
            .ToList();
        if (randomCandidates.Count > 0)
            return randomCandidates[Random.Range(0, randomCandidates.Count)];

        return null;
    }

    List<PlayerData> BuildOfferPackage(List<PlayerData> aiAvailable, int aiRosterCount,
                                        PlayerData target, List<PlayerData> userRoster, TeamData aiTeam)
    {
        long userPayroll = userRoster.Sum(p => p.salary);
        var fullAiRoster = DatabaseManager.Instance.GetPlayersByTeam(aiTeam.id);
        long aiPayroll = fullAiRoster.Sum(p => p.salary);

        bool aiHardCapped = aiTeam.first_apron_hard_capped == 1;
        bool userHardCapped = _myTeam.first_apron_hard_capped == 1;

        // Try 1-for-1: closest salary match first
        foreach (var p in aiAvailable.OrderBy(p => Mathf.Abs(p.salary - target.salary)))
        {
            var pack = new List<PlayerData> { p };
            if (TradeHelper.ValidateTrade(pack, new List<PlayerData> { target },
                    aiRosterCount, userRoster.Count, _myTeam.name, userPayroll,
                    aiTeam.name, aiPayroll, aiHardCapped, userHardCapped).Count == 0)
                return pack;
        }

        // Try 2-for-1
        for (int i = 0; i < aiAvailable.Count; i++)
        {
            for (int j = i + 1; j < aiAvailable.Count; j++)
            {
                var pack = new List<PlayerData> { aiAvailable[i], aiAvailable[j] };
                if (TradeHelper.ValidateTrade(pack, new List<PlayerData> { target },
                        aiRosterCount, userRoster.Count, _myTeam.name, userPayroll,
                        aiTeam.name, aiPayroll, aiHardCapped, userHardCapped).Count == 0)
                    return pack;
            }
        }

        // Try 3-for-1
        if (aiRosterCount - 3 >= 10)
        {
            for (int i = 0; i < aiAvailable.Count; i++)
            {
                for (int j = i + 1; j < aiAvailable.Count; j++)
                {
                    for (int k = j + 1; k < aiAvailable.Count; k++)
                    {
                        var pack = new List<PlayerData> { aiAvailable[i], aiAvailable[j], aiAvailable[k] };
                        if (TradeHelper.ValidateTrade(pack, new List<PlayerData> { target },
                                aiRosterCount, userRoster.Count, _myTeam.name, userPayroll,
                                aiTeam.name, aiPayroll, aiHardCapped, userHardCapped).Count == 0)
                            return pack;
                    }
                }
            }
        }

        return null;
    }

    void ShowNextPendingTradeOffer()
    {
        var pending = DatabaseManager.Instance.GetPendingTradeOffers(_manager.id);
        if (pending.Count == 0)
        {
            _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            Refresh();
            return;
        }

        var offer = pending[0];
        var myRoster = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var aiRoster = DatabaseManager.Instance.GetPlayersByTeam(offer.team_id_from);

        var wantedIds = offer.GetWantedPlayerIds();
        var offeredIds = offer.GetOfferedPlayerIds();
        var wantedPickIds = offer.GetWantedPickIds();
        var offeredPickIds = offer.GetOfferedPickIds();

        var ourPlayers = wantedIds
            .Select(id => myRoster.FirstOrDefault(p => p.id == id))
            .Where(p => p != null)
            .ToList();

        var theirPlayers = offeredIds
            .Select(id => aiRoster.FirstOrDefault(p => p.id == id))
            .Where(p => p != null)
            .ToList();

        var ourPicks = wantedPickIds
            .Select(id => DatabaseManager.Instance.GetDraftPickById(id))
            .Where(p => p != null && p.current_team_id == _myTeam.id)
            .ToList();
        var theirPicks = offeredPickIds
            .Select(id => DatabaseManager.Instance.GetDraftPickById(id))
            .Where(p => p != null && p.current_team_id == offer.team_id_from)
            .ToList();

        if (ourPlayers.Count != wantedIds.Count || theirPlayers.Count != offeredIds.Count
            || ourPicks.Count != wantedPickIds.Count || theirPicks.Count != offeredPickIds.Count)
        {
            DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 2);
            ShowNextPendingTradeOffer();
            return;
        }

        ShowTradeOfferModal(offer, ourPlayers, theirPlayers, ourPicks, theirPicks);
    }

    void ShowTradeOfferModal(TradeOfferData offer, List<PlayerData> ourPlayers, List<PlayerData> theirPlayers,
                              List<DraftPickData> ourPicks = null, List<DraftPickData> theirPicks = null)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var aiTeam = _allTeams.FirstOrDefault(t => t.id == offer.team_id_from);
        string teamName = aiTeam != null ? aiTeam.name : "?";
        var teamAbbrs = DatabaseManager.Instance.GetAllTeams().ToDictionary(t => t.id, t => t.abbreviation);

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box");
        box.AddToClassList("trade-offer-modal-box");
        _firedOverlay.Add(box);

        var title = new Label("PROPUESTA DE INTERCAMBIO");
        title.AddToClassList("fired-modal-title");
        box.Add(title);

        var columns = new VisualElement();
        columns.AddToClassList("trade-offer-columns");
        box.Add(columns);

        var leftCol = BuildPlayerColumn("TÚ ENVÍAS", _myTeam.name, ourPlayers);
        columns.Add(leftCol);

        var rightCol = BuildPlayerColumn("TÚ RECIBES", teamName, theirPlayers);
        columns.Add(rightCol);

        if (ourPicks != null && ourPicks.Count > 0)
        {
            var pickLbl = new Label($"+ {string.Join(", ", ourPicks.Select(p => $"R{p.round} {(teamAbbrs.TryGetValue(p.original_team_id, out var a1) ? a1 : "???")}"))}");
            pickLbl.AddToClassList("trade-offer-picks-label");
            leftCol.Add(pickLbl);
        }
        if (theirPicks != null && theirPicks.Count > 0)
        {
            var pickLbl = new Label($"+ {string.Join(", ", theirPicks.Select(p => $"R{p.round} {(teamAbbrs.TryGetValue(p.original_team_id, out var a2) ? a2 : "???")}"))}");
            pickLbl.AddToClassList("trade-offer-picks-label");
            rightCol.Add(pickLbl);
        }

        var btnGroup = new VisualElement();
        btnGroup.AddToClassList("injured-modal-btn-group");

        var rejectBtn = new Button();
        rejectBtn.text = "RECHAZAR";
        rejectBtn.AddToClassList("injured-modal-btn");
        rejectBtn.style.backgroundColor = new StyleColor(new Color(0.753f, 0.224f, 0.169f));
        rejectBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 2);
            _firedOverlay.style.display = DisplayStyle.None;
            ShowNextPendingTradeOffer();
        });
        btnGroup.Add(rejectBtn);

        var acceptBtn = new Button();
        acceptBtn.text = "ACEPTAR";
        acceptBtn.AddToClassList("injured-modal-btn");
        acceptBtn.style.backgroundColor = new StyleColor(new Color(0.153f, 0.682f, 0.376f));
        acceptBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var p in ourPlayers)
            {
                p.team_id = offer.team_id_from;
                DatabaseManager.Instance.UpdatePlayer(p);
                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = _season?.id ?? 0,
                    game_day = _season?.current_game_day ?? 0,
                    game_date = _season?.current_date ?? now,
                    team_id_from = _myTeam.id,
                    team_id_to = offer.team_id_from,
                    player_id = p.id,
                    trade_type = "trade"
                });
            }

            foreach (var p in theirPlayers)
            {
                p.team_id = _myTeam.id;
                DatabaseManager.Instance.UpdatePlayer(p);
                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = _season?.id ?? 0,
                    game_day = _season?.current_game_day ?? 0,
                    game_date = _season?.current_date ?? now,
                    team_id_from = offer.team_id_from,
                    team_id_to = _myTeam.id,
                    player_id = p.id,
                    trade_type = "trade"
                });
            }

            // Transfer picks
            if (ourPicks != null && ourPicks.Count > 0)
            {
                var ids = ourPicks.Select(p => p.id).ToList();
                DatabaseManager.Instance.TransferDraftPicks(ids, _myTeam.id, offer.team_id_from);
                foreach (var pk in ourPicks)
                {
                    DatabaseManager.Instance.InsertTrade(new TradeData
                    {
                        season_id = _season?.id ?? 0,
                        game_day = _season?.current_game_day ?? 0,
                        game_date = _season?.current_date ?? now,
                        team_id_from = _myTeam.id,
                        team_id_to = offer.team_id_from,
                        player_id = 0,
                        pick_id = pk.id,
                        trade_type = "pick_trade"
                    });
                }
            }
            if (theirPicks != null && theirPicks.Count > 0)
            {
                var ids = theirPicks.Select(p => p.id).ToList();
                DatabaseManager.Instance.TransferDraftPicks(ids, offer.team_id_from, _myTeam.id);
                foreach (var pk in theirPicks)
                {
                    DatabaseManager.Instance.InsertTrade(new TradeData
                    {
                        season_id = _season?.id ?? 0,
                        game_day = _season?.current_game_day ?? 0,
                        game_date = _season?.current_date ?? now,
                        team_id_from = offer.team_id_from,
                        team_id_to = _myTeam.id,
                        player_id = 0,
                        pick_id = pk.id,
                        trade_type = "pick_trade"
                    });
                }
            }

            var ourNames = string.Join(", ", ourPlayers.Select(p => $"{p.first_name} {p.last_name}"));
            var theirNames = string.Join(", ", theirPlayers.Select(p => $"{p.first_name} {p.last_name}"));
            var ourPicksText = ourPicks != null && ourPicks.Count > 0
                ? " y " + string.Join(", ", ourPicks.Select(p => $"R{p.round} {(teamAbbrs.TryGetValue(p.original_team_id, out var a3) ? a3 : "???")}")) : "";
            var theirPicksText = theirPicks != null && theirPicks.Count > 0
                ? " y " + string.Join(", ", theirPicks.Select(p => $"R{p.round} {(teamAbbrs.TryGetValue(p.original_team_id, out var a4) ? a4 : "???")}")) : "";

            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 1,
                sender_id = 0,
                title = "Intercambio aceptado",
                body = $"Has intercambiado a {ourNames}{ourPicksText} por {theirNames}{theirPicksText} con {teamName}.",
                game_day = _season.current_game_day,
                game_date = now,
                created_at = now,
                date_sent = now,
                is_read = 0
            });

            DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 1);
            _firedOverlay.style.display = DisplayStyle.None;
            ShowNextPendingTradeOffer();
        });
        btnGroup.Add(acceptBtn);

        box.Add(btnGroup);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(rejectBtn);
            CursorManager.Instance.RegisterHandCursor(acceptBtn);
        }
    }

    VisualElement BuildPlayerColumn(string label, string teamName, List<PlayerData> players)
    {
        var col = new VisualElement();
        col.AddToClassList("trade-offer-col");

        var header = new Label(label);
        header.AddToClassList("trade-offer-col-header");
        col.Add(header);

        var teamLabel = new Label(teamName);
        teamLabel.AddToClassList("trade-offer-col-team");
        col.Add(teamLabel);

        foreach (var p in players)
        {
            var card = new VisualElement();
            card.AddToClassList("trade-offer-player-card");

            var photo = new VisualElement();
            photo.AddToClassList("trade-offer-photo");
            Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
            if (tex != null)
                photo.style.backgroundImage = new StyleBackground(tex);
            card.Add(photo);

            var info = new VisualElement();
            info.AddToClassList("trade-offer-player-info");

            var nameRow = new VisualElement();
            nameRow.AddToClassList("trade-offer-player-name-row");

            var nameLbl = new Label($"{p.first_name} {p.last_name}");
            nameLbl.AddToClassList("trade-offer-player-name");
            nameRow.Add(nameLbl);

            var ovrLbl = new Label(p.overall.ToString());
            ovrLbl.AddToClassList("trade-offer-player-ovr");
            if (p.overall >= 80)
                ovrLbl.style.color = new StyleColor(new Color32(39, 174, 96, 255));
            else if (p.overall >= 60)
                ovrLbl.style.color = new StyleColor(new Color32(212, 160, 23, 255));
            else
                ovrLbl.style.color = new StyleColor(new Color32(192, 57, 43, 255));
            nameRow.Add(ovrLbl);

            info.Add(nameRow);

            var posAge = new Label($"{PositionCodes.GetShort(p.position)}  ·  {p.age} años");
            posAge.AddToClassList("trade-offer-player-detail");
            info.Add(posAge);

            var salaryLbl = new Label(SalaryStr(p.salary));
            salaryLbl.AddToClassList("trade-offer-player-salary");
            info.Add(salaryLbl);

            card.Add(info);
            col.Add(card);
        }

        return col;
    }

    string SalaryStr(long salary)
    {
        return "$" + salary.ToString("N0").Replace(',', '.');
    }

    void ApplyRenovation(TeamData team)
    {
        var info = GetRenovationInfo(team.arena_renovation_type);
        if (info.name == "") return;

        if (team.capacity + info.capacityBonus > 50000) return;

        team.capacity += info.capacityBonus;
        team.arena_renovation_count++;

        // Increase facilities every 3 renovations
        if (team.arena_renovation_count >= 3 && team.facilities < 5)
        {
            team.facilities++;
            team.arena_renovation_count = 0;
        }

        // Deduct cost
        if (team.arena_renovation_cost > 0)
        {
            team.budget -= team.arena_renovation_cost;
            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = team.id,
                season_id = _season.id,
                record_type = FinanceRecord.TYPE_RENOVATION,
                game_day = _season.current_game_day,
                amount = team.arena_renovation_cost
            });
        }

        DatabaseManager.Instance.UpdateTeamBudget(team.id, team.budget);

        // Update team with renovation reset
        var dbTeam = DatabaseManager.Instance.GetTeamById(team.id);
        dbTeam.capacity = team.capacity;
        dbTeam.facilities = team.facilities;
        dbTeam.arena_renovation_count = team.arena_renovation_count;
        dbTeam.arena_renovation_end_day = 0;
        dbTeam.arena_renovation_type = "";
        dbTeam.arena_renovation_cost = 0;
        DatabaseManager.Instance.UpdateTeam(dbTeam);

        // Send message
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            title = $"Remodelación completada: {info.name}",
            body = $"La remodelación \"{info.name}\" ha finalizado. Se han añadido {info.capacityBonus} asientos. Coste total: {team.arena_renovation_cost:N0} $.",
            game_day = _season.current_game_day,
            game_date = System.DateTime.Parse(_season.year_start + "-10-22").AddDays(_season.current_game_day - 1).ToString("yyyy-MM-dd"),
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        });
    }

    (string name, int capacityBonus, long cost) GetRenovationInfo(string type)
    {
        return type switch
        {
            "general_seats" => ("Ampliar Grada General", 3000, 10_000_000),
            "tribune" => ("Ampliar Tribuna", 2000, 20_000_000),
            "vip_seats" => ("Ampliar Grada VIP", 1000, 35_000_000),
            _ => ("", 0, 0)
        };
    }

    int CalculateAttendance(GameData game, TeamData homeTeam)
    {
        bool myTeamIsHome = game.home_team_id == _myTeam.id;
        bool myTeamIsAway = game.away_team_id == _myTeam.id;

        var teamGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        var homeTeamGames = teamGames.Where(g => g.home_team_id == homeTeam.id || g.away_team_id == homeTeam.id).ToList();
        int wins = homeTeamGames.Count(g =>
            (g.home_team_id == homeTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == homeTeam.id && g.away_score > g.home_score));
        int totalPlayed = homeTeamGames.Count;
        float winPct = totalPlayed > 0 ? (float)wins / totalPlayed : 0.5f;

        float baseAttendance;
        float randomFactor = 0.92f + UnityEngine.Random.value * 0.16f;

        if (myTeamIsHome)
        {
            // My home game: fan confidence + rival reputation
            var rival = DatabaseManager.Instance.GetTeamById(game.away_team_id);
            float rivalRepFactor = rival != null ? (rival.reputation / 5f) * 0.08f : 0f;
            baseAttendance = homeTeam.capacity * (
                0.30f +
                (_manager.fan_confidence / 100f) * 0.35f +
                winPct * 0.15f +
                rivalRepFactor
            );
        }
        else if (myTeamIsAway)
        {
            // My away game: home team base + bonus from my reputation as visiting draw
            float myRepFactor = (_myTeam.reputation / 5f) * 0.06f;
            baseAttendance = homeTeam.capacity * (
                0.55f +
                winPct * 0.30f +
                myRepFactor
            );
        }
        else
        {
            // Other teams' games: standard formula
            baseAttendance = homeTeam.capacity * (0.55f + winPct * 0.40f);
        }

        // Ticket price elasticity: more expensive → fewer attendees
        // Smooth exponential decay from 1.0 at $30 down to ~0.20 at $500
        var finSettings = DatabaseManager.Instance.GetTeamSettings(homeTeam.id);
        int ticketPrice = finSettings != null ? (int)finSettings.ticket_price : 50;
        float priceFactor = Mathf.Clamp(Mathf.Exp(-(ticketPrice - 30f) / 150f), 0.20f, 1.0f);

        // Objective compliance: not on track for the team's season target
        float objectiveFactor = GetObjectiveComplianceFactor(homeTeam, teamGames);

        return (int)Mathf.Min(homeTeam.capacity, baseAttendance * randomFactor * priceFactor * objectiveFactor);
    }

    float GetObjectiveComplianceFactor(TeamData homeTeam, List<GameData> teamGames)
    {
        if (string.IsNullOrEmpty(homeTeam.objective)) return 1.0f;
        if (homeTeam.objective == "Zona tranquila") return 1.0f;

        int targetPos = 0;
        if (homeTeam.objective == "Campeonato") targetPos = 1;
        else if (homeTeam.objective == "Playoffs") targetPos = 6;
        else if (homeTeam.objective == "Play-In") targetPos = 10;
        else return 1.0f;

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var conferenceTeams = allTeams.Where(t => t.conference == homeTeam.conference).ToList();

        var ranked = conferenceTeams.Select(t =>
        {
            var tGames = teamGames.Where(g => g.home_team_id == t.id || g.away_team_id == t.id).ToList();
            int w = tGames.Count(g =>
                (g.home_team_id == t.id && g.home_score > g.away_score) ||
                (g.away_team_id == t.id && g.away_score > g.home_score));
            int l = tGames.Count - w;
            return new { Team = t, Wins = w, Losses = l, Pct = tGames.Count > 0 ? (float)w / tGames.Count : 0f };
        })
        .OrderByDescending(x => x.Pct)
        .ThenBy(x => x.Losses)
        .ThenByDescending(x => x.Wins)
        .ToList();

        int currentPos = ranked.FindIndex(x => x.Team.id == homeTeam.id) + 1;
        if (currentPos <= 0) return 1.0f;

        int gap = currentPos - targetPos;
        if (gap <= 0) return 1.0f;

        // 1 pos off → 0.90, 5 pos off → 0.50, 10+ pos off → 0.30
        return Mathf.Clamp(1.0f - gap * 0.06f, 0.30f, 1.0f);
    }

    void UpdatePlayersMoraleAfterGame(List<GameSimulator.PlayerStatSnapshot> stats, int teamId, bool teamWon)
    {
        // Get last 10 games for this team to calculate win%
        var teamGames = DatabaseManager.Instance.GetStandingsGames(_manager.id)
            .Where(g => (g.home_team_id == teamId || g.away_team_id == teamId) && g.is_played == 1)
            .OrderByDescending(g => g.game_day).Take(10).ToList();
        int winsInLast10 = teamGames.Count(g =>
            (g.home_team_id == teamId && g.home_score > g.away_score) ||
            (g.away_team_id == teamId && g.away_score > g.home_score));
        float winPct = teamGames.Count > 0 ? (float)winsInLast10 / teamGames.Count : 0.5f;

        bool isMyTeam = teamId == _myTeam.id;
        int gameDay = _season?.current_game_day ?? 0;
        string gameDate = _season?.current_date ?? "";

        int GetRoleDelta(PlayerRole role, float ratio)
        {
            return role switch
            {
                PlayerRole.Estrella => ratio >= 1.0f ? 2 : ratio >= 0.7f ? 1 : ratio >= 0.4f ? -1 : ratio > 0f ? -2 : -3,
                PlayerRole.Titular => ratio >= 1.0f ? 2 : ratio >= 0.7f ? 1 : ratio >= 0.4f ? 0 : ratio > 0f ? -1 : -2,
                PlayerRole.Banquillo => ratio >= 1.0f ? 2 : ratio >= 0.7f ? 1 : ratio >= 0.4f ? 0 : ratio > 0f ? 0 : -1,
                _ => ratio >= 1.0f ? 2 : ratio >= 0.7f ? 1 : 0
            };
        }

        float ExpectedMinForRole(PlayerRole role) => role switch
        {
            PlayerRole.Estrella => 40f,
            PlayerRole.Titular => 28f,
            PlayerRole.Banquillo => 10f,
            _ => 3f
        };

        void ProcessPlayer(PlayerData player, float minutes, float? avgRating)
        {
            float expectedMin = ExpectedMinForRole(player.role);
            float ratio = expectedMin > 0f ? minutes / expectedMin : 1f;
            int roleDelta = GetRoleDelta(player.role, ratio);
            if (teamWon) roleDelta = Mathf.Min(roleDelta + 1, 2);

            int formDelta = avgRating.HasValue
                ? (avgRating.Value >= 28f ? 2
                    : avgRating.Value >= 20f ? 1
                    : avgRating.Value >= 15f ? 0
                    : avgRating.Value >= 10f ? -1
                    : -2)
                : 0;

            int streakDelta = winPct >= 0.7f ? 1 : winPct <= 0.3f ? -1 : 0;
            int contractDelta = player.contract_years == 1 ? -1 : 0;
            int injuryDelta = player.injury_days > 0 ? -2 : 0;

            int totalDelta = Mathf.Clamp(roleDelta + formDelta + streakDelta + contractDelta + injuryDelta, -3, 3);
            int newMorale = Mathf.Clamp(player.morale + totalDelta, 0, 100);
            DatabaseManager.Instance.UpdatePlayerMorale(player.id, newMorale);

            // Generate complaint messages for my team players with very low morale
            if (isMyTeam && player.role != PlayerRole.UltimoRecurso && newMorale < 20)
            {
                int minGameDay = gameDay - 10;
                var recentMsg = DatabaseManager.Instance.Db.Table<MessageData>()
                    .FirstOrDefault(m => m.sender_id == player.id && m.game_day > minGameDay);
                if (recentMsg == null)
                {
                    string title, body;
                    if (newMorale < 10)
                    {
                        title = $"Queja: {player.first_name} {player.last_name}";
                        body = $"{player.first_name} {player.last_name} está furioso por la falta de minutos. Exige un traspaso inmediato.";
                    }
                    else
                    {
                        title = $"Preocupación: {player.first_name} {player.last_name}";
                        body = $"{player.first_name} {player.last_name} no está conforme con los minutos que está jugando. Espera tener más protagonismo teniendo en cuenta el rol que se le prometió.";
                    }
                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = player.id,
                        title = title,
                        body = body,
                        game_day = gameDay,
                        game_date = gameDate,
                        created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        is_read = 0
                    });
                }
            }
        }

        // ── Active players (12 que jugaron) ──
        var activeIds = new HashSet<int>();
        foreach (var ps in stats)
        {
            var player = DatabaseManager.Instance.GetPlayerById(ps.player_id);
            if (player == null) continue;
            activeIds.Add(player.id);

            var last5Stats = DatabaseManager.Instance.GetPlayerGameStats(player.id)
                .OrderByDescending(s => s.game_id).Take(5).ToList();
            float avgRating = last5Stats.Count > 0 ? (float)last5Stats.Average(s => s.rating) : 15f;

            ProcessPlayer(player, ps.minutes, avgRating);
        }

        // ── Inactive healthy players (no convocados pero sanos) ──
        var allTeamPlayers = DatabaseManager.Instance.GetPlayersByTeam(teamId);
        foreach (var player in allTeamPlayers)
        {
            if (player.injury_days > 0) continue;
            if (activeIds.Contains(player.id)) continue;
            ProcessPlayer(player, 0f, null);
        }
    }

    void UpdateFanConfidence(GameData game, GameSimulator.GameResult result)
    {
        bool isHome = game.home_team_id == _myTeam.id;
        int myScore = isHome ? result.home_score : result.away_score;
        int rivalScore = isHome ? result.away_score : result.home_score;
        bool won = myScore > rivalScore;
        int margin = Mathf.Abs(myScore - rivalScore);

        int change = 0;
        if (won)
        {
            change = isHome ? 4 : 2;
            if (margin <= 5) change += 1;  // close exciting win
            if (margin >= 20) change += 1; // dominant win
        }
        else
        {
            change = isHome ? -3 : -2;
            if (margin <= 5) change -= 1;  // close painful loss
            if (margin >= 20) change -= 1; // embarrassing blowout
        }

        _manager.fan_confidence = Mathf.Clamp(_manager.fan_confidence + change, 0, 100);
        DatabaseManager.Instance.SaveManager(_manager);
    }

    void ProcessGameFinances(GameData game, GameSimulator.GameResult result)
    {
        if (_season == null) return;

        // Only process for home team
        var homeTeam = DatabaseManager.Instance.GetTeamById(game.home_team_id);
        if (homeTeam == null) return;

        var finSettings = DatabaseManager.Instance.GetTeamSettings(homeTeam.id);
        const int defaultTicketPrice = 50;
        int ticketPrice = finSettings != null ? (int)finSettings.ticket_price : defaultTicketPrice;

        int attendance = CalculateAttendance(game, homeTeam);
        float ticketMultiplier = homeTeam.id == _myTeam.id ? GetArenaTicketMultiplier() : 1.0f;
        long ticketRevenue = (long)(attendance * ticketPrice * ticketMultiplier);

        // Always save attendance, even if financial settings are missing
        DatabaseManager.Instance.SaveGameAttendance(new GameAttendanceData
        {
            game_id = game.id,
            attendance = attendance,
            ticket_price = ticketPrice,
            revenue = ticketRevenue
        });

        if (ticketRevenue > 0)
        {
            homeTeam.budget += ticketRevenue;
            DatabaseManager.Instance.UpdateTeamBudget(homeTeam.id, homeTeam.budget);

            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = homeTeam.id,
                season_id = _season.id,
                record_type = FinanceRecord.TYPE_TICKET,
                game_day = game.game_day,
                amount = ticketRevenue
            });
        }

        // Skip sponsor/TV processing if settings aren't configured
        if (finSettings == null) return;

        // Sponsor home game income
        if (finSettings.sponsor_id > 0 && finSettings.sponsor_years_remaining > 0)
        {
            var sponsor = DatabaseManager.Instance.GetSponsorById(finSettings.sponsor_id);
            if (sponsor != null)
            {
                homeTeam.budget += sponsor.home_game_income;
                DatabaseManager.Instance.UpdateTeamBudget(homeTeam.id, homeTeam.budget);
                DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
                {
                    team_id = homeTeam.id,
                    season_id = _season.id,
                    record_type = FinanceRecord.TYPE_SPONSORSHIP,
                    game_day = game.game_day,
                    amount = sponsor.home_game_income
                });
            }
        }

        // TV home game income
        if (finSettings.tv_channel_id > 0 && finSettings.tv_years_remaining > 0)
        {
            var tv = DatabaseManager.Instance.GetTVChannelById(finSettings.tv_channel_id);
            if (tv != null)
            {
                homeTeam.budget += tv.home_game_income;
                DatabaseManager.Instance.UpdateTeamBudget(homeTeam.id, homeTeam.budget);
                DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
                {
                    team_id = homeTeam.id,
                    season_id = _season.id,
                    record_type = FinanceRecord.TYPE_TV,
                    game_day = game.game_day,
                    amount = tv.home_game_income
                });
            }
        }
    }

    void CreateGameResultMessage(GameData game, GameSimulator.GameResult result)
    {
        bool isHome = game.home_team_id == _myTeam.id;
        int myScore = isHome ? result.home_score : result.away_score;
        int rivalScore = isHome ? result.away_score : result.home_score;
        bool won = myScore > rivalScore;

        // Rival name
        var rival = DatabaseManager.Instance.GetTeamById(isHome ? game.away_team_id : game.home_team_id);
        string rivalName = rival?.name ?? "Rival";

        // Home team and attendance (always shown)
        var homeTeam = DatabaseManager.Instance.GetTeamById(game.home_team_id);
        string arenaName = homeTeam?.arena ?? "Pabellón";

        // Read saved attendance from database (calculated by ProcessGameFinances)
        var attendanceData = DatabaseManager.Instance.GetGameAttendance(game.id);
        int attendance = attendanceData?.attendance ?? 0;

        // Fallback: calculate if missing (e.g., older saves or preseason games)
        if (attendance == 0 && homeTeam != null)
        {
            attendance = CalculateAttendance(game, homeTeam);
        }

        // MVP of my team (most points)
        var myStats = isHome ? result.home_stats : result.away_stats;
        var mvp = myStats.OrderByDescending(s => s.points).FirstOrDefault();
        string mvpName = mvp != null ? $"{mvp.name}" : "";

        // Text variants
        string body;
        int variant = UnityEngine.Random.Range(0, 5);
        if (won)
        {
            switch (variant)
            {
                case 0:
                    body = $"Gran victoria contra {rivalName} por {myScore}-{rivalScore}.";
                    break;
                case 1:
                    body = $"El equipo se impuso a {rivalName} con un resultado de {myScore}-{rivalScore}.";
                    break;
                case 2:
                    body = $"Buen triunfo ante {rivalName}: {myScore}-{rivalScore}.";
                    break;
                case 3:
                    body = $"Victoria importante frente a {rivalName} ({myScore}-{rivalScore}).";
                    break;
                default:
                    body = $"El {rivalName} sucumbe ante nosotros: {myScore}-{rivalScore}.";
                    break;
            }
        }
        else
        {
            switch (variant)
            {
                case 0:
                    body = $"Derrota ante {rivalName} por {rivalScore}-{myScore}.";
                    break;
                case 1:
                    body = $"El equipo cae contra {rivalName} ({rivalScore}-{myScore}).";
                    break;
                case 2:
                    body = $"Dura derrota frente a {rivalName}: {rivalScore}-{myScore}.";
                    break;
                case 3:
                    body = $"No pudimos con {rivalName}. Resultado: {rivalScore}-{myScore}.";
                    break;
                default:
                    body = $"{rivalName} se lleva la victoria: {rivalScore}-{myScore}.";
                    break;
            }
        }

        body += $"\nPabellón: {arenaName} — Asistencia: {attendance:N0} espectadores.";
        if (!string.IsNullOrEmpty(mvpName))
            body += $"\nMVP del partido: {mvpName}.";

        var msg = new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"Resultado: {rivalName} ({myScore}-{rivalScore})",
            body = body,
            game_day = game.game_day,
            game_date = game.game_date,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        };
        DatabaseManager.Instance.AddMessage(msg);
    }

    void ProcessGameInjuries(GameSimulator.GameResult result, string gameDate)
    {
        if (result.injuries == null) return;
        foreach (var inj in result.injuries)
        {
            if (inj.player_id == _myTeam.id || true) // Notify for all injuries affecting my team
            {
                var player = DatabaseManager.Instance.GetPlayerById(inj.player_id);
                if (player != null && player.team_id == _myTeam.id)
                {
                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        title = $"Lesión: {player.first_name} {player.last_name}",
                        body = $"{player.first_name} {player.last_name} ha sufrido {inj.type}. Estará de baja {inj.days} días.",
                        game_day = _season.current_game_day,
                        game_date = gameDate,
                        created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        is_read = 0
                    });
                }
            }
        }
    }

    void UpdateManagerStats(int gameDay)
    {
        var teamGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        var myGames = teamGames.Where(g => g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id).ToList();

        int wins = myGames.Count(g =>
            (g.home_team_id == _myTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == _myTeam.id && g.away_score > g.home_score));
        int losses = myGames.Count - wins;

        // Trust: based on win percentage
        float winPct = myGames.Count > 0 ? (float)wins / myGames.Count : 0.5f;
        int trustChange = winPct > 0.5f ? 2 : winPct > 0.4f ? 1 : winPct < 0.2f ? -2 : -1;
        _manager.trust = Mathf.Clamp(_manager.trust + trustChange, 0, 100);

        // Morale: based on recent form (last 5 games)
        var last5 = myGames.OrderByDescending(g => g.game_day).Take(5).ToList();
        int recentWins = last5.Count(g =>
            (g.home_team_id == _myTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == _myTeam.id && g.away_score > g.home_score));
        int moraleChange = recentWins >= 4 ? 3 : recentWins >= 3 ? 1 : recentWins <= 1 ? -2 : 0;
        _manager.morale = Mathf.Clamp(_manager.morale + moraleChange, 0, 100);

        DatabaseManager.Instance.SaveManager(_manager);
    }

    void EvaluateMonthlyAwards(System.DateTime evaluationDate)
    {
        string prevMonthStart, prevMonthEnd;
        int prevMonth = evaluationDate.Month - 1;
        int prevYear = evaluationDate.Year;
        if (prevMonth == 0) { prevMonth = 12; prevYear--; }

        // Awards are for the PREVIOUS month's games, so use the previous month's name
        string monthName = new System.DateTime(prevYear, prevMonth, 1).ToString("MMMM", new System.Globalization.CultureInfo("es-ES"));

        Debug.Log($"[Dashboard] EvaluateMonthlyAwards: evalDate={evaluationDate:yyyy-MM-dd}, prevMonth={prevMonth}, prevYear={prevYear}, monthName={monthName}");

        if (prevMonth == 4 && evaluationDate.Month == 5)
        {
            // End-of-season April evaluation: Apr 1 to end of regular season
            prevMonthStart = $"{prevYear}-04-01";
            prevMonthEnd = _season.current_date;
        }
        else
        {
            prevMonthStart = $"{prevYear}-{prevMonth:D2}-01";
            prevMonthEnd = $"{prevYear}-{prevMonth:D2}-{System.DateTime.DaysInMonth(prevYear, prevMonth)}";
        }

        var winners = DatabaseManager.Instance.EvaluateMonthlyAwards(
            _season.id, monthName, prevMonthStart, prevMonthEnd, _manager.id, _myTeam.id);

        if (winners.Count == 0) return;

        // Build news message
        var managers = winners.Where(w => w.award_type == "manager").OrderBy(w => w.rank).ToList();
        var players = winners.Where(w => w.award_type == "player").OrderBy(w => w.rank).ToList();
        var rookies = winners.Where(w => w.award_type == "rookie").OrderBy(w => w.rank).ToList();

        var body = new System.Text.StringBuilder();
        body.AppendLine($"<b>MANAGER DEL MES - {monthName}</b>");
        foreach (var m in managers)
        {
            string mgrName = m.team_id.HasValue
                ? DatabaseManager.Instance.GetManagerNameByTeamId(m.team_id.Value)
                : null;
            if (string.IsNullOrEmpty(mgrName))
                mgrName = m.team_name;
            body.AppendLine($"{m.rank}. {mgrName} ({(m.value * 100).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%)");
        }
        body.AppendLine();
        body.AppendLine($"<b>JUGADOR DEL MES - {monthName}</b>");
        foreach (var p in players)
            body.AppendLine($"{p.rank}. {p.player_name} ({p.team_name}) - {p.value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} VAL");
        body.AppendLine();
        body.AppendLine($"<b>ROOKIE DEL MES - {monthName}</b>");
        foreach (var r in rookies)
            body.AppendLine($"{r.rank}. {r.player_name} ({r.team_name}) - {r.value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} VAL");

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 2,
            sender_id = 0,
            title = $"Premios del Mes de {monthName}",
            body = body.ToString(),
            game_day = _season.current_game_day,
            game_date = _season.current_date,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        });
    }

    void ProcessMonthlyPayroll(int gameDay)
    {
        if (_myTeam == null || _season == null || _manager == null) return;

        // Payroll on game days ~1, 31, 61, 91, 121, 151, 181 (1st of each month)
        int[] payrollDays = { 1, 31, 61, 91, 121, 151, 181 };
        if (!payrollDays.Contains(gameDay)) return;

        foreach (var team in DatabaseManager.Instance.GetAllTeams())
        {
            ProcessTeamPlayerPayroll(team, gameDay);
            ProcessTeamLuxuryTax(team, gameDay);
        }

        ProcessEmployeePayroll(gameDay);
    }

    void ProcessTeamPlayerPayroll(TeamData team, int gameDay)
    {
        var players = DatabaseManager.Instance.GetPlayersByTeam(team.id);

        // Player monthly salaries (only if not already paid this cycle)
        var existingPayroll = DatabaseManager.Instance.GetFinanceRecord(team.id, _season.id, FinanceRecord.TYPE_SALARIES, gameDay);
        if (existingPayroll != null) return;

        long monthlyPayroll = players.Sum(p => p.salary) / 12;

        team.budget -= monthlyPayroll;
        DatabaseManager.Instance.UpdateTeamBudget(team.id, team.budget);

        DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
        {
            team_id = team.id,
            season_id = _season.id,
            record_type = FinanceRecord.TYPE_SALARIES,
            game_day = gameDay,
            amount = monthlyPayroll
        });

        if (team.id != _myTeam.id) return;

        // Payroll message (solo para el equipo del usuario)
        try
        {
            string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var gameDate = System.DateTime.Parse(_season.year_start + "-10-22").AddDays(gameDay - 1);
            string monthName = gameDate.ToString("MMMM", new System.Globalization.CultureInfo("es-ES"));
            monthName = char.ToUpper(monthName[0]) + monthName.Substring(1);

            var msg = new MessageData
            {
                manager_id = _manager.id,
                sender_type = 1,
                sender_id = 0,
                title = "Pago de n\u00f3minas",
                body = $"Se han pagado las n\u00f3minas del mes de {monthName} por un total de ${monthlyPayroll:N0}.",
                game_day = gameDay,
                game_date = gameDate.ToString("yyyy-MM-dd"),
                created_at = now,
                date_sent = now,
                is_read = 0
            };
            DatabaseManager.Instance.AddMessage(msg);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Payroll] Error creating message: {ex.Message}\n{ex.StackTrace}");
        }
    }

    void ProcessEmployeePayroll(int gameDay)
    {
        // Employee monthly salaries (solo el equipo del usuario; no hay empleados IA)
        var existingEmployeePayroll = DatabaseManager.Instance.GetFinanceRecord(
            _myTeam.id, _season.id, FinanceRecord.TYPE_EMPLOYEE_SALARY, gameDay);
        if (existingEmployeePayroll != null) return;

        var employees = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        long monthlyEmployeePayroll = employees.Sum(e => e.salary) / 12;

        if (monthlyEmployeePayroll > 0)
        {
            _myTeam.budget -= monthlyEmployeePayroll;
            DatabaseManager.Instance.UpdateTeamBudget(_myTeam.id, _myTeam.budget);

            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = _myTeam.id,
                season_id = _season.id,
                record_type = FinanceRecord.TYPE_EMPLOYEE_SALARY,
                game_day = gameDay,
                amount = monthlyEmployeePayroll
            });
        }
    }

    void ProcessTeamLuxuryTax(TeamData team, int gameDay)
    {
        var existingTax = DatabaseManager.Instance.GetFinanceRecord(team.id, _season.id, FinanceRecord.TYPE_TAX, gameDay);
        if (existingTax != null) return;

        var leagueSett = DatabaseManager.Instance.GetLeagueSettings();
        long taxThreshold = leagueSett?.luxury_tax ?? TradeHelper.LUXURY_TAX;
        var players = DatabaseManager.Instance.GetPlayersByTeam(team.id);
        long annualTax = TradeHelper.CalculateLuxuryTax(players.Sum(p => p.salary), taxThreshold);
        if (annualTax > 0)
        {
            long monthlyTax = annualTax / 12;
            team.budget -= monthlyTax;
            DatabaseManager.Instance.UpdateTeamBudget(team.id, team.budget);

            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = team.id,
                season_id = _season.id,
                record_type = FinanceRecord.TYPE_TAX,
                game_day = gameDay,
                amount = -monthlyTax,
                created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
    }

    void ProcessSubscriptionRevenue(int gameDay)
    {
        // Process around game day 11 (Nov 1)
        if (gameDay < 10 || gameDay > 12) return;

        var existing = DatabaseManager.Instance.GetFinanceRecord(_myTeam.id, _season.id, FinanceRecord.TYPE_SUBSCRIPTION, 0);
        if (existing != null) return;

        var finSettings = DatabaseManager.Instance.GetTeamSettings(_myTeam.id);
        if (finSettings == null) return;

        // Early season performance
        var earlyGames = DatabaseManager.Instance.GetStandingsGames(_manager.id)
            .Where(g => (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id) && g.game_day <= 4)
            .ToList();
        int wins = earlyGames.Count(g =>
            (g.home_team_id == _myTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == _myTeam.id && g.away_score > g.home_score));

        float performanceMult = 1.0f + (wins * 0.05f);
        // Reputation: 1 star -> 0.70, 5 stars -> 1.30
        float reputationMult = 0.7f + (_myTeam.reputation / 5f) * 0.6f;
        float randomFactor = 0.85f + UnityEngine.Random.value * 0.30f;

        float baseRatio = 0.5f;
        float priceFactor = (2000 - finSettings.subscription_price) / 10000f;
        int numSubscribers = (int)(_myTeam.capacity * (baseRatio + priceFactor) * performanceMult * reputationMult * randomFactor);
        numSubscribers = (int)Mathf.Clamp(numSubscribers, 0, _myTeam.capacity);

        long subAmount = numSubscribers * finSettings.subscription_price;

        if (subAmount > 0)
        {
            _myTeam.budget += subAmount;
            DatabaseManager.Instance.UpdateTeamBudget(_myTeam.id, _myTeam.budget);

            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = _myTeam.id,
                season_id = _season.id,
                record_type = FinanceRecord.TYPE_SUBSCRIPTION,
                game_day = gameDay,
                amount = subAmount
            });

            string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 1,
                sender_id = 0,
                title = "Campaña de abonos cerrada",
                body = $"Se ha cerrado la campaña de abonados de la temporada. Se han vendido {numSubscribers:N0} abonos a un precio de ${finSettings.subscription_price:N0} cada uno, obteniendo un ingreso total de ${subAmount:N0}.",
                game_day = gameDay,
                game_date = System.DateTime.Parse(_season.year_start + "-10-22").AddDays(gameDay - 1).ToString("yyyy-MM-dd"),
                created_at = now,
                date_sent = now,
                is_read = 0
            });
        }
    }

    // ── ÚLTIMO PARTIDO ───────────────────────────────────

    void RefreshLastGame()
    {
        var last = DatabaseManager.Instance.GetLastPlayedGame(_manager.id, _myTeam.id);

        if (last == null)
        {
            _noLastGame.style.display = DisplayStyle.Flex;
            _lastGameResult.style.display = DisplayStyle.None;
            _lastGameDate.text = "";
            return;
        }

        _noLastGame.style.display = DisplayStyle.None;
        _lastGameResult.style.display = DisplayStyle.Flex;

        var home = _allTeams.Find(t => t.id == last.home_team_id);
        var away = _allTeams.Find(t => t.id == last.away_team_id);

        SetTeamLogo(_lastHomeLog, home?.logo, "64x64");
        SetTeamLogo(_lastAwayLog, away?.logo, "64x64");
        _lastHomeName.text = home?.name.ToUpper() ?? "";
        _lastAwayName.text = away?.name.ToUpper() ?? "";
        _lastHomeScore.text = last.home_score.ToString();
        _lastAwayScore.text = last.away_score.ToString();

        // Force layout recalculation after scores are set to prevent overflow on first load
        _lastGameResult?.MarkDirtyRepaint();

        try
        {
            _lastGameDate.text = System.DateTime.Parse(last.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            _lastGameDate.text = last.game_date;
        }

        // Badge victoria/derrota
        bool myTeamIsHome = last.home_team_id == _myTeam.id;
        int myScore = myTeamIsHome ? last.home_score : last.away_score;
        int oppScore = myTeamIsHome ? last.away_score : last.home_score;
        bool won = myScore > oppScore;

        _lastResultBadge.text = won ? "VICTORIA" : "DERROTA";
        _lastResultBadge.RemoveFromClassList("badge-win");
        _lastResultBadge.RemoveFromClassList("badge-loss");
        _lastResultBadge.AddToClassList(won ? "badge-win" : "badge-loss");

        // Meta
        bool lastIsHome = last.home_team_id == _myTeam.id;
        var lastHomeTeam = _allTeams.Find(t => t.id == last.home_team_id);

        _lastGameLocation.text = lastIsHome ? "🏠  LOCAL" : "✈  VISITANTE";
        _lastGameLocation.RemoveFromClassList("game-meta-location--home");
        _lastGameLocation.RemoveFromClassList("game-meta-location--away");
        _lastGameLocation.AddToClassList(lastIsHome ? "game-meta-location--home" : "game-meta-location--away");

        _lastGameArena.text = lastHomeTeam != null
            ? DatabaseManager.Instance.GetTeamById(lastHomeTeam.id)?.arena ?? ""
            : "";

        _lastGameType.text = last.game_type switch
        {
            "preseason" => "AMISTOSO",
            "regular" => "TEMPORADA REGULAR",
            "playin" => "PLAY-IN",
            "playoff" => "PLAYOFFS",
            _ => last.game_type.ToUpper()
        };
    }

    // ── PRÓXIMO PARTIDO ──────────────────────────────────

    void RefreshNextGame()
    {
        var next = DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id);

        if (next == null)
        {
            _noNextGame.style.display = DisplayStyle.Flex;
            _nextGameResult.style.display = DisplayStyle.None;
            _nextGameDate.text = "";
            return;
        }

        _noNextGame.style.display = DisplayStyle.None;
        _nextGameResult.style.display = DisplayStyle.Flex;

        var home = _allTeams.Find(t => t.id == next.home_team_id);
        var away = _allTeams.Find(t => t.id == next.away_team_id);

        SetTeamLogo(_nextHomeLog, home?.logo, "64x64");
        SetTeamLogo(_nextAwayLog, away?.logo, "64x64");
        _nextHomeName.text = home?.name.ToUpper() ?? "";
        _nextAwayName.text = away?.name.ToUpper() ?? "";
        try
        {
            _nextGameDate.text = System.DateTime.Parse(next.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            _nextGameDate.text = next.game_date;
        }

        // Meta
        bool nextIsHome = next.home_team_id == _myTeam.id;
        var nextHomeTeam = _allTeams.Find(t => t.id == next.home_team_id);

        _nextGameLocation.text = nextIsHome ? "🏠  LOCAL" : "✈  VISITANTE";
        _nextGameLocation.RemoveFromClassList("game-meta-location--home");
        _nextGameLocation.RemoveFromClassList("game-meta-location--away");
        _nextGameLocation.AddToClassList(nextIsHome ? "game-meta-location--home" : "game-meta-location--away");

        _nextGameArena.text = nextHomeTeam != null
            ? DatabaseManager.Instance.GetTeamById(nextHomeTeam.id)?.arena ?? ""
            : "";

        _nextGameType.text = next.game_type switch
        {
            "preseason" => "AMISTOSO",
            "regular" => "TEMPORADA REGULAR",
            "playin" => "PLAY-IN",
            "playoff" => "PLAYOFFS",
            _ => next.game_type.ToUpper()
        };
    }

    // ── CLASIFICACIÓN ────────────────────────────────────

    void ShowStandings(string conf)
    {
        _currentConf = conf;

        _tabEast.RemoveFromClassList("conf-tab--active");
        _tabWest.RemoveFromClassList("conf-tab--active");
        if (conf == "East") _tabEast.AddToClassList("conf-tab--active");
        else _tabWest.AddToClassList("conf-tab--active");

        var confTeams = _allTeams.Where(t => t.conference == conf).ToList();
        var standings = BuildStandings(confTeams);

        _standingsBody.Clear();

        foreach (var row in standings)
        {
            var team = _allTeams.Find(t => t.id == row.teamId);
            var rowElem = CreateStandingsRow(row, team);
            _standingsBody.Add(rowElem);
        }
    }

    List<StandingRow> BuildStandings(List<TeamData> confTeams)
    {
        var data = confTeams.ToDictionary(t => t.id, t => new StandingRow
        {
            teamId = t.id,
            wins = 0,
            losses = 0,
            pf = 0,
            pa = 0,
            games = new List<bool>()
        });

        var confIds = confTeams.Select(t => t.id).ToHashSet();

        foreach (var g in _allGames)
        {
            if (confIds.Contains(g.home_team_id))
            {
                bool homeWon = g.home_score > g.away_score;
                data[g.home_team_id].wins += homeWon ? 1 : 0;
                data[g.home_team_id].losses += homeWon ? 0 : 1;
                data[g.home_team_id].pf += g.home_score;
                data[g.home_team_id].pa += g.away_score;
                data[g.home_team_id].games.Add(homeWon);
            }
            if (confIds.Contains(g.away_team_id))
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

        int gp = row.wins + row.losses;
        float pct = gp > 0 ? (float)row.wins / gp : 0f;
        var streak = CalcStreak(row.games);

        // Rank
        var rankLbl = new Label();
        rankLbl.AddToClassList("col-rank");
        rankLbl.text = row.rank.ToString();

        // Logo (32x32 desde carpeta específica, con crop)
        var logoElem = new VisualElement();
        logoElem.AddToClassList("col-team-logo");
        if (team != null)
        {
            var logo32 = Resources.Load<Sprite>($"Teams/Logos/32x32/{team.logo}");
            if (logo32 != null)
                logoElem.style.backgroundImage = new StyleBackground(logo32);
            else
                SetTeamLogo(logoElem, team.logo);
        }

        // Nombre
        var nameLbl = new Label();
        nameLbl.AddToClassList("col-team-name");
        nameLbl.text = team?.name ?? "???";

        // PJ
        var gpLbl = new Label();
        gpLbl.AddToClassList("col-stat");
        gpLbl.text = gp.ToString();

        // Victorias
        var wLbl = new Label();
        wLbl.AddToClassList("col-stat");
        wLbl.AddToClassList("col-wins");
        wLbl.text = row.wins.ToString();

        // Derrotas
        var lLbl = new Label();
        lLbl.AddToClassList("col-stat");
        lLbl.AddToClassList("col-losses");
        lLbl.text = row.losses.ToString();

        // %
        var pctLbl = new Label();
        pctLbl.AddToClassList("col-stat");
        pctLbl.text = pct.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        // Racha
        var streakLbl = new Label();
        streakLbl.AddToClassList("col-streak");
        streakLbl.text = streak.text;
        streakLbl.AddToClassList(streak.type == "win" ? "streak-win" :
                                  streak.type == "loss" ? "streak-loss" : "streak-none");

        elem.Add(rankLbl);
        elem.Add(logoElem);
        elem.Add(nameLbl);
        elem.Add(gpLbl);
        elem.Add(wLbl);
        elem.Add(lLbl);
        elem.Add(pctLbl);
        elem.Add(streakLbl);

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

    // ── ESTADISTICAS ───────────────────────────────────────
    void RefreshPlayerStats()
    {
        if (_season == null || _myTeam == null) return;

        var teamStats = DatabaseManager.Instance.GetTeamPlayerSeasonStats(_season.id, _myTeam.id, _manager.id);
        if (teamStats.Count == 0) return;

        var myPlayerIds = _players.Select(p => p.id).ToHashSet();
        teamStats = teamStats.Where(s => myPlayerIds.Contains(s.player_id)).ToList();
        if (teamStats.Count == 0) return;

        var scorer = teamStats.OrderByDescending(s => (float)s.total_points / s.games).First();
        SetStatCard("StatScorer", "StatScorerName", "StatScorerGames",
            (scorer.total_points / (float)scorer.games).ToString("F1"),
            $"{scorer.first_name} {scorer.last_name}",
            $"{scorer.games} {(scorer.games == 1 ? "Partido Jugado" : "Partidos Jugados")}",
            scorer.player_id);

        var rebounder = teamStats.OrderByDescending(s => (float)s.total_rebounds / s.games).First();
        SetStatCard("StatRebounder", "StatRebounderName", "StatRebounderGames",
            (rebounder.total_rebounds / (float)rebounder.games).ToString("F1"),
            $"{rebounder.first_name} {rebounder.last_name}",
            $"{rebounder.games} {(rebounder.games == 1 ? "Partido Jugado" : "Partidos Jugados")}",
            rebounder.player_id);

        var assister = teamStats.OrderByDescending(s => (float)s.total_assists / s.games).First();
        SetStatCard("StatAssister", "StatAssisterName", "StatAssisterGames",
            (assister.total_assists / (float)assister.games).ToString("F1"),
            $"{assister.first_name} {assister.last_name}",
            $"{assister.games} {(assister.games == 1 ? "Partido Jugado" : "Partidos Jugados")}",
            assister.player_id);

        var stealer = teamStats.OrderByDescending(s => (float)s.total_steals / s.games).First();
        SetStatCard("StatStealer", "StatStealerName", "StatStealerGames",
            (stealer.total_steals / (float)stealer.games).ToString("F1"),
            $"{stealer.first_name} {stealer.last_name}",
            $"{stealer.games} {(stealer.games == 1 ? "Partido Jugado" : "Partidos Jugados")}",
            stealer.player_id);

        var blocker = teamStats.OrderByDescending(s => (float)s.total_blocks / s.games).First();
        SetStatCard("StatBlocker", "StatBlockerName", "StatBlockerGames",
            (blocker.total_blocks / (float)blocker.games).ToString("F1"),
            $"{blocker.first_name} {blocker.last_name}",
            $"{blocker.games} {(blocker.games == 1 ? "Partido Jugado" : "Partidos Jugados")}",
            blocker.player_id);

        var rated = teamStats.OrderByDescending(s => (float)s.total_rating / s.games).First();
        SetStatCard("StatRated", "StatRatedName", "StatRatedGames",
            (rated.total_rating / (float)rated.games).ToString("F1"),
            $"{rated.first_name} {rated.last_name}",
            $"{rated.games} {(rated.games == 1 ? "Partido Jugado" : "Partidos Jugados")}",
            rated.player_id);
    }

    void SetStatCard(string valName, string playerName, string gamesName,
                     string val, string player, string games, int playerId = 0)
    {
        var valLbl = _root.Q<Label>(valName);
        var playerLbl = _root.Q<Label>(playerName);
        var gamesLbl = _root.Q<Label>(gamesName);
        var photoName = valName + "Photo";
        var photo = _root.Q<VisualElement>(photoName);

        if (valLbl != null) valLbl.text = val;
        if (playerLbl != null) playerLbl.text = player;
        if (gamesLbl != null) gamesLbl.text = games;

        if (photo != null && playerId > 0)
        {
            var p = _players.FirstOrDefault(pl => pl.id == playerId)
                     ?? DatabaseManager.Instance.GetPlayer(playerId);

            if (p != null)
            {
                Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
                if (tex != null)
                    photo.style.backgroundImage = new StyleBackground(tex);
            }
        }
    }

    // ── ESTADÍSTICAS DEL EQUIPO ─────────────────────────

    int GetMyTeamConferenceRank()
    {
        if (_myTeam == null || _allGames == null) return 0;

        var confTeams = _allTeams.Where(t => t.conference == _myTeam.conference).ToList();
        var standings = new List<(TeamData team, int wins, int losses, float pct)>();

        foreach (var team in confTeams)
        {
            var teamGames = _allGames.Where(g => g.is_played == 1 &&
                (g.home_team_id == team.id || g.away_team_id == team.id)).ToList();
            int wins = teamGames.Count(g =>
                (g.home_team_id == team.id && g.home_score > g.away_score) ||
                (g.away_team_id == team.id && g.away_score > g.home_score));
            int losses = teamGames.Count - wins;
            float pct = teamGames.Count > 0 ? (float)wins / teamGames.Count : 0f;
            standings.Add((team, wins, losses, pct));
        }

        standings.Sort((a, b) =>
        {
            if (b.pct != a.pct) return b.pct.CompareTo(a.pct);
            if (a.losses != b.losses) return a.losses.CompareTo(b.losses);
            return b.wins.CompareTo(a.wins);
        });

        for (int i = 0; i < standings.Count; i++)
        {
            if (standings[i].team.id == _myTeam.id)
                return i + 1;
        }
        return 0;
    }

    void RefreshTeamStats()
    {
        if (_myTeam == null) return;

        // Objetivo de la temporada
        if (_teamObjective != null)
            _teamObjective.text = _myTeam.objective ?? "--";

        // Estado del objetivo: calcular según posición en conferencia
        int rank = GetMyTeamConferenceRank();
        bool objectiveMet = false;
        string obj = _myTeam.objective ?? "";
        if (rank > 0)
        {
            if (obj == "Zona tranquila")
                objectiveMet = rank <= 12;          // 11+ = no entrar a nada
            else if (obj == "Play-In")
                objectiveMet = rank <= 10;          // 1-10 = al menos play-in
            else if (obj == "Playoffs")
                objectiveMet = rank <= 6;          // 1-6 = en posición de playoffs
            else if (obj == "Campeonato")
                objectiveMet = rank <= 2;           // 1-2 = top directo, contender
        }

        if (_teamObjectiveStatus != null)
        {
            string iconName = objectiveMet ? "boton-v-64px" : "boton-x-64px";
            var tex = Resources.Load<Texture2D>($"Icons/{iconName}");
            if (tex != null)
                _teamObjectiveStatus.style.backgroundImage = new StyleBackground(tex);
        }

        // Logo del equipo (32x32 desde carpeta específica)
        if (_teamStatsLogo != null)
        {
            var logo32 = Resources.Load<Sprite>($"Teams/Logos/32x32/{_myTeam.logo}");
            if (logo32 != null)
                _teamStatsLogo.style.backgroundImage = new StyleBackground(logo32);
            else
                SetTeamLogo(_teamStatsLogo, _myTeam.logo);
        }

        // Media equipo (overall)
        int teamAvg = _players.Count > 0 ? Mathf.RoundToInt((float)_players.Average(p => p.GetCalculatedAverage())) : 0;
        if (_teamOverallLabel != null)
            _teamOverallLabel.text = $"Media: {teamAvg}";
        if (_teamOverallRingVal != null)
            _teamOverallRingVal.text = teamAvg.ToString();

        // Química equipo
        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        if (_teamChemistryLabel != null)
            _teamChemistryLabel.text = $"Química: {chemistry}";
        if (_teamChemistryIcon != null)
        {
            var tex = Resources.Load<Texture2D>("Icons/quimica");
            if (tex != null)
                _teamChemistryIcon.style.backgroundImage = new StyleBackground(tex);
        }
        if (_teamChemistryRingVal != null)
            _teamChemistryRingVal.text = chemistry.ToString();
        if (_teamChemistryRing != null)
        {
            Color ringColor;
            if (chemistry >= 70)
                ringColor = new Color32(39, 174, 96, 255);
            else if (chemistry >= 40)
                ringColor = new Color32(212, 160, 23, 255);
            else
                ringColor = new Color32(192, 57, 43, 255);
            _teamChemistryRing.style.borderTopColor = new StyleColor(ringColor);
            _teamChemistryRing.style.borderBottomColor = new StyleColor(ringColor);
            _teamChemistryRing.style.borderLeftColor = new StyleColor(ringColor);
            _teamChemistryRing.style.borderRightColor = new StyleColor(ringColor);
        }

        // Pabellón
        if (_teamArenaName != null)
            _teamArenaName.text = _myTeam.arena ?? "Pabellón";
        if (_teamArenaCapacity != null)
            _teamArenaCapacity.text = $"{_myTeam.capacity:N0} espectadores";

        // Estrellas de reputación
        if (_teamReputationStars != null)
        {
            _teamReputationStars.Clear();
            for (int i = 1; i <= 5; i++)
            {
                var star = new Label { text = i <= _myTeam.reputation ? "★" : "☆" };
                star.AddToClassList("team-stat-star");
                star.style.color = i <= _myTeam.reputation ? new Color(0.84f, 0.63f, 0.09f) : new Color(0.2f, 0.25f, 0.35f);
                _teamReputationStars.Add(star);
            }
        }
    }

    // ── RELACIONES ───────────────────────────────────────

    void RefreshBoard()
    {
        if (_manager == null) return;
        SetCircle(_barTrust, _valTrust, _manager.trust);
        SetCircle(_barMorale, _valMorale, _manager.morale);
        SetCircle(_barFanConfidence, _valFanConfidence, _manager.fan_confidence);
    }

    // ── MENSAJES ────────────────────────────────────────

    void RefreshMessages()
    {
        if (_messagesBody == null || _manager == null) return;
        _messagesBody.Clear();

        var all = DatabaseManager.Instance.GetMessages(_manager.id);
        if (all == null || all.Count == 0)
        {
            var lbl = new Label("NO HAY NOTICIAS");
            lbl.AddToClassList("no-games-text");
            _messagesBody.Add(lbl);
            return;
        }

        var latest = all.OrderByDescending(m => m.id)
                        .Where(m => !m.title.StartsWith("Premios del Mes"))
                        .Take(8).ToList();

        foreach (var msg in latest)
        {
            var item = new VisualElement();
            item.AddToClassList("message-item");

            var subj = new Label(msg.title ?? "Sin asunto");
            subj.AddToClassList("message-item-subject");
            item.Add(subj);

            var body = new Label(msg.body ?? "");
            body.AddToClassList("message-item-body");
            item.Add(body);

            _messagesBody.Add(item);

            if (msg.is_read == 0)
                DatabaseManager.Instance.MarkMessageRead(msg.id);
        }
    }

    void SetCircle(VisualElement circle, Label val, int value)
    {
        if (circle == null || val == null) return;
        float pct = Mathf.Clamp01(value / 100f);

        Color bgColor, borderColor;
        if (value >= 70)
        {
            bgColor = new Color32(39, 174, 96, 40);
            borderColor = new Color32(39, 174, 96, 255);
        }
        else if (value >= 40)
        {
            bgColor = new Color32(212, 160, 23, 40);
            borderColor = new Color32(212, 160, 23, 255);
        }
        else
        {
            bgColor = new Color32(192, 57, 43, 40);
            borderColor = new Color32(192, 57, 43, 255);
        }

        circle.style.backgroundColor = new StyleColor(bgColor);
        circle.style.borderTopColor = new StyleColor(borderColor);
        circle.style.borderBottomColor = new StyleColor(borderColor);
        circle.style.borderLeftColor = new StyleColor(borderColor);
        circle.style.borderRightColor = new StyleColor(borderColor);

        val.text = $"{value}%";
    }

    // ── HELPERS ──────────────────────────────────────────

    void SetTeamLogo(VisualElement elem, string logoName, string sizeFolder = null)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;

        if (!string.IsNullOrEmpty(sizeFolder))
        {
            var sprite = Resources.Load<Sprite>($"Teams/Logos/{sizeFolder}/{logoName}");
            if (sprite != null)
            {
                elem.style.backgroundImage = new StyleBackground(sprite);
                return;
            }
        }

        if (_logoSprites.TryGetValue(logoName, out var fallback))
            elem.style.backgroundImage = new StyleBackground(fallback);
    }

    List<PlayerData> BuildAllStarRoster(string conference)
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var conferenceTeamIds = allTeams
            .Where(t => t.conference == conference)
            .Select(t => t.id)
            .ToHashSet();

        var allPlayers = DatabaseManager.Instance.Db.Table<PlayerData>()
            .Where(p => p.team_id != 0 && p.injury_days == 0)
            .ToList();

        var candidates = allPlayers
            .Where(p => conferenceTeamIds.Contains(p.team_id))
            .ToList();

        var roster = new List<PlayerData>();
        HashSet<int> usedIds = new();
        var positions = PositionCodes.Order;

        foreach (var pos in positions)
        {
            var selected = candidates
                .Where(p => p.position == pos)
                .OrderByDescending(p => p.overall)
                .Take(3)
                .ToList();
            foreach (var p in selected)
            {
                usedIds.Add(p.id);
                roster.Add(p);
            }
        }

        if (roster.Count < 15)
        {
            var remaining = candidates
                .Where(p => !usedIds.Contains(p.id))
                .OrderByDescending(p => p.overall)
                .Take(15 - roster.Count);
            roster.AddRange(remaining);
        }

        return roster;
    }

    // ── CLASE AUXILIAR ───────────────────────────────────

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

    // ═══════════════════════════════════════════════════════════
    //  CONFIG MODAL
    // ═══════════════════════════════════════════════════════════

    void SetupPlayerCoach()
    {
        if (_myTeam == null || _manager == null) return;
        DatabaseManager.Instance.AddPlayerCoachEntry(_myTeam.id, _manager.name);
        DatabaseManager.Instance.SetCoachInactive(_myTeam.id);
    }

    void AssignSeasonPoints()
    {
        if (_manager == null || _season == null) return;

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        allGames.AddRange(DatabaseManager.Instance.GetPlayoffGames(_manager.id));
        allGames.AddRange(DatabaseManager.Instance.GetPlayInGames(_manager.id));
        var ranking = DatabaseManager.Instance.GetCoachRanking();

        var eastTeams = allTeams.Where(t => t.conference == "East").ToList();
        var westTeams = allTeams.Where(t => t.conference == "West").ToList();

        var teamRank = new Dictionary<int, int>();
        ComputeConfRanks(eastTeams, allGames, teamRank);
        ComputeConfRanks(westTeams, allGames, teamRank);

        int championId = 0, finalistId = 0;
        var finalsList = DatabaseManager.Instance.GetFinalsRecords();
        var seasonKey = $"{_season.year_start}-{_season.year_end}";
        var finals = finalsList.FirstOrDefault(f => f.season == seasonKey);
        if (finals != null)
        {
            championId = allTeams.FirstOrDefault(t => t.logo == finals.champ_keyword)?.id ?? 0;
            finalistId = allTeams.FirstOrDefault(t => t.logo == finals.finalist_keyword)?.id ?? 0;
        }

        foreach (var team in allTeams)
        {
            var coach = ranking.FirstOrDefault(c => c.team_id == team.id && (c.status == "active" || c.status == "player"));
            if (coach == null) continue;

            int points = CalcTeamPoints(team, allGames, teamRank, championId, finalistId);
            DatabaseManager.Instance.UpdateCoachScore(coach.id, points);
        }
    }

    void ComputeConfRanks(List<TeamData> confTeams, List<GameData> allGames, Dictionary<int, int> teamRank)
    {
        var standings = new List<(int teamId, int wins, int losses)>();
        foreach (var t in confTeams)
        {
            var tg = allGames.Where(g => g.is_played == 1 && g.game_type == "regular" && (g.home_team_id == t.id || g.away_team_id == t.id)).ToList();
            int w = tg.Count(g => (g.home_team_id == t.id && g.home_score > g.away_score) || (g.away_team_id == t.id && g.away_score > g.home_score));
            standings.Add((t.id, w, tg.Count - w));
        }
        standings.Sort((a, b) =>
        {
            float pctA = a.wins + a.losses > 0 ? (float)a.wins / (a.wins + a.losses) : 0;
            float pctB = b.wins + b.losses > 0 ? (float)b.wins / (b.wins + b.losses) : 0;
            if (pctB != pctA) return pctB.CompareTo(pctA);
            if (a.losses != b.losses) return a.losses.CompareTo(b.losses);
            return b.wins.CompareTo(a.wins);
        });
        for (int i = 0; i < standings.Count; i++)
            teamRank[standings[i].teamId] = i + 1;
    }

    int CalcTeamPoints(TeamData team, List<GameData> allGames, Dictionary<int, int> teamRank, int championId, int finalistId)
    {
        int pts = 0;

        var rg = allGames.Where(g => g.is_played == 1 && g.game_type == "regular" && (g.home_team_id == team.id || g.away_team_id == team.id)).ToList();
        int rWins = rg.Count(g => (g.home_team_id == team.id && g.home_score > g.away_score) || (g.away_team_id == team.id && g.away_score > g.home_score));
        pts += Mathf.RoundToInt(rWins * 0.5f);
        if (rWins >= 60) pts += 20;
        else if (rWins >= 50) pts += 10;

        var pg = allGames.Where(g => g.is_played == 1 && (g.game_type == "playoff" || g.game_type == "playin") && (g.home_team_id == team.id || g.away_team_id == team.id)).ToList();
        int pWins = pg.Count(g => (g.home_team_id == team.id && g.home_score > g.away_score) || (g.away_team_id == team.id && g.away_score > g.home_score));
        pts += pWins * 2;

        if (team.id == championId) pts += 100;
        else if (team.id == finalistId) pts += 50;
        else if (pWins >= 8) pts += 25;

        int rank = teamRank.TryGetValue(team.id, out var r) ? r : 0;
        string obj = team.objective ?? "";
        bool met = false;
        if (rank > 0)
        {
            if (obj == "Zona tranquila") met = rank <= 12;
            else if (obj == "Play-In") met = rank <= 10;
            else if (obj == "Playoffs") met = rank <= 6;
            else if (obj == "Campeonato") met = rank <= 2;
        }

        if (met)
        {
            if (obj == "Campeonato") pts += 40;
            else if (obj == "Playoffs") pts += 25;
            else if (obj == "Play-In") pts += 12;
            else if (obj == "Zona tranquila") pts += 6;
        }
        else if (rank > 0)
        {
            pts -= 15;
        }

        return pts;
    }
}
