using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;

public class LoadGameController : UIScreenController
{
    private VisualElement _slotsGrid;
    private VisualElement _slotsArea;
    private VisualElement _slotsPaginator;
    private VisualElement _emptyState;
    private Button _btnBack;

    private VisualElement _deleteModalOverlay;
    private VisualElement _deleteModalBox;
    private Button _btnDeleteYes;
    private Button _btnDeleteNo;

    private int _pendingDeleteSlot = -1;
    private int _currentPage;
    private const int CARDS_PER_PAGE = 4;

    private Dictionary<string, Sprite> _logoSprites = new();
    private Dictionary<string, Sprite> _arenaSprites = new();
    private Sprite _defaultArenaSprite;
    private Texture2D _calendarTex;
    private Texture2D _budgetTex;
    private Texture2D _payrollTex;
    private Texture2D _chemistryTex;

    protected override void OnEnable()
    {
        base.OnEnable();
        CursorManager.Instance?.SetDefaultCursor();
    }

    protected override void CacheReferences()
    {
        _slotsGrid = _root.Q<VisualElement>("SlotsGrid");
        _slotsArea = _root.Q<VisualElement>("SlotsArea");
        _slotsPaginator = _root.Q<VisualElement>("SlotsPaginator");
        _emptyState = _root.Q<VisualElement>("EmptyState");
        _btnBack = _root.Q<Button>("BtnBack");

        _deleteModalOverlay = _root.Q<VisualElement>("DeleteModalOverlay");
        _deleteModalBox = _root.Q<VisualElement>("DeleteModalBox");
        _btnDeleteYes = _root.Q<Button>("BtnDeleteYes");
        _btnDeleteNo = _root.Q<Button>("BtnDeleteNo");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/80x80/");
        foreach (var s in logos) _logoSprites[s.name] = s;

        var arenas = Resources.LoadAll<Sprite>("Arenas/");
        foreach (var s in arenas) _arenaSprites[s.name] = s;
        _defaultArenaSprite = Resources.Load<Sprite>("Arenas/default");

        _calendarTex = Resources.Load<Texture2D>("Icons/calendario");
        _budgetTex = Resources.Load<Texture2D>("Icons/finanzas");
        _payrollTex = Resources.Load<Texture2D>("Icons/contrato");
        _chemistryTex = Resources.Load<Texture2D>("Icons/quimica");

        GameSaveManager.CleanupAllOrphanDbs();
    }

    protected override void RegisterCallbacks()
    {
        _btnBack?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.MainMenu); });

        _btnDeleteYes?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ConfirmDelete(); });
        _btnDeleteNo?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseDeleteModal(); });
        _deleteModalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _deleteModalOverlay)
                { PlayClick(); CloseDeleteModal(); }
        });

        _root.focusable = true;
        _root.Focus();
        _root.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape && _deleteModalOverlay?.style.display == DisplayStyle.Flex)
                CloseDeleteModal();
        });

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnBack);
            CursorManager.Instance.RegisterHandCursor(_btnDeleteYes);
            CursorManager.Instance.RegisterHandCursor(_btnDeleteNo);
        }
    }

    protected override void Refresh()
    {
        _currentPage = 0;
        RefreshSlots();
    }

    void RefreshSlots()
    {
        _slotsGrid.Clear();
        _slotsPaginator.Clear();

        var allSlots = GameSaveManager.GetAllSlots();
        var existingSlots = allSlots
            .Where(s => s.exists && !string.IsNullOrEmpty(s.managerName) && !string.IsNullOrEmpty(s.teamName))
            .ToList();

        if (existingSlots.Count == 0)
        {
            _slotsArea.style.display = DisplayStyle.None;
            _slotsPaginator.style.display = DisplayStyle.None;
            _emptyState.style.display = DisplayStyle.Flex;
            return;
        }

        _slotsArea.style.display = DisplayStyle.Flex;
        _emptyState.style.display = DisplayStyle.None;

        foreach (var slot in existingSlots)
            RefreshSlotStatus(slot);

        int totalPages = Mathf.CeilToInt((float)existingSlots.Count / CARDS_PER_PAGE);
        if (_currentPage >= totalPages) _currentPage = totalPages - 1;
        if (_currentPage < 0) _currentPage = 0;

        int start = _currentPage * CARDS_PER_PAGE;
        int end = Mathf.Min(start + CARDS_PER_PAGE, existingSlots.Count);

        for (int i = start; i < end; i++)
        {
            var card = CreateSlotCard(existingSlots[i]);
            _slotsGrid.Add(card);
        }

        if (totalPages > 1)
            BuildPaginator(totalPages);
    }

    void RefreshSlotStatus(SaveSlotInfo slot)
    {
        try
        {
            string dbPath = GameSaveManager.GetSaveDbPath(slot.slotNumber);
            if (!File.Exists(dbPath)) return;

            using var conn = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly);
            var manager = conn.Table<ManagerData>()
                .OrderByDescending(m => m.id)
                .FirstOrDefault();

            if (manager != null)
                slot.managerStatus = manager.trust < 10 ? "fired" : "active";

            if (manager == null) return;
            var team = conn.Table<TeamData>().FirstOrDefault(t => t.id == manager.team_id);
            if (team != null)
            {
                slot.teamLogo = team.logo;
                slot.teamName = team.name;
                slot.teamBudget = team.budget;

                int teamId = manager.team_id;
                slot.teamPayroll = conn.Table<PlayerData>()
                    .Where(p => p.team_id == teamId)
                    .Sum(p => (long?)p.salary) ?? 0;
                slot.teamChemistry = team.team_chemistry;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LoadGame] No se pudo leer el estado del slot {slot.slotNumber}: {ex.Message}");
        }
    }

    VisualElement CreateSlotCard(SaveSlotInfo slot)
    {
        var card = new VisualElement();
        card.AddToClassList("slot-card");

        // ── Header: date + season ──
        var header = new VisualElement();
        header.AddToClassList("slot-card-header");

        var dateGroup = new VisualElement();
        dateGroup.AddToClassList("slot-date-group");

        if (_calendarTex != null)
        {
            var calIcon = new VisualElement();
            calIcon.AddToClassList("slot-date-icon");
            calIcon.style.backgroundImage = new StyleBackground(_calendarTex);
            dateGroup.Add(calIcon);
        }

        var dateLbl = new Label { text = FormatDate(slot.currentDate) };
        dateLbl.AddToClassList("slot-date-text");
        dateGroup.Add(dateLbl);
        header.Add(dateGroup);

        var seasonLbl = new Label { text = $"TEMP. {slot.seasonYear}" };
        seasonLbl.AddToClassList("slot-season-text");
        header.Add(seasonLbl);
        card.Add(header);

        // ── Arena photo ──
        var arenaPhoto = new VisualElement();
        arenaPhoto.AddToClassList("slot-arena-photo");
        if (!string.IsNullOrEmpty(slot.teamLogo))
        {
            Sprite arenaSpr = null;
            string key = slot.teamLogo;
            if (key == "sixers") key = "76ers";
            _arenaSprites.TryGetValue(key, out arenaSpr);
            if (arenaSpr == null) arenaSpr = _defaultArenaSprite;
            if (arenaSpr != null)
                arenaPhoto.style.backgroundImage = new StyleBackground(arenaSpr);
        }
        card.Add(arenaPhoto);

        // ── Team section: logo + name ──
        var teamSection = new VisualElement();
        teamSection.AddToClassList("slot-team-section");

        var logoElem = new VisualElement();
        logoElem.AddToClassList("slot-team-logo");
        if (!string.IsNullOrEmpty(slot.teamLogo) && _logoSprites.TryGetValue(slot.teamLogo, out var logoSpr))
            logoElem.style.backgroundImage = new StyleBackground(logoSpr);
        teamSection.Add(logoElem);

        var teamInfo = new VisualElement();
        teamInfo.AddToClassList("slot-team-info");

        var teamNameLbl = new Label { text = slot.teamName ?? "Sin equipo" };
        teamNameLbl.AddToClassList("slot-team-name");
        teamInfo.Add(teamNameLbl);

        var teamSeasonLbl = new Label { text = $"Temporada {slot.seasonYear}" };
        teamSeasonLbl.AddToClassList("slot-team-season");
        teamInfo.Add(teamSeasonLbl);
        teamSection.Add(teamInfo);
        card.Add(teamSection);

        // ── Stats ──
        var stats = new VisualElement();
        stats.AddToClassList("slot-stats");

        stats.Add(BuildStatRow(_budgetTex, "PRESUPUESTO", FormatMoney(slot.teamBudget)));
        stats.Add(BuildStatRow(_payrollTex, "MASA SALARIAL", FormatMoney(slot.teamPayroll)));
        stats.Add(BuildStatRow(_chemistryTex, "QUÍMICA DE EQUIPO", $"{slot.teamChemistry}%"));
        card.Add(stats);

        // ── Status badge ──
        bool isFired = slot.managerStatus == "fired";
        var badge = new VisualElement();
        badge.AddToClassList("slot-status-badge");
        badge.AddToClassList(isFired ? "slot-status-badge--fired" : "slot-status-badge--active");

        var dot = new VisualElement();
        dot.AddToClassList("slot-status-dot");
        badge.Add(dot);

        var statusLbl = new Label { text = isFired ? "DESPEDIDO" : "ACTIVO" };
        statusLbl.AddToClassList("slot-status-label");
        badge.Add(statusLbl);
        card.Add(badge);

        // ── Actions ──
        var actions = new VisualElement();
        actions.AddToClassList("slot-actions");

        var loadBtn = new Button { text = "CARGAR" };
        loadBtn.AddToClassList("btn-load");
        int slotNum = slot.slotNumber;
        if (isFired)
        {
            loadBtn.SetEnabled(false);
            loadBtn.AddToClassList("btn-load--disabled");
        }
        else
        {
            loadBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnLoadSlot(slotNum); });
        }
        if (CursorManager.Instance != null)
        {
            loadBtn.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
            loadBtn.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
        }
        actions.Add(loadBtn);

        var deleteBtn = new Button { text = "BORRAR" };
        deleteBtn.AddToClassList("btn-delete");
        deleteBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnDeleteSlot(slotNum); });
        if (CursorManager.Instance != null)
        {
            deleteBtn.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
            deleteBtn.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
        }
        actions.Add(deleteBtn);
        card.Add(actions);

        return card;
    }

    VisualElement BuildStatRow(Texture2D icon, string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("slot-stat-row");

        var left = new VisualElement();
        left.AddToClassList("slot-stat-left");

        if (icon != null)
        {
            var iconElem = new VisualElement();
            iconElem.AddToClassList("slot-stat-icon");
            iconElem.style.backgroundImage = new StyleBackground(icon);
            left.Add(iconElem);
        }

        var lbl = new Label { text = label };
        lbl.AddToClassList("slot-stat-label");
        left.Add(lbl);
        row.Add(left);

        var valLbl = new Label { text = value };
        valLbl.AddToClassList("slot-stat-value");
        row.Add(valLbl);

        return row;
    }

    void BuildPaginator(int totalPages)
    {
        _slotsPaginator.style.display = DisplayStyle.Flex;

        var btnPrev = new Button { text = "\u25C0" };
        btnPrev.AddToClassList("loadgame-paginator-btn");
        btnPrev.SetEnabled(_currentPage > 0);
        if (_currentPage == 0) btnPrev.AddToClassList("loadgame-paginator-btn--disabled");
        int prevPage = _currentPage - 1;
        btnPrev.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _currentPage = prevPage;
            RefreshSlots();
        });
        CursorManager.Instance?.RegisterHandCursor(btnPrev);
        _slotsPaginator.Add(btnPrev);

        var pageLabel = new Label($"{_currentPage + 1} / {totalPages}");
        pageLabel.AddToClassList("loadgame-paginator-label");
        _slotsPaginator.Add(pageLabel);

        var btnNext = new Button { text = "\u25B6" };
        btnNext.AddToClassList("loadgame-paginator-btn");
        btnNext.SetEnabled(_currentPage < totalPages - 1);
        if (_currentPage >= totalPages - 1) btnNext.AddToClassList("loadgame-paginator-btn--disabled");
        int nextPage = _currentPage + 1;
        btnNext.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _currentPage = nextPage;
            RefreshSlots();
        });
        CursorManager.Instance?.RegisterHandCursor(btnNext);
        _slotsPaginator.Add(btnNext);
    }

    string FormatDate(string date)
    {
        if (string.IsNullOrEmpty(date)) return "---";
        if (System.DateTime.TryParse(date, out var dt))
            return dt.ToString("dd MMM yyyy").ToUpper();
        return date;
    }

    string FormatMoney(long amount)
    {
        if (amount >= 1_000_000)
            return $"${amount / 1_000_000}M";
        return $"${amount:N0}";
    }

    void OnLoadSlot(int slotNumber)
    {
        try
        {
            string dbPath = GameSaveManager.GetSaveDbPath(slotNumber);
            if (File.Exists(dbPath))
            {
                using var conn = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly);
                var mgr = conn.Table<ManagerData>()
                    .OrderByDescending(m => m.id)
                    .FirstOrDefault();
                if (mgr != null && mgr.trust < 10) return;
            }
        }
        catch (System.Exception) { }

        DatabaseManager.Instance.InitSaveSlot(slotNumber);
        GameSaveManager.UpdateSlotFromDatabase(slotNumber);
        ScreenManager.Instance.GoTo(GameScreen.Dashboard);
    }

    void OnDeleteSlot(int slotNumber)
    {
        _pendingDeleteSlot = slotNumber;
        OpenDeleteModal();
    }

    void OpenDeleteModal()
    {
        CursorManager.Instance?.SetDefaultCursor();
        _deleteModalOverlay.style.display = DisplayStyle.Flex;
        _deleteModalBox.style.display = DisplayStyle.Flex;
    }

    void CloseDeleteModal()
    {
        _deleteModalOverlay.style.display = DisplayStyle.None;
        _deleteModalBox.style.display = DisplayStyle.None;
        _pendingDeleteSlot = -1;
    }

    void ConfirmDelete()
    {
        if (_pendingDeleteSlot >= 0)
            GameSaveManager.DeleteSave(_pendingDeleteSlot);
        CloseDeleteModal();
        RefreshSlots();
    }
}
