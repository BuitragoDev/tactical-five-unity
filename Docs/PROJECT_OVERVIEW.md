# PROJECT_OVERVIEW — Tactical Five

> **Purpose of this document:** entry point to the whole project. Read this first, then follow the reading order at the bottom.

**Version analyzed:** Unity `6000.3.15f1` (Unity 6), editor build with a single scene, in-app version shown as `v0.9.0 · Beta` (`MainMenu.uxml` footer).

**Product:** *Tactical Five* — a single-player NBA-management simulation game, entirely in Spanish, played with mouse/keyboard on desktop (target resolution 1920×1080). It is a "GM mode" (no playable basketball): the player manages a franchise season by season, simulating games and making roster/financial decisions.

---

## 1. What the game is

- You are the **manager** (`ManagerData`) of one of **30 NBA-like franchises** (`TeamData`) named after real NBA teams (division/conference/arena/capacity/owner/logo/jerseys are seeded real data).
- A season runs roughly from **October 22 to mid-April** plus Play-In/Playoffs, modeled as a sequence of **game days** (one `GameData` row per match, up to 15 matches/day).
- You do **not** control a player live: matches are **simulated possession-by-possession** (`GameSimulator`) with your starting lineup (`LineupData`), substitutions, chemistry, morale, fatigue and injuries. The match can be shown live in a **play-by-play overlay** (marcador, reloj, barra de progreso y boxscore en vivo) or simulated directly (`Directa`), configurable via "Vista de Partido" en los ajustes.
- The strategic layer includes: roster building (trades, free agents, renewals), finances (budget, ticket price, subscriptions, sponsors, TV deals, loans, arena renovations, luxury tax), training, staff (employees/scouts/psychologist), morale/relationships, and a full league simulation (draft lottery, playoffs, awards, historical records).

## 2. Game modes

Defined in `GameEnums.cs` (`GameMode`): `None`, `Manager`, `ProManager`, `Editor`.

| Mode | Entry point | Meaning |
|---|---|---|
| `Manager` | `MainMenuController.OnManagerClicked` | Standard career; selects a team, plays seasons |
| `ProManager` | `MainMenuController.OnProManagerClicked` | Harder mode; shows a restrictions modal (`OpenProModal`) before starting. All harder rules implemented: objective-based season-end firing, earlier budget firing (threshold 2), no NT-MLE on FA (Taxpayer MLE only). Code-level differences: worst-team selection, bottom-10 new-season offers, annual team change |
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

**Simulación rápida:** desde `CalendarController` el jugador elige una fecha y confirma; se navega al Dashboard, que ejecuta `FastSimRoutine` (`DashboardController`): hace un bucle de `ProcessGameDayRoutine(fastSim: true)` hasta alcanzar la fecha objetivo o el fin de temporada. En modo `fastSim` cada día se procesa por trozos (pre-lote en 3 pasos, partidos uno a uno y química por pareja, con `yield return null` entre ellos) dentro de los mismos transactions atómicos del día, de modo que el hilo principal queda libre cada frame: el spinner del header gira continuo y el botón **DETENER SIMULACIÓN** queda activo en todo momento (con cursor hand vía `CursorManager`); la parada se aplica al terminar el día en curso. La navegación del sidebar se deshabilita durante la simulación para proteger los transactions. Tras cada día se recargan `_players`/`_allGames` y se llama `Refresh()` para que la fecha del header y los paneles del Dashboard avancen en vivo; al terminar, `_fastSimRunning` se pone a `false` antes del `Refresh()` final para que `RefreshActionButton` recupere CONTINUAR / SIMULAR PARTIDOS / DÍA DE PARTIDO. Quinteto incompleto/lesionados muestran el modal de quinteto: la opción automática arregla la alineación y reanuda la simulación desde el mismo día saltándose el pre-lote ya commiteado vía `_fastSimSkipPreBatch`; la opción manual lleva a Quinteto; un tope de 3 auto-rellenos fallidos detiene la sim). **Pausa por ofertas:** al final de cada día en modo `fastSim`, `ProcessGameDayRoutine` comprueba si hay ofertas maduradas (renovaciones/fichajes) o propuestas de traspaso pendientes; si las hay, la simulación se detiene en ese punto (el día ya está commiteado y no se duplica al reanudar) y `FastSimRoutine` muestra los modales con la simulación parada: las ofertas maduradas muestran un modal con dos botones — **IR A QUINTETO** (azul: detiene la simulación y navega a Quinteto) y **SEGUIR SIMULANDO** (verde: reanuda); los traspasos entrantes se muestran vía `ShowNextPendingTradeOffer` — RECHAZAR reanuda la simulación y ACEPTAR la detiene (como DETENER, sin resumen). Al terminar la simulación (salvo DETENER o aceptar un traspaso) se muestra un modal de resumen simplificado: solo el título "SIMULACIÓN COMPLETADA" en verde con un botón CERRAR (sin rango de fechas, balance W-L ni acontecimientos). En el flujo día a día (avanzar por SIMULAR PARTIDOS / DÍA DE PARTIDO, sin fast sim), el modal de respuesta de ofertas/renovaciones muestra un único botón CERRAR en azul. El canal Calendar→Dashboard es `GameResultCache.FastSimTargetDate`.

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
- ~500 commits; last merge to `main`: play-by-play + S&T + options + trade deadline + AI GM strategy (`41a5d45`, merge de `crear-mejoras2`).
- `PLAN.md` (a plan for fixing free-agent offers/trades) is **largely implemented**: draft picks model, hard cap flag, luxury tax, buyout/stretch, sign-and-trade (own FA with Bird rights), options (TO/PO) with re-sign, and trade deadline event all exist. See `MEMORY.md` and `TODO_TECHNICAL_DEBT.md`.
- Known structural debt: duplicate `CursorManager`, orphaned `SettingsController`, unused `SQLiteAsync.cs`. See `TODO_TECHNICAL_DEBT.md`.

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
