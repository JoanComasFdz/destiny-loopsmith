using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Causality;

/// <summary>
/// Kernel: "this weapon" (<c>weapon:this</c>, <c>weapon:this:arc</c>) in an element a weapon holds — a weapon mod, a
/// perk, an exotic's perk or its catalyst. The rules name no weapon; the build check binds the element to the weapon
/// holding it, by its slot (one weapon per slot), so the engine and the loop graph match an ordinary
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
        || element.Passives.Any(passive => passive.Modifier is Passive.ModifyDamage modify && IsThisWeapon(modify.Against));

    /// <summary>
    /// The element as the weapon in <paramref name="slot"/>, of <paramref name="type"/>, holds it: each "this weapon" is
    /// that slot's weapon; a rule, outcome or passive for "this weapon, when it is" another type goes (a rule left with no
    /// outcome goes too).
    /// </summary>
    public static BuildElement BindToWeapon(this BuildElement element, WeaponSlot slot, DamageType type) =>
        element with
        {
            Rules = [.. element.Rules.SelectMany(rule => ToSequence(BindRule(rule, slot, type)))],
            Passives = [.. element.Passives.SelectMany(passive => ToSequence(BindPassive(passive, slot, type)))],
        };

    private static bool IsNamingThisWeapon(Rule rule) =>
        rule.On.FindVia().Match(via => IsThisWeapon(via.Value), _ => false)
        || rule.Then.Any(outcome => outcome is Outcome.ModifyDamage modify && IsThisWeapon(modify.Against));

    private static bool IsThisWeapon(DamageSource source) =>
        source is DamageSource.ThisWeapon or DamageSource.ThisWeaponOfType;

    private static Optional<Rule> BindRule(Rule rule, WeaponSlot slot, DamageType type)
    {
        var trigger = BindTrigger(rule.On, slot, type);
        var outcomes = rule.Then.SelectMany(outcome => ToSequence(BindOutcome(outcome, slot, type))).ToImmutableArray();
        return trigger.Bind(on => outcomes.IsEmpty ? Optional.None<Rule>() : Optional.Some(rule with { On = on, Then = outcomes }));
    }

    private static Optional<PassiveRule> BindPassive(PassiveRule passive, WeaponSlot slot, DamageType type) =>
        passive.Modifier is Passive.ModifyDamage modify
            ? BindSource(modify.Against, slot, type).Map(against => passive with { Modifier = modify with { Against = against } })
            : Optional.Some(passive);

    private static Optional<Outcome> BindOutcome(Outcome outcome, WeaponSlot slot, DamageType type) =>
        outcome is Outcome.ModifyDamage modify
            ? BindSource(modify.Against, slot, type).Map(Outcome (against) => modify with { Against = against })
            : Optional.Some(outcome);

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
            killFewer => BindSource(killFewer.Via, slot, type).Map(Trigger (via) => killFewer with { Via = via }));

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
                : Optional.None<DamageSource>());

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}
