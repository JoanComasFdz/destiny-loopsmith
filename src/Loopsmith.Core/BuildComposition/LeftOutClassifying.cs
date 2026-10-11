using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: what a DIM loadout's left-out item is — a gap (no rule knows it yet) or something with no part in a loop,
/// by what the manifest says it is: a legendary armor piece (its set's bonuses already count from the pieces worn) or a
/// jump. Anything else the rules don't know is a gap; nothing is guessed (ADRs D12).
/// </summary>
public static class LeftOutClassifying
{
    /// <summary>What a left-out item is, by what the manifest says it is: a gap unless it has no part in a loop.</summary>
    public static LeftOutReason ClassifyLeftOut(RuleCatalog catalog, LeftOutItem item) =>
        catalog.Manifest.FindItem(item.Hash)
            .Bind(known => DescribeNoLoopEffect(catalog.Manifest, known))
            .Match<LeftOutReason>(why => new LeftOutReason.NoLoopEffect(why.Value), _ => new LeftOutReason.NotInRules());

    /// <summary>The left-out items no rule knows yet: the build's real gaps.</summary>
    public static ImmutableArray<LeftOutItem> ListMissing(RuleCatalog catalog, Build build) =>
        [.. build.LeftOut.Where(item => ClassifyLeftOut(catalog, item).IsMissing())];

    /// <summary>The left-out items with no part in a loop, each with why: what "No part in a loop" lists.</summary>
    public static ImmutableArray<NoLoopEffectItem> ListNoLoopEffect(RuleCatalog catalog, Build build) =>
    [
        .. build.LeftOut.SelectMany(item => ClassifyLeftOut(catalog, item).Match(
            _ => [],
            none => new[] { new NoLoopEffectItem(item, none.Why) })),
    ];

    private static Optional<string> DescribeNoLoopEffect(ManifestExcerpt manifest, ManifestItem item) =>
        item.Kind switch
        {
            ManifestKind.Armor armor when item.Tier == Optional.Some(ItemTier.Legendary) =>
                Optional.Some(armor.Set.Bind(set => manifest.ArmorSets.TryGetValue(set, out var found) ? Optional.Some(found) : Optional.None<ArmorSet>()).Match(
                    set => $"A {set.Value.Name} piece: its set's bonuses count from the pieces worn; the piece itself carries stats and mod slots, nothing a loop uses.",
                    _ => "Legendary armor: stats and mod slots, nothing a loop uses.")),
            ManifestKind.Movement => Optional.Some("A jump: movement has no part in a loop."),
            _ => Optional.None<string>(),
        };
}
