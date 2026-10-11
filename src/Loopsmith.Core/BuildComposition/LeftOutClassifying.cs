using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: what a DIM loadout's left-out item is — a gap (no rule knows it yet) or something with no part in a loop,
/// by what the manifest says it is: a stat mod or a tuning mod (it changes only stats, which the build's stats count
/// when they add up from the player's armor), a legendary armor piece (its set's bonuses already count from the pieces
/// worn) or a jump. Anything else the rules don't know is a gap; nothing is guessed (ADRs D12).
/// </summary>
public static class LeftOutClassifying
{
    private const string StatsOnly =
        "it changes only stats, and the build's stats count it when they add up from your armor (after the Bungie login). It sets nothing off.";

    /// <summary>What a left-out item is, by what the manifest says it is: a gap unless it has no part in a loop.</summary>
    public static LeftOutReason ClassifyLeftOut(RuleCatalog catalog, LeftOutItem item) =>
        FindNoLoopEffect(catalog.Manifest, item.Hash)
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

    /// <summary>Why an item has no part in a loop: a stat or tuning mod by its stat plug, else by its manifest kind; none: a gap.</summary>
    private static Optional<string> FindNoLoopEffect(ManifestExcerpt manifest, ItemHash hash) =>
        manifest.StatPlugs.TryGetValue(hash, out var plug) && DescribeStatsOnly(plug) is Optional<string>.Some statsOnly
            ? statsOnly
            : manifest.FindItem(hash).Bind(known => DescribeNoLoopEffect(manifest, known));

    /// <summary>A stat mod's, a tuning mod's or Balanced Tuning's stats and why that is all it does; none for another plug.</summary>
    private static Optional<string> DescribeStatsOnly(StatPlug plug) =>
        plug.Match(
            _ => Optional.None<string>(),
            _ => Optional.None<string>(),
            _ => Optional.None<string>(),
            tuning => Optional.Some($"A tuning mod ({DescribeStats(tuning.Stats)}): {StatsOnly}"),
            balanced => Optional.Some($"Balanced Tuning ({DescribeStats(balanced.Stats)} where its piece has 0, one piece per tuning socket): {StatsOnly}"),
            mod => Optional.Some($"A stat mod ({DescribeStats(mod.Stats)}): {StatsOnly}"),
            _ => Optional.None<string>());

    /// <summary>"+10 Grenade", "+5 Grenade, −5 Melee", "+1 to each stat": what a plug changes.</summary>
    private static string DescribeStats(ImmutableArray<PlugStat> stats) =>
        stats.Select(stat => stat.Stat).Distinct().Count() == Enum.GetValues<StatKind>().Length && stats.Select(stat => stat.Value).Distinct().Count() == 1
            ? $"{DescribeChange(stats[0].Value)} to each stat"
            : string.Join(", ", stats.Select(stat => $"{DescribeChange(stat.Value)} {stat.Stat}"));

    private static string DescribeChange(int value) =>
        $"{(value < 0 ? "−" : "+")}{Math.Abs(value).ToString(CultureInfo.InvariantCulture)}";

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
