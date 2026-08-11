using SQLite;

[Table("draft_picks")]
public class DraftPickData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int season_id { get; set; }
    public int round { get; set; }
    public int pick_number { get; set; }
    public int original_team_id { get; set; }
    public int current_team_id { get; set; }
    public int protected_from { get; set; }
    public int is_swap { get; set; }
    public int swap_original_team_id { get; set; }
}
