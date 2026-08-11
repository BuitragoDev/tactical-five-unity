# NEXT_PROPOSALS — Mejoras pendientes (en orden de implementación)

Estado: 0/4 completadas. Rama: `crear-mejoras2`.

## A. Previa/pronóstico del partido (Matchup Preview)
- [ ] Helper `Scripts/Stats/MatchupPreview.cs`: favorito + probabilidad de victoria
      a partir de overall, moraleja, química y ventaja local (misma fórmula que
      `GameSimulator`), con forma reciente de `player_game_stats`.
- [ ] Panel "PRONÓSTICO" en `MatchDay` (entre el banner y los box scores) y,
      en una 2ª fase, en el día seleccionado de `Calendar`.
- Encaje: complementa el play-by-play; solo lectura + UI, no toca persistencia.

## B. Retiro de dorsales (jersey retirement)
- [ ] Tabla `retired_numbers(team_id, player_id, number, season_label)` + seed
      con leyendas (patrón `HallOfFameSeeder`).
- [ ] Inducción junto al Salón de la Fama (`DatabaseManager.Records.cs:1312`).
- [ ] Vista en Palmarés y en el perfil del jugador (contadores de honores).
- Encaje: sigue la línea de rings/Finales-MVP/HoF ya implementados.

## C. Picks protegidos + swap
- [ ] Columnas `protected_from`/`is_swap` en `DraftPickData.cs` + migración
      `ALTER TABLE`.
- [ ] Resolución de lotería en `DraftGenerator.cs`.
- [ ] UI: mostrar protección en ofertas/picks de `MarketController`.
- Encaje: la IA ya valora picks y protege jóvenes (`TradeHelper`).

## D. G-League / IR / contratos two-way
- [ ] Liga de desarrollo, reserva por lesión y contratos two-way.
- [ ] Toca RosterController, flujo de lesiones (InjuredController) y Dashboard loop.
- Es el de mayor calado; se hará en último lugar.
