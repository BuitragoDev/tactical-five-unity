using UnityEngine;

/// <summary>
/// Escalado de texturas por interpolación bilineal (para generar las copias de
/// tamaño de los logos subidos por el usuario). Sin dependencias de UI.
/// </summary>
public static class TextureScaler
{
    public static Texture2D Resize(Texture2D source, int newWidth, int newHeight)
    {
        if (source == null || newWidth <= 0 || newHeight <= 0) return source;

        var result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        var pixels = new Color[newWidth * newHeight];

        float xRatio = newWidth > 1 ? (float)(source.width - 1) / (newWidth - 1) : 0f;
        float yRatio = newHeight > 1 ? (float)(source.height - 1) / (newHeight - 1) : 0f;

        for (int y = 0; y < newHeight; y++)
        {
            float sy = y * yRatio;
            int y0 = Mathf.FloorToInt(sy);
            int y1 = Mathf.Min(y0 + 1, source.height - 1);
            float fy = sy - y0;

            for (int x = 0; x < newWidth; x++)
            {
                float sx = x * xRatio;
                int x0 = Mathf.FloorToInt(sx);
                int x1 = Mathf.Min(x0 + 1, source.width - 1);
                float fx = sx - x0;

                Color top = Color.Lerp(source.GetPixel(x0, y0), source.GetPixel(x1, y0), fx);
                Color bottom = Color.Lerp(source.GetPixel(x0, y1), source.GetPixel(x1, y1), fx);
                pixels[y * newWidth + x] = Color.Lerp(top, bottom, fy);
            }
        }

        result.SetPixels(pixels);
        result.Apply();
        return result;
    }
}
