# EVENTS — Tactical Five

> **Important architectural fact [F]:** there is **no C# event bus, no `UnityEvent` wiring between systems, and no message broker** in this project. Communication happens through four explicit mechanisms:
> 1. **DB messages** (`messages` table) — one-way notifications the user reads in the inbox.
> 2. **Static mutable state** — `GameResultCache`, `ScreenManager.SelectedPlayerId/CurrentMode/CurrentScreen`.
> 3. **PlayerPrefs** — persisted settings (audio, quality, sim mode, load management). One-time data migrations are stored in SQLite `schema_migrations`, not PlayerPrefs.
> 4. **UI Toolkit callbacks** — `ClickEvent`, `KeyDownEvent`, `MouseEnterEvent`, `MouseLeaveEvent`, `ChangeEvent`, coroutines with `WaitUntil` for modal resolution.
>
> These are all **point-to-point**; nothing is decoupled via events. This is a design observation (see `CODE_GUIDELINES.md`).

---

## 1. Global static state (the de-facto event channel)

### `GameResultCache` (`Scripts/GameResultCache.cs`)
| Field | Written by | Read by | Effect |
|---|---|---|---|
| `LastGameDay` | `DashboardController` (`ProcessGameDayRoutine`) | `GameResultsController` | GameResults screen shows that day |
| `SimulatedGameIds` | `DashboardController` | `GameResultsController` | Which games have live results |
| `GameStarters` (dict game→HashSet<int>) | `DashboardController` (starters = first 5 `GetActivePlayers`) | `GameResultsController`, `MatchDayController` | Highlight starters, box scores |
| `PlayByPlayLogs` (dict game→`List<PlayByPlayEvent>`) | `DashboardController.ProcessSingleGame` (solo modo Play-by-play) | `MatchDayController` (overlay play-by-play) | Crónica del partido: marcador, reloj y deltas por evento |
| `PendingBudgetWarning` | `DashboardController.CheckBudgetAfterGame` | `DashboardController.CheckBudgetWarning` | Shows fired/warning modal after day |
| `FastSimTargetDate` | `CalendarController.ConfirmFastSim` | `DashboardController.FastSimRoutine` | Target date for fast sim; consumed/reset in `DashboardController.OnEnable` |

`GameResultCache.Clear()` is called at the start of each simulated game day.

### `ScreenManager` static state
| Field | Written by | Read by | Effect |
|---|---|---|---|
| `SelectedPlayerId` | `RosterController` / `StatsController` (click on player row) | `TrajectoryController`, `PlayerProfileController` (show that player) | Cross-screen player context |
| `CurrentMode` | `GoTo(screen, mode)` | `SelectTeamController` (labels mode), others | Mode-aware UI |
| `CurrentScreen` | `GoTo` | UI code | Current screen marker |

### `AchievementService` toast queue (`Scripts/Core/AchievementService.cs`)
| Field | Written by | Read by | Effect |
|---|---|---|---|
| `_pendingToasts` (static `List<GmAchievementDefinition>`) | `UnlockIfMissing(..., notify:true)` adds | `DashboardController.Update` via `TakeNextToast()` | Toast overlay (logro desbloqueado) en Dashboard; `ShowAchievementToast` :269 |

## 2. DB messages (the inbox)

All inserted via `DatabaseManager.AddMessage(MessageData)`. `sender_type`: **0 = system**, **1 = player**, **2 = quick news**. Consumers: `MessagesController` (list), `HeaderController` (unread count).

| Event (title pattern) | Emitter (file:line) | Consequence |
|---|---|---|
| Match result message (`Resultado: ...`) | `DashboardController.CreateGameResultMessage` | Inbox entry with score/attendance |
| Fichaje / Oferta aceptada/rechazada | `DashboardController.ProcessMaturedOffers` | User learns outcome of FA/renewal |
| Contrato renovado | `DashboardController.ProcessMaturedOffers` | Same; contract text includes TO/PO via `FormatContractYears` |
| Fichaje cancelado (player signed elsewhere) | `DashboardController.ProcessMaturedOffers` | Same |
| Fichaje rechazado (plantilla completa / ilegal) | `DashboardController.ProcessMaturedOffers` | Same |
| Hard cap activado | `DashboardController.ProcessMaturedOffers` | Warns user of NT-MLE hard cap |
| Recuperado de lesión | `DashboardController.ProcessGameDayRoutine` | Inbox; player back in lineup |
| Lesión (from game) | `DashboardController.ProcessGameInjuries` | Inbox |
| Queja / Preocupación de jugador (morale < 20/10) | `DashboardController.UpdatePlayersMoraleAfterGame` | Signals morale issue |
| Remodelación iniciada/completada | `ArenaController`, `DashboardController` | Arena capacity increased |
| Última semana de traspasos (Feb 1) | `DashboardController.ProcessGameDayRoutine` | Reminder |
| Simulación rápida hasta fecha | `CalendarController.ConfirmFastSim` → `GameResultCache.FastSimTargetDate` → `DashboardController.FastSimRoutine` | Pausa ante ofertas maduradas/traspasos entrantes (IR A QUINTETO / SEGUIR SIMULANDO); al terminar, modal de resumen simplificado (solo "SIMULACIÓN COMPLETADA" + CERRAR) |
| Noticias rápidas (hitos/rachas/campanadas/TD/40pts) | `QuickNewsGenerator.Generate` (max 2/day) | Inbox |
| Premio del mes (Manager/Jugador/Rookie) | `DatabaseManager.EvaluateMonthlyAwards` | Inbox + `monthly_awards` |
| Trade AI offers to player | `DashboardController.GenerateAITradeOffersForPlayer` → shown via `ShowNextPendingTradeOffer` modal (not inbox) | User accepts/rejects in a modal |
| Star FA signed by AI | `DashboardController.ProcessStarFreeAgentSignings` | Inbox; player leaves FA pool |
| Logro desbloqueado | `AchievementService.UnlockIfMissing` → `DashboardController.Update` | Toast overlay (no inbox) |

## 3. PlayerPrefs keys (settings events)

| Key | Written by | Read by |
|---|---|---|
| `TF_Audio_Master` | `AudioManager.SetMasterVolume` | `AudioManager.LoadSettings` |
| `TF_Audio_Music` | `AudioManager.SetMusicVolume` | `AudioManager.LoadSettings` |
| `TF_Audio_SFX` | `AudioManager.SetSFXVolume` | `AudioManager.LoadSettings` |
| `TF_Graphics_Quality` | `AudioManager.SetQualityLevel` | `AudioManager.LoadSettings` |
| `TF_SimMode` | `UIScreenController.SelectConfigSimMode` (Vista de Partido) | `UIScreenController.GetSimMode`, `MatchDayController` | 0 = Directa, 1 = Play-by-play |
| `TF_PbpSpeed` | `MatchDayController.SelectPbpSpeed` (x1/x3/x5/x10) | `MatchDayController` | Velocidad del overlay play-by-play |
| `TF_LoadMgmt_Enabled` | `QuintetoController` toggle (load management) | `DashboardController.ProcessGameDayRoutine` (rest modal) | 1 = activar descanso jugadores cansados en back-to-back |

## 4. DB-side one-time migrations (moved from PlayerPrefs to `schema_migrations`)

**All data migrations now keyed in the `schema_migrations` table** (`name` PRIMARY KEY, `applied_at`), living with the slot — deleting the slot resets state. Helpers `IsMigrationApplied(name)` / `MarkMigrationApplied(name)` (`DatabaseManager.cs:762-785`).

| Migration name | Purpose |
|---|---|
| `overall_recalc` | Recompute `overall` = mean of 11 attrs (cap by potential) |
| `draft_picks_reset` | Wipe `draft_picks` and reseed for the active season (uses previous standings) |

(Column-based additive migrations still use `PRAGMA table_info`; see `DATA_MODEL.md §5`.)

## 5. UI Toolkit events (per screen)

Standard per-controller wiring in `OnEnable` after `CacheReferences()`:

- `ClickEvent` — all buttons (nav, actions, modals). Registered with `RegisterCallback<ClickEvent>`.
- `KeyDownEvent` — `MainMenuController` (Escape opens/closes modals), `LoadingController` (any key skips).
- `MouseEnterEvent` / `MouseLeaveEvent` — registered via `CursorManager.RegisterHandCursor(element)` with `TrickleDown.TrickleDown`.
- `ChangeEvent<float>` — `CustomSlider` value changes update volume labels/`AudioManager`.
- Coroutine `WaitUntil` — modal resolution flags (`_emptyLineupModalResolved`, `_injuredModalResolved`, `_renewResult` auto-close after 5s).
- `schedule.Execute(...)` — deferred actions (100 ms cursor registration in `MainMenuController`).

## 6. Cross-scene "transitions" (navigation)

`ScreenManager.GoTo(GameScreen, GameMode)` is the only navigation event; controllers use `OnEnable` (rebind) and rely on `SetActive` semantics (there is no `OnDisable` teardown beyond Unity's default). Full navigation tree in `UI_TOOLKIT.md`.

---

## Complete event table (emitter → listener → effect)

| # | Emitter | Mechanism | Listener | Effect |
|---|---|---|---|---|
| 1 | `DashboardController` | `GameResultCache` | `GameResultsController`, `MatchDayController` | Results/boxscores screen |
| 2 | `RosterController`/`StatsController` | `ScreenManager.SelectedPlayerId` | `TrajectoryController`, `PlayerProfileController` | Player career/profile screen |
| 3 | `ScreenManager.GoTo` | static enum + SetActive | All controllers `OnEnable` | Screen swap |
| 4 | Any controller | `AudioManager.Instance?.PlaySFX("click")` | `AudioManager` | SFX |
| 5 | `MainMenuController` | `AudioManager.Instance?.PlayMusic` | `AudioManager` | Music |
| 6 | Any controller | `CursorManager.RegisterHandCursor` | `CursorManager` | Hand cursor on hover |
| 7 | `DashboardController` | `PlayerPrefs` | `AudioManager` | volumes/quality |
| 8 | `DatabaseManager.RunMigrations` | `schema_migrations` + `PRAGMA table_info` | itself (next init) | one-time migrations |
| 9 | Any game logic | `DatabaseManager.AddMessage` | `MessagesController`/`HeaderController` | inbox |
| 10 | `QuickNewsGenerator` | `AddMessage` + dedup query | inbox | news |
| 11 | `DashboardController` | `ShowNextPendingTradeOffer` modal | user input | trade answer → `TradeOfferData` |
| 12 | `DashboardController` | `ProcessMaturedOffers` | inbox | contract outcomes |
| 13 | `AchievementService` | static toast queue | `DashboardController.Update` | achievement toast |
| 14 | `QuintetoController` | `PlayerPrefs` `TF_LoadMgmt_Enabled` | `DashboardController` | load-management rest modal |

## Open questions

- No formal eventing: is this intentional simplicity or a debt item? (see `TODO_TECHNICAL_DEBT.md`)
- `MessagesController` unread badge logic vs `HeaderController` — same count? ([D] Header reads messages table directly.)
