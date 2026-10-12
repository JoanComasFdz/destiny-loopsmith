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
    /// The champion a build's weapon breaks, as DIM shows it: its copy's breaker (<paramref name="marks"/>, after the
    /// login), else the weapon's own (an exotic's: Thunderlord's Disruption), by the excerpt's breaker types; none when
    /// it has none.
    /// </summary>
    public static Optional<BreakerType> FindWeaponBreaker(ManifestExcerpt manifest, WeaponLoadout weapon, Optional<GearMarks> marks) =>
        new[] { marks.Bind(found => found.Breaker), manifest.FindWeaponKind(weapon).Bind(kind => kind.Breaker) }
            .FindFirstSome()
            .Bind(manifest.FindBreakerType);
}
