using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// Pure: the icon shown with what a step does or an event names, as the game shows it (bungie.net paths from the
/// manifest excerpt): a weapon's hit or kill shows the build's weapon in that slot, an ability its element, a pickup
/// or a declared status its keyword's icon. None for a new pack, a finisher, or where the excerpt has no icon.
/// </summary>
public static class ActionIcons
{
    /// <summary>The icon of a step's action (Festival Flight for a kinetic hit, Skip Grenade for a grenade cast, Ionic Trace for picking one up).</summary>
    public static Optional<string> FindActionIcon(RuleCatalog catalog, Build build, PlayerAction action) =>
        action.Match(
            cast => FindAbility(build.Abilities, cast.Kind).Bind(catalog.FindElementIcon),
            _ => build.Abilities.ClassAbility.Bind(catalog.FindElementIcon),
            fire => FindWeaponIcon(catalog, build, fire.Slot),
            collect => catalog.FindKeywordIcon(new KeywordRef.Pickup(collect.Pickup)),
            declare => declare.Declaration.Match(
                max => catalog.FindKeywordIcon(new KeywordRef.Status(max.Status)),
                end => catalog.FindKeywordIcon(new KeywordRef.Status(end.Status)),
                _ => Optional.None<string>()),
            _ => Optional.None<string>());

    /// <summary>The icon of the build's weapon in a slot (its manifest item's), when the build gives its hash.</summary>
    public static Optional<string> FindWeaponIcon(RuleCatalog catalog, Build build, WeaponSlot slot) =>
        build.Weapons
            .Where(weapon => weapon.Slot == slot)
            .Select(weapon => weapon.Hash.Bind(catalog.Manifest.FindItem).Bind(item => item.Icon))
            .FindFirstSome();

    private static Optional<ElementId> FindAbility(AbilityLoadout abilities, OffensiveAbility kind) =>
        kind switch
        {
            OffensiveAbility.Grenade => abilities.Grenade,
            OffensiveAbility.Melee => abilities.Melee,
            _ => abilities.Super,
        };
}
