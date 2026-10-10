using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Simulation;

/// <summary>The state after one outcome, how it was applied, and the events it caused.</summary>
public sealed record Application(GameState State, AppliedOutcome Applied, ImmutableArray<PendingEvent> Derived);

public static class OutcomeApplication
{
    /// <summary>
    /// What an outcome changes in the state (a buff, a debuff or a pickup becomes present or goes) and the events it sets
    /// off. Numbers are facts shown with their certainty, never added up (ADRs D1).
    /// </summary>
    public static Application ApplyOutcome(ValidatedBuild build, GameState state, Outcome outcome) =>
        outcome.Match(
            grantEnergy => AnnotateFact(state, outcome, ReadGrantCertainty(grantEnergy.Amount), "amount unknown"),
            convert => ConvertStacksToEnergy(state, outcome, convert),
            applyBuff => ApplyBuff(build, state, outcome, applyBuff),
            removeBuff => RemoveBuff(state, outcome, removeBuff),
            debuffTarget => new Application(state.PutDebuff(debuffTarget.Status), ToApplied(outcome, Certainty.Known, Optional.None<string>()), []),
            spawn => Spawn(build, state, outcome, spawn),
            spawnSummon => SpawnSummon(build, state, outcome, spawnSummon),
            strikeTarget => StrikeTarget(build, state, outcome, strikeTarget),
            modifyDamage => AnnotateFact(state, outcome, modifyDamage.Change.ReadCertainty(), "value unknown"),
            restoreHealth => AnnotateFact(state, outcome, restoreHealth.Amount.ReadCertainty(), "value unknown"),
            resetCooldown => KeepUnchanged(state, outcome, ""));

    /// <summary><c>full</c> is one whole charge (known); a fraction is as certain as its value.</summary>
    private static Certainty ReadGrantCertainty(EnergyGrant grant) =>
        grant.Match(
            fraction => fraction.Amount.ReadCertainty(),
            _ => Certainty.Known);

    /// <summary>The buff is consumed when it is present (a real state change); the energy it converts into is a fact.</summary>
    private static Application ConvertStacksToEnergy(GameState state, Outcome outcome, Outcome.ConvertStacksToEnergy convert) =>
        state.HasBuff(convert.Consumed)
            ? AnnotateFact(state.DropBuff(convert.Consumed), outcome, convert.PerStack.ReadCertainty(), "amount unknown")
            : KeepUnchanged(state, outcome, "nothing to consume");

    /// <summary>
    /// Makes the buff present. A grant of a status that stacks always raises <see cref="GameEvent.BuffGained"/> and keeps
    /// a declared maximum (the cap is a fact); a status that doesn't stack raises it only when it wasn't active. Extra
    /// stacks from passives (Spark of Frequency) are named in the caveat, never added (ADRs D1).
    /// </summary>
    private static Application ApplyBuff(ValidatedBuild build, GameState state, Outcome outcome, Outcome.ApplyBuff apply)
    {
        var stacking = build.Catalog.Glossary.IsStacking(apply.Status);
        var wasActive = state.HasBuff(apply.Status);
        var next = state.PutBuff(apply.Status);
        var gained = stacking || !wasActive
            ? ImmutableArray.Create<PendingEvent>(new PendingEvent.Ready(new GameEvent.BuffGained(apply.Status)))
            : [];
        var caveat = JoinCaveats(DescribeExtraStacks(build, state, apply.Status), gained.IsEmpty ? "already active" : "");
        return new Application(next, ToApplied(outcome, Certainty.Known, caveat), gained);
    }

    private static Application RemoveBuff(GameState state, Outcome outcome, Outcome.RemoveBuff remove) =>
        state.HasBuff(remove.Status)
            ? new Application(state.DropBuff(remove.Status), ToApplied(outcome, Certainty.Known, Optional.None<string>()), [])
            : KeepUnchanged(state, outcome, "was not active");

    private static Application Spawn(ValidatedBuild build, GameState state, Outcome outcome, Outcome.Spawn spawn)
    {
        if (build.Catalog.Glossary.IsCollectedAutomatically(spawn.Pickup))
        {
            var pickedUp = Enumerable.Repeat<PendingEvent>(new PendingEvent.Ready(new GameEvent.PickedUp(spawn.Pickup)), spawn.Count);
            return new Application(state, ToApplied(outcome, Certainty.Known, "tracks to you"), pickedUp.ToImmutableArray());
        }

        return new Application(state.PutPickup(spawn.Pickup), ToApplied(outcome, Certainty.Known, "on the ground"), []);
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

    /// <summary>A fact shown with its certainty; <paramref name="unknownCaveat"/> marks it when the value is unknown.</summary>
    private static Application AnnotateFact(GameState state, Outcome outcome, Certainty certainty, string unknownCaveat) =>
        new(state, ToApplied(outcome, certainty, JoinCaveats(certainty == Certainty.Unknown ? unknownCaveat : "")), []);

    /// <summary>An outcome that changes nothing, with the reason when there is one ("was not active").</summary>
    private static Application KeepUnchanged(GameState state, Outcome outcome, string caveat) =>
        new(state, ToApplied(outcome, Certainty.Known, JoinCaveats(caveat)), []);

    private static AppliedOutcome ToApplied(Outcome outcome, Certainty certainty, Optional<string> caveat) =>
        new(outcome, certainty, caveat);

    /// <summary>"+1 from Spark of Frequency": the passives whose extra stacks apply to this grant now.</summary>
    private static string DescribeExtraStacks(ValidatedBuild build, GameState state, StatusId status) =>
        string.Join(", ", build.Equipped
            .SelectMany(e => e.Element.Passives.Select(p => (e.Element.Name, Passive: p)))
            .Where(x => x.Passive.When.IsSatisfiedBy(state))
            .SelectMany(x => x.Passive.Modifier is Passive.ExtraStacks extra && extra.Status == status
                ? new[] { $"+{extra.Extra.Value} from {x.Name}" }
                : []));

    private static Optional<string> JoinCaveats(params string[] caveats)
    {
        var joined = string.Join("; ", caveats.Where(c => c.Length > 0));
        return joined.Length == 0 ? Optional.None<string>() : Optional.Some(joined);
    }
}
