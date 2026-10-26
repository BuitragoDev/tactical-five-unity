using SQLite;

/// <summary>Edición anual de la NBA Cup (F5-04). No toca `seasons.phase`.</summary>
[Table("tournament_editions")]
public class TournamentEditionData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public int season_id { get; set; }
    [Indexed]
    public int manager_id { get; set; }
    public int year_start { get; set; }
    /// <summary>group → knockout → finished.</summary>
    public string status { get; set; }
    public int champion_team_id { get; set; }
    public int mvp_player_id { get; set; }
    public string created_at { get; set; }
}

/// <summary>Pertenencia de cada equipo a un grupo de la Cup.</summary>
[Table("tournament_groups")]
public class TournamentGroupData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public int edition_id { get; set; }
    public string group_name { get; set; }
    public string conference { get; set; }
    [Indexed]
    public int team_id { get; set; }
}

/// <summary>Clasificación de la fase de grupos de la Cup.</summary>
[Table("tournament_standings")]
public class TournamentStandingData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public int edition_id { get; set; }
    public string group_name { get; set; }
    public string conference { get; set; }
    [Indexed]
    public int team_id { get; set; }
    public int wins { get; set; }
    public int losses { get; set; }
    public int points_for { get; set; }
    public int points_against { get; set; }
    public int point_diff { get; set; }
    public int rank { get; set; }
}
