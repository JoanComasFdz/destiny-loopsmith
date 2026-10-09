using System.Collections.Immutable;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.Causality;

/// <summary>
/// Kernel: what a trigger <em>means</em>. Shared by Simulation (does this event fire the rule?)
/// and LoopGraphing (can this outcome ever fire that rule?), so both read the same semantics.
/// </summary>
public static class TriggerMatching
{
    public static bool IsTriggeredBy(this Trigger trigger, GameEvent gameEvent) =>
        trigger.Match(
            abilityCast => gameEvent is GameEvent.AbilityCast cast && cast.Kind == abilityCast.Kind,
            killAny => gameEvent is GameEvent.Killed killed && killAny.Via.IsMatchedBy(killed.Origin),
            killOfTier => gameEvent is GameEvent.Killed killed
                && killOfTier.Via.IsMatchedBy(killed.Origin)
                && killed.Tier == killOfTier.Tier,
            killDebuffed => gameEvent is GameEvent.Killed killed
                && killDebuffed.Via.IsMatchedBy(killed.Origin)
                && HasEveryStatus(killed.TargetHas, killDebuffed.TargetHas),
            damage => gameEvent is GameEvent.Damaged damaged && damage.Via.IsMatchedBy(damaged.Origin),
            damageDebuffed => gameEvent is GameEvent.Damaged damaged
                && damageDebuffed.Via.IsMatchedBy(damaged.Origin)
                && HasEveryStatus(damaged.TargetHas, damageDebuffed.TargetHas),
            pickUp => gameEvent is GameEvent.PickedUp pickedUp && pickedUp.Pickup == pickUp.Pickup,
            buffGained => gameEvent is GameEvent.BuffGained gained && gained.Status == buffGained.Status,
            stacksMaxed => gameEvent is GameEvent.StacksMaxed maxed && maxed.Status == stacksMaxed.Status);

    public static bool IsMatchedBy(this DamageSource source, DamageOrigin origin) =>
        source.Match(
            _ => true,
            _ => origin is DamageOrigin.Weapon,
            weaponOfType => origin is DamageOrigin.Weapon weapon && weapon.Type == weaponOfType.Type,
            _ => origin is DamageOrigin.Ability,
            abilityOf => origin is DamageOrigin.Ability ability && ability.Kind == abilityOf.Kind,
            ofType => ReadDamageType(origin) == ofType.Type,
            keywordOf => origin is DamageOrigin.Keyword keyword && keyword.Status == keywordOf.Status,
            summonOf => origin is DamageOrigin.Summoned summoned && summoned.Summon == summonOf.Summon);

    /// <summary>The statuses a trigger needs on the target (empty when it doesn't care).</summary>
    public static ImmutableArray<StatusId> ListRequiredTargetStatuses(this Trigger trigger) =>
        trigger.Match(
            _ => ImmutableArray<StatusId>.Empty,
            _ => ImmutableArray<StatusId>.Empty,
            _ => ImmutableArray<StatusId>.Empty,
            killDebuffed => killDebuffed.TargetHas,
            _ => ImmutableArray<StatusId>.Empty,
            damageDebuffed => damageDebuffed.TargetHas,
            _ => ImmutableArray<StatusId>.Empty,
            _ => ImmutableArray<StatusId>.Empty,
            _ => ImmutableArray<StatusId>.Empty);

    public static DamageType ReadDamageType(this DamageOrigin origin) =>
        origin.Match(
            weapon => weapon.Type,
            ability => ability.Type,
            keyword => keyword.Type,
            summoned => summoned.Type);

    private static bool HasEveryStatus(ImmutableArray<StatusId> present, ImmutableArray<StatusId> required) =>
        required.All(present.Contains);
}
