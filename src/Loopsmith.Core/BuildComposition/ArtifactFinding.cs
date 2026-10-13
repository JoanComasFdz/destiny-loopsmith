using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: a build's artifact, by the manifest excerpt: the one it names (<c>artifact:</c>, picked in the designer's artifact
/// editor), else — a build that names artifact perks, not the artifact (a build file, a DIM link) — the one that unlocks
/// the most of its perks. Artifacts share perks (Horde Shuttle is both the Tablet of Ruin's and the Implement of
/// Curiosity's), so each perk counts once, by any of its hashes, and there is none when two unlock as many: no guess.
/// </summary>
public static class ArtifactFinding
{
    public static Optional<BuildArtifact> FindArtifact(RuleCatalog catalog, Build build)
    {
        var perks = ListArtifactPerks(catalog, build);
        return build.Artifact.Match(
            named => catalog.Manifest.FindItem(named.Value).Bind(item => item.Kind is ManifestKind.Artifact artifact
                ? Optional.Some(new BuildArtifact(item, MatchPerks(artifact, perks)))
                : Optional.None<BuildArtifact>()),
            _ => InferArtifact(catalog, perks));
    }

    /// <summary>The artifact that unlocks the most of the perks; none when none does or two unlock as many.</summary>
    private static Optional<BuildArtifact> InferArtifact(RuleCatalog catalog, ImmutableArray<(Optional<ElementId> Element, ImmutableArray<ItemHash> Hashes)> perks)
    {
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
        .. perks.SelectMany(perk => perk.Hashes.Where(hash => artifact.Tiers.Any(tier => tier.Perks.Contains(hash))).Take(1).Select(hash => new ArtifactPerkOfBuild(hash, perk.Element))),
    ];
}
