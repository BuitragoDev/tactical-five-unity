using SQLite;

[Table("retired_numbers")]
public class RetiredNumberData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int team_id { get; set; }
    public int number { get; set; }
    public int player_id { get; set; } // 0 si el jugador ya no existe (siempre tras retiro)
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string position { get; set; }
    public int rings { get; set; }
    public int career_points { get; set; }
    public string induction_season { get; set; }

    public string FullName => $"{first_name} {last_name}";
}