using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

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
                ? $"Activo desde el {FormatGameDayDate(s.started_game_day)} · {IntensityLabel(s.intensity)}"
                : $"Del {FormatGameDayDate(s.started_game_day)} al {FormatGameDayDate(s.last_update_game_day)}"
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

    /// <summary>
    /// Convierte una jornada interna a la fecha de la partida (dd/MM/yyyy): usa el
    /// game_date de un partido de esa jornada y, si no lo hay, la deriva del inicio
    /// de temporada (22 de octubre + días).
    /// </summary>
    string FormatGameDayDate(int gameDay)
    {
        if (gameDay <= 0) return "—";
        var game = DatabaseManager.Instance.GetAllGamesByGameDay(_manager.id, gameDay)
            .FirstOrDefault(g => !string.IsNullOrEmpty(g.game_date));
        if (game != null && System.DateTime.TryParse(game.game_date, out var gd))
            return gd.ToString("dd/MM/yyyy");
        if (_season != null)
            return new System.DateTime(_season.year_start, 10, 22)
                .AddDays(gameDay - 1).ToString("dd/MM/yyyy");
        return "—";
    }

    /// <summary>Intensidad del hilo redactada de forma clara.</summary>
    static string IntensityLabel(int intensity) => intensity switch
    {
        <= 1 => "acaba de empezar",
        2 => "va a más",
        3 => "en su punto álgido",
        _ => "al máximo"
    };

    static string ResolutionLabel(string resolution) => resolution switch
    {
        StorylineResolution.Broken => "terminó",
        StorylineResolution.Completed => "resuelto",
        StorylineResolution.Abandoned => "cerrado al acabar la temporada",
        StorylineResolution.Departed => "el jugador salió del equipo",
        _ => resolution
    };
}
