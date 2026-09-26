using SQLite;

[Table("gleague_players")]
public class GLeaguePlayerData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int gleague_team_id { get; set; }
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string position { get; set; }      // PG/SG/SF/PF/C
    public int age { get; set; }
    public int overall { get; set; }
    public int potential { get; set; }
    public int speed { get; set; }
    public int shooting { get; set; }
    public int three_point { get; set; }
    public int passing { get; set; }
    public int dribbling { get; set; }
    public int defense { get; set; }
    public int rebounding { get; set; }
    public int athleticism { get; set; }
    public int iq { get; set; }
    public int steals { get; set; }
    public int blocks { get; set; }
    public string photo { get; set; }

    /// <summary>Media de los 11 atributos (redondeada) capada por el potencial.
    /// Mismo invariante que `PlayerData`: fuente única del overall.</summary>
    public int GetCalculatedAverage()
    {
        int sum = speed + shooting + three_point + passing + dribbling + defense
                + rebounding + athleticism + iq + steals + blocks;
        int avg = (int)System.Math.Round(sum / 11.0);
        return potential > 0 ? System.Math.Min(avg, potential) : avg;
    }

    /// <summary>Recalcula y fija `overall` desde los atributos (tras el desarrollo).</summary>
    public void RecalculateOverall() => overall = GetCalculatedAverage();
}
