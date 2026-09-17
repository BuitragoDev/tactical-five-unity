# CUSTOM_SCROLLBAR — Barra de desplazamiento propia (UI Toolkit)

> **Propósito:** documento de referencia reutilizable. Explica **por qué** el `ScrollView` de
> Unity no es fiable en este proyecto y **cómo** implementar una barra de scroll propia
> (estilo ventana de Windows / Football Manager) en cualquier listado nuevo.
>
> Implementación de referencia: modal de traspasos de `Market`
> (`Market.uxml`, `Market.uss`, `MarketController.cs`).
> Estado: funciona con rueda de ratón, arrastre del thumb y clic en la pista.

---

## 1. El problema (leer antes de usar un `ScrollView`)

**[F] El tema del proyecto no incluye los estilos base de Unity para `ScrollView`.**
`TacticalFiveTheme.tss` solo importa:

```
@import url("../Styles/GlobalVariables.uss");
@import url("../Styles/Typography.uss");
@import url("../Styles/Utilities.uss");
```

Al no extender `UnityDefaultRuntimeTheme`, los elementos internos del `ScrollView`
(`.unity-scroll-view__content-viewport`, `.unity-scroll-view__content-container`,
`.unity-scroll-view__vertical-scroller`, `.unity-scroller__slider`,
`.unity-base-slider__dragger`…) **no reciben ningún estilo base**.

**Consecuencias observadas `[F]`:**

1. El viewport no tiene `overflow: hidden`, así que su altura acaba siendo igual a la del
   contenido → `scrollableHeight = 0`.
2. La barra interna aparece mal colocada (a la izquierda o abajo) y su thumb no se
   dimensiona ni se mueve de forma coherente.
3. Aunque se fuerce `verticalScrollerVisibility = AlwaysVisible`, el resultado sigue siendo
   poco fiable (el thumb no sigue a la rueda ni se puede arrastrar).

### Enfoques descartados (no repetir)

| Intento | Resultado |
|---|---|
| Estilar `.unity-scroll-view__vertical-scroller` / `.unity-base-slider__dragger` | El thumb no se sincroniza; el layout del scrollbar lo recoloca Unity |
| `verticalScrollerVisibility = AlwaysVisible` + forzar `display` del `Scroller`/`Slider` | La barra aparece pero no se mueve |
| `Scroller` externo + `ScrollView.scrollOffset` + `RegisterValueChangedCallback` | El `ScrollView` actualiza su scroller interno con `SetValueWithoutNotify`; no hay evento fiable y el rango (`highValue`) queda a 0 |
| `position: absolute` sobre el scroller interno | Rompe la geometría: el thumb se queda fijo arriba |
| Parchear los internos por USS (`.unity-scroll-view__content-viewport { overflow: hidden }`) | **Necesario pero no suficiente por sí solo**; sigue dependiendo del comportamiento interno |

> Los demás listados del juego (`Messages`, `Trajectory`, `Cartera`, `Stats`, `GLeague`,
> picks del Market) usan `ScrollView` de Unity con parches parciales de
> `.unity-scroll-view__*`. Funcionan porque son listas cortas o con paginación.
> **Para listas largas con barra arrastrable, usar el patrón de este documento.**

---

## 2. Arquitectura de la solución

Tres piezas, todas bajo nuestro control (sin `ScrollView`, sin `Scroller`, sin `Slider`):

```
market-trade-player-list-row        ← fila: [ viewport ][ track ]
├── market-trade-panel-body         ← VIEWPORT: overflow: hidden, flex-direction: column
│   └── market-trade-panel-content  ← CONTENT: filas; flex-shrink: 0; se desplaza con style.top = -scrollY
└── market-trade-scrollbar          ← TRACK: position: relative
    └── market-trade-scrollbar-thumb ← THUMB: position: absolute; azul; arrastrable
```

- **Viewport**: recorta (`overflow: hidden`).
- **Content**: conserva su altura natural (`flex-shrink: 0`) y se traslada con
  `style.top = -scrollY`. Su `layout.height` es la altura total del listado.
- **Track/Thumb**: barra propia. El thumb se dimensiona y posiciona desde la geometría real.

La **única fuente de verdad** es `scrollY` (float). Todo lo demás se deriva:

```
max       = contentHeight - viewportHeight
thumbH    = max <= 0 ? trackHeight : max(36, trackHeight * viewportHeight / contentHeight)
travel    = trackHeight - thumbH
thumb.top = travel * (scrollY / max)
content.top = -scrollY
```

---

## 3. UXML

Sustituir el `ScrollView` por el viewport y añadir la barra como hermano:

```xml
<ui:VisualElement class="market-trade-player-list-row">
    <ui:VisualElement name="MyPlayerViewport" class="market-trade-panel-body">
        <ui:VisualElement name="MyTeamBody" class="market-trade-panel-content" />
    </ui:VisualElement>
    <ui:VisualElement name="MyPlayerScrollTrack" class="market-trade-scrollbar">
        <ui:VisualElement name="MyPlayerScrollThumb" class="market-trade-scrollbar-thumb" />
    </ui:VisualElement>
</ui:VisualElement>
```

Referencias reales: `Market.uxml:133-141` (mi equipo) y `Market.uxml:196-204` (otro equipo).

**Reglas:**
- El viewport y el track deben ser **hermanos** dentro de una fila (`flex-direction: row`).
- Las filas se siguen añadiendo al elemento *content* (`MyTeamBody`), no al viewport.

---

## 4. USS

```css
/* Fila: viewport + barra */
.market-trade-player-list-row {
    flex-direction: row;
    flex-grow: 1;
    min-height: 0;
    align-items: stretch;
}

/* Viewport: recorta y apila en columna */
.market-trade-panel-body {
    flex-direction: column;
    flex-grow: 1;
    flex-shrink: 1;
    min-height: 0;
    overflow: hidden;
    position: relative;
}

/* Contenido: imprescindible flex-shrink 0, si no se comprime y max = 0 */
.market-trade-panel-content {
    flex-direction: column;
    flex-shrink: 0;
}

/* Pista */
.market-trade-scrollbar {
    width: 12px;
    min-width: 12px;
    flex-shrink: 0;
    align-self: stretch;
    margin-left: 4px;
    background-color: rgba(4, 12, 25, 0.92);
    border-left-width: 1px;
    border-left-color: rgba(65, 105, 165, 0.35);
    position: relative;
}

/* Thumb */
.market-trade-scrollbar-thumb {
    position: absolute;
    left: 2px;
    right: 2px;
    top: 0;
    height: 40px;
    background-color: rgb(42, 95, 201);
    border-radius: 3px;
}
.market-trade-scrollbar:hover .market-trade-scrollbar-thumb {
    background-color: rgb(75, 135, 235);
}
```

Referencias reales: `Market.uss:1191-1234`.

> **Trampa `[F]`:** si el *content* no tiene `flex-shrink: 0`, el flexbox lo comprime a la
> altura del viewport, `contentHeight == viewportHeight` y `max = 0`: el thumb ocupa todo el
> alto y no se puede mover.

---

## 5. C# (implementación reutilizable)

Referencia completa: `MarketController.cs:278-422`. Resumen de piezas:

### 5.1 Estado

```csharp
private TradeScrollbar _myBar;

class TradeScrollbar
{
    public VisualElement Viewport;
    public VisualElement Content;
    public VisualElement Track;
    public VisualElement Thumb;
    public float ScrollY;        // única fuente de verdad
    public bool Dragging;
    public float StartPointerY;  // para el arrastre
    public float StartScrollY;
}
```

### 5.2 Creación y eventos

```csharp
TradeScrollbar BindTradeScrollbar(VisualElement viewport, VisualElement content,
                                  VisualElement track, VisualElement thumb)
{
    if (viewport == null || content == null || track == null || thumb == null) return null;

    var bar = new TradeScrollbar
    {
        Viewport = viewport, Content = content, Track = track, Thumb = thumb
    };

    // Rueda del ratón (burbujea desde las filas hasta el viewport)
    viewport.RegisterCallback<WheelEvent>(evt =>
    {
        float max = GetScrollMax(bar);
        if (max <= 0f) return;
        bar.ScrollY = Mathf.Clamp(bar.ScrollY + evt.delta.y * 30f, 0f, max);
        ApplyScroll(bar);
        evt.StopPropagation();
    });

    // Arrastre del thumb (pointer capture para no perderlo)
    thumb.RegisterCallback<PointerDownEvent>(evt =>
    {
        bar.Dragging = true;
        bar.StartPointerY = evt.position.y;   // posición de PANEL, no local
        bar.StartScrollY = bar.ScrollY;
        thumb.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    });
    thumb.RegisterCallback<PointerMoveEvent>(evt =>
    {
        if (!bar.Dragging) return;
        float max = GetScrollMax(bar);
        float travel = track.layout.height - thumb.layout.height;
        if (max <= 0f || travel <= 0f) return;
        float delta = evt.position.y - bar.StartPointerY;
        bar.ScrollY = Mathf.Clamp(bar.StartScrollY + delta / travel * max, 0f, max);
        ApplyScroll(bar);
        evt.StopPropagation();
    });
    thumb.RegisterCallback<PointerUpEvent>(evt =>
    {
        if (bar.Dragging) { bar.Dragging = false; thumb.ReleasePointer(evt.pointerId); }
        evt.StopPropagation();
    });
    thumb.RegisterCallback<PointerCaptureOutEvent>(_ => bar.Dragging = false);

    // Clic en la pista: salto de página
    track.RegisterCallback<PointerDownEvent>(evt =>
    {
        if (bar.Dragging) return;
        float max = GetScrollMax(bar);
        if (max <= 0f) return;
        float page = viewport.layout.height;
        float y = evt.localPosition.y;
        float thumbTop = thumb.layout.y;
        float thumbBottom = thumbTop + thumb.layout.height;
        float target = y < thumbTop ? bar.ScrollY - page
                     : (y > thumbBottom ? bar.ScrollY + page : bar.ScrollY);
        bar.ScrollY = Mathf.Clamp(target, 0f, max);
        ApplyScroll(bar);
        evt.StopPropagation();
    });

    CursorManager.Instance?.RegisterHandCursor(thumb);
    CursorManager.Instance?.RegisterHandCursor(track);

    // Recalcular cuando cambie la geometría (lista reconstruida o panel redimensionado)
    content.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));
    viewport.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));

    ApplyScroll(bar);
    return bar;
}
```

### 5.3 Cálculo y pintado

```csharp
static float GetScrollMax(TradeScrollbar bar)
{
    if (bar == null || bar.Content == null || bar.Viewport == null) return 0f;
    float contentHeight = bar.Content.layout.height;
    float viewportHeight = bar.Viewport.layout.height;
    return Mathf.Max(0f, contentHeight - viewportHeight);
}

void ClampAndApply(TradeScrollbar bar)
{
    if (bar == null) return;
    bar.ScrollY = Mathf.Clamp(bar.ScrollY, 0f, GetScrollMax(bar));
    ApplyScroll(bar);
}

void ApplyScroll(TradeScrollbar bar)
{
    if (bar == null || bar.Content == null) return;

    // Mover el contenido (position: relative por defecto en UI Toolkit → top funciona)
    bar.Content.style.top = -bar.ScrollY;

    float trackHeight   = bar.Track.layout.height;
    float contentHeight = bar.Content.layout.height;
    float viewportHeight = bar.Viewport.layout.height;
    if (trackHeight <= 0f || contentHeight <= 0f) return;

    float max = Mathf.Max(0f, contentHeight - viewportHeight);
    float thumbHeight = max <= 0f
        ? trackHeight
        : Mathf.Max(36f, trackHeight * viewportHeight / contentHeight);
    thumbHeight = Mathf.Min(thumbHeight, trackHeight);

    float travel = trackHeight - thumbHeight;
    float t = max <= 0f ? 0f : Mathf.Clamp01(bar.ScrollY / max);

    bar.Thumb.style.height = thumbHeight;
    bar.Thumb.style.top = travel * t;
}

void ResetTradeScrollbars()
{
    if (_myBar != null)    { _myBar.ScrollY = 0f;    ApplyScroll(_myBar); }
    // ... resto de barras
}

void SyncAllTradeScrollbars()   // red de seguridad
{
    if (_tradePanels == null || _tradePanels.resolvedStyle.display == DisplayStyle.None) return;
    ClampAndApply(_myBar);
    // ... resto de barras
}

void Update() => SyncAllTradeScrollbars();
```

---

## 6. Cómo integrarlo en un listado nuevo (checklist)

1. **UXML:** sustituir el `ScrollView` por `viewport` (con `overflow: hidden`) + `track` +
   `thumb`, dentro de una fila. Mantener el mismo `name` del elemento que recibe las filas
   (el *content*).
2. **USS:** copiar las 4 reglas de §4. **No olvidar `flex-shrink: 0` en el content.**
3. **Controller:**
   - Cachear `viewport`, `content`, `track`, `thumb` en `CacheReferences()`.
   - `BindTradeScrollbar(...)` **una sola vez** (o guardar contra doble registro, porque
     las pantallas se re-inicializan en `OnEnable`).
   - Llamar a `ApplyScroll(bar)` / `Reset...Scrollbars()` tras reconstruir o filtrar la
     lista.
   - No hace falta `Update()`, pero se deja como red de seguridad.
4. **Probar:** rueda sobre la lista, arrastre del thumb, clic en la pista y redimensionado
   de la ventana.

---

## 7. Reglas y trampas `[F]`

1. **No usar `ScrollView` de Unity para listas largas** con el tema actual del proyecto.
2. **`flex-shrink: 0` en el content** es obligatorio (si no, `max = 0`).
3. **`evt.position`** (coordenadas de panel) para el delta del arrastre; **no**
   `evt.localPosition` en el thumb, porque el thumb se mueve bajo el puntero.
4. **`CapturePointer` + `PointerCaptureOutEvent`**: imprescindibles para que el arrastre no
   se pierda al salir del thumb.
5. **La rueda va en el viewport**, no en las filas (las filas no reciben `WheelEvent`).
6. **Resetear `ScrollY`** al abrir el modal y al re-filtrar; si no, el contenido queda
   desplazado con el thumb al tope.
7. **Un solo registro de callbacks**: `OnEnable` se ejecuta cada vez que se muestra la
   pantalla (las pantallas se ocultan, no se destruyen).
8. **Thumb mínimo de 36px** para que siga siendo agarrable con listas muy largas.
9. Si en el futuro se añade el tema base de Unity (`UnityDefaultRuntimeTheme`) como parent
   del `.tss`, revisar esta solución: los motivos de §1 podrían desaparecer.

---

## 8. Referencias en el código

| Archivo | Qué contiene |
|---|---|
| `Assets/_TacticalFive/UI/Screens/Market/Market.uxml` | viewport + track + thumb (`:133-141`, `:196-204`) |
| `Assets/_TacticalFive/UI/Screens/Market/Market.uss` | estilos viewport/content/track/thumb (`:1191-1234`) |
| `Assets/_TacticalFive/Scripts/UI/MarketController.cs` | `ConfigurePlayerScroll` (`:278`), `BindTradeScrollbar` (`:285`), `GetScrollMax` (`:369`), `ClampAndApply` (`:377`), `ApplyScroll` (`:384`), `ResetTradeScrollbars` (`:406`), `SyncAllTradeScrollbars` (`:412`), `Update` (`:419`) |
| `Assets/_TacticalFive/Scripts/Core/UIScreenController.cs` | ciclo de vida de pantalla (`OnEnable` → `CacheReferences` → `LoadData` → `RegisterCallbacks` → `Refresh`) |

## 9. Pendiente / mejoras futuras

- Extraer la lógica a un control reutilizable (p. ej. `ScrollbarBinder` o una clase
  `CustomVerticalScroll`) en `Scripts/UI/` para no duplicarla en cada pantalla.
- Evaluar migrar otros listados largos (`Cartera`, `Messages`, `Trajectory`) a este patrón.
- Los picks del Market usan también este patrón manual: viewport + content + track/thumb
  (`Market.uxml` y `MarketController.cs`). Así no se salen del panel cuando hay más picks
  de los que caben en la zona fija de `ACTIVOS ADICIONALES`.
