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
            { PlayClick(); OpenConfigModal(); });
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
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Sponsors] RefreshHeader error: {ex.Message}"); }
        BuildCurrentSponsorBanner();
        BuildCards();
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

        _btnAction.text = "MENÚ PRINCIPAL";
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
