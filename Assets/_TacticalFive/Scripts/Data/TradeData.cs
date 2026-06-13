using SQLite;

[Table("trades")]
public class TradeData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int season_id { get; set; }
    public int game_day { get; set; }
    public string game_date { get; set; }
    public int team_id_from { get; set; }
    public int team_id_to { get; set; }
    public int player_id { get; set; }
    public string trade_type { get; set; } // "trade" | "free_agent"
    public int? partner_player_id { get; set; }
}
