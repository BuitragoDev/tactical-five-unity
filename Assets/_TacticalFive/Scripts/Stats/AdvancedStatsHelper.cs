public static class AdvancedStatsHelper
{
    public static float CalcEFG(int fgm, int fga, int fg3m)
    {
        if (fga <= 0) return 0f;
        return (float)(fgm + 0.5 * fg3m) / fga * 100f;
    }

    public static float CalcTS(int points, int fga, int fta)
    {
        float denom = 2f * (fga + 0.44f * fta);
        if (denom <= 0f) return 0f;
        return points / denom * 100f;
    }

    public static int CalcEff(int pts, int reb, int ast, int stl, int blk,
                              int fgm, int fga, int ftm, int fta, int tov)
    {
        return pts + reb + ast + stl + blk
               - (fga - fgm)
               - (fta - ftm)
               - tov;
    }

    public static float CalcPER(float eff, float minutes)
    {
        if (minutes <= 0f) return 0f;
        return eff / minutes * 48f;
    }
}
