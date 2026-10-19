using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.Causality;

/// <summary>
/// A weapon of an ammo type (docs/rule-format.md, "Damage sources"): <c>weapon:special</c> matches a Special weapon's
/// damage, <c>weapon:special:arc</c> only a Special Arc one's, and a weapon whose ammo type the manifest doesn't say
/// matches neither.
/// </summary>
public sealed class AmmoTypeMatchingTests
{
    private static readonly DamageSource Special = new DamageSource.WeaponOfAmmo(AmmoType.Special, Optional.None<DamageType>());
    private static readonly DamageSource SpecialArc = new DamageSource.WeaponOfAmmo(AmmoType.Special, Optional.Some(DamageType.Arc));

    [Theory]
    [InlineData(AmmoType.Special, DamageType.Arc, true, true)]
    [InlineData(AmmoType.Special, DamageType.Strand, true, false)]
    [InlineData(AmmoType.Primary, DamageType.Arc, false, false)]
    [InlineData(AmmoType.Heavy, DamageType.Arc, false, false)]
    public void A_weapon_of_an_ammo_type_matches_that_ammo_and_when_named_that_element(AmmoType ammo, DamageType type, bool special, bool specialArc)
    {
        var origin = new DamageOrigin.Weapon(WeaponSlot.Energy, type, Optional.Some(ammo));

        Assert.Equal(special, Special.IsMatchedBy(origin, Optional.None<DamageType>()));
        Assert.Equal(specialArc, SpecialArc.IsMatchedBy(origin, Optional.None<DamageType>()));
    }

    [Fact]
    public void A_weapon_whose_ammo_type_is_unknown_or_no_weapon_matches_no_ammo_type()
    {
        Assert.False(SpecialArc.IsMatchedBy(new DamageOrigin.Weapon(WeaponSlot.Energy, DamageType.Arc, Optional.None<AmmoType>()), Optional.None<DamageType>()));
        Assert.False(SpecialArc.IsMatchedBy(new DamageOrigin.Ability(AbilityKind.Grenade, DamageType.Arc), Optional.None<DamageType>()));
    }

    [Fact]
    public void A_weapon_of_an_ammo_type_reads_with_its_ammo_then_its_element()
    {
        Assert.Equal("Special Arc weapon", Glossary.DescribeDamageSource(SpecialArc, Optional.None<DamageType>()));
        Assert.Equal("Special weapon", Glossary.DescribeDamageSource(Special, Optional.None<DamageType>()));
    }
}
