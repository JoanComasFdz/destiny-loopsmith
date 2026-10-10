using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.BuildComposition;

/// <summary>
/// Pure: picking a weapon's perks, column by column, among the ones the manifest excerpt says it rolls with (ADRs D14).
/// A pick is part of the weapon's <see cref="WeaponLoadout.Roll"/>; a perk the build named by id in that column gives
/// way to it, so each column holds one perk.
/// </summary>
public static class WeaponRolling
{
    /// <summary>
    /// The weapon with <paramref name="perk"/> picked in trait column <paramref name="column"/> (0-based), or that column
    /// back to "?" when none. A column that doesn't roll (one perk) can't be picked; neither can a perk it doesn't have.
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
