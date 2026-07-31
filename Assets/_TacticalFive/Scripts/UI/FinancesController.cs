using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class FinancesController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Finances;

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
    private TeamSettingsData _teamSettings;
    private List<FinanceRecord> _financeRecords;

    readonly int[] _ticketPrices = { 30, 50, 70, 100, 150, 200, 250, 300, 500 };
    readonly int[] _subscriptionPrices = { 1000, 1200, 1500, 1800, 2100, 2400, 2700, 3000, 3300 };
    protected override void CacheReferences()
    {

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
    protected override void LoadData()
    {
        base.LoadData();

        
        

        
        
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
    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _tabTickets.clicked += () => { PlayClick(); SwitchTab("tickets"); };
        _tabIncome.clicked += () => { PlayClick(); SwitchTab("income"); };
        _tabExpenses.clicked += () => { PlayClick(); SwitchTab("expenses"); };
        _tabChart.clicked += () => { PlayClick(); SwitchTab("chart"); };
    }
    protected override void Refresh()
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.SetDefaultCursor();
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Finances] RefreshHeader error: {ex.Message}"); }
        BuildSummary();
        SetupTicketDropdowns();
        BuildIncomePanel();
        BuildExpensesPanel();
        BuildChartPanel();
        _root.Q<Button>("SubmenuDecisiones")?.AddToClassList("nav-submenu-item--active");
    }

    /* ═══════════════════════════════════════════
       SUMMARY BOXES
       ═══════════════════════════════════════════ */

    void BuildSummary()
    {
        if (_myTeam == null || _season == null) return;

        // Budget
        _summaryBudget.text = FormatMoney(_myTeam.budget);

        // Annual payroll
        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long annualPayroll = players.Sum(p => p.salary);
        _summaryAnnualPayroll.text = FormatMoney(annualPayroll);

        // Monthly payroll
        long monthlyPayroll = annualPayroll / 12;
        _summaryMonthlyPayroll.text = FormatMoney(monthlyPayroll);

        // Net balance
        long income = DatabaseManager.Instance.GetTotalIncome(_myTeam.id, _season.id);
        long expenses = DatabaseManager.Instance.GetTotalExpenses(_myTeam.id, _season.id);
        long net = income - expenses;
        _summaryNetBalance.text = FormatMoney(net);
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
            btn.text = $"{price:N0} $";
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
            btn.text = $"{price:N0} $";
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
                    btn.text = "CERRADO";
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

        _totalIncomeValue.text = FormatMoney(total);
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
        AddTableRow(_expensesTable, "Luxury tax", System.Math.Abs(taxTotal), false);
        AddTableRow(_expensesTable, "Rescisiones (buyout)", buyoutTotal, false);

        _totalExpensesValue.text = FormatMoney(total);
    }

    void AddTableRow(VisualElement table, string label, long value, bool isIncome)
    {
        var row = new VisualElement();
        row.AddToClassList("finances-row");

        var lbl = new Label(label);
        lbl.AddToClassList("finances-row-label");

        var val = new Label(FormatMoney(value));
        val.AddToClassList("finances-row-value");
        val.AddToClassList(isIncome ? "finances-row-value--income" : "finances-row-value--expense");

        row.Add(lbl);
        row.Add(val);
        table.Add(row);
    }

    /* ═══════════════════════════════════════════
       CHART PANEL
       ═══════════════════════════════════════════ */

    System.DateTime GetDateForGameDay(int gameDay, bool hasPreseason)
    {
        var start = new System.DateTime(_season.year_start, 10, 22);
        if (gameDay >= 1)
            return start.AddDays(gameDay - 1);
        if (gameDay < 0)
            return start.AddDays(gameDay);
        return hasPreseason
            ? new System.DateTime(_season.year_start, 9, 5)
            : start;
    }

    int GetMonthIndex(System.DateTime date, bool hasPreseason)
    {
        if (hasPreseason)
        {
            int idx = (date.Month - 9 + 12) % 12;
            return idx <= 9 ? idx : -1;
        }
        else
        {
            int idx = (date.Month - 10 + 12) % 12;
            return idx <= 9 ? idx : -1;
        }
    }

    void BuildChartPanel()
    {
        _chartBars.Clear();
        _chartLabels.Clear();

        if (_season == null) return;

        bool hasPreseason = DatabaseManager.Instance.GetAllGames(_manager.id)
            .Any(g => g.game_type == "preseason");

        string[] monthNames = hasPreseason
            ? new[] { "Septiembre", "Octubre", "Noviembre", "Diciembre", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio" }
            : new[] { "Octubre", "Noviembre", "Diciembre", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio" };

        var totals = new (long inc, long exp)[10];
        foreach (var r in _financeRecords)
        {
            var date = GetDateForGameDay(r.game_day, hasPreseason);
            int idx = GetMonthIndex(date, hasPreseason);
            if (idx < 0 || idx >= 10) continue;

            if (r.record_type <= FinanceRecord.TYPE_TV)
                totals[idx].inc += r.amount;
            else
                totals[idx].exp += r.amount;
        }

        var monthData = totals.Select(t => (income: t.inc, expenses: t.exp, balance: t.inc - t.exp)).ToArray();

        long maxAbs = 1;
        foreach (var d in monthData)
            maxAbs = (long)Mathf.Max(maxAbs, Mathf.Abs((int)d.balance));

        for (int i = 0; i < 10; i++)
        {
            var (inc, exp, bal) = monthData[i];
            bool isIncome = bal >= 0;
            float pct = maxAbs > 0 ? Mathf.Abs((float)bal) / maxAbs : 0f;
            int barHeight = Mathf.Max(4, (int)(pct * 220));

            var wrapper = new VisualElement();
            wrapper.AddToClassList("chart-bar-wrapper");

            var valueLbl = new Label(FormatMoney(bal));
            valueLbl.AddToClassList("chart-month-value");
            valueLbl.AddToClassList(isIncome ? "chart-month-value--income" : "chart-month-value--expense");
            wrapper.Add(valueLbl);

            var bar = new VisualElement();
            bar.AddToClassList("chart-bar");
            bar.AddToClassList(isIncome ? "chart-bar--income" : "chart-bar--expense");
            bar.style.height = new StyleLength(new Length(barHeight, LengthUnit.Pixel));
            wrapper.Add(bar);

            _chartBars.Add(wrapper);

            var monthLbl = new Label(monthNames[i]);
            monthLbl.AddToClassList("chart-month-label");
            _chartLabels.Add(monthLbl);
        }
    }

    string FormatMoney(long amount)
    {
        return System.Math.Abs(amount).ToString("N0").Replace(',', '.') + " $";
    }
}
