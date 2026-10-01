using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Pantalla HILOS: situaciones narrativas de la temporada que se abren, escalan y se
/// cierran (movida desde la pestaña HISTORIA de Palmarés).
/// </summary>
public class HilosController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Hilos;

    private VisualElement _historiaBody;
    private CustomScrollbar.Handle _historiaBar;

    protected override void CacheReferences()
    {
        _historiaBody = _root.Q<VisualElement>("HistoriaBody");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var icon = _root.Q<VisualElement>("HilosScreenIcon");
        if (icon != null)
        {
            var tex = Resources.Load<Texture2D>("Icons/mensajes");
            if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
        }
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Hilos] RefreshHeader error: {ex.Message}"); }
        BuildHilos();
        _root.Q<VisualElement>("ActualidadSubmenu")?.AddToClassList("nav-submenu--visible");
        _root.Q<Button>("SubmenuHilos")?.AddToClassList("nav-submenu-item--active");
    }

    void BuildHilos()
    {
        if (_historiaBody == null || _manager == null || _myTeam == null) return;
        _historiaBody.Clear();

        var storylines = DatabaseManager.Instance.GetStorylineHistory(_manager.id, _myTeam.id);
        if (storylines.Count == 0)
        {
            var empty = new Label("Todavía no hay hilos: las rachas, el descontento y los momentos de forma aparecerán aquí.");
            empty.AddToClassList("historia-empty");
            _historiaBody.Add(empty);
            return;
        }

        foreach (var s in storylines)
        {
            bool active = s.resolved == 0;
            var row = new VisualElement();
            row.AddToClassList("historia-row");
            row.AddToClassList(active ? "historia-row--active" : "historia-row--closed");

            var badge = new Label(active ? "ABIERTO" : "CERRADO");
            badge.AddToClassList("historia-badge");
            badge.AddToClassList(active ? "historia-badge--active" : "historia-badge--closed");
            row.Add(badge);

            var col = new VisualElement();
            col.AddToClassList("historia-col");

            var title = new Label(s.title ?? s.type);
            title.AddToClassList("historia-title");
            col.Add(title);

            if (!string.IsNullOrEmpty(s.summary))
            {
                var sum = new Label(s.summary);
                sum.AddToClassList("historia-summary");
                sum.style.whiteSpace = WhiteSpace.Normal;
                col.Add(sum);
            }

            string meta = active
                ? $"Jornada {s.started_game_day} · escalón {s.intensity}"
                : $"Jornada {s.started_game_day} → {s.last_update_game_day}"
                  + (string.IsNullOrEmpty(s.resolution) ? "" : $" · {ResolutionLabel(s.resolution)}");
            var metaLbl = new Label(meta);
            metaLbl.AddToClassList("historia-meta");
            col.Add(metaLbl);

            row.Add(col);
            _historiaBody.Add(row);
        }

        // Barra de scroll propia (una vez por visita).
        _historiaBar = CustomScrollbar.Bind(
            _root.Q<VisualElement>("HistoriaViewport"), _historiaBody,
            _root.Q<VisualElement>("HistoriaTrack"), _root.Q<VisualElement>("HistoriaThumb"));
    }

    static string ResolutionLabel(string resolution) => resolution switch
    {
        StorylineResolution.Broken => "terminó",
        StorylineResolution.Completed => "resuelto",
        StorylineResolution.Abandoned => "cerrado al acabar la temporada",
        StorylineResolution.Departed => "el jugador salió del equipo",
        _ => resolution
    };
}
