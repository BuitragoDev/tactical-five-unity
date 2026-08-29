using SQLite;

[Table("scouted_players")]
public class ScoutedPlayerData
{
    public int team_id { get; set; }
    public int player_id { get; set; }
    public int scouted_day { get; set; }
}
