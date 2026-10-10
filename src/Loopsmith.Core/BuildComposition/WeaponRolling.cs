using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: a weapon's perks, column by column, among the ones the manifest excerpt says it rolls with (ADRs D14). A column
/// holds one perk: the roll's pick, else the perk the build names in it, else (a column that doesn't roll) its only one.
/// The build check equips what this places and the designer shows it, so the two can't disagree.
/// </summary>
public static class WeaponRolling
{
    /// <summary>The trait columns the build fills so far and the named perks not placed in one yet.</summary>
    private sealed record Placing(ImmutableArray<WeaponPerkSlot> Slots, ImmutableArray<ElementId> Unplaced);

    /// <summary>
    /// The weapon's perks: one slot per trait column the excerpt gives, in column order, then each perk the build names
    /// that is in none of them. A named perk whose column holds another (the roll's pick) is in neither: see
    /// <see cref="ListDisplacedPerks"/>.
    /// </summary>
    public static ImmutableArray<WeaponPerkSlot> ListPerkSlots(RuleCatalog catalog, WeaponLoadout weapon)
    {
        var columns = catalog.Manifest.ListTraitColumns(weapon);
        var placed = PlaceColumns(catalog, weapon, columns);
        return [.. placed.Slots, .. placed.Unplaced.Where(id => !IsInAnyColumn(catalog, id, columns)).Select(id => new WeaponPerkSlot.Named(id))];
    }

    /// <summary>The perks the build names in a column that holds another perk (the roll's pick, or a perk named before it): they don't count.</summary>
    public static ImmutableArray<ElementId> ListDisplacedPerks(RuleCatalog catalog, WeaponLoadout weapon)
    {
        var columns = catalog.Manifest.ListTraitColumns(weapon);
        var placed = PlaceColumns(catalog, weapon, columns);
        return [.. placed.Unplaced.Where(id => IsInAnyColumn(catalog, id, columns))];
    }

    /// <summary>
    /// The weapon with <paramref name="perk"/> picked in trait column <paramref name="column"/> (0-based), or that column
    /// back to "?" when none. A column that doesn't roll (one perk) can't be picked; neither can a perk it doesn't have.
    /// A perk the build named in that column goes, so the file says what the column holds.
    /// </summary>
    public static Result<WeaponLoadout, string> PickPerk(RuleCatalog catalog, WeaponLoadout weapon, int column, Optional<ItemHash> perk)
    {
        var columns = catalog.Manifest.ListTraitColumns(weapon);
        return CheckPick(catalog, weapon, columns, column, perk)
            .Map(picked => weapon with
            {
                Perks = [.. weapon.Perks.Where(id => !catalog.IsInColumn(id, picked))],
                Roll = SetPick(weapon.Roll, column, perk),
            });
    }

    private static Placing PlaceColumns(RuleCatalog catalog, WeaponLoadout weapon, ImmutableArray<TraitColumn> columns) =>
        columns
            .Select((column, number) => (Column: column, Number: number))
            .Aggregate(new Placing([], weapon.Perks), (placing, next) => PlaceColumn(catalog, weapon, placing, next.Column, next.Number));

    /// <summary>One column: the roll's pick wins; else the first named perk not placed yet that is in it; a column that doesn't roll has its one perk.</summary>
    private static Placing PlaceColumn(RuleCatalog catalog, WeaponLoadout weapon, Placing placing, TraitColumn column, int number)
    {
        var pick = number < weapon.Roll.Length ? weapon.Roll[number] : Optional.None<ItemHash>();
        var named = placing.Unplaced.Where(id => catalog.IsInColumn(id, column)).Select(Optional.Some).FindFirstSome();
        var unplaced = pick.IsSome() && column.Options.Length > 1
            ? placing.Unplaced
            : named.Match(id => placing.Unplaced.Remove(id.Value), _ => placing.Unplaced);
        WeaponPerkSlot slot = column.Options.Length == 1
            ? new WeaponPerkSlot.Fixed(number, column.Options[0])
            : new WeaponPerkSlot.Rolling(number, column.Options, pick.IsSome() ? pick : named.Bind(id => catalog.FindColumnHash(id, column)));
        return new Placing(placing.Slots.Add(slot), unplaced);
    }

    private static bool IsInAnyColumn(RuleCatalog catalog, ElementId id, ImmutableArray<TraitColumn> columns) =>
        columns.Any(column => catalog.IsInColumn(id, column));

    private static Result<TraitColumn, string> CheckPick(
        RuleCatalog catalog, WeaponLoadout weapon, ImmutableArray<TraitColumn> columns, int column, Optional<ItemHash> perk) =>
        column < 0 || column >= columns.Length
            ? Fail($"{weapon.Name} has no perk column {column + 1} in the manifest excerpt.")
            : columns[column].Options.Length == 1
                ? Fail($"{weapon.Name}'s column {column + 1} doesn't roll: it is always {catalog.Manifest.DescribeItemHash(columns[column].Options[0])}.")
                : perk.Match(
                    hash => columns[column].Options.Contains(hash.Value)
                        ? Succeed(columns[column])
                        : Fail($"{catalog.Manifest.DescribeTypedItemHash(hash.Value)} isn't a perk of {weapon.Name}'s column {column + 1}."),
                    _ => Succeed(columns[column]));

    private static Result<TraitColumn, string> Succeed(TraitColumn column) => new Result<TraitColumn, string>.Ok(column);

    private static Result<TraitColumn, string> Fail(string message) => new Result<TraitColumn, string>.Error(message);

    /// <summary>The roll with one column set, padded with "?" up to it and without trailing "?" (the file writes no more than it says).</summary>
    private static ImmutableArray<Optional<ItemHash>> SetPick(ImmutableArray<Optional<ItemHash>> roll, int column, Optional<ItemHash> perk)
    {
        var padded = roll.AddRange(Enumerable.Repeat(Optional.None<ItemHash>(), Math.Max(column + 1 - roll.Length, 0)));
        var set = padded.SetItem(column, perk);
        return [.. set.Reverse().SkipWhile(pick => !pick.IsSome()).Reverse()];
    }
}
