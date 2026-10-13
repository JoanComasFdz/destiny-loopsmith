using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: the artifacts a build can equip (Artifacts 2.0: every artifact of the manifest excerpt with perk slots), the one
/// it equips with its perks, the artifact's limits, and a build with another artifact and perks. An artifact's perks come
/// in tiers, and each of its perk slots takes the tiers up to its own (the excerpt's <c>slots</c>: two take Tier 1, three
/// Tiers 1–2, two Tiers 1–3); the perks fit when, for each tier, no more of them are of that tier or higher than there
/// are slots that take it — at most 7 perks, 5 of Tiers 2–3 and 2 of Tier 3. A perk the rules know is the build's
/// element; any other a left-out artifact perk (⊘).
/// </summary>
public static class ArtifactEditing
{
    /// <summary>Every artifact the excerpt has with perk slots, by name.</summary>
    public static ImmutableArray<ManifestItem> ListArtifacts(RuleCatalog catalog) =>
    [
        .. catalog.Manifest.Items.Values
            .Where(item => item.Kind is ManifestKind.Artifact { Tiers.IsEmpty: false, Slots.IsEmpty: false })
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Hash.Value),
    ];

    /// <summary>The artifact with this hash, when the excerpt has it.</summary>
    public static Optional<ManifestKind.Artifact> FindArtifact(RuleCatalog catalog, ItemHash hash) =>
        catalog.Manifest.FindItem(hash).Bind(item => item.Kind is ManifestKind.Artifact artifact ? Optional.Some(artifact) : Optional.None<ManifestKind.Artifact>());

    /// <summary>The build's artifact and its perks of it (<see cref="ArtifactFinding.FindArtifact"/>); no artifact, no perks, when it has none.</summary>
    public static ArtifactChoice ReadChoice(RuleCatalog catalog, Build build) =>
        ArtifactFinding.FindArtifact(catalog, build).Match(
            found => new ArtifactChoice(Optional.Some(found.Value.Item.Hash), [.. found.Value.Perks.Select(perk => perk.Hash)]),
            _ => new ArtifactChoice(Optional.None<ItemHash>(), []));

    /// <summary>The tier a perk is in (1-based); none for a perk the artifact doesn't have.</summary>
    public static Optional<int> FindTier(ManifestKind.Artifact artifact, ItemHash perk) =>
        artifact.Tiers
            .Select((tier, index) => tier.Perks.Contains(perk) ? Optional.Some(index + 1) : Optional.None<int>())
            .FindFirstSome();

    /// <summary>
    /// The artifact's limits with these perks picked: one for each tier its slots start to thin out at (Tier 1: every
    /// slot; then each tier fewer slots take), with how many of the perks count against it.
    /// </summary>
    public static ImmutableArray<ArtifactLimit> CountLimits(ManifestKind.Artifact artifact, ImmutableArray<ItemHash> perks)
    {
        var tiers = perks.SelectMany(perk => Flatten(FindTier(artifact, perk))).ToImmutableArray();
        return
        [
            .. Enumerable.Range(1, artifact.Tiers.Length)
                .Where(from => from == 1 || CountSlots(artifact, from) != CountSlots(artifact, from - 1))
                .Select(from => new ArtifactLimit(from, artifact.Tiers.Length, tiers.Count(tier => tier >= from), CountSlots(artifact, from))),
        ];
    }

    /// <summary>
    /// The limit picking <paramref name="perk"/> as well would break — of those it counts against that are full, the
    /// narrowest (the highest tier) — none when it fits, is picked already or isn't the artifact's.
    /// </summary>
    public static Optional<ArtifactLimit> FindBrokenLimit(ManifestKind.Artifact artifact, ImmutableArray<ItemHash> perks, ItemHash perk) =>
        perks.Contains(perk)
            ? Optional.None<ArtifactLimit>()
            : FindTier(artifact, perk).Bind(tier => CountLimits(artifact, perks)
                .Where(limit => limit.FromTier <= tier && limit.Used >= limit.Capacity)
                .OrderByDescending(limit => limit.FromTier)
                .Select(Optional.Some)
                .FindFirstSome());

    /// <summary>
    /// The perks in the artifact's slots, in slot order (none: an empty slot): the highest tiers first, each in the free
    /// slot that takes the fewest tiers (picked order among a tier); none when they don't fit or one isn't the artifact's.
    /// </summary>
    public static Optional<ImmutableArray<Optional<ItemHash>>> PlacePerks(ManifestKind.Artifact artifact, ImmutableArray<ItemHash> perks)
    {
        var empty = artifact.Slots.Select(_ => Optional.None<ItemHash>()).ToImmutableArray();
        var ordered = perks
            .Select((perk, index) => (Perk: perk, Tier: FindTier(artifact, perk), Index: index))
            .OrderByDescending(entry => entry.Tier.UnwrapOr(0))
            .ThenBy(entry => entry.Index);
        return ordered.Aggregate(
            Optional.Some(empty),
            (placed, next) => placed.Bind(slots => next.Tier.Bind(tier => FindFreeSlot(artifact, slots, tier)
                .Map(at => slots.SetItem(at, Optional.Some(next.Perk))))));
    }

    /// <summary>The free slot that takes this tier and the fewest tiers, the first such.</summary>
    private static Optional<int> FindFreeSlot(ManifestKind.Artifact artifact, ImmutableArray<Optional<ItemHash>> slots, int tier) =>
        Enumerable.Range(0, slots.Length)
            .Where(at => !slots[at].IsSome() && artifact.Slots[at] >= tier)
            .OrderBy(at => artifact.Slots[at])
            .ThenBy(at => at)
            .Select(Optional.Some)
            .FindFirstSome();

    private static int CountSlots(ManifestKind.Artifact artifact, int tier) =>
        artifact.Slots.Count(slot => slot >= tier);

    /// <summary>The rules' artifact perk with this hash; none: the rules don't know it (it is left out, ⊘).</summary>
    public static Optional<BuildElement> FindPerkElement(RuleCatalog catalog, ItemHash perk) =>
        catalog.FindElementByHash(perk).Bind(element => element.Kind == ElementKind.ArtifactPerk ? Optional.Some(element) : Optional.None<BuildElement>());

    /// <summary>
    /// The perks to show for an artifact: the last picked for it (<paramref name="remembered"/>, by artifact), else the
    /// build's own when it is the build's artifact, else none.
    /// </summary>
    public static ImmutableArray<ItemHash> RecallPerks(RuleCatalog catalog, Build build, ImmutableDictionary<ItemHash, ImmutableArray<ItemHash>> remembered, ItemHash artifact)
    {
        var held = ReadChoice(catalog, build);
        return remembered.TryGetValue(artifact, out var perks)
            ? perks
            : held.Artifact == Optional.Some(artifact) ? held.Perks : [];
    }

    /// <summary>
    /// The build with this artifact and these perks, or why it can't hold them (an artifact the excerpt doesn't have, a
    /// perk that isn't the artifact's or is picked twice, perks that don't fit its slots, perks without an artifact).
    /// </summary>
    public static Result<Build, string> ApplyChoice(RuleCatalog catalog, Build build, ArtifactChoice choice)
    {
        if (choice.Artifact is not Optional<ItemHash>.Some picked)
        {
            return choice.Perks.IsEmpty
                ? new Result<Build, string>.Ok(Write(catalog, build, Optional.None<ItemHash>(), []))
                : Refuse("Artifact perks need an artifact.");
        }

        var name = DescribeItem(catalog, picked.Value);
        if (FindArtifact(catalog, picked.Value) is not Optional<ManifestKind.Artifact>.Some artifact)
        {
            return Refuse($"{name} isn't an artifact Loopsmith knows.");
        }

        var foreign = choice.Perks.Where(perk => !FindTier(artifact.Value, perk).IsSome()).ToImmutableArray();
        if (!foreign.IsEmpty)
        {
            return Refuse($"{DescribeItem(catalog, foreign[0])} isn't one of {name}'s perks.");
        }

        if (choice.Perks.Distinct().Count() != choice.Perks.Length)
        {
            return Refuse("An artifact perk can be picked once.");
        }

        if (!PlacePerks(artifact.Value, choice.Perks).IsSome())
        {
            return Refuse($"These perks don't fit {name}'s slots.");
        }

        var ordered = choice.Perks
            .OrderBy(perk => FindTier(artifact.Value, perk).UnwrapOr(0))
            .ThenBy(perk => artifact.Value.Tiers.SelectMany(tier => tier.Perks).ToList().IndexOf(perk))
            .ToImmutableArray();
        return new Result<Build, string>.Ok(Write(catalog, build, Optional.Some(picked.Value), ordered));
    }

    /// <summary>The build holding the artifact and its perks: a perk the rules know is its element, any other a left-out artifact perk.</summary>
    private static Build Write(RuleCatalog catalog, Build build, Optional<ItemHash> artifact, ImmutableArray<ItemHash> perks) =>
        build with
        {
            Artifact = artifact,
            ArtifactPerks = [.. perks.SelectMany(perk => Flatten(FindPerkElement(catalog, perk).Map(element => element.Id)))],
            LeftOut =
            [
                .. build.LeftOut.Where(item => item.Part != LoadoutPart.ArtifactPerk),
                .. perks.Where(perk => !FindPerkElement(catalog, perk).IsSome()).Select(perk => new LeftOutItem(LoadoutPart.ArtifactPerk, perk)),
            ],
        };

    private static IEnumerable<T> Flatten<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);

    private static string DescribeItem(RuleCatalog catalog, ItemHash hash) =>
        catalog.Manifest.FindItem(hash).Match(item => item.Value.Name, _ => $"Item {hash.Value}");

    private static Result<Build, string> Refuse(string why) =>
        new Result<Build, string>.Error(why);
}
