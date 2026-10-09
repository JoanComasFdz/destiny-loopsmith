using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Simulation;

/// <summary>The state after one outcome, how it was applied, and the events it caused.</summary>
public sealed record Application(GameState State, AppliedOutcome Applied, ImmutableArray<PendingEvent> Derived);

public static class OutcomeApplication
{
    public static Application ApplyOutcome(ValidatedBuild build, GameState state, Outcome outcome, int copies) =>
        outcome.Match(
            grantEnergy => GrantEnergy(build, state, outcome, grantEnergy, copies),
            convert => ConvertStacksToEnergy(build, state, outcome, convert, copies),
            applyBuff => ApplyBuff(build, state, outcome, applyBuff),
            removeBuff => RemoveBuff(state, outcome, removeBuff),
            debuffTarget => DebuffTarget(build, state, outcome, debuffTarget),
            spawn => Spawn(build, state, outcome, spawn),
            spawnSummon => SpawnSummon(build, state, outcome, spawnSummon),
            strikeTarget => StrikeTarget(build, state, outcome, strikeTarget),
            modifyDamage => AnnotateValue(state, outcome, modifyDamage.Change, copies),
            restoreHealth => AnnotateValue(state, outcome, restoreHealth.Amount, copies),
            resetCooldown => ResetCooldown(state, outcome, resetCooldown));

    private static Application GrantEnergy(ValidatedBuild build, GameState state, Outcome outcome, Outcome.GrantEnergy grant, int copies) =>
        grant.Amount.Match(
            fraction => AddEnergy(build, state, outcome, grant.To, fraction.Amount.ResolveForCopies(copies), 1),
            _ => FillGauge(state, outcome, grant.To));

    private static Application ConvertStacksToEnergy(
        ValidatedBuild build, GameState state, Outcome outcome, Outcome.ConvertStacksToEnergy convert, int copies)
    {
        var stacks = state.ReadStacks(convert.Consumed);
        var consumed = state.DropBuff(convert.Consumed);
        if (stacks == 0)
        {
            return Unchanged(state, outcome, Certainty.Known, "no stacks to consume");
        }

        var perStack = convert.PerStack.ResolveForCopies(copies);
        return AddEnergy(build, consumed, outcome, convert.To, perStack, stacks);
    }

    private static Application AddEnergy(
        ValidatedBuild build, GameState state, Outcome outcome, AbilityKind to, ResolvedValue amount, int multiplier)
    {
        var scalar = ResolveChunkScalar(build, to);
        var gauge = state.ReadGauge(to);
        var stackNote = multiplier > 1 ? $"×{multiplier} stacks" : "";
        return amount.Value.Match(
            known =>
            {
                var gained = known.Value * multiplier * scalar.Value.UnwrapOr(1m);
                var next = state.SetEnergy(to, gauge.Energy.Value + gained);
                var certainty = amount.Certainty.CombineCertainty(scalar.Certainty);
                var caveat = JoinCaveats(stackNote, scalar.Certainty == Certainty.Assumed ? "chunk scalar unknown → 1× assumed" : "");
                return new Application(next, new AppliedOutcome(outcome, certainty, caveat), []);
            },
            _ => new Application(
                state,
                new AppliedOutcome(outcome, Certainty.Unknown, JoinCaveats(stackNote, "amount unknown — not applied")),
                []));
    }

    private static Application FillGauge(GameState state, Outcome outcome, AbilityKind to)
    {
        var gauge = state.ReadGauge(to);
        var next = state.SetEnergy(to, gauge.MaxCharges);
        return new Application(next, new AppliedOutcome(outcome, Certainty.Known, Optional.None<string>()), []);
    }

    private static Application ResetCooldown(GameState state, Outcome outcome, Outcome.ResetCooldown reset) =>
        FillGauge(state, outcome, reset.Which);

    private static Application ApplyBuff(ValidatedBuild build, GameState state, Outcome outcome, Outcome.ApplyBuff apply)
    {
        var definition = FindStatus(build, apply.Status);
        var bonus = SumExtraStacks(build, state, apply.Status);
        var existing = state.ReadStacks(apply.Status);
        var wasActive = state.HasBuff(apply.Status);
        var maxStacks = definition.Bind(d => d.MaxStacks).Map(max => max.Value);
        var stacks = maxStacks.Match(
            max => Math.Min(max.Value, existing + apply.Stacks.Value + bonus.Extra),
            _ => 1);
        var duration = apply.Duration.IsSome() ? apply.Duration : definition.Bind(d => d.Duration);
        var next = state.PutBuff(new ActiveStatus(apply.Status, StackCount.From(stacks), duration));
        var gained = !wasActive || stacks > existing
            ? ImmutableArray.Create<PendingEvent>(new PendingEvent.Ready(new GameEvent.BuffGained(apply.Status, StackCount.From(stacks))))
            : [];
        var maxed = maxStacks.Match(max => max.Value > 1 && stacks == max.Value && existing < max.Value, _ => false)
            ? ImmutableArray.Create<PendingEvent>(new PendingEvent.Ready(new GameEvent.StacksMaxed(apply.Status)))
            : [];
        var caveat = JoinCaveats(
            bonus.Extra > 0 ? $"+{bonus.Extra} from {string.Join(", ", bonus.Sources)}" : "",
            gained.IsEmpty ? "already active — refreshed" : "");
        return new Application(next, new AppliedOutcome(outcome, Certainty.Known, caveat), gained.AddRange(maxed));
    }

    private static Application RemoveBuff(GameState state, Outcome outcome, Outcome.RemoveBuff remove) =>
        state.HasBuff(remove.Status)
            ? new Application(state.DropBuff(remove.Status), new AppliedOutcome(outcome, Certainty.Known, Optional.None<string>()), [])
            : Unchanged(state, outcome, Certainty.Known, "was not active");

    private static Application DebuffTarget(ValidatedBuild build, GameState state, Outcome outcome, Outcome.DebuffTarget debuff)
    {
        var duration = debuff.Duration.IsSome() ? debuff.Duration : FindStatus(build, debuff.Status).Bind(d => d.Duration);
        var next = state.PutDebuff(new ActiveStatus(debuff.Status, StackCount.From(1), duration));
        return new Application(next, new AppliedOutcome(outcome, Certainty.Known, Optional.None<string>()), []);
    }

    private static Application Spawn(ValidatedBuild build, GameState state, Outcome outcome, Outcome.Spawn spawn)
    {
        var collects = Optional.FromNullable(build.Catalog.Glossary.Pickups.GetValueOrDefault(spawn.Pickup))
            .Map(p => p.CollectsAutomatically)
            .UnwrapOr(false);
        if (collects)
        {
            var pickedUp = Enumerable.Repeat<PendingEvent>(new PendingEvent.Ready(new GameEvent.PickedUp(spawn.Pickup)), spawn.Count);
            return new Application(state, new AppliedOutcome(outcome, Certainty.Known, "tracks to you"), pickedUp.ToImmutableArray());
        }

        var next = state.AddPickups(spawn.Pickup, spawn.Count);
        return new Application(next, new AppliedOutcome(outcome, Certainty.Known, "on the ground"), []);
    }

    private static Application SpawnSummon(ValidatedBuild build, GameState state, Outcome outcome, Outcome.SpawnSummon summon)
    {
        var type = Optional.FromNullable(build.Catalog.Glossary.Summons.GetValueOrDefault(summon.Summon))
            .Map(s => s.DamageType)
            .UnwrapOr(DamageType.Kinetic);
        var hits = Enumerable.Repeat<PendingEvent>(new PendingEvent.HitTarget(new DamageOrigin.Summoned(summon.Summon, type)), summon.Count);
        return new Application(state, new AppliedOutcome(outcome, Certainty.Known, Optional.None<string>()), hits.ToImmutableArray());
    }

    private static Application StrikeTarget(ValidatedBuild build, GameState state, Outcome outcome, Outcome.StrikeTarget strike)
    {
        var type = FindStatus(build, strike.Via).Map(s => s.Affinity.ToDamageType()).UnwrapOr(DamageType.Kinetic);
        var origin = new DamageOrigin.Keyword(strike.Via, type);
        var hit = ImmutableArray.Create<PendingEvent>(new PendingEvent.HitTarget(origin));
        var events = strike.Hit == HitOutcome.Kill ? hit.Add(new PendingEvent.KillTarget(origin)) : hit;
        return new Application(state, new AppliedOutcome(outcome, Certainty.Known, Optional.None<string>()), events);
    }

    private static Application AnnotateValue(GameState state, Outcome outcome, GameValue value, int copies)
    {
        var resolved = value.ResolveForCopies(copies);
        var caveat = resolved.Certainty == Certainty.Unknown ? "value unknown" : "";
        return Unchanged(state, outcome, resolved.Certainty, caveat);
    }

    private static Application Unchanged(GameState state, Outcome outcome, Certainty certainty, string caveat) =>
        new(state, new AppliedOutcome(outcome, certainty, JoinCaveats(caveat)), []);

    private static ResolvedValue ResolveChunkScalar(ValidatedBuild build, AbilityKind kind)
    {
        var profile = build.Equipped
            .Select(e => e.Element.Ability)
            .Select(ability => ability.Match(some => some.Value.Kind == kind ? some.Value : null, _ => null))
            .FirstOrDefault(p => p is not null);
        var resolved = Optional.FromNullable(profile)
            .Map(p => p.ChunkScalar.ResolveForCopies(1))
            .UnwrapOr(new ResolvedValue(Optional.None<decimal>(), Certainty.Unknown));
        return resolved.Value.IsSome() ? resolved : new ResolvedValue(Optional.Some(1m), Certainty.Assumed);
    }

    private static Optional<StatusDefinition> FindStatus(ValidatedBuild build, StatusId status) =>
        Optional.FromNullable(build.Catalog.Glossary.Statuses.GetValueOrDefault(status));

    private sealed record StackBonus(int Extra, ImmutableArray<string> Sources);

    private static StackBonus SumExtraStacks(ValidatedBuild build, GameState state, StatusId status)
    {
        var bonuses = build.Equipped
            .SelectMany(e => e.Element.Passives.Select(p => (e.Element.Name, Passive: p)))
            .Where(x => x.Passive.When.IsSatisfiedBy(state))
            .Select(x => (x.Name, Extra: x.Passive.Effect is Passive.ExtraStacks extra && extra.Status == status ? extra.Extra.Value : 0))
            .Where(x => x.Extra > 0)
            .ToImmutableArray();
        return new StackBonus(bonuses.Sum(x => x.Extra), bonuses.Select(x => x.Name).ToImmutableArray());
    }

    private static Optional<string> JoinCaveats(params string[] caveats)
    {
        var joined = string.Join("; ", caveats.Where(c => c.Length > 0));
        return joined.Length == 0 ? Optional.None<string>() : Optional.Some(joined);
    }
}
