using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A seasonal artifact, as the designer's Artifact box names it. A case is added when a test needs it.</summary>
[Union]
public partial record Artifact
{

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Artifact"/>: the only place those strings live.</summary>
public static class ArtifactShowing
{
    private static readonly Artifact[] Listed = [];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this Artifact value) =>
        value.Match(
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="Artifact.Unlisted"/>.</summary>
    public static Artifact ReadArtifact(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new Artifact.Unlisted(shown);
}
