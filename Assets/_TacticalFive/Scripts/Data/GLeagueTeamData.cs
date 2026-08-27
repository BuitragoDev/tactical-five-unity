using SQLite;

[Table("gleague_teams")]
public class GLeagueTeamData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public string name { get; set; }          // nombre de la filial (ej. "Maine Celtics")
    public string abbreviation { get; set; }  // código corto de la filial
    public string conference { get; set; }    // "East" / "West"
    public string logo { get; set; }          // clave del PNG en Teams/GLeague/{size} (ej. "celtics_gleague")
    public int nba_team_id { get; set; }      // franquicia NBA matriz
}
