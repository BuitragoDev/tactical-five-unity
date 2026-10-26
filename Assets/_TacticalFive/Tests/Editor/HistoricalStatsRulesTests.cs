using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Regresión de SEA-06: el histórico de carrera se agregaba por nombre completo, así que
/// un rookie generado con el nombre de una leyenda heredaba sus totales, su inducción al
/// Salón de la Fama y su dorsal retirado. La clave es ahora `player_id`.
/// </summary>
public class HistoricalStatsRulesTests
{
    static HistoricalPlayerStatsData Row(int playerId, string first, string last, int points = 100)
        => new HistoricalPlayerStatsData
        {
            player_id = playerId,
            first_name = first,
            last_name = last,
            total_points = points
        };

    [Test]
    public void RowForPlayer_UsesPlayerId_NotName()
    {
        var rows = new List<HistoricalPlayerStatsData>
        {
            Row(HistoricalStatsRules.LEGEND_ID, "Michael", "Jordan", 32292), // leyenda
            Row(HistoricalStatsRules.UNKNOWN_ID, "Michael", "Jordan", 40),    // fila vieja sin dueño
            Row(42, "Michael", "Jordan", 15)                                  // el rookie real
        };

        var row = HistoricalStatsRules.RowForPlayer(42, rows);

        Assert.That(row, Is.Not.Null);
        Assert.That(row.player_id, Is.EqualTo(42));
        Assert.That(row.total_points, Is.EqualTo(15), "No debe heredar los totales de la leyenda");
    }

    [Test]
    public void RowForPlayer_DoesNotInheritLegend_WhenPlayerHasNoRow()
    {
        var rows = new List<HistoricalPlayerStatsData>
        {
            Row(HistoricalStatsRules.LEGEND_ID, "Michael", "Jordan", 32292)
        };

        Assert.That(HistoricalStatsRules.RowForPlayer(42, rows), Is.Null);
        Assert.That(HistoricalStatsRules.RowForPlayer(0, rows), Is.Null, "Id inválido");
    }

    [Test]
    public void CanAdopt_OnlyOwnerlessRow_WithUniqueName()
    {
        var orphan = Row(HistoricalStatsRules.UNKNOWN_ID, "John", "Smith");

        Assert.That(HistoricalStatsRules.CanAdopt(orphan, new List<int> { 7 }), Is.True);
        Assert.That(HistoricalStatsRules.CanAdopt(orphan, new List<int> { 7, 8 }), Is.False,
            "Con homónimos la fila es una mezcla y no se puede repartir");
        Assert.That(HistoricalStatsRules.CanAdopt(orphan, new List<int>()), Is.False,
            "Sin jugador con ese nombre no hay a quién adoptarla");
    }

    [Test]
    public void CanAdopt_RejectsLegendRow_AndOwnedRow()
    {
        var legend = Row(HistoricalStatsRules.LEGEND_ID, "Michael", "Jordan");
        var owned = Row(42, "Michael", "Jordan");

        Assert.That(HistoricalStatsRules.CanAdopt(legend, new List<int> { 42 }), Is.False,
            "Las filas del seeder son intocables");
        Assert.That(HistoricalStatsRules.CanAdopt(owned, new List<int> { 42 }), Is.False,
            "Una fila con dueño no se reasigna");
    }

    [Test]
    public void BuildAdoptionPlan_DetectsAmbiguousHomonyms()
    {
        var unique = Row(HistoricalStatsRules.UNKNOWN_ID, "Unique", "Player");
        unique.id = 10;
        var ambiguous = Row(HistoricalStatsRules.UNKNOWN_ID, "John", "Smith");
        ambiguous.id = 11;
        var owned = Row(42, "John", "Smith");
        owned.id = 12;

        var players = new Dictionary<(string, string), List<int>>
        {
            [("Unique", "Player")] = new List<int> { 7 },
            [("John", "Smith")] = new List<int> { 8, 9 }
        };

        var plan = HistoricalStatsRules.BuildAdoptionPlan(
            new List<HistoricalPlayerStatsData> { unique, ambiguous, owned }, players);

        Assert.That(plan, Has.Count.EqualTo(1));
        Assert.That(plan[10], Is.EqualTo(7), "El nombre único se adopta");
        Assert.That(plan.ContainsKey(11), Is.False, "Dos homónimos → no se adopta");
        Assert.That(plan.ContainsKey(12), Is.False, "Una fila con dueño no entra en el plan");
    }

    [Test]
    public void IsLegendRow_IdentifiesSeederRows()
    {
        Assert.That(HistoricalStatsRules.IsLegendRow(Row(HistoricalStatsRules.LEGEND_ID, "A", "B")), Is.True);
        Assert.That(HistoricalStatsRules.IsLegendRow(Row(HistoricalStatsRules.UNKNOWN_ID, "A", "B")), Is.False);
        Assert.That(HistoricalStatsRules.IsLegendRow(Row(1, "A", "B")), Is.False);
        Assert.That(HistoricalStatsRules.IsLegendRow(null), Is.False);
    }
}
