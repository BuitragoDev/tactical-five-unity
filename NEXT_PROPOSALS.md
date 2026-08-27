# NEXT_PROPOSALS — Mejoras pendientes (en orden de implementación)

Estado: 4/4 completadas (verificado en HEAD `81d9e4f`).

## A. Previa/pronóstico del partido (Matchup Preview) — **HECHO**
- [x] Helper `Scripts/Stats/MatchupPreview.cs`: favorito + probabilidad de victoria a
      partir de overall, moraleja, química y ventaja local (misma fórmula que
      `GameSimulator`), con forma reciente de `player_game_stats`.
- [x] Panel "PRONÓSTICO" en `MatchDay` (entre el banner y los box scores).
- Encaje: complementa el play-by-play; solo lectura + UI, no toca persistencia.

## B. Retiro de dorsales (jersey retirement) — **HECHO**
- [x] Tabla `retired_numbers(team_id, player_id, number, rings, career_points, …)` +
      seed con leyendas (`RetiredNumberSeeder` 53 + `VeteranRetiredNumberSeeder` 17).
- [x] Inducción junto al Salón de la Fama (`TryRetireNumber` en `StartNewSeason`).
- [x] Vista en la pantalla `Dorsales` (tabs Actuales/Retirados) y en el perfil del
      jugador (contadores de honores); `AssignJerseyNumber` reserva dorsales.

## C. Picks protegidos + swap — **HECHO**
- [x] Columnas `protected_from`/`is_swap`/`swap_original_team_id` en `DraftPickData.cs`.
- [x] Resolución de lotería en `DraftGenerator.cs` (revierte al original si cae dentro
      del rango; swap: el tenedor se queda la mejor posición).
- [x] UI: protección/swaps visibles en el mercado de picks de `MarketController`.
- Encaje: la IA ya valora picks y protege jóvenes (`TradeHelper.PickBonus`).

## D. G-League / IR / contratos two-way — **HECHO**
- [x] **IR (reserva de lesionados):** columna `players.is_on_ir`; botón PONER/SACAR IR en
      `InjuredController` (baja ≥90 días, `TradeHelper.IsEligibleForIR`); no cuenta en el
      tope de plantilla (`GetRosterCount` excluye IR). Al recuperarse, si quedan >17 la IA
      libera al peor y el usuario recibe un modal "PLANTILLA LLENA" (`ShowPendingIRReleaseModal`).
- [x] **Contratos two-way:** columna `players.is_two_way`/`offers.is_two_way` + salario fijo
      `TradeHelper.TWO_WAY_SALARY` (máx 2/equipo, edad ≤23). Toggle TWO-WAY en la oferta FA
      (`MarketController`), firma IA (`TrySignFreeAgent`) y rookies de 2ª ronda (`DraftGenerator`).
- [x] **G-League ligera:** `GLeagueHelper` (asignar/recuperar, desarrollo +1 atributo/7 días
      cap `potential`); botones ASIGNAR G / G-LEAGUE en `RosterController`,
      panel de stats en el detalle del jugador, gancho semanal en `ProcessGameDayRoutine`
      (`ProcessGLeagueDevelopment`), tabla `gleague_season_stats`; jugadores G-League excluidos
      de quinteto/simulación (`GetActivePlayers`, `QuintetoController`, `MatchDayController`, All-Star).
- [x] **G-League como liga completa** (ampliación rama `gleague`): 30 filiales reales
      (`gleague_teams`) + 11 prospectos cada una (`gleague_players`); calendario propio de 28
      partidos/filial solo en días NBA (nov→mar, pausa All-Star) con `GLeagueScheduleGenerator`;
      simulación diaria vía `ProcessGLeagueGame` (`SimulateGame(persistToDb:false)`, sin
      player_game_stats/records/fatiga/lesiones; stats por partido en `gleague_season_stats`);
      clasificación en memoria (`GLeagueStandings`); playoffs eliminatorios QF→SF→CF→Gran Final
      (`GLeaguePostSeason`, campeón en `gleague_champions`); pantalla GLeague con 4 pestañas
      (PLANTILLA/RESULTADOS/CLASIFICACIÓN/ESTADÍSTICAS). Los ids de filial van codificados
      en `games` (+1000, `DecodeGlTeamId`); la postseason nunca toca `seasons.phase`.
- Encaje: sin pantalla nueva; reutiliza Roster/Lesionados/Mercado/Quinteto.
