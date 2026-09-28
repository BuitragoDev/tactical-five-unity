using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// ECO-15: un swap de picks debe ser una permutación de los dueños de dos slots, nunca
/// una sobreescritura que deje a un tercero sin pick (o a un equipo con dos).
/// </summary>
public class DraftGeneratorTests
{
    // Un dueño por slot (posición 0-29), como en DraftGenerator.BuildSlotOwners.
    static Dictionary<int, TeamData> Owners(params int[] ids)
    {
        var map = new Dictionary<int, TeamData>();
        for (int i = 0; i < ids.Length; i++) map[i] = new TeamData { id = ids[i] };
        return map;
    }

    [Test]
    public void ApplySwap_ExchangesOwners_KeepingDistinctCount()
    {
        var owners = Owners(1, 2, 3, 4);

        DraftGenerator.ApplySwap(owners, 0, 2);

        Assert.That(owners[0].id, Is.EqualTo(3));
        Assert.That(owners[2].id, Is.EqualTo(1));
        Assert.That(DraftGenerator.CountDistinctOwners(owners), Is.EqualTo(4));
    }

    [Test]
    public void ApplySwap_IgnoresInvalidSlots()
    {
        var owners = Owners(1, 2, 3);

        DraftGenerator.ApplySwap(owners, 1, 1);    // mismo slot
        DraftGenerator.ApplySwap(owners, -1, 2);   // slot inexistente
        DraftGenerator.ApplySwap(owners, 0, 9);    // slot inexistente
        DraftGenerator.ApplySwap(null, 0, 1);      // sin diccionario

        Assert.That(owners[0].id, Is.EqualTo(1));
        Assert.That(owners[1].id, Is.EqualTo(2));
        Assert.That(owners[2].id, Is.EqualTo(3));
    }

    [Test]
    public void ApplySwap_NeverLosesOwners_OnOverlappingSwaps()
    {
        // Swaps encadenados sobre slots solapados: el número de dueños distintos no baja.
        var owners = Owners(1, 2, 3, 4);
        int before = DraftGenerator.CountDistinctOwners(owners);

        DraftGenerator.ApplySwap(owners, 0, 1);
        DraftGenerator.ApplySwap(owners, 1, 2);
        DraftGenerator.ApplySwap(owners, 0, 2);

        Assert.That(DraftGenerator.CountDistinctOwners(owners), Is.EqualTo(before));
    }

    [Test]
    public void CountDistinctOwners_IgnoresNullsAndMissingSlots()
    {
        Assert.That(DraftGenerator.CountDistinctOwners(Owners(1, 1, 2, 2)), Is.EqualTo(2));

        var withNull = Owners(7);
        withNull[1] = null;
        Assert.That(DraftGenerator.CountDistinctOwners(withNull), Is.EqualTo(1));

        Assert.That(DraftGenerator.CountDistinctOwners(null), Is.EqualTo(0));
    }
}
