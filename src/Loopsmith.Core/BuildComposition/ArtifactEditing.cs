using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: the artifacts a build can equip (Artifacts 2.0: every artifact of the manifest excerpt with perk slots), the one
/// it equips with the perk in each of its slots, what a slot can take, and a build with another artifact and perks. An
/// artifact's perks come in tiers, and each of its perk slots takes the tiers up to its own (the excerpt's <c>slots</c>:
/// two take Tier 1, three Tiers 1–2, two Tiers 1–3), so a slot only ever holds a perk it takes and nothing is counted. A
/// perk the rules know is the build's element; any other a left-out artifact perk (⊘).
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

    /// <summary>
    /// The build's artifact (<see cref="ArtifactFinding.FindArtifact"/>) and the perk in each of its slots: the slots the
    /// build names when they hold its perks, each in a slot that takes it; else its perks placed (<see cref="PlacePerks"/>:
    /// a build file names its perks, not their slots). No artifact: no slots.
    /// </summary>
    public static ArtifactChoice ReadChoice(RuleCatalog catalog, Build build) =>
        ArtifactFinding.FindArtifact(catalog, build).Match(
            found => found.Value.Item.Kind is ManifestKind.Artifact artifact
                ? new ArtifactChoice(Optional.Some(found.Value.Item.Hash), ReadSlots(artifact, build.Artifact, [.. found.Value.Perks.Select(perk => perk.Hash)]))
                : new ArtifactChoice(Optional.None<ItemHash>(), []),
            _ => new ArtifactChoice(Optional.None<ItemHash>(), []));

    /// <summary>The slots the build names when they fit the artifact and hold exactly its perks, else its perks placed.</summary>
    private static ImmutableArray<Optional<ItemHash>> ReadSlots(ManifestKind.Artifact artifact, Optional<ArtifactLoadout> named, ImmutableArray<ItemHash> perks) =>
        named
            .Bind(loadout => FitsSlots(artifact, loadout.Slots) && ListHeld(loadout.Slots).ToHashSet().SetEquals(perks)
                ? Optional.Some(loadout.Slots)
                : Optional.None<ImmutableArray<Optional<ItemHash>>>())
            .UnwrapOr(PlacePerks(artifact, perks));

    /// <summary>The tier a perk is in (1-based); none for a perk the artifact doesn't have.</summary>
    public static Optional<int> FindTier(ManifestKind.Artifact artifact, ItemHash perk) =>
        artifact.Tiers
            .Select((tier, index) => tier.Perks.Contains(perk) ? Optional.Some(index + 1) : Optional.None<int>())
            .FindFirstSome();

    /// <summary>What slot number <paramref name="slot"/> (0-based) can hold: every perk of the tiers it takes, Tier 1's first.</summary>
    public static ImmutableArray<ItemHash> ListSlotOptions(ManifestKind.Artifact artifact, int slot) =>
        slot >= 0 && slot < artifact.Slots.Length
            ? [.. artifact.Tiers.Take(artifact.Slots[slot]).SelectMany(tier => tier.Perks).Distinct()]
            : [];

    /// <summary>
    /// The perks in the artifact's slots, in slot order (none: an empty slot): the highest tiers first, each in the free
    /// slot that takes the fewest tiers (in the order given among a tier); a perk no free slot takes, or the artifact
    /// doesn't have, is left out.
    /// </summary>
    public static ImmutableArray<Optional<ItemHash>> PlacePerks(ManifestKind.Artifact artifact, ImmutableArray<ItemHash> perks) =>
        perks
            .Select((perk, index) => (Perk: perk, Tier: FindTier(artifact, perk), Index: index))
            .OrderByDescending(entry => entry.Tier.UnwrapOr(0))
            .ThenBy(entry => entry.Index)
            .Aggregate(
                ListEmptySlots(artifact),
                (slots, next) => next.Tier.Bind(tier => FindFreeSlot(artifact, slots, tier))
                    .Match(at => slots.SetItem(at.Value, Optional.Some(next.Perk)), _ => slots));

    /// <summary>The free slot that takes this tier and the fewest tiers, the first such.</summary>
    private static Optional<int> FindFreeSlot(ManifestKind.Artifact artifact, ImmutableArray<Optional<ItemHash>> slots, int tier) =>
        Enumerable.Range(0, slots.Length)
            .Where(at => !slots[at].IsSome() && artifact.Slots[at] >= tier)
            .OrderBy(at => artifact.Slots[at])
            .ThenBy(at => at)
            .Select(Optional.Some)
            .FindFirstSome();

    private static ImmutableArray<Optional<ItemHash>> ListEmptySlots(ManifestKind.Artifact artifact) =>
        [.. artifact.Slots.Select(_ => Optional.None<ItemHash>())];

    /// <summary>True when there is one entry per slot, each perk the artifact's, in a slot that takes its tier, and none twice.</summary>
    private static bool FitsSlots(ManifestKind.Artifact artifact, ImmutableArray<Optional<ItemHash>> slots) =>
        slots.Length == artifact.Slots.Length
        && slots.Select((slot, at) => slot.Match(perk => FindTier(artifact, perk.Value).Match(tier => tier.Value <= artifact.Slots[at], _ => false), _ => true)).All(fits => fits)
        && ListHeld(slots).Distinct().Count() == ListHeld(slots).Length;

    private static ImmutableArray<ItemHash> ListHeld(ImmutableArray<Optional<ItemHash>> slots) =>
        [.. slots.SelectMany(slot => Flatten(slot))];

    /// <summary>The rules' artifact perk with this hash; none: the rules don't know it (it is left out, ⊘).</summary>
    public static Optional<BuildElement> FindPerkElement(RuleCatalog catalog, ItemHash perk) =>
        catalog.FindElementByHash(perk).Bind(element => element.Kind == ElementKind.ArtifactPerk ? Optional.Some(element) : Optional.None<BuildElement>());

    /// <summary>
    /// The slots to show for an artifact: the last picked for it (<paramref name="remembered"/>, by artifact), else the
    /// build's own when it is the build's artifact, else empty ones.
    /// </summary>
    public static ImmutableArray<Optional<ItemHash>> RecallSlots(RuleCatalog catalog, Build build, ImmutableDictionary<ItemHash, ImmutableArray<Optional<ItemHash>>> remembered, ItemHash artifact)
    {
        var held = ReadChoice(catalog, build);
        return remembered.TryGetValue(artifact, out var slots)
            ? slots
            : held.Artifact == Optional.Some(artifact)
                ? held.Slots
                : FindArtifact(catalog, artifact).Match(found => ListEmptySlots(found.Value), _ => ImmutableArray<Optional<ItemHash>>.Empty);
    }

    /// <summary>
    /// The build with this artifact and the perk in each of its slots, or why it can't hold them (an artifact the excerpt
    /// doesn't have, not one entry per slot, a perk that isn't the artifact's, in a slot that doesn't take its tier or
    /// picked twice, perks without an artifact).
    /// </summary>
    public static Result<Build, string> ApplyChoice(RuleCatalog catalog, Build build, ArtifactChoice choice)
    {
        var held = ListHeld(choice.Slots);
        if (choice.Artifact is not Optional<ItemHash>.Some picked)
        {
            return held.IsEmpty
                ? new Result<Build, string>.Ok(Write(catalog, build, Optional.None<ArtifactLoadout>()))
                : Refuse("Artifact perks need an artifact.");
        }

        var name = DescribeItem(catalog, picked.Value);
        if (FindArtifact(catalog, picked.Value) is not Optional<ManifestKind.Artifact>.Some artifact)
        {
            return Refuse($"{name} isn't an artifact Loopsmith knows.");
        }

        if (choice.Slots.Length != artifact.Value.Slots.Length)
        {
            return Refuse($"{name} has {artifact.Value.Slots.Length} perk slots, not {choice.Slots.Length}.");
        }

        var foreign = held.Where(perk => !FindTier(artifact.Value, perk).IsSome()).ToImmutableArray();
        if (!foreign.IsEmpty)
        {
            return Refuse($"{DescribeItem(catalog, foreign[0])} isn't one of {name}'s perks.");
        }

        if (held.Distinct().Count() != held.Length)
        {
            return Refuse("An artifact perk can be picked once.");
        }

        var misplaced = Enumerable.Range(0, choice.Slots.Length)
            .Where(at => choice.Slots[at].Match(perk => FindTier(artifact.Value, perk.Value).UnwrapOr(0) > artifact.Value.Slots[at], _ => false))
            .ToImmutableArray();
        if (!misplaced.IsEmpty)
        {
            var at = misplaced[0];
            var perk = choice.Slots[at].Match(found => found.Value, _ => picked.Value);
            return Refuse($"{DescribeItem(catalog, perk)} is a Tier {FindTier(artifact.Value, perk).UnwrapOr(0)} perk: slot {at + 1} takes up to Tier {artifact.Value.Slots[at]}.");
        }

        return new Result<Build, string>.Ok(Write(catalog, build, Optional.Some(new ArtifactLoadout(picked.Value, choice.Slots))));
    }

    /// <summary>
    /// The build holding the artifact and its slots: a perk the rules know is its element, any other a left-out artifact
    /// perk, each in slot order.
    /// </summary>
    private static Build Write(RuleCatalog catalog, Build build, Optional<ArtifactLoadout> artifact)
    {
        var perks = artifact.Match(found => ListHeld(found.Value.Slots), _ => ImmutableArray<ItemHash>.Empty);
        return build with
        {
            Artifact = artifact,
            ArtifactPerks = [.. perks.SelectMany(perk => Flatten(FindPerkElement(catalog, perk).Map(element => element.Id)))],
            LeftOut =
            [
                .. build.LeftOut.Where(item => item.Part != LoadoutPart.ArtifactPerk),
                .. perks.Where(perk => !FindPerkElement(catalog, perk).IsSome()).Select(perk => new LeftOutItem(LoadoutPart.ArtifactPerk, perk)),
            ],
        };
    }

    private static IEnumerable<T> Flatten<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);

    private static string DescribeItem(RuleCatalog catalog, ItemHash hash) =>
        catalog.Manifest.FindItem(hash).Match(item => item.Value.Name, _ => $"Item {hash.Value}");

    private static Result<Build, string> Refuse(string why) =>
        new Result<Build, string>.Error(why);
}
