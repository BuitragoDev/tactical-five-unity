using SQLite;

/// <summary>Resultado individual del camp de desarrollo post-draft (F5-05).</summary>
[Table("summer_league_stats")]
public class SummerLeagueStatData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public int manager_id { get; set; }
    [Indexed]
    public int season_id { get; set; }
    [Indexed]
    public int player_id { get; set; }
    public int team_id { get; set; }
    public int games { get; set; }
    public int minutes { get; set; }
    public int points { get; set; }
    public int rebounds { get; set; }
    public int assists { get; set; }
    public int development_gain { get; set; }
    public string created_at { get; set; }
}
