using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

/// <summary>
/// Marca de las clases de barra de scroll anidadas en los controladores (mismo
/// patrón copiado en varias pantallas). No tiene miembros: solo identifica el tipo.
/// </summary>
public interface IScrollBarHandle { }

/// <summary>
/// Evita re-enlazar la barra de scroll del MISMO viewport dentro de una misma visita.
/// Los controladores reconstruyen sus listas (filtros, pestañas, teclas de búsqueda) y
/// vuelven a llamar a BindScrollBar, lo que acumulaba handlers de rueda/arrastre y
/// volvía el scroll errático. El árbol de UI Toolkit se recrea al reactivar la
/// pantalla, así que los viewports nuevos se enlazan solos en la siguiente visita.
/// Se usa una ConditionalWeakTable para no retener los elementos (sin fugas).
/// </summary>
public static class ScrollBarBinder
{
    private static readonly ConditionalWeakTable<VisualElement, object> _bound = new();

    /// <summary>Devuelve la barra ya enlazada a ese viewport en esta visita, si la hay.</summary>
    public static bool TryGet<T>(VisualElement viewport, out T bar) where T : class
    {
        bar = null;
        if (viewport == null) return false;
        if (_bound.TryGetValue(viewport, out var stored) && stored is T typed)
        {
            bar = typed;
            return true;
        }
        return false;
    }

    public static void Remember(VisualElement viewport, object bar)
    {
        if (viewport == null || bar == null) return;
        _bound.Remove(viewport);
        _bound.Add(viewport, bar);
    }
}
