using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.ReportComparison;

/// <summary>
/// Two loops' orders side by side (docs/loop-format.md, "Comparison"; ADRs D2). Pure. Triggers are matched by
/// occurrence of the same action — A's first <c>grenade:kill</c> with B's first — so each row shows a trigger's place
/// in both loops and what that place changes. Nothing is scored. The loops may use different builds.
/// </summary>
public static class LoopComparing
{
    public static LoopComparison CompareLoops(LoopReport left, LoopReport right)
    {
        var leftSteps = ListOccurrences(left.Steps);
        var rightSteps = ListOccurrences(right.Steps);
        var matched = leftSteps.Select(step => CompareTrigger(
            step.Step.Label,
            Optional.Some(step.Step),
            FindOccurrence(rightSteps, step.Key)));
        var rightOnly = rightSteps
            .Where(step => !leftSteps.Any(other => other.Key == step.Key))
            .Select(step => CompareTrigger(step.Step.Label, Optional.None<StepAnalysis>(), Optional.Some(step.Step)));
        return new LoopComparison(left, right, [.. matched, .. rightOnly]);
    }

    /// <summary>Each step with its occurrence key: <c>grenade:kill#2</c> is the loop's second grenade kill.</summary>
    private static ImmutableArray<(string Key, StepAnalysis Step)> ListOccurrences(ImmutableArray<StepAnalysis> steps) =>
        [
            .. steps.Select((step, index) =>
                ($"{step.Token}#{steps.Take(index + 1).Count(other => other.Token == step.Token)}", step)),
        ];

    private static Optional<StepAnalysis> FindOccurrence(ImmutableArray<(string Key, StepAnalysis Step)> steps, string key) =>
        steps.Select(step => step.Key == key ? Optional.Some(step.Step) : Optional.None<StepAnalysis>()).FindFirstSome();

    private static TriggerComparison CompareTrigger(string label, Optional<StepAnalysis> left, Optional<StepAnalysis> right)
    {
        var leftSetOff = left.Match(step => step.Value.SetsOff, _ => []);
        var rightSetOff = right.Match(step => step.Value.SetsOff, _ => []);
        var bothPresent = left.IsSome() && right.IsSome();
        return new TriggerComparison(
            label,
            left,
            right,
            bothPresent ? [.. leftSetOff.Where(element => !rightSetOff.Any(other => other.Source == element.Source))] : [],
            bothPresent ? [.. rightSetOff.Where(element => !leftSetOff.Any(other => other.Source == element.Source))] : []);
    }
}
