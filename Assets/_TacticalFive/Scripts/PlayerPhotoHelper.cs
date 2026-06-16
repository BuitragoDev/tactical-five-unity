using System.IO;
using UnityEngine;

public static class PlayerPhotoHelper
{
    private static string _photoDir;
    private static string PhotoDir
    {
        get
        {
            if (_photoDir == null)
                _photoDir = Path.Combine(Application.persistentDataPath, "PlayerPhotos");
            return _photoDir;
        }
    }

    public static Texture2D Load(int playerId, string photoField)
    {
        // 1. Check persistent data path for {id}.png (runtime-generated)
        string path = Path.Combine(PhotoDir, $"{playerId}.png");
        if (File.Exists(path))
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
                return tex;
        }

        // 2. Check Resources/PlayerPhotos/{id}
        Texture2D res = Resources.Load<Texture2D>($"PlayerPhotos/{playerId}");
        if (res != null) return res;

        // 3. Check Resources/PlayerPhotos/{photoField}
        if (!string.IsNullOrEmpty(photoField))
        {
            res = Resources.Load<Texture2D>($"PlayerPhotos/{photoField}");
            if (res != null) return res;
        }

        // 4. Fallback
        return Resources.Load<Texture2D>("PlayerPhotos/default");
    }

    public static void CreateRookiePhoto(int playerId)
    {
        int idx = Random.Range(1, 11);
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
        Directory.CreateDirectory(PhotoDir);
        File.WriteAllBytes(Path.Combine(PhotoDir, $"{playerId}.png"), bytes);
        Object.DestroyImmediate(copy);
    }
}
