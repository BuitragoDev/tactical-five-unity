# TODO_TECHNICAL_DEBT — Tactical Five

> Prioritized list of bugs, refactors, duplicates, technical debt, risks, and improvements, each with impact and reference. **[F]** confirmed, **[D]** deduction, **[H]** hypothesis.
> Priorities: **P0** = can break the app / data loss; **P1** = significant maintenance or correctness burden; **P2** = moderate; **P3** = nice-to-have.

---

## P0 — Critical

### B1. Duplicate `CursorManager` can destroy the `ScreenManager` GameObject
- **Type:** bug / fragility.
- **Detail [F]:** `MainMenu.unity` has two `CursorManager` components — one in the root `CursorManager` GO and one inside the `ScreenManager` GO (with `ScreenManager` and `DatabaseManager`). The singleton guard in `Awake` does `if (Instance != null && Instance != this) { Destroy(gameObject); return; }`. If the instance inside the `ScreenManager` GO loses the race, `Destroy(gameObject)` destroys the whole `ScreenManager` GO (navigation + DB gateway gone). Order is undefined.
- **Impact:** game-breaking at boot, intermittent.
- **Fix:** remove one instance; move `DontDestroyOnLoad` responsibility explicitly onto `ScreenManager`/`DatabaseManager` (they currently rely on the co-located `CursorManager`).

### B2. `OverallMigration_{slot}` PlayerPrefs flag is machine-global and never cleared on slot deletion
- **Type:** data correctness (low severity).
- **Detail [F]:** migration flags are `PlayerPrefs` keyed by slot number; `DeleteSave` doesn't remove them. Reusing a slot number after delete skips the overall recalculation. Harmless today (new data is correct) but a latent trap.
- **Impact:** low; flag as "needs care" if migrations evolve.

---

## P1 — High

### B3. `DatabaseManager` is a 5,600-line monolith
- **Type:** refactor.
- **Detail [F]:** all persistence, seeding, migrations, awards, records, and chemistry logic live in one class.
- **Impact:** hard to review, test, extend.
- **Fix:** split into `TeamRepository`, `PlayerRepository`, `GameRepository`, `FinanceRepository`, `SeasonFlow`, etc., keeping `DatabaseManager` as facade.

### B4. Config modal + confirm dialogs duplicated across ~30 controllers
- **Type:** duplication.
- **Detail [F]:** the settings modal (3 volume sliders + quality buttons) and "volver al menú / salir" confirm dialogs exist in each screen's UXML/USS and each controller (`ArenaController`, `DashboardController`, `MainMenuController`, `RosterController`, …).
- **Impact:** any UI change to settings touches dozens of files; drift risk.
- **Fix:** single `ConfigModalController` + one shared UXML/USS; controllers attach it.

### B5. No UI base controller
- **Type:** refactor.
- **Detail [F]:** 38 controllers each duplicate the same `OnEnable` pipeline, full-screen styling, logo dictionaries, and `_manager/_myTeam/_season` loading.
- **Impact:** ~60% boilerplate duplication.
- **Fix:** `UIScreenController` base with template methods (`CacheReferences`, `LoadData`, `RegisterCallbacks`, `Refresh`).

### B6. `GetTopPlayersByStat` was a stub
- **Type:** ~~incomplete feature~~ **done.**
- **Detail [F]:** `DatabaseManager.GetTopPlayersByStat` previously returned the manager's roster sorted by overall. **Fixed** — now aggregates `player_game_stats` in SQL (regular-season games only), see `DatabaseManager.Players.cs:64`. `StatsController.BuildSeasonStats` also moved to a single SQL aggregate (`GetSeasonPlayerStatsAggregates`).

### B7. `GameScreen.Settings` dead + `SettingsController` orphaned
- **Type:** dead code / incomplete wiring.
- **Detail [F]:** no `case` in `ScreenManager.GoTo`, no `settingsDocument` in scene; `Settings.uxml`/`SettingsController` unused.
- **Impact:** dead code; confusion about where settings live (they live in the per-screen config modal).
- **Fix:** either remove or wire it and centralize settings there.

### B8. All DB work is synchronous on main thread; heavy batches block UI
- **Type:** performance — **mostly resolved.**
- **Detail [F]:** the bundled `SQLite.cs` is synchronous-only (no `SQLiteAsyncConnection`). **Now** heavy batches run in `Task.Run` with a dedicated `SQLiteConnection` + WAL. `DatabaseManager.RunInBackground` (pre-lote, `33f4e12`) and `RunInBackgroundAsync` (`5bcca3b`, `71775bf`) set an `AsyncLocal` ambient connection so all DB helpers write on the background connection while the coroutine waits. Off main thread: daily injury/physical batch, `StartNewSeason`, AI transfers + star-FA signings (`_aiRng`, `System.Random` thread-safe).
- **Closed (2026-08):** match-day simulation (`GameSimulator.SimulateGame`) is intentionally left on the main thread — measured fast and stable; B8 is closed with the batches already moved. Draft generation also remains on main thread; revisit only if targeted stalls appear.
- **Impact:** stutter removed for season start and pre-game batch.
- **Fix (rest):** none required.

### B9. No transactions around most multi-write operations
- **Type:** data integrity.
- **Detail [F]:** only schedule/playoff/seed saves wrap writes in transactions. `StartNewSeason`, game-day sims, offer processing do many independent writes.
- **Impact:** crash mid-flow leaves partial state (game played, no stats; aging applied but caps not raised).
- **Fix:** wrap day/session flows in transactions.

### B11. No schema versioning
- **Type:** maintainability.
- **Detail [F]:** migrations rely on column presence; two one-time migrations keyed in `PlayerPrefs`.
- **Impact:** hard to reason about future breaking migrations; slot deletion interactions (B2).
- **Fix:** adopt `PRAGMA user_version` + ordered migration list.

### B12. Stringly-typed state everywhere
- **Type:** robustness.
- **Detail [F]:** `phase` ("regular"/"playin"/...), `game_type`, positions, staff positions ("PABELLON"), `trade_type` as raw strings; typos produce silent failures.
- **Impact:** runtime bugs hard to catch.
- **Fix:** enums + helper conversions (like `PositionCodes`).

---

## P2 — Medium

### B13. Repeated `Resources.LoadAll<Sprite>` logo dictionaries on every `OnEnable`
- **Detail [F]:** every controller rebuilds logo dictionaries.
- **Impact:** minor load; allocation churn.
- **Fix:** static lazy cache.

### B14. Heavy LINQ aggregations in C# that could be SQL
- **Detail [F]:** e.g., league leaders computed via `GroupBy` over all `player_game_stats` in memory; standings computed per call.
- **Impact:** slower with long careers.
- **Fix:** move to SQL aggregates.

### B15. Reflection in `CompleteTrainingAndApply`
- **Detail [F]:** attribute +2 via `typeof(PlayerData).GetProperty`.
- **Impact:** fragile (renames break silently); minor perf.
- **Fix:** switch on attribute name or dictionary.

### B16. `league_settings.apron/repeater_apron` vs `TradeHelper.FIRST/SECOND_APRON` duplication
- **Detail [F]:** two sources of truth; UI mostly uses `TradeHelper`.
- **Impact:** drift risk when constants change.
- **Fix:** single source; seed DB from it.

### B17. Orphan `Assets/_TacticalFive/Data/Database.meta`
- **Detail [F]:** folder meta without folder.
- **Impact:** noise; potential confusion.
- **Fix:** delete or recreate folder.

### B18. Error handling is `Debug.Log`-heavy with try/catch only in a few places
- **Detail [F]:** most DB calls unguarded; `catch (Exception)` used only in payroll/message creation.
- **Impact:** crashes on corrupt DB.
- **Fix:** central exception wrapper returning fallbacks.

### B19. `PlayerPrefs` used for migration flags (see B2) and settings without a settings DTO
- **Detail [F]:** keys `TF_Audio_*`, `TF_Graphics_Quality`.
- **Impact:** no central registry; magic strings.
- **Fix:** `GameSettings` static wrapper.

### B20. `GameMode.ProManager` has limited behavioral difference
- **Detail [F]:** ProManager selects only the worst teams (`SelectTeamController` `GetWorstTeams(5)`), limits new-season offers to current team + 3 random from bottom-10, and shows a restrictions modal (`MainMenuController.OpenProModal`). All announced harder rules are now **implemented**: objective-based season-end firing (`ShowObjectiveFiredModal`), easier budget firing (`CheckBudgetWarning` threshold 2), **no NT-MLE** on FA offers (`GetMaxOfferBreakdown`/`CalculateMaxOfferSalary` `proManagerOnly` forces Taxpayer MLE; `MarketController.UpdateFAMaxInfo`/`UpdateFAWarning`/`SendFAOffer`; `DashboardController.ProcessMaturedOffers`) and no NT-MLE hard-cap activation. Objective/rank logic centralized in `ObjectiveHelper`. **B20 closed / done.**
- **Impact:** resolved.
- **Fix:** none remaining. Verify balance of the Taxpayer-MLE offer cap in a full season.

---

## P3 — Low / Nice-to-have

- **B21.** No namespaces / Spanish-English mix in code — acceptable, but consider per-area namespaces.
- **B22.** Comments only in Spanish — fine for current team; document if external contributors join.
- **B23.** Fonts referenced via `project://database/Assets/...` (editor-absolute) in USS — works in editor builds; verify in built players.
- **B24.** `_Recovery` scenes leftover in `Assets/_Recovery` (gitignored) — clean up.
- **B25.** No unit/integration tests (only `com.unity.test-framework` package present, no test assemblies). Add tests for `TradeHelper`, `GameSimulator`, `DatabaseManager` migrations.
- **B26.** Emoji as icons in UXML (🏟👑💎🛒) — platform font-dependent; consider sprites.
- **B27.** `PLAN.md` (in repo root, currently uncommitted) documents an improvement plan; some parts already implemented (draft picks, hard cap, luxury tax, buyout). **Sync the plan with the code or delete it.**
- **B28.** No changelog or versioning policy for `v0.9.0 Beta`.

---

## Suggested next actions (ordered)

1. Fix **B1** (remove duplicate CursorManager; explicit `DontDestroyOnLoad`).
2. Introduce UI base controller (**B5**) and shared config modal (**B4**) — highest maintenance win.
3. Wire/remove **B7** (Settings dead code).
4. Add schema versioning (**B11**) and transaction wrappers (**B9**) before any new major feature.
5. Decide on **B20** (ProManager) and **B27** (PLAN.md sync).
6. Add tests (**B25**) for the core static helpers.
