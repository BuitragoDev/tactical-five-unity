using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class LoadGameController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _slotsContainer;
    private VisualElement _emptyState;
    private VisualElement _slotsArea;
    private Button _btnBack;

    private VisualElement _deleteModalOverlay;
    private VisualElement _deleteModalBox;
    private Button _btnDeleteYes;
    private Button _btnDeleteNo;

    private int _pendingDeleteSlot = -1;

    private Dictionary<string, Sprite> _logoSprites = new();

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        // Reset cursor al entrar (evita que se quede como mano de la pantalla anterior)
        CursorManager.Instance?.SetDefaultCursor();

        // Load team logos
        var logos = Resources.LoadAll<Sprite>("Teams/Logos/80x80/");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _slotsContainer = _root.Q<VisualElement>("SlotsContainer");
        _emptyState = _root.Q<VisualElement>("EmptyState");
        _slotsArea = _root.Q<VisualElement>("SlotsScrollView").parent;
        _btnBack = _root.Q<Button>("BtnBack");

        _btnBack?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.MainMenu); });

        // Modal borrar partida
        _deleteModalOverlay = _root.Q<VisualElement>("DeleteModalOverlay");
        _deleteModalBox = _root.Q<VisualElement>("DeleteModalBox");
        _btnDeleteYes = _root.Q<Button>("BtnDeleteYes");
        _btnDeleteNo = _root.Q<Button>("BtnDeleteNo");

        _btnDeleteYes?.RegisterCallback<ClickEvent>(_ => { PlayClick(); ConfirmDelete(); });
        _btnDeleteNo?.RegisterCallback<ClickEvent>(_ => { PlayClick(); CloseDeleteModal(); });
        _deleteModalOverlay?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _deleteModalOverlay)
                { PlayClick(); CloseDeleteModal(); }
        });

        // Escape para cerrar modal
        _root.focusable = true;
        _root.Focus();
        _root.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape && _deleteModalOverlay?.style.display == DisplayStyle.Flex)
                CloseDeleteModal();
        });

        // Cursores
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnBack);
            CursorManager.Instance.RegisterHandCursor(_btnDeleteYes);
            CursorManager.Instance.RegisterHandCursor(_btnDeleteNo);
        }

        // Limpiar DBs huérfanas antes de refrescar
        GameSaveManager.CleanupAllOrphanDbs();

        RefreshSlots();
    }

    void RefreshSlots()
    {
        _slotsContainer.Clear();

        var allSlots = GameSaveManager.GetAllSlots();
        // Solo slots verdaderamente válidos: exists + managerName + teamName
        var existingSlots = allSlots
            .Where(s => s.exists && !string.IsNullOrEmpty(s.managerName) && !string.IsNullOrEmpty(s.teamName))
            .ToList();

        if (existingSlots.Count == 0)
        {
            _slotsArea.style.display = DisplayStyle.None;
            _emptyState.style.display = DisplayStyle.Flex;
            return;
        }

        _slotsArea.style.display = DisplayStyle.Flex;
        _emptyState.style.display = DisplayStyle.None;

        foreach (var slot in existingSlots)
        {
            var card = CreateSlotCard(slot);
            _slotsContainer.Add(card);
        }
    }

    VisualElement CreateSlotCard(SaveSlotInfo slot)
    {
        var card = new VisualElement();
        card.AddToClassList("slot-card");

        // Team Logo
        var logoElem = new VisualElement();
        logoElem.AddToClassList("slot-team-logo");
        if (!string.IsNullOrEmpty(slot.teamLogo) && _logoSprites.TryGetValue(slot.teamLogo, out var sprite))
        {
            logoElem.style.backgroundImage = new StyleBackground(sprite);
        }
        card.Add(logoElem);

        // Info Block
        var info = new VisualElement();
        info.AddToClassList("slot-info");

        var managerLbl = new Label { text = slot.managerName ?? "Manager" };
        managerLbl.AddToClassList("slot-manager");
        info.Add(managerLbl);

        var teamLbl = new Label { text = slot.teamName ?? "Sin equipo" };
        teamLbl.AddToClassList("slot-team-name");
        info.Add(teamLbl);

        string metaText = $"Temporada {slot.seasonYear}";
        if (slot.currentGameDay > 0)
            metaText += $"  ·  Día {slot.currentGameDay}";
        var metaLbl = new Label { text = metaText };
        metaLbl.AddToClassList("slot-meta");
        info.Add(metaLbl);

        if (!string.IsNullOrEmpty(slot.currentDate))
        {
            var dateLbl = new Label { text = $"Fecha: {slot.currentDate}" };
            dateLbl.AddToClassList("slot-date");
            info.Add(dateLbl);
        }

        if (!string.IsNullOrEmpty(slot.lastPlayedRealDate))
        {
            var realDateLbl = new Label { text = $"Última sesión: {slot.lastPlayedRealDate}" };
            realDateLbl.AddToClassList("slot-date");
            info.Add(realDateLbl);
        }

        card.Add(info);

        // Actions
        var actions = new VisualElement();
        actions.AddToClassList("slot-actions");

        var loadBtn = new Button { text = "CARGAR" };
        loadBtn.AddToClassList("btn-load");
        loadBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnLoadSlot(slot.slotNumber); });
        if (CursorManager.Instance != null)
        {
            loadBtn.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
            loadBtn.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
        }
        actions.Add(loadBtn);

        var deleteBtn = new Button { text = "BORRAR" };
        deleteBtn.AddToClassList("btn-delete");
        deleteBtn.RegisterCallback<ClickEvent>(_ => { PlayClick(); OnDeleteSlot(slot.slotNumber); });
        if (CursorManager.Instance != null)
        {
            deleteBtn.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
            deleteBtn.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
        }
        actions.Add(deleteBtn);

        card.Add(actions);
        return card;
    }

    void OnLoadSlot(int slotNumber)
    {
        DatabaseManager.Instance.InitSaveSlot(slotNumber);
        GameSaveManager.UpdateSlotFromDatabase(slotNumber);

        // Ir al dashboard (continuar la partida donde se quedó)
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

    void PlayClick()
    {
        AudioManager.Instance?.PlaySFX("click");
    }
}
