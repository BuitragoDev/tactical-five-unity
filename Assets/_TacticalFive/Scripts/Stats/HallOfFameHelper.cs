public static class HallOfFameHelper
{
    // Umbrales de inducción automática al Salón de la Fama.
    public const int RINGS_REQUIRED = 1;
    public const int FINALS_MVP_REQUIRED = 1;
    public const int CAREER_POINTS_THRESHOLD = 15000;
    public const int CAREER_REBOUNDS_THRESHOLD = 8000;
    public const int CAREER_ASSISTS_THRESHOLD = 5000;

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
                                    int assistsThreshold = CAREER_ASSISTS_THRESHOLD)
    {
        return rings >= ringsRequired
            || finalsMvps >= finalsMvpRequired
            || careerPoints >= pointsThreshold
            || careerRebounds >= reboundsThreshold
            || careerAssists >= assistsThreshold;
    }
}