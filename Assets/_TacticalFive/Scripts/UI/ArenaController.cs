using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class ArenaController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private Button _btnAction;
    private Label _arenaNameHeader;
    private VisualElement _arenaInfoBody;
    private VisualElement _arenaStars;
    private VisualElement _arenaStatus;
    private VisualElement _arenaImage;
    private VisualElement _renovationWarning;
    private Label _renovationWarningText;
    private VisualElement _upgradeCardsContainer;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;

    // Renovation config (same as Django)
    readonly Dictionary<string, (string name, string desc, string icon, int capacityBonus, long cost, int durationWeeks)> _renovationTypes = new()
    {
        ["general_seats"] = ("Ampliar Grada General", "Aumenta la capacidad de la grada general", "🏟", 3000, 10_000_000, 3),
        ["tribune"] = ("Ampliar Tribuna", "Ampliación de la tribuna principal", "👑", 2000, 20_000_000, 5),
        ["vip_seats"] = ("Ampliar Grada VIP", "Nuevos palcos y zona VIP premium", "💎", 1000, 35_000_000, 8),
    };
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
        _arenaNameHeader = _root.Q<Label>("ArenaNameHeader");
        _arenaInfoBody = _root.Q<VisualElement>("ArenaInfoBody");
        _arenaStars = _root.Q<VisualElement>("ArenaStars");
        _arenaStatus = _root.Q<VisualElement>("ArenaStatus");
        _arenaImage = _root.Q<VisualElement>("ArenaImage");
        _renovationWarning = _root.Q<VisualElement>("RenovationWarning");
        _renovationWarningText = _root.Q<Label>("RenovationWarningText");
        _upgradeCardsContainer = _root.Q<VisualElement>("UpgradeCardsContainer");
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
    }

    void RegisterCallbacks()
    {
        // Sidebar unificado
        SidebarController.Attach(_root, GameScreen.Arena);
        HeaderController.Attach(_root);
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetDefaultCursor();
            RegisterHandCursors();
        }
    }

    void RegisterHandCursors()
    {
        foreach (var btn in _root.Query<Button>(null, "nav-item").Build())
            CursorManager.Instance.RegisterHandCursor(btn);
        foreach (var btn in _root.Query<Button>(null, "nav-submenu-item").Build())
            CursorManager.Instance.RegisterHandCursor(btn);

        var cursorTargets = new[] { "BtnAction", "ConfigIcon",
            "NavDashboard", "NavRoster", "NavCalendar", "NavResults", "NavStandings",
            "NavPalmares", "NavPlayoffs", "NavStats", "NavMarket", "NavFinances",
            "NavArena", "NavMessages" };
        foreach (var name in cursorTargets)
        {
            var el = _root.Q<VisualElement>(name);
            if (el != null)
                CursorManager.Instance.RegisterHandCursor(el);
        }

        // Upgrade card buttons are registered individually in CreateUpgradeCard
    }

    void RegisterNavButtons()
    {
        var allSubmenus = new[]
        {
            _root.Q<VisualElement>("RosterSubmenu"),
            _root.Q<VisualElement>("PalmaresSubmenu"),
            _root.Q<VisualElement>("MarketSubmenu"),
            _root.Q<VisualElement>("FinanceSubmenu"),
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
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OpenConfigModal(); });
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
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Arena] RefreshHeader error: {ex.Message}"); }
        BuildArenaInfo();
        BuildArenaLevel();
        BuildArenaImage();
        BuildUpgrades();
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

    /* ═══════════════════════════════════════════
       PANEL 1: ARENA INFO
       ═══════════════════════════════════════════ */

    void BuildArenaInfo()
    {
        _arenaInfoBody.Clear();
        _arenaNameHeader.text = _myTeam.arena?.ToUpper() ?? "PABELLÓN";

        // City
        AddInfoRow("Ciudad", _myTeam.city);
        // Base capacity
        AddInfoRow("Capacidad base", $"{_myTeam.capacity:N0}");
        // Effective capacity (same as base for now)
        AddInfoRow("Capacidad efectiva", $"{_myTeam.capacity:N0}", true);
    }

    void AddInfoRow(string label, string value, bool green = false)
    {
        var row = new VisualElement();
        row.AddToClassList("arena-info-row");

        var lbl = new Label(label);
        lbl.AddToClassList("arena-info-label");

        var val = new Label(value);
        val.AddToClassList("arena-info-value");
        if (green) val.AddToClassList("arena-info-value--green");

        row.Add(lbl);
        row.Add(val);
        _arenaInfoBody.Add(row);
    }

    void AddTierRow(string label, string tierName, Color tierColor)
    {
        var row = new VisualElement();
        row.AddToClassList("arena-info-row");

        var lbl = new Label(label);
        lbl.AddToClassList("arena-info-label");

        var badge = new Label(tierName);
        badge.AddToClassList("arena-tier-badge");
        badge.style.backgroundColor = new StyleColor(tierColor);
        // Adjust text color for dark tiers
        if (tierName is "Básico" or "Estándar")
            badge.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));

        row.Add(lbl);
        row.Add(badge);
        _arenaInfoBody.Add(row);
    }

    string GetTierName(int level)
    {
        return level switch
        {
            1 => "BÁSICO",
            2 => "ESTÁNDAR",
            3 => "PREMIUM",
            4 => "ÉLITE",
            5 => "LEGENDARIO",
            _ => "DESCONOCIDO"
        };
    }

    Color GetTierColor(int level)
    {
        return level switch
        {
            1 => new Color(0.55f, 0.45f, 0.33f),   // #8B7355
            2 => new Color(0.63f, 0.63f, 0.63f),   // #A0A0A0
            3 => new Color(0.29f, 0.50f, 0.94f),   // #4A80F0
            4 => new Color(0.83f, 0.63f, 0.09f),   // #D4A017
            5 => new Color(1.00f, 0.84f, 0.00f),   // #FFD700
            _ => Color.gray
        };
    }

    /* ═══════════════════════════════════════════
       PANEL 2: ARENA LEVEL / STARS
       ═══════════════════════════════════════════ */

    void BuildArenaLevel()
    {
        _arenaStars.Clear();
        _arenaStatus.Clear();

        // Stars
        for (int i = 1; i <= 5; i++)
        {
            var star = new Label("⭐");
            star.AddToClassList("arena-star");
            if (i <= _myTeam.facilities)
                star.AddToClassList("arena-star--active");
            else
                star.AddToClassList("arena-star--inactive");
            _arenaStars.Add(star);
        }

        // Status
        bool underRenovation = IsUnderRenovation();
        if (underRenovation)
        {
            int daysLeft = Mathf.Max(0, _myTeam.arena_renovation_end_day - GetCurrentDay());
            _arenaStatus.AddToClassList("arena-status--busy");

            var icon = new Label("⏳");
            icon.AddToClassList("arena-status-icon");

            var text = new Label($"Remodelación en curso: {daysLeft} días");
            text.AddToClassList("arena-status-text");
            text.AddToClassList("arena-status-text--busy");

            _arenaStatus.Add(icon);
            _arenaStatus.Add(text);
        }
        else
        {
            _arenaStatus.AddToClassList("arena-status--idle");

            var icon = new Label("✓");
            icon.AddToClassList("arena-status-icon");

            var text = new Label("Sin obras en curso");
            text.AddToClassList("arena-status-text");
            text.AddToClassList("arena-status-text--idle");

            _arenaStatus.Add(icon);
            _arenaStatus.Add(text);
        }

        // Tier badge (aligned right, below stars/status)
        var oldBadge = _arenaStatus.parent.Q<VisualElement>("TierBadgeRow");
        oldBadge?.RemoveFromHierarchy();

        var tierRow = new VisualElement();
        tierRow.name = "TierBadgeRow";
        tierRow.style.flexDirection = FlexDirection.Row;
        tierRow.style.justifyContent = Justify.FlexEnd;
        tierRow.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        tierRow.style.marginTop = 12;

        var badge = new Label(GetTierName(_myTeam.facilities));
        badge.AddToClassList("arena-tier-badge");
        badge.style.backgroundColor = new StyleColor(GetTierColor(_myTeam.facilities));
        if (GetTierName(_myTeam.facilities) is "BÁSICO" or "ESTÁNDAR")
            badge.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));

        tierRow.Add(badge);
        _arenaStatus.parent.Add(tierRow);
    }

    /* ═══════════════════════════════════════════
       PANEL 3: ARENA IMAGE
       ═══════════════════════════════════════════ */

    void BuildArenaImage()
    {
        var arenaSprite = Resources.Load<Sprite>($"Arenas/{_myTeam.logo}");
        if (arenaSprite != null)
            _arenaImage.style.backgroundImage = new StyleBackground(arenaSprite);
        else
        {
            var defaultSprite = Resources.Load<Sprite>("Arenas/default");
            if (defaultSprite != null)
                _arenaImage.style.backgroundImage = new StyleBackground(defaultSprite);
        }
    }

    /* ═══════════════════════════════════════════
       BOTTOM: UPGRADE CARDS
       ═══════════════════════════════════════════ */

    void BuildUpgrades()
    {
        _upgradeCardsContainer.Clear();

        bool underRenovation = IsUnderRenovation();

        // Warning banner
        if (underRenovation)
        {
            _renovationWarning.style.display = DisplayStyle.Flex;
            int daysLeft = Mathf.Max(0, _myTeam.arena_renovation_end_day - GetCurrentDay());
            _renovationWarningText.text = $"Hay una remodelación en curso. Debes esperar a que termine para iniciar otra.";
        }
        else
        {
            _renovationWarning.style.display = DisplayStyle.None;
        }

        // Cards
        foreach (var kvp in _renovationTypes)
        {
            var card = CreateUpgradeCard(kvp.Key, kvp.Value, underRenovation);
            _upgradeCardsContainer.Add(card);
        }
    }

    VisualElement CreateUpgradeCard(string type, (string name, string desc, string icon, int capacityBonus, long cost, int durationWeeks) info, bool underRenovation)
    {
        var card = new VisualElement();
        card.AddToClassList("upgrade-card");

        // Icon
        var icon = new Label(info.icon);
        icon.AddToClassList("upgrade-card-icon");
        card.Add(icon);

        // Name
        var nameLbl = new Label(info.name);
        nameLbl.AddToClassList("upgrade-card-name");
        card.Add(nameLbl);

        // Description
        var descLbl = new Label(info.desc);
        descLbl.AddToClassList("upgrade-card-desc");
        card.Add(descLbl);

        // Bonus
        var bonusLbl = new Label($"+{info.capacityBonus:N0} asientos");
        bonusLbl.AddToClassList("upgrade-card-bonus");
        card.Add(bonusLbl);

        // Footer: cost + duration
        var footer = new VisualElement();
        footer.AddToClassList("upgrade-card-footer");

        var costLbl = new Label($"${info.cost:N0}");
        costLbl.AddToClassList("upgrade-card-cost");

        var durationLbl = new Label($"{info.durationWeeks} semanas");
        durationLbl.AddToClassList("upgrade-card-duration");

        footer.Add(costLbl);
        footer.Add(durationLbl);
        card.Add(footer);

        // Button
        var btn = new Button();
        btn.AddToClassList("upgrade-card-btn");

        if (underRenovation)
        {
            btn.text = "EN OBRAS";
            btn.AddToClassList("upgrade-card-btn--disabled");
            btn.SetEnabled(false);
        }
        else if (_myTeam.budget < info.cost)
        {
            btn.text = "SIN FONDOS";
            btn.AddToClassList("upgrade-card-btn--no-funds");
            btn.SetEnabled(false);
        }
        else
        {
            btn.text = "INICIAR";
            btn.clicked += () => { PlayClick(); StartRenovation(type); };
        }
        card.Add(btn);

        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(btn);

        return card;
    }

    /* ═══════════════════════════════════════════
       ACTIONS
       ═══════════════════════════════════════════ */

    void StartRenovation(string type)
    {
        if (!_renovationTypes.TryGetValue(type, out var info)) return;
        if (IsUnderRenovation()) return;
        if (_myTeam.budget < info.cost) return;

        int currentDay = GetCurrentDay();
        int endDay = currentDay + (info.durationWeeks * 7);

        _myTeam.arena_renovation_end_day = endDay;
        _myTeam.arena_renovation_type = type;
        _myTeam.arena_renovation_cost = info.cost;
        DatabaseManager.Instance.UpdateTeam(_myTeam);

        // Message
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            title = $"Remodelación iniciada: {info.name}",
            body = $"Se ha iniciado la remodelación \"{info.name}\". Duración: {info.durationWeeks} semanas. Finalizará el día {endDay}.",
            game_day = currentDay,
            game_date = System.DateTime.Parse(_season.year_start + "-10-22").AddDays(currentDay - 1).ToString("yyyy-MM-dd"),
            is_read = 0
        });

        LoadData();
        Refresh();
    }

    bool IsUnderRenovation()
    {
        return _myTeam != null && _myTeam.arena_renovation_end_day > 0 && GetCurrentDay() < _myTeam.arena_renovation_end_day;
    }

    int GetCurrentDay()
    {
        if (_season == null) return 0;
        return _season.current_game_day;
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
