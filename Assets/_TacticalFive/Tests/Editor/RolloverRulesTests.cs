using NUnit.Framework;

/// <summary>
/// B41: reglas del rollover para rookies. Un rookie de primer año envejece y progresa,
/// pero su contrato no decrementa hasta el año siguiente.
/// </summary>
public class RolloverRulesTests
{
    [Test]
    public void ShouldDecrementContract_FirstYearRookie_IsFalse()
    {
        Assert.That(RolloverRules.ShouldDecrementContract(true), Is.False,
            "el contrato de rookie no baja en su primer año");
    }

    [Test]
    public void ShouldDecrementContract_Veteran_IsTrue()
    {
        Assert.That(RolloverRules.ShouldDecrementContract(false), Is.True);
    }

    [Test]
    public void RookieAgesAndProgressesButKeepsContract()
    {
        // Escenario del bug B41: un rookie de 19 con contrato de 3 años.
        int age = 19;
        int contractYears = 3;
        bool isFirstYearRookie = true;

        // Rollover: envejece siempre; el contrato solo baja si la regla lo permite.
        age += 1;
        if (RolloverRules.ShouldDecrementContract(isFirstYearRookie))
            contractYears = System.Math.Max(0, contractYears - 1);

        Assert.That(age, Is.EqualTo(20), "debe cumplir años");
        Assert.That(contractYears, Is.EqualTo(3), "su contrato de rookie sigue intacto");

        // Al año siguiente ya no es rookie: ahora sí decrementa.
        isFirstYearRookie = false;
        age += 1;
        if (RolloverRules.ShouldDecrementContract(isFirstYearRookie))
            contractYears = System.Math.Max(0, contractYears - 1);

        Assert.That(age, Is.EqualTo(21));
        Assert.That(contractYears, Is.EqualTo(2), "en el 2º rollover el contrato baja");
    }
}
