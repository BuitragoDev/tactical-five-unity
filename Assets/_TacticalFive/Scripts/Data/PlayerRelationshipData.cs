using SQLite;

[Table("player_relationship")]
public class PlayerRelationshipData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int team_id { get; set; }
    public int player_a_id { get; set; }
    public int player_b_id { get; set; }
    public int bond { get; set; }
}
