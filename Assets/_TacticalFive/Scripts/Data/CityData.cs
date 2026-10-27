using SQLite;

/// <summary>Proyecto de ciudad (F7-05). Una fila por nivel iniciado.</summary>
[Table("city_projects")]
public class CityProjectData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public int manager_id { get; set; }
    [Indexed]
    public int season_id { get; set; }
    public int team_id { get; set; }
    public string project_key { get; set; }
    public int level { get; set; }
    /// <summary>active → completed.</summary>
    public string status { get; set; }
    public int started_day { get; set; }
    public int completion_day { get; set; }
    public long cost { get; set; }
    public string created_at { get; set; }
}

/// <summary>Evento de ciudad pendiente o resuelto (F7-05).</summary>
[Table("city_events")]
public class CityEventData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public int manager_id { get; set; }
    [Indexed]
    public int season_id { get; set; }
    public string event_key { get; set; }
    /// <summary>pending → resolved.</summary>
    public string status { get; set; }
    public int game_day { get; set; }
    public string created_at { get; set; }
    public int resolved_day { get; set; }
    public int choice_index { get; set; }
}
