using SQLite;

[Table("preseason_games")]
public class PreseasonGameData
{
    [PrimaryKey, AutoIncrement]
    public int    id           { get; set; }
    public int    manager_id   { get; set; }
    public int    home_team_id { get; set; }
    public int    away_team_id { get; set; }
    public string date         { get; set; } // "2025-09-XX"
    public bool   is_home      { get; set; } // true = jugamos en casa
    public string status       { get; set; } // "scheduled" | "played"
    public int    home_score   { get; set; }
    public int    away_score   { get; set; }
}