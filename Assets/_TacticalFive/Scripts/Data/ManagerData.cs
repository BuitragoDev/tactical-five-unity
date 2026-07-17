using SQLite;

[Table("managers")]
public class ManagerData
{
    [PrimaryKey, AutoIncrement]
    public int    id        { get; set; }
    public string name      { get; set; }
    public int    team_id   { get; set; }
    public string game_mode { get; set; }
    public int    trust          { get; set; }
    public int    morale         { get; set; }
    public int    fan_confidence { get; set; }
    public int    budget_red_warnings { get; set; }

    // Career stats (accumulated across seasons)
    public int career_reg_wins   { get; set; }
    public int career_reg_losses { get; set; }
    public int career_po_wins    { get; set; }
    public int career_po_losses  { get; set; }
    public int championships     { get; set; }
    public int seasons_completed { get; set; }
}