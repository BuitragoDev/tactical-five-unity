using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;

public class DatabaseManager : MonoBehaviour
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
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_Games_Standings ON games(manager_id, game_type, is_played, game_day)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerGameStats_GameId ON player_game_stats(game_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerGameStats_PlayerId ON player_game_stats(player_id)");
        _db.Execute("CREATE INDEX IF NOT EXISTS IX_PlayerGameStats_TeamId ON player_game_stats(team_id)");
    }

    void RunMigrations()
    {
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

        // Recalculate overall for all players from their 11 attributes (fix seed data mismatch)
        string migrationKey = $"OverallMigration_{_activeSaveSlot}";
        if (PlayerPrefs.GetInt(migrationKey, 0) == 0)
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
            PlayerPrefs.SetInt(migrationKey, 1);
            PlayerPrefs.Save();
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
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Migration error for trade_offers: {ex.Message}");
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
    }

    class ColumnInfo
    {
        public int cid { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public int notnull { get; set; }
        public object dflt_value { get; set; }
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
    }

    bool EnsureDb()
    {
        if (_db == null)
        {
            Debug.LogError("[DB] No hay base de datos activa. Llama InitSaveSlot() primero.");
            return false;
        }
        return true;
    }

    // ── EQUIPOS ────────────────────────────────────────

    public List<TeamData> GetAllTeams()
    {
        if (!EnsureDb()) return new List<TeamData>();
        return _db.Table<TeamData>().ToList();
    }

    public List<TeamData> GetTeamsByConference(string conference)
    {
        if (!EnsureDb()) return new List<TeamData>();
        return _db.Table<TeamData>()
                  .Where(t => t.conference == conference)
                  .ToList();
    }

    public TeamData GetTeamById(int id)
    {
        if (!EnsureDb()) return null;
        return _db.Table<TeamData>()
                  .Where(t => t.id == id)
                  .FirstOrDefault();
    }

    public PlayerData GetPlayerById(int id)
    {
        if (!EnsureDb()) return null;
        return _db.Table<PlayerData>()
                  .Where(p => p.id == id)
                  .FirstOrDefault();
    }

    public TeamSettingsData GetTeamSettings(int teamId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<TeamSettingsData>()
                  .Where(t => t.team_id == teamId)
                  .FirstOrDefault();
    }

    public void UpdateTeamSettings(TeamSettingsData settings)
    {
        if (!EnsureDb()) return;
        _db.Update(settings);
    }

    public void SaveTeamSettings(TeamSettingsData settings)
    {
        if (!EnsureDb()) return;
        if (settings.team_id <= 0) return;
        _db.InsertOrReplace(settings);
    }

    public void UpdateTeamBudget(int teamId, long newBudget)
    {
        if (!EnsureDb()) return;
        var team = GetTeamById(teamId);
        if (team != null)
        {
            team.budget = newBudget;
            _db.Update(team);
        }
    }

    public void UpdateTeam(TeamData team)
    {
        if (!EnsureDb()) return;
        _db.Update(team);
    }

    // ── CHEMISTRY HELPERS ──────────────────────────────────
    public int GetTeamChemistry(int teamId)
    {
        var team = GetTeamById(teamId);
        return team?.team_chemistry ?? 50;
    }

    public void UpdateTeamChemistry(int teamId, int chemistry)
    {
        var team = GetTeamById(teamId);
        if (team == null) return;
        team.team_chemistry = Mathf.Clamp(chemistry, 0, 100);
        UpdateTeam(team);
    }

    public void UpdatePlayerMorale(int playerId, int morale)
    {
        var player = _db.Table<PlayerData>().FirstOrDefault(p => p.id == playerId);
        if (player == null) return;
        player.morale = Mathf.Clamp(morale, 0, 100);
        _db.Update(player);
    }

    public int GetPlayerMorale(int playerId)
    {
        var player = _db.Table<PlayerData>().FirstOrDefault(p => p.id == playerId);
        return player?.morale ?? 50;
    }

    public int CalculateTeamChemistry(int teamId, int currentGameDay)
    {
        var players = GetPlayersByTeam(teamId);
        if (players.Count == 0) return 50;

        int avgMorale = (int)players.Average(p => p.morale);

        // Roster stability: check if any trade involving this team in last 30 days
        int stability = 1;
        int tradeThreshold = currentGameDay - 30;
        var recentTrades = _db.Table<TradeData>()
            .Where(t => (t.team_id_from == teamId || t.team_id_to == teamId)
                     && t.game_day > tradeThreshold)
            .ToList();
        if (recentTrades.Count > 0)
            stability = 0;

        var team = GetTeamById(teamId);
        int facilities = team?.facilities ?? 3;
        int facilitiesBonus = Mathf.RoundToInt((facilities / 5f) * 3);

        return Mathf.Clamp(avgMorale + stability * 2 + facilitiesBonus, 0, 100);
    }

    // ── END CHEMISTRY HELPERS ──────────────────────────────

    // ── TRAINING HELPERS ───────────────────────────────────
    public List<TrainingData> GetTeamTraining(int teamId)
    {
        return _db.Table<TrainingData>()
                  .Where(t => t.team_id == teamId && t.completed == 0)
                  .ToList();
    }

    public TrainingData GetPlayerActiveTraining(int playerId)
    {
        return _db.Table<TrainingData>()
                  .Where(t => t.player_id == playerId && t.completed == 0)
                  .FirstOrDefault();
    }

    public void InsertTraining(TrainingData training)
    {
        _db.Insert(training);
    }

    public void CompleteTraining(int id)
    {
        var t = _db.Table<TrainingData>().FirstOrDefault(x => x.id == id);
        if (t != null)
        {
            t.completed = 1;
            _db.Update(t);
        }
    }

    public void CompleteTrainingAndApply(TrainingData t)
    {
        ApplyTrainingEffect(t);
        t.completed = 1;
        _db.Update(t);
    }

    void ApplyTrainingEffect(TrainingData t)
    {
        var player = GetPlayerById(t.player_id);
        if (player == null) return;

        var prop = typeof(PlayerData).GetProperty(t.attribute);
        if (prop == null) return;

        int val = (int)prop.GetValue(player);
        val = Mathf.Min(val + 2, 99);
        prop.SetValue(player, val);

        // Recalculate overall as average of all attributes, capped by potential
        int sum = player.shooting + player.three_point + player.passing + player.dribbling
                + player.defense + player.rebounding + player.speed + player.athleticism
                + player.iq + player.steals + player.blocks;
        player.overall = (int)System.Math.Round(sum / 11f);
        if (player.overall > player.potential)
            player.overall = player.potential;

        _db.Update(player);
    }
    // ── END TRAINING HELPERS ───────────────────────────────

    // Los 5 peores equipos por overall (para ProManager)
    public List<TeamData> GetWorstTeams(int count = 5)
    {
        var all = _db.Table<TeamData>().ToList();
        all.Sort((a, b) => a.overall.CompareTo(b.overall));
        return all.GetRange(0, Mathf.Min(count, all.Count));
    }

    // ── PLAYERS ───────────────────────────────────────────

    public List<PlayerData> GetFreeAgents()
    {
        if (!EnsureDb()) return new List<PlayerData>();
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id == 0)
                  .OrderByDescending(p => p.overall)
                  .ToList();
    }

    public PlayerData GetPlayer(int id)
    {
        return _db.Table<PlayerData>().FirstOrDefault(p => p.id == id);
    }

    public List<PlayerData> GetPlayersByTeam(int teamId)
    {
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id == teamId)
                  .OrderByDescending(p => p.overall)
                  .ToList();
    }

    public List<PlayerData> GetRetiringPlayers()
    {
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id != 0 && p.contract_years <= 1 && p.age >= 40)
                  .OrderByDescending(p => p.age)
                  .ToList();
    }

    public List<PlayerData> GetExpiringPlayers()
    {
        return _db.Table<PlayerData>()
                  .Where(p => p.team_id != 0 && p.contract_years == 1 && p.age < 35)
                  .OrderByDescending(p => p.salary)
                  .ToList();
    }

    public void UpdatePlayer(PlayerData player)
    {
        if (!EnsureDb()) return;
        _db.Update(player);
    }

    public List<PlayerData> GetTopPlayersByStat(int managerId, string stat, int count = 1)
    {
        // Para ahora devolvemos jugadores del equipo del manager ordenados por overall
        // Cuando haya estadísticas reales esto se actualizará
        var manager = GetActiveManager();
        if (manager == null) return new List<PlayerData>();

        var players = GetPlayersByTeam(manager.team_id);
        return players.Take(count).ToList();
    }

    // ── EMPLOYEE ────────────────────────────────────────

    public List<EmployeeData> GetEmployeesByTeam(int teamId)
    {
        return _db.Table<EmployeeData>()
                  .Where(e => e.team_id == teamId)
                  .ToList();
    }

    public List<EmployeeData> GetEmployeeCandidates()
    {
        return _db.Table<EmployeeData>()
                  .Where(e => e.team_id == 0)
                  .OrderBy(e => e.position)
                  .ToList();
    }

    public void InsertEmployee(EmployeeData emp)
    {
        _db.Insert(emp);
    }

    public void UpdateEmployee(EmployeeData emp)
    {
        _db.Update(emp);
    }

    public void DeleteEmployee(int id)
    {
        _db.Delete<EmployeeData>(id);
    }

    public void DeleteEmployeeCandidates()
    {
        _db.Execute("DELETE FROM employees WHERE team_id = 0");
    }

    // ── LOANS ────────────────────────────────────────

    public List<LoanData> GetLoansByTeam(int teamId)
    {
        return _db.Table<LoanData>()
                  .Where(l => l.team_id == teamId)
                  .OrderBy(l => l.slot)
                  .ToList();
    }

    public LoanData GetLoanBySlot(int teamId, int slot)
    {
        return _db.Table<LoanData>()
                  .FirstOrDefault(l => l.team_id == teamId && l.slot == slot);
    }

    public void InsertLoan(LoanData loan)
    {
        _db.Insert(loan);
    }

    public void UpdateLoan(LoanData loan)
    {
        _db.Update(loan);
    }

    public void DeleteLoan(int id)
    {
        _db.Delete<LoanData>(id);
    }

    // ── SCOUTS ────────────────────────────────────────

    public List<ScoutData> GetScoutsByTeam(int teamId)
    {
        return _db.Table<ScoutData>()
                  .Where(s => s.team_id == teamId)
                  .OrderBy(s => s.slot)
                  .ToList();
    }

    public ScoutData GetScoutBySlot(int teamId, int slot)
    {
        return _db.Table<ScoutData>()
                  .FirstOrDefault(s => s.team_id == teamId && s.slot == slot);
    }

    public void InsertScout(ScoutData scout)
    {
        _db.Insert(scout);
    }

    public void UpdateScout(ScoutData scout)
    {
        _db.Update(scout);
    }

    public void DeleteScout(int id)
    {
        _db.Delete<ScoutData>(id);
    }

    // ── MANAGER ────────────────────────────────────────

    public void SaveManager(ManagerData manager)
    {
        if (manager.id == 0)
            _db.Insert(manager);
        else
            _db.Update(manager);
    }

    public ManagerData GetActiveManager()
    {
        return _db.Table<ManagerData>()
                  .OrderByDescending(m => m.id)
                  .FirstOrDefault();
    }

    public void ClearAllManagers()
    {
        if (_db != null)
            _db.Execute("DELETE FROM managers");
    }

    // ── LEAGUE SETTINGS ───────────────────────────────

    public LeagueSettingsData GetLeagueSettings()
    {
        return _db.Table<LeagueSettingsData>()
              .Where(s => s.is_active == 1)
              .FirstOrDefault();
    }

    // ── SEED DATA ─────────────────────────────────────

    void SeedLeagueSettings()
    {
        _db.Insert(new LeagueSettingsData
        {
            salary_cap = 155_000_000,
            luxury_tax = 189_000_000,
            apron = 199_000_000,
            repeater_apron = 209_000_000,
            mid_level = 14_000_000,
            bi_annual = 5_000_000,
            minimum_salary = 2_000_000,
            is_active = 1
        });
    }

    void SeedTeams()
    {
        var teams = new List<TeamData>
        {
            // ── ESTE — ATLÁNTICO ──
            new TeamData { name="Boston Celtics",        abbreviation="BOS", city="Boston",        conference="East", division="Atlántico",  arena="TD Garden",               capacity=19156, owner="Wyc Grousbeck",   attack=88, defense=87, overall=88, budget=310_000_000, reputation=5, facilities=5, logo="celtics",   jersey_home="celtics_home",   jersey_away="celtics_away",   salary_margin=-60_000_000, objective="Playoffs" },
            new TeamData { name="Brooklyn Nets",         abbreviation="BKN", city="Brooklyn",      conference="East", division="Atlántico",  arena="Barclays Center",         capacity=17732, owner="Joe Tsai",         attack=66, defense=65, overall=65, budget=230_000_000, reputation=2, facilities=3, logo="nets",      jersey_home="nets_home",      jersey_away="nets_away",      salary_margin=35_000_000,  objective="Zona tranquila" },
            new TeamData { name="New York Knicks",       abbreviation="NYK", city="New York",      conference="East", division="Atlántico",  arena="Madison Square Garden",   capacity=19812, owner="James Dolan",      attack=88, defense=86, overall=87, budget=310_000_000, reputation=5, facilities=5, logo="knicks",    jersey_home="knicks_home",    jersey_away="knicks_away",    salary_margin=-55_000_000, objective="Campeonato" },
            new TeamData { name="Philadelphia 76ers",    abbreviation="PHI", city="Philadelphia",  conference="East", division="Atlántico",  arena="Wells Fargo Center",      capacity=20478, owner="Josh Harris",      attack=79, defense=78, overall=79, budget=265_000_000, reputation=3, facilities=4, logo="sixers",    jersey_home="76ers_home",     jersey_away="76ers_away",     salary_margin=-15_000_000, objective="Play-In" },
            new TeamData { name="Toronto Raptors",       abbreviation="TOR", city="Toronto",       conference="East", division="Atlántico",  arena="Scotiabank Arena",        capacity=19800, owner="MLSE",             attack=80, defense=79, overall=80, budget=265_000_000, reputation=3, facilities=4, logo="raptors",   jersey_home="raptors_home",   jersey_away="raptors_away",   salary_margin=-10_000_000, objective="Playoffs" },

            // ── ESTE — CENTRAL ──
            new TeamData { name="Chicago Bulls",         abbreviation="CHI", city="Chicago",       conference="East", division="Central",   arena="United Center",           capacity=20917, owner="Jerry Reinsdorf",  attack=68, defense=67, overall=67, budget=230_000_000, reputation=3, facilities=4, logo="bulls",     jersey_home="bulls_home",     jersey_away="bulls_away",     salary_margin=30_000_000,  objective="Zona tranquila" },
            new TeamData { name="Cleveland Cavaliers",   abbreviation="CLE", city="Cleveland",     conference="East", division="Central",   arena="Rocket Arena",            capacity=19432, owner="Dan Gilbert",      attack=85, defense=86, overall=86, budget=285_000_000, reputation=4, facilities=4, logo="cavaliers", jersey_home="cavaliers_home", jersey_away="cavaliers_away", salary_margin=-40_000_000, objective="Playoffs" },
            new TeamData { name="Detroit Pistons",       abbreviation="DET", city="Detroit",       conference="East", division="Central",   arena="Little Caesars Arena",    capacity=20332, owner="Tom Gores",        attack=87, defense=88, overall=87, budget=285_000_000, reputation=3, facilities=4, logo="pistons",   jersey_home="pistons_home",   jersey_away="pistons_away",   salary_margin=-45_000_000, objective="Playoffs" },
            new TeamData { name="Indiana Pacers",        abbreviation="IND", city="Indianapolis",  conference="East", division="Central",   arena="Gainbridge Fieldhouse",   capacity=17923, owner="Herb Simon",       attack=77, defense=75, overall=76, budget=255_000_000, reputation=3, facilities=3, logo="pacers",    jersey_home="pacers_home",    jersey_away="pacers_away",    salary_margin=5_000_000,   objective="Play-In" },
            new TeamData { name="Milwaukee Bucks",       abbreviation="MIL", city="Milwaukee",     conference="East", division="Central",   arena="Fiserv Forum",            capacity=17341, owner="Marc Lasry",       attack=75, defense=73, overall=74, budget=250_000_000, reputation=4, facilities=4, logo="bucks",     jersey_home="bucks_home",     jersey_away="bucks_away",     salary_margin=10_000_000,  objective="Play-In" },

            // ── ESTE — SURESTE ──
            new TeamData { name="Atlanta Hawks",         abbreviation="ATL", city="Atlanta",       conference="East", division="Sureste", arena="State Farm Arena",        capacity=18118, owner="Tony Ressler",     attack=81, defense=79, overall=80, budget=265_000_000, reputation=3, facilities=3, logo="hawks",     jersey_home="hawks_home",     jersey_away="hawks_away",     salary_margin=-10_000_000, objective="Playoffs" },
            new TeamData { name="Charlotte Hornets",     abbreviation="CHA", city="Charlotte",     conference="East", division="Sureste", arena="Spectrum Center",         capacity=19077, owner="Gabe Plotkin",     attack=74, defense=73, overall=73, budget=235_000_000, reputation=2, facilities=3, logo="hornets",   jersey_home="hornets_home",   jersey_away="hornets_away",   salary_margin=20_000_000,  objective="Play-In" },
            new TeamData { name="Miami Heat",            abbreviation="MIA", city="Miami",         conference="East", division="Sureste", arena="Kaseya Center",           capacity=19600, owner="Micky Arison",     attack=76, defense=77, overall=77, budget=255_000_000, reputation=4, facilities=4, logo="heat",      jersey_home="heat_home",      jersey_away="heat_away",      salary_margin=5_000_000,   objective="Play-In" },
            new TeamData { name="Orlando Magic",         abbreviation="ORL", city="Orlando",       conference="East", division="Sureste", arena="Kia Center",              capacity=18846, owner="DeVos family",     attack=78, defense=80, overall=79, budget=260_000_000, reputation=3, facilities=3, logo="magic",     jersey_home="magic_home",     jersey_away="magic_away",     salary_margin=-10_000_000, objective="Playoffs" },
            new TeamData { name="Washington Wizards",    abbreviation="WAS", city="Washington",    conference="East", division="Sureste", arena="Capital One Arena",       capacity=20356, owner="Ted Leonsis",      attack=63, defense=62, overall=62, budget=215_000_000, reputation=2, facilities=3, logo="wizards",   jersey_home="wizards_home",   jersey_away="wizards_away",   salary_margin=55_000_000,  objective="Zona tranquila" },

            // ── OESTE — NOROESTE ──
            new TeamData { name="Denver Nuggets",        abbreviation="DEN", city="Denver",        conference="West", division="Noroeste", arena="Ball Arena",              capacity=19520, owner="Ann Walton Kroenke", attack=88, defense=85, overall=87, budget=305_000_000, reputation=4, facilities=4, logo="nuggets",   jersey_home="nuggets_home",   jersey_away="nuggets_away",   salary_margin=-65_000_000, objective="Playoffs" },
            new TeamData { name="Minnesota Timberwolves",abbreviation="MIN", city="Minneapolis",   conference="West", division="Noroeste", arena="Target Center",           capacity=18978, owner="Marc Lore",        attack=83, defense=85, overall=84, budget=275_000_000, reputation=3, facilities=3, logo="wolves",    jersey_home="wolves_home",    jersey_away="wolves_away",    salary_margin=-25_000_000, objective="Playoffs" },
            new TeamData { name="Oklahoma City Thunder",  abbreviation="OKC", city="Oklahoma City", conference="West", division="Noroeste", arena="Paycom Center",           capacity=18203, owner="Clay Bennett",     attack=90, defense=93, overall=92, budget=285_000_000, reputation=4, facilities=4, logo="thunder",   jersey_home="thunder_home",   jersey_away="thunder_away",   salary_margin=-55_000_000, objective="Campeonato" },
            new TeamData { name="Portland Trail Blazers", abbreviation="POR", city="Portland",      conference="West", division="Noroeste", arena="Moda Center",             capacity=19393, owner="Jody Allen",       attack=74, defense=74, overall=74, budget=240_000_000, reputation=3, facilities=3, logo="blazers",   jersey_home="blazers_home",   jersey_away="blazers_away",   salary_margin=15_000_000,  objective="Play-In" },
            new TeamData { name="Utah Jazz",              abbreviation="UTA", city="Salt Lake City", conference="West", division="Noroeste", arena="Delta Center",            capacity=18306, owner="Ryan Smith",       attack=67, defense=66, overall=66, budget=225_000_000, reputation=2, facilities=3, logo="jazz",      jersey_home="jazz_home",      jersey_away="jazz_away",      salary_margin=40_000_000,  objective="Zona tranquila" },

            // ── OESTE — PACÍFICO ──
            new TeamData { name="Golden State Warriors",  abbreviation="GSW", city="San Francisco", conference="West", division="Pacífico",   arena="Chase Center",            capacity=18064, owner="Joe Lacob",        attack=79, defense=77, overall=78, budget=270_000_000, reputation=5, facilities=5, logo="warriors",  jersey_home="warriors_home",  jersey_away="warriors_away",  salary_margin=-20_000_000, objective="Play-In" },
            new TeamData { name="Los Angeles Clippers",   abbreviation="LAC", city="Los Angeles",   conference="West", division="Pacífico",   arena="Intuit Dome",             capacity=18000, owner="Steve Ballmer",    attack=75, defense=76, overall=75, budget=255_000_000, reputation=3, facilities=5, logo="clippers",  jersey_home="clippers_home",  jersey_away="clippers_away",  salary_margin=10_000_000,  objective="Play-In" },
            new TeamData { name="Los Angeles Lakers",     abbreviation="LAL", city="Los Angeles",   conference="West", division="Pacífico",   arena="Crypto.com Arena",        capacity=18997, owner="Jeanie Buss",      attack=85, defense=83, overall=84, budget=295_000_000, reputation=5, facilities=5, logo="lakers",    jersey_home="lakers_home",    jersey_away="lakers_away",    salary_margin=-50_000_000, objective="Playoffs" },
            new TeamData { name="Phoenix Suns",           abbreviation="PHX", city="Phoenix",       conference="West", division="Pacífico",   arena="Footprint Center",        capacity=18055, owner="Mat Ishbia",       attack=80, defense=79, overall=80, budget=265_000_000, reputation=3, facilities=4, logo="suns",      jersey_home="suns_home",      jersey_away="suns_away",      salary_margin=-15_000_000, objective="Play-In" },
            new TeamData { name="Sacramento Kings",       abbreviation="SAC", city="Sacramento",    conference="West", division="Pacífico",   arena="Golden 1 Center",         capacity=17608, owner="Vivek Ranadivé",   attack=69, defense=68, overall=68, budget=230_000_000, reputation=2, facilities=4, logo="kings",     jersey_home="kings_home",     jersey_away="kings_away",     salary_margin=30_000_000,  objective="Zona tranquila" },

            // ── OESTE — SUROESTE ──
            new TeamData { name="Dallas Mavericks",      abbreviation="DAL", city="Dallas",        conference="West", division="Suroeste", arena="American Airlines Center", capacity=19200, owner="Patrick Dumont",   attack=72, defense=70, overall=71, budget=245_000_000, reputation=4, facilities=4, logo="mavericks", jersey_home="mavericks_home", jersey_away="mavericks_away", salary_margin=20_000_000,  objective="Play-In" },
            new TeamData { name="Houston Rockets",       abbreviation="HOU", city="Houston",       conference="West", division="Suroeste", arena="Toyota Center",           capacity=18055, owner="Tilman Fertitta",  attack=83, defense=83, overall=83, budget=275_000_000, reputation=3, facilities=3, logo="rockets",   jersey_home="rockets_home",   jersey_away="rockets_away",   salary_margin=-25_000_000, objective="Playoffs" },
            new TeamData { name="Memphis Grizzlies",     abbreviation="MEM", city="Memphis",       conference="West", division="Suroeste", arena="FedExForum",              capacity=17794, owner="Robert Pera",      attack=72, defense=73, overall=72, budget=235_000_000, reputation=3, facilities=3, logo="grizzlies", jersey_home="grizzlies_home", jersey_away="grizzlies_away", salary_margin=25_000_000,  objective="Zona tranquila" },
            new TeamData { name="New Orleans Pelicans",  abbreviation="NOP", city="New Orleans",   conference="West", division="Suroeste", arena="Smoothie King Center",    capacity=17791, owner="Gayle Benson",     attack=67, defense=66, overall=66, budget=225_000_000, reputation=2, facilities=3, logo="pelicans",  jersey_home="pelicans_home",  jersey_away="pelicans_away",  salary_margin=35_000_000,  objective="Zona tranquila" },
            new TeamData { name="San Antonio Spurs",     abbreviation="SAS", city="San Antonio",   conference="West", division="Suroeste", arena="AT&T Center",             capacity=18418, owner="Peter Holt",       attack=88, defense=91, overall=90, budget=290_000_000, reputation=4, facilities=4, logo="spurs",     jersey_home="spurs_home",     jersey_away="spurs_away",     salary_margin=-45_000_000, objective="Campeonato" },
        };

        _db.InsertAll(teams);
        _db.Execute("UPDATE teams SET team_chemistry = 50 WHERE team_chemistry IS NULL OR team_chemistry = 0");
        _db.Execute("UPDATE players SET morale = 50 WHERE morale IS NULL OR morale = 0");
        Debug.Log($"[DB] {teams.Count} equipos insertados.");
    }

    // ── SEASON ────────────────────────────────────────────
    public SeasonData CreateSeason(int managerId, string gameMode)
    {
        // Find the max year_start from existing seasons, default to 2025
        var lastSeason = _db.Table<SeasonData>()
            .OrderByDescending(s => s.year_start)
            .FirstOrDefault();
        int yearStart = lastSeason != null ? lastSeason.year_start + 1 : 2026;

        var season = new SeasonData
        {
            year_start = yearStart,
            year_end = yearStart + 1,
            is_active = 1,
            current_game_day = 0,
            game_mode = gameMode,
            phase = "preseason",
            manager_id = managerId,
            generated = 0,
            current_date = $"{yearStart}-09-05"
        };
        _db.Insert(season);
        return season;
    }

    public SeasonData GetActiveSeason(int managerId)
    {
        return _db.Table<SeasonData>()
                .Where(s => s.manager_id == managerId && s.is_active == 1)
                .FirstOrDefault();
    }

    public int GetCurrentDay(int managerId)
    {
        var season = GetActiveSeason(managerId);
        if (season == null) return 0;
        return season.current_game_day;
    }

    // ── GAMES ─────────────────────────────────────────────

    public List<GameData> GetAllGames(int managerId)
    {
        if (!EnsureDb()) return new List<GameData>();
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId)
                  .OrderBy(g => g.game_day)
                  .ToList();
    }

    public List<GameData> GetSeasonGames(int managerId, int seasonId)
    {
        if (!EnsureDb()) return new List<GameData>();
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId && g.season_id == seasonId)
                  .OrderBy(g => g.game_day)
                  .ToList();
    }

    public List<GameData> GetUpcomingGames(int managerId, int currentDay)
    {
        if (!EnsureDb()) return new List<GameData>();
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId && g.game_day > currentDay && g.is_played == 0)
                  .OrderBy(g => g.game_day)
                  .ToList();
    }

    public void SavePreseasonGames(List<GameData> games)
    {
        // Borrar amistosos anteriores de este manager/temporada
        if (games.Count == 0) return;
        int managerId = games[0].manager_id;
        var existing = _db.Table<GameData>()
                        .Where(g => g.manager_id == managerId
                                && g.game_type == "preseason")
                        .ToList();
        foreach (var g in existing)
            _db.Delete(g);

        foreach (var g in games)
            _db.Insert(g);
    }

    public List<GameData> GetPreseasonGames(int managerId)
    {
        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.game_type == "preseason")
                .OrderBy(g => g.game_day)
                .ToList();
    }

    public void SaveRegularSeasonGames(List<GameData> games)
    {
        // Insertar en lotes para mejor rendimiento
        _db.BeginTransaction();
        try
        {
            foreach (var g in games)
                _db.Insert(g);
            _db.Commit();
            Debug.Log($"[DB] {games.Count} partidos de liga regular guardados.");
        }
        catch (Exception e)
        {
            _db.Rollback();
            Debug.LogError($"[DB] Error guardando partidos: {e.Message}");
        }
    }

    public GameData GetNextGame(int managerId, int teamId)
    {
        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.is_played == 0
                        && (g.home_team_id == teamId || g.away_team_id == teamId))
                .OrderBy(g => g.game_date)
                .FirstOrDefault();
    }

    public string GetCurrentDateString(int managerId)
    {
        var season = GetActiveSeason(managerId);
        if (season == null) return "";

        if (!string.IsNullOrEmpty(season.current_date))
            return System.DateTime.Parse(season.current_date).ToString("dd/MM/yyyy");

        if (season.current_game_day == 0)
        {
            var firstPre = _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                         && g.game_type == "preseason")
                .OrderByDescending(g => g.game_day)
                .FirstOrDefault();
            if (firstPre != null)
                return System.DateTime.Parse(firstPre.game_date).ToString("dd/MM/yyyy");
            return new System.DateTime(season.year_start, 10, 22).ToString("dd/MM/yyyy");
        }

        if (season.current_game_day < 0)
        {
            var lastGame = _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                         && g.is_played == 1
                         && g.game_day == season.current_game_day)
                .FirstOrDefault();
            if (lastGame != null)
                return System.DateTime.Parse(lastGame.game_date).ToString("dd/MM/yyyy");
        }

        var seasonStart = new System.DateTime(season.year_start, 10, 22);
        return seasonStart.AddDays(season.current_game_day - 1).ToString("dd/MM/yyyy");
    }

    public GameData GetLastPlayedGame(int managerId, int teamId)
    {
        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.is_played == 1
                        && (g.home_team_id == teamId || g.away_team_id == teamId))
                .OrderByDescending(g => g.game_day)
                .FirstOrDefault();
    }

    public List<GameData> GetGamesOnDay(int managerId, int gameDay)
    {
        // Calcular la fecha correspondiente al día
        var season = GetActiveSeason(managerId);
        if (season == null) return new List<GameData>();

        var date = new System.DateTime(season.year_start, 10, 22)
                    .AddDays(gameDay - 1)
                    .ToString("yyyy-MM-dd");

        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.game_date == date
                        && g.is_played == 0
                        && g.game_type == "regular")
                .ToList();
    }

    public List<GameData> GetGamesOnDate(int managerId, string date)
    {
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId
                           && g.game_date == date
                           && g.is_played == 0)
                  .ToList();
    }

    public List<GameData> GetGamesByGameDay(int managerId, int gameDay)
    {
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId
                           && g.game_day == gameDay
                           && g.is_played == 0)
                  .ToList();
    }

    public List<GameData> GetAllGamesByGameDay(int managerId, int gameDay)
    {
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId
                           && g.game_day == gameDay)
                  .ToList();
    }

    public List<GameData> GetStandingsGames(int managerId)
    {
        return _db.Table<GameData>()
                .Where(g => g.manager_id == managerId
                        && g.game_type == "regular"
                        && g.is_played == 1)
                .OrderBy(g => g.game_day)
                .ThenBy(g => g.id)
                .ToList();
    }

    public void UpdateSeason(SeasonData season)
    {
        _db.Update(season);
    }

    public void UpdateGame(GameData game)
    {
        _db.Update(game);
    }

    // ── PLAYOFFS ───────────────────────────────────────────

    public void SavePlayInGames(List<GameData> games)
    {
        if (games.Count == 0) return;
        _db.BeginTransaction();
        try
        {
            foreach (var g in games)
                _db.Insert(g);
            _db.Commit();
            Debug.Log($"[DB] {games.Count} partidos Play-In guardados.");
        }
        catch (Exception e)
        {
            _db.Rollback();
            Debug.LogError($"[DB] Error guardando partidos Play-In: {e.Message}");
        }
    }

    public void SavePlayoffGames(List<GameData> games)
    {
        if (games.Count == 0) return;
        _db.BeginTransaction();
        try
        {
            foreach (var g in games)
                _db.Insert(g);
            _db.Commit();
            Debug.Log($"[DB] {games.Count} partidos de Playoff guardados.");
        }
        catch (Exception e)
        {
            _db.Rollback();
            Debug.LogError($"[DB] Error guardando partidos de Playoff: {e.Message}");
        }
    }

    public List<GameData> GetPlayInGames(int managerId)
    {
        if (!EnsureDb()) return new List<GameData>();
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId && g.game_type == "playin")
                  .OrderBy(g => g.game_day)
                  .ToList();
    }

    public List<GameData> GetPlayoffGames(int managerId)
    {
        if (!EnsureDb()) return new List<GameData>();
        return _db.Table<GameData>()
                  .Where(g => g.manager_id == managerId && g.game_type == "playoff")
                  .OrderBy(g => g.game_day)
                  .ToList();
    }

    void SeedPlayers()
    {
        var teams = _db.Table<TeamData>().ToList();
        var teamByAbbr = teams.ToDictionary(t => t.abbreviation, t => t.id);

        var players = new System.Collections.Generic.List<PlayerData>();

        // Helper local
        void Add(string abbr, string fn, string ln, string pos, int age, string nat,
                 int h, int w, int ovr, int pot, int spd, int sht, int thr, int pas,
                 int drb, int def, int reb, int ath, int iq, int stl, int blk,
                 long sal, int yrs, bool rookie)
        {
            int teamId = teamByAbbr.TryGetValue(abbr, out var id) ? id : 0;

            var attrs = GeneratePositionAttrs(ovr, pos, fn + ln);
            int calcOvr = (int)System.Math.Round(attrs.Average());
            if (calcOvr > pot) calcOvr = pot;

            players.Add(new PlayerData
            {
                team_id = teamId,
                first_name = fn,
                last_name = ln,
                position = pos,
                age = age,
                nationality = nat,
                height_cm = h,
                weight_kg = w,
                overall = calcOvr,
                potential = pot,
                speed = attrs[0],
                shooting = attrs[1],
                three_point = attrs[2],
                passing = attrs[3],
                dribbling = attrs[4],
                defense = attrs[5],
                rebounding = attrs[6],
                athleticism = attrs[7],
                iq = attrs[8],
                steals = attrs[9],
                blocks = attrs[10],
                salary = sal,
                contract_years = yrs,
                is_rookie = rookie ? 1 : 0,
                seasons_with_team = Math.Max(1, 5 - yrs),
                injury_days = 0,
                injury_type = "",
                treated = 0
            });
        }

        // ── ATL ──
        Add("ATL", "Jalen", "Johnson", "SF", 24, "USA", 203, 99, 86, 91, 92, 85, 83, 92, 88, 88, 93, 98, 92, 80, 55, 30_000_000, 5, false);
        Add("ATL", "Nickeil", "Alexander-Walker", "SG", 27, "CAN", 196, 90, 81, 82, 95, 86, 78, 80, 84, 88, 72, 86, 84, 88, 50, 15_500_000, 4, false);
        Add("ATL", "Dyson", "Daniels", "SG", 22, "AUS", 196, 93, 79, 86, 95, 75, 66, 82, 80, 93, 70, 85, 82, 99, 42, 7_700_000, 2, false);
        Add("ATL", "Onyeka", "Okongwu", "C", 25, "USA", 206, 104, 80, 84, 88, 72, 50, 71, 64, 91, 99, 97, 89, 70, 89, 15_000_000, 4, false);
        Add("ATL", "Zaccharie", "Risacher", "SF", 21, "FRA", 203, 90, 76, 84, 85, 87, 89, 81, 85, 75, 63, 77, 87, 73, 34, 13_200_000, 4, false);
        Add("ATL", "CJ", "McCollum", "SG", 34, "USA", 191, 86, 78, 74, 90, 94, 88, 85, 92, 75, 57, 77, 88, 71, 41, 22_000_000, 2, false);
        Add("ATL", "Jonathan", "Kuminga", "SF", 22, "COD", 201, 99, 78, 88, 95, 83, 72, 72, 78, 80, 78, 97, 81, 72, 50, 7_000_000, 2, false);
        Add("ATL", "Buddy", "Hield", "SG", 33, "BAH", 196, 97, 73, 70, 86, 96, 94, 74, 76, 68, 59, 74, 80, 63, 33, 19_000_000, 2, false);
        Add("ATL", "Gabe", "Vincent", "PG", 29, "USA", 193, 88, 69, 67, 84, 74, 68, 74, 72, 78, 60, 70, 76, 70, 33, 11_000_000, 2, false);
        Add("ATL", "Corey", "Kispert", "SF", 27, "USA", 201, 97, 68, 68, 78, 80, 80, 65, 67, 68, 63, 69, 70, 65, 43, 14_000_000, 3, false);
        Add("ATL", "Asa", "Newell", "PF", 20, "USA", 208, 100, 66, 80, 79, 61, 44, 57, 53, 73, 81, 89, 73, 59, 57, 3_600_000, 4, false);
        Add("ATL", "Jock", "Landale", "C", 29, "AUS", 211, 109, 66, 63, 71, 65, 43, 67, 53, 67, 84, 74, 80, 57, 65, 3_200_000, 2, false);
        Add("ATL", "Caleb", "Houstan", "SF", 23, "CAN", 201, 97, 66, 74, 79, 71, 73, 62, 64, 66, 62, 69, 67, 67, 46, 2_000_000, 3, false);
        Add("ATL", "Keaton", "Wallace", "SG", 25, "USA", 191, 93, 62, 62, 80, 67, 61, 63, 65, 65, 49, 73, 65, 63, 31, 2_300_000, 2, false);
        Add("ATL", "Christian", "Koloko", "C", 25, "CMR", 213, 104, 60, 68, 73, 52, 34, 52, 46, 63, 73, 73, 61, 54, 79, 2_000_000, 2, false);


        // ── BOS ──
        Add("BOS", "Jayson", "Tatum", "PF", 28, "USA", 203, 95, 94, 96, 99, 99, 99, 99, 99, 99, 96, 99, 99, 90, 67, 54100000, 4, false);
        Add("BOS", "Jaylen", "Brown", "SG", 29, "USA", 198, 101, 90, 91, 99, 99, 94, 90, 98, 99, 89, 99, 99, 89, 57, 53100000, 4, false);
        Add("BOS", "Derrick", "White", "PG", 32, "USA", 193, 86, 87, 86, 94, 90, 86, 92, 92, 98, 70, 92, 98, 90, 55, 28100000, 3, false);
        Add("BOS", "Payton", "Pritchard", "PG", 28, "USA", 185, 88, 84, 85, 99, 96, 96, 90, 96, 82, 62, 82, 96, 86, 39, 7200000, 3, false);
        Add("BOS", "Sam", "Hauser", "SF", 28, "USA", 201, 98, 80, 80, 88, 96, 98, 79, 83, 81, 69, 81, 87, 75, 43, 10000000, 4, false);
        Add("BOS", "Nikola", "Vucevic", "C", 35, "MNE", 208, 120, 83, 83, 71, 95, 87, 91, 81, 85, 98, 83, 98, 59, 65, 21400000, 1, false);
        Add("BOS", "Neemias", "Queta", "C", 27, "POR", 213, 111, 75, 78, 83, 71, 47, 65, 59, 81, 95, 89, 81, 65, 89, 3000000, 2, false);
        Add("BOS", "Jordan", "Walsh", "SF", 22, "USA", 201, 93, 74, 81, 89, 71, 63, 69, 71, 87, 71, 91, 77, 75, 50, 2200000, 2, false);
        Add("BOS", "Baylor", "Scheierman", "SG", 25, "USA", 198, 92, 75, 82, 86, 84, 90, 78, 80, 75, 67, 77, 82, 69, 37, 2600000, 2, false);
        Add("BOS", "Dalano", "Banton", "PG", 26, "CAN", 203, 92, 76, 78, 90, 82, 75, 81, 84, 79, 67, 83, 81, 73, 41, 2500000, 1, false);
        Add("BOS", "Hugo", "Gonzalez", "SF", 20, "ESP", 198, 91, 72, 84, 86, 74, 68, 68, 74, 78, 66, 86, 76, 74, 42, 2200000, 4, false);
        Add("BOS", "Luka", "Garza", "C", 27, "USA", 208, 110, 74, 74, 69, 91, 73, 75, 69, 73, 96, 75, 87, 49, 57, 2300000, 1, false);
        Add("BOS", "Amari", "Williams", "C", 24, "GBR", 211, 113, 71, 79, 71, 65, 52, 67, 59, 77, 89, 85, 77, 58, 81, 1200000, 4, false);
        Add("BOS", "Max", "Shulga", "PG", 24, "UKR", 193, 88, 69, 76, 82, 75, 78, 77, 79, 67, 57, 73, 77, 63, 31, 1200000, 4, false);
        Add("BOS", "Ron", "Harper Jr.", "SF", 26, "USA", 198, 111, 70, 73, 80, 74, 76, 66, 70, 76, 66, 80, 76, 66, 40, 1200000, 2, false);

        // ── BKN ──
        Add("BKN", "Michael", "Porter Jr.", "SF", 28, "USA", 208, 99, 86, 86, 93, 99, 99, 82, 91, 86, 88, 89, 95, 64, 60, 38300000, 2, false);
        Add("BKN", "Nic", "Claxton", "C", 27, "USA", 211, 98, 84, 86, 94, 78, 51, 76, 68, 98, 99, 98, 92, 72, 98, 25300000, 3, false);
        Add("BKN", "Terance", "Mann", "SG", 29, "USA", 196, 98, 79, 79, 88, 83, 73, 83, 85, 87, 71, 88, 87, 81, 43, 15500000, 2, false);
        Add("BKN", "Noah", "Clowney", "PF", 22, "USA", 208, 95, 78, 86, 85, 79, 75, 71, 75, 83, 81, 91, 81, 73, 64, 3400000, 2, false);
        Add("BKN", "Ziaire", "Williams", "SF", 24, "USA", 206, 84, 77, 82, 90, 82, 78, 74, 80, 82, 72, 86, 82, 72, 49, 6250000, 1, false);
        Add("BKN", "Egor", "Demin", "PG", 19, "RUS", 203, 91, 79, 92, 90, 82, 80, 94, 92, 74, 68, 88, 88, 72, 41, 6900000, 4, false);
        Add("BKN", "Nolan", "Traore", "PG", 19, "FRA", 191, 84, 77, 91, 98, 80, 70, 95, 92, 70, 60, 90, 86, 74, 32, 3810000, 4, false);
        Add("BKN", "Ben", "Saraf", "PG", 19, "ISR", 198, 91, 76, 90, 89, 79, 72, 93, 89, 74, 66, 85, 83, 70, 36, 2880000, 4, false);
        Add("BKN", "Danny", "Wolf", "PF", 21, "USA", 211, 113, 76, 89, 75, 77, 73, 89, 83, 75, 90, 79, 85, 53, 57, 2800000, 4, false);
        Add("BKN", "Drake", "Powell", "SG", 20, "USA", 196, 88, 74, 88, 88, 74, 67, 67, 72, 84, 69, 90, 76, 78, 49, 3370000, 4, false);
        Add("BKN", "Day'Ron", "Sharpe", "C", 24, "USA", 208, 120, 78, 80, 84, 76, 46, 70, 64, 88, 99, 93, 86, 62, 90, 6250000, 3, false);
        Add("BKN", "Jalen", "Wilson", "PF", 25, "USA", 198, 100, 76, 79, 82, 82, 79, 73, 77, 83, 79, 84, 83, 69, 45, 2200000, 2, false);
        Add("BKN", "Ochai", "Agbaji", "SG", 25, "USA", 196, 98, 78, 79, 92, 84, 83, 71, 77, 86, 69, 88, 84, 83, 41, 6500000, 1, false);
        Add("BKN", "Josh", "Minott", "SF", 23, "USA", 203, 98, 75, 82, 90, 73, 63, 69, 73, 86, 72, 92, 76, 80, 51, 2500000, 2, false);
        Add("BKN", "E.J.", "Liddell", "PF", 25, "USA", 201, 109, 73, 75, 77, 78, 70, 68, 68, 81, 78, 81, 78, 68, 56, 2200000, 1, false);

        // ── CHA ──
        Add("CHA", "LaMelo", "Ball", "PG", 25, "USA", 201, 82, 89, 92, 99, 99, 99, 99, 99, 85, 70, 99, 99, 89, 42, 37900000, 4, false);
        Add("CHA", "Brandon", "Miller", "SF", 23, "USA", 206, 91, 87, 93, 95, 97, 93, 89, 97, 87, 77, 95, 95, 81, 51, 16000000, 2, false);
        Add("CHA", "Miles", "Bridges", "PF", 28, "USA", 201, 102, 84, 84, 92, 92, 84, 83, 90, 88, 83, 98, 90, 75, 49, 25000000, 2, false);
        Add("CHA", "Coby", "White", "SG", 26, "USA", 196, 88, 85, 86, 99, 98, 94, 92, 96, 82, 72, 92, 96, 78, 36, 12000000, 2, false);
        Add("CHA", "Tre", "Mann", "PG", 25, "USA", 191, 81, 80, 82, 96, 93, 89, 91, 95, 75, 61, 85, 91, 73, 31, 12000000, 3, false);
        Add("CHA", "Kon", "Knueppel", "SG", 20, "USA", 198, 99, 79, 90, 87, 92, 96, 85, 91, 75, 69, 83, 93, 69, 29, 7000000, 4, false);
        Add("CHA", "Liam", "McNeeley", "SF", 20, "USA", 201, 98, 77, 89, 83, 89, 93, 81, 87, 75, 71, 81, 89, 67, 31, 4500000, 4, false);
        Add("CHA", "Tidjane", "Salaun", "PF", 20, "FRA", 206, 98, 76, 91, 86, 78, 75, 71, 76, 80, 75, 92, 80, 74, 49, 6500000, 3, false);
        Add("CHA", "Ryan", "Kalkbrenner", "C", 23, "USA", 216, 117, 76, 84, 73, 79, 55, 69, 65, 83, 96, 81, 85, 53, 97, 3200000, 4, false);
        Add("CHA", "Josh", "Green", "SG", 25, "AUS", 196, 95, 77, 78, 89, 78, 76, 74, 80, 86, 68, 90, 82, 82, 42, 13000000, 3, false);
        Add("CHA", "Grant", "Williams", "PF", 27, "USA", 198, 107, 77, 77, 78, 84, 82, 76, 74, 86, 80, 80, 88, 72, 47, 13000000, 2, false);
        Add("CHA", "Xavier", "Tillman", "C", 27, "USA", 203, 111, 76, 76, 75, 76, 65, 73, 69, 88, 90, 80, 86, 69, 65, 7000000, 2, false);
        Add("CHA", "Moussa", "Diabate", "C", 23, "FRA", 211, 95, 75, 82, 87, 69, 37, 63, 59, 83, 99, 95, 79, 65, 89, 2500000, 3, false);
        Add("CHA", "Pat", "Connaughton", "SG", 33, "USA", 196, 94, 75, 74, 84, 82, 84, 75, 78, 78, 69, 82, 82, 74, 37, 9400000, 1, false);

        // ── CHI ──
        Add("CHI", "Josh", "Giddey", "PG", 23, "AUS", 203, 93, 86, 90, 89, 87, 91, 99, 96, 85, 89, 93, 99, 75, 43, 30000000, 5, false);
        Add("CHI", "Anfernee", "Simons", "SG", 27, "USA", 191, 82, 86, 87, 99, 99, 99, 96, 99, 80, 62, 97, 99, 78, 38, 27000000, 2, false);
        Add("CHI", "Collin", "Sexton", "PG", 27, "USA", 188, 86, 84, 84, 99, 99, 89, 91, 99, 79, 61, 98, 95, 81, 33, 19000000, 2, false);
        Add("CHI", "Matas", "Buzelis", "SF", 21, "USA", 208, 95, 82, 92, 89, 87, 83, 81, 89, 85, 77, 93, 87, 77, 54, 7000000, 3, false);
        Add("CHI", "Isaac", "Okoro", "SF", 25, "USA", 196, 102, 79, 80, 91, 77, 70, 76, 79, 93, 70, 95, 83, 91, 44, 11000000, 2, false);
        Add("CHI", "Patrick", "Williams", "PF", 25, "USA", 201, 97, 78, 82, 84, 80, 78, 72, 76, 87, 76, 89, 82, 84, 50, 18000000, 4, false);
        Add("CHI", "Guerschon", "Yabusele", "PF", 30, "FRA", 203, 118, 79, 79, 82, 87, 86, 78, 80, 84, 84, 86, 88, 72, 42, 12000000, 2, false);
        Add("CHI", "Zach", "Collins", "C", 29, "USA", 211, 113, 78, 78, 74, 82, 76, 82, 76, 84, 90, 80, 88, 64, 62, 18000000, 2, false);
        Add("CHI", "Nick", "Richards", "C", 28, "JAM", 213, 111, 77, 77, 84, 80, 40, 66, 62, 87, 99, 91, 85, 64, 89, 5000000, 2, false);
        Add("CHI", "Tre", "Jones", "PG", 26, "USA", 185, 83, 83, 82, 94, 88, 82, 97, 95, 86, 64, 88, 97, 88, 34, 9000000, 2, false);
        Add("CHI", "Rob", "Dillingham", "PG", 21, "USA", 188, 79, 82, 93, 98, 99, 96, 99, 99, 71, 55, 92, 91, 71, 31, 6500000, 4, false);
        Add("CHI", "Noa", "Essengue", "PF", 19, "FRA", 208, 97, 77, 92, 87, 76, 68, 72, 78, 83, 80, 91, 80, 78, 54, 6500000, 4, false);
        Add("CHI", "Leonard", "Miller", "PF", 22, "CAN", 208, 98, 77, 89, 86, 79, 67, 73, 77, 82, 84, 92, 81, 75, 51, 2800000, 3, false);
        Add("CHI", "Jalen", "Smith", "C", 26, "USA", 208, 98, 77, 80, 78, 82, 78, 71, 70, 82, 90, 82, 82, 63, 69, 9000000, 2, false);

        // ── CLE ──
        Add("CLE", "Donovan", "Mitchell", "SG", 30, "USA", 191, 97, 93, 93, 99, 99, 99, 99, 99, 99, 81, 99, 99, 99, 51, 54000000, 4, false);
        Add("CLE", "Evan", "Mobley", "PF", 25, "USA", 211, 98, 92, 95, 88, 92, 84, 90, 92, 99, 98, 96, 99, 82, 92, 45000000, 5, false);
        Add("CLE", "James", "Harden", "PG", 37, "USA", 196, 100, 86, 86, 87, 98, 97, 99, 99, 77, 73, 87, 99, 85, 45, 32000000, 2, false);
        Add("CLE", "Jarrett", "Allen", "C", 28, "USA", 206, 110, 86, 86, 91, 89, 47, 79, 75, 99, 99, 98, 97, 73, 99, 30000000, 3, false);
        Add("CLE", "Keon", "Ellis", "SG", 27, "USA", 193, 79, 82, 82, 92, 85, 83, 79, 85, 94, 69, 90, 88, 94, 43, 9000000, 3, false);
        Add("CLE", "Max", "Strus", "SF", 30, "USA", 196, 97, 81, 81, 90, 92, 95, 80, 84, 84, 72, 88, 90, 78, 38, 16000000, 2, false);
        Add("CLE", "Dennis", "Schroder", "PG", 33, "GER", 188, 78, 79, 79, 94, 86, 75, 92, 90, 77, 61, 88, 88, 83, 35, 7000000, 1, false);
        Add("CLE", "Sam", "Merrill", "SG", 30, "USA", 193, 93, 77, 77, 87, 91, 99, 76, 81, 77, 66, 81, 85, 72, 32, 5000000, 2, false);
        Add("CLE", "Larry", "Nance Jr.", "PF", 33, "USA", 201, 111, 77, 77, 79, 78, 74, 76, 74, 87, 81, 83, 87, 72, 56, 6000000, 1, false);
        Add("CLE", "Jaylon", "Tyson", "SF", 23, "USA", 198, 99, 76, 84, 85, 81, 79, 77, 81, 81, 73, 86, 81, 71, 41, 3200000, 3, false);
        Add("CLE", "Tyrese", "Proctor", "PG", 22, "AUS", 196, 84, 78, 88, 92, 82, 80, 94, 92, 77, 61, 86, 88, 73, 33, 4500000, 4, false);
        Add("CLE", "Craig", "Porter Jr.", "PG", 26, "USA", 188, 83, 77, 79, 94, 80, 74, 90, 88, 78, 58, 88, 84, 80, 33, 2500000, 2, false);
        Add("CLE", "Dean", "Wade", "PF", 30, "USA", 206, 103, 76, 76, 78, 81, 85, 72, 74, 85, 77, 79, 83, 70, 52, 7000000, 2, false);
        Add("CLE", "Thomas", "Bryant", "C", 29, "USA", 208, 112, 75, 75, 79, 85, 67, 71, 69, 81, 93, 81, 85, 57, 57, 3500000, 1, false);

        // ── DAL ──
        Add("DAL", "Kyrie", "Irving", "PG", 34, "USA", 188, 88, 90, 90, 99, 99, 99, 99, 99, 86, 70, 99, 99, 95, 46, 36566002, 2, false);
        Add("DAL", "Cooper", "Flagg", "SF", 19, "USA", 206, 92, 87, 98, 88, 88, 84, 86, 90, 94, 84, 95, 92, 82, 74, 14386320, 4, false);
        Add("DAL", "P.J.", "Washington", "PF", 27, "USA", 201, 104, 84, 84, 88, 90, 86, 82, 86, 92, 86, 92, 90, 78, 54, 14152174, 2, false);
        Add("DAL", "Dereck", "Lively II", "C", 22, "USA", 216, 104, 84, 91, 89, 83, 44, 75, 70, 99, 99, 99, 93, 74, 99, 5253360, 2, false);
        Add("DAL", "Daniel", "Gafford", "C", 27, "USA", 208, 120, 82, 82, 87, 87, 40, 71, 67, 99, 99, 95, 91, 67, 99, 14386320, 2, false);
        Add("DAL", "Klay", "Thompson", "SG", 36, "USA", 196, 99, 81, 81, 85, 97, 99, 79, 85, 85, 71, 83, 95, 73, 39, 16666667, 2, false);
        Add("DAL", "Max", "Christie", "SG", 23, "USA", 196, 86, 79, 86, 88, 81, 81, 77, 83, 88, 69, 90, 83, 86, 43, 7714286, 3, false);
        Add("DAL", "Naji", "Marshall", "SF", 28, "USA", 198, 99, 79, 79, 89, 81, 75, 79, 83, 87, 73, 91, 85, 81, 45, 9000000, 2, false);
        Add("DAL", "Caleb", "Martin", "SF", 30, "USA", 196, 92, 78, 78, 87, 81, 81, 75, 79, 87, 71, 89, 85, 81, 42, 9594044, 2, false);
        Add("DAL", "Khris", "Middleton", "SF", 34, "USA", 201, 100, 81, 81, 79, 94, 94, 91, 92, 81, 75, 81, 96, 69, 39, 33926296, 1, false);
        Add("DAL", "AJ", "Johnson", "PG", 21, "USA", 196, 72, 78, 90, 98, 82, 76, 96, 94, 70, 58, 94, 84, 72, 34, 3090480, 4, false);
        Add("DAL", "Tyler", "Smith", "PF", 21, "USA", 206, 101, 75, 88, 82, 79, 77, 69, 73, 79, 79, 88, 79, 67, 53, 1955377, 3, false);
        Add("DAL", "Ryan", "Nembhard", "PG", 23, "CAN", 180, 81, 76, 82, 96, 79, 75, 98, 94, 67, 53, 80, 90, 77, 27, 321875, 4, false);
        Add("DAL", "Brandon", "Williams", "PG", 26, "USA", 188, 86, 74, 76, 95, 82, 76, 87, 85, 68, 54, 85, 82, 72, 28, 2200000, 1, false);
        Add("DAL", "Marvin", "Bagley III", "PF", 27, "USA", 208, 106, 76, 76, 89, 87, 56, 72, 74, 85, 95, 91, 83, 56, 48, 2296274, 1, false);

        // ── DEN ──
        Add("DEN", "Nikola", "Jokic", "C", 31, "SRB", 211, 129, 98, 99, 93, 99, 99, 99, 99, 99, 99, 99, 99, 89, 93, 62000000, 4, false);
        Add("DEN", "Jamal", "Murray", "PG", 29, "CAN", 193, 98, 88, 88, 99, 99, 99, 99, 99, 84, 72, 98, 99, 84, 36, 50000000, 4, false);
        Add("DEN", "Aaron", "Gordon", "PF", 31, "USA", 203, 107, 85, 85, 91, 87, 80, 82, 87, 93, 87, 99, 91, 80, 58, 33000000, 3, false);
        Add("DEN", "Cameron", "Johnson", "SF", 30, "USA", 203, 95, 84, 84, 91, 96, 98, 83, 89, 85, 79, 89, 92, 75, 47, 23000000, 2, false);
        Add("DEN", "Christian", "Braun", "SG", 25, "USA", 198, 100, 84, 86, 93, 88, 86, 84, 90, 92, 76, 95, 90, 86, 44, 11000000, 4, false);
        Add("DEN", "Tyus", "Jones", "PG", 30, "USA", 185, 89, 81, 81, 90, 88, 82, 99, 98, 77, 61, 82, 99, 88, 27, 8000000, 2, false);
        Add("DEN", "Jonas", "Valanciunas", "C", 34, "LIT", 211, 120, 80, 80, 73, 93, 73, 85, 79, 89, 99, 79, 99, 57, 54, 10000000, 1, false);
        Add("DEN", "Bruce", "Brown", "SG", 30, "USA", 193, 92, 79, 79, 91, 81, 75, 83, 85, 89, 68, 91, 85, 83, 38, 6000000, 1, false);
        Add("DEN", "Peyton", "Watson", "SF", 23, "USA", 201, 91, 79, 88, 87, 75, 70, 72, 77, 89, 73, 93, 79, 77, 77, 5500000, 3, false);
        Add("DEN", "Julian", "Strawther", "SG", 24, "USA", 198, 93, 78, 84, 90, 88, 92, 79, 87, 77, 67, 85, 87, 71, 35, 3200000, 2, false);
        Add("DEN", "DaRon", "Holmes II", "PF", 23, "USA", 208, 107, 77, 89, 81, 79, 74, 72, 77, 83, 81, 89, 79, 70, 62, 4000000, 4, false);
        Add("DEN", "Jalen", "Pickett", "PG", 27, "USA", 188, 83, 76, 76, 82, 82, 78, 96, 92, 75, 59, 78, 94, 73, 27, 2500000, 2, false);
        Add("DEN", "Tim", "Hardaway Jr.", "SG", 34, "USA", 196, 93, 77, 77, 89, 94, 98, 75, 83, 75, 63, 83, 87, 67, 33, 6000000, 1, false);
        Add("DEN", "Zeke", "Nnaji", "PF", 25, "USA", 206, 109, 73, 78, 81, 74, 64, 68, 70, 81, 83, 85, 77, 64, 56, 8000000, 3, false);
        Add("DEN", "David", "Roddy", "SF", 25, "USA", 193, 116, 72, 74, 80, 77, 75, 69, 73, 78, 73, 84, 77, 67, 39, 2200000, 1, false);

        // ── DET ──
        Add("DET", "Cade", "Cunningham", "PG", 25, "USA", 198, 100, 92, 94, 98, 99, 96, 99, 99, 93, 85, 99, 99, 89, 45, 45000000, 5, false);
        Add("DET", "Jalen", "Duren", "C", 22, "USA", 211, 113, 85, 92, 93, 89, 44, 77, 73, 97, 99, 99, 95, 72, 97, 13000000, 3, false);
        Add("DET", "Ausar", "Thompson", "SF", 23, "USA", 201, 98, 84, 92, 93, 78, 66, 82, 85, 97, 80, 99, 87, 95, 62, 10000000, 3, false);
        Add("DET", "Tobias", "Harris", "PF", 34, "USA", 203, 102, 80, 80, 78, 89, 88, 84, 86, 84, 84, 82, 93, 68, 44, 26000000, 1, false);
        Add("DET", "Caris", "LeVert", "SG", 32, "USA", 198, 93, 80, 80, 94, 90, 82, 88, 94, 78, 67, 90, 90, 74, 33, 12000000, 2, false);
        Add("DET", "Kevin", "Huerter", "SG", 28, "USA", 201, 91, 79, 79, 91, 91, 95, 82, 87, 78, 68, 84, 89, 70, 34, 17000000, 2, false);
        Add("DET", "Isaiah", "Stewart", "PF", 25, "USA", 203, 113, 79, 80, 77, 81, 77, 72, 73, 89, 89, 87, 83, 73, 68, 15000000, 3, false);
        Add("DET", "Duncan", "Robinson", "SG", 32, "USA", 201, 97, 78, 78, 89, 97, 99, 77, 83, 75, 66, 83, 89, 66, 34, 9000000, 2, false);
        Add("DET", "Ronald", "Holland II", "SF", 20, "USA", 203, 93, 77, 92, 90, 74, 64, 72, 78, 88, 74, 95, 78, 84, 50, 9000000, 4, false);
        Add("DET", "Marcus", "Sasser", "PG", 25, "USA", 188, 84, 77, 80, 95, 89, 87, 89, 91, 71, 56, 85, 87, 71, 26, 4200000, 2, false);
        Add("DET", "Javonte", "Green", "SF", 33, "USA", 193, 93, 75, 75, 86, 74, 69, 67, 72, 90, 68, 94, 78, 86, 41, 3500000, 1, false);
        Add("DET", "Paul", "Reed", "PF", 27, "USA", 206, 95, 75, 78, 82, 74, 53, 68, 66, 88, 92, 90, 78, 70, 64, 5000000, 2, false);
        Add("DET", "Wendell", "Moore Jr.", "SG", 24, "USA", 196, 97, 72, 78, 84, 73, 67, 73, 77, 81, 65, 85, 77, 77, 33, 2500000, 2, false);
        Add("DET", "Chaz", "Lanier", "SG", 24, "USA", 196, 84, 73, 82, 88, 86, 94, 73, 81, 69, 57, 81, 82, 65, 27, 2800000, 4, false);

        // ── GSW ──
        Add("GSW", "Stephen", "Curry", "PG", 38, "USA", 188, 84, 94, 94, 99, 99, 99, 99, 99, 97, 82, 99, 99, 99, 52, 59600000, 2, false);
        Add("GSW", "Jimmy", "Butler III", "SF", 37, "USA", 201, 104, 89, 89, 91, 97, 83, 95, 97, 97, 81, 95, 99, 91, 53, 54000000, 2, false);
        Add("GSW", "Kristaps", "Porzingis", "C", 31, "LAT", 221, 109, 86, 86, 84, 95, 91, 82, 76, 92, 90, 88, 95, 60, 93, 30700000, 2, false);
        Add("GSW", "Draymond", "Green", "PF", 36, "USA", 198, 104, 83, 83, 76, 76, 70, 94, 86, 98, 84, 82, 99, 92, 56, 26000000, 3, false);
        Add("GSW", "Brandin", "Podziemski", "SG", 23, "USA", 193, 93, 82, 89, 90, 86, 84, 90, 94, 84, 76, 90, 92, 78, 38, 4000000, 2, false);
        Add("GSW", "Moses", "Moody", "SG", 24, "USA", 196, 96, 80, 85, 87, 87, 89, 78, 84, 85, 70, 89, 85, 84, 42, 12000000, 3, false);
        Add("GSW", "De'Anthony", "Melton", "SG", 28, "USA", 188, 91, 79, 79, 91, 81, 77, 81, 83, 93, 67, 87, 85, 91, 33, 9000000, 1, false);
        Add("GSW", "Al", "Horford", "C", 40, "DOM", 206, 109, 78, 78, 67, 80, 80, 82, 74, 86, 86, 70, 96, 61, 76, 9000000, 1, false);
        Add("GSW", "Gary", "Payton II", "SG", 33, "USA", 188, 88, 77, 77, 92, 74, 63, 72, 78, 98, 65, 94, 82, 96, 33, 9000000, 1, false);
        Add("GSW", "Seth", "Curry", "SG", 36, "USA", 188, 76, 76, 76, 88, 96, 99, 84, 88, 68, 55, 78, 92, 61, 27, 5000000, 1, false);
        Add("GSW", "Quinten", "Post", "C", 25, "NED", 213, 111, 75, 82, 72, 83, 85, 70, 68, 78, 89, 78, 82, 52, 68, 2200000, 3, false);
        Add("GSW", "Charles", "Bassey", "C", 26, "NGA", 208, 104, 74, 80, 81, 71, 38, 63, 60, 85, 97, 91, 81, 62, 85, 2500000, 2, false);
        Add("GSW", "Gui", "Santos", "SF", 24, "BRA", 203, 95, 74, 82, 84, 75, 71, 73, 76, 82, 73, 86, 78, 73, 43, 2200000, 3, false);

        // ── HOU ──
        Add("HOU", "Kevin", "Durant", "SF", 38, "USA", 211, 109, 93, 93, 96, 99, 99, 99, 99, 94, 88, 99, 99, 78, 62, 54700000, 2, false);
        Add("HOU", "Alperen", "Sengun", "C", 24, "TUR", 211, 110, 90, 94, 87, 99, 87, 99, 99, 91, 99, 93, 99, 73, 64, 38000000, 5, false);
        Add("HOU", "Amen", "Thompson", "SF", 23, "USA", 201, 97, 88, 96, 99, 83, 67, 91, 93, 98, 85, 99, 93, 97, 63, 11000000, 3, false);
        Add("HOU", "Fred", "VanVleet", "PG", 32, "USA", 183, 89, 84, 84, 90, 92, 88, 98, 96, 90, 60, 86, 98, 96, 30, 44500000, 2, false);
        Add("HOU", "Jabari", "Smith Jr.", "PF", 23, "USA", 211, 100, 84, 91, 87, 88, 88, 79, 85, 90, 87, 92, 90, 77, 61, 15000000, 2, false);
        Add("HOU", "Tari", "Eason", "SF", 25, "USA", 203, 98, 83, 89, 91, 82, 74, 78, 82, 95, 82, 97, 85, 93, 54, 12000000, 3, false);
        Add("HOU", "Reed", "Sheppard", "PG", 22, "USA", 188, 82, 81, 92, 90, 90, 98, 92, 94, 79, 59, 83, 92, 88, 26, 11000000, 3, false);
        Add("HOU", "Dorian", "Finney-Smith", "SF", 33, "USA", 201, 100, 79, 79, 82, 81, 84, 75, 77, 92, 73, 88, 84, 88, 45, 14000000, 2, false);
        Add("HOU", "Clint", "Capela", "C", 32, "SUI", 208, 116, 79, 79, 84, 78, 39, 69, 63, 98, 99, 92, 88, 65, 94, 9000000, 1, false);
        Add("HOU", "Steven", "Adams", "C", 33, "NZL", 211, 120, 77, 77, 74, 76, 36, 72, 64, 99, 99, 90, 93, 60, 84, 6000000, 1, false);
        Add("HOU", "Josh", "Okogie", "SG", 28, "NGA", 193, 97, 75, 75, 90, 71, 63, 69, 72, 94, 67, 94, 78, 90, 37, 3500000, 1, false);
        Add("HOU", "Aaron", "Holiday", "PG", 30, "USA", 183, 84, 74, 74, 89, 82, 83, 85, 84, 76, 54, 78, 84, 74, 25, 3500000, 1, false);
        Add("HOU", "Jae'Sean", "Tate", "SF", 31, "USA", 193, 104, 74, 74, 81, 75, 65, 71, 75, 87, 73, 89, 79, 81, 38, 3500000, 1, false);
        Add("HOU", "Jeff", "Green", "PF", 40, "USA", 203, 107, 73, 73, 78, 79, 76, 70, 72, 79, 74, 80, 85, 64, 46, 3500000, 1, false);
        Add("HOU", "Tristen", "Newton", "PG", 24, "USA", 196, 86, 72, 80, 87, 75, 73, 87, 85, 71, 58, 79, 81, 70, 26, 2200000, 3, false);

        // ── IND ──
        Add("IND", "Tyrese", "Haliburton", "PG", 26, "USA", 196, 84, 93, 95, 99, 99, 99, 99, 99, 99, 83, 99, 99, 99, 49, 55000000, 5, false);
        Add("IND", "Pascal", "Siakam", "PF", 32, "CMR", 203, 104, 89, 89, 92, 99, 84, 95, 97, 94, 88, 97, 99, 82, 52, 52000000, 4, false);
        Add("IND", "Ivica", "Zubac", "C", 29, "CRO", 213, 109, 85, 85, 80, 94, 53, 84, 79, 99, 99, 92, 98, 67, 90, 20000000, 3, false);
        Add("IND", "Andrew", "Nembhard", "PG", 26, "CAN", 193, 87, 84, 86, 92, 89, 83, 98, 96, 88, 67, 90, 98, 88, 35, 18000000, 4, false);
        Add("IND", "Aaron", "Nesmith", "SF", 27, "USA", 198, 98, 82, 83, 90, 88, 90, 77, 83, 90, 73, 92, 86, 90, 43, 16000000, 3, false);
        Add("IND", "Jarace", "Walker", "PF", 22, "USA", 203, 107, 80, 91, 82, 78, 75, 78, 80, 90, 78, 94, 84, 84, 57, 9000000, 3, false);
        Add("IND", "Obi", "Toppin", "PF", 28, "USA", 206, 100, 79, 79, 92, 87, 81, 77, 83, 79, 77, 98, 85, 69, 41, 14000000, 2, false);
        Add("IND", "T.J.", "McConnell", "PG", 34, "USA", 185, 86, 79, 79, 89, 83, 71, 96, 93, 83, 55, 83, 96, 95, 25, 10000000, 2, false);
        Add("IND", "Ben", "Sheppard", "SG", 25, "USA", 198, 86, 77, 82, 86, 82, 88, 78, 82, 84, 66, 84, 84, 80, 33, 3200000, 2, false);
        Add("IND", "Johnny", "Furphy", "SF", 21, "AUS", 206, 86, 76, 89, 86, 81, 84, 75, 81, 81, 67, 88, 81, 73, 39, 2900000, 3, false);
        Add("IND", "Jay", "Huff", "C", 28, "USA", 216, 109, 77, 78, 72, 84, 82, 70, 66, 82, 94, 76, 84, 53, 84, 4500000, 2, false);
        Add("IND", "Kam", "Jones", "PG", 23, "USA", 193, 92, 75, 84, 88, 83, 81, 88, 87, 71, 57, 83, 85, 73, 29, 2800000, 4, false);
        Add("IND", "Kobe", "Brown", "PF", 26, "USA", 201, 113, 74, 78, 80, 78, 74, 72, 74, 82, 78, 84, 80, 68, 44, 2600000, 2, false);
        Add("IND", "Quenton", "Jackson", "SG", 27, "USA", 196, 79, 73, 76, 92, 76, 70, 78, 80, 78, 58, 88, 78, 76, 29, 2200000, 2, false);

        // ── LAC ──
        Add("LAC", "Kawhi", "Leonard", "SF", 35, "USA", 201, 102, 92, 92, 89, 99, 92, 94, 99, 99, 85, 94, 99, 96, 55, 50000000, 2, false);
        Add("LAC", "Darius", "Garland", "PG", 26, "USA", 185, 87, 89, 91, 99, 99, 99, 99, 99, 86, 70, 99, 99, 92, 38, 40000000, 4, false);
        Add("LAC", "Bradley", "Beal", "SG", 33, "USA", 193, 94, 85, 85, 97, 99, 96, 95, 99, 80, 64, 94, 99, 76, 36, 5500000, 2, false);
        Add("LAC", "John", "Collins", "PF", 29, "USA", 206, 103, 84, 84, 92, 94, 83, 79, 87, 87, 93, 98, 91, 69, 51, 26000000, 3, false);
        Add("LAC", "Bennedict", "Mathurin", "SG", 24, "CAN", 198, 95, 84, 90, 99, 98, 90, 86, 96, 82, 72, 98, 92, 72, 39, 9000000, 2, false);
        Add("LAC", "Brook", "Lopez", "C", 38, "USA", 216, 127, 80, 80, 63, 89, 87, 73, 69, 91, 96, 69, 95, 53, 95, 9000000, 1, false);
        Add("LAC", "Bogdan", "Bogdanovic", "SG", 34, "SRB", 196, 102, 80, 80, 87, 94, 98, 89, 93, 75, 63, 85, 96, 69, 31, 16000000, 2, false);
        Add("LAC", "Derrick", "Jones Jr.", "SF", 29, "USA", 198, 95, 79, 79, 92, 77, 67, 69, 77, 93, 73, 98, 81, 91, 51, 11000000, 2, false);
        Add("LAC", "Kris", "Dunn", "PG", 32, "USA", 191, 93, 78, 78, 85, 73, 63, 91, 87, 97, 61, 87, 83, 97, 34, 6000000, 2, false);
        Add("LAC", "Nicolas", "Batum", "PF", 38, "FRA", 203, 104, 76, 76, 72, 77, 85, 81, 79, 85, 75, 73, 93, 72, 44, 5000000, 1, false);
        Add("LAC", "Isaiah", "Jackson", "C", 24, "USA", 208, 93, 77, 85, 90, 74, 34, 66, 62, 90, 95, 97, 80, 64, 95, 8000000, 3, false);
        Add("LAC", "Jordan", "Miller", "SG", 25, "USA", 196, 88, 76, 82, 89, 82, 78, 80, 84, 80, 66, 87, 84, 74, 32, 2500000, 2, false);
        Add("LAC", "TyTy", "Washington Jr.", "PG", 24, "USA", 191, 89, 74, 80, 90, 81, 75, 90, 88, 69, 55, 83, 85, 73, 25, 2500000, 2, false);
        Add("LAC", "Cam", "Christie", "SG", 21, "USA", 198, 86, 73, 84, 86, 78, 84, 73, 78, 77, 63, 84, 78, 73, 29, 2200000, 4, false);

        // ── LAL ──
        Add("LAL", "Luka", "Doncic", "PG", 27, "SLO", 201, 104, 95, 97, 99, 99, 99, 99, 99, 89, 79, 89, 99, 99, 77, 55000000, 5, false);
        Add("LAL", "LeBron", "James", "SF", 42, "USA", 206, 113, 89, 89, 92, 99, 91, 99, 99, 91, 93, 97, 99, 82, 48, 52600000, 1, false);
        Add("LAL", "Austin", "Reaves", "SG", 28, "USA", 196, 89, 86, 88, 96, 99, 96, 99, 99, 82, 71, 92, 99, 78, 35, 30000000, 4, false);
        Add("LAL", "Deandre", "Ayton", "C", 28, "BAH", 213, 113, 84, 84, 89, 97, 52, 81, 78, 97, 99, 93, 95, 66, 77, 34000000, 2, false);
        Add("LAL", "Marcus", "Smart", "PG", 32, "USA", 193, 100, 81, 81, 85, 81, 75, 90, 86, 98, 65, 86, 94, 98, 33, 21000000, 2, false);
        Add("LAL", "Rui", "Hachimura", "PF", 28, "JPN", 203, 104, 80, 80, 87, 93, 83, 78, 83, 83, 81, 93, 89, 66, 44, 18000000, 2, false);
        Add("LAL", "Jarred", "Vanderbilt", "PF", 27, "USA", 203, 97, 79, 79, 90, 75, 53, 71, 77, 98, 84, 99, 83, 94, 45, 12000000, 3, false);
        Add("LAL", "Dalton", "Knecht", "SG", 25, "USA", 198, 96, 78, 87, 91, 93, 99, 80, 85, 74, 64, 85, 87, 68, 32, 4500000, 3, false);
        Add("LAL", "Jake", "LaRavia", "SF", 25, "USA", 201, 106, 77, 80, 84, 83, 83, 79, 81, 83, 73, 84, 85, 73, 39, 6000000, 2, false);
        Add("LAL", "Maxi", "Kleber", "PF", 34, "GER", 208, 109, 76, 76, 74, 80, 87, 72, 70, 86, 82, 76, 89, 66, 54, 11000000, 1, false);
        Add("LAL", "Nick", "Smith Jr.", "SG", 22, "USA", 188, 84, 76, 87, 95, 87, 85, 87, 89, 69, 55, 87, 85, 71, 26, 4500000, 3, false);
        Add("LAL", "Jaxson", "Hayes", "C", 26, "USA", 213, 100, 75, 80, 89, 78, 34, 62, 60, 86, 93, 97, 80, 60, 86, 3000000, 2, false);
        Add("LAL", "Drew", "Timme", "PF", 26, "USA", 208, 107, 74, 78, 75, 85, 68, 81, 79, 75, 89, 75, 93, 56, 38, 2200000, 2, false);
        Add("LAL", "Bronny", "James", "PG", 22, "USA", 191, 95, 70, 80, 85, 70, 66, 76, 78, 79, 54, 83, 76, 78, 25, 2500000, 3, false);
        Add("LAL", "Luke", "Kennard", "SG", 30, "USA", 196, 93, 76, 76, 87, 90, 99, 83, 88, 69, 59, 81, 90, 61, 29, 9000000, 2, false);

        // ── MEM ──
        Add("MEM", "Ja", "Morant", "PG", 27, "USA", 188, 79, 91, 93, 99, 99, 99, 99, 99, 93, 79, 99, 99, 98, 49, 44000000, 4, false);
        Add("MEM", "Taylor", "Hendricks", "PF", 23, "USA", 206, 97, 84, 92, 88, 86, 84, 77, 82, 94, 82, 96, 88, 80, 67, 11000000, 3, false);
        Add("MEM", "Zach", "Edey", "C", 24, "CAN", 224, 136, 83, 90, 71, 99, 47, 78, 75, 99, 99, 86, 99, 61, 99, 9000000, 3, false);
        Add("MEM", "GG", "Jackson", "SF", 21, "USA", 206, 98, 82, 93, 92, 92, 86, 79, 88, 83, 79, 96, 87, 71, 49, 6000000, 3, false);
        Add("MEM", "Kentavious", "Caldwell-Pope", "SG", 33, "USA", 196, 93, 80, 80, 88, 86, 90, 78, 82, 93, 66, 84, 88, 91, 34, 18000000, 2, false);
        Add("MEM", "Santi", "Aldama", "PF", 25, "ESP", 211, 98, 80, 85, 84, 87, 89, 80, 84, 82, 82, 85, 87, 68, 52, 12000000, 4, false);
        Add("MEM", "Ty", "Jerome", "PG", 29, "USA", 196, 88, 80, 80, 89, 93, 89, 98, 97, 73, 57, 83, 99, 77, 25, 9000000, 2, false);
        Add("MEM", "Jaylen", "Wells", "SG", 22, "USA", 201, 93, 79, 89, 89, 85, 87, 78, 83, 87, 68, 91, 83, 82, 36, 3500000, 3, false);
        Add("MEM", "Scotty", "Pippen Jr.", "PG", 25, "USA", 185, 84, 78, 84, 95, 84, 78, 93, 91, 80, 54, 87, 87, 84, 25, 5000000, 3, false);
        Add("MEM", "Cedric", "Coward", "SF", 22, "USA", 198, 94, 77, 88, 88, 81, 84, 73, 79, 84, 69, 90, 81, 79, 39, 4000000, 4, false);
        Add("MEM", "Cam", "Spencer", "SG", 26, "USA", 193, 93, 76, 80, 86, 86, 94, 81, 84, 75, 61, 81, 88, 71, 29, 2200000, 2, false);
        Add("MEM", "Rayan", "Rupert", "SF", 22, "FRA", 198, 88, 75, 88, 89, 73, 67, 71, 75, 89, 65, 93, 77, 85, 41, 3500000, 3, false);
        Add("MEM", "Olivier-Maxence", "Prosper", "SF", 24, "CAN", 201, 104, 75, 84, 87, 75, 67, 71, 75, 88, 69, 92, 79, 83, 39, 3200000, 3, false);
        Add("MEM", "Walter", "Clayton Jr.", "PG", 23, "USA", 188, 88, 74, 84, 90, 85, 89, 85, 87, 67, 51, 83, 83, 69, 25, 2800000, 4, false);

        // ── MIA ──
        Add("MIA", "Bam", "Adebayo", "C", 29, "USA", 206, 116, 90, 90, 88, 91, 78, 90, 91, 99, 97, 97, 99, 88, 72, 51000000, 4, false);
        Add("MIA", "Tyler", "Herro", "SG", 26, "USA", 196, 88, 87, 89, 99, 99, 99, 99, 99, 83, 70, 97, 99, 79, 34, 33000000, 4, false);
        Add("MIA", "Andrew", "Wiggins", "SF", 31, "CAN", 201, 89, 83, 83, 91, 89, 82, 80, 86, 91, 76, 97, 89, 84, 48, 28000000, 2, false);
        Add("MIA", "Norman", "Powell", "SG", 33, "USA", 193, 97, 82, 82, 96, 98, 94, 82, 92, 79, 67, 94, 94, 73, 33, 22000000, 2, false);
        Add("MIA", "Kel'el", "Ware", "C", 22, "USA", 213, 104, 82, 93, 83, 83, 73, 72, 70, 91, 97, 93, 85, 62, 93, 8000000, 3, false);
        Add("MIA", "Nikola", "Jovic", "PF", 23, "SRB", 208, 101, 80, 89, 85, 87, 87, 84, 87, 78, 80, 87, 89, 66, 50, 6000000, 3, false);
        Add("MIA", "Jaime", "Jaquez Jr.", "SF", 25, "USA", 198, 102, 80, 86, 86, 88, 78, 84, 90, 84, 73, 90, 92, 76, 39, 7000000, 3, false);
        Add("MIA", "Davion", "Mitchell", "PG", 28, "USA", 183, 92, 79, 79, 92, 80, 74, 90, 86, 98, 54, 86, 86, 98, 25, 9000000, 2, false);
        Add("MIA", "Kasparas", "Jakucionis", "PG", 20, "LIT", 196, 91, 78, 92, 89, 85, 87, 95, 93, 73, 59, 85, 89, 75, 28, 7000000, 4, false);
        Add("MIA", "Pelle", "Larsson", "SG", 24, "SWE", 196, 98, 76, 82, 85, 80, 82, 78, 82, 84, 66, 87, 82, 78, 32, 2500000, 3, false);
        Add("MIA", "Simone", "Fontecchio", "SF", 31, "ITA", 201, 95, 76, 76, 84, 86, 94, 78, 82, 76, 68, 82, 86, 68, 32, 8000000, 2, false);
        Add("MIA", "Keshad", "Johnson", "PF", 24, "USA", 201, 102, 74, 82, 86, 73, 65, 69, 72, 86, 72, 92, 78, 80, 41, 2000000, 3, false);
        Add("MIA", "Dru", "Smith", "PG", 28, "USA", 191, 92, 73, 73, 85, 74, 68, 84, 82, 84, 52, 82, 80, 87, 25, 2200000, 1, false);

        // ── MIL ──
        Add("MIL", "Giannis", "Antetokounmpo", "PF", 32, "GRE", 211, 110, 97, 97, 99, 99, 82, 99, 99, 99, 99, 99, 99, 97, 96, 62000000, 4, false);
        Add("MIL", "Myles", "Turner", "C", 30, "USA", 211, 113, 87, 87, 81, 93, 87, 77, 75, 99, 99, 89, 97, 61, 99, 32000000, 4, false);
        Add("MIL", "Kyle", "Kuzma", "SF", 31, "USA", 206, 100, 82, 82, 90, 94, 86, 82, 90, 82, 80, 92, 92, 71, 43, 22000000, 2, false);
        Add("MIL", "Bobby", "Portis", "PF", 32, "USA", 208, 113, 82, 82, 81, 97, 87, 78, 83, 85, 99, 85, 93, 64, 50, 13000000, 2, false);
        Add("MIL", "Kevin", "Porter Jr.", "PG", 26, "USA", 193, 92, 81, 84, 97, 93, 83, 95, 97, 72, 62, 95, 89, 76, 32, 10000000, 2, false);
        Add("MIL", "Gary", "Trent Jr.", "SG", 27, "USA", 196, 94, 80, 80, 92, 92, 94, 79, 86, 81, 65, 86, 88, 84, 33, 9000000, 2, false);
        Add("MIL", "AJ", "Green", "SG", 27, "USA", 193, 86, 78, 80, 90, 91, 99, 80, 86, 76, 64, 84, 90, 70, 28, 7000000, 3, false);
        Add("MIL", "Taurean", "Prince", "SF", 32, "USA", 198, 99, 77, 77, 83, 84, 90, 75, 79, 85, 69, 83, 85, 79, 35, 6000000, 1, false);
        Add("MIL", "Andre", "Jackson Jr.", "SG", 25, "USA", 198, 95, 77, 86, 90, 73, 63, 77, 81, 92, 69, 96, 81, 90, 35, 4000000, 3, false);
        Add("MIL", "Ryan", "Rollins", "PG", 24, "USA", 193, 82, 76, 84, 92, 85, 81, 90, 88, 73, 55, 87, 85, 75, 25, 3500000, 3, false);
        Add("MIL", "Ousmane", "Dieng", "SF", 23, "FRA", 208, 84, 76, 89, 85, 75, 73, 79, 81, 85, 69, 91, 79, 77, 42, 5000000, 3, false);
        Add("MIL", "Jericho", "Sims", "C", 28, "USA", 208, 113, 74, 76, 86, 72, 34, 62, 58, 88, 95, 95, 80, 58, 86, 3000000, 2, false);
        Add("MIL", "Gary", "Harris", "SG", 32, "USA", 193, 95, 73, 73, 83, 77, 81, 73, 77, 83, 59, 81, 83, 79, 27, 3000000, 1, false);
        Add("MIL", "Pete", "Nance", "PF", 26, "USA", 208, 102, 71, 76, 76, 76, 78, 68, 70, 78, 79, 76, 78, 58, 44, 1800000, 2, false);
        Add("MIL", "Thanasis", "Antetokounmpo", "SF", 34, "GRE", 201, 99, 68, 68, 87, 68, 40, 60, 64, 83, 68, 91, 74, 77, 36, 1200000, 1, false);

        // ── MIN ──
        Add("MIN", "Anthony", "Edwards", "SG", 25, "USA", 193, 102, 95, 97, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 66, 58000000, 5, false);
        Add("MIN", "Julius", "Randle", "PF", 31, "USA", 203, 113, 86, 86, 95, 97, 84, 87, 91, 89, 91, 95, 95, 72, 50, 33000000, 3, false);
        Add("MIN", "Rudy", "Gobert", "C", 34, "FRA", 216, 117, 84, 84, 80, 91, 42, 76, 72, 99, 99, 99, 99, 68, 99, 38000000, 2, false);
        Add("MIN", "Jaden", "McDaniels", "SF", 25, "USA", 206, 95, 84, 88, 91, 85, 81, 79, 83, 98, 77, 96, 89, 92, 53, 18000000, 4, false);
        Add("MIN", "Donte", "DiVincenzo", "SG", 29, "USA", 193, 92, 82, 82, 92, 92, 94, 87, 88, 85, 65, 89, 90, 89, 31, 12000000, 2, false);
        Add("MIN", "Naz", "Reid", "C", 27, "USA", 206, 113, 82, 88, 86, 93, 89, 80, 84, 84, 89, 86, 87, 66, 58, 16000000, 4, false);
        Add("MIN", "Mike", "Conley", "PG", 38, "USA", 185, 79, 80, 80, 84, 90, 91, 98, 97, 76, 52, 82, 95, 90, 25, 10000000, 1, false);
        Add("MIN", "Ayo", "Dosunmu", "PG", 26, "USA", 193, 91, 79, 82, 91, 88, 84, 89, 91, 82, 60, 86, 89, 84, 25, 8000000, 3, false);
        Add("MIN", "Kyle", "Anderson", "SF", 32, "USA", 206, 104, 78, 78, 75, 79, 77, 85, 87, 87, 81, 83, 87, 83, 34, 9000000, 1, false);
        Add("MIN", "Terrence", "Shannon Jr.", "SG", 25, "USA", 198, 98, 80, 90, 94, 88, 82, 80, 84, 82, 70, 92, 88, 80, 40, 6000000, 3, false);
        Add("MIN", "Jaylen", "Clark", "SG", 24, "USA", 193, 88, 76, 84, 88, 76, 68, 78, 80, 90, 64, 90, 80, 88, 34, 3000000, 3, false);
        Add("MIN", "Bones", "Hyland", "PG", 25, "USA", 188, 80, 75, 85, 95, 85, 91, 87, 89, 66, 52, 82, 83, 70, 25, 4000000, 2, false);
        Add("MIN", "Joe", "Ingles", "SF", 38, "AUS", 203, 102, 74, 74, 63, 79, 92, 88, 90, 81, 65, 71, 89, 71, 25, 3000000, 1, false);

        // ── NOP ──
        Add("NOP", "Zion", "Williamson", "PF", 26, "USA", 201, 129, 95, 96, 99, 99, 89, 98, 99, 99, 99, 99, 99, 88, 77, 60000000, 4, false);
        Add("NOP", "Dejounte", "Murray", "PG", 30, "USA", 193, 83, 85, 86, 97, 89, 81, 97, 95, 91, 68, 91, 95, 97, 34, 28000000, 3, false);
        Add("NOP", "Jordan", "Poole", "SG", 27, "USA", 193, 88, 82, 88, 99, 99, 97, 87, 95, 73, 55, 93, 95, 75, 34, 25000000, 3, false);
        Add("NOP", "Trey", "Murphy III", "SF", 26, "USA", 206, 95, 84, 90, 93, 95, 97, 81, 89, 87, 71, 95, 93, 81, 42, 16000000, 4, false);
        Add("NOP", "Herbert", "Jones", "SF", 27, "USA", 201, 95, 83, 83, 88, 85, 73, 83, 84, 99, 79, 99, 88, 98, 37, 14000000, 3, false);
        Add("NOP", "Saddiq", "Bey", "SF", 27, "USA", 203, 102, 81, 81, 89, 92, 92, 81, 87, 83, 73, 89, 89, 79, 37, 12000000, 2, false);
        Add("NOP", "Kevon", "Looney", "C", 30, "USA", 206, 111, 80, 80, 71, 83, 52, 79, 75, 97, 99, 85, 89, 59, 91, 12000000, 2, false);
        Add("NOP", "Yves", "Missi", "C", 22, "CMR", 213, 104, 80, 90, 86, 83, 41, 71, 67, 94, 99, 94, 86, 63, 96, 9000000, 4, false);
        Add("NOP", "Hunter", "Dickinson", "C", 25, "USA", 213, 118, 78, 84, 63, 85, 55, 77, 71, 89, 99, 81, 91, 54, 93, 6000000, 2, false);
        Add("NOP", "DeAndre", "Jordan", "C", 38, "USA", 211, 120, 74, 74, 57, 77, 27, 67, 65, 95, 98, 85, 93, 57, 93, 3000000, 1, false);
        Add("NOP", "Jordan", "Hawkins", "SG", 23, "USA", 193, 88, 78, 86, 96, 98, 99, 78, 86, 70, 56, 86, 88, 70, 31, 4000000, 3, false);
        Add("NOP", "Trey", "Alexander", "SG", 23, "USA", 193, 86, 76, 84, 94, 90, 88, 83, 86, 73, 55, 86, 85, 71, 25, 2500000, 4, false);
        Add("NOP", "Bryce", "McGowens", "SG", 24, "USA", 198, 90, 75, 84, 97, 89, 83, 79, 83, 72, 56, 85, 83, 70, 28, 2000000, 3, false);
        Add("NOP", "Jeremiah", "Fears", "PG", 20, "USA", 188, 82, 78, 92, 99, 90, 88, 94, 92, 68, 55, 88, 86, 72, 26, 5000000, 4, false);
        Add("NOP", "Derik", "Queen", "PF", 20, "USA", 208, 110, 77, 90, 79, 84, 71, 75, 77, 85, 90, 86, 86, 61, 53, 4000000, 4, false);

        // ── NYK ──
        Add("NYK", "Jalen", "Brunson", "PG", 29, "USA", 188, 86, 93, 93, 99, 99, 99, 99, 99, 95, 81, 99, 99, 99, 55, 48000000, 4, false);
        Add("NYK", "Karl-Anthony", "Towns", "C", 30, "USA", 213, 112, 92, 93, 93, 99, 99, 93, 95, 85, 93, 95, 99, 74, 54, 52000000, 4, false);
        Add("NYK", "Mikal", "Bridges", "SF", 30, "USA", 198, 95, 87, 87, 95, 92, 86, 84, 90, 99, 82, 99, 93, 97, 40, 30000000, 4, false);
        Add("NYK", "OG", "Anunoby", "SF", 29, "GBR", 201, 105, 87, 87, 94, 88, 83, 85, 88, 99, 85, 99, 94, 99, 43, 28000000, 4, false);
        Add("NYK", "Josh", "Hart", "SG", 31, "USA", 196, 97, 84, 84, 90, 86, 82, 86, 88, 96, 78, 94, 90, 92, 42, 18000000, 3, false);
        Add("NYK", "Mitchell", "Robinson", "C", 28, "USA", 213, 111, 83, 83, 89, 79, 38, 71, 68, 99, 99, 99, 99, 73, 99, 14000000, 3, false);
        Add("NYK", "Miles", "McBride", "PG", 26, "USA", 185, 88, 81, 81, 92, 86, 85, 90, 88, 94, 63, 88, 86, 92, 27, 10000000, 3, false);
        Add("NYK", "Jordan", "Clarkson", "SG", 33, "USA", 193, 88, 81, 81, 98, 99, 99, 88, 94, 67, 57, 87, 94, 75, 33, 14000000, 2, false);
        Add("NYK", "Tyler", "Kolek", "PG", 25, "USA", 188, 84, 78, 84, 91, 90, 88, 98, 97, 68, 50, 84, 91, 76, 25, 4000000, 3, false);
        Add("NYK", "Landry", "Shamet", "SG", 29, "USA", 193, 88, 76, 76, 89, 91, 99, 77, 85, 71, 59, 81, 89, 67, 28, 6000000, 1, false);
        Add("NYK", "Jeremy", "Sochan", "PF", 23, "USA", 203, 104, 82, 88, 89, 84, 72, 84, 86, 97, 78, 97, 88, 91, 36, 9000000, 3, false);
        Add("NYK", "Pacome", "Dadiet", "SF", 20, "FRA", 203, 93, 76, 88, 86, 79, 77, 75, 79, 84, 67, 90, 83, 79, 37, 3000000, 4, false);
        Add("NYK", "Kevin", "McCullar Jr.", "SF", 25, "USA", 198, 97, 75, 82, 85, 78, 70, 74, 78, 91, 66, 91, 81, 83, 28, 2500000, 3, false);
        Add("NYK", "Ariel", "Hukporti", "C", 23, "GER", 213, 110, 74, 84, 73, 71, 29, 63, 59, 93, 96, 94, 85, 57, 94, 2200000, 3, false);

        // ── OKC ──
        Add("OKC", "Shai", "Gilgeous-Alexander", "PG", 27, "CAN", 198, 88, 98, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 77, 62000000, 5, false);
        Add("OKC", "Jalen", "Williams", "SF", 25, "USA", 198, 95, 92, 94, 99, 99, 98, 94, 99, 99, 85, 99, 99, 98, 43, 38000000, 4, false);
        Add("OKC", "Chet", "Holmgren", "C", 24, "USA", 216, 97, 90, 92, 87, 94, 89, 83, 87, 96, 99, 93, 94, 69, 99, 36000000, 4, false);
        Add("OKC", "Isaiah", "Hartenstein", "C", 28, "GER", 213, 113, 84, 84, 79, 84, 61, 82, 79, 96, 98, 90, 92, 67, 96, 18000000, 3, false);
        Add("OKC", "Luguentz", "Dort", "SG", 27, "CAN", 193, 99, 84, 84, 95, 86, 76, 84, 85, 99, 70, 99, 91, 97, 42, 16000000, 3, false);
        Add("OKC", "Alex", "Caruso", "SG", 32, "USA", 196, 92, 82, 82, 88, 82, 76, 86, 88, 97, 72, 98, 92, 98, 25, 14000000, 2, false);
        Add("OKC", "Isaiah", "Joe", "SG", 27, "USA", 193, 88, 80, 80, 92, 98, 99, 86, 92, 76, 60, 82, 94, 74, 27, 8000000, 2, false);
        Add("OKC", "Aaron", "Wiggins", "SF", 27, "USA", 198, 93, 80, 82, 92, 88, 86, 80, 84, 88, 71, 90, 88, 84, 29, 7000000, 3, false);
        Add("OKC", "Cason", "Wallace", "PG", 22, "USA", 191, 88, 82, 88, 93, 85, 79, 91, 89, 94, 65, 93, 87, 93, 33, 9000000, 4, false);
        Add("OKC", "Kenrich", "Williams", "PF", 30, "USA", 198, 99, 78, 78, 78, 80, 74, 80, 82, 89, 78, 89, 85, 87, 36, 7000000, 2, false);
        Add("OKC", "Jaylin", "Williams", "C", 23, "USA", 206, 108, 78, 84, 71, 77, 71, 77, 79, 82, 82, 81, 82, 73, 83, 6000000, 3, false);
        Add("OKC", "Nikola", "Topic", "PG", 20, "SRB", 198, 88, 78, 92, 92, 86, 85, 98, 96, 73, 53, 85, 90, 71, 29, 5000000, 4, false);
        Add("OKC", "Jared", "McCain", "SG", 22, "USA", 191, 88, 80, 88, 97, 97, 99, 87, 91, 73, 57, 85, 93, 71, 30, 6000000, 4, false);
        Add("OKC", "Ajay", "Mitchell", "PG", 23, "BEL", 193, 86, 75, 84, 90, 83, 79, 86, 84, 79, 59, 83, 84, 73, 25, 2000000, 4, false);

        // ── ORL ──
        Add("ORL", "Paolo", "Banchero", "PF", 26, "USA", 208, 113, 92, 94, 99, 99, 93, 95, 98, 96, 94, 99, 99, 79, 61, 48000000, 5, false);
        Add("ORL", "Franz", "Wagner", "SF", 25, "GER", 206, 100, 88, 90, 95, 97, 94, 92, 95, 95, 78, 95, 95, 86, 46, 30000000, 4, false);
        Add("ORL", "Desmond", "Bane", "SG", 28, "USA", 196, 98, 88, 88, 99, 99, 99, 92, 99, 86, 72, 94, 99, 88, 41, 35000000, 4, false);
        Add("ORL", "Jalen", "Suggs", "PG", 25, "USA", 191, 93, 84, 86, 94, 88, 82, 90, 92, 96, 69, 94, 90, 94, 35, 22000000, 4, false);
        Add("ORL", "Wendell", "Carter Jr.", "C", 27, "USA", 208, 113, 84, 84, 78, 86, 76, 80, 78, 94, 95, 90, 90, 62, 95, 18000000, 3, false);
        Add("ORL", "Jonathan", "Isaac", "PF", 28, "USA", 208, 104, 82, 82, 84, 76, 66, 72, 76, 98, 86, 98, 84, 96, 66, 17000000, 3, false);
        Add("ORL", "Anthony", "Black", "PG", 22, "USA", 198, 95, 80, 86, 90, 82, 76, 88, 88, 92, 67, 90, 84, 90, 33, 9000000, 4, false);
        Add("ORL", "Jett", "Howard", "SG", 23, "USA", 198, 92, 78, 84, 91, 91, 99, 82, 87, 74, 62, 83, 89, 72, 28, 6000000, 4, false);
        Add("ORL", "Moritz", "Wagner", "C", 29, "GER", 211, 111, 80, 80, 83, 92, 87, 81, 85, 79, 88, 81, 88, 63, 53, 12000000, 2, false);
        Add("ORL", "Goga", "Bitadze", "C", 27, "GEO", 211, 113, 78, 78, 75, 81, 53, 71, 69, 91, 92, 89, 85, 59, 93, 8000000, 2, false);
        Add("ORL", "Tristan", "da Silva", "SF", 24, "GER", 206, 98, 78, 86, 84, 85, 88, 79, 83, 83, 71, 86, 85, 81, 33, 5000000, 4, false);
        Add("ORL", "Jevon", "Carter", "PG", 31, "USA", 188, 90, 76, 76, 85, 79, 79, 83, 85, 87, 56, 87, 81, 89, 25, 7000000, 1, false);
        Add("ORL", "Jamal", "Cain", "SF", 27, "USA", 198, 95, 74, 82, 86, 78, 73, 74, 78, 84, 67, 86, 80, 82, 26, 2000000, 2, false);

        // ── PHI ──
        Add("PHI", "Joel", "Embiid", "C", 32, "CAM", 213, 127, 92, 93, 99, 99, 98, 99, 99, 99, 99, 99, 99, 79, 87, 65000000, 5, false);
        Add("PHI", "Tyrese", "Maxey", "PG", 26, "USA", 188, 86, 92, 94, 99, 99, 99, 99, 99, 94, 74, 99, 99, 99, 52, 42000000, 5, false);
        Add("PHI", "Paul", "George", "SF", 36, "USA", 206, 100, 88, 88, 96, 98, 98, 87, 93, 99, 73, 98, 96, 99, 31, 35000000, 2, false);
        Add("PHI", "Kelly", "Oubre Jr.", "SF", 30, "USA", 198, 95, 80, 82, 93, 89, 86, 80, 84, 89, 66, 89, 87, 87, 30, 12000000, 2, false);
        Add("PHI", "Quentin", "Grimes", "SG", 26, "USA", 196, 92, 82, 84, 93, 93, 93, 82, 88, 88, 66, 90, 91, 88, 30, 9000000, 3, false);
        Add("PHI", "Kyle", "Lowry", "PG", 40, "USA", 183, 88, 78, 78, 79, 85, 87, 97, 97, 79, 50, 81, 91, 87, 25, 8000000, 1, false);
        Add("PHI", "VJ", "Edgecombe", "SG", 20, "BAH", 193, 90, 80, 92, 99, 91, 87, 84, 87, 80, 62, 89, 87, 84, 30, 6000000, 4, false);
        Add("PHI", "Justin", "Edwards", "SF", 22, "USA", 201, 95, 78, 88, 90, 87, 83, 77, 81, 87, 67, 89, 85, 85, 27, 3000000, 4, false);
        Add("PHI", "Dominick", "Barlow", "PF", 23, "USA", 206, 102, 76, 86, 84, 80, 72, 76, 78, 86, 78, 88, 82, 84, 28, 2500000, 3, false);
        Add("PHI", "Trendon", "Watford", "PF", 25, "USA", 203, 104, 78, 84, 86, 86, 78, 82, 84, 84, 75, 86, 86, 82, 29, 4000000, 2, false);
        Add("PHI", "Andre", "Drummond", "C", 32, "USA", 211, 127, 80, 80, 75, 85, 34, 67, 66, 99, 99, 99, 95, 62, 99, 5000000, 1, false);
        Add("PHI", "Adem", "Bona", "C", 22, "NGR", 208, 104, 78, 88, 80, 78, 48, 66, 68, 92, 96, 92, 86, 58, 94, 3000000, 4, false);
        Add("PHI", "Johni", "Broome", "C", 24, "USA", 206, 110, 76, 82, 72, 80, 56, 68, 70, 87, 91, 84, 85, 54, 89, 3000000, 3, false);
        Add("PHI", "MarJon", "Beauchamp", "SF", 25, "USA", 201, 95, 76, 84, 93, 81, 74, 76, 80, 85, 68, 89, 83, 81, 26, 2500000, 3, false);

        // ── PHX ──
        Add("PHX", "Devin", "Booker", "SG", 29, "USA", 198, 96, 91, 92, 99, 99, 99, 99, 99, 99, 93, 99, 99, 99, 61, 60000000, 5, false);
        Add("PHX", "Jalen", "Green", "SG", 24, "USA", 198, 89, 86, 90, 99, 99, 99, 94, 98, 78, 63, 96, 99, 80, 41, 35000000, 4, false);
        Add("PHX", "Mark", "Williams", "C", 25, "USA", 213, 118, 84, 86, 84, 88, 48, 74, 76, 99, 99, 96, 95, 66, 99, 22000000, 4, false);
        Add("PHX", "Dillon", "Brooks", "SF", 30, "CAN", 198, 102, 82, 82, 93, 87, 84, 82, 86, 95, 70, 95, 89, 93, 28, 20000000, 3, false);
        Add("PHX", "Grayson", "Allen", "SG", 30, "USA", 193, 90, 82, 82, 93, 97, 99, 87, 93, 83, 61, 85, 93, 85, 26, 15000000, 2, false);
        Add("PHX", "Royce", "O'Neale", "SF", 32, "USA", 198, 104, 80, 80, 85, 83, 87, 83, 87, 92, 71, 91, 87, 89, 25, 11000000, 2, false);
        Add("PHX", "Ryan", "Dunn", "SF", 23, "USA", 203, 98, 78, 88, 90, 80, 74, 76, 80, 92, 70, 94, 86, 90, 26, 5000000, 4, false);
        Add("PHX", "Khaman", "Maluach", "C", 20, "SUD", 218, 110, 78, 92, 75, 79, 48, 64, 65, 93, 99, 91, 89, 58, 97, 6000000, 4, false);
        Add("PHX", "Oso", "Ighodaro", "C", 23, "USA", 208, 104, 78, 86, 78, 76, 55, 69, 71, 90, 94, 88, 86, 61, 90, 4000000, 3, false);
        Add("PHX", "Jordan", "Goodwin", "PG", 27, "USA", 193, 92, 78, 78, 88, 82, 78, 86, 88, 88, 60, 89, 84, 90, 25, 4000000, 2, false);
        Add("PHX", "Collin", "Gillespie", "PG", 27, "USA", 185, 84, 76, 80, 88, 86, 88, 94, 92, 68, 51, 80, 88, 76, 25, 3000000, 2, false);
        Add("PHX", "Amir", "Coffey", "SG", 28, "USA", 198, 93, 76, 78, 88, 83, 81, 79, 83, 85, 59, 87, 83, 83, 25, 3000000, 1, false);

        // ── POR ──
        Add("POR", "Damian", "Lillard", "PG", 36, "USA", 188, 88, 92, 92, 99, 99, 99, 99, 99, 95, 75, 99, 99, 99, 50, 40000000, 2, false);
        Add("POR", "Scoot", "Henderson", "PG", 22, "USA", 191, 90, 88, 92, 99, 99, 94, 99, 99, 83, 67, 98, 99, 88, 43, 12000000, 4, false);
        Add("POR", "Shaedon", "Sharpe", "SG", 23, "CAN", 198, 93, 90, 94, 99, 99, 99, 98, 99, 90, 75, 99, 99, 88, 45, 18000000, 4, false);
        Add("POR", "Jrue", "Holiday", "PG", 35, "USA", 193, 95, 84, 84, 91, 87, 81, 95, 95, 95, 69, 96, 95, 95, 25, 25000000, 2, false);
        Add("POR", "Jerami", "Grant", "PF", 32, "USA", 206, 102, 84, 84, 93, 93, 90, 86, 90, 93, 78, 91, 91, 89, 30, 28000000, 3, false);
        Add("POR", "Deni", "Avdija", "SF", 26, "ISR", 206, 100, 84, 86, 92, 88, 82, 88, 90, 95, 80, 93, 92, 94, 30, 16000000, 3, false);
        Add("POR", "Toumani", "Camara", "SF", 25, "BEL", 198, 98, 82, 86, 91, 85, 78, 83, 85, 97, 76, 95, 89, 95, 28, 8000000, 3, false);
        Add("POR", "Donovan", "Clingan", "C", 22, "USA", 218, 120, 84, 86, 78, 90, 39, 75, 76, 99, 99, 99, 99, 71, 99, 9000000, 4, false);
        Add("POR", "Robert", "Williams III", "C", 28, "USA", 208, 108, 82, 82, 86, 82, 41, 70, 69, 99, 99, 98, 94, 65, 99, 12000000, 2, false);
        Add("POR", "Matisse", "Thybulle", "SG", 29, "AUS", 196, 95, 80, 80, 94, 80, 70, 76, 80, 99, 69, 99, 88, 99, 26, 9000000, 2, false);
        Add("POR", "Blake", "Wesley", "PG", 23, "USA", 193, 86, 78, 84, 93, 85, 80, 84, 85, 87, 58, 89, 86, 86, 25, 3000000, 3, false);
        Add("POR", "Vit", "Krejci", "SG", 26, "CZE", 198, 88, 76, 78, 87, 87, 88, 83, 85, 79, 57, 83, 85, 77, 25, 4000000, 2, false);
        Add("POR", "Caleb", "Love", "PG", 24, "USA", 188, 84, 76, 86, 97, 91, 93, 83, 87, 70, 50, 83, 85, 71, 26, 2500000, 4, false);
        Add("POR", "Kris", "Murray", "SF", 25, "USA", 203, 98, 76, 84, 85, 83, 79, 77, 81, 87, 69, 85, 83, 81, 26, 3000000, 3, false);

        // ── SAC ──
        Add("SAC", "Domantas", "Sabonis", "C", 30, "LTU", 211, 115, 90, 90, 89, 98, 87, 96, 98, 89, 91, 92, 98, 71, 81, 48000000, 5, false);
        Add("SAC", "Zach", "LaVine", "SG", 31, "USA", 196, 91, 88, 90, 99, 99, 99, 98, 99, 83, 65, 99, 99, 85, 43, 40000000, 4, false);
        Add("SAC", "DeMar", "DeRozan", "SF", 36, "USA", 201, 100, 86, 86, 99, 99, 98, 92, 98, 82, 69, 96, 99, 79, 35, 25000000, 2, false);
        Add("SAC", "Malik", "Monk", "SG", 28, "USA", 191, 90, 85, 88, 99, 99, 99, 93, 98, 75, 61, 94, 99, 81, 37, 18000000, 3, false);
        Add("SAC", "Keegan", "Murray", "SF", 26, "USA", 203, 102, 84, 86, 90, 92, 94, 84, 88, 92, 77, 92, 92, 90, 33, 22000000, 4, false);
        Add("SAC", "De'Andre", "Hunter", "SF", 28, "USA", 203, 102, 83, 84, 89, 91, 85, 81, 85, 96, 77, 94, 91, 93, 31, 20000000, 3, false);
        Add("SAC", "Russell", "Westbrook", "PG", 37, "USA", 191, 91, 82, 82, 99, 93, 79, 98, 99, 75, 59, 93, 94, 87, 26, 8000000, 1, false);
        Add("SAC", "Devin", "Carter", "PG", 23, "USA", 188, 86, 80, 86, 94, 86, 80, 90, 88, 88, 61, 90, 86, 90, 27, 6000000, 4, false);
        Add("SAC", "Doug", "McDermott", "SF", 34, "USA", 201, 102, 78, 78, 90, 99, 99, 80, 88, 73, 59, 78, 94, 71, 27, 5000000, 1, false);
        Add("SAC", "Patrick", "Baldwin Jr.", "SF", 23, "USA", 206, 100, 76, 84, 85, 87, 89, 76, 80, 82, 64, 83, 85, 80, 25, 3000000, 3, false);
        Add("SAC", "Precious", "Achiuwa", "PF", 27, "NGR", 203, 104, 80, 82, 91, 84, 70, 78, 80, 93, 86, 93, 87, 86, 32, 9000000, 2, false);
        Add("SAC", "Drew", "Eubanks", "C", 29, "USA", 208, 111, 78, 78, 77, 81, 53, 69, 71, 90, 92, 89, 87, 57, 92, 5000000, 2, false);
        Add("SAC", "Killian", "Hayes", "PG", 25, "FRA", 196, 88, 76, 80, 86, 79, 73, 86, 88, 85, 61, 87, 81, 85, 25, 4000000, 2, false);
        Add("SAC", "Maxime", "Raynaud", "C", 23, "FRA", 213, 110, 76, 84, 72, 79, 73, 70, 73, 81, 87, 79, 81, 54, 87, 3000000, 4, false);

        // ── SAS ──
        Add("SAS", "Victor", "Wembanyama", "C", 22, "FRA", 224, 104, 94, 99, 90, 99, 86, 92, 95, 99, 99, 99, 99, 99, 99, 60000000, 5, false);
        Add("SAS", "De'Aaron", "Fox", "PG", 28, "USA", 191, 84, 90, 92, 99, 99, 99, 99, 99, 86, 68, 99, 99, 96, 47, 45000000, 5, false);
        Add("SAS", "Stephon", "Castle", "SG", 21, "USA", 198, 96, 84, 88, 96, 92, 84, 90, 92, 88, 69, 92, 92, 90, 39, 9000000, 4, false);
        Add("SAS", "Dylan", "Harper", "SG", 20, "USA", 196, 92, 82, 90, 97, 93, 88, 88, 90, 84, 62, 90, 90, 86, 34, 7000000, 4, false);
        Add("SAS", "Devin", "Vassell", "SG", 27, "USA", 198, 94, 84, 86, 95, 95, 94, 86, 90, 90, 68, 92, 94, 88, 32, 22000000, 4, false);
        Add("SAS", "Keldon", "Johnson", "SF", 26, "USA", 198, 100, 82, 84, 94, 94, 88, 84, 88, 86, 71, 90, 92, 84, 31, 18000000, 3, false);
        Add("SAS", "Harrison", "Barnes", "SF", 34, "USA", 203, 102, 80, 80, 86, 89, 87, 84, 87, 88, 72, 88, 88, 86, 25, 18000000, 2, false);
        Add("SAS", "Julian", "Champagnie", "SF", 26, "USA", 203, 98, 78, 82, 87, 89, 91, 79, 83, 85, 65, 85, 85, 83, 26, 6000000, 2, false);
        Add("SAS", "Luke", "Kornet", "C", 30, "USA", 216, 113, 78, 76, 72, 80, 57, 69, 71, 90, 94, 86, 88, 57, 94, 5000000, 2, false);
        Add("SAS", "Mason", "Plumlee", "C", 36, "USA", 211, 115, 76, 74, 70, 74, 57, 67, 69, 88, 92, 84, 86, 57, 92, 4000000, 1, false);
        Add("SAS", "Kelly", "Olynyk", "PF", 35, "CAN", 211, 108, 78, 78, 77, 88, 86, 83, 86, 81, 77, 81, 87, 75, 37, 12000000, 2, false);
        Add("SAS", "Bismack", "Biyombo", "C", 33, "COD", 206, 116, 76, 72, 72, 70, 31, 61, 62, 98, 99, 96, 92, 57, 98, 5000000, 1, false);
        Add("SAS", "Harrison", "Ingram", "SF", 24, "USA", 201, 95, 74, 82, 85, 79, 78, 76, 79, 83, 64, 83, 81, 81, 25, 2500000, 3, false);

        // ── TOR ──
        Add("TOR", "Scottie", "Barnes", "PF", 25, "USA", 206, 103, 92, 92, 96, 96, 91, 95, 96, 99, 95, 99, 98, 96, 51, 52000000, 5, false);
        Add("TOR", "Brandon", "Ingram", "SF", 28, "USA", 206, 98, 88, 90, 99, 99, 99, 92, 96, 86, 68, 98, 99, 88, 44, 38000000, 4, false);
        Add("TOR", "Immanuel", "Quickley", "PG", 27, "USA", 188, 86, 86, 88, 99, 99, 99, 97, 99, 78, 60, 94, 97, 86, 38, 25000000, 4, false);
        Add("TOR", "RJ", "Barrett", "SF", 26, "CAN", 201, 100, 84, 86, 97, 97, 91, 86, 89, 86, 70, 93, 95, 88, 32, 20000000, 3, false);
        Add("TOR", "Jakob", "Poeltl", "C", 30, "AUT", 213, 114, 84, 82, 79, 85, 59, 79, 81, 97, 98, 95, 93, 61, 97, 16000000, 2, false);
        Add("TOR", "Gradey", "Dick", "SG", 23, "USA", 198, 90, 82, 86, 96, 99, 99, 86, 92, 78, 60, 88, 96, 76, 32, 8000000, 4, false);
        Add("TOR", "Ja'Kobe", "Walter", "SG", 21, "USA", 196, 92, 80, 86, 94, 92, 90, 83, 87, 81, 61, 88, 90, 83, 31, 6000000, 4, false);
        Add("TOR", "Jamal", "Shead", "PG", 24, "CAN", 183, 84, 78, 82, 95, 87, 86, 95, 93, 70, 52, 86, 87, 82, 25, 3000000, 3, false);
        Add("TOR", "Jonathan", "Mogbo", "PF", 23, "USA", 203, 102, 78, 84, 88, 82, 74, 78, 80, 91, 78, 90, 86, 86, 25, 2500000, 4, false);
        Add("TOR", "Collin", "Murray-Boyles", "PF", 22, "USA", 203, 104, 78, 86, 86, 82, 72, 78, 80, 89, 80, 91, 86, 88, 26, 3000000, 4, false);
        Add("TOR", "Trayce", "Jackson-Davis", "C", 26, "USA", 206, 111, 80, 84, 82, 84, 56, 72, 74, 91, 93, 90, 88, 58, 92, 5000000, 3, false);
        Add("TOR", "Sandro", "Mamukelashvili", "PF", 27, "GEO", 208, 104, 78, 80, 82, 88, 84, 80, 84, 86, 80, 84, 86, 78, 26, 4000000, 2, false);
        Add("TOR", "A.J.", "Lawson", "SG", 26, "CAN", 196, 90, 76, 80, 93, 83, 81, 79, 81, 83, 62, 85, 83, 81, 25, 2000000, 2, false);
        Add("TOR", "Jamison", "Battle", "SF", 25, "USA", 198, 95, 76, 82, 87, 87, 90, 77, 81, 79, 61, 83, 85, 81, 25, 2000000, 3, false);

        // ── UTA ──
        Add("UTA", "Lauri", "Markkanen", "PF", 29, "FIN", 213, 109, 90, 92, 99, 99, 99, 94, 99, 92, 76, 97, 99, 90, 46, 42000000, 4, false);
        Add("UTA", "Jaren", "Jackson Jr.", "PF", 27, "USA", 211, 110, 92, 90, 88, 92, 86, 82, 86, 99, 99, 97, 96, 88, 99, 48000000, 5, false);
        Add("UTA", "Walker", "Kessler", "C", 25, "USA", 213, 118, 84, 80, 76, 85, 44, 72, 76, 99, 99, 99, 99, 76, 99, 18000000, 4, false);
        Add("UTA", "Jusuf", "Nurkic", "C", 31, "BIH", 213, 127, 82, 78, 69, 85, 52, 77, 81, 93, 97, 93, 95, 63, 97, 16000000, 2, false);
        Add("UTA", "Keyonte", "George", "PG", 22, "USA", 193, 88, 84, 88, 99, 98, 94, 96, 97, 76, 58, 92, 94, 86, 34, 8000000, 4, false);
        Add("UTA", "Isaiah", "Collier", "PG", 21, "USA", 193, 92, 82, 90, 99, 92, 86, 93, 93, 78, 60, 90, 91, 86, 34, 7000000, 4, false);
        Add("UTA", "Ace", "Bailey", "SF", 19, "USA", 203, 96, 84, 94, 98, 94, 90, 85, 89, 87, 71, 92, 90, 89, 39, 9000000, 5, false);
        Add("UTA", "Brice", "Sensabaugh", "SF", 22, "USA", 201, 102, 80, 86, 94, 98, 99, 82, 88, 76, 59, 86, 92, 75, 31, 4000000, 4, false);
        Add("UTA", "Cody", "Williams", "SF", 21, "USA", 203, 95, 78, 88, 92, 86, 82, 78, 82, 86, 66, 88, 86, 86, 26, 5000000, 4, false);
        Add("UTA", "Kyle", "Filipowski", "PF", 22, "USA", 211, 110, 82, 86, 79, 86, 82, 77, 81, 84, 82, 84, 86, 75, 86, 6000000, 4, false);
        Add("UTA", "Oscar", "Tshiebwe", "C", 25, "COD", 206, 120, 78, 78, 70, 79, 30, 70, 72, 95, 99, 93, 91, 62, 97, 3000000, 3, false);
        Add("UTA", "Kevin", "Love", "PF", 37, "USA", 203, 113, 78, 76, 71, 88, 89, 85, 89, 81, 79, 81, 89, 73, 33, 8000000, 1, false);
        Add("UTA", "Svi", "Mykhailiuk", "SG", 28, "UKR", 201, 92, 78, 80, 93, 93, 97, 83, 87, 73, 57, 85, 91, 73, 26, 5000000, 2, false);
        Add("UTA", "John", "Konchar", "SG", 30, "USA", 196, 95, 78, 78, 85, 81, 81, 83, 85, 89, 68, 87, 85, 89, 25, 6000000, 2, false);

        // ── WAS ──
        Add("WAS", "Trae", "Young", "PG", 27, "USA", 185, 82, 94, 94, 99, 99, 99, 99, 99, 96, 78, 99, 99, 99, 68, 52000000, 5, false);
        Add("WAS", "D'Angelo", "Russell", "PG", 30, "USA", 193, 88, 84, 86, 97, 98, 99, 97, 99, 75, 59, 89, 95, 83, 33, 25000000, 2, false);
        Add("WAS", "Anthony", "Davis", "PF", 33, "USA", 208, 115, 92, 90, 84, 91, 78, 88, 90, 99, 99, 97, 97, 90, 99, 50000000, 4, false);
        Add("WAS", "Alex", "Sarr", "C", 21, "FRA", 213, 100, 86, 92, 83, 85, 75, 79, 81, 90, 94, 91, 89, 85, 94, 12000000, 5, false);
        Add("WAS", "Bilal", "Coulibaly", "SF", 22, "FRA", 201, 95, 84, 90, 95, 89, 84, 85, 87, 95, 78, 95, 91, 93, 32, 9000000, 4, false);
        Add("WAS", "Cam", "Whitmore", "SF", 22, "USA", 198, 100, 86, 92, 98, 98, 92, 86, 90, 92, 70, 96, 94, 92, 38, 8000000, 4, false);
        Add("WAS", "Jaden", "Hardy", "SG", 23, "USA", 193, 90, 82, 88, 99, 99, 95, 88, 91, 76, 60, 89, 93, 80, 32, 6000000, 3, false);
        Add("WAS", "Tre", "Johnson", "SG", 20, "USA", 196, 88, 82, 90, 99, 96, 98, 85, 88, 77, 61, 90, 92, 83, 33, 5000000, 4, false);
        Add("WAS", "Kyshawn", "George", "SF", 21, "CAN", 203, 94, 80, 88, 92, 88, 90, 82, 86, 86, 66, 88, 86, 88, 28, 5000000, 4, false);
        Add("WAS", "Will", "Riley", "SF", 20, "USA", 201, 92, 78, 88, 94, 86, 88, 80, 84, 82, 63, 86, 84, 84, 27, 4000000, 4, false);
        Add("WAS", "Bub", "Carrington", "PG", 21, "USA", 193, 86, 78, 86, 97, 89, 85, 89, 91, 72, 54, 85, 87, 81, 28, 4000000, 4, false);
        Add("WAS", "Sharife", "Cooper", "PG", 25, "USA", 183, 82, 74, 84, 94, 84, 81, 88, 86, 67, 49, 81, 82, 77, 25, 2000000, 2, false);
        Add("WAS", "Justin", "Champagnie", "SF", 25, "USA", 198, 95, 78, 82, 88, 86, 84, 78, 82, 89, 68, 88, 84, 86, 25, 3000000, 2, false);
        Add("WAS", "Anthony", "Gill", "PF", 32, "USA", 203, 104, 76, 78, 78, 82, 80, 76, 80, 88, 75, 86, 84, 82, 25, 4000000, 1, false);
        Add("WAS", "Tristan", "Vukcevic", "C", 22, "SRB", 213, 108, 80, 86, 76, 85, 82, 74, 78, 80, 82, 82, 84, 72, 85, 5000000, 4, false);

        _db.BeginTransaction();
        try
        {
            foreach (var p in players)
                _db.Insert(p);
            _db.Commit();
            Debug.Log($"[DB] {players.Count} jugadores insertados.");
        }
        catch (System.Exception e)
        {
            _db.Rollback();
            Debug.LogError($"[DB] Error insertando jugadores: {e.Message}");
        }
    }

    int[] GeneratePositionAttrs(int ovr, string pos, string seed)
    {
        // Base positional profiles for a reference 90-OVR player
        // Order: speed, shooting, three_point, passing, dribbling,
        //        defense, rebounding, athleticism, iq, steals, blocks
        int[] profile = pos switch
        {
            "PG" => new[] { 94, 88, 92, 97, 97, 78, 62, 82, 95, 80, 50 },
            "SG" => new[] { 92, 95, 97, 85, 88, 80, 58, 85, 88, 82, 50 },
            "SF" => new[] { 88, 88, 88, 82, 80, 90, 72, 92, 85, 86, 62 },
            "PF" => new[] { 78, 78, 72, 72, 65, 95, 95, 90, 80, 72, 82 },
            "C"  => new[] { 68, 72, 55, 62, 55, 98, 99, 85, 78, 65, 96 },
            _    => new[] { 85, 85, 85, 85, 85, 85, 85, 85, 85, 85, 85 }
        };

        var rng = new System.Random(seed.GetHashCode());
        int[] result = new int[11];
        int sum = 0;

        for (int i = 0; i < 11; i++)
        {
            float ratio = ovr / 90f;
            int raw = Mathf.RoundToInt(profile[i] * ratio);
            raw += rng.Next(-2, 3);

            int floor = Mathf.Max(25, ovr - 22);
            raw = Mathf.Clamp(raw, floor, 99);
            result[i] = raw;
            sum += raw;
        }

        // Escalado proporcional: preserva perfil posicional, promedio = ovr
        int targetSum = ovr * 11;
        float factor = (float)targetSum / sum;
        int newSum = 0;
        for (int i = 0; i < 11; i++)
        {
            int scaled = Mathf.RoundToInt(result[i] * factor);
            int floor = Mathf.Max(25, ovr - 22);
            result[i] = Mathf.Clamp(scaled, floor, 99);
            newSum += result[i];
        }

        // Ajustar resto por redondeo/clamp
        int remaining = targetSum - newSum;
        if (remaining > 0)
        {
            var candidates = result.Select((v, i) => new { v, i })
                .Where(x => x.v < 99).OrderByDescending(x => x.v).ToList();
            foreach (var c in candidates)
            {
                if (remaining <= 0) break;
                int add = Mathf.Min(remaining, 99 - c.v);
                result[c.i] += add;
                remaining -= add;
            }
        }
        else if (remaining < 0)
        {
            var candidates = result.Select((v, i) => new { v, i })
                .Where(x => x.v > Mathf.Max(25, ovr - 22)).OrderBy(x => x.v).ToList();
            foreach (var c in candidates)
            {
                if (remaining >= 0) break;
                int sub = Mathf.Min(-remaining, c.v - Mathf.Max(25, ovr - 22));
                result[c.i] -= sub;
                remaining += sub;
            }
        }

        return result;
    }

    public void SeedFreeAgents()
    {
        var freeAgents = new System.Collections.Generic.List<PlayerData>();

        // Cada agente libre: (fn, ln, pos, age, nat, h, w, ovr, pot, spd, sht, thr, pas,
        //                      drb, def, reb, ath, iq, stl, blk, sal, yrs)
        void AddFA(string fn, string ln, string pos, int age, string nat,
                    int h, int w, int ovr, int pot, int spd, int sht, int thr, int pas,
                    int drb, int def, int reb, int ath, int iq, int stl, int blk,
                    long sal, int yrs)
        {
            var attrs = GeneratePositionAttrs(ovr, pos, fn + ln);
            int calcOvr = (int)System.Math.Round(attrs.Average());
            if (calcOvr > pot) calcOvr = pot;

            freeAgents.Add(new PlayerData
            {
                team_id = 0,
                first_name = fn,
                last_name = ln,
                position = pos,
                age = age,
                nationality = nat,
                height_cm = h,
                weight_kg = w,
                overall = calcOvr,
                potential = pot,
                speed = attrs[0],
                shooting = attrs[1],
                three_point = attrs[2],
                passing = attrs[3],
                dribbling = attrs[4],
                defense = attrs[5],
                rebounding = attrs[6],
                athleticism = attrs[7],
                iq = attrs[8],
                steals = attrs[9],
                blocks = attrs[10],
                salary = sal,
                contract_years = yrs,
                is_rookie = 0,
                seasons_with_team = 0,
                injury_days = 0,
                injury_type = "",
                treated = 0
            });
        }
        // ── Free Agents ──
        AddFA("Tyson", "Etienne", "SG", 26, "USA", 188, 86, 76, 82, 84, 78, 74, 76, 72, 60, 44, 74, 76, 70, 10, 2000000, 2);
        AddFA("Chaney", "Johnson", "PF", 24, "USA", 201, 98, 74, 82, 78, 76, 70, 72, 74, 80, 62, 78, 76, 74, 10, 2000000, 2);
        AddFA("Trevon", "Scott", "SF", 27, "USA", 198, 95, 74, 80, 78, 74, 70, 72, 74, 76, 56, 76, 74, 72, 8, 1500000, 1);
        AddFA("Malachi", "Smith", "PG", 25, "USA", 185, 82, 76, 84, 84, 78, 76, 80, 78, 60, 42, 74, 76, 70, 10, 2000000, 2);
        AddFA("Tosan", "Evbuomwan", "SF", 24, "GBR", 201, 95, 76, 82, 80, 76, 72, 74, 76, 78, 58, 78, 76, 76, 10, 2000000, 2);
        AddFA("Sion", "James", "SG", 23, "USA", 196, 92, 76, 84, 82, 76, 74, 74, 76, 74, 52, 76, 74, 74, 10, 2000000, 2);
        AddFA("Antonio", "Reeves", "SG", 24, "USA", 193, 90, 78, 86, 80, 82, 84, 70, 74, 60, 44, 74, 78, 68, 12, 2500000, 3);
        AddFA("PJ", "Hall", "PF", 24, "USA", 206, 110, 78, 84, 76, 78, 70, 72, 74, 82, 70, 80, 78, 78, 14, 3000000, 3);
        AddFA("Mouhamadou", "Gueye", "PF", 26, "USA", 208, 102, 74, 82, 78, 72, 64, 70, 72, 82, 74, 82, 76, 80, 10, 2000000, 2);
        AddFA("Yuki", "Kawamura", "PG", 23, "JPN", 173, 72, 74, 84, 88, 74, 76, 82, 80, 56, 36, 74, 74, 68, 10, 2000000, 2);
        AddFA("Mac", "McClung", "SG", 26, "USA", 188, 84, 76, 86, 90, 78, 80, 80, 78, 58, 40, 76, 76, 72, 10, 2000000, 1);
        AddFA("Lachlan", "Olbrich", "PF", 24, "AUS", 206, 104, 72, 80, 74, 72, 66, 70, 72, 80, 72, 78, 74, 76, 8, 1500000, 2);
        AddFA("Olivier", "Sarr", "C", 26, "FRA", 213, 108, 78, 84, 70, 78, 70, 70, 74, 82, 80, 80, 78, 82, 14, 3000000, 2);
        AddFA("Tristan", "Enaruna", "SF", 25, "USA", 203, 98, 74, 82, 78, 74, 70, 72, 74, 78, 60, 78, 76, 76, 10, 2000000, 2);
        AddFA("Riley", "Minix", "SF", 24, "USA", 198, 94, 74, 82, 80, 74, 72, 72, 74, 76, 58, 76, 74, 74, 10, 2000000, 2);
        AddFA("Nae'Qwan", "Tomlin", "PF", 25, "USA", 206, 104, 74, 82, 78, 72, 66, 70, 72, 80, 70, 80, 76, 78, 10, 2000000, 2);
        AddFA("Dwight", "Powell", "C", 34, "CAN", 208, 108, 76, 78, 68, 74, 60, 72, 74, 82, 82, 80, 78, 84, 8, 4000000, 1);
        AddFA("Moussa", "Cisse", "C", 23, "MLI", 211, 102, 74, 82, 70, 70, 40, 60, 62, 84, 86, 82, 80, 86, 10, 2000000, 2);
        AddFA("John", "Poulakidas", "SG", 24, "USA", 198, 92, 74, 82, 76, 78, 80, 72, 74, 70, 50, 74, 74, 72, 10, 2000000, 2);
        AddFA("Spencer", "Jones", "SF", 24, "USA", 206, 100, 74, 82, 78, 78, 80, 72, 74, 76, 58, 76, 76, 74, 10, 2000000, 2);
        AddFA("Curtis", "Jones", "SG", 25, "USA", 193, 90, 76, 84, 84, 78, 76, 78, 76, 60, 44, 74, 76, 72, 10, 2000000, 2);
        AddFA("KJ", "Simpson", "PG", 23, "USA", 185, 84, 78, 86, 88, 80, 78, 82, 80, 58, 40, 76, 76, 74, 12, 2500000, 3);
        AddFA("Isaac", "Jones", "PF", 25, "USA", 206, 102, 76, 84, 78, 74, 66, 70, 72, 82, 72, 82, 78, 80, 10, 2000000, 2);
        AddFA("Daniss", "Jenkins", "PG", 24, "USA", 188, 84, 76, 84, 86, 78, 76, 80, 78, 60, 42, 74, 76, 72, 10, 2000000, 2);
        AddFA("Tolu", "Smith", "C", 24, "USA", 208, 112, 76, 84, 70, 74, 50, 66, 68, 84, 86, 82, 80, 84, 10, 2000000, 2);
        AddFA("Will", "Richard", "SG", 23, "USA", 196, 92, 76, 84, 82, 78, 76, 74, 76, 74, 54, 76, 76, 74, 10, 2000000, 2);
        AddFA("LJ", "Cryer", "SG", 24, "USA", 193, 88, 78, 86, 80, 82, 84, 72, 74, 60, 44, 74, 78, 68, 12, 2500000, 3);
        AddFA("Malevy", "Leons", "PF", 25, "NED", 206, 100, 74, 82, 76, 74, 70, 72, 74, 82, 70, 80, 76, 80, 10, 2000000, 2);
        AddFA("Pat", "Spencer", "SG", 29, "USA", 196, 95, 74, 80, 82, 76, 78, 78, 80, 72, 50, 78, 76, 78, 8, 1500000, 1);
        AddFA("Nate", "Williams", "SG", 26, "USA", 196, 92, 74, 80, 84, 76, 74, 74, 76, 74, 52, 76, 74, 74, 8, 1500000, 1);
        AddFA("Keshon", "Gilbert", "PG", 23, "USA", 185, 84, 76, 84, 86, 78, 76, 80, 78, 58, 40, 74, 76, 72, 10, 2000000, 2);
        AddFA("JD", "Davison", "PG", 23, "USA", 183, 82, 78, 86, 90, 78, 76, 82, 80, 60, 42, 76, 76, 74, 12, 2500000, 3);
        AddFA("Isaiah", "Crawford", "SF", 24, "USA", 198, 94, 74, 82, 78, 74, 72, 72, 74, 78, 58, 78, 76, 76, 10, 2000000, 2);
        AddFA("Jalen", "Slawson", "SF", 25, "USA", 201, 98, 74, 82, 80, 74, 72, 72, 74, 80, 60, 80, 76, 78, 10, 2000000, 2);
        AddFA("Micah", "Potter", "C", 26, "USA", 208, 110, 74, 80, 68, 74, 66, 70, 72, 82, 80, 80, 76, 84, 8, 2000000, 1);
        AddFA("Taelon", "Peter", "SG", 23, "USA", 196, 90, 74, 82, 82, 76, 74, 72, 74, 74, 52, 76, 74, 74, 8, 1500000, 2);
        AddFA("Ethan", "Thompson", "SG", 25, "USA", 196, 92, 74, 80, 82, 76, 74, 72, 74, 74, 52, 76, 74, 74, 8, 1500000, 2);
        AddFA("Adou", "Thiero", "SF", 21, "USA", 201, 94, 78, 88, 86, 78, 74, 76, 78, 82, 62, 82, 78, 80, 16, 3000000, 4);
        AddFA("Chris", "Manon", "SG", 24, "USA", 193, 88, 74, 80, 82, 74, 72, 72, 74, 74, 50, 76, 74, 74, 8, 1500000, 2);
        AddFA("Yanic", "Konan Niederhauser", "C", 23, "SUI", 213, 108, 78, 86, 72, 76, 60, 68, 72, 84, 82, 82, 80, 86, 12, 2500000, 3);
        AddFA("Norchad", "Omier", "PF", 24, "NCA", 203, 104, 78, 86, 78, 74, 66, 70, 72, 84, 74, 82, 78, 80, 12, 2500000, 3);
        AddFA("Sean", "Pedulla", "PG", 23, "USA", 183, 82, 76, 84, 86, 78, 76, 80, 78, 58, 40, 74, 76, 72, 10, 2000000, 2);
        AddFA("Kobe", "Sanders", "SG", 24, "USA", 196, 92, 76, 82, 82, 76, 74, 74, 76, 74, 52, 76, 74, 74, 8, 1500000, 2);
        AddFA("Dariq", "Whitehead", "SF", 22, "USA", 198, 92, 80, 90, 84, 82, 80, 74, 76, 78, 56, 80, 78, 78, 18, 4000000, 4);
        AddFA("Jahmai", "Mashack", "SG", 23, "USA", 196, 94, 76, 84, 84, 74, 70, 76, 78, 82, 58, 82, 76, 80, 10, 2000000, 2);
        AddFA("Javon", "Small", "PG", 23, "USA", 183, 82, 76, 84, 88, 78, 76, 80, 78, 58, 40, 74, 76, 72, 10, 2000000, 2);
        AddFA("Adama", "Bal", "SG", 23, "FRA", 198, 92, 76, 84, 82, 76, 74, 72, 74, 74, 52, 76, 74, 74, 10, 2000000, 2);
        AddFA("Toby", "Okani", "SF", 24, "USA", 201, 96, 74, 82, 80, 74, 72, 72, 74, 78, 60, 78, 76, 76, 10, 2000000, 2);
        AddFA("Lucas", "Williamson", "SG", 26, "USA", 196, 92, 74, 80, 78, 74, 72, 72, 74, 76, 54, 76, 74, 74, 8, 1500000, 1);
        AddFA("Taj", "Gibson", "PF", 39, "USA", 206, 108, 74, 72, 60, 70, 40, 66, 70, 80, 82, 80, 78, 82, 6, 3000000, 1);
        AddFA("Trevor", "Keels", "SG", 23, "USA", 193, 92, 76, 84, 84, 76, 74, 72, 74, 74, 52, 76, 74, 74, 10, 2000000, 2);
        AddFA("Jahmir", "Young", "PG", 24, "USA", 183, 82, 76, 84, 86, 78, 76, 80, 78, 58, 40, 74, 76, 72, 10, 2000000, 2);
        AddFA("Vladislav", "Goldin", "C", 24, "RUS", 216, 112, 78, 84, 68, 76, 60, 70, 72, 84, 86, 82, 80, 86, 12, 2500000, 3);
        AddFA("Myron", "Gardner", "PF", 25, "USA", 206, 102, 74, 82, 78, 74, 66, 70, 72, 82, 74, 80, 76, 78, 10, 2000000, 2);
        AddFA("Zyon", "Pullin", "PG", 23, "USA", 183, 82, 76, 84, 86, 78, 76, 80, 78, 58, 40, 74, 76, 72, 10, 2000000, 2);
        AddFA("Julian", "Phillips", "SF", 22, "USA", 201, 94, 80, 88, 84, 80, 78, 76, 78, 80, 60, 80, 78, 80, 16, 3000000, 3);
        AddFA("Rocco", "Zikarsky", "C", 19, "AUS", 218, 115, 78, 90, 60, 70, 40, 62, 66, 86, 88, 84, 82, 90, 14, 4000000, 4);
        AddFA("Joan", "Beringer", "C", 20, "FRA", 211, 104, 78, 88, 70, 72, 50, 64, 68, 84, 86, 82, 80, 88, 14, 3000000, 4);
        AddFA("Enrique", "Freeman", "PF", 24, "USA", 203, 102, 76, 84, 78, 74, 66, 70, 72, 82, 74, 80, 76, 78, 10, 2000000, 2);
        AddFA("Cormac", "Ryan", "SG", 26, "USA", 196, 92, 76, 84, 80, 78, 80, 72, 74, 74, 52, 76, 76, 74, 10, 2000000, 2);
        AddFA("Alex", "Antetokounmpo", "SF", 24, "GRE", 203, 95, 74, 84, 84, 74, 70, 72, 74, 78, 60, 80, 76, 78, 10, 2000000, 2);
        AddFA("Trey", "Jemison III", "C", 25, "USA", 208, 110, 76, 82, 68, 74, 50, 66, 68, 84, 86, 82, 80, 84, 10, 2000000, 2);
        AddFA("Dillon", "Jones", "SF", 23, "USA", 198, 94, 76, 84, 82, 76, 74, 72, 74, 78, 58, 78, 76, 76, 10, 2000000, 2);
        AddFA("Mohamed", "Diawara", "SF", 22, "FRA", 201, 92, 76, 84, 80, 74, 72, 72, 74, 78, 56, 78, 76, 76, 10, 2000000, 2);
        AddFA("Micah", "Peavy", "SG", 23, "USA", 196, 92, 76, 84, 84, 76, 74, 74, 76, 74, 52, 76, 74, 74, 10, 2000000, 2);
        AddFA("Josh", "Oduro", "PF", 24, "USA", 203, 104, 76, 84, 76, 74, 66, 70, 72, 82, 74, 80, 76, 78, 10, 2000000, 2);
        AddFA("Karlo", "Matkovic", "PF", 24, "CRO", 208, 104, 78, 86, 78, 76, 66, 72, 74, 84, 78, 82, 80, 82, 12, 2500000, 3);
        AddFA("Colin", "Castleton", "C", 25, "USA", 211, 112, 78, 84, 70, 76, 50, 68, 70, 84, 86, 82, 80, 84, 12, 2500000, 3);
        AddFA("Jase", "Richardson", "SG", 19, "USA", 196, 88, 73, 70, 76, 72, 70, 76, 78, 74, 54, 80, 78, 78, 18, 5000000, 4);
        AddFA("Noah", "Penda", "SF", 21, "FRA", 201, 94, 78, 88, 84, 78, 76, 74, 76, 78, 58, 80, 78, 78, 16, 3000000, 4);
        AddFA("Alex", "Morales", "SG", 24, "USA", 193, 88, 74, 80, 82, 74, 72, 72, 74, 74, 50, 76, 74, 74, 8, 1500000, 2);
        AddFA("Brooks", "Barnhizer", "SF", 23, "USA", 198, 94, 78, 84, 82, 78, 74, 74, 76, 80, 60, 80, 78, 80, 14, 2500000, 3);
        AddFA("Branden", "Carlson", "C", 25, "USA", 213, 112, 78, 84, 68, 76, 60, 70, 72, 84, 86, 82, 80, 84, 12, 2500000, 3);
        AddFA("Payton", "Sandfort", "SF", 22, "USA", 201, 94, 70, 76, 78, 72, 76, 74, 76, 72, 54, 78, 78, 76, 14, 2500000, 3);
        AddFA("Thomas", "Sorber", "C", 19, "USA", 211, 110, 72, 80, 60, 68, 60, 62, 74, 76, 78, 74, 72, 78, 18, 5000000, 4);
        AddFA("Emoni", "Bates", "SF", 22, "USA", 207, 92, 76, 84, 84, 76, 74, 74, 76, 74, 52, 76, 74, 74, 10, 2000000, 2);
        AddFA("Koby", "Brea", "SG", 24, "USA", 196, 90, 78, 86, 80, 84, 86, 72, 74, 60, 44, 74, 78, 68, 12, 2500000, 3);
        AddFA("Rasheer", "Fleming", "PF", 22, "USA", 206, 102, 72, 86, 78, 80, 74, 74, 76, 82, 72, 82, 80, 80, 16, 3000000, 3);
        AddFA("Isaiah", "Livers", "PF", 27, "USA", 203, 102, 76, 82, 76, 80, 80, 72, 74, 78, 62, 78, 76, 76, 10, 2000000, 2);
        AddFA("CJ", "Huntley", "PF", 25, "USA", 206, 104, 74, 82, 76, 74, 66, 70, 72, 82, 74, 80, 76, 78, 10, 2000000, 2);
        AddFA("Jamaree", "Bouyea", "PG", 26, "USA", 183, 82, 76, 84, 86, 78, 76, 80, 78, 58, 40, 74, 76, 72, 10, 2000000, 2);
        AddFA("Haywood", "Highsmith", "SF", 28, "USA", 198, 95, 78, 82, 82, 78, 76, 74, 76, 82, 60, 80, 78, 80, 10, 2000000, 2);
        AddFA("Dalen", "Terry", "SG", 23, "USA", 198, 92, 78, 84, 84, 78, 76, 74, 76, 78, 56, 78, 76, 76, 12, 2500000, 3);
        AddFA("Jabari", "Walker", "PF", 23, "USA", 206, 104, 78, 86, 80, 78, 74, 74, 76, 82, 70, 82, 78, 80, 14, 3000000, 3);
        AddFA("Tyrese", "Martin", "SG", 26, "USA", 196, 92, 76, 82, 82, 76, 74, 74, 76, 74, 52, 76, 74, 74, 10, 2000000, 2);
        AddFA("Isaiah", "Stevens", "PG", 25, "USA", 183, 82, 76, 84, 86, 78, 76, 80, 78, 58, 40, 74, 76, 72, 10, 2000000, 2);
        AddFA("Dylan", "Cardwell", "C", 24, "USA", 208, 110, 76, 82, 68, 74, 50, 66, 68, 84, 86, 82, 80, 84, 10, 2000000, 2);
        AddFA("Nique", "Clifford", "SF", 24, "USA", 198, 94, 78, 84, 82, 78, 74, 74, 76, 80, 60, 80, 78, 80, 14, 2500000, 3);
        AddFA("Daeqwon", "Plowden", "SG", 26, "USA", 196, 92, 76, 82, 82, 76, 74, 74, 76, 74, 52, 76, 74, 74, 10, 2000000, 2);
        AddFA("Chris", "Youngblood", "SG", 24, "USA", 196, 92, 76, 82, 82, 76, 74, 74, 76, 74, 52, 76, 74, 74, 10, 2000000, 2);
        AddFA("Sidy", "Cissoko", "SF", 21, "FRA", 201, 94, 77, 88, 84, 80, 76, 74, 76, 80, 60, 80, 78, 80, 16, 3000000, 4);
        AddFA("Jayson", "Kent", "SF", 23, "USA", 198, 94, 76, 84, 82, 76, 74, 74, 76, 78, 58, 78, 76, 76, 10, 2000000, 2);
        AddFA("Yang", "Hansen", "C", 19, "CHN", 218, 115, 80, 90, 60, 78, 60, 72, 74, 86, 88, 84, 82, 88, 18, 5000000, 4);
        AddFA("Garrett", "Temple", "SG", 39, "USA", 196, 88, 74, 72, 70, 72, 72, 78, 80, 76, 50, 78, 74, 76, 6, 3000000, 1);
        AddFA("Alijah", "Martin", "SG", 22, "USA", 193, 88, 76, 84, 84, 76, 74, 74, 76, 74, 52, 76, 74, 74, 10, 2000000, 2);
        AddFA("Emanuel", "Miller", "PF", 24, "USA", 203, 100, 78, 84, 78, 76, 70, 72, 74, 80, 62, 80, 78, 78, 14, 2500000, 3);
        AddFA("Jordan", "McLaughlin", "PG", 29, "USA", 183, 82, 78, 82, 84, 78, 76, 82, 80, 58, 40, 74, 76, 72, 10, 2000000, 2);
        AddFA("Lindy", "Waters III", "SG", 28, "USA", 196, 92, 78, 84, 82, 80, 84, 72, 74, 74, 52, 76, 76, 74, 10, 2000000, 2);
        AddFA("Carter", "Bryant", "SF", 20, "USA", 206, 96, 78, 88, 74, 72, 78, 74, 76, 70, 60, 82, 80, 82, 18, 5000000, 4);
        AddFA("David", "Jones Garcia", "SF", 23, "ESP", 198, 94, 78, 86, 82, 78, 76, 74, 76, 78, 58, 80, 78, 78, 14, 2500000, 3);
        AddFA("Chucky", "Hepburn", "PG", 22, "USA", 185, 84, 78, 86, 88, 80, 78, 82, 80, 58, 40, 76, 76, 74, 12, 2500000, 3);
        AddFA("Julian", "Reese", "PF", 22, "USA", 206, 104, 78, 84, 76, 78, 66, 70, 72, 82, 74, 82, 78, 80, 14, 2500000, 3);
        AddFA("Leaky", "Black", "SG", 26, "USA", 196, 92, 76, 78, 82, 74, 70, 74, 76, 84, 58, 84, 76, 82, 8, 2000000, 2);
        AddFA("Jamir", "Watkins", "SF", 23, "USA", 198, 95, 78, 84, 84, 78, 74, 74, 76, 80, 60, 80, 78, 78, 14, 2500000, 3);
        AddFA("Kennedy", "Chandler", "PG", 23, "USA", 183, 82, 76, 84, 88, 78, 76, 82, 80, 58, 40, 74, 76, 72, 10, 2000000, 2);
        AddFA("Hayden", "Gray", "SG", 24, "USA", 196, 90, 74, 82, 82, 74, 72, 72, 74, 74, 50, 76, 74, 74, 8, 1500000, 2);
        AddFA("Elijah", "Harkless", "SF", 25, "USA", 198, 94, 76, 82, 80, 76, 72, 74, 76, 80, 58, 80, 76, 78, 10, 2000000, 2);
        AddFA("Blake", "Hinson", "SF", 25, "USA", 201, 100, 76, 84, 78, 82, 84, 72, 74, 70, 52, 76, 76, 74, 10, 2000000, 2);
        AddFA("Bez", "Mbeng", "PG", 23, "CMR", 188, 84, 76, 84, 86, 78, 76, 80, 78, 58, 40, 74, 76, 72, 10, 2000000, 2);
        AddFA("Justin", "Jackson", "SG", 26, "USA", 196, 92, 76, 82, 82, 76, 74, 74, 76, 74, 52, 76, 74, 74, 10, 2000000, 2);
        AddFA("Dante", "Exum", "PG", 30, "AUS", 196, 90, 74, 76, 82, 68, 58, 76, 74, 72, 50, 64, 72, 66, 22, 3000000, 1);
        AddFA("Lonzo", "Ball", "PG", 27, "USA", 198, 86, 70, 74, 78, 64, 60, 74, 70, 70, 56, 64, 70, 68, 30, 2000000, 1);
        AddFA("Patrick", "Beverley", "PG", 38, "USA", 185, 81, 68, 66, 76, 62, 56, 68, 64, 70, 56, 60, 70, 68, 42, 2000000, 1);
        AddFA("Monte", "Morris", "PG", 30, "USA", 188, 83, 72, 74, 78, 66, 58, 76, 74, 68, 50, 62, 72, 62, 22, 2000000, 1);
        AddFA("Ish", "Smith", "PG", 37, "USA", 183, 79, 64, 64, 80, 62, 48, 72, 70, 70, 48, 60, 70, 60, 32, 2000000, 1);
        AddFA("Cameron", "Payne", "PG", 31, "USA", 188, 84, 68, 70, 80, 68, 58, 72, 70, 64, 48, 60, 68, 54, 22, 2000000, 1);
        AddFA("Wesley", "Matthews", "PG", 38, "USA", 193, 95, 62, 62, 72, 62, 58, 60, 60, 62, 52, 56, 64, 50, 30, 2000000, 1);
        AddFA("Malik", "Beasley", "SG", 29, "USA", 193, 84, 76, 77, 78, 80, 88, 62, 74, 62, 52, 75, 70, 64, 32, 8000000, 1);
        AddFA("Cam", "Thomas", "SG", 23, "USA", 196, 93, 84, 92, 76, 88, 84, 68, 80, 70, 60, 72, 82, 69, 36, 10000000, 3);
        AddFA("Ben", "Simmons", "PG", 28, "AUS", 208, 99, 78, 76, 88, 60, 44, 84, 80, 84, 72, 88, 78, 82, 30, 8000000, 2);
        AddFA("Lonnie", "Walker", "SG", 26, "USA", 196, 93, 72, 76, 84, 74, 72, 64, 70, 58, 50, 60, 70, 50, 24, 2000000, 1);
        AddFA("Terrence", "Ross", "SG", 34, "USA", 198, 93, 72, 72, 80, 78, 80, 62, 68, 56, 50, 60, 70, 48, 22, 2000000, 1);
        AddFA("Josh", "Richardson", "SG", 32, "USA", 198, 95, 72, 72, 80, 74, 72, 68, 70, 60, 54, 62, 72, 56, 30, 2000000, 1);
        AddFA("Timothé", "Luwawu-Cabarrot", "SG", 28, "FRA", 198, 93, 68, 72, 80, 72, 68, 64, 66, 58, 52, 58, 68, 50, 20, 2000000, 1);
        AddFA("Reggie", "Bullock", "SG", 33, "USA", 198, 95, 70, 70, 78, 76, 80, 62, 66, 56, 52, 60, 70, 48, 20, 2000000, 1);
        AddFA("Glenn", "Robinson", "SG", 30, "USA", 198, 100, 68, 72, 80, 72, 68, 62, 66, 58, 54, 58, 68, 50, 26, 2000000, 1);
        AddFA("Alec", "Burks", "SG", 33, "USA", 198, 97, 72, 72, 78, 76, 74, 66, 70, 58, 52, 62, 72, 52, 22, 5000000, 1);
        AddFA("Cedi", "Osman", "SG", 30, "TUR", 201, 95, 68, 70, 76, 72, 72, 66, 68, 58, 54, 60, 68, 50, 18, 2000000, 1);
        AddFA("Troy", "Brown", "SG", 26, "USA", 198, 95, 66, 72, 74, 64, 56, 66, 66, 64, 56, 58, 66, 54, 28, 2000000, 1);
        AddFA("Jaden", "Ivey", "SG", 24, "USA", 190, 88, 79, 87, 88, 72, 74, 80, 86, 68, 58, 87, 72, 75, 45, 14000000, 1);
        AddFA("Talen", "Horton-Tucker", "SG", 24, "USA", 196, 93, 72, 74, 78, 66, 56, 60, 60, 62, 54, 64, 60, 59, 30, 11500000, 2);
        AddFA("Hamidou", "Diallo", "SG", 26, "USA", 196, 99, 70, 74, 86, 66, 50, 58, 64, 60, 62, 58, 68, 56, 36, 2000000, 1);
        AddFA("Tony", "Snell", "SG", 33, "USA", 198, 97, 64, 64, 74, 70, 74, 58, 62, 54, 50, 58, 66, 42, 16, 2000000, 1);
        AddFA("Rodney", "McGruder", "SG", 33, "USA", 193, 95, 64, 64, 74, 66, 62, 58, 62, 54, 50, 58, 66, 46, 20, 2000000, 1);
        AddFA("Juan", "Hernangomez", "SG", 30, "ESP", 198, 95, 66, 70, 74, 70, 68, 60, 64, 56, 58, 60, 66, 48, 20, 2000000, 1);
        AddFA("Oshae", "Brissett", "SF", 26, "CAN", 203, 95, 72, 76, 78, 72, 66, 64, 68, 62, 60, 62, 70, 56, 30, 2000000, 1);
        AddFA("Bojan", "Bogdanovic", "SF", 36, "CRO", 203, 104, 76, 76, 74, 82, 84, 68, 70, 56, 50, 60, 76, 48, 22, 19000000, 2);
        AddFA("Justise", "Winslow", "SF", 29, "USA", 198, 104, 66, 72, 74, 58, 42, 68, 68, 66, 62, 60, 70, 56, 36, 2000000, 1);
        AddFA("Danuel", "House", "SF", 31, "USA", 198, 95, 68, 70, 76, 72, 68, 62, 64, 56, 54, 60, 68, 48, 22, 2000000, 1);
        AddFA("Rondae", "Hollis-Jefferson", "SF", 30, "USA", 198, 104, 70, 72, 78, 66, 40, 66, 70, 64, 62, 66, 70, 56, 30, 2000000, 1);
        AddFA("Maurice", "Harkless", "SF", 32, "USA", 203, 99, 66, 66, 74, 66, 58, 60, 62, 58, 60, 58, 68, 48, 28, 2000000, 1);
        AddFA("James", "Ennis", "SF", 34, "USA", 198, 99, 66, 66, 76, 66, 60, 60, 62, 56, 54, 58, 66, 48, 24, 2000000, 1);
        AddFA("Justin", "Holiday", "SF", 35, "USA", 198, 95, 66, 66, 74, 70, 68, 62, 64, 56, 54, 58, 66, 48, 28, 2000000, 1);
        AddFA("Maxwell", "Lewis", "SF", 22, "USA", 201, 95, 64, 76, 74, 64, 60, 58, 62, 54, 50, 56, 66, 50, 18, 2000000, 1);
        AddFA("Terquavion", "Smith", "SF", 22, "USA", 193, 86, 66, 76, 82, 68, 62, 64, 70, 56, 48, 58, 68, 50, 20, 2000000, 1);
        AddFA("Chris", "Boucher", "PF", 33, "CAN", 203, 90, 74, 74, 72, 74, 78, 60, 64, 66, 70, 75, 72, 62, 78, 4000000, 1);
        AddFA("Dario", "Saric", "PF", 31, "CRO", 208, 102, 72, 74, 72, 72, 68, 70, 68, 64, 60, 62, 72, 54, 30, 5000000, 1);
        AddFA("Orlando", "Robinson", "PF", 24, "USA", 211, 104, 64, 72, 66, 50, 30, 50, 50, 60, 76, 64, 58, 46, 42, 2000000, 1);
        AddFA("Thaddeus", "Young", "PF", 36, "USA", 203, 100, 68, 68, 72, 64, 40, 64, 68, 62, 64, 62, 70, 54, 56, 2000000, 1);
        AddFA("Noah", "Vonleh", "PF", 29, "USA", 208, 113, 62, 66, 68, 50, 28, 48, 50, 60, 76, 64, 60, 44, 42, 2000000, 1);
        AddFA("JaMychal", "Green", "PF", 34, "USA", 203, 102, 66, 66, 68, 62, 50, 56, 58, 62, 72, 64, 64, 48, 36, 2000000, 1);
        AddFA("Mo", "Bamba", "C", 26, "USA", 213, 104, 70, 76, 68, 58, 46, 52, 54, 62, 80, 70, 60, 42, 70, 2000000, 1);
        AddFA("James", "Wiseman", "C", 24, "USA", 213, 109, 72, 80, 72, 64, 30, 54, 54, 64, 78, 70, 64, 46, 58, 8000000, 2);
        AddFA("Bruno", "Fernando", "C", 26, "ANG", 206, 109, 66, 72, 70, 52, 24, 50, 50, 60, 78, 68, 60, 44, 48, 2000000, 1);
        AddFA("Justin", "Minaya", "C", 26, "USA", 203, 102, 62, 70, 68, 54, 32, 50, 50, 60, 74, 64, 58, 42, 44, 2000000, 1);
        AddFA("Tony", "Bradley", "C", 27, "USA", 211, 113, 66, 72, 68, 56, 24, 48, 48, 60, 76, 66, 60, 42, 50, 2000000, 1);
        AddFA("Boban", "Marjanovic", "C", 36, "SRB", 224, 131, 66, 66, 60, 56, 30, 50, 50, 56, 82, 64, 56, 40, 44, 2000000, 1);

        _db.BeginTransaction();
        try
        {
            foreach (var fa in freeAgents)
                _db.Insert(fa);
            _db.Commit();
            Debug.Log($"[DB] {freeAgents.Count} agentes libres insertados.");
        }
        catch (System.Exception e)
        {
            _db.Rollback();
            Debug.LogError($"[DB] Error insertando agentes libres: {e.Message}");
        }
    }

    public void SeedSponsors()
    {
        var sponsors = new List<SponsorData>
        {
            new SponsorData { name = "Apple",             logo = "Patrocinadores/1.png", initial_income = 128000000, home_game_income = 850000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Beats",             logo = "Patrocinadores/2.png",  initial_income = 118000000, home_game_income = 550000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Billboard",         logo = "Patrocinadores/3.png",  initial_income = 121000000, home_game_income = 620000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "BMW",               logo = "Patrocinadores/4.png",  initial_income = 122000000, home_game_income = 650000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Bridgestone",       logo = "Patrocinadores/5.png", initial_income = 118000000, home_game_income = 540000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Domino's Pizza",    logo = "Patrocinadores/6.png",  initial_income = 120000000, home_game_income = 600000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "MasterCard",        logo = "Patrocinadores/7.png",  initial_income = 115000000, home_game_income = 450000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Etihad Airways",    logo = "Patrocinadores/8.png",  initial_income = 116000000, home_game_income = 480000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Good Year",         logo = "Patrocinadores/9.png", initial_income = 114000000, home_game_income = 420000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Zoom",              logo = "Patrocinadores/10.png", initial_income = 119000000, home_game_income = 570000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Unicef",            logo = "Patrocinadores/11.png", initial_income = 114500000, home_game_income = 430000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Razer",             logo = "Patrocinadores/12.png", initial_income = 114500000, home_game_income = 430000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Starbucks",         logo = "Patrocinadores/13.png",  initial_income = 117000000, home_game_income = 500000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Lenovo",            logo = "Patrocinadores/14.png", initial_income = 118000000, home_game_income = 540000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Airbnb",            logo = "Patrocinadores/15.png", initial_income = 113000000, home_game_income = 390000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "McDonald's",        logo = "Patrocinadores/16.png",  initial_income = 115000000, home_game_income = 450000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Nvidia",            logo = "Patrocinadores/17.png", initial_income = 124000000, home_game_income = 720000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Qatar Airways",     logo = "Patrocinadores/18.png", initial_income = 116000000, home_game_income = 480000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "SONY",              logo = "Patrocinadores/19.png", initial_income = 126000000, home_game_income = 780000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Netflix",           logo = "Patrocinadores/20.png",  initial_income = 124000000, home_game_income = 720000, contract_years = 1, is_active = 0, team_id = 0 },
        };

        foreach (var s in sponsors)
            _db.Insert(s);
        Debug.Log($"[DB] {sponsors.Count} sponsors insertados.");

        // Select 3 random sponsors for the first season
        var all = _db.Table<SponsorData>().ToList();
        if (all.Count >= 3)
        {
            var selected = all.OrderBy(_ => UnityEngine.Random.value).Take(3).ToList();
            foreach (var s in selected)
            {
                s.is_active = 1;
                _db.Update(s);
            }
            Debug.Log($"[DB] 3 patrocinadores activos seleccionados: {string.Join(", ", selected.Select(s => s.name))}");
        }
    }

    public void SeedTvChannels()
    {
        var channels = new List<TvChannelData>
        {
            new TvChannelData { name = "DAZN",      logo = "Televisiones/1.png",   initial_income = 122000000, home_game_income = 650000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2500000, viewership_multiplier = 1.8f },
            new TvChannelData { name = "TV5",       logo = "Televisiones/2.png",   initial_income = 118000000, home_game_income = 550000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 1200000, viewership_multiplier = 1.2f },
            new TvChannelData { name = "FOX",       logo = "Televisiones/3.png",  initial_income = 118000000, home_game_income = 540000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2200000, viewership_multiplier = 1.6f },
            new TvChannelData { name = "Movistar",  logo = "Televisiones/4.png",   initial_income = 117000000, home_game_income = 500000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2200000, viewership_multiplier = 1.6f },
            new TvChannelData { name = "NBC",       logo = "Televisiones/5.png",   initial_income = 121000000, home_game_income = 620000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2600000, viewership_multiplier = 1.8f },
            new TvChannelData { name = "CBS",       logo = "Televisiones/6.png",   initial_income = 120000000, home_game_income = 600000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2500000, viewership_multiplier = 1.8f },
            new TvChannelData { name = "ESPN",      logo = "Televisiones/7.png",  initial_income = 126000000, home_game_income = 780000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 3000000, viewership_multiplier = 2.0f },
            new TvChannelData { name = "Sky",       logo = "Televisiones/8.png",   initial_income = 118000000, home_game_income = 540000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2300000, viewership_multiplier = 1.7f },
            new TvChannelData { name = "ITV",       logo = "Televisiones/9.png",  initial_income = 114000000, home_game_income = 420000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2000000, viewership_multiplier = 1.5f },
            new TvChannelData { name = "Hulu",      logo = "Televisiones/10.png",  initial_income = 119000000, home_game_income = 570000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2100000, viewership_multiplier = 1.5f }
        };

        foreach (var c in channels)
            _db.Insert(c);
        Debug.Log($"[DB] {channels.Count} canales de TV insertados.");

        // Select 3 random TV channels for the first season
        var all = _db.Table<TvChannelData>().ToList();
        if (all.Count >= 3)
        {
            var selected = all.OrderBy(_ => UnityEngine.Random.value).Take(3).ToList();
            foreach (var c in selected)
            {
                c.is_active = 1;
                _db.Update(c);
            }
            Debug.Log($"[DB] 3 canales de TV activos seleccionados: {string.Join(", ", selected.Select(c => c.name))}");
        }
    }

    public void SeedHistoricalRecords()
    {
        var records = new List<HistoricalRecordData>
        {
            new HistoricalRecordData { stat_type = "points",     player_name = "Wilt Chamberlain", value = 100, game_date = "1962-03-02", team_abbreviation = "PHW" },
            new HistoricalRecordData { stat_type = "rebounds",   player_name = "Wilt Chamberlain", value = 55,  game_date = "1960-11-24", team_abbreviation = "PHW" },
            new HistoricalRecordData { stat_type = "assists",    player_name = "Scott Skiles",     value = 30,  game_date = "1990-12-30", team_abbreviation = "ORL" },
            new HistoricalRecordData { stat_type = "steals",     player_name = "Kendall Gill",     value = 11,  game_date = "1999-04-03", team_abbreviation = "NJN" },
            new HistoricalRecordData { stat_type = "blocks",     player_name = "Elmore Smith",     value = 17,  game_date = "1973-10-28", team_abbreviation = "LAL" },
            new HistoricalRecordData { stat_type = "fgm",        player_name = "Wilt Chamberlain", value = 36,  game_date = "1962-03-02", team_abbreviation = "PHW" },
            new HistoricalRecordData { stat_type = "fg3m",       player_name = "Klay Thompson",    value = 14,  game_date = "2018-10-29", team_abbreviation = "GSW" },
            new HistoricalRecordData { stat_type = "ftm",        player_name = "Bam Adebayo",      value = 36,  game_date = "2026-03-10", team_abbreviation = "MIA" },
            new HistoricalRecordData { stat_type = "turnovers",  player_name = "Jason Kidd",       value = 14,  game_date = "2000-11-17", team_abbreviation = "PHX" },
        };

        foreach (var r in records)
            _db.Insert(r);
        Debug.Log($"[DB] {records.Count} records históricos insertados.");
    }

    public void SeedTeamRecords()
    {
        var allTeams = GetAllTeams();
        int count = 0;
        foreach (var team in allTeams)
        {
            if (TeamRecordSeeder.Data.TryGetValue(team.name, out var entries))
            {
                foreach (var e in entries)
                {
                    var rec = new TeamRecordData
                    {
                        team_id = team.id,
                        stat_type = e.stat_type,
                        player_name = e.player_name,
                        value = e.value,
                        game_date = e.game_date
                    };
                    _db.Insert(rec);
                    count++;
                }
            }
        }
        Debug.Log($"[DB] {count} récords de equipo insertados.");
    }

    public void SeedHistoricalPlayerStats()
    {
        var stats = new List<HistoricalPlayerStatsData>();
        foreach (var d in HistoricalPlayerStatsSeeder.Data)
        {
            stats.Add(new HistoricalPlayerStatsData
            {
                first_name = d.first,
                last_name = d.last,
                position = d.pos,
                overall = d.ovr,
                team_name = d.team,
                team_abbreviation = d.abbr,
                team_logo = d.logo,
                games = d.gp,
                total_points = d.pts,
                total_rebounds = d.reb,
                total_assists = d.ast,
                total_steals = d.stl,
                total_blocks = d.blk,
                total_turnovers = d.tov,
                total_fgm = d.fgm,
                total_fga = d.fga,
                total_fg3m = d.fg3m,
                total_fg3a = d.fg3a,
                total_ftm = d.ftm,
                total_fta = d.fta,
                total_double_doubles = d.dd,
                total_triple_doubles = d.td,
                total_minutes = d.gp * 30,
                total_rating = d.pts + d.reb + d.ast + d.stl + d.blk
            });
        }

        foreach (var s in stats)
            _db.Insert(s);
        Debug.Log($"[DB] {stats.Count} estadísticas históricas de jugadores insertadas.");
    }

    void SeedPalmaresData()
    {
        foreach (var r in PalmaresSeeder.FinalsData)
            _db.Insert(r);
        foreach (var r in PalmaresSeeder.AwardsData)
            _db.Insert(r);
        foreach (var r in PalmaresSeeder.QuintetData)
            _db.Insert(r);
        Debug.Log($"[DB] {PalmaresSeeder.FinalsData.Count} finales, {PalmaresSeeder.AwardsData.Count} premios, {PalmaresSeeder.QuintetData.Count} quintetos insertados.");
    }

    // ── PALMARES ────────────────────────────────────────

    public List<FinalsRecord> GetFinalsRecords()
    {
        if (!EnsureDb()) return new List<FinalsRecord>();
        return _db.Table<FinalsRecord>().ToList();
    }

    public List<AwardsRecord> GetAwardsRecords()
    {
        if (!EnsureDb()) return new List<AwardsRecord>();
        return _db.Table<AwardsRecord>().ToList();
    }

    public List<QuintetRecord> GetQuintetRecords()
    {
        if (!EnsureDb()) return new List<QuintetRecord>();
        return _db.Table<QuintetRecord>().ToList();
    }

    // ── PLAYER GAME STATS ─────────────────────────────────

    public void DeletePlayerGameStatsForGame(int gameId)
    {
        _db.Execute("DELETE FROM player_game_stats WHERE game_id = ?", gameId);
    }

    public void SavePlayerGameStats(PlayerGameStats stats)
    {
        _db.Insert(stats);
    }

    public List<PlayerGameStats> GetPlayerGameStats(int playerId)
    {
        return _db.Table<PlayerGameStats>()
                  .Where(s => s.player_id == playerId)
                  .ToList();
    }

    public List<PlayerGameStats> GetGamePlayerStats(int gameId)
    {
        return _db.Table<PlayerGameStats>()
                  .Where(s => s.game_id == gameId)
                  .OrderByDescending(s => s.points)
                  .ToList();
    }

    public List<PlayerGameStats> GetGamePlayerStatsBatch(List<int> gameIds)
    {
        if (gameIds == null || gameIds.Count == 0) return new List<PlayerGameStats>();
        return _db.Query<PlayerGameStats>(
            "SELECT * FROM player_game_stats WHERE game_id IN (" +
            string.Join(",", gameIds) + ")");
    }

    public int GetPlayerGamesPlayedInSeason(int playerId, int seasonId)
    {
        if (!EnsureDb()) return 0;
        var gameIds = _db.Table<GameData>()
                         .Where(g => g.season_id == seasonId && g.is_played == 1)
                         .Select(g => g.id)
                         .ToList();
        return _db.Table<PlayerGameStats>()
                  .Where(s => s.player_id == playerId && gameIds.Contains(s.game_id))
                  .Count();
    }

    public List<PlayerData> GetLeagueTopScorers(int managerId, int count = 10)
    {
        var season = GetActiveSeason(GetActiveManager()?.id ?? 0);
        if (season == null) return new List<PlayerData>();

        var allGames = _db.Table<GameData>()
                          .Where(g => g.manager_id == season.manager_id
                                   && g.is_played == 1
                                   && g.game_type == "regular")
                          .ToList();

        var playerPoints = new Dictionary<int, int>();
        foreach (var game in allGames)
        {
            var stats = GetGamePlayerStats(game.id);
            foreach (var s in stats)
            {
                playerPoints[s.player_id] = playerPoints.GetValueOrDefault(s.player_id, 0) + s.points;
            }
        }

        var sorted = playerPoints.OrderByDescending(p => p.Value).Take(count).ToList();
        var result = new List<PlayerData>();
        foreach (var kvp in sorted)
        {
            var player = _db.Table<PlayerData>().Where(p => p.id == kvp.Key).FirstOrDefault();
            if (player != null) result.Add(player);
        }
        return result;
    }

    public List<PlayerData> GetLeagueTopRebounders(int managerId, int count = 10)
    {
        var season = GetActiveSeason(GetActiveManager()?.id ?? 0);
        if (season == null) return new List<PlayerData>();

        var allGames = _db.Table<GameData>()
                          .Where(g => g.manager_id == season.manager_id
                                   && g.is_played == 1
                                   && g.game_type == "regular")
                          .ToList();

        var playerRebounds = new Dictionary<int, int>();
        foreach (var game in allGames)
        {
            var stats = GetGamePlayerStats(game.id);
            foreach (var s in stats)
            {
                playerRebounds[s.player_id] = playerRebounds.GetValueOrDefault(s.player_id, 0) + s.rebounds;
            }
        }

        var sorted = playerRebounds.OrderByDescending(p => p.Value).Take(count).ToList();
        var result = new List<PlayerData>();
        foreach (var kvp in sorted)
        {
            var player = _db.Table<PlayerData>().Where(p => p.id == kvp.Key).FirstOrDefault();
            if (player != null) result.Add(player);
        }
        return result;
    }

    public List<PlayerData> GetLeagueTopAssisters(int managerId, int count = 10)
    {
        var season = GetActiveSeason(GetActiveManager()?.id ?? 0);
        if (season == null) return new List<PlayerData>();

        var allGames = _db.Table<GameData>()
                          .Where(g => g.manager_id == season.manager_id
                                   && g.is_played == 1
                                   && g.game_type == "regular")
                          .ToList();

        var playerAssists = new Dictionary<int, int>();
        foreach (var game in allGames)
        {
            var stats = GetGamePlayerStats(game.id);
            foreach (var s in stats)
            {
                playerAssists[s.player_id] = playerAssists.GetValueOrDefault(s.player_id, 0) + s.assists;
            }
        }

        var sorted = playerAssists.OrderByDescending(p => p.Value).Take(count).ToList();
        var result = new List<PlayerData>();
        foreach (var kvp in sorted)
        {
            var player = _db.Table<PlayerData>().Where(p => p.id == kvp.Key).FirstOrDefault();
            if (player != null) result.Add(player);
        }
        return result;
    }

    public (PlayerData player, float avgPts, float avgReb, float avgAst, float avgStl, float avgBlk, float avgVal, int games) GetPlayerSeasonStats(int playerId, int managerId)
    {
        var season = GetActiveSeason(managerId);
        if (season == null) return (null, 0, 0, 0, 0, 0, 0, 0);

        var row = _db.Query<PlayerSeasonStatsRow>(
            @"SELECT p.id AS player_id, p.first_name, p.last_name, p.position,
                     COUNT(*) AS games,
                     SUM(ps.points) AS total_points,
                     SUM(ps.rebounds) AS total_rebounds,
                     SUM(ps.assists) AS total_assists,
                     SUM(ps.steals) AS total_steals,
                     SUM(ps.blocks) AS total_blocks,
                     SUM(ps.rating) AS total_rating
              FROM player_game_stats ps
              JOIN players p ON ps.player_id = p.id
              JOIN games g ON ps.game_id = g.id
              WHERE g.manager_id = ?
                AND g.season_id = ?
                AND ps.player_id = ?
                AND g.game_type = 'regular'
                AND g.is_played = 1
              GROUP BY ps.player_id",
            managerId, season.id, playerId).FirstOrDefault();

        if (row == null)
        {
            var player = _db.Table<PlayerData>().Where(p => p.id == playerId).FirstOrDefault();
            return (player, 0, 0, 0, 0, 0, 0, 0);
        }

        float avgPts = row.games > 0 ? (float)row.total_points / row.games : 0;
        float avgReb = row.games > 0 ? (float)row.total_rebounds / row.games : 0;
        float avgAst = row.games > 0 ? (float)row.total_assists / row.games : 0;
        float avgStl = row.games > 0 ? (float)row.total_steals / row.games : 0;
        float avgBlk = row.games > 0 ? (float)row.total_blocks / row.games : 0;
        float avgVal = row.games > 0 ? (float)row.total_rating / row.games : 0;

        var p = _db.Table<PlayerData>().Where(p2 => p2.id == playerId).FirstOrDefault();
        return (p, avgPts, avgReb, avgAst, avgStl, avgBlk, avgVal, row.games);
    }

    public List<PlayerSeasonStatsRow> GetTeamPlayerSeasonStats(int seasonId, int teamId, int managerId)
    {
        return _db.Query<PlayerSeasonStatsRow>(
            @"SELECT p.id AS player_id, p.first_name, p.last_name, p.position,
                     COUNT(*) AS games,
                     SUM(ps.points) AS total_points,
                     SUM(ps.rebounds) AS total_rebounds,
                     SUM(ps.assists) AS total_assists,
                     SUM(ps.steals) AS total_steals,
                     SUM(ps.blocks) AS total_blocks,
                     SUM(ps.rating) AS total_rating
              FROM player_game_stats ps
              JOIN players p ON ps.player_id = p.id
              JOIN games g ON ps.game_id = g.id
              WHERE g.manager_id = ?
                AND g.season_id = ?
                AND ps.team_id = ?
                AND g.game_type = 'regular'
                AND g.is_played = 1
              GROUP BY ps.player_id",
            managerId, seasonId, teamId);
    }

    // ── SPONSORS ──────────────────────────────────────────

    public List<SponsorData> GetAllSponsors()
    {
        if (!EnsureDb()) return new List<SponsorData>();
        return _db.Table<SponsorData>().ToList();
    }

    public SponsorData GetSponsorById(int id)
    {
        if (!EnsureDb()) return null;
        return _db.Table<SponsorData>()
                  .Where(s => s.id == id)
                  .FirstOrDefault();
    }

    public void UpdateSponsor(SponsorData sponsor)
    {
        if (!EnsureDb()) return;
        _db.Update(sponsor);
    }

    public SponsorData GetActiveSponsor(int teamId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<SponsorData>()
                  .Where(s => s.team_id == teamId && s.is_active == 1)
                  .FirstOrDefault();
    }

    public List<SponsorData> GetAvailableSponsors(int teamId)
    {
        if (!EnsureDb()) return new List<SponsorData>();
        return _db.Table<SponsorData>()
                  .Where(s => s.is_active == 1)
                  .ToList();
    }

    public void SignSponsor(int sponsorId, int seasonId, int teamId, int gameDay = 0)
    {
        if (!EnsureDb()) return;
        var sponsor = GetSponsorById(sponsorId);
        if (sponsor == null) return;

        // Assign sponsor to team
        sponsor.team_id = teamId;
        sponsor.season_id = seasonId;
        _db.Update(sponsor);

        // Update team settings (create if missing)
        var settings = GetTeamSettings(teamId);
        if (settings == null)
        {
            settings = new TeamSettingsData
            {
                team_id = teamId,
                ticket_price = 50,
                subscription_price = 2100
            };
            _db.Insert(settings);
        }
        settings.sponsor_id = sponsorId;
        settings.sponsor_years_remaining = sponsor.contract_years;
        UpdateTeamSettings(settings);

        // Add initial income to budget
        var team = GetTeamById(teamId);
        if (team != null)
        {
            team.budget += sponsor.initial_income;
            UpdateTeam(team);
        }

        // Create finance record
        var finance = new FinanceRecord
        {
            team_id = teamId,
            season_id = seasonId,
            record_type = FinanceRecord.TYPE_SPONSORSHIP,
            amount = sponsor.initial_income,
            game_day = gameDay,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        _db.Insert(finance);
    }

    public void FireSponsor(int sponsorId, int seasonId, int teamId)
    {
        if (!EnsureDb()) return;
        var sponsor = GetSponsorById(sponsorId);
        if (sponsor != null)
        {
            sponsor.is_active = 0;
            sponsor.team_id = 0;
            _db.Update(sponsor);
        }
    }

    public void SignSponsor(SponsorData sponsor)
    {
        if (!EnsureDb()) return;
        sponsor.is_active = 1;
        _db.Update(sponsor);
    }

    public void FireSponsor(SponsorData sponsor)
    {
        if (!EnsureDb()) return;
        sponsor.is_active = 0;
        sponsor.team_id = 0;
        _db.Update(sponsor);
    }

    // ── TV CHANNELS ───────────────────────────────────────

    public List<TvChannelData> GetTVChannels()
    {
        if (!EnsureDb()) return new List<TvChannelData>();
        return _db.Table<TvChannelData>().ToList();
    }

    public TvChannelData GetTVChannelById(int id)
    {
        if (!EnsureDb()) return null;
        return _db.Table<TvChannelData>()
                  .Where(c => c.id == id)
                  .FirstOrDefault();
    }

    public TvChannelData GetActiveTVChannel(int teamId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<TvChannelData>()
                  .Where(c => c.team_id == teamId && c.is_active == 1)
                  .FirstOrDefault();
    }

    public List<TvChannelData> GetAvailableTVChannels(int teamId)
    {
        if (!EnsureDb()) return new List<TvChannelData>();
        var allActive = _db.Table<TvChannelData>()
                           .Where(c => c.is_active == 1)
                           .ToList();

        // New games have exactly 3 active channels seeded
        if (allActive.Count <= 3) return allActive;

        // Old data: more than 3 active. Include signed channel if any,
        // then fill with others up to 3 total.
        var signed = allActive.FirstOrDefault(c => c.team_id == teamId);
        var result = new List<TvChannelData>();

        if (signed != null)
            result.Add(signed);

        var others = allActive.Where(c => c.team_id != teamId)
                              .OrderBy(c => c.id)
                              .Take(3 - result.Count)
                              .ToList();
        result.AddRange(others);

        return result;
    }

    public void SignTVChannel(int channelId, int seasonId, int teamId, int gameDay = 0)
    {
        if (!EnsureDb()) return;
        var channel = GetTVChannelById(channelId);
        if (channel == null) return;

        // Assign channel to team
        channel.team_id = teamId;
        channel.season_id = seasonId;
        _db.Update(channel);

        // Update team settings (create if missing)
        var settings = GetTeamSettings(teamId);
        if (settings == null)
        {
            settings = new TeamSettingsData
            {
                team_id = teamId,
                ticket_price = 50,
                subscription_price = 2100
            };
            _db.Insert(settings);
        }
        settings.tv_channel_id = channelId;
        settings.tv_years_remaining = channel.contract_years;
        UpdateTeamSettings(settings);

        // Add initial income to budget
        var team = GetTeamById(teamId);
        if (team != null)
        {
            team.budget += channel.initial_income;
            UpdateTeam(team);
        }

        // Create finance record
        var finance = new FinanceRecord
        {
            team_id = teamId,
            season_id = seasonId,
            record_type = FinanceRecord.TYPE_TV,
            amount = channel.initial_income,
            game_day = gameDay,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        _db.Insert(finance);
    }

    public void FireTVChannel(TvChannelData channel)
    {
        if (!EnsureDb()) return;
        channel.is_active = 0;
        channel.team_id = 0;
        _db.Update(channel);
    }

    // ── FINANCE RECORDS ───────────────────────────────────

    public void AddFinanceRecord(FinanceRecord record)
    {
        _db.Insert(record);
    }

    public List<FinanceRecord> GetFinanceRecords(int teamId, int seasonId)
    {
        if (!EnsureDb()) return new List<FinanceRecord>();
        return _db.Table<FinanceRecord>()
                  .Where(r => r.team_id == teamId && r.season_id == seasonId)
                  .ToList();
    }

    public long GetTotalIncome(int teamId, int seasonId)
    {
        if (!EnsureDb()) return 0;
        var records = _db.Table<FinanceRecord>()
                         .Where(r => r.team_id == teamId && r.season_id == seasonId
                                  && r.record_type <= FinanceRecord.TYPE_TV)
                         .ToList();
        return records.Sum(r => r.amount);
    }

    public long GetTotalExpenses(int teamId, int seasonId)
    {
        if (!EnsureDb()) return 0;
        var records = _db.Table<FinanceRecord>()
                         .Where(r => r.team_id == teamId && r.season_id == seasonId
                                  && r.record_type >= FinanceRecord.TYPE_RENOVATION)
                         .ToList();
        return records.Sum(r => r.amount);
    }

    public long GetFinanceTotalByType(int teamId, int seasonId, int recordType)
    {
        if (!EnsureDb()) return 0;
        var records = _db.Table<FinanceRecord>()
                         .Where(r => r.team_id == teamId && r.season_id == seasonId
                                  && r.record_type == recordType)
                         .ToList();
        return records.Sum(r => r.amount);
    }

    public FinanceRecord GetFinanceRecord(int teamId, int seasonId, int recordType, int gameDay)
    {
        if (gameDay > 0)
        {
            return _db.Table<FinanceRecord>()
                      .Where(r => r.team_id == teamId && r.season_id == seasonId
                               && r.record_type == recordType && r.game_day == gameDay)
                      .FirstOrDefault();
        }
        else
        {
            return _db.Table<FinanceRecord>()
                      .Where(r => r.team_id == teamId && r.season_id == seasonId
                               && r.record_type == recordType)
                      .FirstOrDefault();
        }
    }

    // ── HISTORICAL PLAYER STATS ───────────────────────────

    public List<HistoricalPlayerStatsData> GetAllHistoricalPlayerStats()
    {
        return _db.Table<HistoricalPlayerStatsData>()
                  .OrderByDescending(p => p.total_points)
                  .ToList();
    }

    public HistoricalPlayerStatsData GetHistoricalPlayerStats(string firstName, string lastName)
    {
        return _db.Table<HistoricalPlayerStatsData>()
                  .Where(p => p.first_name == firstName && p.last_name == lastName)
                  .FirstOrDefault();
    }

    public void SaveHistoricalPlayerStats(HistoricalPlayerStatsData stats)
    {
        var existing = GetHistoricalPlayerStats(stats.first_name, stats.last_name);
        if (existing != null)
        {
            stats.id = existing.id;
            _db.Update(stats);
        }
        else
        {
            _db.Insert(stats);
        }
    }

    public void UpdateHistoricalPlayerStatsFromSeason(int seasonId, int managerId)
    {
        var seasonStats = _db.Query<HistoricalStatsAggregateRow>(
            @"SELECT ps.player_id,
                     COUNT(*) AS games,
                     SUM(ps.points) AS total_points,
                     SUM(ps.rebounds) AS total_rebounds,
                     SUM(ps.assists) AS total_assists,
                     SUM(ps.steals) AS total_steals,
                     SUM(ps.blocks) AS total_blocks,
                     SUM(ps.turnovers) AS total_turnovers,
                     SUM(ps.fgm) AS total_fgm,
                     SUM(ps.fga) AS total_fga,
                     SUM(ps.fg3m) AS total_fg3m,
                     SUM(ps.fg3a) AS total_fg3a,
                     SUM(ps.ftm) AS total_ftm,
                     SUM(ps.fta) AS total_fta,
                     SUM(ps.oreb) AS total_oreb,
                     SUM(ps.dreb) AS total_dreb,
                     SUM(ps.double_double) AS total_double_doubles,
                     SUM(ps.triple_double) AS total_triple_doubles,
                     CAST(SUM(ps.minutes) AS INTEGER) AS total_minutes,
                     SUM(ps.rating) AS total_rating
              FROM player_game_stats ps
              JOIN games g ON ps.game_id = g.id
              WHERE g.season_id = ? AND g.is_played = 1
              GROUP BY ps.player_id",
            seasonId);

        foreach (var ss in seasonStats)
        {
            var player = GetPlayerById(ss.player_id);
            if (player == null) continue;

            var hist = GetHistoricalPlayerStats(player.first_name, player.last_name);
            if (hist == null)
            {
                var team = GetTeamById(player.team_id);
                hist = new HistoricalPlayerStatsData
                {
                    first_name = player.first_name,
                    last_name = player.last_name,
                    position = player.position,
                    overall = player.overall,
                    team_name = team?.name ?? "",
                    team_abbreviation = team?.abbreviation ?? "",
                    team_logo = team?.logo ?? ""
                };
            }

            hist.games += ss.games;
            hist.total_points += ss.total_points;
            hist.total_rebounds += ss.total_rebounds;
            hist.total_assists += ss.total_assists;
            hist.total_steals += ss.total_steals;
            hist.total_blocks += ss.total_blocks;
            hist.total_turnovers += ss.total_turnovers;
            hist.total_fgm += ss.total_fgm;
            hist.total_fga += ss.total_fga;
            hist.total_fg3m += ss.total_fg3m;
            hist.total_fg3a += ss.total_fg3a;
            hist.total_ftm += ss.total_ftm;
            hist.total_fta += ss.total_fta;
            hist.total_oreb += ss.total_oreb;
            hist.total_dreb += ss.total_dreb;
            hist.total_double_doubles += ss.total_double_doubles;
            hist.total_triple_doubles += ss.total_triple_doubles;
            hist.total_minutes += ss.total_minutes;
            hist.total_rating += ss.total_rating;

            var currentTeam = GetTeamById(player.team_id);
            if (currentTeam != null)
            {
                hist.team_name = currentTeam.name;
                hist.team_abbreviation = currentTeam.abbreviation;
                hist.team_logo = currentTeam.logo;
            }

            SaveHistoricalPlayerStats(hist);
        }
    }

    public void SaveSeasonEndRecords(int seasonId, int managerId)
    {
        var season = GetActiveSeason(managerId);
        if (season == null) return;
        string seasonLabel = $"{season.year_start}-{season.year_end.ToString().Substring(2)}";
        Debug.Log($"[DB] Saving season-end records for {seasonLabel}...");

        // ── Finals Record ──
        var finalsGames = _db.Table<GameData>()
            .Where(g => g.manager_id == managerId
                     && g.season_id == seasonId
                     && g.series_label == "playoff-r4-finals"
                     && g.is_played == 1)
            .ToList();

        if (finalsGames.Count > 0)
        {
            int teamA = finalsGames[0].home_team_id;
            int teamB = finalsGames[0].away_team_id;
            var winCount = new Dictionary<int, int>();
            foreach (var g in finalsGames)
            {
                int winner = g.home_score >= g.away_score ? g.home_team_id : g.away_team_id;
                winCount[winner] = winCount.GetValueOrDefault(winner, 0) + 1;
            }

            int champId = winCount.OrderByDescending(kv => kv.Value).First().Key;
            int finalistId = champId == teamA ? teamB : teamA;
            int champWins = winCount[champId];
            int finalistWins = winCount.GetValueOrDefault(finalistId, 0);

            var champTeam = GetTeamById(champId);
            var finalistTeam = GetTeamById(finalistId);

            // Copy Finals player stats to finals_player_stats table
            var finalsGameIds = finalsGames.Select(g => g.id).ToList();
            var allFinalsStats = new List<PlayerGameStats>();
            if (finalsGameIds.Count > 0)
            {
                _db.Execute("DELETE FROM finals_player_stats WHERE game_id IN (" +
                    string.Join(",", finalsGameIds) + ")");
                allFinalsStats = _db.Query<PlayerGameStats>(
                    "SELECT * FROM player_game_stats WHERE game_id IN (" +
                    string.Join(",", finalsGameIds) + ")");
                foreach (var ps in allFinalsStats)
                {
                    _db.Insert(new FinalsPlayerStatsData
                    {
                        game_id = ps.game_id,
                        player_id = ps.player_id,
                        team_id = ps.team_id,
                        minutes = ps.minutes,
                        points = ps.points,
                        fgm = ps.fgm,
                        fga = ps.fga,
                        fg3m = ps.fg3m,
                        fg3a = ps.fg3a,
                        ftm = ps.ftm,
                        fta = ps.fta,
                        oreb = ps.oreb,
                        dreb = ps.dreb,
                        rebounds = ps.rebounds,
                        assists = ps.assists,
                        steals = ps.steals,
                        blocks = ps.blocks,
                        turnovers = ps.turnovers,
                        pf = ps.pf,
                        rating = ps.rating,
                        double_double = ps.double_double,
                        triple_double = ps.triple_double
                    });
                }
            }

            // Finals MVP: player from champ team with best average rating
            string finalsMvp = "";
            var champStats = allFinalsStats.Where(s => s.team_id == champId).ToList();
            if (champStats.Count > 0)
            {
                var topPlayer = champStats
                    .GroupBy(s => s.player_id)
                    .Select(g => new { PlayerId = g.Key, AvgRating = g.Average(s => s.rating) })
                    .OrderByDescending(x => x.AvgRating)
                    .First();
                var mvpPlayer = GetPlayerById(topPlayer.PlayerId);
                if (mvpPlayer != null)
                    finalsMvp = $"{mvpPlayer.first_name} {mvpPlayer.last_name}";
            }

            _db.Insert(new FinalsRecord
            {
                season = seasonLabel,
                champ_name = champTeam?.name ?? "",
                champ_keyword = champTeam?.logo ?? "",
                finalist_name = finalistTeam?.name ?? "",
                finalist_keyword = finalistTeam?.logo ?? "",
                result = $"{champWins}-{finalistWins}",
                mvp = finalsMvp
            });
            Debug.Log($"[DB] FinalsRecord saved: {champTeam?.name} {champWins}-{finalistWins} over {finalistTeam?.name}");
        }

        // ── Season awards & All-NBA quintets (regular season) ──
        var seasonStats = _db.Query<HistoricalStatsAggregateRow>(
            @"SELECT ps.player_id,
                     COUNT(*) AS games,
                     SUM(ps.points) AS total_points,
                     SUM(ps.rebounds) AS total_rebounds,
                     SUM(ps.assists) AS total_assists,
                     SUM(ps.steals) AS total_steals,
                     SUM(ps.blocks) AS total_blocks,
                     SUM(ps.turnovers) AS total_turnovers,
                     SUM(ps.fgm) AS total_fgm,
                     SUM(ps.fga) AS total_fga,
                     SUM(ps.fg3m) AS total_fg3m,
                     SUM(ps.fg3a) AS total_fg3a,
                     SUM(ps.ftm) AS total_ftm,
                     SUM(ps.fta) AS total_fta,
                     SUM(ps.oreb) AS total_oreb,
                     SUM(ps.dreb) AS total_dreb,
                     SUM(ps.double_double) AS total_double_doubles,
                     SUM(ps.triple_double) AS total_triple_doubles,
                     CAST(SUM(ps.minutes) AS INTEGER) AS total_minutes,
                     SUM(ps.rating) AS total_rating
              FROM player_game_stats ps
              JOIN games g ON ps.game_id = g.id
              WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              GROUP BY ps.player_id",
            seasonId);

        if (seasonStats.Count > 0)
        {
            // MVP
            var mvpCandidates = seasonStats.Where(s => s.games >= 65).ToList();
            if (mvpCandidates.Count == 0) mvpCandidates = seasonStats;
            var topMvp = mvpCandidates.OrderByDescending(s => (double)s.total_rating / s.games).First();
            var mvpPlayer = GetPlayerById(topMvp.player_id);
            var mvpTeam = mvpPlayer != null ? GetTeamById(mvpPlayer.team_id) : null;
            string mvpName = mvpPlayer != null ? $"{mvpPlayer.first_name} {mvpPlayer.last_name}" : "";
            string mvpRatingStr = ((double)topMvp.total_rating / Math.Max(1, topMvp.games)).ToString("F1");

            // Rookie of the Year
            var rookieCandidates = seasonStats
                .Where(s =>
                {
                    var p = GetPlayerById(s.player_id);
                    return p != null && p.is_rookie == 1;
                })
                .ToList();
            string rookieName = "", rookieTeamKeyword = "", rookieRatingStr = "";
            if (rookieCandidates.Count > 0)
            {
                var rookiesQualified = rookieCandidates.Where(r => r.games >= 65).ToList();
                if (rookiesQualified.Count == 0) rookiesQualified = rookieCandidates;
                var topRookie = rookiesQualified.OrderByDescending(r => (double)r.total_rating / r.games).First();
                var rookiePlayer = GetPlayerById(topRookie.player_id);
                var rookieTeam = rookiePlayer != null ? GetTeamById(rookiePlayer.team_id) : null;
                rookieName = rookiePlayer != null ? $"{rookiePlayer.first_name} {rookiePlayer.last_name}" : "";
                rookieTeamKeyword = rookieTeam?.logo ?? "";
                rookieRatingStr = ((double)topRookie.total_rating / Math.Max(1, topRookie.games)).ToString("F1");
            }

            _db.Insert(new AwardsRecord
            {
                season = seasonLabel,
                mvp = mvpName,
                mvp_team_keyword = mvpTeam?.logo ?? "",
                mvp_rating = mvpRatingStr,
                rookie = rookieName,
                rookie_team_keyword = rookieTeamKeyword,
                rookie_rating = rookieRatingStr
            });
            Debug.Log($"[DB] AwardsRecord saved: MVP={mvpName}, ROY={rookieName}");

            // All-NBA Quintets
            string[] positions = { "PG", "SG", "SF", "PF", "C" };
            var posValues = new Dictionary<string, (string name, string team)>
            {
                { "PG", ("", "") }, { "SG", ("", "") }, { "SF", ("", "") },
                { "PF", ("", "") }, { "C",  ("", "") }
            };

            foreach (string pos in positions)
            {
                var posPlayers = seasonStats
                    .Where(s =>
                    {
                        var p = GetPlayerById(s.player_id);
                        return p != null && p.position == pos;
                    })
                    .ToList();
                if (posPlayers.Count == 0) continue;

                var qualified = posPlayers.Where(x => x.games >= 65).ToList();
                if (qualified.Count == 0) qualified = posPlayers;
                var best = qualified.OrderByDescending(x => (double)x.total_rating / x.games).First();
                var player = GetPlayerById(best.player_id);
                var team = player != null ? GetTeamById(player.team_id) : null;
                string fullName = player != null ? $"{player.first_name} {player.last_name}" : "";
                string teamKw = team?.logo ?? "";
                posValues[pos] = (fullName, teamKw);
            }

            _db.Insert(new QuintetRecord
            {
                season = seasonLabel,
                pg = posValues["PG"].name,
                pg_team = posValues["PG"].team,
                sg = posValues["SG"].name,
                sg_team = posValues["SG"].team,
                sf = posValues["SF"].name,
                sf_team = posValues["SF"].team,
                pf = posValues["PF"].name,
                pf_team = posValues["PF"].team,
                c = posValues["C"].name,
                c_team = posValues["C"].team
            });
            Debug.Log($"[DB] QuintetRecord saved for {seasonLabel}");
        }

        Debug.Log($"[DB] Season-end records complete for {seasonLabel}");
    }

    // ── GAME ATTENDANCE ───────────────────────────────────

    public void SaveGameAttendance(GameAttendanceData attendance)
    {
        var existing = _db.Table<GameAttendanceData>()
                          .Where(a => a.game_id == attendance.game_id)
                          .FirstOrDefault();
        if (existing != null)
        {
            attendance.game_id = existing.game_id;
            _db.Update(attendance);
        }
        else
        {
            _db.Insert(attendance);
        }
    }

    public GameAttendanceData GetGameAttendance(int gameId)
    {
        return _db.Table<GameAttendanceData>()
                  .Where(a => a.game_id == gameId)
                  .FirstOrDefault();
    }

    // ── FINALS PLAYER STATS ───────────────────────────────

    public void SaveFinalsPlayerStats(FinalsPlayerStatsData stats)
    {
        _db.Insert(stats);
    }

    public List<FinalsPlayerStatsData> GetFinalsPlayerStats(int gameId)
    {
        return _db.Table<FinalsPlayerStatsData>()
                  .Where(s => s.game_id == gameId)
                  .OrderByDescending(s => s.points)
                  .ToList();
    }

    public List<FinalsPlayerStatsData> GetFinalsPlayerStatsByTeam(int gameId, int teamId)
    {
        return _db.Table<FinalsPlayerStatsData>()
                  .Where(s => s.game_id == gameId && s.team_id == teamId)
                  .OrderByDescending(s => s.points)
                  .ToList();
    }

    public FinalsMVPDetails GetFinalsMVPDetails(int seasonId, int managerId)
    {
        if (!EnsureDb()) return null;

        var finalsGames = _db.Table<GameData>()
            .Where(g => g.manager_id == managerId
                     && g.season_id == seasonId
                     && g.series_label == "playoff-r4-finals"
                     && g.is_played == 1)
            .ToList();

        if (finalsGames.Count == 0) return null;

        // Determine champion team
        var winCount = new Dictionary<int, int>();
        foreach (var g in finalsGames)
        {
            int winner = g.home_score >= g.away_score ? g.home_team_id : g.away_team_id;
            winCount[winner] = winCount.GetValueOrDefault(winner, 0) + 1;
        }
        int champId = winCount.OrderByDescending(kv => kv.Value).First().Key;

        // Get all finals player stats for champion team
        var finalsGameIds = finalsGames.Select(g => g.id).ToList();
        var champStats = new List<FinalsPlayerStatsData>();
        if (finalsGameIds.Count > 0)
        {
            champStats = _db.Query<FinalsPlayerStatsData>(
                "SELECT * FROM finals_player_stats WHERE game_id IN (" +
                string.Join(",", finalsGameIds) + ") AND team_id = " + champId);
        }

        if (champStats.Count == 0) return null;

        // Group by player, compute averages, pick best avg rating
        var topPlayer = champStats
            .GroupBy(s => s.player_id)
            .Select(g => new
            {
                PlayerId = g.Key,
                AvgRating = g.Average(s => s.rating),
                AvgPts = g.Average(s => s.points),
                AvgReb = g.Average(s => s.rebounds),
                AvgAst = g.Average(s => s.assists),
                GamesPlayed = g.Count()
            })
            .Where(x => x.GamesPlayed >= 2)
            .OrderByDescending(x => x.AvgRating)
            .FirstOrDefault();

        if (topPlayer == null) return null;

        var player = GetPlayerById(topPlayer.PlayerId);
        if (player == null) return null;

        var champTeam = GetTeamById(champId);

        return new FinalsMVPDetails
        {
            PlayerName = $"{player.first_name} {player.last_name}",
            TeamName = champTeam?.name ?? "",
            AvgPts = (float)topPlayer.AvgPts,
            AvgReb = (float)topPlayer.AvgReb,
            AvgAst = (float)topPlayer.AvgAst
        };
    }

    // ── PLAYER AWARDS ────────────────────────────────────

    public PlayerAwardInfo GetRegularSeasonMVP(int seasonId, int managerId)
    {
        return QueryTopPlayer(seasonId, managerId, null, 65);
    }

    public PlayerAwardInfo GetRookieOfYear(int seasonId, int managerId)
    {
        return QueryTopPlayer(seasonId, managerId, true, 65);
    }

    public List<PlayerAwardInfo> GetAllStarTeam(int seasonId, int managerId)
    {
        return GetBestPerPosition(seasonId, managerId, null, 65);
    }

    public List<PlayerAwardInfo> GetAllRookieTeam(int seasonId, int managerId)
    {
        return GetBestPerPosition(seasonId, managerId, true, 65);
    }

    PlayerAwardInfo QueryTopPlayer(int seasonId, int managerId, bool? rookieOnly, int minGames)
    {
        if (!EnsureDb()) return null;
        string rookieFilter = rookieOnly == true ? "AND p.is_rookie = 1" : "";
        string sql = $@"
            SELECT p.id, p.first_name, p.last_name, p.position, t.name AS team_name, t.logo AS team_logo,
                   COUNT(*) AS games,
                   AVG(ps.points) AS avg_pts,
                   AVG(ps.rebounds) AS avg_reb,
                   AVG(ps.assists) AS avg_ast,
                   AVG(ps.rating) AS avg_rating
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN players p ON ps.player_id = p.id
            JOIN teams t ON p.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.manager_id = ? {rookieFilter}
            GROUP BY ps.player_id
            HAVING games >= ?
            ORDER BY avg_rating DESC
            LIMIT 1";
        var row = _db.Query<PlayerAwardQueryRow>(sql, seasonId, managerId, minGames).FirstOrDefault();
        if (row == null) return null;
        return new PlayerAwardInfo
        {
            PlayerName = $"{row.first_name} {row.last_name}",
            TeamName = row.team_name ?? "",
            TeamKeyword = row.team_logo ?? "",
            Position = row.position ?? "",
            AvgPts = (float)row.avg_pts,
            AvgReb = (float)row.avg_reb,
            AvgAst = (float)row.avg_ast,
            AvgRating = (float)row.avg_rating
        };
    }

    List<PlayerAwardInfo> GetBestPerPosition(int seasonId, int managerId, bool? rookieOnly, int minGames)
    {
        if (!EnsureDb()) return new List<PlayerAwardInfo>();
        string rookieFilter = rookieOnly == true ? "AND p.is_rookie = 1" : "";
        var result = new List<PlayerAwardInfo>();
        string[] positions = { "PG", "SG", "SF", "PF", "C" };
        foreach (var pos in positions)
        {
            string sql = $@"
                SELECT p.id, p.first_name, p.last_name, p.position, t.name AS team_name, t.logo AS team_logo,
                       COUNT(*) AS games,
                       AVG(ps.points) AS avg_pts,
                       AVG(ps.rebounds) AS avg_reb,
                       AVG(ps.assists) AS avg_ast,
                       AVG(ps.rating) AS avg_rating
                FROM player_game_stats ps
                JOIN games g ON ps.game_id = g.id
                JOIN players p ON ps.player_id = p.id
                JOIN teams t ON p.team_id = t.id
                WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
                  AND g.manager_id = ? AND p.position = ? {rookieFilter}
                GROUP BY ps.player_id
                HAVING games >= ?
                ORDER BY avg_rating DESC
                LIMIT 1";
            var row = _db.Query<PlayerAwardQueryRow>(sql, seasonId, managerId, pos, minGames).FirstOrDefault();
            if (row != null)
            {
                result.Add(new PlayerAwardInfo
                {
                    PlayerName = $"{row.first_name} {row.last_name}",
                    TeamName = row.team_name ?? "",
                    TeamKeyword = row.team_logo ?? "",
                    Position = row.position ?? "",
                    AvgPts = (float)row.avg_pts,
                    AvgReb = (float)row.avg_reb,
                    AvgAst = (float)row.avg_ast,
                    AvgRating = (float)row.avg_rating
                });
            }
        }
        return result;
    }

    // ── RECORDS TRACKING ──────────────────────────────────

    public List<HistoricalRecordData> GetAllHistoricalRecords()
    {
        if (!EnsureDb()) return new List<HistoricalRecordData>();
        return _db.Table<HistoricalRecordData>().ToList();
    }

    public List<TeamRecordData> GetTeamRecords(int teamId)
    {
        if (!EnsureDb()) return new List<TeamRecordData>();
        return _db.Table<TeamRecordData>()
                  .Where(r => r.team_id == teamId)
                  .ToList();
    }

    public List<SeasonRecord> GetAllSeasonRecords(int seasonId)
    {
        if (!EnsureDb()) return new List<SeasonRecord>();
        return _db.Table<SeasonRecord>()
                  .Where(r => r.season_id == seasonId)
                  .ToList();
    }

    public HistoricalRecordData GetHistoricalRecord(string statType)
    {
        if (!EnsureDb()) return null;
        return _db.Table<HistoricalRecordData>()
                  .Where(r => r.stat_type == statType)
                  .FirstOrDefault();
    }

    public TeamRecordData GetTeamRecord(int teamId, string statType)
    {
        if (!EnsureDb()) return null;
        return _db.Table<TeamRecordData>()
                  .Where(r => r.team_id == teamId && r.stat_type == statType)
                  .FirstOrDefault();
    }

    public List<SeasonGameRecordData> GetCurrentSeasonRecords(int seasonId)
    {
        if (!EnsureDb()) return new List<SeasonGameRecordData>();
        var all = _db.Table<SeasonGameRecordData>()
                     .Where(r => r.season_id == seasonId)
                     .ToList();
        // Pick highest value per stat_type
        var result = new List<SeasonGameRecordData>();
        var seen = new HashSet<string>();
        foreach (var r in all.OrderByDescending(r => r.value))
        {
            if (seen.Add(r.stat_type))
                result.Add(r);
        }
        return result;
    }

    public SeasonGameRecordData GetSeasonGameRecord(int teamId, int seasonId, string statType)
    {
        if (!EnsureDb()) return null;
        return _db.Table<SeasonGameRecordData>()
                  .Where(r => r.team_id == teamId && r.season_id == seasonId && r.stat_type == statType)
                  .FirstOrDefault();
    }

    public void CheckAndUpdateRecords(GameData game, List<GameSimulator.PlayerStatSnapshot> playerStats, int teamId)
    {
        var team = GetTeamById(teamId);
        if (team == null) return;

        string[] statFields = { "points", "rebounds", "assists", "steals", "blocks", "fgm", "fg3m", "ftm", "turnovers" };

        foreach (var ps in playerStats)
        {
            var player = GetPlayerById(ps.player_id);
            if (player == null) continue;

            string playerName = $"{player.first_name} {player.last_name}";

            foreach (var stat in statFields)
            {
                int value = stat switch
                {
                    "rebounds" => ps.oreb + ps.dreb,
                    "points" => ps.points,
                    "assists" => ps.assists,
                    "steals" => ps.steals,
                    "blocks" => ps.blocks,
                    "fgm" => ps.fgm,
                    "fg3m" => ps.fg3m,
                    "ftm" => ps.ftm,
                    "turnovers" => ps.turnovers,
                    _ => 0
                };

                if (value <= 0) continue;

                // Historical Record
                var histRecord = GetHistoricalRecord(stat);
                if (histRecord == null || value > histRecord.value)
                {
                    if (histRecord == null)
                    {
                        histRecord = new HistoricalRecordData
                        {
                            stat_type = stat,
                            player_name = playerName,
                            value = value,
                            game_date = game.game_date,
                            team_abbreviation = team.abbreviation
                        };
                        _db.Insert(histRecord);
                    }
                    else
                    {
                        histRecord.player_name = playerName;
                        histRecord.value = value;
                        histRecord.game_date = game.game_date;
                        histRecord.team_abbreviation = team.abbreviation;
                        _db.Update(histRecord);
                    }
                }

                // Team Record
                var teamRecord = GetTeamRecord(teamId, stat);
                if (teamRecord == null || value > teamRecord.value)
                {
                    if (teamRecord == null)
                    {
                        teamRecord = new TeamRecordData
                        {
                            team_id = teamId,
                            stat_type = stat,
                            player_name = playerName,
                            value = value,
                            game_date = game.game_date
                        };
                        _db.Insert(teamRecord);
                    }
                    else
                    {
                        teamRecord.player_name = playerName;
                        teamRecord.value = value;
                        teamRecord.game_date = game.game_date;
                        _db.Update(teamRecord);
                    }
                }

                // Season Game Record
                var season = GetActiveSeason(GetActiveManager()?.id ?? 0);
                if (season != null)
                {
                    var seasonRecord = GetSeasonGameRecord(teamId, season.id, stat);
                    if (seasonRecord == null || value > seasonRecord.value)
                    {
                        if (seasonRecord == null)
                        {
                            seasonRecord = new SeasonGameRecordData
                            {
                                team_id = teamId,
                                season_id = season.id,
                                stat_type = stat,
                                player_name = playerName,
                                value = value,
                                game_date = game.game_date
                            };
                            _db.Insert(seasonRecord);
                        }
                        else
                        {
                            seasonRecord.player_name = playerName;
                            seasonRecord.value = value;
                            seasonRecord.game_date = game.game_date;
                            _db.Update(seasonRecord);
                        }
                    }
                }
            }
        }
    }

    // ── MESSAGES ──────────────────────────────────────────

    public void AddMessage(MessageData message)
    {
        _db.Insert(message);
        Debug.Log($"[DB] AddMessage OK: id={message.id} title='{message.title}' game_day={message.game_day} manager_id={message.manager_id}");
    }

    public List<MessageData> GetMessages(int managerId)
    {
        if (!EnsureDb()) return new List<MessageData>();
        return _db.Table<MessageData>()
                  .Where(m => m.manager_id == managerId)
                  .OrderByDescending(m => m.date_sent)
                  .ToList();
    }

    public void MarkMessageRead(int messageId)
    {
        if (!EnsureDb()) return;
        var message = _db.Table<MessageData>().Where(m => m.id == messageId).FirstOrDefault();
        if (message != null)
        {
            message.is_read = 1;
            _db.Update(message);
        }
    }

    public void DeleteMessage(int messageId)
    {
        if (!EnsureDb()) return;
        _db.Delete<MessageData>(messageId);
    }

    // ── OFFERS ──────────────────────────────────────────────

    public void AddOffer(OfferData offer)
    {
        if (!EnsureDb()) return;
        _db.Insert(offer);
        Debug.Log($"[DB] AddOffer OK: player={offer.player_id} salary={offer.offer_salary} years={offer.offer_years}");
    }

    public List<OfferData> GetMaturedUnprocessedOffers(int managerId, int currentDay)
    {
        if (!EnsureDb()) return new List<OfferData>();
        var all = _db.Table<OfferData>().Where(o => o.manager_id == managerId).ToList();
        Debug.Log($"[DB] GetMaturedUnprocessedOffers: total offers for manager={managerId}: {all.Count}");
        foreach (var o in all)
            Debug.Log($"[DB]   offer id={o.id} player={o.player_id} day_sent={o.day_sent} processed={o.processed} currentDay={currentDay} mature={currentDay >= o.day_sent + 7}");
        return all.Where(o => o.processed == 0 && currentDay >= o.day_sent + 7).ToList();
    }

    public int GetPendingFAOfferCount(int managerId)
    {
        if (!EnsureDb()) return 0;
        return _db.Table<OfferData>().Count(o => o.manager_id == managerId && o.offer_type == 1 && o.processed == 0);
    }

    public HashSet<int> GetPendingFAPlayerIds(int managerId)
    {
        if (!EnsureDb()) return new HashSet<int>();
        return new HashSet<int>(_db.Table<OfferData>()
            .Where(o => o.manager_id == managerId && o.offer_type == 1 && o.processed == 0)
            .Select(o => o.player_id));
    }

    public void MarkOfferProcessed(int offerId)
    {
        if (!EnsureDb()) return;
        var offer = _db.Table<OfferData>().FirstOrDefault(o => o.id == offerId);
        if (offer != null)
        {
            offer.processed = 1;
            _db.Update(offer);
        }
    }

    // ── TRADE OFFERS ───────────────────────────────────────

    public void AddTradeOffer(TradeOfferData offer)
    {
        if (!EnsureDb()) return;
        _db.Insert(offer);
    }

    public List<TradeOfferData> GetPendingTradeOffers(int managerId)
    {
        if (!EnsureDb()) return new List<TradeOfferData>();
        return _db.Table<TradeOfferData>()
            .Where(o => o.manager_id == managerId && o.processed == 0)
            .ToList();
    }

    public void MarkTradeOfferProcessed(int offerId, int status)
    {
        if (!EnsureDb()) return;
        var offer = _db.Table<TradeOfferData>().FirstOrDefault(o => o.id == offerId);
        if (offer != null)
        {
            offer.processed = status;
            _db.Update(offer);
        }
    }

    // ── TRADES ─────────────────────────────────────────────

    public void InsertTrade(TradeData trade)
    {
        _db.Insert(trade);
        Debug.Log($"[DB] InsertTrade OK: id={trade.id} player={trade.player_id} {trade.team_id_from}->{trade.team_id_to}");
    }

    public List<TradeData> GetTradesBySeason(int seasonId)
    {
        if (!EnsureDb()) return new List<TradeData>();
        return _db.Table<TradeData>()
                  .Where(t => t.season_id == seasonId)
                  .OrderByDescending(t => t.game_day)
                  .ToList();
    }

    public bool HasTeamTradedThisSeason(int teamId, int seasonId)
    {
        if (!EnsureDb()) return false;
        return _db.Table<TradeData>()
                  .Where(t => t.season_id == seasonId && (t.team_id_from == teamId || t.team_id_to == teamId))
                  .Count() > 0;
    }

    public void StartNewSeason(int oldSeasonId, int newTeamId, string gameMode, int managerId)
    {
        // 0. Archive historical stats BEFORE clearing tables
        var oldSeason = _db.Find<SeasonData>(oldSeasonId);
        if (oldSeason != null)
            UpdateHistoricalPlayerStatsFromSeason(oldSeasonId, managerId);

        var allPlayers = _db.Table<PlayerData>().ToList();

        // 1. Retire players 40+
        foreach (var p in allPlayers.Where(p => p.age >= 40))
            _db.Delete(p);

        // 2. Age + attribute changes (progression/regression by career phase)
        var remaining = _db.Table<PlayerData>().ToList();
        foreach (var p in remaining)
        {
            p.age += 1;

            // Base change by age group
            int baseChange;
            if (p.age <= 22) baseChange = 4;       // Crecimiento rápido
            else if (p.age <= 27) baseChange = 1;  // Prime temprano
            else if (p.age <= 30) baseChange = 0;  // Prime tardío
            else if (p.age <= 34) baseChange = -3; // Declive suave
            else baseChange = -5;                   // Declive fuerte

            // Position priority attributes (get +1 extra)
            var priorityAttrs = new HashSet<string>();
            switch (p.position)
            {
                case "PG":
                    priorityAttrs = new HashSet<string> { "passing", "dribbling", "speed", "iq", "three_point" };
                    break;
                case "SG":
                    priorityAttrs = new HashSet<string> { "shooting", "three_point", "speed", "dribbling", "steals" };
                    break;
                case "SF":
                    priorityAttrs = new HashSet<string> { "shooting", "defense", "athleticism", "speed", "rebounding" };
                    break;
                case "PF":
                    priorityAttrs = new HashSet<string> { "defense", "rebounding", "blocks", "athleticism" };
                    break;
                case "C":
                    priorityAttrs = new HashSet<string> { "rebounding", "blocks", "defense", "iq", "athleticism" };
                    break;
            }

            int Apply(string name, int current)
            {
                int change = baseChange;
                if (priorityAttrs.Contains(name)) change += 1;
                change += UnityEngine.Random.Range(-1, 2);
                return Math.Max(0, Math.Min(99, current + change));
            }

            p.speed = Apply("speed", p.speed);
            p.shooting = Apply("shooting", p.shooting);
            p.three_point = Apply("three_point", p.three_point);
            p.passing = Apply("passing", p.passing);
            p.dribbling = Apply("dribbling", p.dribbling);
            p.defense = Apply("defense", p.defense);
            p.rebounding = Apply("rebounding", p.rebounding);
            p.athleticism = Apply("athleticism", p.athleticism);
            p.iq = Apply("iq", p.iq);
            p.steals = Apply("steals", p.steals);
            p.blocks = Apply("blocks", p.blocks);

            // Recalculate overall as average of all attributes, capped by potential
            int sum = p.speed + p.shooting + p.three_point + p.passing + p.dribbling +
                      p.defense + p.rebounding + p.athleticism + p.iq + p.steals + p.blocks;
            p.overall = (int)System.Math.Round(sum / 11f);
            if (p.overall > p.potential)
                p.overall = p.potential;

            // Save old team for seasons_with_team tracking
            int oldTeamId = p.team_id;

            // 3. Decrement contracts
            p.contract_years -= 1;
            if (p.contract_years <= 0)
            {
                p.contract_years = 0;
                p.team_id = 0;
            }

            // Track team changes for seasons_with_team
            if (p.team_id == 0)
            {
                // Free agent — keep current seasons_with_team
            }
            else if (oldTeamId == p.team_id)
            {
                p.seasons_with_team += 1;  // Same team
            }
            else
            {
                p.seasons_with_team = 1;   // New team (traded, or FA signed)
            }

            _db.Update(p);
        }

        // 3b. Decrement employee contracts
        var allEmployees = _db.Table<EmployeeData>().Where(e => e.team_id != 0).ToList();
        foreach (var emp in allEmployees)
        {
            emp.contract_years -= 1;
            if (emp.contract_years <= 0)
                _db.Delete(emp);
            else
                _db.Update(emp);
        }

        // 4. Decrement sponsor/TV contracts for all teams
        foreach (var team in GetAllTeams())
        {
            var settings = GetTeamSettings(team.id);
            if (settings == null) continue;

            bool changed = false;
            if (settings.sponsor_years_remaining > 0)
            {
                settings.sponsor_years_remaining -= 1;
                if (settings.sponsor_years_remaining <= 0)
                {
                    settings.sponsor_id = 0;
                    // Fire the sponsor so it becomes available again
                    var activeSponsor = GetActiveSponsor(team.id);
                    if (activeSponsor != null)
                        FireSponsor(activeSponsor);
                }
                changed = true;
            }
            if (settings.tv_years_remaining > 0)
            {
                settings.tv_years_remaining -= 1;
                if (settings.tv_years_remaining <= 0)
                {
                    settings.tv_channel_id = 0;
                    var activeChannel = GetActiveTVChannel(team.id);
                    if (activeChannel != null)
                        FireTVChannel(activeChannel);
                }
                changed = true;
            }
            if (changed)
                UpdateTeamSettings(settings);
        }

        // 5. Clear tables
        _db.Execute("DELETE FROM player_game_stats");
        _db.Execute("DELETE FROM finals_player_stats");
        _db.Execute("DELETE FROM games");
        _db.Execute("DELETE FROM messages");
        _db.Execute("DELETE FROM game_attendance");
        _db.Execute("DELETE FROM finance_records");

        // 6. Fill rosters to 12 for all teams (except the user's new team)
        var allTeams = GetAllTeams();
        var freeAgents = _db.Table<PlayerData>()
            .Where(p => p.team_id == 0 && p.age < 40)
            .OrderByDescending(p => p.overall)
            .ToList();

        // Sort teams: fill user's new team last to maximize free agent pool for AI
        var teamsToFill = allTeams.Where(t => t.id != newTeamId).ToList();

        foreach (var team in teamsToFill)
        {
            var roster = GetPlayersByTeam(team.id);
            int need = 12 - roster.Count;
            if (need <= 0) continue;

            var posCounts = new Dictionary<string, int>();
            foreach (string pos in new[] { "PG", "SG", "SF", "PF", "C" })
                posCounts[pos] = roster.Count(p => p.position == pos);

            for (int i = 0; i < need && freeAgents.Count > 0; i++)
            {
                string minPos = posCounts.OrderBy(kv => kv.Value).First().Key;

                PlayerData signed = null;
                foreach (var fa in freeAgents)
                {
                    if (fa.position == minPos)
                    {
                        signed = fa;
                        break;
                    }
                }
                if (signed == null && freeAgents.Count > 0)
                    signed = freeAgents[0];

                if (signed != null)
                {
                    signed.team_id = team.id;
                    signed.contract_years = Math.Max(1, 4 - signed.age / 10);
                    signed.seasons_with_team = 1;
                    _db.Update(signed);
                    freeAgents.Remove(signed);
                    posCounts[signed.position] = posCounts.GetValueOrDefault(signed.position) + 1;
                }
            }
        }

        // Now fill user's team to 12 if needed
        {
            var userRoster = GetPlayersByTeam(newTeamId);
            int need = 12 - userRoster.Count;
            if (need > 0)
            {
                var posCounts = new Dictionary<string, int>();
                foreach (string pos in new[] { "PG", "SG", "SF", "PF", "C" })
                    posCounts[pos] = userRoster.Count(p => p.position == pos);

                for (int i = 0; i < need && freeAgents.Count > 0; i++)
                {
                    string minPos = posCounts.OrderBy(kv => kv.Value).First().Key;

                    PlayerData signed = null;
                    foreach (var fa in freeAgents)
                    {
                        if (fa.position == minPos)
                        {
                            signed = fa;
                            break;
                        }
                    }
                    if (signed == null && freeAgents.Count > 0)
                        signed = freeAgents[0];

                    if (signed != null)
                    {
                        signed.team_id = newTeamId;
                        signed.contract_years = Math.Max(1, 4 - signed.age / 10);
                        signed.seasons_with_team = 1;
                        _db.Update(signed);
                        freeAgents.Remove(signed);
                        posCounts[signed.position] = posCounts.GetValueOrDefault(signed.position) + 1;
                    }
                }
            }
        }

        // Seed relationships for user's team
        var userPlayers = GetPlayersByTeam(newTeamId);
        if (userPlayers.Count >= 2)
        {
            SeedTeamPersonalities(newTeamId, userPlayers);
            SeedTeamRelationships(newTeamId, userPlayers);
        }
        AutoSeedLineup(newTeamId, userPlayers);

        // 7. Increase salary cap by 5%
        var leagueSettings = GetLeagueSettings();
        if (leagueSettings != null)
        {
            leagueSettings.salary_cap = (long)(leagueSettings.salary_cap * 1.05);
            leagueSettings.luxury_tax = (long)(leagueSettings.luxury_tax * 1.05);
            leagueSettings.apron = (long)(leagueSettings.apron * 1.05);
            leagueSettings.repeater_apron = (long)(leagueSettings.repeater_apron * 1.05);
            leagueSettings.mid_level = (long)(leagueSettings.mid_level * 1.05);
            leagueSettings.bi_annual = (long)(leagueSettings.bi_annual * 1.05);
            leagueSettings.minimum_salary = (long)(leagueSettings.minimum_salary * 1.05);
            _db.Update(leagueSettings);
        }

        // 8. Deactivate old season
        if (oldSeason != null)
        {
            oldSeason.is_active = 0;
            _db.Update(oldSeason);
        }

        // 9. Assign random sponsors/TV to teams without one
        var availableSponsors = _db.Table<SponsorData>().Where(s => s.is_active == 1).ToList();
        var availableChannels = _db.Table<TvChannelData>().Where(c => c.is_active == 1).ToList();

        foreach (var team in allTeams)
        {
            var settings = GetTeamSettings(team.id);
            if (settings == null) continue;

            if (settings.sponsor_id == 0 && availableSponsors.Count > 0)
            {
                var rngSp = new System.Random();
                var pick = availableSponsors[rngSp.Next(availableSponsors.Count)];
                SignSponsor(pick.id, 0, team.id);
                // Re-read available sponsors (the signed one is now assigned)
                availableSponsors = _db.Table<SponsorData>().Where(s => s.is_active == 1).ToList();
            }

            if (settings.tv_channel_id == 0 && availableChannels.Count > 0)
            {
                var rngTv = new System.Random();
                var pick = availableChannels[rngTv.Next(availableChannels.Count)];
                SignTVChannel(pick.id, 0, team.id);
                availableChannels = _db.Table<TvChannelData>().Where(c => c.is_active == 1).ToList();
            }
        }

        // 10. Create new season (preseason)
        int newYearStart = oldSeason != null ? oldSeason.year_start + 1 : 2027;
        var newSeason = new SeasonData
        {
            year_start = newYearStart,
            year_end = newYearStart + 1,
            is_active = 1,
            current_game_day = 0,
            current_date = $"{newYearStart}-09-05",
            game_mode = gameMode,
            phase = "preseason",
            manager_id = managerId,
            generated = 0
        };
        _db.Insert(newSeason);
    }

    public PlayerPersonalityData GetPlayerPersonality(int playerId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<PlayerPersonalityData>()
                  .Where(p => p.player_id == playerId)
                  .FirstOrDefault();
    }

    public List<PlayerPersonalityData> GetTeamPersonalities(int teamId)
    {
        if (!EnsureDb()) return new List<PlayerPersonalityData>();
        return _db.Table<PlayerPersonalityData>()
                  .Where(p => p.team_id == teamId)
                  .ToList();
    }

    public void InsertOrUpdatePersonality(PlayerPersonalityData personality)
    {
        if (!EnsureDb()) return;
        var existing = GetPlayerPersonality(personality.player_id);
        if (existing != null)
        {
            personality.id = existing.id;
            _db.Update(personality);
        }
        else
        {
            _db.Insert(personality);
        }
    }

    public List<PlayerRelationshipData> GetTeamRelationships(int teamId)
    {
        if (!EnsureDb()) return new List<PlayerRelationshipData>();
        return _db.Table<PlayerRelationshipData>()
                  .Where(r => r.team_id == teamId)
                  .ToList();
    }

    public PlayerRelationshipData GetRelationship(int playerAId, int playerBId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<PlayerRelationshipData>()
                  .Where(r => (r.player_a_id == playerAId && r.player_b_id == playerBId)
                           || (r.player_a_id == playerBId && r.player_b_id == playerAId))
                  .FirstOrDefault();
    }

    public void InsertOrUpdateRelationship(PlayerRelationshipData relationship)
    {
        if (!EnsureDb()) return;
        var existing = GetRelationship(relationship.player_a_id, relationship.player_b_id);
        if (existing != null)
        {
            relationship.id = existing.id;
            _db.Update(relationship);
        }
        else
        {
            _db.Insert(relationship);
        }
    }

    static readonly string[] PersonalityTypes = {
        "Líder", "Mentor", "Estrella", "Guerrero", "Tranquilo", "Intenso", "Profesional", "Novato"
    };

    static readonly (string t1, string t2, int compatMod)[][] PersonalityTraits = {
        new[] {("Carismático", "Motivador", 15), ("Comunicativo", "Exigente", 10)},        // Líder
        new[] {("Paciente", "Generoso", 12), ("Sabio", "Protector", 10)},                   // Mentor
        new[] {("Orgulloso", "Exigente", 0), ("Carismático", "Sensible", -5)},              // Estrella
        new[] {("Resiliente", "Competitivo", 10), ("Disciplinado", "Feroz", 8)},            // Guerrero
        new[] {("Respetuoso", "Estable", 8), ("Pacífico", "Constante", 5)},                 // Tranquilo
        new[] {("Apasionado", "Explosivo", 0), ("Competitivo", "Impulsivo", -8)},           // Intenso
        new[] {("Disciplinado", "Constante", 12), ("Responsable", "Puntual", 10)},          // Profesional
        new[] {("Entusiasta", "Respetuoso", 10), ("Hambriento", "Inquieto", 5)}             // Novato
    };

    public void SeedTeamPersonalities(int teamId, List<PlayerData> players)
    {
        if (!EnsureDb()) return;
        var rng = new System.Random();
        foreach (var p in players)
        {
            if (GetPlayerPersonality(p.id) != null) continue;
            int typeIdx = rng.Next(PersonalityTypes.Length);
            var traitPair = PersonalityTraits[typeIdx][rng.Next(2)];
            var data = new PlayerPersonalityData
            {
                player_id = p.id,
                team_id = teamId,
                personality_type = PersonalityTypes[typeIdx],
                trait_1 = traitPair.t1,
                trait_2 = traitPair.t2,
                compatibility_modifier = traitPair.compatMod
            };
            _db.Insert(data);
        }
    }

    public void SeedTeamRelationships(int teamId, List<PlayerData> players)
    {
        if (!EnsureDb()) return;
        var rng = new System.Random();
        for (int i = 0; i < players.Count; i++)
        {
            for (int j = i + 1; j < players.Count; j++)
            {
                if (GetRelationship(players[i].id, players[j].id) != null) continue;
                int compatMod = 0;
                var pA = GetPlayerPersonality(players[i].id);
                var pB = GetPlayerPersonality(players[j].id);
                if (pA != null && pB != null)
                    compatMod = (pA.compatibility_modifier + pB.compatibility_modifier) / 2;
                int bond = Mathf.Clamp(50 + compatMod + rng.Next(-12, 13), 1, 99);
                _db.Insert(new PlayerRelationshipData
                {
                    team_id = teamId,
                    player_a_id = players[i].id,
                    player_b_id = players[j].id,
                    bond = bond
                });
            }
        }
    }

    public void EnsureTeamRelationshipsSeeded(int teamId)
    {
        var players = GetPlayersByTeam(teamId);
        if (players.Count < 2) return;
        SeedTeamPersonalities(teamId, players);
        SeedTeamRelationships(teamId, players);
    }

    public void UpdateRelationshipsAfterGame(int teamId, int gameId, bool isWin, List<int> playedPlayerIds)
    {
        var rels = GetTeamRelationships(teamId);
        if (rels.Count == 0) return;
        var rng = new System.Random();
        foreach (var rel in rels)
        {
            bool aPlayed = playedPlayerIds.Contains(rel.player_a_id);
            bool bPlayed = playedPlayerIds.Contains(rel.player_b_id);
            int delta;
            if (aPlayed && bPlayed)
                delta = isWin ? rng.Next(1, 4) : rng.Next(0, 2);
            else if (aPlayed || bPlayed)
                delta = rng.Next(-1, 1);
            else
                delta = rng.Next(-2, 0);
            rel.bond = Mathf.Clamp(rel.bond + delta, 1, 99);
            _db.Update(rel);
        }
    }

    public List<LineupData> GetTeamLineup(int teamId)
    {
        if (!EnsureDb()) return new List<LineupData>();
        return _db.Table<LineupData>()
                  .Where(l => l.team_id == teamId)
                  .ToList();
    }

    public LineupData GetPlayerLineupSlot(int playerId)
    {
        if (!EnsureDb()) return null;
        return _db.Table<LineupData>()
                  .Where(l => l.player_id == playerId)
                  .FirstOrDefault();
    }

    public List<LineupData> GetStarters(int teamId)
    {
        return GetTeamLineup(teamId).Where(l => l.slot == 0).ToList();
    }

    public List<LineupData> GetBench(int teamId)
    {
        return GetTeamLineup(teamId).Where(l => l.slot == 1).ToList();
    }

    public List<LineupData> GetInactive(int teamId)
    {
        return GetTeamLineup(teamId).Where(l => l.slot == 2).ToList();
    }

    public void DeleteLineupEntry(int id)
    {
        if (!EnsureDb()) return;
        var entry = _db.Table<LineupData>().FirstOrDefault(l => l.id == id);
        if (entry != null)
            _db.Delete(entry);
    }

    public void SetPlayerSlot(int playerId, int teamId, int slot)
    {
        if (!EnsureDb()) return;
        var existing = GetPlayerLineupSlot(playerId);
        if (existing != null)
        {
            existing.slot = slot;
            existing.slot_index = -1;
            _db.Update(existing);
        }
        else
        {
            _db.Insert(new LineupData
            {
                player_id = playerId,
                team_id = teamId,
                slot = slot,
                slot_index = -1
            });
        }
    }

    public void SetPlayerSlot(int playerId, int teamId, int slot, int slotIndex)
    {
        if (!EnsureDb()) return;
        var existing = GetPlayerLineupSlot(playerId);
        if (existing != null)
        {
            existing.slot = slot;
            existing.slot_index = slotIndex;
            _db.Update(existing);
        }
        else
        {
            _db.Insert(new LineupData
            {
                player_id = playerId,
                team_id = teamId,
                slot = slot,
                slot_index = slotIndex
            });
        }
    }

    public void AutoSeedLineup(int teamId, List<PlayerData> players, HashSet<int> forceInactiveIds = null)
    {
        if (!EnsureDb()) return;
        if (players.Count == 0) return;

        // Remove any existing lineup for this team
        var existing = GetTeamLineup(teamId);
        foreach (var e in existing)
            _db.Delete(e);

        var assigned = new HashSet<int>();
        if (forceInactiveIds != null)
            assigned.UnionWith(forceInactiveIds);

        var posOrder = new[] { "PG", "SG", "SF", "PF", "C" };

        // Assign best player at each position as starter
        for (int si = 0; si < posOrder.Length; si++)
        {
            var best = players
                .Where(p => p.position == posOrder[si] && !assigned.Contains(p.id))
                .OrderByDescending(p => p.overall)
                .FirstOrDefault();
            if (best != null)
            {
                _db.Insert(new LineupData
                {
                    player_id = best.id,
                    team_id = teamId,
                    slot = 0,
                    slot_index = si
                });
                assigned.Add(best.id);
            }
        }

        // Fill bench with the next best unassigned players (up to 7 bench = 12 total active)
        var remaining = players
            .Where(p => !assigned.Contains(p.id))
            .OrderByDescending(p => p.overall)
            .ToList();

        int maxActive = 12;
        int benchSlots = Mathf.Min(remaining.Count, maxActive - assigned.Count);
        for (int i = 0; i < benchSlots; i++)
        {
            _db.Insert(new LineupData
            {
                player_id = remaining[i].id,
                team_id = teamId,
                slot = 1,
                slot_index = i
            });
            assigned.Add(remaining[i].id);
        }

        // Inactive slots: forced-inactive players first, then remaining (capped at 5 total)
        int inactIdx = 0;
        const int maxInactive = 5;

        if (forceInactiveIds != null)
        {
            var candidates = forceInactiveIds
                .Select(pid => players.FirstOrDefault(p => p.id == pid))
                .Where(p => p != null);
            foreach (var p in candidates)
            {
                if (inactIdx >= maxInactive) break;
                _db.Insert(new LineupData
                {
                    player_id = p.id,
                    team_id = teamId,
                    slot = 2,
                    slot_index = inactIdx
                });
                inactIdx++;
            }
        }

        foreach (var p in remaining.Skip(benchSlots))
        {
            if (inactIdx >= maxInactive) break;
            _db.Insert(new LineupData
            {
                player_id = p.id,
                team_id = teamId,
                slot = 2,
                slot_index = inactIdx
            });
            inactIdx++;
        }
    }

    void OnDestroy()
    {
        _db?.Close();
    }
}

public class PlayerSeasonStatsRow
{
    public int player_id { get; set; }
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string position { get; set; }
    public int games { get; set; }
    public int total_points { get; set; }
    public int total_rebounds { get; set; }
    public int total_assists { get; set; }
    public int total_steals { get; set; }
    public int total_blocks { get; set; }
    public int total_rating { get; set; }
}

public class PlayerAwardQueryRow
{
    public int id { get; set; }
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string position { get; set; }
    public string team_name { get; set; }
    public string team_logo { get; set; }
    public int games { get; set; }
    public double avg_pts { get; set; }
    public double avg_reb { get; set; }
    public double avg_ast { get; set; }
    public double avg_rating { get; set; }
}

public class HistoricalStatsAggregateRow
{
    public int player_id { get; set; }
    public int games { get; set; }
    public int total_points { get; set; }
    public int total_rebounds { get; set; }
    public int total_assists { get; set; }
    public int total_steals { get; set; }
    public int total_blocks { get; set; }
    public int total_turnovers { get; set; }
    public int total_fgm { get; set; }
    public int total_fga { get; set; }
    public int total_fg3m { get; set; }
    public int total_fg3a { get; set; }
    public int total_ftm { get; set; }
    public int total_fta { get; set; }
    public int total_oreb { get; set; }
    public int total_dreb { get; set; }
    public int total_double_doubles { get; set; }
    public int total_triple_doubles { get; set; }
    public int total_minutes { get; set; }
    public int total_rating { get; set; }
}