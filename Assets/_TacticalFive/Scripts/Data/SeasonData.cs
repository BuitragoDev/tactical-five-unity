using SQLite;

[Table("seasons")]
public class SeasonData
{
    [PrimaryKey, AutoIncrement]
    public int    id                { get; set; }
    public int    year_start        { get; set; }
    public int    year_end          { get; set; }
    public int    is_active         { get; set; }
    public int    current_game_day  { get; set; }
    public string game_mode         { get; set; }
    public string phase             { get; set; } // "preseason" | "regular" | "playin" | "playoffs" | "finished"
    public int    manager_id        { get; set; }
    public int    generated        { get; set; } // 0 = no, 1 = sí
    public string current_date     { get; set; } // "yyyy-MM-dd" — fecha actual del calendario
    public int    last_ai_trade_day { get; set; } // último gameDay en que se ejecutó ProcessAITransfers
}