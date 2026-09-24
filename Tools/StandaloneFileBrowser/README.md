# Parche nativo — diálogo del SO delante del juego (Linux)

`UnityStandaloneFileBrowser` crea el diálogo GTK con **padre `NULL`**
(`gtk_file_chooser_dialog_new(title, NULL, ...)`). En GNOME, si el juego está en
**fullscreen**, el gestor de ventanas deja el diálogo detrás y muestra la
notificación *"…está preparado"*; hay que pulsarla/Alt+Tab.

Este parche (en `library.c`) asocia el diálogo a la **ventana X11 activa** (la de
Unity) antes de mostrarlo:

- Obtiene el XID de la ventana top-level activa (`_NET_ACTIVE_WINDOW`, con
  fallback a `XGetInputFocus` subiendo hasta el top-level).
- `XSetTransientForHint(...)` para fijarla como padre transitorio.
- `gtk_window_set_keep_above(TRUE)` + `gtk_window_present(...)`.

Resultado: el diálogo aparece **delante** del juego, sin notificación y sin
necesidad de Alt+Tab.

## Requisitos (Ubuntu/Debian)

```bash
sudo apt update
sudo apt install build-essential cmake pkg-config libgtk-3-dev libx11-dev
```

## Compilar

```bash
chmod +x Tools/StandaloneFileBrowser/build.sh
Tools/StandaloneFileBrowser/build.sh
```

El script compila y sobreescribe
`Assets/StandaloneFileBrowser/Plugins/Linux/x86_64/libStandaloneFileBrowser.so`
(se conserva su `.meta`, así que Unity usa la misma GUID).

Después, en Unity: deja que reimporte el plugin y **recompila la build**.

## Notas

- Solo afecta a **Linux**. Las rutas de Windows (plugin propio) y macOS
  (`.bundle`) no se tocan.
- Si compilas para otras arquitecturas, copia el `.so` resultante a la carpeta
  correspondiente dentro de `Assets/StandaloneFileBrowser/Plugins/Linux/`.
- Para volver al `.so` original, reimporta el plugin desde el repo.
