using SQLite;

[Table("allstar_records")]
public class AllStarRecord
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int manager_id { get; set; }
    public string season { get; set; }
    public int east_score { get; set; }
    public int west_score { get; set; }
    public string mvp { get; set; }
    public int mvp_player_id { get; set; }
}
