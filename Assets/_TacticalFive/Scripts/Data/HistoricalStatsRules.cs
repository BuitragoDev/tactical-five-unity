using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Reglas del histórico de carrera (`historical_player_stats`). Antes se agregaba por
/// NOMBRE COMPLETO, así que un rookie aleatorio con el nombre de una leyenda (los pools
/// de nombres se solapan) heredaba sus totales, sus inducciones al HOF y su dorsal
/// retirado. Ahora cada fila se identifica por `player_id`:
///   · `player_id &gt; 0`  → carrera de un jugador real de la partida (clave estable).
///   · `player_id == LEGEND_ID (-1)` → fila del seeder de leyendas; intocable.
///   · `player_id == UNKNOWN_ID (0)` → fila antigua huérfana (pre-fix); solo se adopta
///     durante la migración si el nombre identifica a un único jugador.
/// Reglas puras (sin DB) para poder testearlas.
/// </summary>
public static class HistoricalStatsRules
{
    public const int LEGEND_ID  = -1;
    public const int UNKNOWN_ID = 0;

    /// <summary>Fila de carrera que le corresponde a un jugador: solo por id (nunca por nombre).</summary>
    public static HistoricalPlayerStatsData RowForPlayer(int playerId,
                                                         IEnumerable<HistoricalPlayerStatsData> rows)
    {
        if (playerId <= 0 || rows == null) return null;
        return rows.FirstOrDefault(r => r != null && r.player_id == playerId);
    }

    /// <summary>
    /// ¿Se puede adoptar esta fila huérfana para un jugador? Solo si sigue sin dueño
    /// (`UNKNOWN_ID`), no es una leyenda y el nombre identifica a un ÚNICO jugador
    /// actual (con varios homónimos la fila es una mezcla y no se puede repartir).
    /// </summary>
    public static bool CanAdopt(HistoricalPlayerStatsData row, IReadOnlyList<int> playersWithSameName)
        => row != null
           && row.player_id == UNKNOWN_ID
           && playersWithSameName != null
           && playersWithSameName.Count == 1;

    /// <summary>¿La fila es del seeder de leyendas (nunca se mezcla con jugadores reales)?</summary>
    public static bool IsLegendRow(HistoricalPlayerStatsData row) => row != null && row.player_id == LEGEND_ID;

    /// <summary>
    /// Plan de adopción de filas huérfanas: devuelve `id de fila → player_id` solo para
    /// las filas cuyo nombre identifica a un ÚNICO jugador. Con homónimos no hay entrada:
    /// la fila se queda sin dueño y la migración la deja intacta.
    /// </summary>
    public static Dictionary<int, int> BuildAdoptionPlan(
        IEnumerable<HistoricalPlayerStatsData> rows,
        IReadOnlyDictionary<(string first, string last), List<int>> playersByName)
    {
        var plan = new Dictionary<int, int>();
        if (rows == null) return plan;
        foreach (var row in rows)
        {
            if (row == null || row.player_id != UNKNOWN_ID) continue;
            var key = (row.first_name, row.last_name);
            if (playersByName != null
                && playersByName.TryGetValue(key, out var ids)
                && CanAdopt(row, ids))
                plan[row.id] = ids[0];
        }
        return plan;
    }
}
