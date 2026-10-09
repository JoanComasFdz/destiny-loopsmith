using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Simulation;

/// <summary>The state after one outcome, how it was applied, and the events it caused.</summary>
public sealed record Application(GameState State, AppliedOutcome Applied, ImmutableArray<PendingEvent> Derived);

public static class OutcomeApplication
{
    private static readonly ResolvedValue FullCharge = new(Optional.Some(1m), Certainty.Known);

    public static Application ApplyOutcome(ValidatedBuild build, GameState state, Outcome outcome, int copies) =>
        outcome.Match(
            grantEnergy => AnnotateRefund(state, outcome, grantEnergy.To, ResolveGrant(grantEnergy.Amount, copies), 1),
            convert => ConvertStacksToEnergy(state, outcome, convert, copies),
            applyBuff => ApplyBuff(build, state, outcome, applyBuff),
            removeBuff => RemoveBuff(state, outcome, removeBuff),
            debuffTarget => DebuffTarget(build, state, outcome, debuffTarget),
            spawn => Spawn(build, state, outcome, spawn),
            spawnSummon => SpawnSummon(build, state, outcome, spawnSummon),
            strikeTarget => StrikeTarget(build, state, outcome, strikeTarget),
            modifyDamage => AnnotateValue(state, outcome, modifyDamage.Change, copies),
            restoreHealth => AnnotateValue(state, outcome, restoreHealth.Amount, copies),
            resetCooldown => AnnotateRefund(state, outcome, resetCooldown.Which, FullCharge, 1));

    /// <summary><c>full</c> is one whole charge (100 %); a fraction is resolved for the copies equipped.</summary>
    private static ResolvedValue ResolveGrant(EnergyGrant grant, int copies) =>
        grant.Match(
            fraction => fraction.Amount.ResolveForCopies(copies),
            _ => FullCharge);

    /// <summary>The stacks are consumed (a real state change); the energy they convert into is explanation only.</summary>
    private static Application ConvertStacksToEnergy(GameState state, Outcome outcome, Outcome.ConvertStacksToEnergy convert, int copies)
    {
        var stacks = state.ReadStacks(convert.Consumed);
        var consumed = state.DropBuff(convert.Consumed);
        if (stacks == 0)
        {
            return KeepUnchanged(state, outcome, Certainty.Known, "no stacks to consume");
        }

        var perStack = convert.PerStack.ResolveForCopies(copies);
        return AnnotateRefund(consumed, outcome, convert.To, perStack, stacks);
    }

    /// <summary>
    /// Records the energy an outcome refunds without changing any gauge — ability energy isn't simulated (ADRs D21).
    /// An unknown amount stays unknown (counted, never applied as a number).
    /// </summary>
    private static Application AnnotateRefund(GameState state, Outcome outcome, AbilityKind to, ResolvedValue amount, int stacks)
    {
        var total = new ResolvedValue(amount.Value.Map(value => value * stacks), amount.Certainty);
        var caveat = JoinCaveats(stacks > 1 ? $"×{stacks} stacks" : "", amount.Value.IsSome() ? "" : "amount unknown");
        var refund = Optional.Some(new EnergyRefund(to, total));
        return new Application(state, new AppliedOutcome(outcome, amount.Certainty, caveat, refund), []);
    }

    private static Application ApplyBuff(ValidatedBuild build, GameState state, Outcome outcome, Outcome.ApplyBuff apply)
    {
        var definition = build.Catalog.Glossary.FindStatus(apply.Status);
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
        return new Application(next, ToApplied(outcome, Certainty.Known, caveat), gained.AddRange(maxed));
    }

    private static Application RemoveBuff(GameState state, Outcome outcome, Outcome.RemoveBuff remove) =>
        state.HasBuff(remove.Status)
            ? new Application(state.DropBuff(remove.Status), ToApplied(outcome, Certainty.Known, Optional.None<string>()), [])
            : KeepUnchanged(state, outcome, Certainty.Known, "was not active");

    private static Application DebuffTarget(ValidatedBuild build, GameState state, Outcome outcome, Outcome.DebuffTarget debuff)
    {
        var duration = debuff.Duration.IsSome() ? debuff.Duration : build.Catalog.Glossary.FindStatus(debuff.Status).Bind(d => d.Duration);
        var next = state.PutDebuff(new ActiveStatus(debuff.Status, StackCount.From(1), duration));
        return new Application(next, ToApplied(outcome, Certainty.Known, Optional.None<string>()), []);
    }

    private static Application Spawn(ValidatedBuild build, GameState state, Outcome outcome, Outcome.Spawn spawn)
    {
        if (build.Catalog.Glossary.IsCollectedAutomatically(spawn.Pickup))
        {
            var pickedUp = Enumerable.Repeat<PendingEvent>(new PendingEvent.Ready(new GameEvent.PickedUp(spawn.Pickup)), spawn.Count);
            return new Application(state, ToApplied(outcome, Certainty.Known, "tracks to you"), pickedUp.ToImmutableArray());
        }

        var next = state.AddPickups(spawn.Pickup, spawn.Count);
        return new Application(next, ToApplied(outcome, Certainty.Known, "on the ground"), []);
    }

    private static Application SpawnSummon(ValidatedBuild build, GameState state, Outcome outcome, Outcome.SpawnSummon summon)
    {
        var type = build.Catalog.Glossary.ResolveSummonDamageType(summon.Summon);
        var hits = Enumerable.Repeat<PendingEvent>(new PendingEvent.HitTarget(new DamageOrigin.Summoned(summon.Summon, type)), summon.Count);
        return new Application(state, ToApplied(outcome, Certainty.Known, Optional.None<string>()), hits.ToImmutableArray());
    }

    private static Application StrikeTarget(ValidatedBuild build, GameState state, Outcome outcome, Outcome.StrikeTarget strike)
    {
        var type = build.Catalog.Glossary.ResolveStrikeDamageType(strike.Via);
        var origin = new DamageOrigin.Keyword(strike.Via, type);
        var hit = ImmutableArray.Create<PendingEvent>(new PendingEvent.HitTarget(origin));
        var events = strike.Hit == HitOutcome.Kill ? hit.Add(new PendingEvent.KillTarget(origin)) : hit;
        return new Application(state, ToApplied(outcome, Certainty.Known, Optional.None<string>()), events);
    }

    private static Application AnnotateValue(GameState state, Outcome outcome, GameValue value, int copies)
    {
        var resolved = value.ResolveForCopies(copies);
        var caveat = resolved.Certainty == Certainty.Unknown ? "value unknown" : "";
        return KeepUnchanged(state, outcome, resolved.Certainty, caveat);
    }

    private static Application KeepUnchanged(GameState state, Outcome outcome, Certainty certainty, string caveat) =>
        new(state, ToApplied(outcome, certainty, JoinCaveats(caveat)), []);

    /// <summary>An outcome that refunds no energy.</summary>
    private static AppliedOutcome ToApplied(Outcome outcome, Certainty certainty, Optional<string> caveat) =>
        new(outcome, certainty, caveat, Optional.None<EnergyRefund>());

    private sealed record StackBonus(int Extra, ImmutableArray<string> Sources);

    private static StackBonus SumExtraStacks(ValidatedBuild build, GameState state, StatusId status)
    {
        var bonuses = build.Equipped
            .SelectMany(e => e.Element.Passives.Select(p => (e.Element.Name, Passive: p)))
            .Where(x => x.Passive.When.IsSatisfiedBy(state))
            .Select(x => (x.Name, Extra: x.Passive.Modifier is Passive.ExtraStacks extra && extra.Status == status ? extra.Extra.Value : 0))
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
