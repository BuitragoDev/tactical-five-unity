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
    private VisualElement _contentOverlay;
    private Button _btnDeleteYes;
    private Button _btnDeleteNo;

    private int _pendingDeleteSlot = -1;
    private int _currentPage;
    private const int CARDS_PER_PAGE = 4;

    private Texture2D _calendarTex;
    private Texture2D _budgetTex;
    private Texture2D _payrollTex;
    private Texture2D _chemistryTex;
    private Texture2D _empleadoTex;

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
        _contentOverlay = _root.Q<VisualElement>("ContentOverlay");
        _btnDeleteYes = _root.Q<Button>("BtnDeleteYes");
        _btnDeleteNo = _root.Q<Button>("BtnDeleteNo");

        // Fila de QoL (exportar/importar partida) encima de la lista de slots.
        if (_slotsArea != null && _slotsArea.parent != null)
        {
            var transferRow = BuildTransferRow();
            int idx = _slotsArea.parent.IndexOf(_slotsArea);
            _slotsArea.parent.Insert(idx < 0 ? 0 : idx, transferRow);
        }
    }

    protected override void LoadData()
    {
        base.LoadData();

        _calendarTex = Resources.Load<Texture2D>("Icons/calendario");
        _budgetTex = Resources.Load<Texture2D>("Icons/finanzas");
        _payrollTex = Resources.Load<Texture2D>("Icons/contrato");
        _chemistryTex = Resources.Load<Texture2D>("Icons/quimica");
        _empleadoTex = Resources.Load<Texture2D>("Icons/empleado");

        GameSaveManager.CleanupAllOrphanDbs();
    }

    protected override void RegisterCallbacks()
    {
        _btnBack?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.MainMenu); });

        _btnDeleteYes?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ConfirmDelete(); });
        _btnDeleteNo?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseDeleteModal(); });

        _root.focusable = true;
        _root.Focus();

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
            .OrderByDescending(s => ParseSlotDate(s.lastPlayedRealDate))
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
            var manager = conn.Query<ManagerStatusRow>(
                "SELECT id, team_id, trust, game_mode, budget_red_warnings FROM managers ORDER BY id DESC LIMIT 1")
                .FirstOrDefault();

            if (manager != null)
                slot.managerStatus = IsRowFired(conn, manager) ? "fired" : "active";

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

    // Ámbito de imágenes de una partida: su propia carpeta save_{n} si ya la tiene,
    // o el template (lo que heredaría al cargarse por primera vez).
    static string SlotScope(int slotNumber)
    {
        string scope = $"save_{slotNumber}";
        return TeamImages.HasCustom(scope) ? scope : "template";
    }

    static bool IsRowFired(SQLiteConnection conn, ManagerStatusRow manager)
    {
        if (manager == null) return false;
        if (manager.trust < 10) return true;

        int threshold = manager.game_mode == "promanager" ? 2 : 3;
        if (manager.budget_red_warnings >= threshold) return true;

        try
        {
            return conn.ExecuteScalar<int>("SELECT is_fired FROM managers ORDER BY id DESC LIMIT 1") == 1;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    VisualElement CreateSlotCard(SaveSlotInfo slot)
    {
        var card = new VisualElement();
        card.AddToClassList("slot-card");

        // Cada partida muestra las imágenes de SU ámbito (save_{n}); si aún no tiene
        // carpeta propia (partida antigua) se usa el template, que es lo que heredará
        // al cargarse por primera vez.
        string scope = SlotScope(slot.slotNumber);

        // ── Arena photo ──
        var arenaPhoto = new VisualElement();
        arenaPhoto.AddToClassList("slot-arena-photo");
        if (!string.IsNullOrEmpty(slot.teamLogo))
        {
            var arenaSpr = TeamImages.ArenaFor(scope, slot.teamLogo);
            if (arenaSpr != null)
                arenaPhoto.style.backgroundImage = new StyleBackground(arenaSpr);
        }
        card.Add(arenaPhoto);

        // ── Manager name ──
        var managerRow = new VisualElement();
        managerRow.AddToClassList("slot-manager-row");

        if (_empleadoTex != null)
        {
            var managerIcon = new VisualElement();
            managerIcon.AddToClassList("slot-manager-icon");
            managerIcon.style.backgroundImage = new StyleBackground(_empleadoTex);
            managerRow.Add(managerIcon);
        }

        var managerLbl = new Label(slot.managerName ?? "Manager");
        managerLbl.AddToClassList("slot-manager-name");
        managerRow.Add(managerLbl);
        card.Add(managerRow);

        // ── Team section: logo + name ──
        var teamSection = new VisualElement();
        teamSection.AddToClassList("slot-team-section");

        var logoElem = new VisualElement();
        logoElem.AddToClassList("slot-team-logo");
        if (!string.IsNullOrEmpty(slot.teamLogo))
        {
            var logoSpr = TeamImages.LogoFor(scope, 64, slot.teamLogo);
            if (logoSpr != null)
                logoElem.style.backgroundImage = new StyleBackground(logoSpr);
        }
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

        // ── Mode badge ──
        string modeText = slot.gameMode == "promanager" ? "PROMANAGER" : "MANAGER";
        var modeBadge = new VisualElement();
        modeBadge.AddToClassList("slot-mode-badge");
        var modeLbl = new Label(modeText);
        modeLbl.AddToClassList("slot-mode-label");
        modeBadge.Add(modeLbl);
        card.Add(modeBadge);

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

        // ── Last saved date ──
        if (!string.IsNullOrEmpty(slot.lastPlayedRealDate))
        {
            var savedDateRow = new VisualElement();
            savedDateRow.AddToClassList("slot-saved-date-row");

            if (_calendarTex != null)
            {
                var calIcon = new VisualElement();
                calIcon.AddToClassList("slot-saved-date-icon");
                calIcon.style.backgroundImage = new StyleBackground(_calendarTex);
                savedDateRow.Add(calIcon);
            }

            var savedDateLbl = new Label { text = FormatSavedDate(slot.lastPlayedRealDate) };
            savedDateLbl.AddToClassList("slot-saved-date-text");
            savedDateRow.Add(savedDateLbl);
            card.Add(savedDateRow);
        }

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

    string FormatMoney(long amount)
    {
        if (amount >= 1_000_000)
            return $"${amount / 1_000_000}M";
        return $"${amount:N0}";
    }

    string FormatSavedDate(string date)
    {
        if (string.IsNullOrEmpty(date)) return "";
        if (System.DateTime.TryParseExact(date, "dd/MM/yyyy HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
            return dt.ToString("dd/MM/yyyy  HH:mm");
        return date;
    }

    static System.DateTime ParseSlotDate(string date)
    {
        if (string.IsNullOrEmpty(date)) return System.DateTime.MinValue;
        if (System.DateTime.TryParseExact(date, "dd/MM/yyyy HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return System.DateTime.MinValue;
    }

    void OnLoadSlot(int slotNumber)
    {
        if (IsSlotFired(slotNumber)) return;

        if (!DatabaseManager.Instance.InitSaveSlot(slotNumber))
        {
            Debug.LogError($"[LoadGame] No se pudo abrir la partida del slot {slotNumber} (DB corrupta o bloqueada).");
            return;
        }
        GameSaveManager.UpdateSlotFromDatabase(slotNumber);
        ScreenManager.Instance.GoTo(GameScreen.Dashboard);
    }

    // ── COPIAS DE SEGURIDAD (QoL) ───────────────────────────
    /// <summary>Definición de una fila de las listas de los modales.</summary>
    class ListRowDef
    {
        public string logo;
        public string title;
        public string sub;
        public System.Action onClick;
        public System.Action onDelete;
    }

    VisualElement BuildTransferRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.Center;
        row.style.marginBottom = 10;

        var saveBtn = new Button { text = "GUARDAR COPIA" };
        saveBtn.AddToClassList("modal-btn");
        saveBtn.AddToClassList("modal-btn--primary");
        saveBtn.style.marginRight = 8;
        saveBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowExportModal(); });
        row.Add(saveBtn);

        var restoreBtn = new Button { text = "RESTAURAR COPIA" };
        restoreBtn.AddToClassList("modal-btn");
        restoreBtn.AddToClassList("modal-btn--secondary");
        restoreBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); ShowImportModal(); });
        row.Add(restoreBtn);

        CursorManager.Instance?.RegisterHandCursor(saveBtn);
        CursorManager.Instance?.RegisterHandCursor(restoreBtn);
        return row;
    }

    void ShowExportModal()
    {
        var defs = new List<ListRowDef>();
        foreach (var slot in GameSaveManager.GetAllSlots().Where(x => x.exists))
        {
            int slotNumber = slot.slotNumber;
            defs.Add(new ListRowDef
            {
                logo = slot.teamLogo,
                title = slot.teamName ?? "Equipo",
                sub = $"{slot.managerName} · {slot.seasonYear} · Jugado {slot.lastPlayedRealDate}",
                onClick = () => DoExport(slotNumber)
            });
        }
        if (defs.Count == 0)
            defs.Add(new ListRowDef { title = "No hay partidas que copiar", sub = "Crea una partida desde el menú principal" });

        ShowScrollListModal("GUARDAR COPIA DE PARTIDA", defs,
            "Elige la partida de la que quieres una copia de seguridad (no se modifica).",
            new List<(string, System.Action)> { ("ABRIR CARPETA", OpenExportsFolder) });
    }

    void DoExport(int slot)
    {
        var info = GameSaveManager.GetSlot(slot);
        string path = GameSaveManager.ExportSlot(slot, info?.managerName ?? "manager", info?.teamName ?? "equipo");
        if (path == null)
        {
            ShowMessageModal("GUARDAR COPIA", "No se pudo crear la copia de la partida.", null);
            return;
        }
        ShowMessageModal("COPIA GUARDADA",
            "Se ha creado una copia de seguridad congelada de esta partida.\n\n" +
            $"Archivo: {Path.GetFileName(path)}\n\n" +
            "El juego NO modifica esa copia: sirve para volver a este punto con RESTAURAR COPIA " +
            "o para guardarla fuera del juego (USB, nube...).",
            new List<(string, System.Action)> { ("ABRIR CARPETA", OpenExportsFolder) });
    }

    void ShowImportModal()
    {
        var defs = new List<ListRowDef>();
        foreach (var exp in GameSaveManager.ListExportInfos())
        {
            var e = exp;
            defs.Add(new ListRowDef
            {
                logo = e.teamLogo,
                title = string.IsNullOrEmpty(e.teamName) ? e.fileName : $"{e.teamName} · {e.managerName}",
                sub = $"{e.date:dd/MM/yyyy HH:mm} · {FormatSize(e.sizeBytes)} · Pulsa para restaurar",
                onClick = () => DoImport(e.path),
                onDelete = () => { GameSaveManager.DeleteExport(e.path); RefreshSlots(); ShowImportModal(); }
            });
        }
        if (defs.Count == 0)
            defs.Add(new ListRowDef { title = "No hay copias guardadas", sub = "Usa GUARDAR COPIA para crear una" });

        ShowScrollListModal("RESTAURAR COPIA", defs,
            "Se restaurará como partida NUEVA en el siguiente slot libre (no borra ninguna partida).",
            new List<(string, System.Action)> { ("ABRIR CARPETA", OpenExportsFolder) });
    }

    void DoImport(string file)
    {
        int slot = GameSaveManager.ImportSave(file);
        if (slot < 0)
        {
            ShowMessageModal("RESTAURAR COPIA", "No se pudo restaurar la copia (archivo no válido).", null);
            return;
        }
        if (DatabaseManager.Instance.InitSaveSlot(slot))
            GameSaveManager.UpdateSlotFromDatabase(slot);
        RefreshSlots();
        ShowMessageModal("COPIA RESTAURADA",
            $"La copia se ha restaurado como partida en el slot {slot}. Ya aparece en la lista para cargarla.",
            null);
    }

    void OpenExportsFolder()
    {
        try
        {
            Directory.CreateDirectory(GameSaveManager.ExportsDir);
            Application.OpenURL("file://" + GameSaveManager.ExportsDir);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LoadGame] No se pudo abrir la carpeta de copias: {e.Message}");
        }
    }

    static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024f / 1024f:N1} MB";
        if (bytes >= 1024) return $"{bytes / 1024f:N0} KB";
        return $"{bytes} B";
    }

    // ── MODALES PROCEDURALES ────────────────────────────────
    /// <summary>Modal con lista scrollable de alto fijo (caben ~4-5 filas).</summary>
    void ShowScrollListModal(string title, List<ListRowDef> defs, string subtitle,
                             List<(string label, System.Action action)> footerExtra)
    {
        var overlay = CreateModalBase(title, subtitle, out var box);

        var listWrap = new VisualElement();
        listWrap.style.flexDirection = FlexDirection.Row;
        // Alto para 3 filas (80px de fila + 6px de margen = 86; 3x86 = 258) con holgura.
        listWrap.style.height = 264;
        listWrap.style.marginLeft = 24;
        listWrap.style.marginRight = 24;

        var viewport = new VisualElement();
        viewport.AddToClassList("lgx-viewport");
        var content = new VisualElement();
        content.AddToClassList("lgx-content");
        viewport.Add(content);
        listWrap.Add(viewport);

        var track = new VisualElement();
        track.AddToClassList("lgx-track");
        var thumb = new VisualElement();
        thumb.AddToClassList("lgx-thumb");
        track.Add(thumb);
        listWrap.Add(track);

        foreach (var d in defs)
        {
            var def = d;
            content.Add(BuildListRow(def.logo, def.title, def.sub,
                def.onClick == null ? null : () => { overlay.RemoveFromHierarchy(); def.onClick(); },
                def.onDelete == null ? null : () => { overlay.RemoveFromHierarchy(); def.onDelete(); }));
        }

        box.Add(listWrap);
        CustomScrollbar.Bind(viewport, content, track, thumb);
        AddModalFooter(overlay, box, footerExtra);
        _root.Add(overlay);
    }

    VisualElement BuildListRow(string logoName, string title, string sub, System.Action onClick, System.Action onDelete)
    {
        var row = new VisualElement();
        row.AddToClassList("lgx-row");

        var logo = new VisualElement();
        logo.AddToClassList("lgx-logo");
        var sprite = string.IsNullOrEmpty(logoName) ? null : TeamImages.Logo(64, logoName);
        if (sprite != null) logo.style.backgroundImage = new StyleBackground(sprite);
        row.Add(logo);

        var col = new VisualElement();
        col.AddToClassList("lgx-col");
        var titleLbl = new Label(title);
        titleLbl.AddToClassList("lgx-title");
        col.Add(titleLbl);
        if (!string.IsNullOrEmpty(sub))
        {
            var subLbl = new Label(sub);
            subLbl.AddToClassList("lgx-sub");
            subLbl.style.whiteSpace = WhiteSpace.Normal;
            col.Add(subLbl);
        }
        row.Add(col);

        if (onClick != null)
        {
            row.RegisterCallback<ClickEvent>(_ => { PlayClick(); onClick(); });
            CursorManager.Instance?.RegisterHandCursor(row);
        }

        if (onDelete != null)
        {
            var del = new Button { text = "✕" };
            del.AddToClassList("lgx-del");
            del.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); PlayClick(); onDelete(); });
            CursorManager.Instance?.RegisterHandCursor(del);
            row.Add(del);
        }

        return row;
    }

    void ShowMessageModal(string title, string text, List<(string label, System.Action action)> footerExtra)
    {
        var overlay = CreateModalBase(title, null, out var box);
        var body = new VisualElement();
        body.AddToClassList("modal-body");
        var label = new Label(text);
        label.AddToClassList("modal-paragraph");
        label.style.whiteSpace = WhiteSpace.Normal;
        body.Add(label);
        box.Add(body);
        AddModalFooter(overlay, box, footerExtra);
        _root.Add(overlay);
    }

    VisualElement CreateModalBase(string title, string subtitle, out VisualElement box)
    {
        var overlay = new VisualElement();
        overlay.AddToClassList("modal-overlay");
        // Las clases traen display:none (los modales de UXML los activan por código).
        overlay.style.display = DisplayStyle.Flex;
        overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.6f));

        box = new VisualElement();
        box.AddToClassList("modal-box");
        box.style.display = DisplayStyle.Flex;
        box.style.flexDirection = FlexDirection.Column;
        box.style.position = Position.Relative; // .modal-box es absolute: así lo centra el overlay
        box.style.width = 820;
        overlay.Add(box);

        var t = new Label(title);
        t.AddToClassList("modal-title");
        box.Add(t);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var sub = new Label(subtitle);
            sub.AddToClassList("modal-paragraph");
            sub.style.marginTop = 10;
            sub.style.marginLeft = 24;
            sub.style.marginRight = 24;
            sub.style.fontSize = 16;
            box.Add(sub);
        }
        return overlay;
    }

    void AddModalFooter(VisualElement overlay, VisualElement box, List<(string label, System.Action action)> extra)
    {
        var actions = new VisualElement();
        actions.AddToClassList("modal-actions");

        if (extra != null)
        {
            foreach (var (label, action) in extra)
            {
                var b = new Button { text = label };
                b.AddToClassList("modal-btn");
                b.AddToClassList("modal-btn--primary");
                b.style.marginRight = 8;
                var act = action;
                b.RegisterCallback<ClickEvent>(_ => { PlayClick(); act?.Invoke(); });
                CursorManager.Instance?.RegisterHandCursor(b);
                actions.Add(b);
            }
        }

        var close = new Button { text = "CERRAR" };
        close.AddToClassList("modal-btn");
        close.AddToClassList("modal-btn--secondary");
        close.RegisterCallback<ClickEvent>(_ => { PlayClick(); overlay.RemoveFromHierarchy(); });
        CursorManager.Instance?.RegisterHandCursor(close);
        actions.Add(close);
        box.Add(actions);
    }

    static bool IsSlotFired(int slotNumber)
    {
        try
        {
            string dbPath = GameSaveManager.GetSaveDbPath(slotNumber);
            if (!File.Exists(dbPath)) return false;

            using var conn = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly);
            var manager = conn.Query<ManagerStatusRow>(
                "SELECT id, team_id, trust, game_mode, budget_red_warnings FROM managers ORDER BY id DESC LIMIT 1")
                .FirstOrDefault();
            return IsRowFired(conn, manager);
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    void OnDeleteSlot(int slotNumber)
    {
        _pendingDeleteSlot = slotNumber;
        OpenDeleteModal();
    }

    void OpenDeleteModal()
    {
        CursorManager.Instance?.SetDefaultCursor();
        if (_contentOverlay != null) _contentOverlay.style.display = DisplayStyle.Flex;
        _deleteModalOverlay.style.display = DisplayStyle.Flex;
        _deleteModalBox.style.display = DisplayStyle.Flex;
    }

    void CloseDeleteModal()
    {
        if (_contentOverlay != null) _contentOverlay.style.display = DisplayStyle.None;
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

public class ManagerStatusRow
{
    public int    id                  { get; set; }
    public int    team_id             { get; set; }
    public int    trust               { get; set; }
    public string game_mode           { get; set; }
    public int    budget_red_warnings { get; set; }
}
