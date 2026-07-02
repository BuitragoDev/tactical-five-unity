using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class SponsorsController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private VisualElement _currentSponsorBanner;
    private Label _currentSponsorName;
    private VisualElement _cardsContainer;
    private Label _infoMessage;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private SponsorData _currentSponsor;
    private List<SponsorData> _availableSponsors;

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
        _currentSponsorBanner = _root.Q<VisualElement>("CurrentSponsorBanner");
        _currentSponsorName = _root.Q<Label>("CurrentSponsorName");
        _cardsContainer = _root.Q<VisualElement>("SponsorsCardsContainer");
        _infoMessage = _root.Q<Label>("SponsorsInfoMessage");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _currentSponsor = DatabaseManager.Instance.GetActiveSponsor(_myTeam.id);
        _availableSponsors = DatabaseManager.Instance.GetAvailableSponsors(_myTeam.id);
    }

    void RegisterCallbacks()
    {
        // Sidebar unificado
        SidebarController.Attach(_root, GameScreen.Sponsors);
        HeaderController.Attach(_root);
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
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
        BuildCurrentSponsorBanner();
        BuildCards();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;
        if (_headerTeamName == null) return;

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

    // Sponsors can only be signed in September (preseason) or October (days 1-10)
    bool IsOctober()
    {
        if (_season == null) return false;
        int day = _season.current_game_day;
        return day <= 10;
    }

    void BuildCurrentSponsorBanner()
    {
        if (_currentSponsor != null)
        {
            _currentSponsorBanner.style.display = DisplayStyle.Flex;
            _currentSponsorName.text = _currentSponsor.name;
        }
        else
        {
            _currentSponsorBanner.style.display = DisplayStyle.None;
        }
    }

    void BuildCards()
    {
        _cardsContainer.Clear();

        if (_availableSponsors == null || _availableSponsors.Count == 0)
        {
            var emptyLbl = new Label("No hay patrocinadores disponibles.");
            emptyLbl.AddToClassList("sponsors-info-message");
            _cardsContainer.Add(emptyLbl);
            return;
        }

        bool hasCurrent = _currentSponsor != null;

        foreach (var sponsor in _availableSponsors)
        {
            var card = CreateCard(sponsor, hasCurrent);
            _cardsContainer.Add(card);
        }
    }

    VisualElement CreateCard(SponsorData sponsor, bool hasCurrent)
    {
        var card = new VisualElement();
        card.AddToClassList("sponsor-card");

        // Logo
        var logo = new VisualElement();
        logo.AddToClassList("sponsor-card-logo");
        // Load sponsor logo from Resources (strip .png extension for Resources.Load)
        var logoPath = sponsor.logo?.Replace(".png", "");
        var sponsorLogo = Resources.Load<Sprite>(logoPath);
        if (sponsorLogo != null)
            logo.style.backgroundImage = new StyleBackground(sponsorLogo);

        // If we have a current sponsor and this is not it, show in grayscale
        if (hasCurrent && _currentSponsor != null && sponsor.id != _currentSponsor.id)
            logo.AddToClassList("sponsor-card-logo--grayscale");

        card.Add(logo);

        // Name
        var nameLbl = new Label(sponsor.name.ToUpper());
        nameLbl.AddToClassList("sponsor-card-name");
        card.Add(nameLbl);

        // Ingreso Inicial
        card.Add(CreateCardRow("Ingreso Inicial", $"${sponsor.initial_income:N0}"));

        // Por Partido en Casa
        card.Add(CreateCardRow("Por Partido en Casa", $"${sponsor.home_game_income:N0}"));

        // Duración
        card.Add(CreateCardRow("Duración", $"{sponsor.contract_years} año{(sponsor.contract_years > 1 ? "s" : "")}"));

        // Button
        var btn = new Button();
        btn.AddToClassList("sponsor-card-btn");
        bool isContracted = hasCurrent && _currentSponsor != null && _currentSponsor.id == sponsor.id;

        if (isContracted)
        {
            btn.text = "CONTRATADO";
            btn.AddToClassList("sponsor-card-btn--disabled");
            btn.SetEnabled(false);
        }
        else if (hasCurrent)
        {
            btn.text = "CONTRATADO";
            btn.AddToClassList("sponsor-card-btn--disabled");
            btn.SetEnabled(false);
        }
        else if (!IsOctober())
        {
            btn.text = "SOLO OCTUBRE";
            btn.AddToClassList("sponsor-card-btn--disabled");
            btn.SetEnabled(false);
        }
        else
        {
            btn.text = "CONTRATAR";
            var sponsorCopy = sponsor;
            btn.clicked += () => { PlayClick(); SignSponsor(sponsorCopy); };
        }
        card.Add(btn);
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(btn);

        return card;
    }

    VisualElement CreateCardRow(string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("sponsor-card-row");

        var lbl = new Label(label);
        lbl.AddToClassList("sponsor-card-label");

        var val = new Label(value);
        val.AddToClassList("sponsor-card-value");

        row.Add(lbl);
        row.Add(val);

        return row;
    }

    void SignSponsor(SponsorData sponsor)
    {
        if (_currentSponsor != null) return; // Can't sign if already have one
        if (!IsOctober()) return; // Sponsors can only be signed in October

        DatabaseManager.Instance.SignSponsor(sponsor.id, _season.id, _myTeam.id, _season.current_game_day);

        // Send message
        var msg = new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"PATROCINADOR FIRMADO: {sponsor.name.ToUpper()}",
            body = $"Se ha firmado un nuevo patrocinio con {sponsor.name}.\n\nIngreso inicial: ${sponsor.initial_income:N0}",
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        };
        DatabaseManager.Instance.AddMessage(msg);

        LoadData();
        Refresh();
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
