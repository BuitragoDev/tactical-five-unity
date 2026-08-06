# SCENES — Tactical Five

> **[F] There is exactly one scene in the build**: `Assets/_TacticalFive/Scenes/MainMenu.unity` (declared in `ProjectSettings/EditorBuildSettings.asset`; no other scene in the build list). The game is a single-scene app where every "screen" is a GameObject holding a `UIDocument`.

---

## 1. `MainMenu.unity`

### Objective
Host the entire game: boot/loading, menus, and all ~39 management screens as inert `UIDocument` GameObjects, plus the three persistent singletons.

### Root GameObjects (40)

| GameObject | Components | Active at boot |
|---|---|---|
| MainCamera | Camera + AudioListener | yes |
| ScreenManager | ScreenManager + **CursorManager** + DatabaseManager | yes |
| CursorManager | CursorManager (duplicate instance — see Risk) | yes |
| LoadingDocument | UIDocument + FullScreenUI + LoadingController | yes |
| MainMenuDocument | UIDocument + FullScreenUI + MainMenuController | no |
| EditorDocument | UIDocument + FullScreenUI + EditorController | no |
| LoadGameDocument | UIDocument + FullScreenUI + LoadGameController | no |
| SelectTeamDocument | UIDocument + FullScreenUI + SelectTeamController | no |
| PreseasonDocument | UIDocument + FullScreenUI + PreseasonController | no |
| DashboardDocument | UIDocument + FullScreenUI + DashboardController | no |
| RosterDocument | UIDocument + FullScreenUI + RosterController | no |
| TrayectoryDocument | UIDocument + FullScreenUI + TrajectoryController | no |
| QuintetoDocument | UIDocument + FullScreenUI + QuintetoController | no |
| TrainingDocument | UIDocument + FullScreenUI + TrainingController | no |
| EmployeesDocument | UIDocument + FullScreenUI + EmployeesController | no |
| InjuredDocument | UIDocument + FullScreenUI + InjuredController | no |
| CalendarDocument | UIDocument + FullScreenUI + CalendarController | no |
| StandingsDocument | UIDocument + FullScreenUI + StandingsController | no |
| PalmaresDocument | UIDocument + FullScreenUI + PalmaresController | no |
| ResultsDocument | UIDocument + FullScreenUI + ResultsController | no |
| PlayoffDocument | UIDocument + FullScreenUI + PlayoffsController | no |
| StatsDocument | UIDocument + FullScreenUI + StatsController | no |
| FinancesDocument | UIDocument + FullScreenUI + FinancesController | no |
| LoansDocument | UIDocument + FullScreenUI + LoansController | no |
| MatchDayDocument | UIDocument + FullScreenUI + MatchDayController | no |
| GameResultsDocument | UIDocument + FullScreenUI + GameResultsController | no |
| RecordsDocument | UIDocument + FullScreenUI + RecordsController | no |
| PremiosMensualesDocument | UIDocument + FullScreenUI + PremiosController | no |
| LogrosDocument | UIDocument + FullScreenUI + LogrosController | no |
| SponsorsDocument | UIDocument + FullScreenUI + SponsorsController | no |
| TVDocument | UIDocument + FullScreenUI + TVController | no |
| ArenaDocument | UIDocument + FullScreenUI + ArenaController | no |
| ManagerDocument | UIDocument + FullScreenUI + ManagerController | no |
| MarketDocument | UIDocument + FullScreenUI + MarketController | no |
| CarteraDocument | UIDocument + FullScreenUI + CarteraController | no |
| HistorialTraspasosDocument | UIDocument + FullScreenUI + HistorialController | no |
| MessagesDocument | UIDocument + FullScreenUI + MessagesController | no |
| SeasonSummaryDocument | UIDocument + FullScreenUI + SeasonSummaryController | no |
| PlayerAwardsDocument | UIDocument + FullScreenUI + PlayerAwardsController | no |
| EndSeasonDocument | UIDocument + FullScreenUI + EndSeasonController | no |
| NewSeasonDocument | UIDocument + FullScreenUI + NewSeasonController | no |

### Serialization notes
- `ScreenManager` has 40 serialized `UIDocument` fields; **`legalNoticeDocument` points to the same object as `mainMenuDocument`** (Legal Notice is a modal inside `MainMenu.uxml`).
- **`settingsDocument` does not exist** → `GameScreen.Settings` unreachable.
- Every document shares `TacticalFivePanelSettings.asset` (guid `84f0403e…`) and has its own `sourceAsset` (the screen UXML).

## 2. Scene lifecycle (in/out flow)

**Input flow:** boot → `ScreenManager.Awake` shows Loading → `LoadingController` (10 s / click / key) → `GoTo(MainMenu)` → user chooses mode → `GoTo(SelectTeam | LoadGame | Editor)`.

**Within a game:** `SelectTeam → Preseason → Dashboard → [all management screens] → EndSeason → Draft → NewSeason → Preseason`.

**Output flow:** from Dashboard via the menu confirmation → `GoTo(MainMenu)` (or `Application.Quit` with confirm dialog). There is no explicit "quit scene"; the app is a single scene until exit.

## 3. Recovery scenes (`Assets/_Recovery/`)

`0.unity`, `0 (1).unity`, `0 (2).unity` — crash-recovery leftovers, **not in the build**, gitignored. Do not rely on them.

## 4. Orphan assets

- `Assets/_TacticalFive/Data/Database.meta` — orphan folder `.meta` (no `Database` folder exists). Harmless; see `TODO_TECHNICAL_DEBT.md`.

## 5. Open questions

- Why does the scene keep the duplicate `CursorManager` (own GO + inside ScreenManager GO)? (Risk R1 — see `MEMORY.md`.)
- `GameScreen.Settings` and `SettingsController` — dead code or WIP?
