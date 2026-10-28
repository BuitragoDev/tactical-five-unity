# UI_TOOLKIT — Tactical Five Interface

> Complete analysis of the UI Toolkit stack. **[F]** fact, **[D]** deduction, **[H]** hypothesis. This is a dated architecture snapshot; live branch/HEAD and screen count: `python3 Tools/project_status.py` (`Docs/PROJECT_STATUS.md`).

## 1. Stack summary

- **Engine:** Unity 6 (6000.3.15f1) UI Toolkit **runtime** (no IMGUI, no uGUI game objects; `com.unity.ugui` present only as package dependency).
- **One panel settings for all screens** (`UI/Resources/TacticalFivePanelSettings.asset`):
  - Render mode: Screen Space Overlay.
  - `m_ScaleMode: ScaleWithScreenSize`, reference 1920×1080, match 0.5, reference DPI 96.
  - `themeUss` → `TacticalFiveTheme.tss` (the only `.tss`) which `@import`s `Styles/GlobalVariables.uss`, `Styles/Typography.uss`, `Styles/Utilities.uss`.
- **Every screen** is a root GameObject with `UIDocument` (same PanelSettings) + `FullScreenUI` + its controller MonoBehaviour. One screen GameObject per `GameScreen` value in `MainMenu.unity`; only 4 active at boot (MainCamera, ScreenManager, CursorManager, LoadingDocument).
- **Per-screen files:** 48 UXML/USS pairs exist under `UI/Screens/`: 47 operational screens plus the uninstantiated `LegalNotice.uxml`; `LegalNotice.uss` is still imported by MainMenu and other screens. Shared USS references mix `project://database/...` and relative paths, not only GUID-style references.

## 2. Theme & styling architecture

```
TacticalFivePanelSettings.asset (PanelSettings)
 └─ themeUss → TacticalFiveTheme.tss
     └─ @import GlobalVariables.uss   (:root CSS variables — colors, accents, buttons)
     └─ @import Typography.uss        (.text-xs … .text-hero, color/text-align utils)
     └─ @import Utilities.uss         (.flex-*, .p-*, .m-*, .w-full, .rounded-*)
```

- **GlobalVariables.uss** defines `--bg-primary rgb(11,16,23)`, `--bg-secondary`, `--bg-card`, `--bg-modal`, borders, text colors, accents (`--accent-blue/gold/green`), button variables.
- **F7-01/F7-02:** no se usa `rgba()` en USS/UXML. Los colores translúcidos se han compuesto a `rgb()` opaco sobre el fondo oscuro del bloque (base `rgb(18,21,28)`) o se resuelven con `opacity`. Las mayúsculas usan `text-transform: uppercase` (nunca `-unity-text-transform` ni `text-transform: upper`); `-unity-text-align` se mantiene. `UiStyleTests` falla si reaparece cualquiera.
- **F7-03 · Resoluciones:** `TacticalFivePanelSettings` usa `ScaleWithScreenSize`, referencia **1920×1080**, `MatchWidthOrHeight` y `match=0.5`. El modal fijo más grande es **1700×675 px de referencia**; con el escalado se queda en 1133×450 a 1280×720, 1209×480 a 1366×768, 1700×675 a 1920×1080, 2267×900 a 2560×1440 y 2626×1043 a ultrawide 3440×1440. Todos caben. Las clases de modal tienen además `max-width/max-height: 100%` como red de seguridad. `UiStyleTests.Modals_FitTheMinimumViewport` verifica la matriz.
- **Typography.uss** sizes 10px→80px; **Utilities.uss** flexbox/spacing helpers.
- **Per-screen USS** overrides; fonts `BarlowCondensed-*` and `Saira_Condensed-*` referenced via `url("project://database/Assets/_TacticalFive/Art/Fonts/...")` with TMP SDF counterparts under `Art/Fonts/*.asset`.

## 3. Reusable components

### Header (`UI/Resources/UI/Core/Header.uxml` + `HeaderController.cs` — static helper)
- Injected at runtime: `Resources.Load<VisualTreeAsset>("UI/Core/Header")` → `template.CloneTree()` appended to the root. — `HeaderController.cs:13`
- Contains: brand ("TF TACTICAL FIVE"), team logo, team/manager name, 4 stat blocks (PRESUPUESTO / MASA SALARIAL / MARGEN / QUÍMICA), season + date, action button (`BtnAction`), config icon (`ConfigIcon`).
- `Attach(root, registerBtnAction)` is **idempotent** (repopulates an existing `TopHeader`; otherwise restructures the container into header + `bodyRow` (sidebar + main) and re-adds modals as absolute children). Default `BtnAction` → `GoTo(Dashboard)`.

### Sidebar (`UI/Resources/UI/Core/Sidebar.uxml` + `SidebarController.cs` — static helper)
- Injected like the header (`Resources.Load<VisualTreeAsset>("UI/Core/Sidebar")`). `Attach(root, activeScreen)` is idempotent; inserts sidebar at index 0; `ApplyActiveState` highlights the current screen; `LoadIcons` loads the 13 nav icons from `Resources/Icons/`.
- **27 nav mappings** (`SidebarController.cs:15-45`) over 13 top-level items + 5 submenus (18 items). Full list in §5. The identical handlers are registered by `UIScreenController.RegisterNavButtons` (13 navs + 18 submenu items, `UIScreenController.cs:138-247`).

### CustomSlider (`Scripts/UI/CustomSlider.cs`)
- Custom slider control (not a native `Slider`) used in the config modals (master/music/SFX). Binds drag to value changes via `OnValueChanged` callback → `AudioManager` volume setters.

### Barra de scroll propia (viewport + content + track/thumb)
- **[F]** El tema del proyecto (`TacticalFiveTheme.tss`) **no incluye los estilos base de Unity** para `ScrollView`, así que la barra interna no se dimensiona ni se sincroniza de forma fiable.
- Para listas largas con barra arrastrable se usa un patrón propio: viewport (`overflow: hidden`) + content desplazado con `style.top = -scrollY` + track/thumb propios. Implementación de referencia: modal de traspasos de `Market` (`MarketController.cs:278-422`).
- **Guía completa y reutilizable: [`Docs/CUSTOM_SCROLLBAR.md`](CUSTOM_SCROLLBAR.md).**

## 4. Controller pattern — `UIScreenController` base

**[F] Screen controllers inherit `UIScreenController`** (base in `Scripts/Core/UIScreenController.cs`, 609 ln). The base centralizes:

```csharp
protected virtual void OnEnable()
{
    _doc = GetComponent<UIDocument>();
    _root = _doc.rootVisualElement;
    MakeFullscreen();          // absolute full-screen root
    CacheReferences();         // virtual — bind UXML elements
    LoadSidebarIcons();        // (dead — sidebar loads its own icons)
    LoadData();                // _manager/_myTeam/_season from DB
    RegisterCallbacks();       // sidebar+header attach, BtnAction, RegisterNavButtons, cursors, ConfigIcon
    InitConfigModal();         // config modal wiring
    Refresh();                 // default: RefreshHeader
    LocalizationService.Apply(_root); // F7-06: resuelve {loc:...} tras el Refresh
}
```

**What the base provides (all virtual):**
- `ScreenId` (per-screen enum id), `BtnActionTarget` (default Dashboard).
- **Fullscreen:** `MakeFullscreen` (absolute 0/0/100%/100%). (Redundant with `FullScreenUI.Awake`.)
- **Chrome injection:** `RegisterCallbacks` calls `SidebarController.Attach(_root, ScreenId)` + `HeaderController.Attach(_root, registerBtnAction:false)`, binds `BtnAction` → `PlayClick(); OnBtnActionClicked()`, `RegisterNavButtons()`, `RegisterHandCursors()`, and the config icon → `OpenConfigModal()`.
- **Navigation:** `RegisterNavButtons` wires 13 navs + 18 submenu items (sidebar submenu toggling with `nav-submenu--visible` class).
- **Config modal:** `InitConfigModal`/`OpenConfigModal`/`CloseConfigModal` — 3 `CustomSlider`s (Master/Music/SFX), quality Low/Medium/High/Ultra, **sim-mode toggle** (Directa / Play-by-play, `TF_SimMode`), main-menu confirm overlay, exit confirm overlay. **This replaced the old per-screen copy-paste duplication.**
- `PlayClick()` → `AudioManager.Instance?.PlaySFX("click")`.
- `Refresh()` → default cursor + `RefreshHeader()` (rebuilds the header stat blocks) in try/catch.
- **Localización (F7-06):** `LocalizationService.Apply(_root)` resuelve tokens `{loc:clave}` en textos, tooltips y placeholders después de `Refresh`; así también cubre el header/sidebar inyectados. Los controllers usan `LocalizationService.T("clave", args)` para texto dinámico. Detalle: [`Docs/LOCALIZATION.md`](LOCALIZATION.md).

**Inheritance quirks (verified):**
- 17 controllers call `base.RegisterCallbacks()`; **12 override `RegisterCallbacks()` WITHOUT base** (Editor, EndSeason, GameResults, LoadGame, MainMenu, MatchDay, NewSeason, PlayerAwards, Preseason, Quintos, SeasonSummary, SelectTeam) — those screens build their own chrome/nav or are standalone (boot/menu/slot screens). The remaining 12 don't override it (base behavior).
- `GameResultsController` re-implements nav/submenu/cursor wiring in its override (references `NavRecords`/`NavSponsors`/`NavTV`, which do **not** exist in `Sidebar.uxml` — null-safe `?.`).
- **Callback lifecycle [F, corregido]:** las pantallas se desactivan con `SetActive(false)` pero no se destruyen. **En Unity 6 `UIDocument.OnDisable()` anula `rootVisualElement` y `OnEnable` vuelve a construir el árbol, así que los handlers NO se acumulan entre visitas.** El riesgo real es re-bindear *dentro* de una misma visita al reconstruir listas (resuelto con `ScrollBarBinder`, `UI-04`).
- `BtnActionTarget` is never overridden; `OnBtnActionClicked` overridden by `CalendarController` and `DashboardController`.

## 5. Screens inventory

| # | Screen | UXML folder | Controller | Notes |
|---|---|---|---|---|
| 1 | Loading | `Screens/Loading` | `LoadingController` | Boot/tip screen → MainMenu (click/key/10s) |
| 2 | MainMenu | `Screens/MainMenu` | `MainMenuController` | Manager / Pro Manager / Load / Editor / Exit; legal + bug-report + config + ProModal |
| 3 | SelectTeam | `Screens/SelectTeam` | `SelectTeamController` | Pick franchise (mode-aware; ProManager → worst teams only) |
| 4 | Preseason | `Screens/Preseason` | `PreseasonController` | Preseason sim + schedule generation |
| 5 | Dashboard | `Screens/Dashboard` | `DashboardController` | Home hub; day advance, fast sim, modals, toasts, deadline day |
| 6 | Roster | `Screens/Roster` | `RosterController` | Roster + player detail; renew/dismiss/buyout/trade-block; 9 modals |
| 7 | Quinteto | `Screens/Quinteto` | `QuintetoController` | Starting five/rotation + load-management toggle |
| 8 | Training | `Screens/Training` | `TrainingController` | Attribute training |
| 9 | Employees | `Screens/Employees` | `EmployeesController` | Staff hiring/firing |
| 10 | Injured | `Screens/Injured` | `InjuredController` | Injured list + treatment |
| 11 | Dorsales | `Screens/Dorsales` | `DorsalesController` | Retired numbers: tabs Actuales/Retirados |
| 12 | Calendar | `Screens/Calendar` | `CalendarController` | Season schedule + SIMULAR HASTA |
| 13 | Results | `Screens/Results` | `ResultsController` | Game results list |
| 14 | Playoffs | `Screens/Playoffs` | `PlayoffsController` | Playoff bracket |
| 15 | Standings | `Screens/Standings` | `StandingsController` | Conference standings + pestaña NBA CUP (grupos, campeón/MVP) |
| 16 | Stats | `Screens/Stats` | `StatsController` | League stat leaders |
| 17 | Records | `Screens/Records` | `RecordsController` | Records screen |
| 18 | Palmares | `Screens/Palmares` | `PalmaresController` | Historical palmarés + Hall of Fame panel |
| 19 | Premios | `Screens/Premios` | `PremiosController` | Monthly awards |
| 20 | Logros | `Screens/Logros` | `LogrosController` | GM achievements: tabs + grid + counter |
| 21 | Market | `Screens/Market` | `MarketController` | Trades + FA market (TO/PO toggles, S&T, deadline banner) |
| 22 | Cartera | `Screens/Cartera` | `CarteraController` | Scouting FM-style: 3 tabs (OJEAR/CARTERA/INFORMES), selector de equipos, filas ricas, fog-of-war |
| 23 | Historial | `Screens/Historial` | `HistorialController` | Trade history |
| 24 | Finances | `Screens/Finances` | `FinancesController` | P&L + Cap sheet |
| 25 | Loans | `Screens/Loans` | `LoansController` | Loans |
| 26 | Sponsors | `Screens/Sponsors` | `SponsorsController` | Sponsor deals |
| 27 | TV | `Screens/TV` | `TVController` | TV deals |
| 28 | Arena | `Screens/Arena` | `ArenaController` | Arena management (tickets/subscriptions/renovations) |
| 29 | Manager | `Screens/Manager` | `ManagerController` | Manager profile, morale/fans circles, psychologist, objective |
| 30 | Messages | `Screens/Messages` | `MessagesController` | Inbox |
| 31 | MatchDay | `Screens/MatchDay` | `MatchDayController` | Pre-match (matchup preview) + play-by-play overlay + boxscore |
| 32 | GameResults | `Screens/GameResults` | `GameResultsController` | Post-match box score |
| 33 | LoadGame | `Screens/LoadGame` | `LoadGameController` | Save slots |
| 34 | Editor | `Screens/Editor` | `EditorController` | Editor (template DB) |
| 35 | EndSeason | `Screens/EndSeason` | `EndSeasonController` | Retirees, HOF, expiring, draft |
| 36 | NewSeason | `Screens/NewSeason` | `NewSeasonController` | Post-draft rollover (options, re-signs, decremento de contratos antes del control de plantilla >16 estándar o >2 two-way (16 + 2 = 18 activos), Summer League/camp de desarrollo y StartNewSeason async) |
| 37 | SeasonSummary | `Screens/SeasonSummary` | `SeasonSummaryController` | Season recap |
| 38 | PlayerAwards | `Screens/PlayerAwards` | `PlayerAwardsController` | Player awards |
| 39 | PlayerProfile | `Screens/PlayerProfile` | `PlayerProfileController` | Banner (foto, dorsal, escudo, honores, MED, trayectoria) + 3 columnas (Información Personal · Atributos como badges · Últimas Lesiones) + stats de temporada; fog-of-war gate |
| 40 | Quintos | `Screens/Quintos` | `QuintosController` | Season All-Star/Rookie quintets |
| 41 | Trajectory | `Screens/Trajectory` | `TrajectoryController` | Player career + evolución TS%/EFI/48 y comparación con la liga |
| 42 | PreGame | `Screens/PreGame` | `PreGameController` | Previas del partido: pronóstico/favorito, registros, ofensiva/defensiva + rango, bajas, alineaciones y claves; botón VOLVER y "IR AL PARTIDO" (visible **solo si el partido es el del día pendiente**; si es dentro de 2-3 días se oculta, y con él el atajo "D") |
| 43 | InfoLeague | `Screens/InfoLeague` | `InfoLeagueController` | Información de la liga: pestañas FINANZAS (economía de la competición) y EQUIPOS (plantillas por conferencia Este/Oeste) |
| 44 | TheCity | `Screens/TheCity` | `TheCityController` | **Centro de mando (F7-05):** pulso de la ciudad (presupuesto, afición, objetivo/ranking y próximo partido), hotspots con estado en vivo e inspector por edificio (estado, métricas y acceso profundo a su pantalla). Pestañas EDIFICIO / PROYECTOS / EVENTOS: proyectos persistentes (`city_projects`) y eventos con decisiones (`city_events`), sin duplicar Finanzas/Pabellón/Empleados. **GameObject `TheCity` (sin sufijo `Document`)** |
| 45 | Buscador | `Screens/Buscador` | `BuscadorController` | Buscador de jugadores: filtros posición/edad/equipo/OVR/solo FA, orden por columna y scrollbar propia |
| 46 | GLeague | `Screens/GLeague` | `GLeagueController` | G-League con 4 pestañas: PLANTILLA (plantilla completa de la filial — prospectos + asignados NBA — con RECUPERAR en los asignados, y elegibles), RESULTADOS (la tabla de partidos del filial por fecha), CLASIFICACIÓN (Este/Oeste, corte playoff) y ESTADÍSTICAS (top-5 promedios); reutiliza clases de Stats.uss y Standings.uss |
| 48 | Hilos | `Screens/Hilos` | `HilosController` | Hilos narrativos de la temporada (movido desde la pestaña HISTORIA de Palmarés); submenú ACTUALIDAD |
| 49 | Rivalidades | `Screens/Rivalidades` | `RivalidadesController` | Dinastías de la liga y rivalidades de playoffs (movido desde la pestaña RIVALIDADES de Palmarés); submenú COMPETICIONES |
| 47 | Rotation | `Screens/Rotation` | `RotationController` | **ROTACIÓN** (submenu PLANTILLA, tras QUINTETO): reparto de los 240 minutos del partido entre los 12 convocados. Slider por jugador (0-48) + columnas POS / ROL / MED / MIN / SUG. (`GameSimulator.GetSuggestedMinutes` → `GetAutomaticMinutes`: rol, nivel y forma física), panel de solo lectura MINUTOS POR POSICIÓN (principal/secundaria), contador de minutos restantes y botones AUTO (240) / RESET (todo a 0). Los sliders nunca permiten pasar de 240 min en total (`CustomSlider.MaxValue`): con el reparto completo (o excedido) cada barra solo puede bajar hasta que alguien libere minutos. Se persiste en `team_lineup.rotation_minutes` (`-1` = automático) y el simulador lo usa como `target_minutes` en `PrepareRotationMetadata`, **vinculante** vía `PickSubstitution` (suelo 2 min; 0 = no juega). Sin nada configurado, la pantalla y el simulador usan el mismo reparto automático de 240 (F2-11) |

> **Conteo vigente:** ejecutar `python3 Tools/project_status.py`; el comando valida que `GameScreen`, los `UIDocument` registrados y los casos de `ScreenManager.GoTo` estén sincronizados. El índice de la tabla es solo una referencia de lectura.

**Sidebar navigation (verified, `SidebarController.cs:15-45`):**
- **INICIO** → Dashboard
- **PLANTILLA** (RosterSubmenu) → Jugadores (Roster), Quinteto, **Rotación**, Entrenamiento, Empleados, Lesionados, Dorsales
- **RESULTADOS** → Calendar, Results, Playoffs
- **CLASIFICACIÓN** → Standings
- **LOGROS** (the current `Sidebar.uxml` label for PalmaresSubmenu) → Palmares, Records, Premios
- **ESTADÍSTICAS** → Stats
- **MERCADO** (MarketSubmenu) → Ofertas (Market), Cartera, Historial
- **FINANZAS** (FinanceSubmenu) → Decisiones (Finances), Préstamos (Loans), Patrocinadores (Sponsors), Televisión (TV)
- **PABELLÓN** → Arena
- **MANAGER** (ManagerSubmenu) → Manager, Logros
- **ACTUALIDAD** (submenu) → NOTICIAS (Messages) · HILOS (Hilos)

## 6. Navigation tree

```mermaid
flowchart TD
    Loading --> MainMenu
    MainMenu -->|Manager/ProManager| SelectTeam
    MainMenu -->|Cargar partida| LoadGame
    MainMenu -->|Editor| Editor
    SelectTeam --> Preseason
    Preseason --> Dashboard
    Dashboard -->|sidebar| Roster
    Dashboard --> Quinteto & Rotation & Training & Employees & Injured & Dorsales
    Dashboard --> Calendar & Results & Playoffs & Standings & Stats & GLeague
    Dashboard --> Palmares & Records & Premios & Logros
    Dashboard --> Market & Cartera & Historial
    Dashboard --> Finances & Loans & Sponsors & TV
    Dashboard --> Arena & Manager & Messages
    Dashboard --> MatchDay
    MatchDay --> GameResults
    Roster --> PlayerProfile & Trajectory
    Stats --> PlayerProfile
    Dashboard --> EndSeason
    EndSeason --> NewSeason
    NewSeason --> Preseason
    Dashboard --> SeasonSummary & PlayerAwards
    Dashboard -->|menú| MainMenu
```

**Navigation mechanics:** `ScreenManager.GoTo(GameScreen, GameMode)` → `ShowOnly(doc)` toggles `SetActive`. Controllers rebuild in `OnEnable`. All `GameScreen` values have cases; no dead values.

## 7. How screens are created/shown/hidden

- **Created:** statically in the scene (each document is a scene GameObject with UIDocument/FullScreenUI/controller). Nothing is instantiated at runtime except the injected header/sidebar subtrees and procedural rows.
- **Shown:** `SetActive(true)` → `OnEnable` → base pipeline binds chrome + data + callbacks.
- **Hidden:** `SetActive(false)` (no manual teardown). Controllers rely on state being re-fetched each `OnEnable`.
- **Destroyed:** never (except app quit). Everything is rebuilt in `OnEnable`; any external mutation must re-trigger `Refresh()` manually.

## 8. Events used (see EVENTS.md for full table)

`ClickEvent` (all buttons), `KeyDownEvent` (Loading, MainMenu Escape), `MouseEnter/MouseLeave` (cursor via `CursorManager.RegisterHandCursor`), `CustomSlider.OnValueChanged` (drag), `WaitUntil` (modal blocking), `schedule.Execute` (deferred actions, e.g. cursor at +100ms).

## 9. Risks & observations

- **Dead `UIScreenController.LoadSidebarIcons`** — runs before the sidebar is attached; sidebar icons are loaded by `SidebarController.LoadIcons`. Cleanup candidate.
- **Double header population:** base `RefreshHeader` duplicates `HeaderController.Populate`; both execute on each screen load.
- **`GameResultsController`** re-implements nav referencing 3 non-existent sidebar elements (`NavRecords`/`NavSponsors`/`NavTV`) — harmless (null-safe) but confusing.

## Preguntas abiertas

- **Resuelto (auditoría 2026-09):** en Unity 6 `UIDocument.OnDisable()` anula `rootVisualElement` y `OnEnable` lo recrea, por lo que los handlers **no se acumulan entre visitas**. El riesgo real está *dentro* de una misma visita: re-bindear callbacks al reconstruir listas (scrollbar) y usar `??=` para handles de elementos que ya no existen. Ya corregido en Stats/Editor (`Bind` en cada visita) y pendiente de unificar el resto de scrollbars.
- ¿Debe eliminarse `LegalNotice.uxml` y conservarse solo el USS compartido, o se pretende convertir el aviso legal en pantalla real?
- ¿La mezcla de rutas USS relativas y `project://database` funciona en todas las plataformas objetivo?
- ¿La resolución mínima soportada es realmente 1920×1080, dado el uso extensivo de tamaños fijos?
- **`FullScreenUI` is redundant** with `UIScreenController.MakeFullscreen`.
- **Manual row building:** no `ListView`/`BindableElement` — rebuilding large lists on every `OnEnable` is cheap here (small datasets) but verbose.
- **Hardcoded 1920×1080** sizing everywhere (sidebar 200px, panels, grid) — no responsive breakpoints; `ScaleWithScreenSize` match 0.5 handles 16:9 mostly.
- **`LegalNotice.uxml`/`.uss`** folder still exists but the LegalNotice screen was removed; MainMenu has the legal modal inline. Orphaned assets.

## 10. Open questions

- Whether `GameResultsController`'s full nav re-implementation (without base) is intentional or leftover from before the base class existed. [H]
- Whether a second All-Star/Rookie quintet (or team) is intended in `Quintos` UI. [H]
