using SQLite;

[Table("finance_records")]
public class FinanceRecord
{
    public const int TYPE_INCOME = 1;
    public const int TYPE_EXPENSE = 2;
    public const int TYPE_SALARIES = 3;
    public const int TYPE_SUBSCRIPTION = 4;
    public const int TYPE_TICKET = 5;
    public const int TYPE_SPONSORSHIP = 6;
    public const int TYPE_TV = 7;
    public const int TYPE_RENOVATION = 8;

    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int team_id { get; set; }
    public int season_id { get; set; }
    public int record_type { get; set; }
    public int game_day { get; set; }
    public long amount { get; set; }
    public string created_at { get; set; }
}
