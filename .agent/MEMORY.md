# MEMORY — Persistent Project Knowledge (Tactical Five)

> Long-term memory of the project: state, decisions and their reasons, confirmed assumptions, known bugs, and things that must never change without review. Updated as the project evolves.

## 1. Project identity

- **Name/product:** Tactical Five (TacticalFive, company "BuitragoStudio").
- **Version:** `v0.9.0 · Beta` (MainMenu footer).
- **Engine:** Unity `6000.3.15f1` (Unity 6). **Build:** single scene `MainMenu.unity` only.
- **Language:** all in-game text is Spanish; code identifiers mix Spanish/English.
- **Git:** ~503 commits; last analyzed commit `50b1a86` (2026-07-29).

## 2. Goal & core mechanics (as built)

- Simulate an NBA-like franchise career: 30 teams, 82-game season (Oct 22 → mid-April), Play-In, Playoffs (best-of-7), draft, awards, records, and infinite seasons with aging/progression.
- Player is GM, not a coach/player: picks lineups, manages contracts/salary cap, finances, staff, training, morale, and hits "advance day".
- Match outcomes decided by `GameSimulator` (possession-based, attribute-driven).

## 3. Current development state (observed)

**Functional (implemented in code):**
- Full screen set (~39 active screens + Loading/MainMenu); navigation via `ScreenManager`.
- New game (Manager / ProManager), load game (slots), editor (template DB).
- Preseason + schedule generation; regular season; Play-In; Playoffs; season end awards; draft; new season loop.
- Salary cap, Bird Rights, exceptions, aprons, hard cap flag, luxury tax (monthly `annual/12`), buyout (stretch), offers (renewal/FA, 7-day maturation), AI trades, star FA signings.
- Economy: tickets, subscriptions, sponsors, TV, loans, arena renovations, employee staff, scouts, psychologist.
- Soft systems: morale, fan confidence, chemistry, personalities, relationships.
- Injuries/fatigue; training (attribute +2); records; monthly awards; All-Star; coach rankings; career stats archive; player photos cascade.
- Persistence: SQLite slots + `template.db` + `saves.json`.

**Pending / incomplete (identified):**
- `GameScreen.Settings` / `SettingsController` orphaned.
- `SQLiteAsync.cs` present but unused.
- `PLAN.md` (repo root) documents an improvement plan; parts already done (draft picks, hard cap, luxury tax, buyout). **Not synced with code.**

## 4. Known bugs / risks (see `TODO_TECHNICAL_DEBT.md` for full list)

- **B1 (critical):** duplicate `CursorManager` in scene can destroy the `ScreenManager` GameObject at boot (singleton guard + `Destroy(gameObject)`). Order undefined.
- **B2:** `PlayerPrefs` migration flags are machine-global per slot number; not cleared on delete.
- **B10:** non-deterministic sim; `string.GetHashCode` seed unstable.
- **B9:** no transactions around multi-write flows (partial state on crash).

## 5. Key architectural decisions & reasons

| Decision | Reason (observed/deduced) |
|---|---|
| Single scene + `UIDocument` screens, `ScreenManager` toggling `SetActive` | All UI in one place; cheap navigation; controllers rebuild in `OnEnable` |
| SQLite as the save/state store | Sim data is relational (teams/players/games/stats); template DB gives fast, consistent new games; avoids JSON save parsing |
| Static utility classes for simulation rules | Stateless, testable helpers; controllers call them directly |
| No event bus; DB-messages + static hand-offs | Simplest possible decoupling for a single-player sim; avoids async complexity |
| `overall` always recomputed from 11 attributes (cap `potential`) | Single source of truth for ratings; training/aging stay consistent |
| Additive column-presence migrations | Old saves open on new code; no version table needed |
| Cap constants duplicated in `TradeHelper` and `league_settings` (seeded) | `TradeHelper` is source for UI; DB copy exists for potential runtime changes (+5%/season applied to DB row) |

## 6. Confirmed assumptions

- `overall` = `round(mean(11 attributes))`, capped by `potential` — verified in seeders, migrations, training, progression.
- `PlayerData.id` manual (seed 1..~600) — stable across cloned slots.
- `league_settings` seeded from `TradeHelper` constants + `bi_annual=5.1M`; `StartNewSeason` raises all caps +5%.
- Employee `reputation` (1–5) drives ticket multiplier and renovation discounts (not `skill`; `EmployeeData` has no `skill` field).
- FA external signings have **no Bird Rights** (`birdMax=0`); exceptions apply by apron when over cap.
- NT-MLE usage sets `first_apron_hard_capped=1`.
- Offers mature after 7 days (`day_sent + 7`); acceptance = `Random(1,101) <= acceptScore`.
- Contract options (TO/PO) are mutually exclusive; if set, `guaranteed_years = max(0, offer_years − 1)`. Persisted on `players`/`offers` (`guaranteed_years`, `has_team_option`, `has_player_option`). Renewal & FA offer modals toggle them; `ProcessMaturedOffers.FormatContractYears` shows them in inbox messages.
- `players.last_team_id` tracks the last team a player played for (set at seed/sign, kept when going to FA via option decline or contract expiry). `IsOwnRecentFA(p, teamId)` = `team_id==0 && last_team_id==teamId && seasons_with_team>0` → used to grant **Bird rights** on re-sign (`GetMaxOfferBreakdown(isFromSameTeam:true)`) and to re-attach the player on accepted re-sign offers. `NewSeasonController` shows a re-sign modal for declined player options (deferred offer, matures in 7 days).
- **Sign-and-trade de FA propio** (`MarketController.ProcessSATrade`): sección "FA RECIENTES (BIRD RIGHTS)" en el panel de traspaso; firma (Bird) + traspaso inmediato → dos `TradeData` (`free_agent` + `sign_and_trade`); receptor bajo hard cap del 1er apron. `TradeHelper.ValidateTrade`/`EvaluateTrade` aceptan `teamASignSalaries`/`teamBSignSalaries` (salario nuevo firmado; no descuenta roster/nómina del que firma). La IA propone S&T por el FA propio del usuario (`GenerateAITradeOffersForPlayer`, resuelto en `ShowNextPendingTradeOffer`) y `pendingSATIds` evita que la IA lo fiche mientras la oferta está pendiente. El S&T de jugador entrante expirante (toggle en `ShowTradeResult`) se mantiene.
- **Trade Deadline con evento real:** `OnActionClicked` intercepta el btnAction el 7 de febrero: modal "DEADLINE DAY" (IR AL MERCADO / CERRAR, una vez por temporada vía `_deadlineDayModalShown` + `_deadlineModalSeasonId`). No avanza la fecha. Rush IA durante Feb 1-8: `ProcessAITransfers` cooldown 3-5 días (`IsDeadlineWeek`), contenders (`IsTeamContender`, top 4 conferencia) ofrecen picks extra, títulos de ofertas IA con prefijo `[DEADLINE]`. Badge `⏳ ÚLTIMOS X DÍAS` en header del Market (`DeadlineCountdownBar` en `Market.uxml`). `DashboardController.ShowDeadlineDayModal`, `IsFeb7OfYearEnd`.
- **AI de GMs con estrategia (`DashboardController`):** enum `TeamStrategy { Rebuild, Balanced, Contend }`; `GetTeamStrategy` (top 4 conferencia o 2+ estrellas OVR≥85 → Contend; últimos 4 o plantilla joven sin estrellas → Rebuild) calculado una vez por ciclo en `BuildTeamStrategyCache` (cache `_teamStrategyCache`). `ProcessAITransfers` usa cooldown por equipo (`_teamTradeCooldown`, limpiado al cambiar de temporada en `OnEnable`; Contend 6 días, Rebuild 8, Balanced 15; 3-5 en deadline), densidad por estrategia (0.45/0.40/0.25) y `maxTrades` 3. `TrySellVeteran` (fire sale): Rebuild ofrece veteranos edad≥30 por jóvenes edad<25 o picks vía `TryExecuteTrade`. `TryFindAITrade`: Contend busca mejoras hasta OVR 90 y añade pick futuro si OVR>84, protegiendo jóvenes (<26 y OVR≥82); Rebuild/Balanced como antes (cap 86). `PickTradeTarget`/`BuildOfferPackage` reciben la estrategia: Contend persigue estrellas OVR≥83 (75%) y nunca ofrece jóvenes de valor; Rebuild busca jóvenes (30%) y solo paga con ≥25 años. `ProcessStarFreeAgentSignings` prioriza Contend > Balanced > Rebuild y Rebuild no ficha OVR≥85 (protege el pick del draft).
- `saves.json` written at: preseason commit (`PreseasonController:429`), every day advance (`DashboardController:1023`), load (`LoadGameController:193`), delete (`GameSaveManager:192`).
- **ProManager SÍ tiene diferencias de juego:** al pulsar "ProManager" en el menú se muestra un modal de restricciones (`ProModalOverlay` en `MainMenu.uxml`; `MainMenuController.OpenProModal/ConfirmProManager/CloseProModal`): sin NT-MLE, cese por presupuesto más exigente, objetivo de temporada exigente y cambio de equipo cada temporada. El slot solo se crea al confirmar (CONTINUAR). Implementado: **cese por objetivo no cumplido al fin de temporada** (solo ProManager, `DashboardController.OnActionClicked` `phase=="finished"` → `ShowObjectiveFiredModal`; coexiste con cese por `trust<10`), **cese por presupuesto a partir de 2 avisos en rojo** (`CheckBudgetWarning`, ProManager=2 vs Manager=3). **Lógica de objetivo/rank centralizada en `ObjectiveHelper`** (static puro: `IsObjectiveMet(objective, rank)` con umbrales Zona tranquila≤12 / Play-In≤10 / Playoffs≤6 / Campeonato≤2 y `GetConferenceRank(teamId, conference, teams, games)` filtrando `regular`+`is_played`; usado por Dashboard `GetMyTeamConferenceRank`/`RefreshTeamStats`/`IsObjectiveMetThisSeason` y `ManagerController.RefreshObjective`). **Sin NT-MLE en ofertas FA** (regla activa en ProManager): `RosterController.GetMaxOfferBreakdown`/`CalculateMaxOfferSalary` aceptan `bool proManagerOnly` (default false) que, para FA externo sobre el cap dentro del 1er apron, fuerza la excepción a la **Taxpayer MLE (`tMle`)** en vez de la No-Taxpayer (`ntMle`); aplicado en `MarketController.UpdateFAMaxInfo`, `UpdateFAWarning` (mensaje "MODO PRO") y `SendFAOffer` (`_faMaxSalary`), y en `DashboardController.ProcessMaturedOffers` (rama `proManagerOnly && totalPayroll<=firstApron` → límite `tMle`). En ProManager el **hard cap por NT-MLE no se activa** (`ProcessMaturedOffers` salta el bloque si `game_mode=="promanager"`). Solo se pueden elegir los peores equipos en `SelectTeamController` (`GetWorstTeams(5)`, el resto deshabilitado) y en `NewSeasonController` las ofertas de equipo disponibles se limitan a tu equipo + 3 aleatorios del bottom-10. `GameMode` guarda `"promanager"` en `seasons.game_mode`.

## 7. Things that must NEVER change without reviewing the rest of the project

1. **`overall` semantics** (mean of 11 attrs, cap by potential) — affects seed, training, aging, migration, drafting, AI evaluation, standings sorting.
2. **Cap/apron constants location** (`TradeHelper`) and their +5%/season growth — used by dozens of screens and validations.
3. **`ScreenManager.GoTo` contract** (mode handling, `ShowOnly` SetActive) — every controller depends on it.
4. **`GameResultCache.Clear()` at day start** — forgetting it corrupts the results flow.
5. **Slot/DB path layout** (`TacticalFive/saves/save_{n}.db`, `template.db`, `saves.json`, `PlayerPhotos/{slot}`) — game saves and delete logic depend on it.
6. **`MessageData.sender_type` semantics** (0 system / 1 player / 2 news) — used for filtering/icons.
7. **`player_season_stats` + `monthly_awards` dual creation path** (CreateTable AND raw SQL) — any schema change must update both.
8. **Duplicate `CursorManager`** — understand B1 before touching singletons in the scene.
9. **`phase` state machine** (preseason→regular→playin→playoff→finished) — drives dashboard, playoffs, awards.
10. **The config modal duplication** — if you refactor it, touch ALL screens that embed it (or use a shared controller).

## 8. Glossary pointer

Terms in `.agent/GLOSSARY.md`. Quick ones: Plantilla=roster, Traspaso=trade, Fichaje=signing, Pabellón=arena, Ojeadores=scouts, Abonos=subscriptions.

## 9. Repo hygiene notes

- `Assets/_Recovery/` contains leftover recovery scenes (gitignored) — not used.
- `Assets/_TacticalFive/Data/Database.meta` is an orphan folder meta — harmless.
- `PLAN.md` exists at root and is **not committed** (working-tree only as of analysis). Decide its fate (sync with code or delete).
- Documentation lives in `Docs/` and `.agent/` — keep in sync with code changes.
