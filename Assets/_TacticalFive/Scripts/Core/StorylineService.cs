using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Motor de narrativa (storylines). Convierte los resultados reales de la partida en
/// HILOS con continuidad: se abren al cruzar un umbral, escalan si la situación sigue
/// y se cierran cuando termina. Cada transición genera una noticia de PRENSA y aplica
/// consecuencias suaves (con presupuesto diario) sobre moral, química y afición.
///
/// Los tipos y sus detectores viven en StorylineCatalog (lógica pura y testeable);
/// aquí está el ciclo de vida y la persistencia.
/// </summary>
public static class StorylineService
{
    /// <summary>Umbral para abrir un hilo de racha.</summary>
    public const int STREAK_START = 5;
    /// <summary>Cada cuántas victorias/derrotas sube un escalón.</summary>
    public const int STREAK_STEP = 5;
    /// <summary>Máximo de hilos abiertos a la vez por equipo.</summary>
    public const int MAX_ACTIVE = 3;

    public enum Action { None, Start, Escalate, Resolve }

    // ── LÓGICA PURA ─────────────────────────────────────────

    /// <summary>Racha actual del equipo: positiva = victorias, negativa = derrotas.</summary>
    public static int CurrentStreak(List<GameData> playedGames, int teamId)
    {
        int streak = 0;
        for (int i = playedGames.Count - 1; i >= 0; i--)
        {
            var g = playedGames[i];
            if (g.is_played != 1) continue;
            bool home = g.home_team_id == teamId;
            bool won = home ? g.home_score > g.away_score : g.away_score > g.home_score;

            if (streak == 0) streak = won ? 1 : -1;
            else if (won && streak > 0) streak++;
            else if (!won && streak < 0) streak--;
            else break; // cambió el signo: la racha terminó
        }
        return streak;
    }

    /// <summary>Escalón alcanzado por una racha (0 si no llega al umbral).</summary>
    public static int Milestone(int streak)
    {
        int abs = Mathf.Abs(streak);
        return abs < STREAK_START ? 0 : abs / STREAK_STEP;
    }

    /// <summary>Tipo de hilo que corresponde a una racha (null si no procede).</summary>
    public static string TypeFor(int streak) => Milestone(streak) == 0
        ? null
        : streak > 0 ? StorylineType.RachaPositiva : StorylineType.Bache;

    /// <summary>Efectos (moral, química, afición) de abrir o escalar un hilo.</summary>
    public static (int morale, int chemistry, int fan) Effects(string type, int milestone)
    {
        int sign = type == StorylineType.RachaPositiva ? 1 : -1;
        int magnitude = Mathf.Clamp(milestone, 1, 4);
        return (sign * (1 + magnitude), sign * magnitude, sign * (2 + magnitude));
    }

    // ── DÍA A DÍA (DB) ──────────────────────────────────────

    /// <summary>Evalúa todos los hilos del equipo del usuario tras simular el día.</summary>
    public static void ProcessDay(ManagerData manager, TeamData myTeam, SeasonData season, int gameDay, string gameDate)
    {
        if (manager == null || myTeam == null || season == null) return;

        var ctx = StorylineContext.Build(manager, myTeam, season, gameDay, gameDate);
        if (ctx == null || ctx.GamesPlayed == 0) return;

        foreach (var def in StorylineCatalog.All)
        {
            var active = DatabaseManager.Instance.GetActiveStoryline(manager.id, myTeam.id, def.type);

            List<StorylineTransition> transitions;
            try
            {
                transitions = def.detect(ctx, def, active) ?? new List<StorylineTransition>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Storyline] Error en '{def.type}': {e.Message}");
                continue;
            }

            foreach (var t in transitions)
            {
                if (t.action == Action.Start && !CanOpen(manager.id, myTeam.id, def, gameDay)) continue;
                Apply(ctx, active, t);
            }
        }
    }

    /// <summary>Máximo de hilos activos y cooldown por tipo.</summary>
    static bool CanOpen(int managerId, int teamId, StorylineDef def, int gameDay)
    {
        if (DatabaseManager.Instance.GetActiveStorylines(managerId, teamId).Count >= MAX_ACTIVE)
            return false;

        var last = DatabaseManager.Instance.GetLastStoryline(managerId, teamId, def.type);
        if (last != null && gameDay - last.last_update_game_day < def.cooldownDays) return false;
        return true;
    }

    static void Apply(StorylineContext ctx, StorylineData active, StorylineTransition t)
    {
        switch (t.action)
        {
            case Action.Start:
                DatabaseManager.Instance.InsertStoryline(new StorylineData
                {
                    manager_id = ctx.manager.id,
                    team_id = ctx.myTeam.id,
                    type = t.type,
                    subject_player_id = t.subjectPlayerId,
                    title = t.title,
                    summary = t.summary,
                    intensity = Mathf.Max(1, t.intensity),
                    started_game_day = ctx.gameDay,
                    last_update_game_day = ctx.gameDay,
                    last_news_game_day = ctx.gameDay,
                    resolved = 0,
                    resolution = "",
                    started_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
                break;

            case Action.Escalate:
                if (active == null) return;
                active.intensity = Mathf.Max(active.intensity + 1, t.intensity);
                active.last_update_game_day = ctx.gameDay;
                active.last_news_game_day = ctx.gameDay;
                if (!string.IsNullOrEmpty(t.title)) active.title = t.title;
                if (!string.IsNullOrEmpty(t.summary)) active.summary = t.summary;
                DatabaseManager.Instance.UpdateStoryline(active);
                break;

            case Action.Resolve:
                if (active == null) return;
                active.resolved = 1;
                active.resolution = string.IsNullOrEmpty(t.resolution) ? StorylineResolution.Broken : t.resolution;
                active.last_update_game_day = ctx.gameDay;
                if (!string.IsNullOrEmpty(t.summary)) active.summary = t.summary;
                DatabaseManager.Instance.UpdateStoryline(active);
                break;

            default:
                return;
        }

        ApplyEffects(ctx, t);
        if (!string.IsNullOrEmpty(t.body)) AddStorylineNews(ctx, t);
    }

    /// <summary>Aplica consecuencias con presupuesto diario (no duplica en exceso lo
    /// que ya mueven los partidos: moral por forma/rol y afición por resultado).</summary>
    static void ApplyEffects(StorylineContext ctx, StorylineTransition t)
    {
        int fan = Mathf.Clamp(t.fan, -5, 5);
        int chem = Mathf.Clamp(t.chemistry, -3, 3);
        int morale = Mathf.Clamp(t.morale, -3, 3);

        if (chem != 0)
            DatabaseManager.Instance.UpdateTeamChemistry(ctx.myTeam.id,
                Mathf.Clamp(ctx.myTeam.team_chemistry + chem, 0, 100));

        if (fan != 0)
        {
            ctx.manager.fan_confidence = Mathf.Clamp(ctx.manager.fan_confidence + fan, 0, 100);
            DatabaseManager.Instance.SaveManager(ctx.manager);
        }

        if (morale == 0) return;
        if (t.subjectPlayerId > 0)
        {
            var p = ctx.FindPlayer(t.subjectPlayerId);
            if (p != null)
                DatabaseManager.Instance.UpdatePlayerMorale(p.id, Mathf.Clamp(p.morale + morale, 0, 100));
        }
        else
        {
            foreach (var p in ctx.Roster)
                DatabaseManager.Instance.UpdatePlayerMorale(p.id, Mathf.Clamp(p.morale + morale, 0, 100));
        }
    }

    static void AddStorylineNews(StorylineContext ctx, StorylineTransition t)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = ctx.manager.id,
            sender_type = 2,
            sender_id = 0,
            title = t.title,
            body = t.body,
            game_day = ctx.gameDay,
            game_date = ctx.gameDate,
            created_at = now,
            date_sent = now,
            is_read = 0,
            category = NewsCategory.PRENSA,
            news_type = t.newsType
        });
    }
}
