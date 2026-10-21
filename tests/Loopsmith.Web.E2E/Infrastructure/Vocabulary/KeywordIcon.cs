using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>
/// A keyword's icon, drawn before its name (the game's own: rules/glossary.yaml, a status's <c>trait</c>), as the UI
/// shows it: the image it loads. A case is added when a test needs it.
/// </summary>
[Union]
public partial record KeywordIcon
{
    /// <summary>Bungie's Overshield keyword (trait 2485406866), Void Overshield's.</summary>
    public partial record Overshield;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="KeywordIcon"/>: the only place those addresses live.</summary>
public static class KeywordIconShowing
{
    private static readonly KeywordIcon[] Listed = [new KeywordIcon.Overshield()];

    /// <summary>The image the UI loads for <paramref name="value"/>.</summary>
    public static string ToShownText(this KeywordIcon value) =>
        value.Match(
            overshield => "https://www.bungie.net/common/destiny2_content/icons/9aa4002a8b199d7b8c1444d6f0aec030.png",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> image names, or <see cref="KeywordIcon.Unlisted"/>.</summary>
    public static KeywordIcon ReadKeywordIcon(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new KeywordIcon.Unlisted(shown);
}
