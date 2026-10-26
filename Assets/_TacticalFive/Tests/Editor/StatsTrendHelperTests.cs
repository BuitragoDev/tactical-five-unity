using NUnit.Framework;

/// <summary>F6-03: tendencias calculadas solo con los splits persistidos.</summary>
public class StatsTrendHelperTests
{
    [Test]
    public void FromPlayer_DerivesPpgTsAndEfi48()
    {
        var row = new PlayerCareerSeasonRow
        {
            season_id = 1, year_start = 2026, year_end = 2027, games = 10,
            total_minutes = 300, total_points = 250,
            total_rebounds = 100, total_assists = 50, total_steals = 10, total_blocks = 10,
            total_fgm = 100, total_fga = 200, total_ftm = 40, total_fta = 50,
            total_turnovers = 20
        };

        var trend = StatsTrendHelper.FromPlayer(row);
        Assert.That(trend.ppg, Is.EqualTo(25f).Within(0.01f));
        Assert.That(trend.tsPct, Is.EqualTo(56.31f).Within(0.05f));
        Assert.That(trend.efi48, Is.EqualTo(46.4f).Within(0.01f));
    }

    [Test]
    public void FromLeague_HandlesEmptyMinutes()
    {
        var row = new LeagueSeasonAggregateRow
        {
            season_id = 2, year_start = 2026, year_end = 2027, games = 0,
            total_minutes = 0, total_points = 0
        };

        var trend = StatsTrendHelper.FromLeague(row);
        Assert.That(trend.ppg, Is.Zero);
        Assert.That(trend.tsPct, Is.Zero);
        Assert.That(trend.efi48, Is.Zero);
    }

    [Test]
    public void Delta_ShowsSignAndOneDecimal()
    {
        Assert.That(StatsTrendHelper.Delta(57.25f, 55.1f), Is.EqualTo("+2.2"));
        Assert.That(StatsTrendHelper.Delta(52.0f, 55.5f), Is.EqualTo("-3.5"));
    }
}
