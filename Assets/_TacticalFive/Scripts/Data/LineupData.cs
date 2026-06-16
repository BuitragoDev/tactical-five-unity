using SQLite;

[Table("team_lineup")]
public class LineupData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public int player_id { get; set; }
    [Indexed]
    public int team_id { get; set; }
    public int slot { get; set; } // 0=starter, 1=bench, 2=inactive
    public int slot_index { get; set; } // starter: 0-4 (PG=0, SG=1, SF=2, PF=3, C=4), bench/inactive: order within section, -1=unset
}
