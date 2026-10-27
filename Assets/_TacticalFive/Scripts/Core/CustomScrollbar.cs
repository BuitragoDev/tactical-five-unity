using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Barra de scroll propia (viewport + content + track/thumb). El ScrollView de
/// Unity no es fiable con el tema actual, así que se usa este control: el contenido
/// se desplaza con un <c>top</c> negativo y el thumb se puede arrastrar (o usar la
/// rueda del ratón). El track y el thumb se definen en el UXML/USS.
/// </summary>
public static class CustomScrollbar
{
    public class Handle
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

    private static readonly ConditionalWeakTable<VisualElement, Handle> _bound = new();

    public static Handle Bind(VisualElement viewport, VisualElement content,
                              VisualElement track, VisualElement thumb)
        => Bind<Handle>(viewport, content, track, thumb);

    /// <summary>
    /// Variante genérica para que cada controlador conserve su tipo de barra (si lo
    /// necesita) sin duplicar la lógica. Es idempotente dentro de la misma visita:
    /// si el viewport ya tiene una barra de este tipo, devuelve la existente.
    /// </summary>
    public static T Bind<T>(VisualElement viewport, VisualElement content,
                            VisualElement track, VisualElement thumb)
        where T : Handle, new()
    {
        if (viewport == null || content == null || track == null || thumb == null) return null;
        if (_bound.TryGetValue(viewport, out var existing) && existing is T typed)
            return typed;

        viewport.style.overflow = Overflow.Hidden;
        viewport.style.flexDirection = FlexDirection.Column;

        var bar = new T
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
            Apply(bar);
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
            Apply(bar);
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
            Apply(bar);
            evt.StopPropagation();
        });

        CursorManager.Instance?.RegisterHandCursor(thumb);
        CursorManager.Instance?.RegisterHandCursor(track);

        content.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));
        viewport.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));

        Apply(bar);
        _bound.Remove(viewport);
        _bound.Add(viewport, bar);
        return bar;
    }

    /// <summary>Vuelve al inicio (p. ej. al recargar/filtrar la lista).</summary>
    public static void Reset(Handle bar)
    {
        if (bar == null) return;
        bar.ScrollY = 0f;
        Apply(bar);
    }

    /// <summary>Reajusta el thumb tras un cambio de layout (p. ej. un panel oculto que se muestra).</summary>
    public static void Refresh(Handle bar) => ClampAndApply(bar);

    static float GetScrollMax(Handle bar)
    {
        if (bar == null || bar.Content == null || bar.Viewport == null) return 0f;
        return Mathf.Max(0f, bar.Content.layout.height - bar.Viewport.layout.height);
    }

    static void ClampAndApply(Handle bar)
    {
        if (bar == null) return;
        bar.ScrollY = Mathf.Clamp(bar.ScrollY, 0f, GetScrollMax(bar));
        Apply(bar);
    }

    static void Apply(Handle bar)
    {
        if (bar == null || bar.Content == null) return;

        bar.Content.style.top = -bar.ScrollY;

        float trackHeight = bar.Track.layout.height;
        float contentHeight = bar.Content.layout.height;
        float viewportHeight = bar.Viewport.layout.height;
        if (trackHeight <= 0f || contentHeight <= 0f) return;

        float max = Mathf.Max(0f, contentHeight - viewportHeight);
        float thumbHeight = max <= 0f
            ? trackHeight
            : Mathf.Max(36f, trackHeight * viewportHeight / contentHeight);
        thumbHeight = Mathf.Min(thumbHeight, trackHeight);
        float travel = trackHeight - thumbHeight;
        float ratio = max > 0f ? bar.ScrollY / max : 0f;

        bar.Thumb.style.height = thumbHeight;
        bar.Thumb.style.top = ratio * travel;
    }
}
