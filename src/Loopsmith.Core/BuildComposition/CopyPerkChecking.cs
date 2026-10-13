using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: a build's weapon against the player's copy it wears (docs/loop-format.md, "Weapons from your account"): the
/// perks the copy has in each trait column — any of a column that rolled two perks or more — and the columns whose perk
/// the build picks that the copy doesn't have. The copy has a perk when it is one of that column's perks on it, or the
/// base perk of an enhanced one there (the enhanced trait does all its base perk does); the enhanced version of a base
/// perk there it hasn't. Which copy the build wears is StatLoading's to say (<c>CopyFinding</c>); whether a plug is an
/// enhanced trait, RuleParsing's (<paramref name="isEnhanced"/> in what follows).
/// </summary>
public static class CopyPerkChecking
{
    /// <summary>The perks the copy has in each of the weapon's trait columns, the one it holds first (none: no socket of it rolls with that column).</summary>
    public static ImmutableArray<ImmutableArray<ItemHash>> ListCopyColumns(RuleCatalog catalog, WeaponLoadout weapon, OwnedWeapon copy) =>
        catalog.Manifest.ListTraitColumns(weapon).ReadColumnPerks(copy.Sockets);

    /// <summary>Whether a column's perks on a copy (<paramref name="held"/>) give it <paramref name="perk"/>: that very perk, or an enhanced version of it.</summary>
    public static bool HasPerk(RuleCatalog catalog, ImmutableArray<ItemHash> held, ItemHash perk, Func<ItemHash, bool> isEnhanced) =>
        held.Contains(perk)
        || (!isEnhanced(perk) && held.Any(plug => isEnhanced(plug) && IsEnhancedVersion(catalog, plug, perk)));

    /// <summary>
    /// One warning for each column that rolls whose perk the build picks (or names in it) and the copy doesn't have:
    /// "Hung Jury SR4: your copy doesn't have Slice in column 3 (it has Rapid Hit or Outlaw)."
    /// </summary>
    public static ImmutableArray<BuildIssue> CheckCopyPerks(RuleCatalog catalog, WeaponLoadout weapon, OwnedWeapon copy, Func<ItemHash, bool> isEnhanced)
    {
        var columns = ListCopyColumns(catalog, weapon, copy);
        return
        [
            .. WeaponRolling.ListPerkSlots(catalog, weapon)
                .OfType<WeaponPerkSlot.Rolling>()
                .Where(slot => slot.Column < columns.Length)
                .SelectMany(slot => slot.Perk.Match(
                    perk => HasPerk(catalog, columns[slot.Column], perk.Value, isEnhanced)
                        ? []
                        : new[] { new BuildIssue(Severity.Warning, DescribeMissing(catalog, weapon, slot.Column, perk.Value, columns[slot.Column], isEnhanced)) },
                    _ => [])),
        ];
    }

    /// <summary>A perk as the build check names it: its rules element's name ("Slice (enhanced)"), else its manifest name, "(enhanced)" for an enhanced trait.</summary>
    public static string DescribePerk(RuleCatalog catalog, ItemHash perk, Func<ItemHash, bool> isEnhanced) =>
        catalog.FindElementByHash(perk).Match(
            element => element.Value.Name,
            _ => isEnhanced(perk) ? $"{catalog.Manifest.DescribeItemHash(perk)} (enhanced)" : catalog.Manifest.DescribeItemHash(perk));

    private static string DescribeMissing(RuleCatalog catalog, WeaponLoadout weapon, int column, ItemHash perk, ImmutableArray<ItemHash> held, Func<ItemHash, bool> isEnhanced)
    {
        var names = held.Select(plug => DescribePerk(catalog, plug, isEnhanced)).ToImmutableArray();
        var has = names.IsEmpty ? "it has none of that column's perks" : $"it has {JoinWithOr(names)}";
        return $"{weapon.Name}: your copy doesn't have {DescribePerk(catalog, perk, isEnhanced)} in column {column + 1} ({has}).";
    }

    /// <summary>Whether <paramref name="enhanced"/> is the enhanced version of <paramref name="perk"/>: their manifest items share a name, or the rules pair their elements.</summary>
    private static bool IsEnhancedVersion(RuleCatalog catalog, ItemHash enhanced, ItemHash perk)
    {
        var name = catalog.Manifest.FindItem(enhanced).Map(item => item.Name);
        var paired = catalog.FindElementByHash(perk).Bind(element => catalog.EnhancedPerks.TryGetValue(element.Id, out var found) ? Optional.Some(found) : Optional.None<ElementId>());
        return (name.IsSome() && catalog.Manifest.FindItem(perk).Map(item => item.Name) == name)
            || (paired.IsSome() && catalog.FindElementByHash(enhanced).Map(element => element.Id) == paired);
    }

    private static string JoinWithOr(ImmutableArray<string> parts) =>
        parts.Length <= 1 ? string.Concat(parts) : $"{string.Join(", ", parts[..^1])} or {parts[^1]}";
}
