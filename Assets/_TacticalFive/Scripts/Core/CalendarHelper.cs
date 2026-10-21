/// <summary>
/// Mapeo entre la fecha del calendario y el índice de día de partido (`game_day`).
///
/// Regla de la temporada: el día 1 es el 22 de octubre del año de inicio; los días
/// anteriores (pretemporada) son negativos y el 0 es el centinela "temporada sin empezar".
///
/// Clase estática pura (sin estado) para poder testearla en EditMode.
/// </summary>
public static class CalendarHelper
{
    /// <summary>Fecha de inicio de la temporada: 22 de octubre del año indicado.</summary>
    public static System.DateTime SeasonStart(int yearStart)
    {
        return new System.DateTime(yearStart, 10, 22);
    }

    /// <summary>
    /// Índice de día de partido de una fecha: 22/10 → 1, 23/10 → 2,
    /// 21/10 → -1 (pretemporada), 28/09 → -24.
    /// </summary>
    public static int DateToGameDay(int yearStart, System.DateTime date)
    {
        var seasonStart = SeasonStart(yearStart);
        if (date >= seasonStart)
            return (int)(date - seasonStart).TotalDays + 1;
        return -(int)(seasonStart - date).TotalDays;
    }

    /// <summary>
    /// Fecha de calendario de un índice de día de partido (inverso de
    /// <see cref="DateToGameDay"/>): 1 → 22/10, 9 → 30/10, −47 → 5/9.
    /// El 0 es un centinela ("temporada sin empezar") y se resuelve como el 22/10.
    /// </summary>
    public static System.DateTime GameDayToDate(int yearStart, int gameDay)
    {
        var seasonStart = SeasonStart(yearStart);
        if (gameDay >= 1) return seasonStart.AddDays(gameDay - 1);
        if (gameDay == 0) return seasonStart;
        return seasonStart.AddDays(gameDay);
    }
}
