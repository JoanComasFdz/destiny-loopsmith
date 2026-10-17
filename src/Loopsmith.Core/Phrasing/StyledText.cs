using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Phrasing;

/// <summary>How a span should look. The host maps tones to colours (CSS classes); tests read the plain text.</summary>
public enum Tone { Plain, Muted, Strong, Warning, Kinetic, Arc, Solar, Void, Stasis, Strand, Prismatic }

public sealed record StyledSpan(string Text, Tone Tone);

public sealed record StyledLine(int Indent, ImmutableArray<StyledSpan> Spans);

/// <summary>
/// An outcome to describe and the copies of its element equipped; <see cref="Spend"/>: the stacks it spent where it was
/// applied, and what they gave (ADRs D33), none for a rule's outcome described on its own.
/// </summary>
public sealed record OutcomeMention(Outcome Outcome, CopyCount Copies, Optional<StackSpend> Spend)
{
    public OutcomeMention(Outcome outcome, CopyCount copies)
        : this(outcome, copies, Optional.None<StackSpend>())
    {
    }
}

/// <summary>An outcome and the phrase that describes it ("+1 Bolt Charge").</summary>
public sealed record DescribedOutcome(Outcome Outcome, string Text);

/// <summary>A keyword a phrase names, in its affinity.</summary>
public sealed record KeywordName(KeywordRef Keyword, Affinity Affinity);

/// <summary>A piece of a phrase: plain text, or a keyword's name ("Bolt Charge", "Jolted").</summary>
public sealed record PhrasePart(string Text, Optional<KeywordName> Keyword);

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

    /// <summary>A keyword's id as its rules write it ("bolt-charge", "orb-of-power").</summary>
    public static string DescribeKeywordId(this KeywordRef keyword) =>
        keyword.Match(status => status.Id.Value, pickup => pickup.Id.Value, summon => summon.Id.Value);

    /// <summary>A name highlighted in its affinity's tone; a neutral one (Orb of Power) strong, so it still stands out.</summary>
    public static Tone ToHighlightTone(this Affinity affinity) =>
        affinity == Affinity.Neutral ? Tone.Strong : affinity.ToTone();

    public static string ToPlainText(this StyledLine line) =>
        new string(' ', line.Indent * 2) + string.Concat(line.Spans.Select(span => span.Text));

    public static string ToPlainText(this IEnumerable<StyledLine> lines) =>
        string.Join("\n", lines.Select(line => line.ToPlainText().TrimEnd()));
}
