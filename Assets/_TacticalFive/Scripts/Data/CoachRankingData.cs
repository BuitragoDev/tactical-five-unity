using SQLite;

[Table("coach_rankings")]
public class CoachRankingData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public string name { get; set; }
    public int team_id { get; set; }
    public string status { get; set; }
    public int score { get; set; }
}
