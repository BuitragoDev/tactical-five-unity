using SQLite;

/// <summary>
/// Estado persistido del draft de una temporada. Evita que, al recargar la partida
/// a mitad de draft, se reinicie y se duplique la clase de rookies.
/// </summary>
[Table("draft_state")]
public class DraftStateData
{
    [PrimaryKey]
    public int season_id { get; set; }
    public int manager_id { get; set; }
    /// <summary>Índice del siguiente pick a resolver (0..60).</summary>
    public int current_pick_index { get; set; }
    /// <summary>1 si el draft terminó.</summary>
    public int completed { get; set; }
    public string updated_at { get; set; }
}
