using NUnit.Framework;
using UnityEngine;

public class EditModeSmokeTests
{
    [Test]
    public void AssemblyCSharp_TypesAreAccessibleFromTestAssembly()
    {
        Assert.That(typeof(ScreenManager).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
        Assert.That(typeof(TradeHelper).IsAbstract && typeof(TradeHelper).IsSealed, Is.True);
    }

    [Test]
    public void SalaryCaps_MatchLeagueSettingsConstants()
    {
        Assert.That(TradeHelper.FIRST_APRON, Is.EqualTo(229_015_000L));
        Assert.That(TradeHelper.SECOND_APRON, Is.EqualTo(241_686_000L));
        Assert.That(TradeHelper.LUXURY_TAX, Is.EqualTo(220_428_000L));
        Assert.That(TradeHelper.NT_MLE, Is.EqualTo(14_100_000L));
        Assert.That(TradeHelper.T_MLE, Is.EqualTo(5_700_000L));
        Assert.That(TradeHelper.MIN_SALARY, Is.EqualTo(2_000_000L));
    }
}
