using System;
using System.Collections.Generic;

/// <summary>
/// F6-03: tendencias por temporada calculadas SOLO con datos persistidos
/// (`player_season_stats` + `player_game_stats`). No inventa posesiones ni
/// métricas que no estén archivadas.
/// </summary>
public class SeasonTrend
{
    public int seasonId;
    public int yearStart;
    public int yearEnd;
    public int games;
    public float ppg;
    public float tsPct;
    public float efi48;
}

public static class StatsTrendHelper
{
    public static SeasonTrend FromPlayer(PlayerCareerSeasonRow row)
    {
        if (row == null) return null;
        int eff = AdvancedStatsHelper.CalcEff(
            row.total_points, row.total_rebounds, row.total_assists,
            row.total_steals, row.total_blocks,
            row.total_fgm, row.total_fga, row.total_ftm, row.total_fta,
            row.total_turnovers);
        return Build(row.season_id, row.year_start, row.year_end, row.games,
            row.total_minutes, row.total_points, row.total_fgm, row.total_fga,
            row.total_ftm, row.total_fta, eff);
    }

    public static SeasonTrend FromLeague(LeagueSeasonAggregateRow row)
    {
        if (row == null) return null;
        int eff = AdvancedStatsHelper.CalcEff(
            row.total_points, row.total_rebounds, row.total_assists,
            row.total_steals, row.total_blocks,
            row.total_fgm, row.total_fga, row.total_ftm, row.total_fta,
            row.total_turnovers);
        return Build(row.season_id, row.year_start, row.year_end, row.games,
            row.total_minutes, row.total_points, row.total_fgm, row.total_fga,
            row.total_ftm, row.total_fta, eff);
    }

    public static List<SeasonTrend> FromLeague(IEnumerable<LeagueSeasonAggregateRow> rows)
    {
        var result = new List<SeasonTrend>();
        foreach (var row in rows ?? new List<LeagueSeasonAggregateRow>())
        {
            var trend = FromLeague(row);
            if (trend != null) result.Add(trend);
        }
        return result;
    }

    public static string LeagueLabel(SeasonTrend trend)
        => trend == null ? "—" : $"Liga {trend.yearStart}-{trend.yearEnd}: TS% {StatFormat.One(trend.tsPct)} · EFI/48 {StatFormat.One(trend.efi48)}";

    public static string Delta(float player, float league)
    {
        float diff = player - league;
        string sign = diff >= 0 ? "+" : "";
        return sign + StatFormat.One(diff);
    }

    static SeasonTrend Build(int seasonId, int yearStart, int yearEnd, int games,
        double minutes, int points, int fgm, int fga, int ftm, int fta, int eff)
    {
        return new SeasonTrend
        {
            seasonId = seasonId,
            yearStart = yearStart,
            yearEnd = yearEnd,
            games = games,
            ppg = games > 0 ? (float)points / games : 0f,
            tsPct = AdvancedStatsHelper.CalcTS(points, fga, fta),
            efi48 = minutes > 0 ? AdvancedStatsHelper.CalcEfi48(eff, (float)minutes) : 0f
        };
    }
}
