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
            return ClampRating(p.GetCalculatedAverage()).ToString();

        int med = ClampRating(p.GetCalculatedAverage());
        return GetRatingBand(med, p.id);
    }

    public static string GetRatingBand(int med, int playerId)
    {
        int offset = (int)((uint)playerId * 2654435761U % BAND_WIDTH);
        int low = ClampRating(med - offset);
        int high = ClampRating(low + BAND_WIDTH);
        return $"{low}-{high}";
    }

    static int ClampRating(int rating)
    {
        if (rating < 0) return 0;
        return rating > 99 ? 99 : rating;
    }
}
