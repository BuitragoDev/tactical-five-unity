using SQLite;

[Table("team_cap_holds")]
public class TeamCapHoldData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public int team_id { get; set; }
    [Indexed]
    public int player_id { get; set; }
    public int season_id { get; set; }
    public long amount { get; set; }
    public string rights_type { get; set; } = "UFA";
    public int is_active { get; set; } = 1;
}
