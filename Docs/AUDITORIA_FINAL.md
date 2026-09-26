# AUDITORÍA FINAL — Tactical Five

> **Documento vivo de trabajo.** Aquí está por escrito TODO lo que hay que arreglar, en qué orden y por qué.
> Cada hallazgo tiene su propia ficha con estado marcable (`[ ]` pendiente → `[x]` hecho).
> Úsalo como checklist maestro: marca `[x]` al terminar, y anota en la **§12 Bitácora** la fecha/commit.
>
> - **Auditoría realizada sobre:** `main` @ `d565623e` (post-merge de `minutos-ui`).
> - **Rama de trabajo de la auditoría:** `auditoria-final`.
> - **Alcance:** 152 archivos `.cs` (~62.400 líneas), 85 tests EditMode, 47 pantallas, 1 escena, 9 partials de `DatabaseManager`.
> - **Nota sobre líneas:** todas las referencias `archivo:línea` corresponden al commit auditado. Si el código se mueve, **vuelve a localizar por grep** antes de tocar (los IDs y descripciones siguen valiendo).

---

## Leyenda

**Severidad**

| Etiqueta | Significado |
|---|---|
| **CRÍTICO** | Corrupción/pérdida de datos, crashes graves, estado irrecuperable (requiere reiniciar), exploits que rompen el juego |
| **ALTO** | Rompe funcionalidades importantes o produce resultados incorrectos |
| **MEDIO** | Problema real con impacto limitado |
| **BAJO** | Calidad, mantenimiento, casos poco frecuentes |

**Tipo**

| Etiqueta | Significado |
|---|---|
| **BUG** | Fallo confirmado en el código |
| **RIESGO** | Mecanismo confirmado, consecuencia extrema no reproducida (falta de tiempo/entorno) |
| **MEJORA** | Recomendación de calidad/robustez, no un fallo |

**Verificación**

| Etiqueta | Significado |
|---|---|
| **[V]** | Re-verificado leyendo el código directamente durante la auditoría |
| **[A]** | Verificado por análisis de código con líneas citadas (no relectura línea a línea del revisor principal) |

---

## 0. Tablero de progreso

| Bloque | Hallazgos | Hechos |
|---|---|---|
| CRÍTICOS | 9 | 9 |
| ALTOS | 21 | 20 (quedan `UI-04` parcial y `CON-05`) |
| MEDIOS | ~32 | 11 |
| BAJOS | ~12 | 3 |
| Tests propuestos | 25 | 13 (98 tests en total) |

> **Fase 1 completada.** **Fase 2 muy avanzada:** integridad de simulación (`SIM-01/02/03/04/12/13`), esquema (índices, `[Table]`, `SEA-12`, ids monótonos), `season_records` (`SEA-11` parcial), economía (`ECO-03/04/05/07/08`, `SEA-10`), rendimiento (`REN-01`, `CON-07`, `CON-08`), UI (`UI-03/05/06`), limpieza de código muerto y corrección de docs (`QA-07/08`), y **tests de persistencia** (`GameSaveManagerTests`, 7) con `BaseDirOverride`.
> **Fase 2 cerrada salvo dos menores diferidos a propósito:** `UI-04` (re-bind del scrollbar *dentro* de una visita en 7 pantallas; el `BindScrollBar` está duplicado y el riesgo de romper el scroll supera al beneficio: hacerlo al unificar la scrollbar compartida) y `CON-05` (cachear standings por día; el coste real es ~1.230 filas/día, impacto bajo). Los tests de `InitSaveSlot`/`StartNewSeason` necesitan una `DatabaseManager` viva en EditMode (se abordarán si se añade un arnés de tests con DB).

### Top 10 por daño/esfuerzo (orden recomendado de ataque)

1. `SAV-01` pérdida total de partidas (arreglo barato, daño máximo).
2. `SEA-01` estadísticas históricas al doble (cambio de 1 línea).
3. `UI-01` + `CON-01` soft-lock del Dashboard y transacción entre frames.
4. `SAV-02` escritura en la DB equivocada desde el hilo de fondo.
5. `ECO-02` traspasos sin revalidar + `ECO-01` buyout gratis.
6. `SAV-04` migraciones no atómicas + `SAV-03` metadata antes de crear la partida.
7. `SEA-02` hard cap de por vida + `SEA-03`/`SEA-05` soft-locks de temporada.
8. `SEA-06` colisión de nombres en el histórico (HOF falso).
9. `ECO-03`/`ECO-04`/`ECO-05` reglas de FA y contabilidad.
10. `SIM-01`/`SIM-02`/`SIM-04` integridad del motor de simulación.

---

## 1. Resumen ejecutivo

**Estado general.** Proyecto funcional y notablemente completo para un desarrollo en solitario: ciclo de temporada completo, draft con protecciones/swaps, playoffs, economía con cap/aprons, G-League, HOF, dorsales, logros, play-by-play. La arquitectura por capas es correcta y las reglas puras están razonablemente aisladas (bien).

**Diagnóstico.** El juego está en fase *"funciona en la partida feliz"*: resiste el flujo normal, pero **no resiste cierres inesperados, operaciones interrumpidas ni 20-50 temporadas**. Los tres ejes de deuda son:

1. **Atomicidad**: muchas operaciones de dominio (trades, buyout, renovaciones, pre-pasos de temporada) se ejecutan como escrituras sueltas sin transacción. Un fallo a mitad deja estados a medias.
2. **Ciclo de vida async**: tareas de fondo sin cancelación, transacción diaria abierta entre frames, `_dbField` repuntado desde un worker, busy timeout de 1 s sin mutex.
3. **Integridad de datos a largo plazo**: históricos indexados por nombre, doble archivado, ids reutilizados, ausencia de índices/constraints, tablas que crecen sin poda.

**Riesgos principales** (detalle en §3):

- Pérdida total de partidas por `saves.json` corrupto → `CleanupAllOrphanDbs` borra todas las DBs. **[V]**
- Escritura en el `.db` equivocado desde el hilo del Editor. **[V]**
- Totales de carrera/HOF duplicados cada temporada. **[V]**
- Buyout gratis e ilimitado + deuda de stretch borrada al cambiar de temporada. **[V]**
- Traspasos de usuario/IA aceptados sin revalidar (plantillas de 18+, salary matching roto). **[V]**
- Soft-lock del Dashboard (requiere reiniciar) al navegar durante la simulación. **[V]**
- Soft-locks de temporada (calendario sin guardar marcado como generado; play-in sin seeds).

**Oportunidades.** Sin cambiar de motor, el proyecto ya tiene la materia prima para un manager profundo: personalidades/relaciones, fog-of-war, `TeamStrategy` de la IA, historial/HOF, Editor de liga, play-by-play. La deuda es de robustez, no de diseño.

**Veredicto y regla de oro.** **No añadir contenido de gameplay hasta cerrar la Fase 1.** Con ~1-2 semanas enfocadas, el juego pasa de "frágil" a "sólido para partidas largas".

---

## 2. Arquitectura actual y dependencias

```
UI Toolkit (47 UIDocument, 1 escena MainMenu.unity)
  ScreenManager.GoTo(GameScreen) → ShowOnly() (SetActive x47)
  UIScreenController (base) → OnEnable: MakeFullscreen → CacheReferences → LoadData → RegisterCallbacks → Refresh
        │
        ├── 47 controllers delgados (toda la lógica de UI en OnEnable)
        │
LÓGICA (static puro)                          SINGLETONS
  GameSimulator (posesiones, ~1230 ln)          ScreenManager
  TradeHelper (cap/aprons/tax/valoración)       DatabaseManager (9 partials)
  DraftGenerator, ScheduleGenerator,            GameSaveManager (estático)
  PlayoffsGenerator, GLeague*,                  AudioManager, CursorManager
  AchievementService, ObjectiveHelper, Stats
        │
DATOS (SQLite sqlite-net, 1 .db por slot)
  DatabaseManager: CreateTables + RunMigrations + Seed + CRUD (150+ métodos)
  save_{n}.db + saves.json + template.db
  Hilos de fondo: RunInBackground / RunInBackgroundAsync
    (WAL + AsyncLocal ambient connection + System.Random thread-static)
  Comunicación sin event bus: GameResultCache (static), mensajes DB, PlayerPrefs
```

**Acoplamientos que importan**

- **`DashboardController` es el hub real** (día, simulación, economía, IA, transiciones de fase, ~8.000 líneas). Es el punto más frágil y con más responsabilidades mezcladas.
- **`StartNewSeason`** (`DatabaseManager.Records.cs`, ~560 líneas, 1 transacción) toca casi todas las tablas: segundo punto crítico.
- **`DatabaseManager`** es un facade gigante del que depende todo → cualquier cambio de esquema impacta en decenas de pantallas.
- **`GameSimulator`** es autónomo (bien), pero se invoca dentro de transacciones con `yield` (mal).

**Conclusión arquitectónica.** La separación por capas es correcta. Los problemas son: (a) la UI ejecuta operaciones multi-tabla sin transacción; (b) el modelo async no tiene mutex ni cancelación.

---

## 3. Hallazgos

> Formato de ficha: **Problema** (qué pasa), **Repro** (cómo llegar), **Por qué** (causa raíz), **Consecuencia**, **Solución** (qué hacer), **Impl.** (notas/archivos a tocar), **Test**.

### 3.1 CRÍTICOS

---

#### `SAV-01` · Pérdida total de todas las partidas por `saves.json` corrupto
- **Estado:** `[x]` hecho
- **Severidad:** CRÍTICO · **Tipo:** BUG · **Verificación:** **[V]**
- **Sistema:** Persistencia / pantalla Cargar partida
- **Archivos:** `Assets/_TacticalFive/Scripts/Data/GameSaveManager.cs:47-66` (`CleanupAllOrphanDbs`), `:68-105` (`GetAllSlots`), `Assets/_TacticalFive/Scripts/UI/LoadGameController.cs:63`
- **Problema:** si `saves.json` no se puede leer/parsear, `GetAllSlots()` deja la lista **vacía** (el `catch` solo hace `Debug.LogWarning`). `CleanupAllOrphanDbs()` considera huérfana toda `.db` sin metadatos válidos y **la borra** (`File.Delete`). Se ejecuta al entrar en Cargar Partida.
- **Repro:** truncar o borrar `saves.json` (o morir durante `File.WriteAllText`), abrir "Cargar partida" → se eliminan todas las `save_*.db`.
- **Por qué:** el metadata es la única fuente de verdad y un fallo de lectura se interpreta como "no existe ninguna partida".
- **Consecuencia:** pérdida total e irreversible de todas las carreras.
- **Solución:**
  1. En error de parseo/lectura, **saltar la limpieza** (no borrar nunca por metadata ilegible).
  2. Escribir `saves.json` de forma atómica: escribir en `.tmp` + `File.Replace`/`Move`, y conservar `saves.json.bak`.
  3. Añadir constante `ReadFailed` al resultado de `GetAllSlots` para que los llamadores distingan "sin partidas" de "no pude leer".
- **Impl.:** `GameSaveManager.SaveSlotInfo` (escritura atómica), `GetAllSlots` (flag/out param), `CleanupAllOrphanDbs` (bail out si la lectura falló). Revisar también `GameSaveManager.cs:26-36` (borrado por slot).
- **Test:** `SaveSlot_CorruptSavesJson_DoesNotDeleteDatabases`.

---

#### `SAV-02` · `_dbField` repuntado desde el hilo de fondo (Editor) sin `finally`
- **Estado:** `[x]` hecho
- **Severidad:** CRÍTICO · **Tipo:** BUG · **Verificación:** **[V]**
- **Sistema:** Editor / plantilla / conexión DB
- **Archivos:** `Assets/_TacticalFive/Scripts/Data/DatabaseManager.cs:24-28` (setter), `:186-200` (`BuildTemplateDatabaseInBackground`), `:166-179` (`EnsureTemplateDb`), `:202-217` (`InitTemplateSession`); `Assets/_TacticalFive/Scripts/UI/EditorController.cs:359-374`
- **Problema:** `private SQLiteConnection _db { get => _ambientDb.Value ?? _dbField; set => _dbField = value; }`. `BuildTemplateDatabaseInBackground` corre en `Task.Run` y hace `_db = new SQLiteConnection(dbPath)` **sobre el campo compartido**; no hay `try/finally`, así que si `CreateTables`/`RunMigrations`/`SeedStaticDataIfNeeded` lanza, `_dbField` queda apuntando a la plantilla o a una conexión ya cerrada. `EnsureTemplateDb` tiene el mismo patrón. `InitTemplateSession` llama `RunMigrations()` **sin** `CreateTables()`.
- **Repro:** pulsar RESET/rebuild del template en el Editor y seguir usando la UI; o forzar excepción en el seed.
- **Por qué:** el modelo de "conexión ambient" pensado para background se rompió al reutilizar el setter de la conexión principal; el `lock` solo serializa builds de template entre sí (no contra `InitSaveSlot`/`EnsureTemplateDb`).
- **Consecuencia:** la UI lee/escribe en el `.db` equivocado; fuga de conexión; sesión apuntando a conexión cerrada.
- **Solución:** construir la plantilla en una **conexión local** (pasar `conn` a los helpers o usarla vía ambient), y restaurar en `finally` con `try/catch`. Nunca repuntar `_dbField` desde un worker. Añadir `CreateTables()` antes de `RunMigrations()` en `InitTemplateSession`.
- **Impl.:** extraer una sobrecarga `CreateTables(SQLiteConnection)`, `RunMigrations(SQLiteConnection)`, `SeedStaticDataIfNeeded(SQLiteConnection)`, o setear `_ambientDb.Value` dentro del `Task.Run` y restaurar en `finally`.
- **Test:** `TemplateBuild_ThrowsMidway_DoesNotRepointMainConnection` (requiere poder inyectar ruta).

---

#### `SEA-01` · Estadísticas históricas archivadas dos veces por temporada (totales ×2)
- **Estado:** `[x]` hecho
- **Severidad:** CRÍTICO · **Tipo:** BUG · **Verificación:** **[V]**
- **Sistema:** Historial / HOF / récords
- **Archivos:** `Assets/_TacticalFive/Scripts/UI/DashboardController.cs:1518`; `Assets/_TacticalFive/Scripts/Data/DatabaseManager.Records.cs:2669-2672`; función acumulativa `Records.cs:1180-1290` (`hist.games += ss.games; hist.total_points += ...`)
- **Problema:** `UpdateHistoricalPlayerStatsFromSeason(seasonId, managerId)` se llama **dos veces** para la misma temporada: al cerrar los playoffs en el Dashboard (bloque `phase == "finished"`) y otra vez al inicio de `StartNewSeason` (que archiva `oldSeasonId`). La función **suma** a las filas existentes.
- **Repro:** jugar una temporada completa y avanzar a la siguiente.
- **Por qué:** dos caminos (transición de fase en UI y rollover en DB) asumen cada uno que es el único archivador.
- **Consecuencia:** totales de carrera ~2×, inducción al HOF y umbrales de récords disparados antes de tiempo → corrupción permanente del legado.
- **Solución:** eliminar una de las dos llamadas (recomendado: la de `StartNewSeason`, ya que el Dashboard la hace en el momento `finished`) **o** hacerla idempotente con un flag por `(season_id)` en `schema_migrations`/tabla de control.
- **Impl.:** borrar `Records.cs:2672` (y el `oldSeason` si queda sin uso) o añadir guarda `IsMigrationApplied($"hist_archived_{seasonId}")`. Decidir cuál es el punto canónico y documentarlo en `Docs/SAVE_SYSTEM.md`.
- **Test:** `StartNewSeason_ArchivesHistoricalStatsExactlyOnce`.

---

#### `ECO-01` · Buyout gratis (no debita presupuesto) y la deuda de stretch se borra en el rollover
- **Estado:** `[x]` hecho
- **Severidad:** CRÍTICO · **Tipo:** BUG · **Verificación:** **[V]**
- **Sistema:** Economía / contratos
- **Archivos:** `Assets/_TacticalFive/Scripts/UI/RosterController.cs:1589-1649`; mismo patrón en `Assets/_TacticalFive/Scripts/UI/DashboardController.cs:3782-3812`; borrado de `finance_records` en `DatabaseManager.Records.cs:3058`
- **Problema:** `ConfirmBuyout()` libera al jugador (`team_id=0`, flags a 0), calcula `remainingSalary = salary * contract_years`, `stretchYears = contract_years * 2`, e inserta `finance_records` de tipo `TYPE_BUYOUT` con `season_id` actual. **Nunca** hay `team.budget -= ...` / `UpdateTeamBudget(...)`. Además `StartNewSeason` hace `DELETE FROM finance_records`, así que la deuda multi-año desaparece al cambiar de temporada.
- **Repro:** rescindir a un jugador caro → el presupuesto no baja y al año siguiente no queda rastro.
- **Por qué:** el buyout se implementó como "solo registro contable", pero la contabilidad de temporada se purga en el rollover.
- **Consecuencia:** exploit económico (buyouts ilimitados y gratis), nóminas irreales, mecánica de presupuesto inútil.
- **Solución:**
  1. Debitar el tramo anual del stretch del `budget` al ejecutar el buyout (`UpdateTeamBudget`).
  2. Modelar la deuda muerta como obligación **persistente** (tabla propia `dead_money` con `team_id`, `season_id`, `amount`, o un `finance_record` que **no** se borre en el rollover), y que `ProcessMonthlyPayroll` la cobre.
- **Impl.:** añadir el débito en `ConfirmBuyout` y `DashboardController.cs:3782-3812`; decidir persistencia (preferible tabla nueva + `CreateTable` + migración `PRAGMA table_info`/`schema_migrations`). Ojo con `contract_years*2 == 0` → usar `Mathf.Max(1, ...)` (ver `UI-06`).
- **Test:** `Buyout_DebitsBudget_AndPersistsDeadMoney`.

---

#### `ECO-02` · Traspasos del usuario y aceptación de ofertas IA sin revalidar
- **Estado:** `[x]` hecho
- **Severidad:** CRÍTICO · **Tipo:** BUG · **Verificación:** **[V]**
- **Sistema:** Traspasos
- **Archivos:** `Assets/_TacticalFive/Scripts/UI/MarketController.cs:1869-1998` (`OnConfirmTrade`), `:1707` (única llamada a `ValidateTrade`); `Assets/_TacticalFive/Scripts/UI/DashboardController.cs:5919-6112` (aceptar oferta IA), `:5954-5956`; `MarketController.cs:1884-1907` (S&T)
- **Problema:** `TradeHelper.ValidateTrade` solo se ejecuta en `CheckTradeStatus` (previsualización). `OnConfirmTrade` muta equipos/jugadores/picks directamente. La rama de S&T **extiende contratos después** de la validación (`s.salary = newSalary`, `contract_years = newYears`) y activa `first_apron_hard_capped`, sin recalcular el matching. La aceptación de ofertas IA no valida nada.
- **Repro:** con 16 jugadores, generar/recibir una oferta 2x1 (se genera válida), fichar un FA hasta 17, aceptar → 18 jugadores. O activar S&T con extensión que rompe el hard cap.
- **Por qué:** la validación se trató como UI (feedback) en lugar de invariante de dominio.
- **Consecuencia:** plantillas >17, salary matching roto, infracción de apron/hard cap; estados que el resto del código no contempla.
- **Solución:** re-ejecutar `ValidateTrade` (incluyendo `teamASignSalaries`/`teamBSignSalaries` del S&T) **dentro** de `OnConfirmTrade` y del handler de aceptación, justo antes de mutar; si hay errores → modal + abortar. Mover toda la mutación a un único helper `ExecuteTrade` transaccional.
- **Impl.:** `MarketController.OnConfirmTrade`, `DashboardController` (botón aceptar de `ShowTradeOfferModal`). Compartir helper con `DashboardController.TryExecuteTrade` (IA) que ya calcula salarios firmados.
- **Test:** `Trade_Confirm_Revalidates_RejectsOverRoster17`; `SignAndTrade_Extension_RespectsHardCap`.

---

#### `UI-01` · Soft-lock del Dashboard: `_isLoading` no se resetea al navegar durante la simulación
- **Estado:** `[x]` hecho
- **Severidad:** CRÍTICO · **Tipo:** BUG · **Verificación:** **[V]**
- **Sistema:** UX / navegación
- **Archivos:** `Assets/_TacticalFive/Scripts/UI/DashboardController.cs:721` (`OnActionClicked` guard), `:223-233` (`OnDisable` → `StopAllCoroutines`), `:2255-2285` (`ShowLoading`/`HideLoading`). `_isLoading` solo se asigna en `:2260/2261/2281`.
- **Problema:** `ShowLoading()` pone `_isLoading = true` y deshabilita `_btnAction`; solo `HideLoading()` lo revierte. `OnDisable()` mata las corrutinas (al cambiar de pantalla con `ScreenManager.ShowOnly`) pero **no** resetea `_isLoading` ni rehabilita el botón; `OnEnable` tampoco. La sidebar solo se bloquea en fast-sim (`:1885`), no en la simulación normal.
- **Repro:** avanzar un día y, durante el batch de fondo, clicar cualquier navegación (p. ej. Plantilla); volver al Dashboard → el botón de avanzar no responde nunca más.
- **Por qué:** el flag de "estoy ocupado" vive en la instancia del controller, que se deshabilita pero no se destruye, y su ciclo de vida no se cierra.
- **Consecuencia:** **soft-lock que exige reiniciar el juego**; además la pre-fase (lesiones/fatiga/IA) ya puede estar commiteada → día a medias.
- **Solución:** resetear `_isLoading = false` y `_btnAction.SetEnabled(true)` en `OnDisable` (cuidando no dejar una corrutina a medias, ver `CON-01`), y bloquear la navegación/config durante la simulación normal igual que en fast-sim.
- **Impl.:** `OnDisable`/`OnEnable`, `OnActionClicked`, `SetSidebarNavigationEnabled(true)` también en el camino normal; considerar un `IsSimulating` global en `ScreenManager`.
- **Test:** `Dashboard_OnDisable_DuringSim_ResetsIsLoading`.

---

#### `CON-01` · La transacción del día se mantiene abierta a través de `yield return null` (sin `finally`)
- **Estado:** `[x]` hecho
- **Severidad:** CRÍTICO · **Tipo:** BUG · **Verificación:** **[V]** (patrón ya documentado en `TODO_TECHNICAL_DEBT.md`)
- **Sistema:** Transacciones / async
- **Archivos:** `Assets/_TacticalFive/Scripts/UI/DashboardController.cs:1196` (`BeginTransaction`), `:1226` y `:1246` (`if (fastSim) yield return null`), `:1580` (`Commit`), `:226` (`StopAllCoroutines`); `Assets/_TacticalFive/Scripts/Data/SQLite.cs:1752-1754` (throw si ya hay transacción)
- **Problema:** entre `Begin` y `Commit` hay `yield return null` en fast-sim. Solo existe `catch → Rollback`, **sin `finally`**. Si la corrutina se aborta (deshabilitar Dashboard, `StopAllCoroutines`, "config → Menú principal" durante fast-sim, ver `UI-02`), la conexión queda con `_transactionDepth == 1`.
- **Repro:** simulación rápida y navegar/salir en un frame de yield; después intentar cualquier `BeginTransaction` → `InvalidOperationException`.
- **Por qué:** se quiso dar feedback por frame sin sacar el trabajo de simulación del bloque transaccional.
- **Consecuencia:** transacción colgada → todas las escrituras siguientes fallan o se cuelan en la transacción del día; día a medias; requiere reinicio.
- **Solución:** **nunca `yield` entre `Begin` y `Commit`**. Bufferizar los resultados por frame fuera de la transacción, o dividir en transacciones por partido/día. Añadir `try/finally` con rollback defensivo si el patrón corrutina se mantiene.
- **Impl.:** reestructurar `ProcessGameDayRoutine`/`FastSimRoutine`. Añadir guarda `if (_db.IsInTransaction) rollback` o helper `SafeTransaction`.
- **Test:** `FastSim_AbortMidway_DoesNotLeaveOpenTransaction`.

---

#### `SIM-04` · Empate persistido tras 5 prórrogas (resultado imposible)
- **Estado:** `[x]` hecho
- **Severidad:** CRÍTICO · **Tipo:** BUG · **Verificación:** **[V]**
- **Sistema:** Simulación
- **Archivos:** `Assets/_TacticalFive/Scripts/GameSimulator.cs:142-158` (bucle OT), `:178-179` (asignación de marcador)
- **Problema:** `while (homeTotal == awayTotal && otCount < 5)`. Tras la 5.ª prórroga el bucle sale por `otCount < 5` **aunque sigan empatados**; no hay desempate posterior y se persiste `home_score == away_score`. El resto del código no trata el empate igual: `StandingsHelper.cs:42-55` (derrota para ambos), `PlayoffsGenerator.cs:243-252` (`AdvancePlayoffSeries` da la serie al visitante), `:429` (`GetSeriesWinner` con `>=` da la serie al local).
- **Repro:** dos equipos muy igualados que empatan 5 prórrogas seguidas (raro pero alcanzable en miles de partidos).
- **Por qué:** se capó la OT por seguridad sin definir el desempate.
- **Consecuencia:** resultado imposible; un 7.º partido empatado deja la serie 3-3 sin partidos y **atasco de bracket**; standings con W+L ≠ partidos.
- **Solución:** garantizar ganador (seguir simulando OT sin límite, o desempate de 1 punto) y unificar las comparaciones de tie-break (`>=`/`>`) en standings y playoffs.
- **Impl.:** `GameSimulator.SimulateGame` (bucle OT + fallback `<2` jugadores `:88-89` que también puede empatar), `StandingsHelper`, `PlayoffsGenerator.GetSeriesWinner`.
- **Test:** `SimulateGame_Overtime_AlwaysProducesWinner`.

---

#### `SAV-03` · El metadata del slot se escribe antes de que la partida exista
- **Estado:** `[x]` hecho
- **Severidad:** CRÍTICO · **Tipo:** BUG · **Verificación:** **[V]**
- **Sistema:** Persistencia / nueva partida
- **Archivos:** `Assets/_TacticalFive/Scripts/UI/PreseasonController.cs:426-494`
- **Problema:** `GameSaveManager.SaveSlotInfo(new SaveSlotInfo { exists = true, managerName = ... })` se ejecuta **antes** de `CreateSeason(...)` y de generar el calendario (`ScheduleGenerator.GenerateSchedule`). Si el proceso muere o algo lanza entre medias, el slot figura como partida válida pero no tiene temporada activa/calendario.
- **Repro:** matar el juego en el primer "Continuar" de pretemporada.
- **Por qué:** se quiso "reservar" el slot antes de trabajar en él.
- **Consecuencia:** partida cargable pero rota (`GetActiveSeason` null, sin partidos), `FindNextGameDay == 0` → soft-lock.
- **Solución:** escribir el metadata **al final** (tras temporada + calendario + G-League), o añadir `initialized` a `SaveSlotInfo` y exigirlo para listar/cargar el slot.
- **Impl.:** `PreseasonController.cs`, `SaveSlotInfo.cs`, `GameSaveManager.GetAllSlots`/`CleanupAllOrphanDbs` (validez por `initialized`, no por `managerName`; ver `QA-03`/`QA-04`).
- **Test:** `InitSaveSlot_InterruptedBeforeSeason_NotListedAsValid`.

---

### 3.2 ALTOS

---

#### `SAV-04` · Migraciones no atómicas, `user_version` prematuro y bloques multi-columna no auto-sanables
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `Assets/_TacticalFive/Scripts/Data/DatabaseManager.cs:330-1055` (p. ej. `:477-486` players, `:505-513` offers), `:1070-1081` (`MarkMigrationApplied`)
- **Problema:** `RunMigrations` no está en transacción; hace `PRAGMA user_version = 2` **antes** de migrar; varios bloques comprueban una sola columna antes de añadir 3 (`guaranteed_years` + `has_team_option` + `has_player_option`): si el 2.º `ALTER` falla, el bloque no se reintenta → `no such column` al leer la tabla. `MarkMigrationApplied` se traga errores → migraciones destructivas (`DELETE FROM draft_picks`) pueden re-ejecutarse.
- **Repro:** error de IO/`SQLITE_BUSY` a mitad de un bloque; o guardar antes de `MarkMigrationApplied`.
- **Consecuencia:** esquema parcial permanente; saves antiguas irrecuperables; picks clobberados.
- **Solución:** envolver `RunMigrations` en transacción; comprobar **cada** columna por separado; `user_version` al final; marcar la migración en la misma transacción que su cambio.
- **Impl.:** `DatabaseManager.cs` (bloque de migraciones); patrón seguro: helper `AddColumnIfMissing(table, column, ddl)`.
- **Test:** `RunMigrations_IsIdempotent_AndRecoversFromPartialColumns`.

---

#### `SAV-05` · `InitSaveSlot` sin manejo de errores (DB corrupta/bloqueada/solo lectura)
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `Assets/_TacticalFive/Scripts/Data/DatabaseManager.cs:55-110`; llamadores `MainMenuController.cs:324-345`, `LoadGameController.cs:441-448`
- **Problema:** sin `try/catch`; `_activeSaveSlot` ya está asignado y `_db` puede quedar no-null/cerrada.
- **Consecuencia:** excepción sin mensaje al jugador; sesión en estado indefinido.
- **Solución:** `try/catch` + preflight `PRAGMA integrity_check`; en fallo, limpiar estado, avisar y ofrecer eliminar/restaurar.
- **Test:** `InitSaveSlot_CorruptDb_ShowsErrorAndRecovers`.

---

#### `SAV-06` · `CloneFromTemplate` no atómico y el test de "partida nueva" impide recompletarlo
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `Assets/_TacticalFive/Scripts/Data/DatabaseManager.cs:238-265`, `:78-89`
- **Problema:** ~16 `InsertAll` sin transacción externa; `isNewGame` se decide con `TeamData.Count() > 0`, así que un clon parcial **no** se vuelve a sembrar.
- **Consecuencia:** slot a medias detectado como válido.
- **Solución:** una transacción para clon + seed; marcador de inicialización (enlaza con `SAV-03`).
- **Test:** `InitSaveSlot_InterruptedClone_IsDetectedAndRebuilt`.

---

#### `SAV-07` · Pares delete+insert sin transacción (lineup, pretemporada, G-League)
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[V]** (AutoSeedLineup)
- **Archivos:** `DatabaseManager.Records.cs:3515-3548` (`EnsureLineupSeeded`), `:3551-3559` (`AutoSeedLineup`); `DatabaseManager.Games.cs:84-98` (`SavePreseasonGames`); `DatabaseManager.GLeague.cs:12-58` (`EnsureGLeagueSeeded`)
- **Problema:** borran y reinsertan sin transacción. `EnsureGLeagueSeeded` solo comprueba `Count()==0`, por lo que un seed parcial queda permanente.
- **Consecuencia:** lineup/pretemporada/G-League a medias tras un fallo.
- **Solución:** `RunInTransaction` en cada par; sembrar por recuento esperado (30 filiales, 11 prospects/filial), no por `Count()==0`.
- **Test:** `SeedGLeague_PartialSeed_CompletesToExpectedCounts`.

---

#### `SEA-02` · `first_apron_hard_capped` nunca se resetea entre temporadas
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[V]**
- **Archivos:** se escribe en `DashboardController.cs:2571`, `:5956`; `MarketController.cs:1901`, `:2820`. **No existe ningún reset** (grep global).
- **Problema:** la regla real es anual; en el juego es permanente.
- **Consecuencia:** un equipo que usó el NT-MLE queda hard-capped de por vida → bloquea traspasos/FA para siempre (el usuario lo percibe como bug).
- **Solución:** en `StartNewSeason`, `UPDATE teams SET first_apron_hard_capped = 0` (o `=0` en el loop de equipos).
- **Test:** `StartNewSeason_ResetsHardCapFlag`.

---

#### `SEA-03` · Calendario sin guardar marcado como generado (soft-lock de temporada)
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `DatabaseManager.Games.cs:109-125` (`SaveRegularSeasonGames` traga la excepción), `:131-158` (`GenerateSchedule`), `UI/PreseasonController.cs:474-494` (`season.generated = 1; phase = "regular"`)
- **Problema:** si el guardado falla, hay 0-partidos parciales pero la temporada se marca generada y pasa a `regular`. `FindNextGameDay` devuelve 0 → el bloque de transición no corre.
- **Consecuencia:** soft-lock (nunca avanza de temporada).
- **Solución:** `SaveRegularSeasonGames` debe devolver éxito/`throw`; marcar `generated=1` solo tras verificar el recuento persistido (`82*15` + All-Star). Igual para el calendario G-League (`PreseasonController.cs:479-489`).
- **Test:** `Schedule_Always82GamesPerTeam`; `Preseason_ScheduleSaveFails_DoesNotMarkGenerated`.

---

#### `SEA-04` · Pre-pasos de nueva temporada fuera de la transacción del rollover
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `UI/NewSeasonController.cs:314-346` (opciones), `:356-381` (decremento de contratos), `:399-440` (`CheckRosterAndStart` → cambia `_manager.team_id`), `:416-425` (llamada a `StartNewSeason`)
- **Problema:** contratos/opciones/equipo se mutan **antes** de la transacción de `StartNewSeason`. Si el rollover falla, no hay rollback de esos cambios.
- **Consecuencia:** doble decremento al reintentar, FA un año antes, manager en el equipo nuevo con la temporada vieja activa.
- **Solución:** mover resolución de opciones y decremento de contratos **dentro** de `StartNewSeason` (ya transaccional); el cambio de equipo del manager también (o restaurarlo en el `catch`).
- **Test:** `NewSeason_FailedRollover_LeavesContractsAndManagerUnchanged`.

---

#### `SEA-05` · Soft-lock en play-in si `GeneratePlayoffs` devuelve 0
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `UI/DashboardController.cs:1378-1391`; `Core/PlayoffsGenerator.cs:169` (`if (confSeeds.Count < 8 || Any(t => t == 0)) continue;`)
- **Problema:** si una conferencia no puede resolver 8 seeds, no se inserta nada y nunca se pone `phase="playoff"` (solo `LogError`). La NBA no tiene el "force-complete" que sí tiene la G-League.
- **Consecuencia:** soft-lock de temporada; el usuario no puede avanzar.
- **Solución:** si `allPlayInPlayed && created == 0` → completar el bracket con diagnóstico o abortar la temporada; validar 30 equipos / 15 por conferencia al arrancar.
- **Test:** `Playoffs_MissingSeeds_DoesNotSoftLock`.

---

#### `SEA-06` · Histórico indexado por nombre → colisión con leyendas sembradas
- **Estado:** `[ ]` pendiente
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[V]**
- **Archivos:** `DatabaseManager.Records.cs:1075-1083` (`GetHistoricalPlayerStats(first_name, last_name)`), `:1192-1204`, `:1256-1290` (`hist.games += ...`); `HistoricalPlayerStatsSeeder.cs:15`; `NamePools.cs:30`
- **Problema:** el histórico se agrega por nombre completo. Los pools de nombres son finitos e incluyen nombres de leyendas (p. ej. "Michael"/"Jordan").
- **Consecuencia:** a lo largo de 20-50 temporadas un rookie aleatorio hereda stats/HOF de una leyenda; totales mezclados; inducciones y dorsales retirados espurios.
- **Solución:** clave por `player_id` estable; si se quiere conservar el histórico sembrado (que no tiene id de jugador), usar una tabla separada para leyendas y nunca mezclarla por nombre.
- **Test:** `HistoricalStats_SameNameRookie_DoesNotInheritLegendTotals`.

---

#### `SIM-01` · Ventaja de casa solo cuando el equipo del usuario juega en casa
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[V]**
- **Archivos:** `GameSimulator.cs:117` (`homeCourtBonus = isHome ? 1.5f : 0`); llamadas `DashboardController.cs:1296,1686,1692` (`isMyHomeGame = game.home_team_id == _myTeam.id`), `:1805` (G-League)
- **Problema:** el parámetro `isHome` significa "el usuario es local", pero el bonus se suma al `homeR` real. Los partidos IA-IA son neutrales y, cuando el usuario juega fuera, el local IA no recibe ventaja.
- **Consecuencia:** sesgo sistemático de resultados y de `MatchupPreview`.
- **Solución:** bonus al local real siempre; separar el flag "es el usuario" solo para UI. Revisar `:115-116` (química local 0.15 vs 0.10, ver `SIM-13`).
- **Test:** `HomeCourt_AppliedToActualHomeTeam_RegardlessOfUser`.

---

#### `SIM-02` · Se escriben stats para jugadores con 0 minutos y cuentan como partido jugado
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[V]**
- **Archivos:** `GameSimulator.cs:231-238` (persiste para todos los `homePS`/`awayPS` sanos); `DatabaseManager.Records.cs:565-595` (`COUNT(*) AS gp` sin filtro de minutos), `:613-623` (`GetPlayerGamesPlayedInSeason`); consumidores `StatsController.cs:384-392`, `PlayerProfileController.cs:173-180`, `DashboardController.cs:6739-6741` (moral, `Take(5)` de ratings)
- **Problema:** todos los convocados sanos generan fila de boxscore (aunque no jueguen) y la agregación las cuenta como GP.
- **Consecuencia:** GP inflados y medias diluidas (MPG/PPG/PER); forma/moral sesgadas a la baja; premios/roles que leen GP distorsionados.
- **Solución:** persistir solo `minutes > 0` (o filtrar `minutes > 0` en agregados y en el `Take(5)` de moral).
- **Test:** `BoxScore_NoRowsForZeroMinutes`; `SeasonAggregate_GamesPlayed_ExcludesDNP`.

---

#### `SIM-03` · Rotación "todo a 0" (RESET) y titular con plan 0 ignorados
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `GameSimulator.cs:83-84` (`strictHome = Any(fixed && target > 0)`), `:331-338` (fixed mantiene `target` 0), `:410-411` (quinteto inicial por índices 0-4), `:997-1018`, `:1068-1070`; `RotationController.cs:531-540` (`ApplyReset` a 0)
- **Problema:** con todos los planes a 0, `strict=false` pero los `target` quedan 0 → entran igualmente jugadores marcados como "no juega". Y un titular con plan 0 juega el arranque del partido hasta la primera ventana.
- **Consecuencia:** la intención del jugador no se respeta.
- **Solución:** si la suma configurada es 0 → `fixed_minutes = false` + heurística automática; sembrar el quinteto inicial desde los planes con minutos pendientes (fallback: mayor OVR).
- **Test:** `Rotation_AllZero_FallsBackToAutomatic`; `Rotation_ZeroMinuteStarter_DoesNotStart`.

---

#### `ECO-03` · El límite two-way no se comprueba al madurar ofertas
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[V]**
- **Archivos:** `DashboardController.cs:2460-2463` (`if (offer.is_two_way == 1) offerLegal = true;`)
- **Problema:** no se re-comprueba `GetTwoWayCount < TradeHelper.MAX_TWO_WAY` ni `IsEligibleForTwoWay`; la UI solo lo valida al abrir la oferta (`MarketController.cs:2350-2351`).
- **Consecuencia:** 3+ contratos two-way por equipo.
- **Solución:** validar límite y edad en la maduración.
- **Test:** `Offer_TwoWay_Rejected_WhenLimitReached`.

---

#### `ECO-04` · Re-firma con Bird rights rechazada a los 7 días (la maduración no conoce `IsOwnRecentFA`)
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[V]**
- **Archivos:** `DashboardController.cs:2454-2489` (rama `offer_type == 1`, solo reglas de FA externo); UI con `birdMax` en `RosterController.cs:1081-1186`; `IsOwnRecentFA` solo se usa en renovaciones (`:2632`) y en el panel (`:5458,5498,5795`)
- **Problema:** el panel de FA permite ofertar por FA propios calculando Bird completo, pero la maduración aplica cap/MLE de FA externo y rechaza.
- **Consecuencia:** funcionalidad anunciada que no funciona; mensajes "FICHAJE RECHAZADO (supera MLE)".
- **Solución:** en `offer_type==1`, ramificar por `IsOwnRecentFA(player, _myTeam.id)` y usar límites Bird coherentes con `GetMaxOfferBreakdown`.
- **Test:** `Offer_OwnRecentFA_CanExceedMLE_WithBirdRights`.

---

#### `ECO-05` · Luxury tax con signo negativo descuadra gastos y balance
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[V]**
- **Archivos:** `DashboardController.cs:7191-7199` (`amount = -monthlyTax`); `DatabaseManager.Records.cs:1028-1036` (`GetTotalExpenses` suma `amount` de `record_type >= TYPE_RENOVATION`); `FinancesController.cs:383-394` (muestra `Math.Abs(taxTotal)`); enum verificado: `TYPE_RENOVATION=5`, `TYPE_TAX=10`, `TYPE_BUYOUT=11`
- **Problema:** el impuesto se guarda negativo pero el agregador lo suma como gasto (debería restar), y la UI lo pinta positivo.
- **Consecuencia:** "Total gastos" y balance neto descuadran en 2× el impuesto; gráfico mensual inconsistente.
- **Solución:** guardar el tax positivo como el resto de gastos (y ajustar el débito de budget si lo hubiera), o excluirlo explícitamente de `GetTotalExpenses`.
- **Test:** `LuxuryTax_ExpenseSign_NetBalanceConsistent`.

---

#### `ECO-06` · La IA renueva y ficha sin cap/aprons
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `DashboardController.cs:4949-4957` (`ProcessAIRenewals`), `:5388,5426` (`TrySignFreeAgent`), `:4856-4862` (star FA); `Records.cs:3122-3137` (relleno con `availableCap` negativo → `Math.Max(minSalary, availableCap)`)
- **Problema:** la IA solo comprueba `team.budget` (caja), no el cap/aprons.
- **Consecuencia:** a 20-50 temporadas las nóminas IA divergen → mercado, valoraciones, traspasos y standings degeneran.
- **Solución:** pasar la IA por el modelo de cap/excepciones (o, mínimo, no firmar si `availableCap <= 0` y limitar resultado al 2.º apron).
- **Test:** `AITeams_NeverExceedSecondApron` (multi-temporada).

---

#### `ECO-07` · Ofertas y trade-offers sobreviven al rollover; la re-firma madura ~1 año tarde
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[V]**
- **Archivos:** `Records.cs:3052-3058` (no se borran `offers`/`trade_offers`), `:2558` (`currentDay >= day_sent + 7`), `:3209` (`current_game_day` = 0 en la nueva temporada); `NewSeasonController.cs:903-928` (`day_sent = _season?.current_game_day`)
- **Problema:** las ofertas pendientes persisten con `day_sent` de la temporada anterior.
- **Consecuencia:** la re-firma no madura hasta bien entrada la temporada siguiente (y suele rechazarse); trade-offers obsoletas se pueden aceptar después del deadline/incluso al año siguiente, y bloquean la generación de ofertas IA (`DashboardController.cs:5473-5474`).
- **Solución:** `DELETE FROM offers`/`trade_offers` en `StartNewSeason` (o expirarlas) y normalizar `day_sent = 0` para las creadas en el rollover.
- **Test:** `StartNewSeason_ExpiresPendingOffers`.

---

#### `ECO-08` · Los traspasos no actualizan tenure ni limpian flags (IR/two-way/G-League/trade block)
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `MarketController.cs:1910-1929`; `DashboardController.cs:5274-5325` (IA, sí limpia), `:5961-6003` (aceptar, no limpia)
- **Problema:** `seasons_with_team`/`last_team_id` no se actualizan al cambiar de equipo (Bird rights heredados) y no se resetean `is_two_way`/`is_on_ir`/`g_league_assigned`/`on_trade_block`.
- **Consecuencia:** Bird rights incorrectos; jugadores two-way/G-League "fantasma" en el equipo destino; flags que viajan.
- **Solución:** centralizar la transferencia en un helper único (`MovePlayerToTeam`) que fije `team_id`, `last_team_id`, `seasons_with_team = 1`, y limpie flags + dorsal.
- **Test:** `Trade_MovesPlayer_ResetsFlagsAndTenure`.

---

#### `CON-02` · Tareas de fondo no cancelables (el rollover sigue escribiendo tras salir)
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[V]** (patrón)
- **Archivos:** `Data/DatabaseManager.cs:112-164` (sin `CancellationToken`); `NewSeasonController.cs:416-440` (`while (!task.IsCompleted) yield return null`); `DashboardController.cs:226`
- **Problema:** `Task.Run` no se cancela al morir la corrutina. `StartNewSeason` (minutos) sigue ejecutándose y commiteando aunque el usuario navegue o cargue otra partida.
- **Consecuencia:** rollover commitado "a traición"; si se carga otro slot, la tarea sigue escribiendo en el `.db` anterior desde su propia conexión.
- **Solución:** `CancellationTokenSource` + single-flight guard por operación; bloquear navegación durante el rollover; comprobar cancelación en puntos seguros.
- **Test:** `StartNewSeason_NavigatingAway_DoesNotCommitPartially`.

---

#### `REN-01` · `EvaluateGameDay` escanea todos los partidos de la carrera cada día
- **Estado:** `[x]` hecho
- **Severidad:** ALTO (rendimiento + semántica) · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `Core/AchievementService.cs:64-71, 91-117`; `DatabaseManager.Games.cs:226-235` (`GetStandingsGames` sin filtro de temporada); llamado en `DashboardController.cs:1256`
- **Problema:** `MyTeamSeasonGames` recorre todos los partidos del manager y recalcula wins/racha con LINQ **cada día** (también en fast-sim). Si algún día se deja de purgar `games`, contaminará temporadas pasadas.
- **Consecuencia:** coste diario alto con allocs; método engañoso.
- **Solución:** filtrar por `season_id` (parámetro) y cachear el resultado por día.
- **Test:** `Achievements_EvaluateGameDay_UsesOnlyCurrentSeason`.

---

#### `UI-02` · Config → Menú principal alcanzable durante fast-sim (abandona la transacción)
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `Core/UIScreenController.cs:413-417`; `DashboardController.cs:1226,1246,1878-1885`
- **Problema:** `SetSidebarNavigationEnabled(false)` deshabilita items de sidebar, pero no el icono de configuración; su handler "Menú principal" navega sin guard.
- **Consecuencia:** se abandona la simulación en medio de la transacción (agravando `CON-01`).
- **Solución:** deshabilitar el icono de config (y el botón de menú) durante la simulación, o guardar el handler con el flag de simulación.
- **Test:** `ConfigIcon_DisabledDuringSimulation`.

---

#### `UI-03` · Scroll muerto tras la primera visita (handle cacheado con `??=`)
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `UI/StatsController.cs:104`; `UI/EditorController.cs:192-198`
- **Problema:** `_scrollBar ??= CustomScrollbar.Bind(...)`. En Unity 6, `UIDocument.OnDisable()` anula `rootVisualElement` y al reactivar se recrea el árbol → el handle cacheado apunta a elementos desechados y nunca se rebindea.
- **Consecuencia:** la lista no se puede scrollear (rueda/arrastre) al volver a la pantalla.
- **Solución:** eliminar `??=` y re-bindear en `CacheReferences` en cada visita; o hacer el handle auto-rebindable.
- **Test:** (manual/PlayMode) `Stats_Scroll_WorksAfterRevisit`.

---

#### `UI-04` · Scrollbar re-bindeada en cada rebuild dentro de la misma visita
- **Estado:** `[~]` parcial
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `MessagesController.cs:223, 827` (cada tecla/filtro); `PalmaresController.cs` (varias, cada pestaña); `BuscadorController.cs:255`; `CarteraController.cs:714`; `GLeagueController.cs:411`; `HistorialController.cs:182`; `InjuredController.cs:817`
- **Problema:** `BindScrollBar(...)` registra `WheelEvent`/pointer/`GeometryChangedEvent` sobre elementos persistidos durante la visita; se vuelve a llamar en cada rebuild → N handlers acumulados.
- **Consecuencia:** un tick de rueda dispara N handlers → scroll errático, capturas duplicadas, degradación de sesión.
- **Solución:** bindear una vez por visita (flag) y `UnregisterCallback` antes de re-bindear; mejor, usar la `CustomScrollbar` compartida.
- **Test:** `Messages_ScrollBoundOnce_PerVisit`.

---

#### `UI-05` · `TrainingController` lanza `InvalidOperationException` al pulsar ENTRENAR/AUTO
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** BUG · **Verificación:** **[A]**
- **Archivos:** `UI/TrainingController.cs:569-571`, `:661-668`
- **Problema:** `BuildPlayerList()` limpia el grid y `OnPlayerSelected(player, _trainingBody.Query<VisualElement>(className: "training-card--selected").First())` → `.First()` sobre vacío lanza (la clase `--selected` no se re-aplica).
- **Consecuencia:** excepción al guardar entrenamiento; panel de detalle sin refrescar; selección perdida.
- **Solución:** pasar la tarjeta recién creada y/o re-aplicar la clase seleccionada en `BuildPlayerList`; usar `FirstOrDefault()`.
- **Test:** `Training_Save_DoesNotThrow_AfterRebuild`.

---

#### `UI-06` · `DivideByZeroException` en buyout con `contract_years == 0`
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** RIESGO · **Verificación:** **[V]**
- **Archivos:** `UI/RosterController.cs:1568-1570` (`OpenBuyoutModal`), `:1593-1595` (`ConfirmBuyout`); contraste `DashboardController.cs:3785` que sí usa `Mathf.Max(1, ...)`
- **Problema:** `stretchYears = contract_years * 2` y `remainingSalary / stretchYears` sin guarda; el botón siempre está registrado/visible.
- **Consecuencia:** crash de pantalla si un jugador en plantilla tiene 0 años (dato inconsistente, ver `ECO-01`).
- **Solución:** `Mathf.Max(1, ...)` y ocultar/deshabilitar el botón si `contract_years <= 0`.
- **Test:** `Buyout_ZeroYearContract_DoesNotThrow`.

---

#### `CON-03` · Busy timeout de 1 s y sin mutex global de escritura
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** RIESGO · **Verificación:** **[A]**
- **Archivos:** `Data/SQLite.cs:600` (`BusyTimeout = 1s`); `DatabaseManager.cs:112-164`; `DashboardController.cs:921, 1067-1071` (la sidebar no se bloquea en el batch normal)
- **Problema:** WAL permite un solo escritor; un batch de fondo tiene el lock y la UI puede intentar escribir en main thread → `SQLITE_BUSY` tras 1 s, que muchos sitios se tragan.
- **Solución:** mutex/cola de escritura único, subir el busy timeout y/o deshabilitar la UI durante los batches; añadir retry/backoff.
- **Test:** `ConcurrentWrite_BatchAndUi_DoesNotThrowBusy`.

---

#### `CON-04` · Batches solapados comparten estado no thread-safe
- **Estado:** `[x]` hecho
- **Severidad:** ALTO · **Tipo:** RIESGO · **Verificación:** **[A]**
- **Archivos:** `DashboardController.cs:158-159` (`_aiRng`), `:4582-4603` (`_teamStrategyCache`), `:4685` (`_teamTradeCooldown`)
- **Problema:** si una tarea huérfana (`CON-02`) sigue viva y se lanza otro batch, dos hilos mutan `System.Random`/`Dictionary` no thread-safe.
- **Consecuencia:** corrupción de diccionario (posible bucle infinito), decisiones IA inconsistentes.
- **Solución:** single-flight guard alrededor del procesamiento de día y de `ProcessAIMarket`.
- **Test:** `DoubleAdvanceDay_SecondIsRejected`.

---

### 3.3 MEDIOS

> Fichas compactas: **Problema → Causa → Consecuencia → Solución**. Marca `[x]` al resolver.

- `[x]` **`ECO-09` · Nómina pagada 7/12 veces** — `DashboardController.cs:7081` (`payrollDays = {1,31,61,91,121,151,181}`) y `:7101` (`salary / 12`). → Solo se cobra ~58% de la nómina anual mientras los ingresos son anuales → presupuestos divergen al alza y el cierre por presupuesto pierde sentido. → Decidir: 12 plazos reales o `sum/7` documentado. **Verificación:** [V]
- `[x]` **`ECO-10` · Préstamos con signo/clasificación erróneos** — `LoansController.cs:544-551` (desembolso `TYPE_LOAN=9` positivo → contado como gasto) y `:588-595` (devolución negativa). → Balance sin sentido. → Clasificar como financiación (rango aparte) o manejarlo explícitamente en los agregadores. **Verificación:** [A]
- `[ ]` **`ECO-11` · Máximo de FA externo sobreestimado en UI** — `RosterController.cs:1131-1132` (`capSpaceMax = player.salary + capSpace`), correcto solo para re-firma. → UI promete más de lo legal; rechazo a los 7 días. → `capSpaceMax = max(0, capSpace)` para FA externos. **Verificación:** [A]
- `[ ]` **`ECO-12` · `TrimRostersToMaxSize` excluye al equipo del usuario** — `Records.cs:3064, 3231-3257`. → El usuario puede empezar con >17 si cerró el modal de IR. → Aplicar también al usuario (auto-trim o prompt). **Verificación:** [A]
- `[ ]` **`ECO-13` · Suelo de 1M anula el máximo legal** — `RosterController.cs:1240-1259`: el `if (_renewSalary < 1M)` no excluye el `else if (_renewSalary > max)`. → Ofertas por encima del máximo legal. → Clamp final con `Math.Max(floor, min(offer, max))` y rechazo explícito si `max < floor`. **Verificación:** [A]
- `[ ]` **`ECO-14` · Two-way de draft ignora la edad ≤23** — `DraftGenerator.cs:403-408, 454-457`. → Contratos two-way a rookies de 24. → Añadir `age <= 23` / `IsEligibleForTwoWay`. **Verificación:** [A]
- `[ ]` **`ECO-15` · Swaps solapados pueden sobreescribir propietarios** — `DraftGenerator.cs:128-156` (`owners[partnerSlot] = swapHolder; owners[selfSlot] = partnerHolder`). → Una ronda con <30 dueños distintos (equipo con 2 picks / otro sin ninguno). → Resolver swaps en pasada determinista y `assert` 30 dueños por ronda. **Verificación:** [A]
- `[x]` **`SEA-07` · Draft no transaccional ni resumible** — `EndSeasonController.cs:363-395, 616-642`; `DraftGenerator.cs:397-465`; `ClearAllRookies` en `:365`. → Recargar a mitad de draft reinicia y puede duplicar la clase (jugadores fantasma, cap explotado). → Persistir estado del draft (cursor + picks usados) y flag de completado; `StartDraft` no-op si hay draft en curso/completado. **Verificación:** [A]
- `[x]` **`SEA-08` · `overall` almacenado ≠ `GetCalculatedAverage()`** — `Records.cs:2823` (`Round(sum/11f)`) vs `PlayerData.cs:64-67` (división entera). → Rankings/valoración IA y UI usan números distintos (±1). → Una sola fórmula (usar `GetCalculatedAverage`, aplicando cap de `potential`) en todo el proyecto. **Verificación:** [V]
- `[x]` **`SEA-09` · `ApplyMentoring` no recalcula `overall`** — `Records.cs:3881-3920`. → Deriva del overall cada offseason. → Recalcular/`cap` tras el loop. (Relacionado con `SEA-08`; puede resolverse en el mismo refactor.) **Verificación:** [A]
- `[x]` **`SEA-10` · Estado transitorio no reseteado en el rollover** — `Records.cs:2664-3229`: `injury_days`, `fisico`, `morale`, `on_trade_block`, `team_chemistry`, `fan_confidence`. → Lesiones/fatiga/trade-block viajan de temporada; química/moral nunca se normalizan. → Resetear explícitamente (o documentar por qué persisten). **Verificación:** [A]
- `[~]` **`SEA-11` · `season_records` solo escribe `most_improved_id`** — lecturas en `Records.cs:1143,1155,1165` comparan `season_mvp_id`, `champion_id`, `first_team_*`… que nunca se escriben (verificado: solo hay lecturas `==`). → La trayectoria del jugador nunca muestra MVP/All-Star/campeón/primer equipo. → Poblar `season_records` en `SaveSeasonEndRecords`. **Verificación:** [V]
- `[x]` **`SEA-12` · `GetActiveSeason` sin `ORDER BY` y `CreateSeason` no desactiva** — `Games.cs:41-46`, `:12-39`; desactivación en `Records.cs:3168-3172`. → Dos temporadas `is_active=1` → se opera sobre la equivocada (awards/récords/rollover). → `OrderByDescending(id).FirstOrDefault()` + forzar desactivación al crear. **Verificación:** [V]
- `[x]` **`SIM-05` · Equipos con 2-4 sanos alinean <5 y <2 genera marcador sin box** — `GameSimulator.cs:86-109, 410` (solo guarda `<2`). → Baloncesto ilegal; fallback con marcador y boxscore vacío; la pre-check de quinteto vacío solo aplica al usuario. → Guardar `<5` (rellenar desde G-League/IR/FA) y generar stats coherentes con el marcador. **Verificación:** [A]
- `[ ]` **`SIM-06` · Lesiones también a jugadores con 0 minutos** — `GameSimulator.cs:278-279, 1161-1181` (prob. plana sobre `fisico` pre-partido). → Distribución de lesiones irreal. → Excluir `minutes <= 0` o ponderar por minutos. **Verificación:** [A]
- `[ ]` **`SIM-07` · `GameResultCache` no se limpia al cargar partida y `Clear()` no resetea `LastGameDay`** — `GameResultCache.cs:23-28`; `LoadGameController.cs:445-447`; consumidores `GameResultsController.cs:206,229`, `MatchDayController.cs:332,514,616,658`. → Resultados de otra partida visibles. → Limpiar (y resetear `LastGameDay`) en el flujo de carga. **Verificación:** **[V]** (solo llamadas a `Clear` verificadas)
- `[ ]` **`SIM-08` · No se limpia el cache en día sin partidos preexistentes que crea play-in** — `DashboardController.cs:1199-1203` vs `:1285-1303` (`gamesToday` se lee antes de crear eliminatorias). → Resultados del día anterior mezclados. → `Clear()` incondicional al inicio de la transacción del día. **Verificación:** [A]
- `[ ]` **`SIM-09` · Fatiga con drenaje neto negativo y sin reset de temporada** — `DashboardController.cs:962-966` (+8 solo en día sin partido), `GameSimulator.cs:262` (`minutes*0.30`), helper muerto `DashboardController.cs:4444-4455`. → Stars caen por debajo de `fisico < 30` (−25% tiro) → el anotador de la liga decae con la temporada. → Rebalancear recuperación y resetear/recuperar en `StartNewSeason`. **Verificación:** [A]
- `[ ]` **`SIM-10` · Solo 35% de canastas asistidas** — `GameSimulator.cs:795` (`Random.value >= 0.35`). → Asistencias ~40% bajas vs NBA real. → Subir a ~0.55-0.60 o añadir segunda fuente de asistencia. **Verificación:** [A]
- `[ ]` **`SIM-11` · Suelo de rebotes añade rebotes sin tiro fallado** — `GameSimulator.cs:189-200`. → Rebotes de equipo > fallos disponibles; medias de élite infladas. → Acotar el suelo por la bolsa real de fallos o quitarlo. **Verificación:** [A]
- `[x]` **`SIM-12` · La prórroga ignora la rotación vinculante** — `GameSimulator.cs:481-486` (`DoSub(..., strict` por defecto `false`)); `SimQuarter` sí pasa `strictHome/Away`. → Los minutos configurados no se respetan en OT. → Pasar `strict` a `SimOvertime`. **Verificación:** [A]
- `[x]` **`SIM-13` · Bonus de química asimétrico (local 0.15 / visitante 0.10)** — `GameSimulator.cs:115-116`. → El local se beneficia el doble de la química, acumulado con `SIM-01`. → Unificar coeficiente o documentar. **Verificación:** **[V]**
- `[ ]` **`SIM-14` · All-Star aplica fatiga/lesiones y escribe stats** — `GameSimulator.cs:231-279` (solo excluye récords), `DashboardController.cs:1701-1742`. → Participantes pierden físico, se lesionan y generan `player_game_stats` con `team_id` negativo; `.First()` puede lanzar si los rosters quedan vacíos. → Excluir fatiga/lesión/stats para `game_type == "allstar"`; guardar `Any()` antes de `First()`. **Verificación:** [A]
- `[ ]` **`CON-05` · `Refresh()` re-escanea partidos varias veces/día** — `DashboardController.cs:468, 7330-7346, 7395-7420, 7596-7600`; `GetTeamRecordShort` llamado 4×. → CPU dominante del fast-sim. → Calcular standings/records una vez por día y cachear; filtrar por `season_id`. **Verificación:** [A]
- `[x]` **`CON-06` · N+1 `GetPlayerGamesPlayedInSeason` en renovaciones IA** — `Records.cs:616-622`; `DashboardController.cs:4942`. → Cientos de queries por ciclo (carga todos los `gameIds` de la temporada en cada llamada). → Un solo `JOIN ... GROUP BY player_id` o diccionario precomputado. **Verificación:** [A]
- `[x]` **`CON-07` · Bandeja completa cargada para mostrar 5** — `DashboardController.cs:7709`; `Records.cs:2519`. → `SELECT ORDER BY id DESC LIMIT 5` + índice `(manager_id, id)`. **Verificación:** [A]
- `[x]` **`CON-08` · `GetLeagueTop*` N+1 y sin filtro de temporada (¿código muerto?)** — `Records.cs:625-716`; sin call sites encontrados. → Eliminar o reescribir en una query agregada con `season_id`. **Verificación:** [A]
- `[x]` **`CON-09` · Fuga de texturas/sprites en `TeamImages`** — `TeamImages.cs:67-73` (`Invalidate` solo limpia diccionarios), `:280-291` (crea `Texture2D`/`Sprite`); `_previewCache` nunca se limpia. → Fuga al cambiar de slot repetidamente (escena única, nunca se descargan). → `Object.Destroy` al invalidar. **Verificación:** [A]
- `[x]` **`CON-10` · `PlayerPhotoHelper.Load` sin caché** — `PlayerPhotoHelper.cs:17-69`; usado hasta 6× por `Refresh` en `DashboardController.cs:7587`. → Textura nueva por llamada con fotos custom. → Cachear por `(scope, playerId)` y `Destroy` en invalidación. **Verificación:** [A]
- `[x]` **`UI-07` · Errores de fast-sim solo a consola** — `DashboardController.cs:1843-1847, 2121`. → El jugador no sabe por qué se detuvo. → Modal/toast con motivo y opción de reintentar. **Verificación:** [A]
- `[ ]` **`UI-08` · `FullScreenUI.Awake` puede desreferenciar null** — `FullScreenUI.cs:9-11` (`rootVisualElement` no existe hasta `OnEnable`). → NRE por pantalla en build; ruido que enmascara errores reales. → Eliminar el componente (la base ya hace fullscreen) o mover a `OnEnable`. **Verificación:** [A]
- `[ ]` **`UI-09` · RESETEAR del Editor sin confirmación** — `EditorController.cs:314, 336-354` (borra `template.db`). → Acción destructiva de un clic. → Modal de confirmación (patrón existente). **Verificación:** [A]
- `[ ]` **`UI-10` · Traspaso propio sin confirmación ni undo** — `MarketController.cs:1869-1998`. → Un traspaso mal armado es irreversible. → Reutilizar el modal de confirmación de ofertas IA con resumen. **Verificación:** [A]
- `[x]` **`QA-01` · `PlayerSeasonStatRow` sin `[Table]`** — `Data/DatabaseRows.cs:111`; `CreateTable` en `DatabaseManager.cs:305` crea una tabla basura distinta de `player_season_stats` (creada por SQL crudo `:805`). → Cualquier uso futuro de `_db.Table<PlayerSeasonStatRow>()` lee vacío. → Añadir `[Table("player_season_stats")]` o eliminar la clase y dejar una sola vía de creación. **Verificación:** **[V]**

---

### 3.4 BAJOS

- `[ ]` `QA-02` · `contract_years`/`guaranteed_years` pueden quedar a −1 con datos raros (`NewSeasonController` opciones). → Clamp `>= 0`.
- `[ ]` `QA-03` · `GetAllSlots` corrige `exists` en memoria pero no lo persiste (`GameSaveManager.cs:90-102`) + `managerName` vacío se trata como inválido (`:59`) → mejor usar `initialized` (enlaza `SAV-01`/`SAV-03`).
- `[ ]` `QA-04` · `DeleteSave` no cierra la conexión activa ni borra `-wal`/`-shm` (`GameSaveManager.cs:125-156`); el borrado silencioso puede dejar huérfanas. → `wal_checkpoint(TRUNCATE)` + borrar sidecars + cerrar si es el slot activo.
- `[ ]` `QA-05` · `EnsureTemplateDb` acepta un `template.db` de 0 bytes/parcial (`DatabaseManager.cs:166-179`). → Validar tamaño/integridad y reconstruir; escribir con rename atómico.
- `[ ]` `QA-06` · `LoadingController._transitioning` no se resetea (`LoadingController.cs:21, 49-51`). → Reset en `OnEnable`.
- `[ ]` `QA-07` · Los docs afirman acumulación de callbacks entre visitas (`AGENTS.md`, `Docs/UI_TOOLKIT.md:79`); en Unity 6 el árbol se recrea. → Corregir la doc y quedarse solo con el fix real (`UI-04`).
- `[ ]` `QA-08` · `DashboardController.cs:6934` (`if (inj.player_id == _myTeam.id || true)`) → condición muerta. → Simplificar.
- `[ ]` `QA-09` · `GLeaguePostSeason.cs:551` pasa `isHome=false` fijo (sin ventaja de casa en playoffs GL). → Pasar el flag real (tras arreglar `SIM-01`).
- `[ ]` `QA-10` · Formato de números inconsistente (`es-ES` vs `InvariantCulture` en `PlayerProfileController.cs:193-206`) y solo hay tooltips en roles/badges G-League. → Centralizar formato y añadir tooltips (ver §9-A).
- `[ ]` `QA-11` · `GamesController`/4 pantallas siguen usando `ScrollView` nativo pese al aviso de `Docs/UI_TOOLKIT.md:44` (`PreseasonController.cs:291`, `PlayoffsController.cs:25`, `SelectTeamController.cs:76`, `QuintetoController.cs:74`). → Migrar a la scrollbar propia.
- `[ ]` `QA-12` · 13 controllers hacen override de `RegisterCallbacks()` sin `base` (los 12 documentados + `PreGameController`). → No copiar el patrón; migrar gradualmente a la base (algunos son de arranque y no la necesitan).

---

## 4. Problemas de base de datos (transversal)

1. **[Sin FKs]** SQLite no aplica integridad referencial → huérfanos posibles en `monthly_awards`, `hall_of_fame`, `retired_numbers`, `player_season_stats`, `player_game_stats`. **Solución:** helpers de borrado en cascada + validaciones `GetPlayerById != null` en las lecturas; `UNIQUE` en `hall_of_fame(player_id)` y `retired_numbers(team_id, number)`.
2. **[Ids reutilizados]** `DraftGenerator.cs:421-423` usa `MAX(id)+1` → si se borra el id máximo, se reutiliza y premios/HOF apuntan al nuevo jugador. **Solución:** tabla de secuencia monótona (`sqlite_sequence` o contador propio) que nunca reutilice.
3. **[Índices]** Solo 4 archivos usan `[Indexed]`. **Añadir:** `players.team_id`, `messages.manager_id`, `games.season_id`, `games.manager_id`, `player_game_stats.player_id`/`game_id`, `player_season_stats.player_id`/`season_id`.
4. **[Crecimiento]** `player_season_stats`, `historical_player_stats`, `monthly_awards`, `trade_data` crecen sin poda. **Solución:** política de retención o índices; medir tamaño de `.db` por temporada.
5. **[Atomicidad]** Ver lista de operaciones sin transacción (`ECO-01/02/07`, `SEA-04`, `SAV-03/06/07`, `ECO-08`). **Solución:** helper `ExecuteInTransaction(Action)` y prohibir mutaciones multi-tabla fuera de él.
6. **[`-wal`/`-shm`]** No se gestionan al borrar (`QA-04`) ni al hacer backup. **Solución:** checkpoint + borrado de sidecars + backup atómico del `.db`.
7. **[`saves.json`]** Fuente de verdad frágil (no atómica, sin backup) (`SAV-01`). **Solución:** escritura atómica + `.bak` + validación cruzada DB↔metadata.

---

## 5. Rendimiento (resumen y plan)

| Prioridad | Operación | Archivo | Plan |
|---|---|---|---|
| 1 | `Refresh()` re-escanea partidos ×4/día | `DashboardController.cs:468,7333` | Cachear standings/records del día |
| 2 | `EvaluateGameDay` O(partidos) diario | `AchievementService.cs:64-117` | Filtrar por `season_id` + cache |
| 3 | N+1 renovaciones IA | `Records.cs:616-622` | Query agregada |
| 4 | Bandeja completa por refresh | `DashboardController.cs:7709` | `LIMIT 5` + índice |
| 5 | `players` sin índice + draft `Any()+Max()` ×60 | `DraftGenerator.cs:421-423` | Índice + secuencia de id |
| 6 | Fugas de texturas | `TeamImages.cs`, `PlayerPhotoHelper.cs` | Caché + `Destroy` |

**Matiz clave:** como el rollover borra `games`/`player_game_stats`/`messages`, el coste **por día no crece con las temporadas**. El crecimiento a 20-50 temporadas está en tablas de carrera y en `players`. El riesgo real de memoria es la **acumulación de texturas** al cambiar de partida.

---

## 6. UX/UI (resumen y plan)

1. `[ ]` **Soft-lock del Dashboard** (`UI-01`) + escape por config (`UI-02`) → prioridad máxima.
2. `[ ]` **Errores de fast-sim invisibles** (`UI-07`) → modal con motivo + reintentar.
3. `[ ]` **Scroll** roto/errático (`UI-03`, `UI-04`) y `ScrollView` nativos (`QA-11`) → scrollbar única.
4. `[ ]` **Confirmaciones** en acciones destructivas: traspaso propio (`UI-10`), RESETEAR Editor (`UI-09`), borrado de noticias.
5. `[ ]` **Crashes por clic**: entrenamiento (`UI-05`), buyout (`UI-06`).
6. `[ ]` **Feedback**: explicar por qué un botón está deshabilitado; tooltips en analítica/cap; formato de números consistente.
7. `[ ]` **QoL**: filtros/orden en listas largas, aviso del tope de 240 en ROTACIÓN, indicador de tope salarial en ofertas.

---

## 7. Edge cases a cubrir (conceptualmente)

- [ ] Plantilla de 0 / <5 sanos; quinteto vacío por lesiones en partido.
- [ ] Todos los `rotation_minutes` a 0; titular con plan 0; suma configurada >240 y <240.
- [ ] `contract_years == 0`; salario 0/extremo; dos contratos activos; contrato negativo.
- [ ] 5 prórrogas empatadas; partido de playoffs empatado (7.º).
- [ ] Dos ofertas two-way en cola; oferta Bird + externa simultáneas para el mismo jugador; oferta madurando cuando el jugador ya firmó/traspasó/retiró.
- [ ] Traspaso entre validación y confirmación con roster cambiado; con IR/two-way/G-League; en el límite de apron/hard cap; solo picks; 0 jugadores por un lado.
- [ ] Cierre del juego durante: trade, firma, día, simulación, rollover, migración, clon de plantilla.
- [ ] `saves.json` corrupto con DBs válidas; `.db` corrupto/bloqueado/solo lectura.
- [ ] Draft interrumpido y recargado; <60 picks; <14 equipos (lottery); conferencia con <8 seeds.
- [ ] Temporada sin partidos guardados pero `generated=1`; `FindNextGameDay==0` con fase no terminal.
- [ ] Dos temporadas activas; id de jugador reutilizado; nombre aleatorio = leyenda sembrada.
- [ ] Cargar partida antigua tras cambios de código/migración.
- [ ] Simular 10 temporadas seguidas y comprobar invariantes (W+L, nóminas vs cap, plantillas ≤17, sin huérfanos).

---

## 8. Tests recomendados

> Los tests viven en `Assets/_TacticalFive/Tests/Editor/` (sin asmdef, EditMode). Los que requieren DB necesitan refactor para inyectar ruta (ver nota en `AGENTS.md §9`). Hoy: 85 tests.

**Simulación (`GameSimulatorTests`)**
- [ ] `SimulateGame_Overtime_AlwaysProducesWinner` (`SIM-04`)
- [ ] `HomeCourt_AppliedToActualHomeTeam_RegardlessOfUser` (`SIM-01`)
- [ ] `BoxScore_NoRowsForZeroMinutes` (`SIM-02`)
- [ ] `SeasonAggregate_GamesPlayed_ExcludesDNP` (`SIM-02`)
- [ ] `GameSimulator_NeverMoreThanFiveOnCourt` / `NoDuplicateOnCourt`
- [ ] `Rotation_AllZero_FallsBackToAutomatic` (`SIM-03`)
- [ ] `Rotation_ZeroMinuteStarter_DoesNotStart` (`SIM-03`)
- [ ] `Overtime_RespectsFixedRotation` (`SIM-12`)

**Temporada / draft**
- [ ] `StartNewSeason_ArchivesHistoricalStatsExactlyOnce` (`SEA-01`)
- [ ] `StartNewSeason_ResetsHardCapFlag` (`SEA-02`)
- [ ] `StartNewSeason_ExpiresPendingOffers` (`ECO-07`)
- [ ] `StartNewSeason_TwoActiveSeasons_IsImpossible` (`SEA-12`)
- [ ] `Schedule_Always82GamesPerTeam` (`SEA-03`)
- [ ] `Preseason_ScheduleSaveFails_DoesNotMarkGenerated` (`SEA-03`)
- [ ] `Playoffs_MissingSeeds_DoesNotSoftLock` (`SEA-05`)
- [ ] `Draft_ProtectedPickReverts_WhenInsideRange` / `Draft_Swaps_KeepThirtyDistinctOwnersPerRound` (`ECO-15`)
- [ ] `Draft_Interrupted_Midway_DoesNotDuplicateClass` (`SEA-07`)
- [ ] `HistoricalStats_SameNameRookie_DoesNotInheritLegendTotals` (`SEA-06`)

**Economía / traspasos / FA**
- [ ] `Trade_Confirm_Revalidates_RejectsOverRoster17` (`ECO-02`)
- [ ] `SignAndTrade_Extension_RespectsHardCap` (`ECO-02`)
- [ ] `Offer_TwoWay_Rejected_WhenLimitReached` (`ECO-03`)
- [ ] `Offer_OwnRecentFA_CanExceedMLE_WithBirdRights` (`ECO-04`)
- [ ] `Buyout_DebitsBudget_AndPersistsDeadMoney` (`ECO-01`)
- [ ] `LuxuryTax_ExpenseSign_NetBalanceConsistent` (`ECO-05`)
- [ ] `MaturedOffer_Rejected_WhenPlayerAlreadySignedTradedOrRetired`
- [ ] `AITeams_NeverExceedSecondApron` (multi-temporada, `ECO-06`)
- [ ] `Trade_MovesPlayer_ResetsFlagsAndTenure` (`ECO-08`)

**Persistencia (requiere refactor de rutas)**
- [ ] **`SaveSlot_CorruptSavesJson_DoesNotDeleteDatabases`** (`SAV-01`) ← el más valioso
- [ ] `InitSaveSlot_InterruptedClone_IsDetectedAndRebuilt` (`SAV-06`)
- [ ] `RunMigrations_IsIdempotent_AndRecoversFromPartialColumns` (`SAV-04`)
- [ ] `DeleteSave_RemovesWalAndShm` (`QA-04`)
- [ ] `InitSaveSlot_CorruptDb_ShowsErrorAndRecovers` (`SAV-05`)

**UI / estado**
- [ ] `Dashboard_OnDisable_DuringSim_ResetsIsLoading` (`UI-01`)
- [ ] `GameResultCache_ClearedOnLoadAndOnZeroGameDay` (`SIM-07`/`SIM-08`)

---

## 9. Nuevas funcionalidades

> Criterio: compatibles con la arquitectura actual (helpers static + SQLite + UXML/USS + controllers sobre `UIScreenController`). No se implementa nada hasta cerrar la Fase 1.

### A. Pequeñas (1-3 días)
| Estado | Idea | Aporta | Sistemas que usa | Prioridad |
|---|---|---|---|---|
| `[ ]` | Confirmación de traspaso propio + resumen | Seguridad | Modal existente en `DashboardController` | Alta |
| `[ ]` | Toast/modal de error de fast-sim con "reintentar" | Confianza | `HandleDayError`, `UIScreenController` | Alta |
| `[ ]` | Aviso/indicador del tope de 240 en ROTACIÓN | Claridad | `RotationController.RefreshSliderLimits` | Media |
| `[ ]` | Arreglar `season_records` (MVP/champion/first_team/All-Star) | Trayectoria del jugador | `SaveSeasonEndRecords` (`SEA-11`) | Alta |
| `[ ]` | Tooltips en eFG%/TS%/PER, aprons y excepciones | Onboarding | UI Toolkit | Media |
| `[ ]` | Flag `initialized` en `SaveSlotInfo` | Integridad | `GameSaveManager` | Alta |
| `[ ]` | Filtros/orden en Historial y Récords | Usabilidad | scrollbar única | Media |
| `[ ]` | Indicador de cap space/aprón en la pantalla de ofertas | Claridad | `TradeHelper` + `league_settings` | Alta |

### B. Medianas (1-3 semanas)
| Estado | Idea | Cómo funciona | Sistemas nuevos/existentes | Prioridad |
|---|---|---|---|---|
| `[ ]` | Draft persistente y resumible + board con votos IA | Tabla `draft_state` (cursor, picks usados) + `DraftGenerator` por pasos | `DraftGenerator`, `EndSeasonController` | Alta |
| `[ ]` | Negociación de agente (contraofertas, promesas de rol/minutos, bonuses) | Extender `OfferData` + `CalculateAcceptScore` con peso de rol/minutos | `OfferData`, `RosterController`, `DashboardController` | Alta |
| `[ ]` | Dashboard de cap/aprons por equipo | Vista de excepciones y espacio salarial | `TradeHelper`, `league_settings` | Alta |
| `[ ]` | Historial de temporadas navegable | Pantalla nueva desde `player_season_stats` + `season_records` | `player_season_stats`, `SeasonRecord` | Media |
| `[ ]` | Parte médico y recuperación realista | Tipos de lesión con tiempos y recaídas | `injury_days`, `GameSimulator` | Media |
| `[ ]` | Camp de pretemporada con decisiones | Foco por jugador, efectos y coste de fatiga | `TrainingData`, `Records` (rollover) | Media |
| `[ ]` | IA con gestión real de cap | Helper de cap compartido con el usuario | `TradeHelper`, `DashboardController` (`ECO-06`) | Alta |
| `[ ]` | Rivalidades/dinastías | Récords de series y prestige de franquicia | `season_records`, `standings` | Media |

### C. Grandes (1-3 meses)
| Estado | Idea | Aporta | Sistemas a tocar | Prioridad |
|---|---|---|---|---|
| `[ ]` | Plan de partido / instrucciones (ritmo, dobles a la estrella, defensa, rotación corta) | El jugador *entrena*, no solo ficha | `GameSimulator` (pacing/usage/defense) + UI | Alta |
| `[ ]` | Staff técnico con esquemas que modifican el simulador | Diferenciación real | `CoachRankingData`, `EmployeeData`, `GameSimulator` | Media-alta |
| `[ ]` | Progresión por arquetipo/edad/minutos/entrenamiento | Carreras creíbles | `Records.cs` (rollover), `ApplyMentoring` (`SEA-08/09/10`) | Alta |
| `[ ]` | Economía de mercado real (asistencia elástica, ingresos por ciudad, patrocinios por fama) | Gestión de franquicia de verdad | `DashboardController` economía, `TeamSettingsData` | Media |
| `[ ]` | Scouting profundo de draft (incertidumbre, busts, informes) | Riesgo real en el draft | `FogOfWarHelper`, `ScoutData`, `DraftGenerator` | Media |
| `[ ]` | All-Star weekend jugable | Contenido | `GameSimulator`, `all_star_records` | Baja-media |

### D. Killer features
1. **Modo histórico / eras** — empezar en cualquier temporada histórica con plantillas/palmarés reales (base ya existente: `HallOfFameSeeder`, `PalmaresSeeder`, `HistoricalPlayerStatsSeeder`, `RetiredNumberSeeder`) y reescribir la historia. *Coste:* alto (necesita datasets y lógica de bootstrap de liga). *Riesgo:* datos.
2. **Motor de narrativa ("storylines")** — rumores, polémicas, rachas y relaciones de vestuario convertidos en noticias (ya hay `QuickNewsGenerator` + personalidades/relaciones sin explotar) que afectan moral, prensa y afición. *Coste:* medio. *Impacto:* convierte números en drama.
3. **MyLeague / Editor total** — llevar `EditorController` a editor de liga completo (equipos, ciudades, reglas, draft classes, logos, pabellones — ya hay `CustomScopes`/`TeamImages`) con export/import. *Coste:* alto. *Impacto:* multiplica la vida útil.
4. **Broadcast en vivo narrado** — posición a posición con comentario (ya se captura `PlayByPlayLogs`) + odds internas. *Coste:* bajo-medio. *Impacto:* espectáculo.
5. **Hotseat/multi-manager local** — varios GMs en la misma liga (la IA ya juega con `TeamStrategy`). *Coste:* medio-alto. *Impacto:* modo social local.

---

## 10. Roadmap por fases

### Fase 1 — Estabilidad (prioridad absoluta, ~1-2 semanas)

**Criterio de salida:** simular 3 temporadas seguidas y cerrar el juego a mitad de cada operación crítica sin corromper datos, sin soft-locks y sin excepciones.

- [x] `SAV-01` borrado masivo de partidas + `saves.json` atómico + `.bak` (commit `3a59f854`). *Nota:* el flag `initialized` no se añadió; se resolvió moviendo el metadata al final en `SAV-03`.
- [x] `SAV-02` no repuntar `_dbField` desde worker + `finally` (conexión local + ambient) + `CreateTables` en template (commit `3a59f854`).
- [x] `CON-01` transacción del día con `finally` que revierte si la corrutina se aborta (commit `2ba82b9c`). *Nota:* se mantiene el `yield` dentro de la transacción (bufferizar del todo queda para Fase 2), pero ya no puede quedar colgada.
- [~] `CON-02` `WaitForBackgroundBatch()` + `CancelBackgroundBatches()` (CTS) + `ThrowIfBatchCancelled()` en puntos seguros de `StartNewSeason`; lock de navegación en el rollover (commits `3a59f854`, `2ba82b9c`, `35e9a28f`).
- [x] `UI-01` reset de `_isLoading`/spinner/botón en `OnDisable` + navegación bloqueada también en simulación normal (commit `2ba82b9c`).
- [x] `UI-02` `ScreenManager.NavigationLocked` + guard en config → Menú principal; sidebar deshabilitada en `ShowLoading` (commit `2ba82b9c`).
- [x] `SEA-01` eliminado el doble archivado histórico (commit `3a59f854`).
- [x] `SEA-02` reset anual de `first_apron_hard_capped` en `StartNewSeason` (commit `3a59f854`).
- [x] `SEA-03` `SaveRegularSeasonGames` devuelve bool; `GenerateSchedule` devuelve -1 si falla; Preseason no marca `generated` y avisa (commit `3a59f854`).
- [x] `SEA-05` escape de diagnóstico en play-in sin seeds: si no se puede construir el bracket, la temporada se cierra como `finished` en lugar de bloquearse (commit `35e9a28f`).
- [x] `SAV-03` metadata del slot al final de `PreseasonController.OnContinue` (commit `3a59f854`).
- [x] `SAV-04` `RunMigrations` en transacción + `user_version` al final + `AddColumnIfMissing` por columna en players/offers (commit `3a59f854`).
- [x] `SAV-05` `InitSaveSlot` devuelve bool con try/catch; los llamadores (MainMenu/LoadGame) abortan si falla (commit `3a59f854`). *Nota:* sin `integrity_check` previo (CreateTables/migraciones ya detectan corrupción).
- [x] `SAV-06` clon+seed en `RunInTransaction` + marcador `slot_initialized` (commit `3a59f854`).
- [x] `SAV-07` transacciones en `AutoSeedLineup`, `EnsureLineupSeeded`, `SavePreseasonGames`, `EnsureGLeagueSeeded` (commit `3a59f854`).
- [x] `ECO-01` buyout: debita el tramo actual del presupuesto + **tabla `dead_money`** persistente; `StartNewSeason` cobra cada tramo futuro y lo registra como gasto (commit `35e9a28f`).
- [x] `ECO-02` revalidación en `MarketController.OnConfirmTrade` **y** en la aceptación de ofertas IA del Dashboard (commit `2ba82b9c`, `35e9a28f`).
- [x] `SEA-04` resuelto por idempotencia: marcador `rollover_pre_{seasonId}` en `schema_migrations`; si el rollover falla y se reintenta, **no** se vuelven a resolver opciones ni a decrementar contratos; el cambio de equipo del manager se movió dentro de `StartNewSeason` (commit `35e9a28f`).
- [x] `SEA-13` (nuevo) `TryEscapeStuckPhase()`: si no quedan partidos y la fase no es terminal, regenera el calendario si falta o avanza/cierra la fase. Llamado desde el early-return de `ProcessGameDayRoutine` (commit `35e9a28f`).
- [~] Tests: añadido `ValidateTrade_AcceptingTwoForOne_AtMaxRoster_ReportsError` (86 tests). *Faltan* `SaveSlot_CorruptSavesJson_*`, `StartNewSeason_Archives…Once`, `Dashboard_OnDisable…`, `SimulateGame_Overtime…` (requieren refactor/inyección de rutas).

### Fase 2 — Arquitectura (~2-4 semanas)

- [ ] Capa de transacción: helper `ExecuteInTransaction`, mutex/cola de escritura, guardas `IsInTransaction`, `try/finally` en `RunInBackground*`.
- [ ] Servicio de traspasos/contratos: mutación centralizada (`MovePlayerToTeam`) + `ValidateTrade` obligatorio.
- [ ] Rollover único: opciones/decrementos dentro de `StartNewSeason`; reset de estado transitorio (`SEA-04`, `SEA-10`).
- [ ] Esquema: índices (`players.team_id`, `messages.manager_id`, `games.season_id`…), `UNIQUE` (HOF, dorsales), ids monótonos (`QA-02`, §4).
- [ ] Rendimiento: cachear standings/records del día, agregados SQL (N+1), `LIMIT` en bandeja, `Destroy` de texturas (`REN-01`, `CON-05..CON-10`).
- [ ] Integridad de simulación: `SIM-01/02/03/04`.
- [ ] Corregir `season_records` (`SEA-11`) y `GetActiveSeason` (`SEA-12`).
- [ ] Limpiar código muerto (`CON-08`, `QA-01`, `QA-08`, `PreseasonGameData`, `FullScreenUI`, `LoadSidebarIcons`) y corregir docs (`QA-07`).

### Fase 3 — Profundidad (~1-3 meses)

- [x] Progresión por arquetipo/edad/minutos/entrenamiento (`PlayerProgression`: edad + potencial como techo + minutos; `SEA-08/09/10` resueltos de raíz).
- [x] IA con cap/aprons real (`ECO-06`). *Pendiente:* economía estable a 20+ temporadas (`ECO-09` nómina 7/12 y `ECO-10` préstamos).
- [x] Draft persistente/resumible (`SEA-07`) + scouting con riesgo (busts).
- [~] Plan de partido (ritmo/ataque/defensa: datos + simulador + IA + panel en QUINTETO). *Pendiente:* staff con esquemas.
- [ ] Dashboard de cap/aprons + negociación de agente.

### Fase 4 — Pulido (~1 mes)

- [ ] UX: confirmaciones, feedback de errores, tooltips, scrollbar única, filtros/orden, formato consistente (§6).
- [ ] QoL: autosave/checkpoint, export/import de liga, resumen de temporada.
- [ ] Balance: asistencias (`SIM-10`), fatiga (`SIM-09`), rebotes (`SIM-11`), química (`SIM-13`), All-Star (`SIM-14`).
- [ ] Killer feature elegida (recomendado: **Modo histórico** o **Motor de narrativa**).

---

## 11. Falsos positivos / no tocar sin revisar

- **Acumulación de callbacks entre visitas**: en Unity 6 `UIDocument.OnDisable()` anula `rootVisualElement` y `OnEnable` recrea el árbol → **no se acumulan handlers cruzando visitas**. El bug real es el re-bind **intra-visita** (`UI-04`) y el `??=` (`UI-03`). Corregir la doc, no el modelo.
- **`_ambientDb`** se restaura correctamente en `finally` (`DatabaseManager.cs:145-148`). **No** añadir mutex alrededor de `AsyncLocal`.
- **Suscripción (abonos)**: no se acredita triple (la query de existencia usa `gameDay=0` y `GetFinanceRecord` ignora el día cuando `<=0`). Verificado.
- **Picks protegidos/swaps**: la resolución individual no duplica propiedad (salvo solapes, `ECO-15`).
- **`TradeHelper.EvaluateTrade`** usa `UnityEngine.Random` y **solo** se llama en main thread; no moverlo al camino IA.
- **`RunInBackground`** sin WAL es inocuo (WAL es propiedad persistente de la DB).
- **`overall` capado por `potential`**: invariante intencional; si se toca, ver `.agent/MEMORY.md` §7.1.

---

## 12. Bitácora de implementación

> Añade una línea por sesión de trabajo: fecha, commit, hallazgos cerrados, notas.

| Fecha | Commit | Cerrados | Notas |
|---|---|---|---|
| 2026-09-26 | `d565623e` | — | Auditoría creada (documento inicial). Rama `auditoria-final`. |
| 2026-09-26 | `3a59f854` | SAV-01..07, SEA-01/02/03 | Persistencia robusta: `saves.json` atómico + `.bak` sin borrado por metadatos ilegibles, plantilla sin race (`_dbField`), migraciones transaccionales, clon+seeds atómicos, metadata del slot al final, archivado histórico único, reset anual del hard cap, calendario no se marca generado si falla el guardado. |
| 2026-09-26 | `2ba82b9c` | CON-01, UI-01, UI-02 (+ECO-01/02 y CON-02 parciales) | `finally` que revierte la transacción del día si se aborta la corrutina; reset de `_isLoading`/spinner/navegación en `OnDisable`; `ScreenManager.NavigationLocked` + guard en config; `WaitForBackgroundBatch()`; lock en el rollover; buyout con coste y guardas; revalidación de traspasos en `OnConfirmTrade`. |
| 2026-09-26 | `35e9a28f` | SEA-04, SEA-05, SEA-13, ECO-01, ECO-02, CON-02 | Escape de soft-locks (`TryEscapeStuckPhase()`: regenera el calendario si falta o cierra la fase); revalidación de la aceptación de ofertas IA del Dashboard; tabla `dead_money` persistente + cobro por temporada; rollover reintentable (`rollover_pre_{seasonId}`); cancelación de lotes de fondo (`CancellationTokenSource`); +1 test de `ValidateTrade`. |
| 2026-09-26 | `1b8cdbf2` | — | Hotfix de compilación: sobrecarga genérica de `TrackBatch` (Task<T>). |
| 2026-09-26 | `0cab1dd4` | SIM-01/02/03/04/12/13 | Integridad de simulación: ventaja de casa al local real (y química simétrica), desempate garantizado (OT y respaldo de <2 jugadores), sin filas de 0 minutos (+migración de limpieza) y rotación todo-0 → automática con quinteto inicial según planes. +5 tests. |
| 2026-09-26 | `b02f2a96` | QA-01, SEA-12, §4 | Esquema: índices que faltaban (`players.team_id`, `games.season_id`, `messages(manager_id,id)`), `[Table]` de `player_season_stats`, una sola temporada activa (`GetActiveSeason` ordenado + `CreateSeason` desactiva) y `AllocatePlayerId()` monótono para el draft. |
| 2026-09-26 | `1504f9db` | SEA-11 (parcial) | `season_records` completo: campeón, finalista, MVP de finales, MVP de temporada, ROY y primer quinteto (faltan All-Star/DPOY/Sexto Hombre/2º quinteto, que el juego no calcula). |
| 2026-09-26 | `d4aae4f5` | ECO-03/04/05/07/08, SEA-10 | Economía: límite two-way y Bird rights en la maduración de ofertas, luxury tax positivo, ofertas normalizadas en el rollover (maduran en 7 días), reset del estado transitorio (lesiones/fatiga/trade block/química/confianza) y `MovePlayerToTeam` en traspasos (tenure + flags). |
| 2026-09-26 | `49379e1c` | REN-01, UI-03/05, CON-07/08, QA-07/08 | Rendimiento y UI: `AchievementService` filtra por temporada y `GetStandingsGames(season)`; bandeja del dashboard con `LIMIT` (`GetRecentMessages`); scrollbar re-bindeada en cada visita (Stats/Editor); crash de Entrenamiento resuelto (se re-aplica la tarjeta seleccionada + `FirstOrDefault`); eliminados `GetLeagueTop*` muertos; `FullScreenUI` null-safe; docs corregidas sobre callbacks. |
| 2026-09-26 | `18d72664` | SIM-05, CON-06 | Alineación incompleta (<5 sanos) pasa a resultado sintético con aviso, en vez de simular con 2-4 en pista; partidos jugados por jugador en **una sola query** (`GetGamesPlayedByPlayerInSeason`) para las renovaciones IA (fin del N+1). |
| 2026-09-26 | `fbf5de69` | CON-09, CON-10 | Memoria: `TeamImages.Invalidate()` destruye los `Sprite`/`Texture2D` creados en runtime (los de `Resources` no se tocan) y limpia `_previewCache`; `PlayerPhotoHelper` cachea texturas por ruta e invalida al sobrescribir o cambiar de ámbito. |
| 2026-09-26 | `3dffd1ca` | Fase 3 · scouting | **Scouting con riesgo (busts)**: los prospectos se muestran como **estimación** (`overall ±rango`); el margen lo fija la reputación de los ojeadores (`ScoutingHelper.MaxError`: rep 1 → ±5, rep 5 → ±1) con error triangular. Al elegir el pick se **revela el valor real** (`Reveal`) y se recalcula el overall → puede salir mejor (boom) o peor (bust). La IA usa la misma estimación. +6 tests. |
| 2026-09-26 | `3e6ed61d` | Fase 3 · staff | El **asistente** (`ASISTENTE`) tiene esquema propio (`employees.scheme_pace/offense/defense` + migración y backfill con `Rng`), **matiza el plan del usuario** (`CoachScheme.Blend`, media) y aporta **bonus/malus al rating** según reputación (`RatingBonus`, −2..+2). Se muestra en el panel PLAN DE PARTIDO de QUINTETO. +4 tests. |
| 2026-09-26 | `5b44d4f7` | ECO-09, ECO-10 | Economía a largo plazo: la nómina se cobra en **12 plazos** de `salario/12` (antes 7 → solo ~58% del sueldo, los presupuestos divergían al alza); los préstamos se clasifican como **ingreso** (`TYPE_LOAN_IN`) y **gasto** (`TYPE_LOAN_REPAY`) con importes positivos, incluidos en las filas y el balance de Finanzas, y migración que reclasifica los registros antiguos. |
| 2026-09-26 | `63d5048e` + `16b4782b` | Fase 3 · plan de partido | Táctica persistente por equipo (`teams.plan_pace/plan_offense/plan_defense` + migración): el simulador ajusta posesiones (ritmo), sesgo de tiro de tres (interior/exterior) y pérdidas/faltas (defensa). La IA deriva el plan de su `TeamStrategy` (Contend rápido/agresivo/exterior; Rebuild lento/interior). Panel de 3 filas en QUINTETO + 6 tests. |
| 2026-09-26 | `2ef769ad` | Fase 3 · progresión | `PlayerProgression` (helper puro): el offseason combina edad, **potencial** (techo real: un jugador en su techo no sube), **minutos de la temporada anterior** (jugar desarrolla; no jugar estanca) y prioridad posicional. +6 tests. |
| 2026-09-26 | `830316da` | SEA-07 | Draft **persistente y resumible**: tabla `draft_state` (season_id, cursor, completed); si el draft ya terminó no se repite, y al recargar a mitad se reanuda desde el pick guardado sin duplicar la clase (no se limpian los flags de rookie de los ya elegidos). |
| 2026-09-26 | `1550aab2` | ECO-06 | IA con modelo de cap/aprons: `TradeHelper.CanAISignExternal` (espacio de cap → MLE por apron → mínimo, tope del 2º apron) y `CanAIRenew` (Bird hasta el 2º apron); aplicados en `TrySignFreeAgent` y `ProcessAIRenewals`. +4 tests. |
| 2026-09-26 | `8e2a9185` | UI-07 | La simulación rápida avisa con un modal cuando se detiene por un error (antes solo iba a consola). |
| 2026-09-26 | `8cf34612` | SEA-08, SEA-09 | `overall` con **fuente única**: `GetCalculatedAverage()` redondea y capa por potencial, `RecalculateOverall()` se llama tras mentoring, progresión/declive, training, desarrollo G-League y Editor. +4 tests (`PlayerDataTests`). |
| 2026-09-26 | `1bea1c43` | CON-03, CON-04 | Concurrencia: `RunInBackground*` espera al lote anterior (single-flight), evitando solapes de estado/RNG y contención del lock; `BusyTimeout` de SQLite de 1 s a **5 s** para que la UI espere en vez de fallar con `SQLITE_BUSY`. |
| 2026-09-26 | `2d1f880d` | Tests Fase 1 | Infra de tests de slots (`GameSaveManager.BaseDirOverride`) + 7 tests de persistencia (`GameSaveManagerTests`): metadata corrupto no borra DBs, backup, reclamo atómico y `-wal`/`-shm`. |

**Hallazgo nuevo detectado durante la implementación (ya corregido):**
- `SEA-13` · **ALTO** · `DashboardController.ProcessGameDayRoutine`: si `FindNextGameDay() == 0` (no quedan partidos sin jugar) el método hacía `yield break` **antes** del bloque de transición de fase; con la temporada en `regular` y todos los partidos jugados, la fase no avanzaba nunca → soft-lock. **Fix aplicado (`35e9a28f`):** `TryEscapeStuckPhase()` se llama en el early-return y regenera el calendario si falta o avanza/cierra la fase (cubre también `SEA-05`).

**Fase 1 completada** salvo el bloque de tests que necesita refactor (inyección de la ruta de la DB para poder probar `GameSaveManager`/`InitSaveSlot`/`StartNewSeason` en EditMode).

**Verificación pendiente en Unity:** compilar (el editor estaba abierto durante los cambios) y lanzar EditMode (86 tests). Probar en partida: cargar/borrar slots, cerrar a mitad de simulación, RESET del Editor, traspaso tras fichar, buyout (comprobar que genera `dead_money` y que al año siguiente se cobra) y avance de temporada (el hard cap debe liberarse).

---

## Anexo · Métricas de la auditoría

- Archivos `.cs`: **152** · Líneas: **62.385**
- Pantallas `UIDocument`: **47** (todas con `case` en `GoTo` y campo asignado)
- Tests EditMode: **85** (7 archivos) — `TradeHelperTests` 22 · `GLLeagueHelperTests` 22 · `AdvancedStatsHelperTests` 11 · `ObjectiveHelperTests` 10 · `HallOfFameHelperTests` 9 · `GameSimulatorTests` 9 · `EditModeSmokeTests` 2
- Archivos con `[Indexed]`: **4**
- Superficie de red/procesos externos: **ninguna** (no hay `UnityWebRequest`, `HttpClient`, `Process.Start`): el "apartado de seguridad" se reduce a robustez frente a datos corruptos.

**Sistemas frágiles identificados:** `DashboardController` (hub con demasiadas responsabilidades), `StartNewSeason` (rollover monolítico), `DatabaseManager` (facade acoplado a todo), ciclo de vida async (transacción entre frames + tareas no cancelables).
