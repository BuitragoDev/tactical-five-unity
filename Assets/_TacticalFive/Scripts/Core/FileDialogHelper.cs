using UnityEngine;

/// <summary>
/// Abre el diálogo nativo del sistema operativo para elegir una imagen PNG,
/// usando el plugin UnityStandaloneFileBrowser (SFB). Funciona en el Editor y
/// en las builds de Windows/macOS/Linux. Si el plugin no está disponible en la
/// plataforma, TryOpenImage devuelve false para que el llamador use un fallback.
/// </summary>
public static class FileDialogHelper
{
    /// <summary>
    /// Muestra el diálogo nativo. Devuelve true si el diálogo se abrió (el
    /// resultado, si el usuario elige algo, llega por <paramref name="onSelected"/>).
    /// Devuelve false si la plataforma no soporta el diálogo nativo (para hacer fallback).
    /// </summary>
    public static bool TryOpenImage(string title, System.Action<string> onSelected)
    {
        try
        {
            var filters = new[] { new SFB.ExtensionFilter("Imágenes PNG", "png") };

#if UNITY_STANDALONE_OSX
            // En macOS las llamadas síncronas lanzan excepción tras cambios de foco;
            // se usa la variante asíncrona.
            SFB.StandaloneFileBrowser.OpenFilePanelAsync(title, "", filters, false, paths =>
            {
                if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
                    onSelected?.Invoke(paths[0]);
            });
#else
            var paths = SFB.StandaloneFileBrowser.OpenFilePanel(title, "", filters, false);
            if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
                onSelected?.Invoke(paths[0]);
#endif
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[FileDialog] Diálogo nativo no disponible: {ex.Message}");
            return false;
        }
    }
}
