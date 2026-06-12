using SQLite;

[Table("finance_records")]
public class FinanceRecord
{
    public const int TYPE_TICKET        = 1;   // Taquilla
    public const int TYPE_SUBSCRIPTION  = 2;   // Abonos
    public const int TYPE_SPONSORSHIP   = 3;   // Patrocinios
    public const int TYPE_TV            = 4;   // Televisión
    public const int TYPE_RENOVATION    = 5;   // Remodelación
    public const int TYPE_DISMISSAL     = 6;   // Despido
    public const int TYPE_SALARIES      = 7;   // Sueldos de jugadores
    public const int TYPE_EMPLOYEE_SALARY = 8;   // Sueldos de empleados
    public const int TYPE_LOAN = 9;               // Préstamo bancario

    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int team_id { get; set; }
    public int season_id { get; set; }
    public int record_type { get; set; }
    public int game_day { get; set; }
    public long amount { get; set; }
    public string created_at { get; set; }
}
