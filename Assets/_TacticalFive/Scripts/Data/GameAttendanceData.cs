using SQLite;

[Table("game_attendance")]
public class GameAttendanceData
{
    [PrimaryKey]
    public int game_id        { get; set; }
    public int attendance     { get; set; }
    public int ticket_price   { get; set; }
    public long revenue       { get; set; }
}
