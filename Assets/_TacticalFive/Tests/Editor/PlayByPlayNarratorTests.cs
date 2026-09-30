using NUnit.Framework;

/// <summary>
/// Narrador del play-by-play (E4): reescribe las jugadas con comentario, conserva el
/// nombre del protagonista y no devuelve vacíos.
/// </summary>
public class PlayByPlayNarratorTests
{
    [Test]
    public void Narrate_EmptyOrNull_ReturnsEmpty()
    {
        Assert.That(PlayByPlayNarrator.Narrate(null, 0, 0, 1, 0.5), Is.Empty);
        Assert.That(PlayByPlayNarrator.Narrate("", 0, 0, 1, 0.5), Is.Empty);
        Assert.That(PlayByPlayNarrator.Narrate("   ", 0, 0, 1, 0.5), Is.Empty);
    }

    [Test]
    public void Narrate_KeepsOriginalDescription()
    {
        // El comentario no debe perder la información base (jugador y acción).
        string desc = "Curry encesta un triple";
        for (int i = 0; i <= 10; i++)
        {
            string narrated = PlayByPlayNarrator.Narrate(desc, 40, 40, 1, i / 10.0);
            Assert.That(narrated, Does.Contain("Curry encesta un triple"));
        }
    }

    [Test]
    public void Narrate_NeverEmptyForAnyType()
    {
        var jugadas = new[]
        {
            "Curry encesta un triple",
            "LeBron anota de dos (asist. Davis)",
            "Jokic anota y añade el tiro libre (falta de Embiid)",
            "Wemby tapona el tiro de Curry",
            "Draymond pierde el balón",
            "Lillard convierte 2/2 tiros libres (falta de Green)",
            "Gobert captura el rebote",
            "Pérdida"
        };
        foreach (var j in jugadas)
            for (int i = 0; i <= 10; i++)
                Assert.That(PlayByPlayNarrator.Narrate(j, 100, 98, 4, i / 10.0), Is.Not.Empty, j);
    }

    [Test]
    public void Narrate_ClutchAddsDrama()
    {
        // Último cuarto y marcador apretado → aparece "momento/partidazo/final".
        bool found = false;
        for (int i = 0; i <= 20; i++)
        {
            string t = PlayByPlayNarrator.Narrate("Curry encesta un triple", 100, 98, 4, i / 20.0);
            if (t.Contains("Momento") || t.Contains("Partidazo") || t.Contains("final")
                || t.Contains("Nervios") || t.Contains("decide"))
            { found = true; break; }
        }
        Assert.That(found, Is.True, "en un final apretado debe haber dramatismo");
    }

    [Test]
    public void Narrate_BlowoutGetsResignedTone()
    {
        bool found = false;
        for (int i = 0; i <= 20; i++)
        {
            string t = PlayByPlayNarrator.Narrate("Curry encesta un triple", 120, 80, 3, i / 20.0);
            if (t.Contains("trámite") || t.Contains("paliza") || t.Contains("piedad"))
            { found = true; break; }
        }
        Assert.That(found, Is.True, "una paliza suena distinta a un final apretado");
    }

    [Test]
    public void Narrate_OvertimeGetsOvertimeFlavor()
    {
        bool found = false;
        for (int i = 0; i <= 20; i++)
        {
            string t = PlayByPlayNarrator.Narrate("Curry anota de dos", 110, 109, 5, i / 20.0);
            if (t.Contains("PRÓRROGA") || t.Contains("Tiempo extra") || t.Contains("extra")
                || t.Contains("rinde"))
            { found = true; break; }
        }
        Assert.That(found, Is.True, "la prórroga tiene su propio tono");
    }

    [Test]
    public void Narrate_IsDeterministicForSameRoll()
    {
        string a = PlayByPlayNarrator.Narrate("Curry encesta un triple", 50, 50, 2, 0.42);
        string b = PlayByPlayNarrator.Narrate("Curry encesta un triple", 50, 50, 2, 0.42);
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void Narrate_DifferentRollsProduceVariety()
    {
        var seen = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i <= 30; i++)
            seen.Add(PlayByPlayNarrator.Narrate("Curry encesta un triple", 50, 48, 3, i / 30.0));
        Assert.That(seen.Count, Is.GreaterThan(1), "debe haber variantes");
    }
}
