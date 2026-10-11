using System.Collections.Immutable;
using System.Globalization;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// What Loopsmith's rules say about an element: what it sets off — as the build has it, or as one copy would when the
/// build doesn't equip it — and where its facts come from. An empty explanation: no rule is written for it yet.
/// </summary>
public sealed record InfoRules(ImmutableArray<StyledLine> Explanation, bool Equipped, InfoSource Source);

/// <summary>Where to read a source itself: the Compendium's sheet, a creator's video or guide.</summary>
public sealed record SourceLink(string Url, string Text);

/// <summary>
/// Where an element's facts come from, as a card of its own: the Destiny Data Compendium (its tab and row), Clarity,
/// Loopsmith's own rule (no row cited) or a creator's claim; Loopsmith's words for it (a paraphrase, never the source's
/// own text); where to read the source itself; a creator's words when it is a claim.
/// </summary>
public sealed record InfoSource(string Title, Optional<string> Paraphrase, string Cite, Optional<SourceLink> Link, Optional<string> Quote);

/// <summary>A section's heading: a perk's name, icon and what it is ("Intrinsic", "Trait · column 1", "Exotic perk").</summary>
public sealed record SectionHeading(string Name, Optional<string> Icon, string Label);

/// <summary>
/// One part of an item's details, each with Bungie's text and what the rules say (none: not in the rules yet): the
/// item itself; one of its perks (its intrinsic, an exotic armor's perk, what a column holds); a column not picked yet;
/// an exotic's catalyst, with its switch (its heading when it is part of the weapon's details); an artifact, with the
/// perks of it the build has (the artifact itself sets nothing off: its perks do).
/// </summary>
[Union]
public partial record InfoSection
{
    partial record Item(Optional<string> BungieText, Optional<InfoRules> Rules);
    partial record Perk(SectionHeading Heading, Optional<string> BungieText, Optional<InfoRules> Rules);
    partial record Unpicked(int Column);
    partial record Catalyst(Optional<SectionHeading> Heading, Optional<string> BungieText, Optional<InfoRules> Rules, CatalystToggle Toggle);
    partial record Artifact(Optional<string> BungieText, ImmutableArray<SectionHeading> Perks);
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
            item => new[] { item.BungieText, item.Rules.Bind(rules => rules.Source.Paraphrase) }.FindFirstSome(),
            perk => new[] { perk.BungieText, perk.Rules.Bind(rules => rules.Source.Paraphrase) }.FindFirstSome(),
            _ => Optional.None<string>(),
            catalyst => new[] { catalyst.BungieText, catalyst.Rules.Bind(rules => rules.Source.Paraphrase) }.FindFirstSome(),
            artifact => artifact.BungieText);

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
                    item.Kind is ManifestKind.Artifact
                        ? DescribeArtifactSection(build, item)
                        : build.Catalog.Manifest.FindIntrinsicPerk([hash]).Match<InfoSection>(
                            perk => new InfoSection.Perk(new SectionHeading(perk.Value.Name, ToIcon(perk.Value), "Exotic perk"), perk.Value.Description, Optional.None<InfoRules>()),
                            _ => new InfoSection.Item(item.Description, Optional.None<InfoRules>())),
                ])));

    /// <summary>An artifact: Bungie's text and, when it is the build's artifact, the build's perks of it (their squares sit in the build).</summary>
    private static InfoSection DescribeArtifactSection(ValidatedBuild build, ManifestItem item) =>
        new InfoSection.Artifact(
            item.Description,
            LoopDesigning.FindArtifact(build).Match(
                found => found.Value.Item.Hash == item.Hash ? [.. found.Value.Perks.Select(perk => DescribeArtifactPerk(build.Catalog, perk))] : ImmutableArray<SectionHeading>.Empty,
                _ => ImmutableArray<SectionHeading>.Empty));

    private static SectionHeading DescribeArtifactPerk(RuleCatalog catalog, ArtifactPerkOfBuild perk) =>
        new(
            perk.Element.Bind(id => catalog.Elements.TryGetValue(id, out var element) ? Optional.Some(element.Name) : Optional.None<string>())
                .UnwrapOr(catalog.Manifest.DescribeItemHash(perk.Hash)),
            catalog.Manifest.FindItem(perk.Hash).Bind(ToIcon),
            perk.Element.IsSome() ? "Artifact perk" : "Artifact perk · not in Loopsmith's rules yet");

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
        new(LoopDesigning.ExplainElement(build, element), LoopDesigning.IsEquipped(build, element.Id), DescribeSource(element));

    /// <summary>The element's source card: the Compendium's tab and row (and its sheet), Clarity's entry, the rule file, a creator's link.</summary>
    private static InfoSource DescribeSource(BuildElement element) =>
        element.Source.Match(
            compendium => new InfoSource(
                "Destiny Data Compendium",
                element.Description,
                $"{compendium.Tab} tab, row {compendium.Row.ToString(CultureInfo.InvariantCulture)} · snapshot of {compendium.Snapshot.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
                Optional.Some(new SourceLink(CompendiumSheet, "Open the Compendium")),
                Optional.None<string>()),
            clarity => new InfoSource(
                "Clarity",
                element.Description,
                $"Clarity {clarity.ClarityVersion} · item {clarity.Hash.Value.ToString(CultureInfo.InvariantCulture)}",
                Optional.None<SourceLink>(),
                Optional.None<string>()),
            authored => new InfoSource(
                "Loopsmith's own rule",
                element.Description,
                $"{authored.RuleFile}, line {authored.Line.ToString(CultureInfo.InvariantCulture)} · no Compendium or Clarity row cited",
                Optional.None<SourceLink>(),
                Optional.None<string>()),
            claim => new InfoSource(
                "A creator's claim",
                element.Description,
                "Not checked against the game data (Compendium or Clarity) yet",
                Optional.Some(new SourceLink(claim.SourceUrl, "Open the source")),
                Optional.Some(claim.Quote)));

    /// <summary>The Destiny Data Compendium's public sheet (README, Licensing: credited and linked, never served as text).</summary>
    private const string CompendiumSheet = "https://docs.google.com/spreadsheets/d/1WaxvbLx7UoSZaBqdFr1u32F2uWVLo-CJunJB4nlGUE4/edit";

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
