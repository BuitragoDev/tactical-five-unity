using SQLite;

[Table("monthly_awards")]
public class MonthlyAwardData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int season_id { get; set; }
    public string month_name { get; set; }
    public string award_type { get; set; }
    public int rank { get; set; }
    public int? manager_id { get; set; }
    public int? player_id { get; set; }
    public int? team_id { get; set; }
    public string team_name { get; set; }
    public string player_name { get; set; }
    public float value { get; set; }
}
