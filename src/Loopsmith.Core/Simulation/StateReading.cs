using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Simulation;

/// <summary>
/// Whether a rule's guards hold in the state: not at all, only by a counted buff's chance stacks (the rule fires as a
/// chance, ADRs D33), or for sure.
/// </summary>
public enum GuardResult { Fails, ByChance, Holds }

/// <summary>Pure reads and immutable updates over <see cref="GameState"/>: what is present, and what the player declared.</summary>
public static class StateReading
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

    /// <summary>Makes the buff present; one already present keeps its place (and its declaration).</summary>
    public static GameState PutBuff(this GameState state, StatusId status) =>
        state.HasBuff(status) ? state : state with { Buffs = state.Buffs.Add(new ActiveBuff(status, AtMax: false, Optional.None<StackTally>())) };

    /// <summary>A counted buff with these stacks (ADRs D33): present, in its place, at its maximum or not.</summary>
    public static GameState PutTally(this GameState state, StatusId status, StackTally tally, bool atMax) =>
        state.HasBuff(status)
            ? state with { Buffs = [.. state.Buffs.Select(buff => buff.Status == status ? buff with { AtMax = atMax, Tally = Optional.Some(tally) } : buff)] }
            : state with { Buffs = state.Buffs.Add(new ActiveBuff(status, atMax, Optional.Some(tally))) };

    public static GameState DeclareAtMax(this GameState state, StatusId status) =>
        state with { Buffs = [.. state.Buffs.Select(buff => buff.Status == status ? buff with { AtMax = true } : buff)] };

    public static GameState DropBuff(this GameState state, StatusId status) =>
        state with { Buffs = state.Buffs.RemoveAll(buff => buff.Status == status) };

    public static GameState PutDebuff(this GameState state, StatusId status) =>
        state.TargetHas(status) ? state : state with { Target = state.Target with { Debuffs = state.Target.Debuffs.Add(status) } };

    /// <summary>A new pack: the same kind of enemies, none of the old pack's debuffs (ADRs D7).</summary>
    public static GameState ReplacePack(this GameState state) =>
        state with { Target = state.Target with { Debuffs = [] } };

    public static GameState DropDebuff(this GameState state, StatusId status) =>
        state with { Target = state.Target with { Debuffs = state.Target.Debuffs.Remove(status) } };

    /// <summary>
    /// Puts the pickup on the ground. Dropped by a chance rule, it is there only by chance until a rule that always fires
    /// drops one too (ADRs D8).
    /// </summary>
    public static GameState PutPickup(this GameState state, PickupId pickup, Likelihood likelihood) =>
        (state.HasPickup(pickup), likelihood) switch
        {
            (false, _) => state with { Pickups = state.Pickups.Add(new GroundPickup(pickup, likelihood)) },
            (true, Likelihood.Always) => state with
            {
                Pickups = [.. state.Pickups.Select(ground => ground.Pickup == pickup ? ground with { Likelihood = Likelihood.Always } : ground)],
            },
            _ => state,
        };

    public static GameState DropPickup(this GameState state, PickupId pickup) =>
        state with { Pickups = state.Pickups.RemoveAll(ground => ground.Pickup == pickup) };

    public static ImmutableArray<StatusId> ListTargetStatuses(this GameState state) =>
        state.Target.Debuffs;

    /// <summary>
    /// The state the next pass of a loop starts in: its steps number from #1 again, and a counted buff's changes become one
    /// "carried over" change with the stacks the pass ended with.
    /// </summary>
    public static GameState StartNextPass(this GameState state) =>
        state with
        {
            Step = 0,
            Buffs = [.. state.Buffs.Select(buff => buff with { Tally = buff.Tally.Map(CarryTallyOver) })],
        };

    private static StackTally CarryTallyOver(StackTally tally) =>
        tally with
        {
            Changes =
            [
                new StackChange(0, new StackChangeOrigin.PassBefore(), new StackMove.CarriedOver(), tally.Sure, tally.Chance),
            ],
        };

    public static bool IsSatisfiedBy(this ImmutableArray<Condition> conditions, GameState state) =>
        conditions.ReadGuard(state) != GuardResult.Fails;

    /// <summary>
    /// Whether every guard holds: for sure, only by chance (a counted buff they read holds the stacks only with its chance
    /// ones, ADRs D33), or not at all.
    /// </summary>
    public static GuardResult ReadGuard(this ImmutableArray<Condition> conditions, GameState state) =>
        conditions.Select(condition => condition.ReadGuard(state)).DefaultIfEmpty(GuardResult.Holds).Min();

    private static GuardResult ReadGuard(this Condition condition, GameState state) =>
        condition.Match(
            hasBuff => ReadHolding(state, hasBuff.Status, StackCount.From(1)),
            lacksBuff => ReadLacking(state, lacksBuff.Status),
            targetHas => ToGuard(state.TargetHas(targetHas.Status)),
            atMax => ToGuard(state.IsAtMax(atMax.Status)),
            during => ToGuard(state.IsCasting(during.Ability)),
            belowMax => ToGuard(state.HasBuff(belowMax.Status) && !state.IsAtMax(belowMax.Status)),
            holds => ReadHolding(state, holds.Status, holds.AtLeast));

    /// <summary>The buff is on you with at least this many stacks: for sure, or only with a counted buff's chance stacks.</summary>
    private static GuardResult ReadHolding(GameState state, StatusId status, StackCount atLeast) =>
        state.HasBuff(status)
            ? state.ReadTally(status).Match(
                tally => (tally.Value.Sure.Value >= atLeast.Value, tally.Value.Sure.Value + tally.Value.Chance.Value >= atLeast.Value) switch
                {
                    (true, _) => GuardResult.Holds,
                    (false, true) => GuardResult.ByChance,
                    _ => GuardResult.Fails,
                },
                _ => GuardResult.Holds)
            : GuardResult.Fails;

    /// <summary>The buff isn't on you: for sure, or — a counted buff with only chance stacks — maybe.</summary>
    private static GuardResult ReadLacking(GameState state, StatusId status) =>
        state.HasBuff(status)
            ? state.ReadTally(status).Match(
                tally => tally.Value.Sure.Value == 0 ? GuardResult.ByChance : GuardResult.Fails,
                _ => GuardResult.Fails)
            : GuardResult.Holds;

    private static GuardResult ToGuard(bool holds) =>
        holds ? GuardResult.Holds : GuardResult.Fails;
}
