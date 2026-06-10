using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class EditorController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;
    private Button _btnAction;

    // Tabs
    private Button _btnTeams, _btnPlayers;
    private VisualElement _teamPanel, _playerPanel;

    // Team list
    private TextField _teamFilter;
    private VisualElement _teamList;
    private TeamData _selectedTeam;
    private List<TeamData> _allTeams;

    // Team detail fields
    private VisualElement _teamDetail;
    private TextField _teamNameInput, _teamAbbrInput, _teamCityInput;
    private TextField _teamArenaInput, _teamCapacityInput, _teamOwnerInput;
    private TextField _teamBudgetInput, _teamAttackInput, _teamDefenseInput, _teamOverallDisplay;
    private DropdownField _teamConferenceDropdown, _teamDivisionDropdown;
    private DropdownField _teamReputationDropdown, _teamFacilitiesDropdown, _teamObjectiveDropdown;

    // Logo/jersey
    private VisualElement _logoPreview, _homeJerseyPreview, _awayJerseyPreview;
    private VisualElement _logoGrid, _homeJerseyGrid, _awayJerseyGrid;
    private string _selectedLogo, _selectedHomeJersey, _selectedAwayJersey;

    // Player list
    private DropdownField _playerTeamFilter, _playerPosFilter;
    private TextField _playerSearch;
    private VisualElement _playerList;
    private PlayerData _selectedPlayer;
    private List<PlayerData> _allPlayers;

    // Player detail fields
    private VisualElement _playerDetail;
    private TextField _playerFName, _playerLName;
    private DropdownField _playerPosDropdown, _playerTeamDropdown;
    private TextField _playerAge, _playerNat, _playerHt, _playerWt;
    private TextField _playerPot;
    private TextField _playerSpeed, _playerShooting, _player3pt, _playerPassing;
    private TextField _playerDribbling, _playerDefense, _playerRebounding;
    private TextField _playerAthleticism, _playerIq, _playerSteals, _playerBlocks;
    private TextField _playerSalary, _playerContract;
    private Label _playerOverallDisplay;

    // Image cache
    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _jerseySprites = new();
    private List<string> _availableLogos = new();
    private List<string> _availableJerseys = new();

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
        LoadImages();
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _btnTeams = _root.Q<Button>("BtnTeams");
        _btnPlayers = _root.Q<Button>("BtnPlayers");
        _teamPanel = _root.Q<VisualElement>("EditorTeamPanel");
        _playerPanel = _root.Q<VisualElement>("EditorPlayerPanel");

        _teamFilter = _root.Q<TextField>("TeamFilter");
        _teamList = _root.Q<VisualElement>("TeamList");
        _teamDetail = _root.Q<VisualElement>("TeamDetail");

        _playerTeamFilter = _root.Q<DropdownField>("PlayerTeamFilter");
        _playerPosFilter = _root.Q<DropdownField>("PlayerPosFilter");
        _playerSearch = _root.Q<TextField>("PlayerSearch");
        _playerList = _root.Q<VisualElement>("PlayerList");
        _playerDetail = _root.Q<VisualElement>("PlayerDetail");
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
            {"NavSettingsIcon", "configuracion"}
        };
        foreach (var kv in iconMap)
        {
            var elem = _root.Q<VisualElement>(kv.Key);
            if (elem == null) continue;
            var tex = Resources.Load<Texture2D>($"Icons/{kv.Value}");
            if (tex != null) elem.style.backgroundImage = new StyleBackground(tex);
        }
    }

    void LoadImages()
    {
        foreach (var s in Resources.LoadAll<Sprite>("Teams/Logos/64x64"))
        {
            _logoSprites[s.name] = s;
            _availableLogos.Add(s.name);
        }
        _availableLogos.Sort();

        foreach (var s in Resources.LoadAll<Sprite>("Teams/Jerseys/121x170"))
        {
            _jerseySprites[s.name] = s;
            _availableJerseys.Add(s.name);
        }
        _availableJerseys.Sort();
    }

    void LoadData()
    {
        if (DatabaseManager.Instance.Db == null)
        {
            int slot = GameSaveManager.FindNextAvailableSlot();
            GameSaveManager.CleanupOrphanDb(slot);
            DatabaseManager.Instance.InitSaveSlot(slot);
        }

        _allTeams = DatabaseManager.Instance.GetAllTeams() ?? new List<TeamData>();
        _allPlayers = DatabaseManager.Instance.Db.Table<PlayerData>().ToList() ?? new List<PlayerData>();

        var teamChoices = new List<string> { "TODOS" };
        teamChoices.AddRange(_allTeams.Select(t => $"{t.abbreviation} - {t.name}"));
        _playerTeamFilter.choices = teamChoices;
        _playerTeamFilter.index = 0;

        _playerPosFilter.choices = new List<string> { "TODOS", "PG", "SG", "SF", "PF", "C" };
        _playerPosFilter.index = 0;
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();

        var actionBtn = _root.Q<Button>("BtnAction");
        if (actionBtn != null)
            actionBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.MainMenu); });
        else
            Debug.LogError("[Editor] BtnAction not found in UXML!");

        _btnTeams?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SwitchTab("teams"); });
        _btnPlayers?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SwitchTab("players"); });

        _teamFilter?.RegisterValueChangedCallback(_ => FilterTeamList());
        _playerTeamFilter?.RegisterValueChangedCallback(_ => FilterPlayerList());
        _playerPosFilter?.RegisterValueChangedCallback(_ => FilterPlayerList());
        _playerSearch?.RegisterValueChangedCallback(_ => FilterPlayerList());
    }

    void RegisterNavButtons()
    {
    }

    void Refresh()
    {
        SwitchTab("teams");
    }

    void SwitchTab(string tab)
    {
        _btnTeams.RemoveFromClassList("editor-tab--active");
        _btnPlayers.RemoveFromClassList("editor-tab--active");

        if (tab == "teams")
        {
            _btnTeams.AddToClassList("editor-tab--active");
            _teamPanel.style.display = DisplayStyle.Flex;
            _playerPanel.style.display = DisplayStyle.None;
            BuildTeamList();
        }
        else
        {
            _btnPlayers.AddToClassList("editor-tab--active");
            _teamPanel.style.display = DisplayStyle.None;
            _playerPanel.style.display = DisplayStyle.Flex;
            BuildPlayerList();
        }
    }

    // ══════════════════════════════════════
    // TEAMS
    // ══════════════════════════════════════

    void BuildTeamList()
    {
        _teamList.Clear();
        _selectedTeam = null;

        string filter = _teamFilter?.value?.ToLower() ?? "";
        var filtered = string.IsNullOrEmpty(filter)
            ? _allTeams
            : _allTeams.Where(t =>
                t.name.ToLower().Contains(filter) ||
                t.city.ToLower().Contains(filter) ||
                (t.abbreviation ?? "").ToLower().Contains(filter)).ToList();

        for (int i = 0; i < filtered.Count; i++)
        {
            var team = filtered[i];
            var item = new VisualElement();
            item.AddToClassList("editor-list-item");

            var logo = new VisualElement();
            logo.AddToClassList("editor-list-item-logo");
            if (_logoSprites.TryGetValue(team.logo ?? "", out var sprite))
                logo.style.backgroundImage = new StyleBackground(sprite);
            item.Add(logo);

            var nameLbl = new Label(team.name.ToUpper());
            nameLbl.AddToClassList("editor-list-item-name");
            var subLbl = new Label(team.abbreviation);
            subLbl.AddToClassList("editor-list-item-sub");
            item.Add(nameLbl);
            item.Add(subLbl);

            var captured = team;
            var capturedItem = item;
            item.RegisterCallback<ClickEvent>(_ => SelectTeam(captured, capturedItem));
            _teamList.Add(item);

            if (i == 0)
                SelectTeam(captured, capturedItem);
        }
    }

    void FilterTeamList()
    {
        BuildTeamList();
    }

    void SelectTeam(TeamData team, VisualElement listItem)
    {
        _selectedTeam = team;
        foreach (var child in _teamList.Children())
            child.RemoveFromClassList("editor-list-item--selected");
        if (listItem != null)
            listItem.AddToClassList("editor-list-item--selected");
        BuildTeamDetail();
    }

    void BuildTeamDetail()
    {
        if (_selectedTeam == null) return;
        _teamDetail.Clear();

        var t = _selectedTeam;
        AddDetailTitle(_teamDetail, $"EDITANDO: {t.name.ToUpper()}");

        AddSectionTitle(_teamDetail, "INFORMACIÓN BÁSICA");
        _teamNameInput = AddInput(_teamDetail, "Nombre", t.name);
        _teamAbbrInput = AddInput(_teamDetail, "Abreviatura", t.abbreviation);
        _teamCityInput = AddInput(_teamDetail, "Ciudad", t.city);
        _teamConferenceDropdown = AddDropdown(_teamDetail, "Conferencia", new[] { "East", "West" }, t.conference);
        _teamDivisionDropdown = AddDropdown(_teamDetail, "División", new[] { "Atlantic", "Central", "Southeast", "Northwest", "Pacific", "Southwest" }, t.division);
        AddDivider(_teamDetail);

        AddSectionTitle(_teamDetail, "PABELLÓN");
        _teamArenaInput = AddInput(_teamDetail, "Nombre", t.arena);
        _teamCapacityInput = AddInput(_teamDetail, "Capacidad", t.capacity.ToString());
        _teamOwnerInput = AddInput(_teamDetail, "Propietario", t.owner);
        AddDivider(_teamDetail);

        AddSectionTitle(_teamDetail, "VALORACIONES");
        _teamAttackInput = AddInput(_teamDetail, "Ataque", t.attack.ToString());
        _teamDefenseInput = AddInput(_teamDetail, "Defensa", t.defense.ToString());
        _teamOverallDisplay = AddInput(_teamDetail, "Overall", t.overall.ToString());
        _teamOverallDisplay.isReadOnly = true;
        _teamReputationDropdown = AddDropdown(_teamDetail, "Reputación", new[] { "1", "2", "3", "4", "5" }, t.reputation.ToString());
        _teamFacilitiesDropdown = AddDropdown(_teamDetail, "Instalaciones", new[] { "1", "2", "3", "4", "5" }, t.facilities.ToString());
        AddDivider(_teamDetail);

        AddSectionTitle(_teamDetail, "FINANZAS");
        _teamBudgetInput = AddInput(_teamDetail, "Presupuesto", t.budget.ToString());
        _teamObjectiveDropdown = AddDropdown(_teamDetail, "Objetivo", new[] { "Campeonato", "Playoffs", "Play-In", "Zona tranquila" }, t.objective);
        AddDivider(_teamDetail);

        AddSectionTitle(_teamDetail, "LOGO DEL EQUIPO");
        BuildImagePicker(_teamDetail, t.logo, _availableLogos, _logoSprites,
            ref _logoPreview, ref _logoGrid, v => { _selectedLogo = v; t.logo = v; RecalcTeamOverall(); });

        AddDivider(_teamDetail);
        AddSectionTitle(_teamDetail, "CAMISETA LOCAL");
        BuildImagePicker(_teamDetail, t.jersey_home,
            _availableJerseys.Where(j => j.EndsWith("_home")).ToList(), _jerseySprites,
            ref _homeJerseyPreview, ref _homeJerseyGrid, v => { _selectedHomeJersey = v; t.jersey_home = v; });

        AddDivider(_teamDetail);
        AddSectionTitle(_teamDetail, "CAMISETA VISITANTE");
        BuildImagePicker(_teamDetail, t.jersey_away,
            _availableJerseys.Where(j => j.EndsWith("_away")).ToList(), _jerseySprites,
            ref _awayJerseyPreview, ref _awayJerseyGrid, v => { _selectedAwayJersey = v; t.jersey_away = v; });

        AddDivider(_teamDetail);
        AddSaveBtn(_teamDetail, "GUARDAR EQUIPO", SaveTeam);
    }

    void RecalcTeamOverall()
    {
        if (_selectedTeam == null) return;
        int.TryParse(_teamAttackInput?.value, out int a);
        int.TryParse(_teamDefenseInput?.value, out int d);
        int ov = (a + d) / 2;
        _selectedTeam.overall = ov;
        if (_teamOverallDisplay != null)
            _teamOverallDisplay.value = ov.ToString();
    }

    void SaveTeam()
    {
        if (_selectedTeam == null) return;
        var t = _selectedTeam;
        t.name = _teamNameInput.value;
        t.abbreviation = _teamAbbrInput.value;
        t.city = _teamCityInput.value;
        t.conference = _teamConferenceDropdown.value;
        t.division = _teamDivisionDropdown.value;
        t.arena = _teamArenaInput.value;
        int.TryParse(_teamCapacityInput.value, out int cap); t.capacity = cap;
        t.owner = _teamOwnerInput.value;
        int.TryParse(_teamAttackInput.value, out int atk); t.attack = atk;
        int.TryParse(_teamDefenseInput.value, out int def); t.defense = def;
        t.overall = (atk + def) / 2;
        long.TryParse(_teamBudgetInput.value, out long bud); t.budget = bud;
        int.TryParse(_teamReputationDropdown.value, out int rep); t.reputation = rep;
        int.TryParse(_teamFacilitiesDropdown.value, out int fac); t.facilities = fac;
        t.objective = _teamObjectiveDropdown.value;
        if (!string.IsNullOrEmpty(_selectedLogo)) t.logo = _selectedLogo;
        if (!string.IsNullOrEmpty(_selectedHomeJersey)) t.jersey_home = _selectedHomeJersey;
        if (!string.IsNullOrEmpty(_selectedAwayJersey)) t.jersey_away = _selectedAwayJersey;

        DatabaseManager.Instance.UpdateTeam(t);
        Debug.Log($"[Editor] Equipo guardado: {t.name}");
    }

    // ══════════════════════════════════════
    // PLAYERS
    // ══════════════════════════════════════

    void BuildPlayerList()
    {
        _playerList.Clear();
        _selectedPlayer = null;

        string teamFilter = _playerTeamFilter?.value ?? "TODOS";
        string posFilter = _playerPosFilter?.value ?? "TODOS";
        string search = _playerSearch?.value?.ToLower() ?? "";

        int? teamId = null;
        if (teamFilter != "TODOS")
        {
            var parts = teamFilter.Split(" - ");
            var team = _allTeams.FirstOrDefault(t => t.abbreviation == parts[0]);
            if (team != null) teamId = team.id;
        }

        var query = _allPlayers.AsEnumerable();
        if (teamId.HasValue) query = query.Where(p => p.team_id == teamId.Value);
        if (posFilter != "TODOS") query = query.Where(p => p.position == posFilter);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => $"{p.first_name} {p.last_name}".ToLower().Contains(search));

        var results = query.OrderBy(p => _allTeams.Find(t => t.id == p.team_id)?.abbreviation ?? "ZZZ")
                          .ThenBy(p => p.last_name).ToList();

        for (int i = 0; i < results.Count; i++)
        {
            var player = results[i];
            var item = new VisualElement();
            item.AddToClassList("editor-list-item");

            var abbr = _allTeams.Find(t => t.id == player.team_id)?.abbreviation ?? "FA";
            var nameLbl = new Label($"{player.first_name} {player.last_name}");
            nameLbl.AddToClassList("editor-list-item-name");
            var subLbl = new Label($"{abbr} | {player.position} | {player.overall}");
            subLbl.AddToClassList("editor-list-item-sub");
            item.Add(nameLbl);
            item.Add(subLbl);

            var captured = player;
            var capturedItem = item;
            item.RegisterCallback<ClickEvent>(_ => SelectPlayer(captured, capturedItem));
            _playerList.Add(item);

            if (i == 0)
                SelectPlayer(captured, capturedItem);
        }
    }

    void FilterPlayerList()
    {
        BuildPlayerList();
    }

    void SelectPlayer(PlayerData player, VisualElement listItem)
    {
        _selectedPlayer = player;
        foreach (var child in _playerList.Children())
            child.RemoveFromClassList("editor-list-item--selected");
        if (listItem != null)
            listItem.AddToClassList("editor-list-item--selected");
        BuildPlayerDetail();
    }

    void BuildPlayerDetail()
    {
        if (_selectedPlayer == null) return;
        _playerDetail.Clear();

        var p = _selectedPlayer;
        AddDetailTitle(_playerDetail, $"EDITANDO: {p.first_name} {p.last_name}");

        AddSectionTitle(_playerDetail, "INFORMACIÓN BÁSICA");
        _playerFName = AddInput(_playerDetail, "Nombre", p.first_name);
        _playerLName = AddInput(_playerDetail, "Apellido", p.last_name);
        _playerPosDropdown = AddDropdown(_playerDetail, "Posición", new[] { "PG", "SG", "SF", "PF", "C" }, p.position);
        _playerAge = AddInput(_playerDetail, "Edad", p.age.ToString());
        _playerNat = AddInput(_playerDetail, "Nacionalidad", p.nationality);
        _playerHt = AddInput(_playerDetail, "Altura (cm)", p.height_cm.ToString());
        _playerWt = AddInput(_playerDetail, "Peso (kg)", p.weight_kg.ToString());

        var teamChoices = new List<string> { "0 - AGENTE LIBRE" };
        teamChoices.AddRange(_allTeams.Select(t => $"{t.id} - {t.name}"));
        var teamAbbr = _allTeams.Find(t => t.id == p.team_id);
        var teamDefault = p.team_id == 0 ? "0 - AGENTE LIBRE" : $"{p.team_id} - {teamAbbr?.name ?? ""}";
        _playerTeamDropdown = AddDropdown(_playerDetail, "Equipo", teamChoices.ToArray(), teamDefault);
        AddDivider(_playerDetail);

        AddSectionTitle(_playerDetail, "VALORACIONES");
        _playerOverallDisplay = new Label($"Overall: {p.overall}  /  Potencial: {p.potential}");
        _playerOverallDisplay.AddToClassList("editor-field-display");
        var ovrRow = new VisualElement();
        ovrRow.AddToClassList("editor-field-row");
        ovrRow.style.paddingLeft = 140;
        ovrRow.Add(_playerOverallDisplay);
        _playerDetail.Add(ovrRow);

        _playerPot = AddInput(_playerDetail, "Potencial", p.potential.ToString());
        _playerSpeed = AddInput(_playerDetail, "Velocidad", p.speed.ToString());
        _playerShooting = AddInput(_playerDetail, "Tiro", p.shooting.ToString());
        _player3pt = AddInput(_playerDetail, "Triple", p.three_point.ToString());
        _playerPassing = AddInput(_playerDetail, "Pase", p.passing.ToString());
        _playerDribbling = AddInput(_playerDetail, "Regate", p.dribbling.ToString());
        _playerDefense = AddInput(_playerDetail, "Defensa", p.defense.ToString());
        _playerRebounding = AddInput(_playerDetail, "Rebote", p.rebounding.ToString());
        _playerAthleticism = AddInput(_playerDetail, "Atletismo", p.athleticism.ToString());
        _playerIq = AddInput(_playerDetail, "IQ", p.iq.ToString());
        _playerSteals = AddInput(_playerDetail, "Robos", p.steals.ToString());
        _playerBlocks = AddInput(_playerDetail, "Tapones", p.blocks.ToString());
        AddDivider(_playerDetail);

        AddSectionTitle(_playerDetail, "CONTRATO");
        _playerSalary = AddInput(_playerDetail, "Salario", p.salary.ToString());
        _playerContract = AddInput(_playerDetail, "Años", p.contract_years.ToString());
        AddDivider(_playerDetail);

        AddSaveBtn(_playerDetail, "GUARDAR JUGADOR", SavePlayer);
    }

    void SavePlayer()
    {
        if (_selectedPlayer == null) return;
        var p = _selectedPlayer;

        p.first_name = _playerFName.value;
        p.last_name = _playerLName.value;
        p.position = _playerPosDropdown.value;
        int.TryParse(_playerAge.value, out int age); p.age = age;
        p.nationality = _playerNat.value;
        int.TryParse(_playerHt.value, out int ht); p.height_cm = ht;
        int.TryParse(_playerWt.value, out int wt); p.weight_kg = wt;

        int.TryParse(_playerTeamDropdown.value.Split(" - ")[0], out int tid);
        p.team_id = tid;

        int.TryParse(_playerPot.value, out int pot); p.potential = pot;

        int.TryParse(_playerSpeed.value, out int spd); p.speed = Mathf.Clamp(spd, 0, 99);
        int.TryParse(_playerShooting.value, out int sho); p.shooting = Mathf.Clamp(sho, 0, 99);
        int.TryParse(_player3pt.value, out int thr); p.three_point = Mathf.Clamp(thr, 0, 99);
        int.TryParse(_playerPassing.value, out int pas); p.passing = Mathf.Clamp(pas, 0, 99);
        int.TryParse(_playerDribbling.value, out int dri); p.dribbling = Mathf.Clamp(dri, 0, 99);
        int.TryParse(_playerDefense.value, out int def); p.defense = Mathf.Clamp(def, 0, 99);
        int.TryParse(_playerRebounding.value, out int reb); p.rebounding = Mathf.Clamp(reb, 0, 99);
        int.TryParse(_playerAthleticism.value, out int ath); p.athleticism = Mathf.Clamp(ath, 0, 99);
        int.TryParse(_playerIq.value, out int iq); p.iq = Mathf.Clamp(iq, 0, 99);
        int.TryParse(_playerSteals.value, out int stl); p.steals = Mathf.Clamp(stl, 0, 99);
        int.TryParse(_playerBlocks.value, out int blk); p.blocks = Mathf.Clamp(blk, 0, 99);

        int sum = p.speed + p.shooting + p.three_point + p.passing + p.dribbling +
                  p.defense + p.rebounding + p.athleticism + p.iq + p.steals + p.blocks;
        p.overall = (int)System.Math.Round(sum / 11f);
        if (p.overall > p.potential) p.overall = p.potential;

        long.TryParse(_playerSalary.value, out long sal); p.salary = sal;
        int.TryParse(_playerContract.value, out int yrs); p.contract_years = yrs;

        DatabaseManager.Instance.UpdatePlayer(p);
        Debug.Log($"[Editor] Jugador guardado: {p.first_name} {p.last_name}");
        BuildPlayerList();
    }

    // ══════════════════════════════════════
    // IMAGE PICKER
    // ══════════════════════════════════════

    void BuildImagePicker(VisualElement parent, string currentVal, List<string> options,
        Dictionary<string, Sprite> sprites, ref VisualElement previewRef, ref VisualElement gridRef,
        System.Action<string> onSelect)
    {
        var row = new VisualElement();
        row.AddToClassList("editor-image-row");

        previewRef = new VisualElement();
        previewRef.AddToClassList("editor-image-preview");
        if (!string.IsNullOrEmpty(currentVal) && sprites.TryGetValue(currentVal, out var ps))
            previewRef.style.backgroundImage = new StyleBackground(ps);
        row.Add(previewRef);

        gridRef = new VisualElement();
        gridRef.AddToClassList("editor-image-grid");

        var localGrid = gridRef;
        var localPreview = previewRef;

        foreach (var img in options)
        {
            var opt = new VisualElement();
            opt.AddToClassList("editor-image-option");
            if (sprites.TryGetValue(img, out var spr))
                opt.style.backgroundImage = new StyleBackground(spr);
            if (img == currentVal)
                opt.AddToClassList("editor-image-option--selected");

            var captured = img;
            opt.RegisterCallback<ClickEvent>(_ =>
            {
                foreach (var c in localGrid.Children())
                    c.RemoveFromClassList("editor-image-option--selected");
                opt.AddToClassList("editor-image-option--selected");
                if (sprites.TryGetValue(captured, out var sel))
                    localPreview.style.backgroundImage = new StyleBackground(sel);
                onSelect?.Invoke(captured);
            });
            localGrid.Add(opt);
        }
        row.Add(localGrid);
        parent.Add(row);
    }

    // ══════════════════════════════════════
    // UI HELPERS
    // ══════════════════════════════════════

    void AddDetailTitle(VisualElement parent, string text)
    {
        var lbl = new Label(text);
        lbl.AddToClassList("editor-detail-title");
        parent.Add(lbl);
    }

    void AddSectionTitle(VisualElement parent, string text)
    {
        var lbl = new Label(text);
        lbl.AddToClassList("editor-section-title");
        parent.Add(lbl);
    }

    void AddDivider(VisualElement parent)
    {
        var div = new VisualElement();
        div.AddToClassList("editor-section-divider");
        parent.Add(div);
    }

    TextField AddInput(VisualElement parent, string labelText, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("editor-field-row");

        var lbl = new Label(labelText);
        lbl.AddToClassList("editor-field-label");

        var field = new TextField { value = value };
        field.AddToClassList("editor-field-input");

        row.Add(lbl);
        row.Add(field);
        parent.Add(row);
        return field;
    }

    DropdownField AddDropdown(VisualElement parent, string labelText, string[] choices, string selected)
    {
        int idx = System.Array.IndexOf(choices, selected);
        if (idx < 0) idx = 0;

        var row = new VisualElement();
        row.AddToClassList("editor-field-row");

        var lbl = new Label(labelText);
        lbl.AddToClassList("editor-field-label");

        var field = new DropdownField();
        field.choices = new List<string>(choices);
        field.index = idx;
        field.AddToClassList("editor-field-dropdown");

        row.Add(lbl);
        row.Add(field);
        parent.Add(row);
        return field;
    }

    void AddSaveBtn(VisualElement parent, string text, System.Action action)
    {
        var btn = new Button();
        btn.AddToClassList("editor-save-btn");
        btn.text = text;
        btn.RegisterCallback<ClickEvent>(_ => { PlayClick(); action(); });
        parent.Add(btn);
    }

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
