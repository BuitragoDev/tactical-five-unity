using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class SelectTeamController : UIScreenController
{
    // Header
    private Button _btnBack;
    private Button _btnContinue;
    private Label _headerMode;

    // Manager name
    private TextField _managerInput;

    // Tabs
    private Button _tabAll;
    private Button _tabEast;
    private Button _tabWest;

    // Grid
    private VisualElement _teamsGrid;
    private ScrollView _gridScroll;

    // Detail
    private ScrollView _detailScroll;
    private VisualElement _detailPlaceholder;
    private VisualElement _detailPanel;
    private VisualElement _detailLogo;
    private Label _detailTeamName;
    private Label _detailCity;
    private Label _detailConferenceVal;
    private Label _detailDivision;
    private Label _detailOwner;
    private Label _detailArena;
    private Label _detailCapacity;
    private Label _detailBudget;
    private Label _detailSalaryMargin;
    private VisualElement _detailReputation;
    private VisualElement _detailFacilities;
    private Label _detailObjective;
    private Label _detailAttack;
    private Label _detailDefense;
    private Label _detailOverall;
    private Button _btnShowSquad;

    // Modal
    private VisualElement _squadModalOverlay;
    private VisualElement _squadModalLogo;
    private Label _squadModalTeamName;
    private Label _squadModalAvg;
    private VisualElement _squadColumnLeft;
    private VisualElement _squadColumnRight;
    private Button _btnCloseSquad;

    private Dictionary<string, Texture2D> _flagTextures = new();

    // Estado
    private List<TeamData> _allTeams;
    private List<TeamData> _worstTeams;
    private TeamData _selectedTeam;
    private List<PlayerData> _cachedPlayers;

    // Sprites
    private Dictionary<string, Sprite> _logoSprites = new();

    protected override void CacheReferences()
    {
        _btnBack = _root.Q<Button>("BtnBack");
        _btnContinue = _root.Q<Button>("BtnContinue");
        _headerMode = _root.Q<Label>("HeaderMode");
        _managerInput = _root.Q<TextField>("ManagerNameInput");
        _tabAll = _root.Q<Button>("TabAll");
        _tabEast = _root.Q<Button>("TabEast");
        _tabWest = _root.Q<Button>("TabWest");
        _gridScroll = _root.Q<ScrollView>("GridScroll");
        _teamsGrid = _root.Q<VisualElement>("TeamsGrid");
        _detailScroll = _root.Q<ScrollView>("DetailScroll");
        _detailPlaceholder = _root.Q<VisualElement>("DetailPlaceholder");
        _detailPanel = _root.Q<VisualElement>("DetailPanel");
        _detailLogo = _root.Q<VisualElement>("DetailLogo");
        _detailTeamName = _root.Q<Label>("DetailTeamName");
        _detailCity = _root.Q<Label>("DetailCity");
        _detailConferenceVal = _root.Q<Label>("DetailConferenceVal");
        _detailDivision = _root.Q<Label>("DetailDivision");
        _detailOwner = _root.Q<Label>("DetailOwner");
        _detailArena = _root.Q<Label>("DetailArena");
        _detailCapacity = _root.Q<Label>("DetailCapacity");
        _detailBudget = _root.Q<Label>("DetailBudget");
        _detailSalaryMargin = _root.Q<Label>("DetailSalaryMargin");
        _detailReputation = _root.Q<VisualElement>("DetailReputation");
        _detailFacilities = _root.Q<VisualElement>("DetailFacilities");
        _detailObjective = _root.Q<Label>("DetailObjective");
        _detailAttack = _root.Q<Label>("DetailAttack");
        _detailDefense = _root.Q<Label>("DetailDefense");
        _detailOverall = _root.Q<Label>("DetailOverall");
        _btnShowSquad = _root.Q<Button>("BtnShowSquad");

        _squadModalOverlay = _root.Q<VisualElement>("SquadModalOverlay");
        _squadModalLogo = _root.Q<VisualElement>("SquadModalLogo");
        _squadModalTeamName = _root.Q<Label>("SquadModalTeamName");
        _squadModalAvg = _root.Q<Label>("SquadModalAvg");
        _squadColumnLeft = _root.Q<VisualElement>("SquadColumnLeft");
        _squadColumnRight = _root.Q<VisualElement>("SquadColumnRight");
        _btnCloseSquad = _root.Q<Button>("BtnCloseSquad");
    }

    void SetupScrollViews()
    {
        // Forzar el contenedor del grid a ser wrap
        if (_gridScroll != null)
        {
            _gridScroll.contentContainer.style.flexDirection = FlexDirection.Row;
            _gridScroll.contentContainer.style.flexWrap = Wrap.Wrap;
            _gridScroll.contentContainer.style.alignContent = Align.FlexStart;
        }

        // Forzar el contenedor del detail a ser columna
        if (_detailScroll != null)
        {
            _detailScroll.contentContainer.style.flexDirection = FlexDirection.Column;
            _detailScroll.contentContainer.style.flexShrink = 0;
        }
    }

    protected override void LoadData()
    {
        base.LoadData();

        LoadSprites();
        LoadTeams();
        SetupScrollViews();
    }

    protected override void RegisterCallbacks()
    {
        _btnBack?.RegisterCallback<ClickEvent>(_ =>
            { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.MainMenu); });
        _btnContinue?.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnContinue(); });
        _tabAll?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowFilter("All"); });
        _tabEast?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowFilter("East"); });
        _tabWest?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowFilter("West"); });

        _btnShowSquad?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowSquadModal(); });
        _btnCloseSquad?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseSquadModal(); });
        _squadModalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _squadModalOverlay)
            { PlayClick(); CloseSquadModal(); }
        });

        // Listener del nombre del manager
        _managerInput?.RegisterValueChangedCallback(_ => ValidateContinue());

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnBack);
            CursorManager.Instance.RegisterHandCursor(_btnContinue);
            CursorManager.Instance.RegisterHandCursor(_tabAll);
            CursorManager.Instance.RegisterHandCursor(_tabEast);
            CursorManager.Instance.RegisterHandCursor(_tabWest);
            CursorManager.Instance.RegisterHandCursor(_btnShowSquad);
            CursorManager.Instance.RegisterHandCursor(_btnCloseSquad);
        }
    }

    protected override void Refresh()
    {
        // Modo de juego
        var mode = ScreenManager.Instance.CurrentMode;
        _headerMode.text = mode == GameMode.ProManager ? "PROMANAGER" : "MANAGER";

        // Estado inicial
        _detailScroll.style.display = DisplayStyle.None;
        _detailPlaceholder.style.display = DisplayStyle.Flex;
        _btnContinue.SetEnabled(false);

        ShowFilter("All");
    }

    void ValidateContinue()
    {
        bool hasTeam = _selectedTeam != null;
        bool hasName = !string.IsNullOrWhiteSpace(_managerInput?.value);
        _btnContinue.SetEnabled(hasTeam && hasName);
    }

    void LoadSprites()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos/120x120/");
        Debug.Log($"[SelectTeam] Logos cargados: {logos.Length}");
        foreach (var s in logos)
            _logoSprites[s.name] = s;

        var flags = Resources.LoadAll<Texture2D>("Flags/");
        Debug.Log($"[SelectTeam] Flags cargados: {flags.Length}");
        foreach (var f in flags)
            _flagTextures[f.name] = f;
    }

    void LoadTeams()
    {
        _allTeams = DatabaseManager.Instance.GetAllTeams();
        _worstTeams = DatabaseManager.Instance.GetWorstTeams(5);
    }

    void ShowFilter(string filter)
    {
        SetTabActive(_tabAll, filter == "All");
        SetTabActive(_tabEast, filter == "East");
        SetTabActive(_tabWest, filter == "West");

        List<TeamData> filtered = filter == "All"
            ? _allTeams
            : _allTeams.FindAll(t => t.conference == filter);

        BuildGrid(filtered);
    }

    void SetTabActive(Button tab, bool active)
    {
        if (active)
        {
            if (!tab.ClassListContains("tab-btn--active"))
                tab.AddToClassList("tab-btn--active");
        }
        else
        {
            tab.RemoveFromClassList("tab-btn--active");
        }
    }

    void BuildGrid(List<TeamData> teams)
    {
        _teamsGrid.Clear();

        bool isProManager = ScreenManager.Instance.CurrentMode == GameMode.ProManager;

        foreach (var team in teams)
        {
            bool disabled = isProManager && !_worstTeams.Exists(w => w.id == team.id);
            _teamsGrid.Add(CreateTeamItem(team, disabled));
        }
    }

    VisualElement CreateTeamItem(TeamData team, bool disabled)
    {
        var item = new VisualElement();
        item.AddToClassList("team-item");

        if (disabled)
        {
            item.AddToClassList("team-item--disabled");
        }
        else
        {
            item.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnTeamSelected(team, item); });
            if (CursorManager.Instance != null)
            {
                item.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
                item.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
            }
        }

        var logo = new VisualElement();
        logo.AddToClassList("team-logo");
        if (_logoSprites.TryGetValue(team.logo, out var sprite))
            logo.style.backgroundImage = new StyleBackground(sprite);

        item.Add(logo);
        return item;
    }

    void OnTeamSelected(TeamData team, VisualElement item)
    {
        _selectedTeam = team;

        _teamsGrid.Query<VisualElement>(className: "team-item--selected")
                  .ForEach(e => e.RemoveFromClassList("team-item--selected"));
        item.AddToClassList("team-item--selected");

        ShowTeamDetail(team);
        ValidateContinue();
    }

    void ShowTeamDetail(TeamData team)
    {
        _detailPlaceholder.style.display = DisplayStyle.None;
        _detailScroll.style.display = DisplayStyle.Flex;

        // Logo
        if (_logoSprites.TryGetValue(team.logo, out var logoSprite))
            _detailLogo.style.backgroundImage = new StyleBackground(logoSprite);

        // Textos
        _detailTeamName.text = team.name.ToUpper();
        _detailCity.text = team.city;
        _detailConferenceVal.text = team.conference == "East" ? "Este" : "Oeste";
        _detailDivision.text = team.division;
        _detailOwner.text = team.owner;
        _detailArena.text = team.arena;
        _detailCapacity.text = $"{team.capacity:N0}";
        _detailBudget.text = $"${team.budget / 1_000_000}M";

        // Margen salarial real = Cap - suma de salarios de jugadores del equipo
        _cachedPlayers = DatabaseManager.Instance.GetPlayersByTeam(team.id);
        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
        long totalPayroll = _cachedPlayers.Sum(p => p.salary);
        long margin = salaryCap - totalPayroll;

        _detailSalaryMargin.text = margin >= 0
            ? $"+${margin / 1_000_000}M"
            : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
        _detailSalaryMargin.style.color = margin >= 0
            ? new StyleColor(new Color(0.15f, 0.68f, 0.38f))
            : new StyleColor(new Color(0.75f, 0.22f, 0.17f));

        // Stats — calculated from player attributes
        int atkSum = 0, defSum = 0, ovrSum = 0;
        foreach (var p in _cachedPlayers)
        {
            atkSum += (p.speed + p.shooting + p.three_point + p.passing + p.dribbling + p.athleticism) / 6;
            defSum += (p.defense + p.rebounding + p.steals + p.blocks + p.iq) / 5;
            ovrSum += p.GetCalculatedAverage();
        }
        int count = _cachedPlayers.Count;
        _detailAttack.text = count > 0 ? Mathf.RoundToInt((float)atkSum / count).ToString() : "-";
        _detailDefense.text = count > 0 ? Mathf.RoundToInt((float)defSum / count).ToString() : "-";
        _detailOverall.text = count > 0 ? Mathf.RoundToInt((float)ovrSum / count).ToString() : "-";

        // Estrellas
        BuildStars(_detailReputation, team.reputation);
        BuildStars(_detailFacilities, team.facilities);

        // Objetivo temporada
        _detailObjective.text = string.IsNullOrEmpty(team.objective) ? "—" : team.objective;
    }

    void BuildStars(VisualElement container, int filled)
    {
        container.Clear();
        for (int i = 1; i <= 5; i++)
        {
            var star = new VisualElement();
            star.AddToClassList("star");
            if (i <= filled)
                star.AddToClassList("star--filled");
            container.Add(star);
        }
    }

    void ShowSquadModal()
    {
        if (_selectedTeam == null || _cachedPlayers == null) return;

        if (_logoSprites.TryGetValue(_selectedTeam.logo, out var logoSprite))
            _squadModalLogo.style.backgroundImage = new StyleBackground(logoSprite);

        _squadModalTeamName.text = $"PLANTILLA DE LOS {_selectedTeam.name.ToUpper()}";

        double teamSum = 0;
        foreach (var p in _cachedPlayers)
            teamSum += p.GetCalculatedAverage();
        double teamAvg = _cachedPlayers.Count > 0 ? teamSum / _cachedPlayers.Count : 0;
        _squadModalAvg.text = Mathf.RoundToInt((float)teamAvg).ToString();

        // Sort by position order: PG (0), SG (1), SF (2), PF (3), C (4)
        var sorted = _cachedPlayers.OrderBy(p => GetPositionOrder(p.position))
                                   .ThenByDescending(p => p.overall).ToList();

        _squadColumnLeft.Clear();
        _squadColumnRight.Clear();

        _squadColumnLeft.Add(MakeColumnHeader());
        _squadColumnRight.Add(MakeColumnHeader());

        int mid = (sorted.Count + 1) / 2;
        for (int i = 0; i < sorted.Count; i++)
        {
            var player = sorted[i];
            var column = i < mid ? _squadColumnLeft : _squadColumnRight;
            column.Add(CreatePlayerRow(player));
        }

        _squadModalOverlay.style.display = DisplayStyle.Flex;
    }

    VisualElement CreatePlayerRow(PlayerData player)
    {
        var row = new VisualElement();
        row.AddToClassList("modal-player-row");

        var nameLbl = new Label();
        nameLbl.AddToClassList("modal-player-name");
        nameLbl.text = $"{player.first_name} {player.last_name}";
        row.Add(nameLbl);

        var posLbl = new Label();
        posLbl.AddToClassList("modal-player-pos");
        posLbl.text = GetPositionDisplay(player.position);
        row.Add(posLbl);

        var flag = new VisualElement();
        flag.AddToClassList("modal-player-flag");
        if (_flagTextures.TryGetValue(player.nationality ?? "", out var flagTex))
            flag.style.backgroundImage = new StyleBackground(flagTex);
        else if (_flagTextures.TryGetValue("default", out var defaultTex))
            flag.style.backgroundImage = new StyleBackground(defaultTex);
        row.Add(flag);

        var ageLbl = new Label();
        ageLbl.AddToClassList("modal-player-age");
        ageLbl.text = player.age.ToString();
        row.Add(ageLbl);

        var avgLbl = new Label();
        avgLbl.AddToClassList("modal-player-avg");
        int ovrVal = player.GetCalculatedAverage();
        avgLbl.text = ovrVal.ToString();
        if (ovrVal > 84)
            avgLbl.AddToClassList("modal-player-avg--high");
        else if (ovrVal >= 70)
            avgLbl.AddToClassList("modal-player-avg--mid");
        else
            avgLbl.AddToClassList("modal-player-avg--low");
        row.Add(avgLbl);

        return row;
    }

    VisualElement MakeColumnHeader()
    {
        var header = new VisualElement();
        header.AddToClassList("modal-header-row");

        var nameLbl = new Label();
        nameLbl.AddToClassList("modal-header-label");
        nameLbl.style.flexGrow = 1;
        nameLbl.style.unityTextAlign = TextAnchor.MiddleLeft;
        nameLbl.text = "NOMBRE";
        header.Add(nameLbl);

        var posLbl = new Label();
        posLbl.AddToClassList("modal-header-label");
        posLbl.style.width = 70;
        posLbl.text = "POS";
        header.Add(posLbl);

        var nacLbl = new Label();
        nacLbl.AddToClassList("modal-header-label");
        nacLbl.style.width = 36;
        nacLbl.text = "NAC";
        header.Add(nacLbl);

        var ageLbl = new Label();
        ageLbl.AddToClassList("modal-header-label");
        ageLbl.style.width = 30;
        ageLbl.text = "EDAD";
        header.Add(ageLbl);

        var avgLbl = new Label();
        avgLbl.AddToClassList("modal-header-label");
        avgLbl.style.width = 40;
        avgLbl.text = "MED";
        header.Add(avgLbl);

        return header;
    }

    string GetPositionDisplay(string pos)
    {
        return PositionCodes.GetName(pos);
    }

    int GetPositionOrder(string pos)
    {
        return System.Array.IndexOf(PositionCodes.Order, pos);
    }

    void CloseSquadModal()
    {
        _squadModalOverlay.style.display = DisplayStyle.None;
    }

    void OnContinue()
    {
        if (_selectedTeam == null) return;

        string managerName = _managerInput?.value;
        if (string.IsNullOrWhiteSpace(managerName))
            managerName = "Manager";

        int activeSlot = DatabaseManager.Instance.ActiveSaveSlot;

        // Borrar managers previos de intentos abandonados
        DatabaseManager.Instance.ClearAllManagers();

        var manager = new ManagerData
        {
            name = managerName,
            team_id = _selectedTeam.id,
            game_mode = ScreenManager.Instance.CurrentMode == GameMode.ProManager
                        ? "promanager" : "manager",
            trust = 50,
            morale = 50,
            fan_confidence = 50
        };

        DatabaseManager.Instance.SaveManager(manager);
        Debug.Log($"[SelectTeam] Manager '{managerName}' → {_selectedTeam.name} (slot {activeSlot})");

        // NOTA: los metadatos de la partida NO se guardan aquí.
        // La partida solo se "crea" oficialmente al pulsar Continuar en Preseason.

        ScreenManager.Instance.GoTo(GameScreen.Preseason);
    }
}