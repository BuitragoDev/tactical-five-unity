public static class HallOfFameHelper
{
    // Umbrales de inducción automática al Salón de la Fama.
    public const int RINGS_REQUIRED = 1;
    public const int FINALS_MVP_REQUIRED = 1;
    public const int CAREER_POINTS_THRESHOLD = 30000;
    public const int CAREER_REBOUNDS_THRESHOLD = 15000;
    public const int CAREER_ASSISTS_THRESHOLD = 10000;
    // E1: los hitos de carrera también inducen (10× All-Star / 3× MVP de temporada).
    public const int ALL_STAR_REQUIRED = 10;
    public const int SEASON_MVP_REQUIRED = 3;

    /// <summary>
    /// Decide si un jugador (retirado) merece entrar en el Salón de la Fama
    /// según anillos, Finales MVP y totales de carrera.
    /// </summary>
    public static bool ShouldInduct(int rings, int finalsMvps,
                                    int careerPoints, int careerRebounds, int careerAssists,
                                    int ringsRequired = RINGS_REQUIRED,
                                    int finalsMvpRequired = FINALS_MVP_REQUIRED,
                                    int pointsThreshold = CAREER_POINTS_THRESHOLD,
                                    int reboundsThreshold = CAREER_REBOUNDS_THRESHOLD,
                                    int assistsThreshold = CAREER_ASSISTS_THRESHOLD,
                                    int allStars = 0,
                                    int seasonMvps = 0,
                                    int allStarsRequired = ALL_STAR_REQUIRED,
                                    int seasonMvpsRequired = SEASON_MVP_REQUIRED)
    {
        return rings >= ringsRequired
            || finalsMvps >= finalsMvpRequired
            || careerPoints >= pointsThreshold
            || careerRebounds >= reboundsThreshold
            || careerAssists >= assistsThreshold
            || allStars >= allStarsRequired
            || seasonMvps >= seasonMvpsRequired;
    }
}