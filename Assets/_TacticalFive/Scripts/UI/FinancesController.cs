using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class FinancesController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;

    // Summary boxes
    private Label _summaryBudget;
    private Label _summaryAnnualPayroll;
    private Label _summaryMonthlyPayroll;
    private Label _summaryNetBalance;

    // Tabs
    private Button _tabTickets;
    private Button _tabIncome;
    private Button _tabExpenses;
    private Button _tabChart;

    // Panels
    private VisualElement _panelTickets;
    private VisualElement _panelIncome;
    private VisualElement _panelExpenses;
    private VisualElement _panelChart;

    // Ticket config
    private VisualElement _ticketPriceGrid;
    private VisualElement _subscriptionPriceGrid;

    // Tables
    private VisualElement _incomeTable;
    private Label _totalIncomeValue;
    private VisualElement _expensesTable;
    private Label _totalExpensesValue;

    // Chart
    private VisualElement _chartBars;
    private VisualElement _chartLabels;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private TeamSettingsData _teamSettings;
    private List<FinanceRecord> _financeRecords;

    readonly int[] _ticketPrices = { 30, 50, 70, 100, 150, 200, 250, 300, 500 };
    readonly int[] _subscriptionPrices = { 1000, 1200, 1500, 1800, 2100, 2400, 2700, 3000, 3300 };

    readonly string[] _monthNames = { "Octubre", "Noviembre", "Diciembre", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio" };
    readonly (int start, int end)[] _monthRanges = {
        (1, 9), (10, 39), (40, 69), (70, 99), (100, 127),
        (128, 157), (158, 187), (188, 217), (218, 247), (248, 277)
    };

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
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");

        _summaryBudget = _root.Q<Label>("SummaryBudget");
        _summaryAnnualPayroll = _root.Q<Label>("SummaryAnnualPayroll");
        _summaryMonthlyPayroll = _root.Q<Label>("SummaryMonthlyPayroll");
        _summaryNetBalance = _root.Q<Label>("SummaryNetBalance");

        _tabTickets = _root.Q<Button>("TabTickets");
        _tabIncome = _root.Q<Button>("TabIncome");
        _tabExpenses = _root.Q<Button>("TabExpenses");
        _tabChart = _root.Q<Button>("TabChart");

        _panelTickets = _root.Q<VisualElement>("PanelTickets");
        _panelIncome = _root.Q<VisualElement>("PanelIncome");
        _panelExpenses = _root.Q<VisualElement>("PanelExpenses");
        _panelChart = _root.Q<VisualElement>("PanelChart");

        _ticketPriceGrid = _root.Q<VisualElement>("TicketPriceGrid");
        _subscriptionPriceGrid = _root.Q<VisualElement>("SubscriptionPriceGrid");

        _incomeTable = _root.Q<VisualElement>("IncomeTable");
        _totalIncomeValue = _root.Q<Label>("TotalIncomeValue");
        _expensesTable = _root.Q<VisualElement>("ExpensesTable");
        _totalExpensesValue = _root.Q<Label>("TotalExpensesValue");

        _chartBars = _root.Q<VisualElement>("ChartBars");
        _chartLabels = _root.Q<VisualElement>("ChartLabels");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _teamSettings = DatabaseManager.Instance.GetTeamSettings(_myTeam.id);

        if (_teamSettings == null)
        {
            _teamSettings = new TeamSettingsData
            {
                team_id = _myTeam.id,
                ticket_price = 50,
                subscription_price = 2100
            };
            DatabaseManager.Instance.SaveTeamSettings(_teamSettings);
        }

        _financeRecords = _season != null
            ? DatabaseManager.Instance.GetFinanceRecords(_myTeam.id, _season.id)
            : new List<FinanceRecord>();
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

        _tabTickets.clicked += () => { PlayClick(); SwitchTab("tickets"); };
        _tabIncome.clicked += () => { PlayClick(); SwitchTab("income"); };
        _tabExpenses.clicked += () => { PlayClick(); SwitchTab("expenses"); };
        _tabChart.clicked += () => { PlayClick(); SwitchTab("chart"); };

        RegisterHandCursors();
    }

    void CloseAllSubmenus()
    {
        _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
        _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible");
        _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible");
        _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible");
    }

    void RegisterHandCursors()
    {
        if (CursorManager.Instance == null) return;
        _root.Query<Button>().ForEach(btn => CursorManager.Instance.RegisterHandCursor(btn));
    }

    void RegisterNavButtons()
    {
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseAllSubmenus();
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
            CloseAllSubmenus();
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
            CloseAllSubmenus();
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
            CloseAllSubmenus();
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
        var configIcon = _root.Q<VisualElement>("ConfigIcon");
        if (configIcon != null && CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(configIcon);
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
    }

    void Refresh()
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.SetDefaultCursor();
        RefreshHeader();
        BuildSummary();
        SetupTicketDropdowns();
        BuildIncomePanel();
        BuildExpensesPanel();
        BuildChartPanel();
        _root.Q<Button>("SubmenuDecisiones")?.AddToClassList("nav-submenu-item--active");
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos) logoDict[s.name] = s;

        if (logoDict.TryGetValue(_myTeam.logo, out var sprite))
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

    /* ═══════════════════════════════════════════
       SUMMARY BOXES
       ═══════════════════════════════════════════ */

    void BuildSummary()
    {
        if (_myTeam == null || _season == null) return;

        // Budget
        _summaryBudget.text = $"${_myTeam.budget:N0}";

        // Annual payroll
        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long annualPayroll = players.Sum(p => p.salary);
        _summaryAnnualPayroll.text = $"${annualPayroll:N0}";

        // Monthly payroll
        long monthlyPayroll = annualPayroll / 12;
        _summaryMonthlyPayroll.text = $"${monthlyPayroll:N0}";

        // Net balance
        long income = DatabaseManager.Instance.GetTotalIncome(_myTeam.id, _season.id);
        long expenses = DatabaseManager.Instance.GetTotalExpenses(_myTeam.id, _season.id);
        long net = income - expenses;
        _summaryNetBalance.text = net >= 0 ? $"${net:N0}" : $"-${Mathf.Abs((int)net):N0}";
        _summaryNetBalance.RemoveFromClassList("finance-summary-value--income");
        _summaryNetBalance.RemoveFromClassList("finance-summary-value--expense");
        _summaryNetBalance.AddToClassList(net >= 0 ? "finance-summary-value--income" : "finance-summary-value--expense");
    }

    /* ═══════════════════════════════════════════
       TABS
       ═══════════════════════════════════════════ */

    void SwitchTab(string tab)
    {
        _tabTickets.RemoveFromClassList("finances-tab--active");
        _tabIncome.RemoveFromClassList("finances-tab--active");
        _tabExpenses.RemoveFromClassList("finances-tab--active");
        _tabChart.RemoveFromClassList("finances-tab--active");

        _panelTickets.style.display = DisplayStyle.None;
        _panelIncome.style.display = DisplayStyle.None;
        _panelExpenses.style.display = DisplayStyle.None;
        _panelChart.style.display = DisplayStyle.None;

        switch (tab)
        {
            case "tickets":
                _tabTickets.AddToClassList("finances-tab--active");
                _panelTickets.style.display = DisplayStyle.Flex;
                break;
            case "income":
                _tabIncome.AddToClassList("finances-tab--active");
                _panelIncome.style.display = DisplayStyle.Flex;
                break;
            case "expenses":
                _tabExpenses.AddToClassList("finances-tab--active");
                _panelExpenses.style.display = DisplayStyle.Flex;
                break;
            case "chart":
                _tabChart.AddToClassList("finances-tab--active");
                _panelChart.style.display = DisplayStyle.Flex;
                break;
        }
    }

    /* ═══════════════════════════════════════════
       TICKET CONFIG
       ═══════════════════════════════════════════ */

    readonly int[] _ticketPriceOptions = { 30, 50, 70, 100, 150, 200, 250, 300, 500 };
    readonly int[] _subscriptionPriceOptions = { 1000, 1200, 1500, 1800, 2100, 2400, 2700, 3000, 3300 };

    Button _selectedTicketBtn;
    Button _selectedSubBtn;

    void SetupTicketDropdowns()
    {
        var settings = _teamSettings ?? new TeamSettingsData { ticket_price = 50, subscription_price = 2100 };
        bool campaignClosed = IsSubscriptionCampaignClosed();

        // Build ticket price grid
        _ticketPriceGrid.Clear();
        foreach (var price in _ticketPriceOptions)
        {
            var btn = new Button();
            btn.AddToClassList("price-btn");
            btn.text = $"${price}";
            btn.clicked += () => { PlayClick(); SelectTicketPrice(price, btn); };
            _ticketPriceGrid.Add(btn);
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(btn);

            if (settings.ticket_price == price)
                SelectTicketPrice(price, btn);
        }

        // Build subscription price grid
        _subscriptionPriceGrid.Clear();
        foreach (var price in _subscriptionPriceOptions)
        {
            var btn = new Button();
            btn.AddToClassList("price-btn");
            btn.text = $"${price:N0}";
            if (campaignClosed)
            {
                btn.AddToClassList("price-btn--disabled");
                btn.SetEnabled(false);
            }
            else
            {
                btn.clicked += () => { PlayClick(); SelectSubscriptionPrice(price, btn); };
            }
            _subscriptionPriceGrid.Add(btn);
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(btn);

            if (settings.subscription_price == price)
            {
                _selectedSubBtn = btn;
                btn.AddToClassList("price-btn--selected");
                if (campaignClosed)
                {
                    btn.text = "CAMPAÑA CERRADA";
                }
            }
        }
    }

    bool IsSubscriptionCampaignClosed()
    {
        if (_season == null) return true;
        var records = DatabaseManager.Instance.GetFinanceRecords(_myTeam.id, _season.id);
        return records != null && records.Any(r => r.record_type == FinanceRecord.TYPE_SUBSCRIPTION);
    }

    void SelectTicketPrice(int price, Button btn)
    {
        if (_selectedTicketBtn != null)
            _selectedTicketBtn.RemoveFromClassList("price-btn--selected");
        _selectedTicketBtn = btn;
        btn.AddToClassList("price-btn--selected");

        var settings = _teamSettings ?? new TeamSettingsData { team_id = _myTeam.id };
        settings.ticket_price = price;
        settings.team_id = _myTeam.id;
        DatabaseManager.Instance.SaveTeamSettings(settings);
    }

    void SelectSubscriptionPrice(int price, Button btn)
    {
        if (IsSubscriptionCampaignClosed()) return;

        if (_selectedSubBtn != null)
            _selectedSubBtn.RemoveFromClassList("price-btn--selected");
        _selectedSubBtn = btn;
        btn.AddToClassList("price-btn--selected");

        var settings = _teamSettings ?? new TeamSettingsData { team_id = _myTeam.id };
        settings.subscription_price = price;
        settings.team_id = _myTeam.id;
        DatabaseManager.Instance.SaveTeamSettings(settings);
    }

    /* ═══════════════════════════════════════════
       INCOME PANEL
       ═══════════════════════════════════════════ */

    void BuildIncomePanel()
    {
        _incomeTable.Clear();

        if (_season == null) return;

        long ticketTotal = DatabaseManager.Instance.GetFinanceTotalByType(_myTeam.id, _season.id, FinanceRecord.TYPE_TICKET);
        long subTotal = DatabaseManager.Instance.GetFinanceTotalByType(_myTeam.id, _season.id, FinanceRecord.TYPE_SUBSCRIPTION);
        long sponsorTotal = DatabaseManager.Instance.GetFinanceTotalByType(_myTeam.id, _season.id, FinanceRecord.TYPE_SPONSORSHIP);
        long tvTotal = DatabaseManager.Instance.GetFinanceTotalByType(_myTeam.id, _season.id, FinanceRecord.TYPE_TV);
        long total = ticketTotal + subTotal + sponsorTotal + tvTotal;

        AddTableRow(_incomeTable, "Taquilla", ticketTotal, true);
        AddTableRow(_incomeTable, "Abonos", subTotal, true);
        AddTableRow(_incomeTable, "Patrocinios", sponsorTotal, true);
        AddTableRow(_incomeTable, "Televisión", tvTotal, true);

        _totalIncomeValue.text = $"${total:N0}";
    }

    /* ═══════════════════════════════════════════
       EXPENSES PANEL
       ═══════════════════════════════════════════ */

    void BuildExpensesPanel()
    {
        _expensesTable.Clear();

        if (_season == null) return;

        long salariesTotal = DatabaseManager.Instance.GetFinanceTotalByType(_myTeam.id, _season.id, FinanceRecord.TYPE_SALARIES);
        long empSalaryTotal = DatabaseManager.Instance.GetFinanceTotalByType(_myTeam.id, _season.id, FinanceRecord.TYPE_EMPLOYEE_SALARY);
        long renovationTotal = DatabaseManager.Instance.GetFinanceTotalByType(_myTeam.id, _season.id, FinanceRecord.TYPE_RENOVATION);
        long dismissalTotal = DatabaseManager.Instance.GetFinanceTotalByType(_myTeam.id, _season.id, FinanceRecord.TYPE_DISMISSAL);
        long taxTotal = DatabaseManager.Instance.GetFinanceTotalByType(_myTeam.id, _season.id, FinanceRecord.TYPE_TAX);
        long buyoutTotal = DatabaseManager.Instance.GetFinanceTotalByType(_myTeam.id, _season.id, FinanceRecord.TYPE_BUYOUT);
        long total = salariesTotal + empSalaryTotal + renovationTotal + dismissalTotal + taxTotal + buyoutTotal;

        AddTableRow(_expensesTable, "Sueldos de jugadores", salariesTotal, false);
        AddTableRow(_expensesTable, "Sueldos empleados", empSalaryTotal, false);
        AddTableRow(_expensesTable, "Remodelaciones", renovationTotal, false);
        AddTableRow(_expensesTable, "Despidos", dismissalTotal, false);
        AddTableRow(_expensesTable, "Luxury tax", taxTotal, false);
        AddTableRow(_expensesTable, "Rescisiones (buyout)", buyoutTotal, false);

        _totalExpensesValue.text = $"${total:N0}";
    }

    void AddTableRow(VisualElement table, string label, long value, bool isIncome)
    {
        var row = new VisualElement();
        row.AddToClassList("finances-row");

        var lbl = new Label(label);
        lbl.AddToClassList("finances-row-label");

        var val = new Label($"${value:N0}");
        val.AddToClassList("finances-row-value");
        val.AddToClassList(isIncome ? "finances-row-value--income" : "finances-row-value--expense");

        row.Add(lbl);
        row.Add(val);
        table.Add(row);
    }

    /* ═══════════════════════════════════════════
       CHART PANEL
       ═══════════════════════════════════════════ */

    void BuildChartPanel()
    {
        _chartBars.Clear();
        _chartLabels.Clear();

        if (_season == null) return;

        var monthData = new (long income, long expenses, long balance)[10];
        for (int i = 0; i < 10; i++)
        {
            int start = _monthRanges[i].start;
            int end = _monthRanges[i].end;

            // game_day = 0 counts as October (first month)
            long inc = _financeRecords
                .Where(r =>
                {
                    int day = r.game_day;
                    if (day == 0) day = 1; // treat unassigned as day 1 (October)
                    return day >= start && day <= end && r.record_type <= FinanceRecord.TYPE_TV;
                })
                .Sum(r => r.amount);

            long exp = _financeRecords
                .Where(r =>
                {
                    int day = r.game_day;
                    if (day == 0) day = 1; // treat unassigned as day 1 (October)
                    return day >= start && day <= end && r.record_type >= FinanceRecord.TYPE_RENOVATION;
                })
                .Sum(r => r.amount);

            monthData[i] = (inc, exp, inc - exp);
        }

        long maxAbs = 1;
        foreach (var d in monthData)
            maxAbs = (long)Mathf.Max(maxAbs, Mathf.Abs((int)d.balance));

        for (int i = 0; i < 10; i++)
        {
            var (inc, exp, bal) = monthData[i];
            bool isIncome = bal >= 0;
            float pct = maxAbs > 0 ? Mathf.Abs((float)bal) / maxAbs : 0f;
            int barHeight = Mathf.Max(4, (int)(pct * 220));

            // Bar wrapper
            var wrapper = new VisualElement();
            wrapper.AddToClassList("chart-bar-wrapper");

            // Month value above bar
            var valueLbl = new Label($"${Mathf.Abs((int)bal):N0}");
            valueLbl.AddToClassList("chart-month-value");
            valueLbl.AddToClassList(isIncome ? "chart-month-value--income" : "chart-month-value--expense");
            wrapper.Add(valueLbl);

            // Bar
            var bar = new VisualElement();
            bar.AddToClassList("chart-bar");
            bar.AddToClassList(isIncome ? "chart-bar--income" : "chart-bar--expense");
            bar.style.height = new StyleLength(new Length(barHeight, LengthUnit.Pixel));
            wrapper.Add(bar);

            _chartBars.Add(wrapper);

            // Month label
            var monthLbl = new Label(_monthNames[i]);
            monthLbl.AddToClassList("chart-month-label");
            _chartLabels.Add(monthLbl);
        }
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
