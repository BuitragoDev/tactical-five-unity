using SQLite;

[Table("sponsors")]
public class SponsorData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public string name { get; set; }
    public string logo { get; set; }
    public int sponsor_type { get; set; }
    public long value { get; set; }
    public int initial_income { get; set; }
    public int home_game_income { get; set; }
    public int contract_years { get; set; }
    public int is_active { get; set; }
    public int team_id { get; set; }
    public int season_id { get; set; }
    public string description { get; set; }
    public int bonus_percent { get; set; }
    public int duration_years { get; set; }
    public long payment { get; set; }
    public string requirements { get; set; }
}
