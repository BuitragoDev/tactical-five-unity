using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class InjuredController : MonoBehaviour
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

    // Medical staff
    private VisualElement _medStaffBody;
    private VisualElement _medStaffCard;
    private VisualElement _medStaffEmpty;

    // Injured table
    private VisualElement _injuredTable;

    // League injured modal
    private VisualElement _leagueInjuredOverlay;
    private ScrollView _leagueInjuredScroll;
    private Button _btnLeagueInjured;
    private Button _btnLeagueInjuredClose;

    // Treatment result modal
    private VisualElement _treatmentResultOverlay;
    private Label _treatmentResultTitle;
    private Label _treatmentResultText;
    private Button _btnTreatmentOk;

    // Data
    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<PlayerData> _allPlayers;
    private List<PlayerData> _injuredPlayers;
    private EmployeeData _medico;

    private Dictionary<string, Sprite> _logoSprites = new();
    private Texture2D _starTex;
    private StyleBackground _starBg;
    private StyleBackground _empleadoBg;
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
        InitConfigModal();
        Refresh();
        CursorManager.Instance?.SetDefaultCursor();
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

        _medStaffBody = _root.Q<VisualElement>("MedStaffBody");
        _medStaffCard = _root.Q<VisualElement>("MedStaffCard");
        _medStaffEmpty = _root.Q<VisualElement>("MedStaffEmpty");
        _injuredTable = _root.Q<VisualElement>("InjuredTable");

        _leagueInjuredOverlay = _root.Q<VisualElement>("LeagueInjuredOverlay");
        _leagueInjuredScroll = _root.Q<ScrollView>("LeagueInjuredScroll");
        _btnLeagueInjured = _root.Q<Button>("BtnLeagueInjured");
        _btnLeagueInjuredClose = _root.Q<Button>("BtnLeagueInjuredClose");

        _treatmentResultOverlay = _root.Q<VisualElement>("TreatmentResultOverlay");
        _treatmentResultTitle = _root.Q<Label>("TreatmentResultTitle");
        _treatmentResultText = _root.Q<Label>("TreatmentResultText");
        _btnTreatmentOk = _root.Q<Button>("BtnTreatmentOk");
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
            {"NavManagerIcon", "manager"},
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
        _allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _injuredPlayers = _allPlayers.Where(p => p.injury_days > 0).ToList();

        var staff = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        _medico = staff.FirstOrDefault(e => e.position == "MEDICO");
    }

    void RegisterCallbacks()
    {
        // Sidebar unificado
        SidebarController.Attach(_root, GameScreen.Injured);
        HeaderController.Attach(_root);
        var allSubmenus = new[] {
            _root.Q<VisualElement>("RosterSubmenu"),
            _root.Q<VisualElement>("PalmaresSubmenu"),
            _root.Q<VisualElement>("MarketSubmenu"),
            _root.Q<VisualElement>("FinanceSubmenu")
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
        _root.Q<Button>("NavManager")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Manager); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OpenConfigModal(); });

        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _btnTreatmentOk?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseTreatmentResult(); });
        _btnLeagueInjured?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenLeagueInjuredModal(); });
        _btnLeagueInjuredClose?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseLeagueInjuredModal(); });
        _leagueInjuredOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _leagueInjuredOverlay)
                { PlayClick(); CloseLeagueInjuredModal(); }
        });

        if (CursorManager.Instance == null) return;
        var cursor = CursorManager.Instance;
        cursor.RegisterHandCursor(_root.Q<Button>("NavDashboard"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavRoster"));
        foreach (var btn in _root.Query<Button>(null, "nav-submenu-item").Build())
            cursor.RegisterHandCursor(btn);
        cursor.RegisterHandCursor(_root.Q<Button>("NavCalendar"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavStandings"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavPalmares"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavResults"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavPlayoffs"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavStats"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavMarket"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavFinances"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavArena"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavManager"));
        cursor.RegisterHandCursor(_root.Q<Button>("NavMessages"));
        cursor.RegisterHandCursor(_root.Q<VisualElement>("ConfigIcon"));
        cursor.RegisterHandCursor(_btnAction);
        cursor.RegisterHandCursor(_btnTreatmentOk);
        cursor.RegisterHandCursor(_btnLeagueInjured);
        cursor.RegisterHandCursor(_btnLeagueInjuredClose);
    }

    void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Injured] RefreshHeader error: {ex.Message}"); }
        BuildMedicalStaff();
        BuildInjuredTable();
        _root.Q<VisualElement>("RosterSubmenu")?.AddToClassList("nav-submenu--visible");
        _root.Q<Button>("SubmenuLesionados")?.AddToClassList("nav-submenu-item--active");
        _treatmentResultOverlay.style.display = DisplayStyle.None;
    }

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

        var teamEmployees = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        long totalPayroll = _allPlayers.Sum(p => p.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - _allPlayers.Sum(p => p.salary);

        string marginText = margin >= 0
            ? $"+${margin / 1_000_000}M"
            : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        _headerMargin.text = marginText;
        _headerMargin.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) _headerMargin.AddToClassList("header-stat-value--negative");

        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        var chemLabel = _root.Q<Label>("HeaderChemistry");
        if (chemLabel != null)
        {
            chemLabel.text = $"{chemistry}%";
            chemLabel.RemoveFromClassList("header-stat-value--gold");
            chemLabel.RemoveFromClassList("header-stat-value--negative");
            if (chemistry < 40)
                chemLabel.AddToClassList("header-stat-value--negative");
            else if (chemistry < 70)
                chemLabel.AddToClassList("header-stat-value--gold");
        }

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _btnAction.text = "MENÚ PRINCIPAL";
    }

    void BuildMedicalStaff()
    {
        if (_medico != null)
        {
            _medStaffCard.style.display = DisplayStyle.Flex;
            _medStaffCard.Clear();
            _medStaffCard.style.minHeight = 100;
            _medStaffEmpty.style.display = DisplayStyle.None;

            var icon = new VisualElement();
            icon.AddToClassList("med-staff-icon");
            icon.style.backgroundImage = _empleadoBg;
            _medStaffCard.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("med-staff-info");

            var nameLbl = new Label();
            nameLbl.AddToClassList("med-staff-name");
            nameLbl.text = $"{_medico.first_name} {_medico.last_name}".ToUpper();
            info.Add(nameLbl);

            var starRow = new VisualElement();
            starRow.style.flexDirection = FlexDirection.Row;
            starRow.style.marginTop = 4;
            for (int i = 0; i < 5; i++)
            {
                var star = new VisualElement();
                star.AddToClassList("med-staff-star");
                if (i >= _medico.reputation)
                    star.AddToClassList("med-staff-star--empty");
                if (_starTex != null)
                    star.style.backgroundImage = _starBg;
                starRow.Add(star);
            }
            info.Add(starRow);

            var recoveryText = _medico.reputation switch
            {
                5 => "RECUPERA: 25%-40%",
                4 => "RECUPERA: 20%-32%",
                3 => "RECUPERA: 15%-25%",
                2 => "RECUPERA: 10%-18%",
                _ => "RECUPERA: 5%-12%"
            };
            info.Add(new Label(recoveryText));

            _medStaffCard.Add(info);
        }
        else
        {
            _medStaffCard.style.display = DisplayStyle.None;
            _medStaffEmpty.style.display = DisplayStyle.Flex;
            _medStaffEmpty.Clear();

            var emptyLbl = new Label();
            emptyLbl.AddToClassList("med-staff-empty-text");
            emptyLbl.text = "A\u00fan no se ha contratado ning\u00fan jefe de servicios m\u00e9dicos.";
            _medStaffEmpty.Add(emptyLbl);

            var hireBtn = new Button();
            hireBtn.AddToClassList("btn-hire");
            hireBtn.text = "IR A EMPLEADOS";
            hireBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                ScreenManager.Instance.GoTo(GameScreen.Employees);
            });
            if (CursorManager.Instance != null)
            {
                hireBtn.RegisterCallback<MouseEnterEvent>(_ =>
                    CursorManager.Instance.SetHandCursor());
                hireBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                    CursorManager.Instance.SetDefaultCursor());
            }
            _medStaffEmpty.Add(hireBtn);
        }
    }

    void BuildInjuredTable()
    {
        _injuredTable.Clear();

        // Header row
        var headerRow = new VisualElement();
        headerRow.AddToClassList("injured-header-row");

        var hNum = new Label(); hNum.AddToClassList("injured-col-num"); hNum.text = "#"; headerRow.Add(hNum);
        var hName = new Label(); hName.AddToClassList("injured-col-name"); hName.text = "JUGADOR"; headerRow.Add(hName);
        var hPos = new Label(); hPos.AddToClassList("injured-col-pos"); hPos.text = "POS"; headerRow.Add(hPos);
        var hInjury = new Label(); hInjury.AddToClassList("injured-col-injury"); hInjury.text = "LESI\u00d3N"; headerRow.Add(hInjury);
        var hDays = new Label(); hDays.AddToClassList("injured-col-days"); hDays.text = "D\u00cdAS"; headerRow.Add(hDays);
        var hAct = new Label(); hAct.AddToClassList("injured-col-action"); hAct.text = "TRATAR"; headerRow.Add(hAct);
        _injuredTable.Add(headerRow);

        if (_injuredPlayers.Count == 0)
        {
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("injured-empty");
            emptyLbl.text = "No hay jugadores lesionados.";
            _injuredTable.Add(emptyLbl);
            return;
        }

        bool hasMedico = _medico != null;

        for (int i = 0; i < _injuredPlayers.Count; i++)
        {
            var player = _injuredPlayers[i];
            var row = new VisualElement();
            row.AddToClassList("injured-row");

            var numLbl = new Label();
            numLbl.AddToClassList("injured-row-num");
            numLbl.text = (i + 1).ToString("D2");
            row.Add(numLbl);

            var nameLbl = new Label();
            nameLbl.AddToClassList("injured-row-name");
            nameLbl.text = $"{player.first_name} {player.last_name}".ToUpper();
            row.Add(nameLbl);

            var posLbl = new Label();
            posLbl.AddToClassList("injured-row-pos");
            posLbl.text = PositionCodes.GetShort(player.position);
            row.Add(posLbl);

            var injuryLbl = new Label();
            injuryLbl.AddToClassList("injured-row-injury");
            injuryLbl.text = string.IsNullOrEmpty(player.injury_type) ? "LESI\u00d3N" : player.injury_type;
            row.Add(injuryLbl);

            var daysLbl = new Label();
            daysLbl.AddToClassList("injured-row-days");
            daysLbl.text = $"{player.injury_days} d\u00eda{(player.injury_days != 1 ? "s" : "")}";
            row.Add(daysLbl);

            bool alreadyTreated = player.treated == 1;
            var treatBtn = new Button();
            treatBtn.AddToClassList("btn-treat");
            treatBtn.text = alreadyTreated ? "TRATADO" : "TRATAR";
            if (!hasMedico || alreadyTreated)
            {
                treatBtn.AddToClassList("btn-treat--disabled");
                treatBtn.SetEnabled(false);
            }
            else
            {
                treatBtn.userData = player;
                treatBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnTreat(player); });
                if (CursorManager.Instance != null)
                {
                    treatBtn.RegisterCallback<MouseEnterEvent>(_ =>
                        CursorManager.Instance.SetHandCursor());
                    treatBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                        CursorManager.Instance.SetDefaultCursor());
                }
            }
            row.Add(treatBtn);

            _injuredTable.Add(row);
        }
    }

    // ── TREATMENT ──

    void OnTreat(PlayerData player)
    {
        if (_medico == null) return;

        float pct = _medico.reputation switch
        {
            5 => Random.Range(0.25f, 0.40f),
            4 => Random.Range(0.20f, 0.32f),
            3 => Random.Range(0.15f, 0.25f),
            2 => Random.Range(0.10f, 0.18f),
            _ => Random.Range(0.05f, 0.12f),
        };

        int oldDays = player.injury_days;
        int newDays = Mathf.CeilToInt(player.injury_days * (1f - pct));
        player.injury_days = Mathf.Clamp(newDays, 1, player.injury_days);
        player.treated = 1;
        DatabaseManager.Instance.UpdatePlayer(player);

        string playerName = $"{player.first_name} {player.last_name}";
        string reductionText = $"{playerName} ha recibido tratamiento m\u00e9dico.\nSus d\u00edas de baja se reducen de {oldDays} a {player.injury_days}.";

        ReloadData();
        Refresh();

        _treatmentResultTitle.text = "TRATAMIENTO COMPLETADO";
        _treatmentResultText.text = reductionText;
        _treatmentResultOverlay.style.display = DisplayStyle.Flex;
    }

    void CloseTreatmentResult()
    {
        _treatmentResultOverlay.style.display = DisplayStyle.None;
    }
    // ── LEAGUE INJURED ──

    void OpenLeagueInjuredModal()
    {
        BuildLeagueInjuredList();
        var scrollWrapper = _root.Q<VisualElement>("LeagueInjuredScrollWrapper");
        if (scrollWrapper != null)
        {
            scrollWrapper.style.height = new StyleLength(new Length(320, LengthUnit.Pixel));
            scrollWrapper.style.maxHeight = new StyleLength(new Length(320, LengthUnit.Pixel));
        }
        _leagueInjuredScroll.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
        _leagueInjuredOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _leagueInjuredOverlay.AddToClassList("modal-overlay--visible");
        _leagueInjuredOverlay.Q<VisualElement>(null, "modal-box")?.AddToClassList("modal-box--visible");
    }

    void CloseLeagueInjuredModal()
    {
        _leagueInjuredOverlay.RemoveFromClassList("modal-overlay--visible");
        _leagueInjuredOverlay.Q<VisualElement>(null, "modal-box")?.RemoveFromClassList("modal-box--visible");
    }

    void BuildLeagueInjuredList()
    {
        _leagueInjuredScroll.Clear();

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var myTeamId = _myTeam.id;
        var injuredList = new List<(TeamData team, PlayerData player)>();

        foreach (var team in allTeams)
        {
            if (team.id == myTeamId) continue;
            var players = DatabaseManager.Instance.GetPlayersByTeam(team.id);
            foreach (var p in players)
            {
                if (p.injury_days > 0)
                    injuredList.Add((team, p));
            }
        }

        if (injuredList.Count == 0)
        {
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("league-injured-empty");
            emptyLbl.text = "No hay jugadores lesionados en la liga.";
            _leagueInjuredScroll.Add(emptyLbl);
            return;
        }

        foreach (var (team, player) in injuredList)
        {
            var row = new VisualElement();
            row.AddToClassList("league-injured-row");
            row.style.height = new StyleLength(new Length(32, LengthUnit.Pixel));
            row.style.minHeight = new StyleLength(new Length(32, LengthUnit.Pixel));

            var logoLbl = new VisualElement();
            logoLbl.AddToClassList("league-injured-row-logo");
            if (_logoSprites.TryGetValue(team.logo, out var sprite))
                logoLbl.style.backgroundImage = new StyleBackground(sprite);
            row.Add(logoLbl);

            var teamLbl = new Label();
            teamLbl.AddToClassList("league-injured-row-team");
            teamLbl.text = team.name;
            row.Add(teamLbl);

            var nameLbl = new Label();
            nameLbl.AddToClassList("league-injured-row-name");
            nameLbl.text = $"{player.first_name} {player.last_name}";
            row.Add(nameLbl);

            var injuryLbl = new Label();
            injuryLbl.AddToClassList("league-injured-row-injury");
            injuryLbl.text = string.IsNullOrEmpty(player.injury_type) ? "LESI\u00d3N" : player.injury_type;
            row.Add(injuryLbl);

            var daysLbl = new Label();
            daysLbl.AddToClassList("league-injured-row-days");
            daysLbl.text = player.injury_days.ToString();
            row.Add(daysLbl);

            _leagueInjuredScroll.Add(row);
        }
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

        _configBtnMainMenu?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenMainMenuConfirmModal(); });
        _configBtnExit?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OpenExitConfirmModal(); });

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

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnConfigCerrar);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityLow);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityMedium);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityHigh);
            CursorManager.Instance.RegisterHandCursor(_configBtnQualityUltra);
            CursorManager.Instance.RegisterHandCursor(_configBtnMainMenu);
            CursorManager.Instance.RegisterHandCursor(_configBtnExit);
            CursorManager.Instance.RegisterHandCursor(_configBtnMainMenuYes);
            CursorManager.Instance.RegisterHandCursor(_configBtnMainMenuNo);
            CursorManager.Instance.RegisterHandCursor(_configBtnExitYes);
            CursorManager.Instance.RegisterHandCursor(_configBtnExitNo);

            if (_configSliderMaster?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderMaster.Container);
            if (_configSliderMusic?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderMusic.Container);
            if (_configSliderSFX?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderSFX.Container);
        }
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
        CloseConfigModal();
        PlayClick();
        _configMainMenuConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configMainMenuConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseMainMenuConfirmModal()
    {
        _configMainMenuConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.RemoveFromClassList("modal-box--visible");
        OpenConfigModal();
    }

    void OpenExitConfirmModal()
    {
        CloseConfigModal();
        PlayClick();
        _configExitConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configExitConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseExitConfirmModal()
    {
        _configExitConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.RemoveFromClassList("modal-box--visible");
        OpenConfigModal();
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
