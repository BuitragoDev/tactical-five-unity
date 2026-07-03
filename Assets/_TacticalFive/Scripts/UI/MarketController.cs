using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class MarketController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private Button _btnNegotiate;
    private VisualElement _tradePanels;
    private VisualElement _freeAgentsPanel;
    private VisualElement _teamGrid;
    private VisualElement _myTeamBody;
    private VisualElement _otherTeamBody;
    private VisualElement _myPicksBody;
    private VisualElement _otherPicksBody;
    private VisualElement _myTableHeader;
    private VisualElement _otherTableHeader;
    private VisualElement _freeAgentsBody;
    private VisualElement _freeAgentsTableHeader;
    private VisualElement _tradeStatus;
    private VisualElement _marketClosedOverlay;
    private VisualElement _faRosterFullModal;
    private Label _faRosterFullText;
    private VisualElement _faRosterFullIcon;
    private VisualElement _faConfirmModal;
    private Button _btnCloseFARosterFull;
    private Button _btnCancelFA;
    private Button _btnConfirmFA;
    private Label _faPlayerName;
    private Label _faText1;
    private Label _faText2;
    private VisualElement _faIcon;
    private Label _faSalaryValue;
    private Label _faSalaryDec;
    private Label _faSalaryInc;
    private Label _faYearsValue;
    private Label _faYearsDec;
    private Label _faYearsInc;
    private Label _faWarningText;
    private Label _faMaxInfo;
    private Label _faPendingText;
    private VisualElement _faFormRowSalary;
    private VisualElement _faFormRowYears;
    private long _faMaxSalary;
    private long _faSalary;
    private int _faYears;
    private bool _faOfferSent;
    private VisualElement _tradeSuccessOverlay;
    private VisualElement _tradeSuccessBox;
    private VisualElement _tradeSuccessIcon;
    private Label _tradeSuccessTitle;
    private Label _tradeSuccessText1;
    private Label _tradeSuccessText2;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams = new();
    private List<PlayerData> _myPlayers;
    private List<PlayerData> _otherPlayers;
    private List<DraftPickData> _myPicks;
    private List<DraftPickData> _otherPicks;
    private List<PlayerData> _freeAgents = new();
    private int _myTotalRosterCount;
    private int _otherTotalRosterCount;
    private TeamData _selectedTeam;
    private bool _isFA = false;
    private PlayerData _pendingFAPlayer;

    private Dictionary<string, Sprite> _headerLogos = new();
    private Dictionary<string, Sprite> _teamGridLogos = new();
    private Dictionary<string, Sprite> _tradePanelLogos = new();
    private HashSet<int> _selectedMyPlayers = new();
    private HashSet<int> _selectedOtherPlayers = new();
    private HashSet<int> _selectedMyPicks = new();
    private HashSet<int> _selectedOtherPicks = new();
    private bool _signAndTradeActive = false;
    private List<PlayerData> _signAndTradeCandidates = new();

    // NBA salary thresholds (use TradeHelper constants as source of truth)

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
        InitTableHeaders();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _btnNegotiate = _root.Q<Button>("BtnNegotiate");
        _tradePanels = _root.Q<VisualElement>("TradePanels");
        _freeAgentsPanel = _root.Q<VisualElement>("FreeAgentsPanel");
        _teamGrid = _root.Q<VisualElement>("TeamGrid");
        _myTeamBody = _root.Q<VisualElement>("MyTeamBody");
        _otherTeamBody = _root.Q<VisualElement>("OtherTeamBody");
        _myPicksBody = _root.Q<VisualElement>("MyPicksBody");
        _otherPicksBody = _root.Q<VisualElement>("OtherPicksBody");
        _myTableHeader = _root.Q<VisualElement>("MyTableHeader");
        _otherTableHeader = _root.Q<VisualElement>("OtherTableHeader");
        _freeAgentsBody = _root.Q<VisualElement>("FreeAgentsBody");
        _freeAgentsTableHeader = _root.Q<VisualElement>("FreeAgentsTableHeader");
        _tradeStatus = _root.Q<VisualElement>("TradeStatus");
        _marketClosedOverlay = _root.Q<VisualElement>("MarketClosedOverlay");
        _faRosterFullModal = _root.Q<VisualElement>("FARosterFullModal");
        _faRosterFullText = _root.Q<Label>("FARosterFullText");
        _faRosterFullIcon = _root.Q<VisualElement>("FARosterFullIcon");
        _faConfirmModal = _root.Q<VisualElement>("FAConfirmModal");
        _btnCloseFARosterFull = _root.Q<Button>("BtnCloseFARosterFull");
        _btnCancelFA = _root.Q<Button>("BtnCancelFA");
        _btnConfirmFA = _root.Q<Button>("BtnConfirmFA");
        _faPlayerName = _root.Q<Label>("FATitle");
        _faText1 = _root.Q<Label>("FAText1");
        _faText2 = _root.Q<Label>("FAText2");
        _faIcon = _root.Q<VisualElement>("FAIcon");
        _faSalaryValue = _root.Q<Label>("FASalaryValue");
        _faSalaryDec = _root.Q<Label>("FASalaryDec");
        _faSalaryInc = _root.Q<Label>("FASalaryInc");
        _faYearsValue = _root.Q<Label>("FAYearsValue");
        _faYearsDec = _root.Q<Label>("FAYearsDec");
        _faYearsInc = _root.Q<Label>("FAYearsInc");
        _faWarningText = _root.Q<Label>("FAWarningText");
        _faMaxInfo = _root.Q<Label>("FAMaxInfo");
        _faPendingText = _root.Q<Label>("FAPendingText");
        _faFormRowSalary = _root.Q<VisualElement>("FAFormRowSalary");
        _faFormRowYears = _root.Q<VisualElement>("FAFormRowYears");
        _tradeSuccessOverlay = _root.Q<VisualElement>("TradeSuccessOverlay");
        _tradeSuccessBox = _root.Q<VisualElement>("TradeSuccessBox");
        _tradeSuccessIcon = _root.Q<VisualElement>("TradeSuccessIcon");
        _tradeSuccessTitle = _root.Q<Label>("TradeSuccessTitle");
        _tradeSuccessText1 = _root.Q<Label>("TradeSuccessText1");
        _tradeSuccessText2 = _root.Q<Label>("TradeSuccessText2");

        if (_tradeSuccessOverlay != null) _tradeSuccessOverlay.style.display = DisplayStyle.None;
    }

    void LoadData()
    {
        var headerLogos = Resources.LoadAll<Sprite>("Teams/Logos/64x64/");
        foreach (var s in headerLogos) _headerLogos[s.name] = s;

        var teamGridLogos = Resources.LoadAll<Sprite>("Teams/Logos/64x64/");
        foreach (var s in teamGridLogos) _teamGridLogos[s.name] = s;

        var tradePanelLogos = Resources.LoadAll<Sprite>("Teams/Logos/32x32/");
        foreach (var s in tradePanelLogos) _tradePanelLogos[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        if (_myTeam == null) return;
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams()
            .Where(t => t.id != _myTeam.id)
            .OrderBy(t => t.name)
            .ToList();
        _freeAgents = DatabaseManager.Instance.GetFreeAgents();
    }

    void RegisterCallbacks()
    {
        // Sidebar unificado
        SidebarController.Attach(_root, GameScreen.Market);
        HeaderController.Attach(_root);
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _btnNegotiate?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OnNegotiate(); });
        _btnCloseFARosterFull?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); _faRosterFullModal.style.display = DisplayStyle.None; });
        _btnCancelFA?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); _faConfirmModal.style.display = DisplayStyle.None; BuildFreeAgents(); });
        _btnConfirmFA?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); SendFAOffer(); });

        // Spinner buttons (long-press para incrementar/decrementar)
        SetupFALongPress(_faSalaryDec, () => StepFASalary(-1));
        SetupFALongPress(_faSalaryInc, () => StepFASalary(1));
        SetupFALongPress(_faYearsDec, () => StepFAYears(-1));
        SetupFALongPress(_faYearsInc, () => StepFAYears(1));

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetDefaultCursor();
            RegisterHandCursors();
        }
    }

    void RegisterHandCursors()
    {
        foreach (var btn in _root.Query<Button>(null, "nav-item").Build())
            CursorManager.Instance.RegisterHandCursor(btn);
        foreach (var btn in _root.Query<Button>(null, "nav-submenu-item").Build())
            CursorManager.Instance.RegisterHandCursor(btn);

        var cursorTargets = new[] { "BtnAction", "BtnNegotiate", "ConfigIcon", "BtnCloseFARosterFull", "BtnCancelFA", "BtnConfirmFA",
            "FASalaryDec", "FASalaryInc", "FAYearsDec", "FAYearsInc",
            "NavDashboard", "NavRoster", "NavCalendar", "NavResults", "NavStandings",
            "NavPalmares", "NavPlayoffs", "NavStats", "NavMarket", "NavFinances",
            "NavArena", "NavMessages" };
        foreach (var name in cursorTargets)
        {
            var el = _root.Q<VisualElement>(name);
            if (el != null)
                CursorManager.Instance.RegisterHandCursor(el);
        }

        // Dynamic elements registered individually at creation time
    }

    void RegisterNavButtons()
    {
        var allSubmenus = new[]
        {
            _root.Q<VisualElement>("RosterSubmenu"),
            _root.Q<VisualElement>("PalmaresSubmenu"),
            _root.Q<VisualElement>("MarketSubmenu"),
            _root.Q<VisualElement>("FinanceSubmenu"),
        };

        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("RosterSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
        });
        _root.Q<Button>("SubmenuJugadores")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        _root.Q<Button>("SubmenuEmpleados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Employees); });
        _root.Q<Button>("SubmenuLesionados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Injured); });
        _root.Q<Button>("SubmenuQuinteto")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Quinteto); });
        _root.Q<Button>("SubmenuVestuario")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Vestuario); });
        _root.Q<Button>("SubmenuEntrenamiento")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Training); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("PalmaresSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
        });
        _root.Q<Button>("SubmenuPalmares")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Palmares); });
        _root.Q<Button>("SubmenuRecords")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Records); });
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });

        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("MarketSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
        });

        _root.Q<Button>("SubmenuOfertas")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Market);
        });
        _root.Q<Button>("SubmenuCartera")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Cartera); });
        _root.Q<Button>("SubmenuHistorial")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Historial); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("FinanceSubmenu");
            if (submenu == null) return;
            bool opening = !submenu.ClassListContains("nav-submenu--visible");
            foreach (var s in allSubmenus)
                if (s != null && s != submenu)
                    s.RemoveFromClassList("nav-submenu--visible");
            submenu.EnableInClassList("nav-submenu--visible", opening);
        });
        _root.Q<Button>("SubmenuDecisiones")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Finances);
        });
        _root.Q<Button>("SubmenuPrestamos")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Loans);
        });
        _root.Q<Button>("SubmenuSponsors")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("SubmenuTV")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });
    }

    void LoadSidebarIcons()
    {
        var iconMap = new Dictionary<string, string>
        {
            {"NavDashboardIcon", "inicio"},
            {"NavRosterIcon", "plantilla"},
            {"NavCalendarIcon", "calendario"},
            {"NavStandingsIcon", "clasificacion"},
            {"NavPalmaresIcon", "palmares"},
            {"NavResultsIcon", "resultados"},
            {"NavPlayoffsIcon", "playoff"},
            {"NavStatsIcon", "estadisticas"},
            {"NavMarketIcon", "mercado"},
            {"NavFinancesIcon", "finanzas"},
            {"NavArenaIcon", "pabellon"},
            {"NavMessagesIcon", "mensajes"},

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

    void Refresh()
    {
        _root.Q<Button>("SubmenuOfertas")?.AddToClassList("nav-submenu-item--active");
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Market] RefreshHeader error: {ex.Message}"); }

        bool windowOpen = IsTransferWindowOpen();
        _teamGrid.style.display = windowOpen ? DisplayStyle.Flex : DisplayStyle.None;
        _btnNegotiate.style.display = DisplayStyle.None;
        _tradePanels.style.display = DisplayStyle.None;
        _freeAgentsPanel.style.display = DisplayStyle.None;
        _marketClosedOverlay.style.display = windowOpen ? DisplayStyle.None : DisplayStyle.Flex;

        if (!windowOpen) return;

        BuildTeamGrid();
        if (_isFA)
        {
            ShowFreeAgents();
        }
        else if (_selectedTeam != null)
        {
            ShowNegotiateButton();
        }
    }

    bool IsTransferWindowOpen()
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date))
            return true;

        if (System.DateTime.TryParse(_season.current_date, out var date))
        {
            var openDate = new System.DateTime(_season.year_start, 9, 1);
            var closeDate = new System.DateTime(_season.year_end, 2, 8);
            return date >= openDate && date <= closeDate;
        }
        return true;
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_headerLogos.TryGetValue(_myTeam.logo, out var sprite))
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

        _btnAction.text = "DASHBOARD";
    }

    // ═══════════════════════════════════════════
    //  TEAM GRID
    // ═══════════════════════════════════════════

    void BuildTeamGrid()
    {
        _teamGrid.Clear();

        foreach (var team in _allTeams)
        {
            var chip = new VisualElement();
            chip.AddToClassList("market-team-chip");
            if (_selectedTeam != null && _selectedTeam.id == team.id)
                chip.AddToClassList("active");

            var logo = new VisualElement();
            logo.AddToClassList("market-team-chip-logo");
            if (_teamGridLogos.TryGetValue(team.logo, out var sprite))
                logo.style.backgroundImage = new StyleBackground(sprite);
            chip.Add(logo);

            int teamId = team.id;
            chip.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectTeam(teamId); });
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(chip);

            _teamGrid.Add(chip);
        }

        // FA button
        var faChip = new VisualElement();
        faChip.AddToClassList("market-team-chip");
        if (_isFA) faChip.AddToClassList("active");

        var faLabel = new Label("FA");
        faLabel.AddToClassList("market-team-chip-fa");
        faChip.Add(faLabel);

        faChip.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectFA(); });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(faChip);
        _teamGrid.Add(faChip);
    }

    void ShowNegotiateButton()
    {
        if (_btnNegotiate == null || _selectedTeam == null) return;
        _btnNegotiate.style.display = DisplayStyle.Flex;
        _tradePanels.style.display = DisplayStyle.None;
        _freeAgentsPanel.style.display = DisplayStyle.None;
        _root.Q<Label>("NegotiateBtnText").text = $"NEGOCIAR CON {_selectedTeam.name.ToUpper()}";
    }

    void SelectTeam(int teamId)
    {
        _selectedTeam = _allTeams.Find(t => t.id == teamId);
        _isFA = false;
        ShowNegotiateButton();
        BuildTeamGrid();
    }

    void SelectFA()
    {
        _selectedTeam = null;
        _isFA = true;
        _btnNegotiate.style.display = DisplayStyle.None;
        _tradePanels.style.display = DisplayStyle.None;
        _freeAgentsPanel.style.display = DisplayStyle.Flex;
        BuildTeamGrid();
        BuildFreeAgents();
    }

    // ═══════════════════════════════════════════
    //  NEGOTIATE
    // ═══════════════════════════════════════════

    void OnNegotiate()
    {
        _btnNegotiate.style.display = DisplayStyle.None;
        _tradePanels.style.display = DisplayStyle.Flex;
        _selectedMyPlayers.Clear();
        _selectedOtherPlayers.Clear();
        _selectedMyPicks.Clear();
        _selectedOtherPicks.Clear();
        LoadTradeData();
    }

    void LoadTradeData()
    {
        _myPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id)
            .Where(p => p.injury_days == 0)
            .OrderByDescending(p => p.overall)
            .ToList();
        _otherPlayers = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeam.id)
            .Where(p => p.injury_days == 0)
            .OrderByDescending(p => p.overall)
            .ToList();
        _myTotalRosterCount = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id).Count;
        _otherTotalRosterCount = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeam.id).Count;

        var activeSeason = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        if (activeSeason != null)
        {
            int activeSeasonId = activeSeason.id;
            var allPicksForSeason = DatabaseManager.Instance.GetDraftPicksForSeason(activeSeasonId);
            _myPicks = allPicksForSeason.Where(p => p.current_team_id == _myTeam.id).ToList();
            _otherPicks = allPicksForSeason.Where(p => p.current_team_id == _selectedTeam.id).ToList();
        }
        else
        {
            _myPicks = new List<DraftPickData>();
            _otherPicks = new List<DraftPickData>();
        }

        // Set team logos and names
        if (_tradePanelLogos.TryGetValue(_myTeam.logo, out var mySprite))
            _root.Q<VisualElement>("MyTeamLogo").style.backgroundImage = new StyleBackground(mySprite);
        _root.Q<Label>("MyTeamName").text = _myTeam.name.ToUpper();

        if (_tradePanelLogos.TryGetValue(_selectedTeam.logo, out var otherSprite))
            _root.Q<VisualElement>("OtherTeamLogo").style.backgroundImage = new StyleBackground(otherSprite);
        _root.Q<Label>("OtherTeamName").text = _selectedTeam.name.ToUpper();

        InitTableHeaders();
        BuildTradeTable();
        BuildPicksPanel();
        UpdateSummary();
    }

    void InitTableHeaders()
    {
        if (_myTableHeader != null && _myTableHeader.childCount == 0)
        {
            PopulateTableHeader(_myTableHeader);
        }
        if (_otherTableHeader != null && _otherTableHeader.childCount == 0)
        {
            PopulateTableHeader(_otherTableHeader);
        }
        if (_freeAgentsTableHeader != null && _freeAgentsTableHeader.childCount == 0)
        {
            PopulateTableHeader(_freeAgentsTableHeader);
        }
    }

    void PopulateTableHeader(VisualElement header)
    {
        header.Clear();

        var name = new Label("JUGADOR");
        name.AddToClassList("market-table-header-label");
        name.AddToClassList("market-table-header-name");
        header.Add(name);

        var pos = new Label("POS");
        pos.AddToClassList("market-table-header-label");
        pos.AddToClassList("market-table-header-pos");
        header.Add(pos);

        var ovr = new Label("OVR");
        ovr.AddToClassList("market-table-header-label");
        ovr.AddToClassList("market-table-header-ovr");
        header.Add(ovr);

        var salary = new Label("SALARIO");
        salary.AddToClassList("market-table-header-label");
        salary.AddToClassList("market-table-header-salary");
        header.Add(salary);

        if (header == _freeAgentsTableHeader)
        {
            var action = new Label("");
            action.AddToClassList("market-table-header-label");
            action.AddToClassList("market-table-header-action");
            header.Add(action);
        }
    }

    void BuildTradeTable()
    {
        _myTeamBody.Clear();
        _otherTeamBody.Clear();

        foreach (var p in _myPlayers)
        {
            var row = CreateTradeRow(p, true);
            _myTeamBody.Add(row);
        }

        foreach (var p in _otherPlayers)
        {
            var row = CreateTradeRow(p, false);
            _otherTeamBody.Add(row);
        }
    }

    void BuildPicksPanel()
    {
        if (_myPicksBody != null) _myPicksBody.Clear();
        if (_otherPicksBody != null) _otherPicksBody.Clear();
        BuildPicksList(_myPicks, _myPicksBody, _selectedMyPicks, true);
        BuildPicksList(_otherPicks, _otherPicksBody, _selectedOtherPicks, false);
    }

    void BuildPicksList(List<DraftPickData> picks, VisualElement body, HashSet<int> selectedSet, bool isMyTeam)
    {
        if (body == null) return;
        if (picks == null || picks.Count == 0)
        {
            var empty = new Label("Sin picks disponibles");
            empty.AddToClassList("market-trade-empty");
            empty.style.color = new StyleColor(new Color32(120, 130, 150, 255));
            empty.style.fontSize = 10;
            empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            empty.style.paddingTop = 4;
            body.Add(empty);
            return;
        }

        foreach (var pk in picks.OrderBy(p => p.round).ThenBy(p => p.pick_number))
        {
            var row = new VisualElement();
            row.AddToClassList("market-pick-row");
            if (selectedSet.Contains(pk.id)) row.AddToClassList("selected");
            row.userData = pk;

            int pickId = pk.id;
            var lbl = new Label($"R{pk.round} #{pk.pick_number}");
            lbl.AddToClassList("market-pick-label");
            lbl.style.color = new StyleColor(new Color32(236, 240, 241, 255));
            lbl.style.fontSize = 11;
            lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
            lbl.style.flexGrow = 1;
            lbl.style.minWidth = 70;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(lbl);

            var round = new Label($"R{pk.round}");
            round.AddToClassList("market-pick-round");
            round.style.color = new StyleColor(new Color32(241, 196, 15, 255));
            round.style.fontSize = 10;
            round.style.unityTextAlign = TextAnchor.MiddleRight;
            round.style.unityFontStyleAndWeight = FontStyle.Bold;
            round.style.width = 24;
            row.Add(round);

            row.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                if (selectedSet.Contains(pickId))
                    selectedSet.Remove(pickId);
                else
                    selectedSet.Add(pickId);
                row.EnableInClassList("selected", selectedSet.Contains(pickId));
                UpdateSummary();
                CheckTradeStatus();
            });
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(row);
            body.Add(row);
        }
    }

    VisualElement CreateTableHeader()
    {
        var header = new VisualElement();
        header.AddToClassList("market-table-header");

        var name = new Label("JUGADOR");
        name.AddToClassList("market-table-header-label");
        name.AddToClassList("market-table-header-name");
        header.Add(name);

        var pos = new Label("POS");
        pos.AddToClassList("market-table-header-label");
        pos.AddToClassList("market-table-header-pos");
        header.Add(pos);

        var ovr = new Label("OVR");
        ovr.AddToClassList("market-table-header-label");
        ovr.AddToClassList("market-table-header-ovr");
        header.Add(ovr);

        var salary = new Label("SALARIO");
        salary.AddToClassList("market-table-header-label");
        salary.AddToClassList("market-table-header-salary");
        header.Add(salary);

        return header;
    }

    VisualElement CreateTradeRow(PlayerData player, bool isMyTeam)
    {
        var row = new VisualElement();
        row.AddToClassList("market-trade-row");
        row.userData = player;

        int playerId = player.id;
        bool myTeam = isMyTeam;
        var selectedSet = isMyTeam ? _selectedMyPlayers : _selectedOtherPlayers;

        if (selectedSet.Contains(playerId))
            row.AddToClassList("selected");

        row.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            if (selectedSet.Contains(playerId))
                selectedSet.Remove(playerId);
            else
                selectedSet.Add(playerId);
            row.EnableInClassList("selected", selectedSet.Contains(playerId));
            UpdateSummary();
            CheckTradeStatus();
        });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(row);

        var name = new Label($"{player.first_name} {player.last_name}");
        name.AddToClassList("market-trade-col-name");
        row.Add(name);

        var pos = new Label(player.position);
        pos.AddToClassList("market-trade-col-pos");
        row.Add(pos);

        var ovr = new Label(player.overall.ToString());
        ovr.AddToClassList("market-trade-col-ovr");
        row.Add(ovr);

        var salary = new Label($"${player.salary:N0}");
        salary.AddToClassList("market-trade-col-salary");
        row.Add(salary);

        return row;
    }

    // ═══════════════════════════════════════════
    //  TRADE SUMMARY
    // ═══════════════════════════════════════════

    void UpdateSummary()
    {
        var mySalaryOut = _myPlayers.Where(p => _selectedMyPlayers.Contains(p.id)).Sum(p => p.salary);
        var otherSalaryOut = _otherPlayers.Where(p => _selectedOtherPlayers.Contains(p.id)).Sum(p => p.salary);

        var myCount = _myTotalRosterCount - _selectedMyPlayers.Count + _selectedOtherPlayers.Count;
        var otherCount = _otherTotalRosterCount - _selectedOtherPlayers.Count + _selectedMyPlayers.Count;

        var myBaseMargin = GetSalaryMargin(_myTeam);
        var otherBaseMargin = GetSalaryMargin(_selectedTeam);
        var myNewMargin = myBaseMargin + mySalaryOut - otherSalaryOut;
        var otherNewMargin = otherBaseMargin + otherSalaryOut - mySalaryOut;

        _root.Q<Label>("SummaryMyTeamName").text = _myTeam.name.ToUpper();
        _root.Q<Label>("SummaryMyPlayerCount").text = myCount.ToString();
        _root.Q<Label>("SummaryMySalaryMargin").text = $"${myNewMargin:N0}";
        _root.Q<Label>("SummaryMySalaryMargin").RemoveFromClassList("market-summary-positive");
        _root.Q<Label>("SummaryMySalaryMargin").RemoveFromClassList("market-summary-negative");
        _root.Q<Label>("SummaryMySalaryMargin").AddToClassList(myNewMargin >= 0 ? "market-summary-positive" : "market-summary-negative");
        _root.Q<Label>("SummaryMySalaryOut").text = $"${mySalaryOut:N0}";
        _root.Q<Label>("SummaryMySalaryIn").text = $"${otherSalaryOut:N0}";

        _root.Q<Label>("SummaryOtherTeamName").text = _selectedTeam.name.ToUpper();
        _root.Q<Label>("SummaryOtherPlayerCount").text = otherCount.ToString();
        _root.Q<Label>("SummaryOtherSalaryMargin").text = $"${otherNewMargin:N0}";
        _root.Q<Label>("SummaryOtherSalaryMargin").RemoveFromClassList("market-summary-positive");
        _root.Q<Label>("SummaryOtherSalaryMargin").RemoveFromClassList("market-summary-negative");
        _root.Q<Label>("SummaryOtherSalaryMargin").AddToClassList(otherNewMargin >= 0 ? "market-summary-positive" : "market-summary-negative");
        _root.Q<Label>("SummaryOtherSalaryOut").text = $"${otherSalaryOut:N0}";
        _root.Q<Label>("SummaryOtherSalaryIn").text = $"${mySalaryOut:N0}";
    }

    long GetSalaryMargin(TeamData team)
    {
        var players = DatabaseManager.Instance.GetPlayersByTeam(team.id);
        var payroll = players.Sum(p => p.salary);
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        var cap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        return cap - payroll;
    }

    // ═══════════════════════════════════════════
    //  TRADE VALIDATION
    // ═══════════════════════════════════════════

    void CheckTradeStatus()
    {
        _tradeStatus.Clear();

        var mySelected = _myPlayers.Where(p => _selectedMyPlayers.Contains(p.id)).ToList();
        var otherSelected = _otherPlayers.Where(p => _selectedOtherPlayers.Contains(p.id)).ToList();
        var myPicksSel = _myPicks != null ? _myPicks.Where(p => _selectedMyPicks.Contains(p.id)).ToList() : new List<DraftPickData>();
        var otherPicksSel = _otherPicks != null ? _otherPicks.Where(p => _selectedOtherPicks.Contains(p.id)).ToList() : new List<DraftPickData>();

        if (mySelected.Count == 0 && otherSelected.Count == 0
            && myPicksSel.Count == 0 && otherPicksSel.Count == 0) return;

        var myPayroll = _myPlayers.Sum(p => p.salary);
        var otherPayroll = _otherPlayers.Sum(p => p.salary);

        var errors = TradeHelper.ValidateTrade(
            mySelected, otherSelected,
            _myTotalRosterCount, _otherTotalRosterCount,
            _selectedTeam.name, otherPayroll,
            _myTeam.name, myPayroll,
            _myTeam.first_apron_hard_capped == 1,
            _selectedTeam.first_apron_hard_capped == 1);

        if (errors.Count > 0)
        {
            ShowTradeInvalid(errors);
            return;
        }

        var result = TradeHelper.EvaluateTrade(
            mySelected, otherSelected,
            _selectedTeam.name, _otherTotalRosterCount, otherPayroll,
            myPicksSel, otherPicksSel);

        ShowTradeResult(result);
    }

    void ShowTradeInvalid(List<string> errors)
    {
        var box = new VisualElement();
        box.AddToClassList("market-trade-status-box");
        box.AddToClassList("invalid");

        var icon = new Label("❌");
        icon.AddToClassList("market-trade-status-icon");
        box.Add(icon);

        var content = new VisualElement();
        var title = new Label("OFERTA NO VÁLIDA");
        title.AddToClassList("market-trade-status-title");
        content.Add(title);

        var errorList = new VisualElement();
        errorList.AddToClassList("market-trade-status-errors");
        foreach (var e in errors)
        {
            var err = new Label($"• {e}");
            errorList.Add(err);
        }
        content.Add(errorList);
        box.Add(content);

        _tradeStatus.Add(box);
    }

    void ShowTradeResult(TradeResult result)
    {
        var box = new VisualElement();
        box.AddToClassList("market-trade-status-box");

        var otherSelected = _otherPlayers.Where(p => _selectedOtherPlayers.Contains(p.id)).ToList();
        _signAndTradeCandidates = otherSelected.Where(p => p.contract_years <= 1).ToList();
        _signAndTradeActive = false;

        if (result.WouldAccept)
        {
            box.AddToClassList("valid");

            var icon = new Label("✅");
            icon.AddToClassList("market-trade-status-icon");
            box.Add(icon);

            var content = new VisualElement();
            var title = new Label("EL EQUIPO ACEPTARÁ ESTA OFERTA");
            title.AddToClassList("market-trade-status-title");
            content.Add(title);

            var score = new Label($"Probabilidad: {result.AcceptScore}%");
            score.AddToClassList("market-trade-status-score");
            content.Add(score);

            // Sign & Trade option for expiring incoming players
            if (_signAndTradeCandidates.Count > 0)
            {
                var satNames = string.Join(", ", _signAndTradeCandidates.Select(p => $"{p.first_name} {p.last_name}"));
                var satRow = new VisualElement();
                satRow.style.flexDirection = FlexDirection.Row;
                satRow.style.alignItems = Align.Center;
                satRow.style.marginTop = 8;
                satRow.style.marginBottom = 4;

                var satToggle = new Label("☐ Sign & Trade");
                satToggle.AddToClassList("market-btn-sat-toggle");
                satRow.Add(satToggle);

                var satInfo = new Label($"({satNames}) — extiende contrato al recibir, activa hard cap");
                satInfo.style.fontSize = 11;
                satInfo.style.color = new StyleColor(Color.gray);
                satInfo.style.marginLeft = 6;
                satRow.Add(satInfo);
                content.Add(satRow);

                satToggle.RegisterCallback<ClickEvent>(_ =>
                {
                    _signAndTradeActive = !_signAndTradeActive;
                    satToggle.text = _signAndTradeActive ? "☑ Sign & Trade" : "☐ Sign & Trade";
                });
            }

            var confirmBtn = new Button();
            confirmBtn.AddToClassList("market-btn-confirm-trade");
            confirmBtn.text = "CONFIRMAR TRASPASO";
            confirmBtn.clicked += () => { PlayClick(); OnConfirmTrade(); };
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(confirmBtn);
            content.Add(confirmBtn);

            box.Add(content);
        }
        else
        {
            box.AddToClassList("rejected");

            var icon = new Label("❌");
            icon.AddToClassList("market-trade-status-icon");
            box.Add(icon);

            var content = new VisualElement();
            var title = new Label("EL EQUIPO NO VA A ACEPTAR ESTA OFERTA");
            title.AddToClassList("market-trade-status-title");
            content.Add(title);

            var score = new Label($"Probabilidad: {result.AcceptScore}%");
            score.AddToClassList("market-trade-status-score");
            content.Add(score);

            box.Add(content);
        }

        _tradeStatus.Add(box);
    }

    void OnConfirmTrade()
    {
        var mySelected = _myPlayers.Where(p => _selectedMyPlayers.Contains(p.id)).ToList();
        var otherSelected = _otherPlayers.Where(p => _selectedOtherPlayers.Contains(p.id)).ToList();

        // Sign & Trade: extender contratos antes del traspaso
        bool satApplied = false;
        if (_signAndTradeActive && _signAndTradeCandidates.Count > 0)
        {
            foreach (var p in _signAndTradeCandidates)
            {
                int newYears = CalcSATYears(p.age);
                long newSalary = CalcSATSalary(p.salary);
                p.contract_years = newYears;
                p.salary = newSalary;
                DatabaseManager.Instance.UpdatePlayer(p);
                Debug.Log($"[S&T] {p.first_name} {p.last_name} extendido: ${newSalary:N0} x {newYears} años");
            }

            if (_myTeam.first_apron_hard_capped == 0)
            {
                _myTeam.first_apron_hard_capped = 1;
                DatabaseManager.Instance.UpdateTeam(_myTeam);
                Debug.Log($"[S&T] {_myTeam.name} sujeto al hard cap del primer apron.");
            }
            satApplied = true;
            _signAndTradeActive = false;
        }

        // Execute trade
        foreach (var p in mySelected)
        {
            p.team_id = _selectedTeam.id;
            DatabaseManager.Instance.UpdatePlayer(p);
        }
        foreach (var p in otherSelected)
        {
            p.team_id = _myTeam.id;
            DatabaseManager.Instance.UpdatePlayer(p);
        }

        // Transfer picks
        var myPicksSel = _myPicks != null ? _myPicks.Where(p => _selectedMyPicks.Contains(p.id)).ToList() : new List<DraftPickData>();
        var otherPicksSel = _otherPicks != null ? _otherPicks.Where(p => _selectedOtherPicks.Contains(p.id)).ToList() : new List<DraftPickData>();
        if (myPicksSel.Count > 0)
        {
            var ids = myPicksSel.Select(p => p.id).ToList();
            DatabaseManager.Instance.TransferDraftPicks(ids, _myTeam.id, _selectedTeam.id);
            foreach (var pk in myPicksSel)
                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = _season?.id ?? 0,
                    game_day = _season?.current_game_day ?? 0,
                    game_date = _season?.current_date ?? System.DateTime.Now.ToString("yyyy-MM-dd"),
                    team_id_from = _myTeam.id,
                    team_id_to = _selectedTeam.id,
                    player_id = 0,
                    trade_type = "pick_trade"
                });
        }
        if (otherPicksSel.Count > 0)
        {
            var ids = otherPicksSel.Select(p => p.id).ToList();
            DatabaseManager.Instance.TransferDraftPicks(ids, _selectedTeam.id, _myTeam.id);
            foreach (var pk in otherPicksSel)
                DatabaseManager.Instance.InsertTrade(new TradeData
                {
                    season_id = _season?.id ?? 0,
                    game_day = _season?.current_game_day ?? 0,
                    game_date = _season?.current_date ?? System.DateTime.Now.ToString("yyyy-MM-dd"),
                    team_id_from = _selectedTeam.id,
                    team_id_to = _myTeam.id,
                    player_id = 0,
                    trade_type = "pick_trade"
                });
        }

        // Record trade history
        string tradeType = satApplied ? "sign_and_trade" : "trade";
        foreach (var p in mySelected)
        {
            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = _season?.id ?? 0,
                game_day = _season?.current_game_day ?? 0,
                game_date = _season?.current_date ?? System.DateTime.Now.ToString("yyyy-MM-dd"),
                team_id_from = _myTeam.id,
                team_id_to = _selectedTeam.id,
                player_id = p.id,
                trade_type = tradeType
            });
        }
        foreach (var p in otherSelected)
        {
            DatabaseManager.Instance.InsertTrade(new TradeData
            {
                season_id = _season?.id ?? 0,
                game_day = _season?.current_game_day ?? 0,
                game_date = _season?.current_date ?? System.DateTime.Now.ToString("yyyy-MM-dd"),
                team_id_from = _selectedTeam.id,
                team_id_to = _myTeam.id,
                player_id = p.id,
                trade_type = tradeType
            });
        }

        var myNames = string.Join(", ", mySelected.Select(p => $"{p.first_name} {p.last_name}"));
        var otherNames = string.Join(", ", otherSelected.Select(p => $"{p.first_name} {p.last_name}"));
        var myPicksSelForMsg = _myPicks != null ? _myPicks.Where(p => _selectedMyPicks.Contains(p.id)).ToList() : new List<DraftPickData>();
        var otherPicksSelForMsg = _otherPicks != null ? _otherPicks.Where(p => _selectedOtherPicks.Contains(p.id)).ToList() : new List<DraftPickData>();
        var myPicksText = myPicksSelForMsg.Count > 0
            ? " y " + string.Join(", ", myPicksSelForMsg.Select(p => $"R{p.round} Pick #{p.pick_number}"))
            : "";
        var otherPicksText = otherPicksSelForMsg.Count > 0
            ? " y " + string.Join(", ", otherPicksSelForMsg.Select(p => $"R{p.round} Pick #{p.pick_number}"))
            : "";
        string satNote = satApplied ? " (Sign & Trade: contratos extendidos, hard cap activado)" : "";

        // Refresh local pick caches after transfer
        var activeSeason = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        if (activeSeason != null)
        {
            var allPicksForSeason = DatabaseManager.Instance.GetDraftPicksForSeason(activeSeason.id);
            _myPicks = allPicksForSeason.Where(p => p.current_team_id == _myTeam.id).ToList();
            _otherPicks = allPicksForSeason.Where(p => p.current_team_id == _selectedTeam.id).ToList();
        }
        _selectedMyPicks.Clear();
        _selectedOtherPicks.Clear();

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = satApplied ? $"Traspaso + Sign & Trade: {_selectedTeam.name}" : $"Traspaso aceptado: {_selectedTeam.name}",
            body = $"El traspaso ha sido aceptado. Envías: {myNames}. Recibes: {otherNames}.{satNote}",
            game_day = _season?.current_game_day ?? 0,
            game_date = System.DateTime.Now.ToString("yyyy-MM-dd"),
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        });

        _selectedMyPlayers.Clear();
        _selectedOtherPlayers.Clear();

        ShowTradeSuccessModal(myNames, otherNames, satApplied, myPicksText, otherPicksText);
    }

    void ShowTradeSuccessModal(string myNames, string otherNames, bool satApplied = false,
                               string myPicksText = "", string otherPicksText = "")
    {
        if (_tradeSuccessTitle != null)
            _tradeSuccessTitle.text = satApplied ? "¡TRASPASO + SIGN & TRADE REALIZADO!" : "¡TRASPASO REALIZADO!";
        if (_tradeSuccessText1 != null) _tradeSuccessText1.text = $"Has enviado a {myNames}{myPicksText} a {_selectedTeam.name}.";
        if (_tradeSuccessText2 != null) _tradeSuccessText2.text = $"Has recibido a {otherNames}{otherPicksText}." + (satApplied ? " (contratos extendidos vía Sign & Trade)" : "");

        if (_tradeSuccessIcon != null)
        {
            var iconSprite = Resources.Load<Sprite>("Icons/contrato");
            if (iconSprite != null)
            {
                _tradeSuccessIcon.style.backgroundImage = new StyleBackground(iconSprite);
            }
            else
            {
                _tradeSuccessIcon.style.backgroundImage = new StyleBackground((Sprite)null);
            }
        }

        if (_tradeSuccessOverlay != null) _tradeSuccessOverlay.style.display = DisplayStyle.Flex;
        if (_tradeSuccessBox != null) _tradeSuccessBox.style.display = DisplayStyle.Flex;

        StartCoroutine(AutoCloseTradeSuccess());
    }

    System.Collections.IEnumerator AutoCloseTradeSuccess()
    {
        yield return new WaitForSeconds(5f);
        if (_tradeSuccessOverlay != null) _tradeSuccessOverlay.style.display = DisplayStyle.None;
        if (_tradeSuccessBox != null) _tradeSuccessBox.style.display = DisplayStyle.None;
        ScreenManager.Instance.GoTo(GameScreen.Roster);
    }

    // ═══════════════════════════════════════════
    //  FREE AGENTS
    // ═══════════════════════════════════════════

    void BuildFreeAgents()
    {
        _freeAgentsBody.Clear();

        var visible = _freeAgents?.Where(p =>
        {
            int cd = p.renewal_cooldown_day;
            return cd <= 0 || _season == null || _season.current_game_day >= cd;
        }).ToList();

        if (visible == null || visible.Count == 0)
        {
            var empty = new Label("No hay agentes libres disponibles");
            empty.AddToClassList("market-empty-fa");
            _freeAgentsBody.Add(empty);
            return;
        }

        var pendingSet = DatabaseManager.Instance.GetPendingFAPlayerIds(_manager.id);

        foreach (var player in visible)
        {
            var row = new VisualElement();
            row.AddToClassList("market-fa-row");

            var name = new Label($"{player.first_name} {player.last_name}");
            name.AddToClassList("market-fa-col-name");
            row.Add(name);

            var pos = new Label(player.position);
            pos.AddToClassList("market-fa-col-pos");
            row.Add(pos);

            var ovr = new Label(player.overall.ToString());
            ovr.AddToClassList("market-fa-col-ovr");
            row.Add(ovr);

            var salary = new Label($"${player.salary:N0}");
            salary.AddToClassList("market-fa-col-salary");
            row.Add(salary);

            var actionCell = new VisualElement();
            actionCell.AddToClassList("market-fa-col-action");
            var btn = new Button();
            btn.AddToClassList("market-btn-fa-sign");
            int playerId = player.id;
            if (pendingSet.Contains(playerId))
            {
                btn.text = "NEGOCIANDO";
                btn.SetEnabled(false);
            }
            else
            {
                btn.text = "FIRMAR";
                btn.clicked += () => { PlayClick(); OnSignPlayer(playerId); };
            }
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(btn);
            actionCell.Add(btn);
            row.Add(actionCell);

            _freeAgentsBody.Add(row);
        }
    }

    void OnSignPlayer(int playerId)
    {
        var player = _freeAgents.Find(p => p.id == playerId);
        if (player == null) return;

        var myPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        int pendingCount = DatabaseManager.Instance.GetPendingFAOfferCount(_manager.id);
        if (myPlayers.Count + pendingCount >= TradeHelper.MAX_ROSTER)
        {
            if (_faRosterFullText != null)
                _faRosterFullText.text = $"No puedes fichar más jugadores. Tu plantilla ({myPlayers.Count}) + ofertas pendientes ({pendingCount}) alcanzaría el máximo de {TradeHelper.MAX_ROSTER}.";
            if (_faRosterFullIcon != null)
            {
                var xTex = Resources.Load<Texture2D>("Icons/boton-x-64px");
                if (xTex != null)
                    _faRosterFullIcon.style.backgroundImage = new StyleBackground(xTex);
            }
            _faRosterFullModal.style.display = DisplayStyle.Flex;
            return;
        }

        _pendingFAPlayer = player;
        _faOfferSent = false;

        // Calcular límites salariales
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long totalPayroll = myPlayers.Sum(p => p.salary);
        _faMaxSalary = RosterController.CalculateMaxOfferSalary(player, leagueSettings, totalPayroll, false);
        UpdateFAMaxInfo();

        // Mostrar formulario, ocultar pending
        if (_faFormRowSalary != null) _faFormRowSalary.style.display = DisplayStyle.Flex;
        if (_faFormRowYears != null) _faFormRowYears.style.display = DisplayStyle.Flex;
        if (_faPendingText != null) _faPendingText.style.display = DisplayStyle.None;
        if (_faWarningText != null) _faWarningText.style.display = DisplayStyle.None;
        if (_btnConfirmFA != null)
        {
            _btnConfirmFA.SetEnabled(true);
            _btnConfirmFA.text = "ENVIAR OFERTA";
        }

        // Valores por defecto
        {
            long autoSalary = player.salary + 2_000_000;
            _faSalary = autoSalary < _faMaxSalary ? autoSalary : _faMaxSalary;
            _faSalary = (long)(Mathf.Round(_faSalary / 100_000f) * 100_000);
        }
        {
            if (player.age > 35) _faYears = 1;
            else if (player.age > 32) _faYears = 2;
            else if (player.age > 28) _faYears = 3;
            else if (player.age > 25) _faYears = 4;
            else _faYears = 5;
        }
        RefreshFASpinners();

        _faPlayerName.text = "OFERTA DE CONTRATO";
        if (_faText1 != null)
            _faText1.text = $"Oferta de contrato para {player.first_name} {player.last_name}";
        if (_faIcon != null)
        {
            var contractTex = Resources.Load<Texture2D>("Icons/contrato");
            if (contractTex != null)
                _faIcon.style.backgroundImage = new StyleBackground(contractTex);
        }

        UpdateFAWarning();
        UpdateFAAcceptScore();
        _faConfirmModal.style.display = DisplayStyle.Flex;
    }

    void SendFAOffer()
    {
        if (_pendingFAPlayer == null || _faOfferSent) return;

        if (_faSalary < 1_000_000) _faSalary = 1_000_000;
        else if (_faSalary > _faMaxSalary) _faSalary = _faMaxSalary;
        if (_faYears < 1) _faYears = 1;
        else if (_faYears > 5) _faYears = 5;
        RefreshFASpinners();

        int salary = (int)_faSalary;
        int years = _faYears;

        _faOfferSent = true;

        var offer = new OfferData
        {
            manager_id = _manager.id,
            player_id = _pendingFAPlayer.id,
            offer_salary = salary,
            offer_years = years,
            day_sent = _season?.current_game_day ?? 0,
            offer_type = 1,
            status = "pending",
            processed = 0
        };
        DatabaseManager.Instance.AddOffer(offer);
        BuildFreeAgents();

        // Ocultar formulario, mostrar pending
        if (_faFormRowSalary != null) _faFormRowSalary.style.display = DisplayStyle.None;
        if (_faFormRowYears != null) _faFormRowYears.style.display = DisplayStyle.None;
        if (_faWarningText != null) _faWarningText.style.display = DisplayStyle.None;
        if (_faPendingText != null) _faPendingText.style.display = DisplayStyle.Flex;

        if (_btnConfirmFA != null)
        {
            _btnConfirmFA.SetEnabled(false);
            _btnConfirmFA.text = "ENVIADA";
        }
    }

    void UpdateFAWarning()
    {
        if (_faWarningText == null || _pendingFAPlayer == null || _myPlayers == null || _myTeam == null) return;

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        if (leagueSettings == null) return;

        long totalPayroll = _myPlayers.Sum(p => p.salary);
        long newTotal = totalPayroll + _faSalary;

        if (totalPayroll > TradeHelper.SECOND_APRON)
        {
            _faWarningText.text = $"2º APRON: Solo puedes ofrecer el mínimo (${TradeHelper.MIN_SALARY:N0}) a FAs externos. No puedes usar excepciones.";
            _faWarningText.style.display = DisplayStyle.Flex;
        }
        else if (totalPayroll > TradeHelper.FIRST_APRON)
        {
            _faWarningText.text = $"1er APRON: Máximo T-MLE (${TradeHelper.T_MLE:N0}). No puedes usar NT-MLE.";
            _faWarningText.style.display = DisplayStyle.Flex;
        }
        else if (totalPayroll > leagueSettings.salary_cap)
        {
            string hardCapNote = _myTeam.first_apron_hard_capped == 1
                ? " · HARD CAP ACTIVO"
                : "";
            _faWarningText.text = $"Sobre el cap: máximo NT-MLE (${TradeHelper.NT_MLE:N0}){hardCapNote}. Usar NT-MLE activará el hard cap del 1er apron.";
            _faWarningText.style.display = DisplayStyle.Flex;
        }
        else if (newTotal > leagueSettings.salary_cap)
        {
            _faWarningText.text = $"AVISO: Esta oferta te sitúa por encima del salary cap (${newTotal / 1_000_000}M > ${leagueSettings.salary_cap / 1_000_000}M).";
            _faWarningText.style.display = DisplayStyle.Flex;
        }
        else if (newTotal > leagueSettings.luxury_tax)
        {
            long overage = newTotal - leagueSettings.luxury_tax;
            _faWarningText.text = $"AVISO: Salario total (${newTotal / 1_000_000}M) supera el límite de lujo en ${overage / 1_000_000}M";
            _faWarningText.style.display = DisplayStyle.Flex;
        }
        else
        {
            _faWarningText.style.display = DisplayStyle.None;
        }
    }

    void UpdateFAAcceptScore()
    {
        if (_faText2 == null || _pendingFAPlayer == null) return;

        if (_faSalary <= 0 || _faYears < 1)
        {
            _faText2.text = "";
            return;
        }

        int teamChem = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        float score = RosterController.CalculateAcceptScore(_pendingFAPlayer, (int)_faSalary, _faYears, 0, teamChem);
        _faText2.text = $"Probabilidad de aceptación: {score:F0}%";
    }

    void UpdateFAMaxInfo()
    {
        if (_faMaxInfo == null || _pendingFAPlayer == null || _myTeam == null) return;

        var settings = DatabaseManager.Instance.GetLeagueSettings();
        if (settings == null) return;

        var roster = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long totalPayroll = roster.Sum(p => p.salary);
        var breakdown = RosterController.GetMaxOfferBreakdown(_pendingFAPlayer, settings, totalPayroll, false);

        string info = $"Máximo: ${breakdown.finalMax:N0} — {breakdown.bindingReason}";
        if (!string.IsNullOrEmpty(breakdown.exceptionName))
            info += $" · [{breakdown.exceptionName}]";

        long capSpace = settings.salary_cap - totalPayroll;
        if (capSpace >= 0)
            info += $" · Margen: ${capSpace:N0}";
        else
            info += $" · Excedido: ${-capSpace:N0}";

        _faMaxInfo.text = info;
        _faMaxInfo.style.display = DisplayStyle.Flex;
    }

    // ── FA Spinner helpers ──────────────────────────────────

    void StepFASalary(int dir)
    {
        long val = _faSalary + 500_000 * dir;
        _faSalary = val < 1_000_000 ? 1_000_000 : (val > _faMaxSalary ? _faMaxSalary : val);
        RefreshFASpinners();
        UpdateFAWarning();
        UpdateFAAcceptScore();
    }

    void StepFAYears(int dir)
    {
        int val = _faYears + dir;
        _faYears = val < 1 ? 1 : (val > 5 ? 5 : val);
        RefreshFASpinners();
        UpdateFAAcceptScore();
    }

    void RefreshFASpinners()
    {
        if (_faSalaryValue != null)
            _faSalaryValue.text = $"${_faSalary:N0}";
        if (_faYearsValue != null)
            _faYearsValue.text = $"{_faYears} año{(_faYears > 1 ? "s" : "")}";

        ToggleFASpinDisabled(_faSalaryDec, _faSalary <= 1_000_000);
        ToggleFASpinDisabled(_faSalaryInc, _faSalary >= _faMaxSalary);
        ToggleFASpinDisabled(_faYearsDec, _faYears <= 1);
        ToggleFASpinDisabled(_faYearsInc, _faYears >= 5);
    }

    void ToggleFASpinDisabled(Label el, bool disabled)
    {
        if (el == null) return;
        if (disabled)
            el.AddToClassList("btn-spin--disabled");
        else
            el.RemoveFromClassList("btn-spin--disabled");
    }

    void SetupFALongPress(VisualElement el, System.Action onStep)
    {
        if (el == null) return;

        IVisualElementScheduledItem scheduled = null;

        el.RegisterCallback<PointerDownEvent>(_ =>
        {
            PlayClick();
            scheduled?.Pause();
            onStep();
            scheduled = el.schedule.Execute(() => onStep()).Every(80).StartingIn(350);
        });

        el.RegisterCallback<PointerUpEvent>(_ =>
        {
            if (scheduled != null)
            {
                scheduled.Pause();
                scheduled = null;
            }
        });

        el.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (scheduled != null)
            {
                scheduled.Pause();
                scheduled = null;
            }
        });
    }

    void ShowFAResultModal(bool success, string playerName)
    {
        if (_tradeSuccessTitle != null)
        {
            _tradeSuccessTitle.text = success ? "¡FICHAJE REALIZADO!" : "¡FICHAJE RECHAZADO!";
            _tradeSuccessTitle.RemoveFromClassList("renew-modal-title--positive");
            _tradeSuccessTitle.RemoveFromClassList("renew-modal-title--negative");
            _tradeSuccessTitle.AddToClassList(success ? "renew-modal-title--positive" : "renew-modal-title--negative");
        }
        if (_tradeSuccessText1 != null)
            _tradeSuccessText1.text = success
                ? $"{playerName} ha aceptado tu oferta."
                : $"{playerName} ha rechazado tu oferta.";
        if (_tradeSuccessText2 != null)
            _tradeSuccessText2.text = success
                ? "Revisa tu plantilla para ver los detalles."
                : "No podrás volver a intentarlo hasta dentro de 14 días.";

        if (_tradeSuccessBox != null)
        {
            _tradeSuccessBox.RemoveFromClassList("renew-modal-box--positive");
            _tradeSuccessBox.RemoveFromClassList("renew-modal-box--negative");
            _tradeSuccessBox.AddToClassList(success ? "renew-modal-box--positive" : "renew-modal-box--negative");
        }

        if (_tradeSuccessIcon != null)
        {
            var iconName = success ? "contrato" : "rechazar";
            var tex = Resources.Load<Texture2D>($"Icons/{iconName}");
            _tradeSuccessIcon.style.backgroundImage = tex != null ? new StyleBackground(tex) : null;
        }

        if (_tradeSuccessOverlay != null) _tradeSuccessOverlay.style.display = DisplayStyle.Flex;
        if (_tradeSuccessBox != null) _tradeSuccessBox.style.display = DisplayStyle.Flex;

        StartCoroutine(AutoCloseFAResult());
    }

    System.Collections.IEnumerator AutoCloseFAResult()
    {
        yield return new WaitForSeconds(4f);
        if (_tradeSuccessOverlay != null) _tradeSuccessOverlay.style.display = DisplayStyle.None;
        if (_tradeSuccessBox != null) _tradeSuccessBox.style.display = DisplayStyle.None;
        if (_tradeSuccessTitle != null)
        {
            _tradeSuccessTitle.RemoveFromClassList("renew-modal-title--positive");
            _tradeSuccessTitle.RemoveFromClassList("renew-modal-title--negative");
        }
        if (_tradeSuccessBox != null)
        {
            _tradeSuccessBox.RemoveFromClassList("renew-modal-box--positive");
            _tradeSuccessBox.RemoveFromClassList("renew-modal-box--negative");
        }
        if (_tradeSuccessIcon != null)
            _tradeSuccessIcon.style.backgroundImage = null;
    }

    void ShowFreeAgents()
    {
        _freeAgentsPanel.style.display = DisplayStyle.Flex;
        _tradePanels.style.display = DisplayStyle.None;
        _btnNegotiate.style.display = DisplayStyle.None;
        BuildFreeAgents();
    }

    // ── Sign & Trade helpers ──────────────────────────────

    int CalcSATYears(int age)
    {
        if (age <= 25) return 5;
        if (age <= 28) return 4;
        if (age <= 32) return 3;
        if (age < 40) return 2;
        return 1;
    }

    long CalcSATSalary(long currentSalary)
    {
        long newSalary = (long)(currentSalary * 1.05);
        newSalary = (long)(Mathf.Round(newSalary / 100_000f) * 100_000);
        if (newSalary < currentSalary) newSalary = currentSalary;
        return newSalary;
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }

}
