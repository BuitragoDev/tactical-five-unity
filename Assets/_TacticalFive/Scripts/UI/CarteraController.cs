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
    private Label _headerScoutCount;
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
        _headerScoutCount = _root.Q<Label>("HeaderScoutCount");
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
        _root.Q<Button>("SubmenuEmpleados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Employees); });
        _root.Q<Button>("SubmenuLesionados")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _root.Q<VisualElement>("RosterSubmenu")?.RemoveFromClassList("nav-submenu--visible"); ScreenManager.Instance.GoTo(GameScreen.Injured); });
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

        if (CursorManager.Instance != null)
        {
            foreach (var name in new[] { "NavDashboard", "NavRoster", "SubmenuJugadores", "SubmenuEmpleados", "SubmenuLesionados", "NavCalendar", "NavStandings", "NavPalmares", "NavResults", "NavPlayoffs", "NavStats", "NavRecords", "NavMarket", "SubmenuOfertas", "SubmenuCartera", "SubmenuHistorial", "NavFinances", "SubmenuDecisiones", "SubmenuPrestamos", "NavSponsors", "NavTV", "NavArena", "NavMessages", "ConfigIcon", "BtnAction" })
            {
                var btn = _root.Q<Button>(name);
                if (btn != null)
                    CursorManager.Instance.RegisterHandCursor(btn);
            }
        }
    }

    void Refresh()
    {
        _root.Q<Button>("SubmenuCartera")?.AddToClassList("nav-submenu-item--active");
        RefreshHeader();
        BuildOjeadorCard();
        BuildTeamList();
        BuildPlayerList();
        BuildScoutSlots();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerScoutCount.text = $"{_scouts.Count}/{MAX_SCOUTS}";
        _btnAction.text = "DASHBOARD";

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
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("fin-staff-empty");
            emptyLbl.text = "No tienes Ojeador contratado.";
            _ojeadorBody.Add(emptyLbl);
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

            if (_logoSprites.TryGetValue(team.logo, out var sprite))
                btn.style.backgroundImage = new StyleBackground(sprite);

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

        var players = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeam.id);

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
        posLbl.text = p.position;
        row.Add(posLbl);

        var ageLbl = new Label();
        ageLbl.AddToClassList("player-row-age");
        ageLbl.text = p.age.ToString();
        row.Add(ageLbl);

        var ovrLbl = new Label();
        ovrLbl.AddToClassList("player-row-ovr");
        ovrLbl.text = p.overall.ToString();
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
                posLbl.text = player.position;
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

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
