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
    private VisualElement _myTableHeader;
    private VisualElement _otherTableHeader;
    private VisualElement _freeAgentsBody;
    private VisualElement _freeAgentsTableHeader;
    private VisualElement _tradeStatus;
    private VisualElement _faRosterFullModal;
    private VisualElement _faConfirmModal;
    private Button _btnCloseFARosterFull;
    private Button _btnCancelFA;
    private Button _btnConfirmFA;
    private Label _faPlayerName;
    private Label _faNewSalary;
    private Label _faYears;
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
    private List<PlayerData> _freeAgents = new();
    private int _myTotalRosterCount;
    private int _otherTotalRosterCount;
    private TeamData _selectedTeam;
    private bool _isFA = false;
    private PlayerData _pendingFAPlayer;
    private Dictionary<int, int> _faCooldowns = new();

    private Dictionary<string, Sprite> _headerLogos = new();
    private Dictionary<string, Sprite> _teamGridLogos = new();
    private Dictionary<string, Sprite> _tradePanelLogos = new();
    private HashSet<int> _selectedMyPlayers = new();
    private HashSet<int> _selectedOtherPlayers = new();

    // NBA salary thresholds
    private const long SALARY_CAP = 155_000_000;
    private const long LUXURY_TAX = 189_000_000;
    private const long FIRST_APRON = 195_900_000;
    private const long SECOND_APRON = 207_800_000;

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
        _myTableHeader = _root.Q<VisualElement>("MyTableHeader");
        _otherTableHeader = _root.Q<VisualElement>("OtherTableHeader");
        _freeAgentsBody = _root.Q<VisualElement>("FreeAgentsBody");
        _freeAgentsTableHeader = _root.Q<VisualElement>("FreeAgentsTableHeader");
        _tradeStatus = _root.Q<VisualElement>("TradeStatus");
        _faRosterFullModal = _root.Q<VisualElement>("FARosterFullModal");
        _faConfirmModal = _root.Q<VisualElement>("FAConfirmModal");
        _btnCloseFARosterFull = _root.Q<Button>("BtnCloseFARosterFull");
        _btnCancelFA = _root.Q<Button>("BtnCancelFA");
        _btnConfirmFA = _root.Q<Button>("BtnConfirmFA");
        _faPlayerName = _root.Q<Label>("FAPlayerName");
        _faNewSalary = _root.Q<Label>("FANewSalary");
        _faYears = _root.Q<Label>("FAYears");
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
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _btnNegotiate?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OnNegotiate(); });
        _btnCloseFARosterFull?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); _faRosterFullModal.style.display = DisplayStyle.None; });
        _btnCancelFA?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); _faConfirmModal.style.display = DisplayStyle.None; });
        _btnConfirmFA?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OnConfirmFA(); });
    }

    void RegisterNavButtons()
    {
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
        _root.Q<Button>("SubmenuEmpleados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); });
        _root.Q<Button>("SubmenuLesionados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Palmares); });
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Playoffs); });
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Stats); });
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Records); });
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Market); });
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Finances); });
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<Button>("NavConfig")?.RegisterCallback<ClickEvent>(_ =>
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
            {"NavRecordsIcon", "records"},
            {"NavMarketIcon", "mercado"},
            {"NavFinancesIcon", "finanzas"},
            {"NavSponsorsIcon", "patrocinador"},
            {"NavTVIcon", "television"},
            {"NavArenaIcon", "pabellon"},
            {"NavMessagesIcon", "mensajes"},
            {"NavConfigIcon", "configuracion"}
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
        RefreshHeader();
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
        long salaryCap = leagueSettings?.salary_cap ?? SALARY_CAP;
        long margin = salaryCap - totalPayroll;
        var marginLbl = _root.Q<Label>("HeaderMargin");
        marginLbl.text = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
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

        // Set team logos and names
        if (_tradePanelLogos.TryGetValue(_myTeam.logo, out var mySprite))
            _root.Q<VisualElement>("MyTeamLogo").style.backgroundImage = new StyleBackground(mySprite);
        _root.Q<Label>("MyTeamName").text = _myTeam.name.ToUpper();

        if (_tradePanelLogos.TryGetValue(_selectedTeam.logo, out var otherSprite))
            _root.Q<VisualElement>("OtherTeamLogo").style.backgroundImage = new StyleBackground(otherSprite);
        _root.Q<Label>("OtherTeamName").text = _selectedTeam.name.ToUpper();

        InitTableHeaders();
        BuildTradeTable();
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
        var cap = leagueSettings?.salary_cap ?? SALARY_CAP;
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

        if (mySelected.Count == 0 && otherSelected.Count == 0) return;

        var errors = ValidateTrade(mySelected, otherSelected);
        if (errors.Count > 0)
        {
            ShowTradeInvalid(errors);
            return;
        }

        var result = EvaluateTrade(mySelected, otherSelected);
        ShowTradeResult(result);
    }

    List<string> ValidateTrade(List<PlayerData> mySelected, List<PlayerData> otherSelected)
    {
        var errors = new List<string>();

        var mySalaryOut = mySelected.Sum(p => p.salary);
        var otherSalaryOut = otherSelected.Sum(p => p.salary);

        var myAfter = _myTotalRosterCount - mySelected.Count + otherSelected.Count;
        var otherAfter = _otherTotalRosterCount - otherSelected.Count + mySelected.Count;

        if (myAfter < 10) errors.Add($"Tu equipo tendría solo {myAfter} jugadores (mínimo 10)");
        if (myAfter > 15) errors.Add($"Tu equipo tendría {myAfter} jugadores (máximo 15)");
        if (otherAfter < 10) errors.Add($"{_selectedTeam.name} tendría solo {otherAfter} jugadores (mínimo 10)");
        if (otherAfter > 15) errors.Add($"{_selectedTeam.name} tendría {otherAfter} jugadores (máximo 15)");

        var otherPayroll = _otherPlayers.Sum(p => p.salary) - otherSalaryOut + mySalaryOut;

        if (otherPayroll > SECOND_APRON)
        {
            if (mySelected.Count > 1)
                errors.Add($"{_selectedTeam.name} está en el segundo apron. No pueden agregar salarios de múltiples jugadores.");
            if (otherSalaryOut < mySalaryOut)
                errors.Add($"{_selectedTeam.name} está en el segundo apron. Solo pueden recibir salario igual o menor al que envían.");
        }
        else if (otherPayroll > FIRST_APRON)
        {
            var maxReceive = otherSalaryOut * 1.10;
            if (mySalaryOut > maxReceive + 250_000)
                errors.Add($"{_selectedTeam.name} está en el primer apron. Solo pueden recibir hasta el 110% del salario enviado.");
        }
        else
        {
            long maxReceive;
            if (otherSalaryOut < 7_500_000)
                maxReceive = otherSalaryOut * 2 + 250_000;
            else if (otherSalaryOut < 29_000_000)
                maxReceive = otherSalaryOut + 7_500_000;
            else
                maxReceive = (long)(otherSalaryOut * 1.25 + 250_000);

            if (mySalaryOut > maxReceive + 250_000)
                errors.Add($"{_selectedTeam.name} no puede recibir más de ${maxReceive:N0}.");
        }

        return errors;
    }

    TradeResult EvaluateTrade(List<PlayerData> mySelected, List<PlayerData> otherSelected)
    {
        var mySalaryOut = mySelected.Sum(p => p.salary);
        var otherSalaryOut = otherSelected.Sum(p => p.salary);

        var otherBestOvr = otherSelected.Count > 0 ? otherSelected.Max(p => p.overall) : 0;
        var myBestOvr = mySelected.Count > 0 ? mySelected.Max(p => p.overall) : 0;
        var otherAvgOvr = otherSelected.Count > 0 ? otherSelected.Average(p => p.overall) : 0;
        var myAvgOvr = mySelected.Count > 0 ? mySelected.Average(p => p.overall) : 0;
        var myTotalOvr = mySelected.Sum(p => p.overall);
        var otherTotalOvr = otherSelected.Sum(p => p.overall);

        var otherCurrentPayroll = _otherPlayers.Sum(p => p.salary);

        int acceptScore = 0;

        // Player quality comparison
        if (otherBestOvr >= 90)
        {
            if (myBestOvr >= 90) acceptScore += 40 + (myBestOvr - otherBestOvr) * 3;
            else if (myBestOvr >= 85) acceptScore += 15 + (myBestOvr - otherBestOvr) * 2;
            else acceptScore -= 50;
        }
        else if (otherBestOvr >= 85)
        {
            if (myBestOvr >= 85) acceptScore += 30 + (myBestOvr - otherBestOvr) * 2;
            else if (myBestOvr >= 80) acceptScore += 10;
            else acceptScore -= 30;
        }
        else if (otherBestOvr >= 80)
        {
            if (myBestOvr >= 80) acceptScore += 20;
            else if (myBestOvr >= 75) acceptScore += 5;
            else acceptScore -= 15;
        }
        else
        {
            if (myAvgOvr >= otherAvgOvr) acceptScore += 10;
            else acceptScore -= 10;
        }

        // Total OVR comparison
        acceptScore += Mathf.Clamp(myTotalOvr - otherTotalOvr, -20, 20);

        // Financial situation
        if (otherCurrentPayroll > SECOND_APRON)
        {
            if (mySalaryOut > otherSalaryOut) acceptScore += 30;
            else acceptScore -= 20;
        }
        else if (otherCurrentPayroll > FIRST_APRON)
        {
            if (mySalaryOut > otherSalaryOut) acceptScore += 20;
            else acceptScore -= 10;
        }
        else if (otherCurrentPayroll > LUXURY_TAX)
        {
            if (mySalaryOut > otherSalaryOut) acceptScore += 15;
            else if (mySalaryOut < otherSalaryOut) acceptScore -= 5;
        }
        else
        {
            if (mySalaryOut > otherSalaryOut) acceptScore += 5;
            else if (mySalaryOut < otherSalaryOut) acceptScore -= 5;
        }

        // Team needs
        var otherAfter = _otherPlayers.Count - otherSelected.Count + mySelected.Count;
        if (otherAfter <= 12) acceptScore += 15;
        else if (otherAfter <= 14) acceptScore += 5;

        // Age factor
        if (mySelected.Count > 0 && otherSelected.Count > 0)
        {
            var myAvgAge = mySelected.Average(p => p.age);
            var otherAvgAge = otherSelected.Average(p => p.age);
            if (myAvgAge < otherAvgAge - 3) acceptScore += 10;
            else if (myAvgAge > otherAvgAge + 3) acceptScore -= 5;
        }

        // Randomness
        acceptScore += Random.Range(-5, 6);

        acceptScore = Mathf.Clamp(acceptScore, 0, 100);

        var threshold = 50;
        if (otherCurrentPayroll > SECOND_APRON) threshold = 40;
        else if (otherCurrentPayroll > FIRST_APRON) threshold = 45;

        return new TradeResult
        {
            WouldAccept = acceptScore >= threshold,
            AcceptScore = acceptScore,
            Threshold = threshold,
            MySelected = mySelected,
            OtherSelected = otherSelected
        };
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

            var confirmBtn = new Button();
            confirmBtn.AddToClassList("market-btn-confirm-trade");
            confirmBtn.text = "CONFIRMAR TRASPASO";
            confirmBtn.clicked += () => { PlayClick(); OnConfirmTrade(); };
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

        // Build names for message and modal
        var myNames = string.Join(", ", mySelected.Select(p => $"{p.first_name} {p.last_name}"));
        var otherNames = string.Join(", ", otherSelected.Select(p => $"{p.first_name} {p.last_name}"));

        // Create message
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"Traspaso aceptado: {_selectedTeam.name}",
            body = $"El traspaso ha sido aceptado. Envías: {myNames}. Recibes: {otherNames}.",
            game_day = _season?.current_game_day ?? 0,
            game_date = System.DateTime.Now.ToString("yyyy-MM-dd"),
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        });

        // Clear selections
        _selectedMyPlayers.Clear();
        _selectedOtherPlayers.Clear();

        // Show success modal and navigate to Roster after 5 seconds
        ShowTradeSuccessModal(myNames, otherNames);
    }

    void ShowTradeSuccessModal(string myNames, string otherNames)
    {
        if (_tradeSuccessTitle != null) _tradeSuccessTitle.text = "¡TRASPASO REALIZADO!";
        if (_tradeSuccessText1 != null) _tradeSuccessText1.text = $"Has enviado a {myNames} a {_selectedTeam.name}.";
        if (_tradeSuccessText2 != null) _tradeSuccessText2.text = $"Has recibido a {otherNames}.";

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

        var expired = _faCooldowns.Where(kv => kv.Value <= _season.current_game_day).Select(kv => kv.Key).ToList();
        foreach (var pid in expired) _faCooldowns.Remove(pid);

        var visible = _freeAgents?.Where(p => !_faCooldowns.ContainsKey(p.id)).ToList();

        if (visible == null || visible.Count == 0)
        {
            var empty = new Label("No hay agentes libres disponibles");
            empty.AddToClassList("market-empty-fa");
            _freeAgentsBody.Add(empty);
            return;
        }

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
            btn.text = "FIRMAR";
            int playerId = player.id;
            btn.clicked += () => { PlayClick(); OnSignPlayer(playerId); };
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
        if (myPlayers.Count >= 15)
        {
            _faRosterFullModal.style.display = DisplayStyle.Flex;
            return;
        }

        _pendingFAPlayer = player;
        _faPlayerName.text = $"{player.first_name} {player.last_name}";

        var newSalary = player.salary + 2_000_000;
        _faNewSalary.text = $"${newSalary:N0}/año";

        int years;
        if (player.age > 35) years = 1;
        else if (player.age > 32) years = 2;
        else if (player.age > 28) years = 3;
        else if (player.age > 25) years = 4;
        else years = 5;
        _faYears.text = $" {years} año{(years > 1 ? "s" : "")}";

        _faConfirmModal.style.display = DisplayStyle.Flex;
    }

    void OnConfirmFA()
    {
        if (_pendingFAPlayer == null) return;

        var player = _pendingFAPlayer;
        _faConfirmModal.style.display = DisplayStyle.None;
        _pendingFAPlayer = null;

        float chance = Mathf.Clamp(_myTeam.reputation * 20 - player.overall * 0.5f + 30, 5, 95);
        bool accepted = Random.Range(0f, 100f) < chance;

        if (accepted)
        {
            var newSalary = player.salary + 2_000_000;

            int years;
            if (player.age > 35) years = 1;
            else if (player.age > 32) years = 2;
            else if (player.age > 28) years = 3;
            else if (player.age > 25) years = 4;
            else years = 5;

            player.team_id = _myTeam.id;
            player.salary = newSalary;
            player.contract_years = years;
            DatabaseManager.Instance.UpdatePlayer(player);

            DatabaseManager.Instance.AddMessage(new MessageData
            {
                manager_id = _manager.id,
                sender_type = 1,
                sender_id = 0,
                title = $"Fichaje: {player.first_name} {player.last_name}",
                body = $"{player.first_name} {player.last_name} ha firmado con tu equipo. Contrato: ${newSalary:N0}/año durante {years} año{(years > 1 ? "s" : "")}.",
                game_day = _season?.current_game_day ?? 0,
                game_date = System.DateTime.Now.ToString("yyyy-MM-dd"),
                created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                is_read = 0
            });

            ShowFAResultModal(true, $"{player.first_name} {player.last_name}");

            _freeAgents = DatabaseManager.Instance.GetFreeAgents();
            BuildFreeAgents();
            RefreshHeader();
        }
        else
        {
            _faCooldowns[player.id] = _season.current_game_day + 14;
            ShowFAResultModal(false, $"{player.first_name} {player.last_name}");
            BuildFreeAgents();
        }
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

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }

    class TradeResult
    {
        public bool WouldAccept;
        public int AcceptScore;
        public int Threshold;
        public List<PlayerData> MySelected;
        public List<PlayerData> OtherSelected;
    }
}
