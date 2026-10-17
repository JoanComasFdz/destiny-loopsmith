using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>Pure reads of a <see cref="GameState"/> the slices share: what is present, and what the player declared.</summary>
public static class GameStateReading
{
    public static bool HasBuff(this GameState state, StatusId status) =>
        state.Buffs.Any(buff => buff.Status == status);

    /// <summary>The buff is at its maximum: declared (<c>max:</c>), or a counted buff's sure stacks at its cap, and nothing has consumed or ended it since.</summary>
    public static bool IsAtMax(this GameState state, StatusId status) =>
        state.Buffs.Any(buff => buff.Status == status && buff.AtMax);

    /// <summary>A counted buff's stacks (ADRs D33); none when it isn't on you or isn't counted.</summary>
    public static Optional<StackTally> ReadTally(this GameState state, StatusId status) =>
        state.Buffs
            .Where(buff => buff.Status == status)
            .Select(buff => buff.Tally)
            .FindFirstSome();

    public static bool TargetHas(this GameState state, StatusId status) =>
        state.Target.Debuffs.Contains(status);

    /// <summary>The step casts this ability (<see cref="GameState.Casting"/>).</summary>
    public static bool IsCasting(this GameState state, AbilityKind ability) =>
        state.Casting is Optional<AbilityKind>.Some casting && casting.Value == ability;

    public static bool HasPickup(this GameState state, PickupId pickup) =>
        state.Pickups.Any(ground => ground.Pickup == pickup);

    /// <summary>The pickups on the ground, in the order they landed.</summary>
    public static ImmutableArray<PickupId> ListPickups(this GameState state) =>
        [.. state.Pickups.Select(ground => ground.Pickup)];

    /// <summary>The pickup is on the ground only because chance rules dropped it (ADRs D8): what it gives is a chance.</summary>
    public static bool HasOnlyByChance(this GameState state, PickupId pickup) =>
        state.Pickups.Any(ground => ground.Pickup == pickup && ground.Likelihood == Likelihood.Chance);

    public static ImmutableArray<StatusId> ListTargetStatuses(this GameState state) =>
        state.Target.Debuffs;
}
