using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class LoadGameController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _slotsContainer;
    private VisualElement _emptyState;
    private Button _btnBack;

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        _slotsContainer = _root.Q<VisualElement>("SlotsContainer");
        _emptyState = _root.Q<VisualElement>("EmptyState");
        _btnBack = _root.Q<Button>("BtnBack");

        _btnBack?.RegisterCallback<ClickEvent>(_ => ScreenManager.Instance.GoTo(GameScreen.MainMenu));

        RefreshSlots();
    }

    void RefreshSlots()
    {
        _slotsContainer.Clear();

        var allSlots = GameSaveManager.GetAllSlots();
        var existingSlots = allSlots.Where(s => s.exists).ToList();

        if (existingSlots.Count == 0)
        {
            _slotsContainer.style.display = DisplayStyle.None;
            _emptyState.style.display = DisplayStyle.Flex;
            return;
        }

        _slotsContainer.style.display = DisplayStyle.Flex;
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

        // Info del slot
        var info = new VisualElement();
        info.AddToClassList("slot-info");

        var managerLbl = new Label { text = slot.managerName ?? "Manager" };
        managerLbl.AddToClassList("slot-manager");
        info.Add(managerLbl);

        string metaText = $"{slot.teamName}  ·  Temp. {slot.seasonYear}";
        var metaLbl = new Label { text = metaText };
        metaLbl.AddToClassList("slot-meta");
        info.Add(metaLbl);

        if (!string.IsNullOrEmpty(slot.currentDate))
        {
            var dateLbl = new Label { text = $"Fecha in-game: {slot.currentDate}" };
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

        // Acciones
        var actions = new VisualElement();
        actions.AddToClassList("slot-actions");

        var loadBtn = new Button { text = "CARGAR" };
        loadBtn.AddToClassList("btn-load");
        loadBtn.RegisterCallback<ClickEvent>(_ => OnLoadSlot(slot.slotNumber));
        actions.Add(loadBtn);

        var deleteBtn = new Button { text = "BORRAR" };
        deleteBtn.AddToClassList("btn-delete");
        deleteBtn.RegisterCallback<ClickEvent>(_ => OnDeleteSlot(slot.slotNumber));
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
        GameSaveManager.DeleteSave(slotNumber);
        RefreshSlots();
    }
}
