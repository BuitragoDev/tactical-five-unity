# MEJORA DE LA SIMULACIÓN VISUAL 2D — Plan y seguimiento

> **Estado:** implementado (F0–F4; F5 parcial) · rama `mejora-simulador` · fecha 2026-11-02.
> Este documento es la checklist viva de la feature. Si la implementación se corta,
> aquí queda qué está hecho, qué falta y cómo continuar.

## 1. Objetivo

Que la vista **Visual 2D** del partido (`TF_SimMode = 2`) parezca un partido de
baloncesto real:

- Comentario **antes** de la acción (saque, subida, pases, cortes, bloqueos) y
  desenlace en el momento en que ocurre.
- Marcador y estadísticas que cambian **cuando** entra el balón, no al empezar.
- **1, 2 o 3 tiros libres** visibles, con acierto/fallo por intento.
- **Fuera de banda** real (balón que sale, saque desde el punto).
- **Espaciado real**: esquinas, alas, *slot*, codo, poste, *dunker*; nadie
  amontonado en el centro.
- **Defensa individual por posición**: cada defensor entre su par y el aro,
  ayuda y protección del aro.
- **Cancha completa**: saque de fondo, subida, *outlet*, contragolpe.
- **24 segundos con fases**: saque → subida → set → acción → tiro → rebote.

## 2. Arquitectura (Opción B: coreógrafo de presentación)

El motor (`GameSimulator`) sigue emitiendo **un evento por ataque**. Una capa
nueva, pura y determinista, convierte ese evento en un **guion de posesión**
(beats con posiciones, balón, ticker, marcador y reloj). La vista solo reproduce.

```
GameSimulator (resultado, sin cambios de azar)
        │  PlayByPlayEvent + metadatos (F0)
        ▼
Sim2DChoreographer (puro) ── Sim2DCourt (geometría) ── Sim2DCommentary (textos)
        │  Sim2DPossessionScript
        ▼
MatchDayController.Sim2D.* (renderer: keyframes, ticker, marcador, TL, OOB)
```

**Invariantes:**

1. El resultado del partido **no cambia**: la coreografía no toca `RunPossession`
   ni sus probabilidades. F0 solo añade metadatos.
2. **RNG separado**: hash determinista (`gameId`, `eventIndex`, `playerId`),
   nunca `UnityEngine.Random` ni el `IRandomSource` del partido.
3. Un evento = un ataque (no se parte el log con deltas).
4. `JumpToSimVisualEnd` debe seguir aplicando todo el estado final.
5. Textos nuevos en `Sim2DCommentary` (narración, como `NewsTexts`), fuera de
   `Scripts/UI`, para no romper `LocalizationTests`.

## 3. Fases y checklist

### F0 — Metadatos del motor
- [x] `PlayByPlayEvent.freeThrowResults` (`List<bool>`) y `freeThrowShooterId`.
- [x] `PossessionOutcome.freeThrowResults` / `freeThrowShooterId`.
- [x] `ShootFreeThrows(..., List<bool> results = null)` guarda cada intento.
- [x] `RunPossession`: bonus, and-one y falta de tiro rellenan los resultados.
- [x] `AddLog` copia los metadatos al evento.
- [x] `DoTO` distingue `out_of_bounds` y `sideline` reutilizando la misma tirada
      (`IsOutOfBoundsTurnoverRoll` 0,50-0,60; `IsSidelineTurnoverRoll` 0,60-0,70).
- [x] El resultado y el número de eventos no cambian: F0 solo añade metadatos y
      reutiliza la tirada existente. La reproducibilidad sigue cubierta por
      `GameSimulatorTests` y la suite completa.

### F1 — Ticker, marcador y tiros libres
- [x] `Sim2DCommentary` (plantillas en español).
- [x] `Sim2DChoreographer` + DTOs (`Script`, `Path`, `Cue`, `ScoreStep`).
- [x] `Sim2DCourt` (geometría y zonas).
- [x] Ticker por beats: el texto aparece cuando ocurre, no antes.
- [x] Marcador y tabla (PTS/REB/AST) en el beat de la canasta.
- [x] TL 1/2/3 por intento, con acierto/fallo y rebote del último fallo.
- [x] Fuera de banda: balón que sale, saque desde el punto.
- [x] Prioridad de líneas por velocidad (x1/x5/x10/x20).
- [x] `JumpToSimVisualEnd` sigue aplicando todo.

### F2 — Cancha completa y transición
- [x] Cada equipo ataca su aro todo el partido.
- [x] Saque de fondo tras canasta/TL, subida desde el campo propio.
- [x] `outlet` tras rebote defensivo.
- [x] Contragolpe tras robo.
- [x] Saque desde el punto tras fuera de banda.
- [x] Segunda oportunidad tras rebote ofensivo (14 s).

### F3 — Espaciado, pases y cortes
- [x] Zonas: esquinas, alas, slot, top, codo, poste, dunker.
- [x] Asignación de 5 zonas distintas (sin amontonar); si el tirador ocupa la
      zona de un compañero, este se reubica en la libre más cercana.
- [x] Tiros desde el spot correcto (triple fuera del arco).
- [x] 1-3 pases en la construcción (asistente real cuando existe).
- [ ] Bloqueo y continuación / corte al aro / poste: los textos `Screen`/`Cut`/
      `PostUp` existen, pero la coreografía actual no genera bloqueos ni cortes
      como acciones separadas; el movimiento se limita a la construcción y el
      tiro. Queda como mejora de una segunda pasada.
- [x] Balón pegado al manejador y pases a receptores reales.

### F4 — Defensa por posición
- [x] Asignación fija por posición (PG↔PG, …).
- [x] On-ball entre su par y el aro.
- [x] Deny a un pase, help a dos pases.
- [x] Pívot protege el aro si su par sale.
- [x] Closeout al tirador y caja en el rebote.
- [x] Sin colapsos al balón (`FindNearest` solo para closeout).

### F5 — Pulido (parcial)
- [x] HUD: reloj de 24/14 derivado del reloj de partido y de la posesión.
- [ ] Variedad de comentarios (evitar repetición): hay varias plantillas, pero
      la selección es determinista y todavía puede repetirse en jugadas seguidas.
- [ ] Icono del ticker por tipo de jugada.
- [ ] Cambios visibles en dead balls: el quinteto se actualiza en el siguiente
      evento, sin animación de entrada/salida desde la banda.
- [ ] Faltas y silbato: F0 no añadió metadato de falta; el play-by-play no las
      representa como acción propia.
- [ ] Rendimiento: el guion se construye una vez por evento (no por frame), pero
      no se ha medido el coste de allocations en un partido completo.

### Verificación
- [x] `Sim2DChoreographerTests` (spacing, matchups, determinismo, TL, OOB, reloj).
- [x] Suite EditMode completa verde: **621/621** (608 base + 13 nuevos).
- [ ] Partido de prueba visual (x1 y x10) sin desincronización: pendiente de una pasada manual en el editor; en batchmode no se puede validar la sincronización visual.
- [x] Docs actualizadas: `Docs/GAMEPLAY.md`, `.agent/MEMORY.md`, este doc.

## 4. Archivos

| Archivo | Cambio |
|---|---|
| `Scripts/GameSimulator.cs` | F0: metadatos de TL y tipo de pérdida. |
| `Scripts/Core/Sim2D/Sim2DCourt.cs` | **Nuevo**: geometría, zonas, matchups, TL. |
| `Scripts/Core/Sim2D/Sim2DChoreographer.cs` | **Nuevo**: guion de posesión. |
| `Scripts/Core/Sim2D/Sim2DCommentary.cs` | **Nuevo**: textos del ticker. |
| `Scripts/UI/MatchDayController.Visual.cs` | Renderer: aplica guion, cues, marcador, TL/OOB. |
| `Tests/Editor/Sim2DChoreographerTests.cs` | **Nuevo**: tests puros. |
| `Docs/GAMEPLAY.md` / `.agent/MEMORY.md` | Documentación de la feature. |

## 5. Progreso

| Fecha | Fase | Estado | Notas |
|---|---|---|---|
| 2026-11-01 | Doc | Hecho | Plan y checklist creados. |
| 2026-11-01 | F0 | Hecho | Metadatos de TL por intento y `out_of_bounds` sin cambiar la tirada. |
| 2026-11-01 | F1 | Hecho | Coreógrafo, ticker por beats, marcador por pasos, TL 1/2/3 y OOB. |
| 2026-11-01 | F2 | Hecho | Saque de fondo/lateral, subida, outlet, contragolpe y segunda oportunidad. |
| 2026-11-01 | F3 | Hecho (parcial) | Zonas, spacing, tiros por spot y pases; bloqueos/cortes quedan para otra pasada. |
| 2026-11-01 | F4 | Hecho | Emparejamiento por posición, on-ball/deny/help, pívot y closeout. |
| 2026-11-02 | F5 | Parcial | HUD 24/14; faltan icono de ticker, faltas, animación de cambios y medición de allocations. |
| 2026-11-02 | Tests | Hecho | `Sim2DChoreographerTests`: 13/13 en verde. Suite EditMode completa: 621/621. |
