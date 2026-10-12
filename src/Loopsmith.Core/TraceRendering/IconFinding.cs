using Loopsmith.Core.Causality;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// Pure: the icon shown with a name, as the game shows it (bungie.net paths from the manifest excerpt): a keyword's is
/// its Bungie trait's (Jolted, Woven Mail), else its element's item's (Slice: its weapon perk), else the shape the game
/// draws for it that Bungie's data has no file for (the glossary's <c>drawnIcon</c>: Unraveling Rounds); an element's
/// is its keyword's when it is one, else its item's; a step's is its action's — a weapon's hit or kill the build's
/// weapon in that slot, an ability its element, a pickup or a declared status its keyword, a finisher the class's own
/// finisher. None where there is no icon (Orb of Power, a new pack).
/// </summary>
public static class IconFinding
{
    /// <summary>
    /// A keyword's icon: its trait's, the game's own keyword icon; else its element's manifest item's; else the shape the
    /// game draws for it (a status's <c>drawnIcon</c>, in its affinity); none when there is none of those.
    /// </summary>
    public static Optional<IconRef> FindKeywordIcon(RuleCatalog catalog, KeywordRef keyword) =>
        new[]
        {
            catalog.Glossary.FindKeywordTrait(keyword).Bind(catalog.Manifest.FindTraitIcon).Map(ToManifestIcon),
            catalog.FindKeywordElement(keyword).Bind(catalog.FindElementItem).Bind(item => item.Icon).Map(ToManifestIcon),
            FindDrawnIcon(catalog.Glossary, keyword),
        }.FindFirstSome();

    /// <summary>An element's icon: for a keyword's element (Bolt Charge, Jolt), the keyword's (so it is the same everywhere); else its manifest item's.</summary>
    public static Optional<IconRef> FindElementIcon(RuleCatalog catalog, ElementId element) =>
        catalog.FindElementKeyword(element).Match(
            keyword => FindKeywordIcon(catalog, keyword.Value),
            _ => catalog.FindElementItem(element).Bind(item => item.Icon).Map(ToManifestIcon));

    /// <summary>The icon of a step's action (Festival Flight for a kinetic hit, Skip Grenade for a grenade cast, Ionic Trace for picking one up).</summary>
    public static Optional<IconRef> FindActionIcon(RuleCatalog catalog, Build build, PlayerAction action) =>
        action.Match(
            cast => FindAbility(build.Abilities, cast.Kind).Bind(element => FindElementIcon(catalog, element)),
            _ => build.Abilities.ClassAbility.ToElement().Bind(element => FindElementIcon(catalog, element)),
            fire => FindWeaponIcon(catalog, build, fire.Slot).Map(ToManifestIcon),
            collect => FindKeywordIcon(catalog, new KeywordRef.Pickup(collect.Pickup)),
            declare => declare.Declaration.Match(
                max => FindKeywordIcon(catalog, new KeywordRef.Status(max.Status)),
                end => FindKeywordIcon(catalog, new KeywordRef.Status(end.Status)),
                _ => Optional.None<IconRef>()),
            _ => catalog.FindClassFinisher(build.Class).Bind(item => item.Icon).Map(ToManifestIcon));

    /// <summary>The icon of the build's weapon in a slot (its manifest item's), when the build gives its hash.</summary>
    public static Optional<string> FindWeaponIcon(RuleCatalog catalog, Build build, WeaponSlot slot) =>
        build.Weapons
            .Where(weapon => weapon.Slot == slot)
            .Select(weapon => weapon.Hash.Bind(catalog.Manifest.FindItem).Bind(item => item.Icon))
            .FindFirstSome();

    /// <summary>A manifest icon path as an icon.</summary>
    public static IconRef ToManifestIcon(string path) => new IconRef.Manifest(path);

    /// <summary>The shape the game draws for a status Bungie's data has no icon for (the glossary's <c>drawnIcon</c>), in its affinity.</summary>
    private static Optional<IconRef> FindDrawnIcon(KeywordGlossary glossary, KeywordRef keyword) =>
        keyword.Match(
            status => glossary.Statuses.TryGetValue(status.Id, out var definition)
                ? definition.DrawnIcon.Map(shape => (IconRef)new IconRef.Drawn(shape, definition.Affinity))
                : Optional.None<IconRef>(),
            _ => Optional.None<IconRef>(),
            _ => Optional.None<IconRef>());

    private static Optional<ElementId> FindAbility(AbilityLoadout abilities, OffensiveAbility kind) =>
        kind switch
        {
            OffensiveAbility.Grenade => abilities.Grenade.ToElement(),
            OffensiveAbility.Melee => abilities.Melee.ToElement(),
            _ => abilities.Super.ToElement(),
        };
}
