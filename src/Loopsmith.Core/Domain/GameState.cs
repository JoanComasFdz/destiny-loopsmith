using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>
/// A buff on the player: present, and — for a status that stacks — whether the player declared it at its maximum
/// (<c>max:</c>, ADRs D3). No stack count: the engine never counts (ADRs D1).
/// </summary>
public sealed record ActiveBuff(StatusId Status, bool AtMax);

/// <summary>"The pack in front of you" (ADRs D7): one tier plus the debuffs present on it.</summary>
public sealed record TargetState(EnemyTier Tier, ImmutableArray<StatusId> Debuffs);

/// <summary>
/// Immutable snapshot of what is present — buffs on you, debuffs on the pack, pickups on the ground — and what the
/// player declared. It holds no number but the step (ADRs D1); the Simulation slice plays steps against it,
/// (state, step) → (state, fired). Abilities are always available (ADRs D5).
/// </summary>
public sealed record GameState(
    int Step,
    ImmutableArray<ActiveBuff> Buffs,
    TargetState Target,
    ImmutableArray<PickupId> Pickups);

/// <summary>What the player chooses to do next. Abilities and weapons say how many enemies they hit or kill (ADRs D4).</summary>
[Union]
public partial record PlayerAction
{
    partial record CastAbility(OffensiveAbility Kind, HitOutcome Hit, TargetCount Targets);
    partial record UseClassAbility(bool Airborne = false);   // airborne: an air move that spends the charge (Ascension)
    partial record FireWeapon(WeaponSlot Slot, HitOutcome Hit, TargetCount Targets);
    partial record CollectPickups(PickupId Pickup);
    partial record Declare(StateDeclaration Declaration);   // a state only play decides (ADRs D3)
}

/// <summary>What the player declares as a step of its own (ADRs D3): <c>max:&lt;status&gt;</c>, <c>end:&lt;status&gt;</c>.</summary>
[Union]
public partial record StateDeclaration
{
    partial record ReachMax(StatusId Status);    // a stacking buff on you is at its maximum
    partial record EndStatus(StatusId Status);   // a buff on you, or a debuff on the pack, has ended
}

/// <summary>A concrete thing that happened — matched against <see cref="Trigger"/> patterns.</summary>
[Union]
public partial record GameEvent
{
    partial record AbilityCast(AbilityKind Kind, bool Airborne = false);
    partial record Damaged(DamageOrigin Origin, EnemyTier Tier, ImmutableArray<StatusId> TargetHas);
    partial record Killed(DamageOrigin Origin, EnemyTier Tier, ImmutableArray<StatusId> TargetHas);
    partial record TargetsHit(DamageOrigin Origin, TargetCount Targets, HitOutcome Hit);   // once per action, after its per-enemy events
    partial record PickedUp(PickupId Pickup);
    partial record BuffGained(StatusId Status);   // a grant of the buff (every grant, for a status that stacks)
    partial record StacksMaxed(StatusId Status);  // only from a declared max: step
}

/// <summary>An outcome as it was applied, with how far to trust it (an energy grant only explains — ADRs D5).</summary>
public sealed record AppliedOutcome(Outcome Outcome, Certainty Certainty, Optional<string> Caveat);

/// <summary>
/// One bullet of a build note: source → trigger → outcomes (reason), at cascade depth.
/// <see cref="EventIndex"/> numbers the events of a step, so bullets of the same event can be grouped.
/// <see cref="NotStackedWith"/> names the element this rule gave way to (<see cref="Rule.DoesNotStackWith"/>): it
/// matched, but applied nothing, so <see cref="Outcomes"/> is empty. <see cref="On"/> and <see cref="When"/> are the
/// rule's own trigger and guards: what it needed (a debuffed target, a buff, a declared maximum).
/// </summary>
public sealed record FiredRule(
    ElementId Source,
    string SourceName,
    ElementKind SourceKind,
    Affinity Affinity,
    GameEvent Trigger,
    ImmutableArray<AppliedOutcome> Outcomes,
    Optional<string> Reason,
    Likelihood Likelihood,
    int Depth,
    int EventIndex,
    Optional<string> NotStackedWith,
    Trigger On,
    ImmutableArray<Condition> When);

/// <summary>A passive whose conditions hold in the current state (shown next to the state, not as a bullet).</summary>
public sealed record ActivePassive(ElementId Source, string SourceName, Affinity Affinity, PassiveRule Passive);

/// <summary>
/// Result of one step: new state, ordered fired rules, and what the player can do now.
/// <see cref="Blocked"/> holds the reason when the step could not happen at all (nothing to pick up, no weapon in
/// that slot, a declaration that doesn't hold) — abilities are never blocked.
/// </summary>
public sealed record Resolution(
    PlayerAction Action,
    GameState State,
    ImmutableArray<FiredRule> Fired,
    ImmutableArray<ActivePassive> ActivePassives,
    ImmutableArray<PlayerAction> NowAvailable,
    ImmutableArray<string> Notes,
    Optional<string> Blocked);
