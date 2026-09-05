using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public static class MatchupPreview
{
    public class TeamPreviewSide
    {
        public string teamName;
        public string cityName;
        public string logo;
        public int wins, losses;
        public int conferenceRank;
        public string conferenceName;
        public List<char> last10 = new();
        public float offRating, defRating;
        public int offRank, defRank;
        public List<PlayerData> starters = new();
        public List<PlayerData> bench = new();
        public List<PlayerData> injured = new();
        public PlayerData keyPts, keyReb, keyAst, keyBlk;
        public float keyPtsVal, keyRebVal, keyAstVal, keyBlkVal;
    }

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
        public TeamPreviewSide home;
        public TeamPreviewSide away;
        public string gameDate;
        public string arenaName;
        public string arenaCity;
    }

    public static PreviewResult Compute(int homeTeamId, int awayTeamId, bool isHome,
        int managerId, int seasonId, string gameDate)
    {
        var db = DatabaseManager.Instance;
        var allTeams = db.GetAllTeams();
        var homeTeam = db.GetTeamById(homeTeamId);
        var awayTeam = db.GetTeamById(awayTeamId);

        var homePlayers = db.GetPlayersByTeam(homeTeamId).Where(p => p.injury_days == 0 && p.g_league_assigned == 0).ToList();
        var awayPlayers = db.GetPlayersByTeam(awayTeamId).Where(p => p.injury_days == 0 && p.g_league_assigned == 0).ToList();

        var allHomePlayers = db.GetPlayersByTeam(homeTeamId);
        var allAwayPlayers = db.GetPlayersByTeam(awayTeamId);

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

        bool hasFavorite = Mathf.Abs(homeProb - 0.5f) > 0.005f;

        var standingsGames = db.GetStandingsGames(managerId)
            .Where(g => g.is_played == 1).ToList();

        var homeSide = BuildSide(homeTeam, homePlayers, allHomePlayers, homeTeamId,
            standingsGames, allTeams, db, managerId, seasonId, homePlayers, awayPlayers);
        var awaySide = BuildSide(awayTeam, awayPlayers, allAwayPlayers, awayTeamId,
            standingsGames, allTeams, db, managerId, seasonId, homePlayers, awayPlayers);

        homeSide.starters = GetStartersFromLineup(homePlayers, db, homeTeamId);
        awaySide.starters = GetStartersFromLineup(awayPlayers, db, awayTeamId);
        homeSide.bench = GetBenchFromLineup(homePlayers, db, homeTeamId, homeSide.starters);
        awaySide.bench = GetBenchFromLineup(awayPlayers, db, awayTeamId, awaySide.starters);
        homeSide.injured = allHomePlayers.Where(p => p.injury_days > 0).OrderByDescending(p => p.overall).Take(3).ToList();
        awaySide.injured = allAwayPlayers.Where(p => p.injury_days > 0).OrderByDescending(p => p.overall).Take(3).ToList();

        ComputeKeyPlayers(homeSide, db, homeTeamId, managerId, seasonId);
        ComputeKeyPlayers(awaySide, db, awayTeamId, managerId, seasonId);

        var offRatings = new Dictionary<int, float>();
        var defRatings = new Dictionary<int, float>();
        foreach (var team in allTeams)
        {
            var teamGames = standingsGames.Where(g =>
                (g.home_team_id == team.id || g.away_team_id == team.id)).ToList();
            if (teamGames.Count == 0) continue;
            float totalScored = 0, totalAllowed = 0;
            foreach (var g in teamGames)
            {
                bool isH = g.home_team_id == team.id;
                totalScored += isH ? g.home_score : g.away_score;
                totalAllowed += isH ? g.away_score : g.home_score;
            }
            int n = teamGames.Count;
            offRatings[team.id] = totalScored / n;
            defRatings[team.id] = totalAllowed / n;
        }

        AssignRanks(homeSide, offRatings, defRatings);
        AssignRanks(awaySide, offRatings, defRatings);

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
            home = homeSide,
            away = awaySide,
            gameDate = !string.IsNullOrEmpty(gameDate)
                ? DateTime.Parse(gameDate).ToString("dd MMM yyyy",
                    new System.Globalization.CultureInfo("es-ES")).ToUpper()
                : "",
            arenaName = homeTeam?.arena ?? "",
            arenaCity = homeTeam?.city ?? "",
        };
    }

    static TeamPreviewSide BuildSide(TeamData team, List<PlayerData> availablePlayers,
        List<PlayerData> allPlayers, int teamId,
        List<GameData> standingsGames, List<TeamData> allTeams,
        DatabaseManager db, int managerId, int seasonId,
        List<PlayerData> homePlayers, List<PlayerData> awayPlayers)
    {
        var side = new TeamPreviewSide();
        if (team == null) return side;

        side.teamName = team.name.ToUpper();
        side.cityName = team.city?.ToUpper() ?? "";
        side.logo = team.logo;

        var teamGames = standingsGames.Where(g =>
            (g.home_team_id == teamId || g.away_team_id == teamId)).ToList();
        side.wins = teamGames.Count(g =>
            (g.home_team_id == teamId && g.home_score > g.away_score) ||
            (g.away_team_id == teamId && g.away_score > g.home_score));
        side.losses = teamGames.Count(g => g.home_score != g.away_score) - side.wins;
        if (side.wins + side.losses == 0) { side.wins = 0; side.losses = 0; }

        side.conferenceRank = ObjectiveHelper.GetConferenceRank(teamId, team.conference, allTeams, standingsGames);
        side.conferenceName = team.conference == "East" ? "ESTE" : "OESTE";

        var lastGames = teamGames.OrderByDescending(g => g.game_day).Take(10).ToList();
        foreach (var g in lastGames)
        {
            bool won = (g.home_team_id == teamId && g.home_score > g.away_score) ||
                       (g.away_team_id == teamId && g.away_score > g.home_score);
            side.last10.Add(won ? 'G' : 'P');
        }
        side.last10.Reverse();

        ComputeTeamStats(side, teamId, standingsGames, db);

        return side;
    }

    static void ComputeTeamStats(TeamPreviewSide side, int teamId,
        List<GameData> standingsGames, DatabaseManager db)
    {
        var teamGames = standingsGames.Where(g =>
            (g.home_team_id == teamId || g.away_team_id == teamId)).ToList();
        if (teamGames.Count == 0) return;

        float totalPtsScored = 0, totalPtsAllowed = 0;
        int gameCount = 0;

        foreach (var g in teamGames)
        {
            bool isHome = g.home_team_id == teamId;
            int scored = isHome ? g.home_score : g.away_score;
            int allowed = isHome ? g.away_score : g.home_score;
            totalPtsScored += scored;
            totalPtsAllowed += allowed;
            gameCount++;
        }

        if (gameCount > 0)
        {
            side.offRating = totalPtsScored / gameCount;
            side.defRating = totalPtsAllowed / gameCount;
        }
    }

    static void AssignRanks(TeamPreviewSide side,
        Dictionary<int, float> offRatings,
        Dictionary<int, float> defRatings)
    {
        var allTeamIds = offRatings.Keys.ToList();
        side.offRank = GetRank(side.offRating, allTeamIds, offRatings, ascending: false);
        side.defRank = GetRank(side.defRating, allTeamIds, defRatings, ascending: true);
    }

    static int GetRank(float value, List<int> teamIds, Dictionary<int, float> ratings, bool ascending)
    {
        int rank = 1;
        foreach (var tid in teamIds)
        {
            if (!ratings.TryGetValue(tid, out var v)) continue;
            if (ascending ? v < value : v > value) rank++;
        }
        return rank;
    }

    static List<PlayerData> GetStartersFromLineup(
        List<PlayerData> availablePlayers, DatabaseManager db, int teamId)
    {
        var lineupStarters = db.GetStarters(teamId)
            .OrderBy(l => l.slot_index)
            .Select(l => availablePlayers.FirstOrDefault(p => p.id == l.player_id))
            .Where(p => p != null && p.injury_days == 0 && p.g_league_assigned == 0)
            .ToList();

        if (lineupStarters.Count == 5)
            return lineupStarters;

        return availablePlayers
            .OrderByDescending(p => p.overall)
            .GroupBy(p => p.position)
            .Select(g => g.First())
            .OrderBy(p => Array.IndexOf(PositionCodes.Order, p.position))
            .Take(5).ToList();
    }

    static List<PlayerData> GetBenchFromLineup(
        List<PlayerData> availablePlayers, DatabaseManager db,
        int teamId, List<PlayerData> starters)
    {
        var starterIds = new HashSet<int>(starters.Select(p => p.id));
        var lineupBench = db.GetBench(teamId)
            .OrderBy(l => l.slot_index)
            .Select(l => availablePlayers.FirstOrDefault(p => p.id == l.player_id))
            .Where(p => p != null && p.injury_days == 0 && p.g_league_assigned == 0
                       && !starterIds.Contains(p.id))
            .Take(7)
            .ToList();

        if (lineupBench.Count > 0)
            return lineupBench;

        return availablePlayers
            .Where(p => !starterIds.Contains(p.id))
            .OrderByDescending(p => p.overall).Take(7).ToList();
    }

    static void ComputeKeyPlayers(TeamPreviewSide side, DatabaseManager db,
        int teamId, int managerId, int seasonId)
    {
        var players = db.GetPlayersByTeam(teamId).Where(p => p.injury_days == 0 && p.g_league_assigned == 0).ToList();
        if (players.Count == 0) return;

        var allTeamPlayers = db.GetPlayersByTeam(teamId);

        var seasonGames = db.GetSeasonGames(managerId, seasonId)
            .Where(g => g.is_played == 1 && g.game_type == "regular").ToList();
        var gameIds = seasonGames.Select(g => g.id).ToList();
        if (gameIds.Count == 0) return;

        var allStats = db.GetGamePlayerStatsBatch(gameIds)
            .Where(s => s.team_id == teamId && s.minutes > 0)
            .GroupBy(s => s.player_id)
            .Select(g => new {
                player_id = g.Key,
                avgPts = g.Average(s => s.points),
                avgReb = g.Average(s => s.rebounds),
                avgAst = g.Average(s => s.assists),
                avgBlk = g.Average(s => s.blocks),
                games = g.Count()
            })
            .Where(x => x.games >= 1)
            .ToList();

        if (allStats.Count == 0) return;

        var topPts = allStats.OrderByDescending(x => x.avgPts).First();
        var topReb = allStats.OrderByDescending(x => x.avgReb).First();
        var topAst = allStats.OrderByDescending(x => x.avgAst).First();
        var topBlk = allStats.OrderByDescending(x => x.avgBlk).First();

        side.keyPts = allTeamPlayers.FirstOrDefault(p => p.id == topPts.player_id);
        side.keyPtsVal = (float)topPts.avgPts;
        side.keyReb = allTeamPlayers.FirstOrDefault(p => p.id == topReb.player_id);
        side.keyRebVal = (float)topReb.avgReb;
        side.keyAst = allTeamPlayers.FirstOrDefault(p => p.id == topAst.player_id);
        side.keyAstVal = (float)topAst.avgAst;
        side.keyBlk = allTeamPlayers.FirstOrDefault(p => p.id == topBlk.player_id);
        side.keyBlkVal = (float)topBlk.avgBlk;
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
