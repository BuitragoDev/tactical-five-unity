# TODO_TECHNICAL_DEBT — Tactical Five

> Prioritized list of bugs, refactors, duplicates, technical debt, risks, and improvements, each with impact and reference. **[F]** confirmed, **[D]** deduction, **[H]** hypothesis.
> Priorities: **P0** = can break the app / data loss; **P1** = significant maintenance or correctness burden; **P2** = moderate; **P3** = nice-to-have.
> Status markers: **DONE** / **PARTIAL** (progress + what remains) / **OPEN**.

---

## P0 — Critical

_(none open — see resolved B1 below)_

---

## P1 — High

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
- **Detail [F]:** `StartNewSeason` and the daily rollover batch are now transactional; schedule/playoff/seed saves already were.
- **Remaining:** most single-mutation writes and mid-day sequences (game → stats → records) are still untransactional. A crash mid-flow can still leave partial state (game played, no stats).
- **Fix:** wrap the game-day pipeline and offer-processing in transactions.

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