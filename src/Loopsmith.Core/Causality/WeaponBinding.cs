using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Causality;

/// <summary>
/// Kernel: "this weapon" (<c>weapon:this</c>, <c>weapon:this:arc</c>) in an element a weapon holds — a weapon mod, a
/// perk, an exotic's perk or its catalyst. The rules name no weapon; the build check binds the element to the weapon
/// holding it, by its slot (one weapon per slot), so the engine matches an ordinary
/// <see cref="DamageSource.WeaponInSlot"/>. "This weapon, when it is Arc" binds only on an Arc weapon: on another one
/// the rule (or the passive, or the damage change) is not part of the build — Synergy's Ionic Trace on an Arc weapon,
/// its Orb of Power on a Kinetic one.
/// </summary>
public static class WeaponBinding
{
    /// <summary>The kinds of element a weapon holds, the only ones whose rules may say "this weapon".</summary>
    public static bool IsHeldByWeapon(this ElementKind kind) =>
        kind is ElementKind.WeaponPerk or ElementKind.WeaponMod or ElementKind.ExoticWeapon or ElementKind.Catalyst;

    /// <summary>Whether a rule, an outcome or a passive of the element says "this weapon".</summary>
    public static bool IsNamingThisWeapon(this BuildElement element) =>
        element.Rules.Any(IsNamingThisWeapon)
        || element.Passives.Any(passive => passive.Modifier switch
        {
            Passive.ModifyDamage modify => IsThisWeapon(modify.Against),
            Passive.ModifyWeaponStats { Weapons: ChangedWeapons.ThisWeapon } => true,
            _ => false,
        });

    /// <summary>
    /// The element as <paramref name="weapon"/> holds it: each "this weapon" is that slot's weapon; a rule, outcome or
    /// passive for "this weapon, when it is" another type goes (a rule left with no outcome goes too). An energy grant by
    /// archetype (Demolitionist's) becomes the weapon's amount when its archetype is known, and stays as both when it isn't.
    /// </summary>
    public static BuildElement BindToWeapon(this BuildElement element, WeaponLoadout weapon) =>
        element with
        {
            Rules = [.. element.Rules.SelectMany(rule => ToSequence(BindRule(rule, weapon)))],
            Passives = [.. element.Passives.SelectMany(passive => ToSequence(BindPassive(passive, weapon.Slot, weapon.Type)))],
        };

    /// <summary>
    /// Where a strike by this weapon comes from once bound: the build's weapon in its slot, with the strike's damage type,
    /// else the weapon's own (Radiolaria Transposer's explosion); none while unbound or for an empty slot.
    /// </summary>
    public static Optional<DamageOrigin> FindWeaponStrikeOrigin(this Build build, Outcome.WeaponStrike strike) =>
        strike.Weapon is DamageSource.WeaponInSlot inSlot
            ? build.Weapons
                .Where(weapon => weapon.Slot == inSlot.Slot)
                .Select(weapon => Optional.Some<DamageOrigin>(new DamageOrigin.Weapon(inSlot.Slot, strike.Type.Match(type => type.Value, _ => weapon.Type), weapon.Ammo, weapon.Archetype)))
                .FindFirstSome()
            : Optional.None<DamageOrigin>();

    private static bool IsNamingThisWeapon(Rule rule) =>
        rule.On.FindVia().Match(via => IsThisWeapon(via.Value), _ => false)
        || rule.Then.Any(IsNamingThisWeapon);

    /// <summary>An outcome that acts on "this weapon": a damage change for it, its strike, a reload or refill of it, an energy grant by its archetype.</summary>
    private static bool IsNamingThisWeapon(Outcome outcome) =>
        outcome switch
        {
            Outcome.ModifyDamage modify => IsThisWeapon(modify.Against),
            Outcome.WeaponStrike strike => IsThisWeapon(strike.Weapon),
            Outcome.ReloadWeapon reload => IsThisWeapon(reload.Weapon),
            Outcome.RefillWeapon refill => IsThisWeapon(refill.Weapon),
            Outcome.GrantEnergy { Amount: EnergyGrant.ByArchetype } => true,
            _ => false,
        };

    private static bool IsThisWeapon(DamageSource source) =>
        source is DamageSource.ThisWeapon or DamageSource.ThisWeaponOfType;

    private static Optional<Rule> BindRule(Rule rule, WeaponLoadout weapon)
    {
        var trigger = BindTrigger(rule.On, weapon.Slot, weapon.Type);
        var outcomes = rule.Then.SelectMany(outcome => ToSequence(BindOutcome(outcome, weapon))).ToImmutableArray();
        return trigger.Bind(on => outcomes.IsEmpty ? Optional.None<Rule>() : Optional.Some(rule with { On = on, Then = outcomes }));
    }

    private static Optional<PassiveRule> BindPassive(PassiveRule passive, WeaponSlot slot, DamageType type) =>
        passive.Modifier switch
        {
            Passive.ModifyDamage modify => BindSource(modify.Against, slot, type).Map(against => passive with { Modifier = modify with { Against = against } }),
            Passive.ModifyWeaponStats { Weapons: ChangedWeapons.ThisWeapon } weapons => Optional.Some(passive with
            {
                Modifier = weapons with { Weapons = new ChangedWeapons.InSlot(slot) },
            }),
            _ => Optional.Some(passive),
        };

    private static Optional<Outcome> BindOutcome(Outcome outcome, WeaponLoadout weapon) =>
        outcome switch
        {
            Outcome.ModifyDamage modify => BindSource(modify.Against, weapon.Slot, weapon.Type).Map(Outcome (against) => modify with { Against = against }),
            Outcome.WeaponStrike strike => BindSource(strike.Weapon, weapon.Slot, weapon.Type).Map(Outcome (bound) => strike with { Weapon = bound }),
            Outcome.ReloadWeapon reload => BindSource(reload.Weapon, weapon.Slot, weapon.Type).Map(Outcome (bound) => reload with { Weapon = bound }),
            Outcome.RefillWeapon refill => BindSource(refill.Weapon, weapon.Slot, weapon.Type).Map(Outcome (bound) => refill with { Weapon = bound }),
            Outcome.GrantEnergy { Amount: EnergyGrant.ByArchetype byArchetype } grant =>
                Optional.Some<Outcome>(grant with { Amount = BindArchetype(byArchetype, weapon.Archetype) }),
            _ => Optional.Some(outcome),
        };

    /// <summary>The weapon's amount when its archetype is known (one of the named ones, or any other); both when it isn't.</summary>
    private static EnergyGrant BindArchetype(EnergyGrant.ByArchetype grant, Optional<string> archetype) =>
        archetype.Match<EnergyGrant>(
            known => new EnergyGrant.Fraction(grant.Archetypes.Contains(known.Value) ? grant.OnArchetypes : grant.Otherwise),
            _ => grant);

    private static Optional<Trigger> BindTrigger(Trigger trigger, WeaponSlot slot, DamageType type) =>
        trigger.Match(
            abilityCast => Optional.Some<Trigger>(abilityCast),
            killAny => BindSource(killAny.Via, slot, type).Map(Trigger (via) => killAny with { Via = via }),
            killOfTier => BindSource(killOfTier.Via, slot, type).Map(Trigger (via) => killOfTier with { Via = via }),
            killDebuffed => BindSource(killDebuffed.Via, slot, type).Map(Trigger (via) => killDebuffed with { Via = via }),
            killMultiple => BindSource(killMultiple.Via, slot, type).Map(Trigger (via) => killMultiple with { Via = via }),
            damage => BindSource(damage.Via, slot, type).Map(Trigger (via) => damage with { Via = via }),
            damageDebuffed => BindSource(damageDebuffed.Via, slot, type).Map(Trigger (via) => damageDebuffed with { Via = via }),
            damageMultiple => BindSource(damageMultiple.Via, slot, type).Map(Trigger (via) => damageMultiple with { Via = via }),
            pickUp => Optional.Some<Trigger>(pickUp),
            buffGained => Optional.Some<Trigger>(buffGained),
            stacksMaxed => Optional.Some<Trigger>(stacksMaxed),
            finisher => Optional.Some<Trigger>(finisher),
            castAs => Optional.Some<Trigger>(castAs),
            killFewer => BindSource(killFewer.Via, slot, type).Map(Trigger (via) => killFewer with { Via = via }),
            criticalHealth => Optional.Some<Trigger>(criticalHealth),
            shieldBroken => BindSource(shieldBroken.Via, slot, type).Map(Trigger (via) => shieldBroken with { Via = via }),
            energyExpended => Optional.Some<Trigger>(energyExpended),
            damageFewer => BindSource(damageFewer.Via, slot, type).Map(Trigger (via) => damageFewer with { Via = via }),
            killBetween => BindSource(killBetween.Via, slot, type).Map(Trigger (via) => killBetween with { Via = via }),
            reload => BindSource(reload.Via, slot, type).Map(Trigger (via) => reload with { Via = via }),
            hitAtCriticalHealth => Optional.Some<Trigger>(hitAtCriticalHealth),
            slideOverAmmo => BindSource(slideOverAmmo.Via, slot, type).Map(Trigger (via) => slideOverAmmo with { Via = via }),
            readyWeapon => BindSource(readyWeapon.Via, slot, type).Map(Trigger (via) => readyWeapon with { Via = via }));

    /// <summary>"This weapon" is the slot's weapon; "this weapon, when it is X" is it when its type is X, else nothing.</summary>
    private static Optional<DamageSource> BindSource(DamageSource source, WeaponSlot slot, DamageType type) =>
        source.Match(
            anySource: _ => Optional.Some(source),
            anyWeapon: _ => Optional.Some(source),
            weaponOfType: _ => Optional.Some(source),
            weaponInSlot: _ => Optional.Some(source),
            anyAbility: _ => Optional.Some(source),
            abilityOf: _ => Optional.Some(source),
            ofType: _ => Optional.Some(source),
            keywordOf: _ => Optional.Some(source),
            summonOf: _ => Optional.Some(source),
            weaponOfSuperType: _ => Optional.Some(source),
            abilityUsedAs: _ => Optional.Some(source),
            thisWeapon: _ => Optional.Some<DamageSource>(new DamageSource.WeaponInSlot(slot)),
            thisWeaponOfType: ofType => ofType.Type == type
                ? Optional.Some<DamageSource>(new DamageSource.WeaponInSlot(slot))
                : Optional.None<DamageSource>(),
            weaponOfAmmo: _ => Optional.Some(source),
            weaponOfAmmoAndType: _ => Optional.Some(source),
            weaponOfArchetype: _ => Optional.Some(source));

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}
