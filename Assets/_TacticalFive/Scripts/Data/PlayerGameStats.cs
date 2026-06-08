using SQLite;

[Table("player_game_stats")]
public class PlayerGameStats
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public int game_id { get; set; }
    [Indexed]
    public int player_id { get; set; }
    [Indexed]
    public int team_id { get; set; }
    public float minutes { get; set; }
    public int points { get; set; }
    public int fgm { get; set; }
    public int fga { get; set; }
    public int fg3m { get; set; }
    public int fg3a { get; set; }
    public int ftm { get; set; }
    public int fta { get; set; }
    public int oreb { get; set; }
    public int dreb { get; set; }
    public int rebounds { get; set; }
    public int assists { get; set; }
    public int steals { get; set; }
    public int blocks { get; set; }
    public int turnovers { get; set; }
    public int pf { get; set; }
    public int rating { get; set; }
    public int double_double { get; set; }
    public int triple_double { get; set; }
}
