using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class EmployeesController : MonoBehaviour
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

    // Staff grid
    private VisualElement _myStaffBody;
    private VisualElement _marketBody;

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
    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<EmployeeData> _myStaff;
    private List<EmployeeData> _candidates;
    private EmployeeData _selectedCandidate;
    private EmployeeData _selectedFireEmployee;
    private bool _isFiring;

    private Dictionary<string, Sprite> _logoSprites = new();
    private Texture2D _starTex;
    private StyleBackground _starBg;
    private StyleBackground _empleadoBg;

    private static readonly Dictionary<string, string> PositionLabels = new()
    {
        { "ASISTENTE", "ASISTENTE" },
        { "MEDICO", "SERVICIO M\u00c9DICO" },
        { "FINANCIERO", "FINANCIERO" },
        { "OJEADOR", "OJEADOR" },
        { "PSICOLOGO", "PSIC\u00d3LOGO" },
        { "PABELLON", "ENCARGADO DEL PABELL\u00d3N" }
    };

    private static readonly string[] PositionOrder = { "ASISTENTE", "MEDICO", "FINANCIERO", "OJEADOR", "PSICOLOGO", "PABELLON" };

    private static readonly int REFRESH_INTERVAL = 30;

    private static readonly string[] FirstNames = {
        // Ingleses (90)
        "James", "John", "William", "George", "Thomas", "Charles", "Henry", "Edward", "Harry", "Arthur",
        "Jack", "Oliver", "Noah", "Jacob", "Leo", "Oscar", "Alfie", "Freddie", "Theo", "Archie",
        "Charlie", "Finley", "Harrison", "Ethan", "Joseph", "Samuel", "Daniel", "Matthew", "Luke", "Adam",
        "Benjamin", "Michael", "Nathan", "Ryan", "Connor", "Louis", "Alexander", "David", "Robert", "Richard",
        "Peter", "Andrew", "Jonathan", "Simon", "Paul", "Mark", "Stephen", "Alan", "Martin", "Graham",
        "Scott", "Dean", "Craig", "Bradley", "Lewis", "Callum", "Jamie", "Kyle", "Reece", "Sean",
        "Dylan", "Logan", "Mason", "Aiden", "Tyler", "Cameron", "Jordan", "Zachary", "Aaron", "Elliot",
        "Blake", "Cole", "Dominic", "Evan", "Isaac", "Max", "Patrick", "Philip", "Ross", "Spencer",
        "Tristan", "Wesley", "Xavier", "Owen", "Caleb", "Nathaniel", "Vincent", "Harvey", "Riley", "Toby",

        // Otros países (10)
        "Luca", "Matteo", "Giovanni", "Carlos", "Miguel",
        "Alejandro", "Pierre", "Jean", "Hans", "Erik"
    };

    private static readonly string[] LastNames = {
        // Ingleses (90)
        "Smith", "Jones", "Taylor", "Brown", "Williams", "Wilson", "Johnson", "Davies", "Robinson", "Wright",
        "Thompson", "Evans", "Walker", "White", "Roberts", "Green", "Hall", "Wood", "Jackson", "Clarke",
        "Turner", "Harris", "Edwards", "Martin", "Cooper", "Hill", "Ward", "Morris", "Moore", "Clark",
        "Lee", "King", "Baker", "Harrison", "Morgan", "Allen", "Scott", "Phillips", "Watson", "Parker",
        "Price", "Bennett", "Young", "Griffiths", "Mitchell", "Carter", "Cook", "Bailey", "Richardson", "Cox",
        "Howard", "Wardle", "Brooks", "Bell", "Murphy", "Miller", "Collins", "Gray", "Hughes", "Marshall",
        "Shaw", "Webb", "Foster", "Butler", "Chapman", "Pearson", "Armstrong", "Reynolds", "Stephens", "Payne",
        "Gardner", "Spencer", "Hunter", "Fox", "Gibson", "Harvey", "Palmer", "Warren", "Knight", "Mason",
        "Ellis", "Bishop", "Porter", "George", "West", "Grant", "Black", "Fisher", "Holmes", "Stone",

        // Otros países (10)
        "Garcia", "Martinez", "Rossi", "Bianchi", "Dubois",
        "Moreau", "Muller", "Schmidt", "Andersen", "Johansson"
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
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _btnAction = _root.Q<Button>("BtnAction");

        _myStaffBody = _root.Q<VisualElement>("MyStaffBody");
        _marketBody = _root.Q<VisualElement>("MarketBody");

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
                for (int i = 0; i < 3; i++)
                {
                    int rep = Random.Range(1, 6);
                    var emp = new EmployeeData
                    {
                        team_id = 0,
                        position = pos,
                        first_name = GenerateFirstName(pos),
                        last_name = GenerateLastName(),
                        reputation = rep,
                        salary = GenerateSalary(pos, rep),
                        contract_years = Random.Range(1, 4),
                        candidate_day = currentDay
                    };
                    DatabaseManager.Instance.InsertEmployee(emp);
                    _candidates.Add(emp);
                }
            }
        }
    }

    string GenerateFirstName(string position)
    {
        return FirstNames[Random.Range(0, FirstNames.Length)];
    }

    string GenerateLastName()
    {
        return LastNames[Random.Range(0, LastNames.Length)];
    }

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
        _root.Q<Button>("SubmenuCartera")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); });
        _root.Q<Button>("SubmenuHistorial")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible"); });
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
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Sponsors); });
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.TV); });
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Settings); });

        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

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

        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(_btnAction);
    }

    void Refresh()
    {
        RefreshHeader();
        BuildStaff();
        BuildMarket();
        _root.Q<Button>("SubmenuEmpleados")?.AddToClassList("nav-submenu-item--active");

        _hireOverlay.style.display = DisplayStyle.None;
        _hireResultOverlay.style.display = DisplayStyle.None;
    }

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

        var teamPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long playerPayroll = teamPlayers.Sum(p => p.salary);
        long totalPayroll = playerPayroll + _myStaff.Sum(e => e.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";
        _headerPayroll.RemoveFromClassList("header-stat-value--negative");
        if (totalPayroll > 0)
            _headerPayroll.AddToClassList("header-stat-value--negative");

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? 155_000_000;
        long margin = salaryCap - playerPayroll;

        _headerMargin.text = margin >= 0
            ? $"+${margin / 1_000_000}M"
            : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";

        _headerMargin.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) _headerMargin.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "DASHBOARD";
    }

    void BuildStaff()
    {
        _myStaffBody.Clear();

        if (_myStaff.Count == 0)
        {
            var emptyLbl = new Label();
            emptyLbl.text = "No tienes personal contratado.";
            emptyLbl.style.color = new StyleColor(new Color32(122, 138, 170, 255));
            emptyLbl.style.fontSize = 16;
            emptyLbl.style.marginTop = 10;
            _myStaffBody.Add(emptyLbl);
            return;
        }

        var posIndex = PositionOrder.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i);
        foreach (var emp in _myStaff.OrderBy(e => posIndex.TryGetValue(e.position, out var idx) ? idx : 999))
        {
            var card = new VisualElement();
            card.AddToClassList("staff-card");

            var icon = new VisualElement();
            icon.AddToClassList("staff-card-icon");
            icon.style.backgroundImage = _empleadoBg;
            card.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("staff-card-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("staff-card-name");
            nameLbl.text = $"{emp.first_name} {emp.last_name}".ToUpper();
            info.Add(nameLbl);

            var posLbl = new Label();
            posLbl.AddToClassList("staff-card-position");
            posLbl.text = PositionLabels.TryGetValue(emp.position, out var lbl) ? lbl : emp.position;
            info.Add(posLbl);

            // Reputation stars
            var starsRow = new VisualElement();
            starsRow.AddToClassList("staff-card-stars");
            for (int i = 0; i < 5; i++)
            {
                var star = new Label();
                star.AddToClassList(i < emp.reputation ? "staff-card-star" : "staff-card-star--empty");
                star.text = "\u2605";
                starsRow.Add(star);
            }
            info.Add(starsRow);

            var salaryLbl = new Label();
            salaryLbl.AddToClassList("staff-card-salary");
            salaryLbl.text = FormatSalary(emp.salary);
            info.Add(salaryLbl);

            card.Add(info);

            var fireBtn = new Button();
            fireBtn.AddToClassList("btn-fire");
            fireBtn.text = "DESPEDIR";
            var captured = emp;
            fireBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnFireClicked(captured); });
            if (CursorManager.Instance != null)
            {
                fireBtn.RegisterCallback<MouseEnterEvent>(_ =>
                    CursorManager.Instance.SetHandCursor());
                fireBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                    CursorManager.Instance.SetDefaultCursor());
            }
            card.Add(fireBtn);
            _myStaffBody.Add(card);
        }
    }

    void BuildMarket()
    {
        _marketBody.Clear();

        var columns = new VisualElement();
        columns.AddToClassList("market-columns");

        var leftCol = new VisualElement();
        leftCol.AddToClassList("market-column");
        var rightCol = new VisualElement();
        rightCol.AddToClassList("market-column");

        int half = PositionOrder.Length / 2;
        for (int i = 0; i < PositionOrder.Length; i++)
        {
            var pos = PositionOrder[i];
            var posCandidates = _candidates
                .Where(c => c.position == pos)
                .OrderByDescending(c => c.reputation)
                .ToList();

            if (posCandidates.Count == 0) continue;

            var group = new VisualElement();
            group.AddToClassList("market-pos-group");

            var header = new Label();
            header.AddToClassList("market-pos-header");
            header.text = PositionLabels.TryGetValue(pos, out var lbl) ? lbl : pos;
            group.Add(header);

            var cardsRow = new VisualElement();
            cardsRow.AddToClassList("market-candidates-row");
            foreach (var candidate in posCandidates)
            {
                var card = BuildCandidateCard(candidate);
                cardsRow.Add(card);
            }
            group.Add(cardsRow);

            if (i < half)
                leftCol.Add(group);
            else
                rightCol.Add(group);
        }

        columns.Add(leftCol);
        columns.Add(rightCol);
        _marketBody.Add(columns);
    }

    VisualElement BuildCandidateCard(EmployeeData candidate)
    {
        var card = new VisualElement();
        card.AddToClassList("market-candidate");

        var nameLbl = new Label();
        nameLbl.AddToClassList("market-candidate-name");
        nameLbl.text = $"{candidate.first_name} {candidate.last_name}".ToUpper();
        card.Add(nameLbl);

        var metaLbl = new Label();
        metaLbl.AddToClassList("market-candidate-meta");
        string plural = candidate.contract_years != 1 ? "s" : "";
        metaLbl.text = $"{candidate.contract_years} a\u00f1o{plural} \u00b7 {FormatSalary(candidate.salary)}".ToUpper();
        card.Add(metaLbl);

        var starsRow = new VisualElement();
        starsRow.AddToClassList("market-candidate-stars");
        for (int i = 0; i < 5; i++)
        {
            var star = new VisualElement();
            star.AddToClassList(i < candidate.reputation ? "market-candidate-star" : "market-candidate-star--empty");
            if (_starTex != null)
                star.style.backgroundImage = _starBg;
            starsRow.Add(star);
        }
        card.Add(starsRow);

        var hireBtn = new Button();
        hireBtn.AddToClassList("btn-hire");
        hireBtn.text = "CONTRATAR";
        hireBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnHireClicked(candidate); });
        if (CursorManager.Instance != null)
        {
            hireBtn.RegisterCallback<MouseEnterEvent>(_ =>
                CursorManager.Instance.SetHandCursor());
            hireBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                CursorManager.Instance.SetDefaultCursor());
        }
        card.Add(hireBtn);

        return card;
    }

    // ── HIRING ──

    void OnHireClicked(EmployeeData candidate)
    {
        _selectedCandidate = candidate;

        string posLabel = PositionLabels.TryGetValue(candidate.position, out var lbl) ? lbl : candidate.position;
        string name = $"{candidate.first_name} {candidate.last_name}";
        string salaryText = FormatSalary(candidate.salary);
        string yearPlural = candidate.contract_years != 1 ? "s" : "";
        string yearsText = $"{candidate.contract_years} a\u00f1o{yearPlural}";

        // Check if we already have someone in this position
        var existing = _myStaff.FirstOrDefault(e => e.position == candidate.position);
        if (existing != null)
        {
            _hireTitle.text = "REEMPLAZAR EMPLEADO";
            _hireText1.text = $"Ya tienes un {posLabel}: {existing.first_name} {existing.last_name}.";
            _hireText2.text = $"¿Deseas reemplazarlo por {name}?";
        }
        else
        {
            _hireTitle.text = "CONTRATAR EMPLEADO";
            _hireText1.text = $"Vas a contratar a {name} como {posLabel}.";
            _hireText2.text = "";
        }

        _hireText3.text = $"Salario: {salaryText} \u00b7 Duraci\u00f3n: {yearsText}";

        // Stars row in modal
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
        string yearsText = $"{emp.contract_years} a\u00f1o{yearPlural}";

        _hireTitle.text = "DESPEDIR EMPLEADO";
        _hireText1.text = $"¿Estás seguro de que quieres despedir a {name}?";
        _hireText2.text = $"Puesto: {posLabel}";
        _hireText3.text = $"Salario: {salaryText} \u00b7 Contrato: {yearsText}";

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

        // Remove existing employee in same position (fire them)
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

        // Assign candidate to team
        _selectedCandidate.team_id = _myTeam.id;
        _selectedCandidate.candidate_day = 0;
        DatabaseManager.Instance.UpdateEmployee(_selectedCandidate);
        _myStaff.Add(_selectedCandidate);
        _candidates.Remove(_selectedCandidate);

        int posCount = _candidates.Count(c => c.position == _selectedCandidate.position);
        if (posCount < 3)
            RefillCandidates(_selectedCandidate.position, currentDay);

        // Signing bonus
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

        // Refresh
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

        DatabaseManager.Instance.DeleteEmployee(_selectedFireEmployee.id);
        _myStaff.Remove(_selectedFireEmployee);

        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 0,
            sender_id = 0,
            title = "Empleado despedido",
            body = $"Se ha despedido a {name} ({posLabel}).",
            game_day = currentDay,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0
        });

        Debug.Log($"[Employees] {name} despedido.");

        _isFiring = false;
        _btnHireConfirm.text = "CONTRATAR";
        _selectedFireEmployee = null;

        Refresh();

        _hireResultTitle.text = "EMPLEADO DESPEDIDO";
        _hireResultText.text = $"{name} ya no trabaja para tu equipo.";
        _hireResultOverlay.style.display = DisplayStyle.Flex;
    }

    void RefillCandidates(string position, int currentDay)
    {
        int toAdd = 3 - _candidates.Count(c => c.position == position);
        for (int i = 0; i < toAdd; i++)
        {
            int rep = Random.Range(1, 6);
            var emp = new EmployeeData
            {
                team_id = 0,
                position = position,
                first_name = GenerateFirstName(position),
                last_name = GenerateLastName(),
                reputation = rep,
                salary = GenerateSalary(position, rep),
                contract_years = Random.Range(1, 4),
                candidate_day = currentDay
            };
            DatabaseManager.Instance.InsertEmployee(emp);
            _candidates.Add(emp);
        }
    }

    string FormatSalary(long salary)
    {
        return "$" + salary.ToString("N0").Replace(',', '.');
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

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
