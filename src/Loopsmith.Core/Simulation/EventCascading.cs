using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Simulation;

/// <summary>Accumulator of one step: state so far, bullets so far, notes, events processed so far.</summary>
public sealed record Cascade(GameState State, ImmutableArray<FiredRule> Fired, ImmutableArray<string> Notes, int EventCount);

/// <summary>A rule that matched an event, with the element (and copy count) it belongs to.</summary>
public sealed record RuleMatch(EquippedElement Equipped, int RuleIndex, Rule Rule);

public static class EventCascading
{
    public const int MaxCascadeDepth = 5;

    /// <summary>
    /// Match → guard → order by phase → apply → cascade every derived event (depth-first).
    /// Each level passes down the (rule, event) pairs already fired up this causal chain (<c>ancestry</c>): a rule
    /// never re-fires on an identical event caused by itself (loops terminate), while sibling
    /// occurrences — two orbs picked up, two traces spawned — each fire.
    /// </summary>
    public static Cascade CascadeEvent(ValidatedBuild build, Cascade cascade, PendingEvent pending, int depth) =>
        CascadeEvent(build, cascade, pending, depth, ImmutableHashSet<string>.Empty, Optional.None<int>());

    /// <summary><paramref name="cause"/>: the place in the step's fired rules of the rule whose outcome raised this event.</summary>
    private static Cascade CascadeEvent(
        ValidatedBuild build, Cascade cascade, PendingEvent pending, int depth, ImmutableHashSet<string> ancestry, Optional<int> cause)
    {
        var gameEvent = ToGameEvent(pending, cascade.State);
        if (depth > MaxCascadeDepth)
        {
            return cascade with { Notes = cascade.Notes.Add($"Cascade stopped at depth {MaxCascadeDepth} before: {ToEventKey(gameEvent)}") };
        }

        var matches = FindMatchingRules(build, cascade.State, gameEvent, ancestry);
        var lineage = ancestry.Union(matches.Select(match => ToFiredKey(match, gameEvent)));
        var applied = ApplyMatches(build, cascade.State, matches, gameEvent, depth, cascade.EventCount, cause);
        var first = cascade.Fired.Length;
        var advanced = cascade with { State = applied.State, Fired = cascade.Fired.AddRange(applied.Fired), EventCount = cascade.EventCount + 1 };
        return applied.Derived.Aggregate(advanced, (acc, derived) =>
            CascadeEvent(build, acc, derived.Event, depth + 1, lineage, Optional.Some(first + derived.Origin)));
    }

    public static GameEvent ToGameEvent(PendingEvent pending, GameState state) =>
        pending.Match(
            ready => ready.Event,
            hit => (GameEvent)new GameEvent.Damaged(hit.Origin, state.Target.Tier, state.ListTargetStatuses()),
            kill => new GameEvent.Killed(kill.Origin, state.Target.Tier, state.ListTargetStatuses()));

    public static ImmutableArray<RuleMatch> FindMatchingRules(
        ValidatedBuild build, GameState state, GameEvent gameEvent, ImmutableHashSet<string> ancestry) =>
        build.Equipped
            .SelectMany(equipped => equipped.Element.Rules.Select((rule, index) => new RuleMatch(equipped, index, rule)))
            .Where(match => match.Rule.On.IsTriggeredBy(gameEvent))
            .Where(match => match.Rule.When.IsSatisfiedBy(state))
            .Where(match => !ancestry.Contains(ToFiredKey(match, gameEvent)))
            .ToImmutableArray();

    /// <summary>An event an outcome raised, with the rule it came from as the rule matched (its index among the matches).</summary>
    private sealed record RaisedEvent(PendingEvent Event, int MatchIndex);

    /// <summary>An event an outcome raised, with the place among this event's fired rules of the rule it came from.</summary>
    private sealed record DerivedEvent(PendingEvent Event, int Origin);

    private sealed record AppliedMatches(GameState State, ImmutableArray<FiredRule> Fired, ImmutableArray<DerivedEvent> Derived);

    private sealed record Step(int MatchIndex, Outcome Outcome, Phase Phase, int Order);

    private sealed record Applied(Step Step, AppliedOutcome Outcome);

    /// <summary>
    /// Rules that don't stack (<see cref="Rule.DoesNotStackWith"/>) give way to the other element's rule on the same
    /// event: they are reported as fired, with no outcomes, naming the element they gave way to.
    /// </summary>
    private static AppliedMatches ApplyMatches(
        ValidatedBuild build, GameState state, ImmutableArray<RuleMatch> matches, GameEvent gameEvent, int depth, int eventIndex, Optional<int> cause)
    {
        var givenWay = matches.Select(match => FindStackingPartner(match, matches)).ToImmutableArray();
        var steps = matches
            .Select((match, matchIndex) => (Match: match, MatchIndex: matchIndex))
            .Where(x => !givenWay[x.MatchIndex].IsSome())
            .SelectMany(x => x.Match.Rule.Then.Select(outcome => (x.MatchIndex, Outcome: outcome)))
            .Select((x, order) => new Step(x.MatchIndex, x.Outcome, x.Outcome.ResolvePhase(), order))
            .OrderBy(step => step.Phase)
            .ThenBy(step => step.Order)
            .ToImmutableArray();

        var seed = (State: state, Applied: ImmutableArray<Applied>.Empty, Raised: ImmutableArray<RaisedEvent>.Empty);
        var result = steps.Aggregate(seed, (acc, step) =>
        {
            var application = OutcomeApplication.ApplyOutcome(build, acc.State, step.Outcome);
            var raised = application.Derived.Select(pending => new RaisedEvent(pending, step.MatchIndex));
            return (application.State, acc.Applied.Add(new Applied(step, application.Applied)), acc.Raised.AddRange(raised));
        });

        var ordered = matches
            .Select((match, matchIndex) => (MatchIndex: matchIndex, Fired: ToFiredRule(match, matchIndex, result.Applied, gameEvent, depth, eventIndex, givenWay[matchIndex], cause)))
            .OrderBy(x => x.Fired.Outcomes.Select(o => o.Outcome.ResolvePhase()).DefaultIfEmpty(Phase.Refund).Min())
            .ToImmutableArray();
        var matchOrder = ordered.Select(x => x.MatchIndex).ToImmutableArray();
        return new AppliedMatches(
            result.State,
            [.. ordered.Select(x => x.Fired)],
            [.. result.Raised.Select(raised => new DerivedEvent(raised.Event, matchOrder.IndexOf(raised.MatchIndex)))]);
    }

    /// <summary>The name of another element matching the same event that this rule doesn't stack with, if any.</summary>
    private static Optional<string> FindStackingPartner(RuleMatch match, ImmutableArray<RuleMatch> matches) =>
        matches
            .Where(other => other.Equipped.Element.Id != match.Equipped.Element.Id)
            .Where(other => match.Rule.DoesNotStackWith.Contains(other.Equipped.Element.Id))
            .Select(other => Optional.Some(other.Equipped.Element.Name))
            .FindFirstSome();

    private static FiredRule ToFiredRule(
        RuleMatch match, int matchIndex, ImmutableArray<Applied> applied, GameEvent gameEvent, int depth, int eventIndex,
        Optional<string> notStackedWith, Optional<int> cause)
    {
        var element = match.Equipped.Element;
        var outcomes = applied.Where(a => a.Step.MatchIndex == matchIndex).Select(a => a.Outcome).ToImmutableArray();
        return new FiredRule(
            element.Id, element.Name, element.Kind, element.Affinity, gameEvent, outcomes,
            match.Rule.Reason, match.Rule.Likelihood, depth, eventIndex, notStackedWith, match.Rule.On, match.Rule.When, cause);
    }

    private static string ToFiredKey(RuleMatch match, GameEvent gameEvent) =>
        $"{match.Equipped.Element.Id}#{match.RuleIndex}@{ToEventKey(gameEvent)}";

    /// <summary>Deterministic identity of an event (records holding arrays don't compare by content).</summary>
    public static string ToEventKey(GameEvent gameEvent) =>
        gameEvent.Match(
            cast => $"cast:{cast.Kind}" + (cast.Airborne ? ":air" : ""),
            damaged => $"damage:{ToOriginKey(damaged.Origin)}:{damaged.Tier}:{string.Join(",", damaged.TargetHas.Order())}",
            killed => $"kill:{ToOriginKey(killed.Origin)}:{killed.Tier}:{string.Join(",", killed.TargetHas.Order())}",
            struck => $"targets:{ToOriginKey(struck.Origin)}:{struck.Targets.Value}:{struck.Hit}",
            pickedUp => $"pickup:{pickedUp.Pickup}",
            gained => $"gain:{gained.Status}",
            maxed => $"max:{maxed.Status}");

    private static string ToOriginKey(DamageOrigin origin) =>
        origin.Match(
            weapon => $"weapon/{weapon.Slot}/{weapon.Type}",
            ability => $"ability/{ability.Kind}/{ability.Type}",
            keyword => $"keyword/{keyword.Status}",
            summoned => $"summon/{summoned.Summon}");
}
