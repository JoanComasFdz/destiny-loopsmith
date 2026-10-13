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
/// rule gives both what the perk picker marks and what the build check warns about, one line per weapon: the line the
/// check already has on its roll (perks the rules don't know) becomes the warning, else a warning of its own.
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
    /// The build check's <paramref name="issues"/> with what the player's copies lack (<paramref name="copies"/>: each
    /// weapon of the build whose copy is theirs, with the copy's perks by column), for each column that rolls whose perk
    /// the build picks (or names in it) and the copy doesn't have. A perk the rules don't know is already named by the
    /// line on the weapon's roll: that one line stays, in its place, as a warning that says it too ("Festival Flight's
    /// roll: Hatchling (Trait) — not in the rule catalog yet, so it sets nothing off. Your copy doesn't have Hatchling in
    /// column 2 (it has …)."). Any other gets a warning of its own at the end ("Hung Jury SR4: your copy doesn't have
    /// Slice in column 3 (it has Rapid Hit or Outlaw).").
    /// </summary>
    public static ImmutableArray<BuildIssue> AddCopyWarnings(
        RuleCatalog catalog, ImmutableArray<BuildIssue> issues, ImmutableArray<(WeaponLoadout Weapon, ImmutableArray<ImmutableArray<ItemHash>> Columns)> copies)
    {
        var lacking = copies
            .Select(copy => new LackingCopy(
                copy.Weapon,
                BuildValidation.CheckUnknownRoll(catalog, copy.Weapon).Bind(roll => issues.Contains(roll) ? Optional.Some(roll) : Optional.None<BuildIssue>()),
                ListMissingPicks(catalog, copy.Weapon, copy.Columns)))
            .ToImmutableArray();
        var merged = lacking
            .SelectMany(copy => copy.Roll.Match(
                roll => copy.Missing.Where(pick => IsOnRollLine(catalog, copy, pick)).Select(pick => $"Your copy {pick.Text}.").ToImmutableArray() is { IsEmpty: false } said
                    ? new[] { KeyValuePair.Create(roll.Value, new BuildIssue(Severity.Warning, $"{roll.Value.Message} {string.Join(" ", said)}")) }
                    : [],
                _ => []))
            .ToImmutableDictionary();
        var separate = lacking.SelectMany(copy => copy.Missing
            .Where(pick => !IsOnRollLine(catalog, copy, pick))
            .Select(pick => new BuildIssue(Severity.Warning, $"{copy.Weapon.Name}: your copy {pick.Text}.")));
        return [.. issues.Select(issue => merged.TryGetValue(issue, out var warning) ? warning : issue), .. separate];
    }

    /// <summary>A pick the copy doesn't have and what to say of it: "doesn't have Slice in column 3 (it has Rapid Hit or Outlaw)".</summary>
    private sealed record MissingPick(ItemHash Perk, string Text);

    /// <summary>A weapon whose copy is the player's: the line the build check has on its roll (when it lists one) and the picks the copy doesn't have.</summary>
    private sealed record LackingCopy(WeaponLoadout Weapon, Optional<BuildIssue> Roll, ImmutableArray<MissingPick> Missing);

    /// <summary>Whether the weapon's roll line names this pick (a perk the rules don't know), so it says what the copy lacks too.</summary>
    private static bool IsOnRollLine(RuleCatalog catalog, LackingCopy copy, MissingPick pick) =>
        copy.Roll.IsSome() && !catalog.FindElementByHash(pick.Perk).IsSome();

    /// <summary>The picks of the weapon's columns that roll that the copy doesn't have, a column at a time.</summary>
    private static ImmutableArray<MissingPick> ListMissingPicks(RuleCatalog catalog, WeaponLoadout weapon, ImmutableArray<ImmutableArray<ItemHash>> copyColumns) =>
    [
        .. ListRollingColumns(catalog, weapon).SelectMany(slot => slot.Perk.Match(
            perk => IsMissing(catalog, copyColumns, slot.Column, perk.Value)
                ? new[] { new MissingPick(perk.Value, DescribeMissing(catalog, slot.Column, perk.Value, copyColumns[slot.Column])) }
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

    private static string DescribeMissing(RuleCatalog catalog, int column, ItemHash perk, ImmutableArray<ItemHash> held)
    {
        var names = held.Select(plug => DescribePerk(catalog, plug)).ToImmutableArray();
        var has = names.IsEmpty ? "it has none of that column's perks" : $"it has {JoinWithOr(names)}";
        return $"doesn't have {DescribePerk(catalog, perk)} in column {column + 1} ({has})";
    }

    /// <summary>A perk as the build check names it: its rules element's name ("Slice (enhanced)"), else its manifest name, "(enhanced)" for an enhanced trait.</summary>
    private static string DescribePerk(RuleCatalog catalog, ItemHash perk) =>
        catalog.FindElementByHash(perk).Match(
            element => element.Value.Name,
            _ => catalog.IsEnhancedPerk(perk) ? $"{catalog.Manifest.DescribeItemHash(perk)} (enhanced)" : catalog.Manifest.DescribeItemHash(perk));

    private static string JoinWithOr(ImmutableArray<string> parts) =>
        parts.Length <= 1 ? string.Concat(parts) : $"{string.Join(", ", parts[..^1])} or {parts[^1]}";
}
