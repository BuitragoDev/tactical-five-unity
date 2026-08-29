using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class CarteraController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Cartera;

    private VisualElement _teamList;
    private Label _selectedTeamLabel;
    private VisualElement _playerList;
    private VisualElement _ojeadorBody;
    private VisualElement _scoutSlots;
    private VisualElement _salaryMarginBox;
    private Label _salaryMarginTitle;
    private Label _salaryMarginValue;
    private VisualElement _pageOjear;
    private VisualElement _pageCartera;
    private VisualElement _pageInformes;
    private Button _tabOjear;
    private Button _tabCartera;
    private Button _tabInformes;
    private Button _sortMedia;
    private Button _sortEdad;
    private Button _sortSalario;
    private VisualElement _informesTableHeader;
    private VisualElement _informesTableBody;

    private List<TeamData> _allTeams;
    private List<ScoutData> _scouts;
    private TeamData _selectedTeam;
    private PlayerData _selectedPlayer;
    private Dictionary<string, Sprite> _logoSprites = new();

    private string _activeTab = "ojear";
    private string _sortBy = "media"; // media | edad | salario
    private const int MAX_SCOUTS = 3;
    private int _expandedSlotIndex = -1;
    private HashSet<int> _scoutedPlayerIds;

    private Texture2D _starTex;
    private StyleBackground _starBg;
    private StyleBackground _empleadoBg;

    protected override void CacheReferences()
    {
        _teamList = _root.Q<VisualElement>("TeamList");
        _selectedTeamLabel = _root.Q<Label>("SelectedTeamLabel");
        _playerList = _root.Q<VisualElement>("PlayerList");
        _salaryMarginBox = _root.Q<VisualElement>("SalaryMarginBox");
        _salaryMarginTitle = _root.Q<Label>("SalaryMarginTitle");
        _salaryMarginValue = _root.Q<Label>("SalaryMarginValue");
        _ojeadorBody = _root.Q<VisualElement>("OjeadorBody");
        _scoutSlots = _root.Q<VisualElement>("ScoutSlots");
        _pageOjear = _root.Q<VisualElement>("PageOjear");
        _pageCartera = _root.Q<VisualElement>("PageCartera");
        _pageInformes = _root.Q<VisualElement>("PageInformes");
        _tabOjear = _root.Q<Button>("TabOjear");
        _tabCartera = _root.Q<Button>("TabCartera");
        _tabInformes = _root.Q<Button>("TabInformes");
        _sortMedia = _root.Q<Button>("SortMedia");
        _sortEdad = _root.Q<Button>("SortEdad");
        _sortSalario = _root.Q<Button>("SortSalario");
        _informesTableHeader = _root.Q<VisualElement>("InformesTableHeader");
        _informesTableBody = _root.Q<VisualElement>("InformesTableBody");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
        foreach (var s in logos)
            _logoSprites[s.name] = s;

        var tex = Resources.Load<Texture2D>("Icons/empleado");
        if (tex != null)
            _empleadoBg = new StyleBackground(tex);

        _starTex = Resources.Load<Texture2D>("Icons/star_24px");
        if (_starTex != null)
            _starBg = new StyleBackground(_starTex);

        _allTeams = DatabaseManager.Instance.GetAllTeams().Where(t => t.id != _myTeam.id).OrderBy(t => t.name).ToList();

        try { _scouts = DatabaseManager.Instance.GetScoutsByTeam(_myTeam.id); }
        catch { DatabaseManager.Instance.Db.CreateTable<ScoutData>(); _scouts = new(); }

        _selectedTeam = null;
        _selectedPlayer = null;
        _activeTab = "ojear";
        _sortBy = "media";
        _expandedSlotIndex = -1;
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _tabOjear?.RegisterCallback<ClickEvent>(_ => ShowTab("ojear"));
        _tabCartera?.RegisterCallback<ClickEvent>(_ => ShowTab("cartera"));
        _tabInformes?.RegisterCallback<ClickEvent>(_ => ShowTab("informes"));
        _sortMedia?.RegisterCallback<ClickEvent>(_ => SetSort("media"));
        _sortEdad?.RegisterCallback<ClickEvent>(_ => SetSort("edad"));
        _sortSalario?.RegisterCallback<ClickEvent>(_ => SetSort("salario"));
    }

    void ShowTab(string tab)
    {
        PlayClick();
        _activeTab = tab;
        _tabOjear?.EnableInClassList("standings-tab--active", tab == "ojear");
        _tabCartera?.EnableInClassList("standings-tab--active", tab == "cartera");
        _tabInformes?.EnableInClassList("standings-tab--active", tab == "informes");
        SetPageDisplay(_pageOjear, tab == "ojear");
        SetPageDisplay(_pageCartera, tab == "cartera");
        SetPageDisplay(_pageInformes, tab == "informes");
        Refresh();
    }

    void SetPageDisplay(VisualElement page, bool show)
    {
        if (page == null) return;
        page.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void SetSort(string sort)
    {
        PlayClick();
        _sortBy = sort;
        _sortMedia?.EnableInClassList("sort-btn--active", sort == "media");
        _sortEdad?.EnableInClassList("sort-btn--active", sort == "edad");
        _sortSalario?.EnableInClassList("sort-btn--active", sort == "salario");
        BuildPlayerList();
    }

    protected override void Refresh()
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.SetDefaultCursor();
        _root.Q<Button>("SubmenuCartera")?.AddToClassList("nav-submenu-item--active");
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Cartera] RefreshHeader error: {ex.Message}"); }

        _scoutedPlayerIds = DatabaseManager.Instance.GetScoutedPlayerIds(_myTeam.id);
        try { _scouts = DatabaseManager.Instance.GetScoutsByTeam(_myTeam.id); } catch { }

        if (_activeTab == "ojear") { BuildOjeadorCard(); BuildTeamList(); BuildPlayerList(); }
        else if (_activeTab == "cartera") { BuildScoutSlots(); }
        else if (_activeTab == "informes") { BuildInformes(); }
    }

    protected override void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;
        var headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        var headerTeamName = _root.Q<Label>("HeaderTeamName");
        var headerManagerName = _root.Q<Label>("HeaderManagerName");
        var headerBudget = _root.Q<Label>("HeaderBudget");
        var headerPayroll = _root.Q<Label>("HeaderPayroll");
        var headerMargin = _root.Q<Label>("HeaderMargin");
        var headerChemistry = _root.Q<Label>("HeaderChemistry");
        var headerSeason = _root.Q<Label>("HeaderSeason");
        var headerDate = _root.Q<Label>("HeaderDate");
        if (headerTeamName == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        headerTeamName.text = _myTeam.name.ToUpper();
        headerManagerName.text = $"Manager: {_manager.name}";

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        long totalPayroll = players.Sum(p => p.salary);

        headerBudget.text = $"${_myTeam.budget / 1_000_000}M";
        headerBudget.style.color = _myTeam.budget < 0
            ? new StyleColor(new Color32(192, 57, 43, 255))
            : new StyleColor(new Color32(39, 174, 96, 255));
        headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long margin = salaryCap - totalPayroll;
        string marginText = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        headerMargin.text = marginText;

        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        headerChemistry.text = $"{chemistry}%";
        headerChemistry.RemoveFromClassList("header-stat-value--gold");
        headerChemistry.RemoveFromClassList("header-stat-value--negative");
        if (chemistry < 40)
            headerChemistry.AddToClassList("header-stat-value--negative");
        else if (chemistry < 70)
            headerChemistry.AddToClassList("header-stat-value--gold");

        headerMargin.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) headerMargin.AddToClassList("header-stat-value--negative");

        _btnAction.text = "MENÚ PRINCIPAL";

        if (_season != null)
        {
            headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }
    }

    // ═══════════ OJEADOR ═══════════

    EmployeeData GetOjeador()
    {
        var employees = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id);
        return employees.FirstOrDefault(e => e.position == "OJEADOR");
    }

    void BuildOjeadorCard()
    {
        _ojeadorBody.Clear();
        var ojeador = GetOjeador();

        if (ojeador == null)
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
            hireBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.Employees); });
            if (CursorManager.Instance != null) CursorManager.Instance.RegisterHandCursor(hireBtn);
            emptyPanel.Add(hireBtn);
            _ojeadorBody.Add(emptyPanel);
            return;
        }

        var card = new VisualElement();
        card.AddToClassList("fin-staff-card");

        var icon = new VisualElement();
        icon.AddToClassList("fin-staff-icon");
        if (_empleadoBg != null) icon.style.backgroundImage = _empleadoBg;
        card.Add(icon);

        var info = new VisualElement();
        info.AddToClassList("fin-staff-info");

        var nameLbl = new Label();
        nameLbl.AddToClassList("fin-staff-name");
        nameLbl.text = $"{ojeador.first_name} {ojeador.last_name}".ToUpper();
        info.Add(nameLbl);

        var repRow = new VisualElement();
        repRow.style.flexDirection = FlexDirection.Row;
        repRow.style.marginTop = 4;
        for (int i = 0; i < 5; i++)
        {
            var star = new VisualElement();
            star.AddToClassList("fin-staff-star");
            if (i >= ojeador.reputation) star.AddToClassList("fin-staff-star--empty");
            if (_starTex != null) star.style.backgroundImage = _starBg;
            repRow.Add(star);
        }
        info.Add(repRow);

        var salaryLbl = new Label();
        salaryLbl.AddToClassList("fin-staff-interest");
        salaryLbl.text = FormatSalary(ojeador.salary);
        info.Add(salaryLbl);

        card.Add(info);
        _ojeadorBody.Add(card);
    }

    // ═══════════ EQUIPOS ═══════════

    void BuildTeamList()
    {
        _teamList.Clear();

        foreach (var team in _allTeams)
        {
            var row = new VisualElement();
            row.AddToClassList("team-select-row");
            if (_selectedTeam != null && _selectedTeam.id == team.id)
                row.AddToClassList("team-select-row--selected");

            var logoImg = new VisualElement();
            logoImg.AddToClassList("team-select-logo");
            if (_logoSprites.TryGetValue(team.logo, out var sprite))
                logoImg.style.backgroundImage = new StyleBackground(sprite);
            row.Add(logoImg);

            var abbrev = new Label();
            abbrev.AddToClassList("team-select-abbrev");
            abbrev.text = team.abbreviation;
            row.Add(abbrev);

            var name = new Label();
            name.AddToClassList("team-select-name");
            name.text = team.name;
            row.Add(name);

            var captured = team;
            row.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                _selectedTeam = captured;
                _selectedPlayer = null;
                BuildTeamList();
                BuildPlayerList();
            });

            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(row);

            _teamList.Add(row);
        }
    }

    // ═══════════ JUGADORES ═══════════

    void BuildPlayerList()
    {
        _playerList.Clear();

        if (_selectedTeam == null)
        {
            if (_salaryMarginBox != null) _salaryMarginBox.style.display = DisplayStyle.None;
            _selectedTeamLabel.text = "Selecciona un equipo para ver sus jugadores";
            return;
        }

        // Salary margin
        if (_salaryMarginBox != null)
        {
            _salaryMarginBox.style.display = DisplayStyle.Flex;
            if (_salaryMarginTitle != null)
                _salaryMarginTitle.text = $"MARGEN SALARIAL DE {_selectedTeam.name.ToUpper()}";
            if (_salaryMarginValue != null)
            {
                var teamPlayers = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeam.id);
                long teamPayroll = teamPlayers.Sum(p => p.salary);
                var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
                long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
                long margin = salaryCap - teamPayroll;
                string absFormatted = System.Math.Abs(margin).ToString("N0").Replace(',', '.');
                _salaryMarginValue.text = margin >= 0 ? $"+{absFormatted} $" : $"-{absFormatted} $";
                _salaryMarginValue.RemoveFromClassList("salary-margin-value--positive");
                _salaryMarginValue.RemoveFromClassList("salary-margin-value--negative");
                _salaryMarginValue.AddToClassList(margin >= 0 ? "salary-margin-value--positive" : "salary-margin-value--negative");
            }
        }

        _selectedTeamLabel.text = "SELECCIONA UN JUGADOR PARA OJEAR";

        var players = DatabaseManager.Instance.GetPlayersByTeam(_selectedTeam.id).ToList();
        players = _sortBy switch
        {
            "edad" => players.OrderBy(p => p.age).ToList(),
            "salario" => players.OrderByDescending(p => p.salary).ToList(),
            _ => players.OrderByDescending(p => p.GetCalculatedAverage()).ToList()
        };

        for (int i = 0; i < players.Count; i++)
        {
            _playerList.Add(BuildPlayerRow(players[i]));
        }

        if (_selectedPlayer != null)
            _playerList.Add(BuildScoutButton(_selectedPlayer));
    }

    VisualElement BuildPlayerRow(PlayerData p)
    {
        var row = new VisualElement();
        row.AddToClassList("player-row");
        if (_selectedPlayer != null && _selectedPlayer.id == p.id)
            row.AddToClassList("player-row--selected");

        // Photo
        var photo = new VisualElement();
        photo.AddToClassList("player-row-photo");
        Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
        if (tex != null) photo.style.backgroundImage = new StyleBackground(tex);
        row.Add(photo);

        // Main (name + meta)
        var main = new VisualElement();
        main.AddToClassList("player-row-main");

        var nameLbl = new Label();
        nameLbl.AddToClassList("player-row-name");
        nameLbl.text = $"{p.first_name} {p.last_name}".ToUpper();
        main.Add(nameLbl);

        var metaLbl = new Label();
        metaLbl.AddToClassList("player-row-meta");
        string posText = PositionCodes.GetShort(p.position);
        if (!string.IsNullOrEmpty(p.secondary_position))
            posText += $"/{PositionCodes.GetShort(p.secondary_position)}";
        metaLbl.text = $"{posText} · {p.age} años · {p.height_cm}cm · {(p.is_rookie == 1 ? "Rookie · " : "")}{CountryCodes.GetName(p.nationality)}";
        main.Add(metaLbl);

        row.Add(main);

        // OVR + salary
        var stats = new VisualElement();
        stats.AddToClassList("player-row-stats");

        var ovrLbl = new Label();
        ovrLbl.AddToClassList("player-row-ovr");
        ovrLbl.text = FogOfWarHelper.GetOvrDisplay(p, _myTeam.id, _scoutedPlayerIds);
        int med = p.GetCalculatedAverage();
        if (med > 84) ovrLbl.AddToClassList("player-ovr--high");
        else if (med >= 70) ovrLbl.AddToClassList("player-ovr--mid");
        else ovrLbl.AddToClassList("player-ovr--low");
        stats.Add(ovrLbl);

        var salaryLbl = new Label();
        salaryLbl.AddToClassList("player-row-salary");
        salaryLbl.text = FormatSalary(p.salary);
        stats.Add(salaryLbl);

        row.Add(stats);

        // Ver perfil button
        var viewBtn = new Button();
        viewBtn.AddToClassList("player-row-view");
        viewBtn.text = "PERFIL";
        var captured = p;
        viewBtn.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            PlayClick();
            ScreenManager.SelectedPlayerId = captured.id;
            ScreenManager.Instance.GoTo(GameScreen.PlayerProfile);
        });
        if (CursorManager.Instance != null) CursorManager.Instance.RegisterHandCursor(viewBtn);
        row.Add(viewBtn);

        row.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _selectedPlayer = captured;
            BuildPlayerList();
        });
        if (CursorManager.Instance != null) CursorManager.Instance.RegisterHandCursor(row);

        return row;
    }

    VisualElement BuildScoutButton(PlayerData player)
    {
        var scoutBtn = new Button();
        bool hasOjeador = GetOjeador() != null;
        bool slotsFull = _scouts.Count >= MAX_SCOUTS;
        bool alreadyScouting = _scouts.Any(s => s.player_id == player.id);

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
            scoutBtn.text = $"OJEAR A {player.first_name.ToUpper()} {player.last_name.ToUpper()}";
            var captured = player;
            scoutBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                if (slotsFull) ShowScoutFullModal();
                else StartScout(captured);
            });
        }

        if (CursorManager.Instance != null) CursorManager.Instance.RegisterHandCursor(scoutBtn);
        return scoutBtn;
    }

    void StartScout(PlayerData player)
    {
        if (_season == null) return;
        var ojeador = GetOjeador();
        if (ojeador == null || _scouts.Count >= MAX_SCOUTS) return;

        int scoutDays = GetScoutDays(ojeador.reputation);
        int endDay = _season.current_game_day + scoutDays - 1;
        if (endDay < _season.current_game_day) endDay = _season.current_game_day;

        int slot = 0;
        for (int i = 0; i < MAX_SCOUTS; i++)
        {
            if (!_scouts.Any(s => s.slot == i)) { slot = i; break; }
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

    // ═══════════ CARTERA (slots) ═══════════

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
                emptyLbl.text = "SLOT DISPONIBLE";
                slot.Add(emptyLbl);
            }
            else
            {
                var player = DatabaseManager.Instance.GetPlayer(scout.player_id);
                if (player == null) continue;

                slot.AddToClassList(scout.completed == 1 ? "scout-slot--completed" : "scout-slot--scouting");

                var header = new VisualElement();
                header.AddToClassList("scout-slot-header");

                var arrowLbl = new Label();
                arrowLbl.AddToClassList("scout-slot-header-arrow");
                arrowLbl.text = _expandedSlotIndex == i ? "▼" : "▶";
                header.Add(arrowLbl);

                var nameLbl = new Label();
                nameLbl.AddToClassList("scout-slot-header-name");
                nameLbl.text = $"{player.first_name} {player.last_name}".ToUpper();
                header.Add(nameLbl);

                var posLbl = new Label();
                posLbl.AddToClassList("scout-slot-header-pos");
                if (scout.completed == 1)
                {
                    string posText = PositionCodes.GetName(player.position);
                    if (!string.IsNullOrEmpty(player.secondary_position))
                        posText += $" / {PositionCodes.GetName(player.secondary_position)}";
                    posLbl.text = posText;
                }
                else
                    posLbl.text = PositionCodes.GetShort(player.position);
                header.Add(posLbl);

                var sep1 = new Label();
                sep1.text = " - ";
                sep1.style.color = Color.white;
                sep1.style.fontSize = 18;
                sep1.style.marginLeft = 4;
                sep1.style.marginRight = 4;
                header.Add(sep1);

                var ageLbl = new Label();
                ageLbl.AddToClassList("scout-slot-header-age");
                ageLbl.text = $"{player.age} años";
                header.Add(ageLbl);

                var sep2 = new Label();
                sep2.text = " - ";
                sep2.style.color = Color.white;
                sep2.style.fontSize = 18;
                sep2.style.marginLeft = 4;
                sep2.style.marginRight = 4;
                header.Add(sep2);

                var medLbl = new Label();
                medLbl.AddToClassList("player-row-ovr");
                medLbl.text = $"{FogOfWarHelper.GetOvrDisplay(player, _myTeam.id, _scoutedPlayerIds)} MED";
                int med = player.GetCalculatedAverage();
                if (med > 84) medLbl.AddToClassList("player-ovr--high");
                else if (med >= 70) medLbl.AddToClassList("player-ovr--mid");
                else medLbl.AddToClassList("player-ovr--low");
                header.Add(medLbl);

                int slotIndex = i;
                header.RegisterCallback<ClickEvent>(_ =>
                {
                    PlayClick();
                    _expandedSlotIndex = _expandedSlotIndex == slotIndex ? -1 : slotIndex;
                    BuildScoutSlots();
                });
                slot.Add(header);

                bool isExpanded = _expandedSlotIndex == i;
                bool isReady = scout.completed == 1 || (scout.end_day <= (_season?.current_game_day ?? 0));

                if (isReady)
                {
                    if (isExpanded)
                        slot.Add(BuildCompletedScoutContent(player, scout));
                }
                else
                {
                    var timerLbl = new Label();
                    timerLbl.AddToClassList("scout-slot-timer");
                    int remaining = scout.end_day - (_season?.current_game_day ?? 0);
                    timerLbl.text = $"Ojeando... {remaining} día{(remaining != 1 ? "s" : "")} restante{(remaining != 1 ? "s" : "")}";
                    slot.Add(timerLbl);

                    if (isExpanded)
                        slot.Add(BuildRemoveScoutButton(scout, slotIndex));
                }
            }

            _scoutSlots.Add(slot);
        }
    }

    Button BuildRemoveScoutButton(ScoutData scout, int slotIndex)
    {
        var removeBtn = new Button();
        removeBtn.AddToClassList("scout-card-remove-btn");
        removeBtn.style.marginTop = 8;
        removeBtn.text = "RETIRAR";
        var captured = scout;
        removeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            DatabaseManager.Instance.DeleteScout(captured.id);
            _scouts = DatabaseManager.Instance.GetScoutsByTeam(_myTeam.id);
            if (_expandedSlotIndex == slotIndex) _expandedSlotIndex = -1;
            Refresh();
        });
        if (CursorManager.Instance != null) CursorManager.Instance.RegisterHandCursor(removeBtn);
        return removeBtn;
    }

    VisualElement BuildCompletedScoutContent(PlayerData p, ScoutData scout)
    {
        var content = new VisualElement();
        content.AddToClassList("scout-slot-content");

        var topRow = new VisualElement();
        topRow.AddToClassList("scout-card-top");

        var photo = new VisualElement();
        photo.AddToClassList("scout-player-photo");
        Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
        if (tex != null) photo.style.backgroundImage = new StyleBackground(tex);
        topRow.Add(photo);

        var attrCol = new VisualElement();
        attrCol.AddToClassList("scout-attr-column");

        var attrs = new[]
        {
            ("Tiro", p.shooting), ("Triple", p.three_point), ("Pase", p.passing),
            ("Bote", p.dribbling), ("Defensa", p.defense), ("Rebote", p.rebounding),
            ("Velocidad", p.speed), ("Atletismo", p.athleticism), ("IQ", p.iq),
            ("Robos", p.steals), ("Tapones", p.blocks),
        };

        foreach (var (label, val) in attrs)
        {
            var row = new VisualElement();
            row.AddToClassList("scout-attr-row");

            var lbl = new Label();
            lbl.AddToClassList("scout-attr-label");
            lbl.text = label;

            var barBg = new VisualElement();
            barBg.AddToClassList("scout-attr-bar-bg");

            var barFill = new VisualElement();
            barFill.AddToClassList("scout-attr-bar-fill");
            if (val < 50) barFill.AddToClassList("scout-attr-bar-fill--low");
            else if (val < 70) barFill.AddToClassList("scout-attr-bar-fill--mid");

            barFill.style.width = new StyleLength(new Length(val, LengthUnit.Percent));
            barBg.Add(barFill);

            var valLbl = new Label();
            valLbl.AddToClassList("scout-attr-val");
            valLbl.text = val.ToString();

            row.Add(lbl);
            row.Add(barBg);
            row.Add(valLbl);
            attrCol.Add(row);
        }

        topRow.Add(attrCol);
        content.Add(topRow);

        var bottomRow = new VisualElement();
        bottomRow.AddToClassList("scout-card-bottom");

        string yearPlural = p.contract_years != 1 ? "s" : "";
        var infoLbl = new Label();
        infoLbl.AddToClassList("scout-card-salary");
        infoLbl.text = $"Salario anual: {FormatSalary(p.salary)}    |    Contrato restante: {p.contract_years} año{yearPlural}";
        bottomRow.Add(infoLbl);

        bottomRow.Add(BuildRemoveScoutButton(scout, _expandedSlotIndex));

        content.Add(bottomRow);
        return content;
    }

    // ═══════════ INFORMES ═══════════

    void BuildInformes()
    {
        _informesTableHeader.Clear();
        _informesTableBody.Clear();

        BuildInformesHeader();

        var scouted = DatabaseManager.Instance.GetScoutedPlayers(_myTeam.id);
        if (scouted.Count == 0)
        {
            var empty = new Label();
            empty.AddToClassList("cartera-empty");
            empty.text = "Todavía no has ojeado a ningún jugador.";
            _informesTableBody.Add(empty);
            return;
        }

        foreach (var s in scouted)
        {
            var player = DatabaseManager.Instance.GetPlayerById(s.player_id);
            if (player == null) continue;
            _informesTableBody.Add(BuildInformeRow(player, s.scouted_day));
        }
    }

    void BuildInformesHeader()
    {
        AddCell(_informesTableHeader, "", "cartera-th cartera-th-photo");
        AddCell(_informesTableHeader, "NOMBRE", "cartera-th cartera-th-name");
        AddCell(_informesTableHeader, "POS", "cartera-th cartera-th-pos");
        AddCell(_informesTableHeader, "EDAD", "cartera-th cartera-th-age");
        AddCell(_informesTableHeader, "MEDIA", "cartera-th cartera-th-ovr");
        AddCell(_informesTableHeader, "SALARIO", "cartera-th cartera-th-salary");
        AddCell(_informesTableHeader, "EQUIPO", "cartera-th cartera-th-team");
        AddCell(_informesTableHeader, "DÍA", "cartera-th cartera-th-day");
        AddCell(_informesTableHeader, "", "cartera-th cartera-th-action");
    }

    VisualElement BuildInformeRow(PlayerData p, int scoutedDay)
    {
        var row = new VisualElement();
        row.AddToClassList("cartera-info-row");

        // Photo
        var photoCell = new VisualElement();
        photoCell.AddToClassList("cartera-td cartera-td-photo");
        Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
        if (tex != null)
        {
            var img = new Image();
            img.image = tex;
            img.AddToClassList("cartera-td-photo-img");
            photoCell.Add(img);
        }
        row.Add(photoCell);

        AddCell(row, $"{p.first_name} {p.last_name}".ToUpper(), "cartera-td cartera-td-name");
        AddCell(row, PositionCodes.GetShort(p.position), "cartera-td cartera-td-pos");
        AddCell(row, $"{p.age}", "cartera-td cartera-td-age");

        var ovrLbl = new Label();
        ovrLbl.AddToClassList("cartera-td");
        ovrLbl.AddToClassList("cartera-td-ovr");
        ovrLbl.text = FogOfWarHelper.GetOvrDisplay(p, _myTeam.id, _scoutedPlayerIds);
        int med = p.GetCalculatedAverage();
        if (med > 84) ovrLbl.AddToClassList("player-ovr--high");
        else if (med >= 70) ovrLbl.AddToClassList("player-ovr--mid");
        else ovrLbl.AddToClassList("player-ovr--low");
        row.Add(ovrLbl);

        AddCell(row, FormatSalary(p.salary), "cartera-td cartera-td-salary");

        var team = DatabaseManager.Instance.GetTeamById(p.team_id);
        AddCell(row, team != null ? team.name : "LIBRE", "cartera-td cartera-td-team");
        AddCell(row, scoutedDay.ToString(), "cartera-td cartera-td-day");

        // Ver perfil
        var viewBtn = new Button();
        viewBtn.AddToClassList("cartera-td cartera-td-action cartera-view-btn");
        viewBtn.text = "VER PERFIL";
        var captured = p;
        viewBtn.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            PlayClick();
            ScreenManager.SelectedPlayerId = captured.id;
            ScreenManager.Instance.GoTo(GameScreen.PlayerProfile);
        });
        if (CursorManager.Instance != null) CursorManager.Instance.RegisterHandCursor(viewBtn);
        row.Add(viewBtn);

        return row;
    }

    void AddCell(VisualElement parent, string text, string cls)
    {
        var label = new Label(text);
        foreach (var c in cls.Split(' '))
            label.AddToClassList(c);
        parent.Add(label);
    }

    // ═══════════ HELPERS ═══════════

    int GetScoutDays(int reputation)
    {
        return reputation switch
        {
            5 => 3, 4 => 5, 3 => 8, 2 => 12, 1 => 16, _ => 20
        };
    }

    string FormatSalary(long amount)
    {
        return amount.ToString("N0").Replace(',', '.') + " $";
    }

    VisualElement _scoutFullOverlay;

    void ShowScoutFullModal()
    {
        if (_scoutFullOverlay != null) return;

        var overlay = new VisualElement();
        overlay.AddToClassList("cartera-modal-overlay");
        overlay.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == overlay) CloseScoutFullModal();
        });

        var box = new VisualElement();
        box.AddToClassList("cartera-modal-box");

        var title = new Label();
        title.AddToClassList("cartera-modal-title");
        title.text = "CARTERA LLENA";
        box.Add(title);

        var msg = new Label();
        msg.AddToClassList("cartera-modal-text");
        msg.text = "No hay más espacio para ojeadores.\nRetira algún jugador de la cartera para poder ojear a más.";
        box.Add(msg);

        var okBtn = new Button();
        okBtn.AddToClassList("cartera-modal-btn");
        okBtn.text = "ENTENDIDO";
        okBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseScoutFullModal(); });
        if (CursorManager.Instance != null) CursorManager.Instance.RegisterHandCursor(okBtn);
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
}
