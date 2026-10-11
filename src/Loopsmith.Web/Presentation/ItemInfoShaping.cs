using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.BuildExplanation;
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

/// <summary>
/// Where an element's facts come from, as a card of its own, each with its title, Loopsmith's words for it (a
/// paraphrase, never the source's own text) and which row or entry it is: a Compendium row, with its sheet; Clarity's
/// entry or Loopsmith's own rule (no row cited); a creator's claim, with its link and their words.
/// </summary>
[Union]
public partial record InfoSource
{
    partial record Compendium(string Title, Optional<string> Paraphrase, string Cite, string SheetUrl);
    partial record Cited(string Title, Optional<string> Paraphrase, string Cite);
    partial record Claim(string Title, Optional<string> Paraphrase, string Cite, string Url, string Quote);
}

/// <summary>A section's heading: a perk's name, icon and what it is ("Intrinsic", "Trait · column 1", "Exotic perk").</summary>
public sealed record SectionHeading(string Name, Optional<string> Icon, string Label);

/// <summary>
/// One part of an item's details, each with Bungie's text and what the rules say (none: not in the rules yet): the
/// item itself; one of its perks (its intrinsic, an exotic armor's perk, what a column holds); a column not picked yet;
/// an exotic's catalyst, with its switch (its heading when it is part of the weapon's details); an artifact, with the
/// perks of it the build has (the artifact itself sets nothing off: its perks do); a status without an element of its own,
/// with the glossary's facts; what a status switches on in this build (other elements' passives while it is there).
/// </summary>
[Union]
public partial record InfoSection
{
    partial record Item(Optional<string> BungieText, Optional<InfoRules> Rules);
    partial record Perk(SectionHeading Heading, Optional<string> BungieText, Optional<InfoRules> Rules);
    partial record Unpicked(int Column);
    partial record Catalyst(Optional<SectionHeading> Heading, Optional<string> BungieText, Optional<InfoRules> Rules, CatalystToggle Toggle);
    partial record Artifact(Optional<string> BungieText, ImmutableArray<SectionHeading> Perks);
    partial record Status(Optional<string> Facts);
    partial record SwitchesOn(string Heading, ImmutableArray<SwitchedOnItem> Effects);
}

/// <summary>An effect a status switches on in the build, as its details list it: the element's heading, what it does, where it reaches.</summary>
public sealed record SwitchedOnItem(SectionHeading Heading, string Text, Optional<string> Note);

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
            status => ShapeStatus(build, status.Status),
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
            item => new[] { item.BungieText, item.Rules.Bind(rules => ReadParaphrase(rules.Source)) }.FindFirstSome(),
            perk => new[] { perk.BungieText, perk.Rules.Bind(rules => ReadParaphrase(rules.Source)) }.FindFirstSome(),
            _ => Optional.None<string>(),
            catalyst => new[] { catalyst.BungieText, catalyst.Rules.Bind(rules => ReadParaphrase(rules.Source)) }.FindFirstSome(),
            artifact => artifact.BungieText,
            status => status.Facts,
            _ => Optional.None<string>());

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
            [section, .. FindStatusOf(catalog, element.Id).SelectMany(status => DescribeSwitchesOn(build, status))]);
    }

    /// <summary>
    /// A status of the state: its element's details when it has one (Bolt Charge, Woven Mail), else the glossary's facts
    /// (Amplified: no rules of its own) — either way with what it switches on in this build.
    /// </summary>
    private static Optional<ItemInfo> ShapeStatus(ValidatedBuild build, StatusId status) =>
        Optional.Some(build.Catalog.Elements.Values
            .Where(element => element.Id.Value == status.Value)
            .Take(1)
            .Select(element => Optional.Some(ShapeElement(build, element)))
            .FindFirstSome()
            .Match(own => own.Value, _ => ShapeBareStatus(build, status)));

    /// <summary>A status the rules have no element for (Amplified): the glossary's facts, and what it switches on.</summary>
    private static ItemInfo ShapeBareStatus(ValidatedBuild build, StatusId status)
    {
        var glossary = build.Catalog.Glossary;
        var facts = glossary.DescribeStatusFacts(status);
        return new ItemInfo(
            glossary.DescribeStatus(status),
            glossary.DescribeStatusKind(status),
            Optional.None<string>(),
            Optional.None<ItemTier>(),
            glossary.ReadStatusAffinity(status),
            Optional.None<string>(),
            [new InfoSection.Status(facts.Length > 0 ? Optional.Some(facts) : Optional.None<string>()), .. DescribeSwitchesOn(build, status)]);
    }

    /// <summary>The status an element of the same id is (Bolt Charge, Woven Mail), when the glossary has one.</summary>
    private static IEnumerable<StatusId> FindStatusOf(RuleCatalog catalog, ElementId element) =>
        catalog.Glossary.Statuses.Keys.Where(status => status.Value == element.Value);

    /// <summary>"While Amplified, in this build": what the status switches on (none: no section).</summary>
    private static IEnumerable<InfoSection> DescribeSwitchesOn(ValidatedBuild build, StatusId status)
    {
        var effects = LoopDesigning.ExplainSwitchedOn(build, status);
        return effects.IsEmpty
            ? []
            : [new InfoSection.SwitchesOn(
                $"While {build.Catalog.Glossary.DescribeStatus(status)}, in this build",
                [.. effects.Select(effect => new SwitchedOnItem(DescribeEffectHeading(build, effect), effect.Text, effect.Note))])];
    }

    /// <summary>The element that has the effect: its name, icon and what it is ("Luminopotent · 2-piece perk", "Arc Fragment").</summary>
    private static SectionHeading DescribeEffectHeading(ValidatedBuild build, SwitchedOnLine effect)
    {
        var item = build.Catalog.FindElementItem(effect.Source);
        return new SectionHeading(
            effect.SourceName,
            LoadoutShaping.FindElementIcon(build.Catalog, effect.Source),
            item.Match(known => DescribeType(build.Catalog, known.Value), _ => effect.SourceKind.DescribeElementKind()));
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

    /// <summary>The element's source card: the Compendium's row (and its sheet), Clarity's entry, the rule file, a creator's link.</summary>
    private static InfoSource DescribeSource(BuildElement element)
    {
        var title = element.Source.DescribeProvenanceTitle();
        var cite = element.Source.DescribeProvenanceCite();
        return element.Source.Match<InfoSource>(
            _ => new InfoSource.Compendium(title, element.Description, cite, CompendiumSheet.Url),
            _ => new InfoSource.Cited(title, element.Description, cite),
            _ => new InfoSource.Cited(title, element.Description, cite),
            claim => new InfoSource.Claim(title, element.Description, cite, claim.SourceUrl, claim.Quote));
    }

    private static Optional<string> ReadParaphrase(InfoSource source) =>
        source.Match(compendium => compendium.Paraphrase, cited => cited.Paraphrase, claim => claim.Paraphrase);

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
