using System.Collections.Immutable;
using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.StepPlaying;

/// <summary>Accumulator of one step: state so far, bullets so far, notes, events processed so far.</summary>
public sealed record Cascade(GameState State, ImmutableArray<FiredRule> Fired, ImmutableArray<string> Notes, int EventCount);

/// <summary>
/// A rule that matched an event, with the element (and copy count) it belongs to, and whether it fires as a chance there:
/// its own, the chain of events that led to it, or guards that hold only by a counted buff's chance stacks (ADRs D8, D33).
/// </summary>
public sealed record RuleMatch(EquippedElement Equipped, int RuleIndex, Rule Rule, Likelihood Likelihood);

public static class EventCascading
{
    public const int MaxCascadeDepth = 5;

    /// <summary>
    /// Match → guard → order by phase → apply → cascade every derived event (depth-first).
    /// Each level passes down the (rule, event) pairs already fired up this causal chain (<c>ancestry</c>): a rule
    /// never re-fires on an identical event caused by itself (loops terminate), while sibling
    /// occurrences — two orbs picked up, two traces spawned — each fire. <paramref name="byChance"/>: the event happens
    /// only by chance (a pickup only chance dropped), so every rule it fires is a chance.
    /// </summary>
    public static Cascade CascadeEvent(ValidatedBuild build, Cascade cascade, PendingEvent pending, int depth, bool byChance) =>
        CascadeEvent(build, cascade, pending, depth, ImmutableHashSet<string>.Empty, Optional.None<int>(), byChance);

    /// <summary><paramref name="cause"/>: the place in the step's fired rules of the rule whose outcome raised this event.</summary>
    private static Cascade CascadeEvent(
        ValidatedBuild build, Cascade cascade, PendingEvent pending, int depth, ImmutableHashSet<string> ancestry, Optional<int> cause, bool byChance)
    {
        var gameEvent = ToGameEvent(pending, cascade.State);
        if (depth > MaxCascadeDepth)
        {
            return cascade with { Notes = cascade.Notes.Add($"Cascade stopped at depth {MaxCascadeDepth} before: {ToEventKey(gameEvent)}") };
        }

        var matches = FindMatchingRules(build, cascade.State, gameEvent, ancestry, byChance);
        var shortOf = ListShortRules(build, cascade.State, gameEvent, ancestry);
        var lineage = ancestry.Union(matches.Select(match => ToFiredKey(match, gameEvent)));
        var applied = ApplyMatches(build, cascade.State, matches, gameEvent, depth, cascade.EventCount, cause);
        var first = cascade.Fired.Length;
        var advanced = cascade with
        {
            State = applied.State,
            Fired = cascade.Fired.AddRange(applied.Fired),
            Notes = cascade.Notes.AddRange(shortOf),
            EventCount = cascade.EventCount + 1,
        };
        return applied.Derived.Aggregate(advanced, (acc, derived) =>
            CascadeEvent(build, acc, derived.Event, depth + 1, lineage, Optional.Some(first + derived.Origin), derived.ByChance));
    }

    public static GameEvent ToGameEvent(PendingEvent pending, GameState state) =>
        pending.Match(
            ready => ready.Event,
            hit => (GameEvent)new GameEvent.Damaged(hit.Origin, state.Target.Tier, state.ListTargetStatuses(), hit.Span),
            kill => new GameEvent.Killed(kill.Origin, kill.Tier, state.ListTargetStatuses(), kill.Zone));

    public static ImmutableArray<RuleMatch> FindMatchingRules(
        ValidatedBuild build, GameState state, GameEvent gameEvent, ImmutableHashSet<string> ancestry, bool byChance)
    {
        var superType = build.ReadSuperType();
        return build.Equipped
            .SelectMany(equipped => equipped.Element.Rules.Select((rule, index) => (Equipped: equipped, Index: index, Rule: rule)))
            .Where(x => x.Rule.On.IsTriggeredBy(gameEvent, superType))
            .Select(x => (x.Equipped, x.Index, x.Rule, Guard: x.Rule.When.ReadGuard(state)))
            .Where(x => x.Guard != GuardResult.Fails)
            .Select(x => new RuleMatch(x.Equipped, x.Index, x.Rule, ReadLikelihood(x.Rule, x.Guard, byChance)))
            .Where(match => !ancestry.Contains(ToFiredKey(match, gameEvent)))
            .ToImmutableArray();
    }

    private static Likelihood ReadLikelihood(Rule rule, GuardResult guard, bool byChance) =>
        byChance || guard == GuardResult.ByChance ? Likelihood.Chance : rule.Likelihood;

    /// <summary>
    /// The rules the event would fire but for the stacks a counted buff holds ("Bulwark Finisher needs x3 Armor Charge,
    /// holds x1", ADRs D33): every other guard holds, and a <c>holds</c> doesn't.
    /// </summary>
    private static ImmutableArray<string> ListShortRules(ValidatedBuild build, GameState state, GameEvent gameEvent, ImmutableHashSet<string> ancestry)
    {
        var superType = build.ReadSuperType();
        var glossary = build.Catalog.Glossary;
        return
        [
            .. build.Equipped
                .SelectMany(equipped => equipped.Element.Rules.Select((rule, index) => (Equipped: equipped, Index: index, Rule: rule)))
                .Where(x => x.Rule.On.IsTriggeredBy(gameEvent, superType))
                .Where(x => x.Rule.When.Where(condition => condition is not Condition.Holds).ToImmutableArray().ReadGuard(state) != GuardResult.Fails)
                .Where(x => !ancestry.Contains(ToFiredKey(x.Equipped, x.Index, gameEvent)))
                .SelectMany(x => x.Rule.When.OfType<Condition.Holds>()
                    .Where(holds => ImmutableArray.Create<Condition>(holds).ReadGuard(state) == GuardResult.Fails)
                    .Take(1)
                    .Select(holds => $"{x.Equipped.Element.Name} needs x{holds.AtLeast.Value} {glossary.DescribeStatus(holds.Status)}, holds "
                        + state.ReadTally(holds.Status).Match(tally => DomainPhrasing.DescribeStacks(tally.Value.Sure, tally.Value.Chance), _ => "none")
                        + " — it doesn't fire.")),
        ];
    }

    /// <summary>An event an outcome raised, with the rule it came from as the rule matched (its index among the matches).</summary>
    private sealed record RaisedEvent(PendingEvent Event, int MatchIndex, bool ByChance);

    /// <summary>An event an outcome raised, with the place among this event's fired rules of the rule it came from.</summary>
    private sealed record DerivedEvent(PendingEvent Event, int Origin, bool ByChance);

    private sealed record AppliedMatches(GameState State, ImmutableArray<FiredRule> Fired, ImmutableArray<DerivedEvent> Derived);

    private sealed record Step(int MatchIndex, Outcome Outcome, Phase Phase, int Order);

    private sealed record Applied(Step Step, AppliedOutcome Outcome);

    /// <summary>
    /// Rules that don't stack (<see cref="Rule.DoesNotStackWith"/>) give way to the other element's rule on the same
    /// event: they are reported as fired, with no outcomes, naming the element they gave way to. An event a chance rule
    /// raised is a chance (ADRs D8); an outcome reads the stacks its own rule spent before it (Bulwark Finisher, ADRs D33).
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

        var seed = (State: state, Applied: ImmutableArray<Applied>.Empty, Raised: ImmutableArray<RaisedEvent>.Empty, Spent: ImmutableDictionary<int, StackSpend>.Empty);
        var result = steps.Aggregate(seed, (acc, step) =>
        {
            var match = matches[step.MatchIndex];
            var spentBefore = acc.Spent.TryGetValue(step.MatchIndex, out var spentByRule) ? Optional.Some(spentByRule) : Optional.None<StackSpend>();
            var source = new OutcomeSource(match.Equipped, match.Likelihood, spentBefore);
            var application = OutcomeApplication.ApplyOutcome(build, acc.State, step.Outcome, source);
            var byChance = match.Likelihood == Likelihood.Chance || application.ByChance;
            var raised = application.Derived.Select(pending => new RaisedEvent(pending, step.MatchIndex, byChance));
            var spent = application.Applied.Spend.Match(some => acc.Spent.SetItem(step.MatchIndex, some.Value), _ => acc.Spent);
            return (application.State, acc.Applied.Add(new Applied(step, application.Applied)), acc.Raised.AddRange(raised), spent);
        });

        var ordered = matches
            .Select((match, matchIndex) => (MatchIndex: matchIndex, Fired: ToFiredRule(match, matchIndex, result.Applied, gameEvent, depth, eventIndex, givenWay[matchIndex], cause)))
            .OrderBy(x => x.Fired.Outcomes.Select(o => o.Outcome.ResolvePhase()).DefaultIfEmpty(Phase.Refund).Min())
            .ToImmutableArray();
        var matchOrder = ordered.Select(x => x.MatchIndex).ToImmutableArray();
        return new AppliedMatches(
            result.State,
            [.. ordered.Select(x => x.Fired)],
            [.. result.Raised.Select(raised => new DerivedEvent(raised.Event, matchOrder.IndexOf(raised.MatchIndex), raised.ByChance))]);
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
            match.Rule.Reason, match.Likelihood, depth, eventIndex, notStackedWith, match.Rule.On, match.Rule.When, cause);
    }

    private static string ToFiredKey(RuleMatch match, GameEvent gameEvent) =>
        ToFiredKey(match.Equipped, match.RuleIndex, gameEvent);

    private static string ToFiredKey(EquippedElement equipped, int ruleIndex, GameEvent gameEvent) =>
        $"{equipped.Element.Id}#{ruleIndex}@{ToEventKey(gameEvent)}";

    /// <summary>Deterministic identity of an event (records holding arrays don't compare by content).</summary>
    public static string ToEventKey(GameEvent gameEvent) =>
        gameEvent.Match(
            cast => $"cast:{cast.Kind}" + (cast.Input == AbilityInput.Press ? "" : $":{cast.Input}"),
            damaged => $"damage:{ToOriginKey(damaged.Origin)}:{damaged.Tier}:{string.Join(",", damaged.TargetHas.Order())}",
            killed => $"kill:{ToOriginKey(killed.Origin)}:{killed.Tier}:{string.Join(",", killed.TargetHas.Order())}" + (killed.Zone == HitZone.Precision ? ":precision" : ""),
            struck => $"targets:{ToOriginKey(struck.Origin)}:{struck.Targets.Value}:{struck.Hit}" + (struck.Zone == HitZone.Precision ? ":precision" : ""),
            pickedUp => $"pickup:{pickedUp.Pickup}",
            gained => $"gain:{gained.Status}",
            maxed => $"max:{maxed.Status}",
            _ => "finisher",
            _ => "critical-health",
            broken => $"shield:{ToOriginKey(broken.Origin)}",
            expended => $"expended:{expended.Kind}",
            reloaded => $"reload:{ToOriginKey(reloaded.Origin)}",
            _ => "hit-at-critical-health",
            ammo => $"ammo:{ammo.Brick}:{ammo.Pickup}",
            readied => $"ready:{ToOriginKey(readied.Origin)}");

    private static string ToOriginKey(DamageOrigin origin) =>
        origin.Match(
            weapon => $"weapon/{weapon.Slot}/{weapon.Type}",
            ability => $"ability/{ability.Kind}/{ability.Type}" + (ability.Input == AbilityInput.Press ? "" : $"/{ability.Input}"),
            keyword => $"keyword/{keyword.Status}",
            summoned => $"summon/{summoned.Summon}",
            _ => "finisher");
}
