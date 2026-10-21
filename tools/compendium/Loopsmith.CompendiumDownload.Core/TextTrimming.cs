namespace Loopsmith.CompendiumDownload.Core;

/// <summary>
/// Trimming and cutting the text that becomes a snapshot's tab files and their names, by Unicode's rules: every
/// whitespace separator trimmed, and characters counted as code points, so a cut never splits a surrogate pair.
/// </summary>
public static class TextTrimming
{
    /// <summary>
    /// The text without the whitespace at either end: what <see cref="string.Trim()"/> trims, and the information
    /// separators U+001C–U+001F too.
    /// </summary>
    public static string TrimWhitespace(string text) => text.Trim(WhitespaceCharacters);

    /// <summary>The first <paramref name="count"/> characters (code points, not UTF-16 units).</summary>
    public static string TakeCharacters(string text, int count)
    {
        var runes = text.EnumerateRunes().Take(count);
        return string.Concat(runes.Select(rune => rune.ToString()));
    }

    /// <summary>How many characters (code points).</summary>
    public static int CountCharacters(string text) => text.EnumerateRunes().Count();

    private static readonly char[] WhitespaceCharacters =
    [
        '\t', '\n', '\v', '\f', '\r', '\u001C', '\u001D', '\u001E', '\u001F', ' ',
        '\u0085', '\u00A0', '\u1680', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200A', '\u2028', '\u2029', '\u202F', '\u205F', '\u3000',
    ];
}
