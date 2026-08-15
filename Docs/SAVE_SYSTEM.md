# SAVE_SYSTEM — Tactical Five

> Complete documentation of persistence: where data lives, how it is saved/loaded, versioning, and risks. **[F]** fact, **[D]** deduction.

## 1. Storage layout

All under `Application.persistentDataPath`:

```
{persistentDataPath}/TacticalFive/
  saves/
    save_1.db        ← save slot 1 (SQLite)
    save_2.db
    ...
    saves.json       ← metadata (SaveSlotInfo[])
  template.db        ← master template (seeded once, cloned into empty slots)
{persistentDataPath}/PlayerPhotos/{slotNumber}/{playerId}.png   ← rookie photos per slot
```

**Paths (source):** `GameSaveManager.cs` (`BaseDir`, `MetaPath`, `GetSaveDbPath`), `DatabaseManager.TemplateDbPath`, `PlayerPhotoHelper.BaseDir/SlotDir`.

## 2. Save model

- **SQLite per slot** — the save IS the database (45 tables). No JSON serialization of game state, no `JsonUtility` on game data (only used for `saves.json`).
- **`saves.json` metadata** (`SaveMeta { SaveSlotInfo[] slots }`): `slotNumber, exists, managerName, teamName, teamLogo, seasonYear, currentDate, lastPlayedRealDate, currentGameDay, gameMode`.
- **`template.db`** — created by `DatabaseManager.EnsureTemplateDb()` / `BuildTemplateDatabaseInBackground()` (Editor flow + boot check). Contains the **15 static tables cloned with data**: `teams, players, league_settings, sponsors, tv_channels, historical_records, team_records, historical_player_stats, finals_records, awards_records, quintet_records, all_star_records, all_star_appearance_seed, trades, hof_players` (plus 7 empty dynamic tables created: `trades`, `training`, `player_personalities`, `player_relationships`, `team_lineup`, `trade_offers`, `draft_picks`).
- **`CloneFromTemplate()`** (`DatabaseManager.cs:203`) — creates the 7 dynamic tables, then `InsertAll` the 15 static tables preserving IDs. If no template exists, `SeedStaticDataIfNeeded()` seeds the slot directly.
- **Template build moved off the main thread** (`BuildTemplateDatabaseInBackground`, guarded by `_templateLock`).

## 3. Save / load flows

### New game
`MainMenuController.OnManagerClicked` / `ConfirmProManager` (ProManager runs `OnProManagerClicked` → `OpenProModal` first, then `ConfirmProManager` on CONTINUAR):
1. `GameSaveManager.FindNextAvailableSlot()` → smallest free slot number.
2. `GameSaveManager.CleanupOrphanDb(slot)`.
3. `DatabaseManager.InitSaveSlot(slot)`:
   - close previous connection; set `_activeSaveSlot`;
   - `new SQLiteConnection(dbPath)` (ReadWrite|Create, ticks);
   - `CreateTables()` → `RunMigrations()` (+ `PRAGMA user_version`);
   - if slot empty: clone template (if exists) or seed; else do nothing.
4. `GoTo(SelectTeam, mode)`.
5. `SelectTeamController` creates `ManagerData` + `SeasonData`.
6. `PreseasonController.OnContinue` commits metadata: `GameSaveManager.SaveSlotInfo(...)` (exists=true, names, logo, seasonYear, lastPlayedRealDate, currentGameDay=0, gameMode).

### Continue / Load
`LoadGameController`:
1. `GameSaveManager.CleanupAllOrphanDbs()`.
2. `GetAllSlots()` → filter valid (`exists && managerName != null && teamName != null`).
3. `InitSaveSlot(slotNumber)` + `UpdateSlotFromDatabase(slotNumber)` (refresh metadata from DB).

### Save slot info refresh
- `UpdateSlotFromDatabase` reads the active manager/team/season and updates `SaveSlotInfo`.
- `SaveSlotInfo` is written whenever game state changes at key points: `PreseasonController.OnContinue` (first commit), `DashboardController.ProcessGameDayRoutine` (every day advance), `LoadGameController`. `GameSaveManager.DeleteSave` also writes.

### Delete
`GameSaveManager.DeleteSave(slot)` → deletes `save_{n}.db`, its `PlayerPhotos/{n}/` folder, and removes the slot from `saves.json`.

### Editor
`EditorController.LoadData` → `EnsureTemplateDb()` → `InitTemplateSession()` (opens `template.db` as active DB). `CloseTemplateSession()` restores the slot connection. Used to build/refresh the master template.

## 4. AutoSave & threading

- There is **no explicit autosave timer** [F]. Everything is persisted **immediately** at the moment of each mutation (`DatabaseManager.UpdatePlayer/UpdateGame/UpdateTeam/AddFinanceRecord/...` write synchronously). The only "save on exit" logic is metadata refresh; quitting mid-session without a mutation loses nothing because mutations are already committed.
- **Background work:** heavy operations run off the main thread via `RunInBackground`/`RunInBackgroundAsync` (SQLite WAL mode, AsyncLocal ambient `_ambientDb` connection, `[ThreadStatic]` RNG) — see `ARCHITECTURE.md §7`. Write-heavy batch operations (e.g. `StartNewSeason`, draft) run inside a **transaction**.

## 5. Versioning & migrations

- **`schema_migrations` table + `PRAGMA user_version`** [F]: `SCHEMA_VERSION = 2` (`DatabaseManager.cs:41,290-291`). The `schema_migrations` table (`name` PK, `applied_at`) stores one-time data migrations **inside the DB** (per-slot: deleting the slot resets state); `PRAGMA user_version` records the schema version.
- **Additive column migrations stay idempotent:** `PRAGMA table_info({table})` → missing column → `ALTER TABLE ... ADD COLUMN` (full list in `DATA_MODEL.md §5`).
- **One-time data migrations keyed by name in `schema_migrations`:** `overall_recalc`, `draft_picks_reset` (see `EVENTS.md §4`).
- **Backfill on column add:** some additive migrations also run an `UPDATE` right after `ADD COLUMN` (e.g. `players.last_team_id = team_id` for players on a roster).
- **Implication:** older saves open fine on newer code; newer saves with extra columns read fine by older code only if older code ignores extra columns (it does — sqlite-net maps known fields only).

## 6. Exactly what is stored (summary)

| Category | Tables |
|---|---|
| Meta | `saves.json` (slots) |
| Static content (template, 15) | teams, players, league_settings, sponsors, tv_channels, historical_records, team_records, historical_player_stats, finals_records, awards_records, quintet_records, all_star_records, all_star_appearance_seed, trades, hof_players |
| Progress | managers, seasons, games, game_attendance |
| Per-game | player_game_stats, finals_player_stats, season_game_records |
| Roster | team_lineup, training, offers, trade_offers, trades, draft_picks, player_personalities, player_relationships |
| Personnel/economy | employees, scouts, loans, finance_records, team_settings |
| Media/history | messages, monthly_awards, season_records, all_star_records, coach_rankings, player_season_stats |
| Legacy | gm_achievements, retired_numbers, schema_migrations, preseason_games |

## 7. Risks & known issues

- **[F] Migration flags now live in the DB** (`schema_migrations`), not machine-global PlayerPrefs — deleting a slot cleanly resets migration state. (The old `OverallMigration_{slot}`/`DraftPicksReset_{slot}` PlayerPrefs keys are gone.)
- **[D] Clone keeps stable IDs** (`players.id` 1..~600). `AllStarAppearanceSeed` correlates by `player_name` — fine within a slot.
- **[F] `SQLiteAsync.cs` was deleted** → async is handled internally by `RunInBackground`/`RunInBackgroundAsync` (WAL + ambient connection). Not all call sites have been migrated off the main thread yet (see `TODO_TECHNICAL_DEBT.md`).
- **[F] Transactions** now wrap `StartNewSeason` and the daily/day rollover batches; most single-mutation writes remain untransactional (a crash mid-flow can still leave partial state, e.g. a game played but no stats).
- **Photo persistence:** rookie photos written to `persistentDataPath/PlayerPhotos/{slot}/` (not in Resources) — deleted with the save; the `Resources/PlayerPhotos/` set (602 photos) is static game content.
- **`first_apron_hard_capped`, `pick_id`, `morale`, `fisico`, `role`, `photo`** are migrated columns — old saves get defaults, so behavior differences on legacy saves are expected.

## 8. Open questions

- Whether `template.db` is intended to ship with the game or be generated at first run on each machine ([D] generated; the Editor flow suggests dev-tool).
- `saves.json` writes are synchronous `JsonUtility.ToJson` — safe on main thread [F]; no conflict handling between concurrent editor sessions [H].