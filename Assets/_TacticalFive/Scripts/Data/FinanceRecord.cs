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
    public const int TYPE_LOAN = 9;               // Préstamo bancario (obsoleto: usar LOAN_IN/LOAN_REPAY)
    public const int TYPE_LOAN_IN = 12;           // Ingreso por préstamo (dinero recibido)
    public const int TYPE_LOAN_REPAY = 13;        // Devolución de préstamo (gasto)
    public const int TYPE_TAX = 10;               // Luxury tax
    public const int TYPE_BUYOUT = 11;             // Buyout (stretch provision)
    public const int TYPE_LEAGUE = 14;             // Reparto de derechos de liga (ingreso)

    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int team_id { get; set; }
    public int season_id { get; set; }
    public int record_type { get; set; }
    public int game_day { get; set; }
    public long amount { get; set; }
    public string created_at { get; set; }

    /// <summary>Tipos que suman en el panel de ingresos y en el balance mensual.</summary>
    public static bool IsIncomeType(int recordType)
        => recordType == TYPE_TICKET || recordType == TYPE_SUBSCRIPTION
        || recordType == TYPE_SPONSORSHIP || recordType == TYPE_TV
        || recordType == TYPE_LOAN_IN || recordType == TYPE_LEAGUE;

    /// <summary>Tipos que suman en el panel de gastos y en el balance mensual.</summary>
    public static bool IsExpenseType(int recordType)
        => recordType == TYPE_RENOVATION || recordType == TYPE_DISMISSAL
        || recordType == TYPE_SALARIES || recordType == TYPE_EMPLOYEE_SALARY
        || recordType == TYPE_LOAN_REPAY || recordType == TYPE_TAX
        || recordType == TYPE_BUYOUT;
}
