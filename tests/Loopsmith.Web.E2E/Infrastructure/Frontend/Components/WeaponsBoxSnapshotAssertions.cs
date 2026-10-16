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

    /// <summary>Its <paramref name="slot"/> slot holds <paramref name="weapon"/>, its square showing <paramref name="damage"/>.</summary>
    public static AssertingThat<WeaponsBoxSnapshot> Holds(this AssertingThat<WeaponsBoxSnapshot> that, WeaponSlot slot, Weapon weapon, DamageType damage)
    {
        var worn = that.InstanceToAssert.Weapons.Where(shown => shown.Slot == slot).ToArray();
        var expected = new WornWeapon(slot, weapon, damage);
        Assert.True(
            worn.Length == 1 && worn[0] == expected,
            $"The Weapons box's {slot.ToShownText()} slot shows {DescribeWorn(worn)}, but \"{weapon.ToShownText()}\", {damage.ToShownText()} was expected.");
        return that;
    }

    /// <summary>It holds no weapon but those in <paramref name="slots"/>.</summary>
    public static AssertingThat<WeaponsBoxSnapshot> HoldsNoOtherWeapon(this AssertingThat<WeaponsBoxSnapshot> that, params WeaponSlot[] slots)
    {
        var others = that.InstanceToAssert.Weapons.Where(shown => !slots.Contains(shown.Slot)).ToArray();
        Assert.True(others.Length == 0, $"The Weapons box also shows {DescribeWorn(others)}, but no other weapon was expected.");
        return that;
    }

    private static string DescribeWorn(IReadOnlyCollection<WornWeapon> worn) =>
        worn.Count == 0
            ? "no weapon"
            : string.Join(", ", worn.Select(shown => $"\"{shown.Weapon.ToShownText()}\", {shown.Damage.ToShownText()} in {shown.Slot.ToShownText()}"));

    private static string Describe(IEnumerable<WeaponSlot> slots) => string.Join(", ", slots.Select(slot => slot.ToShownText()));
}
