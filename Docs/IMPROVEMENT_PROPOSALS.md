# IMPROVEMENT_PROPOSALS — Tactical Five

> Propuestas de mejora para hacer el juego más potente, basadas en el estado actual
> del proyecto. Este documento es la referencia futura del menú de mejoras; cada
> propuesta está agrupada por frente y priorizada por impacto vs. esfuerzo.
> Fecha del análisis: 2026-08-02 · Rama de referencia: `crear-mejoras2`.

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
- **Mejores vitrinas**: box score detallado por cuarto, "jugador del partido",
  gráfica de rachas de la clasificación.
- **Logros/trofeos del GM** + palmarés visual enriquecido.
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
- **Picks protegidos** y más flexibilidad de transferencia de picks.
- **Trade deadline con evento real** — **hecho** (rama `crear-mejoras2`, commit `649c98e`): modal DEADLINE DAY en Feb 7 que intercepta el btnAction (IR AL MERCADO / CERRAR, una vez por temporada); rush IA con cooldown 3-5 días Feb 1-8, contenders ofrecen picks, tag `[DEADLINE]` en ofertas; badge `⏳ ÚLTIMOS X DÍAS` en Market.
- **Objetivos de temporada del propietario** con recompensas/cese (ya hay factor
  "objetivo" en asistencia y despido por presupuesto → expandir).
- **Rest / load management** (los back-to-backs existen, pero no la decisión de descanso).
- **IR (injury reserve) / G-League / two-way contracts**.

## Frente 3 — Simulación & AI (realismo)

- **AI de GMs más inteligente** — **hecho** (rama `crear-mejoras2`): enum `TeamStrategy` (Rebuild/Balanced/Contend) en `DashboardController`; cooldowns y densidad por estrategia (Contend 0.45/6d, Rebuild 0.40/8d, Balanced 0.25/15d; 3-5 en deadline), fire sale de rebuild (`TrySellVeteran`), contender busca upgrades OVR≤90 con pick futuro protegiendo jóvenes, ofertas al usuario según estrategia (`PickTradeTarget`/`BuildOfferPackage`) y Star FA que prioriza contender sobre rebuild (que tankea y no ficha OVR≥85). Ver `SYSTEMS.md` §S.
- **Analytics avanzados**: PER/WS/eFG/TS%/espaciado por encima del box score actual.
- **Fog-of-war en valoraciones** (el ojeador da rangos, no OVR exacto) — casa con
  la pantalla de Ojeadores.
- **Desarrollo/regresión más realista**: declive atlético por posición y mentoring
  de veteranos.

## Frente 4 — Modos y contenido

- **ProManager real** (`TODO_TECHNICAL_DEBT.md` B20): dificultad aumentada. El modal de
  restricciones del menú principal anuncia las reglas (`MainMenuController.OpenProModal`).
  Hecho: **cese por objetivo no cumplido** al fin de temporada (solo ProManager,
  `ShowObjectiveFiredModal`), **cese por presupuesto más fácil** (umbral 2 vs 3 en
  `CheckBudgetWarning`), **sin NT-MLE** (FA sobre el cap limitado a Taxpayer MLE vía
  `GetMaxOfferBreakdown(proManagerOnly:true)` y sin activación del hard cap por NT-MLE);
  lógica centralizada en `ObjectiveHelper`. **B20 cerrado.**
- **Expansión / liga personalizada** vía el editor de `template.db`.
- **Historial rico**: anillos por jugador, retiro de dorsales, hall of fame.

## Frente 5 — Técnico / rendimiento (potencia bajo el capó)

- **`SQLiteAsync` + trabajo en hilo de fondo** (`TODO_TECHNICAL_DEBT.md` B8) para
  temporadas largas sin congelar el hilo principal.
- **Caché de logos/estática** (`TODO_TECHNICAL_DEBT.md` B13) y `ListView` en tablas.
- Cerrar TODO pendiente: `Settings` muerto (`B7`), `CursorManager` duplicado (`B1`),
  tests para `GameSimulator`/migraciones (ya hay 17 para `TradeHelper`).

---

## Prioridad sugerida (impacto vs. esfuerzo)

| Prioridad | Iniciativa | Frente |
|---|---|---|
| **Hecho** | Play-by-play en vivo del partido (commit `9c69e09`) | 1 |
| **Media-Alta** | Cap sheet (info, hecho) + opciones de contrato + sim "¿y si firmo?" | 2 |
| **Media** | ProManager diferenciado (B20) | 4 |
| **Media** | AI de GM más lista + analytics | 3 |
| **Media** | Async DB (B8) | 5 |
| **Baja-Media** | Logros, G-League, picks protegidos | 2/4 |

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
