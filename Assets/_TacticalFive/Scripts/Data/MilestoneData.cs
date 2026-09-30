using SQLite;

/// <summary>
/// Hito de carrera alcanzado por un jugador (E1). Se registra una vez por hito para no
/// repetir la noticia, guardar la fecha y conservarlo aunque el jugador se retire (la fila
/// sobrevive a la desaparición de `players`, que es lo que da "legado" al sistema).
/// </summary>
[Table("player_milestones")]
public class MilestoneData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public int player_id { get; set; }
    /// <summary>Clave del hito (`points_10000`, `allstar_10`…) según `MilestoneCatalog`.</summary>
    public string milestone { get; set; }
    public int season_id { get; set; }
    public int game_day { get; set; }
    public int team_id { get; set; }
    public string created_at { get; set; }
}
