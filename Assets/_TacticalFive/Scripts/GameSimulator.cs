using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public static class GameSimulator
{
    public class PlayerStatSnapshot
    {
        public int player_id;
        public string name;
        public string position;
        public int overall, shooting, three_point, passing, rebounding, defense, steals_attr, blocks_attr;
        public float minutes;
        public int fgm, fga, fg3m, fg3a, ftm, fta;
        public int oreb, dreb;
        public int assists, steals, blocks, turnovers, pf, points, rating;
        public int double_double, triple_double;
    }

    public class TeamStats
    {
        public int fgm, fga, fg3m, fg3a, ftm, fta, points;
        public int oreb, dreb, reb;
        public int assists, steals, blocks, turnovers, pf;
    }

    public class GameResult
    {
        public GameData game;
        public int home_score, away_score;
        public List<(int home, int away)> quarters = new();
        public List<PlayerStatSnapshot> home_stats = new();
        public List<PlayerStatSnapshot> away_stats = new();
        public TeamStats home_team_stats = new();
        public TeamStats away_team_stats = new();
        public List<(int player_id, string type, int days)> injuries = new();
    }

    public static GameResult SimulateGame(GameData game, List<PlayerData> homePlayers, List<PlayerData> awayPlayers,
        int homeChemistry = 50, int awayChemistry = 50, bool isHome = false)
    {
        var homePS = homePlayers.Where(p => p.injury_days == 0).Select(InitPS).ToList();
        var awayPS = awayPlayers.Where(p => p.injury_days == 0).Select(InitPS).ToList();

        if (homePS.Count < 2 || awayPS.Count < 2)
        {
            int homeScore = UnityEngine.Random.Range(105, 126);
            int awayScore = UnityEngine.Random.Range(100, 121);
            var qs = DistributeQuarters(homeScore, awayScore);
            game.home_score = homeScore;
            game.away_score = awayScore;
            game.is_played = 1;
            game.q1_home = qs[0].Item1; game.q1_away = qs[0].Item2;
            game.q2_home = qs[1].Item1; game.q2_away = qs[1].Item2;
            game.q3_home = qs[2].Item1; game.q3_away = qs[2].Item2;
            game.q4_home = qs[3].Item1; game.q4_away = qs[3].Item2;
            return new GameResult
            {
                game = game,
                home_score = homeScore,
                away_score = awayScore,
                quarters = qs,
                home_stats = homePS,
                away_stats = awayPS,
                home_team_stats = CalcTeamStats(homePS),
                away_team_stats = CalcTeamStats(awayPS)
            };
        }

        int homeR = (int)homePlayers.Average(p => Mathf.Clamp(p.overall + (p.morale - 50) * 0.1f, 0, 99));
        int awayR = (int)awayPlayers.Average(p => Mathf.Clamp(p.overall + (p.morale - 50) * 0.1f, 0, 99));

        // Apply chemistry bonus
        float homeChemBonus = (homeChemistry - 50) * 0.15f;
        float awayChemBonus = (awayChemistry - 50) * 0.10f;
        float homeCourtBonus = isHome ? 1.5f : 0;

        homeR = Mathf.Clamp(Mathf.RoundToInt(homeR + homeChemBonus + homeCourtBonus), 0, 99);
        awayR = Mathf.Clamp(Mathf.RoundToInt(awayR + awayChemBonus), 0, 99);
        float pace = Mathf.Clamp(98 + (homeR + awayR - 140) * 0.06f + UnityEngine.Random.Range(-2f, 2f), 92, 104);

        var quarters = new List<(int, int)>();
        int homeTotal = 0, awayTotal = 0;

        for (int q = 0; q < 4; q++)
        {
            var (hPts, aPts) = SimQuarter(q + 1, homeR, awayR, homePS, awayPS, pace);
            homeTotal += hPts; awayTotal += aPts;
            quarters.Add((hPts, aPts));
        }

        foreach (var ps in homePS.Concat(awayPS))
        {
            ps.minutes = Mathf.Round(ps.minutes);
            ps.rating = ps.points + ps.oreb + ps.dreb + ps.assists + ps.steals + ps.blocks
                        - (ps.fga - ps.fgm) - (ps.fta - ps.ftm) - ps.turnovers - ps.pf;
            int cats = 0;
            if (ps.points >= 10) cats++;
            if (ps.oreb + ps.dreb >= 10) cats++;
            if (ps.assists >= 10) cats++;
            if (ps.steals >= 10) cats++;
            if (ps.blocks >= 10) cats++;
            if (cats >= 3) { ps.triple_double = 1; ps.double_double = 0; }
            else if (cats >= 2) { ps.double_double = 1; ps.triple_double = 0; }
        }

        int otCount = 0;
        while (homeTotal == awayTotal && otCount < 5)
        {
            otCount++;
            var (hOT, aOT) = SimOvertime(homeR, awayR, homePS, awayPS);
            homeTotal += hOT; awayTotal += aOT;
            quarters.Add((hOT, aOT));
        }

        game.home_score = homeTotal;
        game.away_score = awayTotal;
        game.is_played = 1;

        // Guardar quarter scores en DB
        var qsArr = quarters.ToArray();
        game.q1_home = qsArr.Length > 0 ? qsArr[0].Item1 : 0;
        game.q1_away = qsArr.Length > 0 ? qsArr[0].Item2 : 0;
        game.q2_home = qsArr.Length > 1 ? qsArr[1].Item1 : 0;
        game.q2_away = qsArr.Length > 1 ? qsArr[1].Item2 : 0;
        game.q3_home = qsArr.Length > 2 ? qsArr[2].Item1 : 0;
        game.q3_away = qsArr.Length > 2 ? qsArr[2].Item2 : 0;
        game.q4_home = qsArr.Length > 3 ? qsArr[3].Item1 : 0;
        game.q4_away = qsArr.Length > 3 ? qsArr[3].Item2 : 0;

        // Guardar estadísticas para TODOS los tipos de partido
        // (la pantalla Stats filtra solo temporada regular)
        DatabaseManager.Instance.DeletePlayerGameStatsForGame(game.id);
        foreach (var ps in homePS)
            DatabaseManager.Instance.SavePlayerGameStats(PS2DB(ps, game.id, game.home_team_id));
        foreach (var ps in awayPS)
            DatabaseManager.Instance.SavePlayerGameStats(PS2DB(ps, game.id, game.away_team_id));

        // Check and update records (exclude All-Star — no records should count)
        if (game.game_type != "allstar")
        {
            DatabaseManager.Instance.CheckAndUpdateRecords(game, homePS, game.home_team_id);
            DatabaseManager.Instance.CheckAndUpdateRecords(game, awayPS, game.away_team_id);
        }

        var homeInjuries = CheckInjuries(homePS);
        var awayInjuries = CheckInjuries(awayPS);

        var result = new GameResult
        {
            game = game,
            home_score = homeTotal,
            away_score = awayTotal,
            quarters = quarters,
            home_stats = homePS,
            away_stats = awayPS,
            home_team_stats = CalcTeamStats(homePS),
            away_team_stats = CalcTeamStats(awayPS),
            injuries = homeInjuries.Concat(awayInjuries).ToList()
        };
        return result;
    }

    static PlayerStatSnapshot InitPS(PlayerData p)
    {
        return new PlayerStatSnapshot
        {
            player_id = p.id,
            name = $"{p.first_name} {p.last_name}",
            position = p.position,
            overall = p.overall,
            shooting = p.shooting,
            three_point = p.three_point,
            passing = p.passing,
            rebounding = p.rebounding,
            defense = p.defense,
            steals_attr = p.steals,
            blocks_attr = p.blocks,
            minutes = 0
        };
    }

    static PlayerGameStats PS2DB(PlayerStatSnapshot ps, int gameId, int teamId)
    {
        return new PlayerGameStats
        {
            game_id = gameId,
            player_id = ps.player_id,
            team_id = teamId,
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
            rebounds = ps.oreb + ps.dreb,
            assists = ps.assists,
            steals = ps.steals,
            blocks = ps.blocks,
            turnovers = ps.turnovers,
            pf = ps.pf,
            rating = ps.rating,
            double_double = ps.double_double,
            triple_double = ps.triple_double
        };
    }

    static (int, int) SimQuarter(int qNum, int homeR, int awayR, List<PlayerStatSnapshot> homePS, List<PlayerStatSnapshot> awayPS, float pace)
    {
        int homePts = 0, awayPts = 0;
        int teamPoss = Mathf.Clamp(Mathf.RoundToInt(pace / 4 * UnityEngine.Random.Range(0.96f, 1.04f)), 22, 28);
        int totalPoss = teamPoss * 2;

        var homeMins = new float[homePS.Count];
        var awayMins = new float[awayPS.Count];
        var homeOn = new HashSet<int>(Enumerable.Range(0, Mathf.Min(5, homePS.Count)));
        var awayOn = new HashSet<int>(Enumerable.Range(0, Mathf.Min(5, awayPS.Count)));
        var subSchedule = SubSchedule(qNum);
        int subIdx = 0;
        float minsPerPoss = 12f / totalPoss;

        for (int pIdx = 0; pIdx < totalPoss; pIdx++)
        {
            float elapsed = pIdx * minsPerPoss;
            if (subIdx < subSchedule.Length && elapsed >= subSchedule[subIdx])
            {
                homeOn = DoSub(homeOn, homePS.Count);
                awayOn = DoSub(awayOn, awayPS.Count);
                subIdx++;
            }
            foreach (int i in homeOn) homeMins[i] += minsPerPoss;
            foreach (int i in awayOn) awayMins[i] += minsPerPoss;

            if (UnityEngine.Random.value < 0.5f)
                homePts += RunPossession(homeOn, awayOn, homePS, awayPS, homeR, awayR);
            else
                awayPts += RunPossession(awayOn, homeOn, awayPS, homePS, awayR, homeR);
        }

        for (int i = 0; i < homePS.Count; i++) homePS[i].minutes += homeMins[i];
        for (int i = 0; i < awayPS.Count; i++) awayPS[i].minutes += awayMins[i];

        return (homePts, awayPts);
    }

    static (int, int) SimOvertime(int homeR, int awayR, List<PlayerStatSnapshot> homePS, List<PlayerStatSnapshot> awayPS)
    {
        int homePts = 0, awayPts = 0;
        int totalPoss = 24;
        var homeMins = new float[homePS.Count];
        var awayMins = new float[awayPS.Count];
        var homeOn = new HashSet<int>(Enumerable.Range(0, Mathf.Min(5, homePS.Count)));
        var awayOn = new HashSet<int>(Enumerable.Range(0, Mathf.Min(5, awayPS.Count)));
        float minsPerPoss = 5f / totalPoss;
        float[] subSchedule = { 2.5f };
        int subIdx = 0;

        for (int pIdx = 0; pIdx < totalPoss; pIdx++)
        {
            float elapsed = pIdx * minsPerPoss;
            if (subIdx < subSchedule.Length && elapsed >= subSchedule[subIdx])
            {
                homeOn = DoSub(homeOn, homePS.Count);
                awayOn = DoSub(awayOn, awayPS.Count);
                subIdx++;
            }
            foreach (int i in homeOn) homeMins[i] += minsPerPoss;
            foreach (int i in awayOn) awayMins[i] += minsPerPoss;

            if (UnityEngine.Random.value < 0.5f)
                homePts += RunPossession(homeOn, awayOn, homePS, awayPS, homeR, awayR);
            else
                awayPts += RunPossession(awayOn, homeOn, awayPS, homePS, awayR, homeR);
        }

        for (int i = 0; i < homePS.Count; i++) homePS[i].minutes += homeMins[i];
        for (int i = 0; i < awayPS.Count; i++) awayPS[i].minutes += awayMins[i];

        return (homePts, awayPts);
    }

    static int RunPossession(HashSet<int> offIds, HashSet<int> defIds, List<PlayerStatSnapshot> offAll, List<PlayerStatSnapshot> defAll, int offR, int defR)
    {
        var off = offIds.Select(i => offAll[i]).ToList();
        var def = defIds.Select(i => defAll[i]).ToList();

        float toPct = 0.07f + (defR - offR) * 0.0003f;
        if (UnityEngine.Random.value < toPct)
        {
            DoTO(off, def);
            return 0;
        }

        var shooter = PickShooter(off);
        if (shooter == null) return 0;

        string shot = ShotType(shooter);
        var defender = def.OrderByDescending(p => p.defense).FirstOrDefault();
        float di = defender != null ? (defender.defense - 70) * 0.002f : 0;

        if (shot == "3")
        {
            float basePct = 0.35f + (shooter.three_point - 70) * 0.005f;
            float pct = Mathf.Clamp(basePct - di, 0.30f, 0.50f);
            shooter.fg3a++; shooter.fga++;
            if (UnityEngine.Random.value < pct)
            {
                shooter.fg3m++; shooter.fgm++; shooter.points += 3;
                DoAst(off, shooter);
                return 3;
            }
            return MissHandler(def, off, shooter, true);
        }

        float base2Pct = 0.52f + (shooter.shooting - 70) * 0.005f;
        float pct2 = Mathf.Clamp(base2Pct - di * 0.4f, 0.45f, 0.72f);
        shooter.fga++;
        if (UnityEngine.Random.value < pct2)
        {
            shooter.fgm++; shooter.points += 2;
            DoAst(off, shooter);
            if (UnityEngine.Random.value < 0.06f)
            {
                DoFoul(def);
                shooter.fta++;
                if (UnityEngine.Random.value < 0.78f)
                {
                    shooter.ftm++; shooter.points++;
                    return 3;
                }
            }
            return 2;
        }
        return MissHandler(def, off, shooter, false);
    }

    static int MissHandler(List<PlayerStatSnapshot> def, List<PlayerStatSnapshot> off, PlayerStatSnapshot shooter, bool isThree)
    {
        foreach (var d in def)
        {
            float blockChance = Mathf.Clamp((d.blocks_attr - 50) / 500f, 0, 0.15f);
            if (UnityEngine.Random.value < blockChance)
            {
                d.blocks++;
                return 0;
            }
        }

        float foulChance = isThree ? 0.18f : 0.14f;
        if (UnityEngine.Random.value < foulChance)
        {
            DoFoul(def);
            int nShots = isThree ? 3 : 2;
            if (UnityEngine.Random.value < 0.10f && !isThree) nShots = 3;
            float ftPct = 0.78f + (shooter.overall - 70) * 0.002f;
            int made = 0;
            for (int i = 0; i < nShots; i++)
            {
                shooter.fta++;
                if (UnityEngine.Random.value < ftPct)
                {
                    shooter.ftm++; shooter.points++; made++;
                }
            }
            return made;
        }

        DoReb(def, off);
        return 0;
    }

    static PlayerStatSnapshot PickShooter(List<PlayerStatSnapshot> court)
    {
        if (court.Count == 0) return null;
        var weights = court.Select(p => p.overall >= 92 ? Mathf.Pow(p.overall, 1.7f) : Mathf.Pow(p.overall, 1.5f)).ToList();
        float total = weights.Sum();
        if (total <= 0) return court[UnityEngine.Random.Range(0, court.Count)];
        float r = UnityEngine.Random.value * total;
        float cum = 0;
        for (int i = 0; i < court.Count; i++)
        {
            cum += weights[i];
            if (r <= cum) return court[i];
        }
        return court[^1];
    }

    static string ShotType(PlayerStatSnapshot p)
    {
        float base3pt = p.position switch
        {
            "PG" => 0.36f,
            "SG" => 0.42f,
            "SF" => 0.35f,
            "PF" => 0.30f,
            "C" => 0.14f,
            _ => 0.30f
        };
        float adj = Mathf.Clamp(base3pt + (p.three_point - 75) * 0.002f, 0.10f, 0.55f);
        return UnityEngine.Random.value < adj ? "3" : "2";
    }

    static void DoAst(List<PlayerStatSnapshot> court, PlayerStatSnapshot scorer)
    {
        if (UnityEngine.Random.value >= 0.75f) return;
        var others = court.Where(p => p != scorer).ToList();
        if (others.Count == 0) return;
        var w = others.Select(p => Mathf.Max(1, p.passing)).ToList();
        float t = w.Sum();
        if (t <= 0) return;
        float r = UnityEngine.Random.value * t;
        float c = 0;
        for (int i = 0; i < others.Count; i++)
        {
            c += w[i];
            if (r <= c) { others[i].assists++; return; }
        }
    }

    static void DoReb(List<PlayerStatSnapshot> def, List<PlayerStatSnapshot> off)
    {
        float dw = def.Sum(p => Mathf.Pow(p.rebounding, 3));
        float ow = off.Sum(p => Mathf.Pow(p.rebounding, 2.5f));
        float t = dw + ow;
        if (t <= 0) return;

        if (UnityEngine.Random.value * t < dw)
            AwardReb(def, true);
        else
            AwardReb(off, false);
    }

    static void AwardReb(List<PlayerStatSnapshot> players, bool defensive)
    {
        var w = players.Select(p => Mathf.Pow(p.rebounding, 3)).ToList();
        float s = w.Sum();
        if (s <= 0) return;
        float r = UnityEngine.Random.value * s;
        float c = 0;
        for (int i = 0; i < players.Count; i++)
        {
            c += w[i];
            if (r <= c)
            {
                if (defensive) players[i].dreb++;
                else players[i].oreb++;
                return;
            }
        }
    }

    static void DoTO(List<PlayerStatSnapshot> off, List<PlayerStatSnapshot> def)
    {
        var handlers = off.Where(p => p.position is "PG" or "SG" or "SF").ToList();
        if (handlers.Count == 0) handlers = off;
        if (handlers.Count > 0)
            handlers[UnityEngine.Random.Range(0, handlers.Count)].turnovers++;

        if (def.Count == 0) return;
        var stlW = def.Select(p => Mathf.Pow(p.steals_attr, 3)).ToList();
        float t = stlW.Sum();
        if (t > 0)
        {
            float r = UnityEngine.Random.value * t;
            float c = 0;
            for (int i = 0; i < def.Count; i++)
            {
                c += stlW[i];
                if (r <= c) { def[i].steals++; return; }
            }
        }
        def.OrderByDescending(p => p.steals_attr).First().steals++;
    }

    static void DoFoul(List<PlayerStatSnapshot> court)
    {
        var bigs = court.Where(p => p.position is "C" or "PF").ToList();
        if (bigs.Count == 0) bigs = court;
        if (bigs.Count > 0)
            bigs[UnityEngine.Random.Range(0, bigs.Count)].pf++;
    }

    static HashSet<int> DoSub(HashSet<int> on, int maxPlayers)
    {
        int n = Mathf.Min(UnityEngine.Random.value < 0.5f ? 2 : 3, on.Count);
        if (n == 0) return on;
        var outList = on.OrderBy(_ => UnityEngine.Random.value).Take(n).ToList();
        var newSet = new HashSet<int>(on.Except(outList));
        var bench = Enumerable.Range(0, maxPlayers).Where(i => !on.Contains(i)).OrderBy(_ => UnityEngine.Random.value).ToList();
        foreach (var i in bench.Take(n))
            newSet.Add(i);
        return newSet;
    }

    static float[] SubSchedule(int qNum) => qNum switch
    {
        1 => new[] { 4f, 8f },
        2 => new[] { 2f, 5f, 8f, 10.5f },
        3 => new[] { 4f, 8f },
        _ => new[] { 2f, 5f, 8f, 10.5f }
    };

    static TeamStats CalcTeamStats(List<PlayerStatSnapshot> ps)
    {
        var ts = new TeamStats();
        foreach (var p in ps)
        {
            ts.fgm += p.fgm; ts.fga += p.fga;
            ts.fg3m += p.fg3m; ts.fg3a += p.fg3a;
            ts.ftm += p.ftm; ts.fta += p.fta;
            ts.oreb += p.oreb; ts.dreb += p.dreb;
            ts.assists += p.assists; ts.steals += p.steals;
            ts.blocks += p.blocks; ts.turnovers += p.turnovers;
            ts.pf += p.pf;
        }
        ts.reb = ts.oreb + ts.dreb;
        ts.points = ts.fg3m * 3 + (ts.fgm - ts.fg3m) * 2 + ts.ftm;
        return ts;
    }

    static List<(int, int)> DistributeQuarters(int homeTotal, int awayTotal)
    {
        var h = Partition(homeTotal, 4, 22, 40);
        var a = Partition(awayTotal, 4, 22, 40);
        return h.Zip(a, (x, y) => (x, y)).ToList();
    }

    static List<int> Partition(int total, int n, int lo, int hi)
    {
        var result = new List<int>();
        int remaining = total;
        for (int i = 0; i < n - 1; i++)
        {
            int low = Mathf.Max(lo, remaining - hi * (n - 1 - i));
            int high = Mathf.Min(hi, remaining - lo * (n - 1 - i));
            if (low > high)
            {
                low = Mathf.Max(lo, remaining / (n - i));
                high = low + 1;
            }
            int val = UnityEngine.Random.Range(low, high + 1);
            result.Add(val);
            remaining -= val;
        }
        result.Add(Mathf.Max(lo, remaining));
        return result;
    }

    static List<(int player_id, string type, int days)> CheckInjuries(List<PlayerStatSnapshot> players)
    {
        var injuries = new List<(int, string, int)>();
        foreach (var ps in players)
        {
            var player = DatabaseManager.Instance.GetPlayerById(ps.player_id);
            if (player == null || player.injury_days > 0) continue;
            if (UnityEngine.Random.value >= 0.008f) continue;

            var injury = PickInjury();
            player.injury_type = injury.type;
            player.injury_days = UnityEngine.Random.Range(injury.minDays, injury.maxDays + 1);
            DatabaseManager.Instance.UpdatePlayer(player);
            injuries.Add((ps.player_id, injury.type, player.injury_days));
        }
        return injuries;
    }

    static (string type, int minDays, int maxDays, int weight)[] INJURY_TYPES =
    {
        // Muy comunes
        ("Sobrecarga muscular", 1, 3, 60),
        ("Calambres musculares", 1, 2, 55),
        ("Golpe / contusión leve", 1, 4, 50),
        ("Esguince leve de tobillo", 3, 7, 40),
        ("Distensión muscular", 5, 10, 35),
        ("Tendinitis leve", 4, 8, 30),
        ("Lumbalgia", 3, 8, 28),

        // Comunes
        ("Esguince moderado de tobillo", 8, 14, 25),
        ("Contusión ósea", 10, 21, 20),
        ("Tendinitis rotuliana", 7, 14, 18),
        ("Fascitis plantar", 7, 15, 15),
        ("Esguince de muñeca", 7, 14, 15),
        ("Lesión en el gemelo", 10, 18, 14),
        ("Contractura muscular", 5, 12, 14),

        // Poco frecuentes
        ("Rotura fibrilar leve", 14, 21, 12),
        ("Subluxación de dedo", 10, 18, 10),
        ("Esguince grave de tobillo", 14, 28, 8),
        ("Luxación de hombro", 21, 35, 7),
        ("Fractura por estrés", 21, 35, 6),
        ("Rotura fibrilar grave", 21, 42, 5),

        // Graves
        ("Rotura parcial de ligamentos", 28, 49, 4),
        ("Rotura de ligamentos", 42, 90, 3),
        ("Rotura de menisco", 35, 70, 3),
        ("Fractura de mano", 30, 60, 2),
        ("Fractura de muñeca", 45, 75, 2),
        ("Rotura del tendón de Aquiles", 120, 240, 1),
        ("Rotura del ligamento cruzado anterior", 180, 300, 1),
    };

    static (string type, int minDays, int maxDays, int weight) PickInjury()
    {
        int total = INJURY_TYPES.Sum(i => i.weight);
        float r = UnityEngine.Random.value * total;
        float cum = 0;
        foreach (var inj in INJURY_TYPES)
        {
            cum += inj.weight;
            if (r <= cum) return inj;
        }
        return INJURY_TYPES[0];
    }
}
