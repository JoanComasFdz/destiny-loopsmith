using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
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
            .. comparison.Triggers.SelectMany(trigger => RenderTrigger(trigger, left, right, labelWidth)),
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
    private static ImmutableArray<StyledLine> RenderTrigger(TriggerComparison trigger, LoopReport left, LoopReport right, int labelWidth)
    {
        var label = trigger.Label.PadRight(labelWidth).ToSpan(Tone.Strong);
        return (trigger.Left, trigger.Right) switch
        {
            (Optional<StepAnalysis>.Some l, Optional<StepAnalysis>.Some r) => RenderBoth(trigger, label, l.Value, r.Value, left, right),
            (Optional<StepAnalysis>.Some l, _) => [new StyledLine(0, [label, $"only in A (#{Number(l.Value)})".ToSpan(Tone.Muted)])],
            (_, Optional<StepAnalysis>.Some r) => [new StyledLine(0, [label, $"only in B (#{Number(r.Value)})".ToSpan(Tone.Muted)])],
            _ => [],
        };
    }

    private static ImmutableArray<StyledLine> RenderBoth(
        TriggerComparison trigger, StyledSpan label, StepAnalysis left, StepAnalysis right, LoopReport leftReport, LoopReport rightReport)
    {
        var same = trigger.OnlyLeft.IsEmpty && trigger.OnlyRight.IsEmpty;
        return
        [
            new StyledLine(0, [label, $"A #{Number(left)} · B #{Number(right)}{(same ? ": sets off the same" : "")}".ToSpan(Tone.Plain)]),
            .. RenderNeeds("A", left, leftReport),
            .. RenderNeeds("B", right, rightReport),
            .. trigger.OnlyLeft.IsEmpty ? [] : new[] { RenderField("only in A", DescribeMentions(trigger.OnlyLeft).ToSpan(Tone.Warning)) },
            .. trigger.OnlyRight.IsEmpty ? [] : new[] { RenderField("only in B", DescribeMentions(trigger.OnlyRight).ToSpan(Tone.Warning)) },
        ];
    }

    /// <summary>"A needs  Reaper ← #1", and "(differs on A's first pass)" when its first pass does something else there.</summary>
    private static ImmutableArray<StyledLine> RenderNeeds(string side, StepAnalysis step, LoopReport report)
    {
        var differs = report.FirstPassDifferences.Any(difference => difference.StepIndex == step.StepIndex);
        if (step.Needs.IsEmpty && !differs)
        {
            return [];
        }

        var needs = step.Needs.IsEmpty ? "nothing from earlier steps" : string.Join(" · ", step.Needs.Select(need => need.DescribeNeedBriefly()));
        return [RenderField($"{side} needs", needs.ToSpan(Tone.Plain), (differs ? $" (differs on {side}'s first pass)" : "").ToSpan(Tone.Muted))];
    }

    private static StyledLine RenderField(string name, params ImmutableArray<StyledSpan> spans) =>
        new(0, [$"{Indent}{name.PadRight(FieldWidth)}".ToSpan(Tone.Muted), .. spans]);

    private static string DescribeMentions(ImmutableArray<ElementMention> mentions) =>
        string.Join(" · ", mentions.Select(mention => mention.DescribeMention()));

    private static string Number(StepAnalysis step) => (step.StepIndex + 1).ToString(Invariant);
}
