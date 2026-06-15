using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class LoansController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerLoanCount;
    private Label _headerSeason;
    private Label _headerDate;
    private Button _btnAction;

    // Financial staff
    private VisualElement _finStaffBody;

    // Loans
    private VisualElement _loansContainer;

    // Data
    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private EmployeeData _financiero;
    private List<LoanData> _activeLoans;

    // Slot state (in-memory editor values)
    private long[] _slotAmounts = new long[4];
    private int[] _slotMonths = new int[4];

    private Dictionary<string, Sprite> _logoSprites = new();
    private Texture2D _starTex;
    private StyleBackground _starBg;
    private StyleBackground _empleadoBg;

    private float _currentRate;

    private const long MIN_AMOUNT = 500_000;
    private const long MAX_AMOUNT = 50_000_000;
    private const long AMOUNT_STEP = 500_000;
    private const int MIN_MONTHS = 1;
    private const int MAX_MONTHS = 24;

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
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerLoanCount = _root.Q<Label>("HeaderLoanCount");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _btnAction = _root.Q<Button>("BtnAction");

        _finStaffBody = _root.Q<VisualElement>("FinStaffBody");
        _loansContainer = _root.Q<VisualElement>("LoansContainer");
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

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _starTex = Resources.Load<Texture2D>("Icons/star_24px");
        if (_starTex != null)
            _starBg = new StyleBackground(_starTex);
        _empleadoBg = new StyleBackground(Resources.Load<Texture2D>("Icons/empleado"));

        _manager = DatabaseManager.Instance.GetActiveManager();
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        ReloadData();
    }

    void ReloadData()
    {
        var allLoans = DatabaseManager.Instance.GetLoansByTeam(_myTeam.id);
        _activeLoans = allLoans.Where(l => l.is_active == 1).ToList();

        var staff = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        _financiero = staff.FirstOrDefault(e => e.position == "FINANCIERO");
        _currentRate = GetInterestRate();

        // Initialize editor values for inactive slots
        for (int i = 0; i < 4; i++)
        {
            var existing = allLoans.FirstOrDefault(l => l.slot == i && l.is_active == 1);
            if (existing == null)
            {
                if (_slotAmounts[i] == 0) _slotAmounts[i] = 1_000_000;
                if (_slotMonths[i] == 0) _slotMonths[i] = 12;
            }
        }
    }

    void RegisterCallbacks()
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
        _root.Q<Button>("SubmenuEmpleados")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Employees);
        });
        _root.Q<Button>("SubmenuLesionados")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Injured);
        });
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

        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(_btnAction);
    }

    void Refresh()
    {
        RefreshHeader();
        BuildFinancialStaff();
        BuildLoans();
        _root.Q<Button>("SubmenuPrestamos")?.AddToClassList("nav-submenu-item--active");
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerLoanCount.text = _activeLoans.Count.ToString();

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "DASHBOARD";
    }

    void BuildFinancialStaff()
    {
        _finStaffBody.Clear();

        if (_financiero != null)
        {
            var card = new VisualElement();
            card.AddToClassList("fin-staff-card");

            var icon = new VisualElement();
            icon.AddToClassList("fin-staff-icon");
            icon.style.backgroundImage = _empleadoBg;
            card.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("fin-staff-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("fin-staff-name");
            nameLbl.text = $"{_financiero.first_name} {_financiero.last_name}".ToUpper();
            info.Add(nameLbl);

            var repRow = new VisualElement();
            repRow.style.flexDirection = FlexDirection.Row;
            repRow.style.marginTop = 4;
            for (int i = 0; i < 5; i++)
            {
                var star = new VisualElement();
                star.AddToClassList("fin-staff-star");
                if (i >= _financiero.reputation)
                    star.AddToClassList("fin-staff-star--empty");
                if (_starTex != null)
                    star.style.backgroundImage = _starBg;
                repRow.Add(star);
            }
            info.Add(repRow);

            var interestLbl = new Label();
            interestLbl.AddToClassList("fin-staff-interest");
            float rate = _currentRate * 100;
            interestLbl.text = $"Inter\u00e9s: {rate:F1}%";
            info.Add(interestLbl);

            card.Add(info);
            _finStaffBody.Add(card);
        }
        else
        {
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("fin-staff-empty");
            emptyLbl.text = "A\u00fan no se ha contratado ning\u00fan director financiero.";
            _finStaffBody.Add(emptyLbl);
        }
    }

    void BuildLoans()
    {
        _loansContainer.Clear();

        if (_financiero == null)
        {
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("loan-empty");
            emptyLbl.text = "Contrata un director financiero para gestionar pr\u00e9stamos.";
            _loansContainer.Add(emptyLbl);
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            var existing = _activeLoans.FirstOrDefault(l => l.slot == i);
            var slot = BuildLoanSlot(i, existing);
            _loansContainer.Add(slot);
        }
    }

    VisualElement BuildLoanSlot(int slotIndex, LoanData existingLoan)
    {
        var slot = new VisualElement();
        slot.AddToClassList("loan-slot");

        bool isActive = existingLoan != null;
        if (isActive)
            slot.AddToClassList("loan-slot--active");

        // Header
        var header = new Label();
        header.AddToClassList("loan-header");
        header.text = $"PR\u00c9STAMO {slotIndex + 1}";
        slot.Add(header);

        if (isActive)
        {
            // Active loan display
            BuildActiveLoanDisplay(slot, existingLoan);
        }
        else
        {
            // Empty loan editor
            BuildLoanEditor(slot, slotIndex);
        }

        return slot;
    }

    void BuildActiveLoanDisplay(VisualElement slot, LoanData loan)
    {
        var amountRow = new VisualElement();
        amountRow.AddToClassList("loan-amount-row");

        var amountLbl = new Label();
        amountLbl.AddToClassList("loan-spin-label");
        amountLbl.text = $"Importe: {FormatLoanAmount(loan.amount)}";
        amountRow.Add(amountLbl);
        slot.Add(amountRow);

        var monthsRow = new VisualElement();
        monthsRow.AddToClassList("loan-months-row");

        var monthsLbl = new Label();
        monthsLbl.AddToClassList("loan-spin-label");
        monthsLbl.text = $"Plazo: {loan.months} meses";
        monthsRow.Add(monthsLbl);
        slot.Add(monthsRow);

        var remainingRow = new VisualElement();
        remainingRow.AddToClassList("loan-months-row");

        var remainingLbl = new Label();
        remainingLbl.AddToClassList("loan-spin-label");
        remainingLbl.text = $"Restante: {loan.remaining_months} meses";
        remainingRow.Add(remainingLbl);
        slot.Add(remainingRow);

        var interestRow = new VisualElement();
        interestRow.AddToClassList("loan-months-row");

        var interestLbl = new Label();
        interestLbl.AddToClassList("loan-spin-label");
        interestLbl.text = $"Inter\u00e9s: {loan.interest_rate * 100:F1}%";
        interestRow.Add(interestLbl);
        slot.Add(interestRow);

        // Payment
        var paymentRow = new VisualElement();
        paymentRow.AddToClassList("loan-payment-row");

        var payLbl = new Label();
        payLbl.AddToClassList("loan-payment-label");
        payLbl.text = "Pago mensual:";
        paymentRow.Add(payLbl);

        var payVal = new Label();
        payVal.AddToClassList("loan-payment-value");
        payVal.text = FormatLoanAmount(loan.monthly_payment);
        paymentRow.Add(payVal);
        slot.Add(paymentRow);

        if (_financiero == null) return;

        // Actions
        var actionsRow = new VisualElement();
        actionsRow.AddToClassList("loan-actions-row");

        var payBtn = new Button();
        payBtn.AddToClassList("btn-pay");
        long totalPay = loan.monthly_payment * loan.remaining_months;
        payBtn.text = $"DEVOLVER {FormatLoanAmount(totalPay)}";
        payBtn.userData = loan;
        payBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnPayLoan(loan); });
        if (CursorManager.Instance != null)
        {
            payBtn.RegisterCallback<MouseEnterEvent>(_ =>
                CursorManager.Instance.SetHandCursor());
            payBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                CursorManager.Instance.SetDefaultCursor());
        }
        actionsRow.Add(payBtn);

        slot.Add(actionsRow);
    }

    void BuildLoanEditor(VisualElement slot, int slotIndex)
    {
        // Amount
        var amountRow = new VisualElement();
        amountRow.AddToClassList("loan-amount-row");

        var btnDecAmt = new Label();
        btnDecAmt.AddToClassList("btn-spin");
        btnDecAmt.focusable = true;
        btnDecAmt.text = "\u25C0";
        btnDecAmt.name = $"BtnDecAmt_{slotIndex}";
        SetupLongPress(btnDecAmt, () => StepAmount(slotIndex, -1));
        SetupCursor(btnDecAmt);
        amountRow.Add(btnDecAmt);

        var amtLabel = new Label();
        amtLabel.AddToClassList("loan-spin-label");
        amtLabel.name = $"LoanAmtLabel_{slotIndex}";
        amtLabel.text = FormatLoanAmount(_slotAmounts[slotIndex]);
        amountRow.Add(amtLabel);

        var btnIncAmt = new Label();
        btnIncAmt.AddToClassList("btn-spin");
        btnIncAmt.focusable = true;
        btnIncAmt.text = "\u25B6";
        btnIncAmt.name = $"BtnIncAmt_{slotIndex}";
        SetupLongPress(btnIncAmt, () => StepAmount(slotIndex, 1));
        SetupCursor(btnIncAmt);
        amountRow.Add(btnIncAmt);
        slot.Add(amountRow);

        // Months
        var monthsRow = new VisualElement();
        monthsRow.AddToClassList("loan-months-row");

        var btnDecMon = new Label();
        btnDecMon.AddToClassList("btn-spin");
        btnDecMon.focusable = true;
        btnDecMon.text = "\u25C0";
        btnDecMon.name = $"BtnDecMon_{slotIndex}";
        SetupLongPress(btnDecMon, () => StepMonths(slotIndex, -1));
        SetupCursor(btnDecMon);
        monthsRow.Add(btnDecMon);

        var monLabel = new Label();
        monLabel.AddToClassList("loan-spin-label");
        monLabel.name = $"LoanMonLabel_{slotIndex}";
        monLabel.text = $"{_slotMonths[slotIndex]} meses";
        monthsRow.Add(monLabel);

        var btnIncMon = new Label();
        btnIncMon.AddToClassList("btn-spin");
        btnIncMon.focusable = true;
        btnIncMon.text = "\u25B6";
        btnIncMon.name = $"BtnIncMon_{slotIndex}";
        SetupLongPress(btnIncMon, () => StepMonths(slotIndex, 1));
        SetupCursor(btnIncMon);
        monthsRow.Add(btnIncMon);
        slot.Add(monthsRow);

        // Monthly payment display
        float rate = _currentRate;
        long monthly = CalculateMonthlyPayment(_slotAmounts[slotIndex], _slotMonths[slotIndex], rate);

        var paymentRow = new VisualElement();
        paymentRow.AddToClassList("loan-payment-row");

        var payLbl = new Label();
        payLbl.AddToClassList("loan-payment-label");
        payLbl.text = "Pago mensual:";
        paymentRow.Add(payLbl);

        var payVal = new Label();
        payVal.AddToClassList("loan-payment-value");
        payVal.name = $"LoanPayVal_{slotIndex}";
        payVal.text = FormatLoanAmount(monthly);
        paymentRow.Add(payVal);
        slot.Add(paymentRow);

        // Actions
        var actionsRow = new VisualElement();
        actionsRow.AddToClassList("loan-actions-row");

        var payBtn = new Button();
        payBtn.AddToClassList("btn-pay");
        payBtn.AddToClassList("btn-pay--disabled");
        payBtn.SetEnabled(false);
        payBtn.text = "DEVOLVER";
        actionsRow.Add(payBtn);

        var contractBtn = new Button();
        contractBtn.AddToClassList("btn-contract");
        contractBtn.text = "CONTRATAR";
        contractBtn.userData = slotIndex;
        contractBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnContractLoan(slotIndex); });
        if (CursorManager.Instance != null)
        {
            contractBtn.RegisterCallback<MouseEnterEvent>(_ =>
                CursorManager.Instance.SetHandCursor());
            contractBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                CursorManager.Instance.SetDefaultCursor());
        }
        actionsRow.Add(contractBtn);

        slot.Add(actionsRow);
    }

    void SetupCursor(VisualElement el)
    {
        if (CursorManager.Instance == null) return;
        el.RegisterCallback<MouseEnterEvent>(_ =>
            CursorManager.Instance.SetHandCursor());
        el.RegisterCallback<MouseLeaveEvent>(_ =>
            CursorManager.Instance.SetDefaultCursor());
    }

    void StepAmount(int slotIndex, int dir)
    {
        long val = _slotAmounts[slotIndex] + AMOUNT_STEP * dir;
        _slotAmounts[slotIndex] = val < MIN_AMOUNT ? MIN_AMOUNT : (val > MAX_AMOUNT ? MAX_AMOUNT : val);
        RefreshLoanSlot(slotIndex);
    }

    void StepMonths(int slotIndex, int dir)
    {
        int val = _slotMonths[slotIndex] + dir;
        _slotMonths[slotIndex] = val < MIN_MONTHS ? MIN_MONTHS : (val > MAX_MONTHS ? MAX_MONTHS : val);
        RefreshLoanSlot(slotIndex);
    }

    void RefreshLoanSlot(int slotIndex)
    {
        var amtLabel = _loansContainer.Q<Label>($"LoanAmtLabel_{slotIndex}");
        if (amtLabel != null)
            amtLabel.text = FormatLoanAmount(_slotAmounts[slotIndex]);

        var monLabel = _loansContainer.Q<Label>($"LoanMonLabel_{slotIndex}");
        if (monLabel != null)
            monLabel.text = $"{_slotMonths[slotIndex]} meses";

        var payVal = _loansContainer.Q<Label>($"LoanPayVal_{slotIndex}");
        if (payVal != null)
        {
            float rate = _currentRate;
            long monthly = CalculateMonthlyPayment(_slotAmounts[slotIndex], _slotMonths[slotIndex], rate);
            payVal.text = FormatLoanAmount(monthly);
        }

        ToggleSpinDisabled($"BtnDecAmt_{slotIndex}", _slotAmounts[slotIndex] <= MIN_AMOUNT);
        ToggleSpinDisabled($"BtnIncAmt_{slotIndex}", _slotAmounts[slotIndex] >= MAX_AMOUNT);
        ToggleSpinDisabled($"BtnDecMon_{slotIndex}", _slotMonths[slotIndex] <= MIN_MONTHS);
        ToggleSpinDisabled($"BtnIncMon_{slotIndex}", _slotMonths[slotIndex] >= MAX_MONTHS);
    }

    void ToggleSpinDisabled(string name, bool disabled)
    {
        var el = _loansContainer.Q<Label>(name);
        if (el == null) return;
        if (disabled)
            el.AddToClassList("btn-spin--disabled");
        else
            el.RemoveFromClassList("btn-spin--disabled");
    }

    float GetInterestRate()
    {
        return _financiero == null ? 0.18f : _financiero.reputation switch
        {
            5 => Random.Range(0.02f, 0.04f),
            4 => Random.Range(0.04f, 0.06f),
            3 => Random.Range(0.06f, 0.09f),
            2 => Random.Range(0.09f, 0.13f),
            _ => Random.Range(0.13f, 0.18f),
        };
    }

    long CalculateMonthlyPayment(long amount, int months, float rate)
    {
        double total = amount * (1.0 + rate);
        return (long)System.Math.Ceiling(total / months);
    }

    void OnContractLoan(int slotIndex)
    {
        if (_financiero == null) return;

        long amount = _slotAmounts[slotIndex];
        int months = _slotMonths[slotIndex];
        float rate = _currentRate;
        long monthly = CalculateMonthlyPayment(amount, months, rate);

        var loan = new LoanData
        {
            team_id = _myTeam.id,
            slot = slotIndex,
            amount = amount,
            months = months,
            monthly_payment = monthly,
            interest_rate = rate,
            remaining_months = months,
            is_active = 1,
        };
        DatabaseManager.Instance.InsertLoan(loan);

        // Add money to team budget
        _myTeam.budget += amount;
        DatabaseManager.Instance.UpdateTeamBudget(_myTeam.id, _myTeam.budget);

        // Finance record
        DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
        {
            team_id = _myTeam.id,
            season_id = _season?.id ?? 0,
            record_type = FinanceRecord.TYPE_LOAN,
            game_day = _season?.current_game_day ?? 0,
            amount = amount,
        });

        // Message
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 0,
            sender_id = 0,
            title = "Pr\u00e9stamo bancario concedido",
            body = $"Se ha concedido un pr\u00e9stamo de {FormatLoanAmount(amount)} a {months} meses al {rate*100:F1}% de inter\u00e9s. Cuota mensual: {FormatLoanAmount(monthly)}.",
            game_day = _season?.current_game_day ?? 0,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0,
        });

        Debug.Log($"[Loans] Pr\u00e9stamo {slotIndex}: {FormatLoanAmount(amount)} a {months} meses, cuota {FormatLoanAmount(monthly)}");

        ReloadData();
        Refresh();
    }

    void OnPayLoan(LoanData loan)
    {
        if (_financiero == null) return;

        long remainingTotal = loan.monthly_payment * loan.remaining_months;

        // Deduct from team budget
        _myTeam.budget -= remainingTotal;
        DatabaseManager.Instance.UpdateTeamBudget(_myTeam.id, _myTeam.budget);

        // Finance record (negative amount = expense)
        DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
        {
            team_id = _myTeam.id,
            season_id = _season?.id ?? 0,
            record_type = FinanceRecord.TYPE_LOAN,
            game_day = _season?.current_game_day ?? 0,
            amount = -remainingTotal,
        });

        // Delete loan
        DatabaseManager.Instance.DeleteLoan(loan.id);

        // Message
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 0,
            sender_id = 0,
            title = "Pr\u00e9stamo bancario saldado",
            body = $"Se ha saldado el pr\u00e9stamo del slot {loan.slot + 1}. Importe total devuelto: {FormatLoanAmount(remainingTotal)}.",
            game_day = _season?.current_game_day ?? 0,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0,
        });

        Debug.Log($"[Loans] Pr\u00e9stamo {loan.slot} saldado: {FormatLoanAmount(remainingTotal)}");

        ReloadData();
        Refresh();
    }

    void SetupLongPress(VisualElement el, System.Action onStep)
    {
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

    string FormatLoanAmount(long amount)
    {
        return "$" + amount.ToString("N0").Replace(',', '.');
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
