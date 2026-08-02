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

El gap más evidente: **el partido es invisible**. Se simula y solo se ve el resultado.

- **Play-by-play en vivo / crónica en texto**: vista de partido con posesiones
  comentadas en texto durante la simulación (`GameSimulator` ya produce `GameResult`
  posesión a posesión; falta un consumidor en `MatchDay`/`GameResults`).
- **Mejores vitrinas**: box score detallado por cuarto, "jugador del partido",
  gráfica de rachas de la clasificación.
- **Logros/trofeos del GM** + palmarés visual enriquecido.
- **Más variedad de audio** (solo 7 WAVs, `SYSTEMS.md §S15`).

## Frente 2 — Profundidad de gestión NBA (lo que los sims "potentes" tienen y aquí falta)

Marcado como pendiente/hueco por los docs (`GAMEPLAY.md` Open questions,
`MEMORY.md §3`):

- **Sign-and-trade real**: `trade_type="sign_and_trade"` existe pero sin flujo UI
  (`SYSTEMS.md §S7`).
- **Cap sheet / planificador de masa salarial a futuro** (años venideros, cap
  proyectado +5%/año).
- **Opciones de contrato** (team/player option) y años garantizados/no garantizados.
- **Picks protegidos** y más flexibilidad de transferencia de picks.
- **Trade deadline con evento real** (el recordatorio existe; falta el cierre con efectos).
- **Objetivos de temporada del propietario** con recompensas/cese (ya hay factor
  "objetivo" en asistencia y despido por presupuesto → expandir).
- **Rest / load management** (los back-to-backs existen, pero no la decisión de descanso).
- **IR (injury reserve) / G-League / two-way contracts**.

## Frente 3 — Simulación & AI (realismo)

- **Determinismo** (`TODO_TECHNICAL_DEBT.md` B10): seed por partido/temporada →
  resultados reproducibles; ya hay `StableHash` FNV-1a, falta inyectarlo en `GameSimulator`.
- **AI de GMs más inteligente**: reconstrucción por edades, priorizar assets, no
  firmar solo lo que falta al roster.
- **Analytics avanzados**: PER/WS/eFG/TS%/espaciado por encima del box score actual.
- **Fog-of-war en valoraciones** (el ojeador da rangos, no OVR exacto) — casa con
  la pantalla de Ojeadores.
- **Desarrollo/regresión más realista**: declive atlético por posición y mentoring
  de veteranos.

## Frente 4 — Modos y contenido

- **ProManager real** (`TODO_TECHNICAL_DEBT.md` B20): dificultad aumentada (sin
  NT-MLE, hard cap más estricto, cese más fácil).
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
| **Alta** | Play-by-play en vivo del partido | 1 |
| **Alta** | Determinismo de simulación (B10) | 3 |
| **Media-Alta** | Cap sheet + opciones de contrato | 2 |
| **Media** | ProManager diferenciado (B20) | 4 |
| **Media** | AI de GM más lista + analytics | 3 |
| **Media** | Async DB (B8) | 5 |
| **Baja-Media** | Logros, G-League, picks protegidos, trade deadline | 2/4 |

---

## Estado / seguimiento

Añadir aquí el estado de cada propuesta cuando se decida abordarla
(`pendiente` / `en curso` / `hecho` con referencia de commit o archivo).
