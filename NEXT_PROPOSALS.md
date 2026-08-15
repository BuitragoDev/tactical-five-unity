# NEXT_PROPOSALS — Mejoras pendientes (en orden de implementación)

Estado: 3/4 completadas (verificado en HEAD `1d88989`).

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

## D. G-League / IR / contratos two-way — **PENDIENTE** (0%)
- [ ] Liga de desarrollo, reserva por lesión y contratos two-way.
- [ ] Toca RosterController, flujo de lesiones (InjuredController) y Dashboard loop.
- Es el de mayor calado; se hará en último lugar.