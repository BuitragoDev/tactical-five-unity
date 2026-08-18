# TODO_TECHNICAL_DEBT — Tactical Five

> Prioritized list of bugs, refactors, duplicates, technical debt, risks, and improvements, each with impact and reference. **[F]** confirmed, **[D]** deduction, **[H]** hypothesis.
> Priorities: **P0** = can break the app / data loss; **P1** = significant maintenance or correctness burden; **P2** = moderate; **P3** = nice-to-have.
> Status markers: **DONE** / **PARTIAL** (progress + what remains) / **OPEN**.

---

## P0 — Critical

_(none open — see resolved B1 below)_

---

## P1 — High

### B44. Primera apertura del Editor sin directorio de datos — **DONE**
- **Type:** first-run persistence bug.
- **Detail [F]:** `EditorController.LoadData` called `DatabaseManager.EnsureTemplateDb`, which opened `Application.persistentDataPath/TacticalFive/template.db` without creating its parent directory. On a fresh profile this produced SQLite `CannotOpen`; creating a Manager/ProManager slot first happened to create the directory and masked the bug.
- **Fix [F]:** `DatabaseManager.EnsureTemplateDirectory` now runs before template creation, background rebuild and template-session opening. `EditorController.LoadData` catches I/O/database errors, logs them and displays an error toast after the UI is initialized instead of aborting `UIScreenController.OnEnable`.
- **References:** `DatabaseManager.cs` (`EnsureTemplateDb`, `BuildTemplateDatabaseInBackground`, `InitTemplateSession`), `EditorController.cs` (`LoadData`, `OnEnable`).

### B45. Estado de FastSim residual al iniciar una nueva partida — **DONE**
- **Type:** stale transient state / coroutine lifecycle.
- **Detail [F]:** `GameResultCache.FastSimTargetDate` and active Dashboard coroutines could survive an interrupted fast simulation in the same Unity session, causing a new Dashboard to show `DETENER SIMULACIÓN` or resume stale work.
- **Fix [F]:** new Manager/ProManager flows clear transient result state; `DashboardController.OnDisable` stops coroutines and resets fast-sim flags and target state.
- **References:** `MainMenuController.cs` (`OnManagerClicked`, `ConfirmProManager`), `DashboardController.cs` (`OnEnable`, `OnDisable`).

### B46. Premio Jugador Más Mejorado vacío tras el primer año — **DONE**
- **Type:** award calculation / historical-stat source.
- **Detail [F]:** `GetMostImprovedPlayer` only queried `player_game_stats` for both seasons, while `StartNewSeason` archived the previous season in `player_season_stats` and deleted the raw game rows. The method also assumed `seasonId - 1`, and `season_records.most_improved_id` was never written.
- **Fix [F]:** the previous real season is resolved by manager; ratings load from raw stats or archived per-season aggregates; the winner is upserted into `season_records` during `SaveSeasonEndRecords`.
- **References:** `DatabaseManager.Records.cs` (`GetMostImprovedPlayer`, `GetSeasonPlayerRatings`, `SaveSeasonEndRecords`), `PlayerAwardsController.cs`.

### B38. `FastSim` yields inside the day transaction — **OPEN**
- **Type:** data integrity / coroutine lifecycle.
- **Detail [F]:** `DashboardController.FastSimRoutine` / `ProcessGameDayRoutine(fastSim:true)` contains `yield return` between `BeginTransaction` and `Commit` (`DashboardController.cs`, around lines 969, 999, 1019, 1169).
- **Impact:** the connection remains in an open transaction across frames; exceptions, navigation or concurrent DB work can leave locks or partial state. This contradicts the intended atomic day pipeline.
- **Fix:** keep simulation/bookkeeping transaction work synchronous within one frame, or split into precomputed data plus a short commit transaction with explicit cancellation/rollback tests.

### B39. Template build replaces shared `_dbField` from a worker thread — **OPEN**
- **Type:** race condition / persistence.
- **Detail [F]:** `BuildTemplateDatabaseInBackground` temporarily assigns and closes the shared `_dbField` while `EditorController` invokes it from a background flow (`DatabaseManager.cs:163-175`, `EditorController.cs:298-313`). The lock does not protect normal UI queries.
- **Impact:** a simultaneous query may use the temporary connection or one that is being closed; template and active-save operations are not isolated.
- **Fix:** use a local connection exclusively for template generation and publish/swap it only on the main thread after completion; block template editing while a save slot is active.

### B40. `overall` derived value is stale/inconsistent — **OPEN**
- **Type:** gameplay correctness / data invariant.
- **Detail [F]:** `PlayerData.GetCalculatedAverage()` uses integer division; `DatabaseManager.Records.ApplyMentoring()` changes attributes without recalculating `overall`.
- **Impact:** sorting, AI evaluation and UI ratings can disagree with the attribute grid by one or more points after mentoring.
- **Fix:** centralize `RecalculateOverall()` and call it after every attribute mutation; add EditMode tests for truncation/rounding and potential caps.

### B41. Rookies skipped by season progression — **OPEN**
- **Type:** gameplay correctness.
- **Detail [F]:** `StartNewSeason` continues before aging/contract progression for `is_rookie == 1` (`DatabaseManager.Records.cs:2632-2638`).
- **Impact:** rookie age, contract years and team tenure do not advance through the rollover path.
- **Fix:** separate “newly drafted this season” handling from normal next-season progression and add a two-season regression test.

### B42. Local advantage and injury rating use wrong population — **OPEN**
- **Type:** simulation correctness.
- **Detail [F]:** `DashboardController` passes `isMyHomeGame`; `GameSimulator` applies the local bonus only for that flag. Team rating uses player lists that include injured players even though active rotations exclude them.
- **Impact:** AI league games lack local advantage and injuries can affect rating in addition to removing rotation minutes.
- **Fix:** pass `isHomeGame = game.home_team_id == homeTeamId`, and derive rating from available players or explicitly document the injury penalty.

### B43. Luxury-tax expense sign disagrees with aggregators — **OPEN**
- **Type:** economy correctness.
- **Detail [F]:** `ProcessTeamLuxuryTax` persists `-monthlyTax`, while `GetTotalExpenses` sums expense types as-is.
- **Impact:** tax can reduce the calculated expense total and inflate displayed balance.
- **Fix:** standardize expense records as positive and subtract at the balance boundary, or normalize all consumers consistently.

### B3. `DatabaseManager` is a monolith — **PARTIAL (mostly done)**
- **Type:** refactor.
- **Detail [F]:** the main file is now 968 ln (down from ~5,600) split into **9 partial classes** by domain: `.Teams`, `.Players`, `.Staff`, `.Manager`, `.Games`, `.Seeding` (1354 ln), `.Records` (3586 ln), `.Achievements`. `SQLite.cs` (sqlite-net wrapper) is separate.
- **Remaining:** `DatabaseManager.Records.cs` (3586 ln) and `.Seeding.cs` (1354 ln) are still oversized; DTOs moved out to `DatabaseRows.cs`. Further split optional.
- **Fix:** split `.Records` into record-checking vs awards vs HOF vs retired-numbers partials.

### B4. Config modal duplication — **DONE**
- **Detail [F]:** the settings modal (3 volume sliders + quality buttons + sim-mode toggle + exit confirms) is now centralized in `UIScreenController.InitConfigModal`/`OpenConfigModal`/`CloseConfigModal` (base class). `CustomSlider` is the shared slider control.
- **Note:** the 12 controllers that override `RegisterCallbacks()` without `base` (Editor, EndSeason, GameResults, LoadGame, MainMenu, MatchDay, NewSeason, PlayerAwards, Preseason, Quintos, SeasonSummary, SelectTeam) don't get the config modal via the base — verify each still exposes settings (most are boot/menu/slot screens).

### B5. No UI base controller — **DONE**
- **Detail [F]:** `UIScreenController` base exists (`Scripts/Core/UIScreenController.cs`, 575 ln); **all 41 controllers inherit it** (`UI_TOOLKIT.md §4`). Provides full-screen, chrome injection (Header/Sidebar), nav wiring, cursors, config modal, `RefreshHeader`.
- **Remaining quirk:** **12 controllers override `RegisterCallbacks()` without `base`** (see B4 note). `GameResultsController` re-implements nav/submenu/cursor wiring in its override referencing 3 non-existent sidebar elements (`NavRecords`/`NavSponsors`/`NavTV`) — null-safe but confusing.

### B7. `GameScreen.Settings` dead — **DONE**
- **Detail [F]:** `GameScreen.Settings` and `SettingsController` were **removed** (no enum value, no `settingsDocument`, no controller). Settings live in the per-screen config modal.

### B8. All DB work synchronous on main thread — **DONE (mostly)**
- **Detail [F]:** `SQLiteAsync.cs` was **deleted**; async is internal now: `DatabaseManager.RunInBackground`/`RunInBackgroundAsync` (`_ambientDb` AsyncLocal connection + WAL + `Task.Run`) run the daily injury/physical batch, `StartNewSeason`, and AI transfers/star-FA signings off the main thread (`_aiRng` `System.Random`, thread-safe).
- **Intentional on main thread:** match simulation (`GameSimulator`) and draft generation — measured fast/stable; revisit only if targeted stalls appear.
- **Fix (rest):** none required.

### B9. Transactions around multi-write operations — **PARTIAL**
- **Detail [F]:** `StartNewSeason` and parts of the daily rollover use transactions; schedule/playoff/seed saves already were. The normal game-day path intends one atomic block, but `FastSimRoutine` yields while that block is open (B38).
- **Remaining:** most single-mutation writes and some mid-day sequences (game → stats → records) are still untransactional. A crash mid-flow can still leave partial state (game played, no stats).
- **Fix:** remove yields from the transaction boundary and wrap remaining multi-write offer/game flows with explicit rollback tests.

### B11. No schema versioning — **DONE**
- **Detail [F]:** `schema_migrations` table (`name` PK, `applied_at`) + `PRAGMA user_version = 2` (`SCHEMA_VERSION = 2`). Data migrations are named and stored **in the DB** (per-slot, survive slot deletion correctly). Column migrations keep the `PRAGMA table_info` pattern.

### B12. Stringly-typed state everywhere — **OPEN**
- **Detail [F]:** `phase` ("regular"/"playin"/...), `game_type`, positions, staff positions ("PABELLON"), `trade_type` as raw strings; typos produce silent failures.
- **Impact:** runtime bugs hard to catch.
- **Fix:** enums + helper conversions (like `PositionCodes`). New code should already follow this.

---

## P2 — Medium

### B13. Repeated `Resources.LoadAll<Sprite>` logo dictionaries on every `OnEnable`
- **Detail [F]:** every controller rebuilds logo dictionaries.
- **Impact:** minor load; allocation churn.
- **Fix:** static lazy cache.

### B14. Heavy LINQ aggregations in C# that could be SQL
- **Detail [F]:** e.g., league leaders/standings computed via in-memory `GroupBy` in places.
- **Impact:** slower with long careers.
- **Fix:** move to SQL aggregates (`GetSeasonPlayerStatsAggregates` already does for one path).

### B15. Reflection in `CompleteTrainingAndApply`
- **Detail [F]:** attribute +2 via `typeof(PlayerData).GetProperty`.
- **Impact:** fragile (renames break silently); minor perf.
- **Fix:** switch on attribute name or dictionary.

### B16. `league_settings.apron/repeater_apron` vs `TradeHelper.FIRST/SECOND_APRON` duplication
- **Detail [F]:** two sources of truth; UI mostly uses `TradeHelper`. (`taxpayer_mid_level` was added to `league_settings` but is unused by most UI too.)
- **Impact:** drift risk when constants change.
- **Fix:** single source; seed DB from it.

### B17. Orphan `Assets/_TacticalFive/Data/Database.meta`
- **Detail [F]:** folder meta without folder.
- **Impact:** noise; potential confusion.
- **Fix:** delete or recreate folder.

### B18. Error handling is `Debug.Log`-heavy with try/catch only in a few places
- **Detail [F]:** most DB calls unguarded; `catch (Exception)` used in a few places only.
- **Impact:** crashes on corrupt DB.
- **Fix:** central exception wrapper returning fallbacks.

### B19. `PlayerPrefs` for settings without a settings DTO
- **Detail [F]:** keys `TF_Audio_*`, `TF_Graphics_Quality`, `TF_SimMode`, `TF_PbpSpeed`, `TF_LoadMgmt_Enabled`.
- **Impact:** no central registry; magic strings.
- **Fix:** `GameSettings` static wrapper.

### B20. ProManager difficulty — **DONE / closed**
- **Detail [F]:** ProManager selects only the worst teams (`GetWorstTeams(5)`), limits new-season offers, shows a restrictions modal. Harder rules implemented: objective-based firing (`ShowObjectiveFiredModal`), easier budget firing (threshold 2), **no NT-MLE** (Taxpayer MLE only via `GetMaxOfferBreakdown`/`CalculateMaxOfferSalary`/`MarketController`), no NT-MLE hard-cap. `ObjectiveHelper` centralizes objective/rank logic.
- **Fix:** verify balance of the Taxpayer-MLE offer cap in a full season.

### B21. Dead `UIScreenController.LoadSidebarIcons` — **OPEN**
- **Detail [F]:** the base calls `LoadSidebarIcons()` before the sidebar is attached; the real icons are loaded by `SidebarController.LoadIcons` (`Resources.Load<Texture2D>($"Icons/{kv.Value}")`). Base method is dead.
- **Fix:** remove the base method + call.

### B22. `FullScreenUI.Awake` duplicates `UIScreenController.MakeFullscreen`
- **Detail [F]:** both force absolute full-screen on the root. Harmless but redundant.
- **Fix:** pick one (e.g. keep `FullScreenUI` for the 39 docs that carry it and drop `MakeFullscreen`).

### B23. Double header population
- **Detail [F]:** base `Refresh()` → `RefreshHeader()` plus `HeaderController.Attach` populate the same header blocks; both run per screen load.
- **Fix:** make `RefreshHeader` idempotent or call once.

---

## P3 — Low / Nice-to-have

- **B30.** No namespaces / Spanish-English mix in code — acceptable, but consider per-area namespaces.
- **B31.** Comments only in Spanish — fine for current team; document if external contributors join.
- **B32.** Fonts referenced via `project://database/Assets/...` (editor-absolute) in USS — works in editor builds; verify in built players.
- **B33.** `_Recovery` scenes leftover in `Assets/_Recovery` (gitignored) — clean up.
- **B34.** No unit/integration tests (only `com.unity.test-framework` package present, no test assemblies). Add tests for `TradeHelper`, `GameSimulator`, `DatabaseManager` migrations.
- **B35.** Emoji as icons in UXML (🏟👑💎🛒) — platform font-dependent; consider sprites.
- **B36.** `PLAN.md` (repo root) documents an improvement plan; several parts are now implemented (draft picks/protections, hard cap, luxury tax, buyout, TO/PO, load management, HOF, achievements). **Sync the plan with the code or delete it.**
- **B37.** No changelog or versioning policy for `v1.0.0 · Beta`.

### Newly-identified dead/orphan code [F]
- **`PreseasonGameData`** (`Data/PreseasonGameData.cs`, `[Table("preseason_games")]`) — class exists but **the table is never created and never used** (preseason uses `games.game_type="preseason"`). Delete.
- **`UI/Screens/LegalNotice/`** (`LegalNotice.uxml` + `.uss`) — orphaned; the legal modal is inline in `MainMenu.uxml` (`BtnLegal`). Delete or rewire.

---

## Suggested next actions (ordered)

1. **B9** — wrap the game-day pipeline in transactions (highest data-integrity value).
2. **B21/B22/B23** — small base-class cleanups (dead `LoadSidebarIcons`, redundant full-screen, double header) — low risk, reduces confusion.
3. **B3** — split `DatabaseManager.Records.cs` (3586 ln) further.
4. **B12** — introduce enums for `phase`/`game_type`/`trade_type` before the next feature that touches them.
5. **B4 note** — reconcile the 12 controllers overriding `RegisterCallbacks()` without `base` (either standardize or document intent).
6. **B36** — sync or delete `PLAN.md`.
7. **B34** — add tests for the core static helpers (`TradeHelper`, `GameSimulator`, migrations).
8. Clean up the dead code (PreseasonGameData, LegalNotice folder, orphan `.meta`).
