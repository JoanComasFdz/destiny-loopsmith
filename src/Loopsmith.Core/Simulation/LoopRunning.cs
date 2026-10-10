using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.Simulation;

/// <summary>
/// Analyses a designed loop by its order of triggers (docs/loop-format.md, "Analysis"; ADRs D2). Pure: same build and
/// steps ⇒ same report. The loop is played from a fresh spawn, then again from where it ended until a pass starts the
/// way an earlier pass started; the state is only what is present and declared (ADRs D1), so this settles within a few
/// passes. Nothing is counted: each step says what it needs and which step provided it, what it sets off, what is wasted.
/// </summary>
public static partial class LoopRunning
{
    /// <summary>A safety cap on the passes played, never reached by a loop whose state settles.</summary>
    private const int MaxPasses = 16;

    public static LoopReport RunLoop(ValidatedBuild build, LoopDesign design) =>
        RunLoop(build, design.Name, design.Steps);

    public static LoopReport RunLoop(ValidatedBuild build, string loopName, ImmutableArray<LoopStep> steps)
    {
        var glossary = build.Catalog.Glossary;
        var actions = steps.Select(step => step.Action).ToImmutableArray();
        var labels = actions.Select(action => glossary.DescribeAction(action, build.Build)).ToImmutableArray();
        var initial = ActionResolution.CreateInitialState();
        if (actions.IsEmpty)
        {
            var empty = new LoopPass(initial, []);
            return new LoopReport(loopName, build.Build.Name, labels, new LoopVerdict.NoSteps(), empty, empty, true, [], [], []);
        }

        var runs = RunPasses(build, initial, actions);
        var first = runs.Passes[0];
        var repeating = runs.Passes[runs.RepeatingIndex];
        var analysed = AnalyzePass(build, repeating, Optional.Some(runs.Passes[^1]), labels);
        var firstAnalysed = AnalyzePass(build, first, Optional.None<LoopPass>(), labels);
        var differences = runs.RepeatingIndex == 0 ? [] : ListDifferences(firstAnalysed, analysed);
        var verdict = JudgeVerdict(firstAnalysed, analysed);
        return new LoopReport(
            loopName, build.Build.Name, labels, verdict, first, repeating, runs.RepeatingIndex == 0, analysed, ListLinks(analysed), differences);
    }

    private sealed record PassRuns(ImmutableArray<LoopPass> Passes, int RepeatingIndex);

    /// <summary>
    /// Passes from the fresh spawn until one starts in a state an earlier pass started in: that earlier pass is the
    /// repeating one, and the last pass played is the one before it. Each pass numbers its steps from #1.
    /// </summary>
    private static PassRuns RunPasses(ValidatedBuild build, GameState initial, ImmutableArray<PlayerAction> actions)
    {
        var passes = ImmutableArray.CreateBuilder<LoopPass>();
        var starts = new List<string>();
        var start = initial;
        while (passes.Count < MaxPasses)
        {
            var seen = starts.IndexOf(ToStateKey(start));
            if (seen >= 0)
            {
                return new PassRuns(passes.ToImmutable(), seen);
            }

            starts.Add(ToStateKey(start));
            var resolutions = ActionResolution.ResolveSequence(build, start, actions);
            passes.Add(new LoopPass(start, resolutions));
            start = resolutions[^1].State with { Step = 0 };
        }

        return new PassRuns(passes.ToImmutable(), passes.Count - 1);
    }

    /// <summary>What is present and declared, in a stable order (records holding arrays don't compare by content).</summary>
    private static string ToStateKey(GameState state) =>
        $"{JoinSorted(state.Buffs.Select(buff => buff.Status.Value + (buff.AtMax ? "*" : "")))};"
        + $"{JoinSorted(state.Target.Debuffs.Select(debuff => debuff.Value))};"
        + $"{JoinSorted(state.Pickups.Select(pickup => pickup.Value))};{state.Target.Tier}";

    private static string JoinSorted(IEnumerable<string> values) => string.Join(",", values.Order(StringComparer.Ordinal));

    private static ImmutableArray<StepAnalysis> AnalyzePass(
        ValidatedBuild build, LoopPass pass, Optional<LoopPass> previous, ImmutableArray<string> labels) =>
        [.. pass.Resolutions.Select((_, index) => AnalyzeStep(build, pass, previous, labels[index], index))];

    private static StepAnalysis AnalyzeStep(ValidatedBuild build, LoopPass pass, Optional<LoopPass> previous, string label, int index)
    {
        var resolution = pass.Resolutions[index];
        var met = ListMetNeeds(build, pass, previous, index);
        var setsOff = ListSetOff(resolution);
        return new StepAnalysis(
            index,
            resolution.Action.ToActionToken(),
            label,
            [.. met.DistinctBy(use => use.Need).Select(use => use.Step)],
            setsOff,
            ListInOrder(resolution, setsOff, met),
            ListWasted(resolution),
            resolution.Blocked);
    }

    /// <summary>
    /// The elements that fire because an earlier step provided what they needed: all of them when the step's own action
    /// needed it (the orb it picks up, the buff it declares at max), else those with a rule that needed it, or that fired
    /// on an event such a rule raised (Dielectric's Bolt Charge on a pack an earlier step jolted sets off Shinobu's Vow).
    /// </summary>
    private static ImmutableArray<ElementMention> ListInOrder(Resolution resolution, ImmutableArray<ElementMention> setsOff, ImmutableArray<MetNeed> met)
    {
        if (met.Any(use => !use.By.IsSome()))
        {
            return setsOff;
        }

        var needing = met.SelectMany(use => use.By.Match(by => new[] { by.Value }, _ => [])).ToImmutableHashSet();
        var inOrder = Enumerable.Range(0, resolution.Fired.Length).Aggregate(ImmutableHashSet<int>.Empty, (places, place) =>
            needing.Contains(place) || IsCausedInOrder(resolution, places, place) || IsFedInOrder(resolution, places, place)
                ? places.Add(place)
                : places);
        var sources = inOrder.Select(place => resolution.Fired[place].Source).ToImmutableHashSet();
        return [.. setsOff.Where(element => sources.Contains(element.Source))];
    }

    /// <summary>The rule fired on an event an in-order rule raised.</summary>
    private static bool IsCausedInOrder(Resolution resolution, ImmutableHashSet<int> inOrder, int place) =>
        resolution.Fired[place].CausedBy.Match(cause => inOrder.Contains(cause.Value), _ => false);

    /// <summary>The rule reads what an in-order rule of the same step provided before it (Slice's Sever, read by To Shreds' kill).</summary>
    private static bool IsFedInOrder(Resolution resolution, ImmutableHashSet<int> inOrder, int place)
    {
        var rule = resolution.Fired[place];
        return ListRuleNeeds(rule).Any(need => inOrder
            .Where(earlier => resolution.Fired[earlier].EventIndex < rule.EventIndex)
            .Any(earlier => resolution.Fired[earlier].Outcomes.Any(applied => IsProvidedBy(applied.Outcome, need))));
    }

    /// <summary>The elements whose rules applied at the step, in the order they first fired.</summary>
    private static ImmutableArray<ElementMention> ListSetOff(Resolution resolution) =>
        [
            .. resolution.Fired
                .Where(rule => !rule.NotStackedWith.IsSome())
                .GroupBy(rule => rule.Source)
                .Select(group => ToMention(group)),
        ];

    private static ImmutableArray<WastedMention> ListWasted(Resolution resolution) =>
        [
            .. resolution.Fired
                .SelectMany(rule => rule.NotStackedWith.Match(partner => new[] { (Rule: rule, Partner: partner.Value) }, _ => []))
                .GroupBy(x => (x.Rule.Source, x.Partner))
                .Select(group => new WastedMention(ToMention(group.Select(x => x.Rule)), group.Key.Partner)),
        ];

    /// <summary>An element is marked chance only when every rule of it that fired there is a chance rule.</summary>
    private static ElementMention ToMention(IEnumerable<FiredRule> rules)
    {
        var fired = rules.ToImmutableArray();
        var likelihood = fired.All(rule => rule.Likelihood == Likelihood.Chance) ? Likelihood.Chance : Likelihood.Always;
        return new ElementMention(fired[0].Source, fired[0].SourceName, fired[0].Affinity, likelihood);
    }

    // ── needs ───────────────────────────────────────────────────────────────────

    /// <summary>A thing a step can need: a buff on you, a debuff on the pack, a pickup on the ground, a declared maximum.</summary>
    [Union]
    internal partial record Need
    {
        partial record Buff(StatusId Status);

        partial record Debuff(StatusId Status);

        partial record GroundPickup(PickupId Pickup);

        partial record DeclaredMax(StatusId Status);
    }

    /// <summary>A need and who needs it: the rule (its place in the step's fired rules) that reads or consumes it, none for the step's own action.</summary>
    private sealed record NeedUse(Need Need, Optional<int> By);

    /// <summary>A use whose need was there when the step began, with the step that provided it.</summary>
    private sealed record MetNeed(Need Need, Optional<int> By, StepNeed Step);

    /// <summary>
    /// What the step uses that was already there when it began — so an earlier step provided it — and that step. What
    /// the step provides for itself (its own hit jolting the pack before its kill) is part of its cascade, not a need.
    /// </summary>
    private static ImmutableArray<MetNeed> ListMetNeeds(ValidatedBuild build, LoopPass pass, Optional<LoopPass> previous, int index)
    {
        var glossary = build.Catalog.Glossary;
        var resolution = pass.Resolutions[index];
        var before = ReadStateBefore(pass, index);
        return
        [
            .. ListNeedUses(resolution, before)
                .Where(use => IsPresent(before, use.Need))
                .SelectMany(use => FindProvider(pass, previous, index, use.Need).Match(
                    provider => new[] { new MetNeed(use.Need, use.By, new StepNeed(DescribeNeed(glossary, use.Need), ReadNeedAffinity(glossary, use.Need), provider.Value)) },
                    _ => [])),
        ];
    }

    private static IEnumerable<NeedUse> ListNeedUses(Resolution resolution, GameState before)
    {
        var fromAction = resolution.Blocked.IsSome()
            ? []
            : resolution.Action switch
            {
                PlayerAction.CollectPickups collect => [new Need.GroundPickup(collect.Pickup)],
                PlayerAction.Declare { Declaration: StateDeclaration.ReachMax max } => [new Need.Buff(max.Status)],
                PlayerAction.Declare { Declaration: StateDeclaration.EndStatus end } =>
                    [before.HasBuff(end.Status) ? new Need.Buff(end.Status) : new Need.Debuff(end.Status)],
                _ => Array.Empty<Need>(),
            };
        var fromRules = resolution.Fired
            .Select((rule, place) => (Rule: rule, Place: place))
            .Where(x => !x.Rule.NotStackedWith.IsSome())
            .SelectMany(x => ListRuleNeeds(x.Rule)
                .Where(need => !IsProvidedBefore(resolution, x.Rule.EventIndex, need))
                .Select(need => new NeedUse(need, Optional.Some(x.Place))));
        return fromAction.Select(need => new NeedUse(need, Optional.None<int>())).Concat(fromRules);
    }

    private static GameState ReadStateBefore(LoopPass pass, int index) =>
        index == 0 ? pass.Start : pass.Resolutions[index - 1].State;

    /// <summary>Whether a rule of the same step provided the need on an event before the one a rule fired on.</summary>
    private static bool IsProvidedBefore(Resolution resolution, int eventIndex, Need need) =>
        resolution.Fired
            .Where(rule => rule.EventIndex < eventIndex)
            .Any(rule => rule.Outcomes.Any(applied => IsProvidedBy(applied.Outcome, need)));

    /// <summary>A rule needs what its trigger and guards read, and the buffs its outcomes consumed.</summary>
    private static IEnumerable<Need> ListRuleNeeds(FiredRule rule)
    {
        var onTarget = rule.On switch
        {
            Trigger.KillDebuffed kill => kill.TargetHas,
            Trigger.DamageDebuffed damage => damage.TargetHas,
            _ => [],
        };
        var guards = rule.When.SelectMany(condition => condition.Match(
            has => new Need[] { new Need.Buff(has.Status) },
            _ => [],
            targetHas => [new Need.Debuff(targetHas.Status)],
            atMax => [new Need.DeclaredMax(atMax.Status)]));
        var consumed = rule.Outcomes
            .Where(applied => !applied.Caveat.IsSome())
            .SelectMany(applied => applied.Outcome switch
            {
                Outcome.RemoveBuff remove => [new Need.Buff(remove.Status)],
                Outcome.ConvertStacksToEnergy convert => [new Need.Buff(convert.Consumed)],
                _ => Array.Empty<Need>(),
            });
        return onTarget.Select(status => (Need)new Need.Debuff(status)).Concat(guards).Concat(consumed);
    }

    private static bool IsPresent(GameState state, Need need) =>
        need.Match(
            buff => state.HasBuff(buff.Status),
            debuff => state.TargetHas(debuff.Status),
            pickup => state.HasPickup(pickup.Pickup),
            declared => state.IsAtMax(declared.Status));

    /// <summary>A step that provided a need, and whether a rule that always fires provided it there.</summary>
    private sealed record Provision(NeedProvider Provider, bool IsCertain);

    /// <summary>
    /// The step that provided the need. Of the steps since it last arrived (present from then on), the latest one where
    /// a rule that always fires provided it, else the latest one — in this pass, and back into the pass before when it
    /// was there from the start of this one.
    /// </summary>
    private static Optional<NeedProvider> FindProvider(LoopPass pass, Optional<LoopPass> previous, int index, Need need)
    {
        var arrival = FindArrival(pass, index, need);
        var inThisPass = ListProvisions(pass, arrival, index, need, (step, elements) => new NeedProvider.EarlierStep(step, elements));
        var inPreviousPass = arrival >= 0
            ? []
            : previous.Match(
                before => ListProvisions(before.Value, FindArrival(before.Value, before.Value.Resolutions.Length, need),
                    before.Value.Resolutions.Length, need, (step, elements) => new NeedProvider.PreviousPass(step, elements)),
                _ => ImmutableArray<Provision>.Empty);
        var provisions = inThisPass.AddRange(inPreviousPass);
        return provisions.Where(provision => provision.IsCertain).Concat(provisions)
            .Select(provision => Optional.Some(provision.Provider))
            .FindFirstSome();
    }

    /// <summary>The latest step before <paramref name="end"/> whose start lacked the need (it arrived there), else -1.</summary>
    private static int FindArrival(LoopPass pass, int end, Need need) =>
        Enumerable.Range(0, end).LastOrDefault(step => !IsPresent(ReadStateBefore(pass, step), need), -1);

    /// <summary>The steps from the arrival (or the start of the pass) to <paramref name="end"/> that provided the need, latest first.</summary>
    private static ImmutableArray<Provision> ListProvisions(
        LoopPass pass, int arrival, int end, Need need, Func<int, ImmutableArray<ElementMention>, NeedProvider> toProvider)
    {
        var first = Math.Max(arrival, 0);
        return
        [
            .. Enumerable.Range(first, end - first).Reverse()
                .SelectMany(step => FindProvidingElements(pass.Resolutions[step], need).Match(
                    elements => new[] { new Provision(toProvider(step, elements.Value), IsCertain(elements.Value)) },
                    _ => [])),
        ];
    }

    /// <summary>A declared state (no elements) or one element whose rule always fires.</summary>
    private static bool IsCertain(ImmutableArray<ElementMention> elements) =>
        elements.IsEmpty || elements.Any(element => element.Likelihood == Likelihood.Always);

    /// <summary>The elements whose applied outcomes provided the need at that step (none for a declared maximum).</summary>
    private static Optional<ImmutableArray<ElementMention>> FindProvidingElements(Resolution resolution, Need need)
    {
        if (need is Need.DeclaredMax declared)
        {
            return !resolution.Blocked.IsSome() && resolution.Action is PlayerAction.Declare { Declaration: StateDeclaration.ReachMax max } && max.Status == declared.Status
                ? Optional.Some(ImmutableArray<ElementMention>.Empty)
                : Optional.None<ImmutableArray<ElementMention>>();
        }

        var providers = resolution.Fired
            .Where(rule => rule.Outcomes.Any(applied => IsProvidedBy(applied.Outcome, need)))
            .GroupBy(rule => rule.Source)
            .Select(group => ToMention(group))
            .ToImmutableArray();
        return providers.IsEmpty ? Optional.None<ImmutableArray<ElementMention>>() : Optional.Some(providers);
    }

    private static bool IsProvidedBy(Outcome outcome, Need need) =>
        (need, outcome) switch
        {
            (Need.Buff buff, Outcome.ApplyBuff apply) => apply.Status == buff.Status,
            (Need.Debuff debuff, Outcome.DebuffTarget applied) => applied.Status == debuff.Status,
            (Need.GroundPickup pickup, Outcome.Spawn spawn) => spawn.Pickup == pickup.Pickup,
            _ => false,
        };

    private static string DescribeNeed(KeywordGlossary glossary, Need need) =>
        need.Match(
            buff => glossary.DescribeStatus(buff.Status),
            debuff => glossary.DescribeStatus(debuff.Status),
            pickup => glossary.DescribePickup(pickup.Pickup),
            declared => $"{glossary.DescribeStatus(declared.Status)} at max");

    private static Affinity ReadNeedAffinity(KeywordGlossary glossary, Need need) =>
        need.Match(
            buff => glossary.ReadStatusAffinity(buff.Status),
            debuff => glossary.ReadStatusAffinity(debuff.Status),
            pickup => glossary.Pickups.TryGetValue(pickup.Pickup, out var definition) ? definition.Affinity : Affinity.Neutral,
            declared => glossary.ReadStatusAffinity(declared.Status));

    // ── the step chain ──────────────────────────────────────────────────────────

    /// <summary>Each need as an arrow from the step that provided it to the step that needs it, one per pair of steps.</summary>
    private static ImmutableArray<StepLink> ListLinks(ImmutableArray<StepAnalysis> steps) =>
        [
            .. steps
                .SelectMany(step => step.Needs.Select(need => (To: step.StepIndex, Need: need, From: ReadProviderStep(need.Provider))))
                .GroupBy(x => (x.From.StepIndex, x.From.FromPreviousPass, x.To))
                .Select(group => new StepLink(
                    group.Key.StepIndex,
                    group.Key.To,
                    group.Key.FromPreviousPass,
                    [.. group.Select(x => x.Need)],
                    group.Any(x => IsCertain(ReadProviderElements(x.Need.Provider))) ? Likelihood.Always : Likelihood.Chance)),
        ];

    private static (int StepIndex, bool FromPreviousPass) ReadProviderStep(NeedProvider provider) =>
        provider.Match(earlier => (earlier.StepIndex, false), previous => (previous.StepIndex, true));

    private static ImmutableArray<ElementMention> ReadProviderElements(NeedProvider provider) =>
        provider.Match(earlier => earlier.Elements, previous => previous.Elements);

    // ── verdict and the first pass ──────────────────────────────────────────────

    private static LoopVerdict JudgeVerdict(ImmutableArray<StepAnalysis> firstPass, ImmutableArray<StepAnalysis> repeatingPass) =>
        FindFirstBlocked(repeatingPass).Match(
            blocked => (LoopVerdict)new LoopVerdict.Breaks(blocked.Value),
            _ => new LoopVerdict.Repeats(FindFirstBlocked(firstPass)));

    private static Optional<BlockedStep> FindFirstBlocked(ImmutableArray<StepAnalysis> steps) =>
        steps
            .Select(step => step.Blocked.Map(reason => new BlockedStep(step.StepIndex, step.Label, reason)))
            .FindFirstSome();

    /// <summary>The steps that do something else on the first pass than in the repeating one.</summary>
    private static ImmutableArray<PassDifference> ListDifferences(ImmutableArray<StepAnalysis> firstPass, ImmutableArray<StepAnalysis> repeatingPass) =>
        [
            .. firstPass.Zip(repeatingPass)
                .Select(pair => new PassDifference(
                    pair.First.StepIndex,
                    pair.First.Label,
                    pair.Second.Blocked.IsSome() ? Optional.None<string>() : pair.First.Blocked,
                    [.. pair.Second.SetsOff.Where(element => !pair.First.SetsOff.Any(other => other.Source == element.Source))],
                    [.. pair.First.SetsOff.Where(element => !pair.Second.SetsOff.Any(other => other.Source == element.Source))]))
                .Where(difference => difference.BlockedOnFirstPass.IsSome()
                    || !difference.OnlyInRepeatingPass.IsEmpty
                    || !difference.OnlyOnFirstPass.IsEmpty),
        ];
}
