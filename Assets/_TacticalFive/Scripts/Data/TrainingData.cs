using SQLite;

[Table("player_training")]
public class TrainingData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int player_id { get; set; }
    public int team_id { get; set; }
    public string attribute { get; set; }
    public int start_day { get; set; }
    public int duration { get; set; }
    public int completed { get; set; }
}
