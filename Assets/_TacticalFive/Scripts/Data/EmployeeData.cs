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

    // ── Esquema táctico (solo relevante para el ASISTENTE) ──
    public int scheme_pace { get; set; }        // -1 lento, 0 normal, 1 rápido
    public int scheme_offense { get; set; }     // 0 equilibrado, 1 interior, 2 exterior
    public int scheme_defense { get; set; }     // 0 conservadora, 1 equilibrada, 2 agresiva
    public int scheme_set { get; set; }         // 1 = esquema asignado (para migrar saves)
}
