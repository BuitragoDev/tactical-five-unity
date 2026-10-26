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
    public void SalaryCaps_MatchThe2026_27CbaRow()
    {
        var season = CbaTable.For(2026);
        Assert.That(TradeHelper.SALARY_CAP, Is.EqualTo(season.SalaryCap));
        Assert.That(TradeHelper.LUXURY_TAX, Is.EqualTo(season.LuxuryTax));
        Assert.That(TradeHelper.FIRST_APRON, Is.EqualTo(season.FirstApron));
        Assert.That(TradeHelper.SECOND_APRON, Is.EqualTo(season.SecondApron));
        Assert.That(TradeHelper.NT_MLE, Is.EqualTo(season.NonTaxpayerMle));
        Assert.That(TradeHelper.T_MLE, Is.EqualTo(season.TaxpayerMle));
        Assert.That(TradeHelper.TWO_WAY_SALARY, Is.EqualTo(season.TwoWaySalary));
        Assert.That(TradeHelper.MIN_SALARY, Is.EqualTo(season.MinimumSalary),
            "El fallback es el mínimo de 0 años de 2026-27.");
    }
}
