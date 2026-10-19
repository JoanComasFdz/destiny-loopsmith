using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>An armor set's 2- or 4-piece bonus, as a step card names what it fired. A case is added when a test needs it.</summary>
[Union]
public partial record ArmorSetBonus
{
    public partial record VigilantWatch;
    public partial record IronConviction;
    public partial record IronSharpensIron;
    public partial record AscendantEscape;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="ArmorSetBonus"/>: the only place those strings live.</summary>
public static class ArmorSetBonusShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this ArmorSetBonus value) =>
        value.Match(
            vigilantWatch => "Vigilant Watch",
            ironConviction => "Iron Conviction",
            ironSharpensIron => "Iron Sharpens Iron",
            ascendantEscape => "Ascendant Escape",
            unlisted => unlisted.Shown);
}
