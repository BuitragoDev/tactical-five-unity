using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Selección de los mejores quintetos de la temporada (1º y 2º) sin repetir jugador.
/// Reglas puras de QuintetPicker.
/// </summary>
public class QuintetPickerTests
{
    static QuintetCandidate P(int id, string pos, float rating, string secondary = "")
        => new QuintetCandidate
        {
            PlayerId = id,
            PlayerName = $"Jugador {id}",
            Position = pos,
            SecondaryPosition = secondary,
            AvgRating = rating
        };

    [Test]
    public void Pick_FirstQuintetTakesTheBestPerPosition()
    {
        var cands = new List<QuintetCandidate>
        {
            P(1, "PG", 90f), P(2, "SG", 89f), P(3, "SF", 88f), P(4, "PF", 87f), P(5, "C", 86f),
            P(6, "PG", 80f), P(7, "SG", 79f)
        };

        var teams = QuintetPicker.Pick(cands, 1);

        Assert.That(teams, Has.Count.EqualTo(1));
        Assert.That(teams[0], Has.Count.EqualTo(5));
        Assert.That(teams[0][0].position, Is.EqualTo("PG"));
        Assert.That(teams[0][0].player.PlayerId, Is.EqualTo(1));
        Assert.That(teams[0][4].player.PlayerId, Is.EqualTo(5));
    }

    [Test]
    public void Pick_SecondQuintetNeverRepeatsPlayers()
    {
        var cands = new List<QuintetCandidate>
        {
            P(1, "PG", 90f), P(2, "SG", 89f), P(3, "SF", 88f), P(4, "PF", 87f), P(5, "C", 86f),
            P(6, "PG", 85f), P(7, "SG", 84f), P(8, "SF", 83f), P(9, "PF", 82f), P(10, "C", 81f)
        };

        var teams = QuintetPicker.Pick(cands, 2);

        Assert.That(teams, Has.Count.EqualTo(2));
        var ids1 = new HashSet<int>();
        foreach (var s in teams[0]) ids1.Add(s.player.PlayerId);
        foreach (var s in teams[1])
        {
            Assert.That(ids1.Contains(s.player.PlayerId), Is.False, "el 2º quinteto no repite a nadie del 1º");
        }
        Assert.That(teams[1][0].player.PlayerId, Is.EqualTo(6));
        Assert.That(teams[1][4].player.PlayerId, Is.EqualTo(10));
    }

    [Test]
    public void Pick_SecondaryPositionCountsForTheSlot()
    {
        // Un tirador con PG como secundaria y mejor rating le quita el puesto de base al PG puro;
        // el PG puro se queda sin hueco (su posición es solo PG).
        var cands = new List<QuintetCandidate>
        {
            P(1, "PG", 90f), P(2, "SG", 95f, secondary: "PG")
        };

        var teams = QuintetPicker.Pick(cands, 1);

        Assert.That(teams[0], Has.Count.EqualTo(1), "solo se cubre el puesto de base");
        Assert.That(teams[0][0].position, Is.EqualTo("PG"));
        Assert.That(teams[0][0].player.PlayerId, Is.EqualTo(2), "la posición secundaria cuenta y gana el mejor rating");
    }

    [Test]
    public void Pick_HandlesIncompleteAndEmptyPools()
    {
        var few = QuintetPicker.Pick(new List<QuintetCandidate> { P(1, "C", 80f) }, 2);
        Assert.That(few[0], Has.Count.EqualTo(1));
        Assert.That(few[1], Is.Empty, "sin más candidatos el 2º quinteto queda vacío");

        Assert.That(QuintetPicker.Pick(null), Is.Empty);
        Assert.That(QuintetPicker.Pick(new List<QuintetCandidate>()), Is.Empty);
    }

    [Test]
    public void Pick_HigherRatingWinsThePosition()
    {
        var cands = new List<QuintetCandidate> { P(1, "C", 70f), P(2, "C", 95f) };

        var teams = QuintetPicker.Pick(cands, 1);

        Assert.That(teams[0][0].player.PlayerId, Is.EqualTo(2));
    }
}
