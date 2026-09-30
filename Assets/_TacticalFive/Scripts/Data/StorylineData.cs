using SQLite;

/// <summary>Tipo de hilo narrativo (storyline).</summary>
public static class StorylineType
{
    // Rachas del equipo
    public const string RachaPositiva = "racha_positiva"; // 5+ victorias seguidas
    public const string Bache = "bache";                  // 5+ derrotas seguidas

    // Jugadores
    public const string JugadorCaliente = "jugador_caliente"; // 3+ partidos anotando mucho
    public const string Sequia = "sequia";                    // 3+ partidos sin aparecer
    public const string Descontento = "descontento";          // minutos por debajo del rol
    public const string RumorTraspaso = "rumor_traspaso";     // jugador en el mercado
    public const string BajaEstrella = "baja_estrella";       // estrella lesionada
    public const string PresionGM = "presion_gm";             // afición/confianza bajo mínimos
    public const string CazaPlayoffs = "caza_playoffs";       // pelea por el play-in / riesgo lottery
    public const string CrisisEconomica = "crisis_economica"; // presupuesto en rojo

    // Rivalidades / dinastías
    public const string RevanchaPlayoffs = "revancha_playoffs"; // reencuentro con quien nos eliminó
    public const string Dinastia = "dinastia";                  // ventana de títulos (3+ en 8 años)
    public const string HitoAlcanzado = "hito_alcanzado";        // hito de carrera (E1)
}

/// <summary>Motivo del cierre del hilo.</summary>
public static class StorylineResolution
{
    public const string Broken = "broken";       // la situación terminó sola (racha rota...)
    public const string Completed = "completed"; // se cumplió el objetivo del hilo
    public const string Abandoned = "abandoned"; // se cerró al acabar la temporada
    public const string Departed = "departed";   // el protagonista salió del equipo (traspaso/FA/buyout)
}

/// <summary>
/// Hilo narrativo persistente: una situación que se ABRE, ESCALA y se CIERRA a lo
/// largo de varias jornadas. Los gestiona StorylineService a partir de los resultados
/// reales de la partida.
/// </summary>
[Table("storylines")]
public class StorylineData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int manager_id { get; set; }
    public int team_id { get; set; }
    public string type { get; set; }
    /// <summary>Jugador protagonista (0 = hilo de equipo).</summary>
    public int subject_player_id { get; set; }
    public string title { get; set; }
    /// <summary>Resumen corto para la línea temporal.</summary>
    public string summary { get; set; }
    /// <summary>Escalones superados (1 = recién abierto, 2 = siguiente hito...).</summary>
    public int intensity { get; set; }
    public int started_game_day { get; set; }
    public int last_update_game_day { get; set; }
    /// <summary>Última jornada en la que se generó noticia (para el cooldown).</summary>
    public int last_news_game_day { get; set; }
    public int resolved { get; set; }
    /// <summary>"" mientras está abierto; si no, StorylineResolution.*</summary>
    public string resolution { get; set; }
    public string started_at { get; set; }
}
