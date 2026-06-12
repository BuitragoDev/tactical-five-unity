using SQLite;

[Table("scouts")]
public class ScoutData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int team_id { get; set; }
    public int slot { get; set; }
    public int player_id { get; set; }
    public int start_day { get; set; }
    public int end_day { get; set; }
    public int completed { get; set; }
}
