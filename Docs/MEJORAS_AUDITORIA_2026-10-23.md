# MEJORAS — Plan de acción derivado de la auditoría 2026-10-23

> **Documento de trabajo vivo.** Cada mejora es una casilla marcable. Se marca `[x]` al terminar y se anota fecha + commit en la **§12 Bitácora**.
> **Origen:** `Docs/AUDITORIA_2026-10-23.md` (commit auditado `e76bf723`).
> **Fecha de creación:** 23 de octubre de 2026.
> **Criterio de orden:** primero lo que hace *verdadero* el juego (datos e invariantes), después lo que lo hace *medible* (calibración), después lo que lo hace *más profundo* (reglas, competiciones, features) y por último lo que lo hace *mantenible* (arquitectura, UI, proceso).

---

## 0. Cómo usar este documento

1. **No se salta el orden de fases salvo lo indicado como paralelizable.** La Fase 3 (calibración) sobre un motor con las posesiones rotas (Fase 2) produce números que parecen buenos y no lo son.
2. Cada ítem tiene: **ref. auditoría**, **esfuerzo** (S < 1 día · M 1–5 días · L 1–3 semanas · XL > 3 semanas), **criterio de hecho** verificable y **punto de partida** en el código.
3. Un ítem solo se marca `[x]` si su criterio se cumple **y** está cubierto por un test o por una comprobación reproducible. "Funciona en mi máquina" no es un criterio.
4. Las fases tienen **puerta de salida**: si no se cumple, no se pasa a la siguiente.
5. Los ítems marcados `[!]` son **decisiones de diseño pendientes** (§11): no son código, son una respuesta que hay que escribir antes de tocar nada.

### Tablero de progreso

| Fase | Contenido | Ítems | Hechos |
|---|---|---:|---:|
| **0** | Higiene y quick wins | 6 | 3 |
| **1** | Datos y balance del seed | 7 | 0 |
| **2** | Motor de simulación | 12 | 0 |
| **3** | Calibración y reproducibilidad | 7 | 0 |
| **4** | Mercado, CBA y economía | 8 | 0 |
| **5** | Calendario y competiciones | 5 | 0 |
| **6** | Histórico y estadísticas | 4 | 0 |
| **7** | UX/UI, resoluciones e idioma | 6 | 0 |
| **8** | Arquitectura y rendimiento | 8 | 0 |
| **9** | Proceso y documentación | 2 | 0 |
| **NF** | Apuestas de producto (sin fecha) | 4 | 0 |
| | **TOTAL** | **69** | **3** |

### Grafo de dependencias

```mermaid
flowchart LR
    F0["F0 · Higiene<br/>6 ítems"]
    F1["F1 · Datos y balance<br/>7 ítems"]
    F2["F2 · Motor<br/>12 ítems"]
    F3["F3 · Calibración<br/>7 ítems"]
    F4["F4 · CBA y mercado<br/>8 ítems"]
    F5["F5 · Calendario y copas<br/>5 ítems"]
    F6["F6 · Histórico<br/>4 ítems"]
    F7["F7 · UX/UI<br/>6 ítems"]
    F8["F8 · Arquitectura<br/>8 ítems"]
    F9["F9 · Proceso<br/>2 ítems"]
    NF["NF · Producto<br/>4 ítems"]

    F0 --> F1
    F0 --> F2
    F2 --> F3
    F1 --> F3
    F3 --> F5
    F3 --> F4
    F0 --> F6
    F0 --> F7
    F3 --> F8
    F1 --> F9
    F5 --> NF
    F3 --> NF
```

---

## 1. Fase 0 — Higiene y quick wins

**Objetivo:** dejar de generar datos incorrectos y poder medir el estado real sin depender de abrir el juego.
**Por qué primero:** los seis ítems son baratos y desbloquean la verificación de todo lo demás. Sin el auditor de seed (F0-01) la Fase 1 se hace a ciegas; sin la fórmula única (F0-04) cada nueva operación puede volver a introducir la discrepancia.

- [x] **F0-01 · Auditor de seed de solo lectura (test EditMode)**
  - *Ref:* `DB-04` · *Esfuerzo:* S · *Depende:* —
  - *Criterio:* un test recorre todo el seed y falla con informe si hay IDs duplicados, atributos fuera de `[1,99]`, OV distinto del recalculado, `potential < overall`, plantillas fuera de rango, salarios por debajo del mínimo o abreviaturas de equipo desconocidas.
  - *Punto de partida:* `DatabaseManager.Seeding.cs`; patrón de test en `Tests/Editor/SaveSlotInitTests.cs`.
  - *Hecho (2026-10-23):* `Tests/Editor/SeedAuditorTests.cs` — siembra una partida temporal y audita 30 equipos, 606 jugadores y 360 prospectos con 24 reglas de error + 5 de aviso. Reproduce los hallazgos manuales del informe (235 OV, 86 atributos bajos, 3 plantillas fuera de rango) y **destapó un bug nuevo en G-League** (ver F0-04).

- [ ] **F0-02 · Test diagnóstico de invariantes de calendario (solo reporta)**
  - *Ref:* `CAL-02` · *Esfuerzo:* S · *Depende:* —
  - *Criterio:* un test genera calendario para una temporada sintética y publica en el fallo: partidos totales, partidos por equipo, localías por equipo y parejas faltantes. En esta fase **no** se arregla el generador (eso es F5-01/F5-02); sirve de línea base reproducible.
  - *Punto de partida:* `ScheduleGenerator.cs:10`; `Tests/Editor/CalendarHelperTests.cs`.

- [x] **F0-03 · Corregir el margen salarial duplicado de `TradeHelper`**
  - *Ref:* `ECO-02` · *Esfuerzo:* S · *Depende:* —
  - *Criterio:* el límite del 110% se define una sola vez; existen tests justo por debajo, igual y por encima del umbral en las tres ramas (por encima del 2º apron, entre 1º y 2º, por debajo del 1º) y el texto de error coincide con el número aplicado.
  - *Punto de partida:* `TradeHelper.cs:59–76`; `Tests/Editor/TradeHelperTests.cs`.
  - *Hecho (2026-10-23):* `TradeHelper.GetMaxIncomingSalary` es la fuente única para el límite y su mensaje. Se eliminó el +$250K extra por debajo del primer apron y el buffer del tramo 110% (los docs CBA describen 110% exacto); el mensaje ahora indica el máximo aplicado. Tests bajo/igual/sobre los tres niveles y tests de los dos cortes de salario. Suite EditMode: **372/372**.

- [x] **F0-04 · Una sola fórmula de `overall`**
  - *Ref:* `ROST-01` · *Esfuerzo:* S · *Depende:* —
  - *Criterio:* existe una única función (p. ej. `PlayerData.CalculateOverall(attrs, potential)`) usada por seeder, draft, editor, migraciones y tests; desaparece la división entera manual; un test verifica que los 606 registros del seed dan el mismo OV antes y después.
  - *Punto de partida:* `DatabaseManager.Seeding.cs:26–27` vs `PlayerData.cs:71–85`.
  - [!] **Hallazgo nuevo del auditor (2026-10-23):** `GLeagueSeeder.cs:94–96` calcula `Mathf.Min(potential, sumaAtributos) / 11`, es decir **`potential / 11`** (la suma nunca es menor que el potencial). Los **360** prospectos de la G-League tienen `overall` 5–7 en vez de ~48–59 hasta que un `RecalculateOverall` los arregla en el primer día de desarrollo. Entra en el alcance de F0-04, y al cerrarlo hay que subir `PLAYER-OVR` y `GLEAGUE-OVR` de aviso a **error** en `SeedAuditorTests.KnownWarnings`.
  - *Hecho (2026-10-23):* `Scripts/Data/PlayerRating.cs` (`FromSum`/`FromAttributes`) es la única implementación; `PlayerData` y `GLeaguePlayerData` delegan en ella, igual que el seed de jugadores y agentes libres, el seed de G-League y las migraciones `overall_recalc`/`gleague_overall_recalc` (esta última nueva, repara las partidas viejas). 235 OV del seed y los 360 de G-League corregidos; `Tests/Editor/PlayerRatingTests.cs` (6 casos) fija la fórmula y que ambos modelos coincidan. `PLAYER-OVR` y `GLEAGUE-OVR` pasan de aviso a **error** en el auditor: el informe queda en **0 errores · 103 avisos**. Suite EditMode: **368/368**.

- [ ] **F0-05 · Fuente canónica de versión, rama y conteos**
  - *Ref:* `ARC-04` · *Esfuerzo:* S · *Depende:* —
  - *Criterio:* un único sitio declara HEAD, rama, número de tests y número de pantallas; `AGENTS.md`, `PROJECT_OVERVIEW.md`, `AUDITORIA_FINAL.md` y `MEMORY.md` dejan de contradecirse o remiten a esa fuente.
  - *Punto de partida:* `AGENTS.md:6`, `Docs/PROJECT_OVERVIEW.md:5`, `Docs/AUDITORIA_FINAL.md:7–10, 44–53`.

- [ ] **F0-06 · Inventario de licencias y atribuciones**
  - *Ref:* `PERF-04` · *Esfuerzo:* S · *Depende:* —
  - *Criterio:* existe un manifiesto con, por cada asset de terceros (fuentes, fotos, escudos, pabellones, audio, librerías: `sqlite-net`, `StandaloneFileBrowser`, TMP), origen, licencia y atribución. No es asesoramiento legal: es trazabilidad.
  - *Punto de partida:* `Assets/StandaloneFileBrowser/LICENSE.txt` es el único manifiesto actual; el resto no está documentado.

**Puerta de salida F0:** los 6 ítems cerrados y una ejecución de EditMode en verde con el auditor de seed incluido.

---

## 2. Fase 1 — Datos y balance del seed

**Objetivo:** que el estado inicial de una partida cumpla las reglas que el propio juego declara.
**Por qué ahora:** cualquier calibración de la Fase 3 se hace sobre estas plantillas. Normalizar después invalida todas las medidas tomadas antes.

- [ ] **F1-01 · Decidir y documentar qué significa "media de equipo 85"** `[!]`
  - *Ref:* `ROST-02` · *Esfuerzo:* S · *Depende:* —
  - *Criterio:* hay una definición escrita (media del roster activo / de los 15 estándar / rating del quinteto) y una nota en `.agent/MEMORY.md`. Todos los ítems siguientes usan esa definición.
  - *Punto de partida:* hoy ningún equipo llega a 85 con media simple (rango 72,00–82,86).

- [ ] **F1-02 · Normalizar composición de plantillas**
  - *Ref:* `ROST-03` · *Esfuerzo:* M · *Depende:* F1-01, F0-04
  - *Criterio:* los 30 equipos quedan en el rango configurado (14–17), sin equipos de 18 (DEN, NYK) ni de 13 (PRT); la composición distingue plazas estándar y two-way; el total de jugadores se documenta.
  - *Punto de partida:* 478 asignados + 128 FA; DEN y NYK con 18, PRT con 13.

- [ ] **F1-03 · Rebalancear OV de los 30 equipos contra la definición de F1-01**
  - *Ref:* `ROST-02` · *Esfuerzo:* L · *Depende:* F1-01, F1-02, F0-04
  - *Criterio:* los 30 equipos cumplen la media definida con tolerancia declarada; se respeta la regla de estrellas (98/90 sin acumular un tercer jugador en ese tramo) y NYK/OKC/PHI dejan de tener dos ≥90. El auditor de F0-01 valida el resultado.
  - *Nota:* no se reporta el convenio en sí como defecto; lo que se corrige es el **incumplimiento** detectado.

- [ ] **F1-04 · Revisar contratos fuera de rango**
  - *Ref:* `ECO-07` · *Esfuerzo:* M · *Depende:* F1-02
  - *Criterio:* los 9 salarios por debajo del mínimo interno quedan justificados por escala (rookie, two-way, mínimo por experiencia) o corregidos; el caso Embiid ($65M) queda documentado con año de firma y base legal del máximo; Houston deja de estar por encima del 2º apron sin expediente que lo explique.
  - *Punto de partida:* nómina sembrada; HOU $274,4M frente a $241,686M.

- [ ] **F1-05 · Definir escala de atributos y validarla**
  - *Ref:* `ROST-04` · *Esfuerzo:* M · *Depende:* F0-01
  - *Criterio:* se decide si los 86 valores < 25 son intencionales; si no, se corrigen; seeder y generador de draft comparten la misma escala mínima por atributo y posición, y el auditor lo comprueba.
  - *Punto de partida:* mínimo 6 en el seed frente a suelo 30/25 en `DraftGenerator.cs:335–356`.

- [ ] **F1-06 · Validador de plantilla antes de clonar el template**
  - *Ref:* `ROST-03`, `ECO-05` · *Esfuerzo:* M · *Depende:* F1-02
  - *Criterio:* `InitSaveSlot`/`EnsureTemplateDb` fallan con mensaje claro si el seed no cumple composición, cupos o atributos; el juego nunca arranca una partida con datos inválidos.
  - *Punto de partida:* `DatabaseManager.cs:411–440` (creación de template), `EnsureTemplateDb`.

- [ ] **F1-07 · Coherencia entre seed y documentación de datos**
  - *Ref:* `DB-04`, `ARC-04` · *Esfuerzo:* S · *Depende:* F1-02
  - *Criterio:* `DATA_MODEL.md` y los comentarios del seeder coinciden con la realidad (el comentario "140 jugadores" frente a 128 `AddFA` es el caso visible); no quedan cifras de plantilla contradictorias en docs ni en código.
  - *Punto de partida:* `DatabaseManager.Seeding.cs:862` y siguientes.

**Puerta de salida F1:** auditor de seed en verde sobre los datos nuevos, y una partida nueva creada desde cero sin avisos del validador.

---

## 3. Fase 2 — Motor de simulación

**Objetivo:** que el marcador sea la consecuencia de la secuencia de posesiones y de los atributos, no de estadísticas añadidas después.
**Por qué ahora:** es la deuda funcional más visible. Añadir contenido (Fase 5) o calibrar (Fase 3) antes de esto multiplica el trabajo.

- [ ] **F2-01 · Un rebote ofensivo conserva la posesión**
  - *Ref:* `SIM-01` · *Esfuerzo:* L · *Depende:* —
  - *Criterio:* tras un OREB el mismo equipo vuelve a atacar dentro de la misma posesión; tras un rebote defensivo cambia el balón. Test determinista: fallo → OREB → nuevo lanzamiento del mismo equipo. El contador de posesiones distingue posesión y *possession used*.
  - *Punto de partida:* `GameSimulator.cs:847–854` termina la posesión sin cambiar el atacante.

- [ ] **F2-02 · Modelo explícito de alternancia de posesiones**
  - *Ref:* `SIM-02` · *Esfuerzo:* L · *Depende:* F2-01
  - *Criterio:* el atacante se decide por regla (cambio tras tiro anotado, rebote defensivo o pérdida; continúa tras OREB), no por moneda; la diferencia de posesiones entre equipos queda en un margen acotado y testeado; existen tests de distribución de posesiones y puntos por partido.
  - *Punto de partida:* `GameSimulator.cs:713–744` (`Random.value < 0.5f` por evento).

- [ ] **F2-03 · Tiros libres, faltas de equipo y expulsión**
  - *Ref:* `SIM-04` · *Esfuerzo:* L · *Depende:* F2-01
  - *Criterio:* el último FT fallado genera rebote vivo; existe bonus de faltas de equipo; seis faltas expulsan y fuerzan sustitución; FTA/PF por equipo entran en el rango objetivo de la Fase 3.
  - *Punto de partida:* `GameSimulator.cs:797–844`, `DoFoul` `:1035–1042`.

- [ ] **F2-04 · Dar efecto a los cuatro atributos ignorados**
  - *Ref:* `SIM-03` · *Esfuerzo:* L · *Depende:* F2-02
  - *Criterio:* `speed`, `dribbling`, `athleticism` e `iq` se copian al snapshot y tienen un efecto declarado y testeado (transición, pérdidas, finalización/rebote, selección de tiro). Tests de sensibilidad: subir 10 puntos en un atributo mueve la métrica asociada en la dirección esperada.
  - *Punto de partida:* `GameSimulator.cs:8–26` y `InitPS:345–366` no los incluyen.

- [ ] **F2-05 · Eliminar los pisos artificiales de estadísticas**
  - *Ref:* `SIM-07` · *Esfuerzo:* M · *Depende:* F2-01, F2-02
  - *Criterio:* desaparecen los mínimos forzados de asistencias/rebotes/robos/tapones; el box score de un jugador es la suma de sus acciones. La distribución se valida en F3 sin pisos.
  - *Punto de partida:* `GameSimulator.cs:213–245`.

- [ ] **F2-06 · Resolver la plantilla corta sin marcador sintético**
  - *Ref:* `SIM-05` · *Esfuerzo:* M · *Depende:* —
  - *Criterio:* con menos de cinco sanos no se inventa un 105–125/100–120: se fuerza una alineación válida (con aviso al jugador) o se simula con los disponibles generando box score coherente. Nunca se persiste un partido sin estadísticas.
  - *Punto de partida:* `GameSimulator.cs:90–117`.

- [ ] **F2-07 · Riesgo de lesión proporcional a la exposición**
  - *Ref:* `SIM-06` · *Esfuerzo:* M · *Depende:* F2-05
  - *Criterio:* la probabilidad depende de minutos jugados, intensidad/físico y riesgo de recaída; un jugador con 0 minutos no se lesiona; se documenta si la lesión es postpartido o intra-partido y la UI lo refleja.
  - *Punto de partida:* `GameSimulator.cs:324–327, 1263–1296`.

- [ ] **F2-08 · Captura de play-by-play bajo demanda**
  - *Ref:* `PERF-01` · *Esfuerzo:* M · *Depende:* —
  - *Criterio:* si el modo es directo no se construyen snapshots, deltas ni narración; el flag de captura se pasa desde el llamador. Medición antes/después de un día de 15 partidos documentada.
  - *Punto de partida:* `GameSimulator.cs:137–138, 501–555`; filtrado tardío en `DashboardController.cs:1878–1879`.

- [ ] **F2-09 · Persistencia de fatiga, lesiones y stats en lote**
  - *Ref:* `PERF-02` · *Esfuerzo:* M · *Depende:* F2-08
  - *Criterio:* un partido no hace un `Update` por jugador; los cambios se acumulan y se escriben una vez, dentro de la transacción del día.
  - *Punto de partida:* `GameSimulator.cs:298–320, 1263–1294`.

- [ ] **F2-10 · Ampliar el plan de partido con decisiones medibles**
  - *Ref:* `TAC-01` · *Esfuerzo:* L · *Depende:* F3-01, F3-04
  - *Criterio:* 3–5 decisiones nuevas (p. ej. cobertura del P&R, presión en transición, carga al rebote ofensivo) cada una con efecto declarado y medido; ninguna opción es decorativa.
  - *Punto de partida:* `GamePlan.cs:8–57`; consumo en `GameSimulator.cs:713–887`.

- [ ] **F2-11 · Rotación estratégica para la IA**
  - *Ref:* `TAC-02` · *Esfuerzo:* M · *Depende:* F2-07
  - *Criterio:* la IA reparte minutos por rol y forma física, no solo por OVR, y respeta promesas de minutos con efecto en moral. Se compara con la rotación del usuario en el informe de calibración.
  - *Punto de partida:* `DashboardController.cs:841–886`.

- [ ] **F2-12 · Especialidades de staff con efecto trazable**
  - *Ref:* `TAC-03` · *Esfuerzo:* L · *Depende:* F2-10
  - *Criterio:* cada especialidad (desarrollo, defensa, recuperación, scouting) modifica una métrica concreta y visible; el jugador puede explicar por qué su equipo mejora. Se retira el bonus genérico único.
  - *Punto de partida:* `DashboardController.cs:5300–5313`; `CoachScheme.cs`.

**Puerta de salida F2:** partido reproducible con la misma semilla (cuando F3-01 exista) y balance de acciones coherente: `PTS = 2·FG2M + 3·FG3M + FT`, `REB = OREB + DREB`, `POS` cuadra con tiros, pérdidas y rebotes.

---

## 4. Fase 3 — Calibración y reproducibilidad

**Objetivo:** poder afirmar con datos que el motor produce una NBA creíble, y demostrarlo en cada cambio.
**Por qué ahora:** es la única forma de saber si F2 mejoró algo. Sin esta fase, todo lo anterior es opinión.

- [ ] **F3-01 · Fuente de aleatoriedad inyectable**
  - *Ref:* `ARC-03` · *Esfuerzo:* L · *Depende:* —
  - *Criterio:* el simulador recibe un `IRandomSource` (o equivalente); con la misma semilla y las mismas entradas el resultado es idéntico; el juego real usa la fuente de Unity, los tests una determinista.
  - *Punto de partida:* `GameSimulator.cs:95, 134`, `ScheduleGenerator.cs:34`, `DraftGenerator.cs:441`.

- [ ] **F3-02 · Banco de calibración (Monte Carlo) y laboratorio**
  - *Ref:* `ARC-03` · *Esfuerzo:* L · *Depende:* F3-01
  - *Criterio:* un runner simula N partidos/temporadas sin UI y emite un informe con media, desviación y percentiles de ritmo, puntos, FG%, 3P%, FTA, TO, AST, REB, PF, victorias y días de baja. En Editor, sin escribir en la partida del jugador.
  - *Punto de partida:* no existe; `SimulateGame(..., persistToDb:false)` es el punto de entrada.

- [ ] **F3-03 · Fijar rangos objetivo y umbrales de aceptación**
  - *Ref:* §5 de la auditoría · *Esfuerzo:* S · *Depende:* F3-02
  - *Criterio:* tabla versionada de métrica → rango aceptable → tolerancia, con fuente declarada (temporadas NBA de referencia, no "a ojo"). Un partido/una temporada fuera de rango hace fallar el test de calibración.
  - *Nota:* los rangos de la §5 de la auditoría son aproximados y deben sustituirse por una fuente citada.

- [ ] **F3-04 · Calibrar el juego ofensivo**
  - *Ref:* `SIM-02`, `SIM-04`, §5 · *Esfuerzo:* L · *Depende:* F3-02, F3-03, F2-01..F2-05
  - *Criterio:* puntos, FG%, 3P%, FTA, TO y AST por equipo dentro de rango en P50 y P5–P95; ningún equipo de la liga simulada queda fuera de forma sistemática por diseño del motor.

- [ ] **F3-05 · Calibrar resultados: victorias, paridad y prórrogas**
  - *Ref:* §5 · *Esfuerzo:* M · *Depende:* F3-04
  - *Criterio:* tras 100 temporadas completas, la distribución de victorias y la correlación OV↔victorias son creíbles; la tasa de prórrogas y el reparto de la liga (récords extremos) están dentro de rango.

- [ ] **F3-06 · Calibrar lesiones y disponibilidad**
  - *Ref:* `SIM-06` · *Esfuerzo:* M · *Depende:* F2-07, F3-04
  - *Criterio:* partidos perdidos por lesión por equipo y temporada dentro de rango; la duración media por gravedad es coherente con `InjuryCatalog`; ningún equipo queda sin poder alinear cinco.

- [ ] **F3-07 · CI: compilación + EditMode + auditorías en cada cambio**
  - *Ref:* `PERF-03` · *Esfuerzo:* M · *Depende:* F0-01, F3-02
  - *Criterio:* un runner aislado ejecuta build, 334+ tests, auditor de seed y test de invariantes de calendario; un fallo bloquea la integración. Sin `.asmdef`, documentar la limitación o crear la separación de assemblies.

**Puerta de salida F3:** informe de calibración adjunto con las métricas de la tabla objetivo, y CI en verde ejecutando el banco de calibración reducido.

---

## 5. Fase 4 — Mercado, CBA y economía

**Objetivo:** que las reglas de plantilla y dinero sean una sola verdad, compartida por IA, usuario y validadores.
**Paralelizable con F3:** no depende de la calibración del motor.

- [ ] **F4-01 · Un único evaluador de traspasos**
  - *Ref:* `ECO-03` · *Esfuerzo:* M · *Depende:* —
  - *Criterio:* `MarketController` deja de usar `TradeHelper.EvaluateTrade` para valorar y pasa por `TradeValueHelper` (o su sucesor); la validación legal (cap, apron, cupos) queda separada de la valoración de valor. Existe un test que compara la misma oferta evaluada desde ambos lados.
  - *Punto de partida:* `MarketController.cs:1726–1734`; `TradeValueHelper` ya usado en `DashboardController` (`:6216, 6534–6609`).

- [ ] **F4-02 · Traspaso del usuario y S&T transaccionales**
  - *Ref:* `ECO-04` · *Esfuerzo:* M · *Depende:* F4-01
  - *Criterio:* contratos, hard cap, jugadores, dorsales, picks, historial y mensajes se escriben dentro de una transacción; un fallo deja el estado intacto y muestra error. En el S&T, la revalidación ocurre **después** de fijar los salarios nuevos.
  - *Punto de partida:* `MarketController.cs:2022–2057, 2074–2135, 2155–2233`.

- [ ] **F4-03 · Reescribir las restricciones de apron con casos del CBA**
  - *Ref:* `ECO-01` · *Esfuerzo:* L · *Depende:* F1-02
  - *Criterio:* cada restricción (por encima del 1º, entre 1º y 2º, 2º apron, hard cap por NT-MLE) es una regla independiente con conteo de jugadores **entrantes y salientes**; existe una tabla de casos documentada y testeada, incluyendo el caso "recibir varios jugadores" y "enviar varios".
  - *Punto de partida:* `TradeHelper.cs:47–77, 145–171`.

- [ ] **F4-04 · Separar plazas estándar y two-way**
  - *Ref:* `ECO-05` · *Esfuerzo:* M · *Depende:* F1-02
  - *Criterio:* el modelo conoce 15 plazas estándar + N two-way + IR; firmar, traspasar, volver de IR y simular respetan la separación. `GetRosterCount` deja de mezclar conceptos.
  - *Punto de partida:* `TradeHelper.cs:14–17, 126–162`; `DatabaseManager.Players.cs`.

- [ ] **F4-05 · Derechos de agente libre: RFA/UFA, cap holds y service time**
  - *Ref:* `ECO-06` · *Esfuerzo:* XL · *Depende:* F4-03, F4-04
  - *Criterio:* un jugador tiene tipo de agente libre y años de servicio; los cap holds entran en la nómina efectiva; se puede igualar una oferta a un RFA y el mercado lo entiende. Tests de cap sheet completo.
  - *Punto de partida:* `PlayerData.cs:31–37` solo tiene contrato, opciones y años.

- [ ] **F4-06 · Escala salarial de rookie por puesto de draft**
  - *Ref:* `ECO-06` · *Esfuerzo:* M · *Depende:* F4-05
  - *Criterio:* el salario de un rookie depende del número de elección y de la temporada, no de un `Random.Range` por rango; existe tabla por año y test de monotonía decreciente con el pick.
  - *Punto de partida:* `DraftGenerator.cs:439–493`.

- [ ] **F4-07 · Parametrizar el CBA por temporada**
  - *Ref:* `ECO-07` · *Esfuerzo:* L · *Depende:* F1-04
  - *Criterio:* cap, tax, aprons y MLE salen de una tabla por temporada con cifras verificadas contra fuente oficial; deja de ser +5% fijo. Incluye repeater tax si se decide modelarlo.
  - *Punto de partida:* `TradeHelper.cs:7–18`; crecimiento en `DatabaseManager.Records.cs:3531–3543`.

- [ ] **F4-08 · Mínimo salarial por experiencia**
  - *Ref:* `ECO-07`, `F1-04` · *Esfuerzo:* S · *Depende:* F4-07
  - *Criterio:* el mínimo depende de años de servicio (0, 1, 2, 3+); los 9 contratos sembrados por debajo del mínimo universal quedan justificados o corregidos.
  - *Punto de partida:* `TradeHelper.MIN_SALARY` único.

**Puerta de salida F4:** un cap sheet calculado por el juego coincide con un cap sheet calculado a mano para tres equipos de prueba (uno por encima del 2º apron, uno entre aprons, uno por debajo del cap).

---

## 6. Fase 5 — Calendario y competiciones

**Objetivo:** que el calendario sea válido por construcción y que existan las competiciones NBA que faltan.
**Depende de F3** para no añadir partidos a un motor sin calibrar.

- [ ] **F5-01 · Localías 41/41 y balance divisional**
  - *Ref:* `CAL-01` · *Esfuerzo:* M · *Depende:* F0-02
  - *Criterio:* cada equipo juega 41 en casa y 41 fuera; cada pareja divisional se reparte 2-2; el test de F0-02 pasa a exigente (falla, no reporta).
  - *Punto de partida:* `ScheduleGenerator.cs:28–46, 167–180`.

- [ ] **F5-02 · Scheduler robusto y fail-fast**
  - *Ref:* `CAL-02` · *Esfuerzo:* M · *Depende:* F5-01
  - *Criterio:* cada partido se identifica individualmente (no por pareja orientada); el greedy registra los que no pudo colocar; si el resultado no son 1.230 partidos y 82 por equipo, la generación falla y **no** marca la temporada como generada.
  - *Punto de partida:* `ScheduleGenerator.cs:48–60, 73–105`.

- [ ] **F5-03 · Viajes, giras y días de descanso**
  - *Ref:* `CAL-03` · *Esfuerzo:* L · *Depende:* F5-02
  - *Criterio:* el calendario modela tramos de gira y descansos, la fatiga los refleja y el jugador puede verlos; sin back-to-backs imposibles por distancia.
  - *Punto de partida:* `ScheduleGenerator.cs:62–105, 184–225`.

- [ ] **F5-04 · NBA Cup**
  - *Ref:* `RET-01` · *Esfuerzo:* L · *Depende:* F5-02
  - *Criterio:* fase de grupos + eliminatoria con criterios de clasificación, calendario propio y estadísticas/premios separados; **no** toca `seasons.phase`; los partidos de grupo no se cuentan dos veces en la temporada regular sin decisión explícita.
  - *Datos:* `games.game_type` nuevo + `series_label`; tablas `tournament_editions`, `tournament_groups`, `tournament_standings`.

- [ ] **F5-05 · Summer League y camp de desarrollo**
  - *Ref:* `RET-01` · *Esfuerzo:* L · *Depende:* F5-04
  - *Criterio:* ventana post-draft con partidos de rookies/sophomores, minutos asignables y efecto acotado en desarrollo; el jugador decide convocatoria. No altera la fase de temporada.

**Puerta de salida F5:** calendario generado 30 veces seguidas cumpliendo 82/41-41/1.230 sin excepciones, y una temporada completa simulada con Cup y Summer League sin romper playoffs ni premios.

---

## 7. Fase 6 — Histórico y estadísticas

**Objetivo:** que la carrera de un jugador sea reconstruible y comparable, no solo un total de puntos.

- [ ] **F6-01 · Splits de tiro y pérdidas en `player_season_stats`**
  - *Ref:* `RET-02` · *Esfuerzo:* M · *Depende:* —
  - *Criterio:* se archivan FGM/FGA, 3PM/3PA, FTM/FTA, TO, OREB/DREB; eFG% y TS% se derivan de datos guardados, no de aproximaciones. Migración aditiva con backfill de temporadas anteriores donde sea posible.
  - *Punto de partida:* `DatabaseManager.cs:1133–1152`.

- [ ] **F6-02 · Resolver el rótulo/fórmula de PER** `[!]`
  - *Ref:* §8 pregunta 7 · *Esfuerzo:* S · *Depende:* —
  - *Criterio:* o se implementa el PER oficial, o se renombra la métrica propia y se explica en el tooltip. No puede llamarse PER y no serlo.
  - *Punto de partida:* `AdvancedStatsHelper.CalcPER`; `StatFormat.PerTip`.

- [ ] **F6-03 · Tendencias y comparativas en la UI de estadísticas**
  - *Ref:* `RET-02` · *Esfuerzo:* M · *Depende:* F6-01
  - *Criterio:* Stats/PlayerProfile/Trajectory muestran evolución por temporada y comparación con la liga usando solo datos persistidos; sin gráficos 3D, solo tablas, sparklines o barras.
  - *Punto de partida:* `StatsController.cs`, `PlayerProfileController.cs`, `TrajectoryController.cs`.

- [ ] **F6-04 · Integridad del histórico por `player_id`**
  - *Ref:* invariante 15 de `MEMORY.md` · *Esfuerzo:* S · *Depende:* F6-01
  - *Criterio:* ninguna consulta nueva resuelve carreras por nombre; las filas huérfanas (`player_id = 0`) siguen las reglas de `HistoricalStatsRules`; test que detecta adopciones ambiguas.
  - *Punto de partida:* `HistoricalStatsRules`, `historical_player_stats`.

**Puerta de salida F6:** una carrera de 10 temporadas se reconstruye íntegra desde histórico + temporada en curso + honores, con eFG/TS calculados.

---

## 8. Fase 7 — UX/UI, resoluciones e idioma

**Objetivo:** consistencia con las convenciones del proyecto y funcionamiento fuera de 1920×1080.
**Paralelizable desde el inicio:** no bloquea nada.

- [ ] **F7-01 · Migrar `rgba()` a `rgb()`**
  - *Ref:* `UX-01` · *Esfuerzo:* M · *Depende:* —
  - *Criterio:* 0 usos de `rgba()` en `Assets/_TacticalFive/UI` (hoy 790 en 46 USS); la transparencia se consigue con `opacity` o color sólido sobre fondo conocido; revisión visual de las pantallas afectadas.
  - *Punto de partida:* `GlobalVariables.uss:6` y siguientes.

- [ ] **F7-02 · Migrar `-unity-text-transform` a `text-transform`**
  - *Ref:* `UX-01` · *Esfuerzo:* S · *Depende:* —
  - *Criterio:* 0 usos de `-unity-text-transform` (hoy 229 en 38 USS) y 0 de `-unity-text-align` si se decide unificar; las mayúsculas se aplican con `text-transform: uppercase`.

- [ ] **F7-03 · Pruebas multi-resolución y tamaños mínimos**
  - *Ref:* `UX-02` · *Esfuerzo:* M · *Depende:* —
  - *Criterio:* revisadas 1280×720, 1366×768, 1920×1080, 2560×1440 y ultrawide; documentado qué pantallas rompen y corregidas con mínimos y escalado; los modales nunca exceden el viewport.
  - *Punto de partida:* `ProjectSettings.asset:45–47`; anchos fijos como `TheCity.uss:34–54`.

- [ ] **F7-04 · Una sola abstracción de scroll**
  - *Ref:* `UX-05` · *Esfuerzo:* M · *Depende:* F7-03
  - *Criterio:* un único componente/helper de scroll sustituye las implementaciones duplicadas; las 12 pantallas con `ScrollView` nativo se migran o se justifica por qué se quedan; listas grandes se virtualizan.
  - *Punto de partida:* `CustomScrollbar.cs` y los `BindScrollBar` repetidos en controllers.

- [ ] **F7-05 · Decidir el futuro de `TheCity`** `[!]`
  - *Ref:* `UX-04` · *Esfuerzo:* M · *Depende:* —
  - *Criterio:* o se documenta como hub de navegación (y se simplifica), o se convierte en pantalla de gestión con estado persistente. No se deja como está sin decisión escrita.
  - *Punto de partida:* `TheCityController.cs:4–38`; `TheCity.uxml:18–55`.

- [ ] **F7-06 · Extraer textos y preparar localización**
  - *Ref:* `UX-03` · *Esfuerzo:* L · *Depende:* F7-03
  - *Criterio:* los literales de UXML/C# salen de una fuente única con claves; el español es el idioma base y añadir otro no requiere tocar controllers.
  - *Punto de partida:* `Packages/manifest.json` sin paquete de Localization.

**Puerta de salida F7:** 0 `rgba()`, 0 `-unity-text-transform`, 5 resoluciones verificadas y checklist visual firmada.

---

## 9. Fase 8 — Arquitectura y rendimiento

**Objetivo:** que el código permita cambiar una regla sin tocar cinco sitios, y que una carrera larga siga siendo rápida.
**Depende de F3:** refactorizar sin tests de comportamiento es adivinar.

- [ ] **F8-01 · Extraer la economía del `DashboardController`**
  - *Ref:* `ARC-01` · *Esfuerzo:* L · *Depende:* F3-07
  - *Criterio:* nómina mensual, abonos, patrocinio, TV, luxury tax y asistencia viven en un servicio con tests; el controller solo llama y pinta.
  - *Punto de partida:* `DashboardController.cs:7763+`, `8288–8473`.

- [ ] **F8-02 · Extraer el mercado IA**
  - *Ref:* `ARC-01` · *Esfuerzo:* L · *Depende:* F4-01, F8-01
  - *Criterio:* `ProcessAITransfers`, `TryFindAITrade`, `TrySellVeteran`, S&T y fichajes de estrellas salen del controller a un servicio con estrategia inyectada y tests propios.
  - *Punto de partida:* `DashboardController.cs:5316–6860`.

- [ ] **F8-03 · Extraer el pipeline del día**
  - *Ref:* `ARC-01` · *Esfuerzo:* XL · *Depende:* F8-01, F8-02
  - *Criterio:* el día se compone de pasos nombrados (pre-lote, simulación, bookkeeping, transiciones) con orden explícito y test de secuencia; el controller queda como orquestador delgado.
  - *Punto de partida:* `ProcessGameDayRoutine`, `FastSimRoutine`.

- [ ] **F8-04 · Dividir `DatabaseManager.Records.cs`**
  - *Ref:* `ARC-01` · *Esfuerzo:* M · *Depende:* F3-07
  - *Criterio:* rollover, premios, histórico y progresión quedan en partials separadas por dominio sin cambiar firmas públicas; los tests siguen en verde.
  - *Punto de partida:* 4.448 líneas en un archivo.

- [ ] **F8-05 · Índices según consultas reales**
  - *Ref:* `DB-02` · *Esfuerzo:* M · *Depende:* —
  - *Criterio:* `EXPLAIN QUERY PLAN` sobre una partida de 10 temporadas; añadidos índices compuestos donde haya full scan en consultas del día o de standings; documentado qué se añadió y por qué.
  - *Punto de partida:* 16 `[Indexed]`, índices manuales en `DatabaseManager.cs:503–515`.

- [ ] **F8-06 · Migraciones que no oculten fallos**
  - *Ref:* `DB-03` · *Esfuerzo:* M · *Depende:* F8-05
  - *Criterio:* cada migración registra su resultado; `user_version` solo avanza si las obligatorias han ido bien; un fallo bloqueante impide abrir la partida con mensaje claro en vez de dejarla a medias.
  - *Punto de partida:* `DatabaseManager.cs:518–523, 1130–1157, 1200–1269`.

- [ ] **F8-07 · Timeout de lotes de fondo explícito**
  - *Ref:* `ARC-02` · *Esfuerzo:* S · *Depende:* —
  - *Criterio:* `WaitForBackgroundBatch` devuelve si terminó o no; el llamador decide (esperar más, cancelar y abortar el cambio de slot, error visible). Nunca se continúa escribiendo en el mismo fichero desde dos conexiones.
  - *Punto de partida:* `DatabaseManager.cs:193–239`.

- [ ] **F8-08 · Integridad referencial y borrados en cascada explícitos**
  - *Ref:* `DB-01` · *Esfuerzo:* L · *Depende:* F8-06
  - *Criterio:* tablas nuevas con FK o con validador equivalente; borrado de partida/slot deja 0 huérfanos verificables; test de integridad que recorre las relaciones declaradas.
  - *Punto de partida:* 0 FK y 0 `PRAGMA foreign_keys`.

**Puerta de salida F8:** `DashboardController` por debajo de ~3.000 líneas, día y economía con tests propios, y un día de 15 partidos medido antes/después.

---

## 10. Fase 9 — Proceso y documentación

**Objetivo:** que este documento no se quede obsoleto como los anteriores.

- [ ] **F9-01 · Sincronizar la base de conocimiento con el estado real**
  - *Ref:* `ARC-04` · *Esfuerzo:* S · *Depende:* F0-05
  - *Criterio:* `AGENTS.md`, `PROJECT_OVERVIEW.md`, `MEMORY.md`, `SKILLS.md`, `DATA_MODEL.md` y `AUDITORIA_FINAL.md` reflejan HEAD, número de tests, número de pantallas y features realmente incluidas; ninguna cifra contradice el código.
  - *Punto de partida:* `AGENTS.md:6` (HEAD `81d9e4f`), `PROJECT_OVERVIEW.md:5` (`8460d21`), auditoría previa con 290 tests frente a 334.

- [ ] **F9-02 · Ritual de cierre de fase**
  - *Ref:* proceso · *Esfuerzo:* S · *Depende:* —
  - *Criterio:* cada fase cerrada deja: ítems marcados, entrada en la §12 con fecha y commit, puerta de salida verificada y docs actualizados en el mismo commit.

**Puerta de salida F9:** una fase completa cerrada siguiendo el ritual, con su entrada en la bitácora.

---

## 11. Apuestas de producto (sin fecha)

No forman parte del plan de corrección; son decisiones de producto que la auditoría deja planteadas. Se abordan **después** de F3 y F5.

- [ ] **NF-01 · Fantasy draft e importación de rosters**
  - *Ref:* `RET-03` · *Esfuerzo:* L
  - *Criterio:* flujo con preview, mapeo de equipos/atributos y validación de cap/contratos antes de aplicar; nunca escribe parcialmente.

- [ ] **NF-02 · Economía de franquicia ampliada**
  - *Ref:* §6 auditoría · *Esfuerzo:* L
  - *Criterio:* merchandising, reparto de ingresos y renovación de derechos con reporte por canal, sin doble contabilización con el presupuesto actual.

- [ ] **NF-03 · Accesibilidad**
  - *Ref:* `UX-03` · *Esfuerzo:* M
  - *Criterio:* navegación por teclado, tamaño de texto y contraste revisados; al menos una alternativa a la lectura de tablas densas.

- [ ] **NF-04 · Modo sandbox / liga editable**
  - *Ref:* `RET-03` · *Esfuerzo:* L
  - *Criterio:* reglas de liga editables (cap, número de equipos, calendario) con validación, separado del modo carrera.

---

## 12. Decisiones pendientes (bloquean ítems)

Estas preguntas no son código: son respuestas que hay que escribir antes de ejecutar el ítem que bloquean.

| # | Pregunta | Bloquea | Responsable |
|---|---|---|---|
| D1 | ¿Qué significa "media de equipo 85"? | F1-01, F1-03 | Diseño |
| D2 | ¿Los 478 + 128 jugadores son la base definitiva? | F1-02, F1-04 | Diseño |
| D3 | ¿Qué temporada/edición del CBA se modela? | F4-03, F4-07, F4-08 | Diseño |
| D4 | ¿Se mantiene el límite propio de 2 two-way o se copia el CBA? | F4-04 | Diseño |
| D5 | ¿Motor de resultados estadístico o aleatoriedad opaca? | F3-01, F3-02 | Técnico |
| D6 | ¿"PER" es el PER oficial o una métrica propia? | F6-02 | Diseño |
| D7 | ¿`TheCity` es hub o sistema de gestión? | F7-05 | Producto |
| D8 | ¿Solo español y 1920×1080, o multi-idioma y multi-resolución? | F7-03, F7-06 | Producto |

---

## 13. Lo que NO hay que hacer

Recogido de las invariantes del proyecto y de los errores que esta auditoría habría inducido si se hubieran "arreglado" mal:

- **No sustituir el motor por posesiones por una distribución de resultados.** El problema es la secuencia, no el enfoque.
- **No añadir pantallas ni competiciones antes de F3.** Se acumula contenido sobre un motor sin calibrar.
- **No normalizar el seed antes de decidir D1.** Se haría dos veces.
- **No reintroducir comparaciones de valor ad hoc en traspasos.** `TradeValueHelper` es la fuente única (invariante 27 de `MEMORY.md`).
- **No limpiar los offsets de la G-League** creyendo que son un bug (invariante 12).
- **No añadir una columna `rotation_minutes` a `players`**: es transitoria `[Ignore]` (invariante 14).
- **No tocar `_db` fuera del main thread** ni abrir conexiones propias.
- **No añadir `yield return` dentro de la transacción del día.**
- **No marcar migraciones como aplicadas si fallaron.**
- **No reportar el convenio de balanceo como defecto**: lo que se corrige es el incumplimiento medido.

---

## 14. Bitácora

| Fecha | Fase | Ítems cerrados | Commit | Notas |
|---|---|---|---|---|
| 2026-10-23 | — | — | `e76bf723` | Creación del plan a partir de `AUDITORIA_2026-10-23.md`. |
| 2026-10-23 | F0 | `F0-01` | `a9f351ba` | Auditor de seed (`SeedAuditorTests`, 26 casos). Reproduce el informe y añade el bug de `overall` de G-League. Suite EditMode: **360/360 en verde**. |
| 2026-10-23 | F0 | `F0-04` | `a9f351ba` | Fórmula única del `overall` en `PlayerRating`; arreglado G-League (OV 5–7 → el de sus atributos) y añadida la migración `gleague_overall_recalc`. Auditor: **0 errores · 103 avisos**. Suite EditMode: **368/368**. |
| 2026-10-23 | F0 | `F0-03` | *(pendiente de commit)* | Matching salarial con límite único y error con cifra exacta; corregido buffer duplicado. Suite EditMode: **372/372**. |
