# DATA_MODEL — Tactical Five

> Complete map of the SQLite schema, the seed data, migrations, and the DTOs used across the game. **[F]** fact, **[D]** deduction.
> All tables live in one SQLite file per save slot (`save_{N}.db`) and in `template.db`. Managed by `DatabaseManager.CreateTables()` / `RunMigrations()` / `SeedStaticDataIfNeeded()`.

## 1. Conventions

- `sqlite-net` attributes: `[PrimaryKey]`, `[AutoIncrement]`, `[Indexed]`, `[Table("name")]`, `[Ignore]`.
- **No `FOREIGN KEY` constraints anywhere** [F]. Relations are by convention via `*_id` columns. Referential integrity is the app's responsibility.
- Dates: most are `TEXT` (`"yyyy-MM-dd"`, `"yyyy-MM-dd HH:mm:ss"`), manually formatted. `storeDateTimeAsTicks = true` is set but unused for game data.
- `PlayerData.id` is **manual, not autoincrement** (seed IDs 1..~600 stable across cloned slots). `TeamData.id` is currently marked `[AutoIncrement]` in `TeamData.cs`; seed data usually supplies stable IDs, but the model does not enforce a manual-ID contract for teams. Other tables autoincrement.
- **Schema versioning [F]:** `schema_migrations` table (`name` PK, `applied_at`) + `PRAGMA user_version = 2` (`SCHEMA_VERSION = 2`). Column-based additive migrations still use `PRAGMA table_info`; data migrations use `IsMigrationApplied`/`MarkMigrationApplied`. **F8-06:** `schema_migration_attempts` registra cada intento (`ok`/`failed` + error + fecha); un fallo obligatorio lanza `DatabaseMigrationException`, revierte todo el bloque y no avanza `user_version`.

### Índices compuestos (F8-05)

`DatabaseManager.QueryIndexDefinitions` aplica índices idempotentes en `CreateTables`:

- `games(manager_id, season_id, game_type, is_played, game_day, id)` y `games(manager_id, game_day, is_played)`.
- `player_game_stats(team_id, game_id)`.
- `player_season_stats(season_id, team_id)` y `(player_id, season_id)`.
- `finance_records(team_id, season_id, record_type, game_day)`.
- `offers(manager_id, processed, day_sent)` y `trade_offers(manager_id, processed)`.
- `draft_picks(current_team_id, draft_year)` y `(draft_year)`.
- `team_lineup(team_id)`, `season_records(season_id)`, `monthly_awards(season_id)` y `team_cap_holds(team_id, season_id, is_active)`.

Se validan con `DatabaseIndexPlanTests` (`EXPLAIN QUERY PLAN`) sobre una base temporal con forma de 10 temporadas.

## 2. Relational overview

```
managers 1──N seasons (manager_id)              managers 1──1 teams (team_id)
teams   1──N players (team_id; 0 = FA)          teams 1──N employees/scouts/loans/team_settings/team_lineup
players 1──N player_game_stats (player_id)      games 1──N player_game_stats (game_id)
games   1──1 game_attendance (game_id PK)       seasons 1──N games/draft_picks/trade_offers/finance_records
players 1──N training/offers/player_personalities   players N──N players → player_relationships
players 1──N finals_player_stats                season_records.*_id → players
teams   1──1 sponsor activo (sponsor_id in team_settings)
teams   1──1 tv_channel activo (tv_channel_id in team_settings)
players 1──1 hof_players (player_id)            teams 1──N retired_numbers (team_id)
```

## 3. Tables (field-by-field)

### teams (`TeamData`)
`id` (PK, `[AutoIncrement]` in the model; seeded IDs are expected to remain stable), `name`, `abbreviation`, `city`, `conference` (East/West), `division`, `arena`, `capacity`, `owner`, `attack`, `defense`, `overall`, `budget`, `reputation` (1–5), `facilities` (1–5), `logo`, `jersey_home`, `jersey_away`, `salary_margin`, `objective` (TEXT: "Campeonato"|"Playoffs"|"Play-In"|"Zona tranquila"|…, migrated), `team_chemistry`, `first_apron_hard_capped` (int 0/1, migrated), `arena_renovation_type/count/cost/end_day` (set via ArenaController), `arena_renovation_end_day`.

### players (`PlayerData`)
`id` (PK manual), `team_id` (0 = FA), `first_name`, `last_name`, `position` (PG/SG/SF/PF/C), `secondary_position` (migrated), `age`, `nationality` (ISO3), `college` (migrated), `height_cm`, `weight_kg`, `overall`, `potential`, and the 11 attributes: `speed, shooting, three_point, passing, dribbling, defense, rebounding, athleticism, iq, steals, blocks`. Then `salary`, `contract_years`, `is_rookie` (0/1), `injury_days`, `injury_type`, `treated`, `renewal_cooldown_day` (migrated), `seasons_with_team`, `years_of_service` (migrated, service time de diseño; saves antiguos: estimación editable), `free_agent_type` (`""` con contrato; `RFA`/`UFA` al expirar), `qualifying_offer_salary`, `morale` (migrated, default 50), `fisico` (migrated, default 99), `role` (migrated, `PlayerRole` int), `photo` (migrated), `number` (dorsal), `on_trade_block` (0/1, marcado desde Roster → jugador TRANSFERIBLE en Market). `salary` es un único importe anual que el juego usa tanto para nómina como para el cap; no hay columnas separadas para salario base/cash y cap hit. En las entradas Houston del seed 2026-27 revisadas en F1-04 se guarda el **cap hit** (p. ej. los mínimos con reembolso CBA), no una base cash distinta. Contract options (migrated): `guaranteed_years` (0 if the last year is an option), `has_team_option` (0/1), `has_player_option` (0/1 — mutually exclusive with team option), `last_team_id` (migrated; último equipo para el que jugó, 0 si nunca/FA externo — habilita Bird rights al re-firmar). `contract_years` includes both guaranteed years and the option year.
- **Honores de carrera (migrados, default 0):** `rings` (campeonatos ganados), `finals_mvps` (MVP de las Finales), `finals_played` (finales disputadas), `season_mvps` (MVP de temporada regular). Se incrementan en `SaveSeasonEndRecords` (ver GAMEPLAY §11). Se muestran como contadores en el header de `PlayerProfile` y `Trajectory` (CAMPEONATOS / FINALES / MVP / MVP FINALS).
- **IR / two-way / G-League (migrados, default 0):** `is_on_ir` (1 = reserva; fuera de cupos activos), `is_two_way` (1 = contrato two-way, salario `TWO_WAY_SALARY`, máximo de diseño 2/equipo, edad ≤23), `g_league_assigned` (1 = asignado a la liga de desarrollo, no juega en NBA). Cupos: 15 estándar + 2 two-way + jugadores IR aparte. `GetRosterCount` cuenta solo estándar activo; `GetTwoWayCount` cuenta contratos two-way incluidos los lesionados; ver GAMEPLAY §14.
- `GetCalculatedAverage()` delegates to `PlayerRating.FromAttributes`: the mean of 11 attributes is rounded and capped by `potential`. Training, progression, mentoring, draft and migrations recalculate `overall` after attribute mutations.

### managers (`ManagerData`)
`id` (AI), `name`, `team_id`, `game_mode`, `created_at`, `lastPlayedRealDate`, `currentGameDay`, `currentDate`, `trust`, `morale`, `fan_confidence` (migrated, default 50), `budget_red_warnings` (migrated, default 0), `is_fired` (migrated, default 0; 1 = despedido, bloquea cargar la partida), `career_reg_wins/losses`, `career_po_wins/losses`, `championships`, `seasons_completed` (migrated).

### seasons (`SeasonData`)
`id`, `year_start`, `year_end`, `is_active` (0/1), `current_game_day`, `game_mode`, `phase` (`preseason`|`regular`|`playin`|`playoff`|`finished`), `manager_id`, `generated` (0/1), `current_date` ("yyyy-MM-dd"), `last_ai_trade_day` (migrated, default -999).

### games (`GameData`)
`id`, `season_id`, `manager_id`, `game_day`, `game_date`, `home_team_id`, `away_team_id`, `home_score`, `away_score`, `is_played` (0/1), `game_type` (`preseason`|`regular`|`playin`|`playoff`|`allstar`|`gleague`|`gleague_playoff`), `series_label` (playoff series id; G-League: `gl-qf-east-N`, `gl-sf-{conf}-{a,b}`, `gl-cf-{conf}`, `gl-final`). Indexes: `season_id, game_day, is_played, game_type, manager_id`. Plus manual index `IX_Games_Standings(manager_id, game_type, is_played, game_day)`. For All-Star: `home_team_id=-1` (East), `away_team_id=-2` (West). **Partidos G-League: los ids de filial van codificados `+GLeagueHelper.GAME_TEAM_ID_OFFSET (1000)`** (ver GAMEPLAY §16).

### player_game_stats (`PlayerGameStats`)
`id`, `game_id`, `player_id`, `team_id`, `minutes`, `points`, `fgm, fga, fg3m, fg3a, ftm, fta`, `oreb, dreb, rebounds`, `assists`, `steals`, `blocks`, `turnovers`, `pf`, `rating`, `double_double`, `triple_double`. Indexes on game/player/team (`IX_PlayerGameStats_GameId/PlayerId/TeamId`).

### gleague_season_stats (`GLeagueSeasonStat`)
`id` (autoinc), `player_id`, `season_id`, `games`, `points`, `rebounds`, `assists`, `steals`, `blocks`, `turnovers`, `rating`. Acumulados **por partido real simulado** de la G-League (`AddGLeagueGameStat`, games += 1; ver GAMEPLAY §16). `player_id` puede ser un jugador NBA real (asignado) o un prospecto con id = fila + `PROSPECT_ID_OFFSET (500000)`. Índices `IX_GLeagueStats_PlayerId`/`IX_GLeagueStats_SeasonId`.

### gleague_teams (`GLeagueTeamData`)
`id` (autoinc), `name` (filial real, ej. "Maine Celtics"), `abbreviation` (iniciales), `conference` (`East`/`West`), `logo` (clave PNG en `Teams/GLeague/{size}`, ej. `celtics_gleague`), `nba_team_id` (franquicia matriz). 30 filiales sembradas una vez por slot (`EnsureGLeagueSeeded`).

### gleague_players (`GLeaguePlayerData`)
`id` (autoinc), `gleague_team_id`, `first_name`, `last_name`, `position`, `age`, `overall`, `potential` + los 11 atributos (`speed…blocks`). 11 prospectos por filial generados en el seeding (`GLeagueSeeder.GenerateProspects`); persisten entre temporadas. Nunca se escriben en `players`; para simular se mapean a `PlayerData` transitorios con id = fila + `PROSPECT_ID_OFFSET`.

### gleague_champions (`GLeagueChampionData`)
`id` (autoinc), `manager_id`, `season_id`, `season` (año final), `gleague_team_id`, `team_name`. Un registro por temporada (guard UNIQUE lógico en `SaveGLeagueChampion`); lo escribe `GLeaguePostSeason.RecordChampionIfNeeded` al ganar la Gran Final.

### game_attendance (`GameAttendanceData`)
`game_id` (PK, no autoinc), `attendance`, `ticket_price`, `revenue`. Saved by `ProcessGameFinances`; read by results/venue UI.

### league_settings (`LeagueSettingsData`, `LeagueSettings.cs`)
`id`, `salary_cap`, `luxury_tax`, `apron`, `repeater_apron` (en realidad 2.º apron), `mid_level`, `taxpayer_mid_level`, `bi_annual`, `minimum_salary`, `two_way_salary`, `season_start_year`, `cba_projected`, `cba_source`, `is_active`. **F4-07:** los topes y excepciones se copian de `CbaTable` (`ApplyTo`) al sembrar y en cada `StartNewSeason`; ya no crecen con un +5 % ciego. `season_start_year` indica la fila aplicada, `cba_projected=1` marca una temporada sin cifra oficial y `cba_source` guarda la procedencia. **F4-08:** `minimum_salary` guarda el **mínimo oficial de 0 años** de la temporada; las bandas de 1, 2 y 3+ años viven en `CbaTable` y se consultan con `TradeHelper.MinimumSalary(settings, years)`. La migración `minimum_salary_by_experience` actualiza partidas antiguas que conservaban el suelo interno de 2 M. La migración `cba_table_by_season` sincroniza partidas antiguas con la fila de su temporada activa.

### team_cap_holds (`TeamCapHoldData`)
`id` (PK AI), `team_id`, `player_id`, `season_id`, `amount`, `rights_type` (`RFA`/`UFA`), `is_active`. Se crea al expirar contrato/opción y se elimina al firmar o renunciar. No es salario contable ni afecta nómina mensual/impuesto; sí suma al payroll efectivo del cap sheet, mercado y validación de traspasos. El hold activo se ignora si el jugador ya no es FA del equipo titular.

### NBA Cup (F5-04)
- `tournament_editions`: `id`, `season_id`, `manager_id`, `year_start`, `status` (`group`/`knockout`/`finished`), `champion_team_id`, `mvp_player_id`, `created_at`.
- `tournament_groups`: `id`, `edition_id`, `group_name`, `conference`, `team_id`. Una fila por equipo; el grupo es la división.
- `tournament_standings`: `id`, `edition_id`, `group_name`, `conference`, `team_id`, `wins`, `losses`, `points_for`, `points_against`, `point_diff`, `rank`.
- Los partidos de grupo son `games` regulares con `series_label="cup-group-..."`; los de eliminatoria son `game_type="cup_playoff"` y `series_label="cup-qf-*"`, `"cup-sf-*"`, `"cup-final"`.

### Summer League (F5-05)
- `summer_league_stats`: `id`, `manager_id`, `season_id`, `player_id`, `team_id`, `games`, `minutes`, `points`, `rebounds`, `assists`, `development_gain`, `created_at`.
- Una fila por convocado; `development_gain` es 0/1 y el camp no se repite si ya existen filas para `manager_id + season_id`.

### Ciudad (F7-05)
- `city_projects`: `id`, `manager_id`, `season_id`, `team_id`, `project_key` (`entorno_pabellon`|`centro_rendimiento`|`programa_aficion`|`centro_medios`), `level`, `status` (`active`|`completed`), `started_day`, `completion_day`, `cost`, `created_at`. Una fila por nivel iniciado; `GetCityProjectLevel` cuenta solo las completadas y el nivel pasivo se consulta por temporada.
- `city_events`: `id`, `manager_id`, `season_id`, `event_key` (`queja_vecinal`|`campana_abonados`|`visita_escolar`|`ola_calor`|`rueda_prensa`), `status` (`pending`|`resolved`), `game_day`, `created_at`, `resolved_day`, `choice_index`. Solo se muestra el evento pendiente de la temporada activa.
- Efectos acotados: asistencia en casa (+4 %/nivel), físico en descanso (+2/nivel), confianza de la afición y moral/físico del vestuario. No crean economía paralela; los costes salen de `teams.budget` y los avisos usan `messages` (`NewsType.PROYECTO_CIUDAD`/`EVENTO_CIUDAD`).

**Reglas simplificadas (F4-05, no CBA oficial):** service 0–3 → RFA; 4+ → UFA. QO = `max(salario_mínimo_de_su_experiencia, salario_anterior × 1.25)`. Hold RFA = QO; UFA = 120 % del salario previo o 150 % si `seasons_with_team ≥ 3`. Two-way no genera cap hold. Matching IA por regla de `FreeAgencyRules`; topes/ventanas CBA quedan para F4-07.

### season_records (`SeasonRecord`)
`id`, `season_id`, `champion_id`, `finalist_id`, `finals_result`, `east/west/div1..6_champion_id`, `finals_mvp_id`, `finals_mvp_rating`, `season_mvp_id/rating/games`, `rookie_of_year_id/rating/games`, `best_defender_id`, `sixth_man_id`, `most_improved_id`, `all_star_pg/sg/sf/pf/c_id`, `first_team_pg..c`, `second_team_pg..c`.

### finance_records (`FinanceRecord`)
`id`, `team_id`, `season_id`, `record_type`, `game_day`, `amount`, `created_at`. Types: `1=Taquilla, 2=Abonos, 3=Patrocinios, 4=Televisión, 5=Remodelación, 6=Despido, 7=Sueldos jugadores, 8=Sueldos empleados, 9=Préstamo, 10=Luxury tax, 11=Buyout`. (`GetTotalIncome` = types ≤4; `GetTotalExpenses` = types ≥5.) `ProcessTeamLuxuryTax` escribe type 10 en **positivo** (`ECO-05`); la migración `loan_records_reclassified` normaliza los registros antiguos.

### storylines (`StorylineData`)

Hilos narrativos persistentes (motor de narrativa, Fase 4). CRUD en el partial
`DatabaseManager.Storylines.cs`.

| Campo | Tipo | Notas |
|---|---|---|
| `id` | INTEGER PK AI | |
| `manager_id` / `team_id` | INTEGER | |
| `type` | TEXT | `StorylineType.*` (racha_positiva, bache, jugador_caliente, sequia, descontento, rumor_traspaso, baja_estrella, presion_gm, caza_playoffs, crisis_economica) |
| `subject_player_id` | INTEGER | 0 = hilo de equipo |
| `title` / `summary` | TEXT | para la noticia y la línea temporal |
| `intensity` | INTEGER | escalón alcanzado |
| `started_game_day` / `last_update_game_day` / `last_news_game_day` | INTEGER | ciclo y cooldown |
| `resolved` | INTEGER | 0 abierto / 1 cerrado |
| `resolution` | TEXT | "" / broken / completed / abandoned (rollover) / departed (el protagonista salió del equipo) |
| `started_at` | TEXT | |

Los tipos de jugador (`jugador_caliente`, `sequia`) admiten **varios hilos a la vez, uno por
protagonista** (`subject_player_id`); el resto son de equipo (sujeto 0). Desde el pulido:
umbral de sequía **relativo** (`max(3, 45 %)` de la media, `ColdThreshold`), mínimo de minutos
y desempate por relevancia (OVR).

### playoff_series (`PlayoffSeriesData`)

Series de playoffs **ya resueltas** (rivalidades/dinastías). Se escribe al cerrarse la serie
en `PlayoffsGenerator.AdvancePlayoffSeries`, porque `games` se purga en el rollover: sin esta
tabla no habría histórico de enfrentamientos. CRUD en `DatabaseManager.Games.cs`.

| Campo | Tipo | Notas |
|---|---|---|
| `id` | INTEGER PK AI | |
| `manager_id` / `season_id` | INTEGER | |
| `label` | TEXT | `series_label` original (`playoff-r1-east-1v8`, `playoff-r4-finals`…). **Clave de idempotencia** junto a manager/temporada |
| `stage` | TEXT | `r1`/`r2`/`r3`/`r4` (Finales) — `PlayoffsGenerator.ParseSeriesLabel` |
| `conference` | TEXT | `East`/`West`/"" (Finales) |
| `high_team_id` / `low_team_id` | INTEGER | teamA (ventaja de cancha) / teamB |
| `high_wins` / `low_wins` | INTEGER | victorias de cada uno en la serie |
| `winner_id` / `loser_id` | INTEGER | |
| `game_day` | INTEGER | jornada del último partido de la serie |

> El **play-in no se registra** (son partidos sueltos, no series). Reglas puras de agregación:
> `RivalryHelper` (rivales, balance, intensidad, último verdugo) y `DynastyHelper` (prestigio y
> dinastías derivadas de `season_records`).

### player_milestones (`MilestoneData`)

Hitos de carrera alcanzados (E1). Una fila por hito y jugador; se registran una sola vez
(noticia única, fecha) y **sobreviven al retiro** del jugador.

| Campo | Tipo | Notas |
|---|---|---|
| `id` | INTEGER PK AI | |
| `player_id` | INTEGER | índice; clave estable del jugador |
| `milestone` | TEXT | clave del hito (`points_10000`, `allstar_10`, `mvp_3`…) según `MilestoneCatalog` |
| `season_id` / `game_day` | INTEGER | cuándo se alcanzó |
| `team_id` | INTEGER | equipo en ese momento |
| `created_at` | TEXT | |

`teams` añade `market_size` (0-100, tamaño de mercado: asistencia y valor de contratos; en partidas
antiguas se rellena por reputación).

### injury_history (`InjuryHistoryData`)

Parte médico (E2): una fila por lesión cerrada (sobrevive a traspasos y temporadas).

| Campo | Tipo | Notas |
|---|---|---|
| `id` | INTEGER PK AI | |
| `player_id` | INTEGER | índice |
| `team_id` | INTEGER | equipo en el momento de la lesión |
| `injury_type` | TEXT | diagnóstico (nombre del catálogo) |
| `severity` | TEXT | LEVE / MODERADA / GRAVE / CRÍTICA |
| `days` | INTEGER | días totales de baja del episodio |
| `season_id` / `game_day` / `date` | INTEGER/TEXT | cuándo se cerró |
| `outcome` | TEXT | `recuperado` o `alta_forzada` |
| `treated` | INTEGER | 1 si pasó por tratamiento |

`players` añade `injury_total_days` (duración total de la lesión en curso) y `recovery_risk`
(0-100, riesgo de recaída tras un alta forzada).

### messages (`MessageData`)
`id`, `manager_id`, `title`, `body`, `date_sent`, `is_read`, `game_day`, `message_type`, `related_id`, `sender_type` (0=system,1=player,2=news), `sender_id`, `game_date`, `created_at`.

### offers (`OfferData`)
`id`, `manager_id`, `player_id`, `offer_salary`, `offer_years`, `guaranteed_years`, `has_team_option` (0/1), `has_player_option` (0/1), `is_two_way` (0/1, nueva columna), `day_sent`, `offer_type` (0=renewal, 1=FA signing), `status` ("pending"/"accepted"/"rejected"), `processed` (0/1). Matured when `processed=0 && currentDay >= day_sent + 7`. When an option is set, `guaranteed_years = max(0, offer_years − 1)`. When `is_two_way=1`, the offer bypasses salary-cap legality checks at maturity and sets `player.is_two_way` on signing.

### trade_offers (`TradeOfferData`)
`id`, `manager_id`, `day_sent`, `team_id_from`, `team_id_to`, `player_ids_out`, `player_ids_in`, `pick_ids_out`, `pick_ids_in` (CSV text, migrated), `processed`, `status`, `trade_type`. Helpers: `GetWantedPlayerIds()`, `GetOfferedPlayerIds()`, `GetWantedPickIds()`, `GetOfferedPickIds()`.

### trades (`TradeData`)
`id`, `season_id`, `game_day`, `game_date`, `team_id_from`, `team_id_to`, `player_id`, `pick_id` (migrated, default 0), `trade_type` ("trade"|"free_agent"|"pick_trade"|"sign_and_trade"), `partner_player_id` (nullable).

Un **Sign-and-Trade de FA propio** genera dos filas: `free_agent` (firma con Bird rights, `team_id_from=0`) + `sign_and_trade` (traspaso inmediato). Sin migración de esquema: `MarketController.ProcessSATrade` y `DashboardController.ShowNextPendingTradeOffer` ejecutan el flujo.

### draft_picks (`DraftPickData`)
`id`, `season_id` (legado), **`draft_year`** (año del draft, clave temporal), `round`, `pick_number`, `original_team_id`, `current_team_id`, **`protected_from`** (0 = sin protección; n = protegido de los primeros n puestos, revierte al original dentro del rango), **`is_swap`** (0/1), **`swap_original_team_id`** (equipo con el que se compara; el tenedor del swap recibe la mejor posición). **Multi-año:** cada equipo posee los drafts de `year_end..year_end+3` (el actual + 3 futuros); al inicio son 8 picks (4 R1 + 4 R2). `pick_number` solo se asigna al draft actual (proyección por standings inversos); en años futuros vale 0 (la posición real se decide al draftear por el `original_team_id`). `SeedDraftPicksMultiYear` siembra el año actual + futuros; `RolloverDraftPicks` (en `StartNewSeason`) borra el año drafteado, proyecta el nuevo actual y añade el año más lejano, conservando los intermedios (traspasos incluidos).

### training (`TrainingData`)
`id`, `team_id`, `player_id`, `attribute`, `duration_days`, `days_remaining`, `completed`.

### team_lineup (`LineupData`)
`id`, `player_id`, `team_id`, `slot` (0=starter, 1=bench, 2=inactive), `slot_index` (migrated, default -1). Seeded by `AutoSeedLineup`. **`rotation_minutes`** (migración aditiva, default -1 = automático): minutos asignados en la pantalla ROTACIÓN para el siguiente partido (`0..48`). Solo se lee del equipo del usuario; la IA se rige por la heurística `GameSimulator.GetTargetMinutes`. Se rellena en `DashboardController.GetActivePlayers` y se escribe con `DatabaseManager.SetLineupMinutes`.

### employees (`EmployeeData`)
`id`, `team_id`, `position` (e.g. "PABELLON", coaches), `first_name`, `last_name`, `salary`, `reputation` (1–5, drives ticket/renovation modifiers), `contract_years`, `candidate_day`. Positions include psychologist (see `ProcessPsychologistMorale`).

### scouts (`ScoutData`)
`id`, `team_id`, `slot`, `name`, `salary`, `contract_years`.

### scouted_players (`ScoutedPlayerData`)
Conocimiento persistente de scouting (fog-of-war). `team_id`, `player_id`, `scouted_day` (día en que se completó el ojeo), PK compuesta `(team_id, player_id)`. Un jugador ojeado permanece con atributos visibles aunque se retire de la cartera. Se escribe al completarse un scout (`ProcessScouts`) y se rellena desde `scouts` donde `completed=1` en la migración.

### loans (`LoanData`)
`id`, `team_id`, `slot`, `name`, `amount`, `total_debt`, `remaining_months`, `interest_rate`, `monthly_payment`.

### sponsors (`SponsorData`)
`id`, `name`, `logo`, `initial_income`, `home_game_income`, `contract_years`, `is_active`, `team_id`. Seeded 20; 3 active random.

### tv_channels (`TvChannelData`)
`id`, `name`, `logo`, `initial_income`, `home_game_income`, `contract_years`, `is_active`, `team_id`, `broadcast_fee`, `viewership_multiplier`. Seeded 10; max 3 active.

### team_settings (`TeamSettingsData`)
`id`, `team_id`, `ticket_price`, `subscription_price`, `sponsor_id`, `sponsor_years_remaining`, `tv_channel_id`, `tv_years_remaining`, `avg_attendance`.

### player_personalities (`PlayerPersonalityData`)
`id`, `player_id`, `team_id`, `personality_type` ("Líder","Mentor","Estrella","Guerrero","Tranquilo","Intenso","Profesional","Novato"), `trait_1`, `trait_2`, `compatibility_modifier`.

### player_relationships (`PlayerRelationshipData`)
`id`, `team_id`, `player_a_id`, `player_b_id`, `bond` (1–99). Symmetric queries via `GetRelationship`.

### coach_rankings (`CoachRankingData`)
`id`, `name`, `team_id`, `status` ("historical"|"active"|"inactive"|"player"), `score`. Seeded ~70 historical + 30 active linked to 2025-26 teams. Updated via `UpdateCoachScore` (ignores historical), `SetCoachInactive`, `ReassignCoachToTeam`, `AddPlayerCoachEntry`.

### hof_players (`HallOfFameData`)
`id`, `player_id` (0 para leyendas precargadas), `first_name`, `last_name`, `position`, `team_abbreviation`, `rings`, `career_points`, `career_rebounds`, `career_assists`, `career_games`, `finals_mvps`, `induction_season`. Legado: `HallOfFameSeeder` (~100 leyendas). Inducción: `TryInductIntoHallOfFame` en `StartNewSeason`.

### retired_numbers (`RetiredNumberData`)
`id`, `team_id`, `number`, `player_id` (0 si el jugador ya no existe — siempre tras retiro), `first_name`, `last_name`, `position`, `rings`, `career_points`. Semilla: `RetiredNumberSeeder` (53) + `VeteranRetiredNumberSeeder` (17). `AssignJerseyNumber` reserva números retirados.

### gm_achievements (`GmAchievementData`)
`id`, `manager_id`, `type` (enum key string, p. ej. `first_win`, `champion`, `reg_wins_60`), `season_id` (nullable), `season_label` (nullable, p. ej. `"2025-26"`), `unlocked_at` ("yyyy-MM-dd HH:mm:ss").
Índice compuesto **`UNIQUE(manager_id, type)`** (`IX_Achievements_Manager_Type`, definido con `[Indexed(Name, Order, Unique)]`): un desbloqueo por logro y por manager. Escritura idempotente vía `INSERT OR IGNORE` (`DatabaseManager.UnlockAchievement`). *Histórico: el índice nació UNIQUE solo sobre `manager_id`, lo que impedía desbloquear más de un logro por partida; `DatabaseManager.CreateTables` ejecuta `DROP INDEX IF EXISTS` antes de `CreateTable` para regenerar el índice compuesto correcto.* Catálogo de 28 tipos en `AchievementCatalog.All`; la pantalla hace `BackfillCareer` silencioso al abrir para partidas ya avanzadas.

### schema_migrations (internal)
`name` (TEXT PK), `applied_at` (TEXT). Creada en `RunMigrations`; registra migraciones one-time por slot. `schema_migration_attempts` (`id`, `name`, `status` `ok`/`failed`, `error`, `attempted_at`) conserva el resultado de cada intento, incluidos los fallos que bloquean la apertura.

### db_meta (`DbMetaData`, internal)
`key` (TEXT PK), `value` (TEXT). Guarda `seed_fingerprint` en el template y se clona a cada slot para trazabilidad. La huella cubre las filas persistidas de `teams`, `players` y `league_settings` (orden estable por id, SHA-256); las ediciones válidas del Editor se conservan y no se comparan con el contenido canónico.

### Records & history tables
- `historical_records` (`HistoricalRecordData`): all-time single-game records (9 real: Wilt 100pts, 55 reb, Skiles 30 ast, …).
- `team_records` (`TeamRecordData`): per-team records seeded from `TeamRecordSeeder`.
- `historical_player_stats` (`HistoricalPlayerStatsData`): career totals for ~120 real legends plus every in-game player (totals + computed `ppg, rpg, apg, spg, bpg, fg_pct, fg3_pct, ft_pct`). Re-seeded if all `total_turnovers == 0`. **Clave `player_id`** (SEA-06): `>0` real player (stable id), `-1` seeder legend (untouchable), `0` legacy ownerless row adopted by unique name during the migration (`HistoricalStatsRules.BuildAdoptionPlan`; ambiguous homonyms are never adopted). Never aggregated by name.
- `season_game_records` (`SeasonGameRecordData`): season-level single-game records (`team_id, season_id, stat_type, player_name, value, game_date`).
- `player_season_stats` (`PlayerSeasonStatRow`): per-player per-season aggregates (created via `CreateTable` AND raw SQL in migrations). Archived in `StartNewSeason`. **F6-01:** incluye `total_fgm/fga/fg3m/fg3a/ftm/fta/turnovers/oreb/dreb`; la migración `player_season_stats_shooting_splits` añade las columnas a partidas antiguas y rellena los splits desde `player_game_stats` cuando la temporada aún existe. Las temporadas ya purgadas quedan a 0 (no reconstruibles).
- `finals_records` (`FinalsRecord`): real finals 1970-71→today (champ/finalist/keyword/result/mvp).
- `finals_player_stats` (`FinalsPlayerStatsData`): copy of game stats restricted to Finals games.
- `awards_records` (`AwardsRecord`): real MVP/ROY by season (keyword-based team matching).
- `quintet_records` (`QuintetRecord`): real All-NBA quintets by season.
- `all_star_records` (`AllStarRecord`): game results + MVP (in-game only).
- `all_star_appearance_seed` (`AllStarAppearanceSeed`): player_name → appearances (correlated by name to `players`).
- `monthly_awards` (`MonthlyAwardData`): `id, season_id, month_name, award_type ("manager"|"player"|"rookie"), rank, manager_id, player_id, team_id, team_name, player_name, value`.

### DTOs sin tabla (no SQLite / no usados)
- **`PreseasonGameData` ([Table("preseason_games")]) es código muerto [F]**: la clase existe en `Data/PreseasonGameData.cs` pero **ninguna parte crea la tabla ni la usa** (la pretemporada usa `games.game_type="preseason"`). Candidata a borrado.

## 4. Seed data

| Seed | Source | Volume | Notes |
|---|---|---|---|
| Teams | `SeedTeams` | 30 | 30 franquicias; metadatos de división, arena, capacidad, owner, atributos, presupuesto, reputación, instalaciones, logos/jerseys, salary_margin y objetivo |
| League settings | `SeedLeagueSettings` | 1 | TradeHelper constants + bi_annual 5.1M + taxpayer_mid_level |
| Players | `SeedPlayers` | 519 llamadas `Add` (519 asignados) | Plantillas de 16–18 por equipo; distribución manual en `DatabaseManager.Seeding.cs`. Rol por `PlayerRoleRules.FromOverall`: ≥90 Estrella, ≥83 Titular, ≥74 Banquillo; `overall` vía `PlayerRating` |
| Free agents | `SeedFreeAgents` | 120 llamadas `AddFA` | `team_id=0`; John Konchar (id 461) también entra por `AddFA`; roles de este helper: ≥80 Estrella, ≥70 Titular, ≥60 Banquillo |
| Sponsors | `SeedSponsors` | 20 | 3 active random |
| TV channels | `SeedTvChannels` | 10 | 3 active random; re-seed if all `initial_income==0` |
| Historical records | `SeedHistoricalRecords` | 9 | Wilt, Skiles, etc. |
| Team records | `SeedTeamRecords` | by team | from `TeamRecordSeeder.Data` |
| Historical player stats | `SeedHistoricalPlayerStats` | ~120 | from `HistoricalPlayerStatsSeeder.Data` |
| Finals/awards/quintets | `SeedPalmaresData` | real data | from `PalmaresSeeder` |
| All-Star | `SeedAllStarData` | ~90 seed + 20 records | correlated by name |
| Coaches | `SeedCoachRankings` | ~100 | historical + active |
| Hall of Fame | `HallOfFameSeeder` | ~100 leyendas | precargadas con `player_id=0` |
| Dorsales retirados | `RetiredNumberSeeder`/`VeteranRetiredNumberSeeder` | 53 + 17 | leyendas con `player_id=0` |
| Draft picks | `SeedDraftPicksMultiYear` | 60/año × 4 años (actual + 3) | año actual con posiciones (standings inversos) + futuros sin posición; `RolloverDraftPicks` los rota |
| Personalities/relationships | `SeedTeamPersonalities`/`SeedTeamRelationships`/`EnsureTeamRelationshipsSeeded` | per team | non-deterministic `System.Random` |

Los conteos del bloque `SeedPlayers`/`SeedFreeAgents` son el snapshot del checkout revisado (2026-10-24): 519 asignados + 120 agentes libres = 639 jugadores, no cupos permanentes. El seed es manual y los totales se deben recalcular cuando el usuario cambie esas listas.

**Validación previa a partida nueva (F1-06/F1-08):** `Scripts/Data/SeedAudit.cs` es el auditor puro compartido por EditMode y runtime y **el único gate bloqueante**. `EnsureTemplateDb` verifica `db_meta.seed_fingerprint` y audita teams/players/settings que realmente clona; si falta el sello, cambió o el contenido es inválido/corrupto, reconstruye el template en `.tmp`, conserva `template.db.bak` y publica con replace atómico tras validar (orden: sembrar → auditar → sellar → publicar, para que la huella no enmascare errores de datos). La G-League no se clona (se siembra por slot) y no bloquea el template. Un seed recién construido inválido y el slot completo al terminar el clon siguen lanzando ERROR y revirtiendo. Una huella distinta de `SeedBaseline.Fingerprint` **solo avisa y reconstruye** (el test de EditMode es quien exige actualizar la constante). Errores del template previo se registran en consola con el informe completo; el modal solo recibe un resumen compacto. Tamaño de roster y atributos legales <25 son informativos.

## 5. Migrations (`RunMigrations`, DatabaseManager.cs:286)

**Column-based (idempotente, aditiva):** `PRAGMA table_info(table)` → if column missing → `ALTER TABLE ... ADD COLUMN`.

| Table | Column(s) added | Default |
|---|---|---|
| `managers` | `fan_confidence` | 50 |
| `teams` | `objective` | — |
| `players` | `renewal_cooldown_day` | 0 |
| `managers` | `budget_red_warnings` | 0 |
| `players` | `morale` | 50 |
| `players` | `role` | 3 |
| `team_lineup` | `slot_index` | -1 |
| `team_lineup` | `rotation_minutes` | -1 |
| `players` | `photo` | '' |
| `players` | `secondary_position` | '' |
| `trade_offers` | `player_ids_out`, `player_ids_in` | '' |
| `trade_offers` | `pick_ids_out`, `pick_ids_in` | '' |
| `seasons` | `last_ai_trade_day` | -999 |
| `trades` | `pick_id` | 0 |
| `teams` | `first_apron_hard_capped` | 0 |
| `managers` | `career_reg_wins/losses`, `career_po_wins/losses`, `championships`, `seasons_completed` | 0 |
| `players` | `fisico` | 99 |
| `players` | `guaranteed_years`, `has_team_option`, `has_player_option` (last year is a TO/PO) | `guaranteed_years`=0, `has_*_option`=0 |
| `players` | `last_team_id` (último equipo para el que jugó; habilita Bird rights al re-firmar) | 0 |
| `players` | `rings` (campeonatos), `finals_mvps` (MVP de las Finales) | 0 |
| `players` | `finals_played` (finales disputadas) | 0 |
| `players` | `season_mvps` (MVP de temporada regular) | 0 |
| `players` | `number` (dorsal), `on_trade_block` | 0 |
| `league_settings` | `taxpayer_mid_level` | 0 |
| `league_settings` | `two_way_salary` (salario two-way) | `TradeHelper.TWO_WAY_SALARY` |
| `players` | `is_on_ir` (reserva de lesionados) | 0 |
| `players` | `is_two_way` (contrato two-way) | 0 |
| `players` | `g_league_assigned` (asignado a G-League) | 0 |
| `offers` | `is_two_way` (oferta two-way) | 0 |

**Data migrations (registradas en `schema_migrations`, viven con el slot):**
- `overall_recalc`: recompute `overall` for all players as mean of 11 attrs (cap potential).
- `overall_recalc_v2`: vuelve a aplicar `PlayerRating` a todas las partidas existentes para reparar valores calculados con la fórmula antigua; solo se marca aplicada al terminar el lote.
- `draft_picks_reset`: wipe `draft_picks` and reseed for the active season (using previous season's standings if available).
- `draft_picks_multiyear`: wipe `draft_picks` and reseed 4 years (current + 3) for the active season (`draft_year` column added via `ALTER TABLE`).

**Raw SQL migrations:** create `player_season_stats`, `monthly_awards` if missing (redundante con `CreateTable`); set `secondary_position` for existing players; `UPDATE` loops.

## 6. DTOs (non-table classes)

- **`DatabaseRows.cs`** (nuevo): `PlayerSeasonStatsRow`, `PlayerAwardQueryRow`, `HistoricalStatsAggregateRow`, `PlayerCareerSeasonRow`, `PlayerSeasonStatRow`, `SeasonAwardRow`, `PlayerAwardEntry`, `MonthlyManagerAwardRow`, `MonthlyPlayerAwardRow` (antes en la cola de `DatabaseManager.cs`).
- Non-SQLite DTOs: `FinalsMVPDetails`, `PlayerAwardInfo`, `GameSimulator.{PlayerStatSnapshot,TeamStats,GameResult,PlayByPlayEvent,StatDelta,PossessionOutcome}`, `TradeHelper.TradeResult`, `RosterController.MaxOfferBreakdown`, `DraftGenerator.DraftPickResult`, `GameResultCache` statics, `GmAchievementDefinition` (`AchievementCatalog`).
- **Muertos [F]:** `PreseasonGameData` (tabla nunca creada), `GameSaveManager` helpers obsoletos si los hubiera (ver `TODO_TECHNICAL_DEBT.md`).

## 7. Data lifecycle (who creates/modifies/consumes)

- **Seed/creates:** `DatabaseManager` (seeders), `SelectTeamController` (ManagerData/SeasonData on new game), `PreseasonController` (schedule), `DraftGenerator` (players/picks), `DashboardController` (games results, finances, attendance, awards, messages), `ArenaController` (renovations), `EndSeasonController` (draft), `StartNewSeason` (new season, aging, FA refills, HOF inductions, retired numbers).
- **Modifies:** all UI controllers through `DatabaseManager`; `GameSimulator` (stats, fatigue, injuries, records); `StartNewSeason` (aging/retirements/contracts/caps); `AchievementService` (gm_achievements).
- **Consumes:** every controller via `DatabaseManager.Instance.*`.

## 8. Open questions

- `league_settings.apron/repeater_apron` vs `TradeHelper.FIRST/SECOND_APRON`: which value is authoritative? Most UI uses `TradeHelper` [D]; the DB row is created but often ignored. Potential source of drift if constants change.
- Historical player `overall` column vs computed career averages — which is displayed in `HistorialController`/`StatsController`? ([D] seeded value.)
- `PreseasonGameData` — ¿código muerto a borrar o WIP de un sistema de pretemporada separado? (ver `TODO_TECHNICAL_DEBT.md`.)
