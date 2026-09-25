using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Contenido personalizado (imágenes) separado por ámbito: <c>template</c> (lo que
/// edita el Editor) o <c>save_{n}</c> (una partida). Cada partida nueva hace un
/// snapshot del template al empezar, de forma que conserva sus propias imágenes
/// aunque después se cambie el template.
///
/// Estructura en disco: <c>persistentDataPath/TacticalFive/Custom/{scope}/...</c>
/// (TeamImages guarda <c>Logos/</c> y <c>Arenas/</c>; PlayerPhotoHelper guarda
/// <c>PlayerPhotos/</c>).
/// </summary>
public static class CustomScopes
{
    public const string Template = "template";

    static string _active = Template;
    static bool _legacyMigrated;

    /// <summary>Ámbito activo ("template" o "save_{n}").</summary>
    public static string Active => _active;

    public static string ScopeRoot(string scope) =>
        Path.Combine(Application.persistentDataPath, "TacticalFive", "Custom",
            string.IsNullOrEmpty(scope) ? Template : scope);

    public static string ActiveRoot => ScopeRoot(_active);

    /// <summary>Fija el ámbito activo. Devuelve true si cambió (o si se ejecutó la
    /// migración de la carpeta global antigua), para que quien tenga cachés las
    /// invalide.</summary>
    public static bool SetScope(string scope)
    {
        if (string.IsNullOrEmpty(scope)) scope = Template;
        bool migrated = MigrateLegacyGlobal();
        if (_active == scope && !migrated) return false;
        _active = scope;
        return true;
    }

    public static bool HasCustom(string scope) => Directory.Exists(ScopeRoot(scope));

    /// <summary>Copia las imágenes custom de un ámbito a otro (p. ej. template →
    /// save_{n}). Siempre crea la carpeta destino, para dejar constancia de que el
    /// snapshot ya se hizo aunque no haya imágenes personalizadas.</summary>
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
        catch (Exception ex)
        {
            Debug.LogError($"[CustomScopes] Error copiando imágenes custom: {ex.Message}");
        }
    }

    public static void DeleteScope(string scope)
    {
        try
        {
            string dir = ScopeRoot(scope);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CustomScopes] Error borrando imágenes custom ({scope}): {ex.Message}");
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

            string templateDir = Path.Combine(root, Template);
            Directory.CreateDirectory(templateDir);
            if (Directory.Exists(legacyLogos) && !Directory.Exists(Path.Combine(templateDir, "Logos")))
                Directory.Move(legacyLogos, Path.Combine(templateDir, "Logos"));
            if (Directory.Exists(legacyArenas) && !Directory.Exists(Path.Combine(templateDir, "Arenas")))
                Directory.Move(legacyArenas, Path.Combine(templateDir, "Arenas"));
            Debug.Log("[CustomScopes] Migradas imágenes custom globales a 'template'.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CustomScopes] Error migrando imágenes custom: {ex.Message}");
            return false;
        }
    }
}
