using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>
/// What a tab's <c>[…]</c> holds. Almost everywhere the PvP value ("25% [10%]"); in Arc's grenade section, by the note
/// heading it, the value with Spark of Magnitude equipped, which holds in PvE too. <c>{…}</c> (a third tier, Armor Mods'
/// activity mods) always holds in PvE.
/// </summary>
public enum BracketMeaning { PvpValue, AlternativeValue }

/// <summary>The numbers a Compendium text states, read as <see cref="StatedNumbers"/>: never its words.</summary>
public static partial class NumberReading
{
    /// <summary>
    /// "Deals 1,500 [250] damage, 25% more for 5+2 s, 0.75x" → PvE 1500, 25 and 0.25, 5, 2, 0.75; all of them and 250.
    /// Thousands separators are read ("1,500"), "↑" marks an enhanced value (kept), a percent is also its fraction.
    /// </summary>
    public static StatedNumbers ReadNumbers(string text, BracketMeaning brackets)
    {
        var pveText = brackets == BracketMeaning.PvpValue ? RemoveBracketed(text) : text;
        return new StatedNumbers(ReadAll(pveText), ReadAll(text));
    }

    private static ImmutableArray<decimal> ReadAll(string text) =>
        [.. NumberPattern().Matches(text)
            .SelectMany(match => ToValues(match.Groups["number"].Value.Replace(",", ""), match.Groups["percent"].Success))
            .Distinct()];

    private static IEnumerable<decimal> ToValues(string number, bool percent)
    {
        var value = decimal.Parse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return percent ? [value, value / 100m] : [value];
    }

    /// <summary>Drops every <c>[…]</c>, nested ones too ("1,245 [530 [690]]" → "1,245 ").</summary>
    private static string RemoveBracketed(string text)
    {
        var kept = new StringBuilder();
        var depth = 0;
        foreach (var character in text)
        {
            depth += character switch { '[' => 1, ']' when depth > 0 => -1, _ => 0 };
            var outside = depth == 0 && character != ']';
            if (outside)
            {
                kept.Append(character);
            }
        }

        return kept.ToString();
    }

    [GeneratedRegex(@"(?<![\w.])(?<number>\d{1,3}(?:,\d{3})+(?!\d)|\d+(?:\.\d+)?)(?<percent>\s?%)?")]
    private static partial Regex NumberPattern();
}
