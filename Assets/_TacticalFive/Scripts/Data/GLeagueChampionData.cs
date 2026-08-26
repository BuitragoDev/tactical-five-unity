using SQLite;

[Table("gleague_champions")]
public class GLeagueChampionData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int manager_id { get; set; }
    public int season_id { get; set; }
    public string season { get; set; }        // año final (ej. "2026")
    public int gleague_team_id { get; set; }
    public string team_name { get; set; }
}
