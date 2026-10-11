using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// Pure: the icon shown with a name, as the game shows it (bungie.net paths from the manifest excerpt): a keyword's is
/// its Bungie trait's (Jolted, Woven Mail), else its element's item's (Slice: its weapon perk); an element's is its
/// keyword's when it is one, else its item's; a step's is its action's — a weapon's hit or kill the build's weapon in
/// that slot, an ability its element, a pickup or a declared status its keyword. None where the excerpt has no icon
/// (Orb of Power, a new pack, a finisher).
/// </summary>
public static class IconFinding
{
    /// <summary>A keyword's icon: its trait's, the game's own keyword icon; else its element's manifest item's; none when the excerpt has neither.</summary>
    public static Optional<string> FindKeywordIcon(RuleCatalog catalog, KeywordRef keyword) =>
        new[]
        {
            catalog.Glossary.FindKeywordTrait(keyword).Bind(catalog.Manifest.FindTraitIcon),
            catalog.FindKeywordElement(keyword).Bind(catalog.FindElementItem).Bind(item => item.Icon),
        }.FindFirstSome();

    /// <summary>An element's icon: for a keyword's element (Bolt Charge, Jolt), the keyword's (so it is the same everywhere); else its manifest item's.</summary>
    public static Optional<string> FindElementIcon(RuleCatalog catalog, ElementId element) =>
        catalog.FindElementKeyword(element).Match(
            keyword => FindKeywordIcon(catalog, keyword.Value),
            _ => catalog.FindElementItem(element).Bind(item => item.Icon));

    /// <summary>The icon of a step's action (Festival Flight for a kinetic hit, Skip Grenade for a grenade cast, Ionic Trace for picking one up).</summary>
    public static Optional<string> FindActionIcon(RuleCatalog catalog, Build build, PlayerAction action) =>
        action.Match(
            cast => FindAbility(build.Abilities, cast.Kind).Bind(element => FindElementIcon(catalog, element)),
            _ => build.Abilities.ClassAbility.Bind(element => FindElementIcon(catalog, element)),
            fire => FindWeaponIcon(catalog, build, fire.Slot),
            collect => FindKeywordIcon(catalog, new KeywordRef.Pickup(collect.Pickup)),
            declare => declare.Declaration.Match(
                max => FindKeywordIcon(catalog, new KeywordRef.Status(max.Status)),
                end => FindKeywordIcon(catalog, new KeywordRef.Status(end.Status)),
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
