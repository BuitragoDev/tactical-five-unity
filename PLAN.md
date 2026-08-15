# Plan de mejoras — Tactical Five

Estado verificado contra el código en la rama `crear-mejoras`. Las entradas marcan
`[hecho]` o `[pendiente]` con referencia `archivo:línea` cuando aplica.

> **Actualización (2026-08-13):** todas las entradas están **mergeadas en `main`** y
> verificadas en HEAD `1d88989`. La revisión de la documentación completa está en
> `Docs/` y `.agent/`. Para trabajo pendiente ver `NEXT_PROPOSALS.md` (G-League/IR) y
> `Docs/TODO_TECHNICAL_DEBT.md`.

## Completado

- **[hecho]** Hard cap / aprons: validación de ofertas por apron y activación del
  hard cap del 1er apron al usar NT-MLE. `RosterController.cs:GetMaxOfferBreakdown`,
  `DashboardController.cs` (maduración de ofertas), `MarketController.cs` (aviso).
- **[hecho]** Luxury tax mensual para todos los equipos (antes solo el usuario).
  `DashboardController.cs:ProcessMonthlyPayroll`/`ProcessTeamLuxuryTax`.
- **[hecho]** Buyout con stretch `contract_years * 2`. `RosterController.cs`.
- **[hecho]** Draft picks al inicio de temporada. `DatabaseManager.cs:SeedDraftPicks`.
- **[hecho]** Fuente única de topes salariales: `league_settings` crece +5%/año y el
  código lee de ahí con fallback a `TradeHelper` (añadida columna `taxpayer_mid_level`).
  `TradeHelper.cs` recibe aprons/luxury tax opcionales en `ValidateTrade`/`EvaluateTrade`.
- **[hecho]** Recuperación de fatiga (`fisico`) para todas las plantillas, no solo el
  equipo del usuario. `DashboardController.cs:ProcessFisicoRecovery`.
- **[hecho]** Migraciones one-time dentro del DB (`schema_migrations` + `PRAGMA user_version`)
  en lugar de flags globales en `PlayerPrefs`. `DatabaseManager.cs:RunMigrations`.
- **[hecho]** `StartNewSeason` y el batch de ofertas maduradas en transacción.
  `DatabaseManager.cs:StartNewSeason`, `DashboardController.cs:ProcessMaturedOffers`.
- **[hecho]** Semilla determinista FNV-1a (antes `string.GetHashCode()`).
  `DatabaseManager.cs:StableHash`.
- **[hecho]** `GetTopPlayersByStat` con estadísticas reales de `player_game_stats`.
  `DatabaseManager.cs:GetTopPlayersByStat`.
- **[hecho]** Eliminado código muerto: `GameScreen.Settings`, `GameScreen.LegalNotice`,
  `SettingsController`, `Settings.uxml/uss`, `SQLiteAsync.cs`, campo `legalNoticeDocument`.
- **[hecho]** CursorManager duplicado eliminado de la escena; guard singleton endurecido;
  `ScreenManager` y `DatabaseManager` persisten con `DontDestroyOnLoad` propio.

- **[hecho]** Sign-and-trade en el trade UI: al recibir jugadores con `contract_years <= 1`,
  se ofrece el toggle "Sign & Trade" (extiende contrato +5%, activa hard cap del 1er apron y
  registra `trade_type="sign_and_trade"`). `MarketController.cs`.
- **[hecho]** Sign-and-trade de FA propio (NBA clásico, equipo origen): sección "FA RECIENTES
  (BIRD RIGHTS) — SIGN & TRADE" en el panel de traspaso; `ProcessSATrade` firma a un `IsOwnRecentFA`
  con su máximo Bird y lo traspasa de inmediato (dos `TradeData`: `free_agent` + `sign_and_trade`;
  receptor bajo hard cap). `TradeHelper.ValidateTrade`/`EvaluateTrade` reciben `teamASignSalaries`/
  `teamBSignSalaries` para valorar el nuevo salario firmado (sin descontar roster/nómina del que
  firma). La IA propone S&T por tu FA propio (`GenerateAITradeOffersForPlayer`→`ShowNextPendingTradeOffer`)
  y respeta `pendingSATIds` en sus fichajes. `MarketController.cs`, `DashboardController.cs`, `TradeHelper.cs`.
- **[hecho]** Validación de ofertas de FA: espacio salarial/excepciones/aprons se validan en la
  maduración (7 días) y el aviso en vivo se muestra al ajustar el salario (`MarketController.cs:UpdateFAWarning`).
- **[hecho]** Validación de ofertas al enviar: si el salario excede el máximo legal, se ajusta y se
  informa en el formulario (antes se recortaba en silencio). `MarketController.cs:SendFAOffer`,
  `RosterController.cs:SendOffer`.
- **[hecho]** `DatabaseManager` dividido en partial classes por dominio (5.714 → ~660 líneas en el
  archivo principal): `DatabaseManager.cs` (conexión, esquema, migraciones, ciclo de vida),
  `DatabaseManager.Teams.cs`, `.Players.cs`, `.Staff.cs` (empleados/préstamos/scouts),
  `.Manager.cs` (manager, league settings), `.Games.cs` (temporadas/partidos),
  `.Seeding.cs` (generación de jugadores, draft), `.Records.cs` (históricos/premios/alineaciones);
  las clases fila (POCOs) pasaron a `DatabaseRows.cs`. Split mecánico verificado
  (round-trip exacto + balance de llaves por segmento).
- **[hecho]** Clase base `UIScreenController` adoptada por las **41 pantallas** (antes cada una con su
  `OnEnable` duplicado). La base centraliza el fullscreen del root, `LoadData`/`RefreshHeader`
  comunes, la inyección del Sidebar/Header unificados, la navegación y el modal de configuración
  (`InitConfigModal`/`OpenConfigModal`/`CloseConfigModal`). `HeaderController`/`SidebarController`
  quedan como helpers `static` idempotentes. `MainMenuController` overrridea el modal de
  configuración (usa `style.display` frente a las clases CSS de la base). Validado con una
  simulación de temporada completa. `Assets/_TacticalFive/Scripts/Core/UIScreenController.cs`.
  *Nota: 12 pantallas (boot/menú/slots) overridean `RegisterCallbacks()` sin `base`.*

- **[hecho]** Transacción del día de partido: `ProcessGameDayRoutine` (`DashboardController.cs`)
  se divide en dos bloques atómicos — lote pre-partido (lesiones, fatiga, scouts, entrenos,
  traspasos IA, ofertas FA, psicólogo) en su propia transacción, y simulación + bookkeeping
  (partidos, química, noticias, transiciones de fase, payroll mensual, avance de fecha,
  premios, `UpdateSeason`) en una transacción de un solo frame (se eliminaron los
  `yield return null` del loop; los modales interactivos de alineación quedan fuera de
  transacción). En fallo: rollback, limpieza de `GameResultCache`/`_pendingRecoveredIds` y
  modal de error sin avanzar el día. `SavePlayInGames`/`SavePlayoffGames` usan
  `RunInTransaction` (savepoints) para anidar con la transacción del día y re-lanzar errores
  en lugar de tragárselos.    `DatabaseManager.Games.cs`.
- **[hecho]** Tests unitarios compilados y ejecutados en el editor (Test Runner, EditMode):
   17/17 en verde. `Assets/_TacticalFive/Tests/Editor/TradeHelperTests.cs` (15 tests:
   luxury tax progresivo, pick bonus, validación de traspasos por apron/hard cap, rating)
   y `EditModeSmokeTests.cs` (2 tests: accesibilidad de tipos y constantes salariales).
   *Nota: un test assembly (asmdef) NO puede referenciar la predefined assembly
   `Assembly-CSharp` (limitación de Unity 6; ni `references`, ni `optionalUnityReferences:
   ["TestAssemblies"]` la habilitan). Por eso los tests viven sin asmdef en una carpeta
   `Editor` → compilan en `Assembly-CSharp-Editor`, que sí ve `Assembly-CSharp` y NUnit.
   `GameSimulator.SimulateGame` depende de `DatabaseManager.Instance`
   (`GameSimulator.cs:159-196`), por lo que no es testeable en EditMode sin refactor.*
- **[hecho]** Opciones de contrato (team/player option) en renovaciones y fichajes FA:
  toggles TO/PO mutuamente excluyentes en el modal de oferta de `RosterController` y
  `MarketController`; al activarse, `guaranteed_years = max(0, years − 1)`. Los mensajes de
  resultado (inbox + modal resumen, `DashboardController.ProcessMaturedOffers`) formatean las
  opciones vía `FormatContractYears` (p. ej. `3 años (Team Option)`, `2 años + Player Option`).
  `RosterController.cs:SendOffer`, `MarketController.cs:SendFAOffer`, `DashboardController.cs`.
- **[hecho]** Maduración de opciones + re-firma de FA propio con Bird rights: al declinar una
  opción el jugador pasa a FA conservando `last_team_id` y `seasons_with_team`; `NewSeasonController`
  muestra un modal de re-firma (mercado + máximo con Bird rights) que envía una oferta diferida a 7
  días; `ProcessMaturedOffers` reasigna `team_id` al aceptarla (o la cancela si fichó por otro equipo).
  `MarketController.OnSignPlayer` respeta `IsOwnRecentFA` para conservar Bird rights al firmar FA propio.
  `DatabaseManager.cs` (migración `last_team_id`), `DatabaseManager.Records.cs`
  (`IsOwnRecentFA`), `NewSeasonController.cs`, `DashboardController.cs`.
- **[hecho]** Líderes de liga en SQL en lugar de LINQ en C#: `BuildSeasonStats`
   (`StatsController.cs`) pasa de N+1 queries + `GroupBy` en memoria a una sola
   agregación SQL (`GetSeasonPlayerStatsAggregates` en `DatabaseManager.Records.cs`,
   `JOIN player_game_stats + games` con `GROUP BY`, filtro por temporada regular
   jugada). Verificado: 348 jugadores y 17 columnas idénticas contra la lógica LINQ
   anterior en `save_1.db`. `GetTopPlayersByStat` (`DatabaseManager.Players.cs`)
    también migrado a SQL (agregación cross-season).
- **[hecho]** Trade Deadline con evento real: modal DEADLINE DAY en Feb 7 que intercepta
  el btnAction (IR AL MERCADO / CERRAR, una vez por temporada vía `_deadlineDayModalShown` +
  `_deadlineModalSeasonId`); rush IA con cooldown de traspasos reducido a 3-5 días en
  Feb 1-8 (`IsDeadlineWeek`), contenders (`IsTeamContender`) ofrecen picks extra, tag
  `[DEADLINE]` en títulos de ofertas IA; badge `⏳ ÚLTIMOS X DÍAS` en el header del Market
  durante la semana de deadline. `DashboardController.cs`, `MarketController.cs`,
  `Market.uxml/uss`, `Dashboard.uss`.
- **[hecho]** AI de GMs con estrategia por equipo: `enum TeamStrategy { Rebuild, Balanced, Contend }`
  (`GetTeamStrategy`/`BuildTeamStrategyCache`), cooldowns por equipo según estrategia
  (`_teamTradeCooldown`; Contend 6d, Rebuild 8d, Balanced 15d; 3-5 en deadline) y densidad
  (0.45/0.40/0.25) en `ProcessAITransfers`; fire sale de rebuild (`TrySellVeteran`, vende
  veteranos ≥30 por jóvenes/picks); contender busca upgrades OVR≤90 con pick futuro protegiendo
  jóvenes (<26, OVR≥82); ofertas al usuario según estrategia (`PickTradeTarget`/`BuildOfferPackage`);
  Star FA prioriza Contend > Balanced > Rebuild y Rebuild no ficha OVR≥85 (tankea el draft).
  `DashboardController.cs`.

- **[hecho]** Logros/trofeos del GM: catálogo de 28 logros (`AchievementCatalog`) en 6
  categorías (Primeros Pasos, Temporada, Jugador Premiado, Playoffs, Carrera, Mercado),
  persistidos por slot en `gm_achievements` con `INSERT OR IGNORE` idempotente.
  Detección en flujo de juego: `AchievementService.EvaluateGameDay` (partido/día,
  `DashboardController.cs:921`), `EvaluateSeasonEnd` (playoffs/campeonatos/premios),
  `EvaluateSignStarFA`/`EvaluateSignAndTrade`/`EvaluateTradeStar` (mercado),
  `EvaluateRecordBreak` (récords). Pantalla `Logros` (UXML/USS) con tabs por categoría,
  grid de 6 columnas y contador `X / total`; botón + contador en `Manager`; toast de
  desbloqueo en el Dashboard. `LogrosController.cs`, `AchievementService.cs`,
  `AchievementCatalog.cs`, `DatabaseManager.Achievements.cs`, `GmAchievementData.cs`.
- **[hecho]** Fix crítico en `gm_achievements`: el índice `IX_Achievements_Manager_Type`
  era UNIQUE solo sobre `manager_id` (impedía desbloquear más de un logro por partida;
  `first_win` jamás se persistía). Migración en `DatabaseManager.CreateTables` que hace
  `DROP INDEX IF EXISTS` y regenera el índice compuesto `UNIQUE(manager_id, type)`.
  `GmAchievementData.cs`, `DatabaseManager.cs`.
- **[hecho]** Toast de logros robusto: el consumo de la cola (`DashboardController.Update`)
  ya no depende de `IsAnyModalOpen()`; se muestra aunque quede un overlay abierto al
  terminar el día. Cursor hand + clic en los tabs de la pantalla Logros y navegación
  `SubmenuLogros` del sidebar (estaba sin handler en `UIScreenController.RegisterNavButtons`).
