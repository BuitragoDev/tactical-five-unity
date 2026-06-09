using SQLite;

[Table("awards_records")]
public class AwardsRecord
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public string season { get; set; }
    public string mvp { get; set; }
    public string mvp_team_keyword { get; set; }
    public string mvp_rating { get; set; }
    public string rookie { get; set; }
    public string rookie_team_keyword { get; set; }
    public string rookie_rating { get; set; }
}
