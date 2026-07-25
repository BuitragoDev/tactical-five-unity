using System.IO;
using UnityEngine;

public static class PlayerPhotoHelper
{
    private static string BaseDir => Path.Combine(Application.persistentDataPath, "PlayerPhotos");

    private static string SlotDir(int slot)
    {
        return Path.Combine(BaseDir, slot.ToString());
    }

    public static Texture2D Load(int playerId, string photoField)
    {
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
            byte[] bytes = File.ReadAllBytes(slotPath);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
                return tex;
        }

        // 4. Check persistent data root (retrocompatibilidad partidas antiguas)
        string rootPath = Path.Combine(BaseDir, $"{playerId}.png");
        if (File.Exists(rootPath))
        {
            byte[] bytes = File.ReadAllBytes(rootPath);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
                return tex;
        }

        // 5. Fallback
        return Resources.Load<Texture2D>("PlayerPhotos/default");
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
}
