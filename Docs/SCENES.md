# SCENES — Tactical Five

> **[F] There is exactly one scene in the build**: `Assets/_TacticalFive/Scenes/MainMenu.unity` (declared in `ProjectSettings/EditorBuildSettings.asset`; no other scene in the build list). The game is a single-scene app where every "screen" is a GameObject holding a `UIDocument`.

---

## 1. `MainMenu.unity`

### Objective
Host the entire game: boot/loading, menus, and all 41 management screens as inert `UIDocument` GameObjects, plus the persistent singletons.

### Root GameObjects (44)

| GameObject | Components | Active at boot |
|---|---|---|
| MainCamera | Camera (ortho) + AudioListener | yes |
| ScreenManager | ScreenManager + DatabaseManager | yes |
| CursorManager | CursorManager (own GO, single instance) | yes |
| LoadingDocument | UIDocument + FullScreenUI + LoadingController | yes |
| 40 × `*Document` | UIDocument + FullScreenUI + controller | no |

**[F] 39 documents carry `FullScreenUI`; `SelectTeamDocument` and `PreseasonDocument` do not** (they render full-screen via UXML/controller logic only).

### Screen documents (41)

| Document GO | Controller |
|---|---|
| LoadingDocument | LoadingController |
| MainMenuDocument | MainMenuController |
| EditorDocument | EditorController |
| LoadGameDocument | LoadGameController |
| SelectTeamDocument | SelectTeamController |
| PreseasonDocument | PreseasonController |
| DashboardDocument | DashboardController |
| RosterDocument | RosterController |
| TrayectoryDocument | TrajectoryController |
| PlayerProfileDocument | PlayerProfileController |
| QuintetoDocument | QuintetoController |
| TrainingDocument | TrainingController |
| EmployeesDocument | EmployeesController |
| InjuredDocument | InjuredController |
| DorsalesDocument | DorsalesController |
| CalendarDocument | CalendarController |
| ResultsDocument | ResultsController |
| PlayoffDocument | PlayoffsController |
| StandingsDocument | StandingsController |
| StatsDocument | StatsController |
| RecordsDocument | RecordsController |
| PalmaresDocument | PalmaresController |
| PremiosMensualesDocument | PremiosController |
| LogrosDocument | LogrosController |
| MarketDocument | MarketController |
| CarteraDocument | CarteraController |
| HistorialTraspasosDocument | HistorialController |
| FinancesDocument | FinancesController |
| LoansDocument | LoansController |
| SponsorsDocument | SponsorsController |
| TVDocument | TVController |
| ArenaDocument | ArenaController |
| ManagerDocument | ManagerController |
| MessagesDocument | MessagesController |
| MatchDayDocument | MatchDayController |
| GameResultsDocument | GameResultsController |
| SeasonSummaryDocument | SeasonSummaryController |
| PlayerAwardsDocument | PlayerAwardsController |
| EndSeasonDocument | EndSeasonController |
| NewSeasonDocument | NewSeasonController |
| QuintosDocument | QuintosController |

### Serialization notes
- `ScreenManager` has **41 serialized `UIDocument` fields** (1:1 with the 41 `GameScreen` enum values; all have cases in `GoTo` — no dead enum values).
- **No `legalNoticeDocument` / `settingsDocument` fields** [F] — the Legal Notice is an inline modal in `MainMenu.uxml` (`BtnLegal`), and `GameScreen.Settings` no longer exists (removed).
- Every document shares `TacticalFivePanelSettings.asset` (guid `84f0403e…`) and has its own `sourceAsset` (the screen UXML).

## 2. Scene lifecycle (in/out flow)

**Input flow:** boot → `ScreenManager.Awake` shows Loading → `LoadingController` (10 s / click / key) → `GoTo(MainMenu)` → user chooses mode → `GoTo(SelectTeam | LoadGame | Editor)`.

**Within a game:** `SelectTeam → Preseason → Dashboard → [all management screens] → EndSeason → Draft → NewSeason → Preseason`.

**Output flow:** from Dashboard via the menu confirmation → `GoTo(MainMenu)` (or `Application.Quit` with confirm dialog). There is no explicit "quit scene"; the app is a single scene until exit.

## 3. Recovery scenes (`Assets/_Recovery/`)

`0.unity`, `0 (1).unity`, `0 (2).unity` — crash-recovery leftovers, **not in the build**, gitignored. Do not rely on them.

## 4. Orphan assets

- `Assets/_TacticalFive/UI/Screens/LegalNotice/` — `LegalNotice.uxml` is not instantiated and has no controller [F]. `LegalNotice.uss` is imported by MainMenu and other screen UXMLs, so only the UXML is orphaned; the USS is shared style content.
- `Assets/_TacticalFive/Data/Database.meta` — orphan folder `.meta` (no `Database` folder exists). Harmless; see `TODO_TECHNICAL_DEBT.md`.

## 5. Open questions

- `SelectTeamDocument`/`PreseasonDocument` lack `FullScreenUI` — intentional (they manage their own layout) or a leftover? [D] intentional: both are standalone flows outside the base-chrome pattern.
- `GameResultsController` re-implements the full nav in its override — leftover from pre-base-class code. (see `UI_TOOLKIT.md §9`.)
