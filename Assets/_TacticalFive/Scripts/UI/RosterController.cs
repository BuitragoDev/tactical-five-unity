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

    // Modal despido
    private VisualElement _dismissOverlay;
    private VisualElement _dismissBox;
    private Label _dismissText1;
    private Label _dismissText2;
    private Button _btnDismissCancel;
    private Button _btnDismissConfirm;

    // Renovar contrato
    private Button _btnRenew;

    // Modal renovación (oferta)
    private VisualElement _renewOverlay;
    private VisualElement _renewBox;
    private VisualElement _renewIcon;
    private Label _renewTitle;
    private Label _renewText1;
    private Label _renewText2;
    private Label _renewText3;
    private Button _btnRenewCancel;
    private Button _btnRenewConfirm;

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

    // Datos
    private int _renewOfferYears;
    private long _renewOfferSalary;
    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<PlayerData> _players;
    private PlayerData _selectedPlayer;

    private Dictionary<string, Sprite> _logoSprites = new();

    private static readonly Dictionary<string, string> PosLabels = new()
    {
        { "PG", "BASE" },
        { "SG", "ESCOLTA" },
        { "SF", "ALERO" },
        { "PF", "ALA-PIVOT" },
        { "C",  "PIVOT" }
    };

    private static readonly List<string> PosOrder =
        new() { "PG", "SG", "SF", "PF", "C" };

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

        CacheReferences();
        LoadSidebarIcons();
        SetupScrollViews();
        LoadData();
        RegisterCallbacks();
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

        // Modal
        _dismissOverlay = _root.Q<VisualElement>("DismissOverlay");
        _dismissBox = _root.Q<VisualElement>("DismissBox");
        _dismissText1 = _root.Q<Label>("DismissText1");
        _dismissText2 = _root.Q<Label>("DismissText2");
        _btnDismissCancel = _root.Q<Button>("BtnDismissCancel");
        _btnDismissConfirm = _root.Q<Button>("BtnDismissConfirm");

        // Renovar contrato
        _btnRenew = _root.Q<Button>("BtnRenew");

        // Modal renovación
        _renewOverlay = _root.Q<VisualElement>("RenewOverlay");
        _renewBox = _root.Q<VisualElement>("RenewBox");
        _renewIcon = _root.Q<VisualElement>("RenewIcon");
        _renewTitle = _root.Q<Label>("RenewTitle");
        _renewText1 = _root.Q<Label>("RenewText1");
        _renewText2 = _root.Q<Label>("RenewText2");
        _renewText3 = _root.Q<Label>("RenewText3");
        _btnRenewCancel = _root.Q<Button>("BtnRenewCancel");
        _btnRenewConfirm = _root.Q<Button>("BtnRenewConfirm");

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
        // Sidebar navegación
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var submenu = _root.Q<VisualElement>("RosterSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
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
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
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
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
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
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
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

        // Renovar contrato
        _btnRenew?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnRenewClicked(); });
        _btnRenewCancel?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseRenewModal(); });
        _btnRenewConfirm?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ConfirmRenew(); });
        _btnRenewBlockOk?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseRenewBlockModal(); });
        _btnRenewCooldownOk?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseRenewCooldownModal(); });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnAction);
            CursorManager.Instance.RegisterHandCursor(_btnDismiss);
            CursorManager.Instance.RegisterHandCursor(_btnDismissCancel);
            CursorManager.Instance.RegisterHandCursor(_btnDismissConfirm);
        }
    }

    void Refresh()
    {
        RefreshHeader();
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

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerBudget.text = $"${_myTeam.budget / 1_000_000}M";
        _headerBudget.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));

        var teamEmployees = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        long totalPayroll = _players.Sum(p => p.salary) + teamEmployees.Sum(e => e.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? 155_000_000;
        long margin = salaryCap - _players.Sum(p => p.salary);

        string marginText = margin >= 0
            ? $"+${margin / 1_000_000}M"
            : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        _headerMargin.text = marginText;
        var chemLabel = _root.Q<Label>("HeaderChemistry");
        if (chemLabel != null)
        {
            chemLabel.text = chemistry.ToString();
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

        _btnAction.text = "DASHBOARD";
    }

    // ── SUMMARY ──────────────────────────────────────────

    void RefreshSummary()
    {
        _summaryPlayers.text = _players.Count.ToString();
        int avgOverall = _players.Count > 0
            ? (int)_players.Average(p => p.overall) : 0;
        _summaryOverall.text = avgOverall.ToString();
        _summaryBudget.text = $"${_myTeam.budget / 1_000_000}M";
    }

    // ── ROSTER LIST ──────────────────────────────────────

    void BuildRosterList()
    {
        _rosterBody.Clear();

        foreach (var pos in PosOrder)
        {
            var posPlayers = _players
                .Where(p => p.position == pos)
                .OrderByDescending(p => p.overall)
                .ToList();

            if (posPlayers.Count == 0) continue;

            // Cabecera posición
            var posHeader = new VisualElement();
            posHeader.AddToClassList("pos-header");

            var badge = new Label();
            badge.AddToClassList("pos-badge");
            badge.text = pos;

            var label = new Label();
            label.AddToClassList("pos-label");
            label.text = PosLabels.TryGetValue(pos, out var lbl) ? lbl : pos;

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
        ovrLbl.text = player.overall.ToString();

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
        _detailPlayerMeta.text = $"{p.position} · {p.age} años · {p.nationality} · {p.height_cm / 100f:F2}m · {p.weight_kg}kg{(p.is_rookie == 1 ? " · Rookie" : "")}";
        _detailOvr.text = p.overall.ToString();

        // Foto
        if (_detailPhoto != null)
        {
            Texture2D tex = null;
            if (!string.IsNullOrEmpty(p.photo))
                tex = Resources.Load<Texture2D>($"PlayerPhotos/{p.photo}");
            else
                tex = Resources.Load<Texture2D>($"PlayerPhotos/{p.id}");
            if (tex == null)
                tex = Resources.Load<Texture2D>("PlayerPhotos/default");
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

        // Calcular oferta automática (lógica Django end_season_renew)
        _renewOfferYears = CalculateAutoYears(_selectedPlayer.age);
        _renewOfferSalary = CalculateAutoSalary(_selectedPlayer.salary);

        string playerName = $"{_selectedPlayer.first_name} {_selectedPlayer.last_name}";
        string salaryText = $"${_renewOfferSalary / 1_000_000}M/año";
        string yearsText = $"{_renewOfferYears} año{(_renewOfferYears > 1 ? "s" : "")}";

        _renewText1.text = $"Oferta de renovación para {playerName}";
        _renewText2.text = $"Salario: {salaryText}  |  Duración: {yearsText}";

        // Calcular probabilidad de aceptación
        float acceptScore = CalculateAcceptScore(_renewOfferSalary, _renewOfferYears);
        _renewText3.text = $"Probabilidad de aceptación: {acceptScore:F0}%";

        SetRenewModalColor(_renewBox, _renewTitle, true);
        _renewOverlay.style.display = DisplayStyle.Flex;
        _renewBox.style.display = DisplayStyle.Flex;
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

    float CalculateAcceptScore(long offerSalary, int offerYears)
    {
        float acceptScore = 50f;
        long currentSalary = _selectedPlayer.salary;
        float salaryIncrease = currentSalary > 0 ? (float)(offerSalary - currentSalary) / currentSalary : 0f;

        if (salaryIncrease >= 0.30f) acceptScore += 25f;
        else if (salaryIncrease >= 0.10f) acceptScore += 15f;
        else if (salaryIncrease >= 0f) acceptScore += 5f;
        else acceptScore -= Mathf.Abs(salaryIncrease) * 50f;

        if (_selectedPlayer.age >= 32) acceptScore += 10f;
        else if (_selectedPlayer.age >= 28) acceptScore += 5f;
        else if (_selectedPlayer.age <= 23) acceptScore -= 5f;

        if (_selectedPlayer.overall >= 85) acceptScore -= 5f;
        else if (_selectedPlayer.overall < 75) acceptScore += 5f;

        int gamesPlayed = 0;
        if (_season != null)
            gamesPlayed = DatabaseManager.Instance.GetPlayerGamesPlayedInSeason(_selectedPlayer.id, _season.id);

        if (gamesPlayed >= 50) acceptScore += 10f;
        else if (gamesPlayed >= 30) acceptScore += 5f;
        else if (gamesPlayed < 10) acceptScore -= 10f;

        if (offerYears >= 4) acceptScore += 10f;
        else if (offerYears >= 3) acceptScore += 5f;
        else if (offerYears < 2) acceptScore -= 5f;

        int teamChem = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        float chemistryMod = (teamChem - 50) * 0.3f;
        acceptScore += chemistryMod;

        return Mathf.Max(10f, Mathf.Min(95f, acceptScore));
    }

    void ConfirmRenew()
    {
        CloseRenewModal();

        if (_selectedPlayer == null) return;

        int currentDay = _season?.current_game_day ?? 0;
        float acceptScore = CalculateAcceptScore(_renewOfferSalary, _renewOfferYears);
        bool accepted = Random.Range(1, 101) <= acceptScore;

        string playerName = $"{_selectedPlayer.first_name} {_selectedPlayer.last_name}";
        string salaryText = $"${_renewOfferSalary / 1_000_000}M/año";
        string yearsText = $"{_renewOfferYears} año{(_renewOfferYears > 1 ? "s" : "")}";

        string cooldownText;
        string resultTitle;
        string resultLine1;
        string resultLine2;

        if (accepted)
        {
            _selectedPlayer.salary = _renewOfferSalary;
            _selectedPlayer.contract_years = _renewOfferYears;
            _selectedPlayer.renewal_cooldown_day = currentDay + 365;
            DatabaseManager.Instance.UpdatePlayer(_selectedPlayer);

            cooldownText = "1 año";
            resultTitle = "CONTRATO RENOVADO";
            resultLine1 = $"{playerName} ha aceptado la oferta.";
            resultLine2 = $"Nuevo contrato: {salaryText} · {yearsText}. No podrás renovarle de nuevo hasta dentro de {cooldownText}.";

            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 0,
                sender_id = 0,
                title = $"Contrato renovado: {playerName}",
                body = $"{playerName} ha aceptado la oferta de renovación. Nuevo contrato: {salaryText} durante {yearsText}. No podrás renovarle de nuevo hasta dentro de {cooldownText}.",
                game_day = currentDay,
                game_date = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = 0
            });
        }
        else
        {
            _selectedPlayer.renewal_cooldown_day = currentDay + 15;
            DatabaseManager.Instance.UpdatePlayer(_selectedPlayer);

            cooldownText = "15 días";
            resultTitle = "OFERTA RECHAZADA";
            resultLine1 = $"{playerName} ha rechazado la oferta.";
            resultLine2 = $"Oferta: {salaryText} · {yearsText}. Podrás intentarlo de nuevo dentro de {cooldownText}.";

            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 0,
                sender_id = 0,
                title = $"Oferta rechazada: {playerName}",
                body = $"{playerName} ha rechazado la oferta de {salaryText} durante {yearsText}. Podrás intentarlo de nuevo dentro de {cooldownText}.",
                game_day = currentDay,
                game_date = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = 0
            });
        }

        // Refresh contract display
        _detailContract.text = $"${_selectedPlayer.salary / 1_000_000}M/año · {_selectedPlayer.contract_years} año{(_selectedPlayer.contract_years != 1 ? "s" : "")}";

        // Refresh roster list
        BuildRosterList();

        // Show result modal with auto-close
        ShowRenewResultModal(resultTitle, resultLine1, resultLine2, accepted);
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

    void CloseRenewModal()
    {
        _renewOverlay.style.display = DisplayStyle.None;
        _renewBox.style.display = DisplayStyle.None;
        ClearRenewModalColor(_renewBox, _renewTitle);
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

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}