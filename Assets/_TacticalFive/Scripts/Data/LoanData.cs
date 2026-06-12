using SQLite;

[Table("loans")]
public class LoanData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int team_id { get; set; }
    public int slot { get; set; }
    public long amount { get; set; }
    public int months { get; set; }
    public long monthly_payment { get; set; }
    public int remaining_months { get; set; }
    public int is_active { get; set; }
}
