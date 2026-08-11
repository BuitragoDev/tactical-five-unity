using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class MatchupPreview
{
    public class PreviewResult
    {
        public int homeRating;
        public int awayRating;
        public float homeWinProb;
        public float awayWinProb;
        public string favoriteName;
        public bool isHomeFavorite;
        public List<PlayerData> homeStars = new();
        public List<PlayerData> awayStars = new();
    }

    /// <summary>Pronóstico previo al partido: ratings, probabilidad de victoria y estrellas de cada equipo.</summary>
    public static PreviewResult Compute(int homeTeamId, int awayTeamId, bool isHome,
        int managerId, int seasonId)
    {
        var db = DatabaseManager.Instance;
        var homePlayers = db.GetPlayersByTeam(homeTeamId).Where(p => p.injury_days == 0).ToList();
        var awayPlayers = db.GetPlayersByTeam(awayTeamId).Where(p => p.injury_days == 0).ToList();

        int homeR = TeamRating(homePlayers);
        int awayR = TeamRating(awayPlayers);

        float homeChemBonus = (db.GetTeamChemistry(homeTeamId) - 50) * 0.15f;
        float awayChemBonus = (db.GetTeamChemistry(awayTeamId) - 50) * 0.10f;
        float homeCourtBonus = isHome ? 1.5f : 0f;

        homeR = Mathf.Clamp(Mathf.RoundToInt(homeR + homeChemBonus + homeCourtBonus
            + RecentFormBonus(db, homeTeamId, managerId, seasonId)), 0, 99);
        awayR = Mathf.Clamp(Mathf.RoundToInt(awayR + awayChemBonus
            + RecentFormBonus(db, awayTeamId, managerId, seasonId)), 0, 99);

        float diff = homeR - awayR;
        float homeProb = 1f / (1f + Mathf.Exp(-diff * 0.08f));

        var homeTeam = db.GetTeamById(homeTeamId);
        var awayTeam = db.GetTeamById(awayTeamId);

        // Solo declarar favorito si hay una ventaja clara; si está prácticamente
        // empatado (50/50) no se declara ningún favorito.
        bool hasFavorite = Mathf.Abs(homeProb - 0.5f) > 0.005f;

        return new PreviewResult
        {
            homeRating = homeR,
            awayRating = awayR,
            homeWinProb = homeProb,
            awayWinProb = 1f - homeProb,
            favoriteName = hasFavorite ? (homeProb >= 0.5f ? homeTeam?.name : awayTeam?.name) : null,
            isHomeFavorite = hasFavorite && homeProb >= 0.5f,
            homeStars = homePlayers.OrderByDescending(p => p.overall).Take(3).ToList(),
            awayStars = awayPlayers.OrderByDescending(p => p.overall).Take(3).ToList(),
        };
    }

    static int TeamRating(List<PlayerData> players)
    {
        if (players == null || players.Count == 0) return 50;
        return (int)players.Average(p => Mathf.Clamp(p.overall + (p.morale - 50) * 0.1f, 0, 99));
    }

    static float RecentFormBonus(DatabaseManager db, int teamId, int managerId, int seasonId)
    {
        var played = db.GetSeasonGames(managerId, seasonId)
                       .Where(g => g.is_played == 1)
                       .OrderByDescending(g => g.game_day)
                       .Take(5)
                       .ToList();
        if (played.Count == 0) return 0f;

        float total = 0f;
        int counted = 0;
        foreach (var g in played)
        {
            var teamStats = db.GetGamePlayerStats(g.id)
                              .Where(s => s.team_id == teamId && s.minutes > 0)
                              .ToList();
            if (teamStats.Count == 0) continue;
            total += (float)teamStats.Average(s => s.rating);
            counted++;
        }
        if (counted == 0) return 0f;

        return (total / counted - 50f) * 0.25f;
    }
}
