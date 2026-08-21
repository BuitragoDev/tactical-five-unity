# IMPROVEMENT_PROPOSALS — Tactical Five

> Propuestas de mejora para hacer el juego más potente, basadas en el estado actual
> del proyecto. Este documento es la referencia futura del menú de mejoras; cada
> propuesta está agrupada por frente y priorizada por impacto vs. esfuerzo.
> Fecha del análisis: 2026-08-02 · Rama de referencia: `crear-mejoras2`.
> **Actualización (2026-08-16):** estado verificado en HEAD `81d9e4f`; las columnas
> "Estado" reflejan lo mergeado en `main`.

---

## Contexto

El juego ya es muy completo: motor de simulación por posesión, ciclo completo de
temporada (82 partidos + Play-In + Playoffs), economía (taquilla, abonos,
patrocinios, TV, préstamos, arena, luxury tax, buyout), salary cap con aprons y
hard cap, traspasos con picks, draft con lottery, soft systems (moral, confianza,
química, personalidades, relaciones), lesiones/fatiga, entrenamiento, staff,
récords/premios y fast sim con pausas ante ofertas/traspasos.

Fuentes consultadas: `Docs/PROJECT_OVERVIEW.md`, `Docs/GAMEPLAY.md`,
`Docs/SYSTEMS.md`, `Docs/SCENES.md`, `Docs/TODO_TECHNICAL_DEBT.md`, `PLAN.md`,
`.agent/MEMORY.md`.

---

## Frente 1 — Engagement / presentación (mayor "potencia" percibida)

El gap más evidente era: **el partido es invisible**. Se simula y solo se ve el resultado.

- ✅ **Play-by-play en vivo / crónica en texto** — **hecho** (`crear-mejoras2`, commit `9c69e09`, mergeado a `main`): toggle "Vista de Partido" (Directa/Play-by-play), overlay inmersivo en MatchDay con marcador acumulado, reloj, barra de progreso, boxscore en vivo ordenado por VAL, totales recalculados, velocidades x1/x3/x5/x10 y botón SALTAR/IR AL RESUMEN. Ver `GAMEPLAY.md` §2.
- ✅ **Logros/trofeos del GM** + palmarés visual enriquecido — **hecho** (28 logros, pantalla Logros, toast; HOF en Palmares; Dorsales).
- **Más variedad de audio** (solo 7 WAVs, `SYSTEMS.md §S15`).

## Frente 2 — Profundidad de gestión NBA (lo que los sims "potentes" tienen y aquí falta)

Marcado como pendiente/hueco por los docs (`GAMEPLAY.md` Open questions,
`MEMORY.md §3`):

- ✅ **Sign-and-trade real** — **hecho** (`crear-mejoras2`, en curso de merge): flujo de S&T de
  FA propio con Bird rights (`MarketController.ProcessSATrade`): sección "FA RECIENTES (BIRD RIGHTS)"
  en el panel de traspaso, firma + traspaso inmediato, dos `TradeData`, receptor bajo hard cap;
  `TradeHelper.ValidateTrade`/`EvaluateTrade` con `teamASignSalaries`/`teamBSignSalaries`;
  IA propone S&T y respeta `pendingSATIds`. El S&T de jugador entrante que expira (PLAN.md) se conserva.
- **Cap sheet / planificador de masa salarial a futuro** (años venideros, cap
  proyectado +5%/año). **Nota**: la parte **visual/informativa** ya está hecha
  (tab «CAP SHEET» en Finances) y la **simulación "¿y si firmo a X?"** ya está
  implementada; quedan ajustes de equilibrio.
- **Opciones de contrato** (team/player option) y años garantizados/no garantizados.
  **Nota**: implementadas en el modal de oferta (Roster renovaciones + Market FA) y en
  los mensajes de resultado (`FormatContractYears`); incluye maduración con re-firma de
  FA propio vía Bird rights (`last_team_id`, `IsOwnRecentFA`, modal de re-firma en
  `NewSeasonController`). Queda fino: soft cap / impacto de mercado en la re-firma.
- ✅ **Picks protegidos + swap** — **hecho**: `protected_from`/`is_swap`/`swap_original_team_id` en `DraftPickData`, resolución en `DraftGenerator`, UI en Market.
- **Trade deadline con evento real** — **hecho** (rama `crear-mejoras2`, commit `649c98e`): modal DEADLINE DAY en Feb 7 que intercepta el btnAction (IR AL MERCADO / CERRAR, una vez por temporada); rush IA con cooldown 3-5 días Feb 1-8, contenders ofrecen picks, tag `[DEADLINE]` en ofertas; badge `⏳ ÚLTIMOS X DÍAS` en Market.
- **Objetivos de temporada del propietario** con recompensas/cese (ya hay factor
  "objetivo" en asistencia y despido por presupuesto → expandir).
- ✅ **Rest / load management** — **hecho**: `TF_LoadMgmt_Enabled` (toggle en Quinteto) + back-to-back → modal para descansar hasta 2 jugadores cansados en `DashboardController`.
- **IR (injury reserve) / G-League / two-way contracts** — **hecho** (`NEXT_PROPOSALS.md` D): reserva de lesionados (`is_on_ir`, no cuenta en tope, modal plantilla llena), contratos two-way (`is_two_way`, salario fijo, máx 2, edad ≤23) y G-League ligera (`g_league_assigned`, desarrollo +1/7 días, stats procedimentales).

## Frente 3 — Simulación & AI (realismo)

- **AI de GMs más inteligente** — **hecho** (rama `crear-mejoras2`): enum `TeamStrategy` (Rebuild/Balanced/Contend) en `DashboardController`; cooldowns y densidad por estrategia (Contend 0.45/6d, Rebuild 0.40/8d, Balanced 0.25/15d; 3-5 en deadline), fire sale de rebuild (`TrySellVeteran`), contender busca upgrades OVR≤90 con pick futuro protegiendo jóvenes, ofertas al usuario según estrategia (`PickTradeTarget`/`BuildOfferPackage`) y Star FA que prioriza contender sobre rebuild (que tankea y no ficha OVR≥85). Ver `SYSTEMS.md` §S.
- ✅ **Analytics avanzados** — **hecho (parcial)**: eFG%, TS% y PER vía `AdvancedStatsHelper` en Stats + PlayerProfile. Queda WS/espaciado por posición.
- ✅ **Fog-of-war en valoraciones** (el ojeador da rangos, no OVR exacto) — **hecho**: `FogOfWarHelper` + Cartera + PlayerProfile (commit `a924226`).
- ✅ **Desarrollo/regresión más realista** — declive atlético por posición y mentoring
  de veteranos (**hecho**, commit `2281089`).

## Frente 4 — Modos y contenido

- **ProManager real** (`TODO_TECHNICAL_DEBT.md` B20): dificultad aumentada. El modal de
  restricciones del menú principal anuncia las reglas (`MainMenuController.OpenProModal`).
  Hecho: **cese por objetivo no cumplido** al fin de temporada (solo ProManager,
  `ShowObjectiveFiredModal`), **cese por presupuesto más fácil** (umbral 2 vs 3 en
  `CheckBudgetWarning`), **sin NT-MLE** (FA sobre el cap limitado a Taxpayer MLE vía
  `GetMaxOfferBreakdown(proManagerOnly:true)` y sin activación del hard cap por NT-MLE);
  lógica centralizada en `ObjectiveHelper`. **B20 cerrado.**
- **Expansión / liga personalizada** vía el editor de `template.db`.
- ✅ **Historial rico** — **hecho**: anillos/MVP/MVP-Finales por jugador (contadores en PlayerProfile/Trajectory), retiro de dorsales (pantalla Dorsales), hall of fame (Palmares + EndSeason), quintos de temporada (pantalla Quintos).

## Frente 5 — Técnico / rendimiento (potencia bajo el capó)

- ✅ **Trabajo en hilo de fondo con WAL** (`TODO_TECHNICAL_DEBT.md` B8) — **en lo crítico**:
  `SQLite.cs` es síncrono y no tiene `SQLiteAsyncConnection`, así que se usa WAL + `Task.Run`
  con una `SQLiteConnection` dedicada por hilo y una conexión "ambient" vía
  `AsyncLocal<SQLiteConnection>` (`DatabaseManager.RunInBackgroundAsync`/`RunInBackground`):
  todos los helpers escriben en la conexión de fondo sin tocar la principal, mientras la
  coroutine espera.
  Movidos fuera del hilo principal: **pre-lote diario de lesiones+físico** (`33f4e12`),
  **`StartNewSeason`** (`5bcca3b`), **traspasos/fichajes AI** (`71775bf`, con
  `System.Random` thread-safe `_aiRng` para el camino AI).
  **Decisión (2026-08):** la simulación de partidos (`GameSimulator.SimulateGame`) **no se
  mueve** a hilo de fondo — ya es rápida y estable en el hilo principal; B8 se da por cerrado
  con estos lotes. La generación del draft sigue en el hilo principal (revisar solo si hay
  bloqueos puntuales).
- **Caché de logos/estática** (`TODO_TECHNICAL_DEBT.md` B13) y `ListView` en tablas.
- Cerrar TODO pendiente: `Settings` muerto (`B7`), `CursorManager` duplicado (`B1`),
  tests para `GameSimulator`/migraciones (ya hay 17 para `TradeHelper`).

---

## Prioridad sugerida (impacto vs. esfuerzo)

| Prioridad | Iniciativa | Frente |
|---|---|---|
| **Hecho** | Play-by-play en vivo del partido (commit `9c69e09`) | 1 |
| **Hecho** | Cap sheet (info) + opciones de contrato TO/PO + re-firma Bird rights + S&T | 2 |
| **Hecho** | ProManager diferenciado (B20 cerrado) | 4 |
| **Hecho** | AI de GM más lista + analytics avanzados + fog-of-war | 3 |
| **Hecho** | Async DB (B8): pre-lote, StartNewSeason y AI trades en hilo de fondo (WAL + conexión ambient) | 5 |
| **Hecho** | Logros, picks protegidos/swap, load management, HOF, dorsales, quintos | 1/2/4 |
| **Hecho** | G-League / IR / contratos two-way (`NEXT_PROPOSALS.md` D) | 2 |

---

## Estado / seguimiento

Añadir aquí el estado de cada propuesta cuando se decida abordarla
(`pendiente` / `en curso` / `hecho` con referencia de commit o archivo).

| Propuesta | Estado |
|---|---|
| Play-by-play en vivo del partido | **Hecho** — commit `9c69e09` (situación de `crear-mejoras2`, mergeade a `main`) |
| Cap sheet (Finances → «CAP SHEET») | **Hecho** — `FinancesController.BuildCapSheet` + pestaña `PanelCap` (info read-only) |
| Sign-and-trade de FA propio (NBA clásico) | **Hecho** — `MarketController.ProcessSATrade` + `TradeHelper` sign salaries + IA |
| Trade deadline con evento real | **Hecho** — `DashboardController.ShowDeadlineDayModal` + `IsDeadlineWeek`/`IsFeb7OfYearEnd` + rush IA + badge Market (commit `649c98e`) |
| AI de GMs más inteligente (estrategia por equipo) | **Hecho** — `TeamStrategy` + cooldowns/densidad por estrategia + fire sale rebuild + Star FA por prioridad |
| Fog-of-war en valoraciones (ojeador da rangos) | **Hecho** — `FogOfWarHelper` + Cartera + PlayerProfile (commit `a924226`) |
| Declive atlético por posición + mentoring de veteranos | **Hecho** — aging por posición + mentoring (commit `2281089`) |
| Async DB (B8): pre-lote diario (lesiones + físico) en hilo de fondo | **Hecho** — `Task.Run` + WAL (commit `33f4e12`) |
| Async DB (B8): `StartNewSeason` en hilo de fondo | **Hecho** — conexión ambient `AsyncLocal` + WAL (commit `5bcca3b`) |
| Async DB (B8): traspasos/fichajes AI en hilo de fondo | **Hecho** — conexión ambient + `System.Random` thread-safe (`_aiRng`) (commit `71775bf`) |
| Contadores de honores del jugador (CAMPEONATOS/FINALES/MVP/MVP FINALS) | **Hecho** — campos `players.rings/finals_mvps/finals_played/season_mvps`, incremento en `SaveSeasonEndRecords`, contadores en header de PlayerProfile/Trajectory (commits `d52d5cf`, `a32ae75`) |
| **Matchup preview / pronóstico del partido** | **Hecho** — `MatchupPreview.cs` + panel PRONÓSTICO en MatchDay |
| **Retiro de dorsales (jersey retirement)** | **Hecho** — `retired_numbers` + seeds (53+17) + pantalla Dorsales + `AssignJerseyNumber` |
| **Picks protegidos + swap** | **Hecho** — `draft_picks.protected_from/is_swap/swap_original_team_id` + resolución en `DraftGenerator` + UI en Market |
| **Logros/trofeos del GM** | **Hecho** — catálogo 28 logros (`AchievementCatalog`) + `gm_achievements` + pantalla Logros + toast |
| **Salón de la Fama** | **Hecho** — `HallOfFameHelper` + `hof_players` (~100 leyendas) + inducción en `StartNewSeason` + panel Palmares |
| **Analytics avanzados (eFG%/TS%/PER)** | **Hecho** — `AdvancedStatsHelper` en Stats + PlayerProfile |
| **Load management (descanso en back-to-back)** | **Hecho** — `TF_LoadMgmt_Enabled` (Quinteto) + modal de descanso en Dashboard |
| **Opciones de contrato TO/PO** | **Hecho** — toggles en renovaciones/FA + `guaranteed_years` + `FormatContractYears` |
| **Re-firma de FA propio (Bird rights)** | **Hecho** — `last_team_id`/`IsOwnRecentFA` + modal de re-firma en NewSeason |
| **G-League / IR / contratos two-way** | **Hecho** — `NEXT_PROPOSALS.md` D (IR, two-way, G-League ligera) |
