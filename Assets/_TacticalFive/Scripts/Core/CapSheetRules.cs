using System.Collections.Generic;
using System.Linq;

/// <summary>Agregaciones puras del cap sheet; los holds no son nómina contable.</summary>
public static class CapSheetRules
{
    public static long ProjectedPayroll(
        IEnumerable<PlayerData> players,
        IEnumerable<TeamCapHoldData> holds,
        int yearIndex)
    {
        if (yearIndex < 0) yearIndex = 0;
        var roster = players ?? Enumerable.Empty<PlayerData>();
        long contracts = roster
            .Where(p => p.is_two_way != 1)
            .Sum(p =>
            {
                bool guaranteed = p.guaranteed_years > yearIndex;
                bool option = p.contract_years > yearIndex && p.guaranteed_years <= yearIndex
                    && (p.has_team_option == 1 || p.has_player_option == 1);
                return guaranteed || option ? System.Math.Max(0L, p.salary) : 0L;
            });
        long activeHolds = yearIndex == 0
            ? (holds ?? Enumerable.Empty<TeamCapHoldData>())
                .Where(h => h.is_active == 1)
                .Sum(h => System.Math.Max(0L, h.amount))
            : 0L;
        return contracts + activeHolds;
    }
}
