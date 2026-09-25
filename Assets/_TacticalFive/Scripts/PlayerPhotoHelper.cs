using System.IO;
using UnityEngine;

public static class PlayerPhotoHelper
{
    private static string BaseDir => Path.Combine(Application.persistentDataPath, "PlayerPhotos");

    private static string SlotDir(int slot)
    {
        return Path.Combine(BaseDir, slot.ToString());
    }

    // Foto personalizada del ámbito activo (template o save_{n}).
    static string CustomPhotoPath(int playerId) =>
        Path.Combine(CustomScopes.ActiveRoot, "PlayerPhotos", $"{playerId}.png");

    public static Texture2D Load(int playerId, string photoField)
    {
        // 0. Foto personalizada del ámbito activo (tiene prioridad sobre Resources).
        if (File.Exists(CustomPhotoPath(playerId)))
        {
            var custom = LoadTexture(CustomPhotoPath(playerId));
            if (custom != null) return custom;
        }

        // 1. Check Resources/PlayerPhotos/{id} (fotos manuales del seed)
        Texture2D res = Resources.Load<Texture2D>($"PlayerPhotos/{playerId}");
        if (res != null) return res;

        // 2. Check Resources/PlayerPhotos/{photoField}
        if (!string.IsNullOrEmpty(photoField))
        {
            res = Resources.Load<Texture2D>($"PlayerPhotos/{photoField}");
            if (res != null) return res;
        }

        // 3. Check persistent data path for this slot (rookies de esta partida)
        int slot = DatabaseManager.Instance?.ActiveSaveSlot ?? 0;
        string slotPath = Path.Combine(SlotDir(slot), $"{playerId}.png");
        if (File.Exists(slotPath))
        {
            var tex = LoadTexture(slotPath);
            if (tex != null) return tex;
        }

        // 4. Check persistent data root (retrocompatibilidad partidas antiguas)
        string rootPath = Path.Combine(BaseDir, $"{playerId}.png");
        if (File.Exists(rootPath))
        {
            var tex = LoadTexture(rootPath);
            if (tex != null) return tex;
        }

        // 5. Fallback
        return Resources.Load<Texture2D>("PlayerPhotos/default");
    }

    static Texture2D LoadTexture(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes)) return tex;
            Object.Destroy(tex);
            return null;
        }
        catch { return null; }
    }

    public static void CreateRookiePhoto(int playerId)
    {
        int idx = Random.Range(1, 101);
        Texture2D src = Resources.Load<Texture2D>($"PlayerPhotos/Default/default{idx}");
        if (src == null) return;

        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        byte[] bytes = copy.EncodeToPNG();
        int slot = DatabaseManager.Instance?.ActiveSaveSlot ?? 0;
        string slotDir = SlotDir(slot);
        Directory.CreateDirectory(slotDir);
        File.WriteAllBytes(Path.Combine(slotDir, $"{playerId}.png"), bytes);
        Object.DestroyImmediate(copy);
    }

    // ── FOTO CUSTOM (Editor) ──────────────────────────────────────────
    public const int PhotoWidth = 256;
    public const int PhotoHeight = 256;

    public static bool ValidatePhotoFile(string path, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            error = "No se encontró el archivo.";
            return false;
        }
        if (!path.ToLowerInvariant().EndsWith(".png") || !HasPngSignature(path))
        {
            error = "La imagen del jugador debe estar en formato PNG.";
            return false;
        }
        var tex = LoadTexture(path);
        if (tex == null)
        {
            error = "No se pudo leer la imagen.";
            return false;
        }
        int w = tex.width, h = tex.height;
        Object.Destroy(tex);
        if (w != PhotoWidth || h != PhotoHeight)
        {
            error = $"La imagen del jugador debe medir {PhotoWidth}×{PhotoHeight} px (actual: {w}×{h}).";
            return false;
        }
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

    /// <summary>Guarda la foto custom del jugador en el ámbito activo (template o
    /// save_{n}). Tiene prioridad sobre Resources al cargar.</summary>
    public static bool SetPhoto(int playerId, string sourcePath, out string error)
    {
        if (!ValidatePhotoFile(sourcePath, out error)) return false;
        try
        {
            string dir = Path.Combine(CustomScopes.ActiveRoot, "PlayerPhotos");
            Directory.CreateDirectory(dir);
            File.Copy(sourcePath, Path.Combine(dir, $"{playerId}.png"), true);
            return true;
        }
        catch (System.Exception ex)
        {
            error = "No se pudo guardar la imagen: " + ex.Message;
            return false;
        }
    }
}
