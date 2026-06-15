using SQLite;

[Table("player_personality")]
public class PlayerPersonalityData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int player_id { get; set; }
    public int team_id { get; set; }
    public string personality_type { get; set; }
    public string trait_1 { get; set; }
    public string trait_2 { get; set; }
    public int compatibility_modifier { get; set; }
}
