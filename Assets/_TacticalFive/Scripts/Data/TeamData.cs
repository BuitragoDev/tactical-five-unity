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
    public string objective                { get; set; }

    // Arena renovation
    public int    arena_renovation_end_day { get; set; }
    public string arena_renovation_type    { get; set; }
    public int    arena_renovation_count   { get; set; }
    public long   arena_renovation_cost    { get; set; }
    public int team_chemistry { get; set; } = 50;
}