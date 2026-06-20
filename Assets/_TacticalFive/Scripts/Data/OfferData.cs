using SQLite;

[Table("offers")]
public class OfferData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int manager_id { get; set; }
    public int player_id { get; set; }
    public long offer_salary { get; set; }
    public int offer_years { get; set; }
    public int day_sent { get; set; }
    public int offer_type { get; set; } // 0 = renovación, 1 = fichaje agente libre
    public string status { get; set; } // "pending" | "accepted" | "rejected"
    public int processed { get; set; } // 0 = sin procesar, 1 = resultado mostrado
}
