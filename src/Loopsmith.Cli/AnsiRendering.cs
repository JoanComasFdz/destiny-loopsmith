using Loopsmith.Core.Phrasing;

namespace Loopsmith.Cli;

/// <summary>Pure: styled lines → terminal text. Palette from the design proposal.</summary>
public static class AnsiRendering
{
    private const string Reset = "\u001b[0m";

    public static string ToTerminalText(StyledLine line, bool useColor)
    {
        var indent = new string(' ', line.Indent * 2);
        var body = string.Concat(line.Spans.Select(span => useColor ? ToColored(span) : span.Text));
        return indent + body;
    }

    private static string ToColored(StyledSpan span) =>
        span.Tone == Tone.Plain || span.Text.Length == 0 ? span.Text : ToEscape(span.Tone) + span.Text + Reset;

    private static string ToEscape(Tone tone) =>
        tone switch
        {
            Tone.Strong => "\u001b[1m",
            Tone.Muted => Rgb(0x8a, 0x94, 0xa8),
            Tone.Warning => Rgb(0xff, 0xcf, 0x5a),
            Tone.Arc => Rgb(0x6f, 0xe3, 0xff),
            Tone.Solar => Rgb(0xff, 0x9a, 0x3c),
            Tone.Void => Rgb(0xb0, 0x7c, 0xff),
            Tone.Stasis => Rgb(0x5a, 0xa0, 0xff),
            Tone.Strand => Rgb(0x4e, 0xe2, 0x8a),
            Tone.Prismatic => Rgb(0xff, 0x5f, 0xa2),
            Tone.Kinetic => Rgb(0xe7, 0xea, 0xf0),
            _ => "",
        };

    private static string Rgb(int r, int g, int b) => $"\u001b[38;2;{r};{g};{b}m";
}
