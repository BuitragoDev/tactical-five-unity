using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Localización ligera de Tactical Five (F7-06). El español es la tabla base;
/// el resto de idiomas se cargan de <c>Resources/Localization/{code}.json</c>.
/// Los UXML usan tokens <c>{loc:clave}</c> y los controllers pueden usar
/// <see cref="T(string, object[])"/> para plantillas con argumentos.
/// </summary>
public static class LocalizationService
{
    public const string DefaultLanguage = "es";
    const string LanguagePrefKey = "TF_Language";

    [Serializable]
    public class LocalizationTable
    {
        public string language;
        public LocalizationEntry[] entries;
    }

    [Serializable]
    public class LocalizationEntry
    {
        public string key;
        public string value;
    }

    static readonly Regex TokenRegex = new Regex(
        @"\{loc:([A-Za-z0-9_.-]+)\}", RegexOptions.Compiled);

    static readonly Dictionary<string, string> _entries = new Dictionary<string, string>();
    static readonly Dictionary<string, string> _fallback = new Dictionary<string, string>();
    static string _language;
    static bool _loaded;

    public static string Language
    {
        get
        {
            EnsureLoaded();
            return _language;
        }
    }

    public static IEnumerable<string> Keys
    {
        get
        {
            EnsureLoaded();
            return _entries.Keys;
        }
    }

    public static string T(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key)) return "";
        EnsureLoaded();

        string value;
        if (!_entries.TryGetValue(key, out value)
            && !_fallback.TryGetValue(key, out value))
        {
            return key;
        }

        if (args == null || args.Length == 0) return value;
        try { return string.Format(value, args); }
        catch (FormatException) { return value; }
    }

    public static bool TryGet(string key, out string value)
    {
        EnsureLoaded();
        value = null;
        return !string.IsNullOrEmpty(key)
            && (_entries.TryGetValue(key, out value)
                || _fallback.TryGetValue(key, out value));
    }

    /// <summary>Resuelve todos los tokens <c>{loc:...}</c> de un texto.</summary>
    public static string Resolve(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        EnsureLoaded();
        return TokenRegex.Replace(raw, m => T(m.Groups[1].Value));
    }

    /// <summary>
    /// Recorre un árbol UI Toolkit y resuelve tokens en textos y tooltips.
    /// Se llama al final de <c>UIScreenController.OnEnable</c>, de modo que
    /// también cubre el header y el sidebar inyectados en RegisterCallbacks.
    /// </summary>
    public static void Apply(VisualElement root)
    {
        if (root == null) return;
        EnsureLoaded();

        root.Query<TextElement>().ForEach(element =>
        {
            string text = element.text;
            if (!string.IsNullOrEmpty(text) && TokenRegex.IsMatch(text))
                element.text = Resolve(text);
        });

        root.Query<VisualElement>().ForEach(element =>
        {
            if (!string.IsNullOrEmpty(element.tooltip) && TokenRegex.IsMatch(element.tooltip))
                element.tooltip = Resolve(element.tooltip);
        });

        root.Query<TextField>().ForEach(field =>
        {
            string placeholder = field.textEdition.placeholder;
            if (!string.IsNullOrEmpty(placeholder) && TokenRegex.IsMatch(placeholder))
                field.textEdition.placeholder = Resolve(placeholder);
        });
    }

    public static void SetLanguage(string code)
    {
        if (string.IsNullOrEmpty(code)) code = DefaultLanguage;
        code = code.Trim().ToLowerInvariant();
        PlayerPrefs.SetString(LanguagePrefKey, code);
        PlayerPrefs.Save();
        _language = code;
        _loaded = false;
        EnsureLoaded();
    }

    /// <summary>Recarga la tabla actual; pensado para tests y cambios de idioma.</summary>
    public static void Reload()
    {
        _loaded = false;
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        _language = PlayerPrefs.GetString(LanguagePrefKey, DefaultLanguage);
        if (string.IsNullOrEmpty(_language)) _language = DefaultLanguage;

        LoadTable(DefaultLanguage, _fallback);
        if (_language == DefaultLanguage)
        {
            _entries.Clear();
            foreach (var pair in _fallback) _entries[pair.Key] = pair.Value;
        }
        else
        {
            LoadTable(_language, _entries);
        }
    }

    static bool LoadTable(string code, Dictionary<string, string> target)
    {
        target.Clear();
        var asset = Resources.Load<TextAsset>($"Localization/{code}");
        if (asset == null)
        {
            Debug.LogWarning($"[Localization] No se encontró Resources/Localization/{code}.json");
            return false;
        }

        var table = JsonUtility.FromJson<LocalizationTable>(asset.text);
        if (table?.entries == null)
        {
            Debug.LogWarning($"[Localization] La tabla {code} está vacía o es inválida.");
            return false;
        }

        foreach (var entry in table.entries)
        {
            if (string.IsNullOrEmpty(entry?.key)) continue;
            target[entry.key] = entry.value ?? "";
        }
        return true;
    }
}
