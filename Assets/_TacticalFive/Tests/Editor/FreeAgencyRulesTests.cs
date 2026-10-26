using System.Collections.Generic;
using NUnit.Framework;

public class FreeAgencyRulesTests
{
    [TestCase(0, "RFA")]
    [TestCase(1, "RFA")]
    [TestCase(3, "RFA")]
    [TestCase(4, "UFA")]
    public void TypeForService_UsesSimplifiedServiceBands(int years, string expected)
    {
        Assert.That(FreeAgencyRules.TypeForService(years), Is.EqualTo(expected));
    }

    [Test]
    public void TwoWay_IsNotRestrictedAndDoesNotReceiveAQualifyingOffer()
    {
        Assert.That(FreeAgencyRules.TypeForService(1, isTwoWay: true), Is.EqualTo("UFA"));
    }

    [Test]
    public void QualifyingOffer_IsAtLeastMinimumAndOtherwise125PercentOfPriorSalary()
    {
        Assert.That(FreeAgencyRules.QualifyingOffer(4_000_000, 2_000_000), Is.EqualTo(5_000_000));
        Assert.That(FreeAgencyRules.QualifyingOffer(1_000_000, 2_000_000), Is.EqualTo(2_000_000));
    }

    [Test]
    public void CapHold_UsesQOForRFA_AndBirdMultiplierForUFA()
    {
        Assert.That(FreeAgencyRules.CapHold(4_000_000, 3, 3, 5_000_000, 2_000_000), Is.EqualTo(5_000_000));
        Assert.That(FreeAgencyRules.CapHold(10_000_000, 4, 3, 0, 2_000_000), Is.EqualTo(15_000_000));
        Assert.That(FreeAgencyRules.CapHold(10_000_000, 4, 2, 0, 2_000_000), Is.EqualTo(12_000_000));
    }

    [Test]
    public void EffectivePayroll_IncludesHoldsButExcludesTwoWayAndSignedPlayerDuplicateHold()
    {
        var roster = new List<PlayerData>
        {
            new PlayerData { id = 1, team_id = 7, salary = 10_000_000, contract_years = 2 },
            new PlayerData { id = 2, team_id = 7, salary = 1_000_000, contract_years = 1, is_two_way = 1 }
        };
        var holds = new List<TeamCapHoldData>
        {
            new TeamCapHoldData { player_id = 1, amount = 9_000_000, is_active = 1 },
            new TeamCapHoldData { player_id = 3, amount = 5_000_000, is_active = 1 },
            new TeamCapHoldData { player_id = 4, amount = 8_000_000, is_active = 0 }
        };

        Assert.That(FreeAgencyRules.EffectivePayroll(roster, holds), Is.EqualTo(15_000_000));
    }

    [Test]
    public void CapSheetProjection_IncludesContractsOptionsAndCurrentHoldOnly()
    {
        var players = new List<PlayerData>
        {
            new PlayerData { salary = 100_000_000, contract_years = 2, guaranteed_years = 1 },
            new PlayerData { salary = 20_000_000, contract_years = 2, guaranteed_years = 1, has_team_option = 1 },
            new PlayerData { salary = 3_000_000, contract_years = 2, guaranteed_years = 2, is_two_way = 1 }
        };
        var holds = new List<TeamCapHoldData>
        {
            new TeamCapHoldData { amount = 10_000_000, is_active = 1 }
        };

        Assert.That(CapSheetRules.ProjectedPayroll(players, holds, 0), Is.EqualTo(130_000_000));
        Assert.That(CapSheetRules.ProjectedPayroll(players, holds, 1), Is.EqualTo(20_000_000));
    }

    [Test]
    public void AI_MatchesOnlyWhenSimplifiedRFAThresholdPasses()
    {
        Assert.That(FreeAgencyRules.ShouldAIMatch("RFA", 80, 20_000_000, 10_000_000), Is.True);
        Assert.That(FreeAgencyRules.ShouldAIMatch("RFA", 79, 15_000_000, 10_000_000), Is.True);
        Assert.That(FreeAgencyRules.ShouldAIMatch("RFA", 79, 15_000_001, 10_000_000), Is.False);
        Assert.That(FreeAgencyRules.ShouldAIMatch("UFA", 99, 1_000_000, 1_000_000), Is.False);
    }
}
