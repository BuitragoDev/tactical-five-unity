using SQLite;

/// <summary>Tipo de hilo narrativo (storyline).</summary>
public static class StorylineType
{
    public const string RachaPositiva = "racha_positiva"; // 5+ victorias seguidas
    public const string Bache = "bache";                  // 5+ derrotas seguidas
}

/// <summary>
/// Hilo narrativo persistente: una situación que se ABRE, ESCALA y se CIERRA a lo
/// largo de varias jornadas (a diferencia de una noticia suelta). Los gestiona
/// StorylineService a partir de los resultados reales de la partida.
/// </summary>
[Table("storylines")]
public class StorylineData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int manager_id { get; set; }
    public int team_id { get; set; }
    public string type { get; set; }
    public string title { get; set; }
    /// <summary>Escalones superados (1 = recién abierto, 2 = 10 victorias/derrotas...).</summary>
    public int intensity { get; set; }
    public int started_game_day { get; set; }
    public int last_update_game_day { get; set; }
    public int resolved { get; set; }
    public string started_at { get; set; }
}
