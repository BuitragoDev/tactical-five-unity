using SQLite;

[Table("games")]
public class GameData
{
    [PrimaryKey, AutoIncrement]
    public int    id           { get; set; }
    public int    season_id    { get; set; }
    public int    game_day     { get; set; }
    public string game_date    { get; set; }
    public int    home_team_id { get; set; }
    public int    away_team_id { get; set; }
    public int    home_score   { get; set; }
    public int    away_score   { get; set; }
    public int    is_played    { get; set; }
    public string game_type    { get; set; }
    public string series_label { get; set; }
    public int    manager_id   { get; set; }
    public int    is_home      { get; set; }
    public int    tv_channel_id { get; set; }

    // Quarter scores (persistidos en DB)
    public int    q1_home      { get; set; }
    public int    q1_away      { get; set; }
    public int    q2_home      { get; set; }
    public int    q2_away      { get; set; }
    public int    q3_home      { get; set; }
    public int    q3_away      { get; set; }
    public int    q4_home      { get; set; }
    public int    q4_away      { get; set; }
}