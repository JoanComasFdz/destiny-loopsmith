using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>An artifact perk, as a step card names what it gave. A case is added when a test needs it.</summary>
[Union]
public partial record ArtifactPerk
{
    public partial record ThunderousRetort;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="ArtifactPerk"/>: the only place those strings live.</summary>
public static class ArtifactPerkShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this ArtifactPerk value) =>
        value.Match(
            thunderousRetort => "Thunderous Retort",
            unlisted => unlisted.Shown);
}
