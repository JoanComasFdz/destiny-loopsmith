using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// One perk of the artifact editor's columns: its square, its tier, whether it is picked, and why it can't be picked as
/// well (none: it can).
/// </summary>
public sealed record ArtifactPerkCell(ItemHash Perk, IconTile Tile, int Tier, bool Picked, Optional<string> Blocked);

/// <summary>A column of the artifact editor: a tier's heading ("Tier 2") and its perks, in the game's order.</summary>
public sealed record ArtifactColumn(int Tier, string Heading, ImmutableArray<ArtifactPerkCell> Perks);

/// <summary>A group of the slot bar: the perk slots that take the same tiers ("Tier 1–2"), each with its perk (none: empty).</summary>
public sealed record ArtifactSlotGroup(int Tier, string Label, ImmutableArray<Optional<IconTile>> Slots);

/// <summary>One of the artifact's counts ("Tier 3: 2/2"); <see cref="Full"/> when no more perks fit it.</summary>
public sealed record ArtifactCount(string Text, bool Full);

/// <summary>
/// The artifact editor's board for one artifact and the perks picked on it: the tier columns, the slot bar (none when the
/// perks don't fit the slots: a build file can hold more), and the counts.
/// </summary>
public sealed record ArtifactBoard(
    ImmutableArray<ArtifactColumn> Columns,
    Optional<ImmutableArray<ArtifactSlotGroup>> Slots,
    ImmutableArray<ArtifactCount> Counts);

/// <summary>
/// The perk the artifact editor's details show, like the game's tooltip: its square, name and kind ("Artifact Perk ·
/// Tier 2"), its state ("Active", "Requirements not met" and why, or what tapping does), and whether the rules know it.
/// </summary>
public sealed record ArtifactPerkDetails(IconTile Tile, string Kind, string State, Optional<string> Blocked, bool Picked);

/// <summary>
/// The artifact editor's Save: the artifact and its perks, and the perks last picked for each artifact while the loop is
/// open (switching to an artifact shows them again).
/// </summary>
public sealed record ArtifactSave(ArtifactChoice Choice, ImmutableDictionary<ItemHash, ImmutableArray<ItemHash>> Remembered);

/// <summary>Pure: the artifact editor's board and details, from the Orchestration API.</summary>
public static class ArtifactShaping
{
    /// <summary>The board of the artifact with these perks picked; none when the excerpt doesn't have the artifact.</summary>
    public static Optional<ArtifactBoard> ShapeBoard(ValidatedBuild build, ItemHash artifact, ImmutableArray<ItemHash> perks) =>
        LoopDesigning.FindArtifactTiers(build, artifact).Map(found => new ArtifactBoard(
            [.. found.Tiers.Select((tier, index) => new ArtifactColumn(
                index + 1,
                DomainPhrasing.DescribeArtifactTier(index + 1),
                [.. tier.Perks.Select(perk => DescribeCell(build, found, perks, perk, index + 1))]))],
            LoopDesigning.PlaceArtifactPerks(found, perks).Map(placed => GroupSlots(build, found, placed)),
            [.. LoopDesigning.CountArtifactLimits(found, perks).Select(limit => new ArtifactCount(limit.DescribeArtifactLimit(), limit.Used >= limit.Capacity))]));

    /// <summary>The details of a perk of the artifact, with these perks picked; none for a perk the artifact doesn't have.</summary>
    public static Optional<ArtifactPerkDetails> DescribeDetails(ValidatedBuild build, ItemHash artifact, ImmutableArray<ItemHash> perks, ItemHash perk) =>
        LoopDesigning.FindArtifactTiers(build, artifact).Bind(found => LoopDesigning.FindArtifactTier(found, perk).Map(tier =>
        {
            var cell = DescribeCell(build, found, perks, perk, tier);
            var state = cell.Picked
                ? "Active · tap it again to take it out"
                : cell.Blocked.IsSome() ? "Requirements not met" : "Tap it to make it active";
            return new ArtifactPerkDetails(cell.Tile, $"Artifact Perk · {DomainPhrasing.DescribeArtifactTier(tier)}", state, cell.Blocked, cell.Picked);
        }));

    /// <summary>An artifact's square in the editor's header and its list (it opens Bungie's text).</summary>
    public static IconTile DescribeArtifact(ManifestItem item) =>
        new(item.Name, item.Icon.Map(LoadoutShaping.ToIconUrl), Affinity.Neutral, item.Tier, true, Optional.None<string>(), 1,
            Optional.Some<ItemDetails>(new ItemDetails.OfItem(item.Hash))) { GameText = item.Description };

    /// <summary>"3 perks picked", "No perks picked": an artifact's line in the editor's list.</summary>
    public static string DescribePicked(int count) =>
        count switch
        {
            0 => "No perks picked",
            1 => "1 perk picked",
            _ => $"{count.ToString(System.Globalization.CultureInfo.InvariantCulture)} perks picked",
        };

    private static ArtifactPerkCell DescribeCell(ValidatedBuild build, ManifestKind.Artifact artifact, ImmutableArray<ItemHash> perks, ItemHash perk, int tier) =>
        new(
            perk,
            LoadoutShaping.DescribeArtifactPerk(build, perk),
            tier,
            perks.Contains(perk),
            LoopDesigning.FindBrokenArtifactLimit(artifact, perks, perk).Map(limit => limit.DescribeBrokenArtifactLimit()));

    /// <summary>The slots grouped by the tiers they take, in slot order, each with its perk's square.</summary>
    private static ImmutableArray<ArtifactSlotGroup> GroupSlots(ValidatedBuild build, ManifestKind.Artifact artifact, ImmutableArray<Optional<ItemHash>> placed) =>
    [
        .. artifact.Slots
            .Select((tier, index) => (Tier: tier, Perk: placed[index]))
            .Aggregate(ImmutableArray<(int Tier, ImmutableArray<Optional<ItemHash>> Perks)>.Empty, (groups, slot) =>
                !groups.IsEmpty && groups[^1].Tier == slot.Tier
                    ? groups.SetItem(groups.Length - 1, (slot.Tier, groups[^1].Perks.Add(slot.Perk)))
                    : groups.Add((slot.Tier, [slot.Perk])))
            .Select(group => new ArtifactSlotGroup(
                group.Tier,
                DomainPhrasing.DescribeArtifactSlot(group.Tier),
                [.. group.Perks.Select(perk => perk.Map(hash => LoadoutShaping.DescribeArtifactPerk(build, hash)))])),
    ];
}
