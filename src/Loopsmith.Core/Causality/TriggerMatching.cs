using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Causality;

/// <summary>
/// Kernel: what a trigger <em>means</em>. Shared by StepPlaying (does this event fire the rule?) and the slices that
/// read a rule's trigger, so all read the same semantics.
/// </summary>
public static class TriggerMatching
{
    /// <summary>
    /// <paramref name="superType"/>: the damage type of the build's Super (<c>BuildReading.ReadSuperType</c>), which a
    /// "weapon of your Super's element" source needs; none when the Super is "?".
    /// </summary>
    public static bool IsTriggeredBy(this Trigger trigger, GameEvent gameEvent, Optional<DamageType> superType) =>
        trigger.Match(
            abilityCast => gameEvent is GameEvent.AbilityCast cast && cast.Kind == abilityCast.Kind,
            killAny => gameEvent is GameEvent.Killed killed && killAny.Via.IsMatchedBy(killed.Origin, superType) && IsZoneMet(killAny.Needs, killed.Zone),
            killOfTier => gameEvent is GameEvent.Killed killed
                && killOfTier.Via.IsMatchedBy(killed.Origin, superType)
                && killed.Tier == killOfTier.Tier
                && IsZoneMet(killOfTier.Needs, killed.Zone),
            killDebuffed => gameEvent is GameEvent.Killed killed
                && killDebuffed.Via.IsMatchedBy(killed.Origin, superType)
                && HasEveryStatus(killed.TargetHas, killDebuffed.TargetHas)
                && IsZoneMet(killDebuffed.Needs, killed.Zone),
            killMultiple => gameEvent is GameEvent.TargetsHit { Hit: HitOutcome.Kill } struck
                && killMultiple.Via.IsMatchedBy(struck.Origin, superType)
                && struck.Targets.Value >= killMultiple.AtLeast.Value
                && IsZoneMet(killMultiple.Needs, struck.Zone),
            damage => gameEvent is GameEvent.Damaged damaged
                && damage.Via.IsMatchedBy(damaged.Origin, superType)
                && IsSpanMet(damage.Needs, damaged.Span),
            damageDebuffed => gameEvent is GameEvent.Damaged damaged
                && damageDebuffed.Via.IsMatchedBy(damaged.Origin, superType)
                && IsSpanMet(damageDebuffed.Needs, damaged.Span)
                && HasEveryStatus(damaged.TargetHas, damageDebuffed.TargetHas),
            damageMultiple => gameEvent is GameEvent.TargetsHit struck   // a kill hits too
                && damageMultiple.Via.IsMatchedBy(struck.Origin, superType)
                && struck.Targets.Value >= damageMultiple.AtLeast.Value,
            pickUp => gameEvent is GameEvent.PickedUp pickedUp && pickedUp.Pickup == pickUp.Pickup,
            buffGained => gameEvent is GameEvent.BuffGained gained && gained.Status == buffGained.Status,
            stacksMaxed => gameEvent is GameEvent.StacksMaxed maxed && maxed.Status == stacksMaxed.Status,
            _ => gameEvent is GameEvent.Finisher,
            castAs => gameEvent is GameEvent.AbilityCast cast && cast.Kind == castAs.Kind && cast.Input == castAs.Input,
            killFewer => gameEvent is GameEvent.TargetsHit { Hit: HitOutcome.Kill } struck
                && killFewer.Via.IsMatchedBy(struck.Origin, superType)
                && struck.Targets.Value < killFewer.FewerThan.Value
                && IsZoneMet(killFewer.Needs, struck.Zone),
            _ => gameEvent is GameEvent.CriticalHealth,
            shieldBroken => gameEvent is GameEvent.ShieldBroken broken && shieldBroken.Via.IsMatchedBy(broken.Origin, superType),
            energyExpended => gameEvent is GameEvent.EnergyExpended expended && expended.Kind == energyExpended.Kind,
            damageFewer => gameEvent is GameEvent.TargetsHit struck   // a kill hits too
                && damageFewer.Via.IsMatchedBy(struck.Origin, superType)
                && struck.Targets.Value < damageFewer.FewerThan.Value,
            killBetween => gameEvent is GameEvent.TargetsHit { Hit: HitOutcome.Kill } struck
                && killBetween.Via.IsMatchedBy(struck.Origin, superType)
                && struck.Targets.Value >= killBetween.AtLeast.Value
                && struck.Targets.Value < killBetween.FewerThan.Value
                && IsZoneMet(killBetween.Needs, struck.Zone),
            reload => gameEvent is GameEvent.Reloaded reloaded && reload.Via.IsMatchedBy(reloaded.Origin, superType));

    /// <summary>
    /// Whether the pattern matches the origin. A finisher has no damage type and is no weapon or ability: only "any
    /// source" matches it. A weapon of your Super's element needs the Super's type (<paramref name="superType"/>). An
    /// ability matches however it was used (Tempest Strike's uppercut is melee damage), unless the pattern names the use.
    /// "This weapon" matches nothing until the build check binds it to the slot of the weapon holding the element
    /// (<c>WeaponBinding</c>): unbound, no weapon is known to hold it. A weapon of an ammo type needs the weapon's ammo
    /// known (its manifest item's): unknown, it matches no ammo type.
    /// </summary>
    public static bool IsMatchedBy(this DamageSource source, DamageOrigin origin, Optional<DamageType> superType) =>
        source.Match(
            _ => true,
            _ => origin is DamageOrigin.Weapon,
            weaponOfType => origin is DamageOrigin.Weapon weapon && weapon.Type == weaponOfType.Type,
            weaponInSlot => origin is DamageOrigin.Weapon weapon && weapon.Slot == weaponInSlot.Slot,
            _ => origin is DamageOrigin.Ability,
            abilityOf => origin is DamageOrigin.Ability ability && ability.Kind == abilityOf.Kind,
            ofType => FindDamageType(origin).Match(type => type.Value == ofType.Type, _ => false),
            keywordOf => origin is DamageOrigin.Keyword keyword && keyword.Status == keywordOf.Status,
            summonOf => origin is DamageOrigin.Summoned summoned && summoned.Summon == summonOf.Summon,
            _ => origin is DamageOrigin.Weapon weapon && superType.Match(type => type.Value == weapon.Type, _ => false),
            usedAs => origin is DamageOrigin.Ability ability && ability.Kind == usedAs.Kind && ability.Input == usedAs.Input,
            _ => false,
            _ => false,
            ofAmmo => origin is DamageOrigin.Weapon weapon
                && weapon.Ammo.Match(ammo => ammo.Value == ofAmmo.Ammo, _ => false)
                && ofAmmo.Type.Match(type => type.Value == weapon.Type, _ => true));

    /// <summary>Whether the trigger is a kill that needs a precision kill (<c>precision: true</c>, ADRs D36).</summary>
    public static bool NeedsPrecisionKill(this Trigger trigger) =>
        trigger.Match(
            abilityCast: _ => false,
            killAny: kill => kill.Needs == HitZone.Precision,
            killOfTier: kill => kill.Needs == HitZone.Precision,
            killDebuffed: kill => kill.Needs == HitZone.Precision,
            killMultiple: kill => kill.Needs == HitZone.Precision,
            damage: _ => false,
            damageDebuffed: _ => false,
            damageMultiple: _ => false,
            pickUp: _ => false,
            buffGained: _ => false,
            stacksMaxed: _ => false,
            finisher: _ => false,
            abilityCastAs: _ => false,
            killFewer: kill => kill.Needs == HitZone.Precision,
            criticalHealth: _ => false,
            shieldBroken: _ => false,
            energyExpended: _ => false,
            damageFewer: _ => false,
            killBetween: kill => kill.Needs == HitZone.Precision,
            reload: _ => false);

    /// <summary>The statuses a trigger needs on the target (empty when it doesn't care).</summary>
    public static ImmutableArray<StatusId> ListRequiredTargetStatuses(this Trigger trigger) =>
        trigger.Match(
            abilityCast: _ => ImmutableArray<StatusId>.Empty,
            killAny: _ => ImmutableArray<StatusId>.Empty,
            killOfTier: _ => ImmutableArray<StatusId>.Empty,
            killDebuffed: killDebuffed => killDebuffed.TargetHas,
            killMultiple: _ => ImmutableArray<StatusId>.Empty,
            damage: _ => ImmutableArray<StatusId>.Empty,
            damageDebuffed: damageDebuffed => damageDebuffed.TargetHas,
            damageMultiple: _ => ImmutableArray<StatusId>.Empty,
            pickUp: _ => ImmutableArray<StatusId>.Empty,
            buffGained: _ => ImmutableArray<StatusId>.Empty,
            stacksMaxed: _ => ImmutableArray<StatusId>.Empty,
            finisher: _ => ImmutableArray<StatusId>.Empty,
            abilityCastAs: _ => ImmutableArray<StatusId>.Empty,
            killFewer: _ => ImmutableArray<StatusId>.Empty,
            criticalHealth: _ => ImmutableArray<StatusId>.Empty,
            shieldBroken: _ => ImmutableArray<StatusId>.Empty,
            energyExpended: _ => ImmutableArray<StatusId>.Empty,
            damageFewer: _ => ImmutableArray<StatusId>.Empty,
            killBetween: _ => ImmutableArray<StatusId>.Empty,
            reload: _ => ImmutableArray<StatusId>.Empty);

    /// <summary>The damage source a hit, kill, shield or reload trigger names (its <c>via</c>); none for the other triggers.</summary>
    public static Optional<DamageSource> FindVia(this Trigger trigger) =>
        trigger.Match(
            abilityCast: _ => Optional.None<DamageSource>(),
            killAny: killAny => Optional.Some(killAny.Via),
            killOfTier: killOfTier => Optional.Some(killOfTier.Via),
            killDebuffed: killDebuffed => Optional.Some(killDebuffed.Via),
            killMultiple: killMultiple => Optional.Some(killMultiple.Via),
            damage: damage => Optional.Some(damage.Via),
            damageDebuffed: damageDebuffed => Optional.Some(damageDebuffed.Via),
            damageMultiple: damageMultiple => Optional.Some(damageMultiple.Via),
            pickUp: _ => Optional.None<DamageSource>(),
            buffGained: _ => Optional.None<DamageSource>(),
            stacksMaxed: _ => Optional.None<DamageSource>(),
            finisher: _ => Optional.None<DamageSource>(),
            abilityCastAs: _ => Optional.None<DamageSource>(),
            killFewer: killFewer => Optional.Some(killFewer.Via),
            criticalHealth: _ => Optional.None<DamageSource>(),
            shieldBroken: shieldBroken => Optional.Some(shieldBroken.Via),
            energyExpended: _ => Optional.None<DamageSource>(),
            damageFewer: damageFewer => Optional.Some(damageFewer.Via),
            killBetween: killBetween => Optional.Some(killBetween.Via),
            reload: reload => Optional.Some(reload.Via));

    /// <summary>
    /// The ability use a trigger names, when it names one: a cast of that use (<c>abilityCast: { ability: classAbility,
    /// input: air }</c>) or a hit or kill by it (<c>via: melee:slide</c>).
    /// </summary>
    public static Optional<(AbilityKind Kind, AbilityInput Input)> FindNamedInput(this Trigger trigger) =>
        trigger is Trigger.AbilityCastAs cast
            ? Optional.Some((cast.Kind, cast.Input))
            : trigger.FindVia().Bind(via => via is DamageSource.AbilityUsedAs usedAs
                ? Optional.Some((usedAs.Kind, usedAs.Input))
                : Optional.None<(AbilityKind, AbilityInput)>());

    /// <summary>The damage type of an origin; none for a finisher, which has none.</summary>
    public static Optional<DamageType> FindDamageType(this DamageOrigin origin) =>
        origin.Match(
            weapon => Optional.Some(weapon.Type),
            ability => Optional.Some(ability.Type),
            keyword => Optional.Some(keyword.Type),
            summoned => Optional.Some(summoned.Type),
            _ => Optional.None<DamageType>());

    private static bool HasEveryStatus(ImmutableArray<StatusId> present, ImmutableArray<StatusId> required) =>
        required.All(present.Contains);

    /// <summary>A source that needs sustained damage fires on a hit step's hits only; any other on every hit (ADRs D34).</summary>
    private static bool IsSpanMet(DamageSpan needs, DamageSpan span) =>
        needs == DamageSpan.Blow || span == DamageSpan.Sustained;

    /// <summary>A source that needs a precision kill fires on the kills the player says are precision ones only; any other on every kill (ADRs D36).</summary>
    private static bool IsZoneMet(HitZone needs, HitZone zone) =>
        needs == HitZone.Body || zone == HitZone.Precision;
}
