using SQLite;

[Table("team_settings")]
public class TeamSettingsData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int team_id { get; set; }
    public string arena_name { get; set; }
    public int arena_capacity { get; set; }
    public int arena_level { get; set; }
    public int avg_attendance { get; set; }
    public float ticket_price { get; set; }
    public int subscription_price { get; set; }
    public int sponsor_id { get; set; }
    public int sponsor_years_remaining { get; set; }
    public int tv_channel_id { get; set; }
    public int tv_years_remaining { get; set; }
}
