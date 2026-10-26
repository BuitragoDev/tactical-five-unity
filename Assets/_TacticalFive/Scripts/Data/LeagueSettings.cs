using SQLite;

[Table("league_settings")]
public class LeagueSettingsData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public long salary_cap { get; set; }
    public long luxury_tax { get; set; }
    public long apron { get; set; }
    public long repeater_apron { get; set; }
    public long mid_level { get; set; }
    public long taxpayer_mid_level { get; set; }
    public long bi_annual { get; set; }
    public long minimum_salary { get; set; }
    public long two_way_salary { get; set; }
    /// <summary>Temporada (año de inicio) a la que pertenecen estos topes. F4-07.</summary>
    public int season_start_year { get; set; }
    /// <summary>1 si la fila CBA es una proyección; 0 si es cifra oficial.</summary>
    public int cba_projected { get; set; }
    /// <summary>Procedencia de las cifras (NBA.com, RealGM o proyección del juego).</summary>
    public string cba_source { get; set; }
    public int is_active { get; set; }
}