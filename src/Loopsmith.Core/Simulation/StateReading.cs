using System.Collections.Immutable;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.Simulation;

/// <summary>Pure reads and immutable updates over <see cref="GameState"/>: what is present, and what the player declared.</summary>
public static class StateReading
{
    public static bool HasBuff(this GameState state, StatusId status) =>
        state.Buffs.Any(buff => buff.Status == status);

    /// <summary>The player declared this buff at its maximum (<c>max:</c>), and nothing has consumed or ended it since.</summary>
    public static bool IsAtMax(this GameState state, StatusId status) =>
        state.Buffs.Any(buff => buff.Status == status && buff.AtMax);

    public static bool TargetHas(this GameState state, StatusId status) =>
        state.Target.Debuffs.Contains(status);

    public static bool HasPickup(this GameState state, PickupId pickup) =>
        state.Pickups.Contains(pickup);

    /// <summary>Makes the buff present; one already present keeps its place (and its declaration).</summary>
    public static GameState PutBuff(this GameState state, StatusId status) =>
        state.HasBuff(status) ? state : state with { Buffs = state.Buffs.Add(new ActiveBuff(status, AtMax: false)) };

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

    public static GameState PutPickup(this GameState state, PickupId pickup) =>
        state.HasPickup(pickup) ? state : state with { Pickups = state.Pickups.Add(pickup) };

    public static GameState DropPickup(this GameState state, PickupId pickup) =>
        state with { Pickups = state.Pickups.Remove(pickup) };

    public static ImmutableArray<StatusId> ListTargetStatuses(this GameState state) =>
        state.Target.Debuffs;

    public static bool IsSatisfiedBy(this ImmutableArray<Condition> conditions, GameState state) =>
        conditions.All(condition => condition.Match(
            hasBuff => state.HasBuff(hasBuff.Status),
            lacksBuff => !state.HasBuff(lacksBuff.Status),
            targetHas => state.TargetHas(targetHas.Status),
            atMax => state.IsAtMax(atMax.Status)));
}
