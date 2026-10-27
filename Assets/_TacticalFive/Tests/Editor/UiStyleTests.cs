using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

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
