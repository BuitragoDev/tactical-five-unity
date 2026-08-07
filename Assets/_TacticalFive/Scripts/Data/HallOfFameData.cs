using SQLite;

[Table("hof_players")]
public class HallOfFameData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int player_id { get; set; } // 0 para leyendas precargadas
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string position { get; set; }
    public string team_abbreviation { get; set; }
    public int rings { get; set; }
    public int career_points { get; set; }
    public int career_rebounds { get; set; }
    public int career_assists { get; set; }
    public int career_games { get; set; }
    public int finals_mvps { get; set; }
    public string induction_season { get; set; }

    public string FullName => $"{first_name} {last_name}";
}