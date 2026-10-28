using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
    public partial class DashboardController : UIScreenController
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
    private bool _dayAborted;

    private sealed class GameDayRunState
    {
        public int gameDay;
        public bool fastSim;
        public bool effectiveSkipPreBatch;
        public bool navigateToPreGame;
        public List<GameData> gamesToday;
        public bool myTeamPlays;
        public bool hasAllStar;
    }
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
        var titleLbl = new Label(LocalizationService.T("ui.code.dashboard_controller.logro_desbloqueado"));
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

        // Enlaces del pie de los paneles: se enlazan por nombre de UXML, no por
        // texto, porque RegisterCallbacks corre antes de LocalizationService.Apply
        // y los labels todavía contienen los tokens {loc:...}.
        var footerLinks = new (string name, GameScreen screen)[]
        {
            ("LastGameFooterLink", GameScreen.Results),
            ("NextGameFooterLink", GameScreen.PreGame),
            ("StandingsFooterLink", GameScreen.Standings),
            ("MessagesFooterLink", GameScreen.Messages),
        };
        foreach (var (name, screen) in footerLinks)
        {
            var linkLabel = _root.Q<Label>(name);
            if (linkLabel == null) continue;

            // Toda la zona del pie es clicable, no solo el texto.
            var clickable = linkLabel.parent ?? linkLabel;
            clickable.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                ScreenManager.Instance.GoTo(screen);
            });
            CursorManager.Instance?.RegisterHandCursor(clickable);
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
        _headerManagerName.text = LocalizationService.T("ui.common.manager", _manager.name);
        _headerBudget.text = $"${_myTeam.budget / 1_000_000}M";
        _headerBudget.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        // Masa salarial real (jugadores + empleados)
        var teamEmployees = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        long totalPayroll = DatabaseManager.Instance.GetEffectiveTeamPayroll(_myTeam.id);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        // Margen salarial = Cap - solo jugadores
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - totalPayroll;

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
            _headerSeason.text = LocalizationService.T("ui.common.season", _season.year_start, _season.year_end);
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

}
