using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Chips de hitos de carrera (E1), usados por Trayectoria. El prefijo de clase
/// (`trajectory`) selecciona los estilos del USS de esa pantalla.
/// </summary>
public static class MilestoneUI
{
    /// <summary>Hito conseguido: icono + texto + (opcional) fecha.</summary>
    public static VisualElement Achieved(MilestoneDef def, string dateText, string classPrefix)
    {
        var chip = new VisualElement();
        chip.AddToClassList($"{classPrefix}-milestone");
        chip.tooltip = $"{MilestoneCatalog.Label(def)} ({def.key})";

        var icon = new VisualElement();
        icon.AddToClassList($"{classPrefix}-milestone-icon");
        var tex = Resources.Load<Texture2D>(MilestoneCatalog.Icon(def));
        if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
        chip.Add(icon);

        var label = new Label(MilestoneCatalog.Label(def));
        label.AddToClassList($"{classPrefix}-milestone-text");
        chip.Add(label);

        if (!string.IsNullOrEmpty(dateText))
        {
            var date = new Label(dateText);
            date.AddToClassList($"{classPrefix}-milestone-date");
            chip.Add(date);
        }

        return chip;
    }

    /// <summary>Siguiente hito con su progreso (barra opcional).</summary>
    public static VisualElement Next(MilestoneDef def, int current, int percent, string classPrefix, bool withBar)
    {
        var chip = new VisualElement();
        chip.AddToClassList($"{classPrefix}-milestone");
        chip.AddToClassList($"{classPrefix}-milestone--next");
        chip.tooltip = $"Le faltan {(def.threshold - current):N0} para {MilestoneCatalog.Label(def)}";

        var icon = new VisualElement();
        icon.AddToClassList($"{classPrefix}-milestone-icon");
        var tex = Resources.Load<Texture2D>(MilestoneCatalog.Icon(def));
        if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
        chip.Add(icon);

        var label = new Label($"PRÓXIMO: {MilestoneCatalog.Label(def)}");
        label.AddToClassList($"{classPrefix}-milestone-text");
        chip.Add(label);

        if (withBar)
        {
            var bar = new VisualElement();
            bar.AddToClassList($"{classPrefix}-milestone-progress");
            var fill = new VisualElement();
            fill.AddToClassList($"{classPrefix}-milestone-progress-fill");
            fill.style.width = new StyleLength(new Length(Mathf.Clamp(percent, 0, 100), LengthUnit.Percent));
            bar.Add(fill);
            chip.Add(bar);
        }

        var pct = new Label($"{current:N0} / {def.threshold:N0} · {percent} %");
        pct.AddToClassList($"{classPrefix}-milestone-pct");
        chip.Add(pct);

        return chip;
    }
}
