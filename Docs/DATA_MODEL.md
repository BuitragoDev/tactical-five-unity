# DATA_MODEL — Tactical Five

> Complete map of the SQLite schema, the seed data, migrations, and the DTOs used across the game. **[F]** fact, **[D]** deduction.
> All tables live in one SQLite file per save slot (`save_{N}.db`) and in `template.db`. Managed by `DatabaseManager.CreateTables()` / `RunMigrations()` / `SeedStaticDataIfNeeded()`.

## 1. Conventions

- `sqlite-net` attributes: `[PrimaryKey]`, `[AutoIncrement]`, `[Indexed]`, `[Table("name")]`, `[Ignore]`.
- **No `FOREIGN KEY` constraints anywhere** [F]. Relations are by convention via `*_id` columns. Referential integrity is the app's responsibility.
- Dates: most are `TEXT` (`"yyyy-MM-dd"`, `"yyyy-MM-dd HH:mm:ss"`), manually formatted. `storeDateTimeAsTicks = true` is set but unused for game data.
- `PlayerData.id` is **manual, not autoincrement** (seed IDs 1..~600 stable across cloned slots). Other tables autoincrement.
- **Schema versioning [F]:** `schema_migrations` table (`name` PK, `applied_at`) + `PRAGMA user_version = 2` (`SCHEMA_VERSION = 2`, `DatabaseManager.cs:41,290-291`). Column-based additive migrations still use `PRAGMA table_info`; data migrations use `IsMigrationApplied`/`MarkMigrationApplied`.

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
`id` (PK, manual), `name`, `abbreviation`, `city`, `conference` (East/West), `division`, `arena`, `capacity`, `owner`, `attack`, `defense`, `overall`, `budget`, `reputation` (1–5), `facilities` (1–5), `logo`, `jersey_home`, `jersey_away`, `salary_margin`, `objective` (TEXT: "Campeonato"|"Playoffs"|"Play-In"|"Zona tranquila"|…, migrated), `team_chemistry`, `first_apron_hard_capped` (int 0/1, migrated), `arena_renovation_type/count/cost/end_day` (set via ArenaController), `arena_renovation_end_day`.

### players (`PlayerData`)
`id` (PK manual), `team_id` (0 = FA), `first_name`, `last_name`, `position` (PG/SG/SF/PF/C), `secondary_position` (migrated), `age`, `nationality` (ISO3), `college` (migrated), `height_cm`, `weight_kg`, `overall`, `potential`, and the 11 attributes: `speed, shooting, three_point, passing, dribbling, defense, rebounding, athleticism, iq, steals, blocks`. Then `salary`, `contract_years`, `is_rookie` (0/1), `injury_days`, `injury_type`, `treated`, `renewal_cooldown_day` (migrated), `seasons_with_team`, `morale` (migrated, default 50), `fisico` (migrated, default 99), `role` (migrated, `PlayerRole` int), `photo` (migrated), `number` (dorsal), `on_trade_block` (0/1, marcado desde Roster → jugador TRANSFERIBLE en Market). Contract options (migrated): `guaranteed_years` (0 if the last year is an option), `has_team_option` (0/1), `has_player_option` (0/1 — mutually exclusive with team option), `last_team_id` (migrated; último equipo para el que jugó, 0 si nunca/FA externo — habilita Bird rights al re-firmar). `contract_years` includes both guaranteed years and the option year.
- **Honores de carrera (migrados, default 0):** `rings` (campeonatos ganados), `finals_mvps` (MVP de las Finales), `finals_played` (finales disputadas), `season_mvps` (MVP de temporada regular). Se incrementan en `SaveSeasonEndRecords` (ver GAMEPLAY §11). Se muestran como contadores en el header de `PlayerProfile` y `Trajectory` (CAMPEONATOS / FINALES / MVP / MVP FINALS).
- `GetCalculatedAverage()` returns `round(mean(11 attrs))`. **`overall` is always recomputed from the attributes and capped by `potential`** (seed, training, progression, migration) [F].

### managers (`ManagerData`)
`id` (AI), `name`, `team_id`, `game_mode`, `created_at`, `lastPlayedRealDate`, `currentGameDay`, `currentDate`, `fan_confidence` (migrated, default 50), `budget_red_warnings` (migrated, default 0), `career_reg_wins/losses`, `career_po_wins/losses`, `championships`, `seasons_completed` (migrated).

### seasons (`SeasonData`)
`id`, `year_start`, `year_end`, `is_active` (0/1), `current_game_day`, `game_mode`, `phase` (`preseason`|`regular`|`playin`|`playoff`|`finished`), `manager_id`, `generated` (0/1), `current_date` ("yyyy-MM-dd"), `last_ai_trade_day` (migrated, default -999).

### games (`GameData`)
`id`, `season_id`, `manager_id`, `game_day`, `game_date`, `home_team_id`, `away_team_id`, `home_score`, `away_score`, `is_played` (0/1), `game_type` (`preseason`|`regular`|`playin`|`playoff`|`allstar`), `series_label` (playoff series id). Indexes: `season_id, game_day, is_played, game_type, manager_id`. Plus manual index `IX_Games_Standings(manager_id, game_type, is_played, game_day)`. For All-Star: `home_team_id=-1` (East), `away_team_id=-2` (West).

### player_game_stats (`PlayerGameStats`)
`id`, `game_id`, `player_id`, `team_id`, `minutes`, `points`, `fgm, fga, fg3m, fg3a, ftm, fta`, `oreb, dreb, rebounds`, `assists`, `steals`, `blocks`, `turnovers`, `pf`, `rating`, `double_double`, `triple_double`. Indexes on game/player/team (`IX_PlayerGameStats_GameId/PlayerId/TeamId`).

### game_attendance (`GameAttendanceData`)
`game_id` (PK, no autoinc), `attendance`, `ticket_price`, `revenue`. Saved by `ProcessGameFinances`; read by results/venue UI.

### league_settings (`LeagueSettingsData`, `LeagueSettings.cs`)
`id`, `salary_cap`, `luxury_tax`, `apron`, `repeater_apron`, `mid_level`, `taxpayer_mid_level` (nueva columna), `bi_annual`, `minimum_salary`, `is_active`. Seeded from `TradeHelper` constants + `bi_annual = 5_100_000`. **`apron`/`repeater_apron` are not the same as `TradeHelper.FIRST_APRON/SECOND_APRON` — most UI code uses `TradeHelper` constants, not this table** [D].

### season_records (`SeasonRecord`)
`id`, `season_id`, `champion_id`, `finalist_id`, `finals_result`, `east/west/div1..6_champion_id`, `finals_mvp_id`, `finals_mvp_rating`, `season_mvp_id/rating/games`, `rookie_of_year_id/rating/games`, `best_defender_id`, `sixth_man_id`, `most_improved_id`, `all_star_pg/sg/sf/pf/c_id`, `first_team_pg..c`, `second_team_pg..c`.

### finance_records (`FinanceRecord`)
`id`, `team_id`, `season_id`, `record_type`, `game_day`, `amount`, `created_at`. Types: `1=Taquilla, 2=Abonos, 3=Patrocinios, 4=Televisión, 5=Remodelación, 6=Despido, 7=Sueldos jugadores, 8=Sueldos empleados, 9=Préstamo, 10=Luxury tax, 11=Buyout`. (`GetTotalIncome` = types ≤4; `GetTotalExpenses` = types ≥5.)

### messages (`MessageData`)
`id`, `manager_id`, `title`, `body`, `date_sent`, `is_read`, `game_day`, `message_type`, `related_id`, `sender_type` (0=system,1=player,2=news), `sender_id`, `game_date`, `created_at`.

### offers (`OfferData`)
`id`, `manager_id`, `player_id`, `offer_salary`, `offer_years`, `guaranteed_years`, `has_team_option` (0/1), `has_player_option` (0/1), `day_sent`, `offer_type` (0=renewal, 1=FA signing), `status` ("pending"/"accepted"/"rejected"), `processed` (0/1). Matured when `processed=0 && currentDay >= day_sent + 7`. When an option is set, `guaranteed_years = max(0, offer_years − 1)`.

### trade_offers (`TradeOfferData`)
`id`, `manager_id`, `day_sent`, `team_id_from`, `team_id_to`, `player_ids_out`, `player_ids_in`, `pick_ids_out`, `pick_ids_in` (CSV text, migrated), `processed`, `status`, `trade_type`. Helpers: `GetWantedPlayerIds()`, `GetOfferedPlayerIds()`, `GetWantedPickIds()`, `GetOfferedPickIds()`.

### trades (`TradeData`)
`id`, `season_id`, `game_day`, `game_date`, `team_id_from`, `team_id_to`, `player_id`, `pick_id` (migrated, default 0), `trade_type` ("trade"|"free_agent"|"pick_trade"|"sign_and_trade"), `partner_player_id` (nullable).

Un **Sign-and-Trade de FA propio** genera dos filas: `free_agent` (firma con Bird rights, `team_id_from=0`) + `sign_and_trade` (traspaso inmediato). Sin migración de esquema: `MarketController.ProcessSATrade` y `DashboardController.ShowNextPendingTradeOffer` ejecutan el flujo.

### draft_picks (`DraftPickData`)
`id`, `season_id`, `round`, `pick_number`, `original_team_id`, `current_team_id`, **`protected_from`** (0 = sin protección; n = protegido de los primeros n puestos, revierte al original dentro del rango), **`is_swap`** (0/1), **`swap_original_team_id`** (equipo con el que se compara; el tenedor del swap recibe la mejor posición). Seeded 2×30 per season ordered by reverse standings (or overall for year 1).

### training (`TrainingData`)
`id`, `team_id`, `player_id`, `attribute`, `duration_days`, `days_remaining`, `completed`.

### team_lineup (`LineupData`)
`id`, `player_id`, `team_id`, `slot` (0=starter, 1=bench, 2=inactive), `slot_index` (migrated, default -1). Seeded by `AutoSeedLineup`.

### employees (`EmployeeData`)
`id`, `team_id`, `position` (e.g. "PABELLON", coaches), `first_name`, `last_name`, `salary`, `reputation` (1–5, drives ticket/renovation modifiers), `contract_years`, `candidate_day`. Positions include psychologist (see `ProcessPsychologistMorale`).

### scouts (`ScoutData`)
`id`, `team_id`, `slot`, `name`, `salary`, `contract_years`.

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
`name` (TEXT PK), `applied_at` (TEXT). Creada en `RunMigrations`; registra migraciones one-time por slot.

### Records & history tables
- `historical_records` (`HistoricalRecordData`): all-time single-game records (9 real: Wilt 100pts, 55 reb, Skiles 30 ast, …).
- `team_records` (`TeamRecordData`): per-team records seeded from `TeamRecordSeeder`.
- `historical_player_stats` (`HistoricalPlayerStatsData`): career totals for ~120 real legends (totals + computed `ppg, rpg, apg, spg, bpg, fg_pct, fg3_pct, ft_pct`). Re-seeded if all `total_turnovers == 0`.
- `season_game_records` (`SeasonGameRecordData`): season-level single-game records (`team_id, season_id, stat_type, player_name, value, game_date`).
- `player_season_stats` (`PlayerSeasonStatRow`): per-player per-season aggregates (created via `CreateTable` AND raw SQL in migrations). Archived in `StartNewSeason`.
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
| Teams | `SeedTeams` | 30 | Real NBA 2025-26 (division, arena, capacity, owner, attrs, budget, reputation, facilities, logo/jerseys, salary_margin, objective) |
| League settings | `SeedLeagueSettings` | 1 | TradeHelper constants + bi_annual 5.1M + taxpayer_mid_level |
| Players | `SeedPlayers` | ~490 | 15-16 per team; role thresholds: ≥88 Estrella, ≥78 Titular, ≥68 Banquillo; `overall` recomputed as mean |
| Free agents | `SeedFreeAgents` | ~142 | `team_id=0`; role thresholds lower (≥80/≥70/≥60) |
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
| Draft picks | `SeedDraftPicks` | 60/season | reverse standings order + protección/swap |
| Personalities/relationships | `SeedTeamPersonalities`/`SeedTeamRelationships`/`EnsureTeamRelationshipsSeeded` | per team | non-deterministic `System.Random` |

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

**Data migrations (registradas en `schema_migrations`, viven con el slot):**
- `overall_recalc`: recompute `overall` for all players as mean of 11 attrs (cap potential).
- `draft_picks_reset`: wipe `draft_picks` and reseed for the active season (using previous season's standings if available).

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