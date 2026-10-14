using System.Collections.Immutable;
using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.BuildComposition;

/// <summary>A build's artifact: the one that unlocks the most of its artifact perks, each perk counted once; no guess on a tie.</summary>
public sealed class ArtifactFindingTests
{
    [Fact]
    public void The_artifact_that_unlocks_the_most_of_the_builds_perks_is_its_artifact()
    {
        var (catalog, build) = Arrange([("shared", [1u]), ("own", [2u])], ("Tablet", [1u, 2u]), ("Implement", [1u]));

        var artifact = Assert.IsType<Optional<BuildArtifact>.Some>(ArtifactFinding.FindArtifact(catalog, build)).Value;

        Assert.Equal(("Tablet", 2), (artifact.Item.Name, artifact.Perks.Length));
    }

    [Fact]
    public void Two_artifacts_that_unlock_as_many_of_its_perks_give_none()
    {
        var (catalog, build) = Arrange([("shared", [1u])], ("Tablet", [1u, 2u]), ("Implement", [1u]));

        Assert.False(ArtifactFinding.FindArtifact(catalog, build).IsSome());
    }

    [Fact]
    public void A_perk_with_two_copies_in_one_artifact_counts_once()
    {
        var (catalog, build) = Arrange([("copied", [1u, 3u]), ("other", [2u])], ("Copies", [1u, 3u]), ("Tablet", [1u, 2u]));

        var artifact = Assert.IsType<Optional<BuildArtifact>.Some>(ArtifactFinding.FindArtifact(catalog, build)).Value;

        Assert.Equal("Tablet", artifact.Item.Name);                                             // counted twice, "Copies" would tie it
        Assert.Equal([1u, 2u], artifact.Perks.Select(perk => perk.Hash.Value));
    }

    /// <summary>A build with these artifact perks (an id and its hashes) and an excerpt with these artifacts (a name and the perk hashes it unlocks).</summary>
    private static (RuleCatalog Catalog, Build Build) Arrange(
        ImmutableArray<(string Id, uint[] Hashes)> perks, params (string Name, uint[] Perks)[] artifacts)
    {
        var validated = ValidateBuild([]);
        var elements = perks.Select(perk => Element(perk.Id, ElementKind.ArtifactPerk, []) with { Hashes = [.. perk.Hashes.Select(ItemHash.From)] });
        var items = artifacts.Select((artifact, index) => new ManifestItem(
            ItemHash.From((uint)(1000 + index)), artifact.Name, new ManifestKind.Artifact([new ArtifactTier([.. artifact.Perks.Select(ItemHash.From)])], [1], Optional.None<string>()),
            Optional.Some("Artifact"), Optional.None<ItemTier>(), Optional.None<string>(), Optional.None<string>(), Optional.None<string>()));
        var catalog = validated.Catalog with
        {
            Elements = elements.Aggregate(validated.Catalog.Elements, (all, element) => all.Add(element.Id, element)),
            Manifest = validated.Catalog.Manifest with { Items = items.ToImmutableDictionary(item => item.Hash) },
        };
        return (catalog, validated.Build with { ArtifactPerks = [.. perks.Select(perk => ElementId.From(perk.Id))] });
    }
}
