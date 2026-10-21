using Loopsmith.Core.Domain;
using Loopsmith.Functional;

namespace Loopsmith.Core.StatLoading;

/// <summary>
/// Pure: the finisher a build's finisher is (docs/loop-format.md, "Next trigger"): after a Bungie login, the one the
/// player's character of the build's class has equipped, when the excerpt (with Bungie's full manifest joined in) names
/// it; else the class's own finisher, the one the game plays when none is equipped (the glossary's).
/// </summary>
public static class FinisherFinding
{
    /// <summary>The finisher's manifest item (its name and icon); none when neither the player's nor the class's is known.</summary>
    public static Optional<ManifestItem> FindFinisher(RuleCatalog catalog, Build build, Optional<BungieProfile> profile) =>
        new[]
        {
            profile.Bind(owned => owned.Finishers
                .Where(finisher => finisher.Class == build.Class)
                .Select(finisher => catalog.Manifest.FindItem(finisher.Hash))
                .FindFirstSome()),
            catalog.FindClassFinisher(build.Class),
        }.FindFirstSome();
}
