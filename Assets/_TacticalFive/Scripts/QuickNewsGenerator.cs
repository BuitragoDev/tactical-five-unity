using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public static class QuickNewsGenerator
{
    public static void Generate(ManagerData manager, TeamData myTeam, SeasonData season, List<GameData> gamesToday, int gameDay, string gameDate)
    {
        if (season == null || gamesToday == null || gamesToday.Count == 0) return;
        if (season.phase != "regular") return;

        var allStandingsGames = DatabaseManager.Instance.GetStandingsGames(manager.id);
        var allTeams = DatabaseManager.Instance.GetAllTeams().ToDictionary(t => t.id);
        bool anySaved = false;
        int newsCount = 0;

        if (gameDay % 41 == 0 && gameDay <= 82)
        {
            SaveNews(manager, gameDay, gameDate, "📊 HITO", gameDay == 41
                ? "¡Mitad de temporada superada! Comienza la segunda vuelta."
                : $"¡Jornada {gameDay}! Se acerca el final de la temporada regular.");
            newsCount++;
        }

        foreach (var game in gamesToday.OrderBy(g => g.id))
        {
            if (newsCount >= 2) break;

            if (!allTeams.TryGetValue(game.home_team_id, out var homeTeam)) continue;
            if (!allTeams.TryGetValue(game.away_team_id, out var awayTeam)) continue;

            int homeStreak = ComputeStreak(allStandingsGames, game.home_team_id);
            int awayStreak = ComputeStreak(allStandingsGames, game.away_team_id);

            if (newsCount < 2 && homeStreak >= 5)
            {
                if (SaveNews(manager, gameDay, gameDate, "🔥 RACHA", $"{homeTeam.name} acumula {homeStreak} victorias consecutivas"))
                    { newsCount++; continue; }
            }
            if (newsCount < 2 && awayStreak >= 5)
            {
                if (SaveNews(manager, gameDay, gameDate, "🔥 RACHA", $"{awayTeam.name} acumula {awayStreak} victorias consecutivas"))
                    { newsCount++; continue; }
            }

            if (newsCount < 2 && homeStreak <= -5)
            {
                if (SaveNews(manager, gameDay, gameDate, "💀 MALA RACHA", $"{homeTeam.name} encadena {-homeStreak} derrotas consecutivas"))
                    { newsCount++; continue; }
            }
            if (newsCount < 2 && awayStreak <= -5)
            {
                if (SaveNews(manager, gameDay, gameDate, "💀 MALA RACHA", $"{awayTeam.name} encadena {-awayStreak} derrotas consecutivas"))
                    { newsCount++; continue; }
            }

            int medDiff = Mathf.Abs(homeTeam.overall - awayTeam.overall);
            bool homeFav = homeTeam.overall > awayTeam.overall;
            bool homeWon = game.home_score > game.away_score;
            if (newsCount < 2 && medDiff >= 15 && ((homeFav && !homeWon) || (!homeFav && homeWon)))
            {
                var winner = homeWon ? homeTeam : awayTeam;
                var loser = homeWon ? awayTeam : homeTeam;
                if (SaveNews(manager, gameDay, gameDate, "⚡ CAMPANADA", $"¡Campanada! {winner.name} ({winner.overall}) derrota a {loser.name} ({loser.overall})"))
                    { newsCount++; continue; }
            }

            bool isMyGame = game.home_team_id == myTeam.id || game.away_team_id == myTeam.id;
            if (newsCount < 2 && isMyGame)
            {
                bool myIsHome = game.home_team_id == myTeam.id;
                int myScore = myIsHome ? game.home_score : game.away_score;
                int oppScore = myIsHome ? game.away_score : game.home_score;
                var rival = myIsHome ? awayTeam : homeTeam;
                if (myScore > oppScore)
                {
                    if (SaveNews(manager, gameDay, gameDate, "🏆 VICTORIA", $"¡Victoria de {myTeam.name} frente a {rival.name}!"))
                        { newsCount++; continue; }
                }
                else
                {
                    if (SaveNews(manager, gameDay, gameDate, "😞 DERROTA", $"Derrota de {myTeam.name} ante {rival.name} por {oppScore - myScore} puntos"))
                        { newsCount++; continue; }
                }
            }

            if (newsCount >= 2) continue;

            var gameStats = DatabaseManager.Instance.GetGamePlayerStats(game.id);
            if (gameStats == null) continue;

            foreach (var ps in gameStats)
            {
                if (newsCount >= 2) break;

                if (ps.triple_double == 1)
                {
                    var player = DatabaseManager.Instance.GetPlayerById(ps.player_id);
                    if (player == null) continue;
                    var opponent = game.home_team_id == player.team_id ? awayTeam : homeTeam;
                    if (SaveNews(manager, gameDay, gameDate, "💎 TRIPLE-DOBLE",
                        $"{player.first_name} {player.last_name} firma un triple-doble ({ps.points}+{ps.rebounds}+{ps.assists}) ante {opponent.name}"))
                        { newsCount++; continue; }
                }

                if (ps.points >= 40)
                {
                    var player = DatabaseManager.Instance.GetPlayerById(ps.player_id);
                    if (player == null) continue;
                    var opponent = game.home_team_id == player.team_id ? awayTeam : homeTeam;
                    if (SaveNews(manager, gameDay, gameDate, "⭐ EXPLOSIÓN",
                        $"{player.first_name} {player.last_name} explota con {ps.points} puntos ante {opponent.name}"))
                        { newsCount++; continue; }
                }
            }
        }
    }

    static int ComputeStreak(List<GameData> allGames, int teamId)
    {
        var teamGames = allGames
            .Where(g => g.home_team_id == teamId || g.away_team_id == teamId)
            .OrderByDescending(g => g.game_day)
            .ThenByDescending(g => g.id)
            .ToList();

        if (teamGames.Count == 0) return 0;

        int streak = 0;
        bool firstWon = false;
        bool firstSet = false;

        foreach (var g in teamGames)
        {
            bool isHome = g.home_team_id == teamId;
            int teamScore = isHome ? g.home_score : g.away_score;
            int oppScore = isHome ? g.away_score : g.home_score;
            bool won = teamScore > oppScore;

            if (!firstSet)
            {
                firstWon = won;
                firstSet = true;
                streak = 1;
                continue;
            }

            if (won == firstWon)
                streak++;
            else
                break;
        }

        return firstWon ? streak : -streak;
    }

    static bool SaveNews(ManagerData manager, int gameDay, string gameDate, string title, string body)
    {
        var existing = DatabaseManager.Instance.Db.Table<MessageData>()
            .FirstOrDefault(m => m.title == title && m.body == body && m.game_day == gameDay);
        if (existing != null) return false;

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = manager.id,
            sender_type = 2,
            sender_id = 0,
            title = title,
            body = body,
            game_day = gameDay,
            game_date = gameDate,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        });
        return true;
    }
}
