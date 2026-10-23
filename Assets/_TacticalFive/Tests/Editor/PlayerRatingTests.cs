using NUnit.Framework;

/// <summary>
/// F0-04 · La fórmula del `overall` vive en un único sitio (`PlayerRating`).
///
/// Antes había cuatro copias: `PlayerData`, `GLeaguePlayerData`, el seed de jugadores
/// y agentes libres (división entera) y el seed de G-League (con un paréntesis mal
/// puesto que daba `potential / 11`). Estos tests fijan la fórmula canónica y que los
/// dos modelos de jugador devuelvan exactamente el mismo valor.
/// </summary>
public class PlayerRatingTests
{
    // Suma = 765 → 765 / 11 = 69.54 → 70
    static PlayerData NbaPlayer(int potential) => new PlayerData
    {
        speed = 70, shooting = 80, three_point = 75, passing = 65, dribbling = 72,
        defense = 68, rebounding = 60, athleticism = 77, iq = 74, steals = 66, blocks = 58,
        potential = potential
    };

    static GLeaguePlayerData GlPlayer(int potential) => new GLeaguePlayerData
    {
        speed = 70, shooting = 80, three_point = 75, passing = 65, dribbling = 72,
        defense = 68, rebounding = 60, athleticism = 77, iq = 74, steals = 66, blocks = 58,
        potential = potential
    };

    [Test]
    public void Overall_EsLaMediaRedondeadaDeLos11Atributos()
    {
        Assert.That(PlayerRating.FromSum(765, 99), Is.EqualTo(70));
        Assert.That(PlayerRating.FromSum(660, 99), Is.EqualTo(60));
    }

    [Test]
    public void Overall_RedondeaAlEnteroMasCercano()
    {
        Assert.That(PlayerRating.FromSum(665, 99), Is.EqualTo(60)); // 60.45 → 60
        Assert.That(PlayerRating.FromSum(666, 99), Is.EqualTo(61)); // 60.55 → 61
    }

    [Test]
    public void Overall_NoSuperaElPotencial()
    {
        Assert.That(PlayerRating.FromSum(765, 65), Is.EqualTo(65));
        Assert.That(NbaPlayer(potential: 65).GetCalculatedAverage(), Is.EqualTo(65));
    }

    [Test]
    public void Overall_SinPotencial_NoSeCapa()
    {
        Assert.That(PlayerRating.FromSum(765, 0), Is.EqualTo(70));
    }

    [Test]
    public void NbaYGLeague_CompartenLaMismaFormula()
    {
        foreach (int potential in new[] { 0, 50, 65, 70, 90, 99 })
            Assert.That(GlPlayer(potential).GetCalculatedAverage(), Is.EqualTo(NbaPlayer(potential).GetCalculatedAverage()),
                $"Discrepancia con potential={potential}");
    }

    [Test]
    public void RecalculateOverall_FijaElOverallDeLosAtributos()
    {
        var nba = NbaPlayer(potential: 99);
        nba.overall = 12;
        nba.RecalculateOverall();
        Assert.That(nba.overall, Is.EqualTo(70));

        var gl = GlPlayer(potential: 60);
        gl.overall = 6;               // el valor que dejaba el bug de GLeagueSeeder
        gl.RecalculateOverall();
        Assert.That(gl.overall, Is.EqualTo(60));
    }
}
