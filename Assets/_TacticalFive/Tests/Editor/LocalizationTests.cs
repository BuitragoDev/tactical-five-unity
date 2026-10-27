using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// F7-06: los textos de UI salen de una tabla única con claves. Este test
/// impide que vuelvan literales estáticos a UXML/controllers.
/// </summary>
public class LocalizationTests
{
    static readonly Regex TokenRegex = new Regex(@"\{loc:([A-Za-z0-9_.-]+)\}");
    static readonly Regex TextAttrRegex = new Regex(@"\btext=""([^""]*)""");
    static readonly Regex PlaceholderAttrRegex = new Regex(@"\b(?:placeholder|placeholder-text)=""([^""]*)""");
    static readonly Regex CsTextRegex = new Regex(@"(?<prefix>\btext\s*=\s*)""(?<value>(?:\\.|[^""\\])*)""");
    static readonly Regex CsLabelRegex = new Regex(@"new\s+Label\(\s*""(?<value>(?:\\.|[^""\\])*)""\s*\)");
    static readonly Regex CsKeyRegex = new Regex(@"LocalizationService\.T\(""([^""]+)""");
    static readonly Regex UxmlTagRegex = new Regex(@"<[^>]+>", RegexOptions.Singleline);

    static string DataRoot => Path.Combine(Application.dataPath, "_TacticalFive");
    static string UiRoot => Path.Combine(DataRoot, "UI");
    static string ScriptsUiRoot => Path.Combine(DataRoot, "Scripts", "UI");
    static string ScreenControllerPath => Path.Combine(DataRoot, "Scripts", "Core", "UIScreenController.cs");

    [SetUp]
    public void SetUp()
    {
        LocalizationService.SetLanguage(LocalizationService.DefaultLanguage);
        LocalizationService.Reload();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey("TF_Language");
        PlayerPrefs.Save();
        LocalizationService.Reload();
    }

    [Test]
    public void SpanishTable_LoadsCoreEntries()
    {
        Assert.That(LocalizationService.TryGet("ui.common.manager", out string manager), Is.True);
        Assert.That(manager, Is.EqualTo("Manager: {0}"));

        Assert.That(LocalizationService.TryGet("ui.common.season", out string season), Is.True);
        Assert.That(season, Is.EqualTo("Temporada {0}-{1}"));
    }

    [Test]
    public void MissingKey_ReturnsKeyAndDoesNotThrow()
    {
        Assert.That(LocalizationService.T("ui.no.existe"), Is.EqualTo("ui.no.existe"));
        Assert.That(LocalizationService.TryGet("ui.no.existe", out _), Is.False);
    }

    [Test]
    public void Templates_AreFormattedWithArguments()
    {
        Assert.That(LocalizationService.T("ui.common.manager", "Buitrago"), Is.EqualTo("Manager: Buitrago"));
        Assert.That(LocalizationService.T("ui.common.season", 2026, 2027), Is.EqualTo("Temporada 2026-2027"));
    }

    [Test]
    public void Apply_ResolvesTextsAndTooltips()
    {
        var root = new VisualElement();
        var label = new Label("{loc:ui.common.manager}");
        var button = new Button { text = "{loc:ui.common.season}" };
        root.Add(label);
        root.Add(button);
        root.tooltip = "{loc:ui.common.manager}";
        var search = new TextField();
        search.textEdition.placeholder = "{loc:ui.common.manager}";
        root.Add(search);

        LocalizationService.Apply(root);

        Assert.That(label.text, Is.EqualTo("Manager: {0}"));
        Assert.That(button.text, Is.EqualTo("Temporada {0}-{1}"));
        Assert.That(root.tooltip, Is.EqualTo("Manager: {0}"));
        Assert.That(search.textEdition.placeholder, Is.EqualTo("Manager: {0}"));
    }

    [Test]
    public void UxmlTexts_AreTokensOrNonTranslatable()
    {
        var offenders = new List<string>();
        foreach (string file in Directory.GetFiles(UiRoot, "*.uxml", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            foreach (Match tag in UxmlTagRegex.Matches(source))
            {
                foreach (Match match in TextAttrRegex.Matches(tag.Value)
                             .Cast<Match>()
                             .Concat(PlaceholderAttrRegex.Matches(tag.Value).Cast<Match>()))
                {
                    string raw = WebUtility.HtmlDecode(match.Groups[1].Value);
                    if (HasLetter(raw) && !raw.TrimStart().StartsWith("{loc:"))
                        offenders.Add($"{Relative(file)}: {raw.Trim()}");
                }
            }
        }

        Assert.That(offenders, Is.Empty,
            "Hay textos UXML sin clave de localización:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void LocalizationTokens_HaveEntries()
    {
        var tokens = new HashSet<string>();
        foreach (string file in Directory.GetFiles(UiRoot, "*.uxml", SearchOption.AllDirectories))
            tokens.UnionWith(TokenRegex.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value));

        foreach (string file in Directory.GetFiles(ScriptsUiRoot, "*.cs", SearchOption.AllDirectories)
                     .Append(ScreenControllerPath))
        {
            if (!File.Exists(file)) continue;
            string source = File.ReadAllText(file);
            tokens.UnionWith(TokenRegex.Matches(source).Select(m => m.Groups[1].Value));
            tokens.UnionWith(CsKeyRegex.Matches(source).Select(m => m.Groups[1].Value));
        }

        tokens.Remove("...");
        tokens.Remove("clave");

        var missing = tokens.Where(key => !LocalizationService.TryGet(key, out _)).OrderBy(k => k).ToList();
        Assert.That(missing, Is.Empty,
            "Tokens sin entrada en es.json:\n" + string.Join("\n", missing));
    }

    [Test]
    public void AllUxml_AfterApply_HaveNoUnresolvedTokens()
    {
        foreach (string file in Directory.GetFiles(UiRoot, "*.uxml", SearchOption.AllDirectories))
        {
            string relative = file.Replace(Path.DirectorySeparatorChar, '/');
            string assetPath = "Assets/" + relative.Substring(Application.dataPath.Length + 1);
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
            Assert.That(asset, Is.Not.Null, $"No se pudo cargar {assetPath}");

            var root = asset.CloneTree();
            LocalizationService.Apply(root);

            var unresolved = root.Query<TextElement>().ToList()
                .Where(e => !string.IsNullOrEmpty(e.text) && e.text.Contains("{loc:"))
                .Select(e => e.text)
                .ToList();
            Assert.That(unresolved, Is.Empty,
                $"Tokens sin resolver en {assetPath}:\n" + string.Join("\n", unresolved));

            foreach (var field in root.Query<TextField>().ToList())
            {
                string placeholder = field.textEdition.placeholder;
                Assert.That(string.IsNullOrEmpty(placeholder) || !placeholder.Contains("{loc:"), Is.True,
                    $"Placeholder sin resolver en {assetPath}: {placeholder}");
            }
        }
    }

    [Test]
    public void MessagesUxml_AfterApply_HasNoTokens()
    {
        var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/_TacticalFive/UI/Screens/Messages/Messages.uxml");
        Assert.That(asset, Is.Not.Null);
        var root = asset.CloneTree();
        LocalizationService.Apply(root);

        var labels = root.Query<TextElement>().ToList();
        Assert.That(labels.Any(e => e.text.Contains("{loc:")), Is.False,
            "Quedaron tokens sin resolver en Messages.uxml");

        var search = root.Q<TextField>("SearchField");
        Assert.That(search, Is.Not.Null);
        Assert.That(search.textEdition.placeholder, Is.Not.Empty);
        Assert.That(search.textEdition.placeholder.Contains("{loc:"), Is.False,
            "El placeholder del buscador no se resolvió");
    }

    [Test]
    public void CSharpStaticUiTexts_AreLocalized()
    {
        var offenders = new List<string>();
        foreach (string file in Directory.GetFiles(ScriptsUiRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            foreach (Match match in CsTextRegex.Matches(source))
            {
                if (IsCommented(source, match.Index)) continue;
                string raw = match.Groups["value"].Value;
                if (raw.Contains("\\")) continue; // símbolos escapados (\u25C0, etc.)
                if (HasLetter(raw))
                    offenders.Add($"{Relative(file)}: text = {raw}");
            }
            foreach (Match match in CsLabelRegex.Matches(source))
            {
                if (IsCommented(source, match.Index)) continue;
                string raw = match.Groups["value"].Value;
                if (raw.Contains("\\")) continue;
                if (HasLetter(raw))
                    offenders.Add($"{Relative(file)}: new Label({raw})");
            }
        }

        Assert.That(offenders, Is.Empty,
            "Hay textos C# sin clave de localización:\n" + string.Join("\n", offenders));
    }

    static bool HasLetter(string value)
        => !string.IsNullOrEmpty(value) && value.Any(char.IsLetter);

    static bool IsCommented(string source, int index)
    {
        int lineStart = source.LastIndexOf('\n', Mathf.Max(0, index - 1)) + 1;
        string line = source.Substring(lineStart, index - lineStart).TrimStart();
        return line.StartsWith("//") || line.StartsWith("*");
    }

    static string Relative(string path)
        => path.Replace(DataRoot + Path.DirectorySeparatorChar, "");
}
