using System.Collections.Immutable;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.Phrasing;

/// <summary>How a span should look. The CLI host maps tones to colours; tests read the plain text.</summary>
public enum Tone { Plain, Muted, Strong, Warning, Kinetic, Arc, Solar, Void, Stasis, Strand, Prismatic }

public sealed record StyledSpan(string Text, Tone Tone);

public sealed record StyledLine(int Indent, ImmutableArray<StyledSpan> Spans);

/// <summary>An outcome to describe, the copies of its element equipped, and a certainty marker ("*" or "").</summary>
public sealed record OutcomeMention(Outcome Outcome, int Copies, string Marker);

public static class StyledText
{
    public static StyledSpan ToSpan(this string text, Tone tone = Tone.Plain) => new(text, tone);

    public static StyledLine ToLine(int indent, params StyledSpan[] spans) => new(indent, [.. spans]);

    public static StyledLine ToLine(this string text, Tone tone = Tone.Plain, int indent = 0) => new(indent, [new StyledSpan(text, tone)]);

    public static Tone ToTone(this Affinity affinity) =>
        affinity switch
        {
            Affinity.Arc => Tone.Arc,
            Affinity.Solar => Tone.Solar,
            Affinity.Void => Tone.Void,
            Affinity.Stasis => Tone.Stasis,
            Affinity.Strand => Tone.Strand,
            Affinity.Prismatic => Tone.Prismatic,
            Affinity.Kinetic => Tone.Kinetic,
            _ => Tone.Plain,
        };

    public static string ToPlainText(this StyledLine line) =>
        new string(' ', line.Indent * 2) + string.Concat(line.Spans.Select(span => span.Text));

    public static string ToPlainText(this IEnumerable<StyledLine> lines) =>
        string.Join("\n", lines.Select(line => line.ToPlainText().TrimEnd()));
}
