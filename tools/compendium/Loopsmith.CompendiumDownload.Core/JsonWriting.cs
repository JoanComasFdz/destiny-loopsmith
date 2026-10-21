using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>
/// JSON as the snapshot's files hold it: <c>", "</c> and <c>": "</c> between items, or one item a line indented by 2;
/// with <paramref name="asciiOnly"/> every character outside printable ASCII escaped (<c>\u00e9</c>), else only quotes,
/// backslashes and control characters.
/// </summary>
public static class JsonWriting
{
    /// <summary>An object of strings, in the order given.</summary>
    public static string WriteObject(IEnumerable<(string Key, string Value)> members, bool asciiOnly, bool indented)
    {
        var parts = members.Select(member => $"{WriteString(member.Key, asciiOnly)}: {WriteString(member.Value, asciiOnly)}").ToImmutableArray();
        return WriteBraces(parts, "{", "}", indented);
    }

    /// <summary>An array of objects of strings, on one line.</summary>
    public static string WriteArrayOfObjects(IEnumerable<IEnumerable<(string Key, string Value)>> items, bool asciiOnly) =>
        WriteBraces([.. items.Select(item => WriteObject(item, asciiOnly, indented: false))], "[", "]", indented: false);

    public static string WriteString(string text, bool asciiOnly)
    {
        var json = new StringBuilder("\"");
        foreach (var character in text)
        {
            json.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\b' => "\\b",
                '\f' => "\\f",
                < ' ' => EscapeCharacter(character),
                > '~' when asciiOnly => EscapeCharacter(character),
                _ => character.ToString(),
            });
        }

        return json.Append('"').ToString();
    }

    private static string WriteBraces(ImmutableArray<string> parts, string open, string close, bool indented) =>
        (parts.IsEmpty, indented) switch
        {
            (true, _) => open + close,
            (false, true) => $"{open}\n  {string.Join(",\n  ", parts)}\n{close}",
            (false, false) => $"{open}{string.Join(", ", parts)}{close}",
        };

    private static string EscapeCharacter(char character) => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture);
}
