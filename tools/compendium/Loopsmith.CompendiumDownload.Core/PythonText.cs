using System.Globalization;
using System.Text;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>
/// The few string operations where Python's and .NET's answers differ, done Python's way, so a snapshot comes out the
/// same as the one sheet_dump.py (the Python version this tool replaced) wrote: what counts as whitespace, slicing by
/// characters, rounding a size to a decimal.
/// </summary>
public static class PythonText
{
    /// <summary>Python's <c>str.strip()</c>: trims what <c>str.isspace</c> holds, which also counts U+001C–U+001F.</summary>
    public static string StripWhitespace(string text) => text.Trim(WhitespaceCharacters);

    /// <summary>Python's <c>s[:count]</c>: the first <paramref name="count"/> characters (code points, not UTF-16 units).</summary>
    public static string TakeCharacters(string text, int count)
    {
        var runes = text.EnumerateRunes().Take(count);
        return string.Concat(runes.Select(rune => rune.ToString()));
    }

    /// <summary>Python's <c>len(s)</c>: how many characters (code points).</summary>
    public static int CountCharacters(string text) => text.EnumerateRunes().Count();

    /// <summary>
    /// Python's <c>round(bytes / 4 / 1000, 1)</c> written as Python prints it ("12.3"): the double nearest the value, then
    /// its nearest tenth, with a tie to the even one.
    /// </summary>
    public static string FormatThousandsToTenths(long bytes)
    {
        var tenths = RoundExactly(bytes / 4.0 / 1000.0, 10, bytes / 400);
        return $"{tenths / 10}.{tenths % 10}";
    }

    /// <summary>Python's <c>f"{bytes / 4 / 1000:.0f}"</c>: the double nearest the value, then its nearest whole number, a tie to the even one.</summary>
    public static string FormatThousandsToWhole(long bytes) =>
        RoundExactly(bytes / 4.0 / 1000.0, 1, bytes / 4000).ToString(CultureInfo.InvariantCulture);

    /// <summary>Python's <c>repr(text)</c>, near enough for the console: in single quotes, or double ones when it holds a single one.</summary>
    public static string QuoteForConsole(string text) =>
        text.Contains('\'') && !text.Contains('"')
            ? $"\"{text.Replace("\\", "\\\\")}\""
            : $"'{text.Replace("\\", "\\\\").Replace("'", "\\'")}'";

    /// <summary>
    /// Python's <c>html.escape(text)</c>: <c>&amp; &lt; &gt; " '</c> as entities.
    /// </summary>
    public static string EscapeHtml(string text) =>
        new StringBuilder(text).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#x27;").ToString();

    /// <summary>
    /// The nearest whole number to <paramref name="value"/> × <paramref name="scale"/>, compared exactly (the double's own
    /// value, not a rounded product): <paramref name="floor"/> is the exact quotient's whole part, so the answer is it or
    /// the next one.
    /// </summary>
    private static long RoundExactly(double value, long scale, long floor)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 0x7FF);
        var mantissa = bits & 0xFFFFFFFFFFFFFL;
        var (significand, power) = exponent == 0 ? (mantissa, -1074) : (mantissa | (1L << 52), exponent - 1075);
        // value × scale − (floor + ½), times 2: significand × 2^power × 2 × scale − (2 × floor + 1)
        var left = new System.Numerics.BigInteger(significand) * 2 * scale;
        var right = new System.Numerics.BigInteger(2 * floor + 1);
        var sign = power >= 0
            ? (left << power).CompareTo(right)
            : left.CompareTo(right << -power);
        return sign switch
        {
            > 0 => floor + 1,
            < 0 => floor,
            _ => floor % 2 == 0 ? floor : floor + 1,
        };
    }

    private static readonly char[] WhitespaceCharacters =
    [
        '\t', '\n', '\v', '\f', '\r', '\u001C', '\u001D', '\u001E', '\u001F', ' ',
        '\u0085', '\u00A0', '\u1680', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200A', '\u2028', '\u2029', '\u202F', '\u205F', '\u3000',
    ];
}
