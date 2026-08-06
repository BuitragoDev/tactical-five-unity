public enum GmAchievementCategory
{
    PrimerosPasos,
    Temporada,
    JugadorPremiado,
    Playoffs,
    Carrera,
    Mercado
}

public class GmAchievementDefinition
{
    public GmAchievementType Type;
    public GmAchievementCategory Category;
    public string Title;
    public string Description;
    public string Icon;
    // Progreso opcional para logros escalonados (p.ej. victorias 250/500/1000).
    public int ProgressValue;
    public int ProgressTarget;

    public GmAchievementDefinition(GmAchievementType type, GmAchievementCategory category,
        string title, string description, string icon = "trofeo64px",
        int progressValue = 0, int progressTarget = 0)
    {
        Type = type;
        Category = category;
        Title = title;
        Description = description;
        Icon = icon;
        ProgressValue = progressValue;
        ProgressTarget = progressTarget;
    }
}

public static class AchievementCatalog
{
    public static readonly GmAchievementDefinition[] All =
    {
        // ── Primeros pasos ──────────────────────────────────
        new(GmAchievementType.first_game, GmAchievementCategory.PrimerosPasos,
            "Primera vez", "Juega tu primer partido como GM.", "calendario"),
        new(GmAchievementType.first_win, GmAchievementCategory.PrimerosPasos,
            "Primera victoria", "Gana tu primer partido de temporada regular.", "inicio"),
        new(GmAchievementType.first_ring, GmAchievementCategory.PrimerosPasos,
            "El primero", "Gana tu primer campeonato.", "trofeo64px"),

        // ── Temporada ──────────────────────────────────────
        new(GmAchievementType.reg_wins_30, GmAchievementCategory.Temporada,
            "Temporada sólida", "Alcanza 30 victorias en una temporada regular.", "clasificacion"),
        new(GmAchievementType.reg_wins_50, GmAchievementCategory.Temporada,
            "50 victorias", "Alcanza 50 victorias en una temporada regular.", "clasificacion", progressValue: 50, progressTarget: 50),
        new(GmAchievementType.reg_wins_60, GmAchievementCategory.Temporada,
            "Élite de la liga", "Alcanza 60 victorias en una temporada regular.", "clasificacion", progressValue: 60, progressTarget: 60),
        new(GmAchievementType.win_streak_5, GmAchievementCategory.Temporada,
            "En racha", "Encadena 5 victorias consecutivas.", "playoff", progressValue: 5, progressTarget: 5),
        new(GmAchievementType.win_streak_10, GmAchievementCategory.Temporada,
            "Imparable", "Encadena 10 victorias consecutivas.", "playoff", progressValue: 10, progressTarget: 10),
        new(GmAchievementType.manager_month, GmAchievementCategory.Temporada,
            "Mejor entrenador del mes", "Gana el premio de entrenador del mes.", "manager_mes"),

        // ── Jugador premiado ───────────────────────────────
        new(GmAchievementType.mvp_player, GmAchievementCategory.JugadorPremiado,
            "MVP en tu plantilla", "Un jugador de tu equipo gana el MVP de la temporada.", "trofeo"),
        new(GmAchievementType.roty_player, GmAchievementCategory.JugadorPremiado,
            "Mejor novato", "Un jugador de tu equipo gana el Rookie del Año.", "trofeo"),
        new(GmAchievementType.first_team, GmAchievementCategory.JugadorPremiado,
            "Entra en el quinteto ideal", "Un jugador de tu equipo entra en el quinteto ideal de la temporada.", "star_24px"),

        // ── Playoffs ───────────────────────────────────────
        new(GmAchievementType.make_playoffs, GmAchievementCategory.Playoffs,
            "Postemporada", "Clasifica a los playoffs.", "playoff"),
        new(GmAchievementType.finals_appearance, GmAchievementCategory.Playoffs,
            "A las Finales", "Llega a las Finales de la liga.", "playoff"),
        new(GmAchievementType.champion, GmAchievementCategory.Playoffs,
            "Campeón", "Gana el campeonato.", "trofeo64px"),
        new(GmAchievementType.back_to_back, GmAchievementCategory.Playoffs,
            "Bicampeón", "Gana dos campeonatos consecutivos.", "trofeo64px"),
        new(GmAchievementType.dynastia_3, GmAchievementCategory.Playoffs,
            "Dinastía", "Gana 3 campeonatos en tu carrera.", "trofeo64px", progressValue: 3, progressTarget: 3),
        new(GmAchievementType.dynastia_5, GmAchievementCategory.Playoffs,
            "Leyenda", "Gana 5 campeonatos en tu carrera.", "trofeo64px", progressValue: 5, progressTarget: 5),

        // ── Carrera ────────────────────────────────────────
        new(GmAchievementType.career_wins_250, GmAchievementCategory.Carrera,
            "Veterano", "Alcanza 250 victorias en temporada regular en tu carrera.", "manager", progressValue: 250, progressTarget: 250),
        new(GmAchievementType.career_wins_500, GmAchievementCategory.Carrera,
            "500 victorias", "Alcanza 500 victorias en temporada regular en tu carrera.", "manager", progressValue: 500, progressTarget: 500),
        new(GmAchievementType.career_wins_1000, GmAchievementCategory.Carrera,
            "Maestro", "Alcanza 1000 victorias en temporada regular en tu carrera.", "manager", progressValue: 1000, progressTarget: 1000),
        new(GmAchievementType.seasons_10, GmAchievementCategory.Carrera,
            "10 temporadas", "Completa 10 temporadas al frente de un equipo.", "calendario", progressValue: 10, progressTarget: 10),
        new(GmAchievementType.seasons_20, GmAchievementCategory.Carrera,
            "20 temporadas", "Completa 20 temporadas al frente de un equipo.", "calendario", progressValue: 20, progressTarget: 20),
        new(GmAchievementType.trust_90, GmAchievementCategory.Carrera,
            "Confianza absoluta", "Alcanza 90 de confianza de la directiva.", "manager"),

        // ── Mercado ────────────────────────────────────────
        new(GmAchievementType.sign_star_fa, GmAchievementCategory.Mercado,
            "Fichaje estrella", "Firma a un agente libre de 85+ de valoración.", "intercambio"),
        new(GmAchievementType.sign_and_trade, GmAchievementCategory.Mercado,
            "Maestro del S&T", "Ejecuta un sign-and-trade.", "intercambio"),
        new(GmAchievementType.trade_star, GmAchievementCategory.Mercado,
            "Golpe de mercado", "Adquiere por traspaso a un jugador de 90+ de valoración.", "intercambio"),
        new(GmAchievementType.break_league_record, GmAchievementCategory.Mercado,
            "Récord histórico", "Un jugador de tu equipo rompe un récord histórico de la liga.", "records")
    };

    public static GmAchievementDefinition Get(GmAchievementType type)
    {
        foreach (var def in All)
            if (def.Type == type) return def;
        return null;
    }

    public static string CategoryName(GmAchievementCategory category)
    {
        return category switch
        {
            GmAchievementCategory.PrimerosPasos => "PRIMEROS PASOS",
            GmAchievementCategory.Temporada => "TEMPORADA",
            GmAchievementCategory.JugadorPremiado => "JUGADOR PREMIADO",
            GmAchievementCategory.Playoffs => "PLAYOFFS",
            GmAchievementCategory.Carrera => "CARRERA",
            GmAchievementCategory.Mercado => "MERCADO",
            _ => ""
        };
    }
}