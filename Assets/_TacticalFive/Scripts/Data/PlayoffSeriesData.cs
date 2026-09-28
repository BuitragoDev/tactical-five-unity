using SQLite;

/// <summary>
/// Una serie de playoffs ya resuelta (rivalidades/dinastías). Se guarda **al cerrarse**
/// la serie porque la tabla `games` se purga en el rollover: sin esto no hay histórico
/// de enfrentamientos. Clave de idempotencia: (manager_id, season_id, label).
/// </summary>
[Table("playoff_series")]
public class PlayoffSeriesData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int manager_id { get; set; }
    public int season_id { get; set; }

    /// <summary>`series_label` original del calendario (`playoff-r1-east-1v8`, `playoff-r4-finals`…).</summary>
    public string label { get; set; }
    /// <summary>Ronda: `r1`, `r2`, `r3`, `r4` (Finales).</summary>
    public string stage { get; set; }
    /// <summary>`East`, `West` o "" (Finales).</summary>
    public string conference { get; set; }

    /// <summary>Equipo con ventaja de cancha (teamA de la serie).</summary>
    public int high_team_id { get; set; }
    public int low_team_id { get; set; }
    public int high_wins { get; set; }
    public int low_wins { get; set; }
    public int winner_id { get; set; }
    public int loser_id { get; set; }
    public int game_day { get; set; }
}
