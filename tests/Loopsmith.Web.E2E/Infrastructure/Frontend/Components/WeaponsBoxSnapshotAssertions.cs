using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the Weapons box.</summary>
public static class WeaponsBoxSnapshotAssertions
{
    /// <summary>Every weapon slot shows, and none holds a weapon.</summary>
    public static AssertingThat<WeaponsBoxSnapshot> HasNothingEquipped(this AssertingThat<WeaponsBoxSnapshot> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(shown.Equipped.IsEmpty, $"The Weapons box shows a weapon in {Describe(shown.Equipped)}, but every slot was expected empty.");
        Assert.True(!shown.Empty.IsEmpty, "The Weapons box shows no weapon slot, but its empty slots were expected.");
        return that;
    }

    private static string Describe(IEnumerable<WeaponSlot> slots) => string.Join(", ", slots.Select(slot => slot.ToShownText()));
}
