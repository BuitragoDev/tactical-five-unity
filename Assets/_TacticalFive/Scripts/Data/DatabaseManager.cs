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
        SeedStaticDataIfNeeded();

        Debug.Log($"[DB] Save slot {slotNumber} inicializado: {DbPath}");
    }

    void CreateTables()
    {
        _db.CreateTable<TeamData>();
        _db.CreateTable<ManagerData>();
        _db.CreateTable<LeagueSettingsData>();
        _db.CreateTable<GameData>();
        _db.CreateTable<SeasonData>();
        _db.CreateTable<PlayerData>();
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
            new TeamData { name="Boston Celtics",        abbreviation="BOS", city="Boston",        conference="East", division="Atlantic",  arena="TD Garden",               capacity=19156, owner="Wyc Grousbeck",   attack=92, defense=91, overall=92, budget=200_000_000, reputation=5, facilities=5, logo="celtics",   jersey_home="celtics_home",   jersey_away="celtics_away",   salary_margin=-57_000_000, objective="Campeonato" },
            new TeamData { name="Brooklyn Nets",         abbreviation="BKN", city="Brooklyn",      conference="East", division="Atlantic",  arena="Barclays Center",         capacity=17732, owner="Joe Tsai",         attack=74, defense=72, overall=73, budget=140_000_000, reputation=3, facilities=3, logo="nets",      jersey_home="nets_home",      jersey_away="nets_away",      salary_margin=20_000_000,  objective="Play-In" },
            new TeamData { name="New York Knicks",       abbreviation="NYK", city="New York",      conference="East", division="Atlantic",  arena="Madison Square Garden",   capacity=19812, owner="James Dolan",      attack=83, defense=82, overall=83, budget=180_000_000, reputation=4, facilities=5, logo="knicks",    jersey_home="knicks_home",    jersey_away="knicks_away",    salary_margin=-30_000_000, objective="Playoffs" },
            new TeamData { name="Philadelphia 76ers",    abbreviation="PHI", city="Philadelphia",  conference="East", division="Atlantic",  arena="Wells Fargo Center",      capacity=20478, owner="Josh Harris",      attack=80, defense=79, overall=80, budget=170_000_000, reputation=4, facilities=4, logo="sixers",     jersey_home="76ers_home",     jersey_away="76ers_away",     salary_margin=-20_000_000, objective="Playoffs" },
            new TeamData { name="Toronto Raptors",       abbreviation="TOR", city="Toronto",       conference="East", division="Atlantic",  arena="Scotiabank Arena",        capacity=19800, owner="MLSE",             attack=76, defense=75, overall=76, budget=150_000_000, reputation=3, facilities=4, logo="raptors",   jersey_home="raptors_home",   jersey_away="raptors_away",   salary_margin=10_000_000,  objective="Play-In" },

            // ── ESTE — CENTRAL ──
            new TeamData { name="Chicago Bulls",         abbreviation="CHI", city="Chicago",       conference="East", division="Central",   arena="United Center",           capacity=20917, owner="Jerry Reinsdorf",  attack=78, defense=76, overall=77, budget=155_000_000, reputation=4, facilities=4, logo="bulls",     jersey_home="bulls_home",     jersey_away="bulls_away",     salary_margin=5_000_000,   objective="Playoffs" },
            new TeamData { name="Cleveland Cavaliers",   abbreviation="CLE", city="Cleveland",     conference="East", division="Central",   arena="Rocket Mortgage Arena",   capacity=19432, owner="Dan Gilbert",      attack=85, defense=86, overall=86, budget=175_000_000, reputation=4, facilities=4, logo="cavaliers", jersey_home="cavaliers_home", jersey_away="cavaliers_away", salary_margin=-40_000_000, objective="Playoffs" },
            new TeamData { name="Detroit Pistons",       abbreviation="DET", city="Detroit",       conference="East", division="Central",   arena="Little Caesars Arena",    capacity=20491, owner="Tom Gores",        attack=70, defense=69, overall=70, budget=130_000_000, reputation=2, facilities=3, logo="pistons",   jersey_home="pistons_home",   jersey_away="pistons_away",   salary_margin=40_000_000,  objective="Zona tranquila" },
            new TeamData { name="Indiana Pacers",        abbreviation="IND", city="Indianapolis",  conference="East", division="Central",   arena="Gainbridge Fieldhouse",   capacity=17923, owner="Herb Simon",       attack=82, defense=80, overall=81, budget=165_000_000, reputation=3, facilities=3, logo="pacers",    jersey_home="pacers_home",    jersey_away="pacers_away",    salary_margin=-15_000_000, objective="Play-In" },
            new TeamData { name="Milwaukee Bucks",       abbreviation="MIL", city="Milwaukee",     conference="East", division="Central",   arena="Fiserv Forum",            capacity=17341, owner="Marc Lasry",       attack=84, defense=83, overall=84, budget=175_000_000, reputation=4, facilities=4, logo="bucks",     jersey_home="bucks_home",     jersey_away="bucks_away",     salary_margin=-35_000_000, objective="Playoffs" },

            // ── ESTE — SURESTE ──
            new TeamData { name="Atlanta Hawks",         abbreviation="ATL", city="Atlanta",       conference="East", division="Southeast", arena="State Farm Arena",        capacity=18118, owner="Tony Ressler",     attack=79, defense=76, overall=78, budget=155_000_000, reputation=3, facilities=3, logo="hawks",     jersey_home="hawks_home",     jersey_away="hawks_away",     salary_margin=0,           objective="Play-In" },
            new TeamData { name="Charlotte Hornets",     abbreviation="CHA", city="Charlotte",     conference="East", division="Southeast", arena="Spectrum Center",         capacity=19077, owner="Gabe Plotkin",     attack=71, defense=70, overall=71, budget=130_000_000, reputation=2, facilities=3, logo="hornets",   jersey_home="hornets_home",   jersey_away="hornets_away",   salary_margin=35_000_000,  objective="Zona tranquila" },
            new TeamData { name="Miami Heat",            abbreviation="MIA", city="Miami",         conference="East", division="Southeast", arena="Kaseya Center",           capacity=19600, owner="Micky Arison",     attack=81, defense=83, overall=82, budget=170_000_000, reputation=4, facilities=4, logo="heat",      jersey_home="heat_home",      jersey_away="heat_away",      salary_margin=-25_000_000, objective="Playoffs" },
            new TeamData { name="Orlando Magic",         abbreviation="ORL", city="Orlando",       conference="East", division="Southeast", arena="Kia Center",              capacity=18846, owner="DeVos family",     attack=77, defense=79, overall=78, budget=150_000_000, reputation=3, facilities=3, logo="magic",     jersey_home="magic_home",     jersey_away="magic_away",     salary_margin=15_000_000,  objective="Play-In" },
            new TeamData { name="Washington Wizards",    abbreviation="WAS", city="Washington",    conference="East", division="Southeast", arena="Capital One Arena",       capacity=20356, owner="Ted Leonsis",      attack=68, defense=67, overall=68, budget=120_000_000, reputation=2, facilities=3, logo="wizards",   jersey_home="wizards_home",   jersey_away="wizards_away",   salary_margin=50_000_000,  objective="Zona tranquila" },

            // ── OESTE — NOROESTE ──
            new TeamData { name="Denver Nuggets",        abbreviation="DEN", city="Denver",        conference="West", division="Northwest", arena="Ball Arena",              capacity=19520, owner="Stan Kroenke",     attack=88, defense=84, overall=86, budget=185_000_000, reputation=4, facilities=4, logo="nuggets",   jersey_home="nuggets_home",   jersey_away="nuggets_away",   salary_margin=-57_000_000, objective="Playoffs" },
            new TeamData { name="Minnesota Timberwolves",abbreviation="MIN", city="Minneapolis",   conference="West", division="Northwest", arena="Target Center",           capacity=18978, owner="Alex Rodriguez",   attack=83, defense=85, overall=84, budget=170_000_000, reputation=3, facilities=3, logo="wolves",    jersey_home="wolves_home",    jersey_away="wolves_away",    salary_margin=-30_000_000, objective="Play-In" },
            new TeamData { name="Oklahoma City Thunder",  abbreviation="OKC", city="Oklahoma City", conference="West", division="Northwest", arena="Paycom Center",           capacity=18203, owner="Clay Bennett",     attack=86, defense=84, overall=85, budget=175_000_000, reputation=3, facilities=3, logo="thunder",   jersey_home="thunder_home",   jersey_away="thunder_away",   salary_margin=-45_000_000, objective="Play-In" },
            new TeamData { name="Portland Trail Blazers", abbreviation="POR", city="Portland",      conference="West", division="Northwest", arena="Moda Center",             capacity=19393, owner="Jody Allen",       attack=72, defense=71, overall=72, budget=135_000_000, reputation=3, facilities=3, logo="blazers",   jersey_home="blazers_home",   jersey_away="blazers_away",   salary_margin=25_000_000,  objective="Play-In" },
            new TeamData { name="Utah Jazz",              abbreviation="UTA", city="Salt Lake City", conference="West", division="Northwest", arena="Delta Center",            capacity=18306, owner="Ryan Smith",       attack=73, defense=72, overall=73, budget=140_000_000, reputation=3, facilities=3, logo="jazz",      jersey_home="jazz_home",      jersey_away="jazz_away",      salary_margin=20_000_000,  objective="Play-In" },

            // ── OESTE — PACÍFICO ──
            new TeamData { name="Golden State Warriors",  abbreviation="GSW", city="San Francisco", conference="West", division="Pacific",   arena="Chase Center",            capacity=18064, owner="Joe Lacob",        attack=83, defense=80, overall=82, budget=175_000_000, reputation=5, facilities=5, logo="warriors",  jersey_home="warriors_home",  jersey_away="warriors_away",  salary_margin=-40_000_000, objective="Campeonato" },
            new TeamData { name="Los Angeles Clippers",   abbreviation="LAC", city="Los Angeles",   conference="West", division="Pacific",   arena="Intuit Dome",             capacity=18000, owner="Steve Ballmer",    attack=81, defense=82, overall=82, budget=170_000_000, reputation=4, facilities=5, logo="clippers",  jersey_home="clippers_home",  jersey_away="clippers_away",  salary_margin=-25_000_000, objective="Playoffs" },
            new TeamData { name="Los Angeles Lakers",     abbreviation="LAL", city="Los Angeles",   conference="West", division="Pacific",   arena="Crypto.com Arena",        capacity=18997, owner="Jeanie Buss",      attack=82, defense=79, overall=81, budget=175_000_000, reputation=5, facilities=5, logo="lakers",    jersey_home="lakers_home",    jersey_away="lakers_away",    salary_margin=-30_000_000, objective="Campeonato" },
            new TeamData { name="Phoenix Suns",           abbreviation="PHX", city="Phoenix",       conference="West", division="Pacific",   arena="Footprint Center",        capacity=18055, owner="Mat Ishbia",       attack=80, defense=78, overall=79, budget=165_000_000, reputation=3, facilities=4, logo="suns",      jersey_home="suns_home",      jersey_away="suns_away",      salary_margin=-10_000_000, objective="Play-In" },
            new TeamData { name="Sacramento Kings",       abbreviation="SAC", city="Sacramento",    conference="West", division="Pacific",   arena="Golden 1 Center",         capacity=17608, owner="Vivek Ranadivé",   attack=79, defense=77, overall=78, budget=155_000_000, reputation=3, facilities=4, logo="kings",     jersey_home="kings_home",     jersey_away="kings_away",     salary_margin=5_000_000,   objective="Play-In" },

            // ── OESTE — SUROESTE ──
            new TeamData { name="Dallas Mavericks",      abbreviation="DAL", city="Dallas",        conference="West", division="Southwest", arena="American Airlines Center", capacity=19200, owner="Patrick Dumont",   attack=87, defense=83, overall=85, budget=180_000_000, reputation=4, facilities=4, logo="mavericks", jersey_home="mavericks_home", jersey_away="mavericks_away", salary_margin=-50_000_000, objective="Playoffs" },
            new TeamData { name="Houston Rockets",       abbreviation="HOU", city="Houston",       conference="West", division="Southwest", arena="Toyota Center",           capacity=18055, owner="Tilman Fertitta",  attack=75, defense=74, overall=75, budget=145_000_000, reputation=3, facilities=3, logo="rockets",   jersey_home="rockets_home",   jersey_away="rockets_away",   salary_margin=20_000_000,  objective="Play-In" },
            new TeamData { name="Memphis Grizzlies",     abbreviation="MEM", city="Memphis",       conference="West", division="Southwest", arena="FedExForum",              capacity=17794, owner="Robert Pera",      attack=76, defense=78, overall=77, budget=150_000_000, reputation=3, facilities=3, logo="grizzlies", jersey_home="grizzlies_home", jersey_away="grizzlies_away", salary_margin=10_000_000,  objective="Play-In" },
            new TeamData { name="New Orleans Pelicans",  abbreviation="NOP", city="New Orleans",   conference="West", division="Southwest", arena="Smoothie King Center",    capacity=17791, owner="Gayle Benson",     attack=77, defense=76, overall=77, budget=150_000_000, reputation=3, facilities=3, logo="pelicans",  jersey_home="pelicans_home",  jersey_away="pelicans_away",  salary_margin=10_000_000,  objective="Play-In" },
            new TeamData { name="San Antonio Spurs",     abbreviation="SAS", city="San Antonio",   conference="West", division="Southwest", arena="AT&T Center",             capacity=18418, owner="Peter Holt",       attack=71, defense=70, overall=71, budget=130_000_000, reputation=3, facilities=3, logo="spurs",     jersey_home="spurs_home",     jersey_away="spurs_away",     salary_margin=35_000_000,  objective="Play-In" },
        };

        _db.InsertAll(teams);
        Debug.Log($"[DB] {teams.Count} equipos insertados.");
    }

    // ── SEASON ────────────────────────────────────────────
    public SeasonData CreateSeason(int managerId, string gameMode)
    {
        var season = new SeasonData
        {
            year_start = 2025,
            year_end = 2026,
            is_active = 1,
            current_game_day = 0,
            game_mode = gameMode,
            phase = "preseason",
            manager_id = managerId
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
                injury_type = ""
            });
        }

        // ── ATL ──
        Add("ATL", "Jalen", "Johnson", "SF", 23, "USA", 203, 99, 82, 88, 82, 74, 72, 76, 74, 75, 80, 88, 78, 65, 43, 13000000, 3, false);
        Add("ATL", "Onyeka", "Okongwu", "C", 24, "USA", 206, 104, 78, 84, 74, 58, 35, 55, 48, 76, 86, 84, 75, 52, 74, 15000000, 3, false);
        Add("ATL", "Dyson", "Daniels", "PG", 22, "AUS", 196, 93, 78, 84, 82, 62, 52, 68, 66, 78, 56, 72, 68, 89, 31, 3000000, 2, false);
        Add("ATL", "Zaccharie", "Risacher", "SF", 20, "FRA", 203, 90, 78, 78, 76, 80, 82, 74, 78, 68, 55, 68, 80, 67, 26, 13000000, 3, false);
        Add("ATL", "Nickeil", "Alexander-Walker", "SG", 26, "CAN", 196, 90, 76, 76, 82, 72, 64, 66, 68, 72, 58, 72, 68, 71, 37, 8000000, 2, false);
        Add("ATL", "Gabe", "Vincent", "PG", 28, "USA", 193, 88, 72, 70, 78, 68, 62, 68, 68, 72, 54, 64, 70, 66, 26, 11000000, 3, false);
        Add("ATL", "Mouhamed", "Gueye", "PF", 21, "SEN", 208, 100, 68, 80, 72, 50, 32, 46, 42, 70, 78, 82, 66, 54, 54, 2000000, 3, false);
        Add("ATL", "Corey", "Kispert", "SF", 26, "USA", 201, 97, 68, 70, 68, 68, 70, 52, 54, 56, 52, 56, 58, 57, 36, 8000000, 3, false);
        Add("ATL", "Jonathan", "Kuminga", "SF", 22, "COD", 201, 99, 80, 90, 88, 76, 64, 64, 66, 72, 70, 88, 74, 67, 43, 7000000, 2, false);
        Add("ATL", "Christian", "Koloko", "C", 24, "CMR", 213, 104, 62, 70, 66, 44, 26, 44, 38, 56, 66, 66, 52, 48, 71, 2000000, 2, false);
        Add("ATL", "Caleb", "Houstan", "SF", 22, "CAN", 201, 97, 68, 78, 72, 64, 66, 54, 56, 58, 54, 62, 60, 63, 39, 2000000, 3, false);

        // ── BOS ──
        Add("BOS", "Jayson", "Tatum", "SF", 26, "USA", 203, 95, 95, 96, 86, 92, 88, 80, 82, 84, 72, 88, 92, 70, 44, 32600000, 4, false);
        Add("BOS", "Jaylen", "Brown", "SG", 28, "USA", 198, 99, 90, 92, 90, 88, 82, 76, 80, 86, 72, 92, 86, 74, 41, 49000000, 5, false);
        Add("BOS", "Kristaps", "Porzingis", "C", 29, "LAT", 221, 108, 85, 84, 76, 82, 80, 68, 62, 76, 74, 78, 80, 50, 73, 30000000, 2, false);
        Add("BOS", "Jrue", "Holiday", "PG", 34, "USA", 193, 95, 84, 80, 86, 78, 68, 80, 80, 84, 62, 84, 86, 74, 37, 36000000, 2, false);
        Add("BOS", "Al", "Horford", "C", 38, "DOM", 206, 108, 78, 72, 68, 72, 66, 70, 62, 76, 78, 68, 80, 56, 73, 26000000, 2, false);
        Add("BOS", "Payton", "Pritchard", "PG", 27, "USA", 185, 82, 79, 80, 82, 80, 82, 72, 78, 72, 52, 68, 76, 72, 28, 14000000, 3, false);
        Add("BOS", "Derrick", "White", "PG", 30, "USA", 196, 90, 82, 80, 84, 76, 72, 74, 76, 80, 60, 78, 82, 74, 35, 22000000, 3, false);
        Add("BOS", "Sam", "Hauser", "SF", 26, "USA", 203, 97, 76, 76, 74, 82, 86, 62, 66, 62, 56, 64, 70, 62, 28, 8000000, 3, false);
        Add("BOS", "Luke", "Kornet", "C", 29, "USA", 216, 113, 72, 70, 64, 64, 52, 58, 52, 62, 76, 66, 72, 45, 74, 3000000, 2, false);
        Add("BOS", "Neemias", "Queta", "C", 25, "POR", 216, 108, 70, 74, 68, 54, 32, 50, 44, 62, 76, 72, 66, 48, 72, 2000000, 2, false);
        Add("BOS", "Jordan", "Walsh", "SF", 22, "USA", 201, 97, 70, 80, 78, 62, 54, 58, 58, 70, 60, 78, 66, 62, 37, 2000000, 2, false);

        // ── BKN ──
        Add("BKN", "Cam", "Thomas", "SG", 22, "USA", 196, 90, 84, 92, 82, 88, 78, 72, 80, 72, 62, 80, 82, 63, 29, 8000000, 3, false);
        Add("BKN", "Nic", "Claxton", "C", 25, "USA", 213, 104, 82, 86, 76, 60, 34, 60, 50, 78, 88, 80, 74, 55, 77, 19000000, 3, false);
        Add("BKN", "Ben", "Simmons", "PG", 28, "AUS", 208, 100, 72, 72, 82, 44, 22, 78, 72, 78, 60, 88, 72, 70, 42, 37900000, 2, false);
        Add("BKN", "Day'Ron", "Simmons", "C", 23, "USA", 211, 118, 72, 80, 70, 52, 28, 50, 44, 66, 80, 74, 66, 48, 73, 8000000, 3, false);
        Add("BKN", "Cam", "Johnson", "SF", 28, "USA", 203, 95, 82, 84, 80, 82, 80, 68, 70, 70, 64, 74, 76, 66, 42, 22000000, 3, false);
        Add("BKN", "Trendon", "Watford", "PF", 24, "USA", 203, 99, 72, 74, 76, 68, 54, 64, 60, 68, 64, 72, 68, 55, 47, 5000000, 2, false);
        Add("BKN", "Noah", "Clowney", "SF", 21, "USA", 206, 97, 70, 82, 78, 58, 48, 56, 56, 68, 62, 80, 64, 62, 40, 3000000, 3, false);
        Add("BKN", "Keon", "Johnson", "SG", 23, "USA", 196, 90, 68, 78, 84, 64, 54, 58, 60, 68, 54, 78, 64, 64, 30, 2000000, 2, false);
        Add("BKN", "Tosan", "Evbuomwan", "SF", 23, "GBR", 201, 95, 68, 76, 72, 62, 56, 60, 58, 64, 58, 68, 64, 56, 38, 2000000, 3, false);
        Add("BKN", "Tyrese", "Martin", "SF", 24, "USA", 201, 95, 70, 72, 74, 64, 56, 58, 56, 66, 56, 70, 62, 58, 38, 2000000, 2, false);
        Add("BKN", "Dennis", "Schroder", "PG", 31, "GER", 188, 82, 78, 76, 84, 74, 66, 80, 82, 72, 56, 78, 76, 72, 30, 4000000, 1, false);

        // ── CHA ──
        Add("CHA", "LaMelo", "Ball", "PG", 23, "USA", 201, 88, 88, 94, 82, 82, 80, 90, 86, 70, 60, 78, 88, 73, 27, 32000000, 4, false);
        Add("CHA", "Miles", "Bridges", "PF", 27, "USA", 201, 99, 82, 84, 86, 78, 70, 68, 72, 72, 72, 88, 78, 64, 61, 25000000, 3, false);
        Add("CHA", "Brandon", "Miller", "SF", 22, "USA", 203, 95, 80, 90, 82, 80, 78, 68, 74, 70, 66, 82, 78, 61, 44, 9500000, 3, false);
        Add("CHA", "Coby", "White", "PG", 24, "USA", 193, 88, 81, 86, 84, 82, 78, 78, 82, 70, 58, 78, 78, 73, 29, 14000000, 3, false);
        Add("CHA", "Grant", "Williams", "PF", 26, "USA", 201, 99, 74, 74, 74, 72, 74, 68, 66, 76, 68, 70, 76, 67, 53, 14000000, 3, false);
        Add("CHA", "Josh", "Green", "SF", 24, "AUS", 196, 93, 74, 80, 86, 68, 60, 66, 66, 78, 64, 84, 70, 74, 40, 4000000, 2, false);
        Add("CHA", "Tre", "Mann", "SG", 24, "USA", 196, 90, 75, 80, 80, 76, 72, 70, 76, 66, 56, 74, 72, 67, 35, 5500000, 2, false);
        Add("CHA", "Tidjane", "Salaün", "PF", 20, "FRA", 208, 93, 78, 78, 78, 76, 66, 60, 62, 66, 66, 72, 72, 56, 59, 7000000, 4, false);
        Add("CHA", "Moussa", "Diabate", "C", 22, "FRA", 211, 104, 66, 78, 70, 50, 30, 46, 42, 60, 74, 82, 60, 52, 71, 10000000, 4, false);
        Add("CHA", "Kon", "Knueppel", "SF", 20, "USA", 198, 97, 80, 90, 82, 82, 95, 70, 76, 70, 60, 78, 78, 73, 27, 10200000, 4, true);
        Add("CHA", "Ryan", "Kalkbrenner", "C", 24, "USA", 216, 116, 70, 68, 68, 58, 34, 52, 46, 64, 76, 72, 68, 45, 73, 2000000, 4, true);
        Add("CHA", "Pat", "Connaughton", "SG", 33, "USA", 196, 94, 72, 82, 76, 72, 72, 60, 64, 64, 56, 70, 68, 64, 27, 2000000, 1, false);

        // ── CHI ──
        Add("CHI", "Coby", "White", "PG", 24, "USA", 193, 88, 81, 86, 84, 82, 78, 78, 82, 70, 58, 78, 78, 73, 29, 14000000, 3, false);
        Add("CHI", "Nikola", "Vucevic", "C", 33, "MNE", 213, 118, 80, 74, 64, 74, 62, 68, 58, 68, 84, 66, 76, 46, 69, 18000000, 2, false);
        Add("CHI", "Josh", "Giddey", "PG", 22, "AUS", 203, 99, 78, 88, 78, 68, 62, 82, 76, 64, 60, 74, 74, 63, 27, 6200000, 2, false);
        Add("CHI", "Patrick", "Williams", "PF", 23, "USA", 203, 99, 78, 86, 82, 72, 68, 66, 68, 76, 72, 84, 74, 67, 61, 10500000, 3, false);
        Add("CHI", "Collin", "Sexton", "PG", 26, "USA", 185, 82, 78, 78, 84, 78, 66, 68, 72, 64, 54, 68, 68, 63, 27, 17900000, 3, false);
        Add("CHI", "Anfernee", "Simons", "PG", 25, "USA", 193, 88, 82, 84, 82, 84, 80, 72, 78, 68, 58, 72, 76, 75, 23, 24300000, 3, false);
        Add("CHI", "Nick", "Richards", "C", 27, "JAM", 213, 113, 72, 72, 66, 52, 30, 48, 44, 64, 80, 76, 68, 48, 71, 8000000, 2, false);
        Add("CHI", "Matas", "Buzelis", "PF", 20, "LTU", 203, 94, 80, 89, 80, 89, 70, 66, 68, 74, 72, 76, 76, 55, 61, 5300000, 3, false);
        Add("CHI", "Torrey", "Craig", "SF", 33, "USA", 201, 95, 70, 68, 76, 66, 56, 58, 58, 72, 64, 74, 68, 61, 42, 2000000, 1, false);
        Add("CHI", "Tre", "Jones", "PG", 25, "USA", 185, 82, 70, 74, 76, 62, 50, 74, 72, 70, 52, 58, 70, 66, 28, 5900000, 2, false);
        Add("CHI", "Rob", "Dillingham", "PG", 20, "USA", 185, 82, 70, 88, 84, 70, 64, 68, 70, 60, 50, 72, 66, 66, 22, 6000000, 4, false);
        Add("CHI", "Adama", "Sanogo", "C", 23, "MLI", 211, 113, 66, 76, 66, 50, 30, 44, 42, 60, 76, 80, 60, 52, 71, 2000000, 2, false);

        // ── CLE ──
        Add("CLE", "Donovan", "Mitchell", "SG", 28, "USA", 185, 86, 91, 92, 90, 90, 84, 80, 86, 78, 64, 88, 88, 67, 33, 35400000, 4, false);
        Add("CLE", "Evan", "Mobley", "C", 23, "USA", 213, 104, 88, 90, 82, 70, 58, 68, 58, 82, 88, 86, 84, 61, 80, 10000000, 2, false);
        Add("CLE", "Jarrett", "Allen", "C", 26, "USA", 211, 104, 83, 83, 78, 60, 36, 58, 50, 78, 88, 84, 78, 61, 77, 20000000, 3, false);
        Add("CLE", "James", "Harden", "SG", 35, "USA", 196, 99, 88, 82, 78, 88, 82, 88, 88, 72, 62, 72, 88, 72, 30, 35600000, 2, false);
        Add("CLE", "Max", "Strus", "SG", 28, "USA", 196, 90, 76, 75, 78, 78, 80, 64, 68, 70, 60, 72, 74, 72, 28, 15000000, 4, false);
        Add("CLE", "Dean", "Wade", "PF", 29, "USA", 206, 103, 81, 80, 88, 80, 72, 66, 70, 78, 66, 88, 76, 74, 45, 6000000, 2, false);
        Add("CLE", "Dennis", "Schroder", "PG", 31, "GER", 188, 82, 79, 76, 84, 74, 66, 80, 82, 72, 56, 78, 76, 72, 30, 13000000, 1, false);
        Add("CLE", "Keon", "Ellis", "SG", 26, "USA", 193, 79, 78, 82, 82, 76, 74, 70, 72, 72, 58, 72, 72, 70, 31, 2600000, 1, false);
        Add("CLE", "Sam", "Merrill", "SG", 28, "USA", 196, 90, 72, 70, 70, 78, 82, 60, 64, 64, 52, 60, 70, 64, 27, 4000000, 2, false);
        Add("CLE", "Thomas", "Bryant", "C", 27, "USA", 211, 113, 72, 72, 68, 62, 48, 54, 46, 60, 76, 68, 66, 48, 70, 3000000, 2, false);
        Add("CLE", "Craig", "Porter Jr.", "PG", 24, "USA", 193, 86, 70, 78, 76, 66, 58, 72, 72, 70, 56, 68, 72, 66, 28, 2000000, 2, false);
        Add("CLE", "Larry", "Nance Jr.", "PF", 32, "USA", 203, 102, 72, 68, 74, 60, 46, 62, 56, 68, 68, 72, 68, 55, 60, 11000000, 2, false);

        // ── DAL ──
        Add("DAL", "Kyrie", "Irving", "PG", 32, "USA", 188, 88, 86, 89, 90, 90, 82, 88, 90, 74, 62, 84, 88, 68, 25, 40000000, 3, false);
        Add("DAL", "Luka", "Doncic", "PG", 25, "SVN", 201, 104, 96, 97, 82, 88, 84, 94, 90, 70, 72, 84, 90, 66, 35, 43000000, 4, false);
        Add("DAL", "Klay", "Thompson", "SG", 34, "USA", 198, 99, 80, 85, 80, 88, 88, 66, 70, 74, 62, 72, 84, 64, 37, 43200000, 1, false);
        Add("DAL", "Cooper", "Flagg", "SF", 19, "USA", 206, 92, 80, 98, 86, 88, 74, 84, 84, 82, 72, 82, 89, 75, 75, 13000000, 4, true);
        Add("DAL", "P.J.", "Washington", "PF", 26, "USA", 201, 99, 80, 80, 78, 76, 70, 66, 68, 74, 72, 76, 76, 55, 61, 15000000, 3, false);
        Add("DAL", "Daniel", "Gafford", "C", 26, "USA", 213, 104, 78, 80, 80, 58, 34, 52, 46, 74, 82, 88, 72, 55, 79, 21000000, 3, false);
        Add("DAL", "Dereck", "Lively II", "C", 21, "USA", 216, 104, 76, 90, 78, 58, 34, 58, 48, 74, 82, 84, 74, 60, 80, 5000000, 3, false);
        Add("DAL", "Khris", "Middleton", "SF", 33, "USA", 201, 102, 86, 82, 80, 86, 74, 74, 74, 72, 66, 74, 82, 71, 39, 40000000, 1, false);
        Add("DAL", "Caleb", "Martin", "SF", 29, "USA", 198, 97, 74, 73, 80, 68, 60, 62, 60, 76, 64, 78, 70, 61, 43, 9000000, 2, false);
        Add("DAL", "Naji", "Marshall", "SF", 29, "USA", 198, 99, 76, 79, 80, 78, 60, 62, 60, 76, 64, 78, 70, 61, 43, 9000000, 2, false);
        Add("DAL", "Dwight", "Powell", "C", 33, "CAN", 211, 113, 70, 68, 68, 58, 34, 52, 46, 64, 76, 72, 68, 45, 73, 8000000, 1, false);
        Add("DAL", "Marvin", "Bagley III", "C", 26, "USA", 208, 102, 72, 70, 72, 62, 38, 52, 46, 56, 70, 68, 58, 51, 72, 6400000, 2, false);

        // ── DEN ──
        Add("DEN", "Nikola", "Jokic", "C", 29, "SRB", 211, 129, 98, 98, 78, 84, 74, 96, 84, 74, 96, 72, 82, 50, 76, 51900000, 4, false);
        Add("DEN", "Jamal", "Murray", "PG", 27, "CAN", 193, 95, 88, 90, 88, 88, 82, 84, 86, 80, 66, 84, 88, 78, 31, 30000000, 3, false);
        Add("DEN", "Aaron", "Gordon", "PF", 29, "USA", 203, 100, 83, 82, 88, 76, 64, 70, 68, 82, 76, 90, 80, 66, 58, 21000000, 3, false);
        Add("DEN", "Cameron", "Johnson", "SF", 30, "USA", 203, 95, 86, 88, 82, 88, 84, 68, 72, 70, 70, 78, 80, 68, 44, 22000000, 2, false);
        Add("DEN", "Tim", "Hardaway Jr.", "SG", 32, "USA", 196, 90, 76, 73, 80, 78, 74, 62, 68, 66, 54, 74, 70, 69, 27, 18000000, 2, false);
        Add("DEN", "Christian", "Braun", "SG", 24, "USA", 198, 99, 82, 88, 80, 86, 74, 74, 74, 72, 66, 74, 82, 71, 69, 9600000, 3, false);
        Add("DEN", "Jonas", "Valanciunas", "C", 32, "LTU", 211, 120, 78, 73, 64, 68, 48, 62, 52, 64, 84, 68, 76, 48, 69, 14000000, 1, false);
        Add("DEN", "Bruce", "Brown", "SF", 28, "USA", 196, 97, 72, 70, 80, 64, 52, 64, 60, 72, 58, 70, 66, 59, 36, 23000000, 2, false);
        Add("DEN", "Tyus", "Jones", "PG", 28, "USA", 185, 82, 74, 72, 76, 68, 58, 78, 76, 68, 52, 56, 72, 64, 29, 14600000, 3, false);
        Add("DEN", "Peyton", "Watson", "SF", 22, "USA", 206, 99, 70, 84, 82, 62, 54, 56, 56, 70, 64, 84, 64, 62, 40, 3000000, 3, false);
        Add("DEN", "Julian", "Strawther", "SG", 23, "USA", 198, 93, 72, 82, 76, 72, 72, 60, 64, 64, 56, 70, 68, 64, 27, 3500000, 3, false);
        Add("DEN", "Zeke", "Nnaji", "PF", 24, "USA", 208, 113, 70, 74, 70, 56, 36, 52, 46, 62, 74, 76, 66, 51, 55, 2000000, 2, false);

        // ── DET ──
        Add("DET", "Cade", "Cunningham", "PG", 23, "USA", 201, 97, 88, 92, 82, 84, 78, 88, 86, 76, 64, 78, 88, 70, 33, 27000000, 4, false);
        Add("DET", "Jalen", "Duren", "C", 21, "USA", 213, 118, 80, 90, 80, 58, 32, 54, 46, 72, 88, 88, 74, 53, 74, 7000000, 3, false);
        Add("DET", "Isaiah", "Stewart", "PF", 23, "USA", 206, 113, 76, 82, 76, 60, 40, 58, 52, 76, 78, 78, 72, 64, 55, 18000000, 4, false);
        Add("DET", "Tobias", "Harris", "PF", 32, "USA", 203, 106, 78, 74, 80, 76, 64, 64, 62, 66, 68, 68, 72, 56, 59, 39700000, 1, false);
        Add("DET", "Kevin", "Huerter", "SG", 26, "USA", 201, 93, 78, 76, 76, 78, 78, 68, 70, 66, 60, 62, 70, 62, 34, 17000000, 3, false);
        Add("DET", "Ausar", "Thompson", "SF", 22, "USA", 198, 97, 72, 86, 86, 64, 52, 58, 58, 76, 66, 90, 68, 63, 40, 7000000, 3, false);
        Add("DET", "Caris", "LeVert", "SG", 30, "USA", 196, 90, 78, 76, 82, 78, 70, 72, 76, 70, 60, 76, 74, 71, 27, 14000000, 2, false);
        Add("DET", "Duncan", "Robinson", "SG", 30, "USA", 201, 95, 74, 72, 70, 80, 88, 58, 62, 64, 56, 62, 70, 60, 32, 18000000, 3, false);
        Add("DET", "Ron", "Holland", "SF", 20, "USA", 203, 99, 68, 88, 82, 58, 50, 54, 56, 66, 62, 86, 62, 64, 38, 4000000, 4, false);
        Add("DET", "Marcus", "Sasser", "PG", 23, "USA", 185, 82, 68, 78, 78, 68, 62, 66, 68, 64, 50, 66, 66, 70, 22, 2000000, 2, false);
        Add("DET", "Paul", "Reed", "PF", 26, "USA", 206, 95, 74, 78, 74, 76, 64, 64, 62, 66, 68, 68, 72, 56, 59, 5700000, 1, false);
        Add("DET", "Javonte", "Green", "SF", 30, "USA", 196, 93, 64, 62, 78, 54, 40, 50, 48, 64, 54, 76, 56, 56, 40, 2000000, 1, false);

        // ── GSW ──
        Add("GSW", "Stephen", "Curry", "PG", 36, "USA", 188, 84, 96, 95, 88, 96, 96, 84, 88, 76, 62, 78, 96, 72, 27, 51900000, 2, false);
        Add("GSW", "Draymond", "Green", "PF", 34, "USA", 198, 103, 80, 76, 78, 64, 44, 84, 72, 86, 64, 78, 86, 70, 53, 22300000, 3, false);
        Add("GSW", "Buddy", "Hield", "SG", 32, "BAH", 196, 93, 80, 76, 80, 84, 88, 66, 72, 68, 58, 68, 76, 64, 34, 21000000, 2, false);
        Add("GSW", "Brandin", "Podziemski", "PG", 22, "USA", 193, 90, 80, 88, 82, 78, 74, 72, 76, 72, 62, 76, 76, 72, 32, 8000000, 3, false);
        Add("GSW", "Jonathan", "Kuminga", "SF", 22, "COD", 201, 99, 80, 90, 88, 76, 64, 64, 66, 72, 70, 88, 74, 67, 43, 7000000, 2, false);
        Add("GSW", "Andrew", "Wiggins", "SF", 29, "CAN", 201, 97, 80, 78, 84, 80, 72, 66, 70, 76, 68, 84, 76, 66, 46, 24300000, 3, false);
        Add("GSW", "Kevon", "Looney", "C", 28, "USA", 206, 104, 72, 70, 66, 48, 28, 54, 46, 66, 78, 68, 70, 43, 67, 7500000, 2, false);
        Add("GSW", "Kyle", "Anderson", "PF", 31, "USA", 208, 113, 74, 70, 66, 68, 58, 72, 66, 68, 60, 68, 72, 58, 47, 10000000, 2, false);
        Add("GSW", "Moses", "Moody", "SG", 23, "USA", 196, 93, 72, 80, 78, 70, 66, 62, 64, 68, 58, 74, 68, 64, 34, 3800000, 2, false);
        Add("GSW", "Trayce", "Jackson-Davis", "C", 24, "USA", 208, 109, 72, 80, 72, 54, 28, 52, 44, 66, 74, 76, 66, 50, 67, 2300000, 2, false);
        Add("GSW", "Gui", "Santos", "SF", 23, "BRA", 201, 95, 70, 78, 74, 68, 62, 60, 62, 68, 58, 68, 66, 58, 38, 2000000, 3, false);

        // ── HOU ──
        Add("HOU", "Alperen", "Sengun", "C", 22, "TUR", 211, 118, 87, 92, 72, 76, 52, 76, 64, 74, 90, 78, 80, 56, 74, 30000000, 4, false);
        Add("HOU", "Jalen", "Green", "SG", 22, "USA", 193, 88, 86, 92, 90, 88, 80, 72, 82, 76, 62, 86, 84, 70, 33, 25200000, 4, false);
        Add("HOU", "Amen", "Thompson", "SF", 22, "USA", 198, 95, 78, 90, 90, 62, 50, 62, 60, 76, 64, 96, 70, 72, 42, 7000000, 3, false);
        Add("HOU", "Fred", "VanVleet", "PG", 30, "USA", 185, 82, 80, 78, 82, 78, 72, 82, 82, 76, 56, 72, 80, 78, 30, 42800000, 3, false);
        Add("HOU", "Jabari", "Smith Jr.", "PF", 22, "USA", 208, 102, 78, 88, 80, 70, 66, 64, 62, 76, 74, 82, 72, 62, 59, 9500000, 3, false);
        Add("HOU", "Steven", "Adams", "C", 31, "NZL", 213, 127, 74, 70, 64, 52, 26, 52, 46, 68, 80, 74, 72, 40, 65, 13200000, 2, false);
        Add("HOU", "Tari", "Eason", "PF", 23, "USA", 203, 102, 74, 84, 82, 68, 54, 60, 58, 74, 68, 88, 68, 68, 58, 4000000, 3, false);
        Add("HOU", "Aaron", "Holiday", "PG", 28, "USA", 185, 82, 72, 72, 78, 70, 64, 72, 72, 68, 52, 62, 70, 62, 26, 4000000, 2, false);
        Add("HOU", "Cam", "Whitmore", "SF", 21, "USA", 196, 97, 74, 86, 82, 74, 64, 62, 62, 74, 62, 86, 68, 68, 40, 4000000, 3, false);
        Add("HOU", "Dillon", "Brooks", "SF", 28, "CAN", 196, 99, 76, 74, 82, 76, 62, 60, 62, 76, 62, 78, 70, 68, 38, 19800000, 3, false);
        Add("HOU", "Jock", "Landale", "C", 29, "AUS", 213, 118, 70, 68, 64, 60, 44, 56, 48, 62, 74, 68, 68, 42, 66, 4000000, 2, false);

        // ── IND ──
        Add("IND", "Tyrese", "Haliburton", "PG", 24, "USA", 196, 90, 90, 94, 86, 84, 80, 94, 88, 74, 62, 82, 90, 80, 29, 22300000, 4, false);
        Add("IND", "Pascal", "Siakam", "PF", 30, "CMR", 206, 104, 88, 86, 84, 82, 64, 78, 74, 80, 74, 84, 82, 66, 55, 37900000, 3, false);
        Add("IND", "Myles", "Turner", "C", 28, "USA", 211, 111, 82, 80, 72, 70, 58, 60, 54, 76, 88, 72, 78, 48, 80, 19900000, 3, false);
        Add("IND", "Andrew", "Nembhard", "PG", 25, "CAN", 193, 88, 78, 82, 82, 72, 66, 78, 76, 74, 58, 72, 76, 70, 30, 9500000, 3, false);
        Add("IND", "Obi", "Toppin", "PF", 26, "USA", 206, 97, 78, 80, 84, 72, 60, 64, 60, 72, 66, 88, 68, 56, 59, 9300000, 3, false);
        Add("IND", "Bennedict", "Mathurin", "SG", 22, "CAN", 198, 99, 80, 88, 84, 80, 74, 66, 72, 70, 64, 80, 74, 67, 37, 8000000, 3, false);
        Add("IND", "Isaiah", "Jackson", "C", 23, "USA", 211, 104, 72, 84, 74, 52, 32, 52, 44, 68, 78, 82, 66, 50, 72, 4000000, 3, false);
        Add("IND", "Aaron", "Nesmith", "SF", 25, "USA", 198, 95, 74, 76, 80, 70, 62, 62, 60, 72, 62, 80, 68, 62, 43, 18500000, 4, false);
        Add("IND", "T.J.", "McConnell", "PG", 32, "USA", 188, 82, 74, 70, 78, 66, 54, 76, 74, 70, 52, 58, 72, 68, 30, 14000000, 3, false);
        Add("IND", "James", "Wiseman", "C", 23, "USA", 213, 113, 72, 80, 72, 54, 30, 50, 44, 64, 76, 78, 64, 46, 72, 3500000, 2, false);
        Add("IND", "Ben", "Sheppard", "SG", 23, "USA", 198, 93, 74, 80, 76, 72, 72, 62, 64, 66, 58, 68, 68, 62, 32, 4000000, 3, false);

        // ── LAC ──
        Add("LAC", "Kawhi", "Leonard", "SF", 33, "USA", 196, 102, 90, 86, 84, 86, 72, 76, 74, 84, 68, 86, 88, 70, 43, 51400000, 2, false);
        Add("LAC", "James", "Harden", "SG", 35, "USA", 196, 99, 88, 82, 78, 88, 82, 88, 88, 72, 62, 72, 88, 72, 30, 35600000, 2, false);
        Add("LAC", "Ivica", "Zubac", "C", 27, "CRO", 216, 120, 80, 78, 64, 62, 32, 60, 52, 72, 88, 74, 76, 48, 76, 16600000, 3, false);
        Add("LAC", "Norman", "Powell", "SG", 31, "USA", 193, 90, 82, 78, 84, 82, 74, 66, 70, 74, 64, 76, 78, 66, 36, 17800000, 3, false);
        Add("LAC", "PJ", "Tucker", "PF", 39, "USA", 198, 99, 68, 62, 76, 62, 56, 58, 54, 72, 58, 68, 66, 58, 43, 11000000, 1, false);
        Add("LAC", "Terance", "Mann", "SF", 28, "USA", 198, 95, 74, 74, 82, 68, 58, 62, 60, 74, 62, 80, 70, 71, 44, 12000000, 3, false);
        Add("LAC", "Kobe", "Brown", "PF", 24, "USA", 203, 102, 72, 78, 76, 68, 58, 62, 58, 68, 66, 74, 68, 58, 50, 4000000, 3, false);
        Add("LAC", "Amir", "Coffey", "SG", 28, "CAN", 198, 95, 72, 70, 76, 72, 64, 60, 62, 68, 56, 70, 66, 62, 34, 4000000, 2, false);
        Add("LAC", "Kevin", "Porter Jr.", "PG", 24, "USA", 196, 90, 74, 78, 80, 72, 66, 72, 72, 70, 58, 72, 68, 66, 30, 5000000, 2, false);
        Add("LAC", "Bones", "Hyland", "PG", 24, "USA", 188, 82, 74, 78, 82, 74, 70, 72, 76, 66, 54, 68, 70, 66, 27, 5000000, 2, false);
        Add("LAC", "Derrick", "Jones Jr.", "SF", 28, "USA", 198, 97, 72, 70, 84, 62, 50, 58, 56, 72, 60, 88, 64, 62, 42, 9000000, 2, false);

        // ── LAL ──
        Add("LAL", "LeBron", "James", "SF", 40, "USA", 206, 113, 88, 82, 80, 82, 72, 88, 82, 76, 62, 82, 90, 68, 40, 51400000, 2, false);
        Add("LAL", "Anthony", "Davis", "PF", 31, "USA", 208, 115, 92, 90, 82, 76, 54, 70, 64, 84, 90, 84, 86, 60, 80, 43200000, 3, false);
        Add("LAL", "Austin", "Reaves", "PG", 26, "USA", 196, 93, 82, 84, 84, 80, 76, 76, 78, 74, 60, 76, 80, 68, 34, 13000000, 3, false);
        Add("LAL", "Rui", "Hachimura", "PF", 26, "JPN", 208, 102, 78, 78, 78, 78, 68, 64, 64, 68, 66, 76, 72, 58, 52, 17000000, 3, false);
        Add("LAL", "Dorian", "Finney-Smith", "SF", 31, "USA", 201, 97, 74, 72, 78, 70, 66, 62, 60, 72, 62, 72, 68, 62, 38, 13000000, 3, false);
        Add("LAL", "Max", "Christie", "SG", 22, "USA", 196, 93, 74, 82, 80, 72, 68, 62, 64, 68, 60, 74, 68, 64, 33, 4000000, 3, false);
        Add("LAL", "Gabe", "Vincent", "PG", 28, "USA", 193, 88, 72, 70, 78, 68, 62, 68, 68, 72, 54, 64, 70, 66, 26, 11000000, 3, false);
        Add("LAL", "Jaxson", "Hayes", "C", 25, "USA", 213, 104, 74, 76, 76, 54, 30, 52, 44, 66, 76, 82, 66, 48, 72, 4500000, 2, false);
        Add("LAL", "Christian", "Wood", "PF", 29, "USA", 208, 104, 76, 74, 74, 74, 64, 60, 54, 62, 76, 72, 70, 50, 63, 13000000, 2, false);
        Add("LAL", "Bronny", "James", "PG", 20, "USA", 188, 86, 72, 82, 80, 68, 62, 66, 66, 64, 52, 70, 66, 62, 28, 7900000, 3, true);
        Add("LAL", "Dalton", "Knecht", "SG", 23, "USA", 196, 93, 76, 82, 76, 78, 78, 62, 66, 64, 58, 68, 68, 64, 32, 5000000, 4, true);

        // ── MEM ──
        Add("MEM", "Ja", "Morant", "PG", 25, "USA", 185, 79, 88, 92, 96, 80, 68, 82, 88, 72, 62, 92, 82, 64, 33, 33400000, 4, false);
        Add("MEM", "Jaren", "Jackson Jr.", "C", 25, "USA", 211, 108, 88, 90, 76, 72, 60, 66, 58, 80, 92, 78, 82, 52, 84, 30000000, 4, false);
        Add("MEM", "Desmond", "Bane", "SG", 26, "USA", 196, 93, 84, 86, 82, 84, 82, 72, 74, 72, 64, 76, 80, 68, 33, 22800000, 4, false);
        Add("MEM", "Brandon", "Clarke", "PF", 28, "CAN", 206, 95, 78, 76, 80, 66, 48, 60, 56, 72, 70, 82, 72, 56, 68, 7000000, 2, false);
        Add("MEM", "Vince", "Williams Jr.", "SF", 23, "USA", 196, 95, 72, 80, 78, 68, 62, 62, 62, 70, 60, 76, 66, 64, 36, 3000000, 3, false);
        Add("MEM", "GG", "Jackson", "PF", 21, "USA", 203, 93, 74, 84, 78, 68, 60, 60, 58, 70, 66, 78, 68, 56, 50, 4000000, 3, false);
        Add("MEM", "Scotty", "Pippen Jr.", "PG", 24, "USA", 185, 79, 72, 78, 80, 68, 60, 72, 72, 66, 52, 62, 68, 66, 24, 5000000, 3, false);
        Add("MEM", "Zach", "Edey", "C", 22, "CAN", 224, 134, 74, 82, 60, 54, 28, 50, 42, 64, 82, 68, 70, 44, 73, 3500000, 3, true);
        Add("MEM", "Cam", "Spencer", "SG", 24, "USA", 193, 86, 72, 76, 76, 72, 72, 62, 66, 64, 56, 66, 68, 62, 28, 2000000, 2, false);
        Add("MEM", "John", "Konchar", "SF", 28, "USA", 198, 97, 70, 68, 74, 66, 56, 60, 58, 64, 56, 68, 64, 59, 35, 7000000, 3, false);
        Add("MEM", "Luke", "Kennard", "SG", 28, "USA", 196, 88, 72, 70, 72, 78, 78, 58, 62, 62, 52, 60, 66, 60, 27, 14000000, 3, false);

        // ── MIA ──
        Add("MIA", "Bam", "Adebayo", "C", 27, "USA", 206, 116, 88, 90, 84, 70, 44, 76, 66, 86, 88, 86, 84, 60, 74, 32600000, 4, false);
        Add("MIA", "Tyler", "Herro", "SG", 24, "USA", 196, 90, 84, 88, 84, 88, 82, 74, 80, 72, 60, 78, 84, 66, 33, 29000000, 4, false);
        Add("MIA", "Jimmy", "Butler", "SF", 35, "USA", 201, 104, 86, 80, 82, 82, 64, 74, 72, 82, 66, 82, 84, 68, 43, 48800000, 2, false);
        Add("MIA", "Terry", "Rozier", "PG", 30, "USA", 188, 82, 80, 78, 84, 80, 72, 72, 76, 72, 60, 72, 76, 64, 36, 25900000, 3, false);
        Add("MIA", "Duncan", "Robinson", "SG", 30, "USA", 201, 95, 74, 72, 70, 80, 88, 58, 62, 64, 56, 62, 70, 60, 32, 18000000, 3, false);
        Add("MIA", "Nikola", "Jovic", "PF", 22, "SRB", 211, 108, 76, 84, 76, 70, 66, 68, 64, 68, 66, 72, 70, 56, 50, 4000000, 3, false);
        Add("MIA", "Josh", "Richardson", "SG", 31, "USA", 196, 93, 74, 70, 80, 74, 64, 66, 64, 74, 60, 74, 68, 66, 36, 4000000, 2, false);
        Add("MIA", "Haywood", "Highsmith", "SF", 28, "USA", 203, 99, 74, 74, 78, 68, 62, 62, 58, 72, 62, 74, 68, 62, 42, 9700000, 3, false);
        Add("MIA", "Kevin", "Love", "PF", 36, "USA", 208, 113, 74, 68, 66, 76, 68, 64, 58, 66, 72, 66, 72, 46, 55, 3900000, 1, false);
        Add("MIA", "Jaime", "Jaquez Jr.", "SF", 23, "USA", 196, 99, 76, 82, 78, 74, 64, 66, 64, 72, 60, 74, 70, 64, 38, 5000000, 3, false);
        Add("MIA", "Thomas", "Bryant", "C", 27, "USA", 211, 113, 72, 72, 68, 62, 48, 54, 46, 60, 76, 68, 66, 48, 70, 3000000, 2, false);

        // ── MIL ──
        Add("MIL", "Giannis", "Antetokounmpo", "PF", 29, "GRE", 211, 110, 97, 97, 92, 78, 52, 86, 80, 90, 78, 96, 88, 66, 68, 51900000, 3, false);
        Add("MIL", "Damian", "Lillard", "PG", 34, "USA", 188, 86, 90, 86, 84, 90, 88, 86, 90, 74, 62, 78, 90, 68, 31, 45600000, 3, false);
        Add("MIL", "Brook", "Lopez", "C", 36, "USA", 213, 120, 78, 70, 64, 68, 62, 58, 54, 68, 80, 66, 72, 48, 78, 13000000, 2, false);
        Add("MIL", "Khris", "Middleton", "SF", 33, "USA", 201, 102, 84, 80, 78, 84, 72, 72, 72, 70, 64, 72, 80, 68, 37, 40000000, 2, false);
        Add("MIL", "Bobby", "Portis", "PF", 30, "USA", 208, 104, 78, 74, 72, 72, 62, 62, 58, 64, 72, 70, 70, 52, 59, 15900000, 3, false);
        Add("MIL", "Taurean", "Prince", "SF", 30, "USA", 201, 99, 74, 72, 78, 70, 64, 62, 60, 70, 60, 70, 66, 58, 40, 13000000, 3, false);
        Add("MIL", "Pat", "Connaughton", "SG", 32, "USA", 196, 94, 72, 70, 76, 72, 72, 60, 64, 64, 56, 70, 68, 62, 27, 8200000, 2, false);
        Add("MIL", "AJ", "Green", "SG", 25, "USA", 196, 90, 74, 76, 78, 74, 76, 62, 64, 66, 56, 66, 68, 62, 30, 4000000, 3, false);
        Add("MIL", "MarJon", "Beauchamp", "SF", 23, "USA", 198, 99, 72, 80, 80, 66, 56, 58, 58, 68, 60, 78, 64, 60, 38, 4000000, 3, false);
        Add("MIL", "Andre", "Jackson Jr.", "SF", 22, "USA", 201, 93, 70, 78, 78, 62, 52, 58, 56, 66, 58, 74, 62, 60, 36, 2000000, 3, false);
        Add("MIL", "Chris", "Livingston", "SF", 21, "USA", 198, 97, 70, 80, 78, 68, 56, 60, 60, 68, 58, 76, 64, 58, 36, 2000000, 3, false);

        // ── MIN ──
        Add("MIN", "Anthony", "Edwards", "SG", 23, "USA", 193, 102, 93, 96, 94, 90, 82, 76, 84, 84, 68, 92, 90, 68, 37, 29000000, 4, false);
        Add("MIN", "Karl-Anthony", "Towns", "C", 29, "DOM", 213, 113, 90, 88, 74, 84, 80, 76, 68, 72, 86, 74, 80, 52, 72, 50000000, 4, false);
        Add("MIN", "Rudy", "Gobert", "C", 31, "FRA", 216, 118, 82, 78, 68, 52, 26, 60, 50, 78, 90, 74, 80, 42, 86, 41000000, 3, false);
        Add("MIN", "Mike", "Conley", "PG", 37, "USA", 185, 82, 74, 68, 78, 72, 64, 78, 76, 70, 56, 62, 76, 68, 28, 13000000, 2, false);
        Add("MIN", "Jaden", "McDaniels", "SF", 23, "USA", 206, 99, 78, 86, 82, 66, 60, 62, 62, 74, 66, 82, 68, 64, 50, 18000000, 4, false);
        Add("MIN", "Nickeil", "Alexander-Walker", "SG", 26, "CAN", 196, 90, 76, 76, 82, 72, 64, 66, 68, 72, 58, 72, 68, 71, 37, 8000000, 2, false);
        Add("MIN", "Naz", "Reid", "C", 25, "USA", 208, 113, 76, 78, 68, 68, 60, 58, 52, 66, 76, 68, 70, 50, 66, 12000000, 3, false);
        Add("MIN", "Kyle", "Anderson", "PF", 31, "USA", 208, 113, 74, 70, 66, 68, 58, 72, 66, 68, 60, 68, 72, 58, 47, 10000000, 2, false);
        Add("MIN", "Rob", "Dillingham", "PG", 20, "USA", 185, 82, 70, 88, 84, 70, 64, 68, 70, 60, 50, 72, 66, 66, 22, 6000000, 4, false);
        Add("MIN", "Josh", "Minott", "SF", 22, "JAM", 203, 95, 70, 78, 78, 62, 52, 58, 56, 68, 58, 74, 62, 58, 36, 2000000, 3, false);
        Add("MIN", "Monte", "Morris", "PG", 29, "USA", 188, 82, 72, 70, 76, 68, 58, 74, 72, 66, 52, 56, 70, 62, 26, 4000000, 2, false);

        // ── NOP ──
        Add("NOP", "Zion", "Williamson", "PF", 24, "USA", 198, 129, 88, 90, 86, 82, 58, 72, 70, 72, 68, 86, 82, 60, 53, 35000000, 4, false);
        Add("NOP", "CJ", "McCollum", "SG", 33, "USA", 193, 88, 82, 78, 80, 84, 78, 72, 76, 70, 60, 70, 80, 62, 33, 33000000, 2, false);
        Add("NOP", "Trey", "Murphy III", "SF", 24, "USA", 203, 97, 78, 84, 84, 76, 74, 64, 66, 70, 60, 76, 72, 62, 36, 13000000, 3, false);
        Add("NOP", "Herbert", "Jones", "SF", 26, "USA", 201, 97, 76, 76, 82, 66, 52, 66, 62, 78, 64, 78, 70, 68, 50, 12000000, 3, false);
        Add("NOP", "Jose", "Alvarado", "PG", 27, "USA", 185, 79, 74, 72, 80, 66, 58, 72, 72, 72, 50, 62, 68, 74, 24, 12500000, 3, false);
        Add("NOP", "Brandon", "Ingram", "SF", 27, "USA", 206, 90, 82, 82, 84, 76, 66, 72, 68, 72, 62, 78, 76, 60, 38, 36000000, 2, false);
        Add("NOP", "Jordan", "Hawkins", "SG", 23, "USA", 193, 88, 74, 80, 76, 74, 72, 62, 64, 64, 56, 66, 68, 62, 30, 4000000, 3, false);
        Add("NOP", "Daniel", "Theis", "C", 31, "GER", 206, 108, 72, 68, 68, 62, 48, 56, 50, 64, 76, 66, 70, 44, 64, 5000000, 2, false);
        Add("NOP", "Javonte", "Green", "SF", 30, "USA", 196, 93, 64, 62, 78, 54, 40, 50, 48, 64, 54, 76, 56, 56, 40, 2000000, 1, false);
        Add("NOP", "Jeremiah", "Robinson-Earl", "PF", 24, "USA", 206, 108, 70, 72, 72, 62, 46, 56, 52, 62, 68, 68, 66, 50, 54, 2000000, 2, false);
        Add("NOP", "Elfrid", "Payton", "PG", 31, "USA", 196, 90, 72, 68, 78, 62, 46, 72, 68, 68, 52, 58, 66, 62, 26, 2500000, 1, false);

        // ── NYK ──
        Add("NYK", "Jalen", "Brunson", "PG", 27, "USA", 185, 86, 90, 92, 86, 88, 78, 88, 88, 76, 62, 76, 88, 68, 27, 156000000, 7, false);
        Add("NYK", "Karl-Anthony", "Towns", "C", 29, "DOM", 213, 113, 90, 88, 74, 84, 80, 76, 68, 72, 86, 74, 80, 52, 72, 50000000, 4, false);
        Add("NYK", "OG", "Anunoby", "SF", 26, "GBR", 203, 102, 84, 86, 84, 76, 66, 68, 66, 82, 68, 84, 80, 72, 48, 21200000, 4, false);
        Add("NYK", "Mikal", "Bridges", "SF", 27, "USA", 201, 95, 82, 82, 84, 76, 66, 68, 66, 78, 64, 80, 76, 66, 43, 30000000, 4, false);
        Add("NYK", "Josh", "Hart", "SF", 29, "USA", 196, 102, 78, 76, 82, 68, 58, 68, 64, 76, 64, 80, 72, 64, 43, 20000000, 3, false);
        Add("NYK", "Donte", "DiVincenzo", "PG", 27, "USA", 193, 90, 78, 76, 82, 76, 72, 68, 70, 72, 58, 70, 74, 66, 35, 12000000, 3, false);
        Add("NYK", "Isaiah", "Hartenstein", "C", 26, "GER", 213, 118, 78, 80, 72, 58, 36, 60, 52, 70, 82, 72, 74, 52, 67, 16000000, 3, false);
        Add("NYK", "Mitchell", "Robinson", "C", 26, "USA", 216, 109, 72, 74, 70, 50, 26, 50, 44, 66, 76, 80, 64, 46, 74, 16000000, 3, false);
        Add("NYK", "Precious", "Achiuwa", "PF", 25, "NGA", 206, 102, 72, 76, 78, 62, 44, 58, 54, 68, 68, 78, 68, 54, 58, 7000000, 2, false);
        Add("NYK", "Miles", "McBride", "PG", 24, "USA", 193, 86, 74, 78, 80, 70, 62, 68, 70, 68, 54, 66, 68, 66, 27, 3000000, 3, false);
        Add("NYK", "Deuce", "McBride", "PG", 24, "USA", 193, 86, 74, 78, 80, 70, 62, 68, 70, 68, 54, 66, 68, 66, 27, 3000000, 3, false);

        // ── OKC ──
        Add("OKC", "Shai", "Gilgeous-Alexander", "PG", 26, "CAN", 196, 88, 96, 97, 92, 92, 80, 84, 88, 88, 66, 86, 92, 76, 37, 34000000, 4, false);
        Add("OKC", "Chet", "Holmgren", "C", 22, "USA", 221, 88, 84, 92, 76, 74, 68, 68, 60, 78, 88, 82, 78, 56, 84, 9800000, 3, false);
        Add("OKC", "Jalen", "Williams", "SG", 23, "USA", 196, 95, 88, 94, 86, 88, 80, 80, 84, 80, 64, 80, 88, 70, 35, 12000000, 3, false);
        Add("OKC", "Luguentz", "Dort", "SG", 25, "CAN", 193, 97, 78, 78, 82, 70, 62, 62, 62, 78, 64, 78, 70, 72, 40, 11000000, 3, false);
        Add("OKC", "Isaiah", "Hartenstein", "C", 26, "GER", 213, 118, 78, 80, 72, 58, 36, 60, 52, 70, 82, 72, 74, 52, 67, 16000000, 3, false);
        Add("OKC", "Alex", "Caruso", "PG", 30, "USA", 193, 88, 76, 74, 82, 68, 60, 66, 64, 74, 58, 68, 70, 76, 30, 18800000, 3, false);
        Add("OKC", "Aaron", "Wiggins", "SG", 24, "USA", 196, 93, 74, 76, 78, 70, 62, 62, 62, 70, 58, 72, 68, 62, 33, 3000000, 3, false);
        Add("OKC", "Kenrich", "Williams", "SF", 29, "USA", 201, 97, 72, 70, 76, 66, 56, 62, 58, 70, 60, 72, 66, 60, 38, 8000000, 3, false);
        Add("OKC", "Ajay", "Mitchell", "PG", 22, "CAN", 193, 86, 74, 82, 78, 70, 64, 70, 68, 68, 54, 68, 68, 66, 28, 3500000, 3, true);
        Add("OKC", "Nikola", "Topic", "PG", 19, "SRB", 196, 93, 76, 90, 80, 72, 64, 76, 74, 68, 58, 72, 72, 66, 28, 7000000, 4, true);
        Add("OKC", "Jaylin", "Williams", "PF", 23, "USA", 203, 99, 72, 78, 74, 58, 44, 60, 54, 66, 68, 74, 66, 54, 52, 3500000, 3, false);

        // ── ORL ──
        Add("ORL", "Paolo", "Banchero", "PF", 22, "USA", 208, 113, 88, 96, 82, 80, 72, 82, 78, 76, 68, 82, 84, 64, 52, 10300000, 3, false);
        Add("ORL", "Franz", "Wagner", "SF", 23, "GER", 206, 97, 86, 92, 82, 80, 72, 76, 72, 74, 66, 78, 82, 64, 42, 9900000, 3, false);
        Add("ORL", "Wendell", "Carter Jr.", "C", 25, "USA", 208, 118, 78, 80, 72, 64, 42, 62, 54, 74, 84, 76, 76, 52, 68, 15000000, 3, false);
        Add("ORL", "Jalen", "Suggs", "PG", 24, "USA", 193, 95, 78, 84, 84, 72, 64, 74, 72, 76, 60, 78, 74, 72, 33, 12500000, 3, false);
        Add("ORL", "Cole", "Anthony", "PG", 24, "USA", 193, 90, 76, 78, 80, 72, 66, 72, 72, 68, 58, 68, 70, 62, 28, 16000000, 4, false);
        Add("ORL", "Moritz", "Wagner", "C", 27, "GER", 211, 109, 76, 76, 70, 68, 56, 60, 54, 62, 74, 68, 68, 50, 62, 11000000, 3, false);
        Add("ORL", "Jonathan", "Isaac", "PF", 27, "USA", 208, 104, 74, 78, 76, 64, 52, 60, 56, 70, 64, 78, 66, 60, 68, 17000000, 3, false);
        Add("ORL", "Gary", "Harris", "SG", 30, "USA", 193, 90, 72, 70, 78, 72, 64, 62, 62, 70, 58, 68, 66, 62, 30, 6000000, 2, false);
        Add("ORL", "Tristan", "da Silva", "SF", 24, "BRA", 203, 99, 74, 80, 76, 70, 64, 62, 62, 66, 60, 70, 66, 56, 42, 2000000, 3, true);
        Add("ORL", "Anthony", "Black", "PG", 21, "USA", 196, 90, 72, 84, 76, 64, 58, 68, 66, 68, 56, 68, 66, 64, 30, 5000000, 3, false);
        Add("ORL", "Kentavious", "Caldwell-Pope", "SG", 31, "USA", 196, 93, 74, 70, 78, 72, 66, 60, 62, 72, 58, 68, 66, 62, 34, 14000000, 2, false);

        // ── PHI ──
        Add("PHI", "Joel", "Embiid", "C", 30, "CMR", 213, 127, 94, 90, 72, 80, 62, 72, 62, 84, 92, 78, 82, 52, 80, 47600000, 4, false);
        Add("PHI", "Tyrese", "Maxey", "PG", 24, "USA", 188, 82, 88, 92, 90, 88, 78, 80, 84, 80, 64, 80, 88, 70, 29, 28000000, 4, false);
        Add("PHI", "Paul", "George", "SF", 34, "USA", 201, 100, 84, 78, 82, 82, 72, 70, 70, 76, 64, 76, 80, 66, 37, 51400000, 4, false);
        Add("PHI", "Tobias", "Harris", "PF", 32, "USA", 203, 106, 78, 74, 80, 76, 64, 64, 62, 66, 68, 68, 72, 56, 59, 39700000, 1, false);
        Add("PHI", "Kelly", "Oubre Jr.", "SF", 28, "USA", 201, 99, 76, 74, 82, 74, 64, 62, 62, 74, 62, 78, 68, 62, 40, 12900000, 2, false);
        Add("PHI", "Kyle", "Lowry", "PG", 38, "USA", 185, 86, 72, 68, 76, 70, 62, 76, 74, 68, 54, 60, 74, 66, 30, 10000000, 2, false);
        Add("PHI", "Andre", "Drummond", "C", 31, "USA", 213, 129, 74, 70, 64, 54, 28, 50, 44, 64, 86, 72, 70, 46, 74, 3500000, 1, false);
        Add("PHI", "KJ", "Martin", "SF", 24, "USA", 198, 97, 72, 76, 80, 66, 54, 58, 58, 70, 60, 78, 66, 58, 42, 4000000, 3, false);
        Add("PHI", "Reggie", "Jackson", "PG", 34, "USA", 188, 86, 72, 68, 76, 68, 58, 72, 72, 66, 52, 60, 68, 60, 28, 4000000, 1, false);
        Add("PHI", "Ricky", "Council IV", "SF", 23, "USA", 198, 93, 70, 78, 76, 66, 56, 58, 58, 68, 56, 72, 64, 58, 36, 2000000, 3, false);
        Add("PHI", "Guerschon", "Yabusele", "C", 30, "FRA", 201, 120, 72, 70, 64, 66, 68, 58, 52, 66, 78, 64, 70, 58, 77, 2000000, 1, false);

        // ── PHX ──
        Add("PHX", "Kevin", "Durant", "SF", 36, "USA", 208, 109, 92, 88, 80, 90, 78, 78, 74, 78, 66, 78, 88, 58, 56, 51400000, 2, false);
        Add("PHX", "Bradley", "Beal", "SG", 31, "USA", 193, 93, 82, 78, 82, 84, 72, 72, 74, 72, 62, 70, 80, 64, 33, 46700000, 2, false);
        Add("PHX", "Devin", "Booker", "SG", 28, "USA", 196, 93, 91, 92, 84, 92, 82, 76, 82, 74, 62, 80, 88, 64, 29, 36000000, 4, false);
        Add("PHX", "Jusuf", "Nurkic", "C", 30, "BIH", 213, 127, 78, 74, 66, 66, 42, 64, 56, 70, 84, 72, 76, 46, 66, 17600000, 3, false);
        Add("PHX", "Grayson", "Allen", "SG", 29, "USA", 196, 90, 76, 74, 78, 78, 74, 62, 66, 68, 58, 66, 70, 60, 32, 12000000, 3, false);
        Add("PHX", "Nassir", "Little", "PF", 25, "USA", 203, 102, 74, 76, 80, 68, 56, 58, 58, 72, 62, 78, 68, 60, 44, 10800000, 3, false);
        Add("PHX", "Ryan", "Dunn", "SF", 22, "USA", 201, 99, 72, 84, 80, 64, 54, 56, 56, 70, 62, 82, 64, 66, 40, 4000000, 3, true);
        Add("PHX", "David", "Roddy", "SF", 23, "USA", 201, 104, 72, 76, 74, 68, 62, 62, 60, 68, 62, 68, 66, 58, 40, 2000000, 3, false);
        Add("PHX", "Josh", "Okogie", "SF", 26, "USA", 196, 99, 70, 70, 80, 64, 52, 56, 54, 70, 56, 76, 62, 60, 38, 5500000, 2, false);
        Add("PHX", "Oso", "Ighodaro", "C", 23, "USA", 211, 108, 70, 80, 70, 54, 30, 50, 44, 62, 74, 74, 62, 48, 68, 2000000, 3, true);
        Add("PHX", "Monte", "Morris", "PG", 29, "USA", 188, 82, 72, 70, 76, 68, 58, 74, 72, 66, 52, 56, 70, 62, 26, 4000000, 2, false);

        // ── POR ──
        Add("POR", "Scoot", "Henderson", "PG", 21, "USA", 193, 88, 80, 92, 92, 72, 64, 78, 76, 70, 60, 82, 72, 66, 30, 8900000, 3, false);
        Add("POR", "Anfernee", "Simons", "PG", 25, "USA", 193, 88, 82, 84, 82, 84, 80, 72, 78, 68, 58, 72, 76, 75, 23, 24300000, 3, false);
        Add("POR", "Shaedon", "Sharpe", "SG", 21, "CAN", 196, 95, 78, 90, 88, 78, 68, 62, 66, 72, 62, 84, 72, 62, 35, 8900000, 3, false);
        Add("POR", "Jerami", "Grant", "PF", 30, "USA", 203, 102, 80, 78, 82, 76, 64, 64, 62, 74, 64, 76, 72, 62, 48, 21000000, 3, false);
        Add("POR", "Robert", "Williams III", "C", 27, "USA", 206, 104, 74, 78, 78, 54, 30, 54, 48, 70, 76, 82, 66, 54, 72, 13000000, 3, false);
        Add("POR", "Matisse", "Thybulle", "SF", 27, "USA", 198, 93, 72, 72, 82, 62, 52, 56, 56, 74, 60, 78, 62, 72, 44, 10000000, 3, false);
        Add("POR", "Jabari", "Walker", "PF", 22, "USA", 203, 102, 72, 80, 76, 64, 52, 60, 56, 68, 66, 76, 68, 58, 50, 3000000, 3, false);
        Add("POR", "Dalano", "Banton", "PG", 25, "CAN", 198, 90, 70, 74, 74, 64, 56, 68, 66, 64, 54, 64, 66, 62, 26, 2000000, 2, false);
        Add("POR", "Rayan", "Rupert", "SF", 21, "FRA", 201, 93, 70, 78, 74, 64, 58, 58, 58, 66, 58, 70, 64, 56, 36, 2000000, 3, true);
        Add("POR", "Donovan", "Clingan", "C", 20, "USA", 216, 120, 74, 86, 68, 54, 28, 52, 44, 64, 78, 76, 64, 46, 74, 5100000, 4, true);
        Add("POR", "Toumani", "Camara", "SF", 23, "GUI", 201, 97, 72, 78, 76, 66, 58, 60, 60, 68, 58, 72, 66, 58, 40, 2000000, 3, false);

        // ── SAC ──
        Add("SAC", "De'Aaron", "Fox", "PG", 27, "USA", 196, 86, 88, 90, 96, 82, 72, 82, 86, 76, 60, 84, 84, 68, 35, 30300000, 4, false);
        Add("SAC", "Domantas", "Sabonis", "C", 28, "LTU", 211, 120, 86, 84, 70, 72, 56, 82, 68, 70, 82, 70, 78, 54, 64, 37400000, 3, false);
        Add("SAC", "Kevin", "Huerter", "SG", 26, "USA", 201, 93, 78, 76, 76, 78, 78, 68, 70, 66, 60, 62, 70, 62, 34, 17000000, 3, false);
        Add("SAC", "Harrison", "Barnes", "SF", 32, "USA", 203, 102, 76, 72, 78, 74, 62, 64, 62, 68, 62, 70, 68, 56, 46, 20000000, 3, false);
        Add("SAC", "Keegan", "Murray", "SF", 24, "USA", 206, 102, 80, 84, 78, 76, 70, 64, 64, 68, 66, 74, 70, 58, 48, 7000000, 3, false);
        Add("SAC", "Malik", "Monk", "SG", 27, "USA", 188, 82, 78, 78, 84, 78, 72, 68, 72, 66, 58, 68, 70, 62, 26, 19000000, 3, false);
        Add("SAC", "Alex", "Len", "C", 31, "UKR", 216, 120, 68, 64, 62, 52, 30, 48, 42, 60, 74, 66, 64, 40, 64, 5500000, 2, false);
        Add("SAC", "Kessler", "Edwards", "SG", 24, "USA", 201, 97, 72, 76, 78, 70, 66, 62, 64, 66, 56, 70, 66, 62, 30, 7000000, 3, false);
        Add("SAC", "Jordan", "McLaughlin", "PG", 29, "USA", 185, 79, 70, 68, 76, 64, 52, 70, 70, 66, 50, 54, 66, 62, 24, 2000000, 2, false);
        Add("SAC", "Colby", "Jones", "PG", 23, "USA", 196, 93, 74, 80, 78, 68, 60, 70, 68, 68, 56, 68, 68, 62, 28, 4000000, 3, false);
        Add("SAC", "Nick", "Allen", "C", 22, "USA", 208, 113, 68, 76, 66, 52, 28, 48, 42, 58, 72, 70, 62, 44, 68, 2000000, 3, true);

        // ── SAS ──
        Add("SAS", "Victor", "Wembanyama", "C", 21, "FRA", 224, 95, 92, 99, 84, 78, 76, 72, 66, 82, 94, 86, 84, 54, 96, 55900000, 4, false);
        Add("SAS", "Chris", "Paul", "PG", 39, "USA", 185, 79, 78, 70, 78, 76, 68, 86, 82, 72, 56, 64, 80, 72, 27, 11000000, 1, false);
        Add("SAS", "Devin", "Vassell", "SG", 24, "USA", 201, 93, 80, 86, 82, 78, 72, 68, 70, 74, 62, 76, 74, 64, 35, 14400000, 3, false);
        Add("SAS", "Keldon", "Johnson", "SF", 25, "USA", 198, 99, 76, 78, 82, 72, 58, 62, 60, 72, 64, 78, 68, 60, 42, 12500000, 3, false);
        Add("SAS", "Zach", "Collins", "C", 27, "USA", 213, 109, 74, 74, 68, 64, 52, 60, 52, 66, 74, 68, 70, 50, 60, 7700000, 2, false);
        Add("SAS", "Tre", "Jones", "PG", 25, "USA", 185, 82, 70, 74, 76, 62, 50, 74, 72, 70, 52, 58, 70, 66, 28, 5900000, 2, false);
        Add("SAS", "Julian", "Champagnie", "SF", 23, "USA", 203, 95, 72, 78, 74, 68, 64, 58, 58, 66, 58, 68, 64, 58, 36, 3000000, 3, false);
        Add("SAS", "Sidy", "Cissoko", "SG", 20, "FRA", 196, 90, 68, 80, 76, 62, 56, 56, 58, 66, 56, 72, 62, 62, 32, 2000000, 3, true);
        Add("SAS", "Blake", "Wesley", "PG", 22, "USA", 193, 86, 68, 78, 76, 66, 58, 64, 64, 66, 52, 64, 64, 62, 26, 2000000, 3, false);
        Add("SAS", "Stephon", "Castle", "PG", 20, "USA", 196, 93, 76, 88, 80, 70, 64, 72, 70, 68, 58, 74, 68, 66, 30, 8100000, 4, true);
        Add("SAS", "Jeremy", "Sochan", "PF", 22, "POL", 203, 104, 74, 82, 78, 66, 58, 64, 60, 72, 64, 74, 66, 62, 46, 8800000, 3, false);

        // ── TOR ──
        Add("TOR", "Scottie", "Barnes", "PF", 23, "USA", 206, 108, 86, 94, 84, 78, 68, 80, 74, 82, 72, 88, 82, 68, 54, 30000000, 4, false);
        Add("TOR", "Immanuel", "Quickley", "PG", 25, "USA", 193, 88, 82, 86, 84, 80, 74, 78, 78, 72, 60, 72, 76, 66, 28, 17900000, 5, false);
        Add("TOR", "RJ", "Barrett", "SF", 24, "CAN", 198, 102, 82, 86, 80, 78, 68, 72, 70, 72, 62, 76, 76, 60, 38, 28000000, 4, false);
        Add("TOR", "Jakob", "Poeltl", "C", 28, "AUT", 213, 118, 78, 78, 68, 58, 30, 62, 52, 72, 84, 72, 74, 48, 70, 19500000, 3, false);
        Add("TOR", "Pascal", "Siakam", "PF", 30, "CMR", 206, 104, 88, 86, 84, 82, 64, 78, 74, 80, 74, 84, 82, 66, 55, 37900000, 3, false);
        Add("TOR", "Gradey", "Dick", "SG", 21, "USA", 198, 93, 76, 84, 78, 74, 74, 62, 64, 66, 58, 68, 68, 62, 30, 6200000, 3, false);
        Add("TOR", "Chris", "Boucher", "C", 31, "CAN", 211, 93, 74, 70, 72, 60, 56, 56, 52, 62, 68, 68, 66, 48, 68, 10800000, 2, false);
        Add("TOR", "Bruce", "Brown", "SF", 28, "USA", 196, 97, 72, 70, 80, 64, 52, 64, 60, 72, 58, 70, 66, 59, 36, 23000000, 2, false);
        Add("TOR", "Ochai", "Agbaji", "SG", 24, "USA", 198, 97, 70, 76, 74, 66, 62, 56, 58, 62, 56, 64, 60, 67, 30, 3600000, 2, false);
        Add("TOR", "Jonathan", "Mogbo", "PF", 22, "USA", 206, 108, 72, 80, 74, 58, 44, 56, 52, 66, 68, 74, 66, 52, 54, 2000000, 3, true);
        Add("TOR", "Davion", "Mitchell", "PG", 25, "USA", 188, 82, 72, 72, 80, 66, 56, 68, 66, 68, 54, 60, 66, 66, 28, 5900000, 3, false);

        // ── UTA ──
        Add("UTA", "Lauri", "Markkanen", "PF", 27, "FIN", 213, 107, 84, 84, 76, 84, 80, 68, 64, 68, 72, 72, 76, 60, 57, 21400000, 4, false);
        Add("UTA", "Jordan", "Clarkson", "SG", 32, "USA", 193, 86, 78, 72, 80, 78, 70, 68, 72, 66, 58, 66, 70, 60, 29, 13000000, 3, false);
        Add("UTA", "John", "Collins", "PF", 27, "USA", 206, 104, 78, 76, 76, 74, 58, 62, 56, 66, 72, 76, 68, 52, 57, 26500000, 3, false);
        Add("UTA", "Keyonte", "George", "PG", 21, "USA", 193, 86, 76, 86, 82, 76, 70, 72, 74, 66, 56, 68, 68, 64, 26, 7200000, 3, false);
        Add("UTA", "Walker", "Kessler", "C", 23, "USA", 216, 106, 74, 84, 68, 52, 28, 54, 46, 66, 82, 74, 68, 46, 78, 8100000, 3, false);
        Add("UTA", "Kyle", "Filipowski", "C", 21, "USA", 213, 104, 74, 84, 72, 64, 54, 58, 52, 64, 68, 70, 66, 48, 58, 5000000, 4, true);
        Add("UTA", "Cody", "Malinowski", "SG", 22, "USA", 193, 86, 72, 78, 76, 70, 68, 60, 62, 64, 54, 64, 64, 60, 27, 2000000, 3, true);
        Add("UTA", "Isaiah", "Collier", "PG", 20, "USA", 193, 88, 74, 86, 80, 66, 58, 72, 70, 64, 54, 68, 66, 64, 26, 6300000, 4, true);
        Add("UTA", "Brice", "Sensabaugh", "SG", 21, "USA", 198, 99, 72, 80, 76, 72, 66, 60, 62, 64, 56, 66, 64, 58, 30, 4500000, 3, true);
        Add("UTA", "Micah", "Potter", "C", 27, "USA", 211, 109, 68, 66, 66, 58, 42, 52, 46, 60, 70, 64, 64, 42, 60, 2000000, 2, false);
        Add("UTA", "Svi", "Mykhailiuk", "SG", 27, "UKR", 201, 91, 70, 68, 72, 72, 70, 58, 60, 62, 54, 60, 64, 56, 28, 3000000, 2, false);

        // ── WAS ──
        Add("WAS", "Kyle", "Kuzma", "PF", 29, "USA", 206, 102, 78, 76, 78, 74, 62, 64, 62, 66, 62, 68, 68, 54, 45, 13000000, 3, false);
        Add("WAS", "Jordan", "Poole", "PG", 25, "USA", 193, 90, 78, 80, 84, 80, 72, 72, 76, 68, 58, 68, 70, 60, 27, 27300000, 4, false);
        Add("WAS", "Tyus", "Jones", "PG", 28, "USA", 185, 82, 74, 72, 76, 68, 58, 78, 76, 68, 52, 56, 72, 64, 29, 14600000, 3, false);
        Add("WAS", "Jonas", "Valanciunas", "C", 32, "LTU", 211, 120, 78, 73, 64, 68, 48, 62, 52, 64, 84, 68, 76, 48, 69, 14000000, 1, false);
        Add("WAS", "Landry", "Shamet", "SG", 27, "USA", 196, 90, 74, 72, 74, 76, 74, 60, 62, 64, 56, 60, 66, 60, 27, 7500000, 3, false);
        Add("WAS", "Bilal", "Coulibaly", "SF", 21, "FRA", 203, 97, 74, 84, 80, 68, 60, 62, 60, 72, 62, 76, 66, 64, 38, 7600000, 3, false);
        Add("WAS", "Alexandre", "Sarr", "C", 20, "FRA", 216, 104, 72, 88, 72, 58, 46, 56, 50, 64, 76, 76, 64, 48, 74, 9700000, 4, true);
        Add("WAS", "Patrick", "Baldwin Jr.", "SF", 22, "USA", 208, 104, 68, 76, 74, 66, 62, 58, 58, 64, 56, 68, 62, 56, 38, 2000000, 3, false);
        Add("WAS", "Corey", "Kispert", "SF", 26, "USA", 201, 97, 68, 70, 68, 68, 70, 52, 54, 56, 52, 56, 58, 57, 36, 8000000, 3, false);
        Add("WAS", "Carlton", "Carrington", "PG", 21, "USA", 193, 86, 72, 82, 78, 66, 60, 66, 64, 64, 52, 64, 64, 60, 26, 4200000, 4, true);
        Add("WAS", "Richaun", "Holmes", "C", 31, "USA", 206, 113, 72, 68, 66, 54, 28, 50, 44, 62, 76, 68, 66, 42, 64, 3000000, 2, false);

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
                injury_type = ""
            });
        }

        // ── PG ──
        AddFA("Dante", "Exum", "PG", 30, "AUS", 196, 90, 74, 76, 82, 68, 58, 76, 74, 72, 50, 64, 72, 66, 22, 3000000, 1);
        AddFA("Lonzo", "Ball", "PG", 27, "USA", 198, 86, 70, 74, 78, 64, 60, 74, 70, 70, 56, 64, 70, 68, 30, 2000000, 1);
        AddFA("Patrick", "Beverley", "PG", 38, "USA", 185, 81, 68, 66, 76, 62, 56, 68, 64, 70, 56, 60, 70, 68, 42, 2000000, 1);
        AddFA("Monte", "Morris", "PG", 30, "USA", 188, 83, 72, 74, 78, 66, 58, 76, 74, 68, 50, 62, 72, 62, 22, 2000000, 1);
        AddFA("Killian", "Hayes", "PG", 24, "FRA", 195, 88, 68, 76, 78, 60, 50, 72, 70, 70, 52, 60, 68, 56, 20, 2000000, 1);
        AddFA("Facundo", "Campazzo", "PG", 34, "ARG", 178, 79, 72, 72, 80, 66, 60, 80, 78, 70, 50, 62, 70, 70, 22, 2000000, 1);
        AddFA("Raul", "Neto", "PG", 33, "BRA", 185, 82, 66, 66, 76, 64, 56, 70, 68, 68, 48, 60, 68, 56, 24, 2000000, 1);
        AddFA("Ish", "Smith", "PG", 37, "USA", 183, 79, 64, 64, 80, 62, 48, 72, 70, 70, 48, 60, 70, 60, 32, 2000000, 1);
        AddFA("Tre", "Jones", "PG", 25, "USA", 188, 84, 70, 74, 78, 64, 54, 76, 72, 70, 50, 62, 72, 58, 20, 2000000, 1);
        AddFA("Bones", "Hyland", "PG", 24, "USA", 188, 84, 72, 78, 84, 72, 66, 70, 74, 62, 48, 60, 70, 52, 18, 2000000, 1);
        AddFA("Jordan", "McLaughlin", "PG", 29, "USA", 183, 79, 66, 66, 82, 60, 52, 78, 74, 70, 48, 60, 70, 62, 34, 2000000, 1);
        AddFA("Troy", "Brown", "PG", 26, "USA", 198, 95, 66, 72, 74, 64, 56, 66, 66, 64, 56, 58, 66, 54, 28, 2000000, 1);
        AddFA("Cameron", "Payne", "PG", 31, "USA", 188, 84, 68, 70, 80, 68, 58, 72, 70, 64, 48, 60, 68, 54, 22, 2000000, 1);
        AddFA("Wesley", "Matthews", "PG", 38, "USA", 193, 95, 62, 62, 72, 62, 58, 60, 60, 62, 52, 56, 64, 50, 30, 2000000, 1);

        // ── SG ──
        AddFA("Malik", "Beasley", "SG", 29, "USA", 193, 84, 76, 77, 78, 80, 88, 62, 74, 62, 52, 75, 70, 64, 32, 8000000, 1);
        AddFA("Cam", "Thomas", "SG", 23, "USA", 196, 93, 84, 92, 76, 88, 84, 68, 80, 70, 60, 72, 82, 69, 36, 10000000, 3);
        AddFA("Bradley", "Beal", "SG", 32, "USA", 191, 95, 82, 82, 84, 84, 80, 76, 78, 70, 62, 74, 82, 64, 38, 46700000, 2);
        AddFA("Ben", "Simmons", "PG", 28, "AUS", 208, 99, 78, 76, 88, 60, 44, 84, 80, 84, 72, 88, 78, 82, 30, 8000000, 2);
        AddFA("Buddy", "Hield", "SG", 32, "USA", 193, 95, 76, 76, 80, 82, 86, 66, 72, 60, 54, 66, 72, 58, 30, 9000000, 2);
        AddFA("Seth", "Curry", "SG", 35, "USA", 188, 84, 72, 72, 78, 82, 84, 66, 74, 58, 52, 62, 72, 54, 22, 3000000, 1);
        AddFA("Gary", "Harris", "SG", 30, "USA", 193, 95, 70, 72, 80, 74, 74, 64, 68, 60, 54, 62, 70, 56, 24, 2000000, 1);
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
        AddFA("Terrence", "Mann", "SG", 28, "USA", 196, 95, 68, 72, 80, 70, 64, 64, 66, 58, 54, 60, 68, 52, 22, 2000000, 1);
        AddFA("Matisse", "Thybulle", "SG", 28, "AUS", 196, 95, 68, 72, 78, 58, 50, 60, 62, 72, 58, 80, 68, 70, 56, 2000000, 1);

        // ── SF ──
        AddFA("Oshae", "Brissett", "SF", 26, "CAN", 203, 95, 72, 76, 78, 72, 66, 64, 68, 62, 60, 62, 70, 56, 30, 2000000, 1);
        AddFA("Bojan", "Bogdanovic", "SF", 36, "CRO", 203, 104, 76, 76, 74, 82, 84, 68, 70, 56, 50, 60, 76, 48, 22, 19000000, 2);
        AddFA("Caleb", "Martin", "SF", 29, "USA", 198, 95, 74, 76, 78, 74, 70, 66, 70, 60, 58, 64, 72, 54, 30, 6800000, 1);
        AddFA("Troy", "Brown", "SF", 26, "USA", 198, 95, 66, 72, 74, 64, 56, 66, 66, 64, 56, 58, 66, 54, 28, 2000000, 1);
        AddFA("Justise", "Winslow", "SF", 29, "USA", 198, 104, 66, 72, 74, 58, 42, 68, 68, 66, 62, 60, 70, 56, 36, 2000000, 1);
        AddFA("Danuel", "House", "SF", 31, "USA", 198, 95, 68, 70, 76, 72, 68, 62, 64, 56, 54, 60, 68, 48, 22, 2000000, 1);
        AddFA("Kelly", "Oubre", "SF", 29, "USA", 201, 95, 74, 78, 84, 74, 68, 60, 68, 58, 56, 62, 70, 50, 28, 12000000, 1);
        AddFA("Taurean", "Prince", "SF", 31, "USA", 198, 99, 70, 72, 78, 74, 72, 64, 66, 58, 54, 60, 70, 50, 24, 4500000, 1);
        AddFA("Rondae", "Hollis-Jefferson", "SF", 30, "USA", 198, 104, 70, 72, 78, 66, 40, 66, 70, 64, 62, 66, 70, 56, 30, 2000000, 1);
        AddFA("Maurice", "Harkless", "SF", 32, "USA", 203, 99, 66, 66, 74, 66, 58, 60, 62, 58, 60, 58, 68, 48, 28, 2000000, 1);
        AddFA("James", "Ennis", "SF", 34, "USA", 198, 99, 66, 66, 76, 66, 60, 60, 62, 56, 54, 58, 66, 48, 24, 2000000, 1);
        AddFA("Justin", "Holiday", "SF", 35, "USA", 198, 95, 66, 66, 74, 70, 68, 62, 64, 56, 54, 58, 66, 48, 28, 2000000, 1);
        AddFA("Maxwell", "Lewis", "SF", 22, "USA", 201, 95, 64, 76, 74, 64, 60, 58, 62, 54, 50, 56, 66, 50, 18, 2000000, 1);
        AddFA("Terquavion", "Smith", "SF", 22, "USA", 193, 86, 66, 76, 82, 68, 62, 64, 70, 56, 48, 58, 68, 50, 20, 2000000, 1);

        // ── PF ──
        AddFA("Chris", "Boucher", "PF", 33, "CAN", 203, 90, 74, 74, 72, 74, 78, 60, 64, 66, 70, 75, 72, 62, 78, 4000000, 1);
        AddFA("Dario", "Saric", "PF", 31, "CRO", 208, 102, 72, 74, 72, 72, 68, 70, 68, 64, 60, 62, 72, 54, 30, 5000000, 1);
        AddFA("Orlando", "Robinson", "PF", 24, "USA", 211, 104, 64, 72, 66, 50, 30, 50, 50, 60, 76, 64, 58, 46, 42, 2000000, 1);
        AddFA("Taj", "Gibson", "PF", 39, "USA", 206, 104, 66, 66, 68, 58, 28, 54, 56, 64, 78, 66, 64, 44, 52, 2000000, 1);
        AddFA("Patrick", "Williams", "PF", 23, "USA", 201, 95, 72, 80, 76, 68, 58, 58, 62, 62, 60, 64, 72, 52, 38, 9000000, 2);
        AddFA("Larry", "Nance", "PF", 32, "USA", 206, 104, 70, 72, 74, 64, 46, 62, 64, 64, 76, 68, 68, 50, 48, 11000000, 2);
        AddFA("John", "Collins", "PF", 27, "USA", 206, 104, 78, 82, 78, 74, 60, 62, 64, 60, 64, 68, 76, 50, 46, 26000000, 2);
        AddFA("Thaddeus", "Young", "PF", 36, "USA", 203, 100, 68, 68, 72, 64, 40, 64, 68, 62, 64, 62, 70, 54, 56, 2000000, 1);
        AddFA("Noah", "Vonleh", "PF", 29, "USA", 208, 113, 62, 66, 68, 50, 28, 48, 50, 60, 76, 64, 60, 44, 42, 2000000, 1);
        AddFA("JaMychal", "Green", "PF", 34, "USA", 203, 102, 66, 66, 68, 62, 50, 56, 58, 62, 72, 64, 64, 48, 36, 2000000, 1);
        AddFA("Paul", "Reed", "PF", 25, "USA", 203, 100, 70, 76, 74, 60, 38, 58, 60, 62, 76, 68, 64, 48, 56, 2000000, 1);

        // ── C ──
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
            new SponsorData { name = "Apple",             logo = "Patrocinadores/1.png", initial_income = 28000000, home_game_income = 850000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Beats",             logo = "Patrocinadores/2.png",  initial_income = 18000000, home_game_income = 550000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Billboard",         logo = "Patrocinadores/3.png",  initial_income = 21000000, home_game_income = 620000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "BMW",               logo = "Patrocinadores/4.png",  initial_income = 22000000, home_game_income = 650000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Bridgestone",       logo = "Patrocinadores/5.png", initial_income = 18000000, home_game_income = 540000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Domino's Pizza",    logo = "Patrocinadores/6.png",  initial_income = 20000000, home_game_income = 600000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "MasterCard",        logo = "Patrocinadores/7.png",  initial_income = 15000000, home_game_income = 450000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Etihad Airways",    logo = "Patrocinadores/8.png",  initial_income = 16000000, home_game_income = 480000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Good Year",         logo = "Patrocinadores/9.png", initial_income = 14000000, home_game_income = 420000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Zoom",              logo = "Patrocinadores/10.png", initial_income = 19000000, home_game_income = 570000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Unicef",            logo = "Patrocinadores/11.png", initial_income = 14500000, home_game_income = 430000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Razer",             logo = "Patrocinadores/12.png", initial_income = 14500000, home_game_income = 430000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Starbucks",         logo = "Patrocinadores/13.png",  initial_income = 17000000, home_game_income = 500000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Lenovo",            logo = "Patrocinadores/14.png", initial_income = 18000000, home_game_income = 540000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Airbnb",            logo = "Patrocinadores/15.png", initial_income = 13000000, home_game_income = 390000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "McDonald's",        logo = "Patrocinadores/16.png",  initial_income = 15000000, home_game_income = 450000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Nvidia",            logo = "Patrocinadores/17.png", initial_income = 24000000, home_game_income = 720000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Qatar Airways",     logo = "Patrocinadores/18.png", initial_income = 16000000, home_game_income = 480000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "SONY",              logo = "Patrocinadores/19.png", initial_income = 26000000, home_game_income = 780000, contract_years = 1, is_active = 0, team_id = 0 },
            new SponsorData { name = "Netflix",           logo = "Patrocinadores/20.png",  initial_income = 24000000, home_game_income = 720000, contract_years = 1, is_active = 0, team_id = 0 },
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
            new TvChannelData { name = "DAZN",      logo = "Televisiones/1.png",   initial_income = 22000000, home_game_income = 650000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2500000, viewership_multiplier = 1.8f },
            new TvChannelData { name = "TV5",       logo = "Televisiones/2.png",   initial_income = 18000000, home_game_income = 550000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 1200000, viewership_multiplier = 1.2f },
            new TvChannelData { name = "FOX",       logo = "Televisiones/3.png",  initial_income = 18000000, home_game_income = 540000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2200000, viewership_multiplier = 1.6f },
            new TvChannelData { name = "Movistar",  logo = "Televisiones/4.png",   initial_income = 17000000, home_game_income = 500000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2200000, viewership_multiplier = 1.6f },
            new TvChannelData { name = "NBC",       logo = "Televisiones/5.png",   initial_income = 21000000, home_game_income = 620000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2600000, viewership_multiplier = 1.8f },
            new TvChannelData { name = "CBS",       logo = "Televisiones/6.png",   initial_income = 20000000, home_game_income = 600000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2500000, viewership_multiplier = 1.8f },
            new TvChannelData { name = "ESPN",      logo = "Televisiones/7.png",  initial_income = 26000000, home_game_income = 780000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 3000000, viewership_multiplier = 2.0f },
            new TvChannelData { name = "Sky",       logo = "Televisiones/8.png",   initial_income = 18000000, home_game_income = 540000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2300000, viewership_multiplier = 1.7f },
            new TvChannelData { name = "ITV",       logo = "Televisiones/9.png",  initial_income = 14000000, home_game_income = 420000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2000000, viewership_multiplier = 1.5f },
            new TvChannelData { name = "Hulu",      logo = "Televisiones/10.png",  initial_income = 19000000, home_game_income = 570000, contract_years = 1, is_active = 0, team_id = 0, broadcast_fee = 2100000, viewership_multiplier = 1.5f }
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

        // Update team settings
        var settings = GetTeamSettings(teamId);
        if (settings != null)
        {
            settings.sponsor_id = sponsorId;
            settings.sponsor_years_remaining = sponsor.contract_years;
            UpdateTeamSettings(settings);
        }

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

        // Update team settings
        var settings = GetTeamSettings(teamId);
        if (settings != null)
        {
            settings.tv_channel_id = channelId;
            settings.tv_years_remaining = channel.contract_years;
            UpdateTeamSettings(settings);
        }

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
                pg = posValues["PG"].name, pg_team = posValues["PG"].team,
                sg = posValues["SG"].name, sg_team = posValues["SG"].team,
                sf = posValues["SF"].name, sf_team = posValues["SF"].team,
                pf = posValues["PF"].name, pf_team = posValues["PF"].team,
                c  = posValues["C"].name,  c_team  = posValues["C"].team
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

    public void StartNewSeason(int oldSeasonId, int newTeamId, string gameMode, int managerId)
    {
        var allPlayers = _db.Table<PlayerData>().ToList();

        // 1. Retire players 40+
        foreach (var p in allPlayers.Where(p => p.age >= 40))
            _db.Delete(p);

        // 2. Age + attribute changes
        var remaining = _db.Table<PlayerData>().ToList();
        foreach (var p in remaining)
        {
            p.age += 1;

            if (p.age < 32)
            {
                p.overall = Math.Min(p.overall + 2, p.potential);
                p.speed = Math.Min(99, p.speed + 2);
                p.shooting = Math.Min(99, p.shooting + 2);
                p.three_point = Math.Min(99, p.three_point + 2);
                p.passing = Math.Min(99, p.passing + 2);
                p.dribbling = Math.Min(99, p.dribbling + 2);
                p.defense = Math.Min(99, p.defense + 2);
                p.rebounding = Math.Min(99, p.rebounding + 2);
                p.athleticism = Math.Min(99, p.athleticism + 2);
                p.iq = Math.Min(99, p.iq + 2);
                p.steals = Math.Min(99, p.steals + 2);
                p.blocks = Math.Min(99, p.blocks + 2);
            }
            else
            {
                p.overall = Math.Max(0, p.overall - 2);
                p.speed = Math.Max(0, p.speed - 2);
                p.shooting = Math.Max(0, p.shooting - 2);
                p.three_point = Math.Max(0, p.three_point - 2);
                p.passing = Math.Max(0, p.passing - 2);
                p.dribbling = Math.Max(0, p.dribbling - 2);
                p.defense = Math.Max(0, p.defense - 2);
                p.rebounding = Math.Max(0, p.rebounding - 2);
                p.athleticism = Math.Max(0, p.athleticism - 2);
                p.iq = Math.Max(0, p.iq - 2);
                p.steals = Math.Max(0, p.steals - 2);
                p.blocks = Math.Max(0, p.blocks - 2);
            }

            // 3. Decrement contracts
            p.contract_years -= 1;
            if (p.contract_years <= 0)
            {
                p.contract_years = 0;
                p.team_id = 0;
            }

            _db.Update(p);
        }

        // 4. Clear tables
        _db.Execute("DELETE FROM player_game_stats");
        _db.Execute("DELETE FROM finals_player_stats");
        _db.Execute("DELETE FROM games");
        _db.Execute("DELETE FROM messages");
        _db.Execute("DELETE FROM game_attendance");
        _db.Execute("DELETE FROM finance_records");

        // 5. Fill rosters to 12 for all teams (except the user's new team)
        var allTeams = GetAllTeams();
        var freeAgents = _db.Table<PlayerData>()
            .Where(p => p.team_id == 0 && p.age < 40)
            .OrderByDescending(p => p.overall)
            .ToList();

        foreach (var team in allTeams)
        {
            if (team.id == newTeamId) continue;

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

        // 6. Deactivate old season
        var oldSeason = _db.Find<SeasonData>(oldSeasonId);
        if (oldSeason != null)
        {
            oldSeason.is_active = 0;
            _db.Update(oldSeason);
        }

        // 7. Create new season
        int newYearStart = oldSeason != null ? oldSeason.year_start + 1 : 2026;
        var newSeason = new SeasonData
        {
            year_start = newYearStart,
            year_end = newYearStart + 1,
            is_active = 1,
            current_game_day = 0,
            game_mode = gameMode,
            phase = "regular",
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