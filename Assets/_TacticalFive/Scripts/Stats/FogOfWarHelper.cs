using System.Collections.Generic;

public static class FogOfWarHelper
{
    const int BAND_WIDTH = 5;

    public static bool CanViewRatings(PlayerData p, int myTeamId, HashSet<int> scoutedIds)
    {
        if (p.team_id == myTeamId) return true;
        return scoutedIds != null && scoutedIds.Contains(p.id);
    }

    public static string GetOvrDisplay(PlayerData p, int myTeamId, HashSet<int> scoutedIds)
    {
        if (CanViewRatings(p, myTeamId, scoutedIds))
            return p.GetCalculatedAverage().ToString();

        int med = p.GetCalculatedAverage();
        return GetRatingBand(med, p.id);
    }

    public static string GetRatingBand(int med, int playerId)
    {
        int offset = (int)((uint)(playerId * 2654435761UL) % BAND_WIDTH);
        int low = med - offset;
        int high = low + BAND_WIDTH;
        return $"{low}-{high}";
    }
}
