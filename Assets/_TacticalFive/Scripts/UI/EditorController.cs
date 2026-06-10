using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class EditorController : MonoBehaviour
{
    class CustomDropdown
    {
        public VisualElement Root;
        public Button Trigger;
        public VisualElement List;
        public Label ValueLabel;
        public string Value => ValueLabel?.text ?? "";
    }
    private UIDocument _doc;
    private VisualElement _root;
    private Button _btnAction;
    private Button _btnReset;

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
    private CustomDropdown _teamConferenceDropdown, _teamDivisionDropdown;
    private CustomDropdown _teamReputationDropdown, _teamFacilitiesDropdown, _teamObjectiveDropdown;

    // Player list
    private CustomDropdown _playerTeamFilter, _playerPosFilter;
    private TextField _playerSearch;
    private VisualElement _playerList;
    private PlayerData _selectedPlayer;
    private List<PlayerData> _allPlayers;

    // Player detail fields
    private VisualElement _playerDetail;
    private TextField _playerFName, _playerLName;
    private CustomDropdown _playerPosDropdown, _playerTeamDropdown;
    private TextField _playerAge, _playerNat, _playerHt, _playerWt;
    private TextField _playerPot;
    private TextField _playerSpeed, _playerShooting, _player3pt, _playerPassing;
    private TextField _playerDribbling, _playerDefense, _playerRebounding;
    private TextField _playerAthleticism, _playerIq, _playerSteals, _playerBlocks;
    private TextField _playerSalary, _playerContract;
    private Label _playerOverallDisplay;

    // Image cache (team list logos only)
    private Dictionary<string, Sprite> _logoSprites = new();
    private VisualElement _dropdownOverlay;
    private CustomDropdown _openDropdown;

    void OnDisable()
    {
        DatabaseManager.Instance?.CloseTemplateSession();
    }

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

        // Root-level overlay so dropdown lists render above everything
        _dropdownOverlay = new VisualElement();
        _dropdownOverlay.style.position = Position.Absolute;
        _dropdownOverlay.style.left = 0;
        _dropdownOverlay.style.right = 0;
        _dropdownOverlay.style.top = 0;
        _dropdownOverlay.style.bottom = 0;
        _dropdownOverlay.pickingMode = PickingMode.Ignore;
        _root.Add(_dropdownOverlay);
    }

    void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _btnReset = _root.Q<Button>("BtnReset");
        _btnTeams = _root.Q<Button>("BtnTeams");
        _btnPlayers = _root.Q<Button>("BtnPlayers");
        _teamPanel = _root.Q<VisualElement>("EditorTeamPanel");
        _playerPanel = _root.Q<VisualElement>("EditorPlayerPanel");

        _teamFilter = _root.Q<TextField>("TeamFilter");
        _teamList = _root.Q<VisualElement>("TeamList");
        _teamDetail = _root.Q<VisualElement>("TeamDetail");

        _playerTeamFilter = WrapFilterDropdown("PlayerTeamFilter");
        _playerPosFilter = WrapFilterDropdown("PlayerPosFilter");
        _playerSearch = _root.Q<TextField>("PlayerSearch");
        _playerList = _root.Q<VisualElement>("PlayerList");
        _playerDetail = _root.Q<VisualElement>("PlayerDetail");
    }

    CustomDropdown WrapFilterDropdown(string name)
    {
        var root = _root.Q<VisualElement>(name);
        var trigger = root.Q<Button>(className: "custom-dropdown__trigger");
        var list = root.Q<VisualElement>(className: "custom-dropdown__list");
        var value = root.Q<Label>(className: "custom-dropdown__value");

        var dd = new CustomDropdown
        {
            Root = root,
            Trigger = trigger,
            List = list,
            ValueLabel = value
        };

        trigger.clicked += () =>
        {
            if (_openDropdown == dd)
                CloseAllDropdowns();
            else
                OpenDropdown(dd);
        };

        return dd;
    }

    void SetFilterDropdownItems(CustomDropdown dd, string[] items, int selectedIndex)
    {
        dd.List.Clear();
        for (int i = 0; i < items.Length; i++)
        {
            var text = items[i];
            var item = new Button();
            item.AddToClassList("custom-dropdown__item");
            item.text = text;
            if (i == selectedIndex)
            {
                item.AddToClassList("custom-dropdown__item--selected");
                dd.ValueLabel.text = text;
            }

            var captured = i;
            item.clicked += () =>
            {
                dd.ValueLabel.text = text;
                CloseAllDropdowns();
                foreach (var b in dd.List.Query<Button>(className: "custom-dropdown__item").ToList())
                    b.RemoveFromClassList("custom-dropdown__item--selected");
                item.AddToClassList("custom-dropdown__item--selected");
                FilterPlayerList();
            };

            dd.List.Add(item);
        }
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
            _logoSprites[s.name] = s;
    }

    void LoadData()
    {
        if (DatabaseManager.Instance.Db == null)
        {
            DatabaseManager.Instance.EnsureTemplateDb();
            DatabaseManager.Instance.InitTemplateSession();
        }

        _allTeams = DatabaseManager.Instance.GetAllTeams() ?? new List<TeamData>();
        _allPlayers = DatabaseManager.Instance.Db.Table<PlayerData>().ToList() ?? new List<PlayerData>();

        var teamChoices = new List<string> { "TODOS" };
        teamChoices.AddRange(_allTeams.Select(t => $"{t.abbreviation} - {t.name}"));
        SetFilterDropdownItems(_playerTeamFilter, teamChoices.ToArray(), 0);

        SetFilterDropdownItems(_playerPosFilter, new[] { "TODOS", "PG", "SG", "SF", "PF", "C" }, 0);
    }

    void RegisterCallbacks()
    {
        RegisterNavButtons();

        var actionBtn = _root.Q<Button>("BtnAction");
        if (actionBtn != null)
            actionBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.MainMenu); });
        else
            Debug.LogError("[Editor] BtnAction not found in UXML!");

        _btnReset?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ResetToDefaults(); });
        _btnTeams?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SwitchTab("teams"); });
        _btnPlayers?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SwitchTab("players"); });

        _teamFilter?.RegisterValueChangedCallback(_ => FilterTeamList());
        _playerSearch?.RegisterValueChangedCallback(_ => FilterPlayerList());
    }

    void RegisterNavButtons()
    {
    }

    void Refresh()
    {
        SwitchTab("teams");
    }

    void ResetToDefaults()
    {
        DatabaseManager.Instance.CloseTemplateSession();

        if (System.IO.File.Exists(DatabaseManager.Instance.TemplateDbPath))
            System.IO.File.Delete(DatabaseManager.Instance.TemplateDbPath);

        DatabaseManager.Instance.EnsureTemplateDb();
        DatabaseManager.Instance.InitTemplateSession();
        LoadData();
        Refresh();
        Debug.Log("[Editor] Base de datos restablecida a valores de serie.");
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

        RecalcTeamRatingsFromPlayers(t);

        var columnsRow = new VisualElement();
        columnsRow.AddToClassList("editor-columns-row");

        // ── LEFT COLUMN ──
        var leftCol = new VisualElement();
        leftCol.AddToClassList("editor-column");

        AddSectionTitle(leftCol, "INFORMACIÓN BÁSICA");
        _teamNameInput = AddInput(leftCol, "Nombre", t.name);
        SetLettersOnly(_teamNameInput);
        _teamAbbrInput = AddInput(leftCol, "Abreviatura", t.abbreviation);
        SetAbbreviationInput(_teamAbbrInput);
        _teamCityInput = AddInput(leftCol, "Ciudad", t.city);
        SetLettersOnly(_teamCityInput);
        _teamConferenceDropdown = AddDropdown(leftCol, "Conferencia", new[] { "East", "West" }, t.conference);
        _teamDivisionDropdown = AddDropdown(leftCol, "División", new[] { "Atlantic", "Central", "Southeast", "Northwest", "Pacific", "Southwest" }, t.division);
        AddDivider(leftCol);

        AddSectionTitle(leftCol, "PABELLÓN");
        _teamArenaInput = AddInput(leftCol, "Nombre", t.arena);
        SetLettersOnly(_teamArenaInput);
        _teamCapacityInput = AddInput(leftCol, "Capacidad", t.capacity.ToString());
        SetDigitsOnly(_teamCapacityInput);
        _teamOwnerInput = AddInput(leftCol, "Propietario", t.owner);
        SetLettersOnly(_teamOwnerInput);
        AddDivider(leftCol);

        // ── RIGHT COLUMN ──
        var rightCol = new VisualElement();
        rightCol.AddToClassList("editor-column");

        AddSectionTitle(rightCol, "VALORACIONES");
        _teamAttackInput = AddInput(rightCol, "Ataque", t.attack.ToString());
        _teamAttackInput.isReadOnly = true;
        _teamDefenseInput = AddInput(rightCol, "Defensa", t.defense.ToString());
        _teamDefenseInput.isReadOnly = true;
        _teamOverallDisplay = AddInput(rightCol, "Overall", t.overall.ToString());
        _teamOverallDisplay.isReadOnly = true;
        _teamReputationDropdown = AddDropdown(rightCol, "Reputación", new[] { "1", "2", "3", "4", "5" }, t.reputation.ToString());
        _teamFacilitiesDropdown = AddDropdown(rightCol, "Instalaciones", new[] { "1", "2", "3", "4", "5" }, t.facilities.ToString());
        AddDivider(rightCol);

        AddSectionTitle(rightCol, "FINANZAS");
        _teamBudgetInput = AddInput(rightCol, "Presupuesto", t.budget.ToString());
        SetDigitsOnly(_teamBudgetInput);
        _teamObjectiveDropdown = AddDropdown(rightCol, "Objetivo", new[] { "Campeonato", "Playoffs", "Play-In", "Zona tranquila" }, t.objective);

        columnsRow.Add(leftCol);
        columnsRow.Add(rightCol);
        _teamDetail.Add(columnsRow);

        AddSaveBtn(_teamDetail, "GUARDAR EQUIPO", SaveTeam);
    }

    void RecalcTeamRatingsFromPlayers(TeamData team)
    {
        var teamPlayers = _allPlayers.Where(p => p.team_id == team.id).ToList();
        if (teamPlayers.Count == 0)
        {
            team.attack = 0;
            team.defense = 0;
            team.overall = 0;
            return;
        }

        float attackSum = 0, defenseSum = 0;
        foreach (var p in teamPlayers)
        {
            attackSum += p.shooting + p.three_point + p.passing + p.dribbling + p.speed;
            defenseSum += p.defense + p.rebounding + p.steals + p.blocks + p.athleticism;
        }
        int count = teamPlayers.Count;
        team.attack = Mathf.RoundToInt(attackSum / (5f * count));
        team.defense = Mathf.RoundToInt(defenseSum / (5f * count));
        team.overall = (team.attack + team.defense) / 2;
    }

    void SaveTeam()
    {
        if (_selectedTeam == null) return;
        var t = _selectedTeam;
        t.name = _teamNameInput.value;
        t.abbreviation = _teamAbbrInput.value;
        t.city = _teamCityInput.value;
        t.conference = _teamConferenceDropdown.Value;
        t.division = _teamDivisionDropdown.Value;
        t.arena = _teamArenaInput.value;
        int.TryParse(_teamCapacityInput.value, out int cap); t.capacity = cap;
        t.owner = _teamOwnerInput.value;
        long.TryParse(_teamBudgetInput.value, out long bud); t.budget = bud;
        int.TryParse(_teamReputationDropdown.Value, out int rep); t.reputation = rep;
        int.TryParse(_teamFacilitiesDropdown.Value, out int fac); t.facilities = fac;
        t.objective = _teamObjectiveDropdown.Value;

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

        string teamFilter = _playerTeamFilter?.Value ?? "TODOS";
        string posFilter = _playerPosFilter?.Value ?? "TODOS";
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

        var columnsRow = new VisualElement();
        columnsRow.AddToClassList("editor-columns-row");

        // ── LEFT COLUMN ──
        var leftCol = new VisualElement();
        leftCol.AddToClassList("editor-column");

        AddSectionTitle(leftCol, "INFORMACIÓN BÁSICA");
        _playerFName = AddInput(leftCol, "Nombre", p.first_name);
        SetLettersOnly(_playerFName);
        _playerLName = AddInput(leftCol, "Apellido", p.last_name);
        SetLettersOnly(_playerLName);
        _playerPosDropdown = AddDropdown(leftCol, "Posición", new[] { "PG", "SG", "SF", "PF", "C" }, p.position);
        _playerAge = AddInput(leftCol, "Edad", p.age.ToString());
        SetDigitsOnly(_playerAge, 2);
        _playerNat = AddInput(leftCol, "Nacionalidad", p.nationality);
        SetAbbreviationInput(_playerNat);
        _playerHt = AddInput(leftCol, "Altura (cm)", p.height_cm.ToString());
        SetDigitsOnly(_playerHt, 3);
        _playerWt = AddInput(leftCol, "Peso (kg)", p.weight_kg.ToString());
        SetDigitsOnly(_playerWt, 3);

        var teamChoices = new List<string> { "0 - AGENTE LIBRE" };
        teamChoices.AddRange(_allTeams.Select(t => $"{t.id} - {t.name}"));
        var teamAbbr = _allTeams.Find(t => t.id == p.team_id);
        var teamDefault = p.team_id == 0 ? "0 - AGENTE LIBRE" : $"{p.team_id} - {teamAbbr?.name ?? ""}";
        _playerTeamDropdown = AddDropdown(leftCol, "Equipo", teamChoices.ToArray(), teamDefault);
        AddDivider(leftCol);

        AddSectionTitle(leftCol, "CONTRATO");
        _playerSalary = AddInput(leftCol, "Salario", p.salary.ToString());
        SetDigitsOnly(_playerSalary);
        _playerContract = AddInput(leftCol, "Años", p.contract_years.ToString());
        SetDigitsOnly(_playerContract, 1);

        // ── RIGHT COLUMN ──
        var rightCol = new VisualElement();
        rightCol.AddToClassList("editor-column");

        AddSectionTitle(rightCol, "VALORACIONES");
        _playerOverallDisplay = new Label($"Overall: {p.overall}  /  Potencial: {p.potential}");
        _playerOverallDisplay.AddToClassList("editor-field-display");
        var ovrRow = new VisualElement();
        ovrRow.AddToClassList("editor-field-row");
        ovrRow.style.paddingLeft = 140;
        ovrRow.Add(_playerOverallDisplay);
        rightCol.Add(ovrRow);

        _playerPot = AddInput(rightCol, "Potencial", p.potential.ToString());
        SetDigitsOnly(_playerPot, 2);
        _playerSpeed = AddInput(rightCol, "Velocidad", p.speed.ToString());
        SetDigitsOnly(_playerSpeed, 2);
        _playerShooting = AddInput(rightCol, "Tiro", p.shooting.ToString());
        SetDigitsOnly(_playerShooting, 2);
        _player3pt = AddInput(rightCol, "Triple", p.three_point.ToString());
        SetDigitsOnly(_player3pt, 2);
        _playerPassing = AddInput(rightCol, "Pase", p.passing.ToString());
        SetDigitsOnly(_playerPassing, 2);
        _playerDribbling = AddInput(rightCol, "Regate", p.dribbling.ToString());
        SetDigitsOnly(_playerDribbling, 2);
        _playerDefense = AddInput(rightCol, "Defensa", p.defense.ToString());
        SetDigitsOnly(_playerDefense, 2);
        _playerRebounding = AddInput(rightCol, "Rebote", p.rebounding.ToString());
        SetDigitsOnly(_playerRebounding, 2);
        _playerAthleticism = AddInput(rightCol, "Atletismo", p.athleticism.ToString());
        SetDigitsOnly(_playerAthleticism, 2);
        _playerIq = AddInput(rightCol, "IQ", p.iq.ToString());
        SetDigitsOnly(_playerIq, 2);
        _playerSteals = AddInput(rightCol, "Robos", p.steals.ToString());
        SetDigitsOnly(_playerSteals, 2);
        _playerBlocks = AddInput(rightCol, "Tapones", p.blocks.ToString());
        SetDigitsOnly(_playerBlocks, 2);

        columnsRow.Add(leftCol);
        columnsRow.Add(rightCol);
        _playerDetail.Add(columnsRow);

        AddSaveBtn(_playerDetail, "GUARDAR JUGADOR", SavePlayer);
    }

    void SavePlayer()
    {
        if (_selectedPlayer == null) return;
        var p = _selectedPlayer;

        p.first_name = _playerFName.value;
        p.last_name = _playerLName.value;
        p.position = _playerPosDropdown.Value;
        int.TryParse(_playerAge.value, out int age); p.age = age;
        p.nationality = _playerNat.value;
        int.TryParse(_playerHt.value, out int ht); p.height_cm = ht;
        int.TryParse(_playerWt.value, out int wt); p.weight_kg = wt;

        int.TryParse(_playerTeamDropdown.Value.Split(" - ")[0], out int tid);
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

    void SetLettersOnly(TextField field)
    {
        field.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.character == '\0' || char.IsControl(evt.character)) return;
            if (!char.IsLetter(evt.character) && evt.character != ' ')
            {
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }, TrickleDown.TrickleDown);
        field.RegisterValueChangedCallback(evt =>
        {
            var filtered = new string(evt.newValue.Where(c => char.IsLetter(c) || c == ' ').ToArray());
            if (filtered != evt.newValue)
                field.value = filtered.Length > 0 ? filtered : evt.previousValue;
        });
    }

    void SetDigitsOnly(TextField field, int maxLength = 0)
    {
        field.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.character == '\0' || char.IsControl(evt.character)) return;
            if (!char.IsDigit(evt.character))
            {
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }, TrickleDown.TrickleDown);
        field.RegisterValueChangedCallback(evt =>
        {
            var filtered = new string(evt.newValue.Where(char.IsDigit).ToArray());
            if (maxLength > 0 && filtered.Length > maxLength)
                filtered = filtered.Substring(0, maxLength);
            if (filtered != evt.newValue)
            {
                int cursor = field.cursorIndex;
                field.value = filtered.Length > 0 ? filtered : evt.previousValue;
                field.cursorIndex = cursor < field.text.Length ? cursor : field.text.Length;
            }
        });
    }

    void SetAbbreviationInput(TextField field)
    {
        field.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.character == '\0' || char.IsControl(evt.character)) return;
            if (!char.IsLetter(evt.character))
            {
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }, TrickleDown.TrickleDown);
        field.RegisterValueChangedCallback(evt =>
        {
            var upper = evt.newValue.ToUpper();
            var filtered = new string(upper.Where(char.IsLetter).ToArray());
            if (filtered.Length > 3) filtered = filtered.Substring(0, 3);
            if (filtered != evt.newValue)
            {
                int cursor = field.cursorIndex;
                field.value = filtered.Length > 0 ? filtered : evt.previousValue;
                field.cursorIndex = cursor < field.text.Length ? cursor : field.text.Length;
            }
        });
    }

    CustomDropdown AddDropdown(VisualElement parent, string labelText, string[] choices, string selected)
    {
        var row = new VisualElement();
        row.AddToClassList("editor-field-row");

        var lbl = new Label(labelText);
        lbl.AddToClassList("editor-field-label");
        row.Add(lbl);

        var dd = BuildCustomDropdown(choices, selected);
        row.Add(dd.Root);
        parent.Add(row);
        return dd;
    }

    CustomDropdown BuildCustomDropdown(string[] choices, string selected)
    {
        int idx = System.Array.IndexOf(choices, selected);
        if (idx < 0) idx = 0;

        var root = new VisualElement();
        root.AddToClassList("custom-dropdown");

        var trigger = new Button();
        trigger.AddToClassList("custom-dropdown__trigger");

        var valueLabel = new Label(choices[idx]);
        valueLabel.AddToClassList("custom-dropdown__value");
        trigger.Add(valueLabel);

        var arrow = new Label("▾");
        arrow.AddToClassList("custom-dropdown__arrow");
        trigger.Add(arrow);

        var list = new VisualElement();
        list.AddToClassList("custom-dropdown__list");
        list.style.display = DisplayStyle.None;

        var dd = new CustomDropdown
        {
            Root = root,
            Trigger = trigger,
            List = list,
            ValueLabel = valueLabel
        };

        foreach (var c in choices)
            AddDropdownItem(dd, c);

        trigger.clicked += () =>
        {
            if (_openDropdown == dd)
                CloseAllDropdowns();
            else
                OpenDropdown(dd);
        };

        root.Add(trigger);
        root.Add(list);
        return dd;
    }

    void AddDropdownItem(CustomDropdown dd, string text)
    {
        var item = new Button();
        item.AddToClassList("custom-dropdown__item");
        item.text = text;
        if (text == dd.ValueLabel.text)
            item.AddToClassList("custom-dropdown__item--selected");

        item.clicked += () =>
        {
            dd.ValueLabel.text = text;
            CloseAllDropdowns();
            foreach (var b in dd.List.Query<Button>(className: "custom-dropdown__item").ToList())
                b.RemoveFromClassList("custom-dropdown__item--selected");
            item.AddToClassList("custom-dropdown__item--selected");
            dd.Root.Focus();
        };

        dd.List.Add(item);
    }

    void OpenDropdown(CustomDropdown dd)
    {
        CloseAllDropdowns();
        if (dd == null) return;

        _openDropdown = dd;
        var list = dd.List;

        list.RemoveFromHierarchy();
        _dropdownOverlay.Add(list);

        var triggerBounds = dd.Trigger.worldBound;
        list.style.position = Position.Absolute;

        // Temporarily show the list off-screen to let layout compute its size
        list.style.left = -9999;
        list.style.top = -9999;
        list.style.display = DisplayStyle.Flex;

        _root.schedule.Execute(() =>
        {
            float listHeight = list.resolvedStyle.height;
            float top = triggerBounds.yCenter - listHeight / 2f;
            if (top < 0) top = 0;
            if (top + listHeight > _root.worldBound.height)
                top = _root.worldBound.height - listHeight;
            float left = triggerBounds.xMax + 4;
            if (left + list.resolvedStyle.width > _root.worldBound.width)
                left = triggerBounds.xMin - list.resolvedStyle.width - 4;
            if (left < 0) left = 0;
            list.style.left = left;
            list.style.top = top;
        }).ExecuteLater(30);

        _root.RegisterCallbackOnce<PointerDownEvent>(OnPointerDownAnywhere);
    }

    void CloseAllDropdowns()
    {
        if (_openDropdown != null)
        {
            var list = _openDropdown.List;
            list.RemoveFromHierarchy();
            list.style.position = Position.Relative;
            list.style.left = StyleKeyword.Null;
            list.style.top = StyleKeyword.Null;
            list.style.width = StyleKeyword.Null;
            list.style.display = DisplayStyle.None;
            _openDropdown.Root.Add(list);
            _openDropdown = null;
        }
    }

    void OnPointerDownAnywhere(PointerDownEvent evt)
    {
        if (_openDropdown == null) return;

        var target = evt.target as VisualElement;
        if (target != null && IsChildOf(target, _openDropdown.List))
            return;
        if (target != null && target == _openDropdown.Trigger)
            return;
        if (target != null && IsChildOf(target, _openDropdown.Trigger))
            return;

        CloseAllDropdowns();
    }

    static bool IsChildOf(VisualElement child, VisualElement parent)
    {
        var current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.parent;
        }
        return false;
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
