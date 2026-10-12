using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>Pure reads of a <see cref="WeaponTarget"/> in its build, which the slices and the hosts share.</summary>
public static class WeaponTargeting
{
    /// <summary>The build's weapon the target replaces; none for an empty slot, or a weapon number the build hasn't.</summary>
    public static Optional<WeaponLoadout> FindTargetWeapon(this Build build, WeaponTarget target) =>
        target is WeaponTarget.InPlaceOf { Weapon: var index } && index >= 0 && index < build.Weapons.Length
            ? Optional.Some(build.Weapons[index])
            : Optional.None<WeaponLoadout>();

    /// <summary>The slot a target fills: its weapon's, or the empty slot's; none for a weapon number the build hasn't.</summary>
    public static Optional<WeaponSlot> FindTargetSlot(this Build build, WeaponTarget target) =>
        target.Match(
            inPlace => build.FindTargetWeapon(inPlace).Map(weapon => weapon.Slot),
            empty => Optional.Some(empty.Slot));
}
