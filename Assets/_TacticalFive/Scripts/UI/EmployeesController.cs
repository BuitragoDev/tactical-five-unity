using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class EmployeesController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Employees;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerSeason;
    private Label _headerDate;

    // Pages
    private VisualElement _pageEmpleados;
    private VisualElement _pageMercado;

    // Tab buttons
    private Button _tabEmpleados;
    private Button _tabMercado;

    // Staff table
    private VisualElement _staffTableBody;

    // Market
    private Button _marketTabASISTENTE;
    private Button _marketTabMEDICO;
    private Button _marketTabFINANCIERO;
    private Button _marketTabOJEADOR;
    private Button _marketTabPSICOLOGO;
    private Button _marketTabPABELLON;
    private VisualElement _marketTableBody;

    // Hire modal
    private VisualElement _hireOverlay;
    private Label _hireTitle;
    private Label _hireText1;
    private Label _hireText2;
    private Label _hireText3;
    private Button _btnHireCancel;
    private Button _btnHireConfirm;

    // Hire result modal
    private VisualElement _hireResultOverlay;
    private Label _hireResultTitle;
    private Label _hireResultText;
    private Button _btnHireResultOk;

    // Data
    private List<EmployeeData> _myStaff;
    private List<EmployeeData> _candidates;
    private EmployeeData _selectedCandidate;
    private EmployeeData _selectedFireEmployee;
    private bool _isFiring;
    private string _activeTab = "empleados";
    private string _activeMarketTab = "ASISTENTE";
    private Dictionary<string, Sprite> _logoSprites = new();
    private Texture2D _starTex;
    private StyleBackground _starBg;

    private static readonly Dictionary<string, string> PositionLabels = new()
    {
        { "ASISTENTE", "ASISTENTE" },
        { "MEDICO", "SERVICIO MÉDICO" },
        { "FINANCIERO", "FINANCIERO" },
        { "OJEADOR", "OJEADOR" },
        { "PSICOLOGO", "PSICÓLOGO" },
        { "PABELLON", "ENCARGADO DEL PABELLÓN" }
    };
    private static readonly string[] PositionOrder = { "ASISTENTE", "MEDICO", "FINANCIERO", "OJEADOR", "PSICOLOGO", "PABELLON" };
    private static readonly int REFRESH_INTERVAL = 30;
    private static readonly int CANDIDATES_PER_POSITION = 5;

    private static readonly (string code, string name)[] Nationalities = {
        ("USA", "Estados Unidos"), ("ESP", "España"), ("FRA", "Francia"),
        ("ITA", "Italia"), ("DEU", "Alemania"), ("GBR", "Reino Unido"),
        ("GRC", "Grecia"), ("SRB", "Serbia"), ("HRV", "Croacia"),
        ("AUS", "Australia"), ("BRA", "Brasil"), ("ARG", "Argentina"),
        ("CAN", "Canadá"), ("NGA", "Nigeria"), ("SEN", "Senegal"),
        ("CMR", "Camerún"), ("LTU", "Lituania"), ("LVA", "Letonia"),
        ("TUR", "Turquía"), ("NOR", "Noruega")
    };

    private static readonly string[] FirstNames = {
        "James", "John", "William", "George", "Thomas", "Charles", "Henry", "Edward", "Harry", "Arthur",
        "Jack", "Oliver", "Noah", "Jacob", "Leo", "Oscar", "Alfie", "Freddie", "Theo", "Archie",
        "Charlie", "Finley", "Harrison", "Ethan", "Joseph", "Samuel", "Daniel", "Matthew", "Luke", "Adam",
        "Benjamin", "Michael", "Nathan", "Ryan", "Connor", "Louis", "Alexander", "David", "Robert", "Richard",
        "Peter", "Andrew", "Jonathan", "Simon", "Paul", "Mark", "Stephen", "Alan", "Martin", "Graham",
        "Scott", "Dean", "Craig", "Bradley", "Lewis", "Callum", "Jamie", "Kyle", "Reece", "Sean",
        "Dylan", "Logan", "Mason", "Aiden", "Tyler", "Cameron", "Jordan", "Zachary", "Aaron", "Elliot",
        "Blake", "Cole", "Dominic", "Evan", "Isaac", "Max", "Patrick", "Philip", "Ross", "Spencer",
        "Tristan", "Wesley", "Xavier", "Owen", "Caleb", "Nathaniel", "Vincent", "Harvey", "Riley", "Toby",
        "Luca", "Matteo", "Giovanni", "Carlos", "Miguel",
        "Alejandro", "Pierre", "Jean", "Hans", "Erik"
    };

    private static readonly string[] LastNames = {
        "Smith", "Jones", "Taylor", "Brown", "Williams", "Wilson", "Johnson", "Davies", "Robinson", "Wright",
        "Thompson", "Evans", "Walker", "White", "Roberts", "Green", "Hall", "Wood", "Jackson", "Clarke",
        "Turner", "Harris", "Edwards", "Martin", "Cooper", "Hill", "Ward", "Morris", "Moore", "Clark",
        "Lee", "King", "Baker", "Harrison", "Morgan", "Allen", "Scott", "Phillips", "Watson", "Parker",
        "Price", "Bennett", "Young", "Griffiths", "Mitchell", "Carter", "Cook", "Bailey", "Richardson", "Cox",
        "Howard", "Wardle", "Brooks", "Bell", "Murphy", "Miller", "Collins", "Gray", "Hughes", "Marshall",
        "Shaw", "Webb", "Foster", "Butler", "Chapman", "Pearson", "Armstrong", "Reynolds", "Stephens", "Payne",
        "Gardner", "Spencer", "Hunter", "Fox", "Gibson", "Harvey", "Palmer", "Warren", "Knight", "Mason",
        "Ellis", "Bishop", "Porter", "George", "West", "Grant", "Black", "Fisher", "Holmes", "Stone",
        "Garcia", "Martinez", "Rossi", "Bianchi", "Dubois",
        "Moreau", "Muller", "Schmidt", "Andersen", "Johansson"
    };

    protected override void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");

        _pageEmpleados = _root.Q<VisualElement>("PageEmpleados");
        _pageMercado = _root.Q<VisualElement>("PageMercado");

        _tabEmpleados = _root.Q<Button>("TabEmpleados");
        _tabMercado = _root.Q<Button>("TabMercado");

        _staffTableBody = _root.Q<VisualElement>("StaffTableBody");

        _marketTabASISTENTE = _root.Q<Button>("MarketTabASISTENTE");
        _marketTabMEDICO = _root.Q<Button>("MarketTabMEDICO");
        _marketTabFINANCIERO = _root.Q<Button>("MarketTabFINANCIERO");
        _marketTabOJEADOR = _root.Q<Button>("MarketTabOJEADOR");
        _marketTabPSICOLOGO = _root.Q<Button>("MarketTabPSICOLOGO");
        _marketTabPABELLON = _root.Q<Button>("MarketTabPABELLON");
        _marketTableBody = _root.Q<VisualElement>("MarketTableBody");

        _hireOverlay = _root.Q<VisualElement>("HireOverlay");
        _hireTitle = _root.Q<Label>("HireTitle");
        _hireText1 = _root.Q<Label>("HireText1");
        _hireText2 = _root.Q<Label>("HireText2");
        _hireText3 = _root.Q<Label>("HireText3");
        _btnHireCancel = _root.Q<Button>("BtnHireCancel");
        _btnHireConfirm = _root.Q<Button>("BtnHireConfirm");

        _hireResultOverlay = _root.Q<VisualElement>("HireResultOverlay");
        _hireResultTitle = _root.Q<Label>("HireResultTitle");
        _hireResultText = _root.Q<Label>("HireResultText");
        _btnHireResultOk = _root.Q<Button>("BtnHireResultOk");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _starTex = Resources.Load<Texture2D>("Icons/star_24px");
        if (_starTex != null)
            _starBg = new StyleBackground(_starTex);

        _myStaff = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        EnsureCandidates();
    }

    void EnsureCandidates()
    {
        int currentDay = _season?.current_game_day ?? 0;
        _candidates = DatabaseManager.Instance.GetEmployeeCandidates();

        bool needsRefresh = _candidates.Count == 0;
        if (!needsRefresh)
        {
            int oldestDay = _candidates.Min(c => c.candidate_day);
            if (currentDay - oldestDay >= REFRESH_INTERVAL)
                needsRefresh = true;
        }

        if (needsRefresh)
        {
            DatabaseManager.Instance.DeleteEmployeeCandidates();
            _candidates.Clear();

            foreach (var pos in PositionOrder)
            {
                for (int i = 0; i < CANDIDATES_PER_POSITION; i++)
                {
                    int rep = Random.Range(1, 6);
                    var emp = new EmployeeData
                    {
                        team_id = 0,
                        position = pos,
                        first_name = GenerateFirstName(),
                        last_name = GenerateLastName(),
                        reputation = rep,
                        salary = GenerateSalary(pos, rep),
                        contract_years = Random.Range(1, 4),
                        candidate_day = currentDay,
                        nationality = Nationalities[Random.Range(0, Nationalities.Length)].code
                    };
                    DatabaseManager.Instance.InsertEmployee(emp);
                    _candidates.Add(emp);
                }
            }
        }
        else
        {
            foreach (var pos in PositionOrder)
            {
                int count = _candidates.Count(c => c.position == pos);
                if (count < CANDIDATES_PER_POSITION)
                    RefillCandidates(pos, currentDay);
            }
        }
    }

    string GenerateFirstName() => FirstNames[Random.Range(0, FirstNames.Length)];
    string GenerateLastName() => LastNames[Random.Range(0, LastNames.Length)];

    long GenerateSalary(string position, int reputation)
    {
        long min = position switch
        {
            "ASISTENTE" => 1_000_000,
            "MEDICO" => 800_000,
            "FINANCIERO" => 1_000_000,
            "OJEADOR" => 500_000,
            "PSICOLOGO" => 600_000,
            "PABELLON" => 700_000,
            _ => 500_000
        };
        long max = position switch
        {
            "ASISTENTE" => 4_000_000,
            "MEDICO" => 3_000_000,
            "FINANCIERO" => 5_000_000,
            "OJEADOR" => 2_000_000,
            "PSICOLOGO" => 2_500_000,
            "PABELLON" => 2_500_000,
            _ => 2_000_000
        };
        long step = 100_000;
        float t = (reputation - 1) / 4f;
        long raw = min + (long)((max - min) * t);
        return (raw / step) * step;
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _btnHireCancel?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseHireModal(); });
        _btnHireConfirm?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ConfirmHire(); });
        _btnHireResultOk?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseHireResultModal(); });
        _hireOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _hireOverlay)
            { PlayClick(); CloseHireModal(); }
        });
        _hireResultOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _hireResultOverlay)
            { PlayClick(); CloseHireResultModal(); }
        });

        _tabEmpleados?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("empleados"); });
        _tabMercado?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowTab("mercado"); });

        _marketTabASISTENTE?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowMarketTab("ASISTENTE"); });
        _marketTabMEDICO?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowMarketTab("MEDICO"); });
        _marketTabFINANCIERO?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowMarketTab("FINANCIERO"); });
        _marketTabOJEADOR?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowMarketTab("OJEADOR"); });
        _marketTabPSICOLOGO?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowMarketTab("PSICOLOGO"); });
        _marketTabPABELLON?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowMarketTab("PABELLON"); });

        if (CursorManager.Instance == null) return;
        var cursor = CursorManager.Instance;
        cursor.RegisterHandCursor(_btnHireCancel);
        cursor.RegisterHandCursor(_btnHireConfirm);
        cursor.RegisterHandCursor(_btnHireResultOk);
    }

    protected override void Refresh()
    {
        _activeTab = "empleados";
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Employees] RefreshHeader error: {ex.Message}"); }
        ShowTab(_activeTab);
        BuildStaff();
        BuildMarketTable();
        _root.Q<VisualElement>("RosterSubmenu")?.AddToClassList("nav-submenu--visible");
        _root.Q<Button>("SubmenuEmpleados")?.AddToClassList("nav-submenu-item--active");

        _hireOverlay.style.display = DisplayStyle.None;
        _hireResultOverlay.style.display = DisplayStyle.None;
    }

    protected override void RefreshHeader()
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

        var teamPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long totalPayroll = teamPlayers.Sum(p => p.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";
        _headerPayroll.RemoveFromClassList("header-stat-value--negative");
        if (totalPayroll > 0)
            _headerPayroll.AddToClassList("header-stat-value--negative");

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - totalPayroll;

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

    // ── TAB MANAGEMENT ──

    void ShowTab(string tab)
    {
        _activeTab = tab;

        _tabEmpleados.RemoveFromClassList("employees-tab--active");
        _tabMercado.RemoveFromClassList("employees-tab--active");

        if (tab == "empleados")
        {
            _tabEmpleados.AddToClassList("employees-tab--active");
            _pageEmpleados.style.display = DisplayStyle.Flex;
            _pageMercado.style.display = DisplayStyle.None;
        }
        else
        {
            _tabMercado.AddToClassList("employees-tab--active");
            _pageEmpleados.style.display = DisplayStyle.None;
            _pageMercado.style.display = DisplayStyle.Flex;
        }
    }

    void ShowMarketTab(string position)
    {
        _activeMarketTab = position;

        _marketTabASISTENTE.RemoveFromClassList("market-tab--active");
        _marketTabMEDICO.RemoveFromClassList("market-tab--active");
        _marketTabFINANCIERO.RemoveFromClassList("market-tab--active");
        _marketTabOJEADOR.RemoveFromClassList("market-tab--active");
        _marketTabPSICOLOGO.RemoveFromClassList("market-tab--active");
        _marketTabPABELLON.RemoveFromClassList("market-tab--active");

        switch (position)
        {
            case "ASISTENTE": _marketTabASISTENTE.AddToClassList("market-tab--active"); break;
            case "MEDICO": _marketTabMEDICO.AddToClassList("market-tab--active"); break;
            case "FINANCIERO": _marketTabFINANCIERO.AddToClassList("market-tab--active"); break;
            case "OJEADOR": _marketTabOJEADOR.AddToClassList("market-tab--active"); break;
            case "PSICOLOGO": _marketTabPSICOLOGO.AddToClassList("market-tab--active"); break;
            case "PABELLON": _marketTabPABELLON.AddToClassList("market-tab--active"); break;
        }

        BuildMarketTable();
    }

    // ── BUILD STAFF TABLE ──

    void BuildStaff()
    {
        _staffTableBody.Clear();

        foreach (var pos in PositionOrder)
        {
            var emp = _myStaff.FirstOrDefault(e => e.position == pos);
            var row = new VisualElement();
            row.AddToClassList("emp-table-row");

            // NOMBRE
            var nameCol = new Label();
            nameCol.AddToClassList("emp-col");
            nameCol.AddToClassList("emp-col--nombre");
            if (emp != null)
            {
                nameCol.text = $"{emp.first_name} {emp.last_name}";
                nameCol.AddToClassList("emp-employee-name");
            }
            else
            {
                nameCol.text = "—";
                nameCol.AddToClassList("emp-empty-text");
            }
            row.Add(nameCol);

            // PUESTO
            var posCol = new Label();
            posCol.AddToClassList("emp-col");
            posCol.AddToClassList("emp-col--puesto");
            posCol.text = PositionLabels.TryGetValue(pos, out var lbl) ? lbl : pos;
            row.Add(posCol);

            // NACIONALIDAD
            var natCol = new VisualElement();
            natCol.AddToClassList("emp-col");
            natCol.AddToClassList("emp-col--nacionalidad");
            if (emp != null && !string.IsNullOrEmpty(emp.nationality))
            {
                var natCell = new VisualElement();
                natCell.AddToClassList("nat-cell");

                var flag = new VisualElement();
                flag.AddToClassList("flag-icon");
                var flagTex = Resources.Load<Texture2D>($"Flags/{emp.nationality}");
                if (flagTex != null)
                    flag.style.backgroundImage = new StyleBackground(flagTex);
                natCell.Add(flag);

                var natName = new Label();
                string countryName = "";
                foreach (var n in Nationalities)
                {
                    if (n.code == emp.nationality) { countryName = n.name; break; }
                }
                natName.text = countryName;
                natName.AddToClassList("emp-col");
                natCell.Add(natName);

                natCol.Add(natCell);
            }
            else
            {
                var emptyNat = new Label();
                emptyNat.text = "—";
                emptyNat.AddToClassList("emp-empty-text");
                natCol.Add(emptyNat);
            }
            row.Add(natCol);

            // REPUTACIÓN
            var repCol = new VisualElement();
            repCol.AddToClassList("emp-col");
            repCol.AddToClassList("emp-col--reputacion");
            if (emp != null)
            {
                var starsRow = new VisualElement();
                starsRow.AddToClassList("emp-stars");
                for (int i = 0; i < 5; i++)
                {
                    var star = new VisualElement();
                    star.AddToClassList(i < emp.reputation ? "emp-star" : "emp-star--empty");
                    if (_starTex != null)
                        star.style.backgroundImage = _starBg;
                    starsRow.Add(star);
                }
                repCol.Add(starsRow);
            }
            else
            {
                var emptyRep = new Label();
                emptyRep.text = "—";
                emptyRep.AddToClassList("emp-empty-text");
                repCol.Add(emptyRep);
            }
            row.Add(repCol);

            // SUELDO
            var salaryCol = new Label();
            salaryCol.AddToClassList("emp-col");
            salaryCol.AddToClassList("emp-col--sueldo");
            salaryCol.text = emp != null ? FormatSalary(emp.salary) : "—";
            if (emp == null) salaryCol.AddToClassList("emp-empty-text");
            row.Add(salaryCol);

            // CONTRATO
            var contractCol = new Label();
            contractCol.AddToClassList("emp-col");
            contractCol.AddToClassList("emp-col--contrato");
            if (emp != null)
            {
                string plural = emp.contract_years != 1 ? "S" : "";
                contractCol.text = $"{emp.contract_years} AÑO{plural}";
            }
            else
            {
                contractCol.text = "—";
                contractCol.AddToClassList("emp-empty-text");
            }
            row.Add(contractCol);

            // ACCIONES
            var actionCol = new VisualElement();
            actionCol.AddToClassList("emp-col");
            actionCol.AddToClassList("emp-col--acciones");
            if (emp != null)
            {
                var fireBtn = new Button();
                fireBtn.AddToClassList("btn-fire-table");
                fireBtn.text = "DESPEDIR";
                var captured = emp;
                fireBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnFireClicked(captured); });
                if (CursorManager.Instance != null)
                {
                    fireBtn.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
                    fireBtn.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
                }
                actionCol.Add(fireBtn);
            }
            row.Add(actionCol);

            _staffTableBody.Add(row);
        }
    }

    // ── BUILD MARKET TABLE ──

    void BuildMarketTable()
    {
        _marketTableBody.Clear();

        var posCandidates = _candidates
            .Where(c => c.position == _activeMarketTab)
            .OrderByDescending(c => c.reputation)
            .ToList();

        foreach (var candidate in posCandidates)
        {
            var row = new VisualElement();
            row.AddToClassList("emp-table-row");

            // NOMBRE
            var nameCol = new Label();
            nameCol.AddToClassList("emp-col");
            nameCol.AddToClassList("emp-col--nombre");
            nameCol.text = $"{candidate.first_name} {candidate.last_name}";
            nameCol.AddToClassList("emp-employee-name");
            row.Add(nameCol);

            // NACIONALIDAD
            var natCol = new VisualElement();
            natCol.AddToClassList("emp-col");
            natCol.AddToClassList("emp-col--nacionalidad");
            var natCell = new VisualElement();
            natCell.AddToClassList("nat-cell");

            var flag = new VisualElement();
            flag.AddToClassList("flag-icon");
            if (!string.IsNullOrEmpty(candidate.nationality))
            {
                var flagTex = Resources.Load<Texture2D>($"Flags/{candidate.nationality}");
                if (flagTex != null)
                    flag.style.backgroundImage = new StyleBackground(flagTex);
            }
            natCell.Add(flag);

            var natName = new Label();
            string countryName = "";
            foreach (var n in Nationalities)
            {
                if (n.code == candidate.nationality) { countryName = n.name; break; }
            }
            natName.text = countryName;
            natName.AddToClassList("emp-col");
            natCell.Add(natName);

            natCol.Add(natCell);
            row.Add(natCol);

            // REPUTACIÓN
            var repCol = new VisualElement();
            repCol.AddToClassList("emp-col");
            repCol.AddToClassList("emp-col--reputacion");
            var starsRow = new VisualElement();
            starsRow.AddToClassList("emp-stars");
            for (int i = 0; i < 5; i++)
            {
                var star = new VisualElement();
                star.AddToClassList(i < candidate.reputation ? "emp-star" : "emp-star--empty");
                if (_starTex != null)
                    star.style.backgroundImage = _starBg;
                starsRow.Add(star);
            }
            repCol.Add(starsRow);
            row.Add(repCol);

            // SUELDO
            var salaryCol = new Label();
            salaryCol.AddToClassList("emp-col");
            salaryCol.AddToClassList("emp-col--sueldo");
            salaryCol.text = FormatSalary(candidate.salary);
            row.Add(salaryCol);

            // CONTRATO
            var contractCol = new Label();
            contractCol.AddToClassList("emp-col");
            contractCol.AddToClassList("emp-col--contrato");
            string plural = candidate.contract_years != 1 ? "S" : "";
            contractCol.text = $"{candidate.contract_years} AÑO{plural}";
            row.Add(contractCol);

            // ACCIONES
            var actionCol = new VisualElement();
            actionCol.AddToClassList("emp-col");
            actionCol.AddToClassList("emp-col--acciones");

            bool hasExisting = _myStaff.Any(e => e.position == candidate.position);
            var hireBtn = new Button();
            hireBtn.AddToClassList(hasExisting ? "btn-replace-table" : "btn-hire-table");
            hireBtn.text = hasExisting ? "REEMPLAZAR" : "CONTRATAR";
            var capturedCandidate = candidate;
            hireBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnHireClicked(capturedCandidate); });
            if (CursorManager.Instance != null)
            {
                hireBtn.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
                hireBtn.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
            }
            actionCol.Add(hireBtn);
            row.Add(actionCol);

            _marketTableBody.Add(row);
        }

        if (posCandidates.Count == 0)
        {
            var emptyRow = new VisualElement();
            emptyRow.AddToClassList("emp-table-row");
            var emptyLabel = new Label();
            emptyLabel.text = "No hay candidatos disponibles para este puesto.";
            emptyLabel.AddToClassList("emp-empty-text");
            emptyRow.Add(emptyLabel);
            _marketTableBody.Add(emptyRow);
        }
    }

    // ── HIRING ──

    void OnHireClicked(EmployeeData candidate)
    {
        _selectedCandidate = candidate;

        string posLabel = PositionLabels.TryGetValue(candidate.position, out var lbl) ? lbl : candidate.position;
        string name = $"{candidate.first_name} {candidate.last_name}";
        string salaryText = FormatSalary(candidate.salary);
        string yearPlural = candidate.contract_years != 1 ? "s" : "";
        string yearsText = $"{candidate.contract_years} año{yearPlural}";

        var existing = _myStaff.FirstOrDefault(e => e.position == candidate.position);
        if (existing != null)
        {
            long penalty = (long)(existing.salary * existing.contract_years * 0.5f);
            _hireTitle.text = "REEMPLAZAR EMPLEADO";
            _hireText1.text = $"Ya tienes un {posLabel}: {existing.first_name} {existing.last_name}.";
            _hireText2.text = $"¿Deseas reemplazarlo por {name}?";
            _hireText3.text = $"Indemnización: {FormatSalary(penalty)} · Salario: {salaryText} · Duración: {yearsText}";
        }
        else
        {
            _hireTitle.text = "CONTRATAR EMPLEADO";
            _hireText1.text = $"Vas a contratar a {name} como {posLabel}.";
            _hireText2.text = "";
            _hireText3.text = $"Salario: {salaryText} · Duración: {yearsText}";
        }

        var oldStars = _hireOverlay.Q<VisualElement>("HireModalStars");
        if (oldStars != null)
            oldStars.RemoveFromHierarchy();

        var modalStars = new VisualElement();
        modalStars.name = "HireModalStars";
        modalStars.AddToClassList("hire-modal-stars");
        for (int i = 0; i < 5; i++)
        {
            var star = new VisualElement();
            star.AddToClassList(i < candidate.reputation ? "hire-modal-star" : "hire-modal-star--empty");
            if (_starTex != null)
                star.style.backgroundImage = _starBg;
            modalStars.Add(star);
        }
        var hireBox = _hireOverlay.Q<VisualElement>("HireBox");
        var text3 = hireBox?.Q<Label>("HireText3");
        if (text3 != null)
        {
            int idx = text3.parent.IndexOf(text3);
            text3.parent.Insert(idx + 1, modalStars);
        }

        _btnHireConfirm.text = existing != null ? "REEMPLAZAR" : "CONTRATAR";
        _hireOverlay.style.display = DisplayStyle.Flex;
    }

    void OnFireClicked(EmployeeData emp)
    {
        _selectedFireEmployee = emp;
        _isFiring = true;

        string posLabel = PositionLabels.TryGetValue(emp.position, out var lbl) ? lbl : emp.position;
        string name = $"{emp.first_name} {emp.last_name}";
        string salaryText = FormatSalary(emp.salary);
        string yearPlural = emp.contract_years != 1 ? "s" : "";
        string yearsText = $"{emp.contract_years} año{yearPlural}";
        long penalty = (long)(emp.salary * emp.contract_years * 0.5f);

        _hireTitle.text = "DESPEDIR EMPLEADO";
        _hireText1.text = $"¿Estás seguro de que quieres despedir a {name}?";
        _hireText2.text = $"Puesto: {posLabel}";
        _hireText3.text = $"Salario: {salaryText} · Contrato: {yearsText} · Indemnización: {FormatSalary(penalty)}";

        var modalStars = _hireOverlay.Q<VisualElement>("HireModalStars");
        if (modalStars != null)
            modalStars.RemoveFromHierarchy();

        _btnHireConfirm.text = "DESPEDIR";
        _hireOverlay.style.display = DisplayStyle.Flex;
    }

    void CloseHireModal()
    {
        var oldStars = _hireOverlay.Q<VisualElement>("HireModalStars");
        if (oldStars != null)
            oldStars.RemoveFromHierarchy();
        _hireOverlay.style.display = DisplayStyle.None;
        _selectedCandidate = null;
        _selectedFireEmployee = null;
        _isFiring = false;
        _btnHireConfirm.text = "CONTRATAR";
    }

    void ConfirmHire()
    {
        if (_isFiring)
        {
            ConfirmFire();
            return;
        }

        if (_selectedCandidate == null) return;

        _hireOverlay.style.display = DisplayStyle.None;

        int currentDay = _season?.current_game_day ?? 0;
        string name = $"{_selectedCandidate.first_name} {_selectedCandidate.last_name}";
        string posLabel = PositionLabels.TryGetValue(_selectedCandidate.position, out var lbl) ? lbl : _selectedCandidate.position;
        string salaryText = FormatSalary(_selectedCandidate.salary);

        var existing = _myStaff.FirstOrDefault(e => e.position == _selectedCandidate.position);
        if (existing != null)
        {
            long penalty = (long)(existing.salary * existing.contract_years * 0.5f);
            _myTeam.budget -= penalty;
            DatabaseManager.Instance.UpdateTeamBudget(_myTeam.id, _myTeam.budget);

            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = _myTeam.id,
                season_id = _season?.id ?? 0,
                record_type = FinanceRecord.TYPE_DISMISSAL,
                game_day = currentDay,
                amount = penalty
            });

            DatabaseManager.Instance.DeleteEmployee(existing.id);
            _myStaff.Remove(existing);
        }

        _selectedCandidate.team_id = _myTeam.id;
        _selectedCandidate.candidate_day = 0;
        DatabaseManager.Instance.UpdateEmployee(_selectedCandidate);
        _myStaff.Add(_selectedCandidate);
        _candidates.Remove(_selectedCandidate);

        int posCount = _candidates.Count(c => c.position == _selectedCandidate.position);
        if (posCount < CANDIDATES_PER_POSITION)
            RefillCandidates(_selectedCandidate.position, currentDay);

        long signingCost = _selectedCandidate.salary;
        _myTeam.budget -= signingCost;
        DatabaseManager.Instance.UpdateTeamBudget(_myTeam.id, _myTeam.budget);

        DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
        {
            team_id = _myTeam.id,
            season_id = _season?.id ?? 0,
            record_type = FinanceRecord.TYPE_SALARIES,
            game_day = currentDay,
            amount = signingCost
        });

        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 0,
            sender_id = 0,
            title = "Nuevo empleado contratado",
            body = $"Se ha contratado a {name} como {posLabel} con un salario de {salaryText}.",
            game_day = currentDay,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0
        });

        Debug.Log($"[Employees] {name} contratado como {posLabel} por {salaryText}.");

        _selectedCandidate = null;
        Refresh();
        ShowHireResult(true, name, posLabel);
    }

    void ConfirmFire()
    {
        if (_selectedFireEmployee == null) return;

        _hireOverlay.style.display = DisplayStyle.None;

        int currentDay = _season?.current_game_day ?? 0;
        string name = $"{_selectedFireEmployee.first_name} {_selectedFireEmployee.last_name}";
        string posLabel = PositionLabels.TryGetValue(_selectedFireEmployee.position, out var lbl) ? lbl : _selectedFireEmployee.position;
        long penalty = (long)(_selectedFireEmployee.salary * _selectedFireEmployee.contract_years * 0.5f);

        _myTeam.budget -= penalty;
        DatabaseManager.Instance.UpdateTeamBudget(_myTeam.id, _myTeam.budget);

        DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
        {
            team_id = _myTeam.id,
            season_id = _season?.id ?? 0,
            record_type = FinanceRecord.TYPE_DISMISSAL,
            game_day = currentDay,
            amount = penalty
        });

        DatabaseManager.Instance.DeleteEmployee(_selectedFireEmployee.id);
        _myStaff.Remove(_selectedFireEmployee);

        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 0,
            sender_id = 0,
            title = "Empleado despedido",
            body = $"Se ha despedido a {name} ({posLabel}). Indemnización pagada: {FormatSalary(penalty)}.",
            game_day = currentDay,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0
        });

        Debug.Log($"[Employees] {name} despedido. Indemnización: {FormatSalary(penalty)}.");

        _isFiring = false;
        _btnHireConfirm.text = "CONTRATAR";
        _selectedFireEmployee = null;

        Refresh();

        _hireResultTitle.text = "EMPLEADO DESPEDIDO";
        _hireResultText.text = $"{name} ya no trabaja para tu equipo. Indemnización pagada: {FormatSalary(penalty)}.";
        _hireResultOverlay.style.display = DisplayStyle.Flex;
    }

    void RefillCandidates(string position, int currentDay)
    {
        int toAdd = CANDIDATES_PER_POSITION - _candidates.Count(c => c.position == position);
        for (int i = 0; i < toAdd; i++)
        {
            int rep = Random.Range(1, 6);
            var emp = new EmployeeData
            {
                team_id = 0,
                position = position,
                first_name = GenerateFirstName(),
                last_name = GenerateLastName(),
                reputation = rep,
                salary = GenerateSalary(position, rep),
                contract_years = Random.Range(1, 4),
                candidate_day = currentDay,
                nationality = Nationalities[Random.Range(0, Nationalities.Length)].code
            };
            DatabaseManager.Instance.InsertEmployee(emp);
            _candidates.Add(emp);
        }
    }

    string FormatSalary(long salary)
    {
        return salary.ToString("N0").Replace(',', '.') + " $";
    }

    void ShowHireResult(bool success, string name, string positionLabel)
    {
        if (success)
        {
            _hireResultTitle.text = "CONTRATACIÓN COMPLETADA";
            _hireResultText.text = $"{name} se ha unido a tu equipo como {positionLabel}.";
        }
        else
        {
            _hireResultTitle.text = "CONTRATACIÓN FALLIDA";
            _hireResultText.text = "No se ha podido completar la contratación.";
        }

        _hireResultOverlay.style.display = DisplayStyle.Flex;
    }

    void CloseHireResultModal()
    {
        _hireResultOverlay.style.display = DisplayStyle.None;
    }
}
