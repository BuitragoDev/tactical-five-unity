using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// F7-01: la UI no usa `rgba()`. La transparencia se resuelve con colores
/// compuestos sobre una base conocida (o `opacity` cuando el diseño lo permite).
/// </summary>
public class UiStyleTests
{
    [Test]
    public void UiStyles_DoNotUseRgba()
    {
        string root = Path.Combine(Application.dataPath, "_TacticalFive", "UI");
        Assert.That(Directory.Exists(root), Is.True, $"No existe {root}");

        var offenders = new List<string>();
        foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".uss") || f.EndsWith(".uxml")))
        {
            if (File.ReadAllText(file).Contains("rgba("))
                offenders.Add(file.Substring(root.Length).TrimStart('/', '\\'));
        }

        Assert.That(offenders, Is.Empty,
            "La UI todavía usa rgba():\n" + string.Join("\n", offenders.OrderBy(x => x)));
    }

    [Test]
    public void PanelSettings_UseResponsiveScaling()
    {
        var settings = Resources.Load<PanelSettings>("TacticalFivePanelSettings");
        Assert.That(settings, Is.Not.Null, "Falta TacticalFivePanelSettings en Resources");

        Assert.That(settings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize),
            "La UI debe escalar con el tamaño de pantalla, no con el DPI físico");
        Assert.That(settings.referenceResolution, Is.EqualTo(new Vector2Int(1920, 1080)));
        Assert.That(settings.screenMatchMode, Is.EqualTo(PanelScreenMatchMode.MatchWidthOrHeight));
        Assert.That(settings.match, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void Modals_FitTheMinimumViewport()
    {
        string root = Path.Combine(Application.dataPath, "_TacticalFive", "UI");
        int maxWidth = 0;
        int maxHeight = 0;
        var widthRe = new Regex(@"width:\s*(\d+)px");
        var heightRe = new Regex(@"height:\s*(\d+)px");

        foreach (string file in Directory.GetFiles(root, "*.uss", SearchOption.AllDirectories))
        {
            foreach (string block in File.ReadAllText(file).Split('}'))
            {
                if (!Regex.IsMatch(block, @"modal|roster-box|lottery-box"))
                    continue;
                foreach (Match m in widthRe.Matches(block))
                    maxWidth = Mathf.Max(maxWidth, int.Parse(m.Groups[1].Value));
                foreach (Match m in heightRe.Matches(block))
                    maxHeight = Mathf.Max(maxHeight, int.Parse(m.Groups[1].Value));
            }
        }

        Assert.That(maxWidth, Is.GreaterThan(0), "No se encontró ningún ancho de modal");
        Assert.That(maxHeight, Is.GreaterThan(0), "No se encontró ningún alto de modal");

        foreach (var (w, h) in new[] { (1280, 720), (1366, 768), (1920, 1080), (2560, 1440), (3440, 1440) })
        {
            // PanelSettings usa ScaleWithScreenSize con MatchWidthOrHeight = 0.5.
            float scale = Mathf.Sqrt((w / 1920f) * (h / 1080f));
            Assert.That(maxWidth * scale, Is.LessThanOrEqualTo(w),
                $"Un modal de {maxWidth}px se sale a {w}x{h} (escala {scale:F3})");
            Assert.That(maxHeight * scale, Is.LessThanOrEqualTo(h),
                $"Un modal de {maxHeight}px de alto se sale a {w}x{h} (escala {scale:F3})");
        }
    }

    [Test]
    public void UiStyles_UseStandardTextTransform()
    {
        string root = Path.Combine(Application.dataPath, "_TacticalFive", "UI");
        Assert.That(Directory.Exists(root), Is.True, $"No existe {root}");

        var offenders = new List<string>();
        foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".uss") || f.EndsWith(".uxml")))
        {
            string text = File.ReadAllText(file);
            if (text.Contains("-unity-text-transform") || text.Contains("text-transform: upper;"))
                offenders.Add(file.Substring(root.Length).TrimStart('/', '\\'));
        }

        Assert.That(offenders, Is.Empty,
            "La UI todavía usa -unity-text-transform o text-transform: upper:\n" +
            string.Join("\n", offenders.OrderBy(x => x)));
    }
}
