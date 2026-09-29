/// <summary>
/// Asignación del rol de un jugador a partir de su media. Fuente única para el seed, el
/// draft y las migraciones: los jugadores creados por el juego (p. ej. los rookies del
/// draft) antes se quedaban con el rol por defecto (`UltimoRecurso`), lo que les daba
/// minutos de fondo de banquillo aunque fuesen buenos.
/// </summary>
public static class PlayerRoleRules
{
    /// <summary>Rol por media (escala de los rosters del seed).</summary>
    public static PlayerRole FromOverall(int overall)
        => overall >= 90 ? PlayerRole.Estrella
         : overall >= 83 ? PlayerRole.Titular
         : overall >= 74 ? PlayerRole.Banquillo
         : PlayerRole.UltimoRecurso;
}
