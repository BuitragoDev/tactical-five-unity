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

## Pendiente

1. **Transacción del día de partido** — no implementada: el procesamiento es una corrutina
   con `yield return` dentro del loop de partidos (`DashboardController.cs`), y una
   transacción abierta entre frames mantendría el lock. Pendiente de rediseño.
2. **Refactor grande** — líderes de liga en SQL en lugar de LINQ en C#.
3. **Tests unitarios** — `Assets/_TacticalFive/Tests/Editor/` con `TradeHelperTests` +
   `EditModeSmokeTests`. Pendiente: compilar/ejecutar en el editor.
   *Nota: un test assembly (asmdef) NO puede referenciar la predefined assembly
   `Assembly-CSharp` (limitación de Unity 6; ni `references`, ni `optionalUnityReferences:
   ["TestAssemblies"]` la habilitan). Por eso los tests viven sin asmdef en una carpeta
   `Editor` → compilan en `Assembly-CSharp-Editor`, que sí ve `Assembly-CSharp` y NUnit.
   `GameSimulator.SimulateGame` depende de `DatabaseManager.Instance`
   (`GameSimulator.cs:159-196`), por lo que no es testeable en EditMode sin refactor.*
