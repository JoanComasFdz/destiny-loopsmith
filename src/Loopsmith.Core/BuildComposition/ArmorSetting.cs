using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: a build's armor sets, by the manifest excerpt (ADRs D14). A set perk is active when the build has it as a set
/// bonus, or wears enough of the set's pieces (a DIM loadout's armor, which the build leaves out as items).
/// </summary>
public static class ArmorSetting
{
    /// <summary>
    /// Each set the build's armor pieces belong to (first), then each set one of its set bonuses is from; per perk
    /// whether it is active and its element when the catalog knows it.
    /// </summary>
    public static ImmutableArray<ArmorSetStatus> ListArmorSets(RuleCatalog catalog, Build build)
    {
        var worn = catalog.Manifest.CountSetPieces(build.LeftOut.Where(item => item.Part == LoadoutPart.Item).Select(item => item.Hash));
        var bonusHashes = build.ArmorSetBonuses
            .SelectMany(id => catalog.Elements.TryGetValue(id, out var element) ? element.Hashes : [])
            .ToImmutableArray();
        var declared = bonusHashes
            .SelectMany(hash => catalog.Manifest.FindSetOfPerk(hash).Match(set => new[] { set.Value }, _ => []))
            .Where(set => !worn.Any(piece => piece.Set.Hash == set.Hash));
        return
        [
            .. worn.Select(piece => DescribeSet(catalog, piece.Set, Optional.Some(piece.Pieces), bonusHashes)),
            .. declared.DistinctBy(set => set.Hash).Select(set => DescribeSet(catalog, set, Optional.None<int>(), bonusHashes)),
        ];
    }

    /// <summary>The build's set bonuses that are in none of the excerpt's sets (no hash yet): shown on their own.</summary>
    public static ImmutableArray<ElementId> ListLooseBonuses(RuleCatalog catalog, Build build) =>
    [
        .. build.ArmorSetBonuses.Where(id => !catalog.Elements.TryGetValue(id, out var element)
            || !element.Hashes.Any(hash => catalog.Manifest.FindSetOfPerk(hash).IsSome())),
    ];

    private static ArmorSetStatus DescribeSet(RuleCatalog catalog, ArmorSet set, Optional<int> pieces, ImmutableArray<ItemHash> bonusHashes) =>
        new(set, pieces, [.. set.Perks.Select(perk => new SetPerkStatus(
            perk,
            bonusHashes.Contains(perk.Perk) || pieces.Match(worn => worn.Value >= perk.Pieces, _ => false),
            catalog.FindElementByHash(perk.Perk).Map(element => element.Id)))]);
}
