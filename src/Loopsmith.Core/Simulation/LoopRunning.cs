using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.Simulation;

/// <summary>
/// Runs a designed loop back to back, cycle after cycle, from a fresh spawn and measures it
/// (docs/loop-format.md, "Analysis"). Pure: same build + steps ⇒ same report. Ability energy isn't simulated
/// (ADRs D21): a cycle only breaks on a step that can't happen at all, and energy outcomes are never added up —
/// the report counts what fired, what was wasted (rules that don't stack, D23) and buff uptime.
/// </summary>
public static class LoopRunning
{
    public const string KillsLabel = "Kills";

    public static LoopReport RunLoop(ValidatedBuild build, LoopDesign design, int maxCycles) =>
        RunLoop(build, design.Name, design.Steps, maxCycles);

    /// <summary>
    /// Cycle N starts from the state cycle N − 1 ended in. A cycle with a blocked step is played to its end and
    /// is the last one run; <paramref name="maxCycles"/> below 1 runs one cycle. A loop without steps runs no
    /// cycle at all (0 completed).
    /// </summary>
    public static LoopReport RunLoop(ValidatedBuild build, string loopName, ImmutableArray<LoopStep> steps, int maxCycles)
    {
        var cycleLimit = Math.Max(1, maxCycles);
        var initial = ActionResolution.CreateInitialState();
        var actions = steps.Select(step => step.Action).ToImmutableArray();
        var cycles = actions.IsEmpty ? [] : RunCycles(build, initial, actions, cycleLimit);
        var completed = cycles.Count(cycle => !cycle.Blocked.IsSome());
        var steady = cycles.IsEmpty ? [] : cycles[Math.Max(completed, 1) - 1].Resolutions;
        var fired = steady.SelectMany(resolution => resolution.Fired).Where(rule => !rule.NotStackedWith.IsSome()).ToImmutableArray();
        return new LoopReport(
            loopName,
            build.Build.Name,
            steps.Length,
            cycles,
            completed,
            cycleLimit,
            TallySources(fired),
            TallyWasted(steady),
            TallyOutcomes(build, steady),
            MeasureUptime(build, steady),
            fired.SelectMany(rule => rule.Outcomes).Count(outcome => outcome.Certainty == Certainty.Unknown),
            fired.Count(rule => rule.Likelihood == Likelihood.Chance));
    }

    private static ImmutableArray<CycleRun> RunCycles(ValidatedBuild build, GameState initial, ImmutableArray<PlayerAction> actions, int cycleLimit)
    {
        var cycles = ImmutableArray.CreateBuilder<CycleRun>();
        var state = initial;
        for (var number = 1; number <= cycleLimit; number++)
        {
            var cycle = RunCycle(build, state, actions, number);
            cycles.Add(cycle);
            if (cycle.Blocked.IsSome())
            {
                break;
            }

            state = cycle.Resolutions[^1].State;
        }

        return cycles.ToImmutable();
    }

    private static CycleRun RunCycle(ValidatedBuild build, GameState state, ImmutableArray<PlayerAction> actions, int number)
    {
        var resolutions = ActionResolution.ResolveSequence(build, state, actions);
        var blocked = resolutions
            .Select((resolution, index) => resolution.Blocked.Map(reason => new BlockedStep(index, resolution.Action, reason)))
            .FindFirstSome();
        return new CycleRun(number, resolutions, blocked);
    }

    /// <summary>How many bullets each element fired, most first (ties in order of first appearance).</summary>
    private static ImmutableArray<SourceTally> TallySources(ImmutableArray<FiredRule> fired) =>
        fired
            .GroupBy(rule => rule.Source)
            .Select((group, order) => (Tally: new SourceTally(group.Key, group.First().SourceName, group.First().Affinity, group.Count()), Order: order))
            .OrderByDescending(x => x.Tally.Fired)
            .ThenBy(x => x.Order)
            .Select(x => x.Tally)
            .ToImmutableArray();

    /// <summary>
    /// How often each element's rule gave nothing because it doesn't stack with another's, per partner, most first
    /// (ties in order of first appearance).
    /// </summary>
    private static ImmutableArray<WastedTally> TallyWasted(ImmutableArray<Resolution> cycle) =>
        cycle
            .SelectMany(resolution => resolution.Fired)
            .SelectMany(rule => rule.NotStackedWith.Match(partner => new[] { (Rule: rule, Partner: partner.Value) }, _ => []))
            .GroupBy(x => (x.Rule.Source, x.Partner))
            .Select((group, order) => (
                Tally: new WastedTally(group.Key.Source, group.First().Rule.SourceName, group.First().Rule.Affinity, group.Key.Partner, group.Count()),
                Order: order))
            .OrderByDescending(x => x.Tally.Count)
            .ThenBy(x => x.Order)
            .Select(x => x.Tally)
            .ToImmutableArray();

    /// <summary>
    /// <c>Kills</c> (the targets of the kill actions performed + killing strikes), then <c>&lt;Pickup&gt; spawned</c> and
    /// <c>&lt;Status&gt; maxed</c>, each in order of first appearance. A status counts as maxed once per
    /// <c>StacksMaxed</c> event a rule reacted to.
    /// </summary>
    private static ImmutableArray<OutcomeTally> TallyOutcomes(ValidatedBuild build, ImmutableArray<Resolution> cycle)
    {
        var glossary = build.Catalog.Glossary;
        var applied = cycle.SelectMany(r => r.Fired).SelectMany(rule => rule.Outcomes).Select(o => o.Outcome).ToImmutableArray();
        var killActions = cycle.Where(r => !r.Blocked.IsSome()).Sum(r => CountKills(r.Action));
        var killingStrikes = applied.Count(outcome => outcome is Outcome.StrikeTarget { Hit: HitOutcome.Kill });
        var spawned = applied
            .OfType<Outcome.Spawn>()
            .GroupBy(spawn => spawn.Pickup)
            .Select(group => new OutcomeTally($"{glossary.DescribePickup(group.Key)} spawned", group.Sum(spawn => spawn.Count)));
        var maxed = cycle
            .SelectMany((resolution, step) => resolution.Fired.SelectMany(rule => rule.Trigger is GameEvent.StacksMaxed max
                ? new[] { (max.Status, Step: step, rule.EventIndex) }
                : []))
            .Distinct()
            .GroupBy(x => x.Status)
            .Select(group => new OutcomeTally($"{glossary.DescribeStatus(group.Key)} maxed", group.Count()));
        return [new OutcomeTally(KillsLabel, killActions + killingStrikes), .. spawned, .. maxed];
    }

    /// <summary>A kill action kills every one of its targets (ADRs D22).</summary>
    private static int CountKills(PlayerAction action) =>
        action switch
        {
            PlayerAction.CastAbility { Hit: HitOutcome.Kill } cast => cast.Targets.Value,
            PlayerAction.FireWeapon { Hit: HitOutcome.Kill } fire => fire.Targets.Value,
            _ => 0,
        };

    /// <summary>After how many of the cycle's steps each buff was active, longest first (ties in order gained).</summary>
    private static ImmutableArray<BuffUptime> MeasureUptime(ValidatedBuild build, ImmutableArray<Resolution> cycle)
    {
        var glossary = build.Catalog.Glossary;
        return cycle
            .SelectMany(resolution => resolution.State.Buffs.Select(buff => buff.Status))
            .Distinct()
            .Select((status, order) => (
                Uptime: new BuffUptime(
                    status,
                    glossary.DescribeStatus(status),
                    glossary.ReadStatusAffinity(status),
                    cycle.Count(resolution => resolution.State.HasBuff(status)),
                    cycle.Length),
                Order: order))
            .OrderByDescending(x => x.Uptime.StepsActive)
            .ThenBy(x => x.Order)
            .Select(x => x.Uptime)
            .ToImmutableArray();
    }
}
