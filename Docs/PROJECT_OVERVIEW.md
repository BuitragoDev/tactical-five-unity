# PROJECT_OVERVIEW — Tactical Five

> **Purpose of this document:** entry point to the whole project. Read this first, then follow the reading order at the bottom.

**Version analyzed:** Unity `6000.3.15f1` (Unity 6), editor build with a single scene, in-app version shown as `v0.9.0 · Beta` (`MainMenu.uxml` footer).

**Product:** *Tactical Five* — a single-player NBA-management simulation game, entirely in Spanish, played with mouse/keyboard on desktop (target resolution 1920×1080). It is a "GM mode" (no playable basketball): the player manages a franchise season by season, simulating games and making roster/financial decisions.

---

## 1. What the game is

- You are the **manager** (`ManagerData`) of one of **30 NBA-like franchises** (`TeamData`) named after real NBA teams (division/conference/arena/capacity/owner/logo/jerseys are seeded real data).
- A season runs roughly from **October 22 to mid-April** plus Play-In/Playoffs, modeled as a sequence of **game days** (one `GameData` row per match, up to 15 matches/day).
- You do **not** control a player live: matches are **simulated possession-by-possession** (`GameSimulator`) with your starting lineup (`LineupData`), substitutions, chemistry, morale, fatigue and injuries.
- The strategic layer includes: roster building (trades, free agents, renewals), finances (budget, ticket price, subscriptions, sponsors, TV deals, loans, arena renovations, luxury tax), training, staff (employees/scouts/psychologist), morale/relationships, and a full league simulation (draft lottery, playoffs, awards, historical records).

## 2. Game modes

Defined in `GameEnums.cs` (`GameMode`): `None`, `Manager`, `ProManager`, `Editor`.

| Mode | Entry point | Meaning |
|---|---|---|
| `Manager` | `MainMenuController.OnManagerClicked` | Standard career; selects a team, plays seasons |
| `ProManager` | `MainMenuController.OnProManagerClicked` | Same flow; **no gameplay difference currently observed in code** — `SelectTeamController` uses `CurrentMode` only to label the screen (hypothesis: intended for a harder/more restricted mode, not yet differentiated) |
| `Editor` | `MainMenuController.OnEditorClicked` | Opens `GameScreen.Editor` → `EditorController`, which seeds the `template.db` used to bootstrap new save slots |
| `None` | — | Default |

## 3. The core loops

### 3.1 Season loop (macro)
```
Select team → Preseason (schedule generated) → Regular season (82 games/team)
  → Play-In → Playoffs → End of season (awards) → Draft → New Season → [loop]
```
Implemented by: `SelectTeamController`, `PreseasonController`, `DashboardController`, `EndSeasonController`, `NewSeasonController`, `StartNewSeason()` (`DatabaseManager.cs:4703`), `DraftGenerator`, `PlayoffsGenerator`, `ScheduleGenerator`.

### 3.2 Game-day loop (micro)
User clicks "avanzar día" in `DashboardController` → `ProcessGameDayRoutine()` (coroutine, `DashboardController.cs:704`):
1. Recover injuries, recover `fisico`.
2. Process scouts, training completions, renewals, AI transfers, star FA signings, psychologist morale.
3. Load today's games (`GetGamesByGameDay`), simulate each with `GameSimulator.SimulateGame`.
4. Save results, process finances, injuries, morale, fan confidence, relationships, chemistry.
5. Generate quick news; handle phase transitions (regular→playin→playoff→finished).
6. Process monthly payroll and subscription revenue; advance `current_date` by 1 day.

## 4. Technical architecture in one paragraph

- **One scene, zero prefabs, zero game ScriptableObjects.** All UI is **UI Toolkit** (`UIDocument` per screen, ~40 screens) instantiated in the scene `MainMenu.unity`; `ScreenManager` (singleton) shows/hides GameObjects to navigate. No `SceneManager.LoadScene` anywhere.
- **Persistence is SQLite** (bundled `sqlite-net` `SQLite.cs` + native plugin `Assets/Plugins/SQLite/{x86_64/SQLite3.dll, Linux/x86_64/libsqlite3.so}`) behind the `DatabaseManager` singleton (~5600 lines, ~40 tables). One `.db` file per save slot under `persistentDataPath/TacticalFive/`.
- **Simulation core** lives in static utility classes: `GameSimulator`, `DraftGenerator`, `ScheduleGenerator`, `PlayoffsGenerator`, `TradeHelper`, `QuickNewsGenerator`.
- **UI is procedural**: controllers are plain `MonoBehaviour`s; tables/rows are built by hand (no `ListView`); a header and sidebar are injected at runtime from `Resources/UI/Core/`.

## 5. Project structure

```
Assets/_TacticalFive/
  Scripts/
    Core/      ScreenManager, FullScreenUI, AudioManager, CursorManager,
               GameEnums, TradeHelper, DraftGenerator, PlayoffsGenerator, ScheduleGenerator
    Data/      DatabaseManager, GameSaveManager, SQLite(.Async), Constants,
               LeagueSettings, SaveSlotInfo + ~40 table model classes + seeders
    UI/        38 screen controllers + CustomSlider
    (root)     GameSimulator, GameResultCache, QuickNewsGenerator, PlayerPhotoHelper
  Scenes/      MainMenu.unity  (the only scene)
  UI/
    Resources/ TacticalFivePanelSettings.asset, TacticalFiveTheme.tss, UI/Core/{Header, Sidebar}
    Screens/   one folder per screen (UXML + USS)
    Styles/    GlobalVariables.uss, Typography.uss, Utilities.uss
  Art/Resources/ Audios, Flags, Icons, PlayerPhotos, Teams/{Logos,Jerseys}, Patrocinadores,
                 Televisiones, Arenas
  Data/        (empty — only an orphan .meta; see SCENES.md/TODO)
Assets/Plugins/SQLite/   native sqlite3 binaries
Assets/TextMesh Pro/     default TMP package content (fonts are TMP SDF)
Assets/UI Toolkit/       Unity default runtime theme
```

## 6. Key facts every developer must know

| Fact | Reference |
|---|---|
| Only one scene in build: `MainMenu.unity` | `ProjectSettings/EditorBuildSettings.asset` |
| Navigation = `ScreenManager.GoTo(GameScreen, GameMode)` toggling `UIDocument` GameObjects | `ScreenManager.cs` |
| All persistence through `DatabaseManager.Instance` (SQLite) | `DatabaseManager.cs` |
| Save slots = `save_{n}.db` + `saves.json`; template = `template.db` | `GameSaveManager.cs` |
| Salary cap / aprons constants (2025-26) | `TradeHelper.cs` |
| Season starts Oct 22; 82 games/team; All-Star break in February | `ScheduleGenerator.cs` |
| `overall` is always the mean of 11 attributes, capped by `potential` | `PlayerData.GetCalculatedAverage()`, migrations |
| No C# event bus — cross-controller communication is via DB, `GameResultCache` statics, `PlayerPrefs`, and `ScreenManager` static state | `EVENTS.md` |
| Audio/volumes/graphics persisted in `PlayerPrefs` keys `TF_Audio_*`, `TF_Graphics_Quality` | `AudioManager.cs` |

## 7. Critical systems (deep links)

1. **Database/persistence** → `SYSTEMS.md`, `DATA_MODEL.md`, `SAVE_SYSTEM.md`
2. **Simulation engine** → `GAMEPLAY.md` (§ match simulation), `SYSTEMS.md`
3. **Economy & salary cap** → `GAMEPLAY.md` (§ economy, contracts), `SYSTEMS.md` (trades/economy)
4. **UI Toolkit navigation** → `UI_TOOLKIT.md`
5. **Season cycle (draft/playoffs/schedule)** → `SYSTEMS.md`
6. **Save system** → `SAVE_SYSTEM.md`

## 8. State of development (observed)

- Very mature feature set (30+ screens, full season cycle, records/awards, finances, personnel, morale, injuries, draft, playoffs).
- Branding/labels: product "TacticalFive", company "BuitragoStudio", version `v0.9.0 Beta`.
- ~503 commits; last commit `50b1a86` (2026-07-29).
- `PLAN.md` (a plan for fixing free-agent offers/trades) is **partially implemented**: draft picks model, hard cap flag, luxury tax and buyout record types already exist; validation happens at maturation time (not at send) and sign-and-trade/buyout UI are not observed. Details in `MEMORY.md` and `TODO_TECHNICAL_DEBT.md`.
- Known structural debt: duplicate `CursorManager`, orphaned `SettingsController`, unused `SQLiteAsync.cs`, stub `GetTopPlayersByStat`. See `TODO_TECHNICAL_DEBT.md`.

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
