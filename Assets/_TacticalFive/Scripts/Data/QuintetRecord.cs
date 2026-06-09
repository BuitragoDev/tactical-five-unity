using SQLite;

[Table("quintet_records")]
public class QuintetRecord
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public string season { get; set; }
    public string pg { get; set; }
    public string pg_team { get; set; }
    public string sg { get; set; }
    public string sg_team { get; set; }
    public string sf { get; set; }
    public string sf_team { get; set; }
    public string pf { get; set; }
    public string pf_team { get; set; }
    public string c { get; set; }
    public string c_team { get; set; }
}
