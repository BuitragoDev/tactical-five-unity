using System.Collections.Generic;

public static class TeamRecordSeeder
{
    public class TeamRecordSeedEntry
    {
        public string stat_type;
        public string player_name;
        public int value;
        public string game_date;

        public TeamRecordSeedEntry(string stat_type, string player_name, int value, string game_date)
        {
            this.stat_type = stat_type;
            this.player_name = player_name;
            this.value = value;
            this.game_date = game_date;
        }
    }

    public static Dictionary<string, List<TeamRecordSeedEntry>> Data => new Dictionary<string, List<TeamRecordSeedEntry>>
    {
        {
            "Atlanta Hawks",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Dominique Wilkins", 57, "1987-01-10"),
                new TeamRecordSeedEntry("rebounds", "Bob Pettit", 35, "1959-01-06"),
                new TeamRecordSeedEntry("assists", "Mookie Blaylock", 23, "1993-03-06"),
                new TeamRecordSeedEntry("steals", "Mookie Blaylock", 10, "1998-04-14"),
                new TeamRecordSeedEntry("blocks", "Dikembe Mutombo", 11, "2000-02-15"),
                new TeamRecordSeedEntry("fgm", "Dominique Wilkins", 21, "1987-01-10"),
                new TeamRecordSeedEntry("fg3m", "Trae Young", 11, "2022-01-03"),
                new TeamRecordSeedEntry("ftm", "John Drew", 23, "1978-01-01"),
                new TeamRecordSeedEntry("turnovers", "Trae Young", 13, "2019-03-01")
            }
        },
        {
            "Boston Celtics",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "John Havlicek", 54, "1973-04-01"),
                new TeamRecordSeedEntry("rebounds", "Bill Russell", 40, "1958-03-23"),
                new TeamRecordSeedEntry("assists", "Rajon Rondo", 20, "2011-04-22"),
                new TeamRecordSeedEntry("steals", "Dennis Johnson", 7, "1986-04-29"),
                new TeamRecordSeedEntry("blocks", "Robert Williams III", 9, "2021-05-22"),
                new TeamRecordSeedEntry("fgm", "John Havlicek", 24, "1973-04-01"),
                new TeamRecordSeedEntry("fg3m", "Ray Allen", 9, "2009-04-30"),
                new TeamRecordSeedEntry("ftm", "Bob Cousy", 30, "1953-03-21"),
                new TeamRecordSeedEntry("turnovers", "Paul Pierce", 12, "2006-11-08")
            }
        },
        {
            "Brooklyn Nets",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Kyrie Irving", 60, "2022-03-15"),
                new TeamRecordSeedEntry("rebounds", "Buck Williams", 27, "1987-02-01"),
                new TeamRecordSeedEntry("assists", "Kevin Porter", 29, "1978-02-24"),
                new TeamRecordSeedEntry("steals", "Kendall Gill", 11, "1999-04-03"),
                new TeamRecordSeedEntry("blocks", "George Johnson", 9, "1979-01-01"),
                new TeamRecordSeedEntry("fgm", "Kyrie Irving", 20, "2022-03-15"),
                new TeamRecordSeedEntry("fg3m", "Kyrie Irving", 8, "2022-03-15"),
                new TeamRecordSeedEntry("ftm", "Kyrie Irving", 12, "2022-03-15"),
                new TeamRecordSeedEntry("turnovers", "Jason Kidd", 12, "2002-01-01")
            }
        },
        {
            "Charlotte Hornets",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Kemba Walker", 60, "2018-11-17"),
                new TeamRecordSeedEntry("rebounds", "Dwight Howard", 30, "2018-03-21"),
                new TeamRecordSeedEntry("assists", "Brevin Knight", 20, "2005-01-11"),
                new TeamRecordSeedEntry("steals", "Eddie Jones", 9, "1999-11-04"),
                new TeamRecordSeedEntry("blocks", "Alonzo Mourning", 9, "1994-01-01"),
                new TeamRecordSeedEntry("fgm", "Kemba Walker", 21, "2018-11-17"),
                new TeamRecordSeedEntry("fg3m", "Kelly Oubre Jr.", 10, "2022-01-26"),
                new TeamRecordSeedEntry("ftm", "Kemba Walker", 12, "2018-11-17"),
                new TeamRecordSeedEntry("turnovers", "Baron Davis", 13, "2002-01-01")
            }
        },
        {
            "Chicago Bulls",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Michael Jordan", 69, "1990-03-28"),
                new TeamRecordSeedEntry("rebounds", "Charles Oakley", 35, "1988-04-22"),
                new TeamRecordSeedEntry("assists", "Guy Rodgers", 24, "1966-10-21"),
                new TeamRecordSeedEntry("steals", "Michael Jordan", 10, "1988-01-29"),
                new TeamRecordSeedEntry("blocks", "Artis Gilmore", 11, "1977-12-20"),
                new TeamRecordSeedEntry("fgm", "Michael Jordan", 27, "1988-01-16"),
                new TeamRecordSeedEntry("fg3m", "Zach LaVine", 13, "2019-11-23"),
                new TeamRecordSeedEntry("ftm", "Michael Jordan", 26, "1987-02-26"),
                new TeamRecordSeedEntry("turnovers", "Artis Gilmore", 12, "1977-11-25")
            }
        },
        {
            "Cleveland Cavaliers",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Donovan Mitchell", 71, "2023-01-02"),
                new TeamRecordSeedEntry("rebounds", "Anderson Varejao", 25, "2014-01-02"),
                new TeamRecordSeedEntry("assists", "Geoff Huston", 27, "1982-01-27"),
                new TeamRecordSeedEntry("steals", "Ron Harper", 10, "1987-03-10"),
                new TeamRecordSeedEntry("blocks", "Larry Nance", 11, "1989-01-07"),
                new TeamRecordSeedEntry("fgm", "LeBron James", 23, "2017-11-03"),
                new TeamRecordSeedEntry("fg3m", "Kyrie Irving", 11, "2015-01-28"),
                new TeamRecordSeedEntry("ftm", "LeBron James", 24, "2006-03-12"),
                new TeamRecordSeedEntry("turnovers", "Ron Harper", 11, "1987-02-22")
            }
        },
        {
            "Dallas Mavericks",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Luka Dončić", 73, "2024-01-26"),
                new TeamRecordSeedEntry("rebounds", "Popeye Jones", 28, "1996-01-09"),
                new TeamRecordSeedEntry("assists", "Jason Kidd", 25, "1996-02-08"),
                new TeamRecordSeedEntry("steals", "Michael Finley", 10, "2001-01-23"),
                new TeamRecordSeedEntry("blocks", "Shawn Bradley", 13, "1998-04-07"),
                new TeamRecordSeedEntry("fgm", "Luka Dončić", 25, "2024-01-26"),
                new TeamRecordSeedEntry("fg3m", "Luka Dončić", 8, "2024-01-26"),
                new TeamRecordSeedEntry("ftm", "Luka Dončić", 15, "2024-01-26"),
                new TeamRecordSeedEntry("turnovers", "Jim Jackson", 10, "1994-01-01")
            }
        },
        {
            "Denver Nuggets",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "David Thompson", 73, "1978-04-09"),
                new TeamRecordSeedEntry("rebounds", "Dikembe Mutombo", 31, "1996-03-28"),
                new TeamRecordSeedEntry("assists", "Fat Lever", 23, "1989-04-21"),
                new TeamRecordSeedEntry("steals", "Fat Lever", 10, "1985-03-09"),
                new TeamRecordSeedEntry("blocks", "Dikembe Mutombo", 12, "1993-04-18"),
                new TeamRecordSeedEntry("fgm", "David Thompson", 28, "1978-04-09"),
                new TeamRecordSeedEntry("fg3m", "J. R. Smith", 11, "2009-04-13"),
                new TeamRecordSeedEntry("ftm", "David Thompson", 20, "1978-04-10"),
                new TeamRecordSeedEntry("turnovers", "Emmanuel Mudiay", 11, "2015-10-28")
            }
        },
        {
            "Detroit Pistons",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Jerry Stackhouse", 57, "2001-04-03"),
                new TeamRecordSeedEntry("rebounds", "Dennis Rodman", 34, "1992-03-04"),
                new TeamRecordSeedEntry("assists", "Kevin Porter", 25, "1979-03-09"),
                new TeamRecordSeedEntry("steals", "André Drummond", 11, "1986-12-02"),
                new TeamRecordSeedEntry("blocks", "Ben Wallace", 10, "2002-11-20"),
                new TeamRecordSeedEntry("fgm", "Kelly Tripucka", 21, "1983-01-29"),
                new TeamRecordSeedEntry("fg3m", "Saddiq Bey", 10, "2022-03-17"),
                new TeamRecordSeedEntry("ftm", "Jerry Stackhouse", 19, "2001-04-03"),
                new TeamRecordSeedEntry("turnovers", "Bob Lanier", 11, "1977-11-25")
            }
        },
        {
            "Golden State Warriors",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Wilt Chamberlain", 100, "1962-03-02"),
                new TeamRecordSeedEntry("rebounds", "Wilt Chamberlain", 55, "1960-11-24"),
                new TeamRecordSeedEntry("assists", "Guy Rodgers", 28, "1963-03-14"),
                new TeamRecordSeedEntry("steals", "Draymond Green", 10, "2017-02-10"),
                new TeamRecordSeedEntry("blocks", "Manute Bol", 13, "1990-02-02"),
                new TeamRecordSeedEntry("fgm", "Wilt Chamberlain", 36, "1962-03-02"),
                new TeamRecordSeedEntry("fg3m", "Klay Thompson", 14, "2018-10-29"),
                new TeamRecordSeedEntry("ftm", "Wilt Chamberlain", 28, "1962-03-02"),
                new TeamRecordSeedEntry("turnovers", "Chris Mullin", 13, "1988-03-31")
            }
        },
        {
            "Houston Rockets",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "James Harden", 61, "2019-03-22"),
                new TeamRecordSeedEntry("rebounds", "Moses Malone", 37, "1979-02-09"),
                new TeamRecordSeedEntry("assists", "Allen Leavell", 22, "1983-01-25"),
                new TeamRecordSeedEntry("steals", "Clyde Drexler", 10, "1996-11-01"),
                new TeamRecordSeedEntry("blocks", "Kevin Kunnert", 9, "1977-02-03"),
                new TeamRecordSeedEntry("fgm", "Calvin Murphy", 24, "1978-03-18"),
                new TeamRecordSeedEntry("fg3m", "Dillon Brooks", 10, "2025-01-24"),
                new TeamRecordSeedEntry("ftm", "James Harden", 24, "2019-03-22"),
                new TeamRecordSeedEntry("turnovers", "Moses Malone", 12, "1981-02-06")
            }
        },
        {
            "Indiana Pacers",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Tyrese Haliburton", 43, "2023-12-23"),
                new TeamRecordSeedEntry("rebounds", "Herb Williams", 29, "1989-01-23"),
                new TeamRecordSeedEntry("assists", "Tyrese Haliburton", 23, "2023-12-30"),
                new TeamRecordSeedEntry("steals", "T.J. McConnell", 10, "2021-03-03"),
                new TeamRecordSeedEntry("blocks", "Roy Hibbert", 11, "2012-11-21"),
                new TeamRecordSeedEntry("fgm", "Chuck Person", 21, "1989-04-21"),
                new TeamRecordSeedEntry("fg3m", "Tyrese Haliburton", 10, "2023-12-23"),
                new TeamRecordSeedEntry("ftm", "Reggie Miller", 21, "1993-11-28"),
                new TeamRecordSeedEntry("turnovers", "Billy Knight", 11, "1978-01-14")
            }
        },
        {
            "Los Angeles Clippers",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Kawhi Leonard", 55, "2020-03-08"),
                new TeamRecordSeedEntry("rebounds", "Swen Nater", 32, "1979-12-14"),
                new TeamRecordSeedEntry("assists", "Ernie DiGregorio", 25, "1974-01-01"),
                new TeamRecordSeedEntry("steals", "Lou Williams", 10, "2018-01-20"),
                new TeamRecordSeedEntry("blocks", "Benoit Benjamin", 10, "1989-03-31"),
                new TeamRecordSeedEntry("fgm", "Bob McAdoo", 22, "1974-02-22"),
                new TeamRecordSeedEntry("fg3m", "Robert Covington", 11, "2022-04-01"),
                new TeamRecordSeedEntry("ftm", "Bob McAdoo", 20, "1974-02-22"),
                new TeamRecordSeedEntry("turnovers", "Blake Griffin", 10, "2013-01-01")
            }
        },
        {
            "Los Angeles Lakers",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Kobe Bryant", 81, "2006-01-22"),
                new TeamRecordSeedEntry("rebounds", "Wilt Chamberlain", 42, "1969-03-07"),
                new TeamRecordSeedEntry("assists", "Magic Johnson", 24, "1984-11-03"),
                new TeamRecordSeedEntry("steals", "Jerry West", 10, "1966-12-10"),
                new TeamRecordSeedEntry("blocks", "Elmore Smith", 17, "1973-10-28"),
                new TeamRecordSeedEntry("fgm", "Wilt Chamberlain", 29, "1969-02-09"),
                new TeamRecordSeedEntry("fg3m", "Kobe Bryant", 12, "2003-01-07"),
                new TeamRecordSeedEntry("ftm", "Kobe Bryant", 18, "2006-01-22"),
                new TeamRecordSeedEntry("turnovers", "Magic Johnson", 9, "1987-03-01")
            }
        },
        {
            "Memphis Grizzlies",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Ja Morant", 52, "2022-02-28"),
                new TeamRecordSeedEntry("rebounds", "Lorenzen Wright", 26, "2001-11-04"),
                new TeamRecordSeedEntry("assists", "Jason Williams", 19, "2002-03-30"),
                new TeamRecordSeedEntry("steals", "Tony Allen", 8, "2012-04-23"),
                new TeamRecordSeedEntry("blocks", "Jaren Jackson Jr.", 8, "2022-12-12"),
                new TeamRecordSeedEntry("fgm", "Ja Morant", 22, "2022-02-28"),
                new TeamRecordSeedEntry("fg3m", "Luke Kennard", 10, "2023-03-24"),
                new TeamRecordSeedEntry("ftm", "Shareef Abdur-Rahim", 20, "1997-12-01"),
                new TeamRecordSeedEntry("turnovers", "Ja Morant", 9, "2022-01-31")
            }
        },
        {
            "Miami Heat",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "LeBron James", 61, "2014-03-03"),
                new TeamRecordSeedEntry("rebounds", "Rony Seikaly", 34, "1993-01-23"),
                new TeamRecordSeedEntry("assists", "Tim Hardaway", 19, "1996-04-19"),
                new TeamRecordSeedEntry("steals", "Mario Chalmers", 9, "2008-11-05"),
                new TeamRecordSeedEntry("blocks", "Hassan Whiteside", 12, "2015-01-25"),
                new TeamRecordSeedEntry("fgm", "LeBron James", 22, "2014-03-03"),
                new TeamRecordSeedEntry("fg3m", "Tyler Herro", 10, "2022-12-15"),
                new TeamRecordSeedEntry("ftm", "Dwyane Wade", 23, "2007-02-01"),
                new TeamRecordSeedEntry("turnovers", "Dwyane Wade", 12, "2007-02-01")
            }
        },
        {
            "Milwaukee Bucks",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Giannis Antetokounmpo", 64, "2023-12-13"),
                new TeamRecordSeedEntry("rebounds", "Swen Nater", 33, "1976-12-19"),
                new TeamRecordSeedEntry("assists", "Ramon Sessions", 24, "2008-04-14"),
                new TeamRecordSeedEntry("steals", "Alvin Robertson", 10, "1990-11-19"),
                new TeamRecordSeedEntry("blocks", "Larry Sanders", 10, "2012-11-30"),
                new TeamRecordSeedEntry("fgm", "Kareem Abdul-Jabbar", 24, "1971-01-27"),
                new TeamRecordSeedEntry("fg3m", "Ray Allen", 10, "2002-04-14"),
                new TeamRecordSeedEntry("ftm", "Giannis Antetokounmpo", 24, "2023-12-13"),
                new TeamRecordSeedEntry("turnovers", "Giannis Antetokounmpo", 12, "2023-01-04")
            }
        },
        {
            "Minnesota Timberwolves",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Karl-Anthony Towns", 62, "2024-01-22"),
                new TeamRecordSeedEntry("rebounds", "Kevin Love", 31, "2010-11-12"),
                new TeamRecordSeedEntry("assists", "Ricky Rubio", 19, "2017-03-13"),
                new TeamRecordSeedEntry("steals", "Corey Brewer", 8, "2014-04-11"),
                new TeamRecordSeedEntry("blocks", "Rasho Nesterovic", 9, "2003-03-10"),
                new TeamRecordSeedEntry("fgm", "Karl-Anthony Towns", 21, "2024-01-22"),
                new TeamRecordSeedEntry("fg3m", "Karl-Anthony Towns", 10, "2024-01-22"),
                new TeamRecordSeedEntry("ftm", "Kevin Love", 19, "2011-12-30"),
                new TeamRecordSeedEntry("turnovers", "Karl-Anthony Towns", 11, "2019-04-05")
            }
        },
        {
            "New Orleans Pelicans",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Anthony Davis", 59, "2016-02-21"),
                new TeamRecordSeedEntry("rebounds", "Anthony Davis", 26, "2019-01-02"),
                new TeamRecordSeedEntry("assists", "Rajon Rondo", 25, "2017-12-27"),
                new TeamRecordSeedEntry("steals", "Chris Paul", 9, "2008-02-20"),
                new TeamRecordSeedEntry("blocks", "Anthony Davis", 10, "2018-03-11"),
                new TeamRecordSeedEntry("fgm", "Anthony Davis", 24, "2016-02-21"),
                new TeamRecordSeedEntry("fg3m", "CJ McCollum", 11, "2022-12-30"),
                new TeamRecordSeedEntry("ftm", "Anthony Davis", 21, "2018-02-26"),
                new TeamRecordSeedEntry("turnovers", "DeMarcus Cousins", 12, "2017-10-26")
            }
        },
        {
            "New York Knicks",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Carmelo Anthony", 62, "2014-01-24"),
                new TeamRecordSeedEntry("rebounds", "Charles Oakley", 35, "1988-04-22"),
                new TeamRecordSeedEntry("assists", "Richie Guerin", 21, "1958-12-12"),
                new TeamRecordSeedEntry("steals", "Michael Ray Richardson", 9, "1980-12-23"),
                new TeamRecordSeedEntry("blocks", "Joe C. Meriweather", 10, "1977-12-12"),
                new TeamRecordSeedEntry("fgm", "Carmelo Anthony", 23, "2014-01-24"),
                new TeamRecordSeedEntry("fg3m", "Evan Fournier", 10, "2022-01-06"),
                new TeamRecordSeedEntry("ftm", "Richie Guerin", 21, "1961-02-11"),
                new TeamRecordSeedEntry("turnovers", "Ray Williams", 11, "1980-02-24")
            }
        },
        {
            "Oklahoma City Thunder",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Russell Westbrook", 58, "2017-03-07"),
                new TeamRecordSeedEntry("rebounds", "Jim Fox", 30, "1973-12-26"),
                new TeamRecordSeedEntry("assists", "Nate McMillan", 25, "1987-02-23"),
                new TeamRecordSeedEntry("steals", "Gus Williams", 10, "1978-02-22"),
                new TeamRecordSeedEntry("blocks", "Serge Ibaka", 11, "2012-02-19"),
                new TeamRecordSeedEntry("fgm", "Dale Ellis", 22, "1989-01-05"),
                new TeamRecordSeedEntry("fg3m", "Paul George", 10, "2019-02-11"),
                new TeamRecordSeedEntry("ftm", "Kevin Durant", 24, "2009-01-23"),
                new TeamRecordSeedEntry("turnovers", "Russell Westbrook", 11, "2017-01-23")
            }
        },
        {
            "Orlando Magic",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Tracy McGrady", 62, "2004-03-10"),
                new TeamRecordSeedEntry("rebounds", "Nikola Vucevic", 29, "2012-12-31"),
                new TeamRecordSeedEntry("assists", "Scott Skiles", 30, "1990-12-30"),
                new TeamRecordSeedEntry("steals", "Nick Anderson", 8, "1991-04-23"),
                new TeamRecordSeedEntry("blocks", "Shaquille O'Neal", 15, "1993-11-20"),
                new TeamRecordSeedEntry("fgm", "Tracy McGrady", 20, "2004-03-10"),
                new TeamRecordSeedEntry("fg3m", "Cole Anthony", 9, "2021-05-16"),
                new TeamRecordSeedEntry("ftm", "Dwight Howard", 21, "2012-01-12"),
                new TeamRecordSeedEntry("turnovers", "Penny Hardaway", 11, "1996-11-08")
            }
        },
        {
            "Philadelphia 76ers",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Wilt Chamberlain", 68, "1967-12-16"),
                new TeamRecordSeedEntry("rebounds", "Wilt Chamberlain", 43, "1965-03-06"),
                new TeamRecordSeedEntry("assists", "Maurice Cheeks", 21, "1982-10-30"),
                new TeamRecordSeedEntry("steals", "Allen Iverson", 10, "1999-05-13"),
                new TeamRecordSeedEntry("blocks", "Manute Bol", 10, "1991-02-14"),
                new TeamRecordSeedEntry("fgm", "Wilt Chamberlain", 30, "1967-12-16"),
                new TeamRecordSeedEntry("fg3m", "Tyrese Maxey", 10, "2023-10-28"),
                new TeamRecordSeedEntry("ftm", "Willie Burton", 24, "1994-12-13"),
                new TeamRecordSeedEntry("turnovers", "Allen Iverson", 12, "2005-03-08")
            }
        },
        {
            "Phoenix Suns",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Devin Booker", 70, "2017-03-24"),
                new TeamRecordSeedEntry("rebounds", "Paul Silas", 27, "1971-01-18"),
                new TeamRecordSeedEntry("assists", "Kevin Johnson", 25, "1994-04-06"),
                new TeamRecordSeedEntry("steals", "Kevin Johnson", 10, "1993-12-09"),
                new TeamRecordSeedEntry("blocks", "Amar'e Stoudemire", 10, "1977-11-25"),
                new TeamRecordSeedEntry("fgm", "Devin Booker", 21, "2017-03-24"),
                new TeamRecordSeedEntry("fg3m", "Grayson Allen", 9, "2024-01-05"),
                new TeamRecordSeedEntry("ftm", "Devin Booker", 24, "2017-03-24"),
                new TeamRecordSeedEntry("turnovers", "Jason Kidd", 14, "2000-11-17")
            }
        },
        {
            "Portland Trail Blazers",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Damian Lillard", 71, "2023-02-26"),
                new TeamRecordSeedEntry("rebounds", "Enes Kanter", 30, "2021-04-10"),
                new TeamRecordSeedEntry("assists", "Rod Strickland", 20, "1994-04-05"),
                new TeamRecordSeedEntry("steals", "Clyde Drexler", 10, "1985-01-10"),
                new TeamRecordSeedEntry("blocks", "Hassan Whiteside", 10, "2019-11-29"),
                new TeamRecordSeedEntry("fgm", "Damian Lillard", 22, "2023-02-26"),
                new TeamRecordSeedEntry("fg3m", "Damian Lillard", 13, "2023-02-26"),
                new TeamRecordSeedEntry("ftm", "Damian Lillard", 18, "2020-01-20"),
                new TeamRecordSeedEntry("turnovers", "Clyde Drexler", 11, "1985-02-21")
            }
        },
        {
            "Sacramento Kings",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Jack Twyman", 59, "1960-01-15"),
                new TeamRecordSeedEntry("rebounds", "Jerry Lucas", 40, "1964-02-29"),
                new TeamRecordSeedEntry("assists", "Rajon Rondo", 20, "2015-11-23"),
                new TeamRecordSeedEntry("steals", "Doug Christie", 9, "2002-12-08"),
                new TeamRecordSeedEntry("blocks", "Duane Causwell", 9, "1991-03-03"),
                new TeamRecordSeedEntry("fgm", "Jack Twyman", 21, "1960-01-15"),
                new TeamRecordSeedEntry("fg3m", "Buddy Hield", 11, "2019-11-25"),
                new TeamRecordSeedEntry("ftm", "Kevin Martin", 23, "2009-04-01"),
                new TeamRecordSeedEntry("turnovers", "Chris Webber", 12, "1999-11-10")
            }
        },
        {
            "San Antonio Spurs",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "David Robinson", 71, "1994-04-24"),
                new TeamRecordSeedEntry("rebounds", "Dennis Rodman", 32, "1993-12-01"),
                new TeamRecordSeedEntry("assists", "John Lucas", 24, "1984-04-15"),
                new TeamRecordSeedEntry("steals", "Alvin Robertson", 10, "1986-02-18"),
                new TeamRecordSeedEntry("blocks", "Victor Wembanyama", 10, "2024-02-12"),
                new TeamRecordSeedEntry("fgm", "David Robinson", 26, "1994-04-24"),
                new TeamRecordSeedEntry("fg3m", "Danny Green", 9, "2014-04-11"),
                new TeamRecordSeedEntry("ftm", "David Robinson", 18, "1994-04-24"),
                new TeamRecordSeedEntry("turnovers", "Artis Gilmore", 10, "1985-03-05")
            }
        },
        {
            "Toronto Raptors",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Fred VanVleet", 54, "2021-02-02"),
                new TeamRecordSeedEntry("rebounds", "Bismack Biyombo", 26, "2016-03-17"),
                new TeamRecordSeedEntry("assists", "Kyle Lowry", 19, "2021-02-26"),
                new TeamRecordSeedEntry("steals", "Doug Christie", 9, "1997-02-25"),
                new TeamRecordSeedEntry("blocks", "Keon Clark", 12, "2001-03-23"),
                new TeamRecordSeedEntry("fgm", "Fred VanVleet", 17, "2021-02-02"),
                new TeamRecordSeedEntry("fg3m", "Fred VanVleet", 11, "2021-02-02"),
                new TeamRecordSeedEntry("ftm", "Kyle Lowry", 21, "2016-02-26"),
                new TeamRecordSeedEntry("turnovers", "Damon Stoudamire", 11, "1997-10-31")
            }
        },
        {
            "Utah Jazz",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Karl Malone", 61, "1990-01-27"),
                new TeamRecordSeedEntry("rebounds", "Truck Robinson", 27, "1977-12-18"),
                new TeamRecordSeedEntry("assists", "John Stockton", 28, "1991-01-15"),
                new TeamRecordSeedEntry("steals", "Lafayette Lever", 8, "1990-11-27"),
                new TeamRecordSeedEntry("blocks", "Mark Eaton", 14, "1985-01-18"),
                new TeamRecordSeedEntry("fgm", "Karl Malone", 22, "1990-01-27"),
                new TeamRecordSeedEntry("fg3m", "Bojan Bogdanovic", 11, "2022-03-06"),
                new TeamRecordSeedEntry("ftm", "Adrian Dantley", 28, "1984-01-04"),
                new TeamRecordSeedEntry("turnovers", "John Stockton", 12, "1989-04-14")
            }
        },
        {
            "Washington Wizards",
            new List<TeamRecordSeedEntry>
            {
                new TeamRecordSeedEntry("points", "Bradley Beal", 60, "2021-01-06"),
                new TeamRecordSeedEntry("rebounds", "Elvin Hayes", 37, "1974-11-17"),
                new TeamRecordSeedEntry("assists", "Kevin Porter", 24, "1980-10-13"),
                new TeamRecordSeedEntry("steals", "Michael Jordan", 9, "2001-11-16"),
                new TeamRecordSeedEntry("blocks", "Manute Bol", 15, "1986-01-25"),
                new TeamRecordSeedEntry("fgm", "Phil Chenier", 22, "1972-12-06"),
                new TeamRecordSeedEntry("fg3m", "Corey Kispert", 9, "2023-04-01"),
                new TeamRecordSeedEntry("ftm", "Gilbert Arenas", 23, "2006-12-17"),
                new TeamRecordSeedEntry("turnovers", "Russell Westbrook", 12, "2021-03-12")
            }
        }
    };
}
