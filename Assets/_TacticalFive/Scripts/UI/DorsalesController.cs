using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class DorsalesController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Dorsales;

    private const int ITEMS_PER_ROW = 9;
    private const int ITEMS_PER_PAGE = 20;

    // Panels
    private VisualElement _panelActuales;
    private VisualElement _panelRetirados;
    private VisualElement _tablaActuales;
    private VisualElement _tablaRetirados;
    private VisualElement _paginatorRetirados;

    // Counters
    private Label _countActivos;
    private Label _countRetirados;

    // Data
    private List<PlayerData> _players;
    private List<RetiredNumberData> _retired;
    private StyleBackground _tshirtBg;
    private StyleBackground _retiredTshirtBg;

    // Pagination
    private int _currentPage;

    protected override void CacheReferences()
    {
        _panelActuales = _root.Q<VisualElement>("PanelActuales");
        _panelRetirados = _root.Q<VisualElement>("PanelRetirados");
        _tablaActuales = _root.Q<VisualElement>("TablaActuales");
        _tablaRetirados = _root.Q<VisualElement>("TablaRetirados");
        _paginatorRetirados = _root.Q<VisualElement>("PaginatorRetirados");
        _countActivos = _root.Q<Label>("CountActivos");
        _countRetirados = _root.Q<Label>("CountRetirados");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var tshirtTex = Resources.Load<Texture2D>("Icons/tshirt");
        if (tshirtTex != null) _tshirtBg = new StyleBackground(tshirtTex);

        var retiredTex = Resources.Load<Texture2D>("Icons/retired_tshirt");
        if (retiredTex != null) _retiredTshirtBg = new StyleBackground(retiredTex);

        var iconActuales = _root.Q<VisualElement>("IconActuales");
        if (iconActuales != null && tshirtTex != null)
            iconActuales.style.backgroundImage = new StyleBackground(tshirtTex);

        var iconRetirados = _root.Q<VisualElement>("IconRetirados");
        if (iconRetirados != null && retiredTex != null)
            iconRetirados.style.backgroundImage = new StyleBackground(retiredTex);
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Dorsales] RefreshHeader error: {ex.Message}"); }
        _currentPage = 0;
        BuildPanelActuales();
        BuildPanelRetirados();
    }

    // ── ACTUALES ──────────────────────────────────────

    void BuildPanelActuales()
    {
        _tablaActuales.Clear();
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var sorted = _players.Where(p => p.number > 0).OrderBy(p => p.number).ToList();

        if (_countActivos != null)
            _countActivos.text = $"{sorted.Count}/{_players.Count}";

        if (sorted.Count == 0)
        {
            var empty = new Label("No hay jugadores con dorsal asignado.");
            empty.AddToClassList("dorsales-empty");
            _tablaActuales.Add(empty);
            return;
        }

        for (int row = 0; row < sorted.Count; row += ITEMS_PER_ROW)
        {
            int count = Mathf.Min(ITEMS_PER_ROW, sorted.Count - row);
            var slice = sorted.GetRange(row, count);

            var rowEl = new VisualElement();
            rowEl.AddToClassList("dorsales-row");

            foreach (var p in slice)
                rowEl.Add(CreatePlayerCard(p));

            _tablaActuales.Add(rowEl);
        }
    }

    VisualElement CreatePlayerCard(PlayerData p)
    {
        var card = new VisualElement();
        card.AddToClassList("dorsales-card");

        var shirt = new VisualElement();
        shirt.AddToClassList("dorsales-card-shirt");
        if (_tshirtBg != null)
            shirt.style.backgroundImage = _tshirtBg;

        var number = new Label(p.number.ToString());
        number.AddToClassList("dorsales-card-number");
        shirt.Add(number);

        card.Add(shirt);

        var name = new Label($"{p.first_name} {p.last_name}".ToUpper());
        name.AddToClassList("dorsales-card-name");
        card.Add(name);

        return card;
    }

    // ── RETIRADOS ─────────────────────────────────────

    void BuildPanelRetirados()
    {
        _tablaRetirados.Clear();
        _retired = DatabaseManager.Instance.GetRetiredNumbers(_myTeam.id);

        if (_countRetirados != null)
            _countRetirados.text = _retired.Count.ToString();

        if (_retired.Count == 0)
        {
            _paginatorRetirados.style.display = DisplayStyle.None;
            var empty = new Label("Este equipo aún no ha retirado ningún dorsal.");
            empty.AddToClassList("dorsales-empty");
            _tablaRetirados.Add(empty);
            return;
        }

        int totalPages = Mathf.CeilToInt((float)_retired.Count / ITEMS_PER_PAGE);
        if (_currentPage >= totalPages) _currentPage = totalPages - 1;

        int start = _currentPage * ITEMS_PER_PAGE;
        int end = Mathf.Min(start + ITEMS_PER_PAGE, _retired.Count);
        var page = _retired.GetRange(start, end - start);

        for (int row = 0; row < page.Count; row += ITEMS_PER_ROW)
        {
            int count = Mathf.Min(ITEMS_PER_ROW, page.Count - row);
            var slice = page.GetRange(row, count);

            var rowEl = new VisualElement();
            rowEl.AddToClassList("dorsales-row");

            foreach (var r in slice)
                rowEl.Add(CreateRetiredCard(r));

            _tablaRetirados.Add(rowEl);
        }

        BuildPaginator(totalPages);
    }

    VisualElement CreateRetiredCard(RetiredNumberData r)
    {
        var card = new VisualElement();
        card.AddToClassList("dorsales-card");

        var shirt = new VisualElement();
        shirt.AddToClassList("dorsales-card-shirt");
        if (_retiredTshirtBg != null)
            shirt.style.backgroundImage = _retiredTshirtBg;

        var number = new Label(r.number.ToString());
        number.AddToClassList("dorsales-card-number");
        number.AddToClassList("dorsales-card-number--retired");
        shirt.Add(number);

        card.Add(shirt);

        var name = new Label(r.FullName.ToUpper());
        name.AddToClassList("dorsales-card-name");
        card.Add(name);

        return card;
    }

    // ── PAGINATOR ─────────────────────────────────────

    void BuildPaginator(int totalPages)
    {
        _paginatorRetirados.Clear();
        _paginatorRetirados.style.display = DisplayStyle.Flex;

        var btnPrev = new Button();
        btnPrev.AddToClassList("dorsales-paginator-btn");
        btnPrev.text = "\u25C0";
        btnPrev.SetEnabled(_currentPage > 0);
        if (_currentPage <= 0) btnPrev.AddToClassList("dorsales-paginator-btn--disabled");
        int prevPage = _currentPage - 1;
        btnPrev.RegisterCallback<ClickEvent>(_ => { PlayClick(); GoToPage(prevPage); });
        if (CursorManager.Instance != null)
        {
            btnPrev.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
            btnPrev.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
        }
        _paginatorRetirados.Add(btnPrev);

        var pageLbl = new Label($"{_currentPage + 1} / {totalPages}");
        pageLbl.AddToClassList("dorsales-paginator-label");
        _paginatorRetirados.Add(pageLbl);

        var btnNext = new Button();
        btnNext.AddToClassList("dorsales-paginator-btn");
        btnNext.text = "\u25B6";
        btnNext.SetEnabled(_currentPage < totalPages - 1);
        if (_currentPage >= totalPages - 1) btnNext.AddToClassList("dorsales-paginator-btn--disabled");
        int nextPage = _currentPage + 1;
        btnNext.RegisterCallback<ClickEvent>(_ => { PlayClick(); GoToPage(nextPage); });
        if (CursorManager.Instance != null)
        {
            btnNext.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
            btnNext.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
        }
        _paginatorRetirados.Add(btnNext);
    }

    void GoToPage(int page)
    {
        _currentPage = page;
        BuildPanelRetirados();
    }
}
