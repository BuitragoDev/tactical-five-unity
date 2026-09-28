using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Rivalidades (playoff_series) y etiquetas de serie. Reglas puras.
/// </summary>
public class RivalryHelperTests
{
    static PlayoffSeriesData Series(int season, string stage, int high, int low, int winner, string label = null)
        => new PlayoffSeriesData
        {
            season_id = season,
            stage = stage,
            label = label ?? $"playoff-{stage}-x",
            high_team_id = high,
            low_team_id = low,
            winner_id = winner,
            loser_id = winner == high ? low : high,
            high_wins = 4,
            low_wins = winner == high ? 2 : 3,
            game_day = 100
        };

    // ── ETIQUETAS ───────────────────────────────────────────

    [Test]
    public void ParseSeriesLabel_ReadsRoundAndConference()
    {
        Assert.That(PlayoffsGenerator.ParseSeriesLabel("playoff-r1-east-1v8"), Is.EqualTo(("r1", "East")));
        Assert.That(PlayoffsGenerator.ParseSeriesLabel("playoff-r3-west-s1"), Is.EqualTo(("r3", "West")));
        Assert.That(PlayoffsGenerator.ParseSeriesLabel("playoff-r4-finals"), Is.EqualTo(("r4", "")));
        Assert.That(PlayoffsGenerator.ParseSeriesLabel("playin-7-8-east"), Is.EqualTo(("", "")),
            "El play-in no es una serie");
        Assert.That(PlayoffsGenerator.ParseSeriesLabel(null), Is.EqualTo(("", "")));
    }

    // ── AGREGACIÓN ──────────────────────────────────────────

    [Test]
    public void RivalsOf_AggregatesBalanceAndLastMeeting()
    {
        var series = new List<PlayoffSeriesData>
        {
            Series(2026, "r1", 1, 2, 1),   // yo (1) gano
            Series(2027, "r2", 2, 1, 2),   // pierdo
            Series(2028, "r4", 1, 2, 2)    // pierdo unas Finales
        };

        var rival = RivalryHelper.RivalsOf(series, 1).Single();

        Assert.That(rival.opponentId, Is.EqualTo(2));
        Assert.That(rival.series, Is.EqualTo(3));
        Assert.That(rival.wins, Is.EqualTo(1));
        Assert.That(rival.losses, Is.EqualTo(2));
        Assert.That(rival.finalsMeetings, Is.EqualTo(1));
        Assert.That(rival.lastSeasonId, Is.EqualTo(2028));
        Assert.That(rival.lastStage, Is.EqualTo("r4"));
        Assert.That(rival.lastWon, Is.False);
    }

    [Test]
    public void RivalsOf_IgnoresOtherTeamsAndNonPlayoffLabels()
    {
        var series = new List<PlayoffSeriesData>
        {
            Series(2026, "r1", 3, 4, 3),                                   // no es mi equipo
            Series(2026, "r1", 1, 5, 1, label: "playin-7-8-east")          // no es serie
        };

        Assert.That(RivalryHelper.RivalsOf(series, 1), Is.Empty);
    }

    [Test]
    public void RivalsOf_FinalsAndRecencyRaiseIntensity()
    {
        var others = new List<PlayoffSeriesData>
        {
            Series(2020, "r1", 1, 9, 1),   // cruce antiguo y desigual
            Series(2026, "r4", 1, 7, 7)    // Finales recientes
        };

        var rivals = RivalryHelper.RivalsOf(others, 1, currentSeasonId: 2026);

        Assert.That(rivals[0].opponentId, Is.EqualTo(7), "Las Finales recientes pesan más");
        Assert.That(rivals[0].intensity, Is.GreaterThan(rivals[1].intensity));
    }

    [Test]
    public void LastEliminationBy_FindsMostRecentLoss()
    {
        var series = new List<PlayoffSeriesData>
        {
            Series(2024, "r1", 1, 2, 2),   // me eliminan
            Series(2028, "r1", 1, 2, 1),   // me vengo
            Series(2029, "r2", 2, 1, 2)    // me vuelven a eliminar
        };

        var elimination = RivalryHelper.LastEliminationBy(series, 1, 2);

        Assert.That(elimination, Is.Not.Null);
        Assert.That(elimination.season_id, Is.EqualTo(2029));
        Assert.That(RivalryHelper.HasHistory(series, 1, 2), Is.True);
        Assert.That(RivalryHelper.HasHistory(series, 1, 99), Is.False);
    }
}
