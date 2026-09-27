using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Motor de narrativa (storylines). Convierte los resultados reales de la partida en
/// HILOS con continuidad: se abren al cruzar un umbral, escalan si la situación sigue
/// y se cierran cuando termina. Cada transición genera una noticia de PRENSA y aplica
/// consecuencias suaves sobre la moral, la química y la confianza de la afición.
/// La lógica de decisión es pura y testeable; `ProcessDay` es la parte con DB.
/// </summary>
public static class StorylineService
{
    /// <summary>Umbral para abrir un hilo de racha.</summary>
    public const int STREAK_START = 5;
    /// <summary>Cada cuántas victorias/derrotas sube un escalón.</summary>
    public const int STREAK_STEP = 5;

    public enum Action { None, Start, Escalate, Resolve }

    // ── LÓGICA PURA ─────────────────────────────────────────

    /// <summary>Racha actual del equipo: positiva = victorias seguidas, negativa = derrotas.</summary>
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

    /// <summary>
    /// Decide la transición: abrir, escalar, cerrar o nada. Pura.
    /// </summary>
    /// <param name="streak">Racha actual (con signo).</param>
    /// <param name="activeType">Tipo del hilo abierto (null si no hay).</param>
    /// <param name="activeIntensity">Escalón del hilo abierto.</param>
    public static Action Decide(int streak, string activeType, int activeIntensity)
    {
        int milestone = Milestone(streak);
        string type = TypeFor(streak);

        if (activeType == null)
            return type == null ? Action.None : Action.Start;

        // El hilo está abierto: si la racha cambió de signo o se rompió, se cierra.
        if (type != activeType)
            return Action.Resolve;

        return milestone > activeIntensity ? Action.Escalate : Action.None;
    }

    /// <summary>Efectos (moral, química, afición) de abrir o escalar un hilo.</summary>
    public static (int morale, int chemistry, int fan) Effects(string type, int milestone)
    {
        int sign = type == StorylineType.RachaPositiva ? 1 : -1;
        int magnitude = Mathf.Clamp(milestone, 1, 4); // 1..4 escalones
        return (sign * (1 + magnitude), sign * magnitude, sign * (2 + magnitude));
    }

    // ── DÍA A DÍA (DB) ──────────────────────────────────────

    /// <summary>Evalúa los hilos del equipo del usuario tras simular el día.</summary>
    public static void ProcessDay(ManagerData manager, TeamData myTeam, SeasonData season, int gameDay, string gameDate)
    {
        if (manager == null || myTeam == null || season == null) return;

        var played = DatabaseManager.Instance.Db.Table<GameData>()
            .Where(g => g.manager_id == manager.id && g.season_id == season.id
                     && g.is_played == 1 && g.game_type == "regular")
            .OrderBy(g => g.game_day).ThenBy(g => g.id)
            .ToList();
        if (played.Count == 0) return;

        int streak = CurrentStreak(played, myTeam.id);
        var active = DatabaseManager.Instance.GetActiveStoryline(manager.id, myTeam.id);

        switch (Decide(streak, active?.type, active?.intensity ?? 0))
        {
            case Action.Start:
                OpenStoryline(manager, myTeam, streak, gameDay, gameDate);
                break;
            case Action.Escalate:
                EscalateStoryline(manager, myTeam, active, streak, gameDay, gameDate);
                break;
            case Action.Resolve:
                CloseStoryline(manager, myTeam, active, gameDay, gameDate);
                break;
        }
    }

    static void OpenStoryline(ManagerData manager, TeamData myTeam, int streak, int gameDay, string gameDate)
    {
        string type = TypeFor(streak);
        int milestone = Milestone(streak);
        string title = type == StorylineType.RachaPositiva
            ? $"Racha de {Mathf.Abs(streak)} victorias"
            : $"Bache de {Mathf.Abs(streak)} derrotas";

        DatabaseManager.Instance.InsertStoryline(new StorylineData
        {
            manager_id = manager.id,
            team_id = myTeam.id,
            type = type,
            title = title,
            intensity = milestone,
            started_game_day = gameDay,
            last_update_game_day = gameDay,
            resolved = 0,
            started_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });

        ApplyEffects(manager, myTeam, type, milestone);
        AddStorylineNews(manager, myTeam, gameDay, gameDate,
            type == StorylineType.RachaPositiva
                ? $"Racha de {Mathf.Abs(streak)} triunfos consecutivos"
                : $"Bache de {Mathf.Abs(streak)} derrotas",
            type == StorylineType.RachaPositiva
                ? $"El {myTeam.name} está en racha: {Mathf.Abs(streak)} victorias seguidas. El vestuario respira confianza y la afición se ilusiona con el proyecto.\n\nEl equipo ha encontrado su identidad y los rivales ya lo miran con respeto. Habrá que ver si mantiene este nivel cuando lleguen los partidos exigentes."
                : $"El {myTeam.name} atraviesa un bache serio: {Mathf.Abs(streak)} derrotas consecutivas. La prensa pregunta por el rumbo del proyecto y el vestuario empieza a mostrar signos de tensión.\n\nEl equipo necesita una reacción inmediata antes de que la situación se enquiste.",
            type == StorylineType.RachaPositiva ? NewsType.RACHA_VICTORIAS : NewsType.MALA_RACHA);
    }

    static void EscalateStoryline(ManagerData manager, TeamData myTeam, StorylineData active, int streak, int gameDay, string gameDate)
    {
        int milestone = Milestone(streak);
        active.intensity = milestone;
        active.last_update_game_day = gameDay;
        DatabaseManager.Instance.UpdateStoryline(active);

        ApplyEffects(manager, myTeam, active.type, milestone);
        AddStorylineNews(manager, myTeam, gameDay, gameDate,
            active.type == StorylineType.RachaPositiva
                ? $"La racha sigue creciendo: {Mathf.Abs(streak)} victorias"
                : $"La crisis se agrava: {Mathf.Abs(streak)} derrotas",
            active.type == StorylineType.RachaPositiva
                ? $"La racha del {myTeam.name} ya es de {Mathf.Abs(streak)} victorias. Lo que empezó como un buen momento se ha convertido en una de las historias de la temporada.\n\nLos analistas empiezan a hablar del equipo como un serio candidato."
                : $"El {myTeam.name} suma ya {Mathf.Abs(streak)} derrotas seguidas. La situación es crítica: los rumores sobre cambios en la plantilla y en el banquillo no dejan de crecer.\n\nHace falta algo más que un buen partido para dar la vuelta a esto.",
            active.type == StorylineType.RachaPositiva ? NewsType.RACHA_VICTORIAS : NewsType.MALA_RACHA);
    }

    static void CloseStoryline(ManagerData manager, TeamData myTeam, StorylineData active, int gameDay, string gameDate)
    {
        active.resolved = 1;
        active.last_update_game_day = gameDay;
        DatabaseManager.Instance.UpdateStoryline(active);

        AddStorylineNews(manager, myTeam, gameDay, gameDate,
            active.type == StorylineType.RachaPositiva ? "Fin de la racha" : "Fin del bache",
            active.type == StorylineType.RachaPositiva
                ? $"Se ha acabado la racha del {myTeam.name}. Tras {active.intensity * StorylineService.STREAK_STEP} victorias seguidas, llega la derrota y toca volver a empezar.\n\nEl equipo deja atrás un tramo brillante que deja al aficionado con ganas de más."
                : $"El {myTeam.name} respira: se rompe el bache de {active.intensity * StorylineService.STREAK_STEP} derrotas. Una victoria que vale más que dos puntos y que puede marcar el punto de inflexión.\n\nEl vestuario recupera algo de aire para las próximas semanas.",
            active.type == StorylineType.RachaPositiva ? NewsType.RACHA_VICTORIAS : NewsType.MALA_RACHA);
    }

    static void ApplyEffects(ManagerData manager, TeamData myTeam, string type, int milestone)
    {
        var (morale, chemistry, fan) = Effects(type, milestone);

        if (chemistry != 0)
            DatabaseManager.Instance.UpdateTeamChemistry(myTeam.id,
                Mathf.Clamp(myTeam.team_chemistry + chemistry, 0, 100));
        if (fan != 0)
        {
            manager.fan_confidence = Mathf.Clamp(manager.fan_confidence + fan, 0, 100);
            DatabaseManager.Instance.SaveManager(manager);
        }

        if (morale != 0)
        {
            foreach (var p in DatabaseManager.Instance.GetPlayersByTeam(myTeam.id))
                DatabaseManager.Instance.UpdatePlayerMorale(p.id, Mathf.Clamp(p.morale + morale, 0, 100));
        }
    }

    static void AddStorylineNews(ManagerData manager, TeamData myTeam, int gameDay, string gameDate,
                                 string title, string body, string newsType)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = manager.id,
            sender_type = 2,
            sender_id = 0,
            title = title,
            body = body,
            game_day = gameDay,
            game_date = gameDate,
            created_at = now,
            date_sent = now,
            is_read = 0,
            category = NewsCategory.PRENSA,
            news_type = newsType
        });
    }
}
