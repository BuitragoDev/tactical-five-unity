using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Servicio central de imágenes de equipos (logos y pabellones) con sistema de
/// "overlay": las imágenes originales viven empaquetadas en Resources (solo
/// lectura) y las personalizadas se guardan en una carpeta escribible del disco
/// (persistentDataPath) que tiene prioridad. Así la edición funciona también en
/// una build y RESETEAR BASE DE DATOS solo tiene que borrar la carpeta custom
/// para volver a las originales (que nunca se modifican).
/// </summary>
public static class TeamImages
{
    // Tamaños de logo soportados (200 es el original en la raíz de Teams/Logos).
    public static readonly int[] LogoSizes = { 200, 120, 100, 80, 64, 32 };
    public const int LogoSourceSize = 200;
    public const int ArenaWidth = 1380;
    public const int ArenaHeight = 752;

    // Ámbito de las imágenes personalizadas: "template" (Editor) o "save_{n}".
    static string _scope = "template";
    static bool _legacyMigrated;

    public static string CustomRoot => ScopeRoot(_scope);
    public static string CustomLogoDir(int size) => Path.Combine(CustomRoot, "Logos", SizeFolder(size));
    public static string CustomArenaDir => Path.Combine(CustomRoot, "Arenas");

    static string ScopeRoot(string scope) =>
        Path.Combine(Application.persistentDataPath, "TacticalFive", "Custom", scope);

    /// <summary>Fija el ámbito de las imágenes personalizadas (template o save_{n}).</summary>
    public static void SetScope(string scope)
    {
        if (string.IsNullOrEmpty(scope)) scope = "template";
        bool migrated = MigrateLegacyGlobal();
        if (_scope == scope && !migrated) return;
        _scope = scope;
        Invalidate();
    }

    public static bool HasCustom(string scope) => Directory.Exists(ScopeRoot(scope));

    /// <summary>Copia las imágenes custom de un ámbito a otro (p. ej. template → save_{n}).
    /// Siempre crea la carpeta destino, para dejar constancia de que el snapshot ya
    /// se hizo aunque no haya imágenes personalizadas.</summary>
    public static void CopyCustom(string fromScope, string toScope)
    {
        try
        {
            string dst = ScopeRoot(toScope);
            Directory.CreateDirectory(dst);
            string src = ScopeRoot(fromScope);
            if (!Directory.Exists(src)) return;
            CopyDir(src, dst);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TeamImages] Error copiando imágenes custom: {ex.Message}");
        }
    }

    public static void DeleteScope(string scope)
    {
        try
        {
            string dir = ScopeRoot(scope);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TeamImages] Error borrando imágenes custom ({scope}): {ex.Message}");
        }
    }

    static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDir(dir, Path.Combine(dst, Path.GetFileName(dir)));
    }

    // Migración: la versión anterior guardaba las imágenes en Custom/{Logos,Arenas}
    // (global, compartido por todas las partidas). Se mueven una vez a Custom/template/.
    static bool MigrateLegacyGlobal()
    {
        if (_legacyMigrated) return false;
        _legacyMigrated = true;
        try
        {
            string root = Path.Combine(Application.persistentDataPath, "TacticalFive", "Custom");
            string legacyLogos = Path.Combine(root, "Logos");
            string legacyArenas = Path.Combine(root, "Arenas");
            if (!Directory.Exists(legacyLogos) && !Directory.Exists(legacyArenas)) return false;

            string templateDir = Path.Combine(root, "template");
            Directory.CreateDirectory(templateDir);
            if (Directory.Exists(legacyLogos) && !Directory.Exists(Path.Combine(templateDir, "Logos")))
                Directory.Move(legacyLogos, Path.Combine(templateDir, "Logos"));
            if (Directory.Exists(legacyArenas) && !Directory.Exists(Path.Combine(templateDir, "Arenas")))
                Directory.Move(legacyArenas, Path.Combine(templateDir, "Arenas"));
            Debug.Log("[TeamImages] Migradas imágenes custom globales a 'template'.");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TeamImages] Error migrando imágenes custom: {ex.Message}");
            return false;
        }
    }

    static string SizeFolder(int size) => size >= LogoSourceSize ? "" : $"{size}x{size}";

    static int ParseSize(string sizeFolder)
    {
        if (string.IsNullOrEmpty(sizeFolder)) return LogoSourceSize;
        int x = sizeFolder.IndexOf('x');
        if (x > 0 && int.TryParse(sizeFolder.Substring(0, x), out int s)) return s;
        return LogoSourceSize;
    }

    static string ArenaFileName(string logo) => logo == "sixers" ? "76ers" : logo;

    // ── Cache (clave = ámbito + tamaño/nombre) ────────────────────────
    static readonly Dictionary<string, Sprite[]> _logoArrays = new();
    static readonly Dictionary<string, Sprite> _logoSprites = new();
    static readonly Dictionary<string, Sprite[]> _arenaArrays = new();
    static readonly Dictionary<string, Sprite> _arenaSprites = new();
    static readonly Dictionary<string, Sprite> _previewCache = new();

    /// <summary>Sprite de previsualización desde un fichero (sin guardarlo). Para
    /// el borrador del Editor antes de pulsar GUARDAR.</summary>
    public static Sprite PreviewSprite(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        if (_previewCache.TryGetValue(path, out var cached)) return cached;
        var sp = LoadSpriteFromFile(path);
        if (sp != null) _previewCache[path] = sp;
        return sp;
    }

    public static void Invalidate()
    {
        _logoArrays.Clear();
        _logoSprites.Clear();
        _arenaArrays.Clear();
        _arenaSprites.Clear();
    }

    // ── LOGOS ─────────────────────────────────────────────────────────
    public static Sprite[] Logos(int size) => LogosFor(_scope, size);
    public static Sprite Logo(int size, string logo) => LogoFor(_scope, size, logo);
    public static Sprite Logo(string sizeFolder, string logo) => LogoFor(_scope, ParseSize(sizeFolder), logo);

    /// <summary>Logos de un ámbito concreto (para la pantalla de Cargar Partida,
    /// que muestra varias partidas a la vez).</summary>
    public static Sprite[] LogosFor(string scope, int size)
    {
        string key = scope + "|" + size;
        if (_logoArrays.TryGetValue(key, out var cached)) return cached;

        var dict = new Dictionary<string, Sprite>();
        foreach (var s in Resources.LoadAll<Sprite>($"Teams/Logos/{SizeFolder(size)}"))
            dict[s.name] = s;

        string dir = Path.Combine(ScopeRoot(scope), "Logos", SizeFolder(size));
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir, "*.png"))
            {
                var sp = LoadSpriteFromFile(f);
                if (sp != null) dict[Path.GetFileNameWithoutExtension(f)] = sp;
            }
        }

        var arr = new Sprite[dict.Count];
        dict.Values.CopyTo(arr, 0);
        _logoArrays[key] = arr;
        return arr;
    }

    public static Sprite LogoFor(string scope, int size, string logo)
    {
        if (string.IsNullOrEmpty(logo)) return null;
        string key = scope + "|" + size + ":" + logo;
        if (_logoSprites.TryGetValue(key, out var cached)) return cached;

        string customFile = Path.Combine(ScopeRoot(scope), "Logos", SizeFolder(size), logo + ".png");
        Sprite sp = File.Exists(customFile)
            ? LoadSpriteFromFile(customFile)
            : Resources.Load<Sprite>($"Teams/Logos/{SizeFolder(size)}/{logo}");

        if (sp != null) _logoSprites[key] = sp;
        return sp;
    }

    // ── ARENAS ────────────────────────────────────────────────────────
    public static Sprite[] Arenas() => ArenasFor(_scope);
    public static Sprite Arena(string logo) => ArenaFor(_scope, logo);

    public static Sprite[] ArenasFor(string scope)
    {
        if (_arenaArrays.TryGetValue(scope, out var cached)) return cached;

        var dict = new Dictionary<string, Sprite>();
        foreach (var s in Resources.LoadAll<Sprite>("Arenas/"))
            dict[s.name] = s;

        string dir = Path.Combine(ScopeRoot(scope), "Arenas");
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir, "*.png"))
            {
                var sp = LoadSpriteFromFile(f);
                if (sp != null) dict[Path.GetFileNameWithoutExtension(f)] = sp;
            }
        }

        var arr = new Sprite[dict.Count];
        dict.Values.CopyTo(arr, 0);
        _arenaArrays[scope] = arr;
        return arr;
    }

    public static Sprite ArenaFor(string scope, string logo)
    {
        string file = string.IsNullOrEmpty(logo) ? "default" : ArenaFileName(logo);
        string key = scope + "|" + file;
        if (_arenaSprites.TryGetValue(key, out var cached)) return cached;

        string customFile = Path.Combine(ScopeRoot(scope), "Arenas", file + ".png");
        Sprite sp = File.Exists(customFile)
            ? LoadSpriteFromFile(customFile)
            : Resources.Load<Sprite>($"Arenas/{file}");

        if (sp == null && file != "default")
            sp = Resources.Load<Sprite>("Arenas/default");

        if (sp != null) _arenaSprites[key] = sp;
        return sp;
    }

    // ── VALIDACIÓN ────────────────────────────────────────────────────
    public static bool ValidateLogoFile(string path, out string error)
        => ValidatePng(path, LogoSourceSize, LogoSourceSize, "El logo", out error);

    public static bool ValidateArenaFile(string path, out string error)
        => ValidatePng(path, ArenaWidth, ArenaHeight, "La imagen del pabellón", out error);

    static bool ValidatePng(string path, int w, int h, string label, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            error = "No se encontró el archivo.";
            return false;
        }
        if (!path.ToLowerInvariant().EndsWith(".png") || !HasPngSignature(path))
        {
            error = $"{label} debe estar en formato PNG.";
            return false;
        }
        var tex = LoadTexture(path);
        if (tex == null)
        {
            error = "No se pudo leer la imagen.";
            return false;
        }
        if (tex.width != w || tex.height != h)
        {
            error = $"{label} debe medir {w}×{h} px (actual: {tex.width}×{tex.height}).";
            Object.Destroy(tex);
            return false;
        }
        Object.Destroy(tex);
        return true;
    }

    static bool HasPngSignature(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var sig = new byte[8];
            if (fs.Read(sig, 0, 8) < 8) return false;
            return sig[0] == 0x89 && sig[1] == 0x50 && sig[2] == 0x4E && sig[3] == 0x47
                && sig[4] == 0x0D && sig[5] == 0x0A && sig[6] == 0x1A && sig[7] == 0x0A;
        }
        catch { return false; }
    }

    static Texture2D LoadTexture(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) { Object.Destroy(tex); return null; }
            return tex;
        }
        catch { return null; }
    }

    // ── GUARDAR ───────────────────────────────────────────────────────
    /// <summary>Guarda un logo custom (200×200) y genera las 5 copias de tamaño.</summary>
    public static bool SetLogo(string logo, string sourcePath, out string error)
    {
        if (!ValidateLogoFile(sourcePath, out error)) return false;

        var src = LoadTexture(sourcePath);
        if (src == null) { error = "No se pudo leer la imagen."; return false; }

        foreach (var size in LogoSizes)
        {
            var dir = CustomLogoDir(size);
            Directory.CreateDirectory(dir);
            Texture2D outTex = size == LogoSourceSize ? src : TextureScaler.Resize(src, size, size);
            File.WriteAllBytes(Path.Combine(dir, logo + ".png"), outTex.EncodeToPNG());
            if (outTex != src) Object.Destroy(outTex);
        }
        Object.Destroy(src);
        Invalidate();
        return true;
    }

    /// <summary>Guarda la imagen custom del pabellón (1380×752).</summary>
    public static bool SetArena(string logo, string sourcePath, out string error)
    {
        if (!ValidateArenaFile(sourcePath, out error)) return false;

        var src = LoadTexture(sourcePath);
        if (src == null) { error = "No se pudo leer la imagen."; return false; }

        Directory.CreateDirectory(CustomArenaDir);
        File.WriteAllBytes(Path.Combine(CustomArenaDir, ArenaFileName(logo) + ".png"), src.EncodeToPNG());
        Object.Destroy(src);
        Invalidate();
        return true;
    }

    /// <summary>Elimina todas las imágenes custom (reset). Las originales quedan intactas.</summary>
    public static void ClearCustom()
    {
        try
        {
            if (Directory.Exists(CustomRoot)) Directory.Delete(CustomRoot, true);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TeamImages] Error borrando imágenes custom: {ex.Message}");
        }
        Invalidate();
    }

    static Sprite LoadSpriteFromFile(string path)
    {
        var tex = LoadTexture(path);
        if (tex == null) return null;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        // El nombre es clave: las pantallas re-indexan los arrays de Logos()/Arenas()
        // por s.name, así que el sprite custom debe llevar el nombre del fichero.
        sp.name = Path.GetFileNameWithoutExtension(path);
        return sp;
    }
}
