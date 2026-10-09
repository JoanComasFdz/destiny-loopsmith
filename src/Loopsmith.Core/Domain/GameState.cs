using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

public sealed record ActiveStatus(StatusId Status, StackCount Stacks, Optional<Seconds> Remaining);

/// <summary>v1 enemy model: "the pack in front of you" — one tier plus the debuffs spread across it.</summary>
public sealed record TargetState(EnemyTier Tier, ImmutableArray<ActiveStatus> Debuffs);

public sealed record GroundPickup(PickupId Pickup, int Count);

/// <summary>
/// Immutable snapshot; Simulation is a pure state machine (state, event) → (state, fired). There is no ability
/// energy: abilities are always available (ADRs D21) — refunds are explained, not tracked.
/// </summary>
public sealed record GameState(
    int Step,
    Seconds Clock,
    ImmutableArray<ActiveStatus> Buffs,
    TargetState Target,
    ImmutableArray<GroundPickup> Pickups);

/// <summary>What the player chooses to do next. Abilities and weapons say how many enemies they hit or kill (ADRs D22).</summary>
[Union]
public partial record PlayerAction
{
    partial record CastAbility(OffensiveAbility Kind, HitOutcome Hit, TargetCount Targets);
    partial record UseClassAbility();
    partial record FireWeapon(WeaponSlot Slot, HitOutcome Hit, TargetCount Targets);
    partial record CollectPickups(PickupId Pickup);
    partial record Wait(Seconds Duration);
}

/// <summary>A concrete thing that happened — matched against <see cref="Trigger"/> patterns.</summary>
[Union]
public partial record GameEvent
{
    partial record AbilityCast(AbilityKind Kind);
    partial record Damaged(DamageOrigin Origin, EnemyTier Tier, ImmutableArray<StatusId> TargetHas);
    partial record Killed(DamageOrigin Origin, EnemyTier Tier, ImmutableArray<StatusId> TargetHas);
    partial record TargetsHit(DamageOrigin Origin, TargetCount Targets, HitOutcome Hit);   // once per action, after its per-enemy events
    partial record PickedUp(PickupId Pickup);
    partial record BuffGained(StatusId Status, StackCount Stacks);
    partial record StacksMaxed(StatusId Status);
}

/// <summary>An outcome as it was applied, with how far to trust it (an energy grant only explains — ADRs D21).</summary>
public sealed record AppliedOutcome(Outcome Outcome, Certainty Certainty, Optional<string> Caveat);

/// <summary>
/// One bullet of a build note: source → trigger → outcomes (reason), at cascade depth.
/// <see cref="EventIndex"/> numbers the events of a step, so bullets of the same event can be grouped.
/// <see cref="NotStackedWith"/> names the element this rule gave way to (<see cref="Rule.DoesNotStackWith"/>): it
/// matched, but applied nothing, so <see cref="Outcomes"/> is empty.
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
    Optional<string> NotStackedWith);

/// <summary>A passive whose conditions hold in the current state (shown next to the state, not as a bullet).</summary>
public sealed record ActivePassive(ElementId Source, string SourceName, Affinity Affinity, PassiveRule Passive);

/// <summary>
/// Result of one step: new state, ordered fired rules, and what the player can do now.
/// <see cref="Blocked"/> holds the reason when the action could not be performed at all (nothing to pick up, no
/// weapon in that slot) — abilities are never blocked.
/// </summary>
public sealed record Resolution(
    PlayerAction Action,
    GameState State,
    ImmutableArray<FiredRule> Fired,
    ImmutableArray<ActivePassive> ActivePassives,
    ImmutableArray<PlayerAction> NowAvailable,
    ImmutableArray<string> Notes,
    Optional<string> Blocked);
