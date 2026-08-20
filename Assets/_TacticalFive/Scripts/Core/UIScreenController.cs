using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class UIScreenController : MonoBehaviour
{
    protected UIDocument _doc;
    protected VisualElement _root;
    protected Button _btnAction;

    protected ManagerData _manager;
    protected TeamData _myTeam;
    protected SeasonData _season;

    protected virtual GameScreen ScreenId => GameScreen.Dashboard;

    protected virtual GameScreen BtnActionTarget => GameScreen.Dashboard;

    protected virtual void OnBtnActionClicked()
    {
        ScreenManager.Instance.GoTo(BtnActionTarget);
    }

    // Config modal
    protected VisualElement _configModalOverlay;
    protected VisualElement _configModalBox;
    protected Button _btnConfigCerrar;
    protected CustomSlider _configSliderMaster;
    protected CustomSlider _configSliderMusic;
    protected CustomSlider _configSliderSFX;
    protected Label _configLabelMaster;
    protected Label _configLabelMusic;
    protected Label _configLabelSFX;
    protected Button _configBtnQualityLow;
    protected Button _configBtnQualityMedium;
    protected Button _configBtnQualityHigh;
    protected Button _configBtnQualityUltra;
    protected Button _configBtnSimDirect;
    protected Button _configBtnSimPlay;

    // Config confirm modals
    protected VisualElement _configMainMenuConfirmOverlay;
    protected Button _configBtnMainMenu;
    protected Button _configBtnMainMenuYes;
    protected Button _configBtnMainMenuNo;
    protected VisualElement _configExitConfirmOverlay;
    protected Button _configBtnExit;
    protected Button _configBtnExitYes;
    protected Button _configBtnExitNo;

    protected virtual void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        MakeFullscreen();

        CacheReferences();
        LoadSidebarIcons();
        LoadData();
        RegisterCallbacks();
        InitConfigModal();
        Refresh();
    }

    protected virtual void MakeFullscreen()
    {
        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
    }

    protected virtual void CacheReferences() { }

    protected virtual void LoadSidebarIcons()
    {
        var iconMap = new System.Collections.Generic.Dictionary<string, string>
        {
            {"NavDashboardIcon", "inicio"},
            {"NavRosterIcon", "plantilla"},
            {"NavCalendarIcon", "calendario"},
            {"NavCompeticionesIcon", "competiciones"},
            {"NavPalmaresIcon", "palmares"},
            {"NavPlayoffsIcon", "playoff"},
            {"NavStatsIcon", "estadisticas"},
            {"NavMarketIcon", "mercado"},
            {"NavFinancesIcon", "finanzas"},
            {"NavCityIcon", "ciudad"},
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

    protected virtual void LoadData()
    {
        if (DatabaseManager.Instance?.Db == null) return;
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
    }

    protected virtual void RegisterCallbacks()
    {
        // Sidebar unificado
        SidebarController.Attach(_root, ScreenId);
        HeaderController.Attach(_root, registerBtnAction: false);
        _btnAction = _root.Q<Button>("BtnAction");
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OnBtnActionClicked(); });
        RegisterHandCursors();

        var configIcon = _root.Q<VisualElement>("ConfigIcon");
        if (configIcon != null)
        {
            configIcon.RegisterCallback<ClickEvent>(_ =>
                { PlayClick(); OpenConfigModal(); });
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(configIcon);
        }
    }

    protected virtual void RegisterNavButtons()
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
        _root.Q<Button>("SubmenuDorsales")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Dorsales); });

        _root.Q<Button>("SubmenuEntrenamiento")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Training); });
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Calendar); });
        _root.Q<Button>("NavCompeticiones")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseAllSubmenus();
            var submenu = _root.Q<VisualElement>("CompetitionsSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });
        _root.Q<Button>("SubmenuResultados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("CompetitionsSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Results); });
        _root.Q<Button>("SubmenuClasificacion")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("CompetitionsSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Standings); });
        _root.Q<Button>("SubmenuInfoLiga")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("CompetitionsSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.InfoLeague); });
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
        _root.Q<Button>("SubmenuPremios")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Premios); });
        _root.Q<Button>("SubmenuLogros")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("ManagerSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Logros);
        });
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
        _root.Q<Button>("NavCity")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseAllSubmenus();
            var submenu = _root.Q<VisualElement>("CitySubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });
        _root.Q<Button>("SubmenuVerCiudad")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("CitySubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.TheCity); });
        _root.Q<Button>("SubmenuPabellon")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("CitySubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Arena); });
        _root.Q<Button>("NavManager")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            CloseAllSubmenus();
            var submenu = _root.Q<VisualElement>("ManagerSubmenu");
            if (submenu != null)
                submenu.EnableInClassList("nav-submenu--visible", !submenu.ClassListContains("nav-submenu--visible"));
        });
        _root.Q<Button>("SubmenuManager")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("ManagerSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Manager);
        });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
    }

    protected virtual void Refresh()
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.SetDefaultCursor();
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[{GetType().Name}] RefreshHeader error: {ex.Message}"); }
    }

    protected void RegisterHandCursors()
    {
        if (CursorManager.Instance == null) return;
        _root.Query<Button>().ForEach(btn => CursorManager.Instance.RegisterHandCursor(btn));
    }

    protected void CloseAllSubmenus()
    {
        _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible");
        _root.Q<VisualElement>("CompetitionsSubmenu")?.RemoveFromClassList("nav-submenu--visible");
        _root.Q<VisualElement>("PalmaresSubmenu")?.RemoveFromClassList("nav-submenu--visible");
        _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible");
        _root.Q<VisualElement>("FinanceSubmenu")?.RemoveFromClassList("nav-submenu--visible");
        _root.Q<VisualElement>("CitySubmenu")?.RemoveFromClassList("nav-submenu--visible");
        _root.Q<VisualElement>("ManagerSubmenu")?.RemoveFromClassList("nav-submenu--visible");
    }

    protected virtual void RefreshHeader()
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

        if (_btnAction != null)
            _btnAction.text = "MENÚ PRINCIPAL";
    }

    protected void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }

    // ── Config modal ────────────────────────────────────────
    protected virtual void InitConfigModal()
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
            if (_configSliderMaster?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderMaster.Container);
            if (_configSliderMusic?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderMusic.Container);
            if (_configSliderSFX?.Container != null)
                CursorManager.Instance.RegisterHandCursor(_configSliderSFX.Container);
        }

        InitConfigSimToggle();
    }

    // ── Config modal: modo de simulación ────────────────────
    public const string SimModePrefKey = "TF_SimMode";

    public static int GetSimMode()
    {
        return PlayerPrefs.GetInt(SimModePrefKey, 0);
    }

    protected void InitConfigSimToggle()
    {
        _configBtnSimDirect = _root.Q<Button>("ConfigBtnSimDirect");
        _configBtnSimPlay   = _root.Q<Button>("ConfigBtnSimPlay");

        _configBtnSimDirect?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigSimMode(0); });
        _configBtnSimPlay?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectConfigSimMode(1); });
    }

    protected void UpdateConfigSimToggle()
    {
        UpdateConfigSimButtons(GetSimMode());
    }

    protected void SelectConfigSimMode(int mode)
    {
        PlayerPrefs.SetInt(SimModePrefKey, mode);
        PlayerPrefs.Save();
        UpdateConfigSimButtons(mode);
    }

    void UpdateConfigSimButtons(int activeIndex)
    {
        if (_configBtnSimDirect != null)
            _configBtnSimDirect.EnableInClassList("settings-quality-btn--active", activeIndex == 0);
        if (_configBtnSimPlay != null)
            _configBtnSimPlay.EnableInClassList("settings-quality-btn--active", activeIndex == 1);
    }

    protected virtual void OpenConfigModal()
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
        UpdateConfigSimToggle();

        _configModalOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configModalOverlay.AddToClassList("modal-overlay--visible");
        _configModalBox.AddToClassList("modal-box--visible");
    }

    protected virtual void CloseConfigModal()
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
}
