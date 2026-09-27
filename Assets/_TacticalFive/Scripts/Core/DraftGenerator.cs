using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DraftGenerator
{
    public class DraftPickSlot
    {
        public int PickIndex { get; set; }       // 0-based (0..59)
        public int PickNumber { get; set; }      // 1-based (1..60)
        public TeamData Team { get; set; }
        public bool IsUserPick { get; set; }
        public bool IsRound2 { get; set; }
    }

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
            .Where(p => p.draft_year == season.year_end && p.round == round)
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
        int ownersBefore = CountDistinctOwners(owners);
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

            if (partnerSlot < selfSlot)
                ApplySwap(owners, selfSlot, partnerSlot);
        }

        // ECO-15: un swap debe ser una permutación de dos slots (nunca reduce el número de
        // dueños distintos). Guarda de regresión.
        int ownersAfter = CountDistinctOwners(owners);
        if (ownersAfter < ownersBefore)
            UnityEngine.Debug.LogWarning($"[Draft] Swap solapado: dueños distintos {ownersBefore} → {ownersAfter}");

        return owners;
    }

    /// <summary>
    /// Intercambia los dueños de los dos slots de un swap (ECO-15). Usar los dueños reales
    /// de los slots —no los `current_team_id` de los picks— garantiza que el intercambio
    /// es una permutación y no sobreescribe a un tercero.
    /// </summary>
    public static void ApplySwap(TeamData[] owners, int selfSlot, int partnerSlot)
    {
        if (owners == null) return;
        if (selfSlot < 0 || partnerSlot < 0 || selfSlot == partnerSlot) return;
        if (selfSlot >= owners.Length || partnerSlot >= owners.Length) return;
        (owners[selfSlot], owners[partnerSlot]) = (owners[partnerSlot], owners[selfSlot]);
    }

    /// <summary>Número de equipos distintos que poseen un pick en la ronda (puro).</summary>
    public static int CountDistinctOwners(TeamData[] owners)
    {
        if (owners == null) return 0;
        var seen = new System.Collections.Generic.HashSet<int>();
        foreach (var t in owners)
            if (t != null) seen.Add(t.id);
        return seen.Count;
    }

    // ── INTERACTIVE DRAFT SUPPORT ────────────────────────────────────

    public static (List<DraftPickSlot> order, string classLabel, float classQuality)
        GenerateDraftOrder(SeasonData season, int managerId, int userTeamId)
    {
        var standings = GetFinalStandings(season, managerId);
        var teamsById = DatabaseManager.Instance.GetAllTeams().ToDictionary(t => t.id);

        var allTeamsOrdered = standings
            .OrderBy(s => s.wins)
            .ThenByDescending(s => s.losses)
            .Select(s => teamsById[s.teamId])
            .Where(t => t != null)
            .ToList();

        var lotteryTeams = allTeamsOrdered.Take(14).ToList();
        var nonLotteryTeams = allTeamsOrdered.Skip(14).ToList();

        double[] odds = {
            0.140, 0.140, 0.140, 0.125, 0.105,
            0.090, 0.075, 0.060, 0.045, 0.030,
            0.020, 0.015, 0.010, 0.005
        };

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

        for (int pick = 14; pick < 30; pick++)
        {
            int idx = pick - 14;
            if (idx < nonLotteryTeams.Count)
                draftOrder[pick] = new List<TeamData> { nonLotteryTeams[idx] };
        }

        var round1Owners = BuildSlotOwners(season, 1, draftOrder);
        var round2Owners = BuildSlotOwners(season, 2, draftOrder);

        var order = new List<DraftPickSlot>();
        for (int pick = 0; pick < 60; pick++)
        {
            bool isRound2 = pick >= 30;
            TeamData team;
            if (isRound2)
                team = draftOrder[pick - 30]?.FirstOrDefault();
            else
                team = draftOrder[pick]?.FirstOrDefault();
            if (team == null) continue;

            int drafTeamId = team.id;
            var slotOwners = isRound2 ? round2Owners : round1Owners;
            if (slotOwners.TryGetValue(pick % 30, out var ownerTeam) && ownerTeam != null)
                drafTeamId = ownerTeam.id;

            var actualTeam = teamsById.ContainsKey(drafTeamId) ? teamsById[drafTeamId] : team;

            order.Add(new DraftPickSlot
            {
                PickIndex = pick,
                PickNumber = pick + 1,
                Team = actualTeam,
                IsUserPick = actualTeam.id == userTeamId,
                IsRound2 = isRound2
            });
        }

        float classQuality;
        string classLabel;
        float classRoll = UnityEngine.Random.value;
        if (classRoll < 0.15f) { classQuality = -3f; classLabel = "weak"; }
        else if (classRoll < 0.70f) { classQuality = 0f; classLabel = "normal"; }
        else if (classRoll < 0.90f) { classQuality = 2f; classLabel = "strong"; }
        else { classQuality = 4f; classLabel = "historic"; }

        return (order, classLabel, classQuality);
    }

    public static List<DraftProspect> GenerateProspectPool(string classLabel, float classQuality, int count = 75, int scoutReputation = 3)
    {
        var generationalPicks = new HashSet<int>();
        if (classLabel == "historic")
        {
            int genCount = UnityEngine.Random.value < 0.5f ? 1 : 2;
            var candidates = Enumerable.Range(0, 14).OrderBy(_ => UnityEngine.Random.value).Take(genCount);
            foreach (var c in candidates) generationalPicks.Add(c);
        }

        var prospects = new List<DraftProspect>();
        for (int i = 0; i < count; i++)
        {
            float baseAvg;
            if (i < 5)
                baseAvg = 83f - (i * 2f) + UnityEngine.Random.Range(-2f, 3f);
            else if (i < 14)
                baseAvg = 75f - ((i - 5) * 15f / 9f) + UnityEngine.Random.Range(-2f, 3f);
            else if (i < 30)
                baseAvg = 65f - ((i - 14) * 10f / 16f) + UnityEngine.Random.Range(-2f, 3f);
            else
                baseAvg = 58f - ((i - 30) * 15f / 44f) + UnityEngine.Random.Range(-3f, 3f);

            baseAvg += classQuality;

            bool isGenerational = generationalPicks.Contains(i);
            if (isGenerational)
                baseAvg = Mathf.Max(baseAvg, 86f + UnityEngine.Random.Range(-2f, 5f));
            else if (i < 5)
                isGenerational = UnityEngine.Random.value < 0.03f;
            else if (i < 14)
                isGenerational = UnityEngine.Random.value < 0.01f;
            if (isGenerational)
                baseAvg = Mathf.Max(baseAvg, 86f + UnityEngine.Random.Range(-2f, 5f));

            string position = Positions[UnityEngine.Random.Range(0, Positions.Length)];
            var modifiers = PositionModifiers[position];
            var heightRange = PositionHeight[position];
            var weightRange = PositionWeight[position];

            int height = UnityEngine.Random.Range(heightRange.min, heightRange.max + 1);
            int weight = UnityEngine.Random.Range(weightRange.min, weightRange.max + 1);

            int age;
            float ageRoll = UnityEngine.Random.value;
            if (ageRoll < 0.20f) age = UnityEngine.Random.Range(18, 20);
            else if (ageRoll < 0.80f) age = UnityEngine.Random.Range(19, 22);
            else age = UnityEngine.Random.Range(22, 25);

            bool hasTrait = UnityEngine.Random.value < 0.30f;
            string trait = "";
            if (hasTrait)
            {
                string[] traits = { "shooter", "scorer", "defender", "athlete", "playmaker", "rim_protector" };
                trait = traits[UnityEngine.Random.Range(0, traits.Length)];
            }

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
                attrs[attr] = (int)Mathf.Clamp(baseAvg + mod + roll + traitBonus, 30, 99);
            }

            int trueOverall = (int)System.Math.Round(attrs.Values.Average());
            // Evaluación con error de scouting: lo que se muestra (y usa la IA) es una
            // estimación; el valor real se revela al elegir el pick.
            int scoutRange = ScoutingHelper.MaxError(scoutReputation);
            int scoutError = ScoutingHelper.ErrorFor(scoutReputation,
                UnityEngine.Random.Range(-scoutRange, scoutRange + 1),
                UnityEngine.Random.Range(-scoutRange, scoutRange + 1));
            int overall = ScoutingHelper.Estimate(trueOverall, scoutError);

            int potential;
            if (isGenerational)
                potential = Mathf.Min(99, overall + UnityEngine.Random.Range(5, 14));
            else if (i < 3)
                potential = Mathf.Min(99, overall + UnityEngine.Random.Range(8, 21));
            else if (i < 30)
                potential = Mathf.Min(99, overall + UnityEngine.Random.Range(5, 17));
            else
                potential = Mathf.Min(99, overall + UnityEngine.Random.Range(3, 15));
            if (overall > potential) overall = potential;

            string nationality = NamePools.RandomPlayerNationality();
            string college = NCAATeams.Names[UnityEngine.Random.Range(0, NCAATeams.Names.Length)];
            var (firstName, lastName) = NamePools.Generate(nationality);

            prospects.Add(new DraftProspect
            {
                first_name = firstName,
                last_name = lastName,
                position = position,
                secondary_position = position switch
                {
                    "PG" => "SG",
                    "SG" => height < 198 ? "PG" : "SF",
                    "SF" => "PF",
                    "PF" => "C",
                    "C" => "PF",
                    _ => ""
                },
                age = age,
                nationality = nationality,
                college = college,
                height_cm = height,
                weight_kg = weight,
                overall = overall,
                potential = potential,
                scout_error = scoutError,
                scout_range = scoutRange,
                speed = ScoutingHelper.Estimate(attrs["speed"], scoutError),
                shooting = ScoutingHelper.Estimate(attrs["shooting"], scoutError),
                three_point = ScoutingHelper.Estimate(attrs["three_point"], scoutError),
                passing = ScoutingHelper.Estimate(attrs["passing"], scoutError),
                dribbling = ScoutingHelper.Estimate(attrs["ball_handling"], scoutError),
                defense = ScoutingHelper.Estimate(attrs["defense"], scoutError),
                rebounding = ScoutingHelper.Estimate(attrs["rebounding"], scoutError),
                athleticism = ScoutingHelper.Estimate(attrs["athleticism"], scoutError),
                iq = ScoutingHelper.Estimate(attrs["iq"], scoutError),
                steals = ScoutingHelper.Estimate(attrs["steals"], scoutError),
                blocks = ScoutingHelper.Estimate(attrs["blocks"], scoutError),
                trait = trait,
                is_generational = isGenerational
            });
        }

        return prospects.OrderByDescending(p => p.overall).ToList();
    }

    public static PlayerData AssignPick(DraftPickSlot slot, DraftProspect prospect, Dictionary<int, TeamData> teamsById)
    {
        int drafTeamId = slot.Team.id;

        bool isRound2 = slot.IsRound2;
        bool asTwoWay = false;
        if (isRound2)
        {
            var twoWayCount = DatabaseManager.Instance.Db.Table<PlayerData>()
                .Count(p => p.team_id == drafTeamId && p.is_two_way == 1);
            // ECO-14: también exige edad ≤ 23 (antes solo la ronda y el cupo).
            asTwoWay = twoWayCount < TradeHelper.MAX_TWO_WAY
                    && TradeHelper.IsEligibleForTwoWayAge(prospect.age);
        }

        long salary;
        if (prospect.is_generational)
            salary = (long)UnityEngine.Random.Range(9000000, 15000001);
        else if (!isRound2 && slot.PickNumber <= 3)
            salary = (long)UnityEngine.Random.Range(7000000, 12000001);
        else if (!isRound2)
            salary = (long)UnityEngine.Random.Range(3000000, 8000001);
        else
            salary = (long)UnityEngine.Random.Range(1500000, 4000001);
        salary = (salary / 100000) * 100000;

        int nextId = DatabaseManager.Instance.AllocatePlayerId();

        // Se revela el valor real: la estimación mostrada puede ser optimista (bust) o
        // pesimista (boom).
        int se = prospect.scout_error;
        var player = new PlayerData
        {
            id = nextId,
            team_id = drafTeamId,
            first_name = prospect.first_name,
            last_name = prospect.last_name,
            position = prospect.position,
            secondary_position = prospect.secondary_position,
            age = prospect.age,
            nationality = prospect.nationality,
            college = prospect.college,
            height_cm = prospect.height_cm,
            weight_kg = prospect.weight_kg,
            overall = ScoutingHelper.Reveal(prospect.overall, se),
            potential = ScoutingHelper.Reveal(prospect.potential, se),
            speed = ScoutingHelper.Reveal(prospect.speed, se),
            shooting = ScoutingHelper.Reveal(prospect.shooting, se),
            three_point = ScoutingHelper.Reveal(prospect.three_point, se),
            passing = ScoutingHelper.Reveal(prospect.passing, se),
            dribbling = ScoutingHelper.Reveal(prospect.dribbling, se),
            defense = ScoutingHelper.Reveal(prospect.defense, se),
            rebounding = ScoutingHelper.Reveal(prospect.rebounding, se),
            athleticism = ScoutingHelper.Reveal(prospect.athleticism, se),
            iq = ScoutingHelper.Reveal(prospect.iq, se),
            steals = ScoutingHelper.Reveal(prospect.steals, se),
            blocks = ScoutingHelper.Reveal(prospect.blocks, se),
            injury_days = 0,
            injury_type = "",
            treated = 0,
            salary = asTwoWay ? (int)TradeHelper.TWO_WAY_SALARY : (int)salary,
            contract_years = asTwoWay ? 2 : 4,
            guaranteed_years = asTwoWay ? 2 : 4,
            is_two_way = asTwoWay ? 1 : 0,
            is_rookie = 1
        };

        // El overall debe seguir siendo la media de los atributos (capada por potencial).
        player.RecalculateOverall();
        DatabaseManager.Instance.AssignJerseyNumber(player, drafTeamId);
        DatabaseManager.Instance.Db.Insert(player);
        PlayerPhotoHelper.CreateRookiePhoto(player.id);
        return player;
    }

    public static int AISelectProspect(List<DraftProspect> available, DraftPickSlot slot)
    {
        if (available.Count == 0) return 0;

        var team = slot.Team;
        var roster = DatabaseManager.Instance.GetPlayersByTeam(team.id);
        var posCounts = new Dictionary<string, int> { ["PG"] = 0, ["SG"] = 0, ["SF"] = 0, ["PF"] = 0, ["C"] = 0 };
        foreach (var p in roster)
        {
            if (posCounts.ContainsKey(p.position))
                posCounts[p.position]++;
        }

        string weakestPos = posCounts.OrderBy(kv => kv.Value).First().Key;

        for (int i = 0; i < available.Count; i++)
        {
            if (available[i].position == weakestPos && available[i].overall >= 70)
                return i;
        }

        return 0;
    }
}
