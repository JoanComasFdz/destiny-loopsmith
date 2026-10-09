using Dunet;

namespace Loopsmith.Core.Domain;

/// <summary>A <em>pattern</em> in a trigger: which damage counts ("Arc weapon", "any ability", "Bolt Charge strike").</summary>
[Union]
public partial record DamageSource
{
    partial record AnySource();
    partial record AnyWeapon();
    partial record WeaponOfType(DamageType Type);
    partial record AnyAbility();
    partial record AbilityOf(AbilityKind Kind);
    partial record OfType(DamageType Type);          // any weapon, ability, keyword or summon of that damage type
    partial record KeywordOf(StatusId Status);       // jolt chain, bolt-charge lightning, unravel threads…
    partial record SummonOf(SummonId Summon);
}

/// <summary>The <em>concrete</em> origin of damage in an event. Patterns (<see cref="DamageSource"/>) match origins.</summary>
[Union]
public partial record DamageOrigin
{
    partial record Weapon(WeaponSlot Slot, DamageType Type);
    partial record Ability(AbilityKind Kind, DamageType Type);
    partial record Keyword(StatusId Status, DamageType Type);
    partial record Summoned(SummonId Summon, DamageType Type);
}
