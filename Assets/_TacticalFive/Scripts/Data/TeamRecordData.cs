using SQLite;

[Table("team_records")]
public class TeamRecordData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int team_id { get; set; }
    public string stat_type { get; set; }
    public string player_name { get; set; }
    public int value { get; set; }
    public string game_date { get; set; }
}
