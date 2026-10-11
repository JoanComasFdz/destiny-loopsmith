using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: a build's artifact, by the manifest excerpt. A build names artifact perks, not the artifact, and artifacts share
/// perks (Horde Shuttle is both the Tablet of Ruin's and the Implement of Curiosity's), so the build's artifact is the one
/// that unlocks the most of its perks — each perk counted once, by any of its hashes — and none when two unlock as many:
/// no guess.
/// </summary>
public static class ArtifactFinding
{
    public static Optional<BuildArtifact> FindArtifact(RuleCatalog catalog, Build build)
    {
        var perks = ListArtifactPerks(catalog, build);
        var candidates = catalog.Manifest.Items.Values
            .SelectMany(item => item.Kind is ManifestKind.Artifact artifact ? [new BuildArtifact(item, MatchPerks(artifact, perks))] : Array.Empty<BuildArtifact>())
            .Where(candidate => !candidate.Perks.IsEmpty)
            .OrderByDescending(candidate => candidate.Perks.Length)
            .ToImmutableArray();
        return candidates.Length == 0 || (candidates.Length > 1 && candidates[1].Perks.Length == candidates[0].Perks.Length)
            ? Optional.None<BuildArtifact>()
            : Optional.Some(candidates[0]);
    }

    /// <summary>The build's artifact perks: its elements (each with its hashes), then what the DIM loadout had that the rules don't know.</summary>
    private static ImmutableArray<(Optional<ElementId> Element, ImmutableArray<ItemHash> Hashes)> ListArtifactPerks(RuleCatalog catalog, Build build) =>
    [
        .. build.ArtifactPerks.Select(id => (Optional.Some(id), catalog.Elements.TryGetValue(id, out var element) ? element.Hashes : [])),
        .. build.LeftOut.Where(entry => entry.Part == LoadoutPart.ArtifactPerk).Select(entry => (Optional.None<ElementId>(), ImmutableArray.Create(entry.Hash))),
    ];

    /// <summary>The build's perks this artifact unlocks, each by the first of its hashes the artifact has.</summary>
    private static ImmutableArray<ArtifactPerkOfBuild> MatchPerks(ManifestKind.Artifact artifact, ImmutableArray<(Optional<ElementId> Element, ImmutableArray<ItemHash> Hashes)> perks) =>
    [
        .. perks.SelectMany(perk => perk.Hashes.Where(artifact.Perks.Contains).Take(1).Select(hash => new ArtifactPerkOfBuild(hash, perk.Element))),
    ];
}
