using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
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
    private Label _lastHomeRecord;
    private Label _lastAwayName;
    private Label _lastAwayRecord;
    private Label _lastHomeScore;
    private Label _lastAwayScore;
    private Label _lastResultBadge;
    private Label _lastGameDate;
    private Label _lastGameArena;

    // Próximo partido
    private Label _noNextGame;
    private VisualElement _nextGameResult;
    private VisualElement _nextHomeLog;
    private VisualElement _nextAwayLog;
    private Label _nextHomeName;
    private Label _nextHomeRecord;
    private Label _nextAwayName;
    private Label _nextAwayRecord;
    private Label _nextGameDate;
    private Label _nextGameArena;
    private VisualElement _lastGameMeta;
    private VisualElement _nextGameMeta;

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
    private Label _storylineBadge; // Label (no VisualElement): se le escribe .text

    // Datos
    private List<TeamData> _allTeams;
    private List<GameData> _allGames;
    // Récords por equipo calculados una vez por carga de _allGames (evita recorrer
    // todos los partidos en cada llamada a GetTeamRecordShort).
    private Dictionary<int, (int w, int l)> _teamRecordCache;
    private List<PlayerData> _players = new();
    private Dictionary<string, PlayerData> _playerByName = new();

    // Sprites
    private Dictionary<string, Sprite> _logoSprites = new();
    private static Dictionary<string, Texture2D> _noticiasSprites = new();

    // Fired modal
    private VisualElement _firedOverlay;

    // Injured lineup modal
    private bool _injuredModalResolved;
    private bool _injuredModalGoToQuinteto;
    private static List<int> _pendingRecoveredIds = new();

    // IR recovery → roster full modal
    private static List<int> _pendingIRReleaseIds = new();
    private int _pendingIRReleaseCount;
    private bool _irReleaseModalResolved;

    // Empty lineup modal
    private bool _emptyLineupModalResolved;
    private bool _emptyLineupGoToQuinteto;

    // Aviso de rotación incompleta (minutos sin asignar o inválidos)
    private bool _rotationModalResolved;
    private bool _rotationModalGoToRotation;
    private bool _fastSimAbortRotation;

    // Load management modal
    private bool _loadMgmtModalResolved;
    private bool _loadMgmtModalGoToQuinteto;
    private List<(LineupData li, PlayerData p)> _loadMgmtTiredPlayers = new();
    private bool _loadMgmtIsB2B;

    // Simulación rápida (hasta una fecha elegida en el Calendario)
    private System.DateTime? _fastSimTarget;
    private string _fastSimStartDate;
    private bool _fastSimAborted;
    private string _fastSimErrorMessage;
    private List<int> _fastSimAbortEmptySlots = new();
    private List<(LineupData li, PlayerData p)> _fastSimAbortInjured = new();
    private bool _fastSimSkipPreBatch;
    /// <summary>
    /// Día de partido para el que ya se commiteó el pre-lote (lesiones/físico,
    /// entrenamiento, mercado IA) y temporada a la que pertenece. Se usa para no
    /// aplicarlo dos veces cuando la pantalla de previa pide simular el día, y se
    /// limpia al cerrar el día para que el siguiente haga el suyo.
    /// </summary>
    private int _preBatchGameDay = int.MinValue;
    private int _preBatchSeasonId = -1;
    private int _fastSimAutoFixAttempts;
    private bool _fastSimRunning;
    private bool _fastSimStopRequested;
    private bool _fastSimAbortOffers;
    private bool _fastSimOfferPauseActive;
    private bool _offerModalResolved;
    private bool _offerModalGoToQuinteto;
    private bool _tradeOfferModalResolved;
    private bool _tradeOfferAccepted;
    private bool _tradeOfferGoToQuinteto;
    private int _pendingTradeChem;
    private bool _fastSimAbortRecovered;
    private bool _recoveryModalResolved;
    private bool _recoveryModalGoToQuinteto;

    private bool _deadlineDayActive;
    private bool _deadlineDayModalShown;
    private int _deadlineModalSeasonId;
    private bool _showDeadlineWeekModal;

    // Toast de logros del GM
    private VisualElement _achievementToast;
    private VisualElement _achievementToastIcon;
    private Label _achievementToastDesc;
    private bool _toastScheduled;

    // AI de GMs: estrategia por equipo y cooldown por equipo para traspasos
    private Dictionary<int, TeamStrategy> _teamStrategyCache = new();
    private Dictionary<int, int> _teamTradeCooldown = new();
    // RNG seguro para el camino AI (se ejecuta en hilo de fondo; UnityEngine.Random no es thread-safe)
    private readonly System.Random _aiRng = new System.Random();
    private bool _airRenewalsProcessed;
    private bool _renewalDeadlineModalShown;
    private bool _renewalDeadlineResolved;

    protected override void OnEnable()
    {
        base.OnEnable();
        _deadlineDayActive = false;
        _firedOverlay = new VisualElement();
        _firedOverlay.AddToClassList("fired-modal-overlay");
        _root.Add(_firedOverlay);

        CreateAchievementToast();

        SetSidebarNavigationEnabled(true);
        AudioManager.Instance?.PlayMusic("backgroundMenu");
        SetupPlayerCoach();

        // Si viene una solicitud de simulación rápida desde el Calendario, se
        // arranca aquí (diferido un frame para que OnEnable termine).
        var pendingTarget = GameResultCache.FastSimTargetDate;
        GameResultCache.FastSimTargetDate = null;
        if (pendingTarget.HasValue)
        {
            _renewalDeadlineModalShown = false;
            _fastSimTarget = pendingTarget.Value;
            _root.schedule.Execute(() =>
            {
                if (_fastSimTarget.HasValue)
                    StartCoroutine(FastSimRoutine(_fastSimTarget.Value));
            }).ExecuteLater(0);
        }
        else
        {
            _fastSimTarget = null;
            _fastSimRunning = false;
            _fastSimStopRequested = false;
            _fastSimOfferPauseActive = false;
            _fastSimAbortOffers = false;
            if (_season != null && _season.id != _deadlineModalSeasonId)
            {
                _deadlineDayModalShown = false;
                _deadlineModalSeasonId = _season.id;
                _teamTradeCooldown.Clear();
                _airRenewalsProcessed = false;
                _renewalDeadlineModalShown = false;
            }
            CheckBudgetWarning();
            ProcessMaturedOffers();
            ShowPendingRecoveryModal();
            ShowPendingIRReleaseModal();
            CheckTradeDeadlineModal();
        }

        // PreGame → simular día y navegar a MatchDay
        if (_simulateFromPreGame)
        {
            _simulateFromPreGame = false;
            ShowLoading();
            // El pre-lote solo se omite si ya se commiteó para ESTE día (flujo normal:
            // AVANZAR DÍA → previa → IR AL PARTIDO). Si la previa se abrió con el
            // enlace "VER PREVIA" sin haber avanzado el día, el pre-lote aún no se ha
            // hecho y debe aplicarse ahora (lesiones, entrenamiento, mercado IA).
            _root.schedule.Execute(() => StartCoroutine(ProcessGameDayRoutine(skipPreBatch: PreBatchAlreadyDone()))).ExecuteLater(0);
        }
    }

    private void OnDisable()
    {
        // Una simulación interrumpida no debe continuar ni contaminar la siguiente partida.
        StopAllCoroutines();
        _fastSimTarget = null;
        _fastSimRunning = false;
        _fastSimStopRequested = false;
        _fastSimOfferPauseActive = false;
        _fastSimAbortOffers = false;
        _tradeOfferModalResolved = true;   // ningún modal pendiente puede bloquear la próxima simulación
        _tradeOfferAccepted = false;
        _tradeOfferGoToQuinteto = false;
        GameResultCache.FastSimTargetDate = null;

        // Liberar SIEMPRE el estado de carga: si no, _isLoading queda a true y el
        // botón de avanzar deja de responder al volver (soft-lock). La transacción
        // del día que pudiera quedar abierta la revierte el finally de ProcessGameDayRoutine.
        _isLoading = false;
        _spinScheduler?.Pause();
        _spinScheduler = null;
        if (_loadingSpinner != null) _loadingSpinner.style.display = DisplayStyle.None;
        _btnAction?.SetEnabled(true);
        SetSidebarNavigationEnabled(true);
        ScreenManager.NavigationLocked = false;
    }

    void Update()
    {
        // Toasts de logros del GM (cola consumida en el hilo principal). Se
        // procesa antes del guard de modales para no perder desbloqueos si al
        // terminar el día queda algún overlay abierto.
        if (_achievementToast != null && !_toastScheduled)
        {
            var def = AchievementService.TakeNextToast();
            if (def != null)
                ShowAchievementToast(def);
        }

        if (IsAnyModalOpen()) return;
        if (Input.GetKeyDown(KeyCode.S))
        {
            PlayClick();
            OnActionClicked();
        }
    }

    void CreateAchievementToast()
    {
        _achievementToast = new VisualElement();
        _achievementToast.style.position = Position.Absolute;
        _achievementToast.style.left = 15;
        _achievementToast.style.right = StyleKeyword.Auto;
        _achievementToast.style.top = 15;
        _achievementToast.style.width = Length.Percent(30);
        _achievementToast.style.flexDirection = FlexDirection.Row;
        _achievementToast.style.alignItems = Align.Center;
        _achievementToast.style.backgroundColor = new StyleColor(new Color(0.09f, 0.12f, 0.19f, 1f));
        _achievementToast.style.borderTopWidth = 1;
        _achievementToast.style.borderBottomWidth = 1;
        _achievementToast.style.borderLeftWidth = 1;
        _achievementToast.style.borderRightWidth = 1;
        var gold = new Color(0.85f, 0.70f, 0.30f, 1f);
        _achievementToast.style.borderTopColor = new StyleColor(gold);
        _achievementToast.style.borderBottomColor = new StyleColor(gold);
        _achievementToast.style.borderLeftColor = new StyleColor(gold);
        _achievementToast.style.borderRightColor = new StyleColor(gold);
        _achievementToast.style.borderTopLeftRadius = 8;
        _achievementToast.style.borderTopRightRadius = 8;
        _achievementToast.style.borderBottomLeftRadius = 8;
        _achievementToast.style.borderBottomRightRadius = 8;
        _achievementToast.style.paddingTop = 10;
        _achievementToast.style.paddingBottom = 10;
        _achievementToast.style.paddingLeft = 14;
        _achievementToast.style.paddingRight = 14;
        _achievementToast.style.display = DisplayStyle.None;

        _achievementToastIcon = new VisualElement();
        _achievementToastIcon.style.width = 36;
        _achievementToastIcon.style.height = 36;
        _achievementToastIcon.style.marginRight = 12;
        _achievementToast.Add(_achievementToastIcon);

        var textBox = new VisualElement();
        textBox.style.flexDirection = FlexDirection.Column;
        var titleLbl = new Label("🏆 LOGRO DESBLOQUEADO");
        titleLbl.style.color = gold;
        titleLbl.style.fontSize = 15;
        titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLbl.style.marginBottom = 2;
        titleLbl.AddToClassList("toast-achievement-title");
        textBox.Add(titleLbl);

        _achievementToastDesc = new Label();
        _achievementToastDesc.style.color = Color.white;
        _achievementToastDesc.style.fontSize = 14;
        _achievementToastDesc.AddToClassList("toast-achievement-desc");
        textBox.Add(_achievementToastDesc);

        _achievementToast.Add(textBox);

        _root.Add(_achievementToast);
    }

    void ShowAchievementToast(GmAchievementDefinition def)
    {
        if (_achievementToast == null) return;
        var tex = Resources.Load<Texture2D>($"Icons/{def.Icon}");
        if (tex != null)
            _achievementToastIcon.style.backgroundImage = new StyleBackground(tex);
        else
            _achievementToastIcon.style.backgroundImage = null;
        _achievementToastDesc.text = def.Title;
        _achievementToast.style.display = DisplayStyle.Flex;
        _toastScheduled = true;
        _root.schedule.Execute(() =>
        {
            _achievementToast.style.display = DisplayStyle.None;
            _toastScheduled = false;
        }).ExecuteLater(4000);
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
        _lastHomeRecord = _root.Q<Label>("LastHomeRecord");
        _lastAwayName = _root.Q<Label>("LastAwayName");
        _lastAwayRecord = _root.Q<Label>("LastAwayRecord");
        _lastHomeScore = _root.Q<Label>("LastHomeScore");
        _lastAwayScore = _root.Q<Label>("LastAwayScore");
        _lastResultBadge = _root.Q<Label>("LastResultBadge");
        _lastGameDate = _root.Q<Label>("LastGameDate");
        _lastGameArena = _root.Q<Label>("LastGameArena");

        // Próximo partido
        _noNextGame = _root.Q<Label>("NoNextGame");
        _nextGameResult = _root.Q<VisualElement>("NextGameResult");
        _nextHomeLog = _root.Q<VisualElement>("NextHomeLog");
        _nextAwayLog = _root.Q<VisualElement>("NextAwayLog");
        _nextHomeName = _root.Q<Label>("NextHomeName");
        _nextHomeRecord = _root.Q<Label>("NextHomeRecord");
        _nextAwayName = _root.Q<Label>("NextAwayName");
        _nextAwayRecord = _root.Q<Label>("NextAwayRecord");
        _nextGameDate = _root.Q<Label>("NextGameDate");
        _nextGameArena = _root.Q<Label>("NextGameArena");
        _lastGameMeta = _root.Q("LastGameMeta");
        _nextGameMeta = _root.Q("NextGameMeta");

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

        // Badge de hilo narrativo activo, junto al título del panel de noticias.
        _storylineBadge = null;
        var messagesHeaderLeft = _messagesBody?.parent?.parent?.Q<VisualElement>(className: "panel-header-left");
        if (messagesHeaderLeft != null)
        {
            _storylineBadge = new Label();
            _storylineBadge.AddToClassList("storyline-badge");
            _storylineBadge.style.display = DisplayStyle.None;
            messagesHeaderLeft.Add(_storylineBadge);
        }

        // Forzar anchos de columnas Row 2 (USS flex no es fiable en build)
        var standingsPanel = _root.Q<VisualElement>("PanelStandings");
        if (standingsPanel != null)
        {
            standingsPanel.style.width = 450;
            standingsPanel.style.minWidth = 450;
            standingsPanel.style.maxWidth = 450;
            standingsPanel.style.flexGrow = 0;
            standingsPanel.style.flexShrink = 0;
        }
        var centerCol = _root.Q<VisualElement>(className: "center-column");
        if (centerCol != null)
        {
            centerCol.style.flexGrow = 1;
            centerCol.style.flexShrink = 1;
        }
        var messagesPanel = _root.Q<VisualElement>("PanelMessages");
        if (messagesPanel != null)
        {
            messagesPanel.style.width = 450;
            messagesPanel.style.minWidth = 450;
            messagesPanel.style.maxWidth = 450;
            messagesPanel.style.flexGrow = 0;
            messagesPanel.style.flexShrink = 0;
        }
    }
    protected override void LoadData()
    {
        base.LoadData();

        var logos = TeamImages.Logos(200);
        foreach (var s in logos) _logoSprites[s.name] = s;

        if (_noticiasSprites.Count == 0)
        {
            var nombres = new[] { "contratado", "despedido", "rechazado", "entrenamiento", "lesionados", "obras-finalizadas", "pago-nominas", "prestamo-bancario", "renovacion", "traspaso", "bienvenida", "recuperado", "quejas", "premios", "eliminacion" };
            foreach (var n in nombres)
            {
                var tex = Resources.Load<Texture2D>("Noticias/" + n);
                if (tex != null) _noticiasSprites[n] = tex;
            }
        }

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
        _teamRecordCache = null;

        // Clasificación: mostrar por defecto la conferencia de mi equipo
        if (_myTeam != null)
            _currentConf = _myTeam.conference == "East" ? "East" : "West";

        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);

        _playerByName.Clear();
        foreach (var p in DatabaseManager.Instance.GetAllPlayers())
            _playerByName[$"{p.first_name} {p.last_name}"] = p;
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

        // Panel footer links (query by link text)
        var footerLinkTexts = new (string text, GameScreen screen)[]
        {
            ("VER RESUMEN", GameScreen.Results),
                ("VER PREVIA", GameScreen.PreGame),
            ("VER CLASIFICACIÓN COMPLETA", GameScreen.Standings),
            ("VER TODAS LAS NOTICIAS", GameScreen.Messages),
        };
        var allClickables = _root.Query(className: "panel-footer").ToList();
        allClickables.AddRange(_root.Query(className: "game-meta-footer").ToList());
        foreach (var clickable in allClickables)
        {
            var linkLabel = clickable.Q<Label>(className: "panel-footer-link");
            if (linkLabel == null) continue;
            foreach (var (text, screen) in footerLinkTexts)
            {
                if (linkLabel.text == text)
                {
                    clickable.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(screen); });
                    break;
                }
            }
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

        if (IsFeb12OfYearEnd() && !_renewalDeadlineModalShown)
            ShowRenewalDeadlineModal(false);
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
                return CalendarHelper.SeasonStart(_season.year_start).ToString("dd/MM/yyyy");
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

            var seasonStart = CalendarHelper.SeasonStart(_season.year_start);
            return seasonStart.AddDays(_season.current_game_day - 1).ToString("dd/MM/yyyy");
        }
        catch { return ""; }
    }

    // ── BOTÓN ACCIÓN ─────────────────────────────────────

    void RefreshActionButton()
    {
        if (_fastSimRunning)
        {
            SetActionBtn("DETENER SIMULACIÓN", "");
            return;
        }

        if (_season == null || _myTeam == null)
        {
            SetActionBtn("CONTINUAR", "");
            return;
        }

        if (_deadlineDayActive)
        {
            SetActionBtn("DEADLINE DAY ACTIVO", "");
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

    void SetSidebarNavigationEnabled(bool enabled)
    {
        _root.Query<Button>(null, "nav-item").ForEach(btn => btn.SetEnabled(enabled));
        _root.Query<Button>(null, "nav-submenu-item").ForEach(btn => btn.SetEnabled(enabled));
    }

    void OnActionClicked()
    {
        if (_fastSimRunning)
        {
            _fastSimStopRequested = true;
            return;
        }
        if (_season == null || _isLoading) return;
        if (_deadlineDayActive) return;

        if (IsFeb7OfYearEnd() && !_deadlineDayModalShown)
        {
            _deadlineDayModalShown = true;
            ShowDeadlineDayModal();
            return;
        }

        if (IsFeb12OfYearEnd() && !_renewalDeadlineModalShown)
        {
            _renewalDeadlineModalShown = true;
            ShowRenewalDeadlineModal();
            return;
        }

        // ── Despido anticipado: equipo eliminado fuera de finished ──
        if (_season.phase == "playin" && _myTeam != null)
        {
            bool teamHasPlayIn = DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == _manager.id && g.game_type == "playin"
                       && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
            if (!teamHasPlayIn && ShowFiredModalIfNeeded()) return;
        }

        if (_season.phase == "playoff" && _myTeam != null)
        {
            bool teamHasPlayoffs = DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == _manager.id && g.game_type == "playoff"
                       && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
            if (!teamHasPlayoffs && ShowFiredModalIfNeeded()) return;
        }

        if (_season.phase == "finished")
        {
            if (ShowFiredModalIfNeeded()) return;
            ScreenManager.Instance.GoTo(GameScreen.SeasonSummary);
            return;
        }

        ShowLoading();
        StartCoroutine(ProcessGameDayRoutine(navigateToPreGame: true));
    }

    // Flag estático: PreGame → Dashboard solicita simulación al reactivarse
    internal static bool _simulateFromPreGame;

    /// <summary>
    /// Called by PreGameController to simulate the game day and navigate to MatchDay.
    /// Since Dashboard is inactive here, we set a flag and navigate back;
    /// DashboardController.OnEnable picks it up and starts the coroutine.
    /// </summary>
    public static void RequestSimulateAndGoToMatchDay()
    {
        _simulateFromPreGame = true;
        ScreenManager.Instance.GoTo(GameScreen.Dashboard);
    }

    /// <summary>
    /// ¿El pre-lote (lesiones/físico, entrenamiento, mercado IA) del día pendiente ya se
    /// aplicó? Si la previa se abrió con "VER PREVIA" sin haber avanzado el día, no se ha
    /// hecho todavía y debe aplicarse al simular.
    /// </summary>
    bool PreBatchAlreadyDone()
    {
        if (_season == null || _manager == null) return false;
        if (_preBatchSeasonId != _season.id) return false;
        return _preBatchGameDay == FindNextGameDay();
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
                    if (p.g_league_assigned == 1) continue;
                    if (!lineupByPlayer.TryGetValue(p.id, out var ls)) continue;
                    if (ls.slot == 0)
                        startersWithIdx.Add((p, ls.slot_index >= 0 ? ls.slot_index : 999));
                    else if (ls.slot == 1)
                        bench.Add(p);
                }

                var orderedStarters = startersWithIdx.OrderBy(s => s.sortIdx).Select(s => s.p).ToList();
                var active = orderedStarters.Concat(bench).Take(12).ToList();
                if (active.Count > 0)
                {
                    // Minutos configurados en la pantalla ROTACIÓN (-1 = automático).
                    foreach (var p in active)
                    {
                        if (lineupByPlayer.TryGetValue(p.id, out var ls))
                            p.rotation_minutes = ls.rotation_minutes;
                    }
                    return active;
                }
            }
        }

        var activePool = DatabaseManager.Instance.GetPlayersByTeam(teamId)
            .Where(p => p.injury_days == 0 && p.g_league_assigned == 0)
            .OrderByDescending(p => p.overall)
            .Take(12)
            .ToList();

        return BuildBalancedLineup(activePool);
    }

    // Quinteto IA balanceado por posición: mejor jugador de cada posición (primaria o
    // secundaria) en orden PG->SG->SF->PF->C; huecos cubiertos con posiciones cercanas
    // y último recurso el mejor overall restante. Los titulares se devuelven primero.
    List<PlayerData> BuildBalancedLineup(List<PlayerData> pool)
    {
        var starters = new PlayerData[PositionCodes.Order.Length];
        var used = new HashSet<int>();

        // Paso 1: mejor jugador por posición exacta (primaria o secundaria)
        for (int i = 0; i < PositionCodes.Order.Length; i++)
        {
            string pos = PositionCodes.Order[i];
            var best = pool
                .Where(p => !used.Contains(p.id) && PlaysPosition(p, pos))
                .OrderByDescending(p => p.overall)
                .FirstOrDefault();
            if (best != null)
            {
                starters[i] = best;
                used.Add(best.id);
            }
        }

        // Paso 2: rellenar huecos con posiciones cercanas
        for (int i = 0; i < starters.Length; i++)
        {
            if (starters[i] != null) continue;
            string pos = PositionCodes.Order[i];
            var best = pool
                .Where(p => !used.Contains(p.id) && IsNearbyPosition(p, pos))
                .OrderByDescending(p => p.overall)
                .FirstOrDefault();
            if (best != null)
            {
                starters[i] = best;
                used.Add(best.id);
            }
        }

        // Paso 3: último recurso — mejor overall restante
        for (int i = 0; i < starters.Length; i++)
        {
            if (starters[i] != null) continue;
            var best = pool
                .Where(p => !used.Contains(p.id))
                .OrderByDescending(p => p.overall)
                .FirstOrDefault();
            if (best != null)
            {
                starters[i] = best;
                used.Add(best.id);
            }
        }

        var bench = pool.Where(p => !used.Contains(p.id)).Take(7).ToList();
        return starters.Where(s => s != null).Concat(bench).ToList();
    }

    static bool PlaysPosition(PlayerData p, string pos)
    {
        return p.position == pos || p.secondary_position == pos;
    }

    static bool IsNearbyPosition(PlayerData p, string pos)
    {
        int idx = System.Array.IndexOf(PositionCodes.Order, pos);
        var nearby = new List<string>();
        if (idx > 0) nearby.Add(PositionCodes.Order[idx - 1]);
        if (idx < PositionCodes.Order.Length - 1) nearby.Add(PositionCodes.Order[idx + 1]);
        return nearby.Contains(p.position) || nearby.Contains(p.secondary_position);
    }

    System.Collections.IEnumerator ProcessGameDayRoutine(bool fastSim = false, bool skipPreBatch = false, bool navigateToPreGame = false)
    {
        int gameDay = FindNextGameDay();
        Debug.Log($"[GameDay] ProcessGameDayRoutine started. gameDay={gameDay}");
        if (gameDay == 0)
        {
            Debug.Log("[Dashboard] No hay más partidos programados.");
            // Red de seguridad: si no quedan partidos pero la fase no es terminal,
            // avanzar/cerrar la temporada en lugar de dejar la partida bloqueada.
            TryEscapeStuckPhase();
            if (!fastSim) HideLoading();
            yield break;
        }

        var db = DatabaseManager.Instance.Db;

        // En una simulación rápida reanudada tras el modal de alineación, el
        // pre-lote del día ya se commiteó antes del aborto; se evita re-ejecutarlo.
        bool effectiveSkipPreBatch = skipPreBatch || (fastSim && _fastSimSkipPreBatch);
        _fastSimSkipPreBatch = false;

        if (!effectiveSkipPreBatch)
        {
            // Paso 1: lesiones y recuperaciones (background)
            var recoveryTask = DatabaseManager.Instance.RunInBackground(conn =>
            {
                conn.BeginTransaction();
                var recovered = new List<(int id, string first, string last)>();
                var recoveredFromIR = new List<int>();
                var rng = new System.Random();

                var teamsPlayToday = new HashSet<int>();
                if (gameDay > 0)
                {
                    foreach (var g in conn.Table<GameData>().Where(g => g.game_day == gameDay).ToList())
                    {
                        teamsPlayToday.Add(g.home_team_id);
                        teamsPlayToday.Add(g.away_team_id);
                    }
                }

                // E2: reputación del jefe de servicios médicos por equipo (acelera la recuperación).
                var medicoByTeam = new Dictionary<int, int>();
                foreach (var e in conn.Table<EmployeeData>().Where(e => e.position == "MEDICO").ToList())
                    medicoByTeam[e.team_id] = e.reputation;

                string today = _season?.current_date ?? "";
                int seasonId = _season?.id ?? 0;

                var allTeams = conn.Table<TeamData>().ToList();
                foreach (var team in allTeams)
                {
                    var players = conn.Table<PlayerData>().Where(p => p.team_id == team.id).ToList();
                    int medicoRep = medicoByTeam.TryGetValue(team.id, out var rep) ? rep : 0;
                    foreach (var p in players)
                    {
                        if (p.injury_days > 0)
                        {
                            var def = InjuryCatalog.ByName(p.injury_type);
                            var sev = def != null
                                ? def.severity
                                : InjuryCatalog.SeverityOf(p.injury_total_days > 0 ? p.injury_total_days : p.injury_days);

                            p.injury_days = InjuryHelper.EvolveDays(
                                p.injury_days, p.injury_total_days, sev, medicoRep, p.fisico, rng.NextDouble());

                            if (p.injury_days <= 0)
                            {
                                int episodeDays = p.injury_total_days > 0 ? p.injury_total_days : 1;

                                // Parte médico: se cierra el expediente en el historial.
                                conn.Insert(new InjuryHistoryData
                                {
                                    player_id = p.id,
                                    team_id = p.team_id,
                                    injury_type = p.injury_type,
                                    severity = InjuryCatalog.SeverityLabel(sev),
                                    days = episodeDays,
                                    season_id = seasonId,
                                    game_day = gameDay,
                                    date = today,
                                    outcome = "recuperado",
                                    treated = p.treated
                                });

                                p.injury_days = 0;
                                p.injury_total_days = 0;
                                p.recovery_risk = 0;
                                p.injury_type = "";
                                p.treated = 0;
                                if (p.is_on_ir == 1)
                                {
                                    p.is_on_ir = 0;
                                    if (p.team_id == _myTeam.id)
                                        recoveredFromIR.Add(p.id);
                                }
                                if (p.team_id == _myTeam.id)
                                    recovered.Add((p.id, p.first_name, p.last_name));
                            }
                            conn.Update(p);
                        }
                        else if (p.recovery_risk > 0)
                        {
                            // Alta forzada: descansar cura el riesgo; jugar lo mantiene vivo.
                            p.recovery_risk -= InjuryHelper.RiskDecayForDay(teamsPlayToday.Contains(team.id));
                            if (p.recovery_risk <= 0)
                            {
                                p.recovery_risk = 0;
                                p.injury_type = "";
                            }
                            conn.Update(p);
                        }
                        if (p.injury_days <= 0 && !teamsPlayToday.Contains(team.id))
                        {
                            // +10 en día de descanso: compensa el desgaste de la
                            // temporada sin que los titulares acaben hundidos (antes
                            // +8 dejaba un drenaje neto que hacía caer el acierto).
                            p.fisico = Mathf.Min(99, p.fisico + 10);
                            conn.Update(p);
                        }
                    }
                }

                // Equipos IA: si recuperar del IR deja la plantilla por encima del
                // límite, liberan al peor jugador para no quedarse sobre el tope.
                foreach (var team in allTeams)
                {
                    if (team.id == _myTeam.id) continue;
                    var roster = conn.Table<PlayerData>().Where(p => p.team_id == team.id && p.is_on_ir == 0).ToList();
                    if (roster.Count <= TradeHelper.MAX_ROSTER) continue;
                    var worst = roster.OrderBy(p => p.GetCalculatedAverage()).FirstOrDefault();
                    if (worst == null) continue;
                    worst.team_id = 0;
                    worst.contract_years = 0;
                    worst.guaranteed_years = 0;
                    worst.has_team_option = 0;
                    worst.has_player_option = 0;
                    worst.is_two_way = 0;
                    worst.is_on_ir = 0;
                    worst.g_league_assigned = 0;
                    conn.Update(worst);
                }

                conn.Commit();
                return (recovered, recoveredFromIR);
            });

            while (!recoveryTask.IsCompleted)
                yield return null;

            try
            {
                var recoveryResult = recoveryTask.Result;
                var recoveredPlayers = recoveryResult.recovered;
                var recoveredFromIR = recoveryResult.recoveredFromIR;

                if (recoveredPlayers.Count > 0)
                {
                    foreach (var (id, first, last) in recoveredPlayers)
                    {
                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = $"Recuperado: {first} {last}",
                            body = NewsTexts.Recuperacion($"{first} {last}", "lesión muscular", 0),
                            game_day = gameDay,
                            game_date = _season?.current_date ?? "",
                            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            is_read = 0,
                            category = NewsCategory.LESIONES,
                            news_type = NewsType.RECUPERACION
                        });
                        _pendingRecoveredIds.Add(id);
                    }
                }

                // Si un jugador del usuario sale del IR y la plantilla queda llena,
                // se muestra un modal para que decida a quién liberar.
                _pendingIRReleaseCount = 0;
                foreach (int id in recoveredFromIR)
                {
                    if (DatabaseManager.Instance.GetRosterCount(_myTeam.id) > TradeHelper.MAX_ROSTER)
                    {
                        _pendingIRReleaseIds.Add(id);
                        _pendingIRReleaseCount++;
                    }
                }
            }
            catch (System.Exception ex)
            {
                _pendingRecoveredIds.Clear();
                HandleDayError("Error en el lote pre-partido", "ERROR AL PROCESAR EL DÍA", "Ha ocurrido un error al procesar el día.", ex, fastSim);
                yield break;
            }
            if (fastSim) yield return null;

            // Paso 2: scouts, entrenamiento y renovaciones
        // B38: la fase de simulación + bookkeeping es síncrona y se ejecuta dentro de una
        // única transacción en un solo frame (sin yields entre BeginTransaction y Commit).
        // El respiro de la UI en simulación rápida se da AQUÍ, antes de abrirla.
        if (fastSim) yield return null;

        db.BeginTransaction();
            try
            {
                ProcessScouts();
                ProcessTraining();
                ProcessRenovations();
                if (_season != null && _season.current_game_day > 0 && _season.current_game_day % 7 == 0)
                    ProcessGLeagueDevelopment();
                db.Commit();
            }
            catch (System.Exception ex)
            {
                db.Rollback();
                _pendingRecoveredIds.Clear();
                HandleDayError("Error en el lote pre-partido", "ERROR AL PROCESAR EL DÍA", "Ha ocurrido un error al procesar el día.", ex, fastSim);
                yield break;
            }
            if (fastSim) yield return null;

            // Paso 3: traspasos AI, fichajes estrella y psicólogo (hilo de fondo)
            var aiTask = DatabaseManager.Instance.RunInBackgroundAsync(() =>
                ProcessAIMarket(gameDay));

            while (!aiTask.IsCompleted)
                yield return null;

            try
            {
                aiTask.GetAwaiter().GetResult();
            }
            catch (System.Exception ex)
            {
                _pendingRecoveredIds.Clear();
                HandleDayError("Error en el lote pre-partido", "ERROR AL PROCESAR EL DÍA", "Ha ocurrido un error al procesar el día.", ex, fastSim);
                yield break;
            }
        }

        // El pre-lote de este día queda commiteado: si más adelante se vuelve a simular
        // el MISMO día (pantalla de previa → IR AL PARTIDO) no debe repetirse.
        if (!effectiveSkipPreBatch)
        {
            _preBatchGameDay = gameDay;
            _preBatchSeasonId = _season?.id ?? -1;
        }

        // Si hay jugadores recuperados y estamos en fast-sim, interrumpir
        // para mostrar el modal (mismo patrón que lesionados/vacíos).
        if (fastSim && _pendingRecoveredIds.Count > 0)
        {
            _fastSimAborted = true;
            _fastSimAbortRecovered = true;
            yield break;
        }

        var gamesToday = DatabaseManager.Instance.GetGamesByGameDay(_manager.id, gameDay);

        // Los partidos G-League comparten game_day con la NBA pero nunca
        // disparan modales de convocatoria ni navegación de mi equipo: los ids
        // de filial viven en otro espacio y podrían coincidir numéricamente.
        bool myTeamPlays = gamesToday.Any(g =>
            !IsGLeagueGame(g) && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
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

            // Check for injured players FIRST — most urgent before lineup decisions
            var injured = GetInjuredActiveLineupPlayers();
            if (injured.Count > 0)
            {
                if (fastSim)
                {
                    _fastSimAborted = true;
                    _fastSimAbortInjured = injured;
                    yield break;
                }
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

            // Check for tired players (load management) — skipped on fast‑sim re‑entry
            if (!effectiveSkipPreBatch && PlayerPrefs.GetInt("TF_LoadMgmt_Enabled", 0) == 1)
            {
                _loadMgmtIsB2B = IsMyTeamBackToBack(gameDay);
                _loadMgmtTiredPlayers = GetTiredActivePlayers();
                if (_loadMgmtIsB2B || _loadMgmtTiredPlayers.Count > 0)
                {
                    if (fastSim)
                    {
                        _fastSimAborted = true;
                        yield break;
                    }
                    _loadMgmtModalResolved = false;
                    _loadMgmtModalGoToQuinteto = false;
                    ShowLoadManagementModal(_loadMgmtTiredPlayers, _loadMgmtIsB2B);
                    yield return new WaitUntil(() => _loadMgmtModalResolved);
                    _loadMgmtTiredPlayers.Clear();
                    _loadMgmtIsB2B = false;
                    if (_loadMgmtModalGoToQuinteto)
                    {
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        yield break;
                    }
                }
            }

            // Check for empty starter slots (e.g. after a trade)
            var emptySlots = GetEmptyStarterSlots();
            if (emptySlots.Count > 0)
            {
                if (fastSim)
                {
                    _fastSimAborted = true;
                    _fastSimAbortEmptySlots = emptySlots;
                    yield break;
                }
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

            // Check de ROTACIÓN: o todos los minutos están asignados (240) o la rotación
            // está en automático (el simulador reparte). Se comprueba al final porque los
            // auto-rellenos anteriores resetean los minutos a -1 (estado válido).
            var rotStatus = GetRotationStatus();
            if (!rotStatus.IsComplete)
            {
                if (fastSim)
                {
                    _fastSimAborted = true;
                    _fastSimAbortRotation = true;
                    yield break;
                }
                _rotationModalResolved = false;
                _rotationModalGoToRotation = false;
                ShowRotationModal(rotStatus);
                yield return new WaitUntil(() => _rotationModalResolved);
                if (_rotationModalGoToRotation)
                {
                    HideLoading();
                    ScreenManager.Instance.GoTo(GameScreen.Rotation);
                    yield break;
                }
            }
        }

        // ── PreGame: navegar ANTES de simular ──
        if (navigateToPreGame && myTeamPlays)
        {
            Refresh();
            GameSaveManager.UpdateSlotFromDatabase(DatabaseManager.Instance.ActiveSaveSlot);
            HideLoading();
            Debug.Log($"[Dashboard] Día {gameDay} — pre-lote listo → PreGame (sin simular)");
            ScreenManager.Instance.GoTo(GameScreen.PreGame);
            yield break;
        }

        db.BeginTransaction();
        try
        {
            // SIM-08: limpieza INCONDICIONAL. En un día sin partidos previos que crea
            // clasificatorios, `gamesToday` se leyó vacío y quedaban resultados del día
            // anterior mezclados en el cache.
            GameResultCache.Clear();
            if (gamesToday.Count > 0)
                GameResultCache.LastGameDay = gameDay;
        }
        catch (System.Exception ex)
        {
            db.Rollback();
            GameResultCache.Clear();
            HandleDayError("Error simulando el día", "ERROR AL SIMULAR EL DÍA", "Ha ocurrido un error al simular el día.", ex, fastSim);
            yield break;
        }

        foreach (var game in gamesToday)
        {
            try
            {
                ProcessSingleGame(game);
            }
            catch (System.Exception ex)
            {
                db.Rollback();
                GameResultCache.Clear();
                HandleDayError("Error simulando el día", "ERROR AL SIMULAR EL DÍA", "Ha ocurrido un error al simular el día.", ex, fastSim);
                yield break;
            }
        }

        // Recalculate team chemistry for all teams involved today
        foreach (var game in gamesToday)
        {
            try
            {
                int chem = DatabaseManager.Instance.CalculateTeamChemistry(game.home_team_id, gameDay);
                DatabaseManager.Instance.UpdateTeamChemistry(game.home_team_id, chem);
                chem = DatabaseManager.Instance.CalculateTeamChemistry(game.away_team_id, gameDay);
                DatabaseManager.Instance.UpdateTeamChemistry(game.away_team_id, chem);
            }
            catch (System.Exception ex)
            {
                db.Rollback();
                GameResultCache.Clear();
                HandleDayError("Error simulando el día", "ERROR AL SIMULAR EL DÍA", "Ha ocurrido un error al simular el día.", ex, fastSim);
                yield break;
            }
        }

        try
        {
            var nbaGamesToday = gamesToday.Where(g => !IsGLeagueGame(g)).ToList();
            if (nbaGamesToday.Count > 0)
            {
                QuickNewsGenerator.Generate(_manager, _myTeam, _season, nbaGamesToday, gameDay, _season?.current_date ?? "");

                // Hitos de legado (E1): antes de los hilos, para que la narrativa los vea.
                MilestoneService.EvaluateDay(_manager, _myTeam, _season, gameDay, _season?.current_date ?? "");

                // Motor de narrativa: abre/escala/cierra hilos (rachas) con noticias y efectos.
                StorylineService.ProcessDay(_manager, _myTeam, _season, gameDay, _season?.current_date ?? "");

                AchievementService.EvaluateGameDay(_manager.id, _myTeam.id, _season.id);
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
                        var elimResult = GameSimulator.SimulateGame(elimGame, homeP, awayP, elimHomeChem, elimAwayChem,
                            homePlan: PlanFor(elimGame.home_team_id), awayPlan: PlanFor(elimGame.away_team_id),
                            homeCoachBonus: CoachBonusFor(elimGame.home_team_id), awayCoachBonus: CoachBonusFor(elimGame.away_team_id));
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

            // Check user's play-in elimination BEFORE allPlayInPlayed transition
            if (_myTeam != null)
            {
                bool hasPlayedPlayIn = DatabaseManager.Instance.Db.Table<GameData>()
                    .Any(g => g.manager_id == _manager.id
                           && g.game_type == "playin"
                           && g.is_played == 1
                           && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
                bool hasRemainingPlayIn = DatabaseManager.Instance.Db.Table<GameData>()
                    .Any(g => g.manager_id == _manager.id
                           && g.game_type == "playin"
                           && g.is_played == 0
                           && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));

                if (hasPlayedPlayIn && !hasRemainingPlayIn)
                {
                    var myPlayInGames = DatabaseManager.Instance.Db.Table<GameData>()
                        .Where(g => g.manager_id == _manager.id
                               && g.game_type == "playin"
                               && g.is_played == 1
                               && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id))
                        .OrderByDescending(g => g.game_day)
                        .ToList();

                    if (myPlayInGames.Count > 0)
                    {
                        var lastGame = myPlayInGames[0];
                        bool iWonLast = (lastGame.home_team_id == _myTeam.id && lastGame.home_score > lastGame.away_score)
                                      || (lastGame.away_team_id == _myTeam.id && lastGame.away_score > lastGame.home_score);

                        if (!iWonLast)
                        {
                            bool playInAlreadySent = DatabaseManager.Instance.Db.Table<MessageData>()
                                .Any(m => m.manager_id == _manager.id && m.news_type == NewsType.ELIMINACION_PLAYIN);
                            if (!playInAlreadySent)
                            {
                                DatabaseManager.Instance.AddMessage(new MessageData
                                {
                                    manager_id = _manager.id,
                                    sender_type = 1,
                                    sender_id = 0,
                                    title = "Eliminación en Play-In",
                                    body = NewsTexts.EliminacionPlayin(_myTeam.name, 0),
                                    game_day = gameDay,
                                    game_date = _season?.current_date ?? "",
                                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                    is_read = 0,
                                    category = NewsCategory.LIGA,
                                    news_type = NewsType.ELIMINACION_PLAYIN
                                });
                                Debug.Log($"[Dashboard] {_myTeam.name} eliminado de Play-In.");
                            }
                        }
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
            else if (_myTeam != null)
            {
                bool hasPlayedPlayoffs = DatabaseManager.Instance.Db.Table<GameData>()
                    .Any(g => g.manager_id == _manager.id
                           && g.game_type == "playoff"
                           && g.is_played == 1
                           && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
                bool hasRemainingPlayoffs = DatabaseManager.Instance.Db.Table<GameData>()
                    .Any(g => g.manager_id == _manager.id
                           && g.game_type == "playoff"
                           && g.is_played == 0
                           && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));

                bool teamWonLastSeries = false;
                string lastSeriesLabel = "";
                if (hasPlayedPlayoffs && !hasRemainingPlayoffs)
                {
                    lastSeriesLabel = DatabaseManager.Instance.Db.Table<GameData>()
                        .Where(g => g.manager_id == _manager.id
                               && g.game_type == "playoff"
                               && g.is_played == 1
                               && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id))
                        .OrderByDescending(g => g.game_day)
                        .Select(g => g.series_label)
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(lastSeriesLabel))
                    {
                        int winner = PlayoffsGenerator.GetSeriesWinner(_season, _manager.id, lastSeriesLabel);
                        teamWonLastSeries = (winner == _myTeam.id);
                    }
                }

                if (hasPlayedPlayoffs && !hasRemainingPlayoffs && !teamWonLastSeries)
                {
                    bool playoffAlreadySent = DatabaseManager.Instance.Db.Table<MessageData>()
                        .Any(m => m.manager_id == _manager.id && m.news_type == NewsType.ELIMINACION_PLAYOFFS);
                    if (!playoffAlreadySent)
                    {
                        string rivalName = "";
                        int round = 0;
                        int rivalWins = 0;
                        int teamWins = 0;

                        if (!string.IsNullOrEmpty(lastSeriesLabel))
                        {
                            var seriesGames = DatabaseManager.Instance.Db.Table<GameData>()
                                .Where(g => g.manager_id == _manager.id
                                       && g.series_label == lastSeriesLabel
                                       && g.is_played == 1)
                                .ToList();

                            if (seriesGames.Count > 0)
                            {
                                int rivalId = seriesGames[0].home_team_id == _myTeam.id
                                    ? seriesGames[0].away_team_id
                                    : seriesGames[0].home_team_id;
                                rivalName = _allTeams?.Find(t => t.id == rivalId)?.name ?? "";

                                foreach (var sg in seriesGames)
                                {
                                    bool iLost = (sg.home_team_id == _myTeam.id && sg.home_score < sg.away_score)
                                              || (sg.away_team_id == _myTeam.id && sg.away_score < sg.home_score);
                                    if (iLost) rivalWins++;
                                    else teamWins++;
                                }
                            }

                            if (lastSeriesLabel.Contains("-r1-")) round = 1;
                            else if (lastSeriesLabel.Contains("-r2-")) round = 2;
                            else if (lastSeriesLabel.Contains("-r3-")) round = 3;
                            else round = 4;
                        }

                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = "Eliminación en Playoffs",
                            body = NewsTexts.EliminacionPlayoffs(_myTeam.name, rivalName, round, rivalWins, teamWins),
                            game_day = gameDay,
                            game_date = _season?.current_date ?? "",
                            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            is_read = 0,
                            category = NewsCategory.LIGA,
                            news_type = NewsType.ELIMINACION_PLAYOFFS
                        });
                        Debug.Log($"[Dashboard] {_myTeam.name} eliminado de Playoffs por {rivalName} en ronda {round}.");
                    }
                }
            }
        }

        // ── G-League postseason (competición paralela; no toca seasons.phase) ──
        try
        {
            GLeaguePostSeason.AdvanceIfNeeded(_manager.id, _season, _myTeam?.id ?? 0);
        }
        catch (System.Exception exGl)
        {
            Debug.LogError($"[Dashboard] Error en postseason G-League: {exGl.Message}\n{exGl.StackTrace}");
        }

        UpdateManagerStats(gameDay);

        ProcessMonthlyPayroll(gameDay);
        ProcessSubscriptionRevenue(gameDay);

        if (_season.phase == "finished")
        {
            DatabaseManager.Instance.UpdateHistoricalPlayerStatsFromSeason(_season.id, _manager.id);
            DatabaseManager.Instance.SaveSeasonEndRecords(_season.id, _manager.id);
            AssignSeasonPoints();
            AchievementService.EvaluateSeasonEnd(_manager.id, _myTeam.id, _season.id);
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
                    sender_type = 2,
                    sender_id = 0,
                    title = "Última semana de traspasos",
                    body = NewsTexts.DeadlineSemana(),
                    game_day = _season.current_game_day,
                    game_date = _season.current_date,
                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    is_read = 0,
                    category = NewsCategory.TRASPASOS,
                    news_type = NewsType.DEADLINE_SEMANA
                });
                _showDeadlineWeekModal = true;
            }

            if (newDate.Month == 2 && newDate.Day == 12 && newDate.Year == _season.year_end)
            {
                DatabaseManager.Instance.AddMessage(new MessageData
                {
                    manager_id = _manager.id,
                    sender_type = 0,
                    sender_id = 0,
                    title = "Fecha límite de renovaciones",
                    body = "Hoy cierra el plazo para renovar contratos. A partir de mañana no podrás ofrecer renovaciones a tus jugadores hasta la próxima temporada.",
                    game_day = _season.current_game_day,
                    game_date = _season.current_date,
                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    is_read = 0,
                    category = NewsCategory.CLUB,
                    news_type = NewsType.RENOVACION
                });
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

            // El día queda cerrado: el siguiente día necesita su propio pre-lote.
            _preBatchGameDay = int.MinValue;
            _preBatchSeasonId = -1;
        }
        catch (System.Exception ex)
        {
            db.Rollback();
            _showDeadlineWeekModal = false;
            GameResultCache.Clear();
            Debug.LogError($"[GameDay] Error simulando el día, revertido: {ex.Message}\n{ex.StackTrace}");
            if (fastSim)
            {
                _fastSimAborted = true;
                yield break;
            }
            HideLoading();
            ShowOfferResultModal("ERROR AL SIMULAR EL DÍA", "Ha ocurrido un error al simular el día.\n\n" + ex.Message, -1);
            yield break;
        }
        finally
        {
            // Red de seguridad: si la corrutina se aborta a mitad (StopAllCoroutines al
            // salir de la pantalla) Unity ejecuta este finally. Sin él, la transacción
            // quedaría abierta y bloquearía todas las escrituras de la sesión.
            if (db.IsInTransaction)
            {
                try { db.Rollback(); } catch { }
                GameResultCache.Clear();
                Debug.LogWarning("[GameDay] Día abortado a mitad de transacción: revertido.");
            }
        }

        _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        _teamRecordCache = null;

        if (_showDeadlineWeekModal)
        {
            _showDeadlineWeekModal = false;
            ShowTradeDeadlineModal(fastSim);
            yield return new WaitUntil(() => _firedOverlay.style.display == DisplayStyle.None);
        }

        if (fastSim)
        {
            // En modo simulación rápida no se navega ni se refresca por día:
            // el refresh se hace al final. Si hay ofertas maduradas o propuestas
            // de traspaso pendientes, se aborta el día para mostrarlas con la
            // simulación parada (el día ya está commiteado).
            var matured = DatabaseManager.Instance.GetMaturedUnprocessedOffers(_manager.id, _season.current_game_day);
            var pendingTrades = DatabaseManager.Instance.GetPendingTradeOffers(_manager.id);
            if (matured.Count > 0 || pendingTrades.Count > 0)
            {
                _fastSimAborted = true;
                _fastSimAbortOffers = true;
                yield break;
            }
            yield break;
        }

        Refresh();

        GameSaveManager.UpdateSlotFromDatabase(DatabaseManager.Instance.ActiveSaveSlot);

        HideLoading();

        if (gamesToday.Count == 0)
        {
            ShowPendingRecoveryModal();
            ShowPendingIRReleaseModal();
            ProcessMaturedOffers();
            Debug.Log($"[Dashboard] Día {gameDay} — sin partidos, continúa en Dashboard");
        }
        else if (myTeamPlays || hasAllStar)
        {
            if (hasAllStar)
            {
                Debug.Log($"[Dashboard] Día {gameDay} — All-Star Game → MatchDay");
                ScreenManager.Instance.GoTo(GameScreen.MatchDay);
            }
            else if (navigateToPreGame)
            {
                Debug.Log($"[Dashboard] Día {gameDay} — mi equipo juega → PreGame");
                ScreenManager.Instance.GoTo(GameScreen.PreGame);
            }
            else
            {
                Debug.Log($"[Dashboard] Día {gameDay} — mi equipo juega → MatchDay");
                ScreenManager.Instance.GoTo(GameScreen.MatchDay);
            }
        }
        else
        {
            Debug.Log($"[Dashboard] Día {gameDay} — {gamesToday.Count} partidos → GameResults");
            ScreenManager.Instance.GoTo(GameScreen.GameResults);
        }
    }

    void ProcessSingleGame(GameData game)
    {
        // ── Partidos G-League: pipeline propio, sin tocar estado NBA ──
        if (IsGLeagueGame(game))
        {
            ProcessGLeagueGame(game);
            return;
        }

        List<PlayerData> homePlayers, awayPlayers;

        if (game.game_type == "allstar")
        {
            homePlayers = BuildAllStarRoster("East");
            awayPlayers = BuildAllStarRoster("West");
            // Exhibición: reparto explícito de los 240 minutos entre los convocados para
            // que jueguen todos (con la heurística por rol solo entraban 6-7). Ignora la
            // rotación de los equipos reales.
            AssignAllStarMinutes(homePlayers);
            AssignAllStarMinutes(awayPlayers);
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

        var result = GameSimulator.SimulateGame(game, homePlayers, awayPlayers, homeChem, awayChem,
            homePlan: PlanFor(game.home_team_id), awayPlan: PlanFor(game.away_team_id),
            homeCoachBonus: CoachBonusFor(game.home_team_id), awayCoachBonus: CoachBonusFor(game.away_team_id),
            capturePlayByPlay: game.game_type != "allstar" && UIScreenController.GetSimMode() == 1);
        DatabaseManager.Instance.UpdateGame(game);
        GameResultCache.SimulatedGameIds.Add(game.id);

        // Plantilla corta (F2-06): el partido se simuló con los disponibles o se resolvió
        // por incomparecencia. Si afecta al equipo del jugador, se le avisa.
        if (result.shortRoster && game.game_type != "allstar"
            && (game.home_team_id == _myTeam.id || game.away_team_id == _myTeam.id))
            NotifyShortRoster(result, game);

        if (UIScreenController.GetSimMode() == 1 && result.playByPlay != null)
            GameResultCache.PlayByPlayLogs[game.id] = result.playByPlay;

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

            // Convocatoria (sustituye a las stats del partido, que ya no se persisten).
            DatabaseManager.Instance.SaveAllStarRoster(_manager.id, _season.id, homePlayers, awayPlayers);

            bool alreadySent = DatabaseManager.Instance.Db.Table<MessageData>()
                .Any(m => m.manager_id == _manager.id && m.news_type == NewsType.ALLSTAR
                       && m.game_day == game.game_day);
            if (!alreadySent)
            {
                bool eastWon = game.home_score > game.away_score;
                int maxScore = Mathf.Max(game.home_score, game.away_score);
                int minScore = Mathf.Min(game.home_score, game.away_score);
                string winnerConf = eastWon ? "Conferencia Este" : "Conferencia Oeste";
                string loserConf = eastWon ? "Conferencia Oeste" : "Conferencia Este";
                DatabaseManager.Instance.AddMessage(new MessageData
                {
                    manager_id = _manager.id,
                    sender_type = 2,
                    sender_id = 0,
                    title = $"All-Star: {winnerConf} {maxScore}-{minScore} {loserConf}",
                    body = NewsTexts.AllStar(_season.year_end, eastWon, game.home_score,
                        game.away_score, mvpStats.name, mvpStats.points),
                    game_day = game.game_day,
                    game_date = game.game_date,
                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    is_read = 0,
                    category = NewsCategory.LIGA,
                    news_type = NewsType.ALLSTAR
                });
            }
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

    // ── AVISO DE PLANTILLA CORTA (F2-06) ─────────────────────

    // El motor ya no inventa el marcador cuando faltan jugadores: simula con los
    // disponibles (1–4) o resuelve una incomparecencia (0–20). El jugador recibe un
    // mensaje explicando lo que ha pasado con su equipo.
    void NotifyShortRoster(GameSimulator.GameResult result, GameData game)
    {
        bool isHome = game.home_team_id == _myTeam.id;
        int mine = isHome ? result.home_stats.Count : result.away_stats.Count;
        int rivalCount = isHome ? result.away_stats.Count : result.home_stats.Count;
        int rivalId = isHome ? game.away_team_id : game.home_team_id;
        string rival = DatabaseManager.Instance.GetTeamById(rivalId)?.name ?? "el rival";

        bool iForfeited = result.walkover && (isHome ? result.home_score == 0 : result.away_score == 0);
        bool rivalForfeited = result.walkover && (isHome ? result.away_score == 0 : result.home_score == 0);

        string title, body;
        if (iForfeited)
        {
            title = "Incomparecencia";
            body = $"No tenías ningún jugador sano disponible: el partido ante {rival} se pierde 0–20 por incomparecencia. Revisa la enfermería y la plantilla cuanto antes.";
        }
        else if (rivalForfeited)
        {
            title = "Incomparecencia del rival";
            body = $"{rival} no pudo presentar a ningún jugador sano: el partido se gana 20–0 por incomparecencia.";
        }
        else if (mine < 5)
        {
            title = "Plantilla incompleta";
            body = $"Has jugado ante {rival} con solo {mine} jugadores sanos. El resultado y el box score se han simulado con los disponibles; conviene reforzar la plantilla.";
        }
        else
        {
            title = "Plantilla incompleta del rival";
            body = $"{rival} ha jugado ante ti con solo {rivalCount} jugadores sanos. El resultado y el box score se han simulado con los disponibles.";
        }

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 0,
            sender_id = 0,
            title = title,
            body = body,
            game_day = _season.current_game_day,
            game_date = _season.current_date,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = NewsType.PLANTILLA_CORTA
        });
    }

    // ── G-LEAGUE: SIMULACIÓN DE PARTIDOS DE LA FILIAL ──────

    public static bool IsGLeagueGame(GameData g)
    {
        return g.game_type == GLeagueScheduleGenerator.TYPE_REGULAR
            || g.game_type == GLeaguePostSeason.TYPE_PLAYOFF;
    }

    /// <summary>
    /// Simula un partido G-League entre dos filiales (prospectos + asignados).
    /// No persiste en player_game_stats ni aplica lesiones/fatiga/records/moral:
    /// solo actualiza el GameData y acumula gleague_season_stats.
    /// </summary>
    void ProcessGLeagueGame(GameData game)
    {
        // Los partidos GL guardan los ids de filial codificados (+GAME_TEAM_ID_OFFSET)
        var homeTeam = DatabaseManager.Instance.GetGLeagueTeam(GLeagueHelper.DecodeGlTeamId(game.home_team_id));
        var awayTeam = DatabaseManager.Instance.GetGLeagueTeam(GLeagueHelper.DecodeGlTeamId(game.away_team_id));
        if (homeTeam == null || awayTeam == null)
        {
            Debug.LogError($"[Dashboard] Partido G-League {game.id} con filiales inexistentes ({game.home_team_id}/{game.away_team_id})");
            game.is_played = 1;
            DatabaseManager.Instance.UpdateGame(game);
            return;
        }

        var prospects = DatabaseManager.Instance.GetAllGLeaguePlayers();
        var assignedByNba = DatabaseManager.Instance.GetGLeagueAssignedByTeam();

        var homePlayers = GLeagueHelper.BuildAffiliateLineup(homeTeam, prospects, assignedByNba);
        var awayPlayers = GLeagueHelper.BuildAffiliateLineup(awayTeam, prospects, assignedByNba);

        var myAffiliate = _myTeam != null ? DatabaseManager.Instance.GetGLeagueTeamByNbaTeam(_myTeam.id) : null;
        bool isMyHomeGame = myAffiliate != null && homeTeam.id == myAffiliate.id;

        var result = GameSimulator.SimulateGame(game, homePlayers, awayPlayers, 50, 50, persistToDb:false, glLeague:true);
        DatabaseManager.Instance.UpdateGame(game);

        // Solo acumular stats de liga regular en gleague_season_stats;
        // los playoffs no se incluyen en las estadísticas de la pestaña.
        if (game.game_type == GLeagueScheduleGenerator.TYPE_REGULAR)
        {
            int seasonId = _season?.id ?? 0;
            foreach (var ps in result.home_stats)
            {
                if (ps.minutes <= 0) continue;
                var (pts, reb, ast, stl, blk, tov) =
                    GLeagueHelper.ClampLine(ps.points, ps.oreb + ps.dreb, ps.assists, ps.steals, ps.blocks, ps.turnovers);
                int missedFg = Mathf.Clamp(ps.fga - ps.fgm, 0, 30);
                int missedFt = Mathf.Clamp(ps.fta - ps.ftm, 0, 10);
                int pf = Mathf.Clamp(ps.pf, 0, 6);
                int clampedRating = pts + reb + ast + stl + blk - missedFg - missedFt - tov - pf;
                DatabaseManager.Instance.AddGLeagueGameStat(ps.player_id, seasonId,
                    pts, reb, ast, stl, blk, tov, clampedRating, homeTeam.id);
            }
            foreach (var ps in result.away_stats)
            {
                if (ps.minutes <= 0) continue;
                var (pts, reb, ast, stl, blk, tov) =
                    GLeagueHelper.ClampLine(ps.points, ps.oreb + ps.dreb, ps.assists, ps.steals, ps.blocks, ps.turnovers);
                int missedFg = Mathf.Clamp(ps.fga - ps.fgm, 0, 30);
                int missedFt = Mathf.Clamp(ps.fta - ps.ftm, 0, 10);
                int pf = Mathf.Clamp(ps.pf, 0, 6);
                int clampedRating = pts + reb + ast + stl + blk - missedFg - missedFt - tov - pf;
                DatabaseManager.Instance.AddGLeagueGameStat(ps.player_id, seasonId,
                    pts, reb, ast, stl, blk, tov, clampedRating, awayTeam.id);
            }
        }
    }

    void HandleDayError(string step, string modalTitle, string modalBody, System.Exception ex, bool fastSim)
    {
        Debug.LogError($"[GameDay] {step}, revertido: {ex.Message}\n{ex.StackTrace}");
        if (fastSim)
        {
            _fastSimAborted = true;
            // Guardar el motivo: al terminar el bucle se muestra al jugador (antes
            // el error solo quedaba en la consola y la simulación se detenía en silencio).
            _fastSimErrorMessage = $"{step}: {ex.Message}";
            _fastSimAbortEmptySlots.Clear();
            _fastSimAbortInjured.Clear();
        }
        else
        {
            HideLoading();
            ShowOfferResultModal(modalTitle, modalBody + "\n\n" + ex.Message, -1);
        }
    }

    // ── SIMULACIÓN RÁPIDA ─────────────────────────────────

    System.Collections.IEnumerator FastSimRoutine(System.DateTime target)
    {
        if (_season == null)
        {
            HideLoading();
            yield break;
        }

        _fastSimAborted = false;
        _fastSimErrorMessage = null;
        _fastSimSkipPreBatch = false;
        _fastSimAutoFixAttempts = 0;
        _fastSimAbortEmptySlots = new List<int>();
        _fastSimAbortInjured = new List<(LineupData, PlayerData)>();
        _fastSimAbortOffers = false;
        _fastSimAbortRecovered = false;
        _fastSimOfferPauseActive = false;
        _offerModalGoToQuinteto = false;
        _tradeOfferModalResolved = true;
        _tradeOfferAccepted = false;
        _tradeOfferGoToQuinteto = false;

        ShowLoading();

        // Durante la simulación el botón de acción permite detenerla.
        _fastSimRunning = true;
        _fastSimStopRequested = false;
        SetActionBtn("DETENER SIMULACIÓN", "");
        _btnAction.SetEnabled(true);
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(_btnAction);
        SetSidebarNavigationEnabled(false);

        _fastSimStartDate = _season?.current_date;

        while (_season.phase != "finished")
        {
            if (_fastSimStopRequested) break;

            string curDate = _season.current_date;
            if (string.IsNullOrEmpty(curDate)
                || !System.DateTime.TryParse(curDate, out System.DateTime cur)
                || cur > target)
                break;

            if (FindNextGameDay() == 0)
                break;

            if (IsFeb7OfYearEnd() && !_deadlineDayModalShown)
            {
                _deadlineDayModalShown = true;
                _deadlineDayActive = true;
                ShowDeadlineDayModal(true);
                yield return new WaitUntil(() => _firedOverlay.style.display == DisplayStyle.None);
                _deadlineDayActive = false;
            }

            yield return ProcessGameDayRoutine(fastSim: true);

            if (IsFeb12OfYearEnd() && !_renewalDeadlineModalShown)
            {
                _renewalDeadlineModalShown = true;
                _renewalDeadlineResolved = false;
                ShowRenewalDeadlineModal(true);
                yield return new WaitUntil(() => _renewalDeadlineResolved);
            }

            if (_pendingIRReleaseCount > 0)
            {
                _irReleaseModalResolved = false;
                ShowPendingIRReleaseModal();
                yield return new WaitUntil(() => _irReleaseModalResolved);
                if (_pendingIRReleaseCount > 0)
                {
                    _fastSimAborted = true;
                    break;
                }
            }

            if (_fastSimAborted)
            {
                bool autoFixed = false;
                if (_fastSimAbortInjured.Count > 0)
                {
                    _injuredModalResolved = false;
                    _injuredModalGoToQuinteto = false;
                    ShowInjuredLineupModal(_fastSimAbortInjured);
                    yield return new WaitUntil(() => _injuredModalResolved);
                    if (_injuredModalGoToQuinteto)
                    {
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        yield break;
                    }
                    autoFixed = true;
                }
                else if (_fastSimAbortRecovered)
                {
                    _recoveryModalResolved = false;
                    _recoveryModalGoToQuinteto = false;
                    ShowPendingRecoveryModal();
                    yield return new WaitUntil(() => _recoveryModalResolved);
                    if (_recoveryModalGoToQuinteto)
                    {
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        yield break;
                    }
                    _fastSimAborted = false;
                    _fastSimAbortRecovered = false;
                    continue;
                }
                else if (_fastSimAbortEmptySlots.Count > 0)
                {
                    _emptyLineupModalResolved = false;
                    _emptyLineupGoToQuinteto = false;
                    ShowEmptyLineupModal(_fastSimAbortEmptySlots);
                    yield return new WaitUntil(() => _emptyLineupModalResolved);
                    if (_emptyLineupGoToQuinteto)
                    {
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        yield break;
                    }
                    autoFixed = true;
                }
                else if (_fastSimAbortRotation)
                {
                    _rotationModalResolved = false;
                    _rotationModalGoToRotation = false;
                    ShowRotationModal(GetRotationStatus());
                    yield return new WaitUntil(() => _rotationModalResolved);
                    if (_rotationModalGoToRotation)
                    {
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Rotation);
                        yield break;
                    }
                    _fastSimAbortRotation = false;
                    autoFixed = true;
                }
                else if (_loadMgmtIsB2B || _loadMgmtTiredPlayers.Count > 0)
                {
                    _loadMgmtModalResolved = false;
                    _loadMgmtModalGoToQuinteto = false;
                    ShowLoadManagementModal(_loadMgmtTiredPlayers, _loadMgmtIsB2B);
                    yield return new WaitUntil(() => _loadMgmtModalResolved);
                    _loadMgmtTiredPlayers.Clear();
                    _loadMgmtIsB2B = false;
                    if (_loadMgmtModalGoToQuinteto)
                    {
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        yield break;
                    }
                    autoFixed = true;
                }
                else if (_fastSimAbortOffers)
                {
                    _fastSimOfferPauseActive = true;

                    if (DatabaseManager.Instance.GetMaturedUnprocessedOffers(_manager.id, _season.current_game_day).Count > 0)
                    {
                        _offerModalResolved = false;
                        _offerModalGoToQuinteto = false;
                        ProcessMaturedOffers();
                        yield return new WaitUntil(() => _offerModalResolved);
                        _firedOverlay.style.display = DisplayStyle.None;
                        if (_offerModalGoToQuinteto)
                        {
                            _fastSimOfferPauseActive = false;
                            _fastSimAbortOffers = false;
                            _fastSimRunning = false;
                            SetSidebarNavigationEnabled(true);
                            HideLoading();
                            ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                            yield break;
                        }
                    }

                    bool tradeAccepted = false;
                    while (DatabaseManager.Instance.GetPendingTradeOffers(_manager.id).Count > 0)
                    {
                        _tradeOfferModalResolved = false;
                        _tradeOfferAccepted = false;
                        _tradeOfferGoToQuinteto = false;
                        ShowNextPendingTradeOffer();
                        // Sin modal no hay nada que esperar: se corta el bucle (antes un
                        // `continue` podía dejarlo girando en vacío).
                        if (_firedOverlay.style.display != DisplayStyle.Flex) break;
                        yield return new WaitUntil(() => _tradeOfferModalResolved);
                        if (_tradeOfferAccepted)
                        {
                            tradeAccepted = true;
                            break;
                        }
                        if (_tradeOfferGoToQuinteto) break;
                    }

                    _fastSimOfferPauseActive = false;
                    _fastSimAbortOffers = false;

                    // Aviso de una oferta que dejó de ser válida: el jugador eligió
                    // IR A QUINTETO para arreglar la plantilla.
                    if (_tradeOfferGoToQuinteto)
                    {
                        _tradeOfferGoToQuinteto = false;
                        _fastSimRunning = false;
                        SetSidebarNavigationEnabled(true);
                        HideLoading();
                        ScreenManager.Instance.GoTo(GameScreen.Quinteto);
                        yield break;
                    }

                    if (tradeAccepted)
                    {
                        _fastSimAborted = true;
                        break;
                    }
                    _fastSimAborted = false;
                    continue;
                }

                _fastSimAbortEmptySlots.Clear();
                _fastSimAbortInjured.Clear();

                if (autoFixed)
                {
                    if (++_fastSimAutoFixAttempts > 3)
                    {
                        Debug.LogWarning("[Dashboard] No se pudo completar la alineación en varios intentos. Se detiene la simulación.");
                        break;
                    }
                    _fastSimAborted = false;
                    _fastSimSkipPreBatch = true;
                    continue;
                }
                break;
            }
            _fastSimAutoFixAttempts = 0;

            // ── Aviso financiero / despido durante FastSim ──
            if (GameResultCache.PendingBudgetWarning)
            {
                _fastSimRunning = false;
                SetSidebarNavigationEnabled(true);
                CheckBudgetWarning(true);
                yield return new WaitUntil(() => _firedOverlay.style.display == DisplayStyle.None);
                if (_season == null) yield break;
                _fastSimRunning = true;
                SetSidebarNavigationEnabled(false);
                SetActionBtn("DETENER SIMULACIÓN", "");
            }

            // ── Despido anticipado durante FastSim ──
            if (_myTeam != null && (_season.phase == "playin" || _season.phase == "playoff"))
            {
                string phaseType = _season.phase == "playin" ? "playin" : "playoff";
                bool teamHasPhaseGames = DatabaseManager.Instance.Db.Table<GameData>()
                    .Any(g => g.manager_id == _manager.id && g.game_type == phaseType
                           && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
                if (!teamHasPhaseGames && ShowFiredModalIfNeeded())
                {
                    _fastSimRunning = false;
                    SetSidebarNavigationEnabled(true);
                    yield return new WaitUntil(() => _firedOverlay.style.display == DisplayStyle.None);
                    yield break;
                }
            }

            // Refrescar cabecera (fecha) y paneles del Dashboard tras cada día
            _myTeam = DatabaseManager.Instance.GetTeamById(_myTeam.id);
            _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        _teamRecordCache = null;
            Refresh();

            // Renderizar un frame por día para que el spinner de la cabecera
            // gire visiblemente durante la simulación rápida.
            yield return null;
        }

        _fastSimRunning = false;
        SetSidebarNavigationEnabled(true);
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _myTeam = DatabaseManager.Instance.GetTeamById(_myTeam.id);
        _allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        _teamRecordCache = null;
        Refresh();
        GameSaveManager.UpdateSlotFromDatabase(DatabaseManager.Instance.ActiveSaveSlot);
        HideLoading();

        if (_fastSimErrorMessage != null)
        {
            // La simulación se detuvo por un error: avisar en vez de salir en silencio.
            ShowOfferResultModal("ERROR EN LA SIMULACIÓN",
                "La simulación se detuvo por un error y el último día no se aplicó.\n\n" + _fastSimErrorMessage, -1);
        }
        else if (!_fastSimAborted && !_fastSimStopRequested)
        {
            // Calcular W/L del período simulado
            string endDate = _season?.current_date ?? _fastSimTarget?.ToString("yyyy-MM-dd") ?? "";
            int wins = 0, losses = 0;
            if (_allGames != null && _myTeam != null)
            {
                var periodGames = _allGames
                    .Where(g => !string.IsNullOrEmpty(g.game_date)
                            && string.Compare(g.game_date, _fastSimStartDate, System.StringComparison.Ordinal) >= 0
                            && string.Compare(g.game_date, endDate, System.StringComparison.Ordinal) <= 0
                            && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id))
                    .ToList();
                wins = periodGames.Count(g =>
                    (g.home_team_id == _myTeam.id && g.home_score > g.away_score)
                    || (g.away_team_id == _myTeam.id && g.away_score > g.home_score));
                losses = periodGames.Count - wins;
            }
            ShowFastSimSummary(_fastSimStartDate, endDate, wins, losses);
        }
    }

    void ShowFastSimSummary(string startDate, string endDate, int wins, int losses)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(42, 95, 201, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(42, 95, 201, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(42, 95, 201, 255));
        box.style.borderRightColor = new StyleColor(new Color32(42, 95, 201, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--blue");
        var title = new Label("SIMULACIÓN COMPLETADA");
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(42, 95, 201, 255));
        header.Add(title);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // Fechas
        var datesRow = new VisualElement();
        datesRow.AddToClassList("fastsim-modal-dates");

        var dateIcon = new VisualElement();
        dateIcon.AddToClassList("fastsim-modal-dates-icon");
        var dateIconTex = Resources.Load<Texture2D>("Icons/calendario");
        if (dateIconTex != null)
            dateIcon.style.backgroundImage = new StyleBackground(dateIconTex);
        datesRow.Add(dateIcon);

        string fmtStart = FormatDateShort(startDate);
        string fmtEnd = FormatDateShort(endDate);
        var dateText = new Label($"{fmtStart}  →  {fmtEnd}");
        dateText.AddToClassList("fastsim-modal-dates-text");
        datesRow.Add(dateText);
        body.Add(datesRow);

        // Stats: W / L
        var statsRow = new VisualElement();
        statsRow.AddToClassList("fastsim-modal-stats");

        var winCol = new VisualElement();
        winCol.AddToClassList("fastsim-modal-stat-col");
        var winNum = new Label(wins.ToString());
        winNum.AddToClassList("fastsim-modal-stat-num");
        winNum.AddToClassList("fastsim-modal-stat-num--win");
        winCol.Add(winNum);
        var winLabel = new Label("GANADOS");
        winLabel.AddToClassList("fastsim-modal-stat-label");
        winCol.Add(winLabel);
        statsRow.Add(winCol);

        var sep = new Label("-");
        sep.AddToClassList("fastsim-modal-stat-sep");
        statsRow.Add(sep);

        var lossCol = new VisualElement();
        lossCol.AddToClassList("fastsim-modal-stat-col");
        var lossNum = new Label(losses.ToString());
        lossNum.AddToClassList("fastsim-modal-stat-num");
        lossNum.AddToClassList("fastsim-modal-stat-num--loss");
        lossCol.Add(lossNum);
        var lossLabel = new Label("PERDIDOS");
        lossLabel.AddToClassList("fastsim-modal-stat-label");
        lossCol.Add(lossLabel);
        statsRow.Add(lossCol);

        body.Add(statsRow);

        // Detail text
        int total = wins + losses;
        string detail = total > 0
            ? $"{total} partidos simulados · {wins} ganados · {losses} perdidos"
            : "Sin partidos jugados en este período";
        var detailLbl = new Label(detail);
        detailLbl.AddToClassList("fastsim-modal-detail");
        body.Add(detailLbl);

        box.Add(body);

        // ── Button ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");
        var btn = new Button();
        btn.text = "CERRAR";
        btn.AddToClassList("btn-modal-confirm");
        btn.AddToClassList("btn-modal-confirm--blue");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(btn);
        box.Add(actions);

        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(btn);
    }

    string FormatDateShort(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "—";
        if (System.DateTime.TryParse(isoDate, out System.DateTime dt))
            return dt.ToString("dd/MM/yyyy");
        return isoDate;
    }

    void ShowLoading()
    {
        if (_isLoading) return;
        _isLoading = true;
        _btnAction.SetEnabled(false);
        _loadingSpinner.style.display = DisplayStyle.Flex;

        // Bloquear navegación (sidebar + config → Menú principal) mientras corre.
        SetSidebarNavigationEnabled(false);
        ScreenManager.NavigationLocked = true;

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
        SetSidebarNavigationEnabled(true);
        ScreenManager.NavigationLocked = false;
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

    /// <summary>
    /// Sincroniza el presupuesto en memoria de _myTeam con el de la copia de DB.
    /// Necesario porque muchos puntos mutan TeamData leído de la DB (no _myTeam):
    /// sin esto la cabecera y CheckBudgetAfterGame trabajan con datos obsoletos
    /// durante una sesión de Dashboard (sobre todo en FastSim, donde no hay OnEnable).
    /// </summary>
    void SyncMyTeamBudget(TeamData team)
    {
        if (team != null && _myTeam != null && team.id == _myTeam.id)
            _myTeam.budget = team.budget;
    }

    void CheckBudgetWarning(bool fastSim = false)
    {
        if (!GameResultCache.PendingBudgetWarning) return;
        GameResultCache.PendingBudgetWarning = false;

        // ProManager: cese por presupuesto más exigente (2 avisos en rojo vs 3).
        int threshold = _manager.game_mode == "promanager" ? 2 : 3;
        if (_manager.budget_red_warnings >= threshold)
            ShowBudgetFiredModal();
        else
            ShowBudgetWarningModal(_manager.budget_red_warnings, fastSim);
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
        bool agentCounterShown = false; // solo una contraoferta por tanda (el resto, al inbox)
        foreach (var offer in offers)
        {
            var player = DatabaseManager.Instance.GetPlayerById(offer.player_id);
            if (player == null)
            {
                DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                continue;
            }

            int gamesPlayed = DatabaseManager.Instance.GetPlayerGamesPlayedInSeason(player.id, _season.id);
            int teamChem = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
            float acceptScore = RosterController.CalculateAcceptScore(player, offer.offer_salary, offer.offer_years, gamesPlayed, teamChem);
            bool accepted = UnityEngine.Random.Range(1, 101) <= acceptScore;

            string playerName = $"{player.first_name} {player.last_name}";
            string salaryText = $"${offer.offer_salary / 1_000_000}M/año";
            string yearsText = FormatContractYears(offer);

            string FormatContractYears(OfferData o)
            {
                bool hasOpt = o.has_team_option == 1 || o.has_player_option == 1;
                if (!hasOpt) return $"{o.offer_years} año{(o.offer_years != 1 ? "s" : "")}";
                string optLabel = o.has_team_option == 1 ? "Team Option" : "Player Option";
                if (o.guaranteed_years == 0)
                    return $"{o.offer_years} año{(o.offer_years != 1 ? "s" : "")} ({optLabel})";
                return $"{o.guaranteed_years} año{(o.guaranteed_years != 1 ? "s" : "")} + {optLabel}";
            }

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
                        body = NewsTexts.FichajeCancelado(playerName, teamName),
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0,
                        category = NewsCategory.CLUB,
                        news_type = NewsType.FICHAJE_CANCELADO
                    });
                    DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                    continue;
                }

                if (accepted)
                {
                    // Verificar límite de plantilla
                    var roster = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
                    int rosterCount = DatabaseManager.Instance.GetRosterCount(_myTeam.id);
                    if (rosterCount + batchSigningsAccepted >= TradeHelper.MAX_ROSTER)
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
                            body = NewsTexts.FichajeRechazado(playerName, _myTeam.name, $"la plantilla está completa ({TradeHelper.MAX_ROSTER} jugadores)"),
                            game_day = _season.current_game_day,
                            game_date = nowStr,
                            created_at = nowStr,
                            date_sent = nowStr,
                            is_read = 0,
                            category = NewsCategory.CLUB,
                            news_type = NewsType.FICHAJE_RECHAZADO
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
                        bool proManagerOnly = _manager.game_mode == "promanager";
                        bool ownRecentFA = DatabaseManager.Instance.IsOwnRecentFA(player, _myTeam.id);
                        if (offer.is_two_way == 1)
                        {
                            // El límite de 2 two-way se valida en la maduración: la UI
                            // solo lo comprueba al abrir la oferta y se podían colar.
                            offerLegal = DatabaseManager.Instance.GetTwoWayCount(_myTeam.id) < TradeHelper.MAX_TWO_WAY
                                      && TradeHelper.IsEligibleForTwoWay(player);
                            illegalReason = "ya tienes 2 contratos two-way (o el jugador no es elegible)";
                        }
                        else if (ownRecentFA)
                        {
                            // FA propio reciente: se mantienen los Bird Rights (la UI ya
                            // ofertaba con este máximo y la maduración los rechazaba).
                            var bd = RosterController.GetMaxOfferBreakdown(player, leagueSettings, totalPayroll, true, proManagerOnly);
                            long birdMax = bd.birdMax > 0 ? bd.birdMax : bd.capSpaceMax;
                            offerLegal = offer.offer_salary <= birdMax;
                            illegalReason = $"supera el máximo con Bird Rights (${birdMax:N0})";
                        }
                        else if (totalPayroll <= leagueSettings.salary_cap)
                        {
                            offerLegal = totalPayroll + offer.offer_salary <= leagueSettings.salary_cap;
                            illegalReason = "sin espacio salarial";
                        }
                        else if (proManagerOnly && totalPayroll <= firstApron)
                        {
                            offerLegal = offer.offer_salary <= tMle;
                            illegalReason = $"supera Mid-Level Exception (Taxpayer) (${tMle:N0}) — MODO PRO sin No-Taxpayer";
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
                            body = NewsTexts.FichajeRechazado(playerName, _myTeam.name, illegalReason),
                            game_day = _season.current_game_day,
                            game_date = nowStr,
                            created_at = nowStr,
                            date_sent = nowStr,
                            is_read = 0,
                            category = NewsCategory.CLUB,
                            news_type = NewsType.FICHAJE_RECHAZADO
                        });
                        DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                        continue;
                    }

                    acceptedCount++;
                    batchSigningsAccepted++;
                    player.team_id = _myTeam.id;
                    player.last_team_id = _myTeam.id;
                    player.salary = offer.offer_salary;
                    player.contract_years = offer.offer_years;
                    player.guaranteed_years = offer.guaranteed_years;
                    player.has_team_option = offer.has_team_option;
                    player.has_player_option = offer.has_player_option;
                    player.is_two_way = offer.is_two_way;
                    player.waived_game_day = -1;
                    player.seasons_with_team = 1;
                    DatabaseManager.Instance.AssignJerseyNumber(player, _myTeam.id);
                    DatabaseManager.Instance.UpdatePlayer(player);

                    AchievementService.EvaluateSignStarFA(_manager.id, _myTeam.id, _season?.id ?? 0, player);

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
                        body = NewsTexts.Fichaje(playerName, _myTeam.name, (int)offer.offer_salary, offer.offer_years),
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0,
                        category = NewsCategory.CLUB,
                        news_type = NewsType.FICHAJE
                    });

                    // Activación del hard cap si se usó NT-MLE (>cap, ≤1er apron).
                    // En ProManager la NT-MLE no existe → el hard cap por NT-MLE no se activa.
                    if (_manager.game_mode != "promanager"
                        && _myTeam.first_apron_hard_capped == 0
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
                            body = NewsTexts.HardCap(playerName, _myTeam.name),
                            game_day = _season.current_game_day,
                            game_date = nowStr,
                            created_at = nowStr,
                            date_sent = nowStr,
                            is_read = 0,
                            category = NewsCategory.TRASPASOS,
                            news_type = NewsType.HARD_CAP
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
                        body = NewsTexts.FichajeRechazado(playerName, _myTeam.name, $"oferta de {salaryText} durante {yearsText}") +
                               " Podrás intentarlo de nuevo dentro de 14 días.",
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0,
                        category = NewsCategory.CLUB,
                        news_type = NewsType.FICHAJE_RECHAZADO
                    });

                    // Contraoferta del agente (si queda margen legal y no es two-way).
                    // El máximo legal se recalcula aquí: las variables de cap del bloque
                    // anterior viven en otro ámbito.
                    if (offer.is_two_way != 1 && !agentCounterShown)
                    {
                        var lsNegFA = DatabaseManager.Instance.GetLeagueSettings();
                        if (lsNegFA != null)
                        {
                            long payrollFA = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id).Sum(p => p.salary);
                            long capFA = lsNegFA.salary_cap;
                            long apron1FA = lsNegFA.apron > 0 ? lsNegFA.apron : TradeHelper.FIRST_APRON;
                            long apron2FA = lsNegFA.repeater_apron > 0 ? lsNegFA.repeater_apron : TradeHelper.SECOND_APRON;
                            long ntMleFA = lsNegFA.mid_level > 0 ? lsNegFA.mid_level : TradeHelper.NT_MLE;
                            long tMleFA = lsNegFA.taxpayer_mid_level > 0 ? lsNegFA.taxpayer_mid_level : TradeHelper.T_MLE;
                            long minFA = lsNegFA.minimum_salary > 0 ? lsNegFA.minimum_salary : TradeHelper.MIN_SALARY;
                            bool proFA = _manager.game_mode == "promanager";

                            long legalMax = payrollFA <= capFA ? Math.Max(0, capFA - payrollFA)
                                          : (proFA && payrollFA <= apron1FA) ? tMleFA
                                          : payrollFA <= apron1FA ? ntMleFA
                                          : payrollFA <= apron2FA ? tMleFA
                                          : minFA;

                            long counter = NegotiationHelper.CounterSalary(offer.offer_salary, (int)acceptScore, legalMax);
                            if (counter > offer.offer_salary)
                            {
                                agentCounterShown = true;
                                ShowAgentCounterModal(player, counter, offer.offer_years, isRenewal: false);
                            }
                        }
                    }
                }
            }
            else
            {
                // RENEWAL (offer_type == 0)
                hasRenewal = true;
                if (accepted)
                {
                    acceptedCount++;
                    player.salary = offer.is_two_way == 1 ? TradeHelper.TWO_WAY_SALARY : offer.offer_salary;
                    player.contract_years = offer.offer_years;
                    player.guaranteed_years = offer.guaranteed_years;
                    player.has_team_option = offer.has_team_option;
                    player.has_player_option = offer.has_player_option;
                    player.is_two_way = offer.is_two_way;
                    // Re-firma de un FA propio reciente (declinó su opción): devolverlo al equipo
                    if (player.team_id == 0 && DatabaseManager.Instance.IsOwnRecentFA(player, _myTeam.id))
                    {
                        player.team_id = _myTeam.id;
                        player.last_team_id = _myTeam.id;
                        DatabaseManager.Instance.AssignJerseyNumber(player, _myTeam.id);
                    }
                    // Si como FA reciente firmó con otro equipo mientras maduraba, cancelar
                    else if (player.team_id != 0 && player.team_id != _myTeam.id)
                    {
                        rejectedCount++;
                        player.renewal_cooldown_day = _season.current_game_day + 15;
                        DatabaseManager.Instance.UpdatePlayer(player);
                        resultSummary += $"✗ {playerName}: RE-FIRMA CANCELADA — el jugador fichó por {DatabaseManager.Instance.GetTeamById(player.team_id)?.name ?? "otro equipo"}.\n";
                        DatabaseManager.Instance.AddMessage(new MessageData
                        {
                            manager_id = _manager.id,
                            sender_type = 1,
                            sender_id = 0,
                            title = $"Re-firma cancelada: {playerName}",
                            body = NewsTexts.RefirmaCancelada(playerName, _myTeam.name),
                            game_day = _season.current_game_day,
                            game_date = nowStr,
                            created_at = nowStr,
                            date_sent = nowStr,
                            is_read = 0,
                            category = NewsCategory.CLUB,
                            news_type = NewsType.REFIRMA_CANCELADA
                        });
                        DatabaseManager.Instance.MarkOfferProcessed(offer.id);
                        continue;
                    }
                    player.renewal_cooldown_day = _season.current_game_day + 365;
                    DatabaseManager.Instance.UpdatePlayer(player);

                    string reSignTag = player.team_id == _myTeam.id && player.last_team_id == _myTeam.id ? " RE-FIRMADO" : "";
                    resultSummary += $"✓ {playerName}: CONTRATO RENOVADO{reSignTag} — {salaryText} · {yearsText}\n";

                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Contrato renovado: {playerName}",
                        body = NewsTexts.Renovacion(playerName, _myTeam.name, (int)offer.offer_salary, offer.offer_years),
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0,
                        category = NewsCategory.CLUB,
                        news_type = NewsType.RENOVACION
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
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Oferta rechazada: {playerName}",
                        body = NewsTexts.OfertaRechazada(playerName, _myTeam.name),
                        game_day = _season.current_game_day,
                        game_date = nowStr,
                        created_at = nowStr,
                        date_sent = nowStr,
                        is_read = 0,
                        category = NewsCategory.CLUB,
                        news_type = NewsType.OFERTA_RECHAZADA
                    });

                    // Contraoferta del agente (la re-firma propia puede pasar del cap,
                    // así que el techo es el valor de mercado del jugador).
                    if (offer.is_two_way != 1 && !agentCounterShown)
                    {
                        var lsNeg = DatabaseManager.Instance.GetLeagueSettings();
                        long capNeg = lsNeg != null ? lsNeg.salary_cap : TradeHelper.SALARY_CAP;
                        long market = DatabaseManager.Instance.EstimateMarketSalary(player.GetCalculatedAverage(), capNeg);
                        long legalMax = Math.Max(offer.offer_salary, market);
                        long counter = NegotiationHelper.CounterSalary(offer.offer_salary, (int)acceptScore, legalMax);
                        if (counter > offer.offer_salary)
                        {
                            agentCounterShown = true;
                            ShowAgentCounterModal(player, counter, offer.offer_years, isRenewal: true);
                        }
                    }
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

    // ── NEGOCIACIÓN CON EL AGENTE ───────────────────────────────────
    /// <summary>Contraoferta del agente tras un rechazo por dinero: aceptarla ficha
    /// (o renueva) con las nuevas condiciones; retirarse deja al jugador como estaba.</summary>
    void ShowAgentCounterModal(PlayerData player, long counterSalary, int years, bool isRenewal)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.width = 760;
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label("EL AGENTE PIDE MÁS");
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        var text = new Label();
        text.AddToClassList("modal-text");
        text.text = $"{player.first_name} {player.last_name} rechazó tu oferta, pero su agente cerraría por " +
                    $"${counterSalary:N0} durante {years} año{(years != 1 ? "s" : "")}.\n\n¿Aceptas las nuevas condiciones?";
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.marginTop = 18;

        var accept = new Button { text = "ACEPTAR" };
        accept.AddToClassList("btn-modal-confirm");
        accept.RegisterCallback<ClickEvent>(_ => { PlayClick(); AcceptAgentCounter(player, counterSalary, years, isRenewal); });
        CursorManager.Instance?.RegisterHandCursor(accept);
        actions.Add(accept);

        var reject = new Button { text = "RETIRARSE" };
        reject.AddToClassList("btn-modal-confirm");
        reject.AddToClassList("btn-modal-confirm--danger");
        reject.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
            Refresh();
        });
        CursorManager.Instance?.RegisterHandCursor(reject);
        actions.Add(reject);

        box.Add(actions);
    }

    void AcceptAgentCounter(PlayerData player, long counterSalary, int years, bool isRenewal)
    {
        _firedOverlay.style.display = DisplayStyle.None;
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string playerName = $"{player.first_name} {player.last_name}";

        if (!isRenewal && DatabaseManager.Instance.GetRosterCount(_myTeam.id) >= TradeHelper.MAX_ROSTER)
        {
            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 0,
                sender_id = 0,
                title = "Plantilla completa",
                body = $"No se pudo fichar a {playerName}: la plantilla está al máximo ({TradeHelper.MAX_ROSTER}).",
                game_day = _season.current_game_day,
                game_date = _season.current_date,
                created_at = now,
                date_sent = now,
                is_read = 0,
                category = NewsCategory.CLUB,
                news_type = NewsType.FICHAJE_RECHAZADO
            });
            Refresh();
            return;
        }

        player.salary = counterSalary;
        player.contract_years = years;
        player.guaranteed_years = years;
        player.has_team_option = 0;
        player.has_player_option = 0;

        if (!isRenewal)
        {
            player.is_two_way = 0;
            DatabaseManager.Instance.MovePlayerToTeam(player, _myTeam.id);
            DatabaseManager.Instance.AssignJerseyNumber(player, _myTeam.id);
            DatabaseManager.Instance.UpdatePlayer(player);
            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = _season?.id ?? 0,
                game_day = _season?.current_game_day ?? 0,
                game_date = _season?.current_date ?? now,
                team_id_from = 0,
                team_id_to = _myTeam.id,
                player_id = player.id,
                trade_type = "free_agent"
            });
            AchievementService.EvaluateSignStarFA(_manager.id, _myTeam.id, _season?.id ?? 0, player);
        }
        else
        {
            // Re-firma de un FA propio reciente: devolverlo al equipo.
            if (player.team_id == 0 && DatabaseManager.Instance.IsOwnRecentFA(player, _myTeam.id))
            {
                DatabaseManager.Instance.MovePlayerToTeam(player, _myTeam.id);
                DatabaseManager.Instance.AssignJerseyNumber(player, _myTeam.id);
            }
            DatabaseManager.Instance.UpdatePlayer(player);
        }

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"{(isRenewal ? "Contrato renovado" : "Fichaje")}: {playerName}",
            body = isRenewal
                ? NewsTexts.Renovacion(playerName, _myTeam.name, (int)counterSalary, years)
                : NewsTexts.Fichaje(playerName, _myTeam.name, (int)counterSalary, years),
            game_day = _season.current_game_day,
            game_date = _season.current_date,
            created_at = now,
            date_sent = now,
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = isRenewal ? NewsType.RENOVACION : NewsType.FICHAJE
        });

        Debug.Log($"[Negotiation] Contraoferta aceptada: {playerName} ${counterSalary:N0} x {years}");
        Refresh();
    }

    /// <summary>
    /// Modal de resultado de ofertas/errores. `onFastSimResolved` lo invocan los botones
    /// de la simulación rápida para liberar la espera de la corrutina correspondiente:
    /// sin él, cerrar el modal dejaba la simulación bloqueada para siempre.
    /// </summary>
    void ShowOfferResultModal(string title, string text, int resultType,
                              System.Action<bool> onFastSimResolved = null)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        // ── Box ──
        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.width = 800;
        Color32 borderColor = resultType == 1 ? new Color32(39, 174, 96, 255)
            : resultType == 0 ? new Color32(212, 160, 23, 255)
            : new Color32(192, 57, 43, 255);
        box.style.borderTopColor = new StyleColor(borderColor);
        box.style.borderBottomColor = new StyleColor(borderColor);
        box.style.borderLeftColor = new StyleColor(borderColor);
        box.style.borderRightColor = new StyleColor(borderColor);
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        string headerMod = resultType == 1 ? "fired-modal-header--green"
            : resultType == 0 ? "fired-modal-header--warning"
            : "";
        header.AddToClassList(headerMod);

        // El título lo decide quien llama ("TRASPASO NO VÁLIDO", "ERROR AL SIMULAR EL
        // DÍA"...). Antes se ignoraba y siempre se veía "OFERTAS RECHAZADAS".
        string subtitleText = !string.IsNullOrEmpty(title) ? title
            : resultType == 1 ? "OFERTAS ACEPTADAS"
            : resultType == 0 ? "HAY OFERTAS RECHAZADAS"
            : "OFERTAS RECHAZADAS";
        var subtitle = new Label(subtitleText);
        subtitle.AddToClassList("fired-modal-title");
        subtitle.style.color = new StyleColor(borderColor);
        header.Add(subtitle);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // ── Operation list ──
        var opList = new VisualElement();
        opList.AddToClassList("offer-result-modal-op-list");
        foreach (var rawLine in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            string line = rawLine.TrimStart();
            string badgeText = null;
            string cardMod = "neutral";
            string badgeMod = null;

            if (line.StartsWith("\u2713"))
            {
                line = line.Substring(1).TrimStart();
                badgeText = "ACEPTADA";
                cardMod = "positive";
                badgeMod = "positive";
            }
            else if (line.StartsWith("\u2717"))
            {
                line = line.Substring(1).TrimStart();
                badgeText = "RECHAZADA";
                cardMod = "negative";
                badgeMod = "negative";
            }
            else if (line.StartsWith("\u26A0"))
            {
                line = line.Substring(1).TrimStart();
                badgeText = "AVISO";
                cardMod = "warning";
                badgeMod = "warning";
            }

            var card = new VisualElement();
            card.AddToClassList("offer-result-modal-op-card");
            card.AddToClassList($"offer-result-modal-op-card--{cardMod}");

            var lineLabel = new Label(line);
            lineLabel.AddToClassList("offer-result-modal-op-text");
            card.Add(lineLabel);

            if (badgeText != null)
            {
                var badge = new VisualElement();
                badge.AddToClassList("offer-result-modal-op-badge");
                badge.AddToClassList($"offer-result-modal-op-badge--{badgeMod}");

                var badgeLbl = new Label(badgeText);
                badgeLbl.AddToClassList("offer-result-modal-op-badge-text");
                badgeLbl.AddToClassList($"offer-result-modal-op-badge-text--{badgeMod}");
                badge.Add(badgeLbl);
                card.Add(badge);
            }

            opList.Add(card);
        }
        body.Add(opList);
        box.Add(body);

        // ── Buttons ──
        if (_fastSimRunning && _fastSimOfferPauseActive)
        {
            var actions = new VisualElement();
            actions.AddToClassList("fired-modal-actions");

            var goToQuintetoBtn = new Button();
            goToQuintetoBtn.text = "IR A QUINTETO";
            goToQuintetoBtn.AddToClassList("btn-modal-confirm");
            goToQuintetoBtn.AddToClassList("btn-modal-confirm--gold");
            goToQuintetoBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                _firedOverlay.style.display = DisplayStyle.None;
                _offerModalGoToQuinteto = true;
                _offerModalResolved = true;
                onFastSimResolved?.Invoke(true);
            });
            actions.Add(goToQuintetoBtn);

            var continueBtn = new Button();
            continueBtn.text = "SEGUIR SIMULANDO";
            continueBtn.AddToClassList("btn-modal-cancel");
            continueBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                _firedOverlay.style.display = DisplayStyle.None;
                _offerModalGoToQuinteto = false;
                _offerModalResolved = true;
                onFastSimResolved?.Invoke(false);
            });
            actions.Add(continueBtn);

            box.Add(actions);

            if (CursorManager.Instance != null)
            {
                CursorManager.Instance.RegisterHandCursor(goToQuintetoBtn);
                CursorManager.Instance.RegisterHandCursor(continueBtn);
            }
        }
        else
        {
            var actions = new VisualElement();
            actions.AddToClassList("fired-modal-actions");

            var btn = new Button();
            btn.text = "CERRAR";
            btn.AddToClassList("btn-modal-cancel");
            btn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                _firedOverlay.style.display = DisplayStyle.None;
                // Aunque no haya simulación en curso, quien pasó un callback debe quedar
                // liberado (nunca dejar una espera sin resolver).
                onFastSimResolved?.Invoke(false);
                ShowNextPendingTradeOffer();
            });
            actions.Add(btn);

            box.Add(actions);

            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(btn);
        }
    }

    void ShowBudgetWarningModal(int num, bool fastSim = false)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderRightColor = new StyleColor(new Color32(212, 160, 23, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label("AVISO FINANCIERO");
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        var text = new Label(
            $"El presupuesto del club está en números rojos.\n\n" +
            $"Aviso {num} de 3."
        );
        text.AddToClassList("fired-modal-text");
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");
        var btn = new Button();
        btn.text = fastSim ? "IR A FINANZAS" : "CERRAR";
        btn.AddToClassList(fastSim ? "btn-modal-confirm" : "btn-modal-cancel");
        if (fastSim) btn.AddToClassList("btn-modal-confirm--gold");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            if (fastSim)
            {
                _fastSimStopRequested = true;
                HideLoading();
                _firedOverlay.style.display = DisplayStyle.None;
                ScreenManager.Instance.GoTo(GameScreen.Finances);
            }
            else
            {
                _firedOverlay.style.display = DisplayStyle.None;
            }
        });
        actions.Add(btn);
        box.Add(actions);
    }

    void ShowBudgetFiredModal()
    {
        MarkManagerFired();

        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        var title = new Label("DESPIDO");
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        var text = new Label(
            "La directiva ha decidido prescindir de tus servicios\n" +
            "debido a la mala gestión financiera del club."
        );
        text.AddToClassList("fired-modal-text");
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");
        var btn = new Button();
        btn.text = "IR AL MENU PRINCIPAL";
        btn.AddToClassList("btn-modal-confirm");
        btn.AddToClassList("btn-modal-confirm--danger");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        actions.Add(btn);
        box.Add(actions);
    }

    /// <summary>
    /// Estado del reparto de minutos de la rotación del equipo del usuario (solo convocados
    /// jugables). `IsComplete` = sin jugables, todo en automático, o 240 exactos y válidos.
    /// </summary>
    RotationStatus GetRotationStatus()
    {
        if (_myTeam == null) return new RotationStatus();

        var lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        if (lineup.Count == 0) return new RotationStatus();

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var byId = players.ToDictionary(p => p.id);

        var slots = new List<RotationSlot>();
        foreach (var l in lineup)
        {
            if (l.slot != 0 && l.slot != 1) continue;
            if (!byId.TryGetValue(l.player_id, out var p) || p == null) continue;
            slots.Add(new RotationSlot
            {
                playerId = p.id,
                minutes = l.rotation_minutes,
                playable = p.injury_days == 0 && p.g_league_assigned != 1,
                suggestion = GameSimulator.GetSuggestedMinutes(p, l.slot == 0)
            });
        }

        return RotationPlanner.Evaluate(slots);
    }

    /// <summary>Aviso de rotación incompleta: minutos sin asignar, excedidos o inválidos.</summary>
    void ShowRotationModal(RotationStatus rot)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        var gold = new Color32(212, 160, 23, 255);
        box.style.borderTopColor = new StyleColor(gold);
        box.style.borderBottomColor = new StyleColor(gold);
        box.style.borderLeftColor = new StyleColor(gold);
        box.style.borderRightColor = new StyleColor(gold);
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label("ROTACIÓN INCOMPLETA");
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        string detail = rot.hasInvalid
            ? "Hay minutos de rotación con valores no válidos."
            : rot.Missing > 0
                ? "Faltan minutos por asignar en la rotación."
                : $"Tienes {rot.total} minutos asignados: te pasas por {-rot.Missing}.";
        var text = new Label(detail + "\nAsigna todos los minutos antes de simular o usa el reparto automático.");
        text.AddToClassList("fired-modal-text");
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var goBtn = new Button();
        goBtn.text = "IR A ROTACIÓN";
        goBtn.AddToClassList("btn-modal-confirm");
        goBtn.AddToClassList("btn-modal-confirm--gold");
        goBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _rotationModalGoToRotation = true;
            _rotationModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(goBtn);

        var autoBtn = new Button();
        autoBtn.text = "ASIGNAR AUTOMÁTICAMENTE";
        autoBtn.AddToClassList("btn-modal-confirm");
        autoBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            DatabaseManager.Instance.AutoAssignRotationMinutes(_myTeam.id, allPlayers);
            _rotationModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(autoBtn);
        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(goBtn);
            CursorManager.Instance.RegisterHandCursor(autoBtn);
        }
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

    bool IsMyTeamBackToBack(int gameDay)
    {
        if (_myTeam == null) return false;
        return DatabaseManager.Instance.Db.Table<GameData>()
            .Any(g => (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id)
                   && g.is_played == 1 && g.game_day == gameDay - 1);
    }

    List<(LineupData li, PlayerData p)> GetTiredActivePlayers()
    {
        var lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        if (lineup.Count == 0) return new List<(LineupData, PlayerData)>();

        var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var playerMap = allPlayers.ToDictionary(p => p.id);

        return lineup
            .Where(l => l.slot <= 1)
            .Select(l => (li: l, p: playerMap.GetValueOrDefault(l.player_id)))
            .Where(x => x.p != null && x.p.injury_days == 0)
            .Where(x => (x.li.slot == 0 && x.p.fisico < 55) || (x.li.slot == 1 && x.p.fisico < 30))
            .OrderBy(x => x.li.slot)
            .ThenBy(x => x.p.fisico)
            .ToList();
    }

    void ShowEmptyLineupModal(List<int> emptySlots)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");

        var title = new Label("QUINTETO INCOMPLETO");
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // ── Empty position cards ──
        var playerList = new VisualElement();
        playerList.AddToClassList("injury-modal-player-list");

        var posNames = new[] { "Base", "Escolta", "Alero", "Ala-Pívot", "Pívot" };
        var posShort = new[] { "B", "E", "A", "AP", "P" };

        foreach (var si in emptySlots)
        {
            var card = new VisualElement();
            card.AddToClassList("trade-modal-player-card");
            card.AddToClassList("empty-modal-card");

            // Slot icon placeholder
            var slotIcon = new VisualElement();
            slotIcon.AddToClassList("empty-modal-slot-icon");

            var iconText = new Label(posShort[si]);
            iconText.AddToClassList("empty-modal-slot-icon-text");
            slotIcon.Add(iconText);
            card.Add(slotIcon);

            // Info
            var info = new VisualElement();
            info.AddToClassList("trade-modal-player-info");

            var nameLbl = new Label(posNames[si]);
            nameLbl.AddToClassList("trade-modal-player-name");
            nameLbl.AddToClassList("injury-modal-text-lg");
            info.Add(nameLbl);

            var metaLbl = new Label("Quinteto titular — slot vacío");
            metaLbl.AddToClassList("trade-modal-player-meta");
            metaLbl.AddToClassList("injury-modal-text-lg");
            info.Add(metaLbl);

            // Badge
            var badge = new VisualElement();
            badge.AddToClassList("empty-modal-badge");

            var badgeText = new Label("SIN ASIGNAR");
            badgeText.AddToClassList("empty-modal-badge-text");
            badge.Add(badgeText);

            info.Add(badge);

            card.Add(info);

            playerList.Add(card);
        }
        body.Add(playerList);
        box.Add(body);

        // ── Buttons ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var manualBtn = new Button();
        manualBtn.text = "REVISAR MANUALMENTE";
        manualBtn.AddToClassList("btn-modal-confirm");
        manualBtn.AddToClassList("btn-modal-confirm--gold");
        manualBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _emptyLineupGoToQuinteto = true;
            _emptyLineupModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(manualBtn);

        var autoBtn = new Button();
        autoBtn.text = "AUTO-RELLENAR";
        autoBtn.AddToClassList("btn-modal-cancel");
        autoBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            var injuredIds = new HashSet<int>(allPlayers
                .Where(p => p.injury_days > 0 || p.g_league_assigned == 1)
                .Select(p => p.id));
            DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, allPlayers, injuredIds);
            _emptyLineupModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(autoBtn);

        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(manualBtn);
            CursorManager.Instance.RegisterHandCursor(autoBtn);
        }
    }

    void ShowInjuredLineupModal(List<(LineupData li, PlayerData p)> injured)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.width = 800;
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");

        var title = new Label("JUGADORES LESIONADOS");
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // ── Injured players ──
        var playerList = new VisualElement();
        playerList.AddToClassList("injury-modal-player-list");

        // Iconos de la acción "tratar lesión" (misma imagen que el editor de contenido).
        var injectionIcon = Resources.Load<Texture2D>("Icons/inyeccion");
        var treatedIcon = Resources.Load<Texture2D>("Icons/boton-v-64px");

        foreach (var (li, p) in injured)
        {
            var card = new VisualElement();
            card.AddToClassList("trade-modal-player-card");
            card.AddToClassList("injury-modal-card");

            // Photo
            var photo = new VisualElement();
            photo.AddToClassList("trade-modal-player-photo");
            Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
            if (tex != null)
                photo.style.backgroundImage = new StyleBackground(tex);
            card.Add(photo);

            // Info
            var info = new VisualElement();
            info.AddToClassList("trade-modal-player-info");

            var nameLbl = new Label($"{p.first_name} {p.last_name}");
            nameLbl.AddToClassList("trade-modal-player-name");
            nameLbl.AddToClassList("injury-modal-text-lg");
            info.Add(nameLbl);

            string stars = p.overall >= 85 ? " ★★★" : p.overall >= 75 ? " ★★" : p.overall >= 65 ? " ★" : "";
            var metaLbl = new Label($"{PositionCodes.GetShort(p.position)} | {p.age} años{stars}");
            metaLbl.AddToClassList("trade-modal-player-meta");
            metaLbl.AddToClassList("injury-modal-text-lg");
            info.Add(metaLbl);

            // Injury badge
            var injuryBadge = new VisualElement();
            injuryBadge.AddToClassList("injury-modal-badge");

            var injuryTypeLbl = new Label(p.injury_type);
            injuryTypeLbl.AddToClassList("injury-modal-badge-text");
            injuryBadge.Add(injuryTypeLbl);

            var injuryDaysLbl = new Label($"{p.injury_days} días");
            injuryDaysLbl.AddToClassList("injury-modal-badge-days");
            injuryBadge.Add(injuryDaysLbl);

            info.Add(injuryBadge);

            // Slot label
            string slotLabel = li.slot == 0 ? "TITULAR" : "BANQUILLO";
            var slotLbl = new Label(slotLabel);
            slotLbl.AddToClassList("injury-modal-slot");
            if (li.slot == 0)
                slotLbl.AddToClassList("injury-modal-slot--starter");
            info.Add(slotLbl);

            card.Add(info);

            // OVR column
            var ovrCol = new VisualElement();
            ovrCol.AddToClassList("trade-modal-player-ovr-col");

            var ovrLbl = new Label(p.overall.ToString());
            ovrLbl.AddToClassList("trade-modal-player-ovr");
            if (p.overall >= 80)
                ovrLbl.style.color = new StyleColor(new Color32(39, 174, 96, 255));
            else if (p.overall >= 60)
                ovrLbl.style.color = new StyleColor(new Color32(212, 160, 23, 255));
            else
                ovrLbl.style.color = new StyleColor(new Color32(192, 57, 43, 255));
            ovrCol.Add(ovrLbl);

            var ovrSub = new Label("MED");
            ovrSub.AddToClassList("trade-modal-player-ovr-label");
            ovrCol.Add(ovrSub);

            card.Add(ovrCol);

            // Botón de tratamiento: icono de inyección (misma acción; al tratar cambia al
            // icono de "hecho" y se actualiza el valor de días EN ESTE MISMO modal).
            var treatBtn = new Button();
            bool alreadyTreated = p.treated == 1;
            var medico = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id)
                .FirstOrDefault(e => e.position == "MEDICO");
            bool hasMedico = medico != null;

            // La caja del botón conserva su tamaño; dentro va la imagen a 40x40.
            var treatIcon = new VisualElement();
            treatIcon.AddToClassList("injury-modal-treat-icon");
            treatBtn.Add(treatIcon);

            void PaintTreatIcon(Texture2D tex, bool done, bool enabled)
            {
                treatBtn.text = "";
                if (tex != null) treatIcon.style.backgroundImage = new StyleBackground(tex);
                treatBtn.EnableInClassList("injury-modal-treat-btn--done", done);
                treatBtn.SetEnabled(enabled);
                treatBtn.tooltip = done ? "Lesión ya tratada"
                               : enabled ? "Tratar lesión (reduce los días de baja)"
                               : "Sin médico en el equipo: no se puede tratar";
            }

            treatBtn.AddToClassList("injury-modal-treat-btn");
            treatBtn.AddToClassList("injury-modal-treat-btn--icon");
            CursorManager.Instance?.RegisterHandCursor(treatBtn);
            PaintTreatIcon(alreadyTreated ? treatedIcon : injectionIcon,
                           done: alreadyTreated,
                           enabled: !alreadyTreated && hasMedico);

            if (!alreadyTreated && hasMedico)
            {
                treatBtn.RegisterCallback<ClickEvent>(_ =>
                {
                    PlayClick();
                    float pct = medico.reputation switch
                    {
                        5 => UnityEngine.Random.Range(0.25f, 0.40f),
                        4 => UnityEngine.Random.Range(0.20f, 0.32f),
                        3 => UnityEngine.Random.Range(0.15f, 0.25f),
                        2 => UnityEngine.Random.Range(0.10f, 0.18f),
                        _ => UnityEngine.Random.Range(0.05f, 0.12f),
                    };
                    int newDays = Mathf.CeilToInt(p.injury_days * (1f - pct));
                    p.injury_days = Mathf.Clamp(newDays, 1, p.injury_days);
                    p.treated = 1;
                    DatabaseManager.Instance.UpdatePlayer(p);

                    // Refrescar el valor mostrado en el propio modal.
                    injuryDaysLbl.text = $"{p.injury_days} días";
                    injuryBadge.tooltip = $"Tratada: quedan {p.injury_days} días de baja";
                    PaintTreatIcon(treatedIcon, done: true, enabled: false);
                });
            }
            card.Add(treatBtn);

            playerList.Add(card);
        }
        body.Add(playerList);
        box.Add(body);

        // ── Buttons ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var manualBtn = new Button();
        manualBtn.text = "CAMBIAR MANUALMENTE";
        manualBtn.AddToClassList("btn-modal-confirm");
        manualBtn.AddToClassList("btn-modal-confirm--gold");
        manualBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _injuredModalGoToQuinteto = true;
            _injuredModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(manualBtn);

        var autoBtn = new Button();
        autoBtn.text = "REEMPLAZAR AUTOMÁTICAMENTE";
        autoBtn.AddToClassList("btn-modal-cancel");
        autoBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            var inactiveIds = new HashSet<int>(allPlayers
                .Where(p => p.injury_days > 0 || p.g_league_assigned == 1)
                .Select(p => p.id));
            DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, allPlayers, inactiveIds);
            _injuredModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(autoBtn);
        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(manualBtn);
            CursorManager.Instance.RegisterHandCursor(autoBtn);
        }
    }

    void ShowLoadManagementModal(List<(LineupData li, PlayerData p)> tiredPlayers, bool isB2B)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.width = 800;
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        var title = new Label("GESTIÓN DE DESCANSO");
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        if (isB2B)
        {
            var b2bLabel = new Label("Back‑to‑back: tu equipo jugó ayer. Considera descansar algún jugador.");
            b2bLabel.AddToClassList("fired-modal-text");
            b2bLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(b2bLabel);
        }

        if (tiredPlayers.Count == 0 && isB2B)
        {
            var infoLabel = new Label("Ningún jugador está por debajo del umbral de cansancio, pero el descanso proactivo puede prevenir lesiones.");
            infoLabel.AddToClassList("fired-modal-text");
            infoLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(infoLabel);
        }

        if (tiredPlayers.Count > 0)
        {
            var text = new Label("Los siguientes jugadores están cansados y tienen mayor riesgo de lesión:");
            text.AddToClassList("fired-modal-text");
            text.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(text);

            var list = new VisualElement();
            list.AddToClassList("injured-modal-list");
            foreach (var (li, p) in tiredPlayers)
            {
                var slotLabel = li.slot == 0 ? "TITULAR" : "BANQUILLO";
                Color fisicoColor = p.fisico >= 60 ? new Color32(39, 174, 96, 255)
                    : p.fisico >= 30 ? new Color32(212, 160, 23, 255)
                    : new Color32(192, 57, 43, 255);
                var row = new Label($"{p.first_name} {p.last_name} — {PositionCodes.GetShort(p.position)} ({slotLabel}) — Físico: {p.fisico}");
                row.AddToClassList("injured-modal-player-row");
                row.style.color = new StyleColor(fisicoColor);
                list.Add(row);
            }
            body.Add(list);
        }

        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        if (tiredPlayers.Count > 0)
        {
            var restBtn = new Button();
            restBtn.text = "DESCANSAR CANSADOS";
            restBtn.AddToClassList("btn-modal-confirm");
            restBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
                var tiredIds = new HashSet<int>(tiredPlayers.Select(x => x.p.id));
                var injuredIds = new HashSet<int>(allPlayers
                    .Where(p => p.injury_days > 0)
                    .Select(p => p.id));
                tiredIds.UnionWith(injuredIds);
                DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, allPlayers, tiredIds);
                _loadMgmtModalResolved = true;
                _firedOverlay.style.display = DisplayStyle.None;
            });
            actions.Add(restBtn);
        }

        var manualBtn = new Button();
        manualBtn.text = "IR AL QUINTETO";
        manualBtn.AddToClassList("btn-modal-confirm");
        manualBtn.AddToClassList("btn-modal-confirm--gold");
        manualBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _loadMgmtModalGoToQuinteto = true;
            _loadMgmtModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(manualBtn);

        var continueBtn = new Button();
        continueBtn.text = "CONTINUAR SIN CAMBIOS";
        continueBtn.AddToClassList("btn-modal-cancel");
        continueBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _loadMgmtModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(continueBtn);

        box.Add(actions);
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
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderRightColor = new StyleColor(new Color32(39, 174, 96, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--green");

        var title = new Label("JUGADOR(ES) RECUPERADO(S)");
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(39, 174, 96, 255));
        header.Add(title);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // ── Player cards ──
        var playerList = new VisualElement();
        playerList.AddToClassList("injury-modal-player-list");

        foreach (var p in recovered)
        {
            var card = new VisualElement();
            card.AddToClassList("trade-modal-player-card");
            card.AddToClassList("recovery-modal-card");

            // Photo
            var photo = new VisualElement();
            photo.AddToClassList("trade-modal-player-photo");
            Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
            if (tex != null)
                photo.style.backgroundImage = new StyleBackground(tex);
            card.Add(photo);

            // Info
            var info = new VisualElement();
            info.AddToClassList("trade-modal-player-info");

            var nameLbl = new Label($"{p.first_name} {p.last_name}");
            nameLbl.AddToClassList("trade-modal-player-name");
            nameLbl.AddToClassList("injury-modal-text-lg");
            info.Add(nameLbl);

            string stars = p.overall >= 85 ? " ★★★" : p.overall >= 75 ? " ★★" : p.overall >= 65 ? " ★" : "";
            var metaLbl = new Label($"{PositionCodes.GetShort(p.position)} | {p.age} años{stars}");
            metaLbl.AddToClassList("trade-modal-player-meta");
            metaLbl.AddToClassList("injury-modal-text-lg");
            info.Add(metaLbl);

            // Recovery badge
            var badge = new VisualElement();
            badge.AddToClassList("recovery-modal-badge");

            var badgeText = new Label("RECUPERADO — DISPONIBLE");
            badgeText.AddToClassList("recovery-modal-badge-text");
            badge.Add(badgeText);

            info.Add(badge);

            card.Add(info);

            // OVR column
            var ovrCol = new VisualElement();
            ovrCol.AddToClassList("trade-modal-player-ovr-col");

            var ovrLbl = new Label(p.overall.ToString());
            ovrLbl.AddToClassList("trade-modal-player-ovr");
            if (p.overall >= 80)
                ovrLbl.style.color = new StyleColor(new Color32(39, 174, 96, 255));
            else if (p.overall >= 60)
                ovrLbl.style.color = new StyleColor(new Color32(212, 160, 23, 255));
            else
                ovrLbl.style.color = new StyleColor(new Color32(192, 57, 43, 255));
            ovrCol.Add(ovrLbl);

            var ovrSub = new Label("MED");
            ovrSub.AddToClassList("trade-modal-player-ovr-label");
            ovrCol.Add(ovrSub);

            card.Add(ovrCol);

            playerList.Add(card);
        }
        body.Add(playerList);
        box.Add(body);

        // ── Buttons ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var goBtn = new Button();
        goBtn.text = "IR A QUINTETO";
        goBtn.AddToClassList("btn-modal-confirm");
        goBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
            if (_fastSimRunning)
            {
                _recoveryModalGoToQuinteto = true;
                _recoveryModalResolved = true;
            }
            else
            {
                ScreenManager.Instance.GoTo(GameScreen.Quinteto);
            }
        });
        actions.Add(goBtn);

        var closeBtn = new Button();
        closeBtn.text = "REHACER CONVOCATORIA";
        closeBtn.AddToClassList("btn-modal-cancel");
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            var inactiveIds = new HashSet<int>(allPlayers
                .Where(p => p.injury_days > 0 || p.g_league_assigned == 1)
                .Select(p => p.id));
            DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, allPlayers, inactiveIds);
            _firedOverlay.style.display = DisplayStyle.None;
            if (_fastSimRunning)
            {
                _recoveryModalGoToQuinteto = false;
                _recoveryModalResolved = true;
            }
        });
        actions.Add(closeBtn);

        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(goBtn);
            CursorManager.Instance.RegisterHandCursor(closeBtn);
        }
    }

    void ShowPendingIRReleaseModal()
    {
        if (_pendingIRReleaseCount <= 0)
        {
            _irReleaseModalResolved = true;
            return;
        }
        _pendingIRReleaseIds.Clear();
        if (DatabaseManager.Instance.GetRosterCount(_myTeam.id) <= TradeHelper.MAX_ROSTER)
        {
            _pendingIRReleaseCount = 0;
            _irReleaseModalResolved = true;
            return;
        }

        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderRightColor = new StyleColor(new Color32(39, 174, 96, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--green");
        var title = new Label("PLANTILLA COMPLETA TRAS RECUPERACI\u00d3N");
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(39, 174, 96, 255));
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        var text = new Label("Un jugador en la reserva de lesionados (IR) se ha recuperado y su plaza vuelve a contar. Tu plantilla supera el m\u00e1ximo de " + TradeHelper.MAX_ROSTER + " jugadores: debes liberar a alguien (buyout).");
        text.AddToClassList("fired-modal-text");
        body.Add(text);

        var list = new VisualElement();
        list.AddToClassList("injured-modal-list");
        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id)
            .Where(p => p.is_on_ir == 0)
            .OrderByDescending(p => p.GetCalculatedAverage())
            .ToList();
        foreach (var p in players)
        {
            var row = new VisualElement();
            row.AddToClassList("injured-modal-player-row");

            var nameLbl = new Label($"{p.first_name} {p.last_name} — {PositionCodes.GetShort(p.position)} · OVR {p.GetCalculatedAverage()} · ${p.salary:N0}");
            nameLbl.style.flexGrow = 1;
            row.Add(nameLbl);

            var releaseBtn = new Button();
            releaseBtn.text = "LIBERAR";
            releaseBtn.AddToClassList("injured-modal-btn");
            releaseBtn.style.backgroundColor = new StyleColor(new Color32(192, 57, 43, 255));
            releaseBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                ReleasePlayerForIR(p);
            });
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(releaseBtn);
            row.Add(releaseBtn);

            list.Add(row);
        }
        body.Add(list);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var cancelBtn = new Button();
        cancelBtn.text = "IR A PLANTILLA";
        cancelBtn.AddToClassList("btn-modal-confirm");
        cancelBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
            _irReleaseModalResolved = true;
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(cancelBtn);
        actions.Add(cancelBtn);
        box.Add(actions);
    }

    void ReleasePlayerForIR(PlayerData player)
    {
        long remainingSalary = player.salary * player.contract_years;
        int stretchYears = Mathf.Max(1, player.contract_years * 2);
        long annualPayment = remainingSalary / stretchYears;
        int currentDay = _season?.current_game_day ?? 0;
        string playerName = $"{player.first_name} {player.last_name}";
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        player.team_id = 0;
        player.last_team_id = 0;
        player.seasons_with_team = 0;
        player.is_on_ir = 0;
        player.g_league_assigned = 0;
        DatabaseManager.Instance.UpdatePlayer(player);

        long remainder = remainingSalary;
        for (int y = 0; y < stretchYears; y++)
        {
            long payment = (y == stretchYears - 1) ? remainder : annualPayment;
            remainder -= payment;
            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = _myTeam.id,
                season_id = _season?.id ?? 0,
                record_type = FinanceRecord.TYPE_BUYOUT,
                game_day = currentDay,
                amount = payment,
                created_at = now
            });
        }

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = "Rescisión de contrato (buyout)",
            body = NewsTexts.Buyout(playerName, _myTeam.name) +
                   $"\n\nSalario restante: ${remainingSalary:N0}\nPago progresivo: ${annualPayment:N0} durante {stretchYears} años.",
            game_day = currentDay,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0,
            category = NewsCategory.TRASPASOS,
            news_type = NewsType.BUYOUT
        });

        _pendingIRReleaseCount--;
        if (DatabaseManager.Instance.GetRosterCount(_myTeam.id) <= TradeHelper.MAX_ROSTER || _pendingIRReleaseCount <= 0)
        {
            _firedOverlay.style.display = DisplayStyle.None;
            _irReleaseModalResolved = true;
            return;
        }
        ShowPendingIRReleaseModal();
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

    void ShowTradeDeadlineModal(bool fastSim = false)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderRightColor = new StyleColor(new Color32(212, 160, 23, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label("CIERRE DE MERCADO");
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        var text = new Label("El período de traspasos finaliza el 8 de febrero. Aún estás a tiempo de realizar operaciones.");
        text.AddToClassList("fired-modal-text");
        text.style.marginTop = 15;
        text.style.marginBottom = 15;
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var marketBtn = new Button();
        marketBtn.text = "IR AL MERCADO";
        marketBtn.AddToClassList("btn-modal-confirm");
        marketBtn.AddToClassList("btn-modal-confirm--gold");
        marketBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
            ScreenManager.Instance.GoTo(GameScreen.Market);
        });
        actions.Add(marketBtn);

        var closeBtn = new Button();
        closeBtn.text = fastSim ? "SEGUIR SIMULANDO" : "CERRAR";
        closeBtn.AddToClassList("btn-modal-cancel");
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(closeBtn);

        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(marketBtn);
            CursorManager.Instance.RegisterHandCursor(closeBtn);
        }
    }

    void ShowDeadlineDayModal(bool fastSim = false)
    {
        _deadlineDayActive = true;
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderRightColor = new StyleColor(new Color32(212, 160, 23, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label("CIERRE DE MERCADO");
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        header.Add(title);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // Operations list
        var pendingTrades = DatabaseManager.Instance.GetPendingTradeOffers(_manager.id) ?? new List<TradeOfferData>();
        var pendingSATIds = GetPendingSATIds();
        var maturedOffers = DatabaseManager.Instance.GetMaturedUnprocessedOffers(_manager.id, _season.current_game_day) ?? new List<OfferData>();

        var opList = new VisualElement();
        opList.AddToClassList("deadline-modal-op-list");

        if (pendingTrades.Count == 0 && pendingSATIds.Count == 0 && maturedOffers.Count == 0)
        {
            var emptyLbl = new Label("No tienes operaciones pendientes.\nÚltima oportunidad para realizar traspasos antes del cierre.");
            emptyLbl.AddToClassList("fired-modal-text");
            emptyLbl.style.marginTop = 15;
            emptyLbl.style.marginBottom = 15;
            body.Add(emptyLbl);
            opList = null;
        }
        else
        {
            var allTeams = DatabaseManager.Instance.GetAllTeams();
            var teamDict = allTeams.ToDictionary(t => t.id);

            // ── Trade offers ──
            foreach (var offer in pendingTrades)
            {
                var card = new VisualElement();
                card.AddToClassList("trade-modal-player-card");
                card.AddToClassList("deadline-modal-card");

                var photo = new VisualElement();
                photo.AddToClassList("trade-modal-player-photo");
                photo.AddToClassList("deadline-modal-icon");
                var iconLbl = new Label("\U0001F504");
                iconLbl.AddToClassList("deadline-modal-icon-text");
                photo.Add(iconLbl);
                card.Add(photo);

                var info = new VisualElement();
                info.AddToClassList("trade-modal-player-info");

                string teamName = teamDict.TryGetValue(offer.team_id_from, out var t) ? t.name : "Desconocido";
                var nameLbl = new Label(teamName.ToUpper());
                nameLbl.AddToClassList("trade-modal-player-name");
                info.Add(nameLbl);

                var wantedIds = offer.GetWantedPlayerIds();
                var offeredIds = offer.GetOfferedPlayerIds();
                var wantedPicks = offer.GetWantedPickIds();
                var offeredPicks = offer.GetOfferedPickIds();

                var wantedNames = wantedIds.Select(id => DatabaseManager.Instance.GetPlayerById(id))
                    .Where(p => p != null).Select(p => $"{p.first_name} {p.last_name}").ToList();
                var offeredNames = offeredIds.Select(id => DatabaseManager.Instance.GetPlayerById(id))
                    .Where(p => p != null).Select(p => $"{p.first_name} {p.last_name}").ToList();

                string detail = "";
                if (wantedNames.Count > 0) detail += $"Te pide: {string.Join(", ", wantedNames)}";
                if (wantedPicks.Count > 0) detail += (detail.Length > 0 ? "\n" : "") + $"Te pide: {wantedPicks.Count} pick(s)";
                if (offeredNames.Count > 0) detail += (detail.Length > 0 ? "\n" : "") + $"Te ofrece: {string.Join(", ", offeredNames)}";
                if (offeredPicks.Count > 0) detail += (detail.Length > 0 ? "\n" : "") + $"Te ofrece: {offeredPicks.Count} pick(s)";

                var metaLbl = new Label(detail);
                metaLbl.AddToClassList("trade-modal-player-meta");
                metaLbl.style.whiteSpace = WhiteSpace.Normal;
                info.Add(metaLbl);
                card.Add(info);

                var badge = new VisualElement();
                badge.AddToClassList("deadline-modal-badge");
                badge.AddToClassList("deadline-modal-badge--trade");
                var badgeText = new Label("TRASPASO");
                badgeText.AddToClassList("deadline-modal-badge-text");
                badge.Add(badgeText);
                card.Add(badge);

                opList.Add(card);
            }

            // ── Matured FA offers ──
            foreach (var offer in maturedOffers)
            {
                bool isSAT = pendingSATIds.Contains(offer.player_id);

                var card = new VisualElement();
                card.AddToClassList("trade-modal-player-card");
                card.AddToClassList("deadline-modal-card");

                var photo = new VisualElement();
                photo.AddToClassList("trade-modal-player-photo");
                photo.AddToClassList("deadline-modal-icon");
                var iconLbl = new Label(isSAT ? "\U0001F91D" : "\U0001F4CB");
                iconLbl.AddToClassList("deadline-modal-icon-text");
                photo.Add(iconLbl);
                card.Add(photo);

                var info = new VisualElement();
                info.AddToClassList("trade-modal-player-info");

                var player = DatabaseManager.Instance.GetPlayerById(offer.player_id);
                string playerName = player != null ? $"{player.first_name} {player.last_name}" : $"Jugador #{offer.player_id}";
                var nameLbl = new Label(playerName.ToUpper());
                nameLbl.AddToClassList("trade-modal-player-name");
                info.Add(nameLbl);

                string salaryStr = offer.offer_salary >= 1_000_000
                    ? $"${offer.offer_salary / 1_000_000f:F1}M"
                    : $"${offer.offer_salary / 1_000f:N0}K";
                string optionStr = "";
                if (offer.has_team_option == 1) optionStr = " (TO último año)";
                else if (offer.has_player_option == 1) optionStr = " (PO último año)";
                string twoWayStr = offer.is_two_way == 1 ? " · TWO-WAY" : "";
                var metaLbl = new Label($"{salaryStr} × {offer.offer_years} años{optionStr}{twoWayStr}");
                metaLbl.AddToClassList("trade-modal-player-meta");
                info.Add(metaLbl);
                card.Add(info);

                var badge = new VisualElement();
                badge.AddToClassList("deadline-modal-badge");
                badge.AddToClassList(isSAT ? "deadline-modal-badge--sat" : "deadline-modal-badge--fa");
                var badgeText = new Label(isSAT ? "SIGN & TRADE" : "OFERTA FA");
                badgeText.AddToClassList("deadline-modal-badge-text");
                badge.Add(badgeText);
                card.Add(badge);

                opList.Add(card);
            }
        }
        if (opList != null)
        {
            body.Add(opList);

            var hint = new Label("Última oportunidad para realizar traspasos antes del cierre.");
            hint.AddToClassList("deadline-modal-hint");
            body.Add(hint);
        }

        box.Add(body);

        // ── Buttons ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var marketBtn = new Button();
        marketBtn.text = "IR AL MERCADO";
        marketBtn.AddToClassList("btn-modal-confirm");
        marketBtn.AddToClassList("btn-modal-confirm--gold");
        marketBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _deadlineDayActive = false;
            _firedOverlay.style.display = DisplayStyle.None;
            ScreenManager.Instance.GoTo(GameScreen.Market);
        });
        actions.Add(marketBtn);

        var closeBtn = new Button();
        closeBtn.text = fastSim ? "SEGUIR SIMULANDO" : "CERRAR";
        closeBtn.AddToClassList("btn-modal-cancel");
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _deadlineDayActive = false;
            _firedOverlay.style.display = DisplayStyle.None;
            RefreshActionButton();
        });
        actions.Add(closeBtn);
        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(marketBtn);
            CursorManager.Instance.RegisterHandCursor(closeBtn);
        }
    }

    void ShowRenewalDeadlineModal(bool fastSim = false)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderRightColor = new StyleColor(new Color32(212, 160, 23, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label("FECHA LÍMITE DE RENOVACIONES");
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        var text = new Label("Hoy 12 de febrero es el último día para renovar contratos de tus jugadores. Las ofertas enviadas hoy tendrán respuesta en 7 días.");
        text.AddToClassList("fired-modal-text");
        body.Add(text);

        var hint = new Label("Si no renuevas a tiempo, tus jugadores pasarán a ser agentes libres al finalizar la temporada.");
        hint.AddToClassList("fired-modal-text");
        hint.style.marginTop = 10;
        body.Add(hint);

        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var rosterBtn = new Button();
        rosterBtn.text = "IR A JUGADORES";
        rosterBtn.AddToClassList("btn-modal-confirm");
        rosterBtn.AddToClassList("btn-modal-confirm--gold");
        rosterBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _renewalDeadlineModalShown = true;
            _firedOverlay.style.display = DisplayStyle.None;
            _renewalDeadlineResolved = true;
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        actions.Add(rosterBtn);

        var closeBtn = new Button();
        closeBtn.text = fastSim ? "SEGUIR SIMULANDO" : "CERRAR";
        closeBtn.AddToClassList("btn-modal-cancel");
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _renewalDeadlineModalShown = true;
            _firedOverlay.style.display = DisplayStyle.None;
            _renewalDeadlineResolved = true;
        });
        actions.Add(closeBtn);

        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(rosterBtn);
            CursorManager.Instance.RegisterHandCursor(closeBtn);
        }
    }

    bool IsFeb7OfYearEnd()
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return false;
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return false;
        return date.Month == 2 && date.Day == 7 && date.Year == _season.year_end;
    }

    bool IsFeb12OfYearEnd()
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return false;
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return false;
        return date.Month == 2 && date.Day == 12 && date.Year == _season.year_end;
    }

    bool IsRenewalDeadlinePassed()
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return false;
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return false;
        return date > new DateTime(_season.year_end, 2, 12);
    }

    // ── Despido anticipado ──

    bool ShouldFireManager()
    {
        if (_manager == null) return false;
        bool isPro = _manager.game_mode == "promanager";
        if (isPro && !IsObjectiveMetThisSeason()) return true;
        if (_manager.trust < 10) return true;
        return false;
    }

    bool IsTeamChampion()
    {
        if (_season == null || _myTeam == null) return false;
        string label = $"{_season.year_start}-{_season.year_end.ToString().Substring(2)}";
        var fr = DatabaseManager.Instance.Db.Table<FinalsRecord>()
            .FirstOrDefault(f => f.season == label);
        return fr != null && fr.champ_name == _myTeam.name;
    }

    bool ShowFiredModalIfNeeded()
    {
        if (IsTeamChampion()) return false;
        if (!ShouldFireManager()) return false;
        bool isPro = _manager.game_mode == "promanager";
        if (isPro && !IsObjectiveMetThisSeason())
            ShowObjectiveFiredModal();
        else
            ShowFiredModal();
        return true;
    }

    void MarkManagerFired()
    {
        if (_manager == null) return;

        try
        {
            _manager.is_fired = 1;
            DatabaseManager.Instance.SaveManager(_manager);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Dashboard] No se pudo marcar is_fired: {ex.Message}");
        }

        int slot = DatabaseManager.Instance.ActiveSaveSlot;
        var info = GameSaveManager.GetSlot(slot);
        if (info != null)
        {
            info.managerStatus = "fired";
            GameSaveManager.SaveSlotInfo(info);
        }
    }

    void ShowFiredModal()
    {
        MarkManagerFired();

        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        var title = new Label("DESPIDO");
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        var text = new Label(
            "La directiva ha decidido prescindir de tus servicios.\n\n" +
            "Los resultados de la temporada no han cumplido las expectativas mínimas."
        );
        text.AddToClassList("fired-modal-text");
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");
        var btn = new Button();
        btn.text = "IR AL MENU PRINCIPAL";
        btn.AddToClassList("btn-modal-confirm");
        btn.AddToClassList("btn-modal-confirm--danger");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        actions.Add(btn);
        box.Add(actions);
    }

    void ShowObjectiveFiredModal()
    {
        MarkManagerFired();

        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        var title = new Label("DESPIDO");
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        string obj = _myTeam?.objective ?? "el objetivo de temporada";
        var text = new Label(
            "La directiva ha decidido prescindir de tus servicios.\n\n" +
            $"No has cumplido el objetivo de la temporada: {obj}.\n" +
            "En el modo ProManager, el objetivo es innegociable."
        );
        text.AddToClassList("fired-modal-text");
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");
        var btn = new Button();
        btn.text = "IR AL MENU PRINCIPAL";
        btn.AddToClassList("btn-modal-confirm");
        btn.AddToClassList("btn-modal-confirm--danger");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        actions.Add(btn);
        box.Add(actions);
    }

    /// <summary>Índice de día de partido de una fecha (regla en <see cref="CalendarHelper"/>).</summary>
    static int DateToGameDay(SeasonData season, System.DateTime date)
    {
        return CalendarHelper.DateToGameDay(season.year_start, date);
    }

    /// <summary>
    /// Red de seguridad contra soft-locks de temporada. Se llama cuando no quedan
    /// partidos pendientes pero la fase no es terminal (calendario parcial, datos
    /// heredados de un guardado fallido, play-in sin seeds...): regenera el
    /// calendario si falta o avanza/cierra la fase para que la partida no se quede
    /// bloqueada para siempre.
    /// </summary>
    void TryEscapeStuckPhase()
    {
        if (_season == null || _manager == null) return;

        int Count(string type) => DatabaseManager.Instance.Db.Table<GameData>()
            .Count(g => g.manager_id == _manager.id && g.game_type == type);
        bool NoPending(string type) => !DatabaseManager.Instance.Db.Table<GameData>()
            .Any(g => g.manager_id == _manager.id && g.game_type == type && g.is_played == 0);

        // 1. Temporada regular sin calendario (fallo de guardado heredado): regenerar.
        if ((_season.phase == "preseason" || _season.phase == "regular") && Count("regular") == 0)
        {
            int count = ScheduleGenerator.GenerateSchedule(_season, DatabaseManager.Instance.GetAllTeams());
            if (count > 0)
            {
                try
                {
                    var glTeams = DatabaseManager.Instance.GetGLeagueTeams();
                    if (glTeams.Count > 0)
                        GLeagueScheduleGenerator.GenerateSchedule(_season, glTeams);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Dashboard] Error regenerando calendario G-League: {ex.Message}");
                }

                _season.generated = 1;
                _season.phase = "regular";
                DatabaseManager.Instance.UpdateSeason(_season);
                Debug.LogWarning($"[Dashboard] Temporada sin calendario: regenerados {count} partidos de liga regular.");
            }
            else
            {
                Debug.LogError("[Dashboard] No se pudo regenerar el calendario de la temporada.");
            }
            return;
        }

        // 2. Regular completa pero sin transición hecha.
        if ((_season.phase == "preseason" || _season.phase == "regular") && NoPending("regular"))
        {
            PlayoffsGenerator.GeneratePlayIn(_season, _manager.id);
            _season.phase = "playin";
            DatabaseManager.Instance.UpdateSeason(_season);
            Debug.LogWarning("[Dashboard] Regular sin partidos pendientes: Play-In generado y fase avanzada.");
            return;
        }

        // 3. Play-In completo: generar playoffs o cerrar la temporada si no hay seeds.
        if (_season.phase == "playin" && NoPending("playin"))
        {
            int created = PlayoffsGenerator.GeneratePlayoffs(_season, _manager.id);
            if (created > 0)
            {
                _season.phase = "playoff";
                DatabaseManager.Instance.UpdateSeason(_season);
                Debug.LogWarning($"[Dashboard] Play-In sin partidos pendientes: playoffs generados ({created}).");
            }
            else
            {
                // Sin seeds suficientes no se puede construir el bracket: cerrar la
                // temporada es preferible a dejarla bloqueada para siempre.
                _season.phase = "finished";
                DatabaseManager.Instance.UpdateSeason(_season);
                Debug.LogError("[Dashboard] Play-In sin partidos y sin seeds suficientes: temporada cerrada como 'finished'.");
            }
            return;
        }

        // 4. Playoffs completos.
        if (_season.phase == "playoff" && NoPending("playoff"))
        {
            _season.phase = "finished";
            DatabaseManager.Instance.UpdateSeason(_season);
            Debug.LogWarning("[Dashboard] Playoffs sin partidos pendientes: temporada cerrada.");
        }
    }

    /// <summary>
    /// Día de partido pendiente: el que se simula al avanzar. 0 = no quedan partidos.
    /// Fuente única de verdad: la usa el botón de acción del Dashboard ("DÍA DE PARTIDO")
    /// y la pantalla de previa para saber si el partido que muestra es el de hoy.
    /// </summary>
    int FindNextGameDay()
    {
        return FindNextGameDay(_season, _manager.id);
    }

    /// <summary>
    /// Versión estática de <see cref="FindNextGameDay()"/> para poder consultarla desde
    /// otras pantallas (p. ej. PreGame) sin depender de la instancia del Dashboard.
    /// </summary>
    public static int FindNextGameDay(SeasonData season, int managerId)
    {
        if (season == null) return 0;

        bool anyUnplayed = DatabaseManager.Instance.Db.Table<GameData>()
            .Any(g => g.manager_id == managerId && g.is_played == 0);
        if (!anyUnplayed) return 0;

        // New system: use current_date for day-by-day advancement
        if (!string.IsNullOrEmpty(season.current_date))
        {
            var currentDate = System.DateTime.Parse(season.current_date);

            // Only skip the Feb break if there are no unplayed games on that date
            // (the All-Star game is scheduled during the break, Feb 13)
            bool hasUnplayedToday = DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == managerId
                       && g.game_date == season.current_date
                       && g.is_played == 0);

            if (!hasUnplayedToday && currentDate.Month == 2 && currentDate.Day >= 8 && currentDate.Day <= 14)
                currentDate = new System.DateTime(currentDate.Year, 2, 15);

            return DateToGameDay(season, currentDate);
        }

        // Fallback for old saves without current_date
        if (season.current_game_day == 0)
        {
            var nextPreseason = DatabaseManager.Instance.Db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                         && g.is_played == 0
                         && g.game_day < 0)
                .OrderByDescending(g => g.game_day)
                .FirstOrDefault();
            if (nextPreseason != null) return nextPreseason.game_day;
            return 1;
        }

        return season.current_game_day + 1;
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
            DatabaseManager.Instance.MarkPlayerScouted(_myTeam.id, s.player_id, s.end_day);
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
                    sender_type = 1,
                    sender_id = 0,
                    title = "Entrenamiento completado",
                    body = NewsTexts.Entrenamiento($"{player.first_name} {player.last_name}", attrName, 0, 0),
                    game_day = _season.current_game_day,
                    game_date = _season.current_date,
                    created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    is_read = 0,
                    category = NewsCategory.CLUB,
                    news_type = NewsType.ENTRENAMIENTO
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

    void ProcessGLeagueDevelopment()
    {
        if (_season == null || _season.id <= 0) return;
        var assigned = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id)
            .Where(p => p.g_league_assigned == 1)
            .ToList();
        if (assigned.Count == 0) return;

        // Tick semanal de desarrollo: +1 atributo hasta el potencial. Las
        // estadísticas ya no son procedurales: se acumulan por partido real
        // simulado en ProcessGLeagueGame.
        foreach (var p in assigned)
        {
            if (p.injury_days > 0 || p.is_on_ir == 1) continue;

            if (GLeagueHelper.ProcessDevelopmentTick(p))
                DatabaseManager.Instance.UpdatePlayer(p);
        }
    }

    // ═══════════════════════════════════════════
    //  AI TRANSFERS
    // ═══════════════════════════════════════════


    bool IsDeadlineWeek()
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return false;
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return false;
        return date.Month == 2 && date.Day >= 1 && date.Day <= 8 && date.Year == _season.year_end;
    }

    bool IsTeamContender(TeamData team)
    {
        if (_allGames == null || _allGames.Count == 0) return false;
        var confTeams = _allTeams?.Where(t => t.conference == team.conference).ToList();
        if (confTeams == null || confTeams.Count == 0) return false;
        var standings = BuildStandings(confTeams);
        var top4 = standings.Take(4).ToList();
        return top4.Any(s => s.teamId == team.id);
    }

    // Clasifica la estrategia de un equipo a partir de su clasificación y plantilla.
    TeamStrategy GetTeamStrategy(TeamData team, List<StandingRow> confStandings, List<PlayerData> roster)
    {
        var row = confStandings?.FirstOrDefault(s => s.teamId == team.id);
        int rank = row?.rank ?? 0;
        int confSize = confStandings?.Count ?? 0;

        var stars = roster.Count(p => p.overall >= 85);
        float avgAge = roster.Count > 0 ? (float)roster.Average(p => p.age) : 30f;

        // Contender: top 4 de su conferencia o plantilla con 2+ estrellas (OVR >= 85)
        bool byRecord = rank > 0 && rank <= 4;
        bool byRoster = stars >= 2;
        if (byRecord || byRoster) return TeamStrategy.Contend;

        // Rebuild: últimos 4 de su conferencia o plantilla joven sin estrellas (OVR >= 80)
        bool weakRecord = rank > 0 && rank >= confSize - 3;
        bool youngNoStars = avgAge <= 27f && stars == 0 && roster.Count(p => p.overall >= 80) <= 1;
        if (weakRecord || youngNoStars) return TeamStrategy.Rebuild;

        return TeamStrategy.Balanced;
    }

    // Precalcula la estrategia de todos los equipos una vez por ciclo de traspaso.
    void BuildTeamStrategyCache()
    {
        _teamStrategyCache.Clear();
        if (_allTeams == null || _allTeams.Count == 0) return;

        var eastTeams = _allTeams.Where(t => t.conference == "East").ToList();
        var westTeams = _allTeams.Where(t => t.conference == "West").ToList();
        var eastStandings = BuildStandings(eastTeams);
        var westStandings = BuildStandings(westTeams);

        foreach (var team in _allTeams)
        {
            var standings = team.conference == "East" ? eastStandings : westStandings;
            var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            _teamStrategyCache[team.id] = GetTeamStrategy(team, standings, roster);
        }
    }

    TeamStrategy GetTeamStrategy(int teamId)
    {
        return _teamStrategyCache.TryGetValue(teamId, out var s) ? s : TeamStrategy.Balanced;
    }

    /// <summary>Plan de partido de un equipo: el del usuario es persistente; el de
    /// la IA se deriva de su estrategia de GM.</summary>
    GamePlan PlanFor(int teamId)
    {
        if (_myTeam != null && teamId == _myTeam.id)
        {
            var userPlan = new GamePlan { pace = _myTeam.plan_pace, offense = _myTeam.plan_offense, defense = _myTeam.plan_defense };
            var assistant = GetMyAssistant();
            return CoachScheme.Blend(userPlan, assistant);
        }
        return GamePlan.FromStrategy(GetTeamStrategy(teamId));
    }

    /// <summary>Asistente del equipo del usuario (aporta esquema y bonus).</summary>
    EmployeeData GetMyAssistant()
    {
        if (_myTeam == null) return null;
        return DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id)
            .FirstOrDefault(e => e.position == CoachScheme.POSITION && e.scheme_set == 1);
    }

    /// <summary>Bonus de rating del cuerpo técnico del equipo indicado.</summary>
    int CoachBonusFor(int teamId)
    {
        if (_myTeam == null || teamId != _myTeam.id) return 0;
        var assistant = GetMyAssistant();
        return assistant != null ? CoachScheme.RatingBonus(assistant.reputation) : 0;
    }

    void ProcessAIMarket(int gameDay)
    {
        // Se ejecuta en hilo de fondo; _db (ambient) apunta a la conexión de fondo.
        // El main thread espera la finalización, así que leer/mutar estado de instancia es seguro.
        var db = DatabaseManager.Instance.Db;
        db.BeginTransaction();
        try
        {
            ProcessBuyoutMarket(gameDay);
            ProcessOwnFreeAgentResignings(gameDay);
            ProcessAITransfers(gameDay);
            ProcessStarFreeAgentSignings(gameDay);
            ProcessAIRenewals();
            ProcessPsychologistMorale();
            db.Commit();
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    void ProcessAITransfers(int gameDay)
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return;

        // Only run every ~10 game days (or 3-5 during deadline week)
        int lastDay = _season.last_ai_trade_day;
        int cooldownDays = IsDeadlineWeek() ? _aiRng.Next(3, 6) : 10;
        Debug.Log($"[AITrades] ProcessAITransfers called. gameDay={gameDay} lastDay={lastDay} diff={gameDay - lastDay}");
        if (gameDay - lastDay < cooldownDays) return;

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

        BuildTeamStrategyCache();

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var seasonId = _season.id;
        var freeAgentsAll = DatabaseManager.Instance.GetFreeAgents();
        var tradedThisSeason = DatabaseManager.Instance.GetPlayersTradedThisSeason(seasonId);

        int teamsAttempted = 0, teamsTraded = 0, teamsFAd = 0;
        const int maxTrades = 3;

        foreach (var team in allTeams.OrderBy(_ => _aiRng.Next(0, 1000)))
        {
            if (team.id == _myTeam.id) continue;

            if (teamsTraded >= maxTrades) break;

            var strategy = GetTeamStrategy(team.id);

            // Densidad por estrategia: contender y rebuild se mueven más que balanced
            float moveChance = strategy switch
            {
                TeamStrategy.Contend => 0.45f,
                TeamStrategy.Rebuild => 0.40f,
                _ => 0.25f
            };
            if ((float)_aiRng.NextDouble() > moveChance) continue;

            // Cooldown por equipo según estrategia
            int teamCooldown = strategy switch
            {
                TeamStrategy.Contend => IsDeadlineWeek() ? 3 : 6,
                TeamStrategy.Rebuild => IsDeadlineWeek() ? 3 : 8,
                _ => IsDeadlineWeek() ? 5 : 15
            };
            int lastTeamTrade = _teamTradeCooldown.TryGetValue(team.id, out var lt) ? lt : -10000;
            if (gameDay - lastTeamTrade < teamCooldown) continue;
            _teamTradeCooldown[team.id] = gameDay;

            var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            if (roster.Count < 10) continue;

            var weakestPos = GetWeakestPosition(roster);
            if (weakestPos == null) continue;

            teamsAttempted++;

            // Rebuild: primero intenta vender veteranos (fire sale) para obtener jóvenes/picks
            if (strategy == TeamStrategy.Rebuild && TrySellVeteran(team, roster, seasonId, gameDay, tradedThisSeason))
            {
                teamsTraded++;
                continue;
            }

            // If roster < 12, sign free agent directly without trying trade
            if (roster.Count < 12)
            {
                Debug.Log($"[AITrades] {team.name} roster={roster.Count} < 12, direct FA at {weakestPos}");
                TrySignFreeAgent(team, roster, weakestPos, freeAgentsAll, seasonId, gameDay);
                teamsFAd++;
                continue;
            }

            bool traded = TryFindAITrade(team, roster, weakestPos, seasonId, gameDay, tradedThisSeason);

            if (traded)
            {
                teamsTraded++;
            }
            else if ((float)_aiRng.NextDouble() < 0.3f)
            {
                Debug.Log($"[AITrades] {team.name} trade failed, trying FA at {weakestPos}");
                TrySignFreeAgent(team, roster, weakestPos, freeAgentsAll, seasonId, gameDay);
                teamsFAd++;
            }
        }

        Debug.Log($"[AITrades] Cycle complete: {teamsAttempted} teams attempted, {teamsTraded} trades, {teamsFAd} FA signings");
        GenerateAITradeOffersForPlayer(gameDay, seasonId, tradedThisSeason);
    }

    // Fire sale de un equipo en reconstrucción: ofrece veteranos (edad >= 30, salario alto)
    // a cambio de jugadores jóvenes o picks de equipos contender/balanced.
    // EXENTO a propósito del filtro de valor (TradeValueHelper): en un fire sale el rebuild
    // regala talento a cambio de juventud/rondas; es su plan, no un error de valoración.
    bool TrySellVeteran(TeamData seller, List<PlayerData> sellerRoster, int seasonId, int gameDay, HashSet<int> tradedThisSeason)
    {
        var vets = sellerRoster
            .Where(p => p.injury_days == 0 && p.age >= 30 && p.overall < 87
                        && p.salary >= TradeHelper.MIN_SALARY
                        && !tradedThisSeason.Contains(p.id) && p.is_rookie == 0)
            .OrderByDescending(p => p.salary)
            .ToList();
        if (vets.Count == 0) return false;

        var buyers = DatabaseManager.Instance.GetAllTeams()
            .Where(t => t.id != _myTeam.id && t.id != seller.id)
            .OrderBy(_ => _aiRng.Next(0, 1000))
            .ToList();

        foreach (var vet in vets)
        {
            foreach (var buyer in buyers)
            {
                if (GetTeamStrategy(buyer.id) == TeamStrategy.Rebuild) continue;

                var buyerRoster = DatabaseManager.Instance.GetPlayersByTeam(buyer.id);
                if (buyerRoster.Count < 12) continue;

                // El comprador paga con jóvenes (edad < 25) baratos o con picks
                var young = buyerRoster
                    .Where(p => p.injury_days == 0 && p.age < 25 && p.salary <= vet.salary * 0.8f
                                && !tradedThisSeason.Contains(p.id) && p.is_rookie == 0)
                    .OrderBy(p => p.salary)
                    .ToList();
                if (young.Count == 0) continue;

                var candidate = young[0];
                var youngPack = new List<PlayerData> { candidate };
                if (young.Count >= 2 && (float)_aiRng.NextDouble() < 0.4f)
                    youngPack.Add(young[1]);

                var futurePicks = GetFuturePicksForTeam(buyer.id, true);
                var picks = new List<DraftPickData>();
                if (futurePicks.Count > 0 && (float)_aiRng.NextDouble() < 0.5f)
                {
                    picks.Add(futurePicks[0]);
                    if (futurePicks.Count > 1 && (float)_aiRng.NextDouble() < 0.4f)
                        picks.Add(futurePicks[1]);
                }

                if (TryExecuteTrade(seller, sellerRoster, buyer, buyerRoster,
                        new List<PlayerData> { vet },
                        youngPack,
                        seasonId, gameDay,
                        aSelectedPicks: null,
                        bSelectedPicks: picks))
                {
                    tradedThisSeason.Add(vet.id);
                    foreach (var yp in youngPack) tradedThisSeason.Add(yp.id);
                    Debug.Log($"[AITrades] {seller.name} vende a {vet.first_name} {vet.last_name} ({vet.age} años) a {buyer.name} por jóvenes/picks (rebuild fire sale)");
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Mercado de buyout: los jugadores rescindidos que siguen libres reciben destino de la
    /// IA en cuanto pasa la jornada de su rescisión. Firman por el mínimo y se genera noticia.
    /// Se ejecuta en el lote de fondo (conexión ambient), antes de los demás pases de FA.
    /// </summary>
    void ProcessBuyoutMarket(int gameDay)
    {
        if (_season == null || _myTeam == null) return;

        // Solo los jugadores de 75+ de media tienen destino inmediato: un rescindido de
        // 68 se queda en agentes libres y decide el mercado general.
        var waived = DatabaseManager.Instance.GetFreeAgents()
            .Where(p => BuyoutMarketRules.CanSignOn(p.waived_game_day, gameDay)
                     && BuyoutMarketRules.IsWanted(p.GetCalculatedAverage()))
            .OrderByDescending(p => p.GetCalculatedAverage())
            .ToList();
        if (waived.Count == 0) return;

        var settings = DatabaseManager.Instance.GetLeagueSettings();
        long minSalary = BuyoutMarketRules.WaivedSalary(settings?.minimum_salary ?? 0);
        var teams = DatabaseManager.Instance.GetAllTeams().Where(t => t.id != _myTeam.id).ToList();
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (var p in waived)
        {
            var candidates = new List<BuyoutCandidate>();
            foreach (var t in teams)
            {
                var roster = DatabaseManager.Instance.GetPlayersByTeam(t.id);
                long payroll = roster.Sum(x => x.salary);
                bool hasRoom = DatabaseManager.Instance.GetRosterCount(t.id) < TradeHelper.MAX_ROSTER
                            && TradeHelper.CanAISignExternal(payroll, minSalary, settings);

                candidates.Add(new BuyoutCandidate
                {
                    teamId = t.id,
                    strategy = GetTeamStrategy(t.id),
                    teamAvg = roster.Count > 0 ? (float)roster.Average(x => x.GetCalculatedAverage()) : 0f,
                    samePosCount = roster.Count(x => x.position == p.position),
                    reputation = t.reputation,
                    hasRoom = hasRoom
                });
            }

            // Entre los mejores postores (contender > balanced > rebuild, con hueco en su
            // posición y mejor proyecto/prestigio) se sortea el destino ponderadamente: el
            // mejor candidato es el más probable, pero no siempre el elegido.
            var ranked = BuyoutMarketRules.RankDestinations(candidates);
            int pick = BuyoutMarketRules.PickSuitor(ranked.Count, _aiRng);
            if (pick < 0) continue;

            var team = teams.FirstOrDefault(t => t.id == ranked[pick].teamId);
            if (team == null) continue;

            p.team_id = team.id;
            p.last_team_id = team.id;
            p.salary = minSalary;
            // Firma de mínimo de 1 año (mercado de buyout): al expirar, su equipo puede
            // renovarle por su valor real o pierde sus derechos al abrirse el mercado.
            p.contract_years = BuyoutMarketRules.ContractYears;
            p.guaranteed_years = BuyoutMarketRules.ContractYears;
            p.has_team_option = 0;
            p.has_player_option = 0;
            p.waived_game_day = -1;
            DatabaseManager.Instance.AssignJerseyNumber(p, team.id);
            DatabaseManager.Instance.UpdatePlayer(p);

            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = _season.id,
                game_day = gameDay,
                game_date = _season.current_date,
                team_id_from = 0,
                team_id_to = team.id,
                player_id = p.id,
                trade_type = "free_agent"
            });

            string playerName = $"{p.first_name} {p.last_name}";
            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 2,
                sender_id = 0,
                title = $"Mercado de buyout: {playerName} firma con {team.name}",
                body = NewsTexts.MercadoBuyout(playerName, team.name),
                game_day = gameDay,
                game_date = _season.current_date,
                created_at = now,
                date_sent = now,
                is_read = 0,
                category = NewsCategory.LIGA,
                news_type = NewsType.FICHAJE
            });

            Debug.Log($"[BuyoutMarket] {team.name} firma a {playerName} (OVR {p.GetCalculatedAverage()}) por el mínimo " +
                      $"[postor {pick + 1}/{ranked.Count}]");
        }
    }

    /// <summary>
    /// Re-firma de **agentes libres propios con Bird rights**: un FA reciente cuyo último
    /// equipo conserva sus derechos (expiró contrato o declinó su opción) puede volver a él
    /// con un contrato acorde a su valor de mercado, aunque el equipo esté por encima del
    /// tope (límite del 2º apron, como las renovaciones). Es lo que permite que un jugador
    /// que firmó por el mínimo tras una rescisión cobre después lo que le corresponde.
    /// Se ejecuta en el lote de fondo, antes de los demás pases de FA.
    /// </summary>
    void ProcessOwnFreeAgentResignings(int gameDay)
    {
        if (_season == null || _myTeam == null) return;

        var settings = DatabaseManager.Instance.GetLeagueSettings();
        long cap = settings?.salary_cap ?? TradeHelper.SALARY_CAP;

        var candidates = DatabaseManager.Instance.GetFreeAgents()
            .Where(p => p.last_team_id > 0 && p.last_team_id != _myTeam.id && p.seasons_with_team > 0)
            .OrderByDescending(p => p.GetCalculatedAverage())
            .ToList();
        if (candidates.Count == 0) return;

        Dictionary<int, int> gamesByPlayer = null;

        foreach (var p in candidates)
        {
            if (p.renewal_cooldown_day > gameDay) continue;

            var team = DatabaseManager.Instance.GetTeamById(p.last_team_id);
            if (team == null) continue;
            if (DatabaseManager.Instance.GetRosterCount(team.id) >= TradeHelper.MAX_ROSTER) continue;
            if (CalculateTeamRenewalScore(p, team, cap) < 50f) continue;

            var offer = CalculateAIRenewalOffer(p, team, cap);
            long payroll = DatabaseManager.Instance.GetPlayersByTeam(team.id).Sum(x => x.salary);

            // Como en las renovaciones: nunca dejar al equipo por encima del 2º apron.
            if (!TradeHelper.CanAIRenew(payroll + offer.salary, settings))
            {
                p.renewal_cooldown_day = gameDay + 15;
                DatabaseManager.Instance.UpdatePlayer(p);
                continue;
            }

            gamesByPlayer ??= DatabaseManager.Instance.GetGamesPlayedByPlayerInSeason(_manager.id, _season.id);
            int gamesPlayed = gamesByPlayer.GetValueOrDefault(p.id, 0);
            int teamChem = DatabaseManager.Instance.GetTeamChemistry(team.id);
            float acceptScore = RosterController.CalculateAcceptScore(
                p, offer.salary, offer.years, gamesPlayed, teamChem);

            if (_aiRng.Next(1, 101) > acceptScore)
            {
                p.renewal_cooldown_day = gameDay + 15;
                DatabaseManager.Instance.UpdatePlayer(p);
                continue;
            }

            p.team_id = team.id;
            p.last_team_id = team.id;
            p.salary = offer.salary;
            p.contract_years = offer.years;
            p.guaranteed_years = offer.teamOption ? offer.years - 1 : offer.years;
            p.has_team_option = offer.teamOption ? 1 : 0;
            p.has_player_option = 0;
            p.waived_game_day = -1;
            p.renewal_cooldown_day = gameDay + 365;
            DatabaseManager.Instance.AssignJerseyNumber(p, team.id);
            DatabaseManager.Instance.UpdatePlayer(p);

            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = _season.id,
                game_day = gameDay,
                game_date = _season.current_date,
                team_id_from = 0,
                team_id_to = team.id,
                player_id = p.id,
                trade_type = "free_agent"
            });

            if (p.overall >= 90) AddAIRenewalNotification(p, team, true, offer);
            Debug.Log($"[OwnFA] {team.name} re-firma a {p.first_name} {p.last_name} " +
                      $"(OVR {p.overall}) por {offer.salary:N0} $ / {offer.years} año(s) con Bird rights");
        }
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
        var allTeams = DatabaseManager.Instance.GetAllTeams()
            .Where(t => t.id != _myTeam.id)
            .ToList();

        var teamAvgs = new Dictionary<int, float>();
        var teamStrategies = new Dictionary<int, TeamStrategy>();
        foreach (var team in allTeams)
        {
            var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            teamAvgs[team.id] = roster.Count > 0 ? (float)roster.Average(p => p.GetCalculatedAverage()) : 0f;
            teamStrategies[team.id] = GetTeamStrategy(team.id);
        }
        // Prioridad de destino: contender > balanced > rebuild (los rebuild tankean y
        // preservan el pick alto del draft).
        var teamsByPriority = allTeams
            .OrderByDescending(t => teamStrategies[t.id] == TeamStrategy.Contend ? 3
                                   : teamStrategies[t.id] == TeamStrategy.Balanced ? 2 : 1)
            .ThenByDescending(t => teamAvgs[t.id])
            .ToList();

        var pendingFAIds = DatabaseManager.Instance.GetPendingFAPlayerIds(_manager.id);
        var pendingSATIds = GetPendingSATIds();

        foreach (var star in stars)
        {
            if (pendingFAIds.Contains(star.id))
            {
                Debug.Log($"[StarFA] Skip {star.first_name} {star.last_name} — user has pending offer");
                continue;
            }

            if (pendingSATIds.Contains(star.id))
            {
                Debug.Log($"[StarFA] Skip {star.first_name} {star.last_name} — user has pending sign-and-trade offer");
                continue;
            }

            bool signed = false;
            foreach (var team in teamsByPriority)
            {
                var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
                if (DatabaseManager.Instance.GetRosterCount(team.id) >= TradeHelper.MAX_ROSTER) continue;

                // Un equipo en reconstrucción no ficha estrellas top (tankea para el draft)
                if (star.GetCalculatedAverage() >= 85 && teamStrategies[team.id] == TeamStrategy.Rebuild) continue;

                long payroll = roster.Sum(p => p.salary);
                bool fromBuyout = star.waived_game_day >= 0;

                // Salario nuevo: un rescindido firma por el mínimo (mercado de buyout); el
                // resto, su precio + un pequeño incremento (como el pase general). Antes se
                // firmaba con el salario viejo y el equipo nuevo heredaba esa nómina.
                long newSalary = fromBuyout
                    ? BuyoutMarketRules.WaivedSalary(leagueSettings?.minimum_salary ?? 0)
                    : star.salary + 2_000_000;

                // La IA ficha con sus excepciones (espacio → MLE → mínimo), no solo con
                // espacio de tope: antes ninguna estrella por encima del cap podía firmar.
                if (!TradeHelper.CanAISignExternal(payroll, newSalary, leagueSettings)) continue;

                star.team_id = team.id;
                star.last_team_id = team.id;
                star.salary = newSalary;
                // El rescindido firma 1 año (y al expirar su equipo puede renovarle por su
                // valor real, o re-firmarle con Bird rights).
                int years = fromBuyout ? BuyoutMarketRules.ContractYears
                          : star.age > 35 ? 1 : star.age > 32 ? 2 : star.age > 28 ? 3 : star.age > 25 ? 4 : 5;
                star.contract_years = years;
                star.guaranteed_years = years;
                star.has_team_option = 0;
                star.has_player_option = 0;
                star.waived_game_day = -1;
                DatabaseManager.Instance.AssignJerseyNumber(star, team.id);
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

    // ── AI RENEWALS ────────────────────────────────────────────────

    void ProcessAIRenewals()
    {
        if (_airRenewalsProcessed) return;
        if (!IsFeb12OfYearEnd() && !IsRenewalDeadlinePassed()) return;
        _airRenewalsProcessed = true;

        Debug.Log("[AIRenewals] Processing AI contract renewals...");

        var db = DatabaseManager.Instance.Db;
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long cap = leagueSettings.salary_cap;
        int renewedCount = 0, lostCount = 0;

        // Una sola query agregada en vez de una por jugador (N+1).
        var gamesByPlayer = DatabaseManager.Instance.GetGamesPlayedByPlayerInSeason(_manager.id, _season.id);

        foreach (var team in allTeams)
        {
            if (team.id == _myTeam.id) continue;

            // Nómina en curso del equipo (se actualiza al renovar).
            long teamPayroll = DatabaseManager.Instance.GetPlayersByTeam(team.id).Sum(p => p.salary);

            var expiringPlayers = db.Query<PlayerData>(
                "SELECT * FROM players WHERE team_id = ? AND contract_years = 1 AND is_two_way = 0 AND is_on_ir = 0",
                team.id);

            foreach (var player in expiringPlayers)
            {
                float teamScore = CalculateTeamRenewalScore(player, team, cap);
                if (teamScore < 50f) continue;

                var offer = CalculateAIRenewalOffer(player, team, cap);

                // No renovar si deja al equipo por encima del 2º apron (evita que las
                // nóminas IA se disparen temporada a temporada).
                long projected = teamPayroll - player.salary + offer.salary;
                if (!TradeHelper.CanAIRenew(projected, leagueSettings))
                {
                    player.renewal_cooldown_day = _season.current_game_day + 15;
                    db.Update(player);
                    lostCount++;
                    if (player.overall >= 90)
                        AddAIRenewalNotification(player, team, false, offer);
                    continue;
                }

                int gamesPlayed = gamesByPlayer.GetValueOrDefault(player.id, 0);
                int teamChem = DatabaseManager.Instance.GetTeamChemistry(team.id);
                float acceptScore = RosterController.CalculateAcceptScore(
                    player, offer.salary, offer.years, gamesPlayed, teamChem);

                bool accepted = _aiRng.Next(1, 101) <= acceptScore;

                if (accepted)
                {
                    player.salary = offer.salary;
                    player.contract_years = offer.years;
                    player.guaranteed_years = offer.teamOption ? offer.years - 1 : offer.years;
                    player.has_team_option = offer.teamOption ? 1 : 0;
                    player.has_player_option = 0;
                    player.renewal_cooldown_day = _season.current_game_day + 365;
                    db.Update(player);
                    teamPayroll = projected;
                    renewedCount++;

                    if (player.overall >= 90)
                        AddAIRenewalNotification(player, team, true, offer);
                }
                else
                {
                    player.renewal_cooldown_day = _season.current_game_day + 15;
                    db.Update(player);
                    lostCount++;

                    if (player.overall >= 90)
                        AddAIRenewalNotification(player, team, false, offer);
                }
            }
        }

        Debug.Log($"[AIRenewals] Done. Renewed: {renewedCount}, Lost: {lostCount}");
    }

    float CalculateTeamRenewalScore(PlayerData player, TeamData team, long cap)
    {
        float score = 0f;

        // OVR (40%)
        if (player.overall >= 85) score += 40f;
        else if (player.overall >= 80) score += 32f;
        else if (player.overall >= 75) score += 24f;
        else if (player.overall >= 70) score += 16f;
        else score += 8f;

        // Rol (20%)
        if (player.role == PlayerRole.Estrella) score += 20f;
        else if (player.role == PlayerRole.Titular) score += 15f;
        else if (player.role == PlayerRole.Banquillo) score += 5f;

        // Edad (15%) — equipos prefieren jóvenes
        if (player.age <= 25) score += 15f;
        else if (player.age <= 30) score += 10f;
        else if (player.age <= 34) score += 5f;

        // Salario vs valor estimado (15%)
        long estimatedValue = EstimateMarketValue(player, cap);
        if (player.salary < estimatedValue) score += 15f;
        else if (player.salary <= estimatedValue * 1.2f) score += 8f;

        // Estrategia del equipo (10%)
        var strategy = GetTeamStrategy(team.id);
        if (strategy == TeamStrategy.Contend && player.overall >= 75) score += 10f;
        else if (strategy == TeamStrategy.Rebuild && player.overall < 75 && player.age <= 28) score += 10f;
        else score += 5f;

        return Mathf.Clamp(score, 0f, 100f);
    }

    long EstimateMarketValue(PlayerData player, long cap)
    {
        // Umbrales por OVR en una sola fuente (TradeHelper); por debajo, el salario actual.
        long byOvr = TradeHelper.MarketValueByOvr(player.overall, cap);
        if (byOvr > 0) return byOvr;
        if (player.overall >= 70) return (long)Mathf.Max(player.salary * 110 / 100, TradeHelper.MIN_SALARY);
        return (long)Mathf.Max(player.salary * 105 / 100, TradeHelper.MIN_SALARY);
    }

    (long salary, int years, bool teamOption) CalculateAIRenewalOffer(
        PlayerData player, TeamData team, long cap)
    {
        long maxByExp;
        int experience = player.age - 22;
        if (experience <= 6) maxByExp = cap / 4;
        else if (experience <= 9) maxByExp = (long)(cap * 0.30);
        else maxByExp = (long)(cap * 0.35);

        long marketValue = EstimateMarketValue(player, cap);
        long salary = (long)Mathf.Min(maxByExp, marketValue);

        // Muy por debajo de su valor de mercado (p. ej. fichó por el mínimo tras una
        // rescisión): el equipo le renueva a su valor real (para una estrella, su máximo
        // por experiencia), sin el tope no-Bird del 120 % sobre el salario mínimo.
        if (marketValue >= player.salary * 3)
        {
            salary = (long)(Mathf.Round(salary / 100_000f) * 100_000);
            if (salary < TradeHelper.MIN_SALARY) salary = TradeHelper.MIN_SALARY;

            int yearsLow;
            if (player.age <= 25) yearsLow = 5;
            else if (player.age <= 28) yearsLow = 4;
            else if (player.age <= 32) yearsLow = 3;
            else if (player.age <= 35) yearsLow = 2;
            else yearsLow = 1;
            return (salary, yearsLow, player.age >= 30);
        }

        if (player.seasons_with_team >= 3)
        {
            salary = (long)Mathf.Min(salary, maxByExp);
        }
        else if (player.seasons_with_team == 2)
        {
            long earlyBirdMax = (long)Mathf.Max(player.salary * 175 / 100, (long)(cap * 1.05));
            salary = (long)Mathf.Min(salary, earlyBirdMax);
        }
        else
        {
            long noBirdMax = player.salary * 120 / 100;
            salary = (long)Mathf.Min(salary, noBirdMax);
        }

        salary = (long)(Mathf.Round(salary / 100_000f) * 100_000);
        if (salary < TradeHelper.MIN_SALARY) salary = TradeHelper.MIN_SALARY;

        int years;
        if (player.age <= 25) years = 5;
        else if (player.age <= 28) years = 4;
        else if (player.age <= 32) years = 3;
        else if (player.age <= 35) years = 2;
        else years = 1;

        bool teamOption = player.age >= 30;

        return (salary, years, teamOption);
    }

    void AddAIRenewalNotification(PlayerData player, TeamData team, bool accepted,
        (long salary, int years, bool teamOption) offer)
    {
        string playerName = $"{player.first_name} {player.last_name}";
        string teamName = team.name;
        string salaryStr = $"${offer.salary:N0}";
        string yearsText = offer.years == 1 ? "1 temporada" : $"{offer.years} temporadas";
        string optionText = offer.teamOption ? " (con opción de equipo)" : "";

        if (accepted)
        {
            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 2,
                sender_id = 0,
                title = $"Renovación estrella: {playerName}",
                body = $"{teamName} ha renovado a {playerName} (OVR {player.overall}) por {yearsText} a {salaryStr}/año{optionText}.",
                game_day = _season.current_game_day,
                game_date = _season.current_date,
                created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = 0,
                category = NewsCategory.TRASPASOS,
                news_type = NewsType.RENOVACION
            });
        }
        else
        {
            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 2,
                sender_id = 0,
                title = $"Estrella sin renovar: {playerName}",
                body = $"{playerName} (OVR {player.overall}) no ha aceptado la renovación de {teamName}. El jugador saldrá a mercado libre al final de temporada.",
                game_day = _season.current_game_day,
                game_date = _season.current_date,
                created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = 0,
                category = NewsCategory.TRASPASOS,
                news_type = NewsType.OFERTA_RECHAZADA
            });
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
                        int seasonId, int gameDay, HashSet<int> tradedThisSeason)
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams()
            .Where(t => t.id != _myTeam.id && t.id != teamA.id)
            .OrderBy(_ => _aiRng.Next(0, 1000))
            .ToList();

        // Rondas disponibles del equipo que propone (una sola consulta para todo el ciclo).
        var aPickPool = GetFuturePicksForTeam(teamA.id, true);

        foreach (var teamB in allTeams)
        {
            var rosterB = DatabaseManager.Instance.GetPlayersByTeam(teamB.id);

            // Los contenders buscan mejoras más fuertes (sin tope de 86) y pueden incluir picks
            var teamAStrategy = GetTeamStrategy(teamA.id);
            int targetCap = teamAStrategy == TeamStrategy.Contend ? 90 : 86;

            var target = rosterB
                .Where(p => p.position == targetPos && p.injury_days == 0 && p.is_rookie == 0)
                .OrderByDescending(p => p.overall)
                .FirstOrDefault(p => p.overall <= targetCap);
            if (target == null) continue;

            var candidates = rosterA
                .Where(p => p.injury_days == 0 && p.id != target.id
                            && !tradedThisSeason.Contains(p.id) && p.is_rookie == 0
                            && rosterA.Count(r => r.position == p.position) >= (
                                p.position == targetPos ? 1 : 2))
                .OrderBy(p => p.overall)
                .ToList();
            if (candidates.Count == 0) continue;

            // Protección universal: estrellas (OVR >= 87) nunca se ofrecen.
            // Contender además protege jóvenes prometedores (edad < 26 y OVR >= 82).
            List<PlayerData> tradeableCandidates = candidates
                .Where(p => p.overall < 87).ToList();
            if (teamAStrategy == TeamStrategy.Contend)
                tradeableCandidates = tradeableCandidates.Where(p => !(p.age < 26 && p.overall >= 82)).ToList();

            if (tradeableCandidates.Count == 0) continue;

            // Los contender suelen añadir un pick futuro como "sweetener" (R1 + a veces R2)
            List<DraftPickData> teamAPicks = null;
            if (teamAStrategy == TeamStrategy.Contend && target.overall > 84 && aPickPool.Count > 0)
            {
                teamAPicks = aPickPool
                    .Take((float)_aiRng.NextDouble() < 0.4f && aPickPool.Count > 1 ? 2 : 1)
                    .ToList();
            }

            // Try 1-for-1 (de peor a mejor jugador): sólo si el traspaso es razonable para
            // los DOS equipos (nadie regala talento sin compensación).
            foreach (var c in tradeableCandidates)
            {
                var aGives = new List<PlayerData> { c };
                var aPicks = teamAPicks;
                if (!IsAITradeFair(teamA, aGives, teamB, new List<PlayerData> { target }, ref aPicks, aPickPool)) continue;
                if (TryExecuteTrade(teamA, rosterA, teamB, rosterB,
                        aGives,
                        new List<PlayerData> { target },
                        seasonId, gameDay,
                        aSelectedPicks: aPicks.Count > 0 ? aPicks : null))
                {
                    tradedThisSeason.Add(c.id);
                    tradedThisSeason.Add(target.id);
                    return true;
                }
            }

            // Try 2-for-1
            for (int i = 0; i < tradeableCandidates.Count; i++)
            {
                for (int j = i + 1; j < tradeableCandidates.Count; j++)
                {
                    var aGives = new List<PlayerData> { tradeableCandidates[i], tradeableCandidates[j] };
                    var aPicks = teamAPicks;
                    if (!IsAITradeFair(teamA, aGives, teamB, new List<PlayerData> { target }, ref aPicks, aPickPool)) continue;
                    if (TryExecuteTrade(teamA, rosterA, teamB, rosterB,
                            aGives,
                            new List<PlayerData> { target },
                            seasonId, gameDay,
                            aSelectedPicks: aPicks.Count > 0 ? aPicks : null))
                    {
                        tradedThisSeason.Add(tradeableCandidates[i].id);
                        tradedThisSeason.Add(tradeableCandidates[j].id);
                        tradedThisSeason.Add(target.id);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// ¿El traspaso IA-IA es razonable para LOS DOS equipos? (Ninguno entrega mucho más
    /// valor del que recibe ni un jugador claramente mejor que el que se lleva.) Si tal
    /// cual no cuadra, el equipo que lo propone intenta compensar con una ronda; si aun
    /// así no cuadra, no hay trato. El fire-sale de <see cref="TrySellVeteran"/> queda
    /// fuera a propósito: ahí un rebuild SÍ regala veteranos por jóvenes/picks.
    /// </summary>
    bool IsAITradeFair(TeamData teamA, List<PlayerData> aGives, TeamData teamB, List<PlayerData> bGives,
                       ref List<DraftPickData> aPicks, List<DraftPickData> pickPool)
    {
        long cap = GetValueCap();
        int draftYear = _season?.year_end ?? 0;
        var strategyA = GetTeamStrategy(teamA.id);
        var strategyB = GetTeamStrategy(teamB.id);

        var picks = aPicks != null ? new List<DraftPickData>(aPicks) : new List<DraftPickData>();

        bool Fair(List<DraftPickData> current) =>
            TradeValueHelper.IsSaneBothWays(strategyA, aGives, current, strategyB, bGives, null, cap, draftYear);

        if (Fair(picks)) { aPicks = picks; return true; }

        // El "sweetener" de contender puede romper el equilibrio: primero sin él.
        if (picks.Count > 0)
        {
            picks.Clear();
            if (Fair(picks)) { aPicks = picks; return true; }
        }

        // Compensar con una ronda del que propone el traspaso.
        if (pickPool != null)
        {
            foreach (var pk in pickPool)
            {
                if (picks.Any(x => x.id == pk.id)) continue;
                picks.Add(pk);
                if (Fair(picks)) { aPicks = picks; return true; }
                picks.Remove(pk);
            }
        }
        return false;
    }

    List<DraftPickData> GetFuturePicksForTeam(int teamId, bool includeRound2)
    {
        var all = DatabaseManager.Instance.GetDraftPicksForTeam(teamId);
        if (all.Count == 0) return all;

        // La IA ofrece un año aleatorio de entre los que posee el equipo.
        var years = all.Select(p => p.draft_year).Distinct().ToList();
        int chosenYear = years[_aiRng.Next(years.Count)];
        var rounded = all.Where(p => p.draft_year == chosenYear).ToList();

        var r1 = rounded.Where(p => p.round == 1).Take(1).ToList();
        if (!includeRound2)
            return r1;
        return r1.Concat(rounded.Where(p => p.round == 2).Take(1)).ToList();
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
            p.g_league_assigned = 0;
            p.is_on_ir = 0;
            p.is_two_way = 0;
            p.on_trade_block = 0;
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
            p.g_league_assigned = 0;
            p.is_on_ir = 0;
            p.is_two_way = 0;
            p.on_trade_block = 0;
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

        foreach (var p in aSelected)
        {
            DatabaseManager.Instance.AssignJerseyNumber(p, teamB.id);
            DatabaseManager.Instance.UpdatePlayer(p);
        }
        foreach (var p in bSelected)
        {
            DatabaseManager.Instance.AssignJerseyNumber(p, teamA.id);
            DatabaseManager.Instance.UpdatePlayer(p);
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
        int rosterCount = DatabaseManager.Instance.GetRosterCount(team.id);
        int twoWayCount = DatabaseManager.Instance.GetTwoWayCount(team.id);
        if (rosterCount >= TradeHelper.MAX_ROSTER && twoWayCount >= TradeHelper.MAX_TWO_WAY) return;

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long aiPayroll = roster.Sum(p => p.salary);

        var pendingFAIds = DatabaseManager.Instance.GetPendingFAPlayerIds(_manager.id);
        var pendingSATIds = GetPendingSATIds();

        var candidates = freeAgents
            .Where(p => p.position == targetPos && p.salary <= team.budget && !pendingFAIds.Contains(p.id) && !pendingSATIds.Contains(p.id))
            .OrderByDescending(p => p.overall)
            .ToList();

        if (candidates.Count == 0)
        {
            // Try any position if no match at targetPos
            candidates = freeAgents
                .Where(p => p.salary <= team.budget && !pendingFAIds.Contains(p.id) && !pendingSATIds.Contains(p.id))
                .OrderByDescending(p => p.overall)
                .ToList();
        }

        foreach (var player in candidates)
        {
            // Si la plantilla está llena solo se puede fichar con un contrato two-way
            bool signAsTwoWay = rosterCount >= TradeHelper.MAX_ROSTER;
            if (signAsTwoWay)
            {
                if (twoWayCount >= TradeHelper.MAX_TWO_WAY) return;
                if (!TradeHelper.IsEligibleForTwoWay(player)) continue;
            }

            int chance = (int)Mathf.Clamp(team.reputation * 20 - player.overall * 0.5f + 30, 5, 95);
            if (_aiRng.Next(0, 100) >= chance) continue;

            // Los two-way no computan contra el cap (igual que para el usuario).
            long minSalary = leagueSettings?.minimum_salary > 0 ? leagueSettings.minimum_salary : TradeHelper.MIN_SALARY;
            long newSalary = signAsTwoWay ? TradeHelper.TWO_WAY_SALARY
                           : player.waived_game_day >= 0 ? minSalary      // mercado de buyout
                           : player.salary + 2_000_000;
            if (!signAsTwoWay && !TradeHelper.CanAISignExternal(aiPayroll, newSalary, leagueSettings))
                continue; // no cabe en el cap/excepciones: probar otro candidato

            player.team_id = team.id;
            player.last_team_id = team.id;
            if (signAsTwoWay)
            {
                player.is_two_way = 1;
                player.salary = TradeHelper.TWO_WAY_SALARY;
                player.contract_years = 2;
                player.guaranteed_years = 2;
            }
            else
            {
                int years = player.waived_game_day >= 0 ? BuyoutMarketRules.ContractYears
                          : player.age > 35 ? 1 : player.age > 32 ? 2 : player.age > 28 ? 3 : player.age > 25 ? 4 : 5;
                player.salary = newSalary;
                player.contract_years = years;
                player.guaranteed_years = years;
                player.waived_game_day = -1;
                aiPayroll += newSalary;
            }
            DatabaseManager.Instance.AssignJerseyNumber(player, team.id);
            DatabaseManager.Instance.UpdatePlayer(player);

            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = seasonId,
                game_day = gameDay,
                game_date = _season.current_date,
                team_id_from = 0,
                team_id_to = team.id,
                player_id = player.id,
                trade_type = signAsTwoWay ? "free_agent_two_way" : "free_agent"
            });

            Debug.Log($"[AI FA] {team.name} signed {player.first_name} {player.last_name} ({player.position}, {player.overall} OVR){(signAsTwoWay ? " — TWO-WAY" : "")}");
            break;
        }
    }

    // ═══════════════════════════════════════════
    //  AI TRADE OFFERS TO PLAYER
    // ═══════════════════════════════════════════

    HashSet<int> GetPendingSATIds()
    {
        var result = new HashSet<int>();
        var pending = DatabaseManager.Instance.GetPendingTradeOffers(_manager.id);
        var freeAgents = DatabaseManager.Instance.GetFreeAgents()
            .Where(p => DatabaseManager.Instance.IsOwnRecentFA(p, _myTeam.id))
            .ToDictionary(p => p.id, p => p);
        foreach (var offer in pending)
        {
            foreach (var id in offer.GetWantedPlayerIds())
            {
                if (freeAgents.ContainsKey(id))
                    result.Add(id);
            }
        }
        return result;
    }

    void GenerateAITradeOffersForPlayer(int gameDay, int seasonId, HashSet<int> tradedThisSeason)
    {
        var existingPending = DatabaseManager.Instance.GetPendingTradeOffers(_manager.id);
        if (existingPending.Count > 0) return;

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var targetedIds = new HashSet<int>();

        foreach (var aiTeam in allTeams.OrderBy(_ => _aiRng.Next(0, 1000)))
        {
            if (aiTeam.id == _myTeam.id) continue;
            if ((float)_aiRng.NextDouble() > 0.50f) continue;

            var aiRoster = DatabaseManager.Instance.GetPlayersByTeam(aiTeam.id);
            if (aiRoster.Count < 11) continue;

            var userRoster = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            int userRosterCount = DatabaseManager.Instance.GetRosterCount(_myTeam.id);
            if (userRoster.Count < 10 || userRosterCount >= TradeHelper.MAX_ROSTER) continue;

            var userHealthy = userRoster.Where(p => p.injury_days == 0).ToList();
            if (userHealthy.Count == 0) continue;

            // S&T de IA: proponer traspasar un FA reciente del usuario (Bird rights)
            if ((float)_aiRng.NextDouble() > 0.55f)
            {
                var userFAs = DatabaseManager.Instance.GetFreeAgents()
                    .Where(p => DatabaseManager.Instance.IsOwnRecentFA(p, _myTeam.id) && p.GetCalculatedAverage() > 75)
                    .OrderByDescending(p => p.GetCalculatedAverage())
                    .ToList();
                var faTarget = userFAs.FirstOrDefault(p => !targetedIds.Contains(p.id));
                if (faTarget != null)
                {
                    targetedIds.Add(faTarget.id);
                    var aiHealthyForSA = aiRoster
                        .Where(p => p.injury_days == 0 && p.overall < 87
                                    && !tradedThisSeason.Contains(p.id) && p.is_rookie == 0)
                        .ToList();
                    if (aiHealthyForSA.Count > 0)
                    {
                        long cap = GetValueCap();
                        int draftYear = _season?.year_end ?? 0;
                        double faValue = TradeValueHelper.PlayerValue(faTarget, cap);

                        // Valor: nunca ofrecer a alguien claramente mejor que el FA pedido
                        // y buscar el paquete de valor más parecido (antes: salario parecido,
                        // que no dice nada del talento).
                        var faPack = aiHealthyForSA
                            .Where(p => p.overall <= faTarget.overall + TradeValueHelper.MaxOvrGap)
                            .OrderBy(p => Math.Abs(TradeValueHelper.PlayerValue(p, cap) - faValue))
                            .Take(2)
                            .ToList();
                        if (faPack.Count > 0)
                        {
                            var futurePicks = GetFuturePicksForTeam(aiTeam.id, true);
                            var saPicks = new List<DraftPickData>();
                            if (futurePicks.Count > 0 && (float)_aiRng.NextDouble() > 0.4f)
                                saPicks.Add(futurePicks[0]);

                            var saCheck = TradeValueHelper.CheckOffer(GetTeamStrategy(aiTeam.id), faPack, saPicks,
                                new List<PlayerData> { faTarget }, null, cap, draftYear);
                            if (saCheck.Sane && saCheck.Outgoing >= saCheck.Incoming * TradeValueHelper.MinOfferRatio)
                            {
                                DatabaseManager.Instance.AddTradeOffer(new TradeOfferData
                                {
                                    manager_id = _manager.id,
                                    team_id_from = aiTeam.id,
                                    player_ids_out = TradeOfferData.JoinIds(new List<int> { faTarget.id }),
                                    player_ids_in = TradeOfferData.JoinIds(faPack.Select(p => p.id).ToList()),
                                    pick_ids_out = TradeOfferData.JoinIds(new List<int>()),
                                    pick_ids_in = TradeOfferData.JoinIds(saPicks.Select(pk => pk.id).ToList()),
                                    day_sent = gameDay,
                                    processed = 0
                                });
                                Debug.Log($"[AITrades] {aiTeam.name} propone S&T por {faTarget.first_name} {faTarget.last_name} (FA propio del usuario)");
                                break;
                            }
                            Debug.Log($"[AITrades] {aiTeam.name} descarta el S&T por {faTarget.last_name}: {saCheck.Reason ?? "valor desequilibrado"}");
                        }
                    }
                }
            }

            var aiStrategy = GetTeamStrategy(aiTeam.id);
            var target = PickTradeTarget(userHealthy, aiRoster, targetedIds, aiStrategy);
            if (target == null) continue;

            targetedIds.Add(target.id);

            var aiHealthy = aiRoster
                .Where(p => p.injury_days == 0 && p.id != target.id)
                .ToList();
            if (aiHealthy.Count == 0) continue;

            var offerPack = BuildOfferPackage(aiHealthy, aiRoster.Count, target, userRoster, aiTeam, aiStrategy, tradedThisSeason);
            if (offerPack == null) continue;

            long valueCap = GetValueCap();
            int valueDraftYear = _season?.year_end ?? 0;
            double valueTolerance = TradeValueHelper.Tolerance(aiStrategy);
            bool isContender = IsTeamContender(aiTeam);

            var offeredPicks = new List<DraftPickData>();
            var wantedPicks = new List<DraftPickData>();

            // Compensación por VALOR (antes se comparaba sólo el mejor OVR del paquete, lo
            // que dejaba pasar "dos estrellas por un rol" e incluso pedía una ronda tuya).
            double incomingValue = TradeValueHelper.PlayerValue(target, valueCap);
            double outgoingValue = TradeValueHelper.PlayersValue(offerPack, valueCap);

            // La IA se queda corta: paga la diferencia con rondas (nunca por encima de lo
            // que su estrategia tolera).
            if (outgoingValue < incomingValue * 0.95)
            {
                bool deepGap = incomingValue - outgoingValue > outgoingValue * 0.5;
                foreach (var pk in GetFuturePicksForTeam(aiTeam.id, deepGap))
                {
                    double pickValue = TradeValueHelper.PickValue(pk, valueDraftYear);
                    if (offeredPicks.Any(x => x.id == pk.id)) continue;
                    if (outgoingValue + pickValue > incomingValue * valueTolerance) break;
                    offeredPicks.Add(pk);
                    outgoingValue += pickValue;
                }
            }
            // La IA entrega más valor (dentro de la tolerancia): pide una ronda tuya como
            // compensación. Nunca las dos cosas a la vez.
            else if (outgoingValue > incomingValue * 1.02)
            {
                var askPicks = GetFuturePicksForTeam(_myTeam.id, false);
                if (askPicks.Count > 0 && (float)_aiRng.NextDouble() < 0.5f)
                {
                    wantedPicks.Add(askPicks[0]);
                    incomingValue += TradeValueHelper.PickValue(askPicks[0], valueDraftYear);
                }
            }

            // Deadline: los contenders presionan y añaden su R1 como incentivo, si sigue
            // cuadrando el valor.
            if (IsDeadlineWeek() && isContender)
            {
                foreach (var pk in GetFuturePicksForTeam(aiTeam.id, true))
                {
                    if (offeredPicks.Any(x => x.id == pk.id)) continue;
                    double pickValue = TradeValueHelper.PickValue(pk, valueDraftYear);
                    if (outgoingValue + pickValue > incomingValue * valueTolerance) break;
                    offeredPicks.Add(pk);
                    outgoingValue += pickValue;
                }
            }

            // Puerta de valor: ni regalar talento (CheckOffer: nada mejor que el objetivo
            // ni entregar más valor del tolerado) ni plantear una oferta insultante.
            var offerCheck = TradeValueHelper.CheckOffer(aiStrategy, offerPack, offeredPicks,
                new List<PlayerData> { target }, wantedPicks, valueCap, valueDraftYear);
            if (!offerCheck.Sane || offerCheck.Outgoing < offerCheck.Incoming * TradeValueHelper.MinOfferRatio)
            {
                Debug.Log($"[AITrades] {aiTeam.name} descarta ofertar por {target.last_name} "
                        + $"({target.overall}): {offerCheck.Reason ?? "oferta demasiado baja"} "
                        + $"[da {offerCheck.Outgoing:0.0} / recibe {offerCheck.Incoming:0.0}]");
                continue;
            }

            var wantIds = new List<int> { target.id };
            var offeredIds = offerPack.Select(p => p.id).ToList();
            DatabaseManager.Instance.AddTradeOffer(new TradeOfferData
            {
                manager_id = _manager.id,
                team_id_from = aiTeam.id,
                player_ids_out = TradeOfferData.JoinIds(wantIds),
                player_ids_in = TradeOfferData.JoinIds(offeredIds),
                pick_ids_out = TradeOfferData.JoinIds(wantedPicks.Select(pk => pk.id).ToList()),
                pick_ids_in = TradeOfferData.JoinIds(offeredPicks.Select(pk => pk.id).ToList()),
                day_sent = gameDay,
                processed = 0
            });
            break;
        }
    }

    PlayerData PickTradeTarget(List<PlayerData> userHealthy, List<PlayerData> aiRoster,
                                HashSet<int> excludedIds, TeamStrategy aiStrategy = TeamStrategy.Balanced)
    {
        var roll = (float)_aiRng.NextDouble();

        // Excluir rookies y jugadores ya excluidos
        var tradeable = userHealthy
            .Where(p => !excludedIds.Contains(p.id) && p.is_rookie == 0)
            .ToList();
        if (tradeable.Count == 0) return null;

        // La IA sólo persigue a quien PUEDE pagar: nunca objetivos muy por encima de su
        // mejor jugador traspasable (no se plantea pedir una estrella para regalar un rol).
        // Los contenders tienen algo más de margen porque completan el pago con rondas.
        int bestTradeable = aiRoster
            .Where(p => p.overall < 87 && p.is_rookie == 0)
            .Select(p => p.overall)
            .DefaultIfEmpty(0)
            .Max();
        if (bestTradeable > 0)
        {
            tradeable = tradeable
                .Where(p => p.overall <= bestTradeable + TradeValueHelper.TargetOvrMargin(aiStrategy))
                .ToList();
            if (tradeable.Count == 0) return null;
        }

        // Trade block: si el usuario ha puesto jugadores en el mercado, la IA
        // los prioriza con un 60 % de probabilidad (siempre que encajen en la
        // estrategia del equipo AI).
        var onBlock = tradeable
            .Where(p => p.on_trade_block == 1)
            .ToList();
        if (onBlock.Count > 0 && _aiRng.NextDouble() < 0.60f)
        {
            var validBlock = onBlock
                .Where(p =>
                    aiStrategy == TeamStrategy.Contend ? p.overall >= 75 :
                    aiStrategy == TeamStrategy.Rebuild ? (p.age <= 28 || p.overall >= 70) : true)
                .ToList();
            if (validBlock.Count > 0)
                return validBlock[_aiRng.Next(validBlock.Count)];
        }

        // Los contenders persiguen a tus mejores jugadores (estrellas)
        if (aiStrategy == TeamStrategy.Contend && roll < 0.75f)
        {
            var starCandidates = tradeable
                .Where(p => p.overall >= 83)
                .OrderByDescending(p => p.overall)
                .ToList();
            if (starCandidates.Count > 0)
                return starCandidates[_aiRng.Next(0, Mathf.Min(2, starCandidates.Count))];
        }

        // Los rebuild buscan activos jóvenes (los explotan o los roban baratos)
        if (aiStrategy == TeamStrategy.Rebuild && roll < 0.30f)
        {
            var youngCandidates = tradeable
                .Where(p => p.age <= 26)
                .OrderByDescending(p => p.overall)
                .ToList();
            if (youngCandidates.Count > 0)
                return youngCandidates[_aiRng.Next(0, Mathf.Min(2, youngCandidates.Count))];
        }

        if (roll < 0.35f)
        {
            var aiWeakPos = GetWeakestPosition(aiRoster);
            if (aiWeakPos != null)
            {
                var candidates = tradeable
                    .Where(p => p.position == aiWeakPos)
                    .OrderByDescending(p => p.overall)
                    .ToList();
                if (candidates.Count > 0)
                    return candidates[_aiRng.Next(0, Mathf.Min(3, candidates.Count))];
            }
        }

        if (roll < 0.60f)
        {
            var candidates = tradeable
                .OrderByDescending(p => p.overall)
                .ToList();
            if (candidates.Count > 0)
                return candidates[_aiRng.Next(0, Mathf.Min(3, candidates.Count))];
        }

        if (roll < 0.80f)
        {
            var candidates = tradeable
                .OrderByDescending(p => p.salary)
                .ToList();
            if (candidates.Count > 0)
                return candidates[_aiRng.Next(0, Mathf.Min(3, candidates.Count))];
        }

        return tradeable[_aiRng.Next(0, tradeable.Count)];
    }

    List<PlayerData> BuildOfferPackage(List<PlayerData> aiAvailable, int aiRosterCount,
                                        PlayerData target, List<PlayerData> userRoster, TeamData aiTeam,
                                        TeamStrategy aiStrategy, HashSet<int> tradedThisSeason)
    {
        long userPayroll = userRoster.Sum(p => p.salary);
        var fullAiRoster = DatabaseManager.Instance.GetPlayersByTeam(aiTeam.id);
        long aiPayroll = fullAiRoster.Sum(p => p.salary);

        bool aiHardCapped = aiTeam.first_apron_hard_capped == 1;
        bool userHardCapped = _myTeam.first_apron_hard_capped == 1;

        // Valor: el paquete ha de poder pagar al objetivo y no puede regalar más de lo
        // que la estrategia permite (ver TradeValueHelper).
        long cap = GetValueCap();
        double targetValue = TradeValueHelper.PlayerValue(target, cap);
        double tolerance = TradeValueHelper.Tolerance(aiStrategy);

        // Protección universal: estrellas (OVR >= 87) nunca se ofrecen.
        // Contender además protege jóvenes prometedores (edad < 26 y OVR >= 82).
        // Rebuild solo ofrece veteranos (age >= 25).
        // Nadie claramente mejor que el objetivo: un 84/85 no se entrega por un 76 por
        // mucho que los salarios encajen (MaxOvrGap).
        var tradeable = aiAvailable.Where(p => !tradedThisSeason.Contains(p.id)
                                              && p.is_rookie == 0 && p.overall < 87
                                              && p.overall <= target.overall + TradeValueHelper.MaxOvrGap).ToList();
        if (aiStrategy == TeamStrategy.Contend)
            tradeable = tradeable.Where(p => !(p.age < 26 && p.overall >= 82)).ToList();
        if (aiStrategy == TeamStrategy.Rebuild)
            tradeable = tradeable.Where(p => p.age >= 25).ToList();
        if (tradeable.Count == 0) return null;

        bool CapOk(List<PlayerData> pack) =>
            TradeHelper.ValidateTrade(pack, new List<PlayerData> { target },
                aiRosterCount, userRoster.Count, _myTeam.name, userPayroll,
                aiTeam.name, aiPayroll, aiHardCapped, userHardCapped).Count == 0;

        // 1-por-1: el valor más parecido al del objetivo (antes se ordenaba por salario,
        // que no dice nada del talento), y nunca por encima de lo que la estrategia tolera.
        foreach (var p in tradeable.OrderBy(p => Math.Abs(TradeValueHelper.PlayerValue(p, cap) - targetValue)))
        {
            if (TradeValueHelper.PlayerValue(p, cap) > targetValue * tolerance) continue;
            var pack = new List<PlayerData> { p };
            if (CapOk(pack)) return pack;
        }

        // 2-por-1: combinaciones ordenadas por cercanía de valor (profundidad por calidad).
        var pairs = new List<(double distance, List<PlayerData> pack)>();
        for (int i = 0; i < tradeable.Count; i++)
        {
            for (int j = i + 1; j < tradeable.Count; j++)
            {
                double v = TradeValueHelper.PlayerValue(tradeable[i], cap)
                         + TradeValueHelper.PlayerValue(tradeable[j], cap);
                if (v > targetValue * tolerance) continue;
                pairs.Add((Math.Abs(v - targetValue), new List<PlayerData> { tradeable[i], tradeable[j] }));
            }
        }
        foreach (var (_, pack) in pairs.OrderBy(x => x.distance))
            if (CapOk(pack)) return pack;

        // 3-por-1 (sólo si la plantilla lo aguanta), mismo criterio de valor.
        if (aiRosterCount - 3 >= 10)
        {
            var triples = new List<(double distance, List<PlayerData> pack)>();
            for (int i = 0; i < tradeable.Count; i++)
            {
                for (int j = i + 1; j < tradeable.Count; j++)
                {
                    for (int k = j + 1; k < tradeable.Count; k++)
                    {
                        double v = TradeValueHelper.PlayerValue(tradeable[i], cap)
                                 + TradeValueHelper.PlayerValue(tradeable[j], cap)
                                 + TradeValueHelper.PlayerValue(tradeable[k], cap);
                        if (v > targetValue * tolerance) continue;
                        triples.Add((Math.Abs(v - targetValue),
                            new List<PlayerData> { tradeable[i], tradeable[j], tradeable[k] }));
                    }
                }
            }
            foreach (var (_, pack) in triples.OrderBy(x => x.distance))
                if (CapOk(pack)) return pack;
        }

        return null;
    }

    /// <summary>
    /// Cap salarial para la valoración de traspasos (crece +5 %/año; nunca usar la
    /// constante a pelo para no desincronizarse con la partida).
    /// </summary>
    long GetValueCap()
    {
        var ls = DatabaseManager.Instance.GetLeagueSettings();
        return ls != null && ls.salary_cap > 0 ? ls.salary_cap : TradeHelper.SALARY_CAP;
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

        // S&T: si la IA pide un FA propio tuyo (no está en el roster), se firma y se traspasa
        var ourSATFAs = new List<PlayerData>();
        var freeAgents = DatabaseManager.Instance.GetFreeAgents().ToList();
        var unaccounted = wantedIds.Where(id => ourPlayers.All(p => p.id != id)).ToList();
        foreach (var id in unaccounted)
        {
            var fa = freeAgents.FirstOrDefault(p => p.id == id && DatabaseManager.Instance.IsOwnRecentFA(p, _myTeam.id));
            if (fa != null)
            {
                ourSATFAs.Add(fa);
                ourPlayers.Add(fa);
            }
        }

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

        // Revalidación al mostrarla: la IA validó el traspaso cuando lo generó, pero la
        // oferta puede quedar pendiente varios días y desde entonces la nómina, la
        // plantilla o el hard cap cambian. Sin esto se enseñaban ofertas que solo
        // fallaban al aceptarlas. Las obsoletas se descartan sin molestar al jugador.
        var staleErrors = ValidatePendingTradeOffer(offer, ourPlayers, theirPlayers, ourSATFAs);
        if (staleErrors.Count > 0)
        {
            DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 2);
            Debug.LogWarning($"[AITrades] Oferta de {offer.team_id_from} descartada por obsoleta: {string.Join(" | ", staleErrors)}");
            ShowNextPendingTradeOffer();
            return;
        }

        ShowTradeOfferModal(offer, ourPlayers, theirPlayers, ourPicks, theirPicks, ourSATFAs);
    }

    /// <summary>
    /// Revalida una oferta de traspaso de la IA con la plantilla y la nómina ACTUALES.
    /// Equivale a la validación que hace la IA al generarla (<see cref="TradeHelper.ValidateTrade"/>),
    /// con el mismo reparto de lados que al aceptar: A = equipo IA (envía), B = mi equipo (recibe).
    /// Devuelve la lista de errores (vacía = la oferta sigue siendo válida).
    /// </summary>
    List<string> ValidatePendingTradeOffer(TradeOfferData offer, List<PlayerData> ourPlayers,
                                           List<PlayerData> theirPlayers, List<PlayerData> ourSATFAs)
    {
        var aiTeam = _allTeams?.FirstOrDefault(t => t.id == offer.team_id_from)
                     ?? DatabaseManager.Instance.GetTeamById(offer.team_id_from);

        // S&T: los FA propios que se firmarían viajan con su nuevo contrato (Bird rights).
        var signSalaries = new Dictionary<int, long>();
        if (ourSATFAs != null && ourSATFAs.Count > 0)
        {
            var lsSat = DatabaseManager.Instance.GetLeagueSettings();
            long myPaySat = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id).Sum(x => x.salary);
            foreach (var fa in ourSATFAs)
            {
                var bd = RosterController.GetMaxOfferBreakdown(fa, lsSat, myPaySat, true);
                long market = DatabaseManager.Instance.EstimateMarketSalary(fa.GetCalculatedAverage(),
                    lsSat != null ? lsSat.salary_cap : TradeHelper.SALARY_CAP);
                signSalaries[fa.id] = bd.birdMax > 0 ? bd.birdMax : market;
            }
        }

        var leagueChk = DatabaseManager.Instance.GetLeagueSettings();
        long firstApronChk = leagueChk?.apron > 0 ? leagueChk.apron : TradeHelper.FIRST_APRON;
        long secondApronChk = leagueChk?.repeater_apron > 0 ? leagueChk.repeater_apron : TradeHelper.SECOND_APRON;
        long luxuryTaxChk = leagueChk?.luxury_tax > 0 ? leagueChk.luxury_tax : TradeHelper.LUXURY_TAX;

        return TradeHelper.ValidateTrade(
            theirPlayers, ourPlayers,
            DatabaseManager.Instance.GetRosterCount(offer.team_id_from),
            DatabaseManager.Instance.GetRosterCount(_myTeam.id),
            _myTeam.name, DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id).Sum(p => p.salary),
            aiTeam != null ? aiTeam.name : "IA",
            DatabaseManager.Instance.GetPlayersByTeam(offer.team_id_from).Sum(p => p.salary),
            aiTeam != null && aiTeam.first_apron_hard_capped == 1,
            _myTeam.first_apron_hard_capped == 1,
            firstApronChk, secondApronChk, luxuryTaxChk,
            null,
            signSalaries.Count > 0 ? signSalaries : null);
    }

    void ShowTradeOfferModal(TradeOfferData offer, List<PlayerData> ourPlayers, List<PlayerData> theirPlayers,
                              List<DraftPickData> ourPicks = null, List<DraftPickData> theirPicks = null,
                              List<PlayerData> ourSATFAs = null)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var aiTeam = _allTeams.FirstOrDefault(t => t.id == offer.team_id_from);
        string teamName = aiTeam != null ? aiTeam.name : "?";
        var teamAbbrs = DatabaseManager.Instance.GetAllTeams().ToDictionary(t => t.id, t => t.abbreviation);

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.width = 1050;
        box.style.borderTopColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderRightColor = new StyleColor(new Color32(212, 160, 23, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");

        var title = new Label("PROPUESTA DE INTERCAMBIO");
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        header.Add(title);
        box.Add(header);

        // ── Teams row ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        body.AddToClassList("trade-offer-body");

        var teamsRow = new VisualElement();
        teamsRow.AddToClassList("trade-modal-teams");

        // Pre-compute totals for cap room calculation
        long ourSalaryTotal = ourPlayers.Sum(p => p.salary);
        long theirSalaryTotal = theirPlayers.Sum(p => p.salary);

        // Left: your team (what you send)
        var leftPanel = BuildTradeTeamPanel(_myTeam, ourPlayers, ourPicks, teamAbbrs, isSending: true, otherSideSalary: theirSalaryTotal);
        teamsRow.Add(leftPanel);

        // Swap icon
        var swapIcon = new VisualElement();
        swapIcon.AddToClassList("trade-modal-swap");
        var mercadoSprite = Resources.Load<Sprite>("Icons/swap_blue");
        if (mercadoSprite != null)
            swapIcon.style.backgroundImage = new StyleBackground(mercadoSprite);
        teamsRow.Add(swapIcon);

        // Right: their team (what you receive)
        var rightPanel = BuildTradeTeamPanel(aiTeam, theirPlayers, theirPicks, teamAbbrs, isSending: false, otherSideSalary: ourSalaryTotal);
        teamsRow.Add(rightPanel);

        body.Add(teamsRow);

        // ── Analysis section ──
        var analysisSection = BuildTradeAnalysis(ourPlayers, theirPlayers, aiTeam);
        body.Add(analysisSection);
        box.Add(body);

        // ── Buttons ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var rejectBtn = new Button();
        rejectBtn.text = "RECHAZAR OFERTA";
        rejectBtn.AddToClassList("btn-modal-confirm");
        rejectBtn.AddToClassList("btn-modal-confirm--danger");
        rejectBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 2);
            _firedOverlay.style.display = DisplayStyle.None;
            if (_fastSimOfferPauseActive)
            {
                _tradeOfferModalResolved = true;
                _tradeOfferAccepted = false;
            }
            else
                ShowNextPendingTradeOffer();
        });
        actions.Add(rejectBtn);

        var acceptBtn = new Button();
        acceptBtn.text = "ACEPTAR INTERCAMBIO";
        acceptBtn.AddToClassList("btn-modal-confirm");
        acceptBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // ── Revalidación obligatoria antes de mutar ──
            // La oferta se generó cuando la plantilla/nómina eran otras; entre medias
            // el jugador pudo fichar/cortar. Sin revalidar se pueden crear plantillas
            // >17 o traspasos que incumplen el salary matching / apron.
            {
                var errorsChk = ValidatePendingTradeOffer(offer, ourPlayers, theirPlayers, ourSATFAs);

                if (errorsChk.Count > 0)
                {
                    DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 2);
                    _firedOverlay.style.display = DisplayStyle.None;
                    // El callback resuelve la espera de la simulación rápida: sin él la
                    // corrutina se quedaba en el WaitUntil y el juego quedaba bloqueado.
                    ShowOfferResultModal("TRASPASO NO VÁLIDO",
                        "La oferta ya no es válida con tu plantilla actual:\n\n" + string.Join("\n", errorsChk), -1,
                        onFastSimResolved: goToQuinteto =>
                        {
                            _tradeOfferModalResolved = true;
                            _tradeOfferAccepted = false;
                            _tradeOfferGoToQuinteto = goToQuinteto;
                        });
                    return;
                }
            }

            // S&T: firmar los FA propios antes de traspazarlos a la IA
            if (ourSATFAs != null && ourSATFAs.Count > 0)
            {
                var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
                long myPayroll = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id).Sum(x => x.salary);
                foreach (var fa in ourSATFAs)
                {
                    var breakdown = RosterController.GetMaxOfferBreakdown(fa, leagueSettings, myPayroll, true);
                    long market = DatabaseManager.Instance.EstimateMarketSalary(fa.GetCalculatedAverage(), leagueSettings != null ? leagueSettings.salary_cap : TradeHelper.SALARY_CAP);
                    long signSalary = breakdown.birdMax > 0 ? breakdown.birdMax : market;
                    int years = fa.age > 35 ? 1 : fa.age > 32 ? 2 : fa.age > 28 ? 3 : fa.age > 25 ? 4 : 5;
                    fa.team_id = _myTeam.id;
                    fa.last_team_id = _myTeam.id;
                    fa.salary = signSalary;
                    fa.contract_years = years;
                    fa.guaranteed_years = years;
                    fa.seasons_with_team = 1;
                    DatabaseManager.Instance.AssignJerseyNumber(fa, _myTeam.id);
                    DatabaseManager.Instance.UpdatePlayer(fa);
                    DatabaseManager.Instance.InsertTrade(new TradeData
                    {
                        season_id = _season?.id ?? 0,
                        game_day = _season?.current_game_day ?? 0,
                        game_date = _season?.current_date ?? now,
                        team_id_from = 0,
                        team_id_to = _myTeam.id,
                        player_id = fa.id,
                        trade_type = "free_agent"
                    });
                }
                if (_myTeam.first_apron_hard_capped == 0)
                {
                    _myTeam.first_apron_hard_capped = 1;
                    DatabaseManager.Instance.UpdateTeam(_myTeam);
                }
            }

            foreach (var p in ourPlayers)
            {
                DatabaseManager.Instance.MovePlayerToTeam(p, offer.team_id_from);
                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = _season?.id ?? 0,
                    game_day = _season?.current_game_day ?? 0,
                    game_date = _season?.current_date ?? now,
                    team_id_from = _myTeam.id,
                    team_id_to = offer.team_id_from,
                    player_id = p.id,
                    trade_type = (ourSATFAs != null && ourSATFAs.Any(x => x.id == p.id)) ? "sign_and_trade" : "trade"
                });
            }

            foreach (var p in theirPlayers)
            {
                DatabaseManager.Instance.MovePlayerToTeam(p, _myTeam.id);
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

            foreach (var p in ourPlayers)
            {
                DatabaseManager.Instance.AssignJerseyNumber(p, offer.team_id_from);
                DatabaseManager.Instance.UpdatePlayer(p);
            }

            foreach (var p in theirPlayers)
            {
                DatabaseManager.Instance.AssignJerseyNumber(p, _myTeam.id);
                DatabaseManager.Instance.UpdatePlayer(p);
            }

            // Apply projected chemistry from the analysis modal
            DatabaseManager.Instance.UpdateTeamChemistry(_myTeam.id, _pendingTradeChem);
            _myTeam.team_chemistry = _pendingTradeChem;
            var headerChem = _root.Q<Label>("HeaderChemistry");
            if (headerChem != null)
            {
                headerChem.text = $"{_pendingTradeChem}%";
                headerChem.RemoveFromClassList("header-stat-value--gold");
                headerChem.RemoveFromClassList("header-stat-value--negative");
                if (_pendingTradeChem < 40)
                    headerChem.AddToClassList("header-stat-value--negative");
                else if (_pendingTradeChem < 70)
                    headerChem.AddToClassList("header-stat-value--gold");
            }

            // Update header payroll & margin
            var leagueSet = DatabaseManager.Instance.GetLeagueSettings();
            long cap = leagueSet?.salary_cap ?? TradeHelper.SALARY_CAP;
            long prevPayroll = _players.Sum(x => x.salary);
            long salIn = theirPlayers.Sum(p => p.salary);
            long salOut = ourPlayers.Sum(p => p.salary);
            long newTotalPayroll = prevPayroll - salOut + salIn;
            long newMargin = cap - newTotalPayroll;
            _headerPayroll.text = $"${newTotalPayroll / 1_000_000}M";
            string newMarginText = newMargin >= 0
                ? $"+${newMargin / 1_000_000}M"
                : $"-${Mathf.Abs((int)(newMargin / 1_000_000))}M";
            _headerMargin.text = newMarginText;
            _headerMargin.RemoveFromClassList("header-stat-value--negative");
            if (newMargin < 0)
                _headerMargin.AddToClassList("header-stat-value--negative");

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

            var satMsg = (ourSATFAs != null && ourSATFAs.Count > 0)
                ? $" (Sign & Trade: {string.Join(", ", ourSATFAs.Select(x => $"{x.first_name} {x.last_name}"))} firmado(s) con Bird rights y traspasado(s))."
                : "";
            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 1,
                sender_id = 0,
                title = "Intercambio aceptado",
                body = NewsTexts.Intercambio(teamName, ourNames + ourPicksText, _myTeam.name, theirNames + theirPicksText, ""),
                game_day = _season.current_game_day,
                game_date = now,
                created_at = now,
                date_sent = now,
                is_read = 0,
                category = NewsCategory.TRASPASOS,
                news_type = NewsType.INTERCAMBIO
            });

            DatabaseManager.Instance.MarkTradeOfferProcessed(offer.id, 1);
            _firedOverlay.style.display = DisplayStyle.None;
            if (_fastSimOfferPauseActive)
            {
                _tradeOfferModalResolved = true;
                _tradeOfferAccepted = true;
            }
            else
                ShowNextPendingTradeOffer();
        });
        actions.Add(acceptBtn);
        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(rejectBtn);
            CursorManager.Instance.RegisterHandCursor(acceptBtn);
        }
    }

    // ═══════════════════════════════════════════
    //  TRADE OFFER MODAL — BUILDERS
    // ═══════════════════════════════════════════

    VisualElement BuildTradeTeamPanel(TeamData team, List<PlayerData> players,
                                       List<DraftPickData> picks, Dictionary<int, string> teamAbbrs,
                                       bool isSending, long otherSideSalary = 0)
    {
        var panel = new VisualElement();
        panel.AddToClassList("trade-modal-team-panel");

        // Team header: logo + name + action label
        var teamHeader = new VisualElement();
        teamHeader.AddToClassList("trade-modal-team-header");

        var logo = new VisualElement();
        logo.AddToClassList("trade-modal-team-logo");
        if (team != null && !string.IsNullOrEmpty(team.logo))
            SetTeamLogo(logo, team.logo, "64x64");
        teamHeader.Add(logo);

        var teamInfo = new VisualElement();
        teamInfo.AddToClassList("trade-modal-team-info");

        var nameLbl = new Label(team != null ? team.name.ToUpper() : "?");
        nameLbl.AddToClassList("trade-modal-team-name");
        teamInfo.Add(nameLbl);

        var actionLbl = new Label("Envía");
        actionLbl.AddToClassList("trade-modal-team-action");
        teamInfo.Add(actionLbl);

        teamHeader.Add(teamInfo);
        panel.Add(teamHeader);

        // Player cards
        foreach (var p in players)
        {
            var card = BuildTradePlayerCard(p);
            panel.Add(card);
        }

        // Picks
        if (picks != null && picks.Count > 0)
        {
            var picksSection = new VisualElement();
            picksSection.AddToClassList("trade-modal-picks-section");
            var allTeams = DatabaseManager.Instance.GetAllTeams();
            foreach (var pk in picks.OrderBy(p => p.draft_year).ThenBy(p => p.round))
            {
                var origTeam = allTeams.FirstOrDefault(t => t.id == pk.original_team_id);
                string teamName = origTeam != null ? origTeam.name : "???";
                string pickText = $"Ronda {pk.round} {pk.draft_year} - {teamName}";
                // Si el pick cambió de dueño, indicar también el equipo actual (la
                // posición la marca el equipo original).
                if (pk.current_team_id != pk.original_team_id)
                {
                    var curTeam = allTeams.FirstOrDefault(t => t.id == pk.current_team_id);
                    if (curTeam != null) pickText += $" ({curTeam.name})";
                }
                var pickLbl = new Label(pickText);
                pickLbl.AddToClassList("trade-modal-picks-label");
                picksSection.Add(pickLbl);
            }
            panel.Add(picksSection);
        }

        // Salary summary bar
        var salaryBar = new VisualElement();
        salaryBar.AddToClassList("trade-modal-salary-bar");

        long totalSalary = players.Sum(p => p.salary);
        var salaryItem = new VisualElement();
        salaryItem.AddToClassList("trade-modal-salary-item");

        var salaryLabel = new Label(isSending ? "SALARIO TOTAL SALIENTE" : "SALARIO TOTAL ENTRANTE");
        salaryLabel.AddToClassList("trade-modal-salary-label");
        salaryItem.Add(salaryLabel);

        var salaryValue = new Label(FormatMoneyShort(totalSalary));
        salaryValue.AddToClassList("trade-modal-salary-value");
        salaryValue.AddToClassList("trade-modal-salary-value--positive");
        salaryItem.Add(salaryValue);
        salaryBar.Add(salaryItem);

        // Cap room resultante — each team's own cap room
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;

        if (isSending)
        {
            // My team's cap room after sending these players and receiving the other side
            long myCurrentPayroll = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id).Sum(x => x.salary);
            long myCapRoom = (salaryCap - myCurrentPayroll) + totalSalary - otherSideSalary;
            long capRoomAfter = myCapRoom;

            var capItem = new VisualElement();
            capItem.AddToClassList("trade-modal-salary-item");

            var capLabel = new Label("CAP ROOM RESULTANTE");
            capLabel.AddToClassList("trade-modal-salary-label");
            capItem.Add(capLabel);

            string capText = (capRoomAfter >= 0 ? "+" : "-") + FormatMoneyShort(System.Math.Abs(capRoomAfter));
            var capValue = new Label(capText);
            capValue.AddToClassList("trade-modal-salary-value");
            capValue.AddToClassList(capRoomAfter >= 0
                ? "trade-modal-salary-value--positive"
                : "trade-modal-salary-value--negative");
            capItem.Add(capValue);
            salaryBar.Add(capItem);
        }
        else
        {
            // AI team's cap room after receiving these players and sending the other side
            long aiCurrentPayroll = DatabaseManager.Instance.GetPlayersByTeam(team.id).Sum(x => x.salary);
            long aiCapRoom = (salaryCap - aiCurrentPayroll) + totalSalary - otherSideSalary;
            long capRoomAfter = aiCapRoom;

            var capItem = new VisualElement();
            capItem.AddToClassList("trade-modal-salary-item");

            var capLabel = new Label("CAP ROOM RESULTANTE");
            capLabel.AddToClassList("trade-modal-salary-label");
            capItem.Add(capLabel);

            string capText = (capRoomAfter >= 0 ? "+" : "-") + FormatMoneyShort(System.Math.Abs(capRoomAfter));
            var capValue = new Label(capText);
            capValue.AddToClassList("trade-modal-salary-value");
            capValue.AddToClassList(capRoomAfter >= 0
                ? "trade-modal-salary-value--positive"
                : "trade-modal-salary-value--negative");
            capItem.Add(capValue);
            salaryBar.Add(capItem);
        }

        panel.Add(salaryBar);

        return panel;
    }

    VisualElement BuildTradePlayerCard(PlayerData p)
    {
        var card = new VisualElement();
        card.AddToClassList("trade-modal-player-card");

        // Photo
        var photo = new VisualElement();
        photo.AddToClassList("trade-modal-player-photo");
        Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
        if (tex != null)
            photo.style.backgroundImage = new StyleBackground(tex);
        card.Add(photo);

        // Info
        var info = new VisualElement();
        info.AddToClassList("trade-modal-player-info");

        var nameLbl = new Label($"{p.first_name} {p.last_name}");
        nameLbl.AddToClassList("trade-modal-player-name");
        info.Add(nameLbl);

        string stars = p.overall >= 85 ? " ★★★" : p.overall >= 75 ? " ★★" : p.overall >= 65 ? " ★" : "";
        var metaLbl = new Label($"{PositionCodes.GetShort(p.position)} | {p.age} años");
        metaLbl.AddToClassList("trade-modal-player-meta");
        info.Add(metaLbl);

        string yearsText = p.contract_years == 1 ? "1 AÑO RESTANTE" : $"{p.contract_years} AÑOS RESTANTES";
        var contractLbl = new Label($"{SalaryStr(p.salary)}  ·  {yearsText}");
        contractLbl.AddToClassList("trade-modal-player-contract");
        info.Add(contractLbl);

        card.Add(info);

        // OVR column
        var ovrCol = new VisualElement();
        ovrCol.AddToClassList("trade-modal-player-ovr-col");

        var ovrLbl = new Label(p.overall.ToString());
        ovrLbl.AddToClassList("trade-modal-player-ovr");
        if (p.overall >= 80)
            ovrLbl.style.color = new StyleColor(new Color32(39, 174, 96, 255));
        else if (p.overall >= 60)
            ovrLbl.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        else
            ovrLbl.style.color = new StyleColor(new Color32(192, 57, 43, 255));
        ovrCol.Add(ovrLbl);

        var ovrSub = new Label("MED");
        ovrSub.AddToClassList("trade-modal-player-ovr-label");
        ovrCol.Add(ovrSub);

        card.Add(ovrCol);

        return card;
    }

    VisualElement BuildTradeAnalysis(List<PlayerData> ourPlayers, List<PlayerData> theirPlayers, TeamData aiTeam)
    {
        var section = new VisualElement();
        section.AddToClassList("trade-modal-analysis");

        var titleLbl = new Label("ANÁLISIS DE LA OPERACIÓN");
        titleLbl.AddToClassList("trade-modal-analysis-title");
        section.Add(titleLbl);

        var row = new VisualElement();
        row.AddToClassList("trade-modal-analysis-row");

        // ── 1. Impacto de Equipo ──
        var impactCard = new VisualElement();
        impactCard.AddToClassList("trade-modal-analysis-card");

        var impactLabel = new Label("IMPACTO DE EQUIPO");
        impactLabel.AddToClassList("trade-modal-analysis-label");
        impactCard.Add(impactLabel);

        // Compute net OVR change
        float ourAvg = ourPlayers.Count > 0 ? (float)ourPlayers.Average(p => p.overall) : 0;
        float theirAvg = theirPlayers.Count > 0 ? (float)theirPlayers.Average(p => p.overall) : 0;
        int netImpact = Mathf.RoundToInt(theirAvg - ourAvg);
        // Factor in roster depth change
        int depthDelta = theirPlayers.Count - ourPlayers.Count;

        string impactText = (netImpact >= 0 ? "+" : "") + netImpact.ToString();
        var impactValue = new Label(impactText);
        impactValue.AddToClassList("trade-modal-analysis-value");
        impactValue.AddToClassList(netImpact > 0
            ? "trade-modal-analysis-value--positive"
            : netImpact < 0
                ? "trade-modal-analysis-value--negative"
                : "trade-modal-analysis-value--neutral");
        impactCard.Add(impactValue);

        string impactDesc = netImpact > 0
            ? "Mejora la calidad de la plantilla"
            : netImpact < 0
                ? "Pierdes calidad media"
                : "Calidad similar";
        if (depthDelta > 0) impactDesc += " y profundidad";
        else if (depthDelta < 0) impactDesc += " pero pierdes profundidad";

        var impactDescLbl = new Label(impactDesc);
        impactDescLbl.AddToClassList("trade-modal-analysis-desc");
        impactCard.Add(impactDescLbl);

        row.Add(impactCard);

        // ── 2. Química del Equipo ──
        var chemCard = new VisualElement();
        chemCard.AddToClassList("trade-modal-analysis-card");

        var chemLabel = new Label("QUÍMICA DEL EQUIPO");
        chemLabel.AddToClassList("trade-modal-analysis-label");
        chemCard.Add(chemLabel);

        int currentChem = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        float avgMoraleIn = theirPlayers.Count > 0 ? (float)theirPlayers.Average(p => p.morale) : 50;
        float avgMoraleOut = ourPlayers.Count > 0 ? (float)ourPlayers.Average(p => p.morale) : 50;
        int chemShift = Mathf.RoundToInt((avgMoraleIn - avgMoraleOut) * 0.15f);
        int projectedChem = Mathf.Clamp(currentChem + chemShift, 0, 100);
        _pendingTradeChem = projectedChem;

        var chemCircle = new VisualElement();
        chemCircle.AddToClassList("trade-modal-chem-circle");
        var chemValue = new Label($"{projectedChem}%");
        chemValue.AddToClassList("trade-modal-chem-value");
        chemCircle.Add(chemValue);
        chemCard.Add(chemCircle);

        string chemDesc;
        if (chemShift > 2) chemDesc = "Mejora notable";
        else if (chemShift > 0) chemDesc = "Ligera mejora";
        else if (chemShift == 0) chemDesc = "Sin cambios significativos";
        else if (chemShift > -3) chemDesc = "Ligero deterioro";
        else chemDesc = "Deterioro notable";

        var chemDescLbl = new Label(chemDesc);
        chemDescLbl.AddToClassList("trade-modal-analysis-desc");
        chemCard.Add(chemDescLbl);

        row.Add(chemCard);

        // ── 3. Flexibilidad Futura ──
        var flexCard = new VisualElement();
        flexCard.AddToClassList("trade-modal-analysis-card");

        var flexLabel = new Label("FLEXIBILIDAD FUTURA");
        flexLabel.AddToClassList("trade-modal-analysis-label");
        flexCard.Add(flexLabel);

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long currentPayroll = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id).Sum(x => x.salary);
        long salaryIn = theirPlayers.Sum(p => p.salary);
        long salaryOut = ourPlayers.Sum(p => p.salary);
        long newPayroll = currentPayroll - salaryOut + salaryIn;
        long currentCapRoom = salaryCap - currentPayroll;
        long newCapRoom = salaryCap - newPayroll;

        int flexScore = 0;
        if (newCapRoom > currentCapRoom) flexScore = 1;
        else if (newCapRoom < currentCapRoom) flexScore = -1;

        string flexText = (flexScore >= 0 ? "+" : "") + flexScore.ToString();
        var flexValue = new Label(flexText);
        flexValue.AddToClassList("trade-modal-analysis-value");
        flexValue.AddToClassList(flexScore > 0
            ? "trade-modal-analysis-value--positive"
            : flexScore < 0
                ? "trade-modal-analysis-value--negative"
                : "trade-modal-analysis-value--neutral");
        flexCard.Add(flexValue);

        long capDiff = newCapRoom - currentCapRoom;
        string flexDesc = capDiff > 0
            ? $"Ganas {FormatMoneyShort(capDiff)} de espacio salarial"
            : capDiff < 0
                ? $"Pierdes {FormatMoneyShort(-capDiff)} de espacio salarial"
                : "Sin cambio en espacio salarial";

        var flexDescLbl = new Label(flexDesc);
        flexDescLbl.AddToClassList("trade-modal-analysis-desc");
        flexCard.Add(flexDescLbl);

        row.Add(flexCard);

        section.Add(row);
        return section;
    }

    string FormatMoneyShort(long amount)
    {
        float millions = amount / 1_000_000f;
        return $"${millions:F2}M";
    }

    string SalaryStr(long salary)
    {
        return salary.ToString("N0").Replace(',', '.') + " $";
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
        SyncMyTeamBudget(team);

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
            sender_type = 1,
            sender_id = 0,
            title = $"Remodelación completada: {info.name}",
            body = NewsTexts.RemodelacionCompletada(_myTeam.name, info.name),
            game_day = _season.current_game_day,
            game_date = System.DateTime.Parse(_season.year_start + "-10-22").AddDays(_season.current_game_day - 1).ToString("yyyy-MM-dd"),
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = NewsType.REMODELACION_COMPLETADA
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

    /// <summary>Ganados y jugados de un equipo desde la lista de partidos de la liga.</summary>
    static (int wins, int played) TeamRecord(List<GameData> allGames, int teamId)
    {
        int wins = 0, played = 0;
        foreach (var g in allGames)
        {
            if (g.home_team_id == teamId)
            {
                played++;
                if (g.home_score > g.away_score) wins++;
            }
            else if (g.away_team_id == teamId)
            {
                played++;
                if (g.away_score > g.home_score) wins++;
            }
        }
        return (wins, played);
    }

    /// <summary>Racha reciente del equipo (últimos 10 partidos) como factor 0.85-1.10.</summary>
    float GetAttendanceStreakFactor(List<GameData> allGames, int teamId)
    {
        var last10 = allGames
            .Where(g => g.home_team_id == teamId || g.away_team_id == teamId)
            .OrderByDescending(g => g.game_day).ThenByDescending(g => g.id)
            .Take(10).ToList();
        if (last10.Count == 0) return 1.0f;
        int wins = last10.Count(g =>
            (g.home_team_id == teamId && g.home_score > g.away_score) ||
            (g.away_team_id == teamId && g.away_score > g.home_score));
        float recent = (float)wins / last10.Count;
        return Mathf.Clamp(0.85f + recent * 0.25f, 0.85f, 1.10f);
    }

    /// <summary>
    /// Asistencia (E3): demanda elástica según mercado, rival, racha y precio; el precio de
    /// referencia depende del mercado y la reputación, y su exceso castiga la ocupación.
    /// </summary>
    int CalculateAttendance(GameData game, TeamData homeTeam)
    {
        if (homeTeam == null || homeTeam.capacity <= 0) return 0;

        bool myTeamIsHome = game.home_team_id == _myTeam.id;

        var allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        var (homeWins, homePlayed) = TeamRecord(allGames, homeTeam.id);
        float homeWinPct = homePlayed > 0 ? (float)homeWins / homePlayed : 0.5f;

        var rival = DatabaseManager.Instance.GetTeamById(
            game.home_team_id == homeTeam.id ? game.away_team_id : game.home_team_id);
        var (rivalWins, rivalPlayed) = TeamRecord(allGames, rival != null ? rival.id : 0);
        float rivalWinPct = rivalPlayed > 0 ? (float)rivalWins / rivalPlayed : 0.5f;

        float objectiveFactor = GetObjectiveComplianceFactor(homeTeam, allGames);
        float streakFactor = GetAttendanceStreakFactor(allGames, homeTeam.id);

        int ticketPrice = 50;
        var finSettings = DatabaseManager.Instance.GetTeamSettings(homeTeam.id);
        if (finSettings != null) ticketPrice = (int)finSettings.ticket_price;

        return MarketEconomy.EstimateHomeAttendance(
            homeTeam.capacity,
            homeWinPct,
            rivalWinPct,
            rival != null ? rival.reputation : 3,
            homeTeam.market_size,
            homeTeam.reputation,
            objectiveFactor,
            streakFactor,
            _manager.fan_confidence / 100f,
            myTeamIsHome,
            ticketPrice,
            UnityEngine.Random.value,
            UnityEngine.Random.value);
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

        void ProcessPlayer(PlayerData player, float minutes, float? avgRating, float expectedMinutes)
        {
            // Minutos esperados: los configurados en ROTACIÓN (target del snapshot) o,
            // si no hay, la tabla fija por rol.
            float expectedMin = expectedMinutes > 0f ? expectedMinutes : ExpectedMinForRole(player.role);
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
                        body = newMorale < 10
                            ? NewsTexts.Queja(_myTeam.name, $"{player.first_name} {player.last_name} la falta de minutos")
                            : NewsTexts.Preocupacion(_myTeam.name, $"{player.first_name} {player.last_name} la falta de minutos"),
                        game_day = gameDay,
                        game_date = gameDate,
                        created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        is_read = 0,
                        category = NewsCategory.PRENSA,
                        news_type = newMorale < 10 ? NewsType.QUEJA : NewsType.PREOCUPACION
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
                .Where(s => s.minutes > 0)
                .OrderByDescending(s => s.game_id).Take(5).ToList();
            float avgRating = last5Stats.Count > 0 ? (float)last5Stats.Average(s => s.rating) : 15f;

            ProcessPlayer(player, ps.minutes, avgRating, ps.target_minutes);
        }

        // ── Inactive healthy players (no convocados pero sanos) ──
        var allTeamPlayers = DatabaseManager.Instance.GetPlayersByTeam(teamId);
        foreach (var player in allTeamPlayers)
        {
            if (player.injury_days > 0) continue;
            if (activeIds.Contains(player.id)) continue;
            ProcessPlayer(player, 0f, null, 0f);
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
            SyncMyTeamBudget(homeTeam);

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

        // Sponsor home game income (E3: escala con la fama y el mercado del equipo)
        if (finSettings.sponsor_id > 0 && finSettings.sponsor_years_remaining > 0)
        {
            var sponsor = DatabaseManager.Instance.GetSponsorById(finSettings.sponsor_id);
            if (sponsor != null)
            {
                long sponsorIncome = ContractMarket.SponsorHomeGameIncome(
                    sponsor.home_game_income, homeTeam.reputation, homeTeam.market_size);
                homeTeam.budget += sponsorIncome;
                DatabaseManager.Instance.UpdateTeamBudget(homeTeam.id, homeTeam.budget);
                SyncMyTeamBudget(homeTeam);
                DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
                {
                    team_id = homeTeam.id,
                    season_id = _season.id,
                    record_type = FinanceRecord.TYPE_SPONSORSHIP,
                    game_day = game.game_day,
                    amount = sponsorIncome
                });
            }
        }

        // TV home game income (E3: escala con la fama y el mercado del equipo)
        if (finSettings.tv_channel_id > 0 && finSettings.tv_years_remaining > 0)
        {
            var tv = DatabaseManager.Instance.GetTVChannelById(finSettings.tv_channel_id);
            if (tv != null)
            {
                long tvIncome = ContractMarket.SponsorHomeGameIncome(
                    tv.home_game_income, homeTeam.reputation, homeTeam.market_size);
                homeTeam.budget += tvIncome;
                DatabaseManager.Instance.UpdateTeamBudget(homeTeam.id, homeTeam.budget);
                SyncMyTeamBudget(homeTeam);
                DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
                {
                    team_id = homeTeam.id,
                    season_id = _season.id,
                    record_type = FinanceRecord.TYPE_TV,
                    game_day = game.game_day,
                    amount = tvIncome
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

        // Top scorer of the game
        var homeScorers = result.home_stats;
        var awayScorers = result.away_stats;
        var allStats = (homeScorers != null ? homeScorers.AsEnumerable() : Enumerable.Empty<GameSimulator.PlayerStatSnapshot>())
            .Concat(awayScorers != null ? awayScorers.AsEnumerable() : Enumerable.Empty<GameSimulator.PlayerStatSnapshot>());
        var topScorer = allStats.OrderByDescending(s => s.points).FirstOrDefault();
        string topScorerName = topScorer?.name ?? mvpName;
        int topScorerPts = topScorer?.points ?? 0;

        string body = NewsTexts.Resultado(_myTeam.name, myScore, rivalName, rivalScore,
            game.game_date ?? "", topScorerName, topScorerPts);
        body += $"\nPabellón: {arenaName} — Asistencia: {attendance:N0} espectadores.";
        if (!string.IsNullOrEmpty(mvpName))
            body += $"\nMVP del partido: {mvpName}.";

        DatabaseManager.Instance.AddMessage(new MessageData
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
            is_read = 0,
            category = NewsCategory.CLUB,
            news_type = NewsType.RESULTADO
        });
    }

    void ProcessGameInjuries(GameSimulator.GameResult result, string gameDate)
    {
        if (result.injuries == null) return;
        foreach (var inj in result.injuries)
        {
            // Avisar solo de las lesiones de jugadores de mi equipo.
            {
                var player = DatabaseManager.Instance.GetPlayerById(inj.player_id);
                if (player != null && player.team_id == _myTeam.id)
                {
                    DatabaseManager.Instance.AddMessage(new MessageData
                    {
                        manager_id = _manager.id,
                        sender_type = 1,
                        sender_id = 0,
                        title = $"Lesión: {player.first_name} {player.last_name}",
                        body = NewsTexts.Lesion($"{player.first_name} {player.last_name}", inj.type, inj.days, false),
                        game_day = _season.current_game_day,
                        game_date = gameDate,
                        created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        is_read = 0,
                        category = NewsCategory.LESIONES,
                        news_type = NewsType.LESION
                    });
                }
            }
        }
    }

    void UpdateManagerStats(int gameDay)
    {
        if (_season == null) return;

        var db = DatabaseManager.Instance.Db;
        var myGames = db.Table<GameData>()
            .Where(g => g.manager_id == _manager.id
                     && g.season_id == _season.id
                     && g.game_type != "preseason"
                     // Excluir la G-League: sus ids de filial pueden coincidir
                     // numéricamente con el id de mi equipo NBA.
                     && g.game_type != GLeagueScheduleGenerator.TYPE_REGULAR
                     && g.game_type != GLeaguePostSeason.TYPE_PLAYOFF
                     && (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id)
                     && g.is_played == 1)
            .OrderBy(g => g.game_day)
            .ThenBy(g => g.id)
            .ToList();

        // Las relaciones solo cambian tras un partido de mi equipo; si hoy mi
        // equipo no jugó (descanso, play-in/playoff de otros equipos o ya
        // eliminado), no se deben aplicar cambios.
        if (!myGames.Any(g => g.game_day == gameDay))
            return;

        // Trust: based on win percentage
        int wins = myGames.Count(g =>
            (g.home_team_id == _myTeam.id && g.home_score > g.away_score) ||
            (g.away_team_id == _myTeam.id && g.away_score > g.home_score));
        int losses = myGames.Count - wins;

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
            is_read = 0,
            category = NewsCategory.LIGA,
            news_type = NewsType.PREMIOS_MES
        });
    }

    void ProcessMonthlyPayroll(int gameDay)
    {
        if (_myTeam == null || _season == null || _manager == null) return;

        // Nómina mensual: 12 plazos de `salario/12` repartidos cada 15 días de la
        // temporada (~181 días). Antes eran 7 pagos al año, así que solo se cobraba
        // ~58% de la nómina y los presupuestos divergían al alza.
        int[] payrollDays = { 1, 16, 31, 46, 61, 76, 91, 106, 121, 136, 151, 166 };
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
        SyncMyTeamBudget(team);

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
                body = NewsTexts.Nomina(_myTeam.name, monthlyPayroll, 0),
                game_day = gameDay,
                game_date = gameDate.ToString("yyyy-MM-dd"),
                created_at = now,
                date_sent = now,
                is_read = 0,
                category = NewsCategory.CLUB,
                news_type = NewsType.NOMINA
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
            SyncMyTeamBudget(team);

            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = team.id,
                season_id = _season.id,
                record_type = FinanceRecord.TYPE_TAX,
                game_day = gameDay,
                // Positivo como el resto de gastos: GetTotalExpenses suma los importes
                // y con signo negativo el balance salía inflado.
                amount = monthlyTax,
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
        // E3: un gran mercado vende más abonos (0.9x - 1.15x).
        float marketMult = 0.90f + MarketEconomy.MarketNormalized(_myTeam.market_size) * 0.25f;
        int numSubscribers = (int)(_myTeam.capacity * (baseRatio + priceFactor) * performanceMult * reputationMult * marketMult * randomFactor);
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
                body = NewsTexts.Abonos(_myTeam.name, numSubscribers, subAmount),
                game_day = gameDay,
                game_date = System.DateTime.Parse(_season.year_start + "-10-22").AddDays(gameDay - 1).ToString("yyyy-MM-dd"),
                created_at = now,
                date_sent = now,
                is_read = 0,
                category = NewsCategory.CLUB,
                news_type = NewsType.ABONOS
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
            if (_lastGameMeta != null) _lastGameMeta.style.display = DisplayStyle.None;
            return;
        }

        _noLastGame.style.display = DisplayStyle.None;
        _lastGameResult.style.display = DisplayStyle.Flex;
        if (_lastGameMeta != null) _lastGameMeta.style.display = DisplayStyle.Flex;

        var home = _allTeams.Find(t => t.id == last.home_team_id);
        var away = _allTeams.Find(t => t.id == last.away_team_id);

        SetTeamLogo(_lastHomeLog, home?.logo, "64x64");
        SetTeamLogo(_lastAwayLog, away?.logo, "64x64");
        _lastHomeName.text = home?.name.ToUpper() ?? "";
        _lastAwayName.text = away?.name.ToUpper() ?? "";
        _lastHomeScore.text = last.home_score.ToString();
        _lastAwayScore.text = last.away_score.ToString();

        if (_lastHomeRecord != null) _lastHomeRecord.text = GetTeamRecordShort(last.home_team_id);
        if (_lastAwayRecord != null) _lastAwayRecord.text = GetTeamRecordShort(last.away_team_id);

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
        var lastHomeTeam = _allTeams.Find(t => t.id == last.home_team_id);

        _lastGameArena.text = lastHomeTeam != null
            ? DatabaseManager.Instance.GetTeamById(lastHomeTeam.id)?.arena ?? ""
            : "";
    }

    string GetTeamRecordShort(int teamId)
    {
        if (_teamRecordCache == null)
        {
            _teamRecordCache = new Dictionary<int, (int w, int l)>();
            void Add(int id, bool won)
            {
                var cur = _teamRecordCache.TryGetValue(id, out var r) ? r : (0, 0);
                _teamRecordCache[id] = won ? (cur.Item1 + 1, cur.Item2) : (cur.Item1, cur.Item2 + 1);
            }
            foreach (var g in _allGames)
            {
                if (g.is_played != 1 || g.game_type != "regular") continue;
                Add(g.home_team_id, g.home_score > g.away_score);
                Add(g.away_team_id, g.away_score > g.home_score);
            }
        }
        var rec = _teamRecordCache.TryGetValue(teamId, out var r2) ? r2 : (0, 0);
        return $"{rec.Item1} - {rec.Item2}";
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
            if (_nextGameMeta != null) _nextGameMeta.style.display = DisplayStyle.None;
            return;
        }

        _noNextGame.style.display = DisplayStyle.None;
        _nextGameResult.style.display = DisplayStyle.Flex;
        if (_nextGameMeta != null) _nextGameMeta.style.display = DisplayStyle.Flex;

        var home = _allTeams.Find(t => t.id == next.home_team_id);
        var away = _allTeams.Find(t => t.id == next.away_team_id);

        SetTeamLogo(_nextHomeLog, home?.logo, "64x64");
        SetTeamLogo(_nextAwayLog, away?.logo, "64x64");
        _nextHomeName.text = home?.name.ToUpper() ?? "";
        _nextAwayName.text = away?.name.ToUpper() ?? "";
        if (_nextHomeRecord != null) _nextHomeRecord.text = GetTeamRecordShort(next.home_team_id);
        if (_nextAwayRecord != null) _nextAwayRecord.text = GetTeamRecordShort(next.away_team_id);
        try
        {
            _nextGameDate.text = System.DateTime.Parse(next.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            _nextGameDate.text = next.game_date;
        }

        // Meta
        var nextHomeTeam = _allTeams.Find(t => t.id == next.home_team_id);

        _nextGameArena.text = nextHomeTeam != null
            ? DatabaseManager.Instance.GetTeamById(nextHomeTeam.id)?.arena ?? ""
            : "";
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
        var standings = BuildStandings(confTeams).Take(10).ToList();

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
        return StandingsHelper.Build(confTeams, _allGames);
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
            var logo32 = TeamImages.Logo(32, team.logo);
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
            (scorer.total_points / (float)scorer.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{scorer.first_name} {scorer.last_name}",
            $"{scorer.games} {(scorer.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
            scorer.player_id);

        var rebounder = teamStats.OrderByDescending(s => (float)s.total_rebounds / s.games).First();
        SetStatCard("StatRebounder", "StatRebounderName", "StatRebounderGames",
            (rebounder.total_rebounds / (float)rebounder.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{rebounder.first_name} {rebounder.last_name}",
            $"{rebounder.games} {(rebounder.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
            rebounder.player_id);

        var assister = teamStats.OrderByDescending(s => (float)s.total_assists / s.games).First();
        SetStatCard("StatAssister", "StatAssisterName", "StatAssisterGames",
            (assister.total_assists / (float)assister.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{assister.first_name} {assister.last_name}",
            $"{assister.games} {(assister.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
            assister.player_id);

        var stealer = teamStats.OrderByDescending(s => (float)s.total_steals / s.games).First();
        SetStatCard("StatStealer", "StatStealerName", "StatStealerGames",
            (stealer.total_steals / (float)stealer.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{stealer.first_name} {stealer.last_name}",
            $"{stealer.games} {(stealer.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
            stealer.player_id);

        var blocker = teamStats.OrderByDescending(s => (float)s.total_blocks / s.games).First();
        SetStatCard("StatBlocker", "StatBlockerName", "StatBlockerGames",
            (blocker.total_blocks / (float)blocker.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{blocker.first_name} {blocker.last_name}",
            $"{blocker.games} {(blocker.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
            blocker.player_id);

        var rated = teamStats.OrderByDescending(s => (float)s.total_rating / s.games).First();
        SetStatCard("StatRated", "StatRatedName", "StatRatedGames",
            (rated.total_rating / (float)rated.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{rated.first_name} {rated.last_name}",
            $"{rated.games} {(rated.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
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
        if (_myTeam == null) return 0;
        return ObjectiveHelper.GetConferenceRank(_myTeam.id, _myTeam.conference, _allTeams, _allGames);
    }

    bool IsObjectiveMetThisSeason()
    {
        if (_myTeam == null) return false;
        return ObjectiveHelper.IsObjectiveMet(_myTeam.objective, GetMyTeamConferenceRank());
    }

    void RefreshTeamStats()
    {
        if (_myTeam == null) return;

        // Objetivo de la temporada
        if (_teamObjective != null)
            _teamObjective.text = _myTeam.objective ?? "--";

        // Estado del objetivo: calcular según posición en conferencia
        int rank = GetMyTeamConferenceRank();
        bool objectiveMet = ObjectiveHelper.IsObjectiveMet(_myTeam.objective, rank);

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
            var logo32 = TeamImages.Logo(32, _myTeam.logo);
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

    /// <summary>Badge con el hilo narrativo activo más relevante del equipo.</summary>
    void RefreshStorylineBadge()
    {
        if (_storylineBadge == null) return;

        var active = DatabaseManager.Instance.GetActiveStorylines(_manager.id, _myTeam.id);

        // Los hilos de jugador cuyo protagonista ya no está en la plantilla no se muestran
        // (el motor los cierra al avanzar el día; esto evita verlos antes).
        var rosterIds = new HashSet<int>(_players.Select(p => p.id));
        active = active.Where(s => s.subject_player_id <= 0 || rosterIds.Contains(s.subject_player_id)).ToList();

        if (active.Count == 0)
        {
            _storylineBadge.style.display = DisplayStyle.None;
            return;
        }

        var top = active.OrderByDescending(s => s.intensity).First();
        _storylineBadge.text = $"HILO: {top.title.ToUpper()}";
        _storylineBadge.tooltip = string.Join("\n", active.Select(s => $"{s.title} (escalón {s.intensity})"));
        _storylineBadge.style.display = DisplayStyle.Flex;
    }

    void RefreshMessages()
    {
        if (_messagesBody == null || _manager == null) return;
        _messagesBody.Clear();
        RefreshStorylineBadge();

        // Solo los últimos: antes se cargaba toda la bandeja en cada refresco.
        var all = DatabaseManager.Instance.GetRecentMessages(_manager.id, 20);
        if (all == null || all.Count == 0)
        {
            var lbl = new Label("NO HAY NOTICIAS");
            lbl.AddToClassList("no-games-text");
            _messagesBody.Add(lbl);
            return;
        }

        var latest = all.OrderByDescending(m => m.id)
                        .Where(m => !m.title.StartsWith("Premios del Mes"))
                        .Take(5).ToList();

        foreach (var msg in latest)
        {
            var item = new VisualElement();
            item.AddToClassList("message-item");

            var textBlock = new VisualElement();
            textBlock.AddToClassList("message-item-text");

            var subj = new Label(msg.title ?? "Sin asunto");
            subj.AddToClassList("message-item-subject");
            textBlock.Add(subj);

            var body = new Label(GetBodyPreview(msg.body));
            body.AddToClassList("message-item-body");
            textBlock.Add(body);

            item.Add(textBlock);
            _messagesBody.Add(item);

            if (msg.is_read == 0)
                DatabaseManager.Instance.MarkMessageRead(msg.id);
        }
    }

    void SetNewsImage(VisualElement icon, MessageData msg)
    {
        if (!string.IsNullOrEmpty(msg.news_type))
        {
            if (SetNewsImageByType(icon, msg)) return;
        }
        SetNewsImageByTitle(icon, msg);
    }

    bool SetNewsImageByType(VisualElement icon, MessageData msg)
    {
        string nt = msg.news_type;

        // Hitos de legado (E1): icono de récord/trofeo.
        if (nt == NewsType.LEGADO)
        {
            var legacyTex = Resources.Load<Texture2D>(MilestoneCatalog.SectionIcon);
            if (legacyTex != null) { icon.style.backgroundImage = new StyleBackground(legacyTex); return true; }
            return false;
        }

        if (nt == NewsType.HITO || nt == NewsType.RACHA_VICTORIAS || nt == NewsType.MALA_RACHA
            || nt == NewsType.CAMPANADA || nt == NewsType.RESULTADO || nt == NewsType.GLEAGUE_CHAMPION
            || nt == NewsType.REVANCHA || nt == NewsType.DINASTIA)
        {
            string teamName = ExtractFirstTeamFromBody(msg.body);
            if (teamName != null)
            {
                var team = _allTeams?.FirstOrDefault(t => t.name == teamName);
                if (team != null && _logoSprites.TryGetValue(team.logo, out var sprite))
                {
                    icon.style.backgroundImage = new StyleBackground(sprite);
                    return true;
                }
            }
            if (nt == NewsType.CAMPANADA)
            {
                var tex = _noticiasSprites.GetValueOrDefault("rechazado");
                if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
            }
            return false;
        }

        if (nt == NewsType.ALLSTAR)
        {
            if (_logoSprites.TryGetValue("all-star-game", out var allStarSprite))
            {
                icon.style.backgroundImage = new StyleBackground(allStarSprite);
                return true;
            }
            return false;
        }

        if (nt == NewsType.TRIPLE_DOBLE || nt == NewsType.EXPLOSION
            || nt == NewsType.FICHAJE || nt == NewsType.HARD_CAP
            // Hilos narrativos de jugador: foto del protagonista
            || nt == NewsType.SEQUIA || nt == NewsType.BAJA_ESTRELLA
            || nt == NewsType.RUMOR_TRASPASO || nt == NewsType.DESCONTENTO)
        {
            string playerName = ExtractFirstFromBody(msg.body, _playerByName.Keys);
            if (playerName != null && _playerByName.TryGetValue(playerName, out var p))
            {
                var ptex = PlayerPhotoHelper.Load(p.id, p.photo);
                if (ptex != null) { icon.style.backgroundImage = new StyleBackground(ptex); return true; }
            }
        }

        if (nt == NewsType.FICHAJE_RECHAZADO || nt == NewsType.FICHAJE_CANCELADO
            || nt == NewsType.OFERTA_RECHAZADA || nt == NewsType.REFIRMA_CANCELADA
            || nt == NewsType.DESPEDIDO || nt == NewsType.EMPLEADO_DESPEDIDO)
        {
            var tex = _noticiasSprites.GetValueOrDefault("despedido");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.RENOVACION || nt == NewsType.REFIRMA_ENVIADA
            || nt == NewsType.AGENTE_LIBRE)
        {
            var tex = _noticiasSprites.GetValueOrDefault("renovacion");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.ENTRENAMIENTO)
        {
            var tex = _noticiasSprites.GetValueOrDefault("entrenamiento");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.RECUPERACION)
        {
            var tex = _noticiasSprites.GetValueOrDefault("recuperado");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.LESION || nt == NewsType.RESERVA_IR)
        {
            var tex = _noticiasSprites.GetValueOrDefault("lesionados");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.INTERCAMBIO || nt == NewsType.TRASPASO || nt == NewsType.SIGN_AND_TRADE
            || nt == NewsType.DEADLINE_SEMANA || nt == NewsType.BUYOUT)
        {
            var tex = _noticiasSprites.GetValueOrDefault("traspaso");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.EMPLEADO_CONTRATADO)
        {
            var tex = _noticiasSprites.GetValueOrDefault("contratado");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.NOMINA || nt == NewsType.ABONOS)
        {
            var tex = _noticiasSprites.GetValueOrDefault("pago-nominas");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.PATROCINIO)
        {
            string sponsorName = ExtractFirstFromBody(msg.body,
                DatabaseManager.Instance.Db.Table<SponsorData>()
                    .Where(s => s.team_id == _myTeam.id && s.is_active == 1)
                    .Select(s => s.name).ToList());
            if (sponsorName != null)
            {
                var sponsor = DatabaseManager.Instance.Db.Table<SponsorData>()
                    .FirstOrDefault(s => s.team_id == _myTeam.id && s.is_active == 1
                                      && s.name == sponsorName);
                if (sponsor != null)
                {
                    var stex = Resources.Load<Texture2D>(sponsor.logo?.Replace(".png", ""));
                    if (stex != null) { icon.style.backgroundImage = new StyleBackground(stex); return true; }
                }
            }
            var texS = _noticiasSprites.GetValueOrDefault("pago-nominas");
            if (texS != null) { icon.style.backgroundImage = new StyleBackground(texS); return true; }
        }

        if (nt == NewsType.TV)
        {
            string channelName = ExtractFirstFromBody(msg.body,
                DatabaseManager.Instance.Db.Table<TvChannelData>()
                    .Where(c => c.team_id == _myTeam.id && c.is_active == 1)
                    .Select(c => c.name).ToList());
            if (channelName != null)
            {
                var channel = DatabaseManager.Instance.Db.Table<TvChannelData>()
                    .FirstOrDefault(c => c.team_id == _myTeam.id && c.is_active == 1
                                       && c.name == channelName);
                if (channel != null)
                {
                    var ctex = Resources.Load<Texture2D>(channel.logo?.Replace(".png", ""));
                    if (ctex != null) { icon.style.backgroundImage = new StyleBackground(ctex); return true; }
                }
            }
            var texT = _noticiasSprites.GetValueOrDefault("pago-nominas");
            if (texT != null) { icon.style.backgroundImage = new StyleBackground(texT); return true; }
        }

        if (nt == NewsType.PRESTAMO_CONCEDIDO || nt == NewsType.PRESTAMO_SALDADO)
        {
            var tex = _noticiasSprites.GetValueOrDefault("prestamo-bancario");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.REMODELACION_INICIADA || nt == NewsType.REMODELACION_COMPLETADA)
        {
            var tex = _noticiasSprites.GetValueOrDefault("obras-finalizadas");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.BIENVENIDA)
        {
            var tex = _noticiasSprites.GetValueOrDefault("bienvenida");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.ELIMINACION_PLAYIN || nt == NewsType.ELIMINACION_PLAYOFFS)
        {
            var tex = _noticiasSprites.GetValueOrDefault("eliminacion");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.PREMIOS_MES)
        {
            var tex = _noticiasSprites.GetValueOrDefault("premios");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.QUEJA || nt == NewsType.PREOCUPACION)
        {
            var tex = _noticiasSprites.GetValueOrDefault("quejas");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        return false;
    }

    void SetNewsImageByTitle(VisualElement icon, MessageData msg)
    {
        if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Resultado:"))
        {
            string rivalName = msg.title.Substring("Resultado: ".Length);
            int parenIdx = rivalName.IndexOf(" (");
            if (parenIdx > 0) rivalName = rivalName.Substring(0, parenIdx);
            var rival = _allTeams?.FirstOrDefault(t => t.name == rivalName);
            if (rival != null && _logoSprites.TryGetValue(rival.logo, out var sprite))
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
                return;
            }
        }
        else if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Patrocinador firmado:"))
        {
            string sponsorName = msg.title.Substring("Patrocinador firmado: ".Length).ToLower();
            var sponsor = DatabaseManager.Instance.Db.Table<SponsorData>()
                .FirstOrDefault(s => s.team_id == _myTeam.id && s.is_active == 1
                                  && s.name.ToLower() == sponsorName);
            if (sponsor != null)
            {
                var stex = Resources.Load<Texture2D>(sponsor.logo?.Replace(".png", ""));
                if (stex != null) { icon.style.backgroundImage = new StyleBackground(stex); return; }
            }
        }
        else if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Derechos televisivos firmados:"))
        {
            string channelName = msg.title.Substring("Derechos televisivos firmados: ".Length).ToLower();
            var channel = DatabaseManager.Instance.Db.Table<TvChannelData>()
                .FirstOrDefault(c => c.team_id == _myTeam.id && c.is_active == 1
                                   && c.name.ToLower() == channelName);
            if (channel != null)
            {
                var ctex = Resources.Load<Texture2D>(channel.logo?.Replace(".png", ""));
                if (ctex != null) { icon.style.backgroundImage = new StyleBackground(ctex); return; }
            }
        }
        else if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Fichaje:"))
        {
            string playerName = msg.title.Substring("Fichaje: ".Length).Trim();
            if (_playerByName.TryGetValue(playerName, out var p))
            {
                var ptex = PlayerPhotoHelper.Load(p.id, p.photo);
                if (ptex != null) { icon.style.backgroundImage = new StyleBackground(ptex); return; }
            }
        }
        else if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Hard cap activado"))
        {
            string playerName = ExtractFirstFromBody(msg.body, _playerByName.Keys);
            if (playerName != null && _playerByName.TryGetValue(playerName, out var p))
            {
                var ptex = PlayerPhotoHelper.Load(p.id, p.photo);
                if (ptex != null) { icon.style.backgroundImage = new StyleBackground(ptex); return; }
            }
        }
        else if (msg.sender_type == 2)
        {
            string teamName = ExtractFirstTeamFromBody(msg.body);
            if (teamName != null)
            {
                var team = _allTeams?.FirstOrDefault(t => t.name == teamName);
                if (team != null && _logoSprites.TryGetValue(team.logo, out var sprite))
                {
                    icon.style.backgroundImage = new StyleBackground(sprite);
                    return;
                }
            }
        }
        else
        {
            string key = GetNoticiasKey(msg.title);
            if (key != null)
            {
                var tex = _noticiasSprites.GetValueOrDefault(key);
                if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return; }
            }
        }
    }

    string ExtractFirstFromBody(string body, IEnumerable<string> knownNames)
    {
        if (string.IsNullOrEmpty(body)) return null;
        int earliest = int.MaxValue;
        string found = null;
        foreach (var name in knownNames)
        {
            int idx = body.IndexOf(name, StringComparison.Ordinal);
            if (idx >= 0 && idx < earliest)
            {
                earliest = idx;
                found = name;
            }
        }
        return found;
    }

    string ExtractFirstTeamFromBody(string body)
    {
        if (string.IsNullOrEmpty(body) || _allTeams == null) return null;
        int earliest = int.MaxValue;
        string found = null;
        foreach (var team in _allTeams)
        {
            int idx = body.IndexOf(team.name, StringComparison.Ordinal);
            if (idx >= 0 && idx < earliest)
            {
                earliest = idx;
                found = team.name;
            }
        }
        return found;
    }

    static string GetBodyPreview(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        string normalized = body.Replace("\r\n", "\n").Trim();
        int idx = normalized.IndexOf("\n\n", StringComparison.Ordinal);
        string preview = idx < 0 ? normalized : normalized.Substring(0, idx).Trim();
        if (idx >= 0 && preview.Length > 0)
        {
            char lastChar = preview[preview.Length - 1];
            if (lastChar != '.' && lastChar != '!' && lastChar != '?' && lastChar != '\u2026')
                preview += "\u2026";
        }
        return preview;
    }

    string GetNoticiasKey(string title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        if (title.StartsWith("Bienvenido")) return "bienvenida";
        if (title.StartsWith("Recuperado")) return "recuperado";
        if (title.StartsWith("Queja") || title.StartsWith("Preocupación")) return "quejas";
        if (title.StartsWith("Fichaje:")) return "contratado";
        if (title.Contains("Nuevo empleado")) return "contratado";
        if (title.StartsWith("Fichaje rechazado")) return "rechazado";
        if (title.Contains("cancelado") || title.Contains("rechazado") || title.Contains("despedido")) return "despedido";
        if (title.Contains("Entrenamiento")) return "entrenamiento";
        if (title.Contains("Lesión") || title.Contains("RESERVA")) return "lesionados";
        if (title.Contains("Remodelación")) return "obras-finalizadas";
        if (title.Contains("nóminas") || title.Contains("abonos")) return "pago-nominas";
        if (title.Contains("Préstamo")) return "prestamo-bancario";
        if (title.Contains("renovado") || title.Contains("Re-firma") || title.Contains("Oferta rechazada")) return "renovacion";
        if (title.Contains("Traspaso") || title.Contains("Sign & Trade") || title.Contains("Intercambio")
            || title.Contains("Última semana") || title.Contains("OFERTAS") || title.Contains("Rescisión")
            || title.Contains("Hard cap") || title.Contains("buyout")) return "traspaso";
        return null;
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
            var sprite = TeamImages.Logo(sizeFolder, logoName);
            if (sprite != null)
            {
                elem.style.backgroundImage = new StyleBackground(sprite);
                return;
            }
        }

        if (_logoSprites.TryGetValue(logoName, out var fallback))
            elem.style.backgroundImage = new StyleBackground(fallback);
    }

    /// <summary>
    /// Convocatoria del partido de las estrellas: los **3 mejores de cada posición por
    /// valoración media de la temporada regular** (15 por conferencia), excluyendo a los
    /// lesionados. Si un jugador aún no tiene partidos (valoración 0) sirve el overall como
    /// desempate.
    /// </summary>
    /// <summary>
    /// Reparto de los 240 minutos del partido de exhibición entre los convocados (todos deben
    /// jugar). Marca `rotation_minutes` de forma vinculante para que el simulador lo respete.
    /// </summary>
    void AssignAllStarMinutes(List<PlayerData> players)
    {
        if (players == null || players.Count == 0) return;

        const int TEAM_MINUTES = 240;
        int n = players.Count;
        int baseMin = TEAM_MINUTES / n;          // 16 con 15 jugadores
        int remainder = TEAM_MINUTES - baseMin * n;

        for (int i = 0; i < n; i++)
            players[i].rotation_minutes = baseMin + (i < remainder ? 1 : 0);
    }

    List<PlayerData> BuildAllStarRoster(string conference)
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var conferenceTeamIds = allTeams
            .Where(t => t.conference == conference)
            .Select(t => t.id)
            .ToHashSet();

        var candidates = DatabaseManager.Instance.Db.Table<PlayerData>()
            .Where(p => p.team_id != 0 && p.injury_days == 0 && p.g_league_assigned == 0)
            .ToList()
            .Where(p => conferenceTeamIds.Contains(p.team_id))
            .ToList();

        // Valoración media (rating) de la temporada regular por jugador.
        var avgRating = new Dictionary<int, double>();
        var gamesPlayed = new Dictionary<int, int>();
        foreach (var s in DatabaseManager.Instance.GetSeasonPlayerStatsAggregates(_manager.id, _season.id))
        {
            avgRating[s.player_id] = s.gp > 0 ? (double)s.total_rating / s.gp : 0;
            gamesPlayed[s.player_id] = s.gp;
        }

        var roster = new List<PlayerData>();
        var usedIds = new HashSet<int>();

        // Por posición se toman los 3 mejores por valoración: el 1º de cada puesto es titular
        // y el 2º/3º suplentes. Los 5 titulares (mejor B/E/A/AP/P) van primero para que
        // `Take(5)` y `SaveAllStarRoster` (i < 5) los identifiquen correctamente.
        var byPosition = new Dictionary<string, List<PlayerData>>();
        foreach (var pos in PositionCodes.Order)
        {
            var selected = candidates
                .Where(p => p.position == pos)
                .OrderByDescending(p => avgRating.TryGetValue(p.id, out var r) ? r : 0)
                .ThenByDescending(p => gamesPlayed.TryGetValue(p.id, out var g) ? g : 0)
                .ThenByDescending(p => p.overall)
                .Take(3)
                .ToList();
            byPosition[pos] = selected;
            foreach (var p in selected) usedIds.Add(p.id);
        }

        // 5 titulares: el mejor de cada posición principal.
        foreach (var pos in PositionCodes.Order)
            if (byPosition[pos].Count > 0) roster.Add(byPosition[pos][0]);

        // Suplentes: el 2º/3º de cada posición.
        foreach (var pos in PositionCodes.Order)
            for (int i = 1; i < byPosition[pos].Count; i++)
                roster.Add(byPosition[pos][i]);

        // Relleno si alguna posición no llega a 3 (por valoración, luego overall).
        if (roster.Count < 15)
        {
            var remaining = candidates
                .Where(p => !usedIds.Contains(p.id))
                .OrderByDescending(p => avgRating.TryGetValue(p.id, out var r) ? r : 0)
                .ThenByDescending(p => p.overall)
                .Take(15 - roster.Count);
            roster.AddRange(remaining);
        }

        return roster;
    }

    // ── CLASE AUXILIAR ───────────────────────────────────

    // StandingRow vive en Scripts/Core/StandingsHelper.cs (compartida con
    // Standings, Play-In/Playoffs y ObjectiveHelper).

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
