using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class HistorialController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _historialBody;
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerChemistry;
    private Button _btnAction;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TradeData> _trades = new();
    private Dictionary<int, TeamData> _teamCache = new();
    private Dictionary<int, PlayerData> _playerCache = new();
    private Dictionary<int, DraftPickData> _pickCache = new();
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
        _historialBody = _root.Q<VisualElement>("HistorialBody");

        var scrollView = _root.Q<ScrollView>();
        if (scrollView != null)
            scrollView.contentContainer.style.flexGrow = 0;
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerChemistry = _root.Q<Label>("HeaderChemistry");
        _btnAction = _root.Q<Button>("BtnAction");

        var headerSeason = _root.Q<Label>("HeaderSeason");
        if (headerSeason != null) headerSeason.text = "";
        var headerDate = _root.Q<Label>("HeaderDate");
        if (headerDate != null) headerDate.text = "";
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
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;

        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        if (_season == null) return;

        _trades = DatabaseManager.Instance.GetTradesBySeason(_season.id);

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        foreach (var t in allTeams)
            _teamCache[t.id] = t;

        foreach (var tr in _trades)
        {
            if (!_playerCache.ContainsKey(tr.player_id))
            {
                var p = DatabaseManager.Instance.GetPlayerById(tr.player_id);
                if (p != null)
                    _playerCache[tr.player_id] = p;
            }

            if (tr.pick_id > 0 && !_pickCache.ContainsKey(tr.pick_id))
            {
                var pk = DatabaseManager.Instance.GetDraftPickById(tr.pick_id);
                if (pk != null)
                    _pickCache[tr.pick_id] = pk;
            }
        }
    }

    void Refresh()
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.SetDefaultCursor();
        _root.Q<Button>("SubmenuHistorial")?.AddToClassList("nav-submenu-item--active");
        if (_btnAction != null) _btnAction.text = "MENÚ PRINCIPAL";

        if (_myTeam != null)
        {
            if (_headerTeamName != null)
                _headerTeamName.text = _myTeam.name.ToUpper();
            if (_headerManagerName != null)
                _headerManagerName.text = $"Manager: {_manager?.name ?? ""}";
            if (_headerTeamLogo != null)
            {
                var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64/");
                foreach (var s in logos)
                {
                    if (s.name == _myTeam.logo)
                    {
                        _headerTeamLogo.style.backgroundImage = new StyleBackground(s);
                        break;
                    }
                }
            }

            if (_headerBudget != null)
            {
                var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
                long totalPayroll = players.Sum(p => p.salary);

                _headerBudget.text = $"${_myTeam.budget / 1_000_000}M";
                _headerBudget.style.color = _myTeam.budget < 0
                    ? new StyleColor(new Color32(192, 57, 43, 255))
                    : new StyleColor(new Color32(39, 174, 96, 255));
                _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

                var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
                long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
                long margin = salaryCap - totalPayroll;
                string marginText = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
                _headerMargin.text = marginText;

                int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
                _headerChemistry.text = $"{chemistry.ToString()}%";
                _headerChemistry.RemoveFromClassList("header-stat-value--gold");
                _headerChemistry.RemoveFromClassList("header-stat-value--negative");
                if (chemistry < 40)
                    _headerChemistry.AddToClassList("header-stat-value--negative");
                else if (chemistry < 70)
                    _headerChemistry.AddToClassList("header-stat-value--gold");

                _headerMargin.RemoveFromClassList("header-stat-value--negative");
                if (margin < 0) _headerMargin.AddToClassList("header-stat-value--negative");
            }
        }

        if (_season != null)
        {
            var headerSeason = _root.Q<Label>("HeaderSeason");
            if (headerSeason != null) headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            var headerDate = _root.Q<Label>("HeaderDate");
            if (headerDate != null) headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        BuildHistorial();
    }

    void BuildHistorial()
    {
        _historialBody.Clear();

        if (_trades == null || _trades.Count == 0)
        {
            var empty = new VisualElement();
            empty.AddToClassList("historial-empty");
            var lbl = new Label("No hay traspasos esta temporada.");
            lbl.AddToClassList("historial-empty-text");
            empty.Add(lbl);
            _historialBody.Add(empty);
            return;
        }

        var sorted = _trades.OrderByDescending(t => t.game_day).ToList();

        foreach (var trade in sorted)
        {
            var row = BuildTradeRow(trade);
            _historialBody.Add(row);
        }
    }

    VisualElement BuildTradeRow(TradeData trade)
    {
        var row = new VisualElement();
        row.AddToClassList("historial-row");

        _playerCache.TryGetValue(trade.player_id, out var player);

        // Col 1: Date
        var dateLabel = new Label();
        dateLabel.AddToClassList("historial-col");
        dateLabel.AddToClassList("historial-col-first");
        try
        {
            dateLabel.text = System.DateTime.Parse(trade.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            dateLabel.text = trade.game_date ?? "";
        }
        row.Add(dateLabel);

        // Col 2: Player name OR Pick label
        var itemLabel = new Label();
        itemLabel.AddToClassList("historial-col");
        itemLabel.AddToClassList("historial-col-player");
        if (trade.trade_type == "pick_trade" && trade.pick_id > 0
            && _pickCache.TryGetValue(trade.pick_id, out var pick)
            && _teamCache.TryGetValue(pick.original_team_id, out var origTeam))
        {
            itemLabel.text = $"R{pick.round} {origTeam.abbreviation}";
            itemLabel.AddToClassList("historial-pick-label");
        }
        else if (player != null)
        {
            itemLabel.text = $"{player.first_name} {player.last_name}";
        }
        else
        {
            itemLabel.text = $"ID {trade.player_id}";
        }
        row.Add(itemLabel);

        // Col 3: Seller logo
        var sellerLogo = new VisualElement();
        sellerLogo.AddToClassList("historial-col");
        sellerLogo.AddToClassList("historial-col-logo");
        TeamData fromTeam = null;
        if (trade.trade_type != "free_agent")
            _teamCache.TryGetValue(trade.team_id_from, out fromTeam);
        if (fromTeam != null)
        {
            var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32/");
            foreach (var s in logos)
            {
                if (s.name == fromTeam.logo)
                {
                    sellerLogo.style.backgroundImage = new StyleBackground(s);
                    break;
                }
            }
        }
        row.Add(sellerLogo);

        // Col 4: Seller name
        var sellerName = new Label();
        sellerName.AddToClassList("historial-col");
        if (trade.trade_type != "free_agent" && fromTeam != null)
            sellerName.text = fromTeam.name;
        else
            sellerName.text = "";
        row.Add(sellerName);

        // Col 5: Buyer logo
        var buyerLogo = new VisualElement();
        buyerLogo.AddToClassList("historial-col");
        buyerLogo.AddToClassList("historial-col-logo");
        if (_teamCache.TryGetValue(trade.team_id_to, out var toTeam))
        {
            var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32/");
            foreach (var s in logos)
            {
                if (s.name == toTeam.logo)
                {
                    buyerLogo.style.backgroundImage = new StyleBackground(s);
                    break;
                }
            }
        }
        row.Add(buyerLogo);

        // Col 6: Buyer name
        var buyerName = new Label();
        buyerName.AddToClassList("historial-col");
        buyerName.text = toTeam != null ? toTeam.name : $"ID {trade.team_id_to}";
        row.Add(buyerName);

        // Col 7: Position
        var posLabel = new Label();
        posLabel.AddToClassList("historial-col");
        posLabel.AddToClassList("historial-col-center");
        posLabel.AddToClassList("historial-col-bold");
        posLabel.text = PositionCodes.GetShort(player?.position);
        row.Add(posLabel);

        // Col 8: Overall
        var ovrLabel = new Label();
        ovrLabel.AddToClassList("historial-col");
        ovrLabel.AddToClassList("historial-col-center");
        ovrLabel.AddToClassList("historial-col-bold");
        ovrLabel.AddToClassList("historial-col-ovr");
        ovrLabel.text = player?.overall.ToString() ?? "";
        row.Add(ovrLabel);

        return row;
    }

    void RegisterCallbacks()
    {
        // Sidebar unificado
        SidebarController.Attach(_root, GameScreen.Historial);
        HeaderController.Attach(_root);
        RegisterNavButtons();
        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Dashboard); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OpenConfigModal(); });
        var configIcon = _root.Q<VisualElement>("ConfigIcon");
        if (configIcon != null && CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(configIcon);
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
        _root.Q<Button>("SubmenuCartera")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Cartera);
        });
        _root.Q<Button>("SubmenuHistorial")?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _root.Q<VisualElement>("MarketSubmenu")?.RemoveFromClassList("nav-submenu--visible");
            ScreenManager.Instance.GoTo(GameScreen.Historial);
        });
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
        PlayClick();
        _configMainMenuConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configMainMenuConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseMainMenuConfirmModal()
    {
        _configMainMenuConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configMainMenuConfirmOverlay.Q<VisualElement>("ConfigMainMenuConfirmBox")?.RemoveFromClassList("modal-box--visible");
    }

    void OpenExitConfirmModal()
    {
        PlayClick();
        _configExitConfirmOverlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
        _configExitConfirmOverlay.AddToClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.AddToClassList("modal-box--visible");
    }

    void CloseExitConfirmModal()
    {
        _configExitConfirmOverlay.RemoveFromClassList("modal-overlay--visible");
        _configExitConfirmOverlay.Q<VisualElement>("ConfigExitConfirmBox")?.RemoveFromClassList("modal-box--visible");
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
