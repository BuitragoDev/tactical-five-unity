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

    // ── Scrollbar ──
    private VisualElement _buscadorViewport, _buscadorScrollTrack, _buscadorScrollThumb;
    private ScrollBarData _buscadorBar;

    protected override void CacheReferences()
    {
        _tableHeader = _root.Q<VisualElement>("BuscadorTableHeader");
        _tableBody = _root.Q<VisualElement>("BuscadorTableBody");
        _panelTitle = _root.Q<Label>("PanelTitle");
        _teamGrid = _root.Q<VisualElement>("BuscadorTeamGrid");
        _buscadorViewport = _root.Q<VisualElement>("BuscadorViewport");
        _buscadorScrollTrack = _root.Q<VisualElement>("BuscadorScrollTrack");
        _buscadorScrollThumb = _root.Q<VisualElement>("BuscadorScrollThumb");
        if (_buscadorViewport != null) _buscadorViewport.style.overflow = Overflow.Hidden;

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

    protected override void LoadData()
    {
        base.LoadData();

        var screenIcon = _root.Q<VisualElement>("BuscadorScreenIcon");
        if (screenIcon != null)
        {
            var iconTex = Resources.Load<Texture2D>("Icons/lupa_icon");
            if (iconTex != null) screenIcon.style.backgroundImage = new StyleBackground(iconTex);
        }

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

    void RenderCurrentPage()
    {
        if (_tableBody == null) return;

        _tableBody.Clear();
        BuildDynamicHeader();
        RenderRows(_filteredPlayers, _filteredPlayers.Count);

        if (_buscadorBar != null) { _buscadorBar.ScrollY = 0f; ApplyScroll(_buscadorBar); }
        _buscadorBar = BindScrollBar(_buscadorViewport, _tableBody, _buscadorScrollTrack, _buscadorScrollThumb);
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

    // ── ScrollBar ──
    class ScrollBarData : IScrollBarHandle
    {
        public VisualElement Viewport;
        public VisualElement Content;
        public VisualElement Track;
        public VisualElement Thumb;
        public float ScrollY;
        public bool Dragging;
        public float StartPointerY;
        public float StartScrollY;
    }

    ScrollBarData BindScrollBar(VisualElement viewport, VisualElement content,
                                VisualElement track, VisualElement thumb)
    {
        if (viewport == null || content == null || track == null || thumb == null) return null;

        // Ya enlazada en esta visita (el viewport persiste): no registrar los
        // handlers otra vez, que se acumulaban al reconstruir la lista.
        if (ScrollBarBinder.TryGet<ScrollBarData>(viewport, out var bound)) return bound;


        viewport.style.overflow = Overflow.Hidden;
        viewport.style.flexDirection = FlexDirection.Column;

        var bar = new ScrollBarData
        {
            Viewport = viewport,
            Content = content,
            Track = track,
            Thumb = thumb
        };

        viewport.RegisterCallback<WheelEvent>(evt =>
        {
            float max = GetScrollMax(bar);
            if (max <= 0f) return;
            bar.ScrollY = Mathf.Clamp(bar.ScrollY + evt.delta.y * 30f, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerDownEvent>(evt =>
        {
            bar.Dragging = true;
            bar.StartPointerY = evt.position.y;
            bar.StartScrollY = bar.ScrollY;
            thumb.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!bar.Dragging) return;
            float max = GetScrollMax(bar);
            float travel = track.layout.height - thumb.layout.height;
            if (max <= 0f || travel <= 0f) return;
            float delta = evt.position.y - bar.StartPointerY;
            bar.ScrollY = Mathf.Clamp(bar.StartScrollY + delta / travel * max, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (bar.Dragging)
            {
                bar.Dragging = false;
                thumb.ReleasePointer(evt.pointerId);
            }
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerCaptureOutEvent>(_ => bar.Dragging = false);

        track.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (bar.Dragging) return;
            float max = GetScrollMax(bar);
            if (max <= 0f) return;
            float page = viewport.layout.height;
            float y = evt.localPosition.y;
            float thumbTop = thumb.layout.y;
            float thumbBottom = thumbTop + thumb.layout.height;
            float target = y < thumbTop ? bar.ScrollY - page
                         : (y > thumbBottom ? bar.ScrollY + page : bar.ScrollY);
            bar.ScrollY = Mathf.Clamp(target, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        CursorManager.Instance?.RegisterHandCursor(thumb);
        CursorManager.Instance?.RegisterHandCursor(track);

        content.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));
        viewport.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));

        ApplyScroll(bar);

        ScrollBarBinder.Remember(viewport, bar);
        return bar;
    }

    static float GetScrollMax(ScrollBarData bar)
    {
        if (bar == null || bar.Content == null || bar.Viewport == null) return 0f;
        float contentHeight = bar.Content.layout.height;
        float viewportHeight = bar.Viewport.layout.height;
        return Mathf.Max(0f, contentHeight - viewportHeight);
    }

    void ClampAndApply(ScrollBarData bar)
    {
        if (bar == null) return;
        bar.ScrollY = Mathf.Clamp(bar.ScrollY, 0f, GetScrollMax(bar));
        ApplyScroll(bar);
    }

    void ApplyScroll(ScrollBarData bar)
    {
        if (bar == null || bar.Content == null) return;

        bar.Content.style.top = -bar.ScrollY;

        float trackHeight = bar.Track.layout.height;
        float contentHeight = bar.Content.layout.height;
        float viewportHeight = bar.Viewport.layout.height;
        if (trackHeight <= 0f || contentHeight <= 0f) return;

        float max = Mathf.Max(0f, contentHeight - viewportHeight);
        float ratio = max > 0f ? bar.ScrollY / max : 0f;
        float thumbMax = trackHeight - bar.Thumb.layout.height;
        bar.Thumb.style.top = ratio * thumbMax;
    }
}
