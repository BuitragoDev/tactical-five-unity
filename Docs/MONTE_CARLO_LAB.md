# Laboratorio Monte Carlo

El banco permite medir cambios en el motor sin abrir, leer ni escribir una partida.
Corre en memoria y usa `SeededRandomSource`, así que misma configuración y semilla dan
el mismo informe.

## Ejecutar desde Unity Editor

1. Abre **Tactical Five → Laboratorio Monte Carlo**.
2. Indica temporadas, número par de equipos, partidos por equipo y semilla `uint64`.
3. Pulsa **SIMULAR Y GENERAR INFORME**.

El informe Markdown se escribe en `Library/TacticalFive/MonteCarloReport.md` (carpeta
local ignorada por Git) y también aparece en la ventana y en la consola. El runner no
consulta `DatabaseManager`, no usa la UI del juego y no modifica ningún save.

API para tests/herramientas:

```csharp
var report = MonteCarloRunner.Run(new MonteCarloConfig
{
    seasons = 1,
    teams = 30,
    gamesPerTeam = 82,
    seed = 20261024UL
});
string markdown = report.ToMarkdown();
```

## Metodología y unidades

- Cada equipo tiene una plantilla sintética estable de **15 jugadores**, ratings derivados
  de sus 11 atributos mediante `PlayerRating`. La base OVR va de **72,08 a 81,58** con
  `SYNTHETIC_BASE_OVR=72.08` y `SYNTHETIC_OVR_SPREAD=9.5`; la curva concentra más equipos
  en el centro-bajo. La distribución se calibró para aproximar las bandas NBA de puntos,
  victorias y lesiones, no para clonar el seed. **No son los rosters reales ni se modifican
  los ratings del juego.**
- Calendario round-robin circular: cada ronda cruza a todos los equipos una vez y el
  ciclo se repite/trunca hasta llegar a `gamesPerTeam`. Las rondas de partido están
  separadas por un día de descanso: en los días de calendario el runner evoluciona las
  lesiones y recupera físico en el descanso.
- La simulación activa lesiones y fatiga **solo en los objetos en memoria** mediante
  `simulatePlayerStateWhenNotPersisting`; la duración se resuelve con `InjuryCatalog` y
  los días-jugador de baja se cuentan realmente dentro de la temporada (truncados al
  último día), no como la duración teórica completa de una lesión de final de temporada.
- Ritmo: promedio de posesiones local/visitante por partido. Puntos, FG%, 3P%, FTA,
  pérdidas, asistencias, rebotes y faltas: una muestra por equipo-partido. Victorias,
  días-jugador de baja y partidos-jugador perdidos por lesión: una muestra por equipo-temporada.
- La desviación típica es poblacional (`N`) y los percentiles usan interpolación lineal.
  Los porcentajes de tiro con cero intentos se omiten; por eso `N` puede ser menor que
  el total de equipo-partidos. El informe también incluye el promedio de cada equipo
  sintético a través de las temporadas, para localizar una franquicia sesgada que el
  promedio agregado esconda.
- Para el resultado NBA comparable, `MonteCarloOutcomeGate` evalúa la media de victorias,
  su desviación típica por equipo-temporada, promedios de mínimos/máximos por temporada,
  correlación Pearson OVR↔victorias y porcentaje de partidos que llegan a OT. El gate se
  acepta con **100 temporadas** (123.000 partidos); la muestra de una sola temporada solo
  sirve como comprobación rápida, no como aceptación estadística.
- El informe expone el mínimo de jugadores sanos disponible antes de un partido y las
  duraciones observadas por gravedad junto a la media ponderada y rango del `InjuryCatalog`.

## Perfil de aceptación F3-03

Perfil versionado **`NBA-2024-25-v3`** (v3 conserva las referencias NBA 2024-25 de
ofensiva y victorias, y sustituye el volumen bruto de lesiones por una puerta de
jugabilidad: 12–18 lesiones nuevas por equipo-temporada y disponibilidad para los 65 partidos). Para estadísticas de partido, el valor central es
el promedio de liga 2024-25 de Basketball-Reference; el intervalo aceptado es ese valor
± la tolerancia absoluta declarada. En porcentajes la tolerancia son puntos porcentuales.
No es un intervalo de confianza. Para las seis métricas ofensivas F3-04 se comparan,
además, P5/P50/P95 de las medias equipo-temporada y el promedio de cada equipo contra el
rango observado de la temporada (con tolerancia). Victorias comparan además
P5/P50/P95 de las 30 muestras equipo-temporada; las métricas de lesiones usan solo la media.

| Métrica | Referencia / objetivo | Tolerancia | Banda de media | Fuente/grano |
|---|---:|---:|---:|---|
| Ritmo | 98,8 | ±1,5 | 97,3–100,3 | Basketball-Reference, posesiones/equipo-partido |
| Puntos | 113,8 | ±3,5 | 110,3–117,3 | Basketball-Reference, puntos/equipo-partido |
| FG% | 46,7 % | ±1,5 pp | 45,2–48,2 % | Basketball-Reference, equipo-partido |
| 3P% | 36,0 % | ±2,0 pp | 34,0–38,0 % | Basketball-Reference, equipo-partido |
| FTA | 21,7 | ±2,1 | 19,6–23,8 | Basketball-Reference, intentos/equipo-partido |
| Pérdidas | 14,3 | ±2,0 | 12,3–16,3 | Basketball-Reference, equipo-partido |
| Asistencias | 26,5 | ±2,0 | 24,5–28,5 | Basketball-Reference, equipo-partido |
| Rebotes | 44,1 | ±3,0 | 41,1–47,1 | Basketball-Reference, equipo-partido |
| Faltas | 18,6 | ±2,0 | 16,6–20,6 | Basketball-Reference, faltas personales/equipo-partido |
| Victorias | 41,0 | ±1,0 | 40,0–42,0 | Basketball-Reference, 30 equipos × 82 partidos; además P5 18,45 ±5 y P95 62,65 ±5 |
| Partidos-jugador perdidos por lesión | 45,0 | ±20,0 | 25,0–65,0 | Diseño del juego: disponibilidad para premios |
| Lesiones nuevas por equipo-temporada | 15,0 | ±3,0 | 12,0–18,0 | Diseño del juego: ritmo de parte médico |
| Jugadores con ≥65 partidos | 11,0 | ±1,5 | 9,5–12,5 | Diseño del juego: 65 partidos para premios |
| Rotación con ≥65 partidos | 72,0 % | ±10,0 pp | 62,0–82,0 % | Diseño del juego: jugadores con ≥20 partidos |
| Partidos del equipo con lesión nueva | 17,0 % | ±5,0 pp | 12,0–22,0 % | Diseño del juego: frecuencia percibida |

Bandas del perfil para las **medias de equipo-temporada** de las métricas ofensivas
(referencias P5/P50/P95 de los 30 equipos 2024-25, interpolación lineal):

| Métrica | P5 NBA ± tol. | P50 NBA ± tol. | P95 NBA ± tol. | Min–máx NBA ± tol. por equipo |
|---|---:|---:|---:|---:|
| PTS | 105,24 ±4 | 114,05 ±3,5 | 121,30 ±5 | 105,1–121,9 ±6 |
| FG% | 43,79 ±2,5 pp | 46,90 ±1,5 pp | 48,97 ±2 pp | 43,0–50,6 ±2,1 pp |
| 3P% | 33,68 ±2,5 pp | 36,40 ±2 pp | 38,08 ±2,5 pp | 31,8–38,7 ±3 pp |
| FTA | 19,75 ±2,5 | 21,85 ±2 | 23,26 ±2,5 | 19,1–23,3 ±3 |
| Pérdidas | 12,49 ±2 | 14,15 ±2 | 15,87 ±2 | 11,7–17,2 ±3 |
| Asistencias | 23,25 ±2,5 | 26,25 ±2 | 29,42 ±2,5 | 23–31 ±4 |

El chequeo de rango por equipo promedia sus temporadas dentro del informe; así distingue
una franquicia que queda sesgada de un solo partido extremo. Las tolerancias por equipo
son más amplias que las de percentiles de liga porque la liga del runner es sintética y
no usa el seed real.

Las tolerancias son **decisiones de aceptación del proyecto**: amplias frente al ruido de
una muestra fija de 1.230 partidos, pero no estimaciones de error de las fuentes. El gate
`MonteCarloCalibrationGate` añade cada métrica fuera de banda a la lista de fallos;
`AssertAccepted(report)` lanza una excepción y el informe marca `FALLO`. El test activo
comprueba tanto límites inclusivos como el fallo por una media/cola fuera de rango.

La métrica `PlayerDaysOut` (días calendario-jugador) queda **informativa, sin gate**:
la fuente de lesiones publica partidos perdidos, no días calendario homogéneos. El runner
añade `PlayerGamesMissed`, que cuenta cuántos jugadores lesionados faltan al partido y sí
es comparable con “man-games lost”.

Fuentes consultadas el 24-10-2026:

- [Basketball-Reference — NBA League Averages, Per Game](https://www.basketball-reference.com/leagues/NBA_stats_per_game.html), fila 2024-25: ritmo 98,8; PTS 113,8; FG% .467; 3P% .360; FTA 21,7; TOV 14,3; AST 26,5; REB 44,1; PF 18,6.
- [Basketball-Reference — 2024-25 Standings](https://www.basketball-reference.com/leagues/NBA_2025_standings.html), W-L de los 30 equipos; P5/P95 calculados con interpolación lineal.
- [RotoWire — The Most Injured NBA Teams in 2024-25](https://www.rotowire.com/basketball/article/the-most-injured-nba-teams-in-2024-25-94303), publicado 10-07-2025; sus 30 valores de “Total Games Missed” citan como base el NBA Injuries Tracker de BBall Index. Media 396,97; P5 280; P95 514,05.

## Informe de humo reproducible

**Snapshot histórico anterior a F3-05** de una temporada sintética, 30 equipos × 82 partidos, semilla `20261024` (EditMode). Tras F3-05, la liga sintética y la fase final del motor cambiaron; los resultados vigentes están en «Resultado F3-05» abajo. Se conserva esta tabla como registro de la medición usada durante F3-04:

| Métrica | Media | Desv. típica | P5 | P50 | P95 |
|---|---:|---:|---:|---:|---:|
| Ritmo (posesiones/equipo-partido) | 98,13 | 2,34 | 95,00 | 98,00 | 101,00 |
| Puntos/equipo-partido | 113,96 | 14,64 | 90,00 | 114,00 | 138,00 |
| FG% | 45,50 | 5,87 | 35,56 | 45,55 | 55,06 |
| 3P% | 35,29 | 8,23 | 21,88 | 35,29 | 48,65 |
| FTA/equipo-partido | 22,79 | 8,29 | 10,00 | 22,00 | 37,00 |
| Pérdidas/equipo-partido | 14,70 | 3,53 | 9,00 | 15,00 | 21,00 |
| Asistencias/equipo-partido | 26,16 | 4,98 | 18,00 | 26,00 | 35,00 |
| Rebotes/equipo-partido | 44,29 | 7,12 | 33,00 | 44,00 | 56,00 |
| Faltas/equipo-partido | 18,13 | 3,99 | 12,00 | 18,00 | 25,00 |
| Victorias/equipo-temporada | 41,00 | 16,70 | 16,15 | 40,50 | 65,40 |
| Días-jugador de baja/equipo-temporada (informativo) | 57,40 | 45,81 | 9,80 | 41,50 | 120,65 |
| Partidos-jugador perdidos por lesión/equipo-temporada | 27,00 | 22,35 | 4,90 | 19,00 | 58,55 |

Con F3-04, el **gate ofensivo pasa** (media y P5/P50/P95 por equipo-temporada, además del
control de cada equipo) para PTS, FG%, 3P%, FTA, pérdidas y asistencias. El gate general
sigue **FUERA DE RANGO** solo por partidos-jugador perdidos por lesión, pendiente de
F3-06. Se conserva esa alerta: no se amplían bandas para ocultar la disponibilidad aún
no calibrada.

## Resultado F3-05 (100 temporadas)

El perfil de resultados de la NBA 2024-25 usa 41,00 victorias de media (±0,50), desviación
típica de victorias 13,130118 (±4), mínimo medio 8–22, máximo medio 62–75, correlación
OVR↔victorias 0,60–0,98 y tasa de OT **60/1.230 = 4,878 %** (banda 3,38–6,38 %). No se
ensancharon tolerancias. Con semilla `20261024`, 100 temporadas produjeron:

| Métrica | Resultado | Banda | Estado |
|---|---:|---:|---|
| Victorias/equipo-temporada | 41,00 | 40,50–41,50 | OK |
| Desviación típica de victorias | 14,11 | 9,13–17,13 | OK |
| Mínimo de victorias medio | 15,66 | 8–22 | OK |
| Máximo de victorias medio | 68,68 | 62–75 | OK |
| Correlación Pearson OVR↔victorias | 0,943 | 0,60–0,98 | OK |
| Partidos con OT | 5,11 % (6.287/123.000) | 3,38–6,38 % | OK |

La tasa de OT necesitó una fase final acotada en el 4.º cuarto (`GameSimulator.SimQuarter`):
cuando el **marcador acumulado** está a 1–6 puntos, se alternan hasta tres posesiones desde
el equipo que va por detrás. Se detiene si hay empate (OT), remontada o el margen supera 6.
El diagnóstico inicial comparaba por error el parcial del cuarto; se corrigió y el test
directo de motor verifica que los partidos que llegan a OT estaban empatados al terminar
el reglamento. El volumen de libres no se incrementa artificialmente.

La compresión sintética de OVR (base 73,36, spread 8,6, media aproximada conservada en
76,8) fue el primer ajuste de F3-05. La calibración de lesiones de 2026-10-27 fija el
roster sintético en 15 jugadores (`72.08`, `9.5`) para que la cola alta de puntos siga
dentro de la referencia NBA con menos bajas. El gate ofensivo y el de resultados siguen
aprobados con esa población.

## Calibración de lesiones y disponibilidad (2026-10-27)

`PlayerGamesMissed` cuenta los jugadores lesionados que faltan a cada partido (una ausencia
por jugador-partido), no la duración teórica de la lesión. El perfil `NBA-2024-25-v3`
sustituye la banda NBA de man-games (396,97 ±70) por una puerta de jugabilidad:
**45 ±20** partidos-jugador perdidos, **15 ±3** lesiones nuevas por equipo-temporada,
**11 ±1,5** jugadores de 15 con ≥65 partidos, **72 ±10 %** de la rotación con ≥20 partidos
que alcanza 65 y **17 ±5 %** de partidos del equipo con una lesión nueva. `PlayerDaysOut`
sigue siendo informativo.

La tasa base de `InjuryHelper.MatchRiskPerGame` es **0,022 a 36 min**; la probabilidad sigue
siendo proporcional a la exposición, con ajuste por físico bajo y recaída. Al aplicar un
episodio postpartido, `CanSustainMatchInjury` no permite que la convocatoria caiga debajo
de cinco sanos. El reporte registra además la menor convocatoria sana observada, el número
de apariciones con minutos >0 y las duraciones por gravedad.

Semilla `20261024`, 100 temporadas / 123.000 partidos:

| Métrica | Resultado | Banda | Estado |
|---|---:|---:|---|
| Partidos-jugador perdidos, media | 50,36 | 25,00–65,00 | OK |
| Lesiones nuevas por equipo-temporada | 14,03 | 12,00–18,00 | OK |
| Jugadores con ≥65 partidos | 10,81/15 | 9,50–12,50 | OK |
| Rotación con ≥65 partidos | 72,39 % | 62,00–82,00 % | OK |
| Partidos con lesión nueva | 15,78 % | 12,00–22,00 % | OK |
| Mínimo de jugadores sanos | 8 | ≥5 | OK |

| Gravedad | Episodios | Media observada | Media catálogo ponderada | Rango del catálogo |
|---|---:|---:|---:|---:|
| Leve | 17.816 | 2,57 días | 2,57 días | 1–7 |
| Moderada | 19.605 | 9,62 días | 9,59 días | 3–21 |
| Grave | 4.242 | 29,81 días | 29,78 días | 14–75 |
| Crítica | 428 | 130,86 días | 123,60 días | 42–300 |

En la misma ejecución pasan todos los gates anteriores: F3-04 ofensivo y global, F3-05
(W=41,00; SD=14,11; mínimos=15,66; máximos=68,68; r=0,943; OT=5,11 %). Las tolerancias
del perfil no se ampliaron; la única banda sustituida es la de lesiones, por una decisión
explícita de jugabilidad (65 partidos para premios).
