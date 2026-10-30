using System.Collections.Generic;
using NUnit.Framework;

public class SeededCareerRulesTests
{
    static HistoricalPlayerStatsData Row(int playerId, string first, string last,
                                         int games, int points, int rebounds, int assists,
                                         int minutes, int dd = 0, int td = 0)
    {
        return new HistoricalPlayerStatsData
        {
            player_id = playerId,
            first_name = first,
            last_name = last,
            games = games,
            total_points = points,
            total_rebounds = rebounds,
            total_assists = assists,
            total_minutes = minutes,
            total_double_doubles = dd,
            total_triple_doubles = td
        };
    }

    [Test]
    public void LooksSyntheticBaseline_DetectaSinteticaMasTemporada()
    {
        // 1.470 PJ sintéticos (15.535 pts, 36 min/PJ) + 78 PJ reales en PHI.
        var owned = Row(480, "LeBron", "James", 1548, 17049, 4599, 4350, 55622, 12);
        var games = Row(480, "LeBron", "James", 78, 1514, 479, 372, 2702, 12);

        Assert.That(SeededCareerRules.LooksSyntheticBaseline(owned, games), Is.True);
    }

    [Test]
    public void LooksSyntheticBaseline_NoConfundeLaCarreraRealDelSeeder()
    {
        // La fila real del seeder usa 30 min/PJ, no los 36 de ApplyInitialCareer.
        var real = Row(-1, "LeBron", "James", 1492, 40474, 11185, 11009, 1492 * 30);

        Assert.That(SeededCareerRules.LooksSyntheticBaseline(real, null), Is.False);
    }

    [Test]
    public void LooksSyntheticBaseline_NoConfundeUnJugadorGenerado()
    {
        // Un rookie generado no tiene base previa: su fila es solo lo jugado.
        var games = Row(900, "Rookie", "Generado", 75, 900, 300, 150, 2100);
        var owned = Row(900, "Rookie", "Generado", 75, 900, 300, 150, 2100);

        Assert.That(SeededCareerRules.LooksSyntheticBaseline(owned, games), Is.False,
            "Sin base sintética previa no se debe tocar la carrera.");
    }

    [Test]
    public void Merge_SumaCarreraBaseYTemporadasJugadas()
    {
        var baseline = Row(-1, "LeBron", "James", 1492, 40474, 11185, 11009, 44760, 620, 125);
        var games = Row(480, "LeBron", "James", 78, 1514, 479, 372, 2702, 12);

        var merged = SeededCareerRules.Merge(baseline, games);

        Assert.That(merged.games, Is.EqualTo(1570));
        Assert.That(merged.total_points, Is.EqualTo(41988));
        Assert.That(merged.total_rebounds, Is.EqualTo(11664));
        Assert.That(merged.total_assists, Is.EqualTo(11381));
        Assert.That(merged.total_minutes, Is.EqualTo(47462));
        Assert.That(merged.total_double_doubles, Is.EqualTo(632));
        Assert.That(merged.total_triple_doubles, Is.EqualTo(125));
        Assert.That(baseline.total_points, Is.EqualTo(40474), "No debe mutar la fila base");
        Assert.That(games.total_points, Is.EqualTo(1514), "No debe mutar la fila jugada");
    }

    [Test]
    public void FindUniqueSeedRow_DevuelveLaFilaSiElNombreEsUnico()
    {
        var rows = new List<HistoricalPlayerStatsData>
        {
            Row(-1, "LeBron", "James", 1492, 40474, 11185, 11009, 44760),
            Row(-1, "Michael", "Jordan", 1072, 32292, 6672, 5633, 32160)
        };

        var found = SeededCareerRules.FindUniqueSeedRow(rows, "lebron", "JAMES");

        Assert.That(found, Is.Not.Null);
        Assert.That(found.total_points, Is.EqualTo(40474));
    }

    [Test]
    public void FindUniqueSeedRow_NoEligeSiHayHomonimos()
    {
        var rows = new List<HistoricalPlayerStatsData>
        {
            Row(-1, "John", "Smith", 100, 1000, 300, 200, 3000),
            Row(-1, "John", "Smith", 200, 2500, 700, 500, 6000)
        };

        Assert.That(SeededCareerRules.FindUniqueSeedRow(rows, "John", "Smith"), Is.Null,
            "Con homónimos no se puede decidir qué carrera pertenece al jugador.");
    }
}
