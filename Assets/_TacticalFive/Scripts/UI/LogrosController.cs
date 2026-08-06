using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class LogrosController : UIScreenController
{
    VisualElement _body;
    VisualElement _tabs;
    Label _progress;
    GmAchievementCategory? _selectedCategory;
    HashSet<string> _unlocked = new HashSet<string>();
    Dictionary<string, GmAchievementData> _unlockedData = new();

    protected override GameScreen ScreenId => GameScreen.Logros;

    protected override void CacheReferences()
    {
        _body = _root.Q<VisualElement>("LogrosBody");
        _tabs = _root.Q<VisualElement>("LogrosTabs");
        _progress = _root.Q<Label>("LogrosProgress");
    }

    protected override void Refresh()
    {
        base.Refresh();
        if (_body == null || _manager == null) return;

        // Backfill silencioso de logros de carrera para partidas ya avanzadas
        if (_season != null)
            AchievementService.BackfillCareer(_manager.id, _myTeam?.id ?? 0, _season.id);

        var unlockedList = DatabaseManager.Instance.GetAchievements(_manager.id);
        _unlocked = new HashSet<string>(unlockedList.Select(a => a.type));
        _unlockedData = unlockedList.ToDictionary(a => a.type, a => a);

        BuildTabs();
        BuildGrid();
    }

    void BuildTabs()
    {
        if (_tabs == null) return;
        _tabs.Clear();

        void MakeTab(string label, GmAchievementCategory? cat)
        {
            var btn = new Button();
            btn.AddToClassList("logros-tab");
            btn.text = label;
            if (_selectedCategory == cat)
                btn.AddToClassList("logros-tab--active");
            btn.clicked += () =>
            {
                PlayClick();
                _selectedCategory = cat;
                Rebuild();
            };
            _tabs.Add(btn);
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(btn);
        }

        MakeTab("TODOS", null);
        foreach (GmAchievementCategory cat in System.Enum.GetValues(typeof(GmAchievementCategory)))
            MakeTab(AchievementCatalog.CategoryName(cat), cat);
    }

    void Rebuild()
    {
        if (_body == null) return;
        BuildTabs();
        BuildGrid();
    }

    void BuildGrid()
    {
        if (_body == null) return;

        _body.Clear();

        var total = 0;
        var unlockedCount = 0;
        foreach (var def in AchievementCatalog.All)
        {
            if (_selectedCategory.HasValue && def.Category != _selectedCategory.Value) continue;
            total++;
            if (_unlocked.Contains(def.Type.ToString())) unlockedCount++;
        }

        if (_progress != null)
            _progress.text = $"DESBLOQUEADOS: {unlockedCount} / {total}";

        if (total == 0)
        {
            var empty = new Label("Sin logros en esta categoría.");
            empty.AddToClassList("logros-empty");
            _body.Add(empty);
            return;
        }

        var grid = new VisualElement();
        grid.AddToClassList("logros-grid");

        foreach (var def in AchievementCatalog.All)
        {
            if (_selectedCategory.HasValue && def.Category != _selectedCategory.Value) continue;
            grid.Add(BuildCard(def));
        }

        _body.Add(grid);
    }

    VisualElement BuildCard(GmAchievementDefinition def)
    {
        bool unlocked = _unlocked.Contains(def.Type.ToString());

        var card = new VisualElement();
        card.AddToClassList("logros-card");
        card.AddToClassList(unlocked ? "logros-card--unlocked" : "logros-card--locked");

        var title = new Label(def.Title);
        title.AddToClassList("logros-card-title");
        card.Add(title);

        var desc = new Label(def.Description);
        desc.AddToClassList("logros-card-desc");
        card.Add(desc);

        var status = new Label();
        status.AddToClassList("logros-card-status");
        if (unlocked)
        {
            status.AddToClassList("logros-card-status--unlocked");
            var data = _unlockedData.TryGetValue(def.Type.ToString(), out var d) ? d : null;
            string detail = data != null && !string.IsNullOrEmpty(data.season_label) ? data.season_label : "DESBLOQUEADO";
            status.text = $"★ {detail.ToUpper()}";
        }
        else
        {
            status.AddToClassList("logros-card-status--locked");
            status.text = "BLOQUEADO";
        }
        card.Add(status);

        return card;
    }
}