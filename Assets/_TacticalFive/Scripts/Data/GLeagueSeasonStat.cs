using SQLite;

[Table("gleague_season_stats")]
public class GLeagueSeasonStat
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int player_id { get; set; }
    public int season_id { get; set; }
    public int games { get; set; }
    public int points { get; set; }
    public int rebounds { get; set; }
    public int assists { get; set; }
    public int steals { get; set; }
    public int blocks { get; set; }
    public int turnovers { get; set; }
    public int rating { get; set; }
}