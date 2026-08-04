using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager
{
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

    void SeedAllStarData()
    {
        var appearances = new List<AllStarAppearanceSeed>
        {
            new() { player_name = "LeBron James", appearances = 23 },
            new() { player_name = "Kareem Abdul-Jabbar", appearances = 18 },
            new() { player_name = "Kevin Durant", appearances = 16 },
            new() { player_name = "Kobe Bryant", appearances = 15 },
            new() { player_name = "Tim Duncan", appearances = 15 },
            new() { player_name = "Kevin Garnett", appearances = 14 },
            new() { player_name = "Dirk Nowitzki", appearances = 14 },
            new() { player_name = "Bob Cousy", appearances = 13 },
            new() { player_name = "Wilt Chamberlain", appearances = 13 },
            new() { player_name = "John Havlicek", appearances = 13 },
            new() { player_name = "Michael Jordan", appearances = 13 },
            new() { player_name = "Bill Russell", appearances = 12 },
            new() { player_name = "Oscar Robertson", appearances = 12 },
            new() { player_name = "Jerry West", appearances = 12 },
            new() { player_name = "Elvin Hayes", appearances = 12 },
            new() { player_name = "Hakeem Olajuwon", appearances = 12 },
            new() { player_name = "Karl Malone", appearances = 12 },
            new() { player_name = "Shaquille O'Neal", appearances = 12 },
            new() { player_name = "Dwyane Wade", appearances = 12 },
            new() { player_name = "Dolph Schayes", appearances = 11 },
            new() { player_name = "Bob Pettit", appearances = 11 },
            new() { player_name = "Elgin Baylor", appearances = 11 },
            new() { player_name = "Julius Erving", appearances = 11 },
            new() { player_name = "Moses Malone", appearances = 11 },
            new() { player_name = "Magic Johnson", appearances = 11 },
            new() { player_name = "Larry Bird", appearances = 11 },
            new() { player_name = "Chris Paul", appearances = 11 },
            new() { player_name = "Isiah Thomas", appearances = 11 },
            new() { player_name = "Carmelo Anthony", appearances = 10 },
            new() { player_name = "Hal Greer", appearances = 10 },
            new() { player_name = "Paul Pierce", appearances = 10 },
            new() { player_name = "Russell Westbrook", appearances = 10 },
            new() { player_name = "Stephen Curry", appearances = 10 },
            new() { player_name = "James Harden", appearances = 10 },
            new() { player_name = "David Robinson", appearances = 10 },
            new() { player_name = "Charles Barkley", appearances = 10 },
            new() { player_name = "Allen Iverson", appearances = 10 },
            new() { player_name = "Clyde Drexler", appearances = 10 },
            new() { player_name = "Patrick Ewing", appearances = 10 },
            new() { player_name = "George Gervin", appearances = 9 },
            new() { player_name = "Gary Payton", appearances = 9 },
            new() { player_name = "Jason Kidd", appearances = 9 },
            new() { player_name = "Scottie Pippen", appearances = 9 },
            new() { player_name = "Dominique Wilkins", appearances = 9 },
            new() { player_name = "Dwight Howard", appearances = 8 },
            new() { player_name = "Tracy McGrady", appearances = 8 },
            new() { player_name = "Anthony Davis", appearances = 8 },
            new() { player_name = "Kawhi Leonard", appearances = 8 },
            new() { player_name = "Giannis Antetokounmpo", appearances = 8 },
            new() { player_name = "Damian Lillard", appearances = 8 },
            new() { player_name = "Nikola Jokic", appearances = 7 },
            new() { player_name = "Joel Embiid", appearances = 7 },
            new() { player_name = "DeMar DeRozan", appearances = 7 },
            new() { player_name = "Luka Doncic", appearances = 6 },
            new() { player_name = "Jimmy Butler III", appearances = 6 },
            new() { player_name = "Donovan Mitchell", appearances = 6 },
            new() { player_name = "Jayson Tatum", appearances = 6 },
            new() { player_name = "Paul George", appearances = 9 },
            new() { player_name = "Karl-Anthony Towns", appearances = 5 },
            new() { player_name = "Devin Booker", appearances = 5 },
            new() { player_name = "Jaylen Brown", appearances = 5 },
            new() { player_name = "Trae Young", appearances = 4 },
            new() { player_name = "Rudy Gobert", appearances = 4 },
            new() { player_name = "Pascal Siakam", appearances = 4 },
            new() { player_name = "Shai Gilgeous-Alexander", appearances = 4 },
            new() { player_name = "Anthony Edwards", appearances = 4 },
            new() { player_name = "Ja Morant", appearances = 3 },
            new() { player_name = "Domantas Sabonis", appearances = 3 },
            new() { player_name = "Julius Randle", appearances = 3 },
            new() { player_name = "Khris Middleton", appearances = 3 },
            new() { player_name = "Bradley Beal", appearances = 3 },
            new() { player_name = "Tyrese Haliburton", appearances = 3 },
            new() { player_name = "Bam Adebayo", appearances = 3 },
            new() { player_name = "Zach LaVine", appearances = 2 },
            new() { player_name = "Darius Garland", appearances = 2 },
            new() { player_name = "Jarrett Allen", appearances = 2 },
            new() { player_name = "Jrue Holiday", appearances = 2 },
            new() { player_name = "Kristaps Porzingis", appearances = 2 },
            new() { player_name = "Scottie Barnes", appearances = 2 },
            new() { player_name = "Jalen Brunson", appearances = 2 },
            new() { player_name = "Victor Wembanyama", appearances = 2 },
            new() { player_name = "Tyrese Maxey", appearances = 2 },
            new() { player_name = "LaMelo Ball", appearances = 1 },
            new() { player_name = "Dejounte Murray", appearances = 1 },
            new() { player_name = "Fred VanVleet", appearances = 1 },
            new() { player_name = "De'Aaron Fox", appearances = 1 },
            new() { player_name = "Andrew Wiggins", appearances = 1 },
            new() { player_name = "Alperen Sengun", appearances = 1 },
            new() { player_name = "Cade Cunningham", appearances = 1 },
            new() { player_name = "Jalen Duren", appearances = 1 },
            new() { player_name = "Jalen Johnson", appearances = 1 },
            new() { player_name = "Norman Powell", appearances = 1 },
        };

        var allPlayers = _db.Table<PlayerData>().ToList();
        foreach (var a in appearances)
        {
            var match = allPlayers.FirstOrDefault(p => $"{p.first_name} {p.last_name}" == a.player_name);
            if (match != null)
                a.player_id = match.id;
        }
        _db.InsertAll(appearances);

        var games = new List<AllStarRecord>
        {
            new() { manager_id = 0, season = "2024", east_score = 211, west_score = 186, mvp = "Damian Lillard", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2017", east_score = 182, west_score = 192, mvp = "Anthony Davis", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2016", east_score = 173, west_score = 196, mvp = "Russell Westbrook", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2015", east_score = 158, west_score = 163, mvp = "Russell Westbrook", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2014", east_score = 163, west_score = 155, mvp = "Kyrie Irving", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2013", east_score = 138, west_score = 143, mvp = "Chris Paul", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2012", east_score = 149, west_score = 152, mvp = "Kevin Durant", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2011", east_score = 143, west_score = 148, mvp = "Kobe Bryant", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2010", east_score = 141, west_score = 139, mvp = "Dwyane Wade", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2009", east_score = 119, west_score = 146, mvp = "Kobe Bryant", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2008", east_score = 134, west_score = 128, mvp = "LeBron James", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2007", east_score = 132, west_score = 153, mvp = "Kobe Bryant", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2006", east_score = 122, west_score = 120, mvp = "LeBron James", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2005", east_score = 125, west_score = 115, mvp = "Allen Iverson", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2004", east_score = 132, west_score = 136, mvp = "Shaquille O'Neal", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2003", east_score = 145, west_score = 155, mvp = "Kevin Garnett", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2002", east_score = 120, west_score = 135, mvp = "Kobe Bryant", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2001", east_score = 111, west_score = 110, mvp = "Allen Iverson", mvp_player_id = 0 },
            new() { manager_id = 0, season = "2000", east_score = 126, west_score = 137, mvp = "Tim Duncan", mvp_player_id = 0 },
            new() { manager_id = 0, season = "1998", east_score = 135, west_score = 114, mvp = "Michael Jordan", mvp_player_id = 0 },
        };
        _db.InsertAll(games);

        Debug.Log($"[DB] {appearances.Count} apariciones All-Star y {games.Count} partidos históricos insertados.");
    }

    void SeedCoachRankings()
    {
        var teams = _db.Table<TeamData>().ToList();
        int GetTeamId(string name) => teams.FirstOrDefault(t => t.name == name)?.id ?? 0;

        var historical = new CoachRankingData[]
        {
            new() { name = "Phil Jackson",          team_id = 0, status = "historical", score = 1700 },
            new() { name = "Red Auerbach",          team_id = 0, status = "historical", score = 1400 },
            new() { name = "Gregg Popovich",        team_id = 0, status = "historical", score = 1300 },
            new() { name = "Pat Riley",             team_id = 0, status = "historical", score = 1250 },
            new() { name = "Don Nelson",            team_id = 0, status = "historical", score = 800  },
            new() { name = "Jerry Sloan",           team_id = 0, status = "historical", score = 750  },
            new() { name = "Larry Brown",           team_id = 0, status = "historical", score = 750  },
            new() { name = "Lenny Wilkens",         team_id = 0, status = "historical", score = 750  },
            new() { name = "Chuck Daly",            team_id = 0, status = "historical", score = 600  },
            new() { name = "Rudy Tomjanovich",      team_id = 0, status = "historical", score = 500  },
            new() { name = "Doc Rivers",            team_id = 0, status = "historical", score = 450  },
            new() { name = "Bill Fitch",            team_id = 0, status = "historical", score = 350  },
            new() { name = "K.C. Jones",            team_id = 0, status = "historical", score = 300  },
            new() { name = "George Karl",           team_id = 0, status = "historical", score = 280  },
            new() { name = "Mike Budenholzer",      team_id = 0, status = "historical", score = 280 },
            new() { name = "John Kundla",           team_id = 0, status = "historical", score = 250  },
            new() { name = "Rick Adelman",          team_id = 0, status = "historical", score = 240 },
            new() { name = "Hubie Brown",           team_id = 0, status = "historical", score = 235 },
            new() { name = "Cotton Fitzsimmons",    team_id = 0, status = "historical", score = 225 },
            new() { name = "Flip Saunders",         team_id = 0, status = "historical", score = 220 },
            new() { name = "Billy Cunningham",      team_id = 0, status = "historical", score = 220 },
            new() { name = "Tex Winter",            team_id = 0, status = "historical", score = 180 },
            new() { name = "George Senesky",        team_id = 0, status = "historical", score = 120 },
            new() { name = "Tom Heinsohn",          team_id = 0, status = "historical", score = 215 },
            new() { name = "Jack Ramsay",           team_id = 0, status = "historical", score = 210 },
            new() { name = "Gene Shue",             team_id = 0, status = "historical", score = 205 },
            new() { name = "Doug Moe",              team_id = 0, status = "historical", score = 200 },
            new() { name = "Jeff Van Gundy",        team_id = 0, status = "historical", score = 195 },
            new() { name = "Stan Van Gundy",        team_id = 0, status = "historical", score = 190 },
            new() { name = "Mike D'Antoni",         team_id = 0, status = "historical", score = 185 },
            new() { name = "Monty Williams",        team_id = 0, status = "historical", score = 184 },
            new() { name = "Frank Vogel",           team_id = 0, status = "historical", score = 182 },
            new() { name = "Byron Scott",           team_id = 0, status = "historical", score = 180 },
            new() { name = "Avery Johnson",         team_id = 0, status = "historical", score = 175 },
            new() { name = "Scott Skiles",          team_id = 0, status = "historical", score = 170 },
            new() { name = "Lionel Hollins",        team_id = 0, status = "historical", score = 165 },
            new() { name = "Mike Fratello",         team_id = 0, status = "historical", score = 160 },
            new() { name = "Paul Westphal",         team_id = 0, status = "historical", score = 155 },
            new() { name = "Jerry Colangelo",       team_id = 0, status = "historical", score = 150 },
            new() { name = "Larry Costello",        team_id = 0, status = "historical", score = 145 },
            new() { name = "Al Attles",             team_id = 0, status = "historical", score = 140 },
            new() { name = "Gene Mauch",            team_id = 0, status = "historical", score = 140 },
            new() { name = "Bill Sharman",          team_id = 0, status = "historical", score = 135 },
            new() { name = "Dick Motta",            team_id = 0, status = "historical", score = 130 },
            new() { name = "Del Harris",            team_id = 0, status = "historical", score = 125 },
            new() { name = "Paul Silas",            team_id = 0, status = "historical", score = 120 },
            new() { name = "George Irvine",         team_id = 0, status = "historical", score = 115 },
            new() { name = "Frank Layden",          team_id = 0, status = "historical", score = 110 },
            new() { name = "Mike Dunleavy Sr.",     team_id = 0, status = "historical", score = 105 },
            new() { name = "Brendan Malone",        team_id = 0, status = "historical", score = 100 },
            new() { name = "Kevin Loughery",        team_id = 0, status = "historical", score = 98 },
            new() { name = "Johnny Kerr",           team_id = 0, status = "historical", score = 96 },
            new() { name = "Dick Harter",           team_id = 0, status = "historical", score = 94 },
            new() { name = "Don Chaney",            team_id = 0, status = "historical", score = 92 },
            new() { name = "Mike Woodson",          team_id = 0, status = "historical", score = 90 },
            new() { name = "P.J. Carlesimo",        team_id = 0, status = "historical", score = 88 },
            new() { name = "Terry Porter",          team_id = 0, status = "historical", score = 86 },
            new() { name = "Dwane Casey",           team_id = 0, status = "historical", score = 78 },
            new() { name = "Terry Stotts",          team_id = 0, status = "historical", score = 76 },
            new() { name = "Vinny Del Negro",       team_id = 0, status = "historical", score = 74 },
            new() { name = "Jim Boylen",            team_id = 0, status = "historical", score = 72 },
            new() { name = "Nate McMillan",         team_id = 0, status = "historical", score = 70 },
            new() { name = "Maurice Cheeks",        team_id = 0, status = "historical", score = 68 },
            new() { name = "Nate Bjorkgren",        team_id = 0, status = "historical", score = 66 },
            new() { name = "Dave Cowens",           team_id = 0, status = "historical", score = 64 },
            new() { name = "Don Casey",             team_id = 0, status = "historical", score = 62 },
            new() { name = "Randy Wittman",         team_id = 0, status = "historical", score = 60 },
            new() { name = "Mike Montgomery",       team_id = 0, status = "historical", score = 58 },
            new() { name = "Jim O'Brien",           team_id = 0, status = "historical", score = 56 },
        };

        var active = new (string name, string teamName, int score)[]
        {
            ("Steve Kerr",         "Golden State Warriors",  700),
            ("Erik Spoelstra",     "Miami Heat",             650),
            ("Tyronn Lue",         "Los Angeles Clippers",   350),
            ("Rick Carlisle",      "Indiana Pacers",         650),
            ("Nick Nurse",         "Philadelphia 76ers",     300),
            ("Ime Udoka",          "Houston Rockets",        150),
            ("Mark Daigneault",    "Oklahoma City Thunder",  140),
            ("Chris Finch",        "Minnesota Timberwolves", 160),
            ("Mike Brown",         "New York Knicks",        450),
            ("J.B. Bickerstaff",   "Detroit Pistons",        160),
            ("Joe Mazzulla",       "Boston Celtics",         250),
            ("Kenny Atkinson",     "Cleveland Cavaliers",    150),
            ("Will Hardy",         "Utah Jazz",              70 ),
            ("Quin Snyder",        "Atlanta Hawks",          220),
            ("Darko Rajaković",    "Toronto Raptors",        55 ),
            ("Jamahl Mosley",      "New Orleans Pelicans",   60 ),
            ("Taylor Jenkins",     "Milwaukee Bucks",        90 ),
            ("JJ Redick",          "Los Angeles Lakers",     5  ),
            ("Brian Keefe",        "Washington Wizards",     5  ),
            ("Charles Lee",        "Charlotte Hornets",      5  ),
            ("Jordi Fernández",    "Brooklyn Nets",          5  ),
            ("Tiago Splitter",     "Chicago Bulls",          5  ),
            ("Doug Christie",      "Sacramento Kings",       5  ),
            ("Sean Sweeney",       "Orlando Magic",          2  ),
            ("Jordan Ott",         "Phoenix Suns",           2  ),
            ("Micah Nori",         "Portland Trail Blazers", 2  ),
            ("Mitch Johnson",      "San Antonio Spurs",      2  ),
            ("David Adelman",      "Denver Nuggets",         2  ),
            ("Tuomas Iisalo",      "Memphis Grizzlies",      2  ),
            ("Dusty May",          "Dallas Mavericks",       2  ),
        };

        foreach (var c in historical) _db.Insert(c);
        foreach (var (name, teamName, score) in active)
        {
            _db.Insert(new CoachRankingData
            {
                name = name,
                team_id = GetTeamId(teamName),
                status = "active",
                score = score
            });
        }
        Debug.Log($"[DB] {historical.Length} coaches históricos, {active.Length} coaches activos insertados.");
    }

    public void AddPlayerCoachEntry(int teamId, string managerName)
    {
        EnsureDb();
        var existing = _db.Table<CoachRankingData>().FirstOrDefault(c => c.status == "player");
        if (existing != null) return;

        _db.Insert(new CoachRankingData
        {
            name = managerName,
            team_id = teamId,
            status = "player",
            score = 0
        });
    }

    public List<CoachRankingData> GetCoachRanking()
    {
        EnsureDb();
        return _db.Table<CoachRankingData>()
                  .OrderByDescending(c => c.score)
                  .ToList();
    }

    public void UpdateCoachScore(int coachId, int scoreDelta)
    {
        EnsureDb();
        var coach = _db.Find<CoachRankingData>(coachId);
        if (coach == null || coach.status == "historical") return;
        coach.score += scoreDelta;
        _db.Update(coach);
    }

    public void SetCoachInactive(int teamId)
    {
        EnsureDb();
        var coach = _db.Table<CoachRankingData>().FirstOrDefault(c => c.team_id == teamId && c.status == "active");
        if (coach != null)
        {
            coach.status = "inactive";
            coach.team_id = 0;
            _db.Update(coach);
        }
    }

    public void ReassignCoachToTeam(int teamId)
    {
        EnsureDb();
        // First check if there's already an active/inactive coach for this team
        var existing = _db.Table<CoachRankingData>().FirstOrDefault(c => c.team_id == teamId && c.status == "player");
        if (existing != null) return; // team is taken by player

        var inactiveOfThisTeam = _db.Table<CoachRankingData>().FirstOrDefault(c => c.status == "inactive");
        if (inactiveOfThisTeam != null)
        {
            inactiveOfThisTeam.status = "active";
            inactiveOfThisTeam.team_id = teamId;
            _db.Update(inactiveOfThisTeam);
        }
    }

    public void UpdatePlayerCoachTeam(int newTeamId)
    {
        EnsureDb();
        var playerCoach = _db.Table<CoachRankingData>().FirstOrDefault(c => c.status == "player");
        if (playerCoach != null)
        {
            playerCoach.team_id = newTeamId;
            _db.Update(playerCoach);
        }
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

    public List<AllStarRecord> GetAllStarRecords(int managerId)
    {
        if (!EnsureDb()) return new List<AllStarRecord>();
        return _db.Table<AllStarRecord>()
                  .Where(a => a.manager_id == 0 || a.manager_id == managerId)
                  .ToList();
    }

    public void SaveAllStarRecord(AllStarRecord record)
    {
        if (!EnsureDb()) return;
        _db.Insert(record);
    }

    public class AllStarAppearanceEntry
    {
        public int player_id { get; set; }
        public string player_name { get; set; }
        public string team_logo { get; set; }
        public int appearances { get; set; }
    }

    public List<AllStarAppearanceEntry> GetAllStarAppearances(int managerId)
    {
        if (!EnsureDb()) return new List<AllStarAppearanceEntry>();

        var fromGames = _db.Query<AllStarAppearanceEntry>(@"
            SELECT p.id as player_id, p.first_name || ' ' || p.last_name as player_name,
                   t.logo as team_logo, COUNT(DISTINCT g.season_id) as appearances
            FROM player_game_stats pgs
            JOIN games g ON pgs.game_id = g.id
            JOIN players p ON pgs.player_id = p.id
            JOIN teams t ON p.team_id = t.id
            WHERE g.game_type = 'allstar' AND g.manager_id = ?
            GROUP BY pgs.player_id", managerId);

        var fromSeed = _db.Table<AllStarAppearanceSeed>().ToList();

        var gameById = fromGames.ToDictionary(e => e.player_id);

        var result = new List<AllStarAppearanceEntry>();

        foreach (var s in fromSeed)
        {
            int totalApps = s.appearances;
            string displayName = s.player_name;
            string logo = "";

            if (s.player_id > 0 && gameById.TryGetValue(s.player_id, out var gameStats))
            {
                totalApps += gameStats.appearances;
                displayName = gameStats.player_name;
                logo = gameStats.team_logo ?? "";
                gameById.Remove(s.player_id);
            }
            else if (s.player_id > 0)
            {
                var player = _db.Table<PlayerData>().FirstOrDefault(p => p.id == s.player_id);
                if (player != null && player.team_id > 0)
                {
                    var team = _db.Table<TeamData>().FirstOrDefault(t => t.id == player.team_id);
                    if (team != null)
                        logo = team.logo;
                }
            }

            result.Add(new AllStarAppearanceEntry
            {
                player_name = displayName,
                appearances = totalApps,
                team_logo = logo
            });
        }

        foreach (var kv in gameById)
        {
            result.Add(new AllStarAppearanceEntry
            {
                player_name = kv.Value.player_name,
                appearances = kv.Value.appearances,
                team_logo = kv.Value.team_logo ?? ""
            });
        }

        return result.OrderByDescending(e => e.appearances).ToList();
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

    public List<SeasonStatsAggregate> GetSeasonPlayerStatsAggregates(int managerId, int seasonId)
    {
        if (!EnsureDb()) return new List<SeasonStatsAggregate>();
        return _db.Query<SeasonStatsAggregate>(
            @"SELECT ps.player_id,
                     COUNT(*) AS gp,
                     SUM(ps.points) AS total_points,
                     SUM(ps.rebounds) AS total_rebounds,
                     SUM(ps.assists) AS total_assists,
                     SUM(ps.steals) AS total_steals,
                     SUM(ps.blocks) AS total_blocks,
                     SUM(ps.fgm) AS total_fgm,
                     SUM(ps.fga) AS total_fga,
                     SUM(ps.fg3m) AS total_fg3m,
                     SUM(ps.fg3a) AS total_fg3a,
                     SUM(ps.ftm) AS total_ftm,
                     SUM(ps.fta) AS total_fta,
                     SUM(ps.turnovers) AS total_turnovers,
                     SUM(ps.minutes) AS total_minutes,
                     SUM(ps.rating) AS total_rating,
                     SUM(ps.double_double) AS total_dd,
                     SUM(ps.triple_double) AS total_td
              FROM player_game_stats ps
              JOIN games g ON ps.game_id = g.id
              WHERE g.manager_id = ?
                AND g.season_id = ?
                AND g.game_type = 'regular'
                AND g.is_played = 1
              GROUP BY ps.player_id",
            managerId, seasonId);
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
            sponsor.is_active = 1;
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
        sponsor.is_active = 1;
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
        channel.is_active = 1;
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

    public List<PlayerCareerSeasonRow> GetPlayerCareerHistory(int playerId, int managerId)
    {
        if (!EnsureDb()) return new List<PlayerCareerSeasonRow>();

        // Archived past seasons + current unarchived season
        return _db.Query<PlayerCareerSeasonRow>(
            @"SELECT season_id, year_start, year_end, team_id,
                     team_abbreviation, team_name,
                     games, total_minutes, total_points,
                     total_rebounds, total_assists,
                     total_steals, total_blocks, total_rating
              FROM player_season_stats
              WHERE player_id = ?

              UNION ALL

              SELECT g.season_id, s.year_start, s.year_end,
                     ps.team_id, t.abbreviation AS team_abbreviation, t.name AS team_name,
                     COUNT(*) AS games,
                     SUM(ps.minutes) AS total_minutes,
                     SUM(ps.points) AS total_points,
                     SUM(ps.rebounds) AS total_rebounds,
                     SUM(ps.assists) AS total_assists,
                     SUM(ps.steals) AS total_steals,
                     SUM(ps.blocks) AS total_blocks,
                     SUM(ps.rating) AS total_rating
              FROM player_game_stats ps
              JOIN games g ON ps.game_id = g.id
              JOIN seasons s ON g.season_id = s.id
              LEFT JOIN teams t ON ps.team_id = t.id
              WHERE ps.player_id = ? AND g.manager_id = ? AND g.is_played = 1
              GROUP BY g.season_id

              ORDER BY season_id",
            playerId, playerId, managerId).ToList();
    }

    public List<PlayerAwardEntry> GetPlayerAwards(int playerId)
    {
        if (!EnsureDb()) return new List<PlayerAwardEntry>();

        var allRecords = _db.Query<SeasonAwardRow>(
            @"SELECT sr.season_id, s.year_start, s.year_end,
                     sr.season_mvp_id, sr.rookie_of_year_id, sr.finals_mvp_id,
                     sr.best_defender_id, sr.sixth_man_id, sr.most_improved_id,
                     sr.all_star_pg_id, sr.all_star_sg_id, sr.all_star_sf_id,
                     sr.all_star_pf_id, sr.all_star_c_id,
                     sr.first_team_pg, sr.first_team_sg, sr.first_team_sf,
                     sr.first_team_pf, sr.first_team_c,
                     sr.second_team_pg, sr.second_team_sg, sr.second_team_sf,
                     sr.second_team_pf, sr.second_team_c,
                     sr.champion_id
              FROM season_records sr
              JOIN seasons s ON sr.season_id = s.id
              ORDER BY sr.season_id").ToList();

        var awards = new List<PlayerAwardEntry>();

        foreach (var r in allRecords)
        {
            if (r.season_mvp_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "mvp" });
            if (r.rookie_of_year_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "roty" });
            if (r.finals_mvp_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "finals_mvp" });
            if (r.best_defender_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "dpoy" });
            if (r.sixth_man_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "sixth_man" });
            if (r.most_improved_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "mip" });
            if (r.champion_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "champion" });

            // All-Star
            if (r.all_star_pg_id == playerId || r.all_star_sg_id == playerId ||
                r.all_star_sf_id == playerId || r.all_star_pf_id == playerId ||
                r.all_star_c_id == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "all_star" });

            // First Team
            if (r.first_team_pg == playerId || r.first_team_sg == playerId ||
                r.first_team_sf == playerId || r.first_team_pf == playerId ||
                r.first_team_c == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "first_team" });

            // Second Team
            if (r.second_team_pg == playerId || r.second_team_sg == playerId ||
                r.second_team_sf == playerId || r.second_team_pf == playerId ||
                r.second_team_c == playerId)
                awards.Add(new PlayerAwardEntry { season_id = r.season_id, year_start = r.year_start, year_end = r.year_end, award_type = "second_team" });
        }

        // Monthly awards (player / rookie of the month)
        var monthlyAwards = _db.Query<MonthlyAwardData>(
            "SELECT * FROM monthly_awards WHERE player_id = ? AND award_type IN ('player_month', 'rookie_month')",
            playerId).ToList();

        foreach (var m in monthlyAwards)
        {
            var season = _db.Find<SeasonData>(m.season_id);
            if (season != null)
                awards.Add(new PlayerAwardEntry { season_id = m.season_id, year_start = season.year_start, year_end = season.year_end, award_type = m.award_type });
        }

        return awards;
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
            string mvpRatingStr = ((double)topMvp.total_rating / Math.Max(1, topMvp.games)).ToString("F1", CultureInfo.InvariantCulture);

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
                rookieRatingStr = ((double)topRookie.total_rating / Math.Max(1, topRookie.games)).ToString("F1", CultureInfo.InvariantCulture);
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

            // All-NBA Quintets — both primary and secondary positions, no duplicate players
            string[] positions = { "PG", "SG", "SF", "PF", "C" };
            var posValues = new Dictionary<string, (string name, string team)>
            {
                { "PG", ("", "") }, { "SG", ("", "") }, { "SF", ("", "") },
                { "PF", ("", "") }, { "C",  ("", "") }
            };
            var assigned = new HashSet<int>();

            foreach (string pos in positions)
            {
                var posPlayers = seasonStats
                    .Where(s =>
                    {
                        if (assigned.Contains(s.player_id)) return false;
                        var p = GetPlayerById(s.player_id);
                        return p != null && (p.position == pos || (!string.IsNullOrEmpty(p.secondary_position) && p.secondary_position == pos));
                    })
                    .ToList();
                if (posPlayers.Count == 0) continue;

                var qualified = posPlayers.Where(x => x.games >= 65).ToList();
                if (qualified.Count == 0) qualified = posPlayers;
                var best = qualified.OrderByDescending(x => (double)x.total_rating / x.games).First();
                assigned.Add(best.player_id);
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
            PlayerId = player.id,
            Photo = player.photo,
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
            SELECT p.id, p.photo, p.first_name, p.last_name, p.position, t.name AS team_name, t.logo AS team_logo,
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
            PlayerId = row.id,
            Photo = row.photo ?? "",
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
        string sql = $@"
            SELECT p.id, p.photo, p.first_name, p.last_name, p.position, p.secondary_position,
                   t.name AS team_name, t.logo AS team_logo,
                   COUNT(*) AS games,
                   AVG(ps.points) AS avg_pts, AVG(ps.rebounds) AS avg_reb,
                   AVG(ps.assists) AS avg_ast, AVG(ps.rating) AS avg_rating
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN players p ON ps.player_id = p.id
            JOIN teams t ON p.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.manager_id = ? {rookieFilter}
            GROUP BY ps.player_id
            HAVING games >= ?
            ORDER BY avg_rating DESC";
        var allRows = _db.Query<PlayerAwardQueryRow>(sql, seasonId, managerId, minGames);
        var result = new List<PlayerAwardInfo>();
        string[] positions = { "PG", "SG", "SF", "PF", "C" };
        var assigned = new HashSet<int>();
        foreach (var pos in positions)
        {
            PlayerAwardQueryRow best = null;
            foreach (var row in allRows)
            {
                if (assigned.Contains(row.id)) continue;
                if (row.position == pos || (!string.IsNullOrEmpty(row.secondary_position) && row.secondary_position == pos))
                {
                    if (best == null || row.avg_rating > best.avg_rating)
                        best = row;
                }
            }
            if (best != null)
            {
                assigned.Add(best.id);
                result.Add(new PlayerAwardInfo
                {
                    PlayerId = best.id,
                    Photo = best.photo ?? "",
                    PlayerName = $"{best.first_name} {best.last_name}",
                    TeamName = best.team_name ?? "",
                    TeamKeyword = best.team_logo ?? "",
                    Position = pos,
                    AvgPts = (float)best.avg_pts,
                    AvgReb = (float)best.avg_reb,
                    AvgAst = (float)best.avg_ast,
                    AvgRating = (float)best.avg_rating
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

    public List<MonthlyAwardData> EvaluateMonthlyAwards(int seasonId, string monthName,
        string startDate, string endDate, int managerId, int myTeamId)
    {
        if (!EnsureDb()) return new List<MonthlyAwardData>();

        Debug.Log($"[MonthlyAwards] Evaluando {monthName}: {startDate} → {endDate} (season {seasonId})");

        _db.Execute("DELETE FROM monthly_awards WHERE season_id = ? AND month_name = ?", seasonId, monthName);

        var results = new List<MonthlyAwardData>();

        // ── Manager of the Month ──
        var managerAwards = _db.Query<MonthlyManagerAwardRow>(@"
            SELECT t.id AS team_id, t.name AS team_name,
                   SUM(CASE WHEN (g.home_team_id = t.id AND g.home_score > g.away_score)
                             OR (g.away_team_id = t.id AND g.away_score > g.home_score) THEN 1 ELSE 0 END) AS wins,
                   COUNT(*) AS games,
                   SUM(CASE WHEN g.home_team_id = t.id THEN g.home_score - g.away_score
                            ELSE g.away_score - g.home_score END) AS diff
            FROM games g
            JOIN teams t ON t.id IN (g.home_team_id, g.away_team_id)
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.game_date >= ? AND g.game_date <= ?
            GROUP BY t.id
            HAVING games >= 5
            ORDER BY CAST(wins AS REAL) / games DESC, diff DESC
            LIMIT 3",
            seasonId, startDate, endDate);

        for (int i = 0; i < managerAwards.Count; i++)
        {
            var m = managerAwards[i];
            int? mgrId = m.team_id == myTeamId ? (int?)managerId : null;
            float winPct = m.games > 0 ? (float)m.wins / m.games : 0f;
            results.Add(new MonthlyAwardData
            {
                season_id = seasonId,
                month_name = monthName,
                award_type = "manager",
                rank = i + 1,
                manager_id = mgrId,
                team_id = m.team_id,
                team_name = m.team_name,
                player_name = m.team_name,
                value = winPct
            });
        }

        // ── Player of the Month ──
        var playerAwards = _db.Query<MonthlyPlayerAwardRow>(@"
            SELECT ps.player_id, p.first_name || ' ' || p.last_name AS player_name,
                   p.team_id, t.name AS team_name,
                   AVG(ps.rating) AS avg_rating, COUNT(*) AS games
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN players p ON ps.player_id = p.id
            LEFT JOIN teams t ON p.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.game_date >= ? AND g.game_date <= ?
            GROUP BY ps.player_id
            HAVING games >= 5
            ORDER BY avg_rating DESC
            LIMIT 3",
            seasonId, startDate, endDate);

        for (int i = 0; i < playerAwards.Count; i++)
        {
            var p = playerAwards[i];
            results.Add(new MonthlyAwardData
            {
                season_id = seasonId,
                month_name = monthName,
                award_type = "player",
                rank = i + 1,
                player_id = p.player_id,
                player_name = p.player_name,
                team_id = p.team_id,
                team_name = p.team_name,
                value = (float)p.avg_rating
            });
        }

        // ── Rookie of the Month ──
        var rookieAwards = _db.Query<MonthlyPlayerAwardRow>(@"
            SELECT ps.player_id, p.first_name || ' ' || p.last_name AS player_name,
                   p.team_id, t.name AS team_name,
                   AVG(ps.rating) AS avg_rating, COUNT(*) AS games
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN players p ON ps.player_id = p.id AND p.is_rookie = 1
            LEFT JOIN teams t ON p.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.game_date >= ? AND g.game_date <= ?
            GROUP BY ps.player_id
            ORDER BY avg_rating DESC
            LIMIT 3",
            seasonId, startDate, endDate);

        for (int i = 0; i < rookieAwards.Count; i++)
        {
            var r = rookieAwards[i];
            results.Add(new MonthlyAwardData
            {
                season_id = seasonId,
                month_name = monthName,
                award_type = "rookie",
                rank = i + 1,
                player_id = r.player_id,
                player_name = r.player_name,
                team_id = r.team_id,
                team_name = r.team_name,
                value = (float)r.avg_rating
            });
        }

        Debug.Log($"[MonthlyAwards] {monthName}: {managerAwards.Count} managers, {playerAwards.Count} players, {rookieAwards.Count} rookies → {results.Count} total");

        // Persist all
        foreach (var award in results)
            _db.Insert(award);

        // Manager of the Month rank 1 gets +10 coach ranking points
        var topManager = results.FirstOrDefault(r => r.award_type == "manager" && r.rank == 1 && r.team_id.HasValue);
        if (topManager != null)
        {
            var coach = _db.Table<CoachRankingData>().FirstOrDefault(c => c.team_id == topManager.team_id.Value);
            if (coach != null && coach.status != "historical")
            {
                coach.score += 10;
                _db.Update(coach);
            }
        }

        return results;
    }

    public List<MonthlyAwardData> GetMonthlyAwardsForSeason(int seasonId)
    {
        if (!EnsureDb()) return new List<MonthlyAwardData>();
        return _db.Query<MonthlyAwardData>(@"
            SELECT * FROM monthly_awards
            WHERE season_id = ?
            ORDER BY
                CASE month_name
                    WHEN 'noviembre' THEN 0
                    WHEN 'diciembre' THEN 1
                    WHEN 'enero' THEN 2
                    WHEN 'febrero' THEN 3
                    WHEN 'marzo' THEN 4
                    ELSE 5
                END,
                CASE award_type
                    WHEN 'manager' THEN 0
                    WHEN 'player' THEN 1
                    ELSE 2
                END,
                rank", seasonId).ToList();
    }

    public int CountManagerOfTheMonthWins(int managerId)
    {
        if (!EnsureDb()) return 0;
        return _db.Table<MonthlyAwardData>()
                  .Count(a => a.manager_id == managerId && a.award_type == "manager" && a.rank == 1);
    }

    public int CountPlayerOfTheMonthWins(int playerId)
    {
        if (!EnsureDb()) return 0;
        return _db.Table<MonthlyAwardData>()
                  .Count(a => a.player_id == playerId && a.award_type == "player" && a.rank == 1);
    }

    public int CountRookieOfTheMonthWins(int playerId)
    {
        if (!EnsureDb()) return 0;
        return _db.Table<MonthlyAwardData>()
                  .Count(a => a.player_id == playerId && a.award_type == "rookie" && a.rank == 1);
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

    public void StartNewSeason(int oldSeasonId, int newTeamId, string gameMode, int managerId, int prevTeamId = 0)
    {
        _db.BeginTransaction();
        try
        {
        // 0. Archive historical stats BEFORE clearing tables
        var oldSeason = _db.Find<SeasonData>(oldSeasonId);
        if (oldSeason != null)
            UpdateHistoricalPlayerStatsFromSeason(oldSeasonId, managerId);

        // 0b. Archive per-season stats for career history
        _db.Execute("DELETE FROM player_season_stats WHERE season_id = ?", oldSeasonId);
        _db.Execute(@"INSERT INTO player_season_stats
            (player_id, season_id, year_start, year_end, team_id,
             team_abbreviation, team_name,
             games, total_minutes, total_points, total_rebounds, total_assists,
             total_steals, total_blocks, total_rating)
            SELECT ps.player_id, g.season_id, s.year_start, s.year_end,
                   ps.team_id, t.abbreviation, t.name,
                   COUNT(*), SUM(ps.minutes), SUM(ps.points), SUM(ps.rebounds),
                   SUM(ps.assists), SUM(ps.steals), SUM(ps.blocks), SUM(ps.rating)
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            JOIN seasons s ON g.season_id = s.id
            LEFT JOIN teams t ON ps.team_id = t.id
            WHERE g.season_id = ? AND g.is_played = 1
            GROUP BY ps.player_id", oldSeasonId, oldSeasonId);

        var allPlayers = _db.Table<PlayerData>().ToList();

        // 1. Retire players 40+
        foreach (var p in allPlayers.Where(p => p.age >= 40))
            _db.Delete(p);

        // 2. Age + attribute changes (progression/regression by career phase)
        var remaining = _db.Table<PlayerData>().ToList();

        // Pre-compute team win% for player option decisions
        var teamWinPctCache = new Dictionary<int, float>();
        var leagueSettings = GetLeagueSettings();
        if (leagueSettings != null && oldSeasonId > 0)
        {
            var seasonGames = _db.Table<GameData>()
                .Where(g => g.season_id == oldSeasonId && g.is_played == 1)
                .ToList();
            var teamGames = new Dictionary<int, (int wins, int total)>();
            foreach (var g in seasonGames)
            {
                if (!teamGames.ContainsKey(g.team_id_home)) teamGames[g.team_id_home] = (0, 0);
                if (!teamGames.ContainsKey(g.team_id_away)) teamGames[g.team_id_away] = (0, 0);
                teamGames[g.team_id_home] = (teamGames[g.team_id_home].wins + (g.score_home > g.score_away ? 1 : 0), teamGames[g.team_id_home].total + 1);
                teamGames[g.team_id_away] = (teamGames[g.team_id_away].wins + (g.score_away > g.score_home ? 1 : 0), teamGames[g.team_id_away].total + 1);
            }
            foreach (var kv in teamGames)
                teamWinPctCache[kv.Key] = kv.Value.total > 0 ? (float)kv.Value.wins / kv.Value.total : 0.5f;
        }

        foreach (var p in remaining)
        {
            if (p.is_rookie == 1)
            {
                _db.Update(p);
                continue;
            }

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

            // 2.5. Resolve contract options before decrement
            // Skip team options for the manager's team (already handled in NewSeason modal)
            if (p.has_team_option == 1 && p.guaranteed_years == 0 && p.contract_years > 0
                && p.team_id != newTeamId)
            {
                if (DecideTeamOption(p))
                {
                    p.contract_years += 1;
                    p.guaranteed_years = p.contract_years;
                }
                p.has_team_option = 0;
            }
            // Player options are always decided by the AI (player decides)
            if (p.has_player_option == 1 && p.guaranteed_years == 0 && p.contract_years > 0)
            {
                float teamPct = teamWinPctCache.TryGetValue(p.team_id, out float wp) ? wp : 0.5f;
                if (DecidePlayerOption(p, teamPct, leagueSettings))
                {
                    p.contract_years += 1;
                    p.guaranteed_years = p.contract_years;
                }
                p.has_player_option = 0;
            }

            // 3. Decrement contracts
            p.contract_years -= 1;
            p.guaranteed_years -= 1;
            if (p.contract_years <= 0)
            {
                p.contract_years = 0;
                p.guaranteed_years = 0;
                p.has_team_option = 0;
                p.has_player_option = 0;
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

        // 4b. Archive manager career stats from the completed season
        if (prevTeamId > 0 && oldSeasonId > 0)
        {
            try
            {
                var seasonGames = _db.Table<GameData>()
                    .Where(g => g.manager_id == managerId && g.season_id == oldSeasonId && g.is_played == 1)
                    .ToList();

                if (seasonGames.Count > 0)
                {
                    var manager = _db.Find<ManagerData>(managerId);
                    if (manager != null)
                    {
                        // Regular season
                        var regGames = seasonGames
                            .Where(g => g.game_type == "regular"
                                && (g.home_team_id == prevTeamId || g.away_team_id == prevTeamId))
                            .ToList();
                        int regW = regGames.Count(g =>
                            (g.home_team_id == prevTeamId && g.home_score > g.away_score) ||
                            (g.away_team_id == prevTeamId && g.away_score > g.home_score));
                        manager.career_reg_wins += regW;
                        manager.career_reg_losses += regGames.Count - regW;

                        // Playoff / Play-In
                        var poGames = seasonGames
                            .Where(g => (g.game_type == "playoff" || g.game_type == "playin")
                                && (g.home_team_id == prevTeamId || g.away_team_id == prevTeamId))
                            .ToList();
                        int poW = poGames.Count(g =>
                            (g.home_team_id == prevTeamId && g.home_score > g.away_score) ||
                            (g.away_team_id == prevTeamId && g.away_score > g.home_score));
                        manager.career_po_wins += poW;
                        manager.career_po_losses += poGames.Count - poW;

                        // Championship
                        if (oldSeason != null)
                        {
                            string seasonLabel = $"{oldSeason.year_start}-{oldSeason.year_end.ToString().Substring(2)}";
                            var finalsRecord = _db.Table<FinalsRecord>()
                                .FirstOrDefault(f => f.season == seasonLabel);
                            if (finalsRecord != null)
                            {
                                var oldTeam = GetTeamById(prevTeamId);
                                if (oldTeam != null && finalsRecord.champ_name == oldTeam.name)
                                    manager.championships += 1;
                            }
                        }

                        manager.seasons_completed += 1;
                        _db.Update(manager);
                        Debug.Log($"[DB] Archived career stats for manager {managerId}: reg {manager.career_reg_wins}-{manager.career_reg_losses}, po {manager.career_po_wins}-{manager.career_po_losses}, rings {manager.championships}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DB] Error archiving manager career stats: {ex.Message}");
            }
        }

        // 5. Clear tables
        _db.Execute("DELETE FROM player_game_stats");
        _db.Execute("DELETE FROM finals_player_stats");
        _db.Execute("DELETE FROM games");
        _db.Execute("DELETE FROM messages");
        _db.Execute("DELETE FROM game_attendance");
        _db.Execute("DELETE FROM finance_records");

        // 5b. Trim AI teams to max 17
        TrimRostersToMaxSize(17, newTeamId);

        // 6. Fill rosters to 15 for all teams (except the user's new team)
        var allTeams = GetAllTeams();
        var freeAgents = _db.Table<PlayerData>()
            .Where(p => p.team_id == 0 && p.age < 40)
            .OrderByDescending(p => p.overall)
            .ToList();

        var capSettings = GetLeagueSettings();
        long minSalary = capSettings?.minimum_salary ?? TradeHelper.MIN_SALARY;
        long salaryCap = capSettings?.salary_cap ?? TradeHelper.SALARY_CAP;

        // Sort AI teams by average overall rating descending (best teams sign first)
        var teamsToFill = allTeams
            .Where(t => t.id != newTeamId)
            .Select(t =>
            {
                var r = GetPlayersByTeam(t.id);
                double avg = r.Count > 0 ? r.Average(p => (double)p.overall) : 0;
                return (team: t, avgOvr: avg);
            })
            .OrderByDescending(x => x.avgOvr)
            .Select(x => x.team)
            .ToList();

        const int targetRosterSize = 15;

        foreach (var team in teamsToFill)
        {
            var roster = GetPlayersByTeam(team.id);
            long payroll = roster.Sum(p => p.salary);
            int need = targetRosterSize - roster.Count;
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
                    long availableCap = salaryCap - payroll;
                    long faSalary = signed.salary;

                    if (faSalary > availableCap)
                    {
                        faSalary = Math.Max(minSalary, availableCap);
                    }

                    signed.salary = faSalary;
                    signed.team_id = team.id;
                    signed.contract_years = 1;
                    signed.guaranteed_years = 1;
                    signed.seasons_with_team = 1;
                    _db.Update(signed);
                    freeAgents.Remove(signed);
                    payroll += faSalary;
                    posCounts[signed.position] = posCounts.GetValueOrDefault(signed.position) + 1;
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
            leagueSettings.taxpayer_mid_level = (long)(leagueSettings.taxpayer_mid_level * 1.05);
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
            if (team.id == newTeamId) continue;

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

        // Seed draft picks for the new season. Use oldSeason standings if it
        // exists; otherwise fall back to overall+reputation ordering.
        int? prevSeasonId = oldSeason != null ? (int?)oldSeason.id : null;
        SeedDraftPicks(newSeason.id, managerId, prevSeasonId);
        _db.Commit();
        }
        catch (System.Exception ex)
        {
        _db.Rollback();
        Debug.LogError($"[DB] StartNewSeason error, rolled back: {ex.Message}\n{ex.StackTrace}");
        throw;
        }
    }

    public void TrimRostersToMaxSize(int maxSize, int excludeTeamId)
    {
        var allTeams = GetAllTeams();
        foreach (var team in allTeams)
        {
            if (team.id == excludeTeamId) continue;
            var roster = GetPlayersByTeam(team.id);
            if (roster.Count <= maxSize) continue;

            var sorted = roster.OrderBy(p => p.GetCalculatedAverage()).ToList();
            int excess = roster.Count - maxSize;
            for (int i = 0; i < excess; i++)
            {
                var p = sorted[i];
                p.team_id = 0;
                p.contract_years = 0;
                p.guaranteed_years = 0;
                p.has_team_option = 0;
                p.has_player_option = 0;
                _db.Update(p);
            }
        }
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
        var forceInactive = forceInactiveIds ?? new HashSet<int>();

        var posOrder = new[] { "PG", "SG", "SF", "PF", "C" };

        // Assign best player at each position as starter (excluding forced-inactive)
        for (int si = 0; si < posOrder.Length; si++)
        {
            var best = players
                .Where(p => (p.position == posOrder[si] || p.secondary_position == posOrder[si])
                            && !assigned.Contains(p.id)
                            && !forceInactive.Contains(p.id))
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

        // Fill bench with the next best unassigned players (excluding forced-inactive)
        var remaining = players
            .Where(p => !assigned.Contains(p.id) && !forceInactive.Contains(p.id))
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

        foreach (var p in players.Where(p => forceInactive.Contains(p.id)))
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

    /// <summary>
    /// AI decides whether to exercise a team option. The option year is the current upcoming season.
    /// Criteria: the player contributes positively (overall above threshold relative to salary/slot).
    /// </summary>
    public bool DecideTeamOption(PlayerData p)
    {
        // Always exercise if the player is decent and salary is reasonable
        if (p.overall < 65) return false;
        // Reject if the player is old and declining (age > 34 or overall < potential-10)
        if (p.age > 34 && p.overall < p.potential - 10) return false;
        // Accept otherwise
        return true;
    }

    /// <summary>
    /// AI decides whether the player exercises their player option (opts out to test FA market).
    /// Uses a weighted score: market value, happiness, loyalty, role, age, team success.
    /// </summary>
    public bool DecidePlayerOption(PlayerData p, float teamWinPct, LeagueSettingsData settings)
    {
        if (settings == null) return DecidePlayerOptionLegacy(p);

        long cap = settings.salary_cap;
        float score = 50f;

        // 1. Market: current salary vs estimated market value
        long marketSalary = EstimateMarketSalary(p.overall, cap);
        float salaryRatio = marketSalary > 0 ? (float)p.salary / marketSalary : 1f;
        if (salaryRatio >= 1.0f)      score += 20f;  // well paid → stay
        else if (salaryRatio < 0.70f) score -= 25f;  // underpaid → seek market

        // 2. Happiness + loyalty
        score += (p.morale - 50) * 0.5f;                    // morale: -25 to +25
        score += System.Math.Min(p.seasons_with_team, 5) * 4f;  // loyalty: +4/yr, max +20

        // 3. Role
        score += p.role switch
        {
            PlayerRole.Estrella => 15f,
            PlayerRole.Titular => 5f,
            PlayerRole.Banquillo => -5f,
            PlayerRole.UltimoRecurso => -15f,
            _ => 0f
        };

        // 4. Age
        if (p.age < 26 && p.potential >= p.overall + 3) score -= 15f;  // young + upside → bet
        else if (p.age > 33)                               score += 10f;  // veteran → security

        // 5. Team success
        if (teamWinPct >= 0.60f)      score += 15f;   // contender
        else if (teamWinPct <= 0.35f) score -= 15f;   // tanking

        // 6. Controlled randomness (±10)
        score += UnityEngine.Random.Range(-10, 11);

        return score >= 50f;
    }

    bool DecidePlayerOptionLegacy(PlayerData p)
    {
        if (p.overall >= 85) return false;
        if (p.age < 28 && p.overall >= 80 && p.potential >= 85) return false;
        return true;
    }

    /// <summary>
    /// Estimated market salary based on overall rating.
    /// Formula: (overall - 40) * 0.005 of the salary cap, clamped to [2%, 35%].
    /// </summary>
    public long EstimateMarketSalary(int overall, long cap)
    {
        float pct = (overall - 40) * 0.005f;
        pct = System.Math.Clamp(pct, 0.02f, 0.35f);
        return (long)(cap * pct);
    }
}
