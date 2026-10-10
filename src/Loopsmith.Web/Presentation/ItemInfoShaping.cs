using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// What Loopsmith's rules say about an element: its description (written from the Compendium or Clarity, never copied),
/// what it sets off — as the build has it, or as one copy would when the build doesn't equip it — and where its facts
/// come from. An empty explanation: no rule is written for it yet.
/// </summary>
public sealed record InfoRules(Optional<string> Description, ImmutableArray<StyledLine> Explanation, bool Equipped, string Source);

/// <summary>A section's heading: a perk's name, icon and what it is ("Intrinsic", "Trait · column 1", "Exotic perk").</summary>
public sealed record SectionHeading(string Name, Optional<string> Icon, string Label);

/// <summary>
/// One part of an item's details, each with Bungie's text and what the rules say (none: not in the rules yet): the
/// item itself; one of its perks (its intrinsic, an exotic armor's perk, what a column holds); a column not picked yet;
/// an exotic's catalyst, with its switch (its heading when it is part of the weapon's details).
/// </summary>
[Union]
public partial record InfoSection
{
    partial record Item(Optional<string> BungieText, Optional<InfoRules> Rules);
    partial record Perk(SectionHeading Heading, Optional<string> BungieText, Optional<InfoRules> Rules);
    partial record Unpicked(int Column);
    partial record Catalyst(Optional<SectionHeading> Heading, Optional<string> BungieText, Optional<InfoRules> Rules, CatalystToggle Toggle);
}

/// <summary>An item's details, as tapping its square shows them: its name, type, icon, rarity, light.gg page and sections.</summary>
public sealed record ItemInfo(
    string Name,
    string TypeLine,
    Optional<string> Icon,
    Optional<ItemTier> Tier,
    Affinity Affinity,
    Optional<string> Page,
    ImmutableArray<InfoSection> Sections);

/// <summary>What hovering a name shows before a tap opens its details: its name, type, icon and first text (Bungie's, else the rules').</summary>
public sealed record ItemPreview(string Name, string TypeLine, Optional<string> Icon, Affinity Affinity, Optional<string> Text);

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
            weapon => LoopDesigning.FindWeapon(build, weapon.Weapon).Map(found => ShapeWeapon(build, found, weapon.Weapon)),
            catalyst => LoopDesigning.FindWeapon(build, catalyst.Weapon).Bind(found => LoopDesigning.FindCatalyst(build, catalyst.Weapon)
                .Map(plug => ShapeCatalyst(build, found, plug, catalyst.Weapon))));

    /// <summary>The short preview of what <see cref="ShapeInfo"/> details: the same name, type and icon, and its first text.</summary>
    public static Optional<ItemPreview> ShapePreview(ValidatedBuild build, ItemDetails details) =>
        ShapeInfo(build, details).Map(info => new ItemPreview(
            info.Name,
            info.TypeLine,
            info.Icon,
            info.Affinity,
            info.Sections.Select(ReadSectionText).FindFirstSome()));

    private static Optional<string> ReadSectionText(InfoSection section) =>
        section.Match(
            item => new[] { item.BungieText, item.Rules.Bind(rules => rules.Description) }.FindFirstSome(),
            perk => new[] { perk.BungieText, perk.Rules.Bind(rules => rules.Description) }.FindFirstSome(),
            _ => Optional.None<string>(),
            catalyst => new[] { catalyst.BungieText, catalyst.Rules.Bind(rules => rules.Description) }.FindFirstSome());

    private static Optional<ItemInfo> ShapeElement(ValidatedBuild build, ElementId id) =>
        build.Catalog.Elements.TryGetValue(id, out var element)
            ? Optional.Some(ShapeElement(build, element))
            : Optional.None<ItemInfo>();

    private static ItemInfo ShapeElement(ValidatedBuild build, BuildElement element)
    {
        var catalog = build.Catalog;
        var item = catalog.FindElementItem(element.Id);
        var rules = Optional.Some(DescribeRules(build, element));
        InfoSection section = catalog.Manifest.FindIntrinsicPerk(element.Hashes).Match<InfoSection>(
            perk => new InfoSection.Perk(new SectionHeading(perk.Value.Name, ToIcon(perk.Value), "Exotic perk"), perk.Value.Description, rules),
            _ => new InfoSection.Item(item.Bind(known => known.Description), rules));
        return new ItemInfo(
            element.Name,
            item.Match(known => DescribeType(catalog, known.Value), _ => element.Kind.DescribeElementKind()),
            item.Bind(ToIcon),
            item.Bind(known => known.Tier),
            element.Affinity,
            item.Bind(known => LoadoutShaping.ToPageUrl(catalog, known.Hash)),
            [section]);
    }

    private static Optional<ItemInfo> ShapeItem(ValidatedBuild build, ItemHash hash) =>
        build.Catalog.FindElementByHash(hash).Match(
            element => Optional.Some(ShapeElement(build, element.Value)),
            _ => build.Catalog.Manifest.FindItem(hash).Map(item => new ItemInfo(
                item.Name,
                DescribeType(build.Catalog, item),
                ToIcon(item),
                item.Tier,
                Affinity.Neutral,
                LoadoutShaping.ToPageUrl(build.Catalog, hash),
                [
                    build.Catalog.Manifest.FindIntrinsicPerk([hash]).Match<InfoSection>(
                        perk => new InfoSection.Perk(new SectionHeading(perk.Value.Name, ToIcon(perk.Value), "Exotic perk"), perk.Value.Description, Optional.None<InfoRules>()),
                        _ => new InfoSection.Item(item.Description, Optional.None<InfoRules>())),
                ])));

    /// <summary>An exotic weapon: its intrinsic perk, each perk its columns hold (or "?"), the perks the build names, its catalyst.</summary>
    private static ItemInfo ShapeWeapon(ValidatedBuild build, WeaponLoadout weapon, int index)
    {
        var catalog = build.Catalog;
        var item = weapon.Hash.Bind(hash => catalog.Manifest.FindItem(hash));
        var sections = LoopDesigning.ListPerkSlots(build, weapon).Select(slot => slot.Match<InfoSection>(
            intrinsic => DescribePerkSection(build, intrinsic.Perk, "Intrinsic"),
            named => DescribeNamedSection(build, named.Perk),
            fixedPerk => DescribePerkSection(build, fixedPerk.Perk, "Trait"),
            rolling => rolling.Perk.Match<InfoSection>(
                perk => DescribePerkSection(build, perk.Value, $"Trait · column {rolling.Column + 1}"),
                _ => new InfoSection.Unpicked(rolling.Column)),
            catalyst => DescribeCatalystSection(build, catalyst, index, true)));
        return new ItemInfo(
            weapon.Name,
            item.Match(known => DescribeType(catalog, known.Value), _ => weapon.Slot.DescribeWeaponSlot()),
            item.Bind(ToIcon),
            item.Bind(known => known.Tier),
            weapon.Type.ToAffinity(),
            weapon.Hash.Bind(hash => LoadoutShaping.ToPageUrl(catalog, hash)),
            [.. sections]);
    }

    /// <summary>An exotic's catalyst on its own: the catalyst item, the perks it grants and its switch.</summary>
    private static ItemInfo ShapeCatalyst(ValidatedBuild build, WeaponLoadout weapon, WeaponPerkSlot.Catalyst catalyst, int index)
    {
        var item = build.Catalog.Manifest.FindItem(catalyst.Plug);
        return new ItemInfo(
            build.Catalog.Manifest.DescribeItemHash(catalyst.Plug),
            $"Catalyst · {weapon.Name}",
            item.Bind(ToIcon),
            item.Bind(known => known.Tier),
            weapon.Type.ToAffinity(),
            LoadoutShaping.ToPageUrl(build.Catalog, catalyst.Plug),
            [DescribeCatalystSection(build, catalyst, index, false)]);
    }

    private static InfoSection DescribePerkSection(ValidatedBuild build, ItemHash perk, string label)
    {
        var item = build.Catalog.Manifest.FindItem(perk);
        var element = build.Catalog.FindElementByHash(perk);
        return new InfoSection.Perk(
            new SectionHeading(
                element.Match(known => known.Value.Name, _ => build.Catalog.Manifest.DescribeItemHash(perk)),
                item.Bind(ToIcon),
                item.Bind(known => known.Type).Match(type => label.StartsWith(type.Value, StringComparison.Ordinal) ? label : $"{label} · {type.Value}", _ => label)),
            item.Bind(known => known.Description),
            element.Map(known => DescribeRules(build, known)));
    }

    private static InfoSection DescribeNamedSection(ValidatedBuild build, ElementId id) =>
        build.Catalog.Elements.TryGetValue(id, out var element)
            ? new InfoSection.Perk(
                new SectionHeading(element.Name, build.Catalog.FindElementItem(id).Bind(ToIcon), element.Kind.DescribeElementKind()),
                build.Catalog.FindElementItem(id).Bind(item => item.Description),
                Optional.Some(DescribeRules(build, element)))
            : new InfoSection.Perk(new SectionHeading(id.Value, Optional.None<string>(), "Perk"), Optional.None<string>(), Optional.None<InfoRules>());

    /// <summary>A catalyst: Bungie's text is its perks' (its own is the generic "upgrades this weapon to a Masterwork").</summary>
    private static InfoSection DescribeCatalystSection(ValidatedBuild build, WeaponPerkSlot.Catalyst catalyst, int weapon, bool withHeading)
    {
        var catalog = build.Catalog;
        var item = catalog.Manifest.FindItem(catalyst.Plug);
        var text = string.Join("\n", catalog.Manifest.ListCatalystPerks(catalyst.Plug)
            .Select(perk => perk.Description.Match(description => $"{perk.Name}: {description.Value}", _ => perk.Name)));
        var label = catalyst.On ? "Catalyst · on" : "Catalyst · off";
        return new InfoSection.Catalyst(
            withHeading ? Optional.Some(new SectionHeading(catalog.Manifest.DescribeItemHash(catalyst.Plug), item.Bind(ToIcon), label)) : Optional.None<SectionHeading>(),
            text.Length > 0 ? Optional.Some(text) : item.Bind(known => known.Description),
            catalog.FindElementByHash(catalyst.Plug).Map(element => DescribeRules(build, element)),
            new CatalystToggle(weapon, catalyst.On));
    }

    private static InfoRules DescribeRules(ValidatedBuild build, BuildElement element) =>
        new(element.Description, LoopDesigning.ExplainElement(build, element), LoopDesigning.IsEquipped(build, element.Id), element.Source.DescribeProvenance());

    /// <summary>"Exotic Machine Gun", "Legendary Helmet", "Luminopotent · 2-piece perk", "Arc Aspect".</summary>
    private static string DescribeType(RuleCatalog catalog, ManifestItem item) =>
        item.Kind is ManifestKind.Perk
            ? catalog.Manifest.FindSetOfPerk(item.Hash).Match(found => $"{found.Value.Set.Name} · {found.Value.Perk.Pieces.Value}-piece perk", _ => "Perk")
            : item.Type.Match(
                type => item.Kind is ManifestKind.Weapon or ManifestKind.Armor && item.Tier is Optional<ItemTier>.Some { Value: ItemTier.Exotic or ItemTier.Legendary } tier
                    ? $"{tier.Value} {type.Value}"
                    : type.Value,
                _ => item.Name);

    private static Optional<string> ToIcon(ManifestItem item) =>
        item.Icon.Map(LoadoutShaping.ToIconUrl);
}
