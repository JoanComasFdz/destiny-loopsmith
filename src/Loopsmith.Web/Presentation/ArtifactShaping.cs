using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>One perk slot of the artifact editor: its place (0-based), the highest tier it takes, and its perk's square (none: empty).</summary>
public sealed record ArtifactSlotView(int Index, int Tier, Optional<IconTile> Perk);

/// <summary>A group of perk slots that take the same tiers, under the game's mark ("◆◆") and named for them ("Tier 1–2").</summary>
public sealed record ArtifactSlotGroup(int Tier, string Mark, string Label, ImmutableArray<ArtifactSlotView> Slots);

/// <summary>A perk a slot's dropdown offers: its square and tier, whether the slot holds it, whether another slot does.</summary>
public sealed record ArtifactPerkOption(ItemHash Perk, IconTile Tile, int Tier, bool Current, bool Elsewhere);

/// <summary>A tier's perks in a slot's dropdown, under its heading ("Tier 2 ◆◆").</summary>
public sealed record ArtifactOptionGroup(int Tier, string Heading, ImmutableArray<ArtifactPerkOption> Perks);

/// <summary>One name of what an artifact works best with ("Arc", "Glaive"), with its icon when Loopsmith has one (an element's).</summary>
public sealed record FocusChip(string Name, Optional<string> Icon);

/// <summary>One line of what an artifact works best with: "Elements" or "Weapons", and its names.</summary>
public sealed record ArtifactFocus(string Label, ImmutableArray<FocusChip> Chips);

/// <summary>What the artifact editor shows under its header, as the game does: the flavour text and what it works best with.</summary>
public sealed record ArtifactHeader(Optional<string> Flavor, ImmutableArray<ArtifactFocus> Focus);

/// <summary>
/// The artifact editor's Save: the artifact and the perk in each slot, and the slots last picked for each artifact while
/// the loop is open (switching to an artifact shows them again).
/// </summary>
public sealed record ArtifactSave(ArtifactChoice Choice, ImmutableDictionary<ItemHash, ImmutableArray<Optional<ItemHash>>> Remembered);

/// <summary>Pure: the artifact editor's slots, dropdowns and header, from the Orchestration API.</summary>
public static class ArtifactShaping
{
    /// <summary>The artifact's slots, grouped by the tiers they take (2, 3, 2), with the perk in each; none when the excerpt lacks the artifact.</summary>
    public static Optional<ImmutableArray<ArtifactSlotGroup>> ShapeSlots(ValidatedBuild build, ItemHash artifact, ImmutableArray<Optional<ItemHash>> slots) =>
        LoopDesigning.FindArtifactTiers(build, artifact).Map(found => GroupSlots(found.Slots
            .Select((tier, index) => new ArtifactSlotView(
                index,
                tier,
                (index < slots.Length ? slots[index] : Optional.None<ItemHash>()).Map(perk => LoadoutShaping.DescribeArtifactPerk(build, perk))))
            .ToImmutableArray()));

    /// <summary>What slot number <paramref name="slot"/> can hold, by tier (Tier 1 first): a perk in another slot is marked, the slot's own too.</summary>
    public static ImmutableArray<ArtifactOptionGroup> ListOptions(ValidatedBuild build, ItemHash artifact, ImmutableArray<Optional<ItemHash>> slots, int slot) =>
        LoopDesigning.FindArtifactTiers(build, artifact).Match(
            found => LoopDesigning.ListArtifactSlotOptions(found.Value, slot)
                .Select(perk => new ArtifactPerkOption(
                    perk,
                    LoadoutShaping.DescribeArtifactPerk(build, perk),
                    LoopDesigning.FindArtifactTier(found.Value, perk).UnwrapOr(0),
                    slot < slots.Length && slots[slot] == Optional.Some(perk),
                    slots.Where((held, at) => at != slot && held == Optional.Some(perk)).Any()))
                .GroupBy(option => option.Tier)
                .OrderBy(group => group.Key)
                .Select(group => new ArtifactOptionGroup(
                    group.Key,
                    $"{DomainPhrasing.DescribeArtifactTier(group.Key)} {DomainPhrasing.DescribeArtifactSlotMark(group.Key)}",
                    [.. group]))
                .ToImmutableArray(),
            _ => []);

    /// <summary>The flavour text and the lines of what the artifact works best with (Bungie's description: "Elements: [Arc] [Void]").</summary>
    public static ArtifactHeader DescribeHeader(ValidatedBuild build, ManifestItem item) =>
        new(
            item.Kind is ManifestKind.Artifact artifact ? artifact.Flavor : Optional.None<string>(),
            item.Description.Match(
                text => text.Value
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(line => line.Contains(':', StringComparison.Ordinal) && line.Contains('[', StringComparison.Ordinal))
                    .Select(line => DescribeFocusLine(build, line))
                    .ToImmutableArray(),
                _ => ImmutableArray<ArtifactFocus>.Empty));

    /// <summary>"Elements: [Arc] [Void]": its label and each bracketed name, an element's with its icon.</summary>
    private static ArtifactFocus DescribeFocusLine(ValidatedBuild build, string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        var names = line[(colon + 1)..]
            .Split('[', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split(']')[0].Trim())
            .Where(name => name.Length > 0);
        return new ArtifactFocus(
            line[..colon].Trim(),
            [.. names.Select(name => new FocusChip(name, Enum.TryParse<DamageType>(name, ignoreCase: true, out var type)
                ? LoadoutShaping.FindDamageIcon(build.Catalog, type)
                : Optional.None<string>()))]);
    }

    /// <summary>An artifact's square in the editor's header and its list (it opens Bungie's text).</summary>
    public static IconTile DescribeArtifact(ManifestItem item) =>
        new(item.Name, item.Icon.Map(LoadoutShaping.ToIconUrl), Affinity.Neutral, item.Tier, true, Optional.None<string>(), 1,
            Optional.Some<ItemDetails>(new ItemDetails.OfItem(item.Hash))) { GameText = item.Description };

    /// <summary>"3 perks picked", "No perks picked": an artifact's line in the editor's list.</summary>
    public static string DescribePicked(ImmutableArray<Optional<ItemHash>> slots) =>
        slots.Count(slot => slot.IsSome()) switch
        {
            0 => "No perks picked",
            1 => "1 perk picked",
            var count => $"{count.ToString(CultureInfo.InvariantCulture)} perks picked",
        };

    /// <summary>The slots grouped where the tier they take changes, in slot order.</summary>
    private static ImmutableArray<ArtifactSlotGroup> GroupSlots(ImmutableArray<ArtifactSlotView> slots) =>
    [
        .. slots
            .Aggregate(ImmutableArray<ImmutableArray<ArtifactSlotView>>.Empty, (groups, slot) =>
                !groups.IsEmpty && groups[^1][^1].Tier == slot.Tier
                    ? groups.SetItem(groups.Length - 1, groups[^1].Add(slot))
                    : groups.Add([slot]))
            .Select(group => new ArtifactSlotGroup(
                group[0].Tier,
                DomainPhrasing.DescribeArtifactSlotMark(group[0].Tier),
                DomainPhrasing.DescribeArtifactSlot(group[0].Tier),
                group)),
    ];
}
