using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.StatLoading;

/// <summary>
/// Pure: which of the player's copies a build wears, after their Bungie login, as DIM resolves a loadout's items: the
/// copy the build names (a dim.gg share's, or picked) when it is in their account, else their own copy of that item.
/// What the game draws on it (its tier, masterwork and breaker: <see cref="GearMarks"/>) is that copy's.
/// </summary>
public static class CopyFinding
{
    /// <summary>
    /// The player's copy the build wears in a slot: the one the build names when it is theirs, else their copy of that
    /// piece with the most points (the copy the stats add up and the armor picker ticks:
    /// <see cref="StatAdding.FindWornCopy"/>); none when they own no copy, or the build names no piece for the slot.
    /// </summary>
    public static Optional<OwnedArmor> FindWornArmor(RuleCatalog catalog, Build build, BungieProfile profile, ArmorSlot slot) =>
        StatAdding.ListStatChoices(catalog, build, profile).Pieces
            .Where(choice => choice.Piece.Slot == slot)
            .Select(Optional.Some)
            .FindFirstSome()
            .Bind(choice => StatAdding.FindWornCopy(choice, build.ArmorCopies))
            .Map(copy => copy.Armor);

    /// <summary>
    /// The player's copy of a build's weapon: the one the build names (a dim.gg share's) when it is theirs, else their
    /// copy of that weapon with the highest tier; none when they own none.
    /// </summary>
    public static Optional<OwnedWeapon> FindWornWeapon(BungieProfile profile, WeaponLoadout weapon)
    {
        var copies = profile.Weapons.Where(owned => weapon.Hash == Optional.Some(owned.Hash)).ToImmutableArray();
        return profile.Weapons.Where(owned => weapon.Instance == Optional.Some(owned.InstanceId))
            .Concat(copies.OrderByDescending(owned => owned.Marks.Tier.Match(tier => tier.Value.Value, _ => 0)))
            .Select(Optional.Some)
            .FindFirstSome();
    }

    /// <summary>
    /// The player's weapons for the slot <paramref name="target"/> fills, for the weapon picker: the copy the build's weapon
    /// there names first (ticked only when it is that very copy, so another copy of theirs can still be picked to load its
    /// perks), then that weapon's other copies, then the rest by name and tier; each with the perks it holds and whether its
    /// catalyst is in. Its note counts the weapons the excerpt can't place. What the build can wear of them (one exotic, one
    /// weapon a slot) is BuildComposition's to say (<c>WeaponSwapping.CheckWeapon</c>).
    /// </summary>
    public static WeaponOptions ListWeaponOptions(RuleCatalog catalog, Build build, BungieProfile profile, WeaponTarget target)
    {
        var slot = build.FindTargetSlot(target);
        var current = build.FindTargetWeapon(target);
        var named = profile.Weapons.Select(owned => (Weapon: owned, Item: catalog.Manifest.FindItem(owned.Hash))).ToImmutableArray();
        var options = named
            .SelectMany(entry => entry.Item.Match(item => new[] { (entry.Weapon, Item: item.Value) }, _ => []))
            .SelectMany(entry => entry.Item.Kind is ManifestKind.Weapon kind && Optional.Some(kind.Slot) == slot
                ? new[] { new WeaponOption(entry.Weapon, entry.Item, kind.Traits.ReadRoll(entry.Weapon.Plugs), kind.HoldsCatalyst(entry.Weapon.Plugs), current.Bind(found => found.Instance) == Optional.Some(entry.Weapon.InstanceId)) }
                : [])
            .OrderByDescending(option => option.Worn)
            .ThenByDescending(option => current.Bind(found => found.Hash) == Optional.Some(option.Weapon.Hash))
            .ThenBy(option => option.Item.Name, StringComparer.Ordinal)
            .ThenByDescending(option => option.Weapon.Marks.Tier.Match(tier => tier.Value.Value, _ => 0))
            .ToImmutableArray();
        var unnamed = named.Count(entry => !entry.Item.IsSome());
        ImmutableArray<string> notes = unnamed > 0 ? [$"{unnamed} of your weapons aren't listed: Loopsmith can't tell their slot yet (Bungie's manifest wasn't read for them)."] : [];
        return new WeaponOptions(slot, options, notes);
    }

    /// <summary>The build's weapons whose copy (the instance id they name: a dim.gg share's, or picked) is in the player's account, by weapon number, with the copy.</summary>
    public static ImmutableArray<(int Weapon, OwnedWeapon Copy)> ListOwnedCopies(Build build, BungieProfile profile) =>
    [
        .. build.Weapons.SelectMany((weapon, index) => weapon.Instance
            .Bind(instance => profile.Weapons.Where(owned => owned.InstanceId == instance).Select(Optional.Some).FindFirstSome())
            .Match(copy => new[] { (index, copy.Value) }, _ => [])),
    ];

    /// <summary>
    /// What the login says of the build's weapons: those <paramref name="loaded"/> (by weapon number: their copies' perks
    /// read) hold their perks now; for the others (no copy named, or one not in the player's account), ⋯ picks one of
    /// theirs. Empty for a build without weapons.
    /// </summary>
    public static string DescribeLoadedWeapons(Build build, ImmutableArray<int> loaded)
    {
        var theirs = loaded.Where(index => index >= 0 && index < build.Weapons.Length).Select(index => build.Weapons[index].Name).ToImmutableArray();
        var others = build.Weapons.Where((_, index) => !loaded.Contains(index)).Select(weapon => weapon.Name).ToImmutableArray();
        return (theirs.IsEmpty, others.IsEmpty) switch
        {
            (true, true) => "",
            (false, true) => "Its weapons are yours too, with their perks.",
            (false, false) => $"{JoinWithAnd(theirs)} {(theirs.Length == 1 ? "is" : "are")} yours too, with {(theirs.Length == 1 ? "its" : "their")} perks; tap ⋯ next to {JoinWithAnd(others)} to pick one of yours.",
            (true, false) => "Tap ⋯ next to a weapon to pick one of yours.",
        };
    }

    /// <summary>The player's weapon with this instance id, or why there is none.</summary>
    public static Result<OwnedWeapon, string> FindWeapon(BungieProfile profile, ItemInstanceId instance) =>
        profile.Weapons.Where(owned => owned.InstanceId == instance).Select(Optional.Some).FindFirstSome()
            .ToResult(() => "That weapon isn't in your account any more: log in again to read your weapons.");

    /// <summary>
    /// The champion a build's weapon breaks, as DIM shows it: its copy's breaker (<paramref name="marks"/>, after the
    /// login), else the weapon's own (an exotic's: Thunderlord's Disruption), by the excerpt's breaker types; none when
    /// it has none.
    /// </summary>
    public static Optional<BreakerType> FindWeaponBreaker(ManifestExcerpt manifest, WeaponLoadout weapon, Optional<GearMarks> marks) =>
        ChooseBreaker(manifest, marks.Bind(found => found.Breaker), manifest.FindWeaponKind(weapon));

    /// <summary>The champion one of the player's weapons breaks (the weapon picker's): its own breaker, else its weapon's.</summary>
    public static Optional<BreakerType> FindCopyBreaker(ManifestExcerpt manifest, OwnedWeapon copy) =>
        ChooseBreaker(manifest, copy.Marks.Breaker, manifest.FindItem(copy.Hash).Bind(item => item.Kind is ManifestKind.Weapon kind ? Optional.Some(kind) : Optional.None<ManifestKind.Weapon>()));

    private static string JoinWithAnd(ImmutableArray<string> parts) =>
        parts.Length <= 1 ? string.Concat(parts) : $"{string.Join(", ", parts[..^1])} and {parts[^1]}";

    /// <summary>A copy's breaker when it has one, else its weapon's own, by the excerpt's breaker types.</summary>
    private static Optional<BreakerType> ChooseBreaker(ManifestExcerpt manifest, Optional<ItemHash> copy, Optional<ManifestKind.Weapon> weapon) =>
        new[] { copy, weapon.Bind(kind => kind.Breaker) }
            .FindFirstSome()
            .Bind(manifest.FindBreakerType);
}
