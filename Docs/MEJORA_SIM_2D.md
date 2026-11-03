# MEJORA DE LA SIMULACIÓN VISUAL 2D — Plan y seguimiento

> **Estado:** implementado (F0–F5) · rama `mejora-simulador` · fecha 2026-11-03.
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
- [x] Acción sin balón por posesión: **bloqueo y continuación**, **corte al
      aro** o **posteo**, elegida de forma determinista (hash FNV estable sobre
      `eventIndex`/`gameId`; nunca toca `IRandomSource`).
      - Bloqueo: el interior se cruza en la línea del defensor del balón y
        continúa al aro (`Bloqueo de X.` / `X continúa al aro.`).
      - Corte: el alero corta al aro y, si no recibe, abre a la esquina débil
        (`X corta al aro.`).
      - Posteo: el interior se sitúa en el poste del lado débil
        (`X juega en el poste.`).
      - El defensor de cada acción acompaña el movimiento (persigue el corte,
        defiende el poste, ayuda en el bloqueo).
      - Solo en media cancha con tiro: se omite en pérdidas, putbacks y
        contragolpes, y nunca coincide con tirador, pasador, receptor
        probable, sacador ni reboteador ofensivo.
- [x] Balón pegado al manejador y pases a receptores reales.

### F4 — Defensa por posición
- [x] Asignación fija por posición (PG↔PG, …).
- [x] On-ball entre su par y el aro.
- [x] Deny a un pase, help a dos pases.
- [x] Pívot protege el aro si su par sale.
- [x] Closeout al tirador y caja en el rebote.
- [x] Sin colapsos al balón (`FindNearest` solo para closeout).

### F5 — Pulido (completo)
- [x] HUD: reloj de 24/14 derivado del reloj de partido y de la posesión.
- [x] Variedad de comentarios: plantillas con variantes deterministas elegidas con
      `StableHash(eventIndex, gameId, salt)` (nunca el RNG del partido). Cubre
      subida, organización, tiro, canasta, fallo, rebote, tapón, pérdida y falta;
      los métodos aceptan `int variant = 0` para no romper llamadas antiguas.
- [x] Icono del ticker por tipo de jugada (`Script.IconName`), reutilizando los
      PNG existentes de `Resources/Icons`: `silbato` (falta), `robo`, `perdidas`,
      `tapones`, `tiro` (tiro/tiro libre), `basket_icon` (canasta), `rebote` y
      `accion` (periodo/sin clasificar). El renderer cachea sprite y textura.
- [x] Cambios visibles en dead balls: el que sale sigue en pista, camina al
      banquillo y se oculta a mitad de jugada (`HideAt = 0,14`); el que entra
      arranca desde el banquillo y completa su recorrido normal. El ticker avisa
      («Entra X por Y»). Se detecta comparando `homeOnCourt`/`awayOnCourt` del
      evento con el anterior, sin tocar el motor.
- [x] Faltas y silbato: `PlayByPlayEvent`/`PossessionOutcome` llevan
      `foulType` (`reach_in`, `shooting`, `and_one`, `ejection`), `foulerId`,
      `foulerName` y `foulBonus`. El coreógrafo añade un cue de falta (el 2+1 se
      canta después de la canasta) y el renderer reproduce el silbato.
- [x] SFX de partido (clips **provisionales**, ver `THIRD_PARTY_ASSETS.md`):
      `silbato` en faltas, `bocina` en inicio/fin de periodo y `canasta` en
      canasta o tiro libre anotado. `AudioManager.PlaySFX(nombre, volumen)`
      respeta el volumen de SFX y cachea los clips.
- [x] Rendimiento: el guion se construye una vez por evento (no por frame). Coste
      medido: **0,021 ms** por guion (200 iteraciones tras calentar) y el guion no
      crece entre llamadas (`Build_RepeatedCalls_*`).

### Verificación
- [x] `Sim2DChoreographerTests` (spacing, matchups, determinismo, TL, OOB, acciones sin balón, reloj, F5): **30/30** (última ejecución filtrada antes del arreglo del saque).
- [ ] `Sim2DChoreographerTests` tras el arreglo del saque de fondo (31 casos): pendiente de ejecutar; el editor tiene el proyecto abierto y bloquea `-batchmode`.
- [ ] Suite EditMode completa: pendiente de ejecutar por el mismo motivo (el último total conocido era 625/625 en F3).
- [ ] Partido de prueba visual (x1 y x10) sin desincronización: pendiente de una pasada manual en el editor; en batchmode no se puede validar la sincronización visual.
- [x] Docs actualizadas: `Docs/GAMEPLAY.md`, `.agent/MEMORY.md`, este doc.

### Correcciones posteriores
- **Saque de fondo tras canasta (2026-11-03):** el saque usaba la línea de fondo del aro **que se ataca**; debe salir de la línea **propia** (la del aro donde se anotó). `Sim2DCourt.OwnBaselineInbound(attackRight)` centraliza el punto (`BaselineX(!attackRight)`) y el coreógrafo lo usa para `InboundKind.Baseline` (canasta, tiro libre y fuera de banda en la línea de fondo). Regresión cubierta por `Build_MadeBasket_InboundsFromOwnBaseline`.

## 4. Archivos

| Archivo | Cambio |
|---|---|
| `Scripts/GameSimulator.cs` | F0: metadatos de TL y tipo de pérdida. F5: metadatos de falta (`foulType`, `foulerId`, `foulerName`, `foulBonus`). |
| `Scripts/Core/Sim2D/Sim2DCourt.cs` | **Nuevo**: geometría, zonas, matchups, TL. |
| `Scripts/Core/Sim2D/Sim2DChoreographer.cs` | **Nuevo**: guion de posesión. F5: `IconName`, cue de falta, variantes de comentario. |
| `Scripts/Core/Sim2D/Sim2DCommentary.cs` | **Nuevo**: textos del ticker. F5: variantes deterministas, falta, tapón, pérdida y cambio. |
| `Scripts/Core/AudioManager.cs` | F5: `PlaySFX(nombre, volumen)` para los SFX de partido. |
| `Scripts/UI/MatchDayController.Visual.cs` | Renderer: aplica guion, cues, marcador, TL/OOB. F5: icono por jugada, SFX y animación de cambios. |
| `Art/Resources/Audios/{silbato,bocina,canasta}.wav` | **Nuevo (provisional)**: SFX de partido. |
| `Art/Resources/Icons/silbato.png` | **Nuevo**: icono de falta en el ticker. |
| `Tests/Editor/Sim2DChoreographerTests.cs` | **Nuevo**: tests puros (F0–F5). |
| `Docs/GAMEPLAY.md` / `.agent/MEMORY.md` | Documentación de la feature. |

## 5. Progreso

| Fecha | Fase | Estado | Notas |
|---|---|---|---|
| 2026-11-01 | Doc | Hecho | Plan y checklist creados. |
| 2026-11-01 | F0 | Hecho | Metadatos de TL por intento y `out_of_bounds` sin cambiar la tirada. |
| 2026-11-01 | F1 | Hecho | Coreógrafo, ticker por beats, marcador por pasos, TL 1/2/3 y OOB. |
| 2026-11-01 | F2 | Hecho | Saque de fondo/lateral, subida, outlet, contragolpe y segunda oportunidad. |
| 2026-11-01 | F3 | Hecho | Zonas, spacing, tiros por spot, pases y acciones sin balón (bloqueo y continuación, corte, posteo). |
| 2026-11-01 | F4 | Hecho | Emparejamiento por posición, on-ball/deny/help, pívot y closeout. |
| 2026-11-02 | F5 | Parcial | HUD 24/14; faltan icono de ticker, faltas, animación de cambios y medición de allocations. |
| 2026-11-02 | Tests | Hecho | `Sim2DChoreographerTests`: 13/13 en verde. Suite EditMode completa: 621/621. |
| 2026-11-02 | F3 (2.ª pasada) | Hecho | Bloqueo y continuación, corte al aro y posteo con rutas, ticker y reacción defensiva; `Sim2DChoreographerTests`: 17/17 y suite completa 625/625. |
| 2026-11-03 | F5 | Hecho | Icono del ticker por jugada, faltas con silbato, animación de cambios, variantes de comentario y coste medido (0,021 ms/jugada). `Sim2DChoreographerTests`: 30/30. |
| 2026-11-03 | Fix | Hecho | El saque tras canasta sale de la línea de fondo propia (`OwnBaselineInbound`), no de la del aro atacado. Test de regresión añadido; pendiente ejecutar la suite con el editor cerrado. |
