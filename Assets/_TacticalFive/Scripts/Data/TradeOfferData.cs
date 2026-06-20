using SQLite;

[Table("trade_offers")]
public class TradeOfferData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int manager_id { get; set; }
    public int team_id_from { get; set; }
    public int player_id_out { get; set; }
    public int player_id_in { get; set; }
    public int day_sent { get; set; }
    public int processed { get; set; } // 0=pendiente, 1=aceptado, 2=rechazado
}
