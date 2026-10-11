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
    /// The player's weapons for a slot, for the weapon picker: the copy the build's weapon there wears first (its weapon
    /// number <paramref name="weapon"/>; none: an empty slot), then that weapon's other copies, then the rest by name and
    /// tier. Its note counts the weapons the excerpt can't place. What the build can wear of them (one exotic) is
    /// BuildComposition's to say (<c>WeaponSwapping.CheckWeapon</c>).
    /// </summary>
    public static WeaponOptions ListWeaponOptions(RuleCatalog catalog, Build build, BungieProfile profile, WeaponSlot slot, Optional<int> weapon)
    {
        var current = weapon.Bind(index => index >= 0 && index < build.Weapons.Length ? Optional.Some(build.Weapons[index]) : Optional.None<WeaponLoadout>());
        var worn = current.Bind(found => FindWornWeapon(profile, found)).Map(copy => copy.InstanceId);
        var named = profile.Weapons.Select(owned => (Weapon: owned, Item: catalog.Manifest.FindItem(owned.Hash))).ToImmutableArray();
        var options = named
            .SelectMany(entry => entry.Item.Match(item => new[] { (entry.Weapon, Item: item.Value) }, _ => []))
            .Where(entry => entry.Item.Kind is ManifestKind.Weapon kind && kind.Slot == slot)
            .Select(entry => new WeaponOption(entry.Weapon, entry.Item, worn == Optional.Some(entry.Weapon.InstanceId)))
            .OrderByDescending(option => option.Worn)
            .ThenByDescending(option => current.Bind(found => found.Hash) == Optional.Some(option.Weapon.Hash))
            .ThenBy(option => option.Item.Name, StringComparer.Ordinal)
            .ThenByDescending(option => option.Weapon.Marks.Tier.Match(tier => tier.Value.Value, _ => 0))
            .ToImmutableArray();
        var unnamed = named.Count(entry => !entry.Item.IsSome());
        ImmutableArray<string> notes = unnamed > 0 ? [$"{unnamed} of your weapons aren't listed: Loopsmith can't tell their slot yet (Bungie's manifest wasn't read for them)."] : [];
        return new WeaponOptions(slot, options, notes);
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

    /// <summary>A copy's breaker when it has one, else its weapon's own, by the excerpt's breaker types.</summary>
    private static Optional<BreakerType> ChooseBreaker(ManifestExcerpt manifest, Optional<ItemHash> copy, Optional<ManifestKind.Weapon> weapon) =>
        new[] { copy, weapon.Bind(kind => kind.Breaker) }
            .FindFirstSome()
            .Bind(manifest.FindBreakerType);
}
