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

- **SQLite per slot** — the save IS the database (all ~40 tables). No JSON serialization of game state, no `JsonUtility` on game data (only used for `saves.json`).
- **`saves.json` metadata** (`SaveMeta { SaveSlotInfo[] slots }`): `slotNumber, exists, managerName, teamName, teamLogo, seasonYear, currentDate, lastPlayedRealDate, currentGameDay, gameMode`.
- **`template.db`** — created by `DatabaseManager.EnsureTemplateDb()` (called from `EditorController.LoadData`). Contains the 13 static tables (`teams, players, league_settings, sponsors, tv_channels, historical_records, team_records, historical_player_stats, finals_records, awards_records, quintet_records, all_star_records, all_star_appearance_seed, trade_data`).
- **`CloneFromTemplate()`** — copies the 13 static tables via `InsertAll` into a fresh slot (preserving IDs), then creates the dynamic tables (`trade_data, training, personalities, relationships, lineup, trade_offers, draft_picks`). If no template exists, `SeedStaticDataIfNeeded()` seeds the slot directly.

## 3. Save / load flows

### New game
`MainMenuController.OnManagerClicked` / `ConfirmProManager` (ProManager runs `OnProManagerClicked` → `OpenProModal` first, then `ConfirmProManager` on CONTINUAR):
1. `GameSaveManager.FindNextAvailableSlot()` → smallest free slot number.
2. `GameSaveManager.CleanupOrphanDb(slot)`.
3. `DatabaseManager.InitSaveSlot(slot)`:
   - close previous connection; set `_activeSaveSlot`;
   - `new SQLiteConnection(dbPath)` (ReadWrite|Create, ticks);
   - `CreateTables()` → `RunMigrations()`;
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
- `SaveSlotInfo` is written whenever game state changes at key points: `PreseasonController.OnContinue` (line 429, first commit), `DashboardController.ProcessGameDayRoutine` (line 1023, every day advance), `LoadGameController` (line 193). `GameSaveManager.DeleteSave` also writes (line 192).

### Delete
`GameSaveManager.DeleteSave(slot)` → deletes `save_{n}.db`, its `PlayerPhotos/{n}/` folder, and removes the slot from `saves.json`.

### Editor
`EditorController.LoadData` → `EnsureTemplateDb()` → `InitTemplateSession()` (opens `template.db` as active DB). `CloseTemplateSession()` restores the slot connection. Used to build/refresh the master template.

## 4. AutoSave

There is **no explicit autosave timer** [F]. Everything is persisted **immediately** at the moment of each mutation (`DatabaseManager.UpdatePlayer/UpdateGame/UpdateTeam/AddFinanceRecord/...` write synchronously). The only "save on exit" logic is metadata refresh; quitting mid-session without a mutation loses nothing because mutations are already committed.

## 5. Versioning & migrations

- **No schema version table, no `PRAGMA user_version`** [F]. Versioning is **additive and idempotent**:
  - Column presence check: `PRAGMA table_info({table})` → missing column → `ALTER TABLE ... ADD COLUMN` (full list in `DATA_MODEL.md §5`).
  - One-time data migrations keyed in `PlayerPrefs`: `OverallMigration_{slot}` (recompute overall = mean of 11 attrs, cap by potential) and `DraftPicksReset_{slot}` (wipe & reseed draft picks for the active season).
  - **Backfill on column add:** some additive migrations also run an `UPDATE` right after `ADD COLUMN`. E.g. adding `players.last_team_id` backfills `last_team_id = team_id` for players currently on a roster (`team_id != 0`).
- **Implication:** older saves open fine on newer code; newer saves with extra columns read fine by older code only if older code ignores extra columns (it does — sqlite-net maps known fields only).

## 6. Exactly what is stored (summary)

| Category | Tables |
|---|---|
| Meta | `saves.json` (slots) |
| Static content | teams, players, league_settings, sponsors, tv_channels, historical_*, records, palmarés seeds |
| Progress | managers, seasons, games, game_attendance |
| Per-game | player_game_stats, finals_player_stats, season_game_records |
| Roster | team_lineup, training, offers, trade_offers, trades, draft_picks, player_personalities, player_relationships |
| Personnel/economy | employees, scouts, loans, finance_records, team_settings |
| Media/history | messages, monthly_awards, season_records, all_star_records, coach_ranking, player_season_stats |

## 7. Risks & known issues

- **[F] `PlayerPrefs` migration flags are machine-global, not per-save-slots beyond the number**: `OverallMigration_{slot}` — deleting slot N and creating a new one skips re-running the overall migration (harmless because new data is correct). Multi-slot is fine.
- **[D] Clone keeps stable IDs** (`players.id` 1..~600). `AllStarAppearanceSeed` correlates by `player_name` — fine within a slot.
- **[F] `SQLiteAsync.cs` is unused** → saves are synchronous on the main thread; large `StartNewSeason`/draft ops can stutter.
- **[F] No transactions around most multi-write operations** (only schedule/seed/playoff saves use transactions). A crash mid-flow can leave partial state (e.g., a game played but no stats).
- **Photo persistence:** rookie photos written to `persistentDataPath/PlayerPhotos/{slot}/` (not in Resources) — deleted with the save; the `Resources/PlayerPhotos/` set (602 photos) is static game content.
- **`first_apron_hard_capped`, `pick_id`, `morale`, `fisico`, `role`, `photo`** are migrated columns — old saves get defaults, so behavior differences on legacy saves are expected.

## 8. Open questions

- Whether `template.db` is intended to ship with the game or be generated at first run on each machine ([D] generated; the Editor flow suggests dev-tool).
- `saves.json` writes are synchronous `JsonUtility.ToJson` — safe on main thread [F]; no conflict handling between concurrent editor sessions [H].
