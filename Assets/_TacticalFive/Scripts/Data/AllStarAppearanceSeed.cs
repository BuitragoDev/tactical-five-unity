using SQLite;

[Table("allstar_appearances_seed")]
public class AllStarAppearanceSeed
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int player_id { get; set; }
    public string player_name { get; set; }
    public int appearances { get; set; }
}
