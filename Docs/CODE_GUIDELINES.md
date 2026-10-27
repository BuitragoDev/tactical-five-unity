# CODE_GUIDELINES — Tactical Five

> Extracted from the actual codebase. Follow these to keep the project coherent. **[F]** observed fact, **[D]** deduction, **[H]** hypothesis.

## 1. General conventions (observed)

- **Language:** C# files are English in structure but heavily Spanish in identifiers/comments/log strings (`_myTeam`, `Cargar`, `Debug.Log("[Dashboard] ...")`). Follow the same bilingual style; do not force English identifiers for game concepts.
- **Namespaces:** none — all scripts are in the global namespace. Classes are referenced by name (`ScreenManager`, `DatabaseManager`).
- **Enums:** `GameEnums.cs` holds `GameScreen`, `GameMode` (global). `Constants.cs` holds `PlayerRole`, `PositionCodes`, `CountryCodes`, `NCAATeams`. `GmAchievementType` is in `GmAchievementType.cs`; `TeamStrategy` in `TeamStrategy.cs`.
- **Null-safety:** controllers heavily use `?.` and null checks before dereferencing UI elements (`_foo?.style.display = ...`).
- **Formatting:** ~4-space indentation; comments in Spanish; section separators like `// ── NOMBRE ─────────────`.
- **Unity lifecycle:** everything UI re-initializes in `OnEnable` (never in `Start`). `Awake` only for singletons/FullScreenUI.
- **Database access:** never open your own `SQLiteConnection`; use `DatabaseManager.Instance`. Heavy batches go through `RunInBackground`/`RunInBackgroundAsync` (ambient connection — do not touch `_db` directly off-thread).
- **Randomness:** `UnityEngine.Random` (static) for UI/offline sims; `System.Random` for background/threaded work (thread-static `_aiRng` in `DashboardController`, `DatabaseManager.Rng`). Never share `UnityEngine.Random` across threads.
- **Resources:** all runtime-loaded content under `Resources/` folders. Load via `Resources.Load<T>` / `Resources.LoadAll<T>`.
- **Texto de UI (F7-06):** los UXML usan `text="{loc:clave}"` (también `placeholder`/`placeholder-text`) y el C# usa `LocalizationService.T("clave")` o `T("clave", args)` para plantillas. No añadas literales visibles nuevos en UXML/controllers; la tabla vive en `Resources/Localization/es.json` y se regenera/valida con `python3 Tools/localization_extract.py`. `LocalizationTests` falla si reaparece un literal estático o falta una entrada. Detalle: [`Docs/LOCALIZATION.md`](LOCALIZATION.md).

## 2. Patterns to follow

| Pattern | How | Example |
|---|---|---|
| Singleton | `public static X Instance { get; private set; }` + `Awake` guard + optional `DontDestroyOnLoad` | `ScreenManager`, `DatabaseManager`, `AudioManager`, `CursorManager` |
| Screen controller | **inherit `UIScreenController`** → `OnEnable`: full-screen root → `CacheReferences()` → `LoadData()` → `RegisterCallbacks()` → `Refresh()` (all virtual; override only what differs) | any `*Controller.cs` |
| DB access | delegate to `DatabaseManager.Instance.<method>`; models are `[Table]` classes with `{ get; set; }` properties | `PlayerData`, `GameData` |
| Static utility | pure static class with no mutable state | `TradeHelper`, `GameSimulator`, `ScheduleGenerator`, `AchievementService` |
| Cross-screen context | `ScreenManager.SelectedPlayerId` | `RosterController` → `PlayerProfileController`/`TrajectoryController` |
| Result hand-off after sim day | `GameResultCache` statics | `DashboardController` → `GameResultsController` |
| Achievement feedback | `AchievementService.UnlockIfMissing` → toast queue → `DashboardController.Update` | Logros |
| User feedback | insert `MessageData` via `DatabaseManager.AddMessage` | everywhere |
| Money formatting | `$"{value:N0}"` / `$"{value:N0} $"` | `DashboardController`, `FinancesController` |
| Logos | `Resources.LoadAll<Sprite>("Teams/Logos/{size}x{size}")` into `Dictionary<string,Sprite>` | every controller with team logos |
| Modals | overlay + box elements; toggle `DisplayStyle.Flex/None`; register close buttons | `UIScreenController.InitConfigModal`; `RosterController` modals |

## 2.1 Modern modal styles (from Dashboard/Roster)

Estos son los estilos estándar para modales y botones de acción. **Copiar siempre de aquí**, no inventar nuevos estilos.

### Modal box

```css
.trade-modal-box {
    width: 720px;
    background-color: rgb(13, 14, 18);
    border-width: 1px;
    border-color: rgb(180, 140, 40);   /* cambiar por color de estado */
    border-radius: 12px;
    padding: 0;
    flex-direction: column;
    overflow: hidden;
}
```

Variantes de color de borde:
- **Rojo (peligro/rescisión):** `border-color: rgb(192, 57, 43)`
- **Verde (positivo/renovar):** `border-color: rgb(39, 174, 96)`
- **Dorado (advertencia):** `border-color: rgb(212, 160, 23)`
- **Azul (informativo):** `border-color: rgb(42, 95, 201)`

### Modal title

```css
.modal-title {
    -unity-font: url("project://database/Assets/_TacticalFive/Art/Fonts/Saira_Condensed-Bold.ttf");
    font-size: 34px;
    letter-spacing: 2px;
    color: rgb(255, 255, 255);
}
```

### Modal text

```css
.modal-text {
    -unity-font: url("project://database/Assets/_TacticalFive/Art/Fonts/Saira_Condensed-Regular.ttf");
    font-size: 20px;
    color: rgb(179, 190, 214);
    white-space: normal;
    -unity-text-align: middle-center;
}
```

### Button row

```css
.btn-row {
    flex-direction: row;
    width: 100%;
    align-self: stretch;
    margin-top: 24px;
}
```

**No usar `gap`** — UI Toolkit no lo soporta. Separar botones con `margin-left: 6px`.

**Mayúsculas:** usar siempre `text-transform: uppercase`; no `-unity-text-transform` ni `text-transform: upper`. No usar `rgba()`: componer el color sobre el fondo oscuro conocido (`rgb(18, 21, 28)` por defecto) o usar `opacity` si el diseño lo permite.

### Action buttons (outline moderno)

Botón de acción principal:

```css
.btn-action {
    -unity-font: url("project://database/Assets/_TacticalFive/Art/Fonts/Saira_Condensed-Bold.ttf");
    flex-grow: 1;
    flex-basis: 0;
    margin-left: 6px;
    background-color: rgb(20, 36, 35);   /* verde compuesto sobre fondo oscuro */
    border-width: 2px;
    border-color: rgb(39, 174, 96);
    color: rgb(100, 200, 140);
    font-size: 16px;
    letter-spacing: 1px;
    padding: 12px 20px;
    -unity-text-align: middle-center;
    border-radius: 8px;
    min-width: 0;
    white-space: normal;
    cursor: pointer;
}
.btn-action:hover {
    background-color: rgb(23, 59, 45);
}
```

Botón de peligro/rescisión:

```css
.btn-action--danger {
    background-color: rgb(35, 25, 30);
    border-color: rgb(192, 57, 43);
    color: rgb(230, 120, 100);
}
.btn-action--danger:hover {
    background-color: rgb(62, 30, 32);
    color: rgb(240, 145, 125);
}
```

**Regla:** botones de cerrar/cancelar → rojo (`--danger`); botones de confirmar/continuar → verde.

### Section headers (tipo Quinteto/Entrenamiento)

Para paneles con cabecera estilo dorado/marrón:

```css
.section {
    flex-direction: column;
    border-width: 1px;
    border-color: rgb(120, 95, 30);
    background-color: rgb(18, 21, 28);
    border-radius: 4px;
}
.section-header {
    flex-direction: row;
    align-items: center;
    padding: 10px 14px;
    background-color: rgb(33, 32, 28);
    border-bottom-width: 1px;
    border-bottom-color: rgb(120, 95, 30);
}
.section-title {
    -unity-font: url("project://database/Assets/_TacticalFive/Art/Fonts/Saira_Condensed-Bold.ttf");
    font-size: 18px;
    color: rgb(212, 160, 23);
    letter-spacing: 2px;
}
```

### Screen title panel (tipo Roster)

Para la cabecera principal de una pantalla, usar un panel de ancho completo con
borde dorado, fondo marrón translúcido, icono a la izquierda, título blanco y
subtítulo dorado. El patrón actual de Roster es:

```xml
<ui:VisualElement class="screen-header panel--gold">
    <ui:VisualElement class="screen-header-row">
        <ui:VisualElement name="ScreenIcon" class="screen-icon" />
        <ui:Label class="screen-title" text="JUGADORES" />
    </ui:VisualElement>
    <ui:Label class="screen-subtitle" text="" />
</ui:VisualElement>
```

```css
.screen-header.panel--gold {
    flex-shrink: 0;
    padding: 0;
    margin: 16px 20px 12px 20px;
    border-width: 1px;
    border-color: rgb(212, 160, 23);
    background-color: rgb(33, 32, 28);
    border-radius: 4px;
    overflow: hidden;
}
.screen-header-row {
    flex-direction: row;
    align-items: center;
    padding: 5px 14px;
    background-color: rgb(33, 32, 28);
}
.screen-icon {
    width: 40px;
    height: 40px;
    margin-right: 10px;
    flex-shrink: 0;
    background-size: contain;
    background-repeat: no-repeat;
    background-position: center;
}
.screen-title {
    -unity-font: url("project://database/Assets/_TacticalFive/Art/Fonts/BarlowCondensed-Bold.ttf");
    font-size: 32px;
    letter-spacing: 3px;
    color: rgb(255, 255, 255);
}
.screen-subtitle {
    -unity-font: url("project://database/Assets/_TacticalFive/Art/Fonts/Saira_Condensed-Regular.ttf");
    font-size: 16px;
    letter-spacing: 1px;
    color: rgb(212, 160, 23);
    padding: 0 14px;
    background-color: rgb(33, 32, 28);
}
```

Reglas del patrón:
- El selector combinado `.screen-header.panel--gold` debe ser más específico que el `.panel--gold` compartido para conservar el borde dorado cuando el USS de Dashboard se carga después.
- No añadir `border-top` o `border-bottom` adicionales al `screen-header-row` o al subtítulo: el panel exterior debe tener un único borde dorado.
- Cargar el icono desde `Resources` en `LoadData`, por ejemplo `Resources.Load<Texture2D>("Icons/plantilla_icon")`, y asignarlo a `style.backgroundImage`.
- Mantener márgenes `16px 20px 12px 20px` para alinear la cabecera con los paneles de contenido.

## 3. How to add a new UI screen

1. **UXML:** create `UI/Screens/MyScreen/MyScreen.uxml` (root element, ids for dynamic parts, `<Style src=...>` pointing at your USS + shared styles).
2. **USS:** `MyScreen.uss` reusing the CSS variables from `GlobalVariables.uss`.
3. **Scene:** add a GameObject `MyScreenDocument` in `MainMenu.unity` with `UIDocument` (PanelSettings = TacticalFivePanelSettings, sourceAsset = your UXML) + `FullScreenUI` + your controller.
4. **ScreenManager:** add a serialized `UIDocument` field, a `GameScreen` enum value, and a `case` in `GoTo`'s switch.
5. **Controller:** `MyScreenController : UIScreenController` (the base provides full-screen, header/sidebar chrome, nav buttons, config modal, cursors — override the virtual hooks you need). Build lists procedurally (no ListView).
6. **Nav:** to open it, call `ScreenManager.Instance.GoTo(GameScreen.MyScreen)`. Add a Sidebar entry if it belongs in the nav.

## 4. How to add a new mechanic/game rule

1. **Model:** add/update a `[Table]` class under `Scripts/Data/` (+ `CreateTable<T>()` in `CreateTables` if new).
2. **Persistence:** add the CRUD methods to `DatabaseManager` (keep ALL DB code there; it's split across partials by domain).
3. **Migration:** extend an existing table → add an `ALTER TABLE ADD COLUMN` block in `RunMigrations` (follow the `PRAGMA table_info` pattern). One-time data fixes → a named migration registered in `schema_migrations` (`IsMigrationApplied`/`MarkMigrationApplied`).
4. **Logic:** put pure rules in a static helper (`TradeHelper`-style) so controllers stay thin.
5. **Trigger:** hook into the appropriate point (`ProcessGameDayRoutine`, `StartNewSeason`, or a controller action).
6. **Feedback:** write `MessageData` entries and refresh UI via the controller's `Refresh()`.

## 5. How to register new DB messages/events

- **Inbox message:** `DatabaseManager.Instance.AddMessage(new MessageData { manager_id, sender_type, sender_id, title, body, game_day, game_date, created_at, date_sent, is_read = 0 })`. `sender_type`: 0 system, 1 player, 2 quick news.
- **Cross-screen data:** prefer a static holder (like `GameResultCache`) or `ScreenManager` statics; **clear it** where appropriate (`GameResultCache.Clear()` at day start).
- There is **no event bus** — do not introduce `Action`/`UnityEvent` systems casually without discussing the architecture (see TODO).

## 6. How to add seed data

Add a `SeedX()` method in `DatabaseManager` guarded by a table-empty check in `SeedStaticDataIfNeeded()`. For large static datasets create a seeder class (e.g., `HistoricalPlayerStatsSeeder`, `PalmaresSeeder`, `TeamRecordSeeder`, `HallOfFameSeeder`, `RetiredNumberSeeder`) and reference it.

## 7. Save-system rules

- New persistent fields ⇒ add a migration (column-presence based), keep defaults sensible.
- Never write outside `DatabaseManager`/`GameSaveManager`; never assume schema version (read `schema_migrations` / `user_version`).
- `template.db` regeneration: the Editor flow rebuilds it; adding seeders means `EnsureTemplateDb` will pick them up when it runs.

## 8. Anti-patterns observed (do not extend)

- **Copy-pasting the config modal / confirm dialogs** into every controller and UXML — **resolved** via `UIScreenController.InitConfigModal` (note: the 12 controllers that override `RegisterCallbacks()` without `base` skip it; keep new screens on the base path).
- **Re-fetching logo dictionaries** in every `OnEnable` — cache once (e.g., static).
- **Multiple sequential DB queries in one refresh** — batch where possible (`GetGamePlayerStatsBatch`, `GetSeasonPlayerStatsAggregates`).
- **`GetComponent`/`FindObjectOfType` in loops** — cache in fields.
- **Direct `gameObject.SetActive` navigation from controllers** — always via `ScreenManager.GoTo`.
- **Stringly-typed positions/roles** (`"PABELLON"`, `"regular"`, positions as strings) — documented in GLOSSARY; consider enums (TODO B12).
- **Dead code duplication with the base** — don't add a second header/sidebar population or a second full-screen setup: the base already does it (`HeaderController.Attach`/`SidebarController.Attach` are idempotent; `MakeFullscreen` + `FullScreenUI` cover sizing).

## 9. Order of files to read for a new developer

See `.agent/SKILLS.md` ("Fastest path to understanding").
