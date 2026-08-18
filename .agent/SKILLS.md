# SKILLS — Agent Onboarding for Tactical Five

> This file is the **initial context** for any AI agent or developer working on this project. Read `Docs/PROJECT_OVERVIEW.md` and `Docs/ARCHITECTURE.md` for depth; this is the fast path to being productive.

---

## 1. What the game is (30 seconds)

A single-player **NBA management sim** ("Tactical Five", `v1.0.0 · Beta`) in Unity 6 (6000.3.15f1), entirely in Spanish, desktop 1920×1080. You manage one of 30 NBA-like teams through endless seasons: rosters, trades, free agents, contracts/salary cap (apróns, hard cap, luxury tax, exceptions), finances (tickets, subscriptions, sponsors, TV, loans, arena), training, morale/chemistry/injuries, draft (with protections/swaps), playoffs, awards, records, Hall of Fame, retired numbers, GM achievements, play-by-play match view, load management, fog-of-war scouting.

## 2. Architecture (essential facts)

| Fact | Consequence for you |
|---|---|
| **One scene** (`MainMenu.unity`); **41 screens** as `UIDocument` GameObjects; `ScreenManager.GoTo(GameScreen, mode)` toggles `SetActive`. | Never load scenes. Never navigate directly with `SetActive` — use `ScreenManager`. |
| **All persistence = SQLite** via `DatabaseManager.Instance` (bundled sqlite-net + native plugin), split into 9 partials. | Never open your own connection. All data access through `DatabaseManager`. |
| **No prefabs, no game ScriptableObjects.** | New views = UXML+USS; new content = seeders + SQLite tables. |
| **No event bus.** Communication = DB messages (`MessageData`), static state (`GameResultCache`, `ScreenManager.*`, `AchievementService` toast queue), `PlayerPrefs` (settings), DB `schema_migrations` (data migrations), UI callbacks. | Don't invent an event system; follow existing channels. |
| **Static utility classes** hold game logic (`GameSimulator`, `TradeHelper`, `DraftGenerator`, `ScheduleGenerator`, `PlayoffsGenerator`, `QuickNewsGenerator`, `AchievementService`, `AdvancedStatsHelper`, `FogOfWarHelper`, `HallOfFameHelper`, `MatchupPreview`, `ObjectiveHelper`). | Put new rules in static helpers; keep controllers thin. |
| **All 41 controllers inherit `UIScreenController`** base (`Scripts/Core/UIScreenController.cs`): full-screen root → `CacheReferences()` → `LoadData()` → `RegisterCallbacks()` → `Refresh()`, plus chrome (Header/Sidebar), nav wiring, cursors and the config modal. 12 controllers override `RegisterCallbacks()` **without** `base` (boot/menu/slot screens). | Inherit the base for new screens; override only what differs. |
| **UI built procedurally** (no `ListView`): `VisualElement` rows via `Clear()`+`Add()`. | Match this pattern for consistency. |
| **Simulation is non-deterministic** (`UnityEngine.Random` on main thread; `System.Random` thread-static `_aiRng`/`Rng` for background work). | Don't expect reproducible results when testing. |
| **Background work:** heavy batches run off-thread via `RunInBackground`/`RunInBackgroundAsync` (WAL + `AsyncLocal` ambient connection). Match sim stays on main thread (intentional). | DB helpers work on whatever connection is ambient; don't touch `_db` directly off-thread. |

## 3. Project organization

```
Assets/_TacticalFive/
  Scripts/Core/     navigation, audio, cursor, base UIScreenController, generators, trade rules, enums, achievements, stats helpers
  Scripts/Data/     DatabaseManager (9 partials), GameSaveManager, ~45 table models, seeders, Constants, DatabaseRows
  Scripts/UI/       41 controllers + CustomSlider
  Scripts/(root)    GameSimulator, GameResultCache, QuickNewsGenerator, PlayerPhotoHelper
  Scenes/           MainMenu.unity
  UI/Resources/     PanelSettings, theme, Header/Sidebar UXML
  UI/Screens/       per-screen UXML+USS
  UI/Styles/        GlobalVariables / Typography / Utilities (.uss)
  Art/Resources/    Audios, Flags, Icons, PlayerPhotos, Teams/Logos & Jerseys, Patrocinadores,
                    Televisiones, Arenas
Assets/Plugins/SQLite/   sqlite3 native binaries (Linux + Windows)
Docs/  .agent/            ← this knowledge base (keep it in sync!)
```

## 4. Fastest path to understanding the code

1. `Docs/PROJECT_OVERVIEW.md` — what/why.
2. `Docs/ARCHITECTURE.md` — modules, singletons, init flow, dependency map.
3. `Docs/GAMEPLAY.md` — every mechanic with formulas.
4. `Docs/UI_TOOLKIT.md` — screens, navigation tree, controller pattern.
5. `Docs/SYSTEMS.md` + `Docs/SAVE_SYSTEM.md` + `Docs/DATA_MODEL.md` — the data layer (this is where most work happens).
6. Source files to open **first**: `ScreenManager.cs`, `DatabaseManager.cs` (huge — use its public API list from `SYSTEMS.md`/`DATA_MODEL.md`), `GameSimulator.cs`, `DashboardController.cs` (the hub), `RosterController.cs`, `MarketController.cs`, `TradeHelper.cs`.

## 5. Conventions you must respect

- Spanish game strings/comments; English structural code is fine (that's the current mix).
- No namespaces; global classes.
- All DB code in `DatabaseManager`; all UI wiring in the controller's `OnEnable`.
- `UnityEngine.Random` for game randomness on the main thread; `System.Random` (thread-static) for background/threaded work.
- AI GM decisions are strategy-aware: `TeamStrategy { Rebuild, Balanced, Contend }` computed per team each trade cycle (`DashboardController.GetTeamStrategy`/`BuildTeamStrategyCache`), drives cooldowns, densities, `TryFindAITrade`, `TrySellVeteran`, `PickTradeTarget`/`BuildOfferPackage` and star FA signings.
- Money: `$"{value:N0}"`; dates as `"yyyy-MM-dd"` strings.
- Logos via `Resources.LoadAll<Sprite>("Teams/Logos/{size}x{size}")` → dictionary.
- Modals: overlay+box elements, `DisplayStyle.Flex/None`.
- Messages to the player via `DatabaseManager.AddMessage`.

## 6. How to add a new UI screen (checklist)

1. `UI/Screens/MyScreen/MyScreen.uxml` (+ `.uss`; reuse `GlobalVariables`/`Typography`/`Utilities`).
2. Add GameObject `MyScreenDocument` in `MainMenu.unity`: `UIDocument` (PanelSettings = `TacticalFivePanelSettings`, sourceAsset = UXML) + `FullScreenUI` + `MyScreenController`.
3. `GameEnums.cs`: add `GameScreen.MyScreen`.
4. `ScreenManager`: serialized field + `case` in `GoTo`.
5. Controller: **`MyScreenController : UIScreenController`** + `Refresh()`; the base provides full-screen, chrome, nav and config modal.
6. Navigate with `ScreenManager.Instance.GoTo(GameScreen.MyScreen)`; add Sidebar entry if in nav.

## 7. How to add a new mechanic

1. Model class (`[Table]`) in `Scripts/Data/` → `CreateTables()` entry.
2. CRUD methods in `DatabaseManager` (in the right partial).
3. Migration: column-based (`PRAGMA table_info` + `ALTER TABLE ADD COLUMN`) or named data migration registered in `schema_migrations` (`IsMigrationApplied`/`MarkMigrationApplied`).
4. Static helper for rules (`TradeHelper` style).
5. Hook into `ProcessGameDayRoutine` / `StartNewSeason` / a controller action.
6. Player feedback via `MessageData` + `Refresh()`.

## 8. How the save system works (must-know)

- Slot = `persistentDataPath/TacticalFive/saves/save_{n}.db`; metadata `saves.json`; seed `template.db` (15 static tables cloned).
- `DatabaseManager.InitSaveSlot(n)` → tables → migrations (`schema_migrations` + `PRAGMA user_version=2`) → clone template or seed.
- No autosave timer: every mutation is committed synchronously at once.
- New fields **require a migration**; default values matter for old saves.
- Rookie photos: `persistentDataPath/PlayerPhotos/{slot}/{id}.png` (deleted with the save).

## 9. Critical dependencies / invariants

- `overall` is intended to equal the mean of 11 attributes capped by `potential`, but current code uses integer division and mentoring does not recalculate it. Preserve the intended invariant when touching rating mutations and verify `PlayerData.GetCalculatedAverage()` before changing formulas.
- Cap/apron constants live in `TradeHelper.cs` (2025-26); `league_settings` copies them; `StartNewSeason` raises them +5%.
- `GameResultCache.Clear()` at the start of each simulated day — don't forget.
- Player salary model: `players.salary` annual; `contract_years`; renewal/FA offers mature after 7 days.
- `first_apron_hard_capped` blocks any transaction above 1st apron after using NT-MLE.
- The normal daily pipeline intends a pre-game batch plus a one-frame sim+bookkeeping transaction. `FastSimRoutine` currently yields while its transaction is open, so do not claim FastSim is fully atomic; do not add more yields inside the transaction.

## 10. Patterns to AVOID

- Overriding `RegisterCallbacks()` without `base` in screens that need the shared chrome/config modal.
- `GetComponent`/`FindObjectOfType` in loops; repeated `Resources.LoadAll` per enable.
- Adding a second source of truth for cap constants.
- Direct `gameObject.SetActive` navigation from controllers.
- `string.GetHashCode` for seeds (unstable across platforms).
- Adding a second header/sidebar population or a second full-screen setup (the base already does it; `HeaderController.Attach`/`SidebarController.Attach` are idempotent).

## 11. Known traps (from `TODO_TECHNICAL_DEBT.md`)

- ~~Duplicate `CursorManager`~~ — **resolved** (single instance on its own GO; `ScreenManager`/`DatabaseManager` persist with their own `DontDestroyOnLoad`).
- ~~`GameScreen.Settings` dead~~ — **removed**; settings live in the per-screen config modal (`UIScreenController.InitConfigModal`).
- ~~`SQLiteAsync`~~ — **deleted**; async is internal (`RunInBackground`/`RunInBackgroundAsync`).
- **`PreseasonGameData`** (`preseason_games`) is **dead code** — table never created, never used. Don't reference it.
- **`UIScreenController.LoadSidebarIcons` is dead** — the real icons load in `SidebarController.LoadIcons`.
- `PLAN.md` at repo root describes features; nearly all are implemented — verify against code before trusting it.

## 12. Keeping this knowledge base in sync

- Any change to schema, navigation, economy, or sim must update the matching doc (`DATA_MODEL.md`, `SCENES.md`, `UI_TOOLKIT.md`, `GAMEPLAY.md`, `MEMORY.md`).
- Update `MEMORY.md` with decisions and "never touch" rules.
- Update `TODO_TECHNICAL_DEBT.md` when you fix items (remove or mark done).
