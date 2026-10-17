using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.StepPlaying;

/// <summary>
/// Plays a designed loop for its analysis (docs/loop-format.md, "Analysis"; ADRs D2). Pure: same build and steps ⇒ same
/// passes. The loop is played from a fresh spawn, then again from where it ended until a pass starts the way an earlier
/// pass started; the state is only what is present and declared (ADRs D1), so this settles within a few passes.
/// </summary>
public static class LoopPlaying
{
    /// <summary>A safety cap on the passes played, never reached by a loop whose state settles.</summary>
    private const int MaxPasses = 16;

    /// <summary>The passes played, and which of them repeats. A loop with no steps is one empty pass from a fresh spawn.</summary>
    public static PlayedLoop PlayLoop(ValidatedBuild build, ImmutableArray<LoopStep> steps)
    {
        var actions = steps.Select(step => step.Action).ToImmutableArray();
        var initial = ActionResolution.CreateInitialState();
        return actions.IsEmpty
            ? new PlayedLoop(steps, [new LoopPass(initial, [])], 0)
            : PlayPasses(build, steps, initial, actions);
    }

    /// <summary>
    /// Passes from the fresh spawn until one starts in a state an earlier pass started in: that earlier pass is the
    /// repeating one, and the last pass played is the one before it. Each pass numbers its steps from #1.
    /// </summary>
    private static PlayedLoop PlayPasses(ValidatedBuild build, ImmutableArray<LoopStep> steps, GameState initial, ImmutableArray<PlayerAction> actions)
    {
        var passes = ImmutableArray.CreateBuilder<LoopPass>();
        var starts = new List<string>();
        var start = initial;
        while (passes.Count < MaxPasses)
        {
            var seen = starts.IndexOf(ToStateKey(start));
            if (seen >= 0)
            {
                return new PlayedLoop(steps, passes.ToImmutable(), seen);
            }

            starts.Add(ToStateKey(start));
            var resolutions = ActionResolution.ResolveSequence(build, start, actions);
            passes.Add(new LoopPass(start, resolutions));
            start = resolutions[^1].State.StartNextPass();
        }

        return new PlayedLoop(steps, passes.ToImmutable(), passes.Count - 1);
    }

    /// <summary>
    /// What is present and declared, with a counted buff's stacks and the pickups only chance dropped, in a stable order
    /// (records holding arrays don't compare by content). A counted buff's changes are its history, not its state.
    /// </summary>
    private static string ToStateKey(GameState state) =>
        $"{JoinSorted(state.Buffs.Select(buff => buff.Status.Value + ToTallyKey(buff.Tally) + (buff.AtMax ? "*" : "")))};"
        + $"{JoinSorted(state.Target.Debuffs.Select(debuff => debuff.Value))};"
        + $"{JoinSorted(state.Pickups.Select(ground => ground.Pickup.Value + (ground.Likelihood == Likelihood.Chance ? "?" : "")))};{state.Target.Tier}";

    private static string ToTallyKey(Optional<StackTally> tally) =>
        tally.Match(some => $"={some.Value.Sure.Value}+{some.Value.Chance.Value}", _ => "");

    private static string JoinSorted(IEnumerable<string> values) => string.Join(",", values.Order(StringComparer.Ordinal));
}
