using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

public sealed record AbilityGauge(AbilityKind Kind, EnergyAmount Energy, int MaxCharges);

public sealed record ActiveStatus(StatusId Status, StackCount Stacks, Optional<Seconds> Remaining);

/// <summary>v1 enemy model: "the pack in front of you" — one tier plus the debuffs spread across it.</summary>
public sealed record TargetState(EnemyTier Tier, ImmutableArray<ActiveStatus> Debuffs);

public sealed record GroundPickup(PickupId Pickup, int Count);

/// <summary>Immutable snapshot; Simulation is a pure state machine (state, event) → (state, fired).</summary>
public sealed record GameState(
    int Step,
    Seconds Clock,
    ImmutableArray<AbilityGauge> Abilities,
    ImmutableArray<ActiveStatus> Buffs,
    TargetState Target,
    ImmutableArray<GroundPickup> Pickups);

/// <summary>What the player chooses to do next.</summary>
[Union]
public partial record PlayerAction
{
    partial record CastAbility(AbilityKind Kind, HitOutcome Hit);
    partial record FireWeapon(WeaponSlot Slot, HitOutcome Hit);
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
    partial record PickedUp(PickupId Pickup);
    partial record BuffGained(StatusId Status, StackCount Stacks);
    partial record StacksMaxed(StatusId Status);
}

/// <summary>An outcome as it was applied, with how far to trust it.</summary>
public sealed record AppliedOutcome(Outcome Outcome, Certainty Certainty, Optional<string> Caveat);

/// <summary>One bullet of a build note: source → trigger → outcomes (reason), at cascade depth.</summary>
public sealed record FiredRule(
    ElementId Source,
    string SourceName,
    ElementKind SourceKind,
    Affinity Affinity,
    GameEvent Trigger,
    ImmutableArray<AppliedOutcome> Outcomes,
    Optional<string> Reason,
    Likelihood Likelihood,
    int Depth);

/// <summary>Result of one step: new state, ordered fired rules, and what the player can do now.</summary>
public sealed record Resolution(
    PlayerAction Action,
    GameState State,
    ImmutableArray<FiredRule> Fired,
    ImmutableArray<PlayerAction> NowAvailable,
    ImmutableArray<string> Notes);
