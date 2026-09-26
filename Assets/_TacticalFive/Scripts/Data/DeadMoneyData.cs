using SQLite;

/// <summary>
/// Deuda muerta por rescisión de contrato (stretch provision). A diferencia de los
/// finance_records (que se borran al cambiar de temporada), esta tabla persiste: el
/// tramo de cada temporada futura se cobra en StartNewSeason.
/// </summary>
[Table("dead_money")]
public class DeadMoneyData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    public int team_id { get; set; }
    public string player_name { get; set; }
    /// <summary>Temporada en la que se hizo la rescisión (informativo).</summary>
    public int origin_season_id { get; set; }
    /// <summary>Importe que se cobra cada temporada futura.</summary>
    public long annual_amount { get; set; }
    /// <summary>Temporadas futuras que quedan por pagar (se decrementa cada rollover).</summary>
    public int years_remaining { get; set; }
}
