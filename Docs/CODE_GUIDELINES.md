# CODE_GUIDELINES — Tactical Five

> Extracted from the actual codebase. Follow these to keep the project coherent. **[F]** observed fact, **[D]** deduction, **[H]** hypothesis.

## 1. General conventions (observed)

- **Language:** C# files are English in structure but heavily Spanish in identifiers/comments/log strings (`_myTeam`, `Cargar`, `Debug.Log("[Dashboard] ...")`). Follow the same bilingual style; do not force English identifiers for game concepts.
- **Namespaces:** none — all scripts are in the global namespace. Classes are referenced by name (`ScreenManager`, `DatabaseManager`).
- **Enums:** `GameEnums.cs` holds `GameScreen`, `GameMode` (global). `Constants.cs` holds `PlayerRole`, `PositionCodes`, `CountryCodes`, `NCAATeams`.
- **Null-safety:** controllers heavily use `?.` and null checks before dereferencing UI elements (`_foo?.style.display = ...`).
- **Formatting:** ~4-space indentation; comments in Spanish; section separators like `// ── NOMBRE ─────────────`.
- **Unity lifecycle:** everything UI re-initializes in `OnEnable` (never in `Start`). `Awake` only for singletons/FullScreenUI.
- **Database access:** never open your own `SQLiteConnection`; use `DatabaseManager.Instance`.
- **Randomness:** `UnityEngine.Random` (static) everywhere except `GeneratePositionAttrs` (seeded `System.Random`).
- **Resources:** all runtime-loaded content under `Resources/` folders. Load via `Resources.Load<T>` / `Resources.LoadAll<T>`.

## 2. Patterns to follow

| Pattern | How | Example |
|---|---|---|
| Singleton | `public static X Instance { get; private set; }` + `Awake` guard + optional `DontDestroyOnLoad` | `ScreenManager`, `DatabaseManager`, `AudioManager`, `CursorManager` |
| Screen controller | `OnEnable`: force full-screen root → `CacheReferences()` → `LoadData()` → `RegisterCallbacks()` → `Refresh()` | any `*Controller.cs` |
| DB access | delegate to `DatabaseManager.Instance.<method>`; models are `[Table]` classes with `{ get; set; }` properties | `PlayerData`, `GameData` |
| Static utility | pure static class with no mutable state | `TradeHelper`, `GameSimulator`, `ScheduleGenerator` |
| Cross-screen context | `ScreenManager.SelectedPlayerId` | `RosterController` → `TrajectoryController` |
| Result hand-off after sim day | `GameResultCache` statics | `DashboardController` → `GameResultsController` |
| User feedback | insert `MessageData` via `DatabaseManager.AddMessage` | everywhere |
| Money formatting | `$"{value:N0}"` / `$"{value:N0} $"` | `DashboardController`, `FinancesController` |
| Logos | `Resources.LoadAll<Sprite>("Teams/Logos/{size}x{size}")` into `Dictionary<string,Sprite>` | every controller with team logos |
| Modals | overlay + box elements; toggle `DisplayStyle.Flex/None`; register close buttons | `RosterController` modals |

## 3. How to add a new UI screen

1. **UXML:** create `UI/Screens/MyScreen/MyScreen.uxml` (root element, ids for dynamic parts, `<Style src=...>` pointing at your USS + `Dashboard.uss`/`LegalNotice.uss` for shared styles).
2. **USS:** `MyScreen.uss` reusing the CSS variables from `GlobalVariables.uss`.
3. **Scene:** add a GameObject `MyScreenDocument` in `MainMenu.unity` with `UIDocument` (PanelSettings = TacticalFivePanelSettings, sourceAsset = your UXML) + `FullScreenUI` + your controller.
4. **ScreenManager:** add a serialized `UIDocument` field, a `GameScreen` enum value, and a `case` in `GoTo`'s switch.
5. **Controller:** `MyScreenController : MonoBehaviour` following the OnEnable pipeline. Build lists procedurally (no ListView).
6. **Nav:** to open it, call `ScreenManager.Instance.GoTo(GameScreen.MyScreen)`. Add a Sidebar entry if it belongs in the nav.

## 4. How to add a new mechanic/game rule

1. **Model:** add/update a `[Table]` class under `Scripts/Data/` (+ `CreateTable<T>()` in `CreateTables` if new).
2. **Persistence:** add the CRUD methods to `DatabaseManager` (keep ALL DB code there).
3. **Migration:** add an `ALTER TABLE ADD COLUMN` block in `RunMigrations` if you extend an existing table (follow the `PRAGMA table_info` pattern).
4. **Logic:** put pure rules in a static helper (`TradeHelper`-style) so controllers stay thin.
5. **Trigger:** hook into the appropriate point (`ProcessGameDayRoutine`, `StartNewSeason`, or a controller action).
6. **Feedback:** write `MessageData` entries and refresh UI via the controller's `Refresh()`.

## 5. How to register new DB messages/events

- **Inbox message:** `DatabaseManager.Instance.AddMessage(new MessageData { manager_id, sender_type, sender_id, title, body, game_day, game_date, created_at, date_sent, is_read = 0 })`. `sender_type`: 0 system, 1 player, 2 quick news.
- **Cross-screen data:** prefer a static holder (like `GameResultCache`) or `ScreenManager` statics; **clear it** where appropriate (`GameResultCache.Clear()` at day start).
- There is **no event bus** — do not introduce `Action`/`UnityEvent` systems casually without discussing the architecture (see TODO).

## 6. How to add seed data

Add a `SeedX()` method in `DatabaseManager` guarded by a table-empty check in `SeedStaticDataIfNeeded()`. For large static datasets create a seeder class (e.g., `HistoricalPlayerStatsSeeder`, `PalmaresSeeder`, `TeamRecordSeeder`) and reference it.

## 7. Save-system rules

- New persistent fields ⇒ add a migration (column-presence based), keep defaults sensible.
- Never write outside `DatabaseManager`/`GameSaveManager`; never assume schema version.
- `template.db` regeneration: the Editor flow rebuilds it; adding seeders means `EnsureTemplateDb` will pick them up when it runs.

## 8. Anti-patterns observed (do not extend)

- **Copy-pasting the config modal / confirm dialogs** into every controller and UXML — refactor into a shared `ConfigModalController` + one UXML/USS (see TODO P1/P2).
- **Re-fetching logo dictionaries** in every `OnEnable` — cache once (e.g., static).
- **Multiple sequential DB queries in one refresh** — batch where possible (`GetGamePlayerStatsBatch`, etc.).
- **`GetComponent`/`FindObjectOfType` in loops** — cache in fields.
- **Direct `gameObject.SetActive` navigation from controllers** — always via `ScreenManager.GoTo`.
- **Stringly-typed positions/roles** (`"PABELLON"`, `"regular"`, positions as strings) — documented in GLOSSARY; consider enums (TODO).

## 9. Order of files to read for a new developer

See `.agent/SKILLS.md` ("Fastest path to understanding").
