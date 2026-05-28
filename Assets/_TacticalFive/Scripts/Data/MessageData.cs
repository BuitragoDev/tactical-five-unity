using SQLite;

[Table("messages")]
public class MessageData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int manager_id { get; set; }
    public int sender_type { get; set; }
    public int sender_id { get; set; }
    public string title { get; set; }
    public string body { get; set; }
    public int game_day { get; set; }
    public string game_date { get; set; }
    public string created_at { get; set; }
    public string date_sent { get; set; }
    public int is_read { get; set; }
}
