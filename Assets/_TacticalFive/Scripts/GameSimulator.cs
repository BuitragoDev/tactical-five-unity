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
        public string secondary_position;
        public PlayerRole role;
        public bool starter;
        public int target_minutes;
        public int overall, shooting, three_point, passing, rebounding, defense, steals_attr, blocks_attr;
        public float minutes;
        public int fgm, fga, fg3m, fg3a, ftm, fta;
        public int oreb, dreb;
        public int assists, steals, blocks, turnovers, pf, points, rating;
        public int double_double, triple_double;
        public int fisico;
    }

    public class TeamStats
    {
        public int fgm, fga, fg3m, fg3a, ftm, fta, points;
        public int oreb, dreb, reb;
        public int assists, steals, blocks, turnovers, pf;
    }

    public class PlayByPlayEvent
    {
        public int quarter;
        public string text;
        public int homeScore;
        public int awayScore;
        public float timeElapsed;
        public List<StatDelta> deltas;
    }

    public class StatDelta
    {
        public int player_id;
        public string stat;
        public float amount;
    }

    public class PossessionOutcome
    {
        public int pts;
        public string desc;
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
        public List<PlayByPlayEvent> playByPlay = new();
    }

    public static GameResult SimulateGame(GameData game, List<PlayerData> homePlayers, List<PlayerData> awayPlayers,
        int homeChemistry = 50, int awayChemistry = 50, bool isHome = false, bool persistToDb = true, bool glLeague = false)
    {
        var homePS = homePlayers.Where(p => p.injury_days == 0).Select(InitPS).ToList();
        var awayPS = awayPlayers.Where(p => p.injury_days == 0).Select(InitPS).ToList();
        PrepareRotationMetadata(homePS, glLeague);
        PrepareRotationMetadata(awayPS, glLeague);

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
        float pace = Mathf.Clamp(101 + (homeR + awayR - 140) * 0.06f + UnityEngine.Random.Range(-2f, 2f), 95, 107);

        var quarters = new List<(int, int)>();
        var playByPlay = new List<PlayByPlayEvent>();
        int homeTotal = 0, awayTotal = 0;

        for (int q = 0; q < 4; q++)
        {
            playByPlay.Add(new PlayByPlayEvent
            {
                quarter = q + 1,
                text = $"Comienza el cuarto {q + 1}",
                homeScore = homeTotal,
                awayScore = awayTotal,
                timeElapsed = 0
            });
            var (hPts, aPts) = SimQuarter(q + 1, homeR, awayR, homePS, awayPS, pace, playByPlay, homeTotal, awayTotal, glLeague);
            homeTotal += hPts; awayTotal += aPts;
            quarters.Add((hPts, aPts));
        }

        int otCount = 0;
        while (homeTotal == awayTotal && otCount < 5)
        {
            otCount++;
            playByPlay.Add(new PlayByPlayEvent
            {
                quarter = 4 + otCount,
                text = "¡A la prórroga!",
                homeScore = homeTotal,
                awayScore = awayTotal,
                timeElapsed = 0
            });
            var (hOT, aOT) = SimOvertime(4 + otCount, homeR, awayR, homePS, awayPS, playByPlay, homeTotal, awayTotal, glLeague);
            homeTotal += hOT; awayTotal += aOT;
            quarters.Add((hOT, aOT));
        }

        playByPlay.Add(new PlayByPlayEvent
        {
            quarter = 4 + otCount,
            text = "¡Fin del partido!",
            homeScore = homeTotal,
            awayScore = awayTotal,
            timeElapsed = 4 + otCount <= 4 ? 12 : 5
        });

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

        // Suelo garantizado para atributos elite (tras simulacion)
        foreach (var ps in homePS.Concat(awayPS))
        {
            if (ps.minutes < 20) continue;
            if (ps.passing >= 95)
                ps.assists = Mathf.Max(ps.assists, Mathf.RoundToInt(ps.minutes / 48f * 10));
            if (ps.rebounding >= 95)
            {
                int curReb = ps.oreb + ps.dreb;
                int floorReb = Mathf.RoundToInt(ps.minutes / 48f * 10f
                    * GetReboundPositionMultiplier(ps.position, false));
                if (curReb < floorReb)
                {
                    int extra = floorReb - curReb;
                    ps.oreb += extra / 2;
                    ps.dreb += extra - extra / 2;
                }
            }
            if (ps.steals_attr >= 90)
                ps.steals = Mathf.Max(ps.steals, Mathf.RoundToInt(ps.minutes / 48f * 3));
            if (ps.blocks_attr >= 95)
            {
                int floorBlocks = Mathf.RoundToInt(ps.minutes / 48f * 3f
                    * GetBlockPositionMultiplier(ps.position));
                ps.blocks = Mathf.Max(ps.blocks, floorBlocks);
            }
        }

        // Calcular rating y dobles-dobles DESPUES del suelo de stats
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

        // Guardar estadísticas para TODOS los tipos de partido
        // (la pantalla Stats filtra solo temporada regular).
        // Los partidos G-League no escriben en player_game_stats: sus stats
        // viven en gleague_season_stats y las gestiona el llamador.
        if (persistToDb)
        {
            DatabaseManager.Instance.DeletePlayerGameStatsForGame(game.id);
            foreach (var ps in homePS)
                DatabaseManager.Instance.SavePlayerGameStats(PS2DB(ps, game.id, game.home_team_id));
            foreach (var ps in awayPS)
                DatabaseManager.Instance.SavePlayerGameStats(PS2DB(ps, game.id, game.away_team_id));
        }

        // Check and update records (exclude All-Star — no records should count)
        if (persistToDb && game.game_type != "allstar")
        {
            DatabaseManager.Instance.CheckAndUpdateRecords(game, homePS, game.home_team_id);
            DatabaseManager.Instance.CheckAndUpdateRecords(game, awayPS, game.away_team_id);
        }

        // Fatiga post-partido (los partidos G-League no desgastan el físico:
        // los prospectos ni existen en players, y a los asignados no se les
        // aplica doble desgaste por jugar en dos competiciones)
        if (persistToDb)
        {
            bool homeBackToBack = DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => (g.home_team_id == game.home_team_id || g.away_team_id == game.home_team_id)
                       && g.is_played == 1 && g.game_day == game.game_day - 1);
            bool awayBackToBack = DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => (g.home_team_id == game.away_team_id || g.away_team_id == game.away_team_id)
                       && g.is_played == 1 && g.game_day == game.game_day - 1);
            foreach (var ps in homePS)
            {
                var player = DatabaseManager.Instance.GetPlayerById(ps.player_id);
                if (player == null) continue;
                int loss = Mathf.RoundToInt(ps.minutes * 0.30f);
                if (homeBackToBack) loss = Mathf.RoundToInt(loss * 1.5f);
                player.fisico = Mathf.Max(0, player.fisico - loss);
                DatabaseManager.Instance.UpdatePlayer(player);
            }
            foreach (var ps in awayPS)
            {
                var player = DatabaseManager.Instance.GetPlayerById(ps.player_id);
                if (player == null) continue;
                int loss = Mathf.RoundToInt(ps.minutes * 0.30f);
                if (awayBackToBack) loss = Mathf.RoundToInt(loss * 1.5f);
                player.fisico = Mathf.Max(0, player.fisico - loss);
                DatabaseManager.Instance.UpdatePlayer(player);
            }
        }

        var homeInjuries = persistToDb ? CheckInjuries(homePS) : new List<(int player_id, string type, int days)>();
        var awayInjuries = persistToDb ? CheckInjuries(awayPS) : new List<(int player_id, string type, int days)>();

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
            injuries = homeInjuries.Concat(awayInjuries).ToList(),
            playByPlay = playByPlay
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
            secondary_position = p.secondary_position,
            role = p.role,
            overall = p.overall,
            shooting = p.shooting,
            three_point = p.three_point,
            passing = p.passing,
            rebounding = p.rebounding,
            defense = p.defense,
            steals_attr = p.steals,
            blocks_attr = p.blocks,
            minutes = 0,
            fisico = p.fisico
        };
    }

    static void PrepareRotationMetadata(List<PlayerStatSnapshot> players, bool glLeague = false)
    {
        for (int i = 0; i < players.Count; i++)
        {
            players[i].starter = i < 5;
            players[i].target_minutes = GetTargetMinutes(players[i], glLeague);
            if (players[i].fisico < 30)
                players[i].target_minutes = Mathf.Max(8, players[i].target_minutes - 6);
        }
    }

    public static int GetTargetMinutes(PlayerStatSnapshot player, bool glLeague = false)
    {
        bool isStar = player.role == PlayerRole.Estrella || player.overall >= 85;
        // En G-League la estrella juega menos minutos aún, repartiendo carga con más banquillo.
        if (isStar) return glLeague ? (player.starter ? 31 : 22) : (player.starter ? 39 : 28);
        if (player.starter)
            return player.role == PlayerRole.Titular || player.overall >= 78 ? 31 : 27;
        if (player.role == PlayerRole.Titular || player.overall >= 75) return 21;
        if (player.role == PlayerRole.Banquillo) return 15;
        return 8;
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
            triple_double = ps.triple_double,
            fisico_start = ps.fisico
        };
    }

    static (int, int) SimQuarter(int qNum, int homeR, int awayR, List<PlayerStatSnapshot> homePS, List<PlayerStatSnapshot> awayPS, float pace, List<PlayByPlayEvent> log, int baseHome = 0, int baseAway = 0, bool glLeague = false)
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
                homeOn = DoSub(homeOn, homePS, homeMins);
                awayOn = DoSub(awayOn, awayPS, awayMins);
                subIdx++;
            }
            foreach (int i in homeOn) homeMins[i] += minsPerPoss;
            foreach (int i in awayOn) awayMins[i] += minsPerPoss;

            if (UnityEngine.Random.value < 0.5f)
            {
                var before = CaptureBox(homePS, awayPS);
                var outHome = RunPossession(homeOn, awayOn, homePS, awayPS, homeR, awayR, glLeague);
                homePts += outHome.pts;
                var after = CaptureBox(homePS, awayPS);
                var deltas = DiffBox(before, after);
                foreach (int i in homeOn) deltas.Add(new StatDelta { player_id = homePS[i].player_id, stat = "min", amount = minsPerPoss });
                foreach (int i in awayOn) deltas.Add(new StatDelta { player_id = awayPS[i].player_id, stat = "min", amount = minsPerPoss });
                AddLog(log, qNum, outHome.desc, baseHome + homePts, baseAway + awayPts, elapsed, deltas);
            }
            else
            {
                var before = CaptureBox(homePS, awayPS);
                var outAway = RunPossession(awayOn, homeOn, awayPS, homePS, awayR, homeR, glLeague);
                awayPts += outAway.pts;
                var after = CaptureBox(homePS, awayPS);
                var deltas = DiffBox(before, after);
                foreach (int i in homeOn) deltas.Add(new StatDelta { player_id = homePS[i].player_id, stat = "min", amount = minsPerPoss });
                foreach (int i in awayOn) deltas.Add(new StatDelta { player_id = awayPS[i].player_id, stat = "min", amount = minsPerPoss });
                AddLog(log, qNum, outAway.desc, baseHome + homePts, baseAway + awayPts, elapsed, deltas);
            }
        }

        for (int i = 0; i < homePS.Count; i++) homePS[i].minutes += homeMins[i];
        for (int i = 0; i < awayPS.Count; i++) awayPS[i].minutes += awayMins[i];

        return (homePts, awayPts);
    }

    static (int, int) SimOvertime(int qNum, int homeR, int awayR, List<PlayerStatSnapshot> homePS, List<PlayerStatSnapshot> awayPS, List<PlayByPlayEvent> log, int baseHome = 0, int baseAway = 0, bool glLeague = false)
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
                homeOn = DoSub(homeOn, homePS, homeMins);
                awayOn = DoSub(awayOn, awayPS, awayMins);
                subIdx++;
            }
            foreach (int i in homeOn) homeMins[i] += minsPerPoss;
            foreach (int i in awayOn) awayMins[i] += minsPerPoss;

            if (UnityEngine.Random.value < 0.5f)
            {
                var before = CaptureBox(homePS, awayPS);
                var outHome = RunPossession(homeOn, awayOn, homePS, awayPS, homeR, awayR, glLeague);
                homePts += outHome.pts;
                var after = CaptureBox(homePS, awayPS);
                var deltas = DiffBox(before, after);
                foreach (int i in homeOn) deltas.Add(new StatDelta { player_id = homePS[i].player_id, stat = "min", amount = minsPerPoss });
                foreach (int i in awayOn) deltas.Add(new StatDelta { player_id = awayPS[i].player_id, stat = "min", amount = minsPerPoss });
                AddLog(log, qNum, outHome.desc, baseHome + homePts, baseAway + awayPts, elapsed, deltas);
            }
            else
            {
                var before = CaptureBox(homePS, awayPS);
                var outAway = RunPossession(awayOn, homeOn, awayPS, homePS, awayR, homeR, glLeague);
                awayPts += outAway.pts;
                var after = CaptureBox(homePS, awayPS);
                var deltas = DiffBox(before, after);
                foreach (int i in homeOn) deltas.Add(new StatDelta { player_id = homePS[i].player_id, stat = "min", amount = minsPerPoss });
                foreach (int i in awayOn) deltas.Add(new StatDelta { player_id = awayPS[i].player_id, stat = "min", amount = minsPerPoss });
                AddLog(log, qNum, outAway.desc, baseHome + homePts, baseAway + awayPts, elapsed, deltas);
            }
        }

        for (int i = 0; i < homePS.Count; i++) homePS[i].minutes += homeMins[i];
        for (int i = 0; i < awayPS.Count; i++) awayPS[i].minutes += awayMins[i];

        return (homePts, awayPts);
    }

    static float FisicoPenalty(int fisico)
    {
        if (fisico >= 30) return 1f;
        return 0.75f + (fisico / 30f) * 0.25f;
    }

    /// <summary>Factor de compresión para la G-League: acerca los pesos de uso/rebote
    /// hacia la media del quinteto, evitando que una estrella NBA (OVR ~80) domine por
    /// completo frente a prospectos (~50) — p.ej. 40 PPP / 18 RPP. 0 = reparto
    /// totalmente igualitario (~20% cada uno); 1 = sin cambios.</summary>
    const float GL_COMPRESS = 0.2f;

    /// <summary>Acerca cada peso hacia la media de la lista (conserva el orden pero
    /// reduce el abismo estrella/resto). Solo se usa en partidos G-League.</summary>
    static void CompressWeights(List<float> weights, float factor)
    {
        if (weights == null || weights.Count < 2) return;
        float mean = weights.Sum() / weights.Count;
        for (int i = 0; i < weights.Count; i++)
            weights[i] = Mathf.Max(0.0001f, mean + (weights[i] - mean) * factor);
    }

    static void AddLog(List<PlayByPlayEvent> log, int quarter, string desc, int homeScore, int awayScore, float elapsed, List<StatDelta> deltas = null)
    {
        if (log == null || string.IsNullOrEmpty(desc)) return;
        log.Add(new PlayByPlayEvent
        {
            quarter = quarter,
            text = desc,
            homeScore = homeScore,
            awayScore = awayScore,
            timeElapsed = elapsed,
            deltas = deltas
        });
    }

    struct BoxSnapshot
    {
        public int player_id;
        public float minutes;
        public int points, fgm, fga, fg3m, fg3a, ftm, fta, oreb, dreb, assists, steals, blocks, turnovers, pf;
    }

    static BoxSnapshot SnapPlayer(PlayerStatSnapshot p)
    {
        return new BoxSnapshot
        {
            player_id = p.player_id,
            minutes = p.minutes,
            points = p.points, fgm = p.fgm, fga = p.fga,
            fg3m = p.fg3m, fg3a = p.fg3a, ftm = p.ftm, fta = p.fta,
            oreb = p.oreb, dreb = p.dreb, assists = p.assists,
            steals = p.steals, blocks = p.blocks, turnovers = p.turnovers, pf = p.pf
        };
    }

    static List<BoxSnapshot> CaptureBox(List<PlayerStatSnapshot> homePS, List<PlayerStatSnapshot> awayPS)
    {
        var snap = new List<BoxSnapshot>(homePS.Count + awayPS.Count);
        for (int i = 0; i < homePS.Count; i++) snap.Add(SnapPlayer(homePS[i]));
        for (int i = 0; i < awayPS.Count; i++) snap.Add(SnapPlayer(awayPS[i]));
        return snap;
    }

    static List<StatDelta> DiffBox(List<BoxSnapshot> before, List<BoxSnapshot> after)
    {
        var deltas = new List<StatDelta>();
        if (before == null || after == null || before.Count != after.Count) return deltas;
        for (int i = 0; i < before.Count; i++)
        {
            var b = before[i]; var a = after[i];
            AddD(deltas, a.player_id, "pts", a.points - b.points);
            AddD(deltas, a.player_id, "fgm", a.fgm - b.fgm);
            AddD(deltas, a.player_id, "fga", a.fga - b.fga);
            AddD(deltas, a.player_id, "fg3m", a.fg3m - b.fg3m);
            AddD(deltas, a.player_id, "fg3a", a.fg3a - b.fg3a);
            AddD(deltas, a.player_id, "ftm", a.ftm - b.ftm);
            AddD(deltas, a.player_id, "fta", a.fta - b.fta);
            AddD(deltas, a.player_id, "oreb", a.oreb - b.oreb);
            AddD(deltas, a.player_id, "dreb", a.dreb - b.dreb);
            AddD(deltas, a.player_id, "ast", a.assists - b.assists);
            AddD(deltas, a.player_id, "stl", a.steals - b.steals);
            AddD(deltas, a.player_id, "blk", a.blocks - b.blocks);
            AddD(deltas, a.player_id, "to", a.turnovers - b.turnovers);
            AddD(deltas, a.player_id, "pf", a.pf - b.pf);
        }
        return deltas;
    }

    static void AddD(List<StatDelta> deltas, int player_id, string stat, float amount)
    {
        if (amount != 0) deltas.Add(new StatDelta { player_id = player_id, stat = stat, amount = amount });
    }

    static PossessionOutcome RunPossession(HashSet<int> offIds, HashSet<int> defIds, List<PlayerStatSnapshot> offAll, List<PlayerStatSnapshot> defAll, int offR, int defR, bool glLeague = false)
    {
        var off = offIds.Select(i => offAll[i]).ToList();
        var def = defIds.Select(i => defAll[i]).ToList();

        float toPct = 0.11f + (defR - offR) * 0.0003f;
        if (UnityEngine.Random.value < toPct)
        {
            string toName = DoTO(off, def);
            return new PossessionOutcome
            {
                pts = 0,
                desc = $"{toName ?? "Pérdida de balón"} pierde el balón"
            };
        }

        var shooter = PickShooter(off, glLeague);
        if (shooter == null)
            return new PossessionOutcome { pts = 0, desc = "Pérdida" };

        string shot = ShotType(shooter);
        var defender = def.OrderByDescending(p => p.defense).FirstOrDefault();
        float di = defender != null ? (defender.defense - 70) * 0.005f : 0;

        if (shot == "3")
        {
            float fp3 = FisicoPenalty(shooter.fisico);
            float basePct = (0.35f + (shooter.three_point - 70) * 0.005f) * fp3;
            float pct = Mathf.Clamp(basePct - di, 0.28f, 0.51f);
            shooter.fg3a++; shooter.fga++;
            if (UnityEngine.Random.value < pct)
            {
                shooter.fg3m++; shooter.fgm++; shooter.points += 3;
                string astr = DoAst(off, shooter, glLeague);
                return new PossessionOutcome
                {
                    pts = 3,
                    desc = astr != null
                        ? $"{shooter.name} encesta un triple (asist. {astr})"
                        : $"{shooter.name} encesta un triple"
                };
            }
            return MissHandler(def, off, shooter, true, glLeague);
        }

        float fp2 = FisicoPenalty(shooter.fisico);
        float base2Pct = (0.50f + (shooter.shooting - 70) * 0.005f) * fp2;
        float pct2 = Mathf.Clamp(base2Pct - di * 0.25f, 0.40f, 0.67f);
        shooter.fga++;
        if (UnityEngine.Random.value < pct2)
        {
            shooter.fgm++; shooter.points += 2;
            string astr = DoAst(off, shooter, glLeague);
            if (UnityEngine.Random.value < 0.06f)
            {
                string foulName = DoFoul(def);
                shooter.fta++;
                if (UnityEngine.Random.value < 0.75f)
                {
                    shooter.ftm++; shooter.points++;
                    return new PossessionOutcome
                    {
                        pts = 3,
                        desc = $"{shooter.name} anota y añade el tiro libre (falta de {foulName ?? "defensa"})"
                    };
                }
                return new PossessionOutcome
                {
                    pts = 2,
                    desc = $"{shooter.name} anota de dos pero falla el tiro adicional"
                };
            }
            return new PossessionOutcome
            {
                pts = 2,
                desc = astr != null
                    ? $"{shooter.name} anota de dos (asist. {astr})"
                    : $"{shooter.name} anota de dos"
            };
        }
        return MissHandler(def, off, shooter, false, glLeague);
    }

    static PossessionOutcome MissHandler(List<PlayerStatSnapshot> def, List<PlayerStatSnapshot> off, PlayerStatSnapshot shooter, bool isThree, bool glLeague = false)
    {
        // Un solo intento colectivo: el defensor se elige por atributo y posición,
        // evitando que el orden de la lista convierta a un base en taponador principal.
        var blockWeights = def
            .Select(d => Mathf.Clamp((d.blocks_attr - 60) / 400f, 0, 0.10f)
                * GetBlockPositionMultiplier(d.position))
            .ToList();
        float totalBlockChance = Mathf.Clamp(blockWeights.Sum(), 0f, 0.20f);
        if (UnityEngine.Random.value < totalBlockChance && totalBlockChance > 0)
        {
            float roll = UnityEngine.Random.value * blockWeights.Sum();
            float cumulative = 0;
            for (int i = 0; i < def.Count; i++)
            {
                cumulative += blockWeights[i];
                if (roll <= cumulative)
                {
                    def[i].blocks++;
                    return new PossessionOutcome
                    {
                        pts = 0,
                        desc = $"{def[i].name} tapona el tiro de {shooter.name}"
                    };
                }
            }
        }

        float foulChance = isThree ? 0.18f : 0.14f;
        if (UnityEngine.Random.value < foulChance)
        {
            string foulName = DoFoul(def);
            int nShots = isThree ? 3 : 2;
            if (UnityEngine.Random.value < 0.10f && !isThree) nShots = 3;
            float ftPct = 0.75f + (shooter.overall - 70) * 0.002f;
            int made = 0;
            for (int i = 0; i < nShots; i++)
            {
                shooter.fta++;
                if (UnityEngine.Random.value < ftPct)
                {
                    shooter.ftm++; shooter.points++; made++;
                }
            }
            string ftDesc = made > 0
                ? $"{shooter.name} convierte {made}/{nShots} tiros libres (falta de {foulName ?? "defensa"})"
                : $"{shooter.name} falla los {nShots} tiros libres";
            return new PossessionOutcome { pts = made, desc = ftDesc };
        }

        string rebName = DoReb(def, off, glLeague);
        return new PossessionOutcome
        {
            pts = 0,
            desc = rebName != null
                ? $"{rebName} captura el rebote"
                : "Rebote capturado"
        };
    }

    static PlayerStatSnapshot PickShooter(List<PlayerStatSnapshot> court, bool glLeague = false)
    {
        if (court.Count == 0) return null;
        var weights = court.Select(p => Mathf.Pow(p.overall / 100f, 2.2f) * FisicoPenalty(p.fisico)).ToList();
        if (glLeague) CompressWeights(weights, GL_COMPRESS);
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
            "PG" => 0.40f,
            "SG" => 0.46f,
            "SF" => 0.38f,
            "PF" => 0.33f,
            "C" => 0.18f,
            _ => 0.30f
        };
        float adj = Mathf.Clamp(base3pt + (p.three_point - 75) * 0.002f, 0.10f, 0.55f);
        adj *= FisicoPenalty(p.fisico);
        return UnityEngine.Random.value < adj ? "3" : "2";
    }

    static string DoAst(List<PlayerStatSnapshot> court, PlayerStatSnapshot scorer, bool glLeague = false)
    {
        // En la G-League hay más canastas asistidas (reparto más coral del ataque):
        // sin el reparto extra, los líderes no llegan ni a 4 APP porque el star
        // acapara los tiros y quedan pocas canastas ajenas que asistir.
        if (UnityEngine.Random.value >= (glLeague ? 0.60f : 0.35f)) return null;
        var others = court.Where(p => p != scorer).ToList();
        if (others.Count == 0) return null;
        var w = others.Select(p => Mathf.Pow(Mathf.Max(1, p.passing), 3f)).ToList();
        float t = w.Sum();
        if (t <= 0) return null;
        float r = UnityEngine.Random.value * t;
        float c = 0;
        for (int i = 0; i < others.Count; i++)
        {
            c += w[i];
            if (r <= c) { others[i].assists++; return others[i].name; }
        }
        return null;
    }

    static string DoReb(List<PlayerStatSnapshot> def, List<PlayerStatSnapshot> off, bool glLeague = false)
    {
        var dw = def.Select(p => ReboundWeight(p, false)).ToList();
        var ow = off.Select(p => ReboundWeight(p, true)).ToList();
        if (glLeague)
        {
            CompressWeights(dw, GL_COMPRESS);
            CompressWeights(ow, GL_COMPRESS);
        }
        float dSum = dw.Sum();
        float oSum = ow.Sum();
        float t = dSum + oSum;
        if (t <= 0) return null;

        if (UnityEngine.Random.value * t < dSum)
            return AwardReb(def, dw, true);
        return AwardReb(off, ow, false);
    }

    static string AwardReb(List<PlayerStatSnapshot> players, List<float> weights, bool defensive)
    {
        if (players.Count == 0) return null;
        float s = weights.Sum();
        if (s <= 0) return null;
        float r = UnityEngine.Random.value * s;
        float c = 0;
        for (int i = 0; i < players.Count; i++)
        {
            c += weights[i];
            if (r <= c)
            {
                if (defensive) players[i].dreb++;
                else players[i].oreb++;
                return players[i].name;
            }
        }
        return null;
    }

    static float ReboundWeight(PlayerStatSnapshot player, bool offensive)
    {
        float exponent = offensive ? 2.5f : 3f;
        return Mathf.Pow(Mathf.Max(1, player.rebounding), exponent)
            * GetReboundPositionMultiplier(player.position, offensive);
    }

    public static float GetReboundPositionMultiplier(string position, bool offensive)
    {
        if (offensive)
        {
            return position switch
            {
                "PG" => 0.35f,
                "SG" => 0.50f,
                "SF" => 0.75f,
                "PF" => 1.20f,
                "C" => 1.40f,
                _ => 1f
            };
        }

        return position switch
        {
            "PG" => 0.45f,
            "SG" => 0.55f,
            "SF" => 0.80f,
            "PF" => 1.20f,
            "C" => 1.35f,
            _ => 1f
        };
    }

    public static float GetBlockPositionMultiplier(string position)
    {
        return position switch
        {
            "PG" => 0.20f,
            "SG" => 0.30f,
            "SF" => 0.55f,
            "PF" => 1.15f,
            "C" => 1.30f,
            _ => 1f
        };
    }

    static string DoTO(List<PlayerStatSnapshot> off, List<PlayerStatSnapshot> def)
    {
        string toName = null;
        var handlers = off.Where(p => p.position is "PG" or "SG" or "SF").ToList();
        if (handlers.Count == 0) handlers = off;
        if (handlers.Count > 0)
        {
            var toWeights = handlers.Select(p => 1f / Mathf.Max(0.1f, FisicoPenalty(p.fisico))).ToList();
            float toTotal = toWeights.Sum();
            float toR = UnityEngine.Random.value * toTotal;
            float toC = 0;
            for (int i = 0; i < handlers.Count; i++)
            {
                toC += toWeights[i];
                if (toR <= toC) { handlers[i].turnovers++; toName = handlers[i].name; break; }
            }
        }

        // Only ~50% of TOs result in a steal (rest are offensive fouls, out-of-bounds, etc.)
        if (UnityEngine.Random.value >= 0.5f) return toName;
        if (def.Count == 0) return toName;
        var stlW = def.Select(p => Mathf.Pow(p.steals_attr, 3)).ToList();
        float t = stlW.Sum();
        if (t > 0)
        {
            float r = UnityEngine.Random.value * t;
            float c = 0;
            for (int i = 0; i < def.Count; i++)
            {
                c += stlW[i];
                if (r <= c) { def[i].steals++; return toName; }
            }
        }
        def.OrderByDescending(p => p.steals_attr).First().steals++;
        return toName;
    }

    static string DoFoul(List<PlayerStatSnapshot> court)
    {
        var bigs = court.Where(p => p.position is "C" or "PF").ToList();
        if (bigs.Count == 0) bigs = court;
        if (bigs.Count == 0) return null;
        var fouler = bigs[UnityEngine.Random.Range(0, bigs.Count)];
        fouler.pf++;
        return fouler.name;
    }

    static HashSet<int> DoSub(HashSet<int> on, List<PlayerStatSnapshot> players, float[] quarterMinutes)
    {
        int maxPlayers = players.Count;
        int n = Mathf.Min(UnityEngine.Random.value < 0.65f ? 1 : 2, on.Count);
        if (n == 0) return on;

        var newSet = new HashSet<int>(on);
        for (int change = 0; change < n; change++)
        {
            var outgoing = newSet
                .Where(i => CanRemoveFromRotation(newSet, players, i))
                .OrderByDescending(i => MinutesRatio(players[i], quarterMinutes[i]))
                .ThenBy(i => players[i].target_minutes)
                .ThenBy(i => players[i].overall)
                .DefaultIfEmpty(-1)
                .First();
            if (outgoing < 0) break;

            var incoming = Enumerable.Range(0, maxPlayers)
                .Where(i => !newSet.Contains(i))
                .OrderByDescending(i => PositionFit(players[i], players[outgoing]))
                .ThenByDescending(i => MinutesNeed(players[i], quarterMinutes[i]))
                .ThenByDescending(i => players[i].overall)
                .DefaultIfEmpty(-1)
                .First();
            if (incoming < 0) break;

            newSet.Remove(outgoing);
            newSet.Add(incoming);
        }

        return newSet;
    }

    static float MinutesRatio(PlayerStatSnapshot player, float currentQuarterMinutes)
    {
        float current = player.minutes + currentQuarterMinutes;
        return player.target_minutes > 0 ? current / player.target_minutes : current;
    }

    static float MinutesNeed(PlayerStatSnapshot player, float currentQuarterMinutes)
    {
        return Mathf.Max(0, player.target_minutes - player.minutes - currentQuarterMinutes);
    }

    static bool CanRemoveFromRotation(HashSet<int> on, List<PlayerStatSnapshot> players, int index)
    {
        var player = players[index];
        bool isStar = player.role == PlayerRole.Estrella || player.overall >= 85;
        if (isStar && player.starter && player.minutes < player.target_minutes * 0.55f)
            return false;

        if (player.position is "C" or "PF")
        {
            int interiors = on.Count(i => players[i].position is "C" or "PF");
            if (interiors <= 1)
                return false;
        }

        return true;
    }

    static int PositionFit(PlayerStatSnapshot incoming, PlayerStatSnapshot outgoing)
    {
        if (PlaysPosition(incoming, outgoing.position)) return 3;

        bool incomingInterior = incoming.position is "C" or "PF";
        bool outgoingInterior = outgoing.position is "C" or "PF";
        if (incomingInterior == outgoingInterior) return 2;
        return 0;
    }

    static bool PlaysPosition(PlayerStatSnapshot player, string position)
    {
        return player.position == position || player.secondary_position == position;
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
            float injuryChance = 0.008f;
            if (ps.fisico < 30)
                injuryChance *= 1f + (30f - ps.fisico) * 0.15f;
            if (UnityEngine.Random.value >= injuryChance) continue;

            var injury = PickInjury();
            player.injury_type = injury.type;
            player.injury_days = UnityEngine.Random.Range(injury.minDays, injury.maxDays + 1);
            player.treated = 0;
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
        ("Rotura parcial de ligamentos", 28, 65, 4),
        ("Rotura de ligamentos", 42, 90, 3),
        ("Rotura de menisco", 35, 70, 3),
        ("Fractura de mano", 30, 66, 2),
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
