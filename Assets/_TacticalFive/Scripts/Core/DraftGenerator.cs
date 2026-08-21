using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DraftGenerator
{
    public class DraftPickResult
    {
        public int PickNumber { get; set; }
        public TeamData Team { get; set; }
        public PlayerData Player { get; set; }
    }

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

    public static List<DraftPickResult> GenerateDraft(SeasonData season, int managerId)
    {
        // Reset is_rookie for all existing players
        var allPlayers = DatabaseManager.Instance.GetAllTeams().SelectMany(t =>
            DatabaseManager.Instance.GetPlayersByTeam(t.id)).ToList();
        
        foreach (var p in allPlayers)
        {
            p.is_rookie = 0;
            DatabaseManager.Instance.UpdatePlayer(p);
        }

        // Get final standings (best → worst)
        var standings = GetFinalStandings(season, managerId);
        if (standings.Count < 30) return new List<DraftPickResult>();

        // All teams sorted worst-first
        var teamsById = DatabaseManager.Instance.GetAllTeams().ToDictionary(t => t.id);
        var allTeamsOrdered = standings
            .OrderBy(s => s.wins)
            .ThenByDescending(s => s.losses)
            .Select(s => teamsById[s.teamId])
            .Where(t => t != null)
            .ToList();

        if (allTeamsOrdered.Count < 30) return new List<DraftPickResult>();

        // Lottery teams (14 worst) and non-lottery teams (16 best)
        var lotteryTeams = allTeamsOrdered.Take(14).ToList();
        var nonLotteryTeams = allTeamsOrdered.Skip(14).ToList();

        // Lottery odds (NBA 2024+): 14.0% top 3, descending to 0.5%
        double[] odds = {
            0.140, 0.140, 0.140, 0.125, 0.105,
            0.090, 0.075, 0.060, 0.045, 0.030,
            0.020, 0.015, 0.010, 0.005
        };

        // Build draft order: picks 1-14 via weighted lottery, 15-30 by record
        var draftOrder = new List<TeamData>[30];
        var available = new List<TeamData>(lotteryTeams);
        var usedOdds = odds.Take(lotteryTeams.Count).ToList();

        for (int pick = 0; pick < 14; pick++)
        {
            var pool = new List<TeamData>();
            for (int j = 0; j < available.Count; j++)
            {
                int entries = (int)(usedOdds[j] * 1000);
                for (int e = 0; e < entries; e++)
                    pool.Add(available[j]);
            }

            int idx = UnityEngine.Random.Range(0, pool.Count);
            var winner = pool[idx];
            draftOrder[pick] = new List<TeamData> { winner };
            int removedIdx = available.IndexOf(winner);
            available.RemoveAt(removedIdx);
            usedOdds.RemoveAt(removedIdx);
        }

        // Picks 15-30: non-lottery teams in worst-first order
        for (int pick = 14; pick < 30; pick++)
        {
            int idx = pick - 14;
            if (idx < nonLotteryTeams.Count)
                draftOrder[pick] = new List<TeamData> { nonLotteryTeams[idx] };
        }

        // Generate players in draft order (60 picks: 30 R1 + 30 R2)
        var draftedPlayers = new List<DraftPickResult>();

        int nextId = DatabaseManager.Instance.Db.Table<PlayerData>().Any()
            ? DatabaseManager.Instance.Db.Table<PlayerData>().Max(p => p.id) + 1
            : 1;

        // Draft class quality (rolled once per draft)
        float classQuality;
        string classLabel;
        float classRoll = UnityEngine.Random.value;
        if (classRoll < 0.15f) { classQuality = -3f; classLabel = "weak"; }
        else if (classRoll < 0.70f) { classQuality = 0f; classLabel = "normal"; }
        else if (classRoll < 0.90f) { classQuality = 2f; classLabel = "strong"; }
        else { classQuality = 4f; classLabel = "historic"; }

        // Pre-roll generational talents for historic drafts
        var generationalPicks = new HashSet<int>();
        if (classLabel == "historic")
        {
            int count = UnityEngine.Random.value < 0.5f ? 1 : 2;
            var candidates = Enumerable.Range(0, 14).OrderBy(_ => UnityEngine.Random.value).Take(count);
            foreach (var c in candidates) generationalPicks.Add(c);
        }

        var round1Owners = BuildSlotOwners(season, 1, draftOrder);
        var round2Owners = BuildSlotOwners(season, 2, draftOrder);

        for (int pick = 0; pick < 60; pick++)
        {
            bool isRound2 = pick >= 30;

            TeamData team;
            if (isRound2)
            {
                // Round 2: same order as round 1. Pick 30 (overall #31) goes
                // to the team that got pick 1 in round 1, pick 59 goes to
                // the team that got pick 30 in round 1.
                int r1Idx = pick - 30;
                team = draftOrder[r1Idx]?.FirstOrDefault();
            }
            else
            {
                team = draftOrder[pick]?.FirstOrDefault();
            }
            if (team == null) continue;

            // ─── BASE AVERAGE BY PICK ──────────────────────────────
            float baseAvg;
            if (isRound2)
            {
                baseAvg = 60f - ((pick - 30) * 10f / 29f);
            }
            else
            {
                baseAvg = pick switch
                {
                    0 => 83f + UnityEngine.Random.Range(-2f, 3f),
                    1 => 80f + UnityEngine.Random.Range(-2f, 3f),
                    2 => 78f + UnityEngine.Random.Range(-2f, 3f),
                    3 => 76f + UnityEngine.Random.Range(-2f, 3f),
                    4 => 75f + UnityEngine.Random.Range(-2f, 3f),
                    _ => 75f - (pick * 15f / 29f),
                };
            }
            baseAvg += classQuality;

            // ─── GENERATIONAL TALENT ────────────────────────────────
            bool isGenerational = false;
            if (classLabel == "historic")
                isGenerational = generationalPicks.Contains(pick);
            else if (pick < 5)
                isGenerational = UnityEngine.Random.value < 0.03f;
            else if (pick < 14)
                isGenerational = UnityEngine.Random.value < 0.01f;

            if (isGenerational)
                baseAvg = Mathf.Max(baseAvg, 86f + UnityEngine.Random.Range(-2f, 5f));

            // ─── POSITION ──────────────────────────────────────────
            string position = Positions[UnityEngine.Random.Range(0, Positions.Length)];
            var modifiers = PositionModifiers[position];
            var heightRange = PositionHeight[position];
            var weightRange = PositionWeight[position];

            int height = UnityEngine.Random.Range(heightRange.min, heightRange.max + 1);
            int weight = UnityEngine.Random.Range(weightRange.min, weightRange.max + 1);

            // ─── AGE VARIETY ────────────────────────────────────────
            int age;
            float ageRoll = UnityEngine.Random.value;
            if (ageRoll < 0.20f) age = UnityEngine.Random.Range(18, 20);
            else if (ageRoll < 0.80f) age = UnityEngine.Random.Range(19, 22);
            else age = UnityEngine.Random.Range(22, 25);

            // ─── TRAIT / ARCHETYPE (30%) ────────────────────────────
            bool hasTrait = UnityEngine.Random.value < 0.30f;
            string trait = "";
            if (hasTrait)
            {
                string[] traits = { "shooter", "scorer", "defender", "athlete", "playmaker", "rim_protector" };
                trait = traits[UnityEngine.Random.Range(0, traits.Length)];
            }

            // ─── ATTRIBUTES ─────────────────────────────────────────
            var attrs = new Dictionary<string, int>();
            foreach (var attr in new[] { "speed", "shooting", "three_point", "passing", "ball_handling",
                                           "defense", "rebounding", "athleticism", "iq", "steals", "blocks" })
            {
                int mod = modifiers.ContainsKey(attr) ? modifiers[attr] : 0;
                int roll = UnityEngine.Random.Range(-5, 6);

                int traitBonus = 0;
                if (hasTrait)
                {
                    traitBonus = trait switch
                    {
                        "shooter" when attr == "three_point" || attr == "shooting" => 8,
                        "scorer" when attr == "shooting" || attr == "ball_handling" => 8,
                        "defender" when attr == "defense" || attr == "steals" || attr == "blocks" => 8,
                        "athlete" when attr == "athleticism" || attr == "speed" => 8,
                        "playmaker" when attr == "passing" || attr == "iq" => 8,
                        "rim_protector" when attr == "blocks" || attr == "rebounding" => 8,
                        _ => 0
                    };
                }

                int value = (int)Mathf.Clamp(baseAvg + mod + roll + traitBonus, 30, 99);
                attrs[attr] = value;
            }

            int overall = (int)System.Math.Round(attrs.Values.Average());

            // ─── POTENTIAL BY PICK ──────────────────────────────────
            int potential;
            if (isGenerational)
                potential = Mathf.Min(99, overall + UnityEngine.Random.Range(5, 14));
            else if (!isRound2 && pick < 3)
                potential = Mathf.Min(99, overall + UnityEngine.Random.Range(8, 21));
            else if (!isRound2)
                potential = Mathf.Min(99, overall + UnityEngine.Random.Range(5, 17));
            else
                potential = Mathf.Min(99, overall + UnityEngine.Random.Range(3, 15));
            if (overall > potential) overall = potential;

            string nationality = UnityEngine.Random.value < 0.9f ? "USA" : GetRandomNationality();
            string college = NCAATeams.Names[UnityEngine.Random.Range(0, NCAATeams.Names.Length)];

            string firstName = FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)];
            string lastName = LastNames[UnityEngine.Random.Range(0, LastNames.Length)];

            long salary;
            if (isGenerational)
                salary = (long)UnityEngine.Random.Range(9000000, 15000001);
            else if (!isRound2 && pick < 3)
                salary = (long)UnityEngine.Random.Range(7000000, 12000001);
            else if (!isRound2)
                salary = (long)UnityEngine.Random.Range(3000000, 8000001);
            else
                salary = (long)UnityEngine.Random.Range(1500000, 4000001);
            salary = (salary / 100000) * 100000;

            int drafTeamId = team.id;
            var slotOwners = isRound2 ? round2Owners : round1Owners;
            if (slotOwners.TryGetValue(pick % 30, out var ownerTeam) && ownerTeam != null)
                drafTeamId = ownerTeam.id;

            // Rookies de 2ª ronda: contrato two-way si el equipo tiene plazas libres
            bool asTwoWay = false;
            if (isRound2)
            {
                var twoWayCount = DatabaseManager.Instance.Db.Table<PlayerData>()
                    .Count(p => p.team_id == drafTeamId && p.is_two_way == 1);
                asTwoWay = twoWayCount < TradeHelper.MAX_TWO_WAY;
            }

            var player = new PlayerData
            {
                id = nextId++,
                team_id = drafTeamId,
                first_name = firstName,
                last_name = lastName,
                position = position,
                secondary_position = position switch
                {
                    "PG" => "SG",
                    "SG" => height < 198 ? "PG" : "SF",
                    "SF" => "PF",
                    "PF" => "C",
                    "C"  => "PF",
                    _ => ""
                },
                age = age,
                nationality = nationality,
                college = college,
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
                treated = 0,
                salary = asTwoWay ? (int)TradeHelper.TWO_WAY_SALARY : (int)salary,
                contract_years = asTwoWay ? 2 : 4,
                guaranteed_years = asTwoWay ? 2 : 4,
                is_two_way = asTwoWay ? 1 : 0,
                is_rookie = 1
            };

            DatabaseManager.Instance.AssignJerseyNumber(player, drafTeamId);
            DatabaseManager.Instance.Db.Insert(player);
            PlayerPhotoHelper.CreateRookiePhoto(player.id);
            draftedPlayers.Add(new DraftPickResult
            {
                PickNumber = pick + 1,
                Team = drafTeamId == team.id ? team : (teamsById.ContainsKey(drafTeamId) ? teamsById[drafTeamId] : team),
                Player = player
            });
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

    // Resuelve qué equipo ejecuta cada slot (posición 1-30) de una ronda,
    // teniendo en cuenta transfers (current_team_id), protección top-N
    // (protected_from) y derechos de swap (is_swap / swap_original_team_id).
    static Dictionary<int, TeamData> BuildSlotOwners(SeasonData season, int round, List<TeamData>[] draftOrder)
    {
        var owners = new Dictionary<int, TeamData>();

        var picks = DatabaseManager.Instance.Db.Table<DraftPickData>()
            .Where(p => p.season_id == season.id && p.round == round)
            .ToList();

        var teamsById = DatabaseManager.Instance.GetAllTeams().ToDictionary(t => t.id);

        // Slot position -> pick (persistido) cuyo original_team_id coincide con el equipo del slot.
        for (int slot = 0; slot < draftOrder.Length; slot++)
        {
            var slotTeam = draftOrder[slot]?.FirstOrDefault();
            if (slotTeam == null) continue;

            var pick = picks.FirstOrDefault(p => p.original_team_id == slotTeam.id);
            TeamData owner = slotTeam;

            if (pick != null)
            {
                if (pick.protected_from > 0 && (slot + 1) <= pick.protected_from)
                {
                    // Pick protegido dentro del rango: revierte al equipo original.
                    owner = slotTeam;
                }
                else if (teamsById.TryGetValue(pick.current_team_id, out var current))
                {
                    owner = current;
                }
            }

            owners[slot] = owner;
        }

        // Swap: el poseedor (current_team_id) del pick marcado como swap se queda con el
        // slot mejor (menor índice) entre su pick y el del equipo partner; el propietario
        // del pick partner se lleva el peor. Aplica solo si la posición del partner es mejor.
        foreach (var swapPick in picks.Where(p => p.is_swap == 1))
        {
            if (swapPick.protected_from > 0) continue;

            var partner = picks.FirstOrDefault(p => p.original_team_id == swapPick.swap_original_team_id);
            if (partner == null) continue;

            int selfSlot = -1;
            int partnerSlot = -1;
            for (int i = 0; i < draftOrder.Length; i++)
            {
                var t = draftOrder[i]?.FirstOrDefault();
                if (t == null) continue;
                if (t.id == swapPick.original_team_id) selfSlot = i;
                if (t.id == partner.original_team_id) partnerSlot = i;
            }
            if (selfSlot == -1 || partnerSlot == -1 || selfSlot == partnerSlot) continue;

            var swapHolder = teamsById.TryGetValue(swapPick.current_team_id, out var sh)
                ? sh : slotById(swapPick.current_team_id);
            var partnerHolder = teamsById.TryGetValue(partner.current_team_id, out var ph)
                ? ph : slotById(partner.current_team_id);

            if (partnerSlot < selfSlot)
            {
                owners[partnerSlot] = swapHolder;
                owners[selfSlot] = partnerHolder;
            }
        }

        return owners;
    }

    static TeamData slotById(int teamId)
    {
        return DatabaseManager.Instance.GetAllTeams().FirstOrDefault(t => t.id == teamId);
    }
}
