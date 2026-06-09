using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DraftGenerator
{
    private static readonly string[] FirstNames = {
        "James", "John", "Robert", "Michael", "William", "David", "Richard", "Joseph",
        "Thomas", "Charles", "Christopher", "Daniel", "Matthew", "Anthony", "Mark",
        "Donald", "Steven", "Paul", "Andrew", "Joshua", "Kenneth", "Kevin", "Brian",
        "George", "Timothy", "Ronald", "Edward", "Jason", "Jeffrey", "Ryan",
        "Jacob", "Gary", "Nicholas", "Eric", "Jonathan", "Stephen", "Larry",
        "Justin", "Scott", "Brandon", "Benjamin", "Samuel", "Raymond", "Gregory",
        "Frank", "Alexander", "Patrick", "Jack", "Dennis", "Jerry", "Tyler",
        "Aaron", "Adam", "Nathan", "Henry", "Zachary", "Douglas", "Peter",
        "Kyle", "Noah", "Ethan", "Jeremy", "Walter", "Christian", "Keith",
        "Roger", "Terry", "Austin", "Sean", "Gerald", "Carl", "Harold", "Dylan",
        "Arthur", "Lawrence", "Jordan", "Jesse", "Bryan", "Billy", "Bruce",
        "Gabriel", "Joe", "Logan", "Albert", "Willie", "Alan", "Wayne",
        "Elijah", "Randy", "Mason", "Vincent", "Liam", "Owen", "Lucas",
        "Isaac", "Hunter", "Caleb", "Connor", "Eli", "Isaiah", "Evan",
        "Chase", "Cameron", "Ian", "Cole", "Adrian", "Carson", "Gavin",
        "Wyatt", "Xavier", "Blake", "Brody", "Colton", "Caden", "Aiden",
        "Luka", "Nikola", "Giannis", "Rui", "Pascal", "Kristaps", "Bogdan",
        "Domantas", "Victor", "Shai", "Deni", "Jusuf", "Sekou", "Mamadi",
    };

    private static readonly string[] LastNames = {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
        "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson",
        "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson",
        "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson",
        "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres",
        "Nguyen", "Hill", "Flores", "Green", "Adams", "Nelson", "Baker", "Hall",
        "Rivera", "Campbell", "Mitchell", "Cruz", "Edwards", "Collins", "Reyes",
        "Stewart", "Morris", "Morales", "Murphy", "Cook", "Rogers", "Gutierrez",
        "Ortiz", "Morgan", "Cooper", "Peterson", "Bailey", "Reed", "Kelly",
        "Howard", "Ramos", "Kim", "Cox", "Ward", "Richardson", "Watson",
        "Brooks", "Chavez", "Wood", "James", "Bennett", "Gray", "Mendoza",
        "Ruiz", "Hughes", "Price", "Alvarez", "Castillo", "Sanders", "Patel",
        "Myers", "Long", "Ross", "Foster", "Jimenez", "Powell", "Jenkins",
        "Perry", "Russell", "Sullivan", "Bell", "Coleman", "Butler", "Henderson",
        "Barnes", "Fisher", "Vasquez", "Simmons", "Romero", "Jordan", "Patterson",
        "Alexander", "Hamilton", "Graham", "Reynolds", "Griffin", "Wallace", "Moreno",
        "Petrov", "Volkov", "Novak", "Kovac", "Horvat", "Papadopoulos", "Nakamura",
        "Tanaka", "Yamamoto", "Okonkwo", "Mensah", "Diallo", "Tremblay",
    };

    private static readonly string[] Positions = { "PG", "SG", "SF", "PF", "C" };

    private static readonly Dictionary<string, Dictionary<string, int>> PositionModifiers = new()
    {
        ["PG"] = new Dictionary<string, int> { {"speed", 8}, {"passing", 8}, {"ball_handling", 8}, {"iq", 5}, {"three_point", 3}, {"shooting", 2}, {"defense", 0}, {"athleticism", 2}, {"rebounding", -5}, {"blocks", -5}, {"steals", 3} },
        ["SG"] = new Dictionary<string, int> { {"speed", 5}, {"shooting", 8}, {"three_point", 8}, {"ball_handling", 5}, {"athleticism", 3}, {"passing", 2}, {"defense", 2}, {"iq", 2}, {"rebounding", -3}, {"blocks", -4}, {"steals", 2} },
        ["SF"] = new Dictionary<string, int> { {"speed", 4}, {"shooting", 5}, {"three_point", 5}, {"defense", 5}, {"athleticism", 5}, {"ball_handling", 3}, {"passing", 3}, {"iq", 3}, {"rebounding", 0}, {"blocks", -2}, {"steals", 2} },
        ["PF"] = new Dictionary<string, int> { {"defense", 6}, {"rebounding", 7}, {"athleticism", 5}, {"blocks", 4}, {"shooting", 2}, {"speed", 0}, {"passing", 0}, {"iq", 2}, {"ball_handling", -2}, {"three_point", 0}, {"steals", 0} },
        ["C"]  = new Dictionary<string, int> { {"rebounding", 10}, {"blocks", 10}, {"defense", 7}, {"athleticism", 3}, {"shooting", -3}, {"speed", -5}, {"passing", -5}, {"iq", 0}, {"ball_handling", -5}, {"three_point", -8}, {"steals", -3} },
    };

    private static readonly Dictionary<string, (int min, int max)> PositionHeight = new()
    {
        ["PG"] = (183, 193),
        ["SG"] = (188, 198),
        ["SF"] = (196, 206),
        ["PF"] = (201, 211),
        ["C"]  = (208, 218),
    };

    private static readonly Dictionary<string, (int min, int max)> PositionWeight = new()
    {
        ["PG"] = (77, 90),
        ["SG"] = (82, 95),
        ["SF"] = (90, 105),
        ["PF"] = (100, 115),
        ["C"]  = (108, 125),
    };

    public static List<PlayerData> GenerateDraft(SeasonData season, int managerId)
    {
        // Reset is_rookie for all existing players
        var allPlayers = DatabaseManager.Instance.GetAllTeams().SelectMany(t =>
            DatabaseManager.Instance.GetPlayersByTeam(t.id)).ToList();
        
        foreach (var p in allPlayers)
        {
            p.is_rookie = 0;
            DatabaseManager.Instance.UpdatePlayer(p);
        }

        // Get final standings (worst team picks first)
        var standings = GetFinalStandings(season, managerId);
        if (standings.Count < 30) return new List<PlayerData>();

        var draftOrder = standings.OrderBy(s => s.wins).Take(30).ToList();
        var draftedPlayers = new List<PlayerData>();

        for (int pick = 0; pick < 30; pick++)
        {
            var team = draftOrder[pick];
            
            // Pick 1 = ~80 avg, Pick 30 = ~60 avg
            float baseAvg = 75 - (pick * (15f / 29f));

            string position = Positions[UnityEngine.Random.Range(0, Positions.Length)];
            var modifiers = PositionModifiers[position];
            var heightRange = PositionHeight[position];
            var weightRange = PositionWeight[position];

            int height = UnityEngine.Random.Range(heightRange.min, heightRange.max + 1);
            int weight = UnityEngine.Random.Range(weightRange.min, weightRange.max + 1);
            int age = UnityEngine.Random.Range(19, 21);

            var attrs = new Dictionary<string, int>();
            foreach (var attr in new[] { "speed", "shooting", "three_point", "passing", "ball_handling",
                                          "defense", "rebounding", "athleticism", "iq", "steals", "blocks" })
            {
                int mod = modifiers.ContainsKey(attr) ? modifiers[attr] : 0;
                int value = (int)Mathf.Clamp(baseAvg + mod + UnityEngine.Random.Range(-5, 6), 30, 99);
                attrs[attr] = value;
            }

            int overall = (int)attrs.Values.Average();
            int potential = Mathf.Min(99, overall + UnityEngine.Random.Range(3, 13));

            string nationality = UnityEngine.Random.value < 0.9f ? "USA" : GetRandomNationality();

            string firstName = FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)];
            string lastName = LastNames[UnityEngine.Random.Range(0, LastNames.Length)];

            long salary = (long)(UnityEngine.Random.Range(3000000, 8000001));
            salary = (salary / 100000) * 100000;

            var player = new PlayerData
            {
                team_id = team.teamId,
                first_name = firstName,
                last_name = lastName,
                position = position,
                age = age,
                nationality = nationality,
                height_cm = height,
                weight_kg = weight,
                overall = overall,
                potential = potential,
                speed = attrs["speed"],
                shooting = attrs["shooting"],
                three_point = attrs["three_point"],
                passing = attrs["passing"],
                dribbling = attrs["ball_handling"],
                defense = attrs["defense"],
                rebounding = attrs["rebounding"],
                athleticism = attrs["athleticism"],
                iq = attrs["iq"],
                steals = attrs["steals"],
                blocks = attrs["blocks"],
                injury_days = 0,
                injury_type = "",
                salary = (int)salary,
                contract_years = 4,
                is_rookie = 1
            };

            DatabaseManager.Instance.Db.Insert(player);
            draftedPlayers.Add(player);
        }

        return draftedPlayers;
    }

    static string GetRandomNationality()
    {
        var nationalities = new[] {
            "GER", "BRA", "GMB", "TGO", "AUS", "PNG", "AUT", "BAH", "NGA", "BEL",
            "MLI", "BIH", "CMR", "CAN", "HTI", "CHN", "CRO", "SLO", "ESP", "FIN",
            "FRA", "BEN", "MTQ", "CIV", "GUI", "MAR", "COD", "SEN", "GEO", "GRE",
            "ISR", "ITA", "JAM", "JPN", "LAT", "LTU", "MNE", "TUR", "NZL", "NED",
            "POR", "GNB", "GBR", "POL", "CZE", "DOM", "RUS", "LCA", "SRB", "SWE",
            "SSD", "UGA", "SUI", "ANG", "UKR",
        };
        return nationalities[UnityEngine.Random.Range(0, nationalities.Length)];
    }

    static List<(int teamId, int wins, int losses)> GetFinalStandings(SeasonData season, int managerId)
    {
        var teams = DatabaseManager.Instance.GetAllTeams();
        var teamData = teams.ToDictionary(t => t.id, t => (teamId: t.id, wins: 0, losses: 0));

        var playedGames = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.game_type == "regular" && g.is_played == 1)
            .OrderBy(g => g.game_day)
            .ToList();

        foreach (var g in playedGames)
        {
            if (teamData.ContainsKey(g.home_team_id))
            {
                var homeEntry = teamData[g.home_team_id];
                if (g.home_score > g.away_score) homeEntry.wins++;
                else homeEntry.losses++;
                teamData[g.home_team_id] = homeEntry;
            }
            if (teamData.ContainsKey(g.away_team_id))
            {
                var awayEntry = teamData[g.away_team_id];
                if (g.away_score > g.home_score) awayEntry.wins++;
                else awayEntry.losses++;
                teamData[g.away_team_id] = awayEntry;
            }
        }

        var rows = teamData.Values.ToList();
        rows.Sort((a, b) =>
        {
            float pctA = a.wins + a.losses > 0 ? (float)a.wins / (a.wins + a.losses) : 0;
            float pctB = b.wins + b.losses > 0 ? (float)b.wins / (b.wins + b.losses) : 0;
            if (pctB != pctA) return pctB.CompareTo(pctA);
            if (a.losses != b.losses) return a.losses.CompareTo(b.losses);
            return b.wins.CompareTo(a.wins);
        });

        return rows;
    }
}
