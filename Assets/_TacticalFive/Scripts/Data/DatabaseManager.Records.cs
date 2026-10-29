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
            new HistoricalRecordData { stat_type = "points",     player_name = "Wilt Chamberlain", value = 100, game_date = "1962-03-02", team_abbreviation = "PHI" },
            new HistoricalRecordData { stat_type = "rebounds",   player_name = "Wilt Chamberlain", value = 55,  game_date = "1960-11-24", team_abbreviation = "PHI" },
            new HistoricalRecordData { stat_type = "assists",    player_name = "Scott Skiles",     value = 30,  game_date = "1990-12-30", team_abbreviation = "ORL" },
            new HistoricalRecordData { stat_type = "steals",     player_name = "Kendall Gill",     value = 11,  game_date = "1999-04-03", team_abbreviation = "BKN" },
            new HistoricalRecordData { stat_type = "blocks",     player_name = "Elmore Smith",     value = 17,  game_date = "1973-10-28", team_abbreviation = "LAL" },
            new HistoricalRecordData { stat_type = "fgm",        player_name = "Wilt Chamberlain", value = 36,  game_date = "1962-03-02", team_abbreviation = "PHI" },
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
                player_id = HistoricalStatsRules.LEGEND_ID,
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

    // ── MERCADO DE CONTRATOS (E3) ─────────────────────────

    /// <summary>
    /// Renegociación de patrocinio/TV al expirar (E3). Un equipo con mercado y prestigio
    /// encuentra sustituto con su contrato base re-escalado por fama; uno pequeño puede
    /// quedarse sin acuerdo. Cada contrato se resuelve por separado.
    /// </summary>
    void RenewMarketDealsForTeam(TeamData team, TeamSettingsData settings)
    {
        if (team == null || settings == null) return;

        bool needSponsor = settings.sponsor_id == 0;
        bool needTv = settings.tv_channel_id == 0;
        if (!needSponsor && !needTv) return;

        if (needSponsor)
        {
            var available = GetAvailableSponsors();
            if (available != null && available.Count > 0
                && ContractMarket.FindsReplacement(team.reputation, team.market_size, Rng.NextDouble()))
            {
                var pick = available[Rng.Next(available.Count)];
                settings.sponsor_id = pick.id;
                settings.sponsor_years_remaining = pick.contract_years > 0 ? pick.contract_years : 3;
                Debug.Log($"[DB] Renovación de patrocinio: {team.name} → {pick.name}");
            }
        }

        if (needTv)
        {
            var available = GetAvailableTVChannels();
            if (available != null && available.Count > 0
                && ContractMarket.FindsReplacement(team.reputation, team.market_size, Rng.NextDouble()))
            {
                var pick = available[Rng.Next(available.Count)];
                settings.tv_channel_id = pick.id;
                settings.tv_years_remaining = pick.contract_years > 0 ? pick.contract_years : 3;
                Debug.Log($"[DB] Renovación de TV: {team.name} → {pick.name}");
            }
        }
    }

    /// <summary>Patrocinios sin equipo asignado (candidatos a renovación).</summary>
    public List<SponsorData> GetAvailableSponsors()
    {
        if (!EnsureDb()) return new List<SponsorData>();
        return _db.Table<SponsorData>()
                  .Where(s => s.team_id == 0)
                  .ToList();
    }

    /// <summary>Canales de TV sin equipo asignado (candidatos a renovación).</summary>
    public List<TvChannelData> GetAvailableTVChannels()
    {
        if (!EnsureDb()) return new List<TvChannelData>();
        return _db.Table<TvChannelData>()
                  .Where(c => c.team_id == 0)
                  .ToList();
    }

    // ── FIN DE MERCADO DE CONTRATOS ───────────────────────

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

    // ── PLAYER GAME STATS ─────────────────────────────────

    public void DeletePlayerGameStatsForGame(int gameId)
    {
        _db.Execute("DELETE FROM player_game_stats WHERE game_id = ?", gameId);
    }

    public void SavePlayerGameStats(PlayerGameStats stats)
    {
        _db.Insert(stats);
    }

    /// <summary>
    /// F2-09: guarda en lote las estadísticas de un partido (una fila por jugador).
    /// Una sola transacción propia; si el llamador ya tiene una abierta (la del día)
    /// escribe dentro de ella y no anida un <c>BEGIN</c>.
    /// </summary>
    public void SavePlayerGameStatsBatch(IList<PlayerGameStats> stats)
    {
        if (!EnsureDb() || stats == null || stats.Count == 0) return;

        bool own = !_db.IsInTransaction;
        if (own) _db.BeginTransaction();
        try
        {
            _db.InsertAll(stats, runInTransaction: false);
            if (own) _db.Commit();
        }
        catch
        {
            if (own) _db.Rollback();
            throw;
        }
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

    /// <summary>Partidos jugados por jugador en una temporada (una sola query agregada).</summary>
    public Dictionary<int, int> GetGamesPlayedByPlayerInSeason(int managerId, int seasonId)
    {
        var dict = new Dictionary<int, int>();
        if (!EnsureDb()) return dict;

        var rows = _db.Query<PlayerGamesRow>(
            @"SELECT ps.player_id AS player_id, COUNT(*) AS games
              FROM player_game_stats ps
              JOIN games g ON ps.game_id = g.id
              WHERE g.manager_id = ? AND g.season_id = ? AND g.is_played = 1
                    AND g.game_type = 'regular'
                    AND ps.minutes > 0
              GROUP BY ps.player_id",
            managerId, seasonId);

        foreach (var r in rows)
            dict[r.player_id] = r.games;
        return dict;
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
                  .Where(s => s.player_id == playerId && s.minutes > 0 && gameIds.Contains(s.game_id))
                  .Count();
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
        long initialIncome = ContractMarket.SponsorInitialIncome(sponsor.initial_income);
        if (team != null)
        {
            team.budget += initialIncome;
            UpdateTeam(team);
        }

        // Create finance record
        var finance = new FinanceRecord
        {
            team_id = teamId,
            season_id = seasonId,
            record_type = FinanceRecord.TYPE_SPONSORSHIP,
            amount = initialIncome,
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
        long initialIncome = ContractMarket.TvInitialIncome(channel.initial_income);
        if (team != null)
        {
            team.budget += initialIncome;
            UpdateTeam(team);
        }

        // Create finance record
        var finance = new FinanceRecord
        {
            team_id = teamId,
            season_id = seasonId,
            record_type = FinanceRecord.TYPE_TV,
            amount = initialIncome,
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
                         .Where(r => r.team_id == teamId && r.season_id == seasonId)
                         .ToList();
        return records.Where(r => FinanceRecord.IsIncomeType(r.record_type))
                      .Sum(r => r.amount);
    }

    public long GetTotalExpenses(int teamId, int seasonId)
    {
        if (!EnsureDb()) return 0;
        var records = _db.Table<FinanceRecord>()
                         .Where(r => r.team_id == teamId && r.season_id == seasonId)
                         .ToList();
        return records.Where(r => FinanceRecord.IsExpenseType(r.record_type))
                      .Sum(r => r.amount);
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

    public int GetPendingFAOfferCount(int managerId, bool twoWay = false)
    {
        if (!EnsureDb()) return 0;
        int twoWayFlag = twoWay ? 1 : 0;
        return _db.Table<OfferData>().Count(o => o.manager_id == managerId
            && o.offer_type == 1 && o.processed == 0 && o.is_two_way == twoWayFlag);
    }

    public HashSet<int> GetPendingFAPlayerIds(int managerId)
    {
        if (!EnsureDb()) return new HashSet<int>();
        return new HashSet<int>(_db.Table<OfferData>()
            .Where(o => o.manager_id == managerId && o.offer_type == 1 && o.processed == 0)
            .Select(o => o.player_id));
    }

    public bool HasPendingRenewalOffer(int managerId, int playerId)
    {
        if (!EnsureDb()) return false;
        return _db.Table<OfferData>().Any(o =>
            o.manager_id == managerId &&
            o.player_id == playerId &&
            o.offer_type == 0 &&
            o.processed == 0);
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

    /// <summary>
    /// Agrupa toda una operación de traspaso (contratos, hard cap, jugadores, picks,
    /// historial y mensaje) en una transacción indivisible.
    /// </summary>
    public void RunTradeTransaction(Action operation)
    {
        if (!EnsureDb()) throw new InvalidOperationException("No hay una partida abierta para ejecutar el traspaso.");
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (_db.IsInTransaction)
            throw new InvalidOperationException("No se puede iniciar un traspaso dentro de otra transacción.");

        _db.RunInTransaction(operation);
    }

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
                  .OrderByDescending(t => t.game_date)
                  .ThenByDescending(t => t.id)
                  .ToList();
    }

    public bool HasTeamTradedThisSeason(int teamId, int seasonId)
    {
        if (!EnsureDb()) return false;
        return _db.Table<TradeData>()
                  .Where(t => t.season_id == seasonId && (t.team_id_from == teamId || t.team_id_to == teamId))
                  .Count() > 0;
    }

    public HashSet<int> GetPlayersTradedThisSeason(int seasonId)
    {
        if (!EnsureDb()) return new HashSet<int>();
        return new HashSet<int>(
            _db.Table<TradeData>()
               .Where(t => t.season_id == seasonId && t.trade_type == "trade")
               .Select(t => t.player_id));
    }

}
