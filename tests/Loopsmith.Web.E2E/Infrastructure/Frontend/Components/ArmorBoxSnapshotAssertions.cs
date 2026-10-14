using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the Armor box.</summary>
public static class ArmorBoxSnapshotAssertions
{
    /// <summary>Every armor slot shows, and none wears a piece.</summary>
    public static AssertingThat<ArmorBoxSnapshot> HasNothingEquipped(this AssertingThat<ArmorBoxSnapshot> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(shown.Worn.IsEmpty, $"The Armor box shows a piece in {Describe(shown.Worn)}, but every slot was expected empty.");
        Assert.True(!shown.Empty.IsEmpty, "The Armor box shows no armor slot, but its empty slots were expected.");
        return that;
    }

    private static string Describe(IEnumerable<ArmorSlot> slots) => string.Join(", ", slots.Select(slot => slot.ToShownText()));
}
