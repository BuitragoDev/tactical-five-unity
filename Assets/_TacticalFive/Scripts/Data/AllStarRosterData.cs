using SQLite;

/// <summary>
/// Convocatoria del All-Star de una temporada (titulares y suplentes de cada conferencia).
/// Sustituye a contar apariciones desde `player_game_stats` del partido de las estrellas:
/// desde `SIM-14` el All-Star no escribe stats, y además así la trayectoria ve a los 24
/// convocados (no solo a 5). Alimenta `GetAllStarAppearances` (Palmarés) y `GetPlayerAwards`.
/// </summary>
[Table("all_star_roster")]
public class AllStarRosterData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int manager_id { get; set; }
    public int season_id { get; set; }
    /// <summary>`East` o `West`.</summary>
    public string conference { get; set; }
    public int player_id { get; set; }
    /// <summary>1 = titular (primeros 5 de la conferencia).</summary>
    public int is_starter { get; set; }
}
