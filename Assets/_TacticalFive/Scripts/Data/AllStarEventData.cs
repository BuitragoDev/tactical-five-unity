using SQLite;

/// <summary>
/// Resultado de un evento del All-Star Weekend (E5): concurso de mates, concurso de triples
/// o el propio partido de las estrellas. Una fila por evento y temporada.
/// </summary>
[Table("all_star_events")]
public class AllStarEventData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int manager_id { get; set; }
    public int season_id { get; set; }
    /// <summary>`slam_dunk` o `three_point`.</summary>
    public string event_type { get; set; }
    public int winner_player_id { get; set; }
    public string winner_name { get; set; }
    public string winner_team_logo { get; set; }
    public int winner_score { get; set; }
    public string created_at { get; set; }
}

/// <summary>
/// Participante (y su puntuación) en un evento del All-Star Weekend (E5).
/// </summary>
[Table("all_star_event_participants")]
public class AllStarEventParticipantData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int event_id { get; set; }
    public int player_id { get; set; }
    public string player_name { get; set; }
    public string team_logo { get; set; }
    public int rank { get; set; }
    public int score { get; set; }
    public int is_winner { get; set; }
}
