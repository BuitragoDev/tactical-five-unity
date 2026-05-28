using SQLite;

[Table("tv_channels")]
public class TvChannelData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public string name { get; set; }
    public string logo { get; set; }
    public int channel_type { get; set; }
    public long value { get; set; }
    public int initial_income { get; set; }
    public int home_game_income { get; set; }
    public int contract_years { get; set; }
    public int is_active { get; set; }
    public string description { get; set; }
    public long broadcast_fee { get; set; }
    public float viewership_multiplier { get; set; }
}
