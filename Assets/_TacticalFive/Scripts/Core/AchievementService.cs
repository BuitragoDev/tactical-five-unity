using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Detección y desbloqueo de logros del GM. Todos los hooks se invocan desde el
// hilo principal; el desbloqueo es idempotente (INSERT OR IGNORE).
public static class AchievementService
{
    private static readonly List<GmAchievementDefinition> _pendingToasts = new();

    // Toasts pendientes de mostrar (cola consumida por DashboardController.Update).
    public static GmAchievementDefinition TakeNextToast()
    {
        if (_pendingToasts.Count == 0) return null;
        var def = _pendingToasts[0];
        _pendingToasts.RemoveAt(0);
        return def;
    }

    public static bool UnlockIfMissing(int managerId, GmAchievementType type, int? seasonId = null, string seasonLabel = null, bool notify = true)
    {
        bool fresh = DatabaseManager.Instance.UnlockAchievement(managerId, type.ToString(), seasonId, seasonLabel);
        if (fresh)
        {
            var def = AchievementCatalog.Get(type);
            if (def != null)
            {
                Debug.Log($"[Logros] Desbloqueado: {def.Title}");
                if (notify) _pendingToasts.Add(def);
            }
        }
        return fresh;
    }

    // Backfill silencioso para carreras ya avanzadas (se invoca al abrir la pantalla).
    public static void BackfillCareer(int managerId, int teamId, int seasonId)
    {
        var manager = DatabaseManager.Instance.GetActiveManager();
        var season = DatabaseManager.Instance.GetActiveSeason(managerId);
        if (manager == null) return;
        string label = season != null ? SeasonLabel(season) : null;

        int careerWins = manager.career_reg_wins;
        if (careerWins >= 250) UnlockIfMissing(managerId, GmAchievementType.career_wins_250, seasonId, label, false);
        if (careerWins >= 500) UnlockIfMissing(managerId, GmAchievementType.career_wins_500, seasonId, label, false);
        if (careerWins >= 1000) UnlockIfMissing(managerId, GmAchievementType.career_wins_1000, seasonId, label, false);

        if (manager.championships >= 3) UnlockIfMissing(managerId, GmAchievementType.dynastia_3, seasonId, label, false);
        if (manager.championships >= 5) UnlockIfMissing(managerId, GmAchievementType.dynastia_5, seasonId, label, false);

        if (manager.seasons_completed >= 10) UnlockIfMissing(managerId, GmAchievementType.seasons_10, seasonId, label, false);
        if (manager.seasons_completed >= 20) UnlockIfMissing(managerId, GmAchievementType.seasons_20, seasonId, label, false);

        if (manager.trust >= 90) UnlockIfMissing(managerId, GmAchievementType.trust_90, seasonId, label, false);
    }

    static bool DidTeamWin(GameData g, int teamId)
    {
        if (g.home_team_id == teamId) return g.home_score > g.away_score;
        if (g.away_team_id == teamId) return g.away_score > g.home_score;
        return false;
    }

    static List<GameData> MyTeamSeasonGames(int managerId, int teamId)
    {
        return DatabaseManager.Instance.GetStandingsGames(managerId)
            .Where(g => g.home_team_id == teamId || g.away_team_id == teamId)
            .OrderBy(g => g.game_day)
            .ThenBy(g => g.id)
            .ToList();
    }

    static int ComputeMaxWinStreak(List<GameData> games, int teamId)
    {
        int max = 0, cur = 0;
        foreach (var g in games)
        {
            if (DidTeamWin(g, teamId)) { cur++; if (cur > max) max = cur; }
            else cur = 0;
        }
        return max;
    }

    static string SeasonLabel(SeasonData season)
    {
        return $"{season.year_start}-{season.year_end.ToString().Substring(2)}";
    }

    // ── DÍA SIMULADO ───────────────────────────────────────

    public static void EvaluateGameDay(int managerId, int teamId, int seasonId)
    {
        var season = DatabaseManager.Instance.GetActiveSeason(managerId);
        var manager = DatabaseManager.Instance.GetActiveManager();
        if (season == null || manager == null) return;

        var games = MyTeamSeasonGames(managerId, teamId);
        if (games.Count == 0) return;

        int wins = games.Count(g => DidTeamWin(g, teamId));
        int losses = games.Count(g => !DidTeamWin(g, teamId));

        // Primer partido / primera victoria de la carrera del GM
        int totalCareerGames = manager.career_reg_wins + manager.career_reg_losses + wins + losses;
        if (totalCareerGames == 1)
            UnlockIfMissing(managerId, GmAchievementType.first_game, season.id, SeasonLabel(season));
        if (manager.career_reg_wins + wins >= 1)
            UnlockIfMissing(managerId, GmAchievementType.first_win, season.id, SeasonLabel(season));

        int maxStreak = ComputeMaxWinStreak(games, teamId);
        if (maxStreak >= 5) UnlockIfMissing(managerId, GmAchievementType.win_streak_5, season.id, SeasonLabel(season));
        if (maxStreak >= 10) UnlockIfMissing(managerId, GmAchievementType.win_streak_10, season.id, SeasonLabel(season));

        if (wins >= 30) UnlockIfMissing(managerId, GmAchievementType.reg_wins_30, season.id, SeasonLabel(season));
        if (wins >= 50) UnlockIfMissing(managerId, GmAchievementType.reg_wins_50, season.id, SeasonLabel(season));
        if (wins >= 60) UnlockIfMissing(managerId, GmAchievementType.reg_wins_60, season.id, SeasonLabel(season));
    }

    // ── FIN DE TEMPORADA ───────────────────────────────────

    public static void EvaluateSeasonEnd(int managerId, int teamId, int seasonId)
    {
        var season = DatabaseManager.Instance.GetActiveSeason(managerId);
        var manager = DatabaseManager.Instance.GetActiveManager();
        var team = DatabaseManager.Instance.GetTeamById(teamId);
        if (season == null || manager == null || team == null) return;

        string label = SeasonLabel(season);
        var games = MyTeamSeasonGames(managerId, teamId);
        int wins = games.Count(g => DidTeamWin(g, teamId));

        var finalsRecords = DatabaseManager.Instance.GetFinalsRecords();
        bool champion = finalsRecords.Any(f => f.season == label && f.champ_name == team.name);

        bool madePlayoffs = DatabaseManager.Instance.Db.Table<GameData>()
            .Any(g => g.manager_id == managerId && g.season_id == seasonId
                   && g.game_type == "playoff"
                   && (g.home_team_id == teamId || g.away_team_id == teamId));
        bool finalsApp = DatabaseManager.Instance.Db.Table<GameData>()
            .Any(g => g.manager_id == managerId && g.season_id == seasonId
                   && g.series_label == "playoff-r4-finals"
                   && (g.home_team_id == teamId || g.away_team_id == teamId));

        // Premios de la temporada (solo los que el juego otorga de verdad)
        var mvp = DatabaseManager.Instance.GetRegularSeasonMVP(seasonId, managerId);
        if (mvp != null && mvp.TeamName == team.name)
            UnlockIfMissing(managerId, GmAchievementType.mvp_player, seasonId, label);

        var roty = DatabaseManager.Instance.GetRookieOfYear(seasonId, managerId);
        if (roty != null && roty.TeamName == team.name)
            UnlockIfMissing(managerId, GmAchievementType.roty_player, seasonId, label);

        var five = DatabaseManager.Instance.GetAllStarTeam(seasonId, managerId);
        if (five.Any(p => p.TeamName == team.name))
            UnlockIfMissing(managerId, GmAchievementType.first_team, seasonId, label);

        var monthly = DatabaseManager.Instance.GetMonthlyAwardsForSeason(seasonId);
        if (monthly.Any(m => m.award_type == "manager" && m.manager_id == managerId && m.rank == 1))
            UnlockIfMissing(managerId, GmAchievementType.manager_month, seasonId, label);

        // Playoffs / campeonato
        if (madePlayoffs)
            UnlockIfMissing(managerId, GmAchievementType.make_playoffs, seasonId, label);
        if (finalsApp)
            UnlockIfMissing(managerId, GmAchievementType.finals_appearance, seasonId, label);
        if (champion)
        {
            UnlockIfMissing(managerId, GmAchievementType.champion, seasonId, label);
            if (manager.championships == 0)
                UnlockIfMissing(managerId, GmAchievementType.first_ring, seasonId, label);

            // Bicampeón: campeón la temporada anterior
            string prevLabel = $"{season.year_start - 1}-{(season.year_end - 1) % 100:00}";
            if (finalsRecords.Any(f => f.season == prevLabel && f.champ_name == team.name))
                UnlockIfMissing(managerId, GmAchievementType.back_to_back, seasonId, label);

            int totalRings = manager.championships + 1;
            if (totalRings >= 3) UnlockIfMissing(managerId, GmAchievementType.dynastia_3, seasonId, label);
            if (totalRings >= 5) UnlockIfMissing(managerId, GmAchievementType.dynastia_5, seasonId, label);
        }

        // Victorias de temporada y de carrera (la temporada actual aún no se ha archivado)
        if (wins >= 30) UnlockIfMissing(managerId, GmAchievementType.reg_wins_30, seasonId, label);
        if (wins >= 50) UnlockIfMissing(managerId, GmAchievementType.reg_wins_50, seasonId, label);
        if (wins >= 60) UnlockIfMissing(managerId, GmAchievementType.reg_wins_60, seasonId, label);

        int totalCareerWins = manager.career_reg_wins + wins;
        if (totalCareerWins >= 250) UnlockIfMissing(managerId, GmAchievementType.career_wins_250, seasonId, label);
        if (totalCareerWins >= 500) UnlockIfMissing(managerId, GmAchievementType.career_wins_500, seasonId, label);
        if (totalCareerWins >= 1000) UnlockIfMissing(managerId, GmAchievementType.career_wins_1000, seasonId, label);

        int seasonsCompleted = manager.seasons_completed + 1;
        if (seasonsCompleted >= 10) UnlockIfMissing(managerId, GmAchievementType.seasons_10, seasonId, label);
        if (seasonsCompleted >= 20) UnlockIfMissing(managerId, GmAchievementType.seasons_20, seasonId, label);

        if (manager.trust >= 90)
            UnlockIfMissing(managerId, GmAchievementType.trust_90, seasonId, label);
    }

    // ── MERCADO ────────────────────────────────────────────

    public static void EvaluateSignStarFA(int managerId, int teamId, int seasonId, PlayerData player)
    {
        if (player == null) return;
        if (player.GetCalculatedAverage() >= 85)
        {
            var season = DatabaseManager.Instance.GetActiveSeason(managerId);
            UnlockIfMissing(managerId, GmAchievementType.sign_star_fa, seasonId,
                season != null ? SeasonLabel(season) : null);
        }
    }

    public static void EvaluateSignAndTrade(int managerId, int teamId, int seasonId)
    {
        var season = DatabaseManager.Instance.GetActiveSeason(managerId);
        UnlockIfMissing(managerId, GmAchievementType.sign_and_trade, seasonId,
            season != null ? SeasonLabel(season) : null);
    }

    public static void EvaluateTradeStar(int managerId, int teamId, int seasonId, List<PlayerData> acquired)
    {
        if (acquired == null || acquired.Count == 0) return;
        if (!acquired.Any(p => p != null && p.GetCalculatedAverage() >= 90)) return;
        var season = DatabaseManager.Instance.GetActiveSeason(managerId);
        UnlockIfMissing(managerId, GmAchievementType.trade_star, seasonId,
            season != null ? SeasonLabel(season) : null);
    }

    public static void EvaluateRecordBreak(int managerId, int teamId, int seasonId)
    {
        var season = DatabaseManager.Instance.GetActiveSeason(managerId);
        UnlockIfMissing(managerId, GmAchievementType.break_league_record, seasonId,
            season != null ? SeasonLabel(season) : null);
    }
}
