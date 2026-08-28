using SQLite;

[Table("employees")]
public class EmployeeData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int team_id { get; set; }
    public string position { get; set; }
    public string first_name { get; set; }
    public string last_name { get; set; }
    public long salary { get; set; }
    public int reputation { get; set; }
    public int contract_years { get; set; }
    public int candidate_day { get; set; }
    public string nationality { get; set; }
}
