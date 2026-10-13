using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: a build's weapon against the perks the player's copy of it has in each trait column (docs/loop-format.md,
/// "Weapons from your account"; StatLoading reads them: <c>CopyFinding.ReadCopyColumns</c>). The copy has a perk when it
/// is one of that column's perks on it — any of a column that rolled two perks or more — or the base perk of an enhanced
/// one there (the enhanced trait does all its base perk does); the enhanced version of a base perk there it hasn't. One
/// rule gives both what the perk picker marks and what the build check warns about.
/// </summary>
public static class CopyPerkChecking
{
    /// <summary>
    /// Every perk the weapon's columns that roll offer (and the one each holds) that the copy doesn't have there: what
    /// the perk picker and the weapon's perk squares mark.
    /// </summary>
    public static ImmutableArray<ColumnPerk> ListMissingPerks(RuleCatalog catalog, WeaponLoadout weapon, ImmutableArray<ImmutableArray<ItemHash>> copyColumns) =>
    [
        .. ListRollingColumns(catalog, weapon).SelectMany(slot => slot.Options
            .Concat(slot.Perk.Match(held => new[] { held.Value }, _ => []))
            .Distinct()
            .Where(perk => IsMissing(catalog, copyColumns, slot.Column, perk))
            .Select(perk => new ColumnPerk(slot.Column, perk))),
    ];

    /// <summary>
    /// One warning for each column that rolls whose perk the build picks (or names in it) and the copy doesn't have:
    /// "Hung Jury SR4: your copy doesn't have Slice in column 3 (it has Rapid Hit or Outlaw)."
    /// </summary>
    public static ImmutableArray<BuildIssue> CheckCopyPerks(RuleCatalog catalog, WeaponLoadout weapon, ImmutableArray<ImmutableArray<ItemHash>> copyColumns) =>
    [
        .. ListRollingColumns(catalog, weapon).SelectMany(slot => slot.Perk.Match(
            perk => IsMissing(catalog, copyColumns, slot.Column, perk.Value)
                ? new[] { new BuildIssue(Severity.Warning, DescribeMissing(catalog, weapon, slot.Column, perk.Value, copyColumns[slot.Column])) }
                : [],
            _ => [])),
    ];

    private static IEnumerable<WeaponPerkSlot.Rolling> ListRollingColumns(RuleCatalog catalog, WeaponLoadout weapon) =>
        WeaponRolling.ListPerkSlots(catalog, weapon).OfType<WeaponPerkSlot.Rolling>();

    /// <summary>Whether the copy, which has <paramref name="copyColumns"/> column by column, doesn't have <paramref name="perk"/> in <paramref name="column"/>.</summary>
    private static bool IsMissing(RuleCatalog catalog, ImmutableArray<ImmutableArray<ItemHash>> copyColumns, int column, ItemHash perk) =>
        column < copyColumns.Length && !HasPerk(catalog, copyColumns[column], perk);

    /// <summary>Whether a column's perks on a copy (<paramref name="held"/>) give it <paramref name="perk"/>: that very perk, or an enhanced version of it.</summary>
    private static bool HasPerk(RuleCatalog catalog, ImmutableArray<ItemHash> held, ItemHash perk) =>
        held.Contains(perk)
        || (!catalog.IsEnhancedPerk(perk) && held.Any(plug => catalog.IsEnhancedPerk(plug) && IsEnhancedVersion(catalog, plug, perk)));

    /// <summary>Whether <paramref name="enhanced"/> is the enhanced version of <paramref name="perk"/>: their manifest items share a name, or the rules pair their elements.</summary>
    private static bool IsEnhancedVersion(RuleCatalog catalog, ItemHash enhanced, ItemHash perk)
    {
        var name = catalog.Manifest.FindItem(enhanced).Map(item => item.Name);
        var paired = catalog.FindElementByHash(perk).Bind(element => catalog.EnhancedPerks.TryGetValue(element.Id, out var found) ? Optional.Some(found) : Optional.None<ElementId>());
        return (name.IsSome() && catalog.Manifest.FindItem(perk).Map(item => item.Name) == name)
            || (paired.IsSome() && catalog.FindElementByHash(enhanced).Map(element => element.Id) == paired);
    }

    private static string DescribeMissing(RuleCatalog catalog, WeaponLoadout weapon, int column, ItemHash perk, ImmutableArray<ItemHash> held)
    {
        var names = held.Select(plug => DescribePerk(catalog, plug)).ToImmutableArray();
        var has = names.IsEmpty ? "it has none of that column's perks" : $"it has {JoinWithOr(names)}";
        return $"{weapon.Name}: your copy doesn't have {DescribePerk(catalog, perk)} in column {column + 1} ({has}).";
    }

    /// <summary>A perk as the build check names it: its rules element's name ("Slice (enhanced)"), else its manifest name, "(enhanced)" for an enhanced trait.</summary>
    private static string DescribePerk(RuleCatalog catalog, ItemHash perk) =>
        catalog.FindElementByHash(perk).Match(
            element => element.Value.Name,
            _ => catalog.IsEnhancedPerk(perk) ? $"{catalog.Manifest.DescribeItemHash(perk)} (enhanced)" : catalog.Manifest.DescribeItemHash(perk));

    private static string JoinWithOr(ImmutableArray<string> parts) =>
        parts.Length <= 1 ? string.Concat(parts) : $"{string.Join(", ", parts[..^1])} or {parts[^1]}";
}
