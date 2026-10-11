using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Causality;

/// <summary>
/// Kernel: what a trigger <em>means</em>. Shared by Simulation (does this event fire the rule?)
/// and LoopGraphing (can this outcome ever fire that rule?), so both read the same semantics.
/// </summary>
public static class TriggerMatching
{
    /// <summary>
    /// <paramref name="superType"/>: the damage type of the build's Super (<c>BuildReading.ReadSuperType</c>), which a
    /// "weapon of your Super's element" source needs; none when the Super is "?".
    /// </summary>
    public static bool IsTriggeredBy(this Trigger trigger, GameEvent gameEvent, Optional<DamageType> superType) =>
        trigger.Match(
            abilityCast => gameEvent is GameEvent.AbilityCast cast && cast.Kind == abilityCast.Kind && (cast.Airborne || !abilityCast.Airborne),
            killAny => gameEvent is GameEvent.Killed killed && killAny.Via.IsMatchedBy(killed.Origin, superType),
            killOfTier => gameEvent is GameEvent.Killed killed
                && killOfTier.Via.IsMatchedBy(killed.Origin, superType)
                && killed.Tier == killOfTier.Tier,
            killDebuffed => gameEvent is GameEvent.Killed killed
                && killDebuffed.Via.IsMatchedBy(killed.Origin, superType)
                && HasEveryStatus(killed.TargetHas, killDebuffed.TargetHas),
            killMultiple => gameEvent is GameEvent.TargetsHit { Hit: HitOutcome.Kill } struck
                && killMultiple.Via.IsMatchedBy(struck.Origin, superType)
                && struck.Targets.Value >= killMultiple.AtLeast.Value,
            damage => gameEvent is GameEvent.Damaged damaged && damage.Via.IsMatchedBy(damaged.Origin, superType),
            damageDebuffed => gameEvent is GameEvent.Damaged damaged
                && damageDebuffed.Via.IsMatchedBy(damaged.Origin, superType)
                && HasEveryStatus(damaged.TargetHas, damageDebuffed.TargetHas),
            damageMultiple => gameEvent is GameEvent.TargetsHit struck   // a kill hits too
                && damageMultiple.Via.IsMatchedBy(struck.Origin, superType)
                && struck.Targets.Value >= damageMultiple.AtLeast.Value,
            pickUp => gameEvent is GameEvent.PickedUp pickedUp && pickedUp.Pickup == pickUp.Pickup,
            buffGained => gameEvent is GameEvent.BuffGained gained && gained.Status == buffGained.Status,
            stacksMaxed => gameEvent is GameEvent.StacksMaxed maxed && maxed.Status == stacksMaxed.Status,
            _ => gameEvent is GameEvent.Finisher);

    /// <summary>
    /// Whether the pattern matches the origin. A finisher has no damage type and is no weapon or ability: only "any
    /// source" matches it. A weapon of your Super's element needs the Super's type (<paramref name="superType"/>).
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
            _ => origin is DamageOrigin.Weapon weapon && superType.Match(type => type.Value == weapon.Type, _ => false));

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
            finisher: _ => ImmutableArray<StatusId>.Empty);

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
}
