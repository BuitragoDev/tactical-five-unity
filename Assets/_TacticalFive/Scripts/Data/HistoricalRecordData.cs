using SQLite;

[Table("historical_records")]
public class HistoricalRecordData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public string stat_type { get; set; }
    public string player_name { get; set; }
    public int value { get; set; }
    public string game_date { get; set; }
    public string team_abbreviation { get; set; }
}
