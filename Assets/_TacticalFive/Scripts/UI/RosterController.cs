using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class RosterController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerSeason;
    private Label _headerDate;
    private Button _btnAction;

    // Summary
    private Label _summaryPlayers;
    private Label _summaryOverall;
    private Label _summaryBudget;

    // Roster list
    private VisualElement _rosterBody;

    // Detail
    private VisualElement _detailEmpty;
    private ScrollView _detailScroll;
    private VisualElement _detailContent;
    private VisualElement _detailPhoto;
    private Label _detailPlayerName;
    private Label _detailPlayerPos;
    private Label _detailPlayerMeta;
    private Label _detailOvr;
    private Label _detailHealth;
    private VisualElement _detailAttrs;
    private VisualElement _detailSeasonStats;
    private Label _statPts;
    private Label _statReb;
    private Label _statAst;
    private Label _statStl;
    private Label _statBlk;
    private Label _detailContract;
    private Label _detailPotential;
    private Button _btnDismiss;
    private Button _btnBuyout;

    // Modal despido
    private VisualElement _dismissOverlay;
    private VisualElement _dismissBox;
    private Label _dismissText1;
    private Label _dismissText2;
    private Button _btnDismissCancel;
    private Button _btnDismissConfirm;

    // Modal buyout
    private VisualElement _buyoutOverlay;
    private VisualElement _buyoutBox;
    private Label _buyoutText1;
    private Label _buyoutText2;
    private Label _buyoutInfo;
    private Button _btnBuyoutStretch;
    private Button _btnBuyoutCancel;

    // Renovar contrato
    private Button _btnRenew;

    // Modal renovación (oferta)
    private VisualElement _renewOverlay;
    private VisualElement _renewBox;
    private VisualElement _renewIcon;
    private Label _renewTitle;
    private Label _renewText1;
    private Label _renewText2;
    private Button _btnRenewCancel;
    private Button _btnRenewConfirm;
    private Label _renewSalaryValue;
    private Label _renewSalaryDec;
    private Label _renewSalaryInc;
    private Label _renewYearsValue;
    private Label _renewYearsDec;
    private Label _renewYearsInc;
    private Label _renewPendingText;
    private VisualElement _renewFormRowSalary;
    private VisualElement _renewFormRowYears;
    private Label _renewWarningText;
    private Label _renewMaxInfo;
    private long _renewMaxSalary;
    private long _renewSalary;
    private int _renewYears;
    private bool _offerSent;

    // Modal renovación bloqueada (≥3 años)
    private VisualElement _renewBlockOverlay;
    private VisualElement _renewBlockBox;
    private VisualElement _renewBlockIcon;
    private Label _renewBlockTitle;
    private Label _renewBlockText;
    private Button _btnRenewBlockOk;

    // Modal cooldown renovación
    private VisualElement _renewCooldownOverlay;
    private VisualElement _renewCooldownBox;
    private VisualElement _renewCooldownIcon;
    private Label _renewCooldownTitle;
    private Label _renewCooldownText;
    private Button _btnRenewCooldownOk;

    // Modal resultado renovación
    private VisualElement _renewResultOverlay;
    private VisualElement _renewResultBox;
    private VisualElement _renewResultIcon;
    private Label _renewResultTitle;
    private Label _renewResultText1;
    private Label _renewResultText2;

    // Config modal
    private VisualElement _configModalOverlay;
    private VisualElement _configModalBox;
    private Button _btnConfigCerrar;
    private CustomSlider _configSliderMaster;
    private CustomSlider _configSliderMusic;
    private CustomSlider _configSliderSFX;
    private Label _configLabelMaster;
    private Label _configLabelMusic;
    private Label _configLabelSFX;
    private Button _configBtnQualityLow;
    private Button _configBtnQualityMedium;
    private Button _configBtnQualityHigh;
    private Button _configBtnQualityUltra;

    // Config confirm modals
    private VisualElement _configMainMenuConfirmOverlay;
    private Button _configBtnMainMenu;
    private Button _configBtnMainMenuYes;
    private Button _configBtnMainMenuNo;
    private VisualElement _configExitConfirmOverlay;
    private Button _configBtnExit;
    private Button _configBtnExitYes;
    private Button _configBtnExitNo;

    // Datos
    private int _renewOfferYears;
    private long _renewOfferSalary;
    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<PlayerData> _players;
    private PlayerData _selectedPlayer;

    private Dictionary<string, Sprite> _logoSprites = new();

    static string GetPositionDisplay(string pos, string secondary)
    {
        var main = PositionCodes.GetName(pos);
        if (string.IsNullOrEmpty(secondary)) return main;
        var sec = PositionCodes.GetName(secondary);
        return $"{main} / {sec}";
    }

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        // Forzar root a ocupar toda la pantalla
        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        CursorManager.Instance?.SetDefaultCursor();
        CacheReferences();
        LoadSidebarIcons();
        SetupScrollViews();
        LoadData();
        RegisterCallbacks();
        InitConfigModal();
        Refresh();
    }

    void CacheReferences()
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
        _btnAction = _root.Q<Button>("BtnAction");

        // Summary
        _summaryPlayers = _root.Q<Label>("SummaryPlayers");
        _summaryOverall = _root.Q<Label>("SummaryOverall");
        _summaryBudget = _root.Q<Label>("SummaryBudget");

        // Roster
        _rosterBody = _root.Q<VisualElement>("RosterBody");

        // Detail
        _detailEmpty = _root.Q<VisualElement>("DetailEmpty");
        _detailScroll = _root.Q<ScrollView>("DetailScroll");
        _detailContent = _root.Q<VisualElement>("DetailContent");
        _detailPhoto = _root.Q<VisualElement>("DetailPhoto");
        _detailPlayerName = _root.Q<Label>("DetailPlayerName");
        _detailPlayerPos = _root.Q<Label>("DetailPlayerPos");
        _detailPlayerMeta = _root.Q<Label>("DetailPlayerMeta");
        _detailOvr = _root.Q<Label>("DetailOvr");
        _detailHealth = _root.Q<Label>("DetailHealth");
        _detailAttrs = _root.Q<VisualElement>("DetailAttrs");
        _detailSeasonStats = _root.Q<VisualElement>("DetailSeasonStats");
        _statPts = _root.Q<Label>("StatPts");
        _statReb = _root.Q<Label>("StatReb");
        _statAst = _root.Q<Label>("StatAst");
        _statStl = _root.Q<Label>("StatStl");
        _statBlk = _root.Q<Label>("StatBlk");
        _detailContract = _root.Q<Label>("DetailContract");
        _detailPotential = _root.Q<Label>("DetailPotential");
        _btnDismiss = _root.Q<Button>("BtnDismiss");
        _btnBuyout = _root.Q<Button>("BtnBuyout");

        // Modal despido
        _dismissOverlay = _root.Q<VisualElement>("DismissOverlay");
        _dismissBox = _root.Q<VisualElement>("DismissBox");
        _dismissText1 = _root.Q<Label>("DismissText1");
        _dismissText2 = _root.Q<Label>("DismissText2");
        _btnDismissCancel = _root.Q<Button>("BtnDismissCancel");
        _btnDismissConfirm = _root.Q<Button>("BtnDismissConfirm");

        // Modal buyout
        _buyoutOverlay = _root.Q<VisualElement>("BuyoutOverlay");
        _buyoutBox = _root.Q<VisualElement>("BuyoutBox");
        _buyoutText1 = _root.Q<Label>("BuyoutText1");
        _buyoutText2 = _root.Q<Label>("BuyoutText2");
        _buyoutInfo = _root.Q<Label>("BuyoutInfo");
        _btnBuyoutStretch = _root.Q<Button>("BtnBuyoutStretch");
        _btnBuyoutCancel = _root.Q<Button>("BtnBuyoutCancel");

        // Renovar contrato
        _btnRenew = _root.Q<Button>("BtnRenew");

        // Modal renovación
        _renewOverlay = _root.Q<VisualElement>("RenewOverlay");
        _renewBox = _root.Q<VisualElement>("RenewBox");
        _renewIcon = _root.Q<VisualElement>("RenewIcon");
        _renewTitle = _root.Q<Label>("RenewTitle");
        _renewText1 = _root.Q<Label>("RenewText1");
        _renewText2 = _root.Q<Label>("RenewText2");
        _btnRenewCancel = _root.Q<Button>("BtnRenewCancel");
        _btnRenewConfirm = _root.Q<Button>("BtnRenewConfirm");
        _renewSalaryValue = _root.Q<Label>("RenewSalaryValue");
        _renewSalaryDec = _root.Q<Label>("RenewSalaryDec");
        _renewSalaryInc = _root.Q<Label>("RenewSalaryInc");
        _renewYearsValue = _root.Q<Label>("RenewYearsValue");
        _renewYearsDec = _root.Q<Label>("RenewYearsDec");
        _renewYearsInc = _root.Q<Label>("RenewYearsInc");
        _renewPendingText = _root.Q<Label>("RenewPendingText");
        _renewFormRowSalary = _root.Q<VisualElement>("RenewFormRowSalary");
        _renewFormRowYears = _root.Q<VisualElement>("RenewFormRowYears");
        _renewWarningText = _root.Q<Label>("RenewWarningText");
        _renewMaxInfo = _root.Q<Label>("RenewMaxInfo");

        // Modal renovación bloqueada
        _renewBlockOverlay = _root.Q<VisualElement>("RenewBlockOverlay");
        _renewBlockBox = _root.Q<VisualElement>("RenewBlockBox");
        _renewBlockIcon = _root.Q<VisualElement>("RenewBlockIcon");
        _renewBlockTitle = _root.Q<Label>("RenewBlockTitle");
        _renewBlockText = _root.Q<Label>("RenewBlockText");
        _btnRenewBlockOk = _root.Q<Button>("BtnRenewBlockOk");

        // Modal cooldown renovación
        _renewCooldownOverlay = _root.Q<VisualElement>("RenewCooldownOverlay");
        _renewCooldownBox = _root.Q<VisualElement>("RenewCooldownBox");
        _renewCooldownIcon = _root.Q<VisualElement>("RenewCooldownIcon");
        _renewCooldownTitle = _root.Q<Label>("RenewCooldownTitle");
        _renewCooldownText = _root.Q<Label>("RenewCooldownText");
        _btnRenewCooldownOk = _root.Q<Button>("BtnRenewCooldownOk");

        // Modal resultado renovación
        _renewResultOverlay = _root.Q<VisualElement>("RenewResultOverlay");
        _renewResultBox = _root.Q<VisualElement>("RenewResultBox");
        _renewResultIcon = _root.Q<VisualElement>("RenewResultIcon");
        _renewResultTitle = _root.Q<Label>("RenewResultTitle");
        _renewResultText1 = _root.Q<Label>("RenewResultText1");
        _renewResultText2 = _root.Q<Label>("RenewResultText2");
    }

    void SetupScrollViews()
    {
        var rosterScroll = _root.Q<ScrollView>("RosterScroll");
        if (rosterScroll != null)
            rosterScroll.contentContainer.style.flexDirection = FlexDirection.Column;

        if (_detailScroll != null)
            _detailScroll.contentContainer.style.flexDirection = FlexDirection.Column;
    }

    void LoadSidebarIcons()
    {
        var iconMap = new System.Collections.Generic.Dictionary<string, string>
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

        // Icono contrato en modal de renovación
        if (_renewIcon != null)
        {
            var contractTex = Resources.Load<Texture2D>("Icons/contrato");
            if (contractTex != null)
                _renewIcon.style.backgroundImage = new StyleBackground(contractTex);
        }
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
    }

    void RegisterCallbacks()
    {
        // Sidebar unificado
        SidebarController.Attach(_root, GameScreen.Roster);
        HeaderController.Attach(_root);
        // Sidebar navegación
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        var allSubmenus = new[] {
            _root.Q<VisualElement>("RosterSubmenu"),
            _root.Q<VisualElement>("PalmaresSubmenu"),
            _root.Q<VisualElement>("MarketSubmenu"),
            _root.Q<VisualElement>("FinanceSubmenu")
        };

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
            { PlayClick(); OpenConfigModal(); });

        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

        // Despido
        _btnDismiss?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenDismissModal(); });
        _btnDismissCancel?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseDismissModal(); });
        _btnDismissConfirm?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ConfirmDismiss(); });
        _dismissOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _dismissOverlay)
            { PlayClick(); CloseDismissModal(); }
        });

        // Buyout
        _btnBuyout?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenBuyoutModal(); });
        _btnBuyoutStretch?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ConfirmBuyout(); });
        _btnBuyoutCancel?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseBuyoutModal(); });
        _buyoutOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _buyoutOverlay)
            { PlayClick(); CloseBuyoutModal(); }
        });

        // Renovar contrato
        _btnRenew?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnRenewClicked(); });
        _btnRenewCancel?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseRenewModal(); });
        _btnRenewConfirm?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SendOffer(); });
        _btnRenewBlockOk?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseRenewBlockModal(); });
        _btnRenewCooldownOk?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseRenewCooldownModal(); });

        // Spinner buttons (long-press para incrementar/decrementar)
        SetupRenewLongPress(_renewSalaryDec, () => StepRenewSalary(-1));
        SetupRenewLongPress(_renewSalaryInc, () => StepRenewSalary(1));
        SetupRenewLongPress(_renewYearsDec, () => StepRenewYears(-1));
        SetupRenewLongPress(_renewYearsInc, () => StepRenewYears(1));

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnAction);
            CursorManager.Instance.RegisterHandCursor(_btnDismiss);
            CursorManager.Instance.RegisterHandCursor(_btnBuyout);
            CursorManager.Instance.RegisterHandCursor(_btnDismissCancel);
            CursorManager.Instance.RegisterHandCursor(_btnDismissConfirm);
            CursorManager.Instance.RegisterHandCursor(_btnBuyoutStretch);
            CursorManager.Instance.RegisterHandCursor(_btnBuyoutCancel);
            CursorManager.Instance.RegisterHandCursor(_btnRenew);
            CursorManager.Instance.RegisterHandCursor(_btnRenewCancel);
            CursorManager.Instance.RegisterHandCursor(_btnRenewConfirm);
            RegisterRenewSpinnerCursor(_renewSalaryDec);
            RegisterRenewSpinnerCursor(_renewSalaryInc);
            RegisterRenewSpinnerCursor(_renewYearsDec);
            RegisterRenewSpinnerCursor(_renewYearsInc);
            CursorManager.Instance.RegisterHandCursor(_btnRenewBlockOk);
            CursorManager.Instance.RegisterHandCursor(_btnRenewCooldownOk);
            CursorManager.Instance.RegisterHandCursor(_root.Q<VisualElement>("ConfigIcon"));

            var navNames = new[] {
                "NavDashboard", "NavRoster", "NavCalendar", "NavStandings",
                "NavPalmares", "NavResults", "NavPlayoffs", "NavStats",
                "NavMarket", "NavFinances", "NavArena", "NavMessages",
                "SubmenuJugadores", "SubmenuQuinteto", "SubmenuEntrenamiento",
                "SubmenuEmpleados", "SubmenuLesionados",
                "SubmenuPalmares", "SubmenuRecords",
                "SubmenuOfertas", "SubmenuCartera", "SubmenuHistorial",
                "SubmenuDecisiones", "SubmenuPrestamos", "SubmenuSponsors", "SubmenuTV"
            };
            foreach (var name in navNames)
            {
                var el = _root.Q<VisualElement>(name);
                if (el != null)
                    CursorManager.Instance.RegisterHandCursor(el);
            }
        }
    }

    void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Roster] RefreshHeader error: {ex.Message}"); }
        RefreshSummary();
        BuildRosterList();
        _root.Q<Button>("SubmenuJugadores")?.AddToClassList("nav-submenu-item--active");

        _detailEmpty.style.display = DisplayStyle.Flex;
        _detailScroll.style.display = DisplayStyle.None;
        _dismissOverlay.style.display = DisplayStyle.None;
        _dismissBox.style.display = DisplayStyle.None;
        CloseRenewModal();
        CloseRenewBlockModal();
        CloseRenewCooldownModal();
        CloseRenewResultModal();
    }

    // ── HEADER ───────────────────────────────────────────

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;
        if (_headerTeamName == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerBudget.text = $"${_myTeam.budget / 1_000_000}M";
        _headerBudget.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        long totalPayroll = _players.Sum(p => p.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - _players.Sum(p => p.salary);

        string marginText = margin >= 0
            ? $"+${margin / 1_000_000}M"
            : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
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
        if (margin < 0) _headerMargin.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "MENÚ PRINCIPAL";
    }

    // ── SUMMARY ──────────────────────────────────────────

    void RefreshSummary()
    {
        _summaryPlayers.text = _players.Count.ToString();
        int avgOverall = _players.Count > 0
            ? (int)_players.Average(p => p.GetCalculatedAverage()) : 0;
        _summaryOverall.text = avgOverall.ToString();
        long totalPayroll = _players.Sum(p => p.salary);
        _summaryBudget.text = $"${totalPayroll / 1_000_000}M";
    }

    // ── ROSTER LIST ──────────────────────────────────────

    void BuildRosterList()
    {
        _rosterBody.Clear();

        foreach (var pos in PositionCodes.Order)
        {
            var posPlayers = _players
                .Where(p => p.position == pos)
                .OrderByDescending(p => p.GetCalculatedAverage())
                .ToList();

            if (posPlayers.Count == 0) continue;

            // Cabecera posición
            var posHeader = new VisualElement();
            posHeader.AddToClassList("pos-header");

            var badge = new Label();
            badge.AddToClassList("pos-badge");
            badge.text = PositionCodes.GetShort(pos);

            var label = new Label();
            label.AddToClassList("pos-label");
            label.text = PositionCodes.GetName(pos);

            posHeader.Add(badge);
            posHeader.Add(label);

            var section = new VisualElement();
            section.AddToClassList("pos-section");
            section.Add(posHeader);

            // Filas de jugadores
            for (int i = 0; i < posPlayers.Count; i++)
            {
                var player = posPlayers[i];
                var row = CreatePlayerRow(i + 1, player);
                section.Add(row);
            }

            _rosterBody.Add(section);
        }
    }

    VisualElement CreatePlayerRow(int num, PlayerData player)
    {
        var row = new VisualElement();
        row.AddToClassList("player-row");
        if (player.injury_days > 0)
            row.AddToClassList("player-row--injured");

        var nameLbl = new Label();
        nameLbl.AddToClassList("player-name");
        if (player.injury_days > 0)
            nameLbl.AddToClassList("player-name--injured");
        nameLbl.text = player.is_rookie == 1
            ? $"{player.first_name} {player.last_name} (R)"
            : $"{player.first_name} {player.last_name}";

        var ovrLbl = new Label();
        ovrLbl.AddToClassList("player-ovr");
        ovrLbl.text = player.GetCalculatedAverage().ToString();

        var metaLbl = new Label();
        metaLbl.AddToClassList("player-meta");
        metaLbl.text = $"{player.age} años · {player.height_cm / 100f:F2}m";

        var contractLbl = new Label();
        contractLbl.AddToClassList("player-contract");
        contractLbl.text = $"{player.contract_years} año{(player.contract_years != 1 ? "s" : "")}";
        if (player.contract_years <= 1)
            contractLbl.AddToClassList("player-contract--expiring");

        // Morale dot
        var moraleDot = new VisualElement();
        moraleDot.AddToClassList("morale-dot");
        Color dotColor;
        if (player.morale >= 70)
            dotColor = new Color32(39, 174, 96, 255);
        else if (player.morale >= 40)
            dotColor = new Color32(212, 160, 23, 255);
        else
            dotColor = new Color32(192, 57, 43, 255);
        moraleDot.style.backgroundColor = new StyleColor(dotColor);
        row.Add(moraleDot);

        row.Add(nameLbl);
        row.Add(ovrLbl);
        row.Add(metaLbl);
        row.Add(contractLbl);

        // Columna lesión (imagen o hueco vacío para mantener alineación)
        var injIcon = new VisualElement();
        injIcon.AddToClassList("player-injury-icon");
        if (player.injury_days > 0)
        {
            var tex = Resources.Load<Texture2D>($"Icons/lesion");
            if (tex != null)
                injIcon.style.backgroundImage = new StyleBackground(tex);
            injIcon.tooltip = $"{player.injury_type} — {player.injury_days} días de baja";
        }
        row.Add(injIcon);

        // Click
        row.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnPlayerSelected(player, row); });
        if (CursorManager.Instance != null)
        {
            row.RegisterCallback<MouseEnterEvent>(_ =>
                CursorManager.Instance.SetHandCursor());
            row.RegisterCallback<MouseLeaveEvent>(_ =>
                CursorManager.Instance.SetDefaultCursor());
        }

        return row;
    }

    // ── DETALLE JUGADOR ──────────────────────────────────

    void OnPlayerSelected(PlayerData player, VisualElement row)
    {
        _selectedPlayer = player;

        // Quitar selección anterior
        _rosterBody.Query<VisualElement>(className: "player-row--selected")
                   .ForEach(e => e.RemoveFromClassList("player-row--selected"));
        row.AddToClassList("player-row--selected");

        ShowPlayerDetail(player);
    }

    void ShowPlayerDetail(PlayerData p)
    {
        _detailEmpty.style.display = DisplayStyle.None;
        _detailScroll.style.display = DisplayStyle.Flex;

        // Cabecera
        _detailPlayerName.text = $"{p.first_name} {p.last_name}".ToUpper();
        _detailPlayerPos.text = GetPositionDisplay(p.position, p.secondary_position);
        _detailPlayerMeta.text = $"{p.age} años · {CountryCodes.GetName(p.nationality)} · {p.height_cm / 100f:F2}m · {p.weight_kg}kg{(p.is_rookie == 1 ? " · Rookie" : "")}";
        _detailOvr.text = p.GetCalculatedAverage().ToString();

        // Foto
        if (_detailPhoto != null)
        {
            Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
            if (tex != null)
                _detailPhoto.style.backgroundImage = new StyleBackground(tex);
            else
                _detailPhoto.style.backgroundImage = StyleKeyword.None;
        }

        // Salud
        if (p.injury_days > 0)
        {
            _detailHealth.text = $"🏥   {p.injury_type} — {p.injury_days} días de baja";
            _detailHealth.RemoveFromClassList("detail-health-ok");
            _detailHealth.AddToClassList("detail-health-injured");
        }
        else
        {
            _detailHealth.text = "✓  DISPONIBLE";
            _detailHealth.RemoveFromClassList("detail-health-injured");
            _detailHealth.AddToClassList("detail-health-ok");
        }

        // Atributos
        BuildAttrBars(p);

        // Stats temporada
        var s = DatabaseManager.Instance.GetPlayerSeasonStats(p.id, _manager.id);
        _statPts.text = s.avgPts.ToString("F1");
        _statReb.text = s.avgReb.ToString("F1");
        _statAst.text = s.avgAst.ToString("F1");
        _statStl.text = s.avgStl.ToString("F1");
        _statBlk.text = s.avgBlk.ToString("F1");

        // Contrato y potencial
        _detailContract.text = $"${p.salary / 1_000_000}M/año · {p.contract_years} año{(p.contract_years != 1 ? "s" : "")}";
        _detailPotential.text = p.potential.ToString();

        // Botón renovar siempre visible
        if (_btnRenew != null)
            _btnRenew.style.display = DisplayStyle.Flex;
    }

    void BuildAttrBars(PlayerData p)
    {
        _detailAttrs.Clear();

        var attrs = new[]
        {
            ("TIRO",      p.shooting),
            ("TRIPLE",    p.three_point),
            ("PASE",      p.passing),
            ("BOTE",      p.dribbling),
            ("DEFENSA",   p.defense),
            ("REBOTE",    p.rebounding),
            ("VELOCIDAD", p.speed),
            ("ATLETISMO", p.athleticism),
            ("IQ",        p.iq),
            ("ROBOS",     p.steals),
            ("TAPONES",   p.blocks),
            ("MORAL",     p.morale),
        };

        foreach (var (label, val) in attrs)
        {
            var row = new VisualElement();
            row.AddToClassList("attr-row");

            var lbl = new Label();
            lbl.AddToClassList("attr-label");
            lbl.text = label;

            var barBg = new VisualElement();
            barBg.AddToClassList("attr-bar-bg");

            var barFill = new VisualElement();
            barFill.AddToClassList("attr-bar-fill");
            if (val < 50) barFill.AddToClassList("attr-bar-fill--low");
            else if (val < 70) barFill.AddToClassList("attr-bar-fill--mid");

            barFill.style.width = new StyleLength(new Length(val, LengthUnit.Percent));
            barBg.Add(barFill);

            var valLbl = new Label();
            valLbl.AddToClassList("attr-val");
            valLbl.text = val.ToString();

            row.Add(lbl);
            row.Add(barBg);
            row.Add(valLbl);
            _detailAttrs.Add(row);
        }
    }

    // ── RENOVAR CONTRATO ──────────────────────────────────

    void OnRenewClicked()
    {
        if (_selectedPlayer == null) return;

        // Verificar cooldown de renovación
        int currentDay = _season?.current_game_day ?? 0;
        if (_selectedPlayer.renewal_cooldown_day > currentDay)
        {
            int daysLeft = _selectedPlayer.renewal_cooldown_day - currentDay;
            OpenRenewCooldownModal(daysLeft);
            return;
        }

        // Si tiene 3+ años restantes: modal de bloqueo
        if (_selectedPlayer.contract_years >= 3)
        {
            OpenRenewBlockModal();
            return;
        }

        // Resetear estado
        _offerSent = false;

        // Calcular límites Bird Rights + cap space
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long totalPayroll = _players != null ? _players.Sum(p => p.salary) : 0;
        _renewMaxSalary = CalculateMaxOfferSalary(_selectedPlayer, leagueSettings, totalPayroll);

        // Mostrar formulario, ocultar pending
        if (_renewFormRowSalary != null) _renewFormRowSalary.style.display = DisplayStyle.Flex;
        if (_renewFormRowYears != null) _renewFormRowYears.style.display = DisplayStyle.Flex;
        if (_renewPendingText != null) _renewPendingText.style.display = DisplayStyle.None;
        if (_renewText1 != null) _renewText1.style.display = DisplayStyle.Flex;
        if (_renewText2 != null) _renewText2.style.display = DisplayStyle.Flex;
        if (_btnRenewConfirm != null)
        {
            _btnRenewConfirm.SetEnabled(true);
            _btnRenewConfirm.text = "ENVIAR OFERTA";
        }

        // Poner valores por defecto
        long autoSalary = CalculateAutoSalary(_selectedPlayer.salary);
        _renewSalary = autoSalary < _renewMaxSalary ? autoSalary : _renewMaxSalary;
        _renewSalary = (long)(Mathf.Round(_renewSalary / 100_000f) * 100_000);
        _renewYears = CalculateAutoYears(_selectedPlayer.age);
        RefreshRenewSpinners();

        ClearRenewModalColor(_renewBox, _renewTitle);
        string playerName = $"{_selectedPlayer.first_name} {_selectedPlayer.last_name}";
        if (_renewText1 != null)
            _renewText1.text = $"Oferta de renovación para {playerName}";

        // Mostrar advertencia de lujo si aplica
        UpdateCapWarning();

        UpdateRenewMaxInfo();
        UpdateAcceptScoreDisplay();

        if (_renewOverlay != null) _renewOverlay.style.display = DisplayStyle.Flex;
        if (_renewBox != null) _renewBox.style.display = DisplayStyle.Flex;
    }

    int CalculateAutoYears(int age)
    {
        if (age <= 25) return 5;
        if (age <= 28) return 4;
        if (age <= 32) return 3;
        if (age < 40) return 2;
        return 1;
    }

    long CalculateAutoSalary(long currentSalary)
    {
        long newSalary = (long)(currentSalary * 1.05);
        newSalary = (long)(Mathf.Round(newSalary / 100_000f) * 100_000);
        if (newSalary < currentSalary) newSalary = currentSalary;
        return newSalary;
    }

    public static float CalculateAcceptScore(PlayerData player, long offerSalary, int offerYears, int gamesPlayed, int teamChemistry)
    {
        float score = 50f;
        float salaryIncrease = player.salary > 0 ? (float)(offerSalary - player.salary) / player.salary : 0f;

        if (salaryIncrease >= 0.30f) score += 25f;
        else if (salaryIncrease >= 0.10f) score += 15f;
        else if (salaryIncrease >= 0f) score += 5f;
        else score -= Mathf.Abs(salaryIncrease) * 50f;

        if (player.age >= 32) score += 10f;
        else if (player.age >= 28) score += 5f;
        else if (player.age <= 23) score -= 5f;

        if (player.overall >= 85) score -= 5f;
        else if (player.overall < 75) score += 5f;

        if (gamesPlayed >= 50) score += 10f;
        else if (gamesPlayed >= 30) score += 5f;
        else if (gamesPlayed < 10) score -= 10f;

        if (offerYears >= 4) score += 10f;
        else if (offerYears >= 3) score += 5f;
        else if (offerYears < 2) score -= 5f;

        float chemistryMod = (teamChemistry - 50) * 0.3f;
        score += chemistryMod;

        return Mathf.Max(10f, Mathf.Min(95f, score));
    }

    float CalculateAcceptScore(long offerSalary, int offerYears)
    {
        int gamesPlayed = 0;
        if (_season != null)
            gamesPlayed = DatabaseManager.Instance.GetPlayerGamesPlayedInSeason(_selectedPlayer.id, _season.id);
        int teamChem = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        return CalculateAcceptScore(_selectedPlayer, offerSalary, offerYears, gamesPlayed, teamChem);
    }

    public struct MaxOfferBreakdown
    {
        public long finalMax;
        public long maxByExp;
        public long birdMax;
        public long capSpaceMax;
        public long exceptionMax;
        public string birdTierName;
        public string bindingReason;
        public string exceptionName;
    }

    public static MaxOfferBreakdown GetMaxOfferBreakdown(PlayerData player, LeagueSettingsData settings, long totalPayroll, bool isFromSameTeam = true)
    {
        var result = new MaxOfferBreakdown();

        if (settings == null)
        {
            result.finalMax = 60_000_000;
            result.birdTierName = "—";
            result.bindingReason = "Sin configuración de liga";
            return result;
        }

        int exp = Mathf.Max(0, player.age - 22);
        if (exp <= 6) result.maxByExp = (long)(settings.salary_cap * 0.25);
        else if (exp <= 9) result.maxByExp = (long)(settings.salary_cap * 0.30);
        else result.maxByExp = (long)(settings.salary_cap * 0.35);

        if (isFromSameTeam)
        {
            if (player.seasons_with_team >= 3)
            {
                result.birdTierName = "COMPLETOS";
                result.birdMax = result.maxByExp;
            }
            else if (player.seasons_with_team == 2)
            {
                result.birdTierName = "EARLY";
                result.birdMax = player.salary * 175 / 100;
                long avgPct = (long)(settings.salary_cap * 105 / 1000);
                if (avgPct > result.birdMax) result.birdMax = avgPct;
            }
            else
            {
                result.birdTierName = "SIN BIRD";
                result.birdMax = player.salary * 120 / 100;
            }
        }
        else
        {
            // FA externo: sin Bird Rights
            result.birdMax = 0;
            result.birdTierName = "SIN BIRD (FA)";
        }

        long capSpace = settings.salary_cap - totalPayroll;
        result.capSpaceMax = player.salary + (capSpace > 0 ? capSpace : 0);

        result.exceptionMax = 0;
        result.exceptionName = "";

        long rawMax;
        if (!isFromSameTeam && totalPayroll > settings.salary_cap)
        {
            // FA externo y equipo sobre el cap: solo excepciones
            if (totalPayroll <= TradeHelper.FIRST_APRON)
            {
                result.exceptionMax = TradeHelper.NT_MLE;
                result.exceptionName = "NT-MLE";
            }
            else if (totalPayroll <= TradeHelper.SECOND_APRON)
            {
                result.exceptionMax = TradeHelper.T_MLE;
                result.exceptionName = "T-MLE";
                result.capSpaceMax = 0;
            }
            else
            {
                result.exceptionMax = TradeHelper.MIN_SALARY;
                result.exceptionName = "Mínimo";
                result.capSpaceMax = 0;
            }
            rawMax = result.exceptionMax;
        }
        else
        {
            rawMax = result.birdMax > result.capSpaceMax ? result.birdMax : result.capSpaceMax;
        }

        result.finalMax = result.maxByExp < rawMax ? result.maxByExp : rawMax;

        // Determine binding reason
        if (result.finalMax == result.maxByExp && result.maxByExp <= rawMax)
            result.bindingReason = $"Máx. por experiencia ({result.maxByExp:N0})";
        else if (!isFromSameTeam && totalPayroll > settings.salary_cap)
            result.bindingReason = $"Excepción {result.exceptionName} (${result.exceptionMax:N0})";
        else if (result.birdMax >= result.capSpaceMax)
            result.bindingReason = $"Bird Rights {result.birdTierName} ({result.birdMax:N0})";
        else
            result.bindingReason = $"Espacio salarial ({result.capSpaceMax:N0})";

        return result;
    }

    public static long CalculateMaxOfferSalary(PlayerData player, LeagueSettingsData settings, long totalPayroll, bool isFromSameTeam = true)
    {
        return GetMaxOfferBreakdown(player, settings, totalPayroll, isFromSameTeam).finalMax;
    }

    string GetBirdRightsTier(PlayerData player)
    {
        if (player.seasons_with_team >= 3) return "COMPLETOS";
        if (player.seasons_with_team == 2) return "EARLY";
        return "SIN BIRD";
    }

    void UpdateCapWarning()
    {
        if (_renewWarningText == null || _selectedPlayer == null || _players == null) return;

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        if (leagueSettings == null) return;

        long totalPayroll = _players.Sum(p => p.salary);
        long newTotal = totalPayroll - _selectedPlayer.salary + _renewSalary;

        if (newTotal > leagueSettings.luxury_tax)
        {
            long overage = newTotal - leagueSettings.luxury_tax;
            _renewWarningText.text = $"AVISO: Salario total (${newTotal / 1_000_000}M) supera el límite de lujo en ${overage / 1_000_000}M";
            _renewWarningText.style.display = DisplayStyle.Flex;
        }
        else
        {
            _renewWarningText.style.display = DisplayStyle.None;
        }
    }

    void UpdateRenewMaxInfo()
    {
        if (_renewMaxInfo == null || _selectedPlayer == null || _players == null) return;

        var settings = DatabaseManager.Instance.GetLeagueSettings();
        if (settings == null) return;

        long totalPayroll = _players.Sum(p => p.salary);
        var breakdown = GetMaxOfferBreakdown(_selectedPlayer, settings, totalPayroll);

        _renewMaxInfo.text = $"Máximo: ${breakdown.finalMax:N0} — {breakdown.bindingReason}";
        _renewMaxInfo.style.display = DisplayStyle.Flex;
    }

    void SendOffer()
    {
        if (_selectedPlayer == null || _offerSent) return;

        // Asegurar que los valores están dentro de límites
        if (_renewSalary < 1000000) _renewSalary = 1000000;
        else if (_renewSalary > _renewMaxSalary) _renewSalary = _renewMaxSalary;
        if (_renewYears < 1) _renewYears = 1;
        else if (_renewYears > 5) _renewYears = 5;

        _renewOfferSalary = _renewSalary;
        _renewOfferYears = _renewYears;
        RefreshRenewSpinners();

        _offerSent = true;

        // Guardar oferta en BD
        int currentDay = _season?.current_game_day ?? 0;
        var offer = new OfferData
        {
            manager_id = _manager.id,
            player_id = _selectedPlayer.id,
            offer_salary = _renewOfferSalary,
            offer_years = _renewOfferYears,
            day_sent = currentDay,
            status = "pending",
            processed = 0
        };
        DatabaseManager.Instance.AddOffer(offer);
        Debug.Log($"[Roster] Oferta guardada: player={_selectedPlayer.id} salary={_renewOfferSalary} years={_renewOfferYears} day_sent={currentDay}");

        // Ocultar formulario, mostrar pending
        if (_renewFormRowSalary != null) _renewFormRowSalary.style.display = DisplayStyle.None;
        if (_renewFormRowYears != null) _renewFormRowYears.style.display = DisplayStyle.None;
        if (_renewText1 != null) _renewText1.style.display = DisplayStyle.None;
        if (_renewText2 != null) _renewText2.style.display = DisplayStyle.None;
        if (_renewPendingText != null) _renewPendingText.style.display = DisplayStyle.Flex;

        // Deshabilitar botón enviar oferta
        if (_btnRenewConfirm != null)
        {
            _btnRenewConfirm.SetEnabled(false);
            _btnRenewConfirm.text = "ENVIADA";
        }
    }

    void ShowRenewResultModal(string title, string line1, string line2, bool positive)
    {
        if (_renewResultTitle != null) _renewResultTitle.text = title;
        if (_renewResultText1 != null) _renewResultText1.text = line1;
        if (_renewResultText2 != null) _renewResultText2.text = line2;
        if (_renewResultOverlay != null) _renewResultOverlay.style.display = DisplayStyle.Flex;
        if (_renewResultBox != null) _renewResultBox.style.display = DisplayStyle.Flex;
        SetRenewModalColor(_renewResultBox, _renewResultTitle, positive);
        SetRenewResultIcon(_renewResultIcon, positive);

        StartCoroutine(AutoCloseRenewResult());
    }

    System.Collections.IEnumerator AutoCloseRenewResult()
    {
        yield return new WaitForSeconds(5f);
        CloseRenewResultModal();
    }

    void CloseRenewResultModal()
    {
        if (_renewResultOverlay != null) _renewResultOverlay.style.display = DisplayStyle.None;
        if (_renewResultBox != null) _renewResultBox.style.display = DisplayStyle.None;
        ClearRenewModalColor(_renewResultBox, _renewResultTitle);
        ClearRenewResultIcon(_renewResultIcon);
    }

    void OpenRenewCooldownModal(int daysLeft)
    {
        if (_selectedPlayer == null || _renewCooldownText == null) return;

        string playerName = $"{_selectedPlayer.first_name} {_selectedPlayer.last_name}";
        _renewCooldownText.text = $"{playerName} no puede ser renovado ahora. Debes esperar {daysLeft} día{(daysLeft != 1 ? "s" : "")} para intentarlo de nuevo.";

        _renewCooldownOverlay.style.display = DisplayStyle.Flex;
        _renewCooldownBox.style.display = DisplayStyle.Flex;
        SetRenewModalColor(_renewCooldownBox, _renewCooldownTitle, false);
        SetRenewResultIcon(_renewCooldownIcon, false);
    }

    void CloseRenewCooldownModal()
    {
        _renewCooldownOverlay.style.display = DisplayStyle.None;
        _renewCooldownBox.style.display = DisplayStyle.None;
        ClearRenewModalColor(_renewCooldownBox, _renewCooldownTitle);
        ClearRenewResultIcon(_renewCooldownIcon);
    }

    // ── Spinner helpers ─────────────────────────────────────

    void StepRenewSalary(int dir)
    {
        long val = _renewSalary + 500_000 * dir;
        _renewSalary = val < 1_000_000 ? 1_000_000 : (val > _renewMaxSalary ? _renewMaxSalary : val);
        RefreshRenewSpinners();
        UpdateCapWarning();
        UpdateAcceptScoreDisplay();
    }

    void StepRenewYears(int dir)
    {
        int val = _renewYears + dir;
        _renewYears = val < 1 ? 1 : (val > 5 ? 5 : val);
        RefreshRenewSpinners();
        UpdateAcceptScoreDisplay();
    }

    void RefreshRenewSpinners()
    {
        if (_renewSalaryValue != null)
            _renewSalaryValue.text = $"${_renewSalary:N0}";
        if (_renewYearsValue != null)
            _renewYearsValue.text = $"{_renewYears} año{(_renewYears > 1 ? "s" : "")}";

        ToggleRenewSpinDisabled(_renewSalaryDec, _renewSalary <= 1_000_000);
        ToggleRenewSpinDisabled(_renewSalaryInc, _renewSalary >= _renewMaxSalary);
        ToggleRenewSpinDisabled(_renewYearsDec, _renewYears <= 1);
        ToggleRenewSpinDisabled(_renewYearsInc, _renewYears >= 5);
    }

    void ToggleRenewSpinDisabled(Label el, bool disabled)
    {
        if (el == null) return;
        if (disabled)
            el.AddToClassList("btn-spin--disabled");
        else
            el.RemoveFromClassList("btn-spin--disabled");
    }

    void SetupRenewLongPress(VisualElement el, System.Action onStep)
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

    void RegisterRenewSpinnerCursor(Label el)
    {
        if (el != null && CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(el);
    }

    void CloseRenewModal()
    {
        if (_renewOverlay != null) _renewOverlay.style.display = DisplayStyle.None;
        if (_renewBox != null) _renewBox.style.display = DisplayStyle.None;
        if (_renewPendingText != null) _renewPendingText.style.display = DisplayStyle.None;
        if (_renewWarningText != null) _renewWarningText.style.display = DisplayStyle.None;
        ClearRenewModalColor(_renewBox, _renewTitle);
        _offerSent = false;
        _renewMaxSalary = 0;
    }

    void UpdateAcceptScoreDisplay()
    {
        if (_selectedPlayer == null || _renewText2 == null) return;
        if (_renewSalary <= 0 || _renewYears < 1)
        {
            _renewText2.text = "";
            return;
        }
        float score = CalculateAcceptScore(_renewSalary, _renewYears);
        _renewText2.text = $"Probabilidad de aceptación: {score:F0}%";
    }

    void OpenRenewBlockModal()
    {
        if (_selectedPlayer == null) return;

        string playerName = $"{_selectedPlayer.first_name} {_selectedPlayer.last_name}";
        _renewBlockText.text = $"{playerName} tiene {_selectedPlayer.contract_years} años de contrato restantes. Solo se pueden renovar contratos con menos de 3 años restantes.";

        _renewBlockOverlay.style.display = DisplayStyle.Flex;
        _renewBlockBox.style.display = DisplayStyle.Flex;
        SetRenewModalColor(_renewBlockBox, _renewBlockTitle, false);
        SetRenewResultIcon(_renewBlockIcon, false);
    }

    void CloseRenewBlockModal()
    {
        _renewBlockOverlay.style.display = DisplayStyle.None;
        _renewBlockBox.style.display = DisplayStyle.None;
        ClearRenewModalColor(_renewBlockBox, _renewBlockTitle);
        ClearRenewResultIcon(_renewBlockIcon);
    }

    void SetRenewModalColor(VisualElement box, Label title, bool positive)
    {
        if (box != null)
        {
            box.RemoveFromClassList("renew-modal-box--positive");
            box.RemoveFromClassList("renew-modal-box--negative");
            box.AddToClassList(positive ? "renew-modal-box--positive" : "renew-modal-box--negative");
        }
        if (title != null)
        {
            title.RemoveFromClassList("renew-modal-title--positive");
            title.RemoveFromClassList("renew-modal-title--negative");
            title.AddToClassList(positive ? "renew-modal-title--positive" : "renew-modal-title--negative");
        }
    }

    void ClearRenewModalColor(VisualElement box, Label title)
    {
        if (box != null)
        {
            box.RemoveFromClassList("renew-modal-box--positive");
            box.RemoveFromClassList("renew-modal-box--negative");
        }
        if (title != null)
        {
            title.RemoveFromClassList("renew-modal-title--positive");
            title.RemoveFromClassList("renew-modal-title--negative");
        }
    }

    void SetRenewResultIcon(VisualElement iconElem, bool positive)
    {
        if (iconElem == null) return;
        string iconName = positive ? "contrato" : "rechazar";
        var tex = Resources.Load<Texture2D>($"Icons/{iconName}");
        if (tex != null)
            iconElem.style.backgroundImage = new StyleBackground(tex);
        else
            iconElem.style.backgroundImage = null;
    }

    void ClearRenewResultIcon(VisualElement iconElem)
    {
        if (iconElem != null)
            iconElem.style.backgroundImage = null;
    }

    // ── MODAL DESPIDO ─────────────────────────────────────

    void OpenDismissModal()
    {
        if (_selectedPlayer == null) return;

        long penalty = (long)(_selectedPlayer.salary * _selectedPlayer.contract_years * 0.5f);

        _dismissText1.text = $"Estás a punto de despedir a {_selectedPlayer.first_name} {_selectedPlayer.last_name}.";
        _dismissText2.text = $"Penalización por despido: ${penalty / 1_000_000}M (50% salario × años restantes).";

        _dismissOverlay.style.display = DisplayStyle.Flex;
        _dismissBox.style.display = DisplayStyle.Flex;
    }

    void CloseDismissModal()
    {
        _dismissOverlay.style.display = DisplayStyle.None;
        _dismissBox.style.display = DisplayStyle.None;
    }

    void ConfirmDismiss()
    {
        if (_selectedPlayer == null) return;

        long penalty = (long)(_selectedPlayer.salary * _selectedPlayer.contract_years * 0.5f);
        int currentDay = _season?.current_game_day ?? 0;
        string playerName = $"{_selectedPlayer.first_name} {_selectedPlayer.last_name}";
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Mover jugador a agentes libres (team_id = 0)
        _selectedPlayer.team_id = 0;
        DatabaseManager.Instance.UpdatePlayer(_selectedPlayer);

        // Descontar penalización del presupuesto
        _myTeam.budget -= penalty;
        DatabaseManager.Instance.UpdateTeamBudget(_myTeam.id, _myTeam.budget);

        // Registrar gasto en finanzas
        DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
        {
            team_id = _myTeam.id,
            season_id = _season?.id ?? 0,
            record_type = FinanceRecord.TYPE_DISMISSAL,
            game_day = currentDay,
            amount = penalty
        });

        long remainingSalary = _selectedPlayer.salary * _selectedPlayer.contract_years;
        long netBalance = remainingSalary - penalty;

        // Crear mensaje de despido
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 0,
            sender_id = 0,
            title = "Jugador despedido",
            body = $"El club ha decidido rescindir el contrato de {playerName} con efecto inmediato.\n\n" +
                   $"La operación supone una penalización económica de {penalty:N0} €, que ha sido cargada a las cuentas del club.\n\n" +
                   $"La salida del jugador libera una plaza en la plantilla y su salario dejará de computar a partir de esta fecha.\n\n" +
                   $"Coste de rescisión: {penalty:N0} €\n" +
                   $"Ahorro salarial restante: {remainingSalary:N0} €\n" +
                   $"Balance neto de la operación: {netBalance:N0} €",
            game_day = currentDay,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0
        });

        Debug.Log($"[Roster] {playerName} despedido. Penalización: ${penalty}.");

        CloseDismissModal();

        // Recargar datos y refrescar
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _selectedPlayer = null;
        Refresh();
    }

    // ── RESCISIÓN (BUYOUT) ────────────────────────────────

    void OpenBuyoutModal()
    {
        if (_selectedPlayer == null) return;

        long remainingSalary = _selectedPlayer.salary * _selectedPlayer.contract_years;
        int stretchYears = _selectedPlayer.contract_years * 2;
        long annualPayment = remainingSalary / stretchYears;
        long lastYear = remainingSalary - annualPayment * (stretchYears - 1);
        string paymentDetail = annualPayment == lastYear
            ? $"${annualPayment:N0}/año x {stretchYears} años"
            : $"${annualPayment:N0}/año x {stretchYears - 1} años + ${lastYear:N0} (último)";

        _buyoutText1.text = $"Rescisión de contrato de {_selectedPlayer.first_name} {_selectedPlayer.last_name}.";
        _buyoutText2.text = $"Salario restante: ${remainingSalary:N0} · {paymentDetail}";

        _buyoutOverlay.style.display = DisplayStyle.Flex;
        _buyoutBox.style.display = DisplayStyle.Flex;
    }

    void CloseBuyoutModal()
    {
        _buyoutOverlay.style.display = DisplayStyle.None;
        _buyoutBox.style.display = DisplayStyle.None;
    }

    void ConfirmBuyout()
    {
        if (_selectedPlayer == null) return;

        long remainingSalary = _selectedPlayer.salary * _selectedPlayer.contract_years;
        int stretchYears = _selectedPlayer.contract_years * 2;
        long annualPayment = remainingSalary / stretchYears;
        int currentDay = _season?.current_game_day ?? 0;
        string playerName = $"{_selectedPlayer.first_name} {_selectedPlayer.last_name}";
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        _selectedPlayer.team_id = 0;
        DatabaseManager.Instance.UpdatePlayer(_selectedPlayer);

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
            sender_type = 0,
            sender_id = 0,
            title = "Rescisión de contrato (buyout)",
            body = $"Se ha rescindido el contrato de {playerName} mediante buyout.\n\n" +
                   $"Salario restante: {remainingSalary:N0}\n" +
                   $"Pago progresivo: ${annualPayment:N0} durante {stretchYears} años\n" +
                   $"Total pagado: {remainingSalary:N0}\n\n" +
                   $"El jugador queda libre y puede firmar por cualquier equipo.",
            game_day = currentDay,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0
        });

        Debug.Log($"[Roster] {playerName} buyout. ${remainingSalary} stretched over {stretchYears} years.");

        CloseBuyoutModal();

        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _selectedPlayer = null;
        Refresh();
    }

    void InitConfigModal()
    {
        _configModalOverlay = _root.Q<VisualElement>("ConfigModalOverlay");
        _configModalBox     = _root.Q<VisualElement>("ConfigModalBox");
        _btnConfigCerrar    = _root.Q<Button>("ConfigBtnCerrar");

        _configSliderMaster = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderMaster"),
            _root.Q<VisualElement>("ConfigFillMaster"),
            _root.Q<VisualElement>("ConfigDraggerMaster"));
        _configSliderMusic  = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderMusic"),
            _root.Q<VisualElement>("ConfigFillMusic"),
            _root.Q<VisualElement>("ConfigDraggerMusic"));
        _configSliderSFX    = new CustomSlider(
            _root.Q<VisualElement>("ConfigSliderSFX"),
            _root.Q<VisualElement>("ConfigFillSFX"),
            _root.Q<VisualElement>("ConfigDraggerSFX"));
        _configLabelMaster  = _root.Q<Label>("ConfigLabelMaster");
        _configLabelMusic   = _root.Q<Label>("ConfigLabelMusic");
        _configLabelSFX     = _root.Q<Label>("ConfigLabelSFX");
        _configBtnQualityLow    = _root.Q<Button>("ConfigBtnQualityLow");
        _configBtnQualityMedium = _root.Q<Button>("ConfigBtnQualityMedium");
        _configBtnQualityHigh   = _root.Q<Button>("ConfigBtnQualityHigh");
        _configBtnQualityUltra  = _root.Q<Button>("ConfigBtnQualityUltra");

        _configBtnMainMenu     = _root.Q<Button>("ConfigBtnMainMenu");
        _configBtnExit         = _root.Q<Button>("ConfigBtnExit");

        _configMainMenuConfirmOverlay = _root.Q<VisualElement>("ConfigMainMenuConfirmOverlay");
        _configBtnMainMenuYes = _root.Q<Button>("ConfigBtnMainMenuYes");
        _configBtnMainMenuNo  = _root.Q<Button>("ConfigBtnMainMenuNo");

        _configExitConfirmOverlay = _root.Q<VisualElement>("ConfigExitConfirmOverlay");
        _configBtnExitYes = _root.Q<Button>("ConfigBtnExitYes");
        _configBtnExitNo  = _root.Q<Button>("ConfigBtnExitNo");

        _configSliderMaster.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMasterVolume(v);
            UpdateConfigLabels();
        };
        _configSliderMusic.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetMusicVolume(v);
            UpdateConfigLabels();
        };
        _configSliderSFX.OnValueChanged = v =>
        {
            AudioManager.Instance?.SetSFXVolume(v);
            UpdateConfigLabels();
        };

        _configBtnQualityLow?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(0); });
        _configBtnQualityMedium?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(1); });
        _configBtnQualityHigh?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(2); });
        _configBtnQualityUltra?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigQuality(3); });

        _btnConfigCerrar?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseConfigModal(); });
        _configModalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configModalOverlay)
                { PlayClick(); CloseConfigModal(); }
        });

        // Juego buttons
        _configBtnMainMenu?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenMainMenuConfirmModal(); });
        _configBtnExit?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenExitConfirmModal(); });

        // Main menu confirm modal
        _configBtnMainMenuYes?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        _configBtnMainMenuNo?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseMainMenuConfirmModal();
        });
        _configMainMenuConfirmOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configMainMenuConfirmOverlay)
                { PlayClick(); CloseMainMenuConfirmModal(); }
        });

        // Exit confirm modal
        _configBtnExitYes?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            QuitGame();
        });
        _configBtnExitNo?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseExitConfirmModal();
        });
        _configExitConfirmOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _configExitConfirmOverlay)
                { PlayClick(); CloseExitConfirmModal(); }
        });
    }

    void OpenConfigModal()
    {
        CursorManager.Instance?.SetDefaultCursor();
        var am = AudioManager.Instance;
        if (am != null)
        {
            _configSliderMaster.SetValueWithoutNotify(am.MasterVolume);
            _configSliderMusic.SetValueWithoutNotify(am.MusicVolume);
            _configSliderSFX.SetValueWithoutNotify(am.SFXVolume);
            UpdateConfigLabels();
        }
        int q = QualitySettings.GetQualityLevel();
        UpdateConfigQualityButtons(Mathf.Clamp(q, 0, 3));

        _configModalOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configModalOverlay.AddToClassList("modal-overlay--visible");
        _configModalBox.AddToClassList("modal-box--visible");
    }

    void CloseConfigModal()
    {
        _configModalOverlay.RemoveFromClassList("modal-overlay--visible");
        _configModalBox.RemoveFromClassList("modal-box--visible");
    }

    void UpdateConfigLabels()
    {
        var am = AudioManager.Instance;
        if (am == null) return;
        if (_configLabelMaster != null)
            _configLabelMaster.text = $"{Mathf.RoundToInt(am.MasterVolume * 100)}%";
        if (_configLabelMusic != null)
            _configLabelMusic.text  = $"{Mathf.RoundToInt(am.MusicVolume  * 100)}%";
        if (_configLabelSFX != null)
            _configLabelSFX.text    = $"{Mathf.RoundToInt(am.SFXVolume    * 100)}%";
    }

    void SelectConfigQuality(int index)
    {
        AudioManager.Instance?.SetQualityLevel(index);
        UpdateConfigQualityButtons(index);
    }

    void UpdateConfigQualityButtons(int activeIndex)
    {
        var buttons = new[] { _configBtnQualityLow, _configBtnQualityMedium, _configBtnQualityHigh, _configBtnQualityUltra };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].EnableInClassList("settings-quality-btn--active", i == activeIndex);
        }
    }

    void OpenMainMenuConfirmModal()
    {
        PlayClick();
        _configMainMenuConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configMainMenuConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseMainMenuConfirmModal()
    {
        _configMainMenuConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.RemoveFromClassList("modal-box--visible");
    }

    void OpenExitConfirmModal()
    {
        PlayClick();
        _configExitConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configExitConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseExitConfirmModal()
    {
        _configExitConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.RemoveFromClassList("modal-box--visible");
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}