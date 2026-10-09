using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Simulation;

/// <summary>Pure reads and immutable updates over <see cref="GameState"/>.</summary>
public static class StateReading
{
    public static AbilityGauge ReadGauge(this GameState state, AbilityKind kind) =>
        kind switch
        {
            AbilityKind.Grenade => state.Abilities.Grenade,
            AbilityKind.Melee => state.Abilities.Melee,
            AbilityKind.ClassAbility => state.Abilities.ClassAbility,
            _ => state.Abilities.Super,
        };

    public static GameState ReplaceGauge(this GameState state, AbilityGauge gauge) =>
        state with
        {
            Abilities = gauge.Kind switch
            {
                AbilityKind.Grenade => state.Abilities with { Grenade = gauge },
                AbilityKind.Melee => state.Abilities with { Melee = gauge },
                AbilityKind.ClassAbility => state.Abilities with { ClassAbility = gauge },
                _ => state.Abilities with { Super = gauge },
            },
        };

    /// <summary>The cost model of the equipped ability of that kind (super, grenade, melee, class ability).</summary>
    public static Optional<AbilityProfile> FindAbilityProfile(this ValidatedBuild build, AbilityKind kind) =>
        build.Equipped
            .Select(e => e.Element.Ability.Bind(p => p.Kind == kind ? Optional.Some(p) : Optional.None<AbilityProfile>()))
            .FindFirstSome();

    public static GameState SetEnergy(this GameState state, AbilityKind kind, decimal charges)
    {
        var gauge = state.ReadGauge(kind);
        var clamped = Math.Clamp(charges, 0m, gauge.MaxCharges);
        return state.ReplaceGauge(gauge with { Energy = EnergyAmount.From(clamped) });
    }

    public static Optional<ActiveStatus> FindBuff(this GameState state, StatusId status) =>
        Optional.FromNullable(state.Buffs.FirstOrDefault(buff => buff.Status == status));

    public static int ReadStacks(this GameState state, StatusId status) =>
        state.FindBuff(status).Map(buff => buff.Stacks.Value).UnwrapOr(0);

    public static bool HasBuff(this GameState state, StatusId status) =>
        state.Buffs.Any(buff => buff.Status == status);

    public static bool TargetHas(this GameState state, StatusId status) =>
        state.Target.Debuffs.Any(debuff => debuff.Status == status);

    public static GameState PutBuff(this GameState state, ActiveStatus buff) =>
        state with { Buffs = ReplaceOrAppend(state.Buffs, buff) };

    public static GameState DropBuff(this GameState state, StatusId status) =>
        state with { Buffs = state.Buffs.RemoveAll(b => b.Status == status) };

    public static GameState PutDebuff(this GameState state, ActiveStatus debuff) =>
        state with { Target = state.Target with { Debuffs = ReplaceOrAppend(state.Target.Debuffs, debuff) } };

    /// <summary>Refreshing a status keeps its place, so traces list statuses in the order they were gained.</summary>
    private static ImmutableArray<ActiveStatus> ReplaceOrAppend(ImmutableArray<ActiveStatus> statuses, ActiveStatus status)
    {
        var index = statuses.Select(s => s.Status).ToImmutableArray().IndexOf(status.Status);
        return index < 0 ? statuses.Add(status) : statuses.SetItem(index, status);
    }

    public static int CountPickups(this GameState state, PickupId pickup) =>
        state.Pickups.Where(p => p.Pickup == pickup).Sum(p => p.Count);

    public static GameState AddPickups(this GameState state, PickupId pickup, int count) =>
        state with
        {
            Pickups = state.Pickups.RemoveAll(p => p.Pickup == pickup)
                .Add(new GroundPickup(pickup, state.CountPickups(pickup) + count)),
        };

    public static GameState ClearPickups(this GameState state, PickupId pickup) =>
        state with { Pickups = state.Pickups.RemoveAll(p => p.Pickup == pickup) };

    public static ImmutableArray<StatusId> ListTargetStatuses(this GameState state) =>
        state.Target.Debuffs.Select(d => d.Status).ToImmutableArray();

    public static bool IsSatisfiedBy(this ImmutableArray<Condition> conditions, GameState state) =>
        conditions.All(condition => condition.Match(
            hasBuff => state.HasBuff(hasBuff.Status),
            lacksBuff => !state.HasBuff(lacksBuff.Status),
            targetHas => state.TargetHas(targetHas.Status)));
}
