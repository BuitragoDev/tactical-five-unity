# Plan de mejoras — Tactical Five

Estado verificado contra el código en la rama `crear-mejoras`. Las entradas marcan
`[hecho]` o `[pendiente]` con referencia `archivo:línea` cuando aplica.

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
  *Nota: no existe un flujo de "firmar FA externo y traspasarlo inmediatamente"; el S&T
  implementado extiende al jugador entrante como parte del traspaso.*
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
- **[hecho]** Clase base `UIScreenController` adoptada por las 37 pantallas (antes cada una con su
  `OnEnable` duplicado). La base centraliza el fullscreen del root, `LoadData`/`RefreshHeader`
  comunes, la inyección del Sidebar/Header unificados, la navegación y el modal de configuración
  (`InitConfigModal`/`OpenConfigModal`/`CloseConfigModal`). `HeaderController`/`SidebarController`
  quedan como helpers `static` idempotentes. `MainMenuController` overrridea el modal de
  configuración (usa `style.display` frente a las clases CSS de la base). Validado con una
  simulación de temporada completa. `Assets/_TacticalFive/Scripts/Core/UIScreenController.cs`.

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
- **[hecho]** Líderes de liga en SQL en lugar de LINQ en C#: `BuildSeasonStats`
   (`StatsController.cs`) pasa de N+1 queries + `GroupBy` en memoria a una sola
   agregación SQL (`GetSeasonPlayerStatsAggregates` en `DatabaseManager.Records.cs`,
   `JOIN player_game_stats + games` con `GROUP BY`, filtro por temporada regular
   jugada). Verificado: 348 jugadores y 17 columnas idénticas contra la lógica LINQ
   anterior en `save_1.db`. `GetTopPlayersByStat` (`DatabaseManager.Players.cs`)
   también migrado a SQL (agregación cross-season).
