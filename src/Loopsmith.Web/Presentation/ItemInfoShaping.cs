using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// What Loopsmith's rules say about an element: its description (written from the Compendium or Clarity, never copied),
/// what it sets off in this build, and where its facts come from. <see cref="Inert"/>: no rule is written for it yet.
/// </summary>
public sealed record InfoRules(Optional<string> Description, ImmutableArray<StyledLine> Explanation, bool Inert, string Source);

/// <summary>
/// One part of an item's details: the item itself (no <see cref="Name"/>), or its intrinsic, a perk or its catalyst.
/// Bungie's text for it, what the rules say (none: not in the rules yet) and, for a catalyst, whether it is on.
/// </summary>
public sealed record InfoSection(
    Optional<string> Name,
    Optional<string> Icon,
    Optional<string> Label,
    Optional<string> BungieText,
    Optional<InfoRules> Rules,
    Optional<CatalystToggle> Catalyst);

/// <summary>An item's details, as tapping its square shows them: its name, type, icon, rarity, light.gg page and sections.</summary>
public sealed record ItemInfo(
    string Name,
    Optional<string> TypeLine,
    Optional<string> Icon,
    Optional<ItemTier> Tier,
    Affinity Affinity,
    Optional<string> Page,
    ImmutableArray<InfoSection> Sections);

/// <summary>
/// Pure: what a square's details show. Bungie's text comes from the manifest excerpt (an exotic armor's from its
/// intrinsic perk, a catalyst's from the perks it grants); the rules' part from the element (Orchestration explains it).
/// </summary>
public static class ItemInfoShaping
{
    public static Optional<ItemInfo> ShapeInfo(ValidatedBuild build, ItemDetails details) =>
        details.Match(
            element => ShapeElement(build, element.Element),
            item => ShapeItem(build, item.Hash),
            weapon => ShapeWeapon(build, weapon.Weapon),
            catalyst => ShapeCatalystInfo(build, catalyst.Weapon));

    private static Optional<ItemInfo> ShapeElement(ValidatedBuild build, ElementId id)
    {
        var catalog = build.Catalog;
        if (!catalog.Elements.TryGetValue(id, out var element))
        {
            return Optional.None<ItemInfo>();
        }

        var item = catalog.FindElementItem(id);
        var intrinsic = FindIntrinsic(catalog, element.Hashes);
        var rules = Optional.Some(DescribeRules(build, element));
        var sections = intrinsic.Match<ImmutableArray<InfoSection>>(
            perk => [new InfoSection(Optional.Some(perk.Value.Name), ToIcon(perk.Value), Optional.Some("Exotic perk"), perk.Value.Description, rules, Optional.None<CatalystToggle>())],
            _ => [new InfoSection(Optional.None<string>(), Optional.None<string>(), Optional.None<string>(), item.Bind(known => known.Description), rules, Optional.None<CatalystToggle>())]);
        return Optional.Some(new ItemInfo(
            element.Name,
            Optional.Some(item.Match(known => DescribeType(catalog, known.Value), _ => element.Kind.DescribeElementKind())),
            item.Bind(ToIcon),
            item.Bind(known => known.Tier),
            element.Affinity,
            item.Bind(known => LoadoutShaping.ToPageUrl(catalog, known.Hash)),
            sections));
    }

    private static Optional<ItemInfo> ShapeItem(ValidatedBuild build, ItemHash hash) =>
        build.Catalog.FindElementByHash(hash).Match(
            element => ShapeElement(build, element.Value.Id),
            _ => build.Catalog.Manifest.FindItem(hash).Map(item => new ItemInfo(
                item.Name,
                Optional.Some(DescribeType(build.Catalog, item)),
                ToIcon(item),
                item.Tier,
                Affinity.Neutral,
                LoadoutShaping.ToPageUrl(build.Catalog, hash),
                [
                    FindIntrinsic(build.Catalog, [hash]).Match(
                        perk => new InfoSection(Optional.Some(perk.Value.Name), ToIcon(perk.Value), Optional.Some("Exotic perk"), perk.Value.Description,
                            Optional.None<InfoRules>(), Optional.None<CatalystToggle>()),
                        _ => new InfoSection(Optional.None<string>(), Optional.None<string>(), Optional.None<string>(), item.Description,
                            Optional.None<InfoRules>(), Optional.None<CatalystToggle>())),
                ])));

    /// <summary>An exotic weapon: its intrinsic perk, each perk its columns hold (or "?"), the perks the build names, its catalyst.</summary>
    private static Optional<ItemInfo> ShapeWeapon(ValidatedBuild build, int index)
    {
        var catalog = build.Catalog;
        if (index < 0 || index >= build.Build.Weapons.Length)
        {
            return Optional.None<ItemInfo>();
        }

        var weapon = build.Build.Weapons[index];
        var item = weapon.Hash.Bind(hash => catalog.Manifest.FindItem(hash));
        var sections = LoopDesigning.ListPerkSlots(build, weapon).Select(slot => slot.Match(
            intrinsic => DescribePerkSection(build, intrinsic.Perk, "Intrinsic"),
            named => DescribeNamedSection(build, named.Perk),
            fixedPerk => DescribePerkSection(build, fixedPerk.Perk, "Trait"),
            rolling => rolling.Perk.Match(
                perk => DescribePerkSection(build, perk.Value, $"Trait · column {rolling.Column + 1}"),
                _ => new InfoSection(Optional.Some(DomainPhrasing.Unknown), Optional.None<string>(), Optional.Some($"Trait · column {rolling.Column + 1}"),
                    Optional.Some("Not picked: a DIM link carries the weapon, not its roll."), Optional.None<InfoRules>(), Optional.None<CatalystToggle>())),
            catalyst => DescribeCatalystSection(build, catalyst, index, Optional.Some(catalog.Manifest.DescribeItemHash(catalyst.Plug)))));
        return Optional.Some(new ItemInfo(
            weapon.Name,
            Optional.Some(item.Match(known => DescribeType(catalog, known.Value), _ => weapon.Slot.DescribeWeaponSlot())),
            item.Bind(ToIcon),
            item.Bind(known => known.Tier),
            weapon.Type.ToAffinity(),
            weapon.Hash.Bind(hash => LoadoutShaping.ToPageUrl(catalog, hash)),
            [.. sections]));
    }

    /// <summary>An exotic's catalyst on its own: the catalyst item, the perks it grants and its switch.</summary>
    private static Optional<ItemInfo> ShapeCatalystInfo(ValidatedBuild build, int index)
    {
        var catalog = build.Catalog;
        var catalyst = index >= 0 && index < build.Build.Weapons.Length
            ? LoopDesigning.ListPerkSlots(build, build.Build.Weapons[index]).OfType<WeaponPerkSlot.Catalyst>().Select(Optional.Some).FindFirstSome()
            : Optional.None<WeaponPerkSlot.Catalyst>();
        return catalyst.Map(found =>
        {
            var item = catalog.Manifest.FindItem(found.Plug);
            return new ItemInfo(
                item.Match(known => known.Value.Name, _ => catalog.Manifest.DescribeItemHash(found.Plug)),
                Optional.Some($"Catalyst · {build.Build.Weapons[index].Name}"),
                item.Bind(ToIcon),
                item.Bind(known => known.Tier),
                build.Build.Weapons[index].Type.ToAffinity(),
                LoadoutShaping.ToPageUrl(catalog, found.Plug),
                [DescribeCatalystSection(build, found, index, Optional.None<string>())]);
        });
    }

    private static InfoSection DescribePerkSection(ValidatedBuild build, ItemHash perk, string label)
    {
        var item = build.Catalog.Manifest.FindItem(perk);
        var element = build.Catalog.FindElementByHash(perk);
        return new InfoSection(
            Optional.Some(element.Match(known => known.Value.Name, _ => build.Catalog.Manifest.DescribeItemHash(perk))),
            item.Bind(ToIcon),
            Optional.Some(item.Bind(known => known.Type).Match(type => type.Value == "Trait" ? label : $"{label} · {type.Value}", _ => label)),
            item.Bind(known => known.Description),
            element.Map(known => DescribeRules(build, known)),
            Optional.None<CatalystToggle>());
    }

    private static InfoSection DescribeNamedSection(ValidatedBuild build, ElementId id) =>
        build.Catalog.Elements.TryGetValue(id, out var element)
            ? new InfoSection(Optional.Some(element.Name), build.Catalog.FindElementItem(id).Bind(ToIcon), Optional.Some(element.Kind.DescribeElementKind()),
                build.Catalog.FindElementItem(id).Bind(item => item.Description), Optional.Some(DescribeRules(build, element)), Optional.None<CatalystToggle>())
            : new InfoSection(Optional.Some(id.Value), Optional.None<string>(), Optional.None<string>(), Optional.None<string>(), Optional.None<InfoRules>(), Optional.None<CatalystToggle>());

    /// <summary>A catalyst: Bungie's text is its perks' (its own is the generic "upgrades this weapon to a Masterwork").</summary>
    private static InfoSection DescribeCatalystSection(ValidatedBuild build, WeaponPerkSlot.Catalyst catalyst, int weapon, Optional<string> name)
    {
        var catalog = build.Catalog;
        var item = catalog.Manifest.FindItem(catalyst.Plug);
        var perks = item.Match(
            known => known.Value.Kind is ManifestKind.ExoticCatalyst granted ? granted.Perks : [],
            _ => ImmutableArray<ItemHash>.Empty);
        var text = string.Join("\n", perks
            .SelectMany(perk => catalog.Manifest.FindItem(perk).Match(found => new[] { found.Value }, _ => []))
            .Select(found => found.Description.Match(description => $"{found.Name}: {description.Value}", _ => found.Name)));
        return new InfoSection(
            name,
            name.IsSome() ? item.Bind(ToIcon) : Optional.None<string>(),
            Optional.Some(catalyst.On ? "Catalyst · on" : "Catalyst · off"),
            text.Length > 0 ? Optional.Some(text) : item.Bind(known => known.Description),
            catalog.FindElementByHash(catalyst.Plug).Map(element => DescribeRules(build, element)),
            Optional.Some(new CatalystToggle(weapon, catalyst.On)));
    }

    private static InfoRules DescribeRules(ValidatedBuild build, BuildElement element) =>
        new(element.Description, LoopDesigning.ExplainElement(build, element.Id), element.Rules.IsEmpty && element.Passives.IsEmpty, element.Source.DescribeProvenance());

    /// <summary>The intrinsic perk of the first of these items that has one (an exotic armor's copies: only one carries it).</summary>
    private static Optional<ManifestItem> FindIntrinsic(RuleCatalog catalog, IEnumerable<ItemHash> hashes) =>
        hashes
            .Select(hash => catalog.Manifest.FindItem(hash).Bind(item => item.Kind switch
            {
                ManifestKind.Armor armor => armor.Intrinsic,
                _ => Optional.None<ItemHash>(),
            }))
            .FindFirstSome()
            .Bind(catalog.Manifest.FindItem);

    /// <summary>"Exotic Machine Gun", "Legendary Helmet", "Luminopotent · 2-piece perk", "Arc Aspect".</summary>
    private static string DescribeType(RuleCatalog catalog, ManifestItem item) =>
        item.Kind is ManifestKind.Perk
            ? catalog.Manifest.FindSetOfPerk(item.Hash).Match(
                set => $"{set.Value.Name} · {set.Value.Perks.Where(perk => perk.Perk == item.Hash).Select(perk => perk.Pieces).FirstOrDefault()}-piece perk",
                _ => "Perk")
            : item.Type.Match(
                type => item.Tier == Optional.Some(ItemTier.Exotic) && item.Kind is ManifestKind.Weapon or ManifestKind.Armor
                    ? $"Exotic {type.Value}"
                    : item.Tier == Optional.Some(ItemTier.Legendary) && item.Kind is ManifestKind.Weapon or ManifestKind.Armor ? $"Legendary {type.Value}" : type.Value,
                _ => item.Name);

    private static Optional<string> ToIcon(ManifestItem item) =>
        item.Icon.Map(LoadoutShaping.ToIconUrl);
}
