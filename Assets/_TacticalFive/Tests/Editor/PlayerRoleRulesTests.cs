using NUnit.Framework;

/// <summary>
/// Rol por media (fuente única para seed, draft y migración). Los jugadores creados por el
/// juego (rookies del draft) antes quedaban en `UltimoRecurso`.
/// </summary>
public class PlayerRoleRulesTests
{
    [Test]
    public void FromOverall_UsesTheRosterScale()
    {
        Assert.That(PlayerRoleRules.FromOverall(95), Is.EqualTo(PlayerRole.Estrella));
        Assert.That(PlayerRoleRules.FromOverall(90), Is.EqualTo(PlayerRole.Estrella));
        Assert.That(PlayerRoleRules.FromOverall(89), Is.EqualTo(PlayerRole.Titular));
        Assert.That(PlayerRoleRules.FromOverall(83), Is.EqualTo(PlayerRole.Titular));
        Assert.That(PlayerRoleRules.FromOverall(82), Is.EqualTo(PlayerRole.Banquillo));
        Assert.That(PlayerRoleRules.FromOverall(74), Is.EqualTo(PlayerRole.Banquillo));
        Assert.That(PlayerRoleRules.FromOverall(73), Is.EqualTo(PlayerRole.UltimoRecurso));
        Assert.That(PlayerRoleRules.FromOverall(40), Is.EqualTo(PlayerRole.UltimoRecurso));
    }
}
