# SKILLS — Agent Onboarding for Tactical Five

> This file is the **initial context** for any AI agent or developer working on this project. Read `Docs/PROJECT_OVERVIEW.md` and `Docs/ARCHITECTURE.md` for depth; this is the fast path to being productive.

---

## 1. What the game is (30 seconds)

A single-player **NBA management sim** ("Tactical Five", `v0.9.0 Beta`) in Unity 6 (6000.3.15f1), entirely in Spanish, desktop 1920×1080. You manage one of 30 NBA-like teams through endless seasons: rosters, trades, free agents, contracts/salary cap, finances (tickets, subscriptions, sponsors, TV, loans, arena), training, morale/chemistry/injuries, draft, playoffs, awards, records.

## 2. Architecture (essential facts)

| Fact | Consequence for you |
|---|---|
| **One scene** (`MainMenu.unity`); ~40 screens as `UIDocument` GameObjects; `ScreenManager.GoTo(GameScreen, mode)` toggles `SetActive`. | Never load scenes. Never navigate directly with `SetActive` — use `ScreenManager`. |
| **All persistence = SQLite** via `DatabaseManager.Instance` (bundled sqlite-net + native plugin). | Never open your own connection. All data access through `DatabaseManager`. |
| **No prefabs, no game ScriptableObjects.** | New views = UXML+USS; new content = seeders + SQLite tables. |
| **No event bus.** Communication = DB messages (`MessageData`), static state (`GameResultCache`, `ScreenManager.*`), `PlayerPrefs` (settings/migrations), UI callbacks. | Don't invent an event system; follow existing channels. |
| **Static utility classes** hold game logic (`GameSimulator`, `TradeHelper`, `DraftGenerator`, `ScheduleGenerator`, `PlayoffsGenerator`, `QuickNewsGenerator`). | Put new rules in static helpers; keep controllers thin. |
| **Controllers = plain MonoBehaviour**, no base class; `OnEnable` pipeline: full-screen root → `CacheReferences()` → `LoadData()` → `RegisterCallbacks()` → `Refresh()`. | Follow the same pipeline for any new screen. |
| **UI built procedurally** (no `ListView`): `VisualElement` rows via `Clear()`+`Add()`. | Match this pattern for consistency. |
| **Simulation is non-deterministic** (`UnityEngine.Random`, no seed). | Don't expect reproducible results when testing. |

## 3. Project organization

```
Assets/_TacticalFive/
  Scripts/Core/     navigation, audio, cursor, generators, trade rules, enums
  Scripts/Data/     DatabaseManager, GameSaveManager, ~40 table models, seeders, Constants
  Scripts/UI/       39 controllers + CustomSlider
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
- `UnityEngine.Random` for game randomness; seeded `System.Random` only in `GeneratePositionAttrs`.
- AI GM decisions are strategy-aware: `TeamStrategy { Rebuild, Balanced, Contend }` computed per team each trade cycle (`DashboardController.GetTeamStrategy`/`BuildTeamStrategyCache`), drives cooldowns, densities, `TryFindAITrade`, `TrySellVeteran`, `PickTradeTarget`/`BuildOfferPackage` and star FA signings.
- Money: `$"{value:N0}"`; dates as `"yyyy-MM-dd"` strings.
- Logos via `Resources.LoadAll<Sprite>("Teams/Logos/{size}x{size}")` → dictionary.
- Modals: overlay+box elements, `DisplayStyle.Flex/None`.
- Messages to the player via `DatabaseManager.AddMessage`.

## 6. How to add a new UI screen (checklist)

1. `UI/Screens/MyScreen/MyScreen.uxml` (+ `.uss`; reuse `Dashboard.uss`/`LegalNotice.uss` styles).
2. Add GameObject `MyScreenDocument` in `MainMenu.unity`: `UIDocument` (PanelSettings = `TacticalFivePanelSettings`, sourceAsset = UXML) + `FullScreenUI` + `MyScreenController`.
3. `GameEnums.cs`: add `GameScreen.MyScreen`.
4. `ScreenManager`: serialized field + `case` in `GoTo`.
5. Controller: `OnEnable` pipeline + `Refresh()`.
6. Navigate with `ScreenManager.Instance.GoTo(GameScreen.MyScreen)`; add Sidebar entry if in nav.

## 7. How to add a new mechanic

1. Model class (`[Table]`) in `Scripts/Data/` → `CreateTables()` entry.
2. CRUD methods in `DatabaseManager`.
3. Migration (`PRAGMA table_info` + `ALTER TABLE ADD COLUMN`) if extending an existing table.
4. Static helper for rules (`TradeHelper` style).
5. Hook into `ProcessGameDayRoutine` / `StartNewSeason` / a controller action.
6. Player feedback via `MessageData` + `Refresh()`.

## 8. How the save system works (must-know)

- Slot = `persistentDataPath/TacticalFive/saves/save_{n}.db`; metadata `saves.json`; seed `template.db`.
- `DatabaseManager.InitSaveSlot(n)` → tables → migrations → clone template or seed.
- No autosave timer: every mutation is committed synchronously at once.
- New fields **require a migration**; default values matter for old saves.
- Rookie photos: `persistentDataPath/PlayerPhotos/{slot}/{id}.png` (deleted with the save).

## 9. Critical dependencies / invariants

- `overall == round(mean(11 attributes))` capped by `potential` — recomputed in seed, training, progression, migration. Keep it true.
- Cap/apron constants live in `TradeHelper.cs` (2025-26); `league_settings` copies them; `StartNewSeason` raises them +5%.
- `GameResultCache.Clear()` at the start of each simulated day — don't forget.
- Player salary model: `players.salary` annual; `contract_years`; renewal/FA offers mature after 7 days.
- `first_apron_hard_capped` blocks any transaction above 1st apron after using NT-MLE.

## 10. Patterns to AVOID

- Copy-pasting the config modal / confirm dialogs (extract a shared controller — see TODO B4).
- `GetComponent`/`FindObjectOfType` in loops; repeated `Resources.LoadAll` per enable.
- Adding a second source of truth for cap constants.
- Direct `gameObject.SetActive` navigation from controllers.
- `string.GetHashCode` for seeds (unstable across platforms).

## 11. Known traps (from `TODO_TECHNICAL_DEBT.md`)

- **Duplicate `CursorManager`** can destroy the `ScreenManager` GO at boot (B1). Don't "fix" navigation until you understand this.
- `GameScreen.Settings` is dead; settings live in the per-screen config modal.
- `SQLiteAsync` unused; all DB is sync on main thread.
- `PLAN.md` at repo root describes features, some already implemented — verify against code before trusting it.

## 12. Keeping this knowledge base in sync

- Any change to schema, navigation, economy, or sim must update the matching doc (`DATA_MODEL.md`, `SCENES.md`, `UI_TOOLKIT.md`, `GAMEPLAY.md`, `MEMORY.md`).
- Update `MEMORY.md` with decisions and "never touch" rules.
- Update `TODO_TECHNICAL_DEBT.md` when you fix items (remove or mark done).
