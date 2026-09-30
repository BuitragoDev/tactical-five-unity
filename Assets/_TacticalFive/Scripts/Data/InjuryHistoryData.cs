using SQLite;

/// <summary>
/// Historial médico de un jugador (E2): una fila por lesión cerrada (recuperado, alta
/// forzada o recaída). Alimenta el bloque "ÚLTIMAS LESIONES" del perfil y sobrevive al
/// traspaso del jugador.
/// </summary>
[Table("injury_history")]
public class InjuryHistoryData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }

    [Indexed]
    public int player_id { get; set; }

    public int team_id { get; set; }
    public string injury_type { get; set; }   // diagnóstico (nombre del catálogo)
    public string severity { get; set; }      // LEVE / MODERADA / GRAVE / CRÍTICA
    public int days { get; set; }             // días totales de baja
    public int season_id { get; set; }
    public int game_day { get; set; }
    public string date { get; set; }          // yyyy-MM-dd
    public string outcome { get; set; }       // recuperado | alta_forzada | recaida
    public int treated { get; set; }          // 1 si pasó por tratamiento del médico
}
