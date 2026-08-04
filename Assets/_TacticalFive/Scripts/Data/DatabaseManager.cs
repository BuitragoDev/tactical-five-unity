using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager : MonoBehaviour
{
    public static DatabaseManager Instance { get; private set; }

    private SQLiteConnection _db;
    public SQLiteConnection Db => _db;
    private int _activeSaveSlot = 0;
    private bool _isTemplateSession = false;

    public string TemplateDbPath =>
        Path.Combine(Application.persistentDataPath, "TacticalFive", "template.db");

    public int ActiveSaveSlot => _activeSaveSlot;

    private string DbPath => GameSaveManager.GetSaveDbPath(_activeSaveSlot);

    private const int SCHEMA_VERSION = 2;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // No inicializar automáticamente — el flujo de guardado decide cuándo y qué slot abrir
    }

    public void InitSaveSlot(int slotNumber)
    {
        if (_db != null)
        {
            try { _db.Close(); } catch { }
            _db = null;
        }

        _activeSaveSlot = slotNumber;

        // Crear directorio de guardados si no existe
        string dir = Path.GetDirectoryName(DbPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _db = new SQLiteConnection(DbPath);
        CreateTables();
        RunMigrations();

        bool hasExistingData = _db.Table<TeamData>().Count() > 0;

        if (File.Exists(TemplateDbPath) && !hasExistingData)
        {
            CloneFromTemplate();
            SeedStaticDataIfNeeded();
        }
        else if (!File.Exists(TemplateDbPath) && !hasExistingData)
        {
            SeedStaticDataIfNeeded();
        }

        Debug.Log($"[DB] Save slot {slotNumber} inicializado: {DbPath}");
    }

    public void EnsureTemplateDb()
    {
        if (File.Exists(TemplateDbPath)) return;

        var oldDb = _db;
        _db = new SQLiteConnection(TemplateDbPath);
        CreateTables();
        RunMigrations();
        SeedStaticDataIfNeeded();
        _db.Close();
        _db = oldDb;
        Debug.Log($"[DB] Template database created: {TemplateDbPath}");
    }

    private static readonly object _templateLock = new();

    public void BuildTemplateDatabaseInBackground(string dbPath)
    {
        lock (_templateLock)
        {
            var oldDb = _db;
            _db = new SQLiteConnection(dbPath);
            CreateTables();
            RunMigrations();
            SeedStaticDataIfNeeded();
            _db.Close();
            _db = oldDb;
            Debug.Log($"[DB] Template database created (background): {dbPath}");
        }
    }

    public void InitTemplateSession()
    {
        if (_db != null)
        {
            try { _db.Close(); } catch { }
            _db = null;
        }
        _db = new SQLiteConnection(TemplateDbPath);
        RunMigrations();
        _isTemplateSession = true;
        Debug.Log("[DB] Template session started");
    }

    public void CloseTemplateSession()
    {
        if (!_isTemplateSession) return;
        if (_db != null)
        {
            try { _db.Close(); } catch { }
            _db = null;
        }
        _isTemplateSession = false;
        Debug.Log("[DB] Template session closed");
    }

    void CloneFromTemplate()
    {
        var template = new SQLiteConnection(TemplateDbPath);
        template.CreateTable<TradeData>();
        template.CreateTable<TrainingData>();
        template.CreateTable<PlayerPersonalityData>();
        template.CreateTable<PlayerRelationshipData>();
        template.CreateTable<LineupData>();
        template.CreateTable<TradeOfferData>();
        template.CreateTable<DraftPickData>();
        _db.InsertAll(template.Table<TeamData>().ToList());
        _db.InsertAll(template.Table<PlayerData>().ToList());
        _db.InsertAll(template.Table<LeagueSettingsData>().ToList());
        _db.InsertAll(template.Table<SponsorData>().ToList());
        _db.InsertAll(template.Table<TvChannelData>().ToList());
        _db.InsertAll(template.Table<HistoricalRecordData>().ToList());
        _db.InsertAll(template.Table<TeamRecordData>().ToList());
        _db.InsertAll(template.Table<HistoricalPlayerStatsData>().ToList());
        _db.InsertAll(template.Table<FinalsRecord>().ToList());
        _db.InsertAll(template.Table<AwardsRecord>().ToList());
        _db.InsertAll(template.Table<QuintetRecord>().ToList());
        _db.InsertAll(template.Table<AllStarRecord>().ToList());
        _db.InsertAll(template.Table<AllStarAppearanceSeed>().ToList());
        _db.InsertAll(template.Table<TradeData>().ToList());
        template.Close();
        Debug.Log("[DB] Static data cloned from template");
    }

    void CreateTables()
    {
        _db.CreateTable<TeamData>();
        _db.CreateTable<LoanData>();
        _db.CreateTable<ManagerData>();
        _db.CreateTable<LeagueSettingsData>();
        _db.CreateTable<GameData>();
        _db.CreateTable<SeasonData>();
        _db.CreateTable<PlayerData>();
        try { _db.Execute("ALTER TABLE players ADD COLUMN college TEXT DEFAULT ''"); } catch { }
        _db.CreateTable<EmployeeData>();
        _db.CreateTable<ScoutData>();
        _db.CreateTable<PlayerGameStats>();
        _db.CreateTable<FinanceRecord>();
        _db.CreateTable<SeasonRecord>();
        _db.CreateTable<MessageData>();
        _db.CreateTable<SponsorData>();
        _db.CreateTable<TvChannelData>();
        _db.CreateTable<TeamSettingsData>();
        _db.CreateTable<HistoricalRecordData>();
        _db.CreateTable<TeamRecordData>();
        _db.CreateTable<SeasonGameRecordData>();
        _db.CreateTable<HistoricalPlayerStatsData>();
        _db.CreateTable<GameAttendanceData>();
        _db.CreateTable<FinalsPlayerStatsData>();
        _db.CreateTable<FinalsRecord>();
        _db.CreateTable<AwardsRecord>();
        _db.CreateTable<QuintetRecord>();
        _db.CreateTable<TradeData>();
        _db.CreateTable<TrainingData>();
        _db.CreateTable<PlayerPersonalityData>();
        _db.CreateTable<PlayerRelationshipData>();
        _db.CreateTable<LineupData>();
        _db.CreateTable<OfferData>();
        _db.CreateTable<TradeOfferData>();
        _db.CreateTable<DraftPickData>();
        _db.CreateTable<CoachRankingData>();
        _db.CreateTable<AllStarRecord>();
        _db.CreateTable<PlayerSeasonStatRow>();
        _db.CreateTable<AllStarAppearanceSeed>();
        _db.CreateTable<MonthlyAwardData>();
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_Games_Standings ON games(manager_id, game_type, is_played, game_day)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerGameStats_GameId ON player_game_stats(game_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerGameStats_PlayerId ON player_game_stats(player_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerGameStats_TeamId ON player_game_stats(team_id)");
    }

    void RunMigrations()
    {
        // Versionado de esquema + registro de migraciones one-time dentro del DB
        // (los flags viven con el slot; borrar el slot reinicia el estado)
        _db.Execute("CREATE TABLE IF NOT EXISTS schema_migrations (name TEXT PRIMARY KEY, applied_at TEXT NOT NULL)");
        _db.Execute("PRAGMA user_version = " + SCHEMA_VERSION);

        // Add fan_confidence to managers if missing
        var managerCols = _db.Query<ColumnInfo>("PRAGMA table_info(managers)");
        bool hasFanConfidence = managerCols.Any(c => c.name == "fan_confidence");
        if (!hasFanConfidence)
        {
            _db.Execute("ALTER TABLE managers ADD COLUMN fan_confidence INTEGER DEFAULT 50");
            Debug.Log("[DB] Migration: added fan_confidence to managers");
        }

        // Add objective to teams if missing
        var teamCols = _db.Query<ColumnInfo>("PRAGMA table_info(teams)");
        bool hasObjective = teamCols.Any(c => c.name == "objective");
        if (!hasObjective)
        {
            _db.Execute("ALTER TABLE teams ADD COLUMN objective TEXT");
            Debug.Log("[DB] Migration: added objective to teams");
        }

        // Add renewal_cooldown_day to players if missing
        var playerCols = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool hasRenewalCooldown = playerCols.Any(c => c.name == "renewal_cooldown_day");
        if (!hasRenewalCooldown)
        {
            _db.Execute("ALTER TABLE players ADD COLUMN renewal_cooldown_day INTEGER DEFAULT 0");
            Debug.Log("[DB] Migration: added renewal_cooldown_day to players");
        }

        // Add budget_red_warnings to managers if missing
        var managerCols2 = _db.Query<ColumnInfo>("PRAGMA table_info(managers)");
        bool hasBudgetWarnings = managerCols2.Any(c => c.name == "budget_red_warnings");
        if (!hasBudgetWarnings)
        {
            _db.Execute("ALTER TABLE managers ADD COLUMN budget_red_warnings INTEGER DEFAULT 0");
            Debug.Log("[DB] Migration: added budget_red_warnings to managers");
        }

        // Add morale to players if missing
        var playerCols2 = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool hasMorale = playerCols2.Any(c => c.name == "morale");
        if (!hasMorale)
        {
            _db.Execute("ALTER TABLE players ADD COLUMN morale INTEGER DEFAULT 50");
            Debug.Log("[DB] Migration: added morale to players");
        }

        // Add role to players if missing
        var playerCols5 = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool hasRole = playerCols5.Any(c => c.name == "role");
        if (!hasRole)
        {
            _db.Execute("ALTER TABLE players ADD COLUMN role INTEGER DEFAULT 3");
            Debug.Log("[DB] Migration: added role to players");
        }

        // Recalculate overall for all players from their 11 attributes (fix seed data mismatch)
        if (!IsMigrationApplied("overall_recalc"))
        {
            var allPlayers = _db.Table<PlayerData>().ToList();
            foreach (var p in allPlayers)
            {
                int sum = p.speed + p.shooting + p.three_point + p.passing + p.dribbling
                        + p.defense + p.rebounding + p.athleticism + p.iq + p.steals + p.blocks;
                int calc = (int)System.Math.Round(sum / 11f);
                if (calc > p.potential)
                    calc = p.potential;
                if (p.overall != calc)
                {
                    p.overall = calc;
                    _db.Update(p);
                }
            }
            MarkMigrationApplied("overall_recalc");
            Debug.Log("[DB] Migration: recalculated overall for all players");
        }

        // Add slot_index to team_lineup if missing
        var lineupCols = _db.Query<ColumnInfo>("PRAGMA table_info(team_lineup)");
        bool hasSlotIndex = lineupCols.Any(c => c.name == "slot_index");
        if (!hasSlotIndex)
        {
            _db.Execute("ALTER TABLE team_lineup ADD COLUMN slot_index INTEGER DEFAULT -1");
            Debug.Log("[DB] Migration: added slot_index to team_lineup");
        }

        // Add photo to players if missing
        var playerCols3 = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool hasPhoto = playerCols3.Any(c => c.name == "photo");
        if (!hasPhoto)
        {
            _db.Execute("ALTER TABLE players ADD COLUMN photo TEXT DEFAULT ''");
            Debug.Log("[DB] Migration: added photo to players");
        }

        // Add secondary_position to players if missing
        var playerCols4 = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        bool hasSecondaryPos = playerCols4.Any(c => c.name == "secondary_position");
        if (!hasSecondaryPos)
        {
            _db.Execute("ALTER TABLE players ADD COLUMN secondary_position TEXT DEFAULT ''");
            Debug.Log("[DB] Migration: added secondary_position to players");
        }

        // Migrate existing players: assign adjacent secondary position
        try
        {
            int migrated = _db.Execute(@"
                UPDATE players
                SET secondary_position = CASE position
                    WHEN 'PG' THEN 'SG'
                    WHEN 'SG' THEN CASE WHEN height_cm < 198 THEN 'PG' ELSE 'SF' END
                    WHEN 'SF' THEN 'PF'
                    WHEN 'PF' THEN 'C'
                    WHEN 'C'  THEN 'PF'
                    ELSE ''
                END
                WHERE secondary_position IS NULL OR secondary_position = ''");
            if (migrated > 0)
                Debug.Log($"[DB] Migration: assigned adjacent secondary_position to {migrated} players");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[DB] Migration: secondary_position data migration failed: {ex.Message}");
        }

        // Add guaranteed_years to players if missing
        var playerColsOpt = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
        if (!playerColsOpt.Any(c => c.name == "guaranteed_years"))
        {
            _db.Execute("ALTER TABLE players ADD COLUMN guaranteed_years INTEGER DEFAULT 0");
            _db.Execute("ALTER TABLE players ADD COLUMN has_team_option INTEGER DEFAULT 0");
            _db.Execute("ALTER TABLE players ADD COLUMN has_player_option INTEGER DEFAULT 0");
            _db.Execute("UPDATE players SET guaranteed_years = contract_years WHERE contract_years > 0");
            Debug.Log("[DB] Migration: added guaranteed_years/has_team_option/has_player_option to players");
        }

        // Add guaranteed_years to offers if missing
        var offersCols = _db.Query<ColumnInfo>("PRAGMA table_info(offers)");
        if (!offersCols.Any(c => c.name == "guaranteed_years"))
        {
            _db.Execute("ALTER TABLE offers ADD COLUMN guaranteed_years INTEGER DEFAULT 0");
            _db.Execute("ALTER TABLE offers ADD COLUMN has_team_option INTEGER DEFAULT 0");
            _db.Execute("ALTER TABLE offers ADD COLUMN has_player_option INTEGER DEFAULT 0");
            _db.Execute("UPDATE offers SET guaranteed_years = offer_years WHERE guaranteed_years = 0");
            Debug.Log("[DB] Migration: added guaranteed_years/has_team_option/has_player_option to offers");
        }

        // Migrate trade_offers to multi-player schema
        try
        {
            var toCols = _db.Query<ColumnInfo>("PRAGMA table_info(trade_offers)");
            if (toCols.Count > 0 && !toCols.Any(c => c.name == "player_ids_out"))
            {
                _db.Execute("ALTER TABLE trade_offers ADD COLUMN player_ids_out TEXT DEFAULT ''");
                _db.Execute("ALTER TABLE trade_offers ADD COLUMN player_ids_in TEXT DEFAULT ''");
                Debug.Log("[DB] Migration: added player_ids_out/player_ids_in to trade_offers");
            }
            if (toCols.Count > 0 && !toCols.Any(c => c.name == "pick_ids_out"))
            {
                _db.Execute("ALTER TABLE trade_offers ADD COLUMN pick_ids_out TEXT DEFAULT ''");
                _db.Execute("ALTER TABLE trade_offers ADD COLUMN pick_ids_in TEXT DEFAULT ''");
                Debug.Log("[DB] Migration: added pick_ids_out/pick_ids_in to trade_offers");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Migration error for trade_offers: {ex.Message}");
        }

        // Reset draft_picks: clear any legacy picks (seeded by overall at
        // season start) and re-seed picks for the current manager's active
        // season based on the previous season's standings (or overall+rep
        // fallback if there is no previous season).
        string picksKey = $"DraftPicksReset_{_activeSaveSlot}";
        if (!IsMigrationApplied("draft_picks_reset"))
        {
            try
            {
                int deleted = _db.Execute("DELETE FROM draft_picks");

                var mgr = GetActiveManager();
                if (mgr != null)
                {
                    var activeSeason = _db.Table<SeasonData>()
                        .Where(s => s.manager_id == mgr.id && s.is_active == 1)
                        .OrderByDescending(s => s.year_start)
                        .FirstOrDefault();
                    if (activeSeason != null)
                    {
                        int? prevSeasonId = _db.Table<SeasonData>()
                            .Where(s => s.manager_id == mgr.id && s.id != activeSeason.id)
                            .OrderByDescending(s => s.year_start)
                            .Select(s => (int?)s.id)
                            .FirstOrDefault();
                        SeedDraftPicks(activeSeason.id, mgr.id, prevSeasonId);
                    }
                }

                MarkMigrationApplied("draft_picks_reset");
                Debug.LogWarning($"[DB] Migration: cleared {deleted} legacy draft_picks and re-seeded for current manager.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DB] Migration error resetting draft_picks: {ex.Message}");
            }
        }

        // Add last_ai_trade_day to seasons
        try
        {
            var sCols = _db.Query<ColumnInfo>("PRAGMA table_info(seasons)");
            if (sCols.Count > 0 && !sCols.Any(c => c.name == "last_ai_trade_day"))
            {
                _db.Execute("ALTER TABLE seasons ADD COLUMN last_ai_trade_day INTEGER DEFAULT -999");
                Debug.Log("[DB] Migration: added last_ai_trade_day to seasons");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Migration error for seasons: {ex.Message}");
        }

        // Add pick_id to trades
        try
        {
            var tCols = _db.Query<ColumnInfo>("PRAGMA table_info(trades)");
            if (tCols.Count > 0 && !tCols.Any(c => c.name == "pick_id"))
            {
                _db.Execute("ALTER TABLE trades ADD COLUMN pick_id INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added pick_id to trades");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Migration error for trades.pick_id: {ex.Message}");
        }

        // Add first_apron_hard_capped to teams
        try
        {
            var tCols = _db.Query<ColumnInfo>("PRAGMA table_info(teams)");
            if (tCols.Count > 0 && !tCols.Any(c => c.name == "first_apron_hard_capped"))
            {
                _db.Execute("ALTER TABLE teams ADD COLUMN first_apron_hard_capped INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added first_apron_hard_capped to teams");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Migration error for first_apron_hard_capped: {ex.Message}");
        }

        // Add career stat columns to managers
        try
        {
            var mgrCols = _db.Query<ColumnInfo>("PRAGMA table_info(managers)");
            if (mgrCols.Count > 0)
            {
                if (!mgrCols.Any(c => c.name == "career_reg_wins"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN career_reg_wins INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added career_reg_wins to managers");
                }
                if (!mgrCols.Any(c => c.name == "career_reg_losses"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN career_reg_losses INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added career_reg_losses to managers");
                }
                if (!mgrCols.Any(c => c.name == "career_po_wins"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN career_po_wins INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added career_po_wins to managers");
                }
                if (!mgrCols.Any(c => c.name == "career_po_losses"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN career_po_losses INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added career_po_losses to managers");
                }
                if (!mgrCols.Any(c => c.name == "championships"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN championships INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added championships to managers");
                }
                if (!mgrCols.Any(c => c.name == "seasons_completed"))
                {
                    _db.Execute("ALTER TABLE managers ADD COLUMN seasons_completed INTEGER DEFAULT 0");
                    Debug.Log("[DB] Migration: added seasons_completed to managers");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Migration error for manager career columns: {ex.Message}");
        }

        // Add fisico to players if missing
        try
        {
            var playerColsFisico = _db.Query<ColumnInfo>("PRAGMA table_info(players)");
            if (!playerColsFisico.Any(c => c.name == "fisico"))
            {
                _db.Execute("ALTER TABLE players ADD COLUMN fisico INTEGER DEFAULT 99");
                Debug.Log("[DB] Migration: added fisico to players");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Migration error for fisico: {ex.Message}");
        }

        // Create player_season_stats table if missing (for career history across seasons)
        try
        {
            _db.Execute("CREATE TABLE IF NOT EXISTS player_season_stats (" +
                "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "player_id INTEGER, " +
                "season_id INTEGER, " +
                "year_start INTEGER, " +
                "year_end INTEGER, " +
                "team_id INTEGER, " +
                "team_abbreviation TEXT, " +
                "team_name TEXT, " +
                "games INTEGER, " +
                "total_minutes REAL, " +
                "total_points INTEGER, " +
                "total_rebounds INTEGER, " +
                "total_assists INTEGER, " +
                "total_steals INTEGER, " +
                "total_blocks INTEGER, " +
                "total_rating INTEGER" +
            ")");
            _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerSeasonStats_PlayerId ON player_season_stats(player_id)");
            _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerSeasonStats_SeasonId ON player_season_stats(season_id)");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Migration error for player_season_stats: {ex.Message}");
        }

        // Create monthly_awards table if missing (for monthly awards)
        try
        {
            _db.Execute("CREATE TABLE IF NOT EXISTS monthly_awards (" +
                "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "season_id INTEGER, " +
                "month_name TEXT, " +
                "award_type TEXT, " +
                "rank INTEGER, " +
                "manager_id INTEGER, " +
                "player_id INTEGER, " +
                "team_id INTEGER, " +
                "team_name TEXT, " +
                "player_name TEXT, " +
                "value REAL" +
            ")");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Migration error for monthly_awards: {ex.Message}");
        }

        // Add taxpayer_mid_level to league_settings if missing
        try
        {
            var lsCols = _db.Query<ColumnInfo>("PRAGMA table_info(league_settings)");
            if (!lsCols.Any(c => c.name == "taxpayer_mid_level"))
            {
                _db.Execute("ALTER TABLE league_settings ADD COLUMN taxpayer_mid_level INTEGER DEFAULT 0");
                Debug.Log("[DB] Migration: added taxpayer_mid_level to league_settings");
            }
            _db.Execute($"UPDATE league_settings SET taxpayer_mid_level = {TradeHelper.T_MLE} WHERE taxpayer_mid_level = 0 OR taxpayer_mid_level IS NULL");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Migration error for taxpayer_mid_level: {ex.Message}");
        }
    }

    bool IsMigrationApplied(string name)
    {
        try
        {
            return _db.ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations WHERE name = ?", name) > 0;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] IsMigrationApplied({name}) error: {ex.Message}");
            return false;
        }
    }

    void MarkMigrationApplied(string name)
    {
        try
        {
            _db.Execute("INSERT OR IGNORE INTO schema_migrations (name, applied_at) VALUES (?, ?)",
                name, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] MarkMigrationApplied({name}) error: {ex.Message}");
        }
    }

    class ColumnInfo
    {
        public int cid { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public int notnull { get; set; }
        public string dflt_value { get; set; }
        public int pk { get; set; }
    }

    void SeedStaticDataIfNeeded()
    {
        if (_db.Table<TeamData>().Count() == 0)
            SeedTeams();

        if (_db.Table<LeagueSettingsData>().Count() == 0)
            SeedLeagueSettings();

        if (_db.Table<PlayerData>().Count() == 0)
        {
            SeedPlayers();
            SeedFreeAgents();
        }

        if (_db.Table<SponsorData>().Count() == 0)
            SeedSponsors();

        if (_db.Table<TvChannelData>().Count() == 0)
            SeedTvChannels();
        else
        {
            // Detect old TV data (all initial_income == 0) and re-seed
            var tvChannels = _db.Table<TvChannelData>().ToList();
            if (tvChannels.Count > 0 && tvChannels.All(c => c.initial_income == 0))
            {
                _db.DeleteAll<TvChannelData>();
                SeedTvChannels();
            }
        }

        if (_db.Table<HistoricalRecordData>().Count() == 0)
            SeedHistoricalRecords();

        if (_db.Table<TeamRecordData>().Count() == 0)
            SeedTeamRecords();

        if (_db.Table<HistoricalPlayerStatsData>().Count() == 0)
            SeedHistoricalPlayerStats();
        else
        {
            // Detect old historical stats (all total_turnovers == 0) and re-seed
            var histStats = _db.Table<HistoricalPlayerStatsData>().ToList();
            if (histStats.Count > 0 && histStats.All(s => s.total_turnovers == 0))
            {
                _db.DeleteAll<HistoricalPlayerStatsData>();
                SeedHistoricalPlayerStats();
            }
        }

        if (_db.Table<FinalsRecord>().Count() == 0)
            SeedPalmaresData();

        if (_db.Table<AllStarRecord>().Count() == 0)
            SeedAllStarData();

        if (_db.Table<CoachRankingData>().Count() == 0)
            SeedCoachRankings();
    }

    public bool EnsureDb()
    {
        if (_db == null)
        {
            Debug.LogError("[DB] No hay base de datos activa. Llama InitSaveSlot() primero.");
            return false;
        }
        return true;
    }


    void OnDestroy()
    {
        _db?.Close();
    }
}
