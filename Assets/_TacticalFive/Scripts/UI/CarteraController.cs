using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class CarteraController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerChemistry;
    private Label _headerSeason;
    private Label _headerDate;
    private Button _btnAction;

    private VisualElement _teamList;
    private Label _selectedTeamLabel;
    private VisualElement _playerList;
    private VisualElement _ojeadorBody;
    private VisualElement _scoutSlots;

    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<TeamData> _allTeams;
    private EmployeeData _ojeador;
    private List<ScoutData> _scouts;
    private TeamData _selectedTeam;
    private PlayerData _selectedPlayer;

    private Dictionary<string, Sprite> _logoSprites = new();
    private StyleBackground _empleadoBg;
    private Texture2D _starTex;
    private StyleBackground _starBg;

    private const int MAX_SCOUTS = 3;
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
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerChemistry = _root.Q<Label>("HeaderChemistry");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _btnAction = _root.Q<Button>("BtnAction");

        _teamList = _root.Q<VisualElement>("TeamList");
        _selectedTeamLabel = _root.Q<Label>("SelectedTeamLabel");
        _playerList = _root.Q<VisualElement>("PlayerList");
        _ojeadorBody = _root.Q<VisualElement>("OjeadorBody");
        _scoutSlots = _root.Q<VisualElement>("ScoutSlots");
    }

    void LoadSidebarIcons()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos)
            _logoSprites[s.name] = s;

        var tex = Resources.Load<Texture2D>("Icons/empleado");
        if (tex != null)
            _empleadoBg = new StyleBackground(tex);

        _starTex = Resources.Load<Texture2D>("Icons/star_24px");
        if (_starTex != null)
            _starBg = new StyleBackground(_starTex);

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
            var t = Resources.Load<Texture2D>($"Icons/{kv.Value}");
            if (t != null)
                iconElem.style.backgroundImage = new StyleBackground(t);
        }
    }

    void LoadData()
    {
        _manager = DatabaseManager.Instance.GetActiveManager();
        if (_manager == null) return;
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _allTeams = DatabaseManager.Instance.GetAllTeams().Where(t => t.id != _myTeam.id).OrderBy(t => t.name).ToList();

        var employees = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        _ojeador = employees.FirstOrDefault(e => e.position == "OJEADOR");

        try
        {
            _scouts = DatabaseManager.Instance.GetScoutsByTeam(_myTeam.id);
        }
        catch
        {
            DatabaseManager.Instance.Db.CreateTable<ScoutData>();
            _scouts = new();
        }

        _selectedTeam = null;
        _selectedPlayer = null;
    }

    void RegisterCallbacks()
    {
        // Sidebar unificado
        SidebarController.Attach(_root, GameScreen.Cartera);
        HeaderController.Attach(_root);
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
        _root.Q<Button>("NavManager")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Manager); });
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Messages); });
        _root.Q<VisualElement>("ConfigIcon")?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); OpenConfigModal(); });

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

    void Refresh()
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.SetDefaultCursor();
        _root.Q<Button>("SubmenuCartera")?.AddToClassList("nav-submenu-item--active");
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Cartera] RefreshHeader error: {ex.Message}"); }
        BuildOjeadorCard();
        BuildTeamList();
        BuildPlayerList();
        BuildScoutSlots();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;
        if (_headerTeamName == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";

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

        _btnAction.text = "MENÚ PRINCIPAL";

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            _headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }
    }

    void BuildOjeadorCard()
    {
        _ojeadorBody.Clear();

        if (_ojeador == null)
        {
            var emptyPanel = new VisualElement();
            emptyPanel.AddToClassList("fin-staff-empty");

            var emptyLbl = new Label();
            emptyLbl.AddToClassList("fin-staff-empty-text");
            emptyLbl.text = "No tienes Ojeador contratado.";
            emptyPanel.Add(emptyLbl);

            var hireBtn = new Button();
            hireBtn.AddToClassList("btn-hire");
            hireBtn.text = "IR A EMPLEADOS";
            hireBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                ScreenManager.Instance.GoTo(GameScreen.Employees);
            });
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(hireBtn);
            emptyPanel.Add(hireBtn);
            _ojeadorBody.Add(emptyPanel);
            return;
        }

        var card = new VisualElement();
        card.AddToClassList("fin-staff-card");

        var icon = new VisualElement();
        icon.AddToClassList("fin-staff-icon");
        if (_empleadoBg != null)
            icon.style.backgroundImage = _empleadoBg;
        card.Add(icon);

        var info = new VisualElement();
        info.AddToClassList("fin-staff-info");

        var nameLbl = new Label();
        nameLbl.AddToClassList("fin-staff-name");
        nameLbl.text = $"{_ojeador.first_name} {_ojeador.last_name}".ToUpper();
        info.Add(nameLbl);

        var repRow = new VisualElement();
        repRow.style.flexDirection = FlexDirection.Row;
        repRow.style.marginTop = 4;
        for (int i = 0; i < 5; i++)
        {
            var star = new VisualElement();
            star.AddToClassList("fin-staff-star");
            if (i >= _ojeador.reputation)
                star.AddToClassList("fin-staff-star--empty");
            if (_starTex != null)
                star.style.backgroundImage = _starBg;
            repRow.Add(star);
        }
        info.Add(repRow);

        var salaryLbl = new Label();
        salaryLbl.AddToClassList("fin-staff-interest");
        salaryLbl.text = FormatSalary(_ojeador.salary);
        info.Add(salaryLbl);

        card.Add(info);
        _ojeadorBody.Add(card);
    }

    void BuildTeamList()
    {
        _teamList.Clear();

        foreach (var team in _allTeams)
        {
            var btn = new Button();
            btn.AddToClassList("team-logo-btn");
            if (_selectedTeam != null && _selectedTeam.id == team.id)
                btn.AddToClassList("team-logo-btn--selected");

            var logoImg = new VisualElement();
            logoImg.AddToClassList("team-logo-img");
            if (_logoSprites.TryGetValue(team.logo, out var sprite))
                logoImg.style.backgroundImage = new StyleBackground(sprite);
            btn.Add(logoImg);

            var captured = team;
            btn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                _selectedTeam = captured;
                _selectedPlayer = null;
                Refresh(); // rebuilds team list (to update selected state), player list, etc.
            });

            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(btn);

            _teamList.Add(btn);
        }
    }

    void BuildPlayerList()
    {
        _playerList.Clear();

        if (_selectedTeam == null)
        {
            _selectedTeamLabel.text = "Selecciona un equipo para ver sus jugadores";
            return;
        }

        _selectedTeamLabel.text = "SELECCIONA UN JUGADOR PARA OJEAR";

        var players = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeam.id)
            .OrderByDescending(p => p.GetCalculatedAverage())
            .ToList();

        for (int i = 0; i < players.Count; i += 2)
        {
            var wrapper = new VisualElement();
            wrapper.style.flexDirection = FlexDirection.Row;

            float flexBasis = 0;
            float flexGrow = 1;
            float flexShrink = 1;

            // Left column
            var col1 = new VisualElement();
            col1.style.flexBasis = flexBasis;
            col1.style.flexGrow = flexGrow;
            col1.style.flexShrink = flexShrink;
            col1.Add(BuildPlayerRow(players[i]));
            wrapper.Add(col1);

            // 4px gap between columns
            var gap = new VisualElement();
            gap.style.width = 4;
            wrapper.Add(gap);

            // Right column (player or empty)
            var col2 = new VisualElement();
            col2.style.flexBasis = flexBasis;
            col2.style.flexGrow = flexGrow;
            col2.style.flexShrink = flexShrink;
            if (i + 1 < players.Count)
                col2.Add(BuildPlayerRow(players[i + 1]));
            wrapper.Add(col2);

            _playerList.Add(wrapper);
        }

        // Ojear button below player list if a player is selected
        if (_selectedPlayer != null)
        {
            var scoutBtn = new Button();
            bool hasOjeador = _ojeador != null;
            bool slotsFull = _scouts.Count >= MAX_SCOUTS;

            var alreadyScouting = _scouts.Any(s => s.player_id == _selectedPlayer.id);
            if (alreadyScouting)
            {
                scoutBtn.AddToClassList("btn-scout--disabled");
                scoutBtn.text = "YA EN CARTERA";
                scoutBtn.SetEnabled(false);
            }
            else if (!hasOjeador)
            {
                scoutBtn.AddToClassList("btn-scout--disabled");
                scoutBtn.text = "SIN OJEADOR";
                scoutBtn.SetEnabled(false);
            }
            else
            {
                scoutBtn.AddToClassList("btn-scout");
                scoutBtn.text = $"OJEAR A {_selectedPlayer.first_name.ToUpper()} {_selectedPlayer.last_name.ToUpper()}";

                var captured = _selectedPlayer;
                scoutBtn.RegisterCallback<ClickEvent>(_ =>
                {
                    PlayClick();
                    if (slotsFull)
                        ShowScoutFullModal();
                    else
                        StartScout(captured);
                });
            }

            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(scoutBtn);

            _playerList.Add(scoutBtn);
        }
    }

    VisualElement BuildPlayerRow(PlayerData p)
    {
        var row = new VisualElement();
        row.AddToClassList("player-row");
        if (_selectedPlayer != null && _selectedPlayer.id == p.id)
            row.AddToClassList("player-row--selected");

        var nameLbl = new Label();
        nameLbl.AddToClassList("player-row-name");
        nameLbl.text = $"{p.first_name} {p.last_name}".ToUpper();
        row.Add(nameLbl);

        var posLbl = new Label();
        posLbl.AddToClassList("player-row-pos");
        posLbl.text = PositionCodes.GetShort(p.position);
        row.Add(posLbl);

        var ageLbl = new Label();
        ageLbl.AddToClassList("player-row-age");
        ageLbl.text = $"{p.age} años";
        row.Add(ageLbl);

        var ovrLbl = new Label();
        ovrLbl.AddToClassList("player-row-ovr");
        int med = p.GetCalculatedAverage();
        ovrLbl.text = med.ToString();
        if (med > 84)
            ovrLbl.AddToClassList("player-ovr--high");
        else if (med >= 70)
            ovrLbl.AddToClassList("player-ovr--mid");
        else
            ovrLbl.AddToClassList("player-ovr--low");
        row.Add(ovrLbl);

        var captured = p;
        row.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _selectedPlayer = captured;
            BuildPlayerList();
        });

        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(row);

        return row;
    }

    void StartScout(PlayerData player)
    {
        if (_season == null || _ojeador == null) return;

        if (_scouts.Count >= MAX_SCOUTS) return;

        int scoutDays = GetScoutDays(_ojeador.reputation);
        int endDay = _season.current_game_day + scoutDays;

        int slot = 0;
        for (int i = 0; i < MAX_SCOUTS; i++)
        {
            if (!_scouts.Any(s => s.slot == i))
            {
                slot = i;
                break;
            }
        }

        var scout = new ScoutData
        {
            team_id = _myTeam.id,
            slot = slot,
            player_id = player.id,
            start_day = _season.current_game_day,
            end_day = endDay,
            completed = 0
        };

        DatabaseManager.Instance.InsertScout(scout);
        _scouts = DatabaseManager.Instance.GetScoutsByTeam(_myTeam.id);
        _selectedPlayer = null;
        Refresh();
    }

    void BuildScoutSlots()
    {
        _scoutSlots.Clear();

        for (int i = 0; i < MAX_SCOUTS; i++)
        {
            var scout = _scouts.FirstOrDefault(s => s.slot == i);
            var slot = new VisualElement();
            slot.AddToClassList("scout-slot");

            if (scout == null)
            {
                slot.AddToClassList("scout-slot--empty");
                var emptyLbl = new Label();
                emptyLbl.AddToClassList("scout-empty-text");
                emptyLbl.text = "— Vacío —";
                slot.Add(emptyLbl);
            }
            else
            {
                var player = DatabaseManager.Instance.GetPlayer(scout.player_id);
                if (player == null) continue;

                if (scout.completed == 1)
                    slot.AddToClassList("scout-slot--completed");
                else
                    slot.AddToClassList("scout-slot--scouting");

                var header = new VisualElement();
                header.AddToClassList("scout-slot-header");

                var nameLbl = new Label();
                nameLbl.AddToClassList("scout-slot-name");
                nameLbl.text = $"{player.first_name} {player.last_name}".ToUpper();
                header.Add(nameLbl);

                var posLbl = new Label();
                posLbl.AddToClassList("scout-slot-pos");
                posLbl.text = PositionCodes.GetShort(player.position);
                header.Add(posLbl);

                var ageLbl = new Label();
                ageLbl.AddToClassList("scout-slot-age");
                ageLbl.text = $"{player.age} a\u00f1os";
                header.Add(ageLbl);

                slot.Add(header);

                if (scout.completed == 1)
                {
                    // Full details
                    var attrs = new VisualElement();
                    attrs.AddToClassList("scout-slot-attributes");

                    AddAttr(attrs, "Media", player.overall.ToString());
                    AddAttr(attrs, "Potencial", player.potential.ToString());
                    AddAttr(attrs, "Velocidad", player.speed.ToString());
                    AddAttr(attrs, "Tiro", player.shooting.ToString());
                    AddAttr(attrs, "Triple", player.three_point.ToString());
                    AddAttr(attrs, "Pase", player.passing.ToString());
                    AddAttr(attrs, "Bote", player.dribbling.ToString());
                    AddAttr(attrs, "Defensa", player.defense.ToString());
                    AddAttr(attrs, "Rebote", player.rebounding.ToString());
                    AddAttr(attrs, "Atletismo", player.athleticism.ToString());
                    AddAttr(attrs, "IQ", player.iq.ToString());
                    AddAttr(attrs, "Robos", player.steals.ToString());
                    AddAttr(attrs, "Tapones", player.blocks.ToString());
                    AddAttr(attrs, "Moral", player.morale.ToString());

                    slot.Add(attrs);

                    var infoRow = new VisualElement();
                    infoRow.style.flexDirection = FlexDirection.Row;
                    infoRow.style.marginTop = 6;

                    var salaryLbl = new Label();
                    salaryLbl.AddToClassList("scout-slot-salary");
                    salaryLbl.style.flexGrow = 1;
                    salaryLbl.text = $"<color=#7a8aaa>Salario:</color> <color=#d0d8e8>{FormatSalary(player.salary)}</color>";
                    infoRow.Add(salaryLbl);

                    var contractLbl = new Label();
                    contractLbl.AddToClassList("scout-slot-contract");
                    contractLbl.style.flexGrow = 1;
                    string yearPlural = player.contract_years != 1 ? "s" : "";
                    contractLbl.text = $"<color=#7a8aaa>Contrato:</color> <color=#d0d8e8>{player.contract_years} a\u00f1o{yearPlural}</color>";
                    infoRow.Add(contractLbl);

                    slot.Add(infoRow);
                }
                else
                {
                    // Minimal info + timer
                    var timerLbl = new Label();
                    timerLbl.AddToClassList("scout-slot-timer");
                    int remaining = scout.end_day - (_season?.current_game_day ?? 0);
                    if (remaining < 0) remaining = 0;
                    timerLbl.text = $"Ojeando... {remaining} d\u00eda{(remaining != 1 ? "s" : "")} restante{(remaining != 1 ? "s" : "")}";
                    slot.Add(timerLbl);
                }

                // Remove button
                {
                    var removeBtn = new Button();
                    removeBtn.AddToClassList("btn-fire");
                    removeBtn.text = "RETIRAR";
                    var captured = scout;
                    removeBtn.RegisterCallback<ClickEvent>(_ =>
                    {
                        PlayClick();
                        DatabaseManager.Instance.DeleteScout(captured.id);
                        _scouts = DatabaseManager.Instance.GetScoutsByTeam(_myTeam.id);
                        Refresh();
                    });
                    if (CursorManager.Instance != null)
                        CursorManager.Instance.RegisterHandCursor(removeBtn);
                    slot.Add(removeBtn);
                }
            }

            _scoutSlots.Add(slot);
        }
    }

    void AddAttr(VisualElement parent, string label, string value)
    {
        var row = new Label();
        row.AddToClassList("scout-attr");
        row.text = $"<color=#7a8aaa>{label}:</color> <color=#d0d8e8>{value}</color>";
        parent.Add(row);
    }

    int GetScoutDays(int reputation)
    {
        return reputation switch
        {
            5 => 3,
            4 => 5,
            3 => 8,
            2 => 12,
            1 => 16,
            _ => 20
        };
    }

    string FormatSalary(long amount)
    {
        return "$" + amount.ToString("N0").Replace(',', '.');
    }

    VisualElement _scoutFullOverlay;

    void ShowScoutFullModal()
    {
        if (_scoutFullOverlay != null) return;

        var overlay = new VisualElement();
        overlay.AddToClassList("cartera-modal-overlay");
        overlay.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == overlay)
                CloseScoutFullModal();
        });

        var box = new VisualElement();
        box.AddToClassList("cartera-modal-box");

        var title = new Label();
        title.AddToClassList("cartera-modal-title");
        title.text = "CARTERA LLENA";
        box.Add(title);

        var msg = new Label();
        msg.AddToClassList("cartera-modal-text");
        msg.text = "No hay m\u00e1s espacio para ojeadores.\nRetira alg\u00fan jugador de la cartera para poder ojear a m\u00e1s.";
        box.Add(msg);

        var okBtn = new Button();
        okBtn.AddToClassList("cartera-modal-btn");
        okBtn.text = "ENTENDIDO";
        okBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseScoutFullModal(); });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(okBtn);
        box.Add(okBtn);

        overlay.Add(box);
        _root.Add(overlay);
        _scoutFullOverlay = overlay;
    }

    void CloseScoutFullModal()
    {
        if (_scoutFullOverlay != null)
        {
            _root.Remove(_scoutFullOverlay);
            _scoutFullOverlay = null;
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
