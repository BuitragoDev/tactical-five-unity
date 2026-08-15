# PROJECT_OVERVIEW — Tactical Five

> **Purpose of this document:** entry point to the whole project. Read this first, then follow the reading order at the bottom.

**Version analyzed:** Unity `6000.3.15f1` (Unity 6), editor build with a single scene, in-app version shown as `v1.0.0 · Beta` (`MainMenu.uxml` footer, line ~102). Code analyzed at HEAD `1d88989` (merge de `crear-mejoras2`, 2026-08-11).

**Product:** *Tactical Five* — a single-player NBA-management simulation game, entirely in Spanish, played with mouse/keyboard on desktop (target resolution 1920×1080). It is a "GM mode" (no playable basketball): the player manages a franchise season by season, simulating games and making roster/financial decisions.

---

## 1. What the game is

- You are the **manager** (`ManagerData`) of one of **30 NBA-like franchises** (`TeamData`) named after real NBA teams (division/conference/arena/capacity/owner/logo/jerseys are seeded real data).
- A season runs roughly from **October 22 to mid-April** plus Play-In/Playoffs, modeled as a sequence of **game days** (one `GameData` row per match, up to 15 matches/day).
- You do **not** control a player live: matches are **simulated possession-by-possession** (`GameSimulator`) with your starting lineup (`LineupData`), substitutions, chemistry, morale, fatigue and injuries. The match can be shown live in a **play-by-play overlay** (marcador, reloj, barra de progreso, boxscore en vivo y velocidades x1/x3/x5/x10) or simulated directly (`Directa`), configurable via "Vista de Partido" en los ajustes (`TF_SimMode`).
- The strategic layer includes: roster building (trades, free agents, renewals, sign-and-trade), finances (budget, ticket price, subscriptions, sponsors, TV deals, loans, arena renovations, luxury tax, buyout), training, staff (employees/scouts/psychologist), morale/relationships/chemistry, and a full league simulation (draft lottery, playoffs, awards, historical records, Hall of Fame, retired numbers, GM achievements).

## 2. Game modes

Defined in `GameEnums.cs` (`GameMode`): `None`, `Manager`, `ProManager`, `Editor`.

| Mode | Entry point | Meaning |
|---|---|---|
| `Manager` | `MainMenuController.OnManagerClicked` | Standard career; selects a team, plays seasons |
| `ProManager` | `MainMenuController.OnProManagerClicked` | Harder mode; shows a restrictions modal (`OpenProModal`) before starting. All harder rules implemented: objective-based season-end firing, earlier budget firing (threshold 2 vs 3), no NT-MLE on FA (Taxpayer MLE only), worst-team selection, bottom-10 new-season offers, annual team change |
| `Editor` | `MainMenuController.OnEditorClicked` | Opens `GameScreen.Editor` → `EditorController`, which seeds the `template.db` used to bootstrap new save slots |
| `None` | — | Default |

> **[F] `GameMode.Editor` is dead** in current code: no code path sets it and nothing reads it. `GameMode.None` is used only as a default in `ScreenManager.GoTo`.

## 3. The core loops

### 3.1 Season loop (macro)
```
Select team → Preseason (schedule generated) → Regular season (82 games/team)
  → Play-In → Playoffs → End of season (awards, HOF, retired numbers) → Draft → New Season → [loop]
```
Implemented by: `SelectTeamController`, `PreseasonController`, `DashboardController`, `EndSeasonController`, `NewSeasonController`, `StartNewSeason()` (`DatabaseManager.Records.cs:2568`), `DraftGenerator`, `PlayoffsGenerator`, `ScheduleGenerator`.

### 3.2 Game-day loop (micro)
User clicks "avanzar día" in `DashboardController` → `ProcessGameDayRoutine()` (coroutine, `DashboardController.cs:752`):
1. **Pre-batch (background threads):** recover injuries/fatigue (`RunInBackground`, +8 `fisico`/day), process scouts+training+renovations, AI market (`ProcessAIMarket` via `RunInBackgroundAsync`). Modals interrupt here: load management (back-to-back rest), empty starters, injured starters.
2. Load today's games, simulate each with `GameSimulator.SimulateGame` inside one atomic main-thread transaction (single frame; `GameResultCache.Clear()` at day start).
3. Post-simulation: chemistry, quick news, achievements, phase transitions (regular→playin→playoff→finished), manager stats, monthly payroll + subscription revenue, season-end block (archive stats, awards, achievement `EvaluateSeasonEnd`), advance date.
4. Deadline-day modal (Feb 7), monthly awards (1st of Dec–Apr), budget/objective-fired checks, `SaveSlotInfo` refresh.

**Simulación rápida:** desde `CalendarController` el jugador elige una fecha y confirma; se navega al Dashboard, que ejecuta `FastSimRoutine` (`DashboardController.cs:1324`): hace un bucle de `ProcessGameDayRoutine(fastSim: true)` hasta alcanzar la fecha objetivo o el fin de temporada. En modo `fastSim` el spinner del header gira continuo y el botón **DETENER SIMULACIÓN** queda activo (cursor hand); la parada se aplica al terminar el día en curso. Quinteto incompleto/lesionados muestran el modal de quinteto (auto-fix reanuda con `_fastSimSkipPreBatch`; manual lleva a Quinteto; tope de 3 auto-fixes). **Pausa por ofertas:** al final de cada día la sim se detiene si hay ofertas maduradas (modal IR A QUINTETO / SEGUIR SIMULANDO) o traspasos entrantes (`ShowNextPendingTradeOffer`); el día ya está commiteado y no se duplica al reanudar. Al terminar (salvo DETENER/aceptar traspaso) se muestra un resumen simplificado "SIMULACIÓN COMPLETADA".

## 4. Technical architecture in one paragraph

- **One scene, zero prefabs, zero game ScriptableObjects.** All UI is **UI Toolkit** (`UIDocument` per screen, **41 screens**) instantiated in the scene `MainMenu.unity`; `ScreenManager` (singleton) shows/hides GameObjects to navigate. No `SceneManager.LoadScene` anywhere.
- **Persistence is SQLite** (bundled `sqlite-net` `SQLite.cs` + native plugin `Assets/Plugins/SQLite/{x86_64,Linux/x86_64}`) behind the `DatabaseManager` singleton (**split into 9 partial classes by domain**; main file ~968 lines). One `.db` file per save slot under `persistentDataPath/TacticalFive/`. **Heavy batches run off the main thread** via `RunInBackground`/`RunInBackgroundAsync` with WAL + a per-thread `AsyncLocal` "ambient" connection.
- **Simulation core** lives in static utility classes: `GameSimulator`, `DraftGenerator`, `ScheduleGenerator`, `PlayoffsGenerator`, `TradeHelper`, `QuickNewsGenerator`, `ObjectiveHelper`, `MatchupPreview`, `AdvancedStatsHelper`, `FogOfWarHelper`, `HallOfFameHelper`.
- **UI is procedural**: controllers extend the `UIScreenController` base class; tables/rows are built by hand (no `ListView`); header and sidebar are injected at runtime from `Resources/UI/Core/`.

## 5. Project structure

```
Assets/_TacticalFive/
  Scripts/
    Core/      ScreenManager, UIScreenController, FullScreenUI, AudioManager, CursorManager,
               GameEnums, TradeHelper, DraftGenerator, PlayoffsGenerator, ScheduleGenerator,
               AchievementCatalog, AchievementService, GmAchievementType
    Data/      DatabaseManager (partials ×9), GameSaveManager, SQLite, Constants,
               LeagueSettings, SaveSlotInfo + ~45 table model classes + seeders
    Stats/     AdvancedStatsHelper, FogOfWarHelper, HallOfFameHelper, MatchupPreview
    UI/        41 screen controllers + CustomSlider (all inherit UIScreenController in Core)
    (root)     GameSimulator, GameResultCache, QuickNewsGenerator, PlayerPhotoHelper, ObjectiveHelper
  Scenes/      MainMenu.unity  (the only scene)
  UI/
    Resources/ TacticalFivePanelSettings.asset, TacticalFiveTheme.tss, UI/Core/{Header, Sidebar}
    Screens/   41 folders (one per screen: UXML + USS) — note LegalNotice/ folder is orphaned CSS
    Styles/    GlobalVariables.uss, Typography.uss, Utilities.uss
  Art/Resources/ Audios, Flags, Icons, PlayerPhotos (602 + 100 defaults), Teams/{Logos,Jerseys},
                 Patrocinadores, Televisiones, Arenas
  Data/        (empty — only an orphan .meta; see SCENES.md/TODO)
Assets/Plugins/SQLite/   native sqlite3 binaries (x86_64 Windows + Linux)
Assets/TextMesh Pro/     default TMP package content (fonts are TMP SDF)
Assets/UI Toolkit/       Unity default runtime theme
Docs/  .agent/            this knowledge base (keep in sync)
```

## 6. Key facts every developer must know

| Fact | Reference |
|---|---|
| Only one scene in build: `MainMenu.unity` | `ProjectSettings/EditorBuildSettings.asset` |
| Navigation = `ScreenManager.GoTo(GameScreen, GameMode)` toggling `UIDocument` GameObjects; **41 screens, all wired** | `ScreenManager.cs:83-215` |
| All persistence through `DatabaseManager.Instance` (SQLite) | `DatabaseManager.cs` |
| Save slots = `save_{n}.db` + `saves.json`; template = `template.db`; **schema versioned** (`schema_migrations` + `PRAGMA user_version = 2`) | `GameSaveManager.cs`, `DatabaseManager.cs:286` |
| Salary cap / aprons constants (2025-26) in `TradeHelper.cs`; caps grow +5%/season | `TradeHelper.cs:7-14`, `StartNewSeason` |
| Season starts Oct 22; 82 games/team; All-Star break Feb 8–14 | `ScheduleGenerator.cs` |
| `overall` is always the mean of 11 attributes, capped by `potential` | `PlayerData.GetCalculatedAverage()`, migrations |
| No C# event bus — cross-controller communication via DB, `GameResultCache` statics, `PlayerPrefs`, and `ScreenManager` static state | `EVENTS.md` |
| Audio/volumes/graphics persisted in `PlayerPrefs` keys `TF_Audio_*`, `TF_Graphics_Quality`, `TF_SimMode`, `TF_PbpSpeed`, `TF_LoadMgmt_Enabled` | `AudioManager.cs`, `UIScreenController.cs`, `MatchDayController.cs` |
| All 41 screen controllers inherit `UIScreenController` (base: fullscreen, header/sidebar injection, nav, config modal, sim-mode toggle) | `UIScreenController.cs` |

## 7. Critical systems (deep links)

1. **Database/persistence** → `SYSTEMS.md` (§S1/S2), `DATA_MODEL.md`, `SAVE_SYSTEM.md`
2. **Simulation engine** → `GAMEPLAY.md` (§ match simulation), `SYSTEMS.md` (§S3)
3. **Economy & salary cap** → `GAMEPLAY.md` (§ economy, contracts), `SYSTEMS.md` (§S7/S8/S9)
4. **UI Toolkit navigation** → `UI_TOOLKIT.md`
5. **Season cycle (draft/playoffs/schedule)** → `SYSTEMS.md` (§S4–S6)
6. **Save system** → `SAVE_SYSTEM.md`

## 8. State of development (observed)

- Very mature feature set (41 screens, full season cycle, records/awards/HOF/retired numbers, finances, personnel, morale, injuries, draft, playoffs, GM achievements, play-by-play).
- Branding/labels: product "TacticalFive", company "BuitragoStudio", version `v1.0.0 Beta`.
- ~607 commits total; last merge to `main`: `1d88989` (merge de `crear-mejoras2`), containing all the features below.
- **Recent features landed since the previous doc snapshot (`50b1a86`):** play-by-play overlay, S&T of own FA (Bird rights), contract options TO/PO with re-sign, trade deadline event, AI GM strategy, GM achievements (28), PlayerProfile screen, advanced analytics (eFG%/TS%/PER), fog-of-war in ratings, async DB (background WAL threads), load management, trade block, Hall of Fame, retired numbers (Dorsales screen), season quintets (Quintos), matchup preview, protected picks + swaps, position-based athletic decline + mentoring, `UIScreenController` base, `DatabaseManager` partial split, `schema_migrations` versioning, atomic game-day transaction.
- **Resolved debt:** duplicate `CursorManager` fixed (single instance now), `SettingsController`/`SQLiteAsync.cs` deleted, dead `GameScreen.Settings` removed, `GetTopPlayersByStat` real SQL aggregation, B8 (sync DB) closed for heavy batches. See `TODO_TECHNICAL_DEBT.md`.

---

## Glossary note

Game-facing terms are Spanish. A mapping is in `.agent/GLOSSARY.md`.

## Recommended reading order

1. `Docs/PROJECT_OVERVIEW.md` (this file)
2. `Docs/ARCHITECTURE.md`
3. `Docs/GAMEPLAY.md`
4. `Docs/UI_TOOLKIT.md`
5. `Docs/SYSTEMS.md`
6. `Docs/SAVE_SYSTEM.md` + `Docs/DATA_MODEL.md`
7. `Docs/SCENES.md` + `Docs/EVENTS.md`
8. `.agent/SKILLS.md` (agent onboarding) then the remaining docs as needed.

## Open questions

- Whether `template.db` is meant to ship with the build or be regenerated at first run (Editor flow suggests dev-tool). [D]
- Whether `GameMode.Editor` (dead) will be revived or removed.
