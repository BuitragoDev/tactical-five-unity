# AGENTS.md — Tactical Five

> Instrucciones de contexto para agentes IA y desarrolladores que trabajan en este proyecto.
> Este archivo **se carga automáticamente** en cada sesión de opencode (raíz del repo).
> Es la puerta de entrada a la base de conocimiento completa (`Docs/` + `.agent/`).
> Estado verificado: HEAD `81d9e4f` (2026-08-16) · rama `main`.

---

## 1. Qué es este proyecto (30 segundos)

**Tactical Five** — sim de gestión NBA (tipo Football Manager pero de baloncesto), **single-player**, en español, `v1.0.0 · Beta`.
- **Motor:** Unity `6000.3.15f1` (Unity 6), **UI Toolkit** (UXML/USS), escritorio 1920×1080.
- **Arquitectura:** **una única escena** (`MainMenu.unity`) con **41 pantallas** como GameObjects `UIDocument`; todo el estado vive en **SQLite**.
- **Juego:** gestionas una de 30 franquicias NBA-like en temporadas infinitas: plantillas, traspasos, agentes libres, salary cap, finanzas, entrenamiento, moral/química/lesiones, draft, playoffs, premios, récords, Hall of Fame, dorsales retirados, logros del GM.
- **Empresa/producto:** BuitragoStudio · TacticalFive.

**Al empezar una sesión en este repo, primero lee estos docs (ver §2):**
`Docs/PROJECT_OVERVIEW.md`, `Docs/ARCHITECTURE.md`, `.agent/SKILLS.md`, `Docs/DATA_MODEL.md`.

---

## 2. Base de conocimiento (dónde está todo)

| Doc | Contenido | Lee esto cuando… |
|---|---|---|
| `Docs/PROJECT_OVERVIEW.md` | Qué es el juego, stack, bucles de juego, estado | Empiezas cualquier tarea |
| `Docs/ARCHITECTURE.md` | Módulos, singletons, ciclo de vida, dependencias (Mermaid) | Tareas que cruzan sistemas |
| `Docs/GAMEPLAY.md` | **Todas las mecánicas con fórmulas exactas** | Tocas simulación, economía, contratos, moral, draft |
| `Docs/SYSTEMS.md` | 18 sistemas: responsabilidad, archivos, métodos clave, riesgos | Necesitas una API de `DatabaseManager` o un sistema completo |
| `Docs/UI_TOOLKIT.md` | Tema/UXML/USS, patrón de controller, **41 pantallas**, árbol de navegación | Trabajas en UI |
| `Docs/SCENES.md` | La escena única, sus 44 GameObjects | Tocas la escena / añades pantalla |
| `Docs/EVENTS.md` | Todos los canales de comunicación (static, DB messages, PlayerPrefs, callbacks) | Añades un evento/mensaje nuevo |
| `Docs/DATA_MODEL.md` | **Las 42 tablas SQLite campo a campo**, seeds, migraciones | Cambias esquema, añades tabla/columna |
| `Docs/SAVE_SYSTEM.md` | Slots, `template.db`, flujos save/load, versionado | Trabajas en persistencia |
| `Docs/CODE_GUIDELINES.md` | Convenciones reales, patrones a seguir/evitar | **Antes de escribir cualquier código nuevo** |
| `Docs/TODO_TECHNICAL_DEBT.md` | Deuda técnica priorizada P0–P3 + código muerto | Arreglas bugs o decides refactors |
| `Docs/IMPROVEMENT_PROPOSALS.md` | Propuestas de mejora futuras (en español) | Ideación |
| `NEXT_PROPOSALS.md` | Mejoras pendientes ordenadas (G-League/IR pendiente) | Sabes qué falta por hacer |
| `.agent/SKILLS.md` | Onboarding exprés para agentes | Quieres el camino rápido |
| `.agent/MEMORY.md` | Estado persistente, decisiones y porqués, invariantes "nunca tocar" | Dudas sobre una decisión pasada |
| `.agent/GLOSSARY.md` | Términos del juego (español) y clases clave | No conoces la jerga del juego |

**Nota importante (opencode):** este `AGENTS.md` y los 6 docs listados en `opencode.json → instructions` se cargan automáticamente. El resto NO — usa la sección §3 (carga perezosa) para leerlos solo cuando la tarea lo requiera.

---

## 3. Carga perezosa de docs (léelos cuando apliquen)

**CRÍTICO para agentes:** lee el doc relevante con tu herramienta Read **solo cuando la tarea lo necesite**; no los leas todos al inicio (los pesados como `GAMEPLAY.md`/`SYSTEMS.md`/`DATA_MODEL.md` están fuera del auto-load a propósito).
- Tarea de **UI/pantalla** → `Docs/UI_TOOLKIT.md` + `Docs/SCENES.md`
- Tarea de **regla de juego/mecánica** → `Docs/GAMEPLAY.md` §correspondiente
- Tarea de **datos/schema/query** → `Docs/DATA_MODEL.md` + `Docs/SAVE_SYSTEM.md`
- Tarea de **persistencia/migración** → `Docs/SAVE_SYSTEM.md` + `Docs/DATA_MODEL.md §5`
- Tarea de **sistema concreto** (traspasos, draft, economía…) → `Docs/SYSTEMS.md`
- Tarea de **comunicación/eventos** → `Docs/EVENTS.md`
- Tarea de **arreglo de bug/deuda** → `Docs/TODO_TECHNICAL_DEBT.md`

---

## 4. Hechos arquitectónicos esenciales (NO contradecir)

| Hecho | Consecuencia |
|---|---|
| **Escena única** `MainMenu.unity`; **41 pantallas** como `UIDocument` GOs; `ScreenManager.GoTo(GameScreen, mode)` conmuta `SetActive` | No cargues escenas. Nunca navegues con `SetActive` directo — usa `ScreenManager.Instance.GoTo`. |
| **Toda la persistencia es SQLite** vía `DatabaseManager.Instance` (sqlite-net incluido; `DatabaseManager` dividido en **9 partial classes** por dominio: `.Teams/.Players/.Staff/.Manager/.Games/.Seeding/.Records/.Achievements`) | Nunca abras tu propia conexión. Todo acceso pasa por `DatabaseManager`. |
| **Sin prefabs y sin ScriptableObjects de juego.** Contenido = seeders + tablas SQLite; vistas = UXML+USS | Para nueva entidad → tabla+seeder; para vista nueva → UXML+USS. |
| **Sin event bus** (ni `Action`/`UnityEvent` entre sistemas). Comunicación: mensajes DB (`MessageData`), estado estático (`GameResultCache`, `ScreenManager.*`, cola de toasts de `AchievementService`), `PlayerPrefs` (solo settings), callbacks UI | No inventes un sistema de eventos. Sigue los canales existentes. |
| **Todos los controllers heredan `UIScreenController`** (base en `Scripts/Core/UIScreenController.cs`, 575 ln): fullscreen, inyección Header/Sidebar, navegación, cursores, modal de configuración, `RefreshHeader` | Nuevas pantallas: hereda la base y sobreescribe solo lo que difiera. |
| **Reglas de juego en clases static puras:** `GameSimulator`, `TradeHelper`, `DraftGenerator`, `ScheduleGenerator`, `PlayoffsGenerator`, `QuickNewsGenerator`, `AchievementService`, `AdvancedStatsHelper`, `FogOfWarHelper`, `HallOfFameHelper`, `MatchupPreview`, `ObjectiveHelper`, `GLeagueHelper` | Reglas nuevas → helper static; controllers delgados. |
| **Simulación no determinista** (`UnityEngine.Random` en main thread; `System.Random` thread-static `_aiRng`/`Rng` en hilos de fondo) | No esperes resultados reproducibles. |
| **Async DB:** los lotes pesados van fuera del main thread vía `RunInBackground`/`RunInBackgroundAsync` (WAL + conexión ambient `AsyncLocal` + `Task.Run`). La simulación de partido y el draft se quedan en main thread **intencionadamente** | Los helpers de DB escriben en la conexión ambient del lote; **no toques `_db` fuera del main thread**. |
| **Schema versionado:** `schema_migrations` (migraciones de datos por nombre) + `PRAGMA user_version = 2`; migraciones de columnas aditivas vía `PRAGMA table_info` | Migración nueva: o `ALTER TABLE ADD COLUMN` (schema) o registro en `schema_migrations` (datos). |
| **Transacciones:** el día de partido (`ProcessGameDayRoutine`) y `StartNewSeason` son atómicos (savepoints anidados en playoffs) | No rompas los bloques transaccionales del día; no introduzcas `yield return null` dentro del bloque sim+bookkeeping. |
| **Cap/apron (2025-26) en `TradeHelper.cs`;** crecen +5%/año en `StartNewSeason`; copia en `league_settings` | Cambios de tope = 1 sola fuente (`TradeHelper`). |

---

## 5. Convenciones que DEBES respetar

- **Idioma:** strings de juego y comentarios en **español**; estructura de código en inglés es aceptable (es el mix actual). No fuerces identificadores ingleses para conceptos del juego.
- **Sin namespaces**; clases globales.
- **Todo el código DB en `DatabaseManager`** (en el partial correcto); todo el wiring UI en el `OnEnable` del controller.
- **Ciclo de vida UI:** todo se re-inicializa en `OnEnable` (nunca `Start`); `Awake` solo para singletons/`FullScreenUI`.
- **Aleatoriedad:** `UnityEngine.Random` en main thread; `System.Random` (thread-static) en hilos de fondo. Nunca compartas `UnityEngine.Random` entre hilos.
- **Fechas:** strings `"yyyy-MM-dd"`. **Moneda:** `$"{value:N0}"` / `$"{value:N0} $"`.
- **Recursos runtime:** bajo carpetas `Resources/`; logos vía `Resources.LoadAll<Sprite>("Teams/Logos/{size}x{size}")` → `Dictionary<string,Sprite>`.
- **Modales:** overlay+box, `DisplayStyle.Flex/None`, botones de cierre registrados.
- **Feedback al jugador:** siempre `DatabaseManager.AddMessage(new MessageData { sender_type, ... })` (`sender_type`: 0=system, 1=player, 2=news).
- **Singletons:** `public static X Instance { get; private set; }` + guard en `Awake` + `DontDestroyOnLoad` propio.
- **Estilo:** indentación ~4 espacios; separadores `// ── NOMBRE ─────────────`.

---

## 6. Cómo añadir…

### …una nueva pantalla (UI)
1. `UI/Screens/MyScreen/MyScreen.uxml` + `.uss` (reutiliza `GlobalVariables`/`Typography`/`Utilities`).
2. GameObject `MyScreenDocument` en `MainMenu.unity`: `UIDocument` (PanelSettings = `TacticalFivePanelSettings`, sourceAsset = UXML) + `FullScreenUI` + controller.
3. `GameEnums.cs`: valor en `GameScreen`.
4. `ScreenManager`: campo `[SerializeField] UIDocument` + `case` en el switch de `GoTo`.
5. Controller: **`MyScreenController : UIScreenController`** (la base ya da fullscreen, chrome, nav, cursores y modal de config).
6. Navegar con `ScreenManager.Instance.GoTo(GameScreen.MyScreen)`; si va en el menú, añade entrada al Sidebar.

### …una nueva mecánica/regla
1. Modelo `[Table]` en `Scripts/Data/` (+ `CreateTable<T>()` en `CreateTables` si es tabla nueva).
2. CRUD en `DatabaseManager` (partial correcto).
3. Migración: columna (`PRAGMA table_info` + `ALTER TABLE ADD COLUMN`) o datos (`schema_migrations`).
4. Reglas puras en helper static (`TradeHelper`-style).
5. Hook en `ProcessGameDayRoutine` / `StartNewSeason` / acción de un controller.
6. Feedback vía `MessageData` + `Refresh()`.

### …datos seed
Método `SeedX()` en `DatabaseManager` con guard de tabla-vacía en `SeedStaticDataIfNeeded()`; datasets grandes → clase seeder (`*Seeder.cs`) referenciada.

---

## 7. Invariantes críticas (no romper)

1. **`overall` pretende ser `mean(11 atributos)` capado por `potential`**, pero `GetCalculatedAverage()` usa división entera y `ApplyMentoring()` no lo recalcula. Mantenlo consistente y verifica cada mutación de atributos.
2. **Constantes de cap/apron en `TradeHelper.cs`** (única fuente; `league_settings` es copia; +5%/año).
3. **`GameResultCache.Clear()` al inicio de cada día simulado** — olvidarlo corrompe el flujo de resultados.
4. **`seasons.phase` machine:** `preseason → regular → playin → playoff → finished` — conduce dashboard, playoffs, premios.
5. **`MessageData.sender_type`** (0/1/2) — usado para filtros/iconos.
6. **`first_apron_hard_capped`** bloquea todo traspaso por encima del 1er apron tras usar NT-MLE.
7. **`player_season_stats` + `monthly_awards` tienen doble vía de creación** (CreateTable Y raw SQL) — cualquier cambio de schema debe tocar ambas.
8. **No tocar `_db` directamente fuera del main thread** — usar la conexión ambient de `RunInBackground`/`RunInBackgroundAsync`.
9. **Los contratos de jugador:** `salary` anual, `contract_years`, ofertas maduran a los 7 días; TO/PO mutuamente excluyentes → `guaranteed_years = max(0, years − 1)`.
10. **Ventana de traspasos IA:** Sep 1 → Feb 8; deadline día = **7 de febrero**; semana deadline = Feb 1–8 (rush IA 3–5 días).
11. **Ids G-League codificados:** los GameData con `game_type=gleague/gleague_playoff` guardan home/away = id de filial `+GAME_TEAM_ID_OFFSET (1000)`; descodificar SIEMPRE con `GLeagueHelper.DecodeGlTeamId`. Los prospectos simulan con id `+PROSPECT_ID_OFFSET (500000)`. La postseason GL (`GLeaguePostSeason`) nunca toca `seasons.phase`.

---

## 8. Trampas y código muerto (conocido)

- **`PreseasonGameData`** (`[Table("preseason_games")]`) — **código muerto**: la tabla nunca se crea ni se usa (la pretemporada usa `games.game_type="preseason"`). No lo referencies.
- **`UIScreenController.LoadSidebarIcons`** — **muerto**: los iconos reales los carga `SidebarController.LoadIcons`.
- **Carpeta `UI/Screens/LegalNotice/`** — **huérfana**: el modal legal está inline en `MainMenu.uxml` (`BtnLegal`).
- **12 controllers overridean `RegisterCallbacks()` SIN `base`** (Editor, EndSeason, GameResults, LoadGame, MainMenu, MatchDay, NewSeason, PlayerAwards, Preseason, Quintos, SeasonSummary, SelectTeam) — no reciben el modal de config ni chrome vía base. No copies este patrón en pantallas nuevas.
- **`FullScreenUI.Awake` duplica `UIScreenController.MakeFullscreen`** — redundante pero inofensivo.
- **Doble población del header** (`RefreshHeader` base + `HeaderController.Attach`) — inofensivo.
- **`GameResultsController`** re-implementa el nav referenciando elementos del sidebar que NO existen (`NavRecords`/`NavSponsors`/`NavTV`) — null-safe, no toques.
- **No reintroducir:** `SQLiteAsync.cs` (borrado; async ya es interno), `GameScreen.Settings`/`SettingsController` (eliminados), flags de migración en `PlayerPrefs` (ahora viven en `schema_migrations`).
- **`Assets/_Recovery/`** — escenas de crash-recovery gitignoreadas, no usadas.
- **`Assets/_TacticalFive/Data/Database.meta`** — meta huérfano sin carpeta, inofensivo.

---

## 9. Comandos

**No hay lint ni typecheck** (C# se compila solo en el editor de Unity). No hay test runner CLI configurado en `package.json`.

**Tests unitarios (EditMode, Unity Test Runner):** 78 `[Test]` en 7 archivos `Assets/_TacticalFive/Tests/Editor/`:
- `TradeHelperTests.cs` (20) · `GLLeagueHelperTests.cs` (22) · `AdvancedStatsHelperTests.cs` (11) · `ObjectiveHelperTests.cs` (10) · `HallOfFameHelperTests.cs` (9) · `GameSimulatorTests.cs` (4) · `EditModeSmokeTests.cs` (2)

**Correr en el editor:** *Window → General → Test Runner → EditMode → Run All*.

**Correr por CLI (headless):**
```bash
<ruta-al-Unity> -batchmode -projectPath <ruta-del-proyecto> -runTests -testPlatform EditMode -testResults results.xml -quit
```
(Linux: típicamente `/opt/unity/Editor/Unity` o similar.)

**Nota sobre tests:** los tests viven **sin asmdef** en una carpeta `Editor` (compilan en `Assembly-CSharp-Editor`) — es una limitación conocida de Unity 6 (un test assembly no puede referenciar la predefined assembly). `GameSimulator.SimulateGame` depende de `DatabaseManager.Instance` y **no es testeable en EditMode** sin refactor. `GameSimulatorTests.cs` (4 tests) solo cubre helpers públicos estáticos (position multipliers, target minutes).

**Verificación tras tocar reglas puras** (`TradeHelper`, `AdvancedStatsHelper`, `ObjectiveHelper`, `HallOfFameHelper`, migraciones): corre el Test Runner EditMode.

---

## 10. Archivos clave (los más importantes)

| Archivo | Rol |
|---|---|
| `Scripts/Core/ScreenManager.cs` | Navegación (41 `UIDocument`, `GoTo` switch) |
| `Scripts/Core/UIScreenController.cs` | Base de los 41 controllers (chrome, nav, config modal) |
| `Scripts/Data/DatabaseManager.cs` + 8 partials | Único acceso a DB (~150 métodos públicos) |
| `Scripts/Data/GameSaveManager.cs` | Slots, `template.db`, `saves.json` |
| `Scripts/GameSimulator.cs` | Motor de partidos (por posesión) |
| `Scripts/Core/TradeHelper.cs` | Salary cap, aprons, luxury tax, valoración IA |
| `Scripts/UI/DashboardController.cs` | El hub: día, sim, traspasos IA, economía, modales |
| `Scripts/UI/RosterController.cs` | Plantilla, renovaciones, buyouts, trade block |
| `Scripts/UI/MarketController.cs` | Traspasos, FA, S&T, deadline |
| `Scripts/Core/GameEnums.cs` | `GameScreen` (41), `GameMode` |
| `Scripts/Core/GLeagueHelper.cs` | Reglas G-League: asignación, desarrollo, codificación ids |
| `Scripts/Core/GLeagueScheduleGenerator.cs` | Calendario G-League (28 partidos/filial) |
| `Scripts/Core/GLeaguePostSeason.cs` | Playoffs G-League (QF→SF→CF→Final, mejor de 3) |
| `Scripts/Core/GLeagueStandings.cs` | Clasificación G-League (en memoria) |

---

## 11. Estado actual del repo

- **Commit:** `81d9e4f` (2026-08-16) · **Rama:** `main`.
- **Versión del juego:** `v1.0.0 · Beta`.
- **Docs sincronizados con este HEAD**; las desviaciones observadas y riesgos abiertos están en `Docs/TODO_TECHNICAL_DEBT.md`.
- **`PLAN.md`** (raíz): todas sus entradas están implementadas y mergeadas en `main`; úsalo como histórico, no como TODO.
- **Trabajo pendiente conocido:** G-League / IR / contratos two-way (`NEXT_PROPOSALS.md` D) y la deuda de `Docs/TODO_TECHNICAL_DEBT.md`.

---

## 12. Regla de oro al trabajar aquí

**Antes de asumir cómo funciona algo, lee el doc correspondiente (§2) o el código real.** Este proyecto tiene mucha lógica en lugares no obvios (economía en `DashboardController`, reglas de traspaso en `TradeHelper`, contexto cross-screen en estáticos). Los números en los docs son hechos verificados con `file:línea`; si crees que algo ha cambiado, verifica contra el código antes de editar la doc.
