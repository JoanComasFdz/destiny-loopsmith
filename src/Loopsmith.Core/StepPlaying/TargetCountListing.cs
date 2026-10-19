using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.StepPlaying;

/// <summary>
/// Pure: the enemy counts a step's ability or weapon offers (ADRs D4). It plays the step from the state with one enemy
/// more each time until one more fills nothing the build can still gain — a stacking buff below its cap (Bolt Charge
/// x10, Armor Charge at the build's cap), an ability's energy below a full charge (Ionic Traces refilling the grenade) —
/// and names what each count is for: the equipped rules that need that many from that ability or weapon in one action
/// (Arc Siphon's two kills, Synergy's twelve) and what fills up there. The sums only bound the choices: they are never
/// game state and never decide what a step fires (ADRs D1, D5, D33).
/// </summary>
public static class TargetCountListing
{
    /// <summary>
    /// 1 up to the last count that still fills something, every larger count an equipped rule needs from this action's
    /// ability or weapon (Synergy's 12), and the action's own count (a step a loop file aims at more).
    /// </summary>
    public static ImmutableArray<TargetCountChoice> ListTargetCountChoices(ValidatedBuild build, GameState state, PlayerAction action)
    {
        var named = ListNamedCounts(build, action);
        var fills = ListFills(build, state, action, named.Select(count => count.AtLeast.Value).DefaultIfEmpty(1).Max());
        var counts = Enumerable.Range(1, fills.Length)
            .Concat(named.Select(count => count.AtLeast.Value))
            .Concat(ReadOwnCount(action))
            .Distinct()
            .Order();
        return
        [
            .. counts.Select(value => new TargetCountChoice(
                TargetCount.From(value),
                [.. named.Where(count => count.AtLeast.Value == value).Select(count => new CountNeed(count.Name, count.Within)).Distinct()],
                value <= fills.Length ? fills[value - 1] : [])),
        ];
    }

    /// <summary>
    /// What an ability or weapon action hits with: the ability (of the subclass's element, by its use) or the weapon in
    /// that slot; none for any other step, or a slot with no weapon.
    /// </summary>
    public static Optional<DamageOrigin> FindActionOrigin(ValidatedBuild build, PlayerAction action) =>
        action.Match(
            cast => Optional.Some<DamageOrigin>(new DamageOrigin.Ability(cast.Kind.ToAbilityKind(), build.Build.Subclass.ToDamageType(), cast.Input)),
            _ => Optional.None<DamageOrigin>(),
            fire => build.Build.Weapons
                .Where(weapon => weapon.Slot == fire.Slot)
                .Select(weapon => Optional.Some<DamageOrigin>(new DamageOrigin.Weapon(fire.Slot, weapon.Type, weapon.Ammo)))
                .FindFirstSome(),
            _ => Optional.None<DamageOrigin>(),
            _ => Optional.None<DamageOrigin>(),
            _ => Optional.None<DamageOrigin>(),
            _ => Optional.None<DamageOrigin>());

    private sealed record NamedCount(string Name, DamageSource Via, TargetCount AtLeast, Optional<Seconds> Within);

    /// <summary>The counts the equipped rules need from this action's ability or weapon in one action.</summary>
    private static ImmutableArray<NamedCount> ListNamedCounts(ValidatedBuild build, PlayerAction action)
    {
        var origin = FindActionOrigin(build, action);
        var superType = build.ReadSuperType();
        return
        [
            .. build.Equipped
                .SelectMany(equipped => equipped.Element.Rules.SelectMany(rule => ReadNamedCount(equipped.Element.Name, rule.On)))
                .Where(count => origin.Match(found => count.Via.IsMatchedBy(found.Value, superType), _ => false)),
        ];
    }

    /// <summary>A kill or hit of at least N in one action names N; "fewer than N" names nothing (the counts below reach it).</summary>
    private static IEnumerable<NamedCount> ReadNamedCount(string name, Trigger trigger) =>
        trigger switch
        {
            Trigger.KillMultiple kill => [new NamedCount(name, kill.Via, kill.AtLeast, kill.Within)],
            Trigger.DamageMultiple damage => [new NamedCount(name, damage.Via, damage.AtLeast, damage.Within)],
            Trigger.KillBetween kill => [new NamedCount(name, kill.Via, kill.AtLeast, Optional.None<Seconds>())],
            _ => [],
        };

    private static IEnumerable<int> ReadOwnCount(PlayerAction action) =>
        action switch
        {
            PlayerAction.CastAbility cast => [cast.Targets.Value],
            PlayerAction.FireWeapon fire => [fire.Targets.Value],
            _ => [],
        };

    /// <summary>What one step's enemies fill, each capped: the stacks of each buff that stacks, the energy of each ability.</summary>
    private sealed record Filling(ImmutableDictionary<StatusId, int> Stacks, ImmutableDictionary<AbilityKind, decimal> Energy);

    /// <summary>
    /// For 1, 2, 3… enemies, what reaches its cap at that count and wasn't there before (a counted buff the state already
    /// holds at its cap fills nothing); the list ends at the last count that still fills something (past
    /// <paramref name="named"/>, the largest count a rule needs, the first count that fills nothing more ends it).
    /// </summary>
    private static ImmutableArray<ImmutableArray<CountFill>> ListFills(ValidatedBuild build, GameState state, PlayerAction action, int named)
    {
        var fills = ImmutableArray.CreateBuilder<ImmutableArray<CountFill>>();
        var held = new Filling(ReadStacks(build, state, []), ImmutableDictionary<AbilityKind, decimal>.Empty);
        var previous = ReadFilling(build, state, action, 1);
        fills.Add(ListReached(build, held, previous));
        var last = 1;
        for (var count = 2; count <= TargetCount.Maximum; count++)
        {
            var filling = ReadFilling(build, state, action, count);
            var grows = IsGrowing(previous, filling);
            if (!grows && count > named)
            {
                break;
            }

            fills.Add(ListReached(build, previous, filling));
            last = grows ? count : last;
            previous = filling;
        }

        return fills.ToImmutable()[..last];
    }

    /// <summary>The step played against <paramref name="count"/> enemies, and what it filled, each to its cap.</summary>
    private static Filling ReadFilling(ValidatedBuild build, GameState state, PlayerAction action, int count)
    {
        var resolution = ActionResolution.ResolveAction(build, state, AimAt(action, TargetCount.From(count)));
        var applied = resolution.Fired
            .Where(fired => !fired.NotStackedWith.IsSome())
            .SelectMany(fired => fired.Outcomes.Select(outcome => (fired.Source, Applied: outcome)))
            .ToImmutableArray();
        return new Filling(ReadStacks(build, resolution.State, applied), ReadEnergy(build, applied));
    }

    private static PlayerAction AimAt(PlayerAction action, TargetCount count) =>
        action switch
        {
            PlayerAction.CastAbility cast => cast with { Targets = count },
            PlayerAction.FireWeapon fire => fire with { Targets = count },
            _ => action,
        };

    /// <summary>
    /// The stacks the step gave each buff that stacks and has a known cap, capped: a counted buff's (ADRs D33), what it holds
    /// after the step; any other's, its grants and the extra stacks the passives give then (Spark of Frequency).
    /// </summary>
    private static ImmutableDictionary<StatusId, int> ReadStacks(ValidatedBuild build, GameState after, ImmutableArray<(ElementId Source, AppliedOutcome Applied)> applied)
    {
        var glossary = build.Catalog.Glossary;
        var granted = applied
            .SelectMany(x => x.Applied.Outcome is Outcome.ApplyBuff apply
                ? new[] { (Apply: apply, Stacks: apply.Stacks.ReadStacks(build.CountCopies(x.Source)).Value) }
                : [])
            .Where(x => glossary.IsStacking(x.Apply.Status) && !glossary.IsCounted(x.Apply.Status))
            .GroupBy(x => x.Apply.Status)
            .Select(group => (Status: group.Key, Stacks: group.Sum(x => x.Stacks + OutcomeApplication.ListExtraStacks(build, after, x.Apply.Status).Sum(extra => extra.Extra))));
        var counted = after.Buffs
            .SelectMany(buff => buff.Tally.Match(tally => new[] { (buff.Status, Stacks: tally.Value.Sure.Value + tally.Value.Chance.Value) }, _ => []));
        return granted.Concat(counted)
            .SelectMany(gain => ReadKnownCap(build, gain.Status).Match(cap => new[] { (gain.Status, Stacks: Math.Min(gain.Stacks, cap.Value)) }, _ => []))
            .ToImmutableDictionary(gain => gain.Status, gain => gain.Stacks);
    }

    private static Optional<int> ReadKnownCap(ValidatedBuild build, StatusId status) =>
        build.ReadStackCap(status).Bind(cap => cap is GameValue.Known known ? Optional.Some((int)known.Value) : Optional.None<int>());

    /// <summary>
    /// The energy the step gave each ability, as a share of one charge and capped at a full one: its grants of a known
    /// amount for the copies equipped, a refill, the energy a Kickstart's spend gives (with its chance stacks).
    /// </summary>
    private static ImmutableDictionary<AbilityKind, decimal> ReadEnergy(ValidatedBuild build, ImmutableArray<(ElementId Source, AppliedOutcome Applied)> applied) =>
        applied
            .SelectMany(x => ReadGrant(x.Applied, build.CountCopies(x.Source)))
            .GroupBy(grant => grant.Ability)
            .ToImmutableDictionary(group => group.Key, group => Math.Min(group.Sum(grant => grant.Amount), 1m));

    private static IEnumerable<(AbilityKind Ability, decimal Amount)> ReadGrant(AppliedOutcome applied, CopyCount copies) =>
        applied.Outcome switch
        {
            Outcome.GrantEnergy { Amount: EnergyGrant.Full } grant => [(grant.To, 1m)],
            Outcome.GrantEnergy { Amount: EnergyGrant.Fraction fraction } grant => ReadKnown(fraction.Amount.NarrowToCopies(copies)).Select(amount => (grant.To, amount)),
            Outcome.ResetCooldown reset => [(reset.Which, 1m)],
            Outcome.ConvertStacksToEnergy convert => applied.Spend
                .Bind(spend => spend.WithChance.IsSome() ? spend.WithChance : spend.AtSure)
                .Match(value => ReadKnown(value.Value), _ => [])
                .Select(amount => (convert.To, amount)),
            _ => [],
        };

    private static IEnumerable<decimal> ReadKnown(GameValue value) =>
        value switch
        {
            GameValue.Known known => [known.Value],
            GameValue.Approximate approximate => [approximate.Value],
            _ => [],
        };

    /// <summary>One more enemy filled something more: a buff's stacks or an ability's energy, below their caps.</summary>
    private static bool IsGrowing(Filling before, Filling after) =>
        after.Stacks.Any(gain => gain.Value > before.Stacks.GetValueOrDefault(gain.Key))
        || after.Energy.Any(gain => gain.Value > before.Energy.GetValueOrDefault(gain.Key));

    /// <summary>What reached its cap at this count and hadn't at the count before: a buff at the build's cap, an ability full.</summary>
    private static ImmutableArray<CountFill> ListReached(ValidatedBuild build, Filling before, Filling after) =>
    [
        .. after.Stacks
            .Where(gain => ReadKnownCap(build, gain.Key).Match(cap => gain.Value >= cap.Value, _ => false))
            .Where(gain => before.Stacks.GetValueOrDefault(gain.Key) < gain.Value)
            .OrderBy(gain => gain.Key.Value, StringComparer.Ordinal)
            .Select(gain => (CountFill)new CountFill.StatusAtCap(gain.Key, gain.Value)),
        .. after.Energy
            .Where(gain => gain.Value >= 1m && before.Energy.GetValueOrDefault(gain.Key) < 1m)
            .OrderBy(gain => gain.Key)
            .Select(gain => (CountFill)new CountFill.AbilityFull(gain.Key)),
    ];
}
