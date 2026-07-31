using System.Collections.Generic;
using NUnit.Framework;

public class TradeHelperTests
{
    static PlayerData Player(long salary, int overall = 80, int age = 25)
    {
        return new PlayerData { salary = salary, overall = overall, age = age };
    }

    [Test]
    public void CalculateLuxuryTax_UnderThreshold_ReturnsZero()
    {
        Assert.That(TradeHelper.CalculateLuxuryTax(200_000_000L), Is.EqualTo(0L));
        Assert.That(TradeHelper.CalculateLuxuryTax(TradeHelper.LUXURY_TAX), Is.EqualTo(0L));
    }

    [Test]
    public void CalculateLuxuryTax_ProgressiveBrackets()
    {
        Assert.That(TradeHelper.CalculateLuxuryTax(230_428_000L), Is.EqualTo(16_250_000L));
        Assert.That(TradeHelper.CalculateLuxuryTax(240_428_000L), Is.EqualTo(45_000_000L));
        Assert.That(TradeHelper.CalculateLuxuryTax(245_428_000L), Is.EqualTo(63_750_000L));
    }

    [Test]
    public void CalculateLuxuryTax_RespectsCustomThreshold()
    {
        Assert.That(TradeHelper.CalculateLuxuryTax(210_000_000L, 200_000_000L), Is.EqualTo(16_250_000L));
    }

    [Test]
    public void PickBonus_FirstOverallPick_IsMaxBonus()
    {
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 1, pick_number = 1 }), Is.EqualTo(19));
    }

    [Test]
    public void PickBonus_LastFirstRoundPick_IsBaseBonus()
    {
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 1, pick_number = 30 }), Is.EqualTo(10));
    }

    [Test]
    public void PickBonus_SecondRound()
    {
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 2, pick_number = 31 }), Is.EqualTo(10));
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 2, pick_number = 60 }), Is.EqualTo(5));
    }

    [Test]
    public void PickBonus_NullAndLateRound()
    {
        Assert.That(TradeHelper.PickBonus(null), Is.EqualTo(0));
        Assert.That(TradeHelper.PickBonus(new DraftPickData { round = 3, pick_number = 5 }), Is.EqualTo(3));
    }

    [Test]
    public void ValidateTrade_FairTrade_HasNoErrors()
    {
        var a = new List<PlayerData> { Player(10_000_000) };
        var b = new List<PlayerData> { Player(10_000_000) };
        var errors = TradeHelper.ValidateTrade(a, b, 12, 12, "Bulls", 100_000_000L, "Lakers", 100_000_000L);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateTrade_BelowMinRoster_ReportsError()
    {
        var a = new List<PlayerData>
        {
            Player(10_000_000), Player(10_000_000), Player(10_000_000),
            Player(10_000_000), Player(10_000_000)
        };
        var errors = TradeHelper.ValidateTrade(a, new List<PlayerData>(), 12, 12, "Bulls", 100_000_000L, "Lakers", 100_000_000L);
        Assert.That(errors, Has.Some.Contains("mínimo 10"));
    }

    [Test]
    public void ValidateTrade_AboveMaxRoster_ReportsError()
    {
        var b = new List<PlayerData> { Player(10_000_000), Player(10_000_000), Player(10_000_000) };
        var errors = TradeHelper.ValidateTrade(new List<PlayerData>(), b, 15, 15, "Bulls", 100_000_000L, "Lakers", 100_000_000L);
        Assert.That(errors, Has.Some.Contains("máximo 17"));
    }

    [Test]
    public void ValidateTrade_HardCappedSecondApron_RejectsMultiplePlayersAndSalaryGain()
    {
        var a = new List<PlayerData> { Player(10_000_000), Player(10_000_000) };
        var b = new List<PlayerData> { Player(5_000_000) };
        var errors = TradeHelper.ValidateTrade(a, b, 12, 12, "Bulls", 250_000_000L, "Lakers", 100_000_000L, false, true);
        Assert.That(errors, Has.Some.Contains("múltiples jugadores"));
        Assert.That(errors, Has.Some.Contains("salario ≤ al que envía"));
    }

    [Test]
    public void ValidateTrade_FirstApron_Applies110PercentRule()
    {
        var a = new List<PlayerData> { Player(10_000_000) };
        var b = new List<PlayerData> { Player(5_000_000) };
        var errors = TradeHelper.ValidateTrade(a, b, 12, 12, "Bulls", 235_000_000L, "Lakers", 100_000_000L);
        Assert.That(errors, Has.Some.Contains("110%"));
    }

    [Test]
    public void ValidateTrade_BelowApron_AppliesSalaryMatchingRule()
    {
        var a = new List<PlayerData> { Player(11_000_000) };
        var b = new List<PlayerData> { Player(5_000_000) };
        var errors = TradeHelper.ValidateTrade(a, b, 12, 12, "Bulls", 100_000_000L, "Lakers", 100_000_000L);
        Assert.That(errors, Has.Some.Contains("no puede recibir más de"));
    }

    [Test]
    public void EvaluateTrade_ScoreClampedAndConsistentWithThreshold()
    {
        var a = new List<PlayerData> { Player(10_000_000, 80, 24), Player(5_000_000, 82, 26) };
        var b = new List<PlayerData> { Player(20_000_000, 90, 28) };
        var result = TradeHelper.EvaluateTrade(a, b, "Bulls", 12, 100_000_000L);
        Assert.That(result.AcceptScore, Is.InRange(0, 100));
        Assert.That(result.Threshold, Is.EqualTo(50));
        Assert.That(result.WouldAccept, Is.EqualTo(result.AcceptScore >= result.Threshold));
    }

    [Test]
    public void EvaluateTrade_SecondApron_LowersThreshold()
    {
        var a = new List<PlayerData> { Player(10_000_000, 80, 24) };
        var b = new List<PlayerData> { Player(5_000_000, 90, 28) };
        var result = TradeHelper.EvaluateTrade(a, b, "Bulls", 12, 250_000_000L);
        Assert.That(result.Threshold, Is.EqualTo(40));
    }
}
