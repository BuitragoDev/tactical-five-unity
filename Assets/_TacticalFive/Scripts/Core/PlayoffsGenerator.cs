using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PlayoffsGenerator
{
    public static int GeneratePlayIn(SeasonData season, int managerId)
    {
        // Get last regular season game day
        var lastRegularDay = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.game_type == "regular")
            .OrderByDescending(g => g.game_day)
            .Select(g => g.game_day)
            .FirstOrDefault();

        if (lastRegularDay == 0) return 0;

        int playInStartDay = lastRegularDay + 7;
        var seasonStart = new DateTime(season.year_start, 10, 22);
        var playInDate = seasonStart.AddDays(playInStartDay - 1);

        // Check if play-in already exists
        var existing = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.game_type == "playin")
            .FirstOrDefault();
        if (existing != null) return 0;

        var gamesToCreate = new List<GameData>();

        foreach (string conf in new[] { "East", "West" })
        {
            var standings = GetStandingsForConference(season, managerId, conf);
            if (standings.Count < 10) continue;

            var seed7 = standings[6];
            var seed8 = standings[7];
            var seed9 = standings[8];
            var seed10 = standings[9];

            // Game 1: Seed 7 vs Seed 8 (home: seed 7)
            gamesToCreate.Add(new GameData
            {
                season_id = season.id,
                manager_id = managerId,
                game_day = playInStartDay,
                game_date = playInDate.ToString("yyyy-MM-dd"),
                home_team_id = seed7.teamId,
                away_team_id = seed8.teamId,
                is_played = 0,
                game_type = "playin",
                series_label = $"playin-7-8-{conf.ToLower()}",
                home_score = 0,
                away_score = 0
            });

            // Game 2: Seed 9 vs Seed 10 (home: seed 9)
            gamesToCreate.Add(new GameData
            {
                season_id = season.id,
                manager_id = managerId,
                game_day = playInStartDay,
                game_date = playInDate.ToString("yyyy-MM-dd"),
                home_team_id = seed9.teamId,
                away_team_id = seed10.teamId,
                is_played = 0,
                game_type = "playin",
                series_label = $"playin-9-10-{conf.ToLower()}",
                home_score = 0,
                away_score = 0
            });
        }

        DatabaseManager.Instance.SavePlayInGames(gamesToCreate);
        return gamesToCreate.Count;
    }

    public static void CreatePlayInEliminator(SeasonData season, int managerId, string conf)
    {
        var game7v8 = DatabaseManager.Instance.Db.Table<GameData>()
            .FirstOrDefault(g => g.manager_id == managerId && g.game_type == "playin" && g.series_label == $"playin-7-8-{conf.ToLower()}" && g.is_played == 1);

        var game9v10 = DatabaseManager.Instance.Db.Table<GameData>()
            .FirstOrDefault(g => g.manager_id == managerId && g.game_type == "playin" && g.series_label == $"playin-9-10-{conf.ToLower()}" && g.is_played == 1);

        if (game7v8 == null || game9v10 == null) return;
        if (game7v8.is_played != 1 || game9v10.is_played != 1) return;

        // Check if eliminator already exists
        var elimExists = DatabaseManager.Instance.Db.Table<GameData>()
            .Any(g => g.manager_id == managerId && g.game_type == "playin" && g.series_label == $"playin-elim-{conf.ToLower()}");
        if (elimExists) return;

        // Loser of 7v8 vs Winner of 9v10
        int loser7v8 = game7v8.home_score > game7v8.away_score ? game7v8.away_team_id : game7v8.home_team_id;
        int winner9v10 = game9v10.home_score > game9v10.away_score ? game9v10.home_team_id : game9v10.away_team_id;

        int elimDay = game7v8.game_day + 2;
        var elimDate = DateTime.Parse(game7v8.game_date).AddDays(2);

        DatabaseManager.Instance.Db.Insert(new GameData
        {
            season_id = season.id,
            manager_id = managerId,
            game_day = elimDay,
            game_date = elimDate.ToString("yyyy-MM-dd"),
            home_team_id = loser7v8,
            away_team_id = winner9v10,
            is_played = 0,
            game_type = "playin",
            series_label = $"playin-elim-{conf.ToLower()}",
            home_score = 0,
            away_score = 0
        });
    }

    public static int GeneratePlayoffs(SeasonData season, int managerId)
    {
        // Check if playoffs already exist
        var existing = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.game_type == "playoff")
            .FirstOrDefault();
        if (existing != null) return 0;

        var seeds = GetPlayoffSeeds(season, managerId);

        var lastPlayInGame = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.game_type == "playin")
            .OrderByDescending(g => g.game_day)
            .FirstOrDefault();

        int baseDay = (lastPlayInGame?.game_day ?? 0) + 4;
        var seasonStart = new DateTime(season.year_start, 10, 22);
        DateTime baseDate = seasonStart.AddDays(baseDay - 1);

        var allGames = new List<GameData>();

        foreach (string conf in new[] { "East", "West" })
        {
            var confSeeds = seeds[conf];
            if (confSeeds.Count < 8 || confSeeds.Any(t => t == 0)) continue;

            int confOffset = conf == "East" ? 0 : 1;

            // Round 1 matchups: 1v8, 4v5, 2v7, 3v6
            var seriesDefs = new[]
            {
                (confSeeds[0], confSeeds[7], $"playoff-r1-{conf.ToLower()}-1v8"),
                (confSeeds[3], confSeeds[4], $"playoff-r1-{conf.ToLower()}-4v5"),
                (confSeeds[1], confSeeds[6], $"playoff-r1-{conf.ToLower()}-2v7"),
                (confSeeds[2], confSeeds[5], $"playoff-r1-{conf.ToLower()}-3v6"),
            };

            foreach (var (home, away, label) in seriesDefs)
            {
                var seriesGames = CreatePlayoffSeriesGames(season, managerId, home, away, label, baseDay + confOffset, baseDate.AddDays(confOffset));
                allGames.AddRange(seriesGames);
            }
        }

        DatabaseManager.Instance.SavePlayoffGames(allGames);
        return allGames.Count;
    }

    public static void AdvancePlayoffSeries(SeasonData season, int managerId)
    {
        // First, create play-in eliminators if needed
        foreach (string conf in new[] { "East", "West" })
        {
            CreatePlayInEliminator(season, managerId, conf);
        }

        // Check if play-in is complete before advancing playoffs
        var playInGames = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.game_type == "playin")
            .ToList();

        bool playInComplete = playInGames.All(g => g.is_played == 1);
        if (!playInComplete) return;

        // Check if playoffs already started
        var playoffGames = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.game_type == "playoff")
            .FirstOrDefault();
        
        if (playoffGames == null)
        {
            GeneratePlayoffs(season, managerId);
            return;
        }

        var allSeries = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.game_type == "playoff" && g.series_label != "")
            .Select(g => g.series_label)
            .Distinct()
            .ToList();

        foreach (var seriesLabel in allSeries)
        {
            var seriesGames = DatabaseManager.Instance.Db.Table<GameData>()
                .Where(g => g.manager_id == managerId && g.series_label == seriesLabel)
                .OrderBy(g => g.game_day)
                .ToList();

            var played = seriesGames.Where(g => g.is_played == 1).ToList();
            if (played.Count == 0) continue;

            int teamA = played[0].home_team_id;
            int teamB = played[0].away_team_id;
            int teamAWins = 0;
            int teamBWins = 0;

            foreach (var g in played)
            {
                if (g.home_score > g.away_score)
                {
                    if (g.home_team_id == teamA) teamAWins++;
                    else teamBWins++;
                }
                else
                {
                    if (g.away_team_id == teamA) teamAWins++;
                    else teamBWins++;
                }
            }

            if (teamAWins < 4 && teamBWins < 4) continue;

            // Series complete - delete remaining games
            var remainingGames = seriesGames.Where(g => g.is_played == 0).ToList();
            foreach (var g in remainingGames)
            {
                DatabaseManager.Instance.Db.Delete(g);
            }

            int winner = teamAWins >= 4 ? teamA : teamB;
            AdvanceWinner(season, managerId, seriesLabel, winner);
        }

        // Check and create next round
        CheckAndCreateNextRound(season, managerId);
    }

    static void AdvanceWinner(SeasonData season, int managerId, string seriesLabel, int winner)
    {
        // Winners are tracked in CheckAndCreateNextRound
    }

    static void CheckAndCreateNextRound(SeasonData season, int managerId)
    {
        var r1Labels = new[]
        {
            "playoff-r1-east-1v8", "playoff-r1-east-4v5", "playoff-r1-east-2v7", "playoff-r1-east-3v6",
            "playoff-r1-west-1v8", "playoff-r1-west-4v5", "playoff-r1-west-2v7", "playoff-r1-west-3v6",
        };

        bool r1Done = r1Labels.All(label => GetSeriesWinner(season, managerId, label) != 0);
        if (r1Done)
        {
            bool r2Exists = DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == managerId && g.game_type == "playoff" && g.series_label.StartsWith("playoff-r2-"));
            if (!r2Exists)
            {
                CreateRound2Series(season, managerId);
            }
        }

        foreach (string conf in new[] { "East", "West" })
        {
            string confLower = conf.ToLower();
            bool r2Done = new[] { 1, 2 }.All(i =>
                GetSeriesWinner(season, managerId, $"playoff-r2-{confLower}-s{i}") != 0);

            if (r2Done)
            {
                bool r3Exists = DatabaseManager.Instance.Db.Table<GameData>()
                    .Any(g => g.manager_id == managerId && g.game_type == "playoff" && g.series_label == $"playoff-r3-{confLower}-s1");
                if (!r3Exists)
                {
                    CreateConferenceFinal(season, managerId, conf);
                }
            }
        }

        bool r3Done = new[] { "playoff-r3-east-s1", "playoff-r3-west-s1" }
            .All(label => GetSeriesWinner(season, managerId, label) != 0);

        if (r3Done)
        {
            bool r4Exists = DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == managerId && g.game_type == "playoff" && g.series_label == "playoff-r4-finals");
            if (!r4Exists)
            {
                CreateFinalsSeries(season, managerId);
            }
        }
    }

    static void CreateRound2Series(SeasonData season, int managerId)
    {
        var lastPlayed = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.game_type == "playoff" && g.is_played == 1)
            .OrderByDescending(g => g.game_day)
            .FirstOrDefault();

        int baseDay = (lastPlayed?.game_day ?? 0) + 4;
        var seasonStart = new DateTime(season.year_start, 10, 22);
        DateTime baseDate = seasonStart.AddDays(baseDay - 1);

        foreach (string conf in new[] { "East", "West" })
        {
            string confLower = conf.ToLower();
            int confOffset = conf == "East" ? 0 : 1;

            int w1 = GetSeriesWinner(season, managerId, $"playoff-r1-{confLower}-1v8");
            int w2 = GetSeriesWinner(season, managerId, $"playoff-r1-{confLower}-4v5");
            int w3 = GetSeriesWinner(season, managerId, $"playoff-r1-{confLower}-2v7");
            int w4 = GetSeriesWinner(season, managerId, $"playoff-r1-{confLower}-3v6");

            if (w1 != 0 && w2 != 0)
            {
                int home = w1 < w2 ? w1 : w2;
                int away = w1 < w2 ? w2 : w1;
                var games = CreatePlayoffSeriesGames(season, managerId, home, away, $"playoff-r2-{confLower}-s1", baseDay + confOffset, baseDate.AddDays(confOffset));
                DatabaseManager.Instance.SavePlayoffGames(games);
            }

            if (w3 != 0 && w4 != 0)
            {
                int home = w3 < w4 ? w3 : w4;
                int away = w3 < w4 ? w4 : w3;
                var games = CreatePlayoffSeriesGames(season, managerId, home, away, $"playoff-r2-{confLower}-s2", baseDay + confOffset, baseDate.AddDays(confOffset));
                DatabaseManager.Instance.SavePlayoffGames(games);
            }
        }
    }

    static void CreateConferenceFinal(SeasonData season, int managerId, string conf)
    {
        var lastPlayed = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.game_type == "playoff" && g.is_played == 1)
            .OrderByDescending(g => g.game_day)
            .FirstOrDefault();

        int baseDay = (lastPlayed?.game_day ?? 0) + 4;
        var seasonStart = new DateTime(season.year_start, 10, 22);
        DateTime baseDate = seasonStart.AddDays(baseDay - 1);

        string confLower = conf.ToLower();
        int w1 = GetSeriesWinner(season, managerId, $"playoff-r2-{confLower}-s1");
        int w2 = GetSeriesWinner(season, managerId, $"playoff-r2-{confLower}-s2");

        if (w1 != 0 && w2 != 0)
        {
            int home = w1 < w2 ? w1 : w2;
            int away = w1 < w2 ? w2 : w1;
            var games = CreatePlayoffSeriesGames(season, managerId, home, away, $"playoff-r3-{confLower}-s1", baseDay, baseDate);
            DatabaseManager.Instance.SavePlayoffGames(games);
        }
    }

    static void CreateFinalsSeries(SeasonData season, int managerId)
    {
        var lastPlayed = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.game_type == "playoff" && g.is_played == 1)
            .OrderByDescending(g => g.game_day)
            .FirstOrDefault();

        int baseDay = (lastPlayed?.game_day ?? 0) + 4;
        var seasonStart = new DateTime(season.year_start, 10, 22);
        DateTime baseDate = seasonStart.AddDays(baseDay - 1);

        int eastWinner = GetSeriesWinner(season, managerId, "playoff-r3-east-s1");
        int westWinner = GetSeriesWinner(season, managerId, "playoff-r3-west-s1");

        if (eastWinner != 0 && westWinner != 0)
        {
            int home = eastWinner < westWinner ? eastWinner : westWinner;
            int away = eastWinner < westWinner ? westWinner : eastWinner;
            var games = CreatePlayoffSeriesGames(season, managerId, home, away, "playoff-r4-finals", baseDay, baseDate);
            DatabaseManager.Instance.SavePlayoffGames(games);
        }
    }

    static int GetSeriesWinner(SeasonData season, int managerId, string seriesLabel)
    {
        var games = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == managerId && g.series_label == seriesLabel && g.is_played == 1)
            .OrderBy(g => g.game_day)
            .ToList();

        if (games.Count == 0) return 0;

        int teamA = games[0].home_team_id;
        int teamB = games[0].away_team_id;
        int teamAWins = 0;
        int teamBWins = 0;

        foreach (var g in games)
        {
            if (g.home_score > g.away_score)
            {
                if (g.home_team_id == teamA) teamAWins++;
                else teamBWins++;
            }
            else
            {
                if (g.away_team_id == teamA) teamAWins++;
                else teamBWins++;
            }
        }

        if (teamAWins >= 4) return teamA;
        if (teamBWins >= 4) return teamB;
        return 0;
    }

    static List<GameData> CreatePlayoffSeriesGames(SeasonData season, int managerId, int homeTeam, int awayTeam, string seriesLabel, int startDay, DateTime startDate)
    {
        var games = new List<GameData>();
        int[] homeGames = { 0, 1, 4, 6 }; // 2-2-1-1-1 format

        for (int gameNum = 0; gameNum < 7; gameNum++)
        {
            bool isHome = homeGames.Contains(gameNum);
            int home = isHome ? homeTeam : awayTeam;
            int away = isHome ? awayTeam : homeTeam;

            int gameDay = startDay + gameNum * 2;
            DateTime gameDate = startDate.AddDays(gameNum * 2);

            bool exists = DatabaseManager.Instance.Db.Table<GameData>()
                .Any(g => g.manager_id == managerId && g.home_team_id == home && g.away_team_id == away && g.game_day == gameDay);

            if (!exists)
            {
                games.Add(new GameData
                {
                    season_id = season.id,
                    manager_id = managerId,
                    game_day = gameDay,
                    game_date = gameDate.ToString("yyyy-MM-dd"),
                    home_team_id = home,
                    away_team_id = away,
                    is_played = 0,
                    game_type = "playoff",
                    series_label = seriesLabel,
                    home_score = 0,
                    away_score = 0
                });
            }
        }

        return games;
    }

    static List<(int teamId, int wins, int losses)> GetStandingsForConference(SeasonData season, int managerId, string conf)
    {
        var teams = DatabaseManager.Instance.GetTeamsByConference(conf);
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

    static Dictionary<string, List<int>> GetPlayoffSeeds(SeasonData season, int managerId)
    {
        var seeds = new Dictionary<string, List<int>>();

        foreach (string conf in new[] { "East", "West" })
        {
            var standings = GetStandingsForConference(season, managerId, conf);
            var top6 = standings.Take(6).Select(s => s.teamId).ToList();

            // Play-in winners
            var game7v8 = DatabaseManager.Instance.Db.Table<GameData>()
                .FirstOrDefault(g => g.manager_id == managerId && g.game_type == "playin" && g.series_label == $"playin-7-8-{conf.ToLower()}" && g.is_played == 1);

            var gameElim = DatabaseManager.Instance.Db.Table<GameData>()
                .FirstOrDefault(g => g.manager_id == managerId && g.game_type == "playin" && g.series_label == $"playin-elim-{conf.ToLower()}" && g.is_played == 1);

            int seed7 = game7v8 != null && game7v8.home_score > game7v8.away_score ? game7v8.home_team_id :
                        game7v8 != null ? game7v8.away_team_id : 0;

            int seed8 = gameElim != null && gameElim.home_score > gameElim.away_score ? gameElim.home_team_id :
                        gameElim != null ? gameElim.away_team_id : 0;

            seeds[conf] = top6.Concat(new[] { seed7, seed8 }).ToList();
        }

        return seeds;
    }
}
