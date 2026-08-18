using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class DorsalesController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Dorsales;

    // Tabs
    private Button _btnTabActuales;
    private Button _btnTabRetirados;
    private VisualElement _tabActuales;
    private VisualElement _tabRetirados;

    // Tables
    private VisualElement _tablaActuales;
    private VisualElement _tablaRetirados;

    // Data
    private List<PlayerData> _players;
    private List<RetiredNumberData> _retired;
    private Texture2D _tshirtTex;
    private StyleBackground _tshirtBg;
    private Texture2D _retiredTshirtTex;
    private StyleBackground _retiredTshirtBg;

    protected override void CacheReferences()
    {
        _btnTabActuales = _root.Q<Button>("BtnTabActuales");
        _btnTabRetirados = _root.Q<Button>("BtnTabRetirados");
        _tabActuales = _root.Q<VisualElement>("TabActuales");
        _tabRetirados = _root.Q<VisualElement>("TabRetirados");
        _tablaActuales = _root.Q<VisualElement>("TablaActuales");
        _tablaRetirados = _root.Q<VisualElement>("TablaRetirados");
    }

    protected override void LoadData()
    {
        base.LoadData();
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _retired = DatabaseManager.Instance.GetRetiredNumbers(_myTeam.id);
        _tshirtTex = Resources.Load<Texture2D>("Icons/tshirt");
        if (_tshirtTex != null)
            _tshirtBg = new StyleBackground(_tshirtTex);
        _retiredTshirtTex = Resources.Load<Texture2D>("Icons/retired_tshirt");
        if (_retiredTshirtTex != null)
            _retiredTshirtBg = new StyleBackground(_retiredTshirtTex);
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _btnTabActuales?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectTab(0); });
        _btnTabRetirados?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectTab(1); });
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Dorsales] RefreshHeader error: {ex.Message}"); }
        SelectTab(0);
        BuildTablaActuales();
        BuildTablaRetirados();
    }

    void SelectTab(int index)
    {
        bool showActuales = index == 0;
        _btnTabActuales?.EnableInClassList("dorsales-tab--active", showActuales);
        _btnTabRetirados?.EnableInClassList("dorsales-tab--active", !showActuales);
        if (_tabActuales != null) _tabActuales.style.display = showActuales ? DisplayStyle.Flex : DisplayStyle.None;
        if (_tabRetirados != null) _tabRetirados.style.display = showActuales ? DisplayStyle.None : DisplayStyle.Flex;
    }

    void BuildTablaActuales()
    {
        _tablaActuales.Clear();

        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);

        if (_players.Count == 0)
        {
            var empty = new Label();
            empty.AddToClassList("dorsales-empty");
            empty.text = "No hay jugadores en la plantilla.";
            _tablaActuales.Add(empty);
            return;
        }

        foreach (var p in _players.OrderBy(p => p.number).ThenByDescending(p => p.overall))
        {
            var card = new VisualElement();
            card.AddToClassList("dorsales-card");

            var shirt = new VisualElement();
            shirt.AddToClassList("dorsales-card-shirt");
            if (_tshirtBg != null)
                shirt.style.backgroundImage = _tshirtBg;

            var number = new Label();
            number.AddToClassList("dorsales-card-number");
            number.text = p.number.ToString();
            shirt.Add(number);

            card.Add(shirt);

            var name = new Label();
            name.AddToClassList("dorsales-card-name");
            name.text = $"{p.first_name} {p.last_name}".ToUpper();
            card.Add(name);

            _tablaActuales.Add(card);
        }
    }

    void BuildTablaRetirados()
    {
        _tablaRetirados.Clear();

        _retired = DatabaseManager.Instance.GetRetiredNumbers(_myTeam.id);

        if (_retired.Count == 0)
        {
            var empty = new Label();
            empty.AddToClassList("dorsales-empty");
            empty.text = "Este equipo a\u00fan no ha retirado ning\u00fan dorsal.";
            _tablaRetirados.Add(empty);
            return;
        }

        foreach (var r in _retired)
        {
            var card = new VisualElement();
            card.AddToClassList("dorsales-card");

            var shirt = new VisualElement();
            shirt.AddToClassList("dorsales-card-shirt");
            if (_retiredTshirtBg != null)
                shirt.style.backgroundImage = _retiredTshirtBg;

            var number = new Label();
            number.AddToClassList("dorsales-card-number");
            number.AddToClassList("dorsales-card-number--retired");
            number.text = r.number.ToString();
            shirt.Add(number);

            card.Add(shirt);

            var name = new Label();
            name.AddToClassList("dorsales-card-name");
            name.text = r.FullName.ToUpper();
            card.Add(name);

            _tablaRetirados.Add(card);
        }
    }
}
