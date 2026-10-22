using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// A <see cref="LoopReport"/> as styled lines: the order, the verdict, then each step of the repeating pass — what it
/// needs and which step provided it, what it sets off in this order (thanks to an earlier step) and on its own, what is
/// wasted — and where the first pass differs.
/// </summary>
public static class LoopReportRendering
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private const string Indent = "   ";

    public static ImmutableArray<StyledLine> RenderLoopReport(LoopReport report) =>
        report.Verdict is LoopVerdict.NoSteps
            ? [RenderTitle(report), StyledText.ToLine(0, report.Verdict.DescribeVerdict().ToSpan(Tone.Warning))]
            :
            [
                RenderTitle(report),
                StyledText.ToLine(0, string.Join(" → ", report.StepLabels).ToSpan(Tone.Plain)),
                StyledText.ToLine(0, report.Verdict.DescribeVerdict().ToSpan(report.Verdict is LoopVerdict.Breaks ? Tone.Warning : Tone.Strong)),
                .. report.Steps.SelectMany(step => RenderStep(step)),
                .. RenderFirstPass(report.FirstPassDifferences),
            ];

    private static readonly StyledLine Blank = StyledText.ToLine(0, "".ToSpan());

    private static StyledLine RenderTitle(LoopReport report) =>
        StyledText.ToLine(0,
            report.LoopName.ToSpan(Tone.Strong),
            $" — {report.BuildName} · {DescribeCount(report.StepLabels.Length, "step")}".ToSpan(Tone.Muted));

    private static ImmutableArray<StyledLine> RenderStep(StepAnalysis step) =>
    [
        Blank,
        StyledText.ToLine(0, $"#{(step.StepIndex + 1).ToString(Invariant)} ".ToSpan(Tone.Muted), step.Label.ToSpan(Tone.Strong)),
        .. step.Blocked.Match(reason => new[] { RenderField("blocked", reason.Value.ToSpan(Tone.Warning)) }, _ => []),
        .. step.NotReady.Match(reason => new[] { RenderField("not ready", reason.Value.ToSpan(Tone.Warning)) }, _ => []),
        .. step.MayNotBeReady.Match(reason => new[] { RenderField("may not be ready", reason.Value.ToSpan(Tone.Warning)) }, _ => []),
        .. step.Needs.IsEmpty ? [] : new[] { RenderField("needs", JoinSpans(step.Needs.Select(need => need.DescribeNeed().ToSpan(need.Affinity.ToTone())))) },
        .. step.InOrder.IsEmpty ? [] : new[] { RenderField("in this order", JoinSpans(step.InOrder.Select(ToMentionSpan))) },
        .. step.OnItsOwn.IsEmpty ? [] : new[] { RenderField("on its own", JoinSpans(step.OnItsOwn.Select(ToMentionSpan))) },
        .. step.Wasted.Select(wasted => RenderField("wasted", [wasted.DescribeWasted().ToSpan(Tone.Warning)])),
    ];

    /// <summary>The steps that do something else on the first pass, from a fresh spawn, than in the repeating pass.</summary>
    private static ImmutableArray<StyledLine> RenderFirstPass(ImmutableArray<PassDifference> differences) =>
        differences.IsEmpty
            ? []
            :
            [
                Blank,
                StyledText.ToLine(0, "First pass ".ToSpan(Tone.Strong), "(from a fresh spawn, where it differs)".ToSpan(Tone.Muted)),
                .. differences.SelectMany(difference => new[]
                    {
                        StyledText.ToLine(0, $"#{(difference.StepIndex + 1).ToString(Invariant)} ".ToSpan(Tone.Muted), difference.Label.ToSpan(Tone.Strong)),
                    }
                    .Concat(difference.BlockedOnFirstPass.Match(reason => new[] { RenderField("blocked", reason.Value.ToSpan(Tone.Warning)) }, _ => []))
                    .Concat(difference.OnlyInRepeatingPass.IsEmpty ? [] : new[] { RenderField("doesn't set off", JoinSpans(difference.OnlyInRepeatingPass.Select(ToMentionSpan))) })
                    .Concat(difference.OnlyOnFirstPass.IsEmpty ? [] : new[] { RenderField("only here", JoinSpans(difference.OnlyOnFirstPass.Select(ToMentionSpan))) })),
            ];

    private static StyledLine RenderField(string name, params ImmutableArray<StyledSpan> spans) =>
        new(0, [$"{Indent}{name.PadRight(FieldWidth)}".ToSpan(Tone.Muted), .. spans]);

    private const int FieldWidth = 17;

    private static StyledSpan ToMentionSpan(ElementMention mention) =>
        mention.DescribeMention().ToSpan(mention.Affinity.ToTone());

    private static ImmutableArray<StyledSpan> JoinSpans(IEnumerable<StyledSpan> spans) =>
        [.. spans.SelectMany((span, index) => index == 0 ? new[] { span } : [" · ".ToSpan(Tone.Muted), span])];

    private static string DescribeCount(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

}
