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
        var matched = leftSteps.Select(step => new TriggerComparison(
            step.Step.Label,
            FindOccurrence(rightSteps, step.Key).Match(
                other => CompareBoth(PlaceStep(left, step.Step), PlaceStep(right, other.Value)),
                _ => new TriggerPlacement.OnlyInLeft(PlaceStep(left, step.Step)))));
        var rightOnly = rightSteps
            .Where(step => !leftSteps.Any(other => other.Key == step.Key))
            .Select(step => new TriggerComparison(step.Step.Label, new TriggerPlacement.OnlyInRight(PlaceStep(right, step.Step))));
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

    private static PlacedStep PlaceStep(LoopReport report, StepAnalysis step) =>
        new(step, report.FirstPassDifferences.Any(difference => difference.StepIndex == step.StepIndex));

    /// <summary>A trigger both loops have, and the elements it sets off in only one of them.</summary>
    private static TriggerPlacement CompareBoth(PlacedStep left, PlacedStep right) =>
        new TriggerPlacement.InBoth(
            left,
            right,
            [.. left.Step.SetsOff.Where(element => !right.Step.SetsOff.Any(other => other.Source == element.Source))],
            [.. right.Step.SetsOff.Where(element => !left.Step.SetsOff.Any(other => other.Source == element.Source))]);
}
