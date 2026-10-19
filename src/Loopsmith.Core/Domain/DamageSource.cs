using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>A <em>pattern</em> in a trigger: which damage counts ("Arc weapon", "any ability", "Bolt Charge strike").</summary>
[Union]
public partial record DamageSource
{
    partial record AnySource();
    partial record AnyWeapon();
    partial record WeaponOfType(DamageType Type);
    partial record WeaponInSlot(WeaponSlot Slot);    // the build's weapon in that slot: an exotic weapon's "this weapon"
    partial record AnyAbility();
    partial record AbilityOf(AbilityKind Kind);                // that ability however it's used (its secondary ones too)
    partial record OfType(DamageType Type);          // any weapon, ability, keyword or summon of that damage type
    partial record KeywordOf(StatusId Status);       // jolt chain, bolt-charge lightning, unravel threads…
    partial record SummonOf(SummonId Summon);
    partial record WeaponOfSuperType();              // a weapon of the build's Super's element (Harmonic mods); none known: nothing
    partial record AbilityUsedAs(AbilityKind Kind, AbilityInput Input);   // only that use of the ability: melee:slide, Tempest Strike's uppercut
    partial record ThisWeapon();                     // the weapon holding the element (a weapon mod's, a perk's): the build check binds it to that weapon's slot
    partial record ThisWeaponOfType(DamageType Type);   // the weapon holding the element, when it is of that type (Synergy's pickup by the weapon's element)
    partial record WeaponOfAmmo(AmmoType Ammo, Optional<DamageType> Type);   // a weapon of that ammo type (and of that damage type, when given): Spark of Beacons's Special Arc weapon; its ammo unknown: nothing
}

/// <summary>The <em>concrete</em> origin of damage in an event. Patterns (<see cref="DamageSource"/>) match origins.</summary>
[Union]
public partial record DamageOrigin
{
    partial record Weapon(WeaponSlot Slot, DamageType Type, Optional<AmmoType> Ammo);   // Ammo: none when the manifest doesn't say
    partial record Ability(AbilityKind Kind, DamageType Type, AbilityInput Input = AbilityInput.Press);
    partial record Keyword(StatusId Status, DamageType Type);
    partial record Summoned(SummonId Summon, DamageType Type);
    partial record Finisher();                       // no damage type: only "any source" matches it
}
