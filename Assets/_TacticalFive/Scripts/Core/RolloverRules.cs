/// <summary>
/// Reglas puras del rollover de temporada (B41). Separan el tratamiento de los jugadores
/// durante su primer año de rookie del resto de la progresión: un rookie cumple años y
/// progresa, pero su contrato de rookie no se decrementa hasta el año siguiente.
/// Sin estado ni DB: lo consume <c>DatabaseManager.StartNewSeason</c>.
/// </summary>
public static class RolloverRules
{
    /// <summary>
    /// ¿Debe decrementarse el contrato de este jugador en el rollover?
    /// En su primer año de rookie (acaba de ser drafteado) el contrato NO baja todavía;
    /// el resto sí. El manager de su propio equipo gestiona el decremento aparte
    /// (<c>NewSeasonController.CheckRosterAndStart</c>) con el mismo criterio.
    /// </summary>
    public static bool ShouldDecrementContract(bool isFirstYearRookie) => !isFirstYearRookie;
}
