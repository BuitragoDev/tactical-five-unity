using SQLite;

[Table("teams")]
public class TeamData
{
    [PrimaryKey, AutoIncrement]
    public int    id                       { get; set; }
    public string name                     { get; set; }
    public string abbreviation             { get; set; }
    public string city                     { get; set; }
    public string conference               { get; set; }
    public string division                 { get; set; }
    public string arena                    { get; set; }
    public int    capacity                 { get; set; }
    public string owner                    { get; set; }
    public int    attack                   { get; set; }
    public int    defense                  { get; set; }
    public int    overall                  { get; set; }
    public long   budget                   { get; set; }
    public int    reputation               { get; set; }
    public int    facilities               { get; set; }
    public string logo                     { get; set; }
    public string jersey_home              { get; set; }
    public string jersey_away              { get; set; }
    public long   salary_margin            { get; set; }
    public int    first_apron_hard_capped  { get; set; }  // 0=no, 1=hard capped at FIRST_APRON

    // ── Plan de partido (táctica persistente del equipo) ──
    public int    plan_pace     { get; set; }              // -1 lento, 0 normal, 1 rápido
    public int    plan_offense  { get; set; }              // 0 equilibrado, 1 interior, 2 exterior
    public int    plan_defense  { get; set; } = 1;         // 0 conservadora, 1 equilibrada, 2 agresiva
    public string objective                { get; set; }

    // Arena renovation
    public int    arena_renovation_end_day { get; set; }
    public string arena_renovation_type    { get; set; }
    public int    arena_renovation_count   { get; set; }
    public long   arena_renovation_cost    { get; set; }
    public int team_chemistry { get; set; } = 50;
}