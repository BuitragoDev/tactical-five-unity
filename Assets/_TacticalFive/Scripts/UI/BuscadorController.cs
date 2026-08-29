using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class BuscadorController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Buscador;

    private VisualElement _tableHeader;
    private VisualElement _tableBody;
    private Label _panelTitle;
    private VisualElement _teamGrid;
    private readonly Dictionary<Button, int> _teamBtnMap = new();
    private Button _previousPageBtn;
    private Button _nextPageBtn;
    private Button _firstPageBtn;
    private Button _lastPageBtn;
    private Label _pageLabel;

    private readonly List<Button> _posBtns = new();
    private readonly List<Button> _ageBtns = new();
    private readonly List<Button> _ovrBtns = new();

    private List<TeamData> _allTeams = new();
    private List<PlayerData> _allPlayers = new();
    private List<PlayerData> _filteredPlayers = new();
    private HashSet<int> _scoutedIds = new();

    private string _position = "TODOS";
    private int _ageBand = 0;   // 0=todas, 1=<=20, 2=21-24, 3=25-29, 4=30+
    private int _teamId = 0;    // 0 = todos los equipos
    private int _minOvr = 0;    // 0 = todas
    private bool _onlyFA = false;
    private string _sortColumn = "media";
    private bool _sortDescending = true;

    private const int PAGE_SIZE = 12;
    private int _currentPage;

    protected override void CacheReferences()
    {
        _tableHeader = _root.Q<VisualElement>("BuscadorTableHeader");
        _tableBody = _root.Q<VisualElement>("BuscadorTableBody");
        _panelTitle = _root.Q<Label>("PanelTitle");
        _teamGrid = _root.Q<VisualElement>("BuscadorTeamGrid");
        _previousPageBtn = _root.Q<Button>("BtnPreviousPage");
        _nextPageBtn = _root.Q<Button>("BtnNextPage");
        _firstPageBtn = _root.Q<Button>("BtnFirstPage");
        _lastPageBtn = _root.Q<Button>("BtnLastPage");
        _pageLabel = _root.Q<Label>("PageLabel");

        var previousIcon = _root.Q<Image>("PreviousPageIcon");
        var nextIcon = _root.Q<Image>("NextPageIcon");
        var previousSprite = Resources.Load<Sprite>("Icons/left_arrow");
        var nextSprite = Resources.Load<Sprite>("Icons/right_arrow");
        SetPageIcon(previousIcon, previousSprite);
        SetPageIcon(nextIcon, nextSprite);
        SetPageIcon(_root.Q<Image>("FirstPageIcon1"), previousSprite);
        SetPageIcon(_root.Q<Image>("FirstPageIcon2"), previousSprite);
        SetPageIcon(_root.Q<Image>("LastPageIcon1"), nextSprite);
        SetPageIcon(_root.Q<Image>("LastPageIcon2"), nextSprite);

        _posBtns.Clear();
        foreach (var name in new[] { "PosTodos", "PosPG", "PosSG", "PosSF", "PosPF", "PosC" })
        {
            var btn = _root.Q<Button>(name);
            if (btn != null) _posBtns.Add(btn);
        }

        _ageBtns.Clear();
        foreach (var name in new[] { "AgeTodas", "Age20", "Age24", "Age29", "Age30" })
        {
            var btn = _root.Q<Button>(name);
            if (btn != null) _ageBtns.Add(btn);
        }

        _ovrBtns.Clear();
        foreach (var name in new[] { "OvrTodas", "Ovr85", "Ovr80", "Ovr75", "Ovr70" })
        {
            var btn = _root.Q<Button>(name);
            if (btn != null) _ovrBtns.Add(btn);
        }
    }

    void SetPageIcon(Image image, Sprite sprite)
    {
        if (image != null && sprite != null)
            image.style.backgroundImage = new StyleBackground(sprite);
    }

    protected override void LoadData()
    {
        base.LoadData();
        _allTeams = DatabaseManager.Instance?.GetAllTeams() ?? new List<TeamData>();
        _allPlayers = DatabaseManager.Instance?.GetAllPlayers() ?? new List<PlayerData>();

        _scoutedIds = new HashSet<int>();
        if (_myTeam != null)
            _scoutedIds = DatabaseManager.Instance.GetScoutedPlayerIds(_myTeam.id);
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();

        string[] posCodes = { "TODOS", "PG", "SG", "SF", "PF", "C" };
        for (int i = 0; i < _posBtns.Count && i < posCodes.Length; i++)
        {
            int idx = i;
            _posBtns[i].RegisterCallback<ClickEvent>(_ => { PlayClick(); _position = posCodes[idx]; ApplyFilters(); });
        }

        for (int i = 0; i < _ageBtns.Count; i++)
        {
            int idx = i;
            _ageBtns[i].RegisterCallback<ClickEvent>(_ => { PlayClick(); _ageBand = idx; ApplyFilters(); });
        }

        int[] ovrThresholds = { 0, 85, 80, 75, 70 };
        for (int i = 0; i < _ovrBtns.Count && i < ovrThresholds.Length; i++)
        {
            int idx = i;
            _ovrBtns[i].RegisterCallback<ClickEvent>(_ => { PlayClick(); _minOvr = ovrThresholds[idx]; ApplyFilters(); });
        }

        _root.Q<Button>("ToggleOnlyFA")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _onlyFA = !_onlyFA; ApplyFilters(); });
        _root.Q<Button>("BtnClearFilters")?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ClearFilters(); });
        _previousPageBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangePage(-1); });
        _nextPageBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangePage(1); });
        _firstPageBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangePage(-10); });
        _lastPageBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ChangePage(10); });
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Buscador] RefreshHeader error: {ex.Message}"); }
        BuildTeamButtons();
        ApplyFilters();
    }

    /* ═══════════════════════════════════════════
       FILTROS
       ═══════════════════════════════════════════ */

    void ClearFilters()
    {
        _position = "TODOS";
        _ageBand = 0;
        _teamId = 0;
        _minOvr = 0;
        _onlyFA = false;
        ApplyFilters();
    }

    void UpdateFilterVisuals()
    {
        for (int i = 0; i < _posBtns.Count; i++)
        {
            bool active = (i == 0 && _position == "TODOS") ||
                          (i == 1 && _position == "PG") ||
                          (i == 2 && _position == "SG") ||
                          (i == 3 && _position == "SF") ||
                          (i == 4 && _position == "PF") ||
                          (i == 5 && _position == "C");
            _posBtns[i]?.EnableInClassList("filter-btn--active", active);
        }

        for (int i = 0; i < _ageBtns.Count; i++)
            _ageBtns[i]?.EnableInClassList("filter-btn--active", i == _ageBand);

        for (int i = 0; i < _ovrBtns.Count; i++)
        {
            int[] thresholds = { 0, 85, 80, 75, 70 };
            _ovrBtns[i]?.EnableInClassList("filter-btn--active", _minOvr == thresholds[i]);
        }

        _root.Q<Button>("ToggleOnlyFA")?.EnableInClassList("filter-btn--active", _onlyFA);

        foreach (var kv in _teamBtnMap)
            kv.Key.EnableInClassList("buscador-team-btn--active", kv.Value == _teamId);
    }

    void ApplyFilters()
    {
        if (_tableBody == null) return;
        UpdateFilterVisuals();

        IEnumerable<PlayerData> query = _allPlayers;

        if (_onlyFA)
        {
            query = query.Where(p => p.team_id == 0);
        }
        else if (_teamId != 0)
        {
            query = query.Where(p => p.team_id == _teamId);
        }

        if (_position != "TODOS")
            query = query.Where(p => p.position == _position || p.secondary_position == _position);

        switch (_ageBand)
        {
            case 1: query = query.Where(p => p.age <= 20); break;
            case 2: query = query.Where(p => p.age >= 21 && p.age <= 24); break;
            case 3: query = query.Where(p => p.age >= 25 && p.age <= 29); break;
            case 4: query = query.Where(p => p.age >= 30); break;
        }

        if (_minOvr > 0)
            query = query.Where(p => p.GetCalculatedAverage() >= _minOvr);

        int total = query.Count();

        _filteredPlayers = SortPlayers(query).ToList();

        if (_panelTitle != null)
        {
            string word = total == 1 ? "JUGADOR" : "JUGADORES";
            _panelTitle.text = $"BUSCADOR — {total} {word}";
        }

        _currentPage = 0;
        RenderCurrentPage();
    }

    IEnumerable<PlayerData> SortPlayers(IEnumerable<PlayerData> query)
    {
        switch (_sortColumn)
        {
            case "age":
                return _sortDescending
                    ? query.OrderByDescending(p => p.age).ThenBy(p => p.last_name)
                    : query.OrderBy(p => p.age).ThenBy(p => p.last_name);
            case "potential":
                return _sortDescending
                    ? query.OrderByDescending(p => p.potential).ThenBy(p => p.last_name)
                    : query.OrderBy(p => p.potential).ThenBy(p => p.last_name);
            case "salary":
                return _sortDescending
                    ? query.OrderByDescending(p => p.salary).ThenBy(p => p.last_name)
                    : query.OrderBy(p => p.salary).ThenBy(p => p.last_name);
            default:
                return _sortDescending
                    ? query.OrderByDescending(p => p.GetCalculatedAverage()).ThenBy(p => p.last_name)
                    : query.OrderBy(p => p.GetCalculatedAverage()).ThenBy(p => p.last_name);
        }
    }

    void SetSort(string column)
    {
        if (_sortColumn == column)
            _sortDescending = !_sortDescending;
        else
        {
            _sortColumn = column;
            _sortDescending = true;
        }

        PlayClick();
        ApplyFilters();
    }

    void ChangePage(int direction)
    {
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(_filteredPlayers.Count / (float)PAGE_SIZE));
        _currentPage = Mathf.Clamp(_currentPage + direction, 0, pageCount - 1);
        RenderCurrentPage();
    }

    void RenderCurrentPage()
    {
        if (_tableBody == null) return;

        int pageCount = Mathf.Max(1, Mathf.CeilToInt(_filteredPlayers.Count / (float)PAGE_SIZE));
        _currentPage = Mathf.Clamp(_currentPage, 0, pageCount - 1);
        var pagePlayers = _filteredPlayers
            .Skip(_currentPage * PAGE_SIZE)
            .Take(PAGE_SIZE)
            .ToList();

        _tableBody.Clear();
        BuildDynamicHeader();
        RenderRows(pagePlayers, _filteredPlayers.Count);

        if (_pageLabel != null)
            _pageLabel.text = $"{_currentPage + 1} de {pageCount}";
        _previousPageBtn?.SetEnabled(_currentPage > 0);
        _nextPageBtn?.SetEnabled(_currentPage < pageCount - 1);
    }

    /* ═══════════════════════════════════════════
       TABLA
       ═══════════════════════════════════════════ */

    void BuildDynamicHeader()
    {
        if (_tableHeader == null) return;
        _tableHeader.Clear();

        _tableHeader.Add(MakeHeaderCell("JUGADOR", "col-player-name", false));
        _tableHeader.Add(MakeHeaderCell("POS", "col-pos", false));
        _tableHeader.Add(MakeHeaderCell("EQUIPO", "col-team-abbrev", false));
        _tableHeader.Add(MakeSortableHeaderCell("EDAD", "age", false));
        _tableHeader.Add(MakeSortableHeaderCell("MEDIA", "media", true));
        _tableHeader.Add(MakeSortableHeaderCell("POTENCIAL", "potential", false));
        _tableHeader.Add(MakeSortableHeaderCell("SALARIO", "salary", false));
    }

    Label MakeHeaderCell(string text, string baseClass, bool isBold)
    {
        var lbl = new Label();
        lbl.AddToClassList(baseClass);
        lbl.AddToClassList("col-stat--header");
        if (isBold) lbl.AddToClassList("col-stat--bold");
        lbl.text = text;
        return lbl;
    }

    Button MakeSortableHeaderCell(string text, string column, bool isBold)
    {
        var btn = new Button();
        btn.AddToClassList("col-stat");
        btn.AddToClassList("col-stat--header");
        btn.AddToClassList("buscador-sort-header");
        if (isBold) btn.AddToClassList("col-stat--bold");
        string arrow = _sortColumn == column ? (_sortDescending ? " ▼" : " ▲") : "";
        btn.text = text + arrow;
        btn.RegisterCallback<ClickEvent>(_ => SetSort(column));
        return btn;
    }

    Label MakeCell(string value, bool isBold)
    {
        var lbl = new Label();
        lbl.AddToClassList("col-stat");
        lbl.text = value;
        if (isBold) lbl.AddToClassList("col-stat--bold");
        return lbl;
    }

    void RenderRows(List<PlayerData> players, int total)
    {
        if (players.Count == 0)
        {
            var empty = new VisualElement();
            empty.AddToClassList("stats-empty");
            var emptyLbl = new Label();
            emptyLbl.AddToClassList("stats-empty-label");
            emptyLbl.text = "NO HAY JUGADORES CON LOS FILTROS SELECCIONADOS";
            empty.Add(emptyLbl);
            _tableBody.Add(empty);
            return;
        }

        foreach (var p in players)
        {
            var row = new VisualElement();
            row.AddToClassList("stats-row");
            if (_myTeam != null && p.team_id == _myTeam.id)
                row.AddToClassList("stats-row--my-team");

            var nameLbl = new Label();
            nameLbl.AddToClassList("col-player-name");
            nameLbl.text = $"{p.first_name} {p.last_name}";
            row.Add(nameLbl);

            var posLbl = new Label();
            posLbl.AddToClassList("col-pos");
            posLbl.text = PositionCodes.GetShort(p.position);
            row.Add(posLbl);

            var team = _allTeams.Find(t => t.id == p.team_id);
            var teamLbl = new Label();
            teamLbl.AddToClassList("col-team-abbrev");
            teamLbl.text = team != null ? team.abbreviation : "LIBRE";
            row.Add(teamLbl);

            row.Add(MakeCell(p.age.ToString(), false));
            row.Add(MakeCell(FogOfWarHelper.GetOvrDisplay(p, _myTeam?.id ?? 0, _scoutedIds), true));
            row.Add(MakeCell(Mathf.Clamp(p.potential, 0, 99).ToString(), false));
            row.Add(MakeCell($"${p.salary:N0}", false));

            row.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                ScreenManager.SelectedPlayerId = p.id;
                ScreenManager.Instance.GoTo(GameScreen.PlayerProfile);
            });
            CursorManager.Instance?.RegisterHandCursor(row);

            _tableBody.Add(row);
        }
    }

    /* ═══════════════════════════════════════════
       BOTONES DE EQUIPO
       ═══════════════════════════════════════════ */

    void BuildTeamButtons()
    {
        if (_teamGrid == null) return;
        _teamGrid.Clear();
        _teamBtnMap.Clear();

        var teams = _allTeams
            .OrderBy(t => t.name)
            .Select(t => (t.abbreviation, t.id))
            .ToList();
        teams.Add(("TODOS", 0));

        for (int i = 0; i < teams.Count; i += 15)
        {
            var row = new VisualElement();
            row.AddToClassList("buscador-team-row");
            _teamGrid.Add(row);

            int rowEnd = Mathf.Min(i + 15, teams.Count);
            for (int j = i; j < rowEnd; j++)
                AddTeamButton(row, teams[j].abbreviation, teams[j].id);
        }
    }

    void AddTeamButton(VisualElement row, string label, int teamId)
    {
        var item = new Button();
        item.AddToClassList("buscador-team-btn");
        item.text = label;
        item.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _teamId = teamId;
            ApplyFilters();
        });
        CursorManager.Instance?.RegisterHandCursor(item);
        row.Add(item);
        _teamBtnMap[item] = teamId;
    }
}
