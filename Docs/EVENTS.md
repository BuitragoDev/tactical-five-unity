# EVENTS — Tactical Five

> **Important architectural fact [F]:** there is **no C# event bus, no `UnityEvent` wiring between systems, and no message broker** in this project. Communication happens through four explicit mechanisms:
> 1. **DB messages** (`messages` table) — one-way notifications the user reads in the inbox.
> 2. **Static mutable state** — `GameResultCache`, `ScreenManager.SelectedPlayerId/CurrentMode/CurrentScreen`.
> 3. **PlayerPrefs** — persisted settings (audio, quality) and one-time migration flags.
> 4. **UI Toolkit callbacks** — `ClickEvent`, `KeyDownEvent`, `MouseEnterEvent`, `MouseLeaveEvent`, `ChangeEvent`, coroutines with `WaitUntil` for modal resolution.
>
> These are all **point-to-point**; nothing is decoupled via events. This is a design observation (see `CODE_GUIDELINES.md`).

---

## 1. Global static state (the de-facto event channel)

### `GameResultCache` (`GameResultCache.cs`)
| Field | Written by | Read by | Effect |
|---|---|---|---|
| `LastGameDay` | `DashboardController` (`ProcessGameDayRoutine`, ~line 796) | `GameResultsController` (to know which day to show) | GameResults screen shows that day |
| `SimulatedGameIds` | `DashboardController` | `GameResultsController` | Which games have live results |
| `GameStarters` (dict game→HashSet<int>) | `DashboardController` (starters = first 5 `GetActivePlayers`) | `GameResultsController`, `MatchDayController` | Highlight starters, box scores |
| `PendingBudgetWarning` | `DashboardController.CheckBudgetAfterGame` (line 1079) | `DashboardController.CheckBudgetWarning` (line 1090) | Shows fired/warning modal after day |

`GameResultCache.Clear()` is called at the start of each simulated game day.

### `ScreenManager` static state
| Field | Written by | Read by | Effect |
|---|---|---|---|
| `SelectedPlayerId` | `RosterController` (click on player row) | `TrajectoryController` (shows that player's career) | Cross-screen player context |
| `CurrentMode` | `GoTo(screen, mode)` | `SelectTeamController` (labels mode), others | Mode-aware UI |
| `CurrentScreen` | `GoTo` | UI code | Current screen marker |

## 2. DB messages (the inbox)

All inserted via `DatabaseManager.AddMessage(MessageData)`. `sender_type`: **0 = system**, **1 = player**, **2 = quick news**. Consumers: `MessagesController` (list), `HeaderController` (unread count).

| Event (title pattern) | Emitter (file:line) | Consequence |
|---|---|---|
| Match result message (`Resultado: ...`) | `DashboardController.CreateGameResultMessage` (~3442) | Inbox entry with score/attendance |
| Fichaje / Oferta aceptada/rechazada | `DashboardController.ProcessMaturedOffers` (1139–1383) | User learns outcome of FA/renewal |
| Contrato renovado | `DashboardController.ProcessMaturedOffers` (1349) | Same |
| Fichaje cancelado (player signed elsewhere) | `DashboardController.ProcessMaturedOffers` (1151) | Same |
| Fichaje rechazado (plantilla completa / ilegal) | `DashboardController.ProcessMaturedOffers` (1180, 1235) | Same |
| Hard cap activado | `DashboardController.ProcessMaturedOffers` (1297) | Warns user of NT-MLE hard cap |
| Recuperado de lesión | `DashboardController.ProcessGameDayRoutine` (721) | Inbox; player back in lineup |
| Lesión (from game) | `DashboardController.ProcessGameInjuries` | Inbox |
| Queja / Preocupación de jugador (morale < 20/10) | `DashboardController.UpdatePlayersMoraleAfterGame` (3282) | Signals morale issue |
| Remodelación iniciada/completada | `ArenaController` (656), `DashboardController` (3102) | Arena capacity increased |
| Última semana de traspasos (Feb 1) | `DashboardController.ProcessGameDayRoutine` (996) | Reminder |
| Simulación rápida hasta fecha | `CalendarController.ConfirmFastSim` → `GameResultCache.FastSimTargetDate` → `DashboardController.FastSimRoutine` | Pausa ante ofertas maduradas/traspasos entrantes (IR A QUINTETO / SEGUIR SIMULANDO); al terminar, modal de resumen simplificado (solo "SIMULACIÓN COMPLETADA" + CERRAR) |
| Noticias rápidas (hitos/rachas/campanadas/TD/40pts) | `QuickNewsGenerator.Generate` (max 2/day) | Inbox |
| Premio del mes (Manager/Jugador/Rookie) | `DatabaseManager.EvaluateMonthlyAwards` | Inbox + `monthly_awards` |
| Trade AI offers to player | `DashboardController.GenerateAITradeOffersForPlayer` → shown via `ShowNextPendingTradeOffer` modal (not inbox) | User accepts/rejects in a modal |
| Star FA signed by AI | `DashboardController.ProcessStarFreeAgentSignings` | Inbox; player leaves FA pool |

## 3. PlayerPrefs keys (settings events)

| Key | Written by | Read by |
|---|---|---|
| `TF_Audio_Master` | `AudioManager.SetMasterVolume` | `AudioManager.LoadSettings` |
| `TF_Audio_Music` | `AudioManager.SetMusicVolume` | `AudioManager.LoadSettings` |
| `TF_Audio_SFX` | `AudioManager.SetSFXVolume` | `AudioManager.LoadSettings` |
| `TF_Graphics_Quality` | `AudioManager.SetQualityLevel` | `AudioManager.LoadSettings` |
| `OverallMigration_{slot}` | `DatabaseManager.RunMigrations` | one-time flag |
| `DraftPicksReset_{slot}` | `DatabaseManager.RunMigrations` | one-time flag |

## 4. UI Toolkit events (per screen)

Standard per-controller wiring in `OnEnable` after `CacheReferences()`:

- `ClickEvent` — all buttons (nav, actions, modals). Registered with `RegisterCallback<ClickEvent>`.
- `KeyDownEvent` — `MainMenuController` (Escape opens/closes modals), `LoadingController` (any key skips).
- `MouseEnterEvent` / `MouseLeaveEvent` — registered via `CursorManager.RegisterHandCursor(element)` with `TrickleDown.TrickleDown`.
- `ChangeEvent<float>` — `CustomSlider` value changes update volume labels/`AudioManager`.
- Coroutine `WaitUntil` — modal resolution flags (`_emptyLineupModalResolved`, `_injuredModalResolved`, `_renewResult` auto-close after 5s).
- `schedule.Execute(...)` — deferred actions (100 ms cursor registration in `MainMenuController`).

## 5. Cross-scene "transitions" (navigation)

`ScreenManager.GoTo(GameScreen, GameMode)` is the only navigation event; controllers use `OnEnable` (rebind) and rely on `SetActive` semantics (there is no `OnDisable` teardown beyond Unity's default). Full navigation tree in `UI_TOOLKIT.md`.

---

## Complete event table (emitter → listener → effect)

| # | Emitter | Mechanism | Listener | Effect |
|---|---|---|---|---|
| 1 | `DashboardController` | `GameResultCache` | `GameResultsController`, `MatchDayController` | Results/boxscores screen |
| 2 | `RosterController` | `ScreenManager.SelectedPlayerId` | `TrajectoryController` | Player career screen |
| 3 | `ScreenManager.GoTo` | static enum + SetActive | All controllers `OnEnable` | Screen swap |
| 4 | Any controller | `AudioManager.Instance?.PlaySFX("click")` | `AudioManager` | SFX |
| 5 | `MainMenuController` | `AudioManager.Instance?.PlayMusic` | `AudioManager` | Music |
| 6 | Any controller | `CursorManager.RegisterHandCursor` | `CursorManager` | Hand cursor on hover |
| 7 | `DashboardController` | `PlayerPrefs` | `AudioManager` | volumes/quality |
| 8 | `DatabaseManager.RunMigrations` | `PlayerPrefs` | itself (next init) | one-time migrations |
| 9 | Any game logic | `DatabaseManager.AddMessage` | `MessagesController`/`HeaderController` | inbox |
| 10 | `QuickNewsGenerator` | `AddMessage` + dedup query | inbox | news |
| 11 | `DashboardController` | `ShowNextPendingTradeOffer` modal | user input | trade answer → `TradeOfferData` |
| 12 | `DashboardController` | `ProcessMaturedOffers` | inbox | contract outcomes |

## Open questions

- No formal eventing: is this intentional simplicity or a debt item? (see `TODO_TECHNICAL_DEBT.md`)
- `MessagesController` unread badge logic vs `HeaderController` — same count? ([D] Header reads messages table directly.)
