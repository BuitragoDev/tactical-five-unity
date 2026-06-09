using SQLite;

[Table("finals_records")]
public class FinalsRecord
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public string season { get; set; }
    public string champ_name { get; set; }
    public string champ_keyword { get; set; }
    public string finalist_name { get; set; }
    public string finalist_keyword { get; set; }
    public string result { get; set; }
    public string mvp { get; set; }
}
