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
            new TeamData { name="Boston Celtics",        abbreviation="BOS", city="Boston",        conference="East", division="Atlantic",  arena="TD Garden",               capacity=19156, owner="Wyc Grousbeck",   attack=88, defense=87, overall=88, budget=310_000_000, reputation=5, facilities=5, logo="celtics",   jersey_home="celtics_home",   jersey_away="celtics_away",   salary_margin=-60_000_000, objective="Playoffs" },
            new TeamData { name="Brooklyn Nets",         abbreviation="BKN", city="Brooklyn",      conference="East", division="Atlantic",  arena="Barclays Center",         capacity=17732, owner="Joe Tsai",         attack=66, defense=65, overall=65, budget=230_000_000, reputation=2, facilities=3, logo="nets",      jersey_home="nets_home",      jersey_away="nets_away",      salary_margin=35_000_000,  objective="Zona tranquila" },
            new TeamData { name="New York Knicks",       abbreviation="NYK", city="New York",      conference="East", division="Atlantic",  arena="Madison Square Garden",   capacity=19812, owner="James Dolan",      attack=88, defense=86, overall=87, budget=310_000_000, reputation=5, facilities=5, logo="knicks",    jersey_home="knicks_home",    jersey_away="knicks_away",    salary_margin=-55_000_000, objective="Campeonato" },
            new TeamData { name="Philadelphia 76ers",    abbreviation="PHI", city="Philadelphia",  conference="East", division="Atlantic",  arena="Wells Fargo Center",      capacity=20478, owner="Josh Harris",      attack=79, defense=78, overall=79, budget=265_000_000, reputation=3, facilities=4, logo="sixers",    jersey_home="76ers_home",     jersey_away="76ers_away",     salary_margin=-15_000_000, objective="Play-In" },
            new TeamData { name="Toronto Raptors",       abbreviation="TOR", city="Toronto",       conference="East", division="Atlantic",  arena="Scotiabank Arena",        capacity=19800, owner="MLSE",             attack=80, defense=79, overall=80, budget=265_000_000, reputation=3, facilities=4, logo="raptors",   jersey_home="raptors_home",   jersey_away="raptors_away",   salary_margin=-10_000_000, objective="Playoffs" },

            // ── ESTE — CENTRAL ──
            new TeamData { name="Chicago Bulls",         abbreviation="CHI", city="Chicago",       conference="East", division="Central",   arena="United Center",           capacity=20917, owner="Jerry Reinsdorf",  attack=68, defense=67, overall=67, budget=230_000_000, reputation=3, facilities=4, logo="bulls",     jersey_home="bulls_home",     jersey_away="bulls_away",     salary_margin=30_000_000,  objective="Zona tranquila" },
            new TeamData { name="Cleveland Cavaliers",   abbreviation="CLE", city="Cleveland",     conference="East", division="Central",   arena="Rocket Arena",            capacity=19432, owner="Dan Gilbert",      attack=85, defense=86, overall=86, budget=285_000_000, reputation=4, facilities=4, logo="cavaliers", jersey_home="cavaliers_home", jersey_away="cavaliers_away", salary_margin=-40_000_000, objective="Playoffs" },
            new TeamData { name="Detroit Pistons",       abbreviation="DET", city="Detroit",       conference="East", division="Central",   arena="Little Caesars Arena",    capacity=20332, owner="Tom Gores",        attack=87, defense=88, overall=87, budget=285_000_000, reputation=3, facilities=4, logo="pistons",   jersey_home="pistons_home",   jersey_away="pistons_away",   salary_margin=-45_000_000, objective="Playoffs" },
            new TeamData { name="Indiana Pacers",        abbreviation="IND", city="Indianapolis",  conference="East", division="Central",   arena="Gainbridge Fieldhouse",   capacity=17923, owner="Herb Simon",       attack=77, defense=75, overall=76, budget=255_000_000, reputation=3, facilities=3, logo="pacers",    jersey_home="pacers_home",    jersey_away="pacers_away",    salary_margin=5_000_000,   objective="Play-In" },
            new TeamData { name="Milwaukee Bucks",       abbreviation="MIL", city="Milwaukee",     conference="East", division="Central",   arena="Fiserv Forum",            capacity=17341, owner="Marc Lasry",       attack=75, defense=73, overall=74, budget=250_000_000, reputation=4, facilities=4, logo="bucks",     jersey_home="bucks_home",     jersey_away="bucks_away",     salary_margin=10_000_000,  objective="Play-In" },

            // ── ESTE — SURESTE ──
            new TeamData { name="Atlanta Hawks",         abbreviation="ATL", city="Atlanta",       conference="East", division="Southeast", arena="State Farm Arena",        capacity=18118, owner="Tony Ressler",     attack=81, defense=79, overall=80, budget=265_000_000, reputation=3, facilities=3, logo="hawks",     jersey_home="hawks_home",     jersey_away="hawks_away",     salary_margin=-10_000_000, objective="Playoffs" },
            new TeamData { name="Charlotte Hornets",     abbreviation="CHA", city="Charlotte",     conference="East", division="Southeast", arena="Spectrum Center",         capacity=19077, owner="Gabe Plotkin",     attack=74, defense=73, overall=73, budget=235_000_000, reputation=2, facilities=3, logo="hornets",   jersey_home="hornets_home",   jersey_away="hornets_away",   salary_margin=20_000_000,  objective="Play-In" },
            new TeamData { name="Miami Heat",            abbreviation="MIA", city="Miami",         conference="East", division="Southeast", arena="Kaseya Center",           capacity=19600, owner="Micky Arison",     attack=76, defense=77, overall=77, budget=255_000_000, reputation=4, facilities=4, logo="heat",      jersey_home="heat_home",      jersey_away="heat_away",      salary_margin=5_000_000,   objective="Play-In" },
            new TeamData { name="Orlando Magic",         abbreviation="ORL", city="Orlando",       conference="East", division="Southeast", arena="Kia Center",              capacity=18846, owner="DeVos family",     attack=78, defense=80, overall=79, budget=260_000_000, reputation=3, facilities=3, logo="magic",     jersey_home="magic_home",     jersey_away="magic_away",     salary_margin=-10_000_000, objective="Playoffs" },
            new TeamData { name="Washington Wizards",    abbreviation="WAS", city="Washington",    conference="East", division="Southeast", arena="Capital One Arena",       capacity=20356, owner="Ted Leonsis",      attack=63, defense=62, overall=62, budget=215_000_000, reputation=2, facilities=3, logo="wizards",   jersey_home="wizards_home",   jersey_away="wizards_away",   salary_margin=55_000_000,  objective="Zona tranquila" },

            // ── OESTE — NOROESTE ──
            new TeamData { name="Denver Nuggets",        abbreviation="DEN", city="Denver",        conference="West", division="Northwest", arena="Ball Arena",              capacity=19520, owner="Ann Walton Kroenke", attack=88, defense=85, overall=87, budget=305_000_000, reputation=4, facilities=4, logo="nuggets",   jersey_home="nuggets_home",   jersey_away="nuggets_away",   salary_margin=-65_000_000, objective="Playoffs" },
            new TeamData { name="Minnesota Timberwolves",abbreviation="MIN", city="Minneapolis",   conference="West", division="Northwest", arena="Target Center",           capacity=18978, owner="Marc Lore",        attack=83, defense=85, overall=84, budget=275_000_000, reputation=3, facilities=3, logo="wolves",    jersey_home="wolves_home",    jersey_away="wolves_away",    salary_margin=-25_000_000, objective="Playoffs" },
            new TeamData { name="Oklahoma City Thunder",  abbreviation="OKC", city="Oklahoma City", conference="West", division="Northwest", arena="Paycom Center",           capacity=18203, owner="Clay Bennett",     attack=90, defense=93, overall=92, budget=285_000_000, reputation=4, facilities=4, logo="thunder",   jersey_home="thunder_home",   jersey_away="thunder_away",   salary_margin=-55_000_000, objective="Campeonato" },
            new TeamData { name="Portland Trail Blazers", abbreviation="POR", city="Portland",      conference="West", division="Northwest", arena="Moda Center",             capacity=19393, owner="Jody Allen",       attack=74, defense=74, overall=74, budget=240_000_000, reputation=3, facilities=3, logo="blazers",   jersey_home="blazers_home",   jersey_away="blazers_away",   salary_margin=15_000_000,  objective="Play-In" },
            new TeamData { name="Utah Jazz",              abbreviation="UTA", city="Salt Lake City", conference="West", division="Northwest", arena="Delta Center",            capacity=18306, owner="Ryan Smith",       attack=67, defense=66, overall=66, budget=225_000_000, reputation=2, facilities=3, logo="jazz",      jersey_home="jazz_home",      jersey_away="jazz_away",      salary_margin=40_000_000,  objective="Zona tranquila" },

            // ── OESTE — PACÍFICO ──
            new TeamData { name="Golden State Warriors",  abbreviation="GSW", city="San Francisco", conference="West", division="Pacific",   arena="Chase Center",            capacity=18064, owner="Joe Lacob",        attack=79, defense=77, overall=78, budget=270_000_000, reputation=5, facilities=5, logo="warriors",  jersey_home="warriors_home",  jersey_away="warriors_away",  salary_margin=-20_000_000, objective="Play-In" },
            new TeamData { name="Los Angeles Clippers",   abbreviation="LAC", city="Los Angeles",   conference="West", division="Pacific",   arena="Intuit Dome",             capacity=18000, owner="Steve Ballmer",    attack=75, defense=76, overall=75, budget=255_000_000, reputation=3, facilities=5, logo="clippers",  jersey_home="clippers_home",  jersey_away="clippers_away",  salary_margin=10_000_000,  objective="Play-In" },
            new TeamData { name="Los Angeles Lakers",     abbreviation="LAL", city="Los Angeles",   conference="West", division="Pacific",   arena="Crypto.com Arena",        capacity=18997, owner="Jeanie Buss",      attack=85, defense=83, overall=84, budget=295_000_000, reputation=5, facilities=5, logo="lakers",    jersey_home="lakers_home",    jersey_away="lakers_away",    salary_margin=-50_000_000, objective="Playoffs" },
            new TeamData { name="Phoenix Suns",           abbreviation="PHX", city="Phoenix",       conference="West", division="Pacific",   arena="Footprint Center",        capacity=18055, owner="Mat Ishbia",       attack=80, defense=79, overall=80, budget=265_000_000, reputation=3, facilities=4, logo="suns",      jersey_home="suns_home",      jersey_away="suns_away",      salary_margin=-15_000_000, objective="Play-In" },
            new TeamData { name="Sacramento Kings",       abbreviation="SAC", city="Sacramento",    conference="West", division="Pacific",   arena="Golden 1 Center",         capacity=17608, owner="Vivek Ranadivé",   attack=69, defense=68, overall=68, budget=230_000_000, reputation=2, facilities=4, logo="kings",     jersey_home="kings_home",     jersey_away="kings_away",     salary_margin=30_000_000,  objective="Zona tranquila" },

            // ── OESTE — SUROESTE ──
            new TeamData { name="Dallas Mavericks",      abbreviation="DAL", city="Dallas",        conference="West", division="Southwest", arena="American Airlines Center", capacity=19200, owner="Patrick Dumont",   attack=72, defense=70, overall=71, budget=245_000_000, reputation=4, facilities=4, logo="mavericks", jersey_home="mavericks_home", jersey_away="mavericks_away", salary_margin=20_000_000,  objective="Play-In" },
            new TeamData { name="Houston Rockets",       abbreviation="HOU", city="Houston",       conference="West", division="Southwest", arena="Toyota Center",           capacity=18055, owner="Tilman Fertitta",  attack=83, defense=83, overall=83, budget=275_000_000, reputation=3, facilities=3, logo="rockets",   jersey_home="rockets_home",   jersey_away="rockets_away",   salary_margin=-25_000_000, objective="Playoffs" },
            new TeamData { name="Memphis Grizzlies",     abbreviation="MEM", city="Memphis",       conference="West", division="Southwest", arena="FedExForum",              capacity=17794, owner="Robert Pera",      attack=72, defense=73, overall=72, budget=235_000_000, reputation=3, facilities=3, logo="grizzlies", jersey_home="grizzlies_home", jersey_away="grizzlies_away", salary_margin=25_000_000,  objective="Zona tranquila" },
            new TeamData { name="New Orleans Pelicans",  abbreviation="NOP", city="New Orleans",   conference="West", division="Southwest", arena="Smoothie King Center",    capacity=17791, owner="Gayle Benson",     attack=67, defense=66, overall=66, budget=225_000_000, reputation=2, facilities=3, logo="pelicans",  jersey_home="pelicans_home",  jersey_away="pelicans_away",  salary_margin=35_000_000,  objective="Zona tranquila" },
            new TeamData { name="San Antonio Spurs",     abbreviation="SAS", city="San Antonio",   conference="West", division="Southwest", arena="AT&T Center",             capacity=18418, owner="Peter Holt",       attack=88, defense=91, overall=90, budget=290_000_000, reputation=4, facilities=4, logo="spurs",     jersey_home="spurs_home",     jersey_away="spurs_away",     salary_margin=-45_000_000, objective="Campeonato" },
        };

        _db.InsertAll(teams);
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
                overall = ovr,
                potential = pot,
                speed = spd,
                shooting = sht,
                three_point = thr,
                passing = pas,
                dribbling = drb,
                defense = def,
                rebounding = reb,
                athleticism = ath,
                iq = iq,
                steals = stl,
                blocks = blk,
                salary = sal,
                contract_years = yrs,
                is_rookie = rookie ? 1 : 0,
                injury_days = 0,
                injury_type = "",
                treated = 0
            });
        }

        // ── ATL ──
        Add("ATL", "Jalen", "Johnson", "SF", 24, "USA", 203, 99, 86, 91, 83, 75, 73, 82, 78, 78, 84, 90, 82, 70, 45, 30_000_000, 5, false);
        Add("ATL", "Nickeil", "Alexander-Walker", "SG", 27, "CAN", 196, 90, 81, 82, 84, 74, 66, 68, 72, 76, 60, 74, 72, 76, 38, 15_500_000, 4, false);
        Add("ATL", "Dyson", "Daniels", "SG", 22, "AUS", 196, 93, 79, 86, 84, 63, 54, 70, 68, 82, 58, 74, 70, 90, 30, 7_700_000, 2, false);
        Add("ATL", "Onyeka", "Okongwu", "C", 25, "USA", 206, 104, 80, 84, 74, 58, 36, 57, 50, 78, 88, 84, 76, 56, 76, 15_000_000, 4, false);
        Add("ATL", "Zaccharie", "Risacher", "SF", 21, "FRA", 203, 90, 76, 84, 76, 78, 80, 72, 76, 66, 54, 68, 78, 64, 24, 13_200_000, 4, false);
        Add("ATL", "CJ", "McCollum", "SG", 34, "USA", 191, 86, 78, 74, 78, 82, 76, 72, 80, 62, 44, 64, 76, 58, 28, 22_000_000, 2, false);
        Add("ATL", "Jonathan", "Kuminga", "SF", 22, "COD", 201, 99, 78, 88, 86, 74, 62, 62, 68, 70, 68, 88, 72, 62, 40, 7_000_000, 2, false);
        Add("ATL", "Buddy", "Hield", "SG", 33, "BAH", 196, 97, 73, 70, 76, 86, 84, 64, 66, 58, 48, 64, 70, 52, 22, 19_000_000, 2, false);
        Add("ATL", "Gabe", "Vincent", "PG", 29, "USA", 193, 88, 69, 67, 76, 66, 60, 66, 64, 70, 52, 62, 68, 62, 24, 11_000_000, 2, false);
        Add("ATL", "Corey", "Kispert", "SF", 27, "USA", 201, 97, 68, 68, 68, 70, 70, 54, 56, 58, 52, 58, 60, 54, 32, 14_000_000, 3, false);
        Add("ATL", "Asa", "Newell", "PF", 20, "USA", 208, 100, 66, 80, 70, 52, 34, 48, 44, 64, 72, 80, 64, 50, 48, 3_600_000, 4, false);
        Add("ATL", "Jock", "Landale", "C", 29, "AUS", 211, 109, 66, 63, 58, 52, 30, 54, 40, 54, 72, 62, 68, 44, 52, 3_200_000, 2, false);
        Add("ATL", "Caleb", "Houstan", "SF", 23, "CAN", 201, 97, 66, 74, 70, 62, 64, 52, 54, 56, 52, 60, 58, 58, 36, 2_000_000, 3, false);
        Add("ATL", "Keaton", "Wallace", "SG", 25, "USA", 191, 93, 62, 62, 76, 62, 56, 58, 60, 60, 44, 68, 60, 58, 26, 2_300_000, 2, false);
        Add("ATL", "Christian", "Koloko", "C", 25, "CMR", 213, 104, 60, 68, 64, 42, 24, 42, 36, 54, 64, 64, 52, 44, 70, 2_000_000, 2, false);


        // ── BOS ──
        Add("BOS", "Jayson", "Tatum", "PF", 28, "USA", 203, 95, 95, 96, 84, 90, 86, 84, 86, 88, 78, 88, 94, 72, 48, 54100000, 4, false);
        Add("BOS", "Jaylen", "Brown", "SG", 29, "USA", 198, 101, 92, 93, 89, 88, 80, 76, 84, 86, 74, 92, 88, 74, 42, 53100000, 4, false);
        Add("BOS", "Derrick", "White", "PG", 32, "USA", 193, 86, 87, 86, 84, 80, 76, 82, 82, 88, 60, 82, 88, 80, 44, 28100000, 3, false);
        Add("BOS", "Payton", "Pritchard", "PG", 28, "USA", 185, 88, 84, 85, 88, 84, 84, 78, 84, 70, 50, 70, 84, 74, 26, 7200000, 3, false);
        Add("BOS", "Sam", "Hauser", "SF", 28, "USA", 201, 98, 80, 80, 76, 84, 88, 66, 70, 68, 56, 68, 74, 62, 30, 10000000, 4, false);
        Add("BOS", "Nikola", "Vucevic", "C", 35, "MNE", 208, 120, 83, 83, 58, 82, 74, 78, 68, 72, 86, 70, 86, 46, 52, 21400000, 1, false);
        Add("BOS", "Neemias", "Queta", "C", 27, "POR", 213, 111, 75, 78, 70, 58, 34, 52, 46, 68, 82, 76, 68, 52, 76, 3000000, 2, false);
        Add("BOS", "Jordan", "Walsh", "SF", 22, "USA", 201, 93, 74, 81, 80, 62, 54, 60, 62, 78, 62, 82, 68, 66, 40, 2200000, 2, false);
        Add("BOS", "Baylor", "Scheierman", "SG", 25, "USA", 198, 92, 75, 82, 78, 76, 82, 70, 72, 66, 58, 68, 74, 60, 28, 2600000, 2, false);
        Add("BOS", "Dalano", "Banton", "PG", 26, "CAN", 203, 92, 76, 78, 82, 74, 66, 72, 76, 70, 58, 74, 72, 64, 32, 2500000, 1, false);
        Add("BOS", "Hugo", "Gonzalez", "SF", 20, "ESP", 198, 91, 72, 84, 80, 68, 62, 62, 68, 72, 60, 80, 70, 68, 36, 2200000, 4, false);
        Add("BOS", "Luka", "Garza", "C", 27, "USA", 208, 110, 74, 74, 54, 76, 58, 60, 54, 58, 82, 60, 72, 34, 42, 2300000, 1, false);
        Add("BOS", "Amari", "Williams", "C", 24, "GBR", 211, 113, 71, 79, 62, 56, 42, 58, 50, 68, 80, 76, 68, 48, 72, 1200000, 4, false);
        Add("BOS", "Max", "Shulga", "PG", 24, "UKR", 193, 88, 69, 76, 76, 68, 72, 70, 72, 60, 50, 66, 70, 56, 24, 1200000, 4, false);
        Add("BOS", "Ron", "Harper Jr.", "SF", 26, "USA", 198, 111, 70, 73, 72, 66, 68, 58, 62, 68, 58, 72, 68, 58, 32, 1200000, 2, false);

        // ── BKN ──
        Add("BKN", "Michael", "Porter Jr.", "SF", 28, "USA", 208, 99, 86, 86, 76, 88, 84, 64, 74, 68, 70, 72, 78, 46, 42, 38300000, 2, false);
        Add("BKN", "Nic", "Claxton", "C", 27, "USA", 211, 98, 84, 86, 78, 62, 34, 60, 52, 82, 88, 82, 76, 56, 82, 25300000, 3, false);
        Add("BKN", "Terance", "Mann", "SG", 29, "USA", 196, 98, 79, 79, 80, 74, 64, 74, 76, 78, 62, 80, 78, 72, 34, 15500000, 2, false);
        Add("BKN", "Noah", "Clowney", "PF", 22, "USA", 208, 95, 78, 86, 76, 70, 66, 62, 66, 74, 72, 82, 72, 64, 54, 3400000, 2, false);
        Add("BKN", "Ziaire", "Williams", "SF", 24, "USA", 206, 84, 77, 82, 80, 72, 68, 64, 70, 72, 62, 76, 72, 62, 38, 6250000, 1, false);
        Add("BKN", "Egor", "Demin", "PG", 19, "RUS", 203, 91, 79, 92, 80, 72, 70, 84, 82, 64, 58, 78, 78, 62, 30, 6900000, 4, false);
        Add("BKN", "Nolan", "Traore", "PG", 19, "FRA", 191, 84, 77, 91, 88, 68, 58, 84, 80, 58, 48, 78, 74, 62, 20, 3810000, 4, false);
        Add("BKN", "Ben", "Saraf", "PG", 19, "ISR", 198, 91, 76, 90, 78, 68, 60, 82, 78, 62, 54, 74, 72, 58, 24, 2880000, 4, false);
        Add("BKN", "Danny", "Wolf", "PF", 21, "USA", 211, 113, 76, 89, 68, 70, 66, 82, 76, 68, 84, 72, 78, 46, 50, 2800000, 4, false);
        Add("BKN", "Drake", "Powell", "SG", 20, "USA", 196, 88, 74, 88, 82, 68, 60, 60, 66, 78, 62, 84, 70, 72, 42, 3370000, 4, false);
        Add("BKN", "Day'Ron", "Sharpe", "C", 24, "USA", 208, 120, 78, 80, 66, 58, 28, 52, 46, 70, 88, 76, 68, 44, 72, 6250000, 3, false);
        Add("BKN", "Jalen", "Wilson", "PF", 25, "USA", 198, 100, 76, 79, 72, 72, 68, 62, 66, 72, 68, 74, 72, 58, 34, 2200000, 2, false);
        Add("BKN", "Ochai", "Agbaji", "SG", 25, "USA", 196, 98, 78, 79, 84, 76, 74, 62, 68, 78, 60, 80, 76, 74, 32, 6500000, 1, false);
        Add("BKN", "Josh", "Minott", "SF", 23, "USA", 203, 98, 75, 82, 82, 64, 54, 60, 64, 78, 64, 84, 68, 72, 42, 2500000, 2, false);
        Add("BKN", "E.J.", "Liddell", "PF", 25, "USA", 201, 109, 73, 75, 68, 68, 60, 58, 58, 72, 68, 72, 68, 58, 46, 2200000, 1, false);

        // ── CHA ──
        Add("CHA", "LaMelo", "Ball", "PG", 25, "USA", 201, 82, 89, 92, 88, 88, 84, 92, 90, 68, 52, 82, 88, 72, 24, 37900000, 4, false);
        Add("CHA", "Brandon", "Miller", "SF", 23, "USA", 206, 91, 87, 93, 84, 86, 82, 78, 86, 76, 66, 84, 84, 70, 40, 16000000, 2, false);
        Add("CHA", "Miles", "Bridges", "PF", 28, "USA", 201, 102, 84, 84, 82, 82, 74, 72, 80, 78, 72, 88, 80, 64, 38, 25000000, 2, false);
        Add("CHA", "Coby", "White", "SG", 26, "USA", 196, 88, 85, 86, 88, 84, 80, 78, 82, 68, 58, 78, 82, 64, 22, 12000000, 2, false);
        Add("CHA", "Tre", "Mann", "PG", 25, "USA", 191, 81, 80, 82, 84, 80, 76, 78, 82, 62, 48, 72, 78, 60, 18, 12000000, 3, false);
        Add("CHA", "Kon", "Knueppel", "SG", 20, "USA", 198, 99, 79, 90, 76, 82, 86, 74, 80, 64, 58, 72, 82, 58, 18, 7000000, 4, false);
        Add("CHA", "Liam", "McNeeley", "SF", 20, "USA", 201, 98, 77, 89, 74, 80, 84, 72, 78, 66, 62, 72, 80, 58, 22, 4500000, 4, false);
        Add("CHA", "Tidjane", "Salaun", "PF", 20, "FRA", 206, 98, 76, 91, 80, 72, 68, 64, 70, 74, 68, 86, 74, 68, 42, 6500000, 3, false);
        Add("CHA", "Ryan", "Kalkbrenner", "C", 23, "USA", 216, 117, 76, 84, 62, 68, 44, 58, 54, 72, 86, 70, 74, 42, 86, 3200000, 4, false);
        Add("CHA", "Josh", "Green", "SG", 25, "AUS", 196, 95, 77, 78, 82, 70, 68, 66, 72, 78, 60, 82, 74, 74, 34, 13000000, 3, false);
        Add("CHA", "Grant", "Williams", "PF", 27, "USA", 198, 107, 77, 77, 68, 74, 72, 66, 64, 76, 70, 70, 78, 62, 36, 13000000, 2, false);
        Add("CHA", "Xavier", "Tillman", "C", 27, "USA", 203, 111, 76, 76, 64, 66, 54, 62, 58, 78, 80, 70, 76, 58, 54, 7000000, 2, false);
        Add("CHA", "Moussa", "Diabate", "C", 23, "FRA", 211, 95, 75, 82, 76, 58, 26, 52, 48, 72, 88, 84, 68, 54, 78, 2500000, 3, false);
        Add("CHA", "Pat", "Connaughton", "SG", 33, "USA", 196, 94, 75, 74, 74, 72, 74, 64, 68, 68, 58, 72, 72, 64, 26, 9400000, 1, false);

        // ── CHI ──
        Add("CHI", "Josh", "Giddey", "PG", 23, "AUS", 203, 93, 86, 90, 76, 74, 78, 90, 84, 72, 76, 80, 88, 62, 30, 30000000, 5, false);
        Add("CHI", "Anfernee", "Simons", "SG", 27, "USA", 191, 82, 86, 87, 90, 88, 82, 76, 86, 60, 42, 78, 82, 58, 18, 27000000, 2, false);
        Add("CHI", "Collin", "Sexton", "PG", 27, "USA", 188, 86, 84, 84, 88, 84, 72, 74, 82, 62, 44, 82, 78, 64, 16, 19000000, 2, false);
        Add("CHI", "Matas", "Buzelis", "SF", 21, "USA", 208, 95, 82, 92, 80, 78, 74, 72, 80, 76, 68, 84, 78, 68, 44, 7000000, 3, false);
        Add("CHI", "Isaac", "Okoro", "SF", 25, "USA", 196, 102, 79, 80, 82, 68, 60, 66, 70, 84, 60, 86, 74, 82, 34, 11000000, 2, false);
        Add("CHI", "Patrick", "Williams", "PF", 25, "USA", 201, 97, 78, 82, 76, 72, 70, 64, 68, 80, 68, 82, 74, 76, 42, 18000000, 4, false);
        Add("CHI", "Guerschon", "Yabusele", "PF", 30, "FRA", 203, 118, 79, 79, 72, 78, 76, 68, 70, 74, 74, 76, 78, 62, 32, 12000000, 2, false);
        Add("CHI", "Zach", "Collins", "C", 29, "USA", 211, 113, 78, 78, 64, 72, 66, 72, 66, 74, 80, 70, 78, 54, 52, 18000000, 2, false);
        Add("CHI", "Nick", "Richards", "C", 28, "JAM", 213, 111, 77, 77, 68, 64, 24, 50, 46, 72, 86, 76, 70, 48, 74, 5000000, 2, false);
        Add("CHI", "Tre", "Jones", "PG", 26, "USA", 185, 83, 83, 82, 78, 72, 66, 82, 80, 70, 48, 72, 82, 72, 18, 9000000, 2, false);
        Add("CHI", "Rob", "Dillingham", "PG", 21, "USA", 188, 79, 91, 93, 92, 84, 80, 86, 90, 54, 38, 76, 74, 54, 14, 6500000, 4, false);
        Add("CHI", "Noa", "Essengue", "PF", 19, "FRA", 208, 97, 77, 92, 80, 68, 60, 64, 70, 76, 72, 84, 72, 70, 46, 6500000, 4, false);
        Add("CHI", "Leonard", "Miller", "PF", 22, "CAN", 208, 98, 77, 89, 78, 70, 58, 64, 68, 74, 76, 84, 72, 66, 42, 2800000, 3, false);
        Add("CHI", "Jalen", "Smith", "C", 26, "USA", 208, 98, 77, 80, 68, 72, 68, 60, 60, 72, 80, 72, 72, 52, 58, 9000000, 2, false);

        // ── CLE ──
        Add("CLE", "Donovan", "Mitchell", "SG", 30, "USA", 191, 97, 93, 93, 92, 90, 82, 84, 90, 72, 54, 86, 90, 76, 24, 54000000, 4, false);
        Add("CLE", "Evan", "Mobley", "PF", 25, "USA", 211, 98, 92, 95, 78, 82, 74, 80, 82, 92, 88, 86, 90, 72, 82, 45000000, 5, false);
        Add("CLE", "James", "Harden", "PG", 37, "USA", 196, 100, 86, 86, 72, 84, 82, 92, 88, 62, 58, 72, 92, 70, 30, 32000000, 2, false);
        Add("CLE", "Jarrett", "Allen", "C", 28, "USA", 206, 110, 86, 86, 74, 72, 30, 62, 58, 86, 92, 82, 80, 56, 84, 30000000, 3, false);
        Add("CLE", "Keon", "Ellis", "SG", 27, "USA", 193, 79, 82, 82, 82, 74, 72, 68, 74, 84, 58, 80, 78, 84, 32, 9000000, 3, false);
        Add("CLE", "Max", "Strus", "SF", 30, "USA", 196, 97, 81, 81, 78, 80, 84, 68, 72, 72, 60, 76, 78, 66, 26, 16000000, 2, false);
        Add("CLE", "Dennis", "Schroder", "PG", 33, "GER", 188, 78, 79, 79, 84, 76, 64, 82, 80, 66, 50, 78, 78, 72, 24, 7000000, 1, false);
        Add("CLE", "Sam", "Merrill", "SG", 30, "USA", 193, 93, 77, 77, 74, 78, 86, 62, 68, 64, 52, 68, 72, 58, 18, 5000000, 2, false);
        Add("CLE", "Larry", "Nance Jr.", "PF", 33, "USA", 201, 111, 77, 77, 70, 68, 64, 66, 64, 78, 72, 74, 78, 62, 46, 6000000, 1, false);
        Add("CLE", "Jaylon", "Tyson", "SF", 23, "USA", 198, 99, 76, 84, 76, 72, 70, 68, 72, 72, 64, 78, 72, 62, 32, 3200000, 3, false);
        Add("CLE", "Tyrese", "Proctor", "PG", 22, "AUS", 196, 84, 78, 88, 80, 70, 68, 82, 80, 64, 48, 74, 76, 60, 20, 4500000, 4, false);
        Add("CLE", "Craig", "Porter Jr.", "PG", 26, "USA", 188, 83, 77, 79, 82, 68, 62, 78, 76, 66, 46, 76, 72, 68, 20, 2500000, 2, false);
        Add("CLE", "Dean", "Wade", "PF", 30, "USA", 206, 103, 76, 76, 68, 72, 76, 62, 64, 76, 68, 70, 74, 60, 42, 7000000, 2, false);
        Add("CLE", "Thomas", "Bryant", "C", 29, "USA", 208, 112, 75, 75, 66, 72, 54, 58, 56, 68, 80, 68, 72, 44, 44, 3500000, 1, false);

        // ── DAL ──
        Add("DAL", "Kyrie", "Irving", "PG", 34, "USA", 188, 88, 90, 90, 86, 90, 86, 92, 92, 58, 42, 80, 92, 68, 18, 36566002, 2, false);
        Add("DAL", "Cooper", "Flagg", "SF", 19, "USA", 206, 92, 87, 98, 82, 82, 78, 80, 84, 88, 78, 90, 86, 76, 68, 14386320, 4, false);
        Add("DAL", "P.J.", "Washington", "PF", 27, "USA", 201, 104, 84, 84, 78, 80, 76, 72, 76, 82, 76, 82, 80, 68, 44, 14152174, 2, false);
        Add("DAL", "Dereck", "Lively II", "C", 22, "USA", 216, 104, 84, 91, 74, 68, 28, 60, 54, 86, 90, 84, 78, 58, 90, 5253360, 2, false);
        Add("DAL", "Daniel", "Gafford", "C", 27, "USA", 208, 120, 82, 82, 72, 72, 24, 56, 52, 84, 88, 80, 76, 52, 86, 14386320, 2, false);
        Add("DAL", "Klay", "Thompson", "SG", 36, "USA", 196, 99, 81, 81, 70, 82, 88, 64, 70, 70, 56, 68, 80, 58, 24, 16666667, 2, false);
        Add("DAL", "Max", "Christie", "SG", 23, "USA", 196, 86, 79, 86, 80, 72, 72, 68, 74, 80, 60, 82, 74, 78, 34, 7714286, 3, false);
        Add("DAL", "Naji", "Marshall", "SF", 28, "USA", 198, 99, 79, 79, 80, 72, 66, 70, 74, 78, 64, 82, 76, 72, 36, 9000000, 2, false);
        Add("DAL", "Caleb", "Martin", "SF", 30, "USA", 196, 92, 78, 78, 78, 72, 72, 66, 70, 78, 62, 80, 76, 72, 32, 9594044, 2, false);
        Add("DAL", "Khris", "Middleton", "SF", 34, "USA", 201, 100, 81, 81, 66, 82, 82, 78, 80, 68, 62, 68, 84, 56, 26, 33926296, 1, false);
        Add("DAL", "AJ", "Johnson", "PG", 21, "USA", 196, 72, 78, 90, 86, 66, 60, 80, 78, 54, 42, 78, 68, 56, 18, 3090480, 4, false);
        Add("DAL", "Tyler", "Smith", "PF", 21, "USA", 206, 101, 75, 88, 76, 72, 70, 62, 66, 72, 72, 82, 72, 60, 46, 1955377, 3, false);
        Add("DAL", "Ryan", "Nembhard", "PG", 23, "CAN", 180, 81, 76, 82, 82, 64, 60, 84, 80, 52, 38, 66, 76, 62, 12, 321875, 4, false);
        Add("DAL", "Brandon", "Williams", "PG", 26, "USA", 188, 86, 74, 76, 84, 70, 64, 76, 74, 56, 42, 74, 70, 60, 16, 2200000, 1, false);
        Add("DAL", "Marvin", "Bagley III", "PF", 27, "USA", 208, 106, 76, 76, 76, 74, 42, 58, 60, 72, 82, 78, 70, 42, 34, 2296274, 1, false);

        // ── DEN ──
        Add("DEN", "Nikola", "Jokic", "C", 31, "SRB", 211, 129, 97, 97, 72, 92, 84, 98, 94, 82, 92, 82, 99, 68, 72, 62000000, 4, false);
        Add("DEN", "Jamal", "Murray", "PG", 29, "CAN", 193, 98, 88, 88, 84, 86, 82, 88, 88, 66, 54, 80, 88, 66, 18, 50000000, 4, false);
        Add("DEN", "Aaron", "Gordon", "PF", 31, "USA", 203, 107, 85, 85, 82, 78, 70, 72, 78, 84, 78, 92, 82, 70, 48, 33000000, 3, false);
        Add("DEN", "Cameron", "Johnson", "SF", 30, "USA", 203, 95, 84, 84, 78, 84, 88, 70, 76, 72, 66, 76, 80, 62, 34, 23000000, 2, false);
        Add("DEN", "Christian", "Braun", "SG", 25, "USA", 198, 100, 84, 86, 84, 78, 76, 74, 80, 82, 66, 86, 80, 76, 34, 11000000, 4, false);
        Add("DEN", "Tyus", "Jones", "PG", 30, "USA", 185, 89, 81, 81, 76, 74, 68, 88, 84, 62, 46, 68, 86, 74, 12, 8000000, 2, false);
        Add("DEN", "Jonas", "Valanciunas", "C", 34, "LIT", 211, 120, 80, 80, 58, 78, 58, 70, 64, 74, 90, 64, 84, 42, 38, 10000000, 1, false);
        Add("DEN", "Bruce", "Brown", "SG", 30, "USA", 193, 92, 79, 79, 82, 72, 66, 74, 76, 80, 58, 82, 76, 74, 28, 6000000, 1, false);
        Add("DEN", "Peyton", "Watson", "SF", 23, "USA", 201, 91, 79, 88, 82, 70, 64, 66, 72, 84, 68, 88, 74, 72, 72, 5500000, 3, false);
        Add("DEN", "Julian", "Strawther", "SG", 24, "USA", 198, 93, 78, 84, 80, 78, 82, 68, 76, 66, 56, 74, 76, 60, 24, 3200000, 2, false);
        Add("DEN", "DaRon", "Holmes II", "PF", 23, "USA", 208, 107, 77, 89, 76, 74, 68, 66, 72, 78, 76, 84, 74, 64, 56, 4000000, 4, false);
        Add("DEN", "Jalen", "Pickett", "PG", 27, "USA", 188, 83, 76, 76, 68, 68, 64, 82, 78, 60, 44, 64, 80, 58, 12, 2500000, 2, false);
        Add("DEN", "Tim", "Hardaway Jr.", "SG", 34, "USA", 196, 93, 77, 77, 74, 80, 84, 60, 68, 60, 48, 68, 72, 52, 18, 6000000, 1, false);
        Add("DEN", "Zeke", "Nnaji", "PF", 25, "USA", 206, 109, 73, 78, 72, 64, 54, 58, 60, 72, 74, 76, 68, 54, 46, 8000000, 3, false);
        Add("DEN", "David", "Roddy", "SF", 25, "USA", 193, 116, 72, 74, 70, 66, 64, 58, 62, 68, 62, 74, 66, 56, 28, 2200000, 1, false);

        // ── DET ──
        Add("DET", "Cade", "Cunningham", "PG", 25, "USA", 198, 100, 91, 94, 80, 86, 78, 90, 88, 74, 66, 82, 92, 70, 26, 45000000, 5, false);
        Add("DET", "Jalen", "Duren", "C", 22, "USA", 211, 113, 85, 92, 78, 74, 28, 62, 58, 82, 92, 88, 80, 56, 82, 13000000, 3, false);
        Add("DET", "Ausar", "Thompson", "SF", 23, "USA", 201, 98, 84, 92, 86, 70, 58, 74, 78, 90, 72, 92, 80, 88, 54, 10000000, 3, false);
        Add("DET", "Tobias", "Harris", "PF", 34, "USA", 203, 102, 80, 80, 66, 78, 76, 72, 74, 72, 72, 70, 82, 56, 32, 26000000, 1, false);
        Add("DET", "Caris", "LeVert", "SG", 32, "USA", 198, 93, 80, 80, 82, 78, 70, 76, 82, 66, 54, 78, 78, 62, 20, 12000000, 2, false);
        Add("DET", "Kevin", "Huerter", "SG", 28, "USA", 201, 91, 79, 79, 78, 78, 82, 68, 74, 64, 54, 70, 76, 56, 20, 17000000, 2, false);
        Add("DET", "Isaiah", "Stewart", "PF", 25, "USA", 203, 113, 79, 80, 68, 72, 68, 62, 64, 80, 80, 78, 74, 64, 58, 15000000, 3, false);
        Add("DET", "Duncan", "Robinson", "SG", 32, "USA", 201, 97, 78, 78, 72, 80, 88, 60, 66, 58, 48, 66, 72, 48, 16, 9000000, 2, false);
        Add("DET", "Ronald", "Holland II", "SF", 20, "USA", 203, 93, 77, 92, 84, 68, 58, 66, 72, 82, 68, 90, 72, 78, 44, 9000000, 4, false);
        Add("DET", "Marcus", "Sasser", "PG", 25, "USA", 188, 84, 77, 80, 82, 76, 74, 76, 78, 58, 42, 72, 74, 58, 12, 4200000, 2, false);
        Add("DET", "Javonte", "Green", "SF", 33, "USA", 193, 93, 75, 75, 78, 66, 60, 58, 64, 82, 60, 86, 70, 78, 32, 3500000, 1, false);
        Add("DET", "Paul", "Reed", "PF", 27, "USA", 206, 95, 75, 78, 72, 64, 42, 58, 56, 78, 82, 80, 68, 60, 54, 5000000, 2, false);
        Add("DET", "Wendell", "Moore Jr.", "SG", 24, "USA", 196, 97, 72, 78, 76, 64, 58, 64, 68, 72, 56, 76, 68, 68, 24, 2500000, 2, false);
        Add("DET", "Chaz", "Lanier", "SG", 24, "USA", 196, 84, 73, 82, 76, 74, 82, 60, 68, 56, 44, 68, 70, 52, 14, 2800000, 4, false);

        // ── GSW ──
        Add("GSW", "Stephen", "Curry", "PG", 38, "USA", 188, 84, 93, 93, 82, 92, 96, 88, 92, 58, 42, 74, 98, 68, 12, 59600000, 2, false);
        Add("GSW", "Jimmy", "Butler III", "SF", 37, "USA", 201, 104, 89, 89, 80, 86, 72, 84, 86, 86, 70, 84, 92, 80, 42, 54000000, 2, false);
        Add("GSW", "Kristaps", "Porzingis", "C", 31, "LAT", 221, 109, 86, 86, 72, 84, 80, 70, 64, 80, 78, 76, 84, 48, 82, 30700000, 2, false);
        Add("GSW", "Draymond", "Green", "PF", 36, "USA", 198, 104, 83, 83, 68, 68, 62, 86, 78, 90, 76, 74, 92, 84, 48, 26000000, 3, false);
        Add("GSW", "Brandin", "Podziemski", "SG", 23, "USA", 193, 93, 82, 89, 80, 76, 74, 80, 84, 74, 66, 80, 82, 68, 28, 4000000, 2, false);
        Add("GSW", "Moses", "Moody", "SG", 24, "USA", 196, 96, 80, 85, 78, 78, 80, 68, 74, 76, 60, 80, 76, 74, 32, 12000000, 3, false);
        Add("GSW", "De'Anthony", "Melton", "SG", 28, "USA", 188, 91, 79, 79, 82, 72, 68, 72, 74, 84, 58, 78, 76, 82, 24, 9000000, 1, false);
        Add("GSW", "Al", "Horford", "C", 40, "DOM", 206, 109, 78, 78, 58, 72, 72, 74, 66, 78, 78, 62, 88, 52, 68, 9000000, 1, false);
        Add("GSW", "Gary", "Payton II", "SG", 33, "USA", 188, 88, 77, 77, 82, 64, 52, 62, 68, 88, 54, 84, 72, 86, 22, 9000000, 1, false);
        Add("GSW", "Seth", "Curry", "SG", 36, "USA", 188, 76, 76, 76, 72, 80, 88, 68, 72, 52, 38, 62, 76, 44, 10, 5000000, 1, false);
        Add("GSW", "Quinten", "Post", "C", 25, "NED", 213, 111, 75, 82, 62, 74, 76, 60, 58, 68, 80, 68, 72, 42, 58, 2200000, 3, false);
        Add("GSW", "Charles", "Bassey", "C", 26, "NGA", 208, 104, 74, 80, 68, 58, 24, 50, 46, 72, 84, 78, 68, 48, 72, 2500000, 2, false);
        Add("GSW", "Gui", "Santos", "SF", 24, "BRA", 203, 95, 74, 82, 76, 66, 62, 64, 68, 74, 64, 78, 70, 64, 34, 2200000, 3, false);

        // ── HOU ──
        Add("HOU", "Kevin", "Durant", "SF", 38, "USA", 211, 109, 92, 92, 76, 92, 86, 84, 88, 74, 68, 80, 94, 58, 42, 54700000, 2, false);
        Add("HOU", "Alperen", "Sengun", "C", 24, "TUR", 211, 110, 90, 94, 72, 84, 72, 88, 84, 76, 88, 78, 92, 58, 48, 38000000, 5, false);
        Add("HOU", "Amen", "Thompson", "SF", 23, "USA", 201, 97, 88, 96, 94, 74, 58, 82, 84, 90, 76, 96, 84, 88, 54, 11000000, 3, false);
        Add("HOU", "Fred", "VanVleet", "PG", 32, "USA", 183, 89, 84, 84, 78, 80, 76, 86, 84, 78, 48, 74, 88, 84, 18, 44500000, 2, false);
        Add("HOU", "Jabari", "Smith Jr.", "PF", 23, "USA", 211, 100, 84, 91, 78, 80, 80, 70, 76, 82, 78, 84, 82, 68, 52, 15000000, 2, false);
        Add("HOU", "Tari", "Eason", "SF", 25, "USA", 203, 98, 83, 89, 84, 74, 66, 70, 74, 88, 74, 90, 78, 86, 46, 12000000, 3, false);
        Add("HOU", "Reed", "Sheppard", "PG", 22, "USA", 188, 82, 81, 92, 80, 80, 88, 82, 84, 68, 48, 72, 82, 78, 14, 11000000, 3, false);
        Add("HOU", "Dorian", "Finney-Smith", "SF", 33, "USA", 201, 100, 79, 79, 74, 72, 76, 66, 68, 84, 64, 80, 76, 80, 36, 14000000, 2, false);
        Add("HOU", "Clint", "Capela", "C", 32, "SUI", 208, 116, 79, 79, 68, 62, 22, 52, 46, 82, 90, 76, 72, 48, 78, 9000000, 1, false);
        Add("HOU", "Steven", "Adams", "C", 33, "NZL", 211, 120, 77, 77, 56, 58, 18, 54, 46, 84, 92, 72, 76, 42, 66, 6000000, 1, false);
        Add("HOU", "Josh", "Okogie", "SG", 28, "NGA", 193, 97, 75, 75, 82, 62, 54, 60, 64, 86, 58, 86, 70, 82, 28, 3500000, 1, false);
        Add("HOU", "Aaron", "Holiday", "PG", 30, "USA", 183, 84, 74, 74, 78, 70, 72, 74, 72, 64, 42, 66, 72, 62, 12, 3500000, 1, false);
        Add("HOU", "Jae'Sean", "Tate", "SF", 31, "USA", 193, 104, 74, 74, 72, 66, 56, 62, 66, 78, 64, 80, 70, 72, 28, 3500000, 1, false);
        Add("HOU", "Jeff", "Green", "PF", 40, "USA", 203, 107, 73, 73, 66, 68, 64, 58, 60, 68, 62, 68, 74, 52, 34, 3500000, 1, false);
        Add("HOU", "Tristen", "Newton", "PG", 24, "USA", 196, 86, 72, 80, 76, 64, 62, 76, 74, 60, 46, 68, 70, 58, 14, 2200000, 3, false);

        // ── IND ──
        Add("IND", "Tyrese", "Haliburton", "PG", 26, "USA", 196, 84, 93, 95, 84, 84, 82, 98, 92, 70, 52, 78, 96, 74, 18, 55000000, 5, false);
        Add("IND", "Pascal", "Siakam", "PF", 32, "CMR", 203, 104, 89, 89, 78, 86, 70, 82, 84, 80, 74, 84, 88, 68, 38, 52000000, 4, false);
        Add("IND", "Ivica", "Zubac", "C", 29, "CRO", 213, 109, 85, 85, 64, 78, 36, 68, 62, 84, 92, 76, 82, 50, 74, 20000000, 3, false);
        Add("IND", "Andrew", "Nembhard", "PG", 26, "CAN", 193, 87, 84, 86, 80, 76, 70, 86, 84, 76, 54, 78, 86, 76, 22, 18000000, 4, false);
        Add("IND", "Aaron", "Nesmith", "SF", 27, "USA", 198, 98, 82, 83, 82, 80, 82, 68, 74, 82, 64, 84, 78, 82, 34, 16000000, 3, false);
        Add("IND", "Jarace", "Walker", "PF", 22, "USA", 203, 107, 80, 91, 76, 72, 68, 72, 74, 84, 72, 88, 78, 78, 50, 9000000, 3, false);
        Add("IND", "Obi", "Toppin", "PF", 28, "USA", 206, 100, 79, 79, 82, 76, 70, 66, 72, 68, 66, 88, 74, 58, 30, 14000000, 2, false);
        Add("IND", "T.J.", "McConnell", "PG", 34, "USA", 185, 86, 79, 79, 78, 72, 60, 86, 82, 72, 44, 72, 86, 84, 12, 10000000, 2, false);
        Add("IND", "Ben", "Sheppard", "SG", 25, "USA", 198, 86, 77, 82, 76, 72, 78, 68, 72, 74, 56, 74, 74, 70, 22, 3200000, 2, false);
        Add("IND", "Johnny", "Furphy", "SF", 21, "AUS", 206, 86, 76, 89, 78, 72, 76, 66, 72, 72, 58, 80, 72, 64, 30, 2900000, 3, false);
        Add("IND", "Jay", "Huff", "C", 28, "USA", 216, 109, 77, 78, 62, 74, 72, 60, 56, 72, 84, 66, 74, 42, 74, 4500000, 2, false);
        Add("IND", "Kam", "Jones", "PG", 23, "USA", 193, 92, 75, 84, 78, 72, 70, 78, 76, 60, 46, 72, 74, 62, 18, 2800000, 4, false);
        Add("IND", "Kobe", "Brown", "PF", 26, "USA", 201, 113, 74, 78, 70, 68, 64, 62, 64, 72, 68, 74, 70, 58, 34, 2600000, 2, false);
        Add("IND", "Quenton", "Jackson", "SG", 27, "USA", 196, 79, 73, 76, 82, 66, 60, 68, 70, 68, 48, 78, 68, 66, 18, 2200000, 2, false);

        // ── LAC ──
        Add("LAC", "Kawhi", "Leonard", "SF", 35, "USA", 201, 102, 91, 91, 76, 88, 80, 82, 88, 90, 72, 82, 94, 84, 42, 50000000, 2, false);
        Add("LAC", "Darius", "Garland", "PG", 26, "USA", 185, 87, 89, 91, 86, 86, 80, 92, 90, 58, 42, 76, 90, 64, 10, 40000000, 4, false);
        Add("LAC", "Bradley", "Beal", "SG", 33, "USA", 193, 94, 85, 85, 80, 84, 78, 78, 84, 62, 46, 76, 84, 58, 18, 5500000, 2, false);
        Add("LAC", "John", "Collins", "PF", 29, "USA", 206, 103, 84, 84, 80, 82, 70, 66, 74, 74, 80, 86, 78, 56, 38, 26000000, 3, false);
        Add("LAC", "Bennedict", "Mathurin", "SG", 24, "CAN", 198, 95, 84, 90, 86, 84, 76, 72, 82, 68, 58, 84, 78, 58, 24, 9000000, 2, false);
        Add("LAC", "Brook", "Lopez", "C", 38, "USA", 216, 127, 80, 80, 52, 78, 76, 62, 58, 80, 86, 58, 84, 42, 84, 9000000, 1, false);
        Add("LAC", "Bogdan", "Bogdanovic", "SG", 34, "SRB", 196, 102, 80, 80, 72, 80, 84, 74, 78, 60, 48, 70, 82, 54, 16, 16000000, 2, false);
        Add("LAC", "Derrick", "Jones Jr.", "SF", 29, "USA", 198, 95, 79, 79, 84, 68, 58, 60, 68, 84, 64, 92, 72, 82, 42, 11000000, 2, false);
        Add("LAC", "Kris", "Dunn", "PG", 32, "USA", 191, 93, 78, 78, 76, 64, 54, 82, 78, 88, 52, 78, 74, 88, 24, 6000000, 2, false);
        Add("LAC", "Nicolas", "Batum", "PF", 38, "FRA", 203, 104, 76, 76, 62, 68, 76, 72, 70, 76, 66, 64, 84, 62, 34, 5000000, 1, false);
        Add("LAC", "Isaiah", "Jackson", "C", 24, "USA", 208, 93, 77, 85, 78, 62, 22, 54, 50, 78, 84, 86, 68, 52, 84, 8000000, 3, false);
        Add("LAC", "Jordan", "Miller", "SG", 25, "USA", 196, 88, 76, 82, 78, 70, 66, 68, 72, 68, 54, 76, 72, 62, 20, 2500000, 2, false);
        Add("LAC", "TyTy", "Washington Jr.", "PG", 24, "USA", 191, 89, 74, 80, 78, 68, 62, 78, 76, 56, 42, 70, 72, 60, 12, 2500000, 2, false);
        Add("LAC", "Cam", "Christie", "SG", 21, "USA", 198, 86, 73, 84, 76, 68, 74, 62, 68, 66, 52, 74, 68, 62, 18, 2200000, 4, false);

        // ── LAL ──
        Add("LAL", "Luka", "Doncic", "PG", 27, "SLO", 201, 104, 97, 98, 82, 94, 82, 96, 96, 68, 72, 82, 98, 68, 24, 55000000, 5, false);
        Add("LAL", "LeBron", "James", "SF", 42, "USA", 206, 113, 90, 90, 72, 86, 72, 88, 88, 72, 74, 78, 98, 62, 28, 52600000, 1, false);
        Add("LAL", "Austin", "Reaves", "SG", 28, "USA", 196, 89, 86, 88, 80, 84, 80, 84, 86, 66, 54, 76, 88, 62, 18, 30000000, 4, false);
        Add("LAL", "Deandre", "Ayton", "C", 28, "BAH", 213, 113, 84, 84, 70, 78, 32, 62, 58, 78, 88, 74, 76, 46, 58, 34000000, 2, false);
        Add("LAL", "Marcus", "Smart", "PG", 32, "USA", 193, 100, 81, 81, 74, 70, 64, 80, 76, 88, 54, 76, 84, 88, 22, 21000000, 2, false);
        Add("LAL", "Rui", "Hachimura", "PF", 28, "JPN", 203, 104, 80, 80, 74, 80, 70, 64, 70, 70, 68, 80, 76, 52, 30, 18000000, 2, false);
        Add("LAL", "Jarred", "Vanderbilt", "PF", 27, "USA", 203, 97, 79, 79, 80, 64, 42, 60, 66, 88, 74, 90, 72, 84, 34, 12000000, 3, false);
        Add("LAL", "Dalton", "Knecht", "SG", 25, "USA", 198, 96, 78, 87, 78, 80, 86, 66, 72, 60, 50, 72, 74, 54, 18, 4500000, 3, false);
        Add("LAL", "Jake", "LaRavia", "SF", 25, "USA", 201, 106, 77, 80, 74, 72, 72, 68, 70, 72, 62, 74, 74, 62, 28, 6000000, 2, false);
        Add("LAL", "Maxi", "Kleber", "PF", 34, "GER", 208, 109, 76, 76, 62, 68, 76, 60, 58, 74, 70, 64, 78, 54, 42, 11000000, 1, false);
        Add("LAL", "Nick", "Smith Jr.", "SG", 22, "USA", 188, 84, 76, 87, 82, 74, 72, 74, 76, 56, 42, 74, 72, 58, 12, 4500000, 3, false);
        Add("LAL", "Jaxson", "Hayes", "C", 26, "USA", 213, 100, 75, 80, 78, 66, 22, 50, 48, 74, 82, 86, 68, 48, 74, 3000000, 2, false);
        Add("LAL", "Drew", "Timme", "PF", 26, "USA", 208, 107, 74, 78, 62, 72, 54, 68, 66, 62, 76, 62, 80, 42, 24, 2200000, 2, false);
        Add("LAL", "Bronny", "James", "PG", 22, "USA", 191, 95, 70, 80, 78, 62, 58, 68, 70, 72, 46, 76, 68, 70, 16, 2500000, 3, false);
        Add("LAL", "Luke", "Kennard", "SG", 30, "USA", 196, 93, 76, 76, 70, 74, 90, 66, 72, 52, 42, 64, 74, 44, 12, 9000000, 2, false);

        // ── MEM ──
        Add("MEM", "Ja", "Morant", "PG", 27, "USA", 188, 79, 92, 94, 96, 88, 72, 90, 92, 62, 48, 94, 88, 68, 18, 44000000, 4, false);
        Add("MEM", "Taylor", "Hendricks", "PF", 23, "USA", 206, 97, 84, 92, 80, 78, 76, 68, 74, 86, 74, 88, 80, 72, 58, 11000000, 3, false);
        Add("MEM", "Zach", "Edey", "C", 24, "CAN", 224, 136, 83, 90, 52, 82, 28, 60, 56, 82, 96, 68, 82, 42, 84, 9000000, 3, false);
        Add("MEM", "GG", "Jackson", "SF", 21, "USA", 206, 98, 82, 93, 82, 82, 76, 68, 78, 72, 68, 86, 76, 60, 38, 6000000, 3, false);
        Add("MEM", "Kentavious", "Caldwell-Pope", "SG", 33, "USA", 196, 93, 80, 80, 78, 76, 80, 68, 72, 84, 56, 74, 78, 82, 24, 18000000, 2, false);
        Add("MEM", "Santi", "Aldama", "PF", 25, "ESP", 211, 98, 80, 85, 74, 78, 80, 70, 74, 72, 72, 76, 78, 58, 42, 12000000, 4, false);
        Add("MEM", "Ty", "Jerome", "PG", 29, "USA", 196, 88, 80, 80, 74, 78, 74, 84, 82, 58, 42, 68, 84, 62, 10, 9000000, 2, false);
        Add("MEM", "Jaylen", "Wells", "SG", 22, "USA", 201, 93, 79, 89, 80, 76, 78, 68, 74, 78, 58, 82, 74, 72, 26, 3500000, 3, false);
        Add("MEM", "Scotty", "Pippen Jr.", "PG", 25, "USA", 185, 84, 78, 84, 84, 72, 66, 82, 80, 68, 42, 76, 76, 72, 12, 5000000, 3, false);
        Add("MEM", "Cedric", "Coward", "SF", 22, "USA", 198, 94, 77, 88, 80, 72, 76, 64, 70, 76, 60, 82, 72, 70, 30, 4000000, 4, false);
        Add("MEM", "Cam", "Spencer", "SG", 26, "USA", 193, 93, 76, 80, 74, 74, 82, 68, 72, 62, 48, 68, 76, 58, 16, 2200000, 2, false);
        Add("MEM", "Rayan", "Rupert", "SF", 22, "FRA", 198, 88, 75, 88, 82, 66, 60, 64, 68, 82, 58, 86, 70, 78, 34, 3500000, 3, false);
        Add("MEM", "Olivier-Maxence", "Prosper", "SF", 24, "CAN", 201, 104, 75, 84, 78, 66, 58, 62, 66, 80, 60, 84, 70, 74, 30, 3200000, 3, false);
        Add("MEM", "Walter", "Clayton Jr.", "PG", 23, "USA", 188, 88, 74, 84, 80, 74, 78, 74, 76, 56, 40, 72, 72, 58, 10, 2800000, 4, false);

        // ── MIA ──
        Add("MIA", "Bam", "Adebayo", "C", 29, "USA", 206, 116, 90, 90, 78, 82, 68, 80, 82, 92, 88, 88, 90, 78, 62, 51000000, 4, false);
        Add("MIA", "Tyler", "Herro", "SG", 26, "USA", 196, 88, 87, 89, 82, 88, 86, 82, 88, 62, 48, 76, 86, 58, 12, 33000000, 4, false);
        Add("MIA", "Andrew", "Wiggins", "SF", 31, "CAN", 201, 89, 83, 83, 82, 80, 72, 70, 76, 82, 66, 88, 80, 74, 38, 28000000, 2, false);
        Add("MIA", "Norman", "Powell", "SG", 33, "USA", 193, 97, 82, 82, 82, 84, 80, 68, 78, 64, 52, 80, 80, 58, 18, 22000000, 2, false);
        Add("MIA", "Kel'el", "Ware", "C", 22, "USA", 213, 104, 82, 93, 74, 74, 64, 62, 60, 82, 88, 84, 76, 52, 84, 8000000, 3, false);
        Add("MIA", "Nikola", "Jovic", "PF", 23, "SRB", 208, 101, 80, 89, 76, 78, 78, 74, 78, 68, 70, 78, 80, 56, 40, 6000000, 3, false);
        Add("MIA", "Jaime", "Jaquez Jr.", "SF", 25, "USA", 198, 102, 80, 86, 76, 78, 68, 74, 80, 74, 62, 80, 82, 66, 28, 7000000, 3, false);
        Add("MIA", "Davion", "Mitchell", "PG", 28, "USA", 183, 92, 79, 79, 82, 70, 64, 80, 76, 88, 44, 76, 76, 90, 10, 9000000, 2, false);
        Add("MIA", "Kasparas", "Jakucionis", "PG", 20, "LIT", 196, 91, 78, 92, 78, 74, 76, 84, 82, 62, 48, 74, 78, 64, 16, 7000000, 4, false);
        Add("MIA", "Pelle", "Larsson", "SG", 24, "SWE", 196, 98, 76, 82, 76, 70, 72, 68, 72, 74, 56, 78, 72, 68, 22, 2500000, 3, false);
        Add("MIA", "Simone", "Fontecchio", "SF", 31, "ITA", 201, 95, 76, 76, 72, 74, 82, 66, 70, 64, 56, 70, 74, 56, 20, 8000000, 2, false);
        Add("MIA", "Keshad", "Johnson", "PF", 24, "USA", 201, 102, 74, 82, 78, 64, 56, 60, 64, 78, 64, 84, 70, 72, 32, 2000000, 3, false);
        Add("MIA", "Dru", "Smith", "PG", 28, "USA", 191, 92, 73, 73, 76, 64, 58, 74, 72, 74, 42, 72, 70, 78, 10, 2200000, 1, false);

        // ── MIL ──
        Add("MIL", "Giannis", "Antetokounmpo", "PF", 32, "GRE", 211, 110, 97, 97, 90, 88, 58, 82, 86, 94, 90, 96, 94, 74, 72, 62000000, 4, false);
        Add("MIL", "Myles", "Turner", "C", 30, "USA", 211, 113, 87, 87, 68, 80, 74, 64, 62, 86, 88, 76, 84, 48, 90, 32000000, 4, false);
        Add("MIL", "Kyle", "Kuzma", "SF", 31, "USA", 206, 100, 82, 82, 76, 80, 72, 68, 76, 68, 66, 78, 78, 56, 28, 22000000, 2, false);
        Add("MIL", "Bobby", "Portis", "PF", 32, "USA", 208, 113, 82, 82, 66, 82, 72, 62, 68, 70, 86, 70, 78, 48, 34, 13000000, 2, false);
        Add("MIL", "Kevin", "Porter Jr.", "PG", 26, "USA", 193, 92, 81, 84, 84, 80, 70, 82, 84, 58, 48, 82, 76, 62, 18, 10000000, 2, false);
        Add("MIL", "Gary", "Trent Jr.", "SG", 27, "USA", 196, 94, 80, 80, 80, 80, 82, 66, 74, 68, 52, 74, 76, 72, 20, 9000000, 2, false);
        Add("MIL", "AJ", "Green", "SG", 27, "USA", 193, 86, 78, 80, 74, 76, 88, 64, 70, 60, 48, 68, 74, 54, 12, 7000000, 3, false);
        Add("MIL", "Taurean", "Prince", "SF", 32, "USA", 198, 99, 77, 77, 72, 74, 80, 64, 68, 74, 58, 72, 74, 68, 24, 6000000, 1, false);
        Add("MIL", "Andre", "Jackson Jr.", "SG", 25, "USA", 198, 95, 77, 86, 82, 64, 54, 68, 72, 84, 60, 88, 72, 82, 26, 4000000, 3, false);
        Add("MIL", "Ryan", "Rollins", "PG", 24, "USA", 193, 82, 76, 84, 80, 72, 68, 78, 76, 60, 42, 74, 72, 62, 12, 3500000, 3, false);
        Add("MIL", "Ousmane", "Dieng", "SF", 23, "FRA", 208, 84, 76, 89, 78, 68, 66, 72, 74, 78, 62, 84, 72, 70, 34, 5000000, 3, false);
        Add("MIL", "Jericho", "Sims", "C", 28, "USA", 208, 113, 74, 76, 72, 58, 20, 48, 44, 74, 82, 82, 66, 44, 72, 3000000, 2, false);
        Add("MIL", "Gary", "Harris", "SG", 32, "USA", 193, 95, 73, 73, 72, 66, 70, 62, 66, 72, 48, 70, 72, 68, 16, 3000000, 1, false);
        Add("MIL", "Pete", "Nance", "PF", 26, "USA", 208, 102, 71, 76, 66, 66, 68, 58, 60, 68, 70, 66, 68, 48, 34, 1800000, 2, false);
        Add("MIL", "Thanasis", "Antetokounmpo", "SF", 34, "GRE", 201, 99, 68, 68, 78, 58, 30, 50, 54, 74, 58, 82, 64, 68, 26, 1200000, 1, false);

        // ── MIN ──
        Add("MIN", "Anthony", "Edwards", "SG", 25, "USA", 193, 102, 96, 97, 94, 90, 86, 84, 88, 74, 60, 88, 90, 72, 20, 58000000, 5, false);
        Add("MIN", "Julius", "Randle", "PF", 31, "USA", 203, 113, 86, 86, 82, 84, 70, 74, 78, 76, 78, 82, 82, 58, 36, 33000000, 3, false);
        Add("MIN", "Rudy", "Gobert", "C", 34, "FRA", 216, 117, 84, 84, 58, 70, 20, 54, 50, 92, 96, 80, 82, 46, 90, 38000000, 2, false);
        Add("MIN", "Jaden", "McDaniels", "SF", 25, "USA", 206, 95, 84, 88, 82, 76, 72, 70, 74, 90, 68, 88, 80, 84, 44, 18000000, 4, false);
        Add("MIN", "Donte", "DiVincenzo", "SG", 29, "USA", 193, 92, 82, 82, 82, 82, 84, 76, 78, 74, 54, 78, 80, 78, 20, 12000000, 2, false);
        Add("MIN", "Naz", "Reid", "C", 27, "USA", 206, 113, 82, 88, 76, 84, 80, 70, 74, 74, 80, 76, 78, 56, 48, 16000000, 4, false);
        Add("MIN", "Mike", "Conley", "PG", 38, "USA", 185, 79, 80, 80, 72, 78, 80, 88, 86, 64, 40, 70, 84, 78, 8, 10000000, 1, false);
        Add("MIN", "Ayo", "Dosunmu", "PG", 26, "USA", 193, 91, 79, 82, 82, 78, 74, 80, 82, 72, 50, 76, 80, 74, 14, 8000000, 3, false);
        Add("MIN", "Kyle", "Anderson", "SF", 32, "USA", 206, 104, 78, 78, 68, 72, 70, 78, 80, 80, 74, 76, 80, 76, 26, 9000000, 1, false);
        Add("MIN", "Terrence", "Shannon Jr.", "SG", 25, "USA", 198, 98, 80, 90, 84, 78, 72, 70, 74, 72, 60, 82, 78, 70, 30, 6000000, 3, false);
        Add("MIN", "Jaylen", "Clark", "SG", 24, "USA", 193, 88, 76, 84, 82, 70, 62, 72, 74, 84, 58, 84, 74, 82, 28, 3000000, 3, false);
        Add("MIN", "Bones", "Hyland", "PG", 25, "USA", 188, 80, 75, 85, 84, 74, 80, 76, 78, 54, 40, 70, 72, 58, 10, 4000000, 2, false);
        Add("MIN", "Joe", "Ingles", "SF", 38, "AUS", 203, 102, 74, 74, 52, 68, 82, 78, 80, 70, 54, 60, 78, 60, 6, 3000000, 1, false);

        // ── NOP ──
        Add("NOP", "Zion", "Williamson", "PF", 26, "USA", 201, 129, 95, 96, 90, 84, 60, 70, 74, 88, 86, 94, 88, 60, 48, 60000000, 4, false);
        Add("NOP", "Dejounte", "Murray", "PG", 30, "USA", 193, 83, 85, 86, 88, 80, 72, 88, 86, 82, 58, 82, 86, 88, 24, 28000000, 3, false);
        Add("NOP", "Jordan", "Poole", "SG", 27, "USA", 193, 88, 82, 88, 84, 86, 82, 72, 80, 58, 40, 78, 80, 60, 18, 25000000, 3, false);
        Add("NOP", "Trey", "Murphy III", "SF", 26, "USA", 206, 95, 84, 90, 82, 84, 86, 70, 78, 76, 60, 84, 82, 70, 30, 16000000, 4, false);
        Add("NOP", "Herbert", "Jones", "SF", 27, "USA", 201, 95, 83, 83, 78, 74, 62, 72, 74, 90, 68, 90, 78, 88, 26, 14000000, 3, false);
        Add("NOP", "Saddiq", "Bey", "SF", 27, "USA", 203, 102, 81, 81, 76, 80, 80, 68, 74, 70, 60, 76, 76, 66, 24, 12000000, 2, false);
        Add("NOP", "Kevon", "Looney", "C", 30, "USA", 206, 111, 80, 80, 60, 72, 40, 68, 64, 86, 88, 74, 78, 48, 80, 12000000, 2, false);
        Add("NOP", "Yves", "Missi", "C", 22, "CMR", 213, 104, 80, 90, 74, 70, 28, 58, 54, 82, 88, 82, 74, 50, 84, 9000000, 4, false);
        Add("NOP", "Hunter", "Dickinson", "C", 25, "USA", 213, 118, 78, 84, 52, 74, 44, 66, 60, 78, 90, 70, 80, 42, 82, 6000000, 2, false);
        Add("NOP", "DeAndre", "Jordan", "C", 38, "USA", 211, 120, 74, 74, 40, 60, 10, 50, 48, 78, 82, 68, 76, 40, 76, 3000000, 1, false);
        Add("NOP", "Jordan", "Hawkins", "SG", 23, "USA", 193, 88, 78, 86, 80, 82, 88, 62, 70, 54, 40, 70, 72, 54, 14, 4000000, 3, false);
        Add("NOP", "Trey", "Alexander", "SG", 23, "USA", 193, 86, 76, 84, 82, 78, 76, 70, 74, 60, 42, 74, 72, 58, 12, 2500000, 4, false);
        Add("NOP", "Bryce", "McGowens", "SG", 24, "USA", 198, 90, 75, 84, 84, 76, 70, 66, 70, 58, 42, 72, 70, 56, 14, 2000000, 3, false);
        Add("NOP", "Jeremiah", "Fears", "PG", 20, "USA", 188, 82, 78, 92, 88, 78, 76, 82, 80, 56, 42, 76, 74, 60, 10, 5000000, 4, false);
        Add("NOP", "Derik", "Queen", "PF", 20, "USA", 208, 110, 77, 90, 70, 76, 62, 66, 68, 76, 82, 78, 78, 52, 44, 4000000, 4, false);

        // ── NYK ──
        Add("NYK", "Jalen", "Brunson", "PG", 29, "USA", 188, 86, 93, 93, 84, 88, 82, 90, 92, 58, 44, 74, 88, 62, 18, 48000000, 4, false);
        Add("NYK", "Karl-Anthony", "Towns", "C", 30, "USA", 213, 112, 89, 90, 78, 88, 84, 78, 80, 70, 78, 80, 86, 58, 38, 52000000, 4, false);
        Add("NYK", "Mikal", "Bridges", "SF", 30, "USA", 198, 95, 87, 87, 84, 80, 74, 72, 78, 92, 70, 88, 82, 86, 28, 30000000, 4, false);
        Add("NYK", "OG", "Anunoby", "SF", 29, "GBR", 201, 105, 87, 87, 82, 76, 70, 72, 76, 94, 72, 90, 82, 90, 30, 28000000, 4, false);
        Add("NYK", "Josh", "Hart", "SG", 31, "USA", 196, 97, 84, 84, 82, 78, 74, 78, 80, 88, 70, 86, 82, 84, 34, 18000000, 3, false);
        Add("NYK", "Mitchell", "Robinson", "C", 28, "USA", 213, 111, 83, 83, 70, 60, 18, 52, 48, 92, 94, 90, 80, 54, 92, 14000000, 3, false);
        Add("NYK", "Miles", "McBride", "PG", 26, "USA", 185, 88, 81, 81, 82, 76, 74, 80, 78, 84, 52, 78, 76, 82, 16, 10000000, 3, false);
        Add("NYK", "Jordan", "Clarkson", "SG", 33, "USA", 193, 88, 81, 81, 82, 86, 84, 72, 78, 50, 40, 70, 78, 58, 16, 14000000, 2, false);
        Add("NYK", "Tyler", "Kolek", "PG", 25, "USA", 188, 84, 78, 84, 80, 78, 76, 88, 86, 56, 38, 72, 80, 64, 10, 4000000, 3, false);
        Add("NYK", "Landry", "Shamet", "SG", 29, "USA", 193, 88, 76, 76, 74, 76, 84, 62, 70, 56, 44, 66, 74, 52, 12, 6000000, 1, false);
        Add("NYK", "Jeremy", "Sochan", "PF", 23, "USA", 203, 104, 82, 88, 80, 74, 62, 74, 76, 88, 68, 88, 78, 82, 26, 9000000, 3, false);
        Add("NYK", "Pacome", "Dadiet", "SF", 20, "FRA", 203, 93, 76, 88, 78, 70, 68, 66, 70, 76, 58, 82, 74, 70, 28, 3000000, 4, false);
        Add("NYK", "Kevin", "McCullar Jr.", "SF", 25, "USA", 198, 97, 75, 82, 78, 70, 62, 66, 70, 84, 58, 84, 74, 76, 20, 2500000, 3, false);
        Add("NYK", "Ariel", "Hukporti", "C", 23, "GER", 213, 110, 74, 84, 64, 62, 20, 54, 50, 84, 88, 86, 76, 48, 86, 2200000, 3, false);

        // ── OKC ──
        Add("OKC", "Shai", "Gilgeous-Alexander", "PG", 27, "CAN", 198, 88, 97, 97, 94, 90, 82, 88, 92, 70, 52, 88, 92, 78, 20, 62000000, 5, false);
        Add("OKC", "Jalen", "Williams", "SF", 25, "USA", 198, 95, 92, 94, 90, 88, 84, 80, 86, 90, 70, 88, 88, 84, 28, 38000000, 4, false);
        Add("OKC", "Chet", "Holmgren", "C", 24, "USA", 216, 97, 90, 92, 78, 86, 80, 74, 78, 88, 92, 84, 86, 60, 94, 36000000, 4, false);
        Add("OKC", "Isaiah", "Hartenstein", "C", 28, "GER", 213, 113, 84, 84, 70, 76, 52, 74, 70, 88, 90, 82, 84, 58, 88, 18000000, 3, false);
        Add("OKC", "Luguentz", "Dort", "SG", 27, "CAN", 193, 99, 84, 84, 86, 76, 66, 74, 76, 92, 60, 90, 82, 88, 32, 16000000, 3, false);
        Add("OKC", "Alex", "Caruso", "SG", 32, "USA", 196, 92, 82, 82, 80, 74, 68, 78, 80, 90, 64, 92, 84, 92, 16, 14000000, 2, false);
        Add("OKC", "Isaiah", "Joe", "SG", 27, "USA", 193, 88, 80, 80, 76, 82, 88, 70, 76, 60, 44, 66, 78, 58, 10, 8000000, 2, false);
        Add("OKC", "Aaron", "Wiggins", "SF", 27, "USA", 198, 93, 80, 82, 82, 78, 76, 70, 74, 78, 60, 80, 78, 74, 18, 7000000, 3, false);
        Add("OKC", "Cason", "Wallace", "PG", 22, "USA", 191, 88, 82, 88, 86, 78, 72, 84, 82, 88, 58, 86, 80, 86, 26, 9000000, 4, false);
        Add("OKC", "Kenrich", "Williams", "PF", 30, "USA", 198, 99, 78, 78, 70, 72, 66, 72, 74, 82, 70, 82, 78, 80, 28, 7000000, 2, false);
        Add("OKC", "Jaylin", "Williams", "C", 23, "USA", 206, 108, 78, 84, 68, 74, 68, 74, 76, 80, 80, 78, 80, 70, 80, 6000000, 3, false);
        Add("OKC", "Nikola", "Topic", "PG", 20, "SRB", 198, 88, 78, 92, 82, 76, 74, 88, 86, 62, 42, 74, 80, 60, 18, 5000000, 4, false);
        Add("OKC", "Jared", "McCain", "SG", 22, "USA", 191, 88, 80, 88, 82, 82, 84, 72, 76, 58, 42, 70, 78, 56, 14, 6000000, 4, false);
        Add("OKC", "Ajay", "Mitchell", "PG", 23, "BEL", 193, 86, 75, 84, 82, 74, 70, 78, 76, 70, 50, 74, 76, 64, 12, 2000000, 4, false);

        // ── ORL ──
        Add("ORL", "Paolo", "Banchero", "PF", 26, "USA", 208, 113, 92, 94, 86, 88, 76, 78, 82, 80, 78, 86, 88, 62, 44, 48000000, 5, false);
        Add("ORL", "Franz", "Wagner", "SF", 25, "GER", 206, 100, 88, 90, 84, 86, 82, 80, 84, 84, 66, 84, 84, 74, 34, 30000000, 4, false);
        Add("ORL", "Desmond", "Bane", "SG", 28, "USA", 196, 98, 88, 88, 82, 88, 90, 74, 82, 68, 54, 76, 82, 70, 22, 35000000, 4, false);
        Add("ORL", "Jalen", "Suggs", "PG", 25, "USA", 191, 93, 84, 86, 86, 80, 74, 82, 84, 88, 60, 86, 82, 86, 26, 22000000, 4, false);
        Add("ORL", "Wendell", "Carter Jr.", "C", 27, "USA", 208, 113, 84, 84, 70, 78, 68, 72, 70, 86, 88, 82, 82, 54, 88, 18000000, 3, false);
        Add("ORL", "Jonathan", "Isaac", "PF", 28, "USA", 208, 104, 82, 82, 78, 70, 60, 66, 70, 94, 80, 92, 78, 90, 60, 17000000, 3, false);
        Add("ORL", "Anthony", "Black", "PG", 22, "USA", 198, 95, 80, 86, 84, 76, 70, 82, 82, 86, 60, 84, 78, 84, 26, 9000000, 4, false);
        Add("ORL", "Jett", "Howard", "SG", 23, "USA", 198, 92, 78, 84, 78, 78, 86, 68, 74, 60, 48, 70, 76, 58, 14, 6000000, 4, false);
        Add("ORL", "Moritz", "Wagner", "C", 29, "GER", 211, 111, 80, 80, 72, 82, 76, 70, 74, 68, 78, 70, 78, 52, 42, 12000000, 2, false);
        Add("ORL", "Goga", "Bitadze", "C", 27, "GEO", 211, 113, 78, 78, 66, 72, 44, 62, 60, 82, 84, 80, 76, 50, 84, 8000000, 2, false);
        Add("ORL", "Tristan", "da Silva", "SF", 24, "GER", 206, 98, 78, 86, 76, 76, 80, 70, 74, 74, 62, 78, 76, 72, 24, 5000000, 4, false);
        Add("ORL", "Jevon", "Carter", "PG", 31, "USA", 188, 90, 76, 76, 80, 74, 74, 78, 80, 82, 50, 82, 76, 84, 10, 7000000, 1, false);
        Add("ORL", "Jamal", "Cain", "SF", 27, "USA", 198, 95, 74, 82, 80, 72, 66, 68, 72, 78, 60, 80, 74, 76, 18, 2000000, 2, false);

        // ── PHI ──
        Add("PHI", "Joel", "Embiid", "C", 32, "CAM", 213, 127, 96, 96, 78, 88, 72, 80, 82, 88, 90, 78, 90, 52, 60, 65000000, 5, false);
        Add("PHI", "Tyrese", "Maxey", "PG", 26, "USA", 188, 86, 92, 94, 96, 90, 82, 86, 88, 64, 44, 82, 86, 70, 22, 42000000, 5, false);
        Add("PHI", "Paul", "George", "SF", 36, "USA", 206, 100, 88, 88, 84, 86, 86, 74, 80, 88, 60, 86, 84, 88, 18, 35000000, 2, false);
        Add("PHI", "Kelly", "Oubre Jr.", "SF", 30, "USA", 198, 95, 80, 82, 84, 80, 76, 70, 74, 80, 56, 80, 78, 78, 20, 12000000, 2, false);
        Add("PHI", "Quentin", "Grimes", "SG", 26, "USA", 196, 92, 82, 84, 82, 82, 82, 70, 76, 76, 54, 78, 80, 76, 18, 9000000, 3, false);
        Add("PHI", "Kyle", "Lowry", "PG", 40, "USA", 183, 88, 78, 78, 70, 76, 78, 88, 88, 70, 40, 72, 82, 78, 8, 8000000, 1, false);
        Add("PHI", "VJ", "Edgecombe", "SG", 20, "BAH", 193, 90, 80, 92, 90, 82, 78, 74, 78, 70, 52, 80, 78, 74, 20, 6000000, 4, false);
        Add("PHI", "Justin", "Edwards", "SF", 22, "USA", 201, 95, 78, 88, 82, 78, 74, 68, 72, 78, 58, 80, 76, 76, 18, 3000000, 4, false);
        Add("PHI", "Dominick", "Barlow", "PF", 23, "USA", 206, 102, 76, 86, 78, 74, 66, 70, 72, 80, 72, 82, 76, 78, 22, 2500000, 3, false);
        Add("PHI", "Trendon", "Watford", "PF", 25, "USA", 203, 104, 78, 84, 78, 78, 70, 74, 76, 76, 66, 78, 78, 74, 20, 4000000, 2, false);
        Add("PHI", "Andre", "Drummond", "C", 32, "USA", 211, 127, 80, 80, 60, 70, 18, 52, 50, 86, 88, 84, 80, 46, 84, 5000000, 1, false);
        Add("PHI", "Adem", "Bona", "C", 22, "NGR", 208, 104, 78, 88, 72, 70, 40, 58, 60, 84, 88, 84, 78, 50, 86, 3000000, 4, false);
        Add("PHI", "Johni", "Broome", "C", 24, "USA", 206, 110, 76, 82, 66, 74, 50, 62, 64, 82, 86, 78, 80, 48, 84, 3000000, 3, false);
        Add("PHI", "MarJon", "Beauchamp", "SF", 25, "USA", 201, 95, 76, 84, 84, 72, 64, 66, 70, 76, 58, 80, 74, 72, 16, 2500000, 3, false);

        // ── PHX ──
        Add("PHX", "Devin", "Booker", "SG", 29, "USA", 198, 96, 95, 96, 88, 92, 86, 82, 88, 72, 56, 84, 90, 74, 24, 60000000, 5, false);
        Add("PHX", "Jalen", "Green", "SG", 24, "USA", 198, 89, 86, 90, 92, 86, 80, 74, 78, 58, 42, 76, 80, 60, 20, 35000000, 4, false);
        Add("PHX", "Mark", "Williams", "C", 25, "USA", 213, 118, 84, 86, 70, 74, 34, 60, 62, 88, 90, 82, 82, 52, 86, 22000000, 4, false);
        Add("PHX", "Dillon", "Brooks", "SF", 30, "CAN", 198, 102, 82, 82, 84, 78, 74, 72, 76, 86, 60, 86, 80, 84, 18, 20000000, 3, false);
        Add("PHX", "Grayson", "Allen", "SG", 30, "USA", 193, 90, 82, 82, 80, 84, 86, 74, 80, 70, 48, 72, 80, 72, 12, 15000000, 2, false);
        Add("PHX", "Royce", "O'Neale", "SF", 32, "USA", 198, 104, 80, 80, 76, 74, 78, 74, 78, 84, 62, 82, 78, 80, 14, 11000000, 2, false);
        Add("PHX", "Ryan", "Dunn", "SF", 23, "USA", 203, 98, 78, 88, 82, 72, 66, 68, 72, 84, 62, 86, 78, 82, 18, 5000000, 4, false);
        Add("PHX", "Khaman", "Maluach", "C", 20, "SUD", 218, 110, 78, 92, 68, 72, 40, 56, 58, 86, 92, 84, 82, 50, 90, 6000000, 4, false);
        Add("PHX", "Oso", "Ighodaro", "C", 23, "USA", 208, 104, 78, 86, 72, 70, 48, 62, 64, 84, 88, 82, 80, 54, 84, 4000000, 3, false);
        Add("PHX", "Jordan", "Goodwin", "PG", 27, "USA", 193, 92, 78, 78, 80, 74, 70, 78, 80, 80, 52, 82, 76, 82, 10, 4000000, 2, false);
        Add("PHX", "Collin", "Gillespie", "PG", 27, "USA", 185, 84, 76, 80, 78, 76, 78, 84, 82, 58, 40, 70, 78, 66, 8, 3000000, 2, false);
        Add("PHX", "Amir", "Coffey", "SG", 28, "USA", 198, 93, 76, 78, 80, 74, 72, 70, 74, 76, 50, 78, 74, 74, 10, 3000000, 1, false);

        // ── POR ──
        Add("POR", "Damian", "Lillard", "PG", 36, "USA", 188, 88, 92, 92, 84, 92, 86, 90, 92, 60, 40, 78, 88, 70, 14, 40000000, 2, false);
        Add("POR", "Scoot", "Henderson", "PG", 22, "USA", 191, 90, 88, 92, 94, 84, 78, 86, 86, 66, 50, 82, 84, 72, 26, 12000000, 4, false);
        Add("POR", "Shaedon", "Sharpe", "SG", 23, "CAN", 198, 93, 90, 94, 96, 88, 84, 78, 82, 70, 54, 84, 86, 68, 24, 18000000, 4, false);
        Add("POR", "Jrue", "Holiday", "PG", 35, "USA", 193, 95, 84, 84, 82, 78, 72, 86, 86, 86, 60, 88, 86, 86, 16, 25000000, 2, false);
        Add("POR", "Jerami", "Grant", "PF", 32, "USA", 206, 102, 84, 84, 82, 82, 78, 74, 78, 82, 66, 80, 80, 78, 18, 28000000, 3, false);
        Add("POR", "Deni", "Avdija", "SF", 26, "ISR", 206, 100, 84, 86, 82, 78, 72, 78, 80, 86, 70, 84, 82, 84, 20, 16000000, 3, false);
        Add("POR", "Toumani", "Camara", "SF", 25, "BEL", 198, 98, 82, 86, 82, 76, 68, 74, 76, 88, 66, 86, 80, 86, 18, 8000000, 3, false);
        Add("POR", "Donovan", "Clingan", "C", 22, "USA", 218, 120, 84, 86, 58, 70, 18, 54, 56, 88, 92, 86, 82, 50, 90, 9000000, 4, false);
        Add("POR", "Robert", "Williams III", "C", 28, "USA", 208, 108, 82, 82, 76, 72, 30, 60, 58, 92, 94, 88, 84, 54, 92, 12000000, 2, false);
        Add("POR", "Matisse", "Thybulle", "SG", 29, "AUS", 196, 95, 80, 80, 84, 70, 60, 66, 70, 94, 58, 90, 78, 90, 14, 9000000, 2, false);
        Add("POR", "Blake", "Wesley", "PG", 23, "USA", 193, 86, 78, 84, 84, 76, 70, 74, 76, 78, 48, 80, 76, 76, 12, 3000000, 3, false);
        Add("POR", "Vit", "Krejci", "SG", 26, "CZE", 198, 88, 76, 78, 78, 78, 80, 74, 76, 70, 48, 74, 76, 68, 10, 4000000, 2, false);
        Add("POR", "Caleb", "Love", "PG", 24, "USA", 188, 84, 76, 86, 86, 80, 82, 72, 76, 58, 38, 72, 74, 60, 12, 2500000, 4, false);
        Add("POR", "Kris", "Murray", "SF", 25, "USA", 203, 98, 76, 84, 78, 76, 72, 70, 74, 80, 62, 78, 76, 74, 16, 3000000, 3, false);

        // ── SAC ──
        Add("SAC", "Domantas", "Sabonis", "C", 30, "LTU", 211, 115, 90, 90, 78, 88, 76, 86, 88, 78, 80, 82, 88, 60, 70, 48000000, 5, false);
        Add("SAC", "Zach", "LaVine", "SG", 31, "USA", 196, 91, 88, 90, 92, 90, 86, 74, 80, 58, 40, 78, 82, 60, 18, 40000000, 4, false);
        Add("SAC", "DeMar", "DeRozan", "SF", 36, "USA", 201, 100, 86, 86, 80, 88, 78, 72, 78, 62, 48, 76, 80, 58, 14, 25000000, 2, false);
        Add("SAC", "Malik", "Monk", "SG", 28, "USA", 191, 90, 85, 88, 88, 86, 84, 70, 76, 52, 38, 72, 80, 58, 14, 18000000, 3, false);
        Add("SAC", "Keegan", "Murray", "SF", 26, "USA", 203, 102, 84, 86, 80, 82, 84, 74, 78, 82, 66, 82, 82, 80, 22, 22000000, 4, false);
        Add("SAC", "De'Andre", "Hunter", "SF", 28, "USA", 203, 102, 83, 84, 78, 80, 74, 70, 74, 86, 66, 84, 80, 82, 20, 20000000, 3, false);
        Add("SAC", "Russell", "Westbrook", "PG", 37, "USA", 191, 91, 82, 82, 88, 78, 64, 84, 86, 60, 44, 78, 80, 72, 10, 8000000, 1, false);
        Add("SAC", "Devin", "Carter", "PG", 23, "USA", 188, 86, 80, 86, 86, 78, 72, 82, 80, 80, 52, 82, 78, 82, 18, 6000000, 4, false);
        Add("SAC", "Doug", "McDermott", "SF", 34, "USA", 201, 102, 78, 78, 72, 82, 88, 62, 70, 54, 40, 60, 76, 52, 8, 5000000, 1, false);
        Add("SAC", "Patrick", "Baldwin Jr.", "SF", 23, "USA", 206, 100, 76, 84, 76, 78, 80, 66, 70, 72, 54, 74, 76, 70, 14, 3000000, 3, false);
        Add("SAC", "Precious", "Achiuwa", "PF", 27, "NGR", 203, 104, 80, 82, 82, 74, 60, 68, 70, 84, 76, 84, 78, 76, 22, 9000000, 2, false);
        Add("SAC", "Drew", "Eubanks", "C", 29, "USA", 208, 111, 78, 78, 68, 72, 44, 60, 62, 82, 84, 80, 78, 48, 84, 5000000, 2, false);
        Add("SAC", "Killian", "Hayes", "PG", 25, "FRA", 196, 88, 76, 80, 80, 72, 66, 80, 82, 78, 54, 80, 74, 78, 12, 4000000, 2, false);
        Add("SAC", "Maxime", "Raynaud", "C", 23, "FRA", 213, 110, 76, 84, 68, 76, 70, 66, 70, 78, 84, 76, 78, 50, 84, 3000000, 4, false);

        // ── SAS ──
        Add("SAS", "Victor", "Wembanyama", "C", 22, "FRA", 224, 104, 96, 96, 78, 88, 74, 80, 84, 96, 96, 94, 92, 88, 98, 60000000, 5, false);
        Add("SAS", "De'Aaron", "Fox", "PG", 28, "USA", 191, 84, 90, 92, 96, 88, 80, 84, 86, 62, 44, 82, 86, 72, 22, 45000000, 5, false);
        Add("SAS", "Stephon", "Castle", "SG", 21, "USA", 198, 96, 84, 88, 86, 82, 74, 80, 82, 78, 58, 82, 82, 80, 28, 9000000, 4, false);
        Add("SAS", "Dylan", "Harper", "SG", 20, "USA", 196, 92, 82, 90, 88, 84, 78, 78, 80, 74, 52, 80, 80, 76, 24, 7000000, 4, false);
        Add("SAS", "Devin", "Vassell", "SG", 27, "USA", 198, 94, 84, 86, 84, 84, 82, 74, 78, 78, 56, 80, 82, 76, 20, 22000000, 4, false);
        Add("SAS", "Keldon", "Johnson", "SF", 26, "USA", 198, 100, 82, 84, 82, 82, 76, 72, 76, 74, 58, 78, 80, 72, 18, 18000000, 3, false);
        Add("SAS", "Harrison", "Barnes", "SF", 34, "USA", 203, 102, 80, 80, 76, 80, 78, 74, 78, 78, 62, 78, 78, 76, 14, 18000000, 2, false);
        Add("SAS", "Julian", "Champagnie", "SF", 26, "USA", 203, 98, 78, 82, 78, 80, 82, 70, 74, 76, 56, 76, 76, 74, 12, 6000000, 2, false);
        Add("SAS", "Luke", "Kornet", "C", 30, "USA", 216, 113, 78, 76, 64, 72, 48, 60, 62, 82, 86, 78, 80, 48, 86, 5000000, 2, false);
        Add("SAS", "Mason", "Plumlee", "C", 36, "USA", 211, 115, 76, 74, 66, 70, 52, 62, 64, 84, 88, 80, 82, 52, 88, 4000000, 1, false);
        Add("SAS", "Kelly", "Olynyk", "PF", 35, "CAN", 211, 108, 78, 78, 70, 82, 80, 76, 80, 74, 70, 74, 80, 68, 30, 12000000, 2, false);
        Add("SAS", "Bismack", "Biyombo", "C", 33, "COD", 206, 116, 76, 72, 62, 60, 20, 50, 52, 88, 90, 86, 82, 46, 88, 5000000, 1, false);
        Add("SAS", "Harrison", "Ingram", "SF", 24, "USA", 201, 95, 74, 82, 80, 74, 72, 70, 74, 78, 58, 78, 76, 76, 14, 2500000, 3, false);

        // ── TOR ──
        Add("TOR", "Scottie", "Barnes", "PF", 25, "USA", 206, 103, 92, 92, 86, 86, 80, 84, 86, 90, 84, 90, 88, 86, 40, 52000000, 5, false);
        Add("TOR", "Brandon", "Ingram", "SF", 28, "USA", 206, 98, 88, 90, 84, 88, 86, 76, 80, 70, 52, 82, 84, 72, 28, 38000000, 4, false);
        Add("TOR", "Immanuel", "Quickley", "PG", 27, "USA", 188, 86, 86, 88, 88, 86, 84, 82, 84, 62, 44, 78, 82, 70, 22, 25000000, 4, false);
        Add("TOR", "RJ", "Barrett", "SF", 26, "CAN", 201, 100, 84, 86, 84, 84, 78, 72, 76, 72, 56, 80, 82, 74, 18, 20000000, 3, false);
        Add("TOR", "Jakob", "Poeltl", "C", 30, "AUT", 213, 114, 84, 82, 70, 76, 50, 70, 72, 88, 90, 86, 84, 52, 88, 16000000, 2, false);
        Add("TOR", "Gradey", "Dick", "SG", 23, "USA", 198, 90, 82, 86, 80, 84, 88, 70, 76, 62, 44, 72, 80, 60, 16, 8000000, 4, false);
        Add("TOR", "Ja'Kobe", "Walter", "SG", 21, "USA", 196, 92, 80, 86, 82, 80, 78, 70, 74, 68, 48, 76, 78, 70, 18, 6000000, 4, false);
        Add("TOR", "Jamal", "Shead", "PG", 24, "CAN", 183, 84, 78, 82, 84, 76, 74, 84, 82, 58, 40, 74, 76, 70, 10, 3000000, 3, false);
        Add("TOR", "Jonathan", "Mogbo", "PF", 23, "USA", 203, 102, 78, 84, 80, 74, 66, 70, 72, 84, 70, 82, 78, 78, 16, 2500000, 4, false);
        Add("TOR", "Collin", "Murray-Boyles", "PF", 22, "USA", 203, 104, 78, 86, 78, 74, 64, 70, 72, 82, 72, 84, 78, 80, 18, 3000000, 4, false);
        Add("TOR", "Trayce", "Jackson-Davis", "C", 26, "USA", 206, 111, 80, 84, 76, 78, 50, 66, 68, 86, 88, 84, 82, 52, 86, 5000000, 3, false);
        Add("TOR", "Sandro", "Mamukelashvili", "PF", 27, "GEO", 208, 104, 78, 80, 74, 80, 76, 72, 76, 78, 72, 76, 78, 70, 14, 4000000, 2, false);
        Add("TOR", "A.J.", "Lawson", "SG", 26, "CAN", 196, 90, 76, 80, 84, 74, 72, 70, 72, 74, 52, 76, 74, 72, 10, 2000000, 2, false);
        Add("TOR", "Jamison", "Battle", "SF", 25, "USA", 198, 95, 76, 82, 78, 78, 82, 68, 72, 70, 52, 74, 76, 72, 12, 2000000, 3, false);

        // ── UTA ──
        Add("UTA", "Lauri", "Markkanen", "PF", 29, "FIN", 213, 109, 90, 92, 84, 90, 88, 74, 80, 72, 56, 78, 84, 70, 26, 42000000, 4, false);
        Add("UTA", "Jaren", "Jackson Jr.", "PF", 27, "USA", 211, 110, 92, 90, 80, 84, 78, 74, 78, 94, 96, 90, 88, 80, 92, 48000000, 5, false);
        Add("UTA", "Walker", "Kessler", "C", 25, "USA", 213, 118, 84, 80, 60, 70, 28, 56, 60, 92, 94, 88, 86, 60, 94, 18000000, 4, false);
        Add("UTA", "Jusuf", "Nurkic", "C", 31, "BIH", 213, 127, 82, 78, 58, 74, 40, 66, 70, 82, 86, 82, 84, 52, 86, 16000000, 2, false);
        Add("UTA", "Keyonte", "George", "PG", 22, "USA", 193, 88, 84, 88, 90, 84, 80, 82, 84, 62, 44, 78, 80, 72, 20, 8000000, 4, false);
        Add("UTA", "Isaiah", "Collier", "PG", 21, "USA", 193, 92, 82, 90, 92, 82, 76, 84, 84, 68, 50, 80, 82, 76, 24, 7000000, 4, false);
        Add("UTA", "Ace", "Bailey", "SF", 19, "USA", 203, 96, 84, 94, 88, 84, 80, 74, 78, 76, 60, 82, 80, 78, 28, 9000000, 5, false);
        Add("UTA", "Brice", "Sensabaugh", "SF", 22, "USA", 201, 102, 80, 86, 80, 84, 86, 68, 74, 62, 44, 72, 78, 60, 16, 4000000, 4, false);
        Add("UTA", "Cody", "Williams", "SF", 21, "USA", 203, 95, 78, 88, 84, 78, 74, 70, 74, 78, 58, 80, 78, 78, 18, 5000000, 4, false);
        Add("UTA", "Kyle", "Filipowski", "PF", 22, "USA", 211, 110, 82, 86, 74, 82, 78, 72, 76, 80, 78, 80, 82, 70, 82, 6000000, 4, false);
        Add("UTA", "Oscar", "Tshiebwe", "C", 25, "COD", 206, 120, 78, 78, 60, 70, 20, 60, 62, 86, 90, 84, 82, 52, 88, 3000000, 3, false);
        Add("UTA", "Kevin", "Love", "PF", 37, "USA", 203, 113, 78, 76, 64, 82, 82, 78, 82, 74, 72, 74, 82, 66, 26, 8000000, 1, false);
        Add("UTA", "Svi", "Mykhailiuk", "SG", 28, "UKR", 201, 92, 78, 80, 80, 80, 84, 70, 74, 60, 44, 72, 78, 60, 10, 5000000, 2, false);
        Add("UTA", "John", "Konchar", "SG", 30, "USA", 196, 95, 78, 78, 78, 74, 74, 76, 78, 82, 60, 80, 78, 82, 12, 6000000, 2, false);

        // ── WAS ──
        Add("WAS", "Trae", "Young", "PG", 27, "USA", 185, 82, 94, 94, 92, 92, 88, 90, 92, 58, 40, 78, 84, 70, 30, 52000000, 5, false);
        Add("WAS", "D'Angelo", "Russell", "PG", 30, "USA", 193, 88, 84, 86, 82, 86, 86, 82, 84, 60, 44, 74, 80, 68, 18, 25000000, 2, false);
        Add("WAS", "Anthony", "Davis", "PF", 33, "USA", 208, 115, 92, 90, 76, 84, 70, 80, 82, 94, 94, 90, 90, 82, 92, 50000000, 4, false);
        Add("WAS", "Alex", "Sarr", "C", 21, "FRA", 213, 100, 86, 92, 80, 82, 72, 76, 78, 88, 92, 88, 86, 82, 92, 12000000, 5, false);
        Add("WAS", "Bilal", "Coulibaly", "SF", 22, "FRA", 201, 95, 84, 90, 88, 82, 76, 78, 80, 88, 70, 88, 84, 86, 24, 9000000, 4, false);
        Add("WAS", "Cam", "Whitmore", "SF", 22, "USA", 198, 100, 86, 92, 90, 86, 80, 74, 78, 80, 58, 84, 82, 80, 26, 8000000, 4, false);
        Add("WAS", "Jaden", "Hardy", "SG", 23, "USA", 193, 90, 82, 88, 88, 84, 80, 72, 76, 60, 44, 74, 78, 64, 16, 6000000, 3, false);
        Add("WAS", "Tre", "Johnson", "SG", 20, "USA", 196, 88, 82, 90, 86, 82, 84, 70, 74, 62, 46, 76, 78, 68, 18, 5000000, 4, false);
        Add("WAS", "Kyshawn", "George", "SF", 21, "CAN", 203, 94, 80, 88, 84, 80, 82, 74, 78, 78, 58, 80, 78, 80, 20, 5000000, 4, false);
        Add("WAS", "Will", "Riley", "SF", 20, "USA", 201, 92, 78, 88, 86, 78, 80, 72, 76, 74, 54, 78, 76, 76, 18, 4000000, 4, false);
        Add("WAS", "Bub", "Carrington", "PG", 21, "USA", 193, 86, 78, 86, 88, 80, 76, 80, 82, 62, 44, 76, 78, 72, 18, 4000000, 4, false);
        Add("WAS", "Sharife", "Cooper", "PG", 25, "USA", 183, 82, 74, 84, 86, 76, 72, 80, 78, 58, 40, 72, 74, 68, 10, 2000000, 2, false);
        Add("WAS", "Justin", "Champagnie", "SF", 25, "USA", 198, 95, 78, 82, 80, 78, 76, 70, 74, 82, 60, 80, 76, 78, 14, 3000000, 2, false);
        Add("WAS", "Anthony", "Gill", "PF", 32, "USA", 203, 104, 76, 78, 70, 74, 72, 68, 72, 80, 66, 78, 76, 74, 8, 4000000, 1, false);
        Add("WAS", "Tristan", "Vukcevic", "C", 22, "SRB", 213, 108, 80, 86, 72, 82, 78, 70, 74, 76, 78, 78, 80, 68, 82, 5000000, 4, false);

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
                overall = ovr,
                potential = pot,
                speed = spd,
                shooting = sht,
                three_point = thr,
                passing = pas,
                dribbling = drb,
                defense = def,
                rebounding = reb,
                athleticism = ath,
                iq = iq,
                steals = stl,
                blocks = blk,
                salary = sal,
                contract_years = yrs,
                is_rookie = 0,
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
        AddFA("Jalen", "Wallace", "SG", 23, "USA", 196, 92, 76, 84, 84, 76, 74, 74, 76, 74, 52, 76, 74, 74, 10, 2000000, 2);
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

            // 3. Decrement contracts
            p.contract_years -= 1;
            if (p.contract_years <= 0)
            {
                p.contract_years = 0;
                p.team_id = 0;
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
                        _db.Update(signed);
                        freeAgents.Remove(signed);
                        posCounts[signed.position] = posCounts.GetValueOrDefault(signed.position) + 1;
                    }
                }
            }
        }

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