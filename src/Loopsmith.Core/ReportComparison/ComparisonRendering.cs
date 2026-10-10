using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.ReportComparison;

/// <summary>
/// A <see cref="LoopComparison"/> as styled lines: each loop's order and verdict, then trigger by trigger its step in
/// each loop, where its needs come from there, and the elements that fire in only one of them.
/// </summary>
public static class ComparisonRendering
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private const string Indent = "   ";

    private const int FieldWidth = 12;

    public static ImmutableArray<StyledLine> RenderComparison(LoopComparison comparison)
    {
        var left = comparison.Left;
        var right = comparison.Right;
        var nameWidth = Math.Max(left.LoopName.Length, right.LoopName.Length) + 3;
        var labelWidth = comparison.Triggers.Select(trigger => trigger.Label.Length).DefaultIfEmpty(0).Max() + 2;
        return
        [
            RenderOrder("A", left, nameWidth),
            RenderOrder("B", right, nameWidth),
            StyledText.ToLine(0, "".ToSpan()),
            .. comparison.Triggers.SelectMany(trigger => RenderTrigger(trigger, labelWidth)),
        ];
    }

    private static StyledLine RenderOrder(string side, LoopReport report, int nameWidth) =>
        StyledText.ToLine(0,
            $"{side}  ".ToSpan(Tone.Muted),
            report.LoopName.PadRight(nameWidth).ToSpan(Tone.Strong),
            $"{string.Join(" → ", report.StepLabels)}   ".ToSpan(Tone.Plain),
            report.Verdict.DescribeShortVerdict().ToSpan(report.Verdict is LoopVerdict.Breaks ? Tone.Warning : Tone.Strong),
            $"   ({report.BuildName})".ToSpan(Tone.Muted));

    /// <summary>The trigger's step in each loop, then where its needs come from there and what fires in only one of them.</summary>
    private static ImmutableArray<StyledLine> RenderTrigger(TriggerComparison trigger, int labelWidth)
    {
        var label = trigger.Label.PadRight(labelWidth).ToSpan(Tone.Strong);
        return trigger.Placement.Match(
            both => RenderBoth(label, both),
            left => [new StyledLine(0, [label, $"only in A (#{FormatStepNumber(left.Step.Step)})".ToSpan(Tone.Muted)])],
            right => [new StyledLine(0, [label, $"only in B (#{FormatStepNumber(right.Step.Step)})".ToSpan(Tone.Muted)])]);
    }

    private static ImmutableArray<StyledLine> RenderBoth(StyledSpan label, TriggerPlacement.InBoth both)
    {
        var same = both.OnlyLeft.IsEmpty && both.OnlyRight.IsEmpty;
        var steps = $"A #{FormatStepNumber(both.Left.Step)} · B #{FormatStepNumber(both.Right.Step)}{(same ? ": sets off the same" : "")}";
        return
        [
            new StyledLine(0, [label, steps.ToSpan(Tone.Plain)]),
            .. RenderNeeds("A", both.Left),
            .. RenderNeeds("B", both.Right),
            .. both.OnlyLeft.IsEmpty ? [] : new[] { RenderField("only in A", DescribeMentions(both.OnlyLeft).ToSpan(Tone.Warning)) },
            .. both.OnlyRight.IsEmpty ? [] : new[] { RenderField("only in B", DescribeMentions(both.OnlyRight).ToSpan(Tone.Warning)) },
        ];
    }

    /// <summary>"A needs  Reaper ← #1", and "(differs on A's first pass)" when its first pass does something else there.</summary>
    private static ImmutableArray<StyledLine> RenderNeeds(string side, PlacedStep placed)
    {
        if (placed.Step.Needs.IsEmpty && !placed.DiffersOnFirstPass)
        {
            return [];
        }

        var needs = placed.Step.Needs.IsEmpty ? "nothing from earlier steps" : string.Join(" · ", placed.Step.Needs.Select(need => need.DescribeNeedBriefly()));
        var firstPass = placed.DiffersOnFirstPass ? $" (differs on {side}'s first pass)" : "";
        return [RenderField($"{side} needs", needs.ToSpan(Tone.Plain), firstPass.ToSpan(Tone.Muted))];
    }

    private static StyledLine RenderField(string name, params ImmutableArray<StyledSpan> spans) =>
        new(0, [$"{Indent}{name.PadRight(FieldWidth)}".ToSpan(Tone.Muted), .. spans]);

    private static string DescribeMentions(ImmutableArray<ElementMention> mentions) =>
        string.Join(" · ", mentions.Select(mention => mention.DescribeMention()));

    private static string FormatStepNumber(StepAnalysis step) => (step.StepIndex + 1).ToString(Invariant);
}
