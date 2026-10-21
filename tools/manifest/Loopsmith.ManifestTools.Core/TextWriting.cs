using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Loopsmith.ManifestTools.Core;

/// <summary>Bungie's text as the excerpt writes it.</summary>
public static partial class TextWriting
{
    /// <summary>
    /// Bungie's text as YAML: without the private-use glyphs of Destiny's icon font (they show as boxes elsewhere), runs
    /// of spaces as one, trimmed, then quoted and ASCII-escaped (<see cref="QuoteText"/>).
    /// </summary>
    public static string ToText(string text) =>
        QuoteText(TrimWhiteSpace(FindSpaceRuns().Replace(FindIconGlyphs().Replace(text, ""), " ")));

    /// <summary>
    /// A text as a JSON string, ASCII only (which YAML reads the same): <c>"</c> and <c>\</c> escaped, the control
    /// characters as <c>\n</c>, <c>\r</c>, <c>\t</c>, <c>\b</c>, <c>\f</c> or <c>\u00XX</c>, and DEL and every character past
    /// ASCII as <c>\uXXXX</c> in lowercase hex (one for each UTF-16 half of a character past U+FFFF).
    /// </summary>
    public static string QuoteText(string text)
    {
        var quoted = new StringBuilder(text.Length + 2).Append('"');
        foreach (var character in text)
        {
            quoted.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\b' => "\\b",
                '\f' => "\\f",
                < ' ' or > '~' => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture),
                _ => character.ToString(),
            });
        }

        return quoted.Append('"').ToString();
    }

    /// <summary>A breaker's name to compare by: hyphens as spaces, trimmed, lowercase ("Shield-Piercing" → "shield piercing").</summary>
    public static string ToBreakerName(string name) => TrimWhiteSpace(name.Replace('-', ' ')).ToLowerInvariant();

    /// <summary>
    /// Trimmed of white space as the Python extractor trimmed it: .NET's white space and the four information separators
    /// (U+001C to U+001F), which Python counts too.
    /// </summary>
    public static string TrimWhiteSpace(string text) =>
        text.Trim().Trim('\u001c', '\u001d', '\u001e', '\u001f') is var trimmed && trimmed.Length != text.Length ? TrimWhiteSpace(trimmed) : text;

    [GeneratedRegex("[-]")]
    private static partial Regex FindIconGlyphs();

    [GeneratedRegex(" {2,}")]
    private static partial Regex FindSpaceRuns();
}
