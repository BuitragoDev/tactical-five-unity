using SQLite;

[Table("league_settings")]
public class LeagueSettingsData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public long salary_cap { get; set; }
    public long luxury_tax { get; set; }
    public long apron { get; set; }
    public long repeater_apron { get; set; }
    public long mid_level { get; set; }
    public long taxpayer_mid_level { get; set; }
    public long bi_annual { get; set; }
    public long minimum_salary { get; set; }
    public int is_active { get; set; }
}