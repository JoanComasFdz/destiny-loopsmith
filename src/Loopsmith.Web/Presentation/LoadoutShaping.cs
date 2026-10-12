using System.Collections.Immutable;
using System.Globalization;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// What tapping a square opens: an element of the rules (and Bungie's text for its item), an item only the manifest
/// excerpt knows, an exotic weapon (its intrinsic, perks and catalyst) or its catalyst; a status of the state (Amplified,
/// which has no element: the glossary's facts and what it switches on). Legendary armor and weapons open nothing: their
/// perks and mods carry what there is to know.
/// </summary>
[Union]
public partial record ItemDetails
{
    partial record OfElement(ElementId Element);
    partial record OfStatus(StatusId Status);
    partial record OfItem(ItemHash Hash);
    partial record OfWeapon(int Weapon);
    partial record OfCatalyst(int Weapon);
}

/// <summary>
/// One square of the DIM-style loadout: the item's icon (a bungie.net address) or none, its name, colour and rarity,
/// whether the rules know it (an item the DIM loadout had but the build leaves out is shown, marked ⊘), and what tapping
/// it opens (none: nothing to show). <see cref="Enhanced"/>: a weapon perk's enhanced trait, drawn as the game draws it.
/// </summary>
public sealed record IconTile(
    string Name,
    Optional<string> Icon,
    Affinity Affinity,
    Optional<ItemTier> Tier,
    bool InRules,
    Optional<string> Description,
    int Copies,
    Optional<ItemDetails> Details,
    bool Enhanced = false);

/// <summary>An ability's square in the Subclass box: its plug's, or an empty slot (a build started from scratch), named for its kind.</summary>
[Union]
public partial record AbilityTile
{
    partial record Shown(IconTile Tile);
    partial record Empty(string Slot);
}

/// <summary>The subclass block: the subclass item, its super, the abilities (class, jump, melee, grenade), aspects, fragments.</summary>
public sealed record SubclassView(
    Optional<IconTile> Subclass,
    AbilityTile Super,
    ImmutableArray<AbilityTile> Abilities,
    ImmutableArray<IconTile> Aspects,
    ImmutableArray<IconTile> Fragments);

/// <summary>One perk a weapon's trait column rolls with, as the picker offers it: its hash, its tile (the rules' element when they know it) and its type as the game words it ("Enhanced Trait").</summary>
public sealed record PerkOption(ItemHash Hash, IconTile Tile, Optional<string> Type);

/// <summary>The perk a rolling column holds: its hash (which option it is) and its tile.</summary>
public sealed record PickedPerk(ItemHash Hash, IconTile Tile);

/// <summary>
/// One perk square of a weapon, from the build's <see cref="WeaponPerkSlot"/>s: a perk that just shows (one the build
/// names in no column, or a column that doesn't roll), or a column that rolls — the perk it holds, if any ("?"), and
/// what it can roll with.
/// </summary>
[Union]
public partial record PerkSquare
{
    partial record Shown(IconTile Perk);
    partial record Pickable(int Column, Optional<PickedPerk> Perk, ImmutableArray<PerkOption> Options);
}

/// <summary>A champion breaker on a weapon's square (Disruption): its name and icon, a bungie.net address.</summary>
public sealed record BreakerBadge(string Name, string Icon);

/// <summary>A copy's gear tier on its square, with the overlay of its diamonds (Bungie's; none when the excerpt lacks it).</summary>
public sealed record TierMark(GearTier Tier, Optional<string> Overlay);

/// <summary>A masterworked copy's mark: a gold border, and the glow over its icon (an exotic's its own; none when the excerpt lacks it).</summary>
public sealed record MasterworkMark(Optional<string> Glow);

/// <summary>What DIM draws in a square's top-right corner: a legendary weapon's frame, or an Armor 3.0 piece's stat focus.</summary>
public enum CornerKind { Frame, StatFocus }

/// <summary>The top-right corner of a square: what it shows (Adaptive Frame, a Weapons focus) and its icon, a bungie.net address.</summary>
public sealed record CornerBadge(CornerKind Kind, string Name, string Icon);

/// <summary>
/// What DIM draws on a copy's square: its gear tier, its masterwork, a weapon's champion breaker, the item's season
/// watermark (top-left), its corner (top-right: a legendary weapon's frame, an armor piece's stat focus) and its power
/// level (in the footer). The tier, masterwork, stat focus and power are the player's copy's, after their Bungie login;
/// the watermark, a weapon's frame and its own breaker (an exotic's, a frame's) show before.
/// </summary>
public sealed record TileMarks(
    Optional<TierMark> Tier,
    Optional<MasterworkMark> Masterwork,
    Optional<BreakerBadge> Breaker,
    Optional<string> Watermark,
    Optional<CornerBadge> Corner,
    Optional<PowerLevel> Power);

/// <summary>An exotic's catalyst square: the catalyst item's tile and whether the build turns it on.</summary>
public sealed record CatalystSquare(IconTile Tile, bool On);

/// <summary>
/// A weapon: where it is in the build (<see cref="Index"/>, what a pick names), its tile, its light.gg page, slot,
/// archetype, damage type (and that type's icon), its marks (tier, masterwork, breaker), its perk squares — one per
/// trait column the excerpt gives, then the perks the build names in none of them — and an exotic's catalyst.
/// </summary>
public sealed record WeaponView(
    int Index,
    IconTile Weapon,
    TileMarks Marks,
    Optional<string> Page,
    WeaponSlot Slot,
    Optional<string> Archetype,
    DamageType Type,
    Optional<string> DamageIcon,
    ImmutableArray<PerkSquare> Perks,
    Optional<CatalystSquare> Catalyst);

/// <summary>An exotic's catalyst turned on or off in the designer (weapon 0-based, in the build's order).</summary>
public sealed record CatalystToggle(int Weapon, bool On);

/// <summary>A set perk's square: its tile, how many pieces it needs and whether it is active.</summary>
public sealed record SetPerkView(IconTile Tile, int Pieces, bool Active);

/// <summary>An armor set: its name, the pieces the build wears (none: the build doesn't say) and its perks.</summary>
public sealed record ArmorSetView(string Name, Optional<int> Pieces, ImmutableArray<SetPerkView> Perks);

/// <summary>A perk picked in the designer: weapon <see cref="Weapon"/>'s column <see cref="Column"/> (both 0-based, in the build's order), or back to "?" when none.</summary>
public sealed record PerkPick(int Weapon, int Column, Optional<ItemHash> Perk);

/// <summary>A piece picked from the player's armor for a slot of the build: the copy, by its instance id.</summary>
public sealed record ArmorPick(ArmorSlot Slot, ItemInstanceId Instance);

/// <summary>A weapon picked from the player's weapons: where it goes in the build (in place of a weapon, or an empty slot), and the copy, by its instance id.</summary>
public sealed record WeaponPick(WeaponTarget Target, ItemInstanceId Instance);

/// <summary>A weapon of the game picked from "Every item": where it goes in the build, and the weapon, by its manifest hash.</summary>
public sealed record WeaponItemPick(WeaponTarget Target, ItemHash Item);

/// <summary>An armor piece of the game picked from "Every item" for a slot of the build, by its manifest hash.</summary>
public sealed record ArmorItemPick(ArmorSlot Slot, ItemHash Item);

/// <summary>A weapon slot of the loadout: the build's weapon in it, or an empty slot (shown in the designer so ⋯ can fill it).</summary>
[Union]
public partial record WeaponRow
{
    partial record Worn(WeaponView Weapon);
    partial record Empty(WeaponSlot Slot);
}

/// <summary>An armor slot: the piece in it (when the loadout names one), its copy's marks and the mods that only fit that slot, one tile each.</summary>
public sealed record ArmorView(ArmorSlot Slot, Optional<IconTile> Piece, TileMarks Marks, ImmutableArray<IconTile> Mods);

/// <summary>
/// The artifact block: the season's artifact the perks are from (when the manifest excerpt has it: tapping it shows
/// Bungie's text) and the artifact perks the build has, then those the DIM loadout had that the rules don't know.
/// </summary>
public sealed record ArtifactView(Optional<IconTile> Item, ImmutableArray<IconTile> Perks);

/// <summary>
/// A build laid out like DIM shows a loadout: subclass, artifact, weapons (kinetic, energy, power), armor with each
/// piece's mods, general mods, the armor sets with their perks, and set bonuses in no set the excerpt has.
/// </summary>
public sealed record LoadoutView(
    SubclassView Subclass,
    ArtifactView Artifact,
    ImmutableArray<WeaponView> Weapons,
    ImmutableArray<ArmorView> Armor,
    ImmutableArray<IconTile> GeneralMods,
    ImmutableArray<ArmorSetView> ArmorSets,
    ImmutableArray<IconTile> SetBonuses);

/// <summary>
/// Pure: a validated build (and the player's profile, after their Bungie login: the marks of their copies) → its
/// DIM-style view: where each tile goes. What an element, a hash or a subclass is comes from
/// the kernel (<see cref="ManifestReading"/>, over the manifest excerpt <c>rules/manifest.yaml</c>); what the excerpt
/// doesn't know has no icon and keeps its catalog name (or "?"). Left-out items (a DIM loadout's,
/// <see cref="Build.LeftOut"/>) sit where DIM would show them, marked as not in the rules: a mod under the armor piece of
/// the slot the manifest gives it, every other mod general; an unknown ability as the left-out subclass plug of its kind.
/// </summary>
public static class LoadoutShaping
{
    private const string IconHost = "https://www.bungie.net";
    private const string PageHost = "https://www.light.gg/db/items/";

    private static readonly ImmutableArray<ArmorSlot> ArmorSlots = [ArmorSlot.Helmet, ArmorSlot.Arms, ArmorSlot.Chest, ArmorSlot.Legs, ArmorSlot.ClassItem];
    private static readonly ImmutableArray<WeaponSlot> WeaponSlots = [WeaponSlot.Kinetic, WeaponSlot.Energy, WeaponSlot.Power];

    private sealed record LeftOutEntry(LoadoutPart Part, Optional<ManifestItem> Item, ItemHash Hash, LeftOutReason Reason);

    public static LoadoutView ShapeLoadout(ValidatedBuild build, Optional<BungieProfile> profile)
    {
        var b = build.Build;
        var catalog = build.Catalog;
        var leftOut = b.LeftOut.Select(item => new LeftOutEntry(item.Part, catalog.Manifest.FindItem(item.Hash), item.Hash, LoopDesigning.ClassifyLeftOut(build, item))).ToImmutableArray();
        var plugs = leftOut.Where(entry => entry.Part == LoadoutPart.SubclassPlug).ToImmutableArray();
        var mods = b.ArmorMods
            .Select(id => (Tile: DescribeElement(catalog, id), Slot: catalog.FindElementItem(id).Bind(item => item.ReadArmorSlot())))
            .Concat(leftOut.Where(entry => entry.Part == LoadoutPart.ArmorMod)
                .Select(entry => (Tile: DescribeLeftOut(catalog, entry), Slot: entry.Item.Bind(item => item.ReadArmorSlot()))))
            .ToImmutableArray();
        var pieces = leftOut
            .Where(entry => entry.Part == LoadoutPart.Item && IsKind<ManifestKind.Armor>(entry.Item))
            .Select(entry => (Slot: entry.Item.Bind(item => item.ReadArmorSlot()), Tile: DescribeLeftOut(catalog, entry)))
            .Concat(ToSome(b.ExoticArmor).Select(id => (Slot: catalog.FindElementItem(id).Bind(item => item.ReadArmorSlot()), Tile: DescribeElement(catalog, id))))
            .ToImmutableArray();

        var subclass = new SubclassView(
            catalog.FindSubclassItem(b.Class, b.Subclass).Map(item => DescribeItem(item, item.Name, true, Optional.None<string>(), b.Subclass.ToAffinity(),
                Optional.Some<ItemDetails>(new ItemDetails.OfItem(item.Hash)))),
            DescribeAbility<ManifestKind.Super>(catalog, b.Abilities.Super, "Super", plugs),
            [
                DescribeAbility<ManifestKind.ClassAbility>(catalog, b.Abilities.ClassAbility, "Class ability", plugs),
                .. DescribeLeftOutPlugs<ManifestKind.Movement>(catalog, plugs).Select(tile => (AbilityTile)new AbilityTile.Shown(tile)),
                DescribeAbility<ManifestKind.Melee>(catalog, b.Abilities.Melee, "Melee", plugs),
                DescribeAbility<ManifestKind.Grenade>(catalog, b.Abilities.Grenade, "Grenade", plugs),
            ],
            [.. b.Aspects.Select(id => DescribeElement(catalog, id)), .. DescribeLeftOutPlugs<ManifestKind.Aspect>(catalog, plugs)],
            [.. b.Fragments.Select(id => DescribeElement(catalog, id)), .. DescribeLeftOutPlugs<ManifestKind.Fragment>(catalog, plugs)]);
        return new LoadoutView(
            subclass,
            DescribeArtifact(build, [.. leftOut.Where(entry => entry.Part == LoadoutPart.ArtifactPerk)]),
            [.. b.Weapons.Select((weapon, index) => DescribeWeapon(build, profile, weapon, index)).OrderBy(view => view.Slot)],
            [
                .. ArmorSlots
                    .Select(slot => DescribeArmorRow(
                        build,
                        profile,
                        slot,
                        pieces.Where(piece => piece.Slot == Optional.Some(slot)).Select(piece => Optional.Some(piece.Tile)).FindFirstSome(),
                        [.. mods.Where(mod => mod.Slot == Optional.Some(slot)).Select(mod => mod.Tile)]))
                    .Where(view => view.Piece.IsSome() || !view.Mods.IsEmpty),
            ],
            GroupCopies(mods.Where(mod => !mod.Slot.IsSome()).Select(mod => mod.Tile)),
            [.. LoopDesigning.ListArmorSets(build).Select(set => DescribeArmorSet(catalog, set))],
            GroupCopies(LoopDesigning.ListLooseSetBonuses(build).Select(id => DescribeElement(catalog, id))));
    }

    /// <summary>
    /// The weapon rows to show: the build's weapons (kinetic, energy, power); with every slot (the designer's), an empty
    /// one where the build has none.
    /// </summary>
    public static ImmutableArray<WeaponRow> ListWeaponRows(LoadoutView view, bool everySlot) =>
    [
        .. view.Weapons.Select(weapon => (Slot: weapon.Slot, Row: (WeaponRow)new WeaponRow.Worn(weapon)))
            .Concat(everySlot
                ? WeaponSlots.Where(slot => !view.Weapons.Any(weapon => weapon.Slot == slot)).Select(slot => (Slot: slot, Row: (WeaponRow)new WeaponRow.Empty(slot)))
                : [])
            .OrderBy(entry => entry.Slot)
            .Select(entry => entry.Row),
    ];

    /// <summary>A weapon's square, by its manifest item: its name, icon and rarity (the weapon picker's; it opens nothing).</summary>
    public static IconTile DescribeWeaponItem(ManifestItem item) =>
        DescribeItem(item, item.Name, true, Optional.None<string>(), ToAffinity(item), Optional.None<ItemDetails>());

    /// <summary>A weapon perk by hash, as its square shows: its catalog element when the rules have it, else the manifest's (marked ⊘).</summary>
    public static IconTile DescribePerk(RuleCatalog catalog, ItemHash hash) =>
        DescribeTrait(catalog, hash);

    /// <summary>The icon of a damage type (a bungie.net address), when the excerpt has it.</summary>
    public static Optional<string> FindDamageIcon(RuleCatalog catalog, DamageType type) =>
        catalog.Manifest.DamageTypeIcons.TryGetValue(type, out var icon) ? Optional.Some(ToIconUrl(icon)) : Optional.None<string>();

    /// <summary>An armor slot's row: its piece, with the marks of the player's copy of it (after their login), and its mods.</summary>
    private static ArmorView DescribeArmorRow(ValidatedBuild build, Optional<BungieProfile> profile, ArmorSlot slot, Optional<IconTile> piece, ImmutableArray<IconTile> mods) =>
        new(
            slot,
            piece,
            DescribeMarks(
                build.Catalog,
                FindArmorPiece(build, slot),
                profile.Bind(owned => LoopDesigning.FindArmorMarks(build, owned, slot)),
                Optional.None<BreakerType>(),
                profile.Bind(owned => LoopDesigning.FindStatFocus(build, owned, slot)).Map(DescribeStatFocus)),
            mods);

    /// <summary>The armor rows to show: the slots with a piece or a mod; every slot (the designer's), an empty one where the build has none.</summary>
    public static ImmutableArray<ArmorView> ListArmorRows(LoadoutView view, bool everySlot) =>
        everySlot
            ? [.. ArmorSlots.Select(slot => view.Armor.Where(row => row.Slot == slot).Select(Optional.Some).FindFirstSome()
                .UnwrapOr(new ArmorView(slot, Optional.None<IconTile>(), NoMarks, [])))]
            : view.Armor;

    /// <summary>An armor piece (the player's, or of the game) as a square: its name, icon and rarity (it opens nothing: the picker shows its details).</summary>
    public static IconTile DescribeArmorItem(ManifestItem item) =>
        DescribeItem(item, item.Name, true, Optional.None<string>(), Affinity.Neutral, Optional.None<ItemDetails>());

    /// <summary>light.gg's page for an item of the manifest (a sandbox perk has none).</summary>
    public static Optional<string> ToPageUrl(RuleCatalog catalog, ItemHash hash) =>
        catalog.Manifest.FindItem(hash).Bind(item => item.Kind is ManifestKind.Perk
            ? Optional.None<string>()
            : Optional.Some(PageHost + hash.Value.ToString(CultureInfo.InvariantCulture) + "/"));

    /// <summary>The icon of the build's subclass item (Arcstrider for an Arc Hunter), when the glossary and the excerpt have it.</summary>
    public static Optional<string> FindSubclassIcon(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        catalog.FindSubclassItem(guardianClass, subclass).Bind(item => item.Icon).Map(ToIconUrl);

    /// <summary>
    /// The build's armor piece in a slot, as the build panel shows it: a piece the DIM loadout wore, else the build's
    /// exotic; none when the excerpt knows neither.
    /// </summary>
    public static Optional<ManifestItem> FindArmorPiece(ValidatedBuild build, ArmorSlot slot)
    {
        var catalog = build.Catalog;
        var worn = build.Build.LeftOut.Where(entry => entry.Part == LoadoutPart.Item).Select(entry => catalog.Manifest.FindItem(entry.Hash));
        var exotic = build.Build.ExoticArmor.Bind(id => catalog.FindElementItem(id));
        return worn.Append(exotic)
            .Select(item => item.Bind(found => found.Kind is ManifestKind.Armor armor && armor.Slot == slot ? Optional.Some(found) : Optional.None<ManifestItem>()))
            .FindFirstSome();
    }

    /// <summary>A square with no marks: one that isn't a copy (a perk, a mod, an empty slot).</summary>
    public static readonly TileMarks NoMarks = new(
        Optional.None<TierMark>(), Optional.None<MasterworkMark>(), Optional.None<BreakerBadge>(), Optional.None<string>(), Optional.None<CornerBadge>(), Optional.None<PowerLevel>());

    /// <summary>
    /// A copy's marks, from Core's (its tier, masterwork, power; none: not the player's copy), its breaker and corner, and
    /// its item's watermark: the overlays are the excerpt's, an exotic's masterwork glow its own.
    /// </summary>
    public static TileMarks DescribeMarks(RuleCatalog catalog, Optional<ManifestItem> item, Optional<GearMarks> marks, Optional<BreakerType> breaker, Optional<CornerBadge> corner)
    {
        var overlays = catalog.Manifest.Overlays;
        var masterworked = marks.Match(found => found.Value.Masterworked, _ => false);
        return new TileMarks(
            marks.Bind(found => found.Tier).Map(found => new TierMark(found, overlays.FindTierOverlay(found).Map(ToIconUrl))),
            masterworked ? Optional.Some(new MasterworkMark(overlays.FindMasterworkOverlay(item.Bind(found => found.Tier)).Map(ToIconUrl))) : Optional.None<MasterworkMark>(),
            breaker.Map(found => new BreakerBadge(found.Name, ToIconUrl(found.Icon))),
            item.Bind(found => found.Watermark).Map(ToIconUrl),
            corner,
            marks.Bind(found => found.Power));
    }

    /// <summary>A legendary weapon's top-right corner: its intrinsic frame (Adaptive Frame), as DIM shows it; none for an exotic.</summary>
    public static Optional<CornerBadge> DescribeFrame(ValidatedBuild build, ManifestItem weapon) =>
        LoopDesigning.FindWeaponFrame(build, weapon).Bind(frame => frame.Icon.Map(icon => new CornerBadge(CornerKind.Frame, frame.Name, ToIconUrl(icon))));

    /// <summary>An Armor 3.0 piece's top-right corner: the stat it focuses on (its highest rolled stat), as DIM shows it.</summary>
    public static CornerBadge DescribeStatFocus(ArmorStatIcon stat) =>
        new(CornerKind.StatFocus, $"{stat.Name} focus", ToIconUrl(stat.Icon));

    /// <summary>A square's corner class: "frame" or "stat-focus".</summary>
    public static string ToCornerClass(CornerBadge corner) =>
        corner.Kind == CornerKind.Frame ? "frame" : "stat-focus";

    /// <summary>A copy's tier as its square's <c>data-tier</c> says it ("4"); empty when Loopsmith doesn't know it.</summary>
    public static string ToTierText(TileMarks marks) =>
        marks.Tier.Match(tier => tier.Value.Tier.Value.ToString(CultureInfo.InvariantCulture), _ => "");

    /// <summary>A masterworked copy's square class ("masterwork": the gold border), else none.</summary>
    public static string ToMasterworkClass(TileMarks marks) =>
        marks.Masterwork.IsSome() ? "masterwork" : "";

    /// <summary>A square's <c>data-masterwork</c>: "true" for a masterworked copy.</summary>
    public static string ToMasterworkText(TileMarks marks) =>
        marks.Masterwork.IsSome() ? "true" : "false";

    /// <summary>A square's tooltip with its marks after it: "Thunderlord — … · Tier 4 · Masterworked · Disruption".</summary>
    public static string AddMarks(string tooltip, TileMarks marks) =>
        string.Join(" · ", new[]
        {
            tooltip,
            marks.Tier.Match(tier => $"Tier {tier.Value.Tier.Value}", _ => ""),
            marks.Masterwork.IsSome() ? "Masterworked" : "",
            marks.Breaker.Match(breaker => breaker.Value.Name, _ => ""),
        }.Where(part => part.Length > 0));

    /// <summary>The bungie.net address of a manifest icon path.</summary>
    public static string ToIconUrl(string path) => IconHost + path;

    /// <summary>An icon's address: a manifest icon's on bungie.net, or a drawn one's (<see cref="IconDrawing"/>).</summary>
    public static string ToIconUrl(IconRef icon) =>
        icon.Match(
            manifest => ToIconUrl(manifest.Path),
            drawn => IconDrawing.DrawIcon(drawn.Shape, drawn.Affinity));

    /// <summary>An element's icon: its manifest item's, else (a keyword's element) the keyword's own icon; none when the excerpt has neither.</summary>
    public static Optional<string> FindElementIcon(RuleCatalog catalog, ElementId id) =>
        LoopDesigning.FindElementIcon(catalog, id).Map(ToIconUrl);

    private static IconTile DescribeElement(RuleCatalog catalog, ElementId id) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? catalog.FindElementItem(id).Match(
                item => DescribeItem(item.Value, element.Name, true, element.Description, element.Affinity, ToElementDetails(id)),
                _ => new IconTile(element.Name, Optional.None<string>(), element.Affinity, Optional.None<ItemTier>(), true, element.Description, 1, ToElementDetails(id)))
            : new IconTile(id.Value, Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), true, Optional.None<string>(), 1, Optional.None<ItemDetails>());

    /// <summary>The build's artifact (Orchestration decides which) and its perks: the build's, then the left-out ones.</summary>
    private static ArtifactView DescribeArtifact(ValidatedBuild build, ImmutableArray<LeftOutEntry> leftOut)
    {
        var catalog = build.Catalog;
        var item = LoopDesigning.FindArtifact(build).Map(found =>
            DescribeItem(found.Item, found.Item.Name, true, found.Item.Description, Affinity.Neutral, Optional.Some<ItemDetails>(new ItemDetails.OfItem(found.Item.Hash))));
        return new ArtifactView(item, [.. build.Build.ArtifactPerks.Select(id => DescribeElement(catalog, id)), .. leftOut.Select(entry => DescribeLeftOut(catalog, entry))]);
    }

    /// <summary>
    /// A plug of the subclass editor as its square shows it: the rules' element when they know it for the build's class, else the manifest's item
    /// marked ⊘ (a jump has no part in a loop, so it carries none). It opens its details either way.
    /// </summary>
    public static IconTile DescribeSubclassPlug(ValidatedBuild build, ItemHash hash)
    {
        var catalog = build.Catalog;
        return LoopDesigning.FindSubclassElement(build, hash).Match(
            element => DescribeElement(catalog, element.Value.Id),
            _ =>
            {
                var left = new LeftOutItem(LoadoutPart.SubclassPlug, hash);
                return DescribeLeftOut(catalog, new LeftOutEntry(left.Part, catalog.Manifest.FindItem(hash), hash, LoopDesigning.ClassifyLeftOut(build, left)));
            });
    }

    /// <summary>The type the game words a plug with ("Arc Aspect", "Movement Ability"), when the excerpt has it.</summary>
    public static Optional<string> FindPlugType(ValidatedBuild build, ItemHash hash) =>
        build.Catalog.Manifest.FindItem(hash).Bind(item => item.Type);

    private static Optional<ItemDetails> ToElementDetails(ElementId id) => Optional.Some<ItemDetails>(new ItemDetails.OfElement(id));

    private static IconTile DescribeUnknownHash(RuleCatalog catalog, ItemHash hash) =>
        new(catalog.Manifest.DescribeItemHash(hash), Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), false,
            Optional.Some("Not in Loopsmith's rules or its manifest excerpt yet."), 1, Optional.None<ItemDetails>());

    private static IconTile DescribeItem(ManifestItem item, string name, bool inRules, Optional<string> description, Affinity affinity, Optional<ItemDetails> details) =>
        new(name, item.Icon.Map(ToIconUrl), affinity, item.Tier, inRules, description, 1, details);

    /// <summary>
    /// A hash the build left out: the manifest's name and icon when the excerpt has it, else just the hash — marked ⊘ when no
    /// rule knows it yet, not when it has no part in a loop (a set's armor piece, a jump: known, not missing). It opens its
    /// manifest details, unless it is legendary armor or a legendary weapon (DIM's perks and mods say more).
    /// </summary>
    private static IconTile DescribeLeftOut(RuleCatalog catalog, LeftOutEntry entry) =>
        entry.Item.Match(
            known => DescribeItem(
                known.Value,
                known.Value.Name,
                !entry.Reason.IsMissing(),
                Optional.Some(entry.Reason.Match(_ => "Not in Loopsmith's rules yet: left out of the build.", none => none.Why)),
                ToAffinity(known.Value),
                IsLegendaryGear(known.Value) ? Optional.None<ItemDetails>() : Optional.Some<ItemDetails>(new ItemDetails.OfItem(known.Value.Hash))),
            _ => DescribeUnknownHash(catalog, entry.Hash));

    private static bool IsLegendaryGear(ManifestItem item) =>
        item.Kind is ManifestKind.Armor or ManifestKind.Weapon && item.Tier != Optional.Some(ItemTier.Exotic);

    /// <summary>
    /// An ability slot: its element; or, when the build has "?", the left-out subclass plug of that kind the manifest
    /// names (marked ⊘); or a "?" tile; or, when it holds nothing, an empty slot named <paramref name="slot"/>.
    /// </summary>
    private static AbilityTile DescribeAbility<TKind>(RuleCatalog catalog, AbilityPlug ability, string slot, ImmutableArray<LeftOutEntry> plugs)
        where TKind : ManifestKind =>
        ability.Match<AbilityTile>(
            known => new AbilityTile.Shown(DescribeElement(catalog, known.Element)),
            _ => new AbilityTile.Shown(DescribeLeftOutPlugs<TKind>(catalog, plugs)
                .Select(Optional.Some)
                .FindFirstSome()
                .UnwrapOr(new IconTile(DomainPhrasing.Unknown, Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), false,
                    Optional.Some("Not known: not in the rule catalog yet, so it sets nothing off."), 1, Optional.None<ItemDetails>()))),
            _ => new AbilityTile.Empty(slot));

    private static IEnumerable<IconTile> DescribeLeftOutPlugs<TKind>(RuleCatalog catalog, ImmutableArray<LeftOutEntry> plugs)
        where TKind : ManifestKind =>
        plugs.Where(entry => IsKind<TKind>(entry.Item)).Select(entry => DescribeLeftOut(catalog, entry));

    private static WeaponView DescribeWeapon(ValidatedBuild build, Optional<BungieProfile> profile, WeaponLoadout weapon, int index)
    {
        var catalog = build.Catalog;
        var item = weapon.Hash.Bind(hash => catalog.Manifest.FindItem(hash));
        var details = item.Bind(known => known.Tier == Optional.Some(ItemTier.Exotic)
            ? Optional.Some<ItemDetails>(new ItemDetails.OfWeapon(index))
            : Optional.None<ItemDetails>());
        var tile = item.Match(
            known => DescribeItem(known.Value, weapon.Name, true, Optional.None<string>(), weapon.Type.ToAffinity(), details),
            _ => new IconTile(weapon.Name, Optional.None<string>(), weapon.Type.ToAffinity(), Optional.None<ItemTier>(), true, Optional.None<string>(), 1, details));
        var damageIcon = FindDamageIcon(catalog, weapon.Type);
        var slots = LoopDesigning.ListPerkSlots(build, weapon);
        var marks = profile.Bind(owned => LoopDesigning.FindWeaponMarks(owned, weapon));
        return new WeaponView(
            index,
            tile,
            DescribeMarks(catalog, item, marks, LoopDesigning.FindWeaponBreaker(build, weapon, marks), item.Bind(known => DescribeFrame(build, known))),
            weapon.Hash.Bind(hash => ToPageUrl(catalog, hash)),
            weapon.Slot,
            weapon.Archetype,
            weapon.Type,
            damageIcon,
            [.. slots.SelectMany(slot => DescribePerkSlot(catalog, slot))],
            slots.OfType<WeaponPerkSlot.Catalyst>().Select(catalyst => Optional.Some(DescribeCatalyst(catalog, catalyst, index))).FindFirstSome());
    }

    /// <summary>A perk square: the intrinsic and the catalyst have none (the weapon's details show the intrinsic; the catalyst has its own square).</summary>
    private static IEnumerable<PerkSquare> DescribePerkSlot(RuleCatalog catalog, WeaponPerkSlot slot) =>
        slot.Match<IEnumerable<PerkSquare>>(
            _ => [],
            named => [new PerkSquare.Shown(DescribeElement(catalog, named.Perk) with { Enhanced = LoopDesigning.IsEnhancedPerk(catalog, named.Perk) })],
            fixedPerk => [new PerkSquare.Shown(DescribeTrait(catalog, fixedPerk.Perk))],
            rolling =>
            [
                new PerkSquare.Pickable(
                    rolling.Column,
                    rolling.Perk.Map(hash => new PickedPerk(hash, DescribeTrait(catalog, hash))),
                    [.. rolling.Options.Select(hash => new PerkOption(hash, DescribeTrait(catalog, hash), catalog.Manifest.FindItem(hash).Bind(item => item.Type)))]),
            ],
            _ => []);

    private static CatalystSquare DescribeCatalyst(RuleCatalog catalog, WeaponPerkSlot.Catalyst catalyst, int weapon)
    {
        var details = Optional.Some<ItemDetails>(new ItemDetails.OfCatalyst(weapon));
        var element = catalog.FindElementByHash(catalyst.Plug);
        var tile = catalog.Manifest.FindItem(catalyst.Plug).Match(
            item => DescribeItem(item.Value, item.Value.Name, element.IsSome(), element.Bind(known => known.Description), Affinity.Neutral, details),
            _ => DescribeUnknownHash(catalog, catalyst.Plug) with { Details = details });
        return new CatalystSquare(tile, catalyst.On);
    }

    private static ArmorSetView DescribeArmorSet(RuleCatalog catalog, ArmorSetStatus set) =>
        new(set.Set.Name, set.Pieces.Map(pieces => pieces.Value), [.. set.Perks.Select(perk => new SetPerkView(
            perk.Element.Match(
                id => DescribeElement(catalog, id.Value),
                _ => catalog.Manifest.FindItem(perk.Perk.Perk).Match(
                    item => DescribeItem(item.Value, item.Value.Name, false, Optional.Some("Not in Loopsmith's rules yet."), Affinity.Neutral,
                        Optional.Some<ItemDetails>(new ItemDetails.OfItem(item.Value.Hash))),
                    _ => DescribeUnknownHash(catalog, perk.Perk.Perk))),
            perk.Perk.Pieces.Value,
            perk.Active))]);

    /// <summary>A perk of a weapon's roll by hash: its catalog element when the rules have it, else the manifest's (marked ⊘).</summary>
    private static IconTile DescribeTrait(RuleCatalog catalog, ItemHash hash) =>
        catalog.FindElementByHash(hash).Match(
            element => DescribeElement(catalog, element.Value.Id),
            _ => DescribeLeftOut(catalog, new LeftOutEntry(LoadoutPart.Item, catalog.Manifest.FindItem(hash), hash, new LeftOutReason.NotInRules())))
        with { Enhanced = LoopDesigning.IsEnhancedPerk(catalog, hash) };

    private static bool IsKind<TKind>(Optional<ManifestItem> item)
        where TKind : ManifestKind =>
        item.Match(known => known.Value.Kind is TKind, _ => false);

    private static Affinity ToAffinity(ManifestItem item) =>
        item.Kind is ManifestKind.Weapon { DamageType: Optional<DamageType>.Some type } ? type.Value.ToAffinity() : Affinity.Neutral;

    /// <summary>Copies of the same tile as one, with a count (general mods: "Grenade Mod ×5").</summary>
    private static ImmutableArray<IconTile> GroupCopies(IEnumerable<IconTile> tiles) =>
        [.. tiles.GroupBy(tile => (tile.Name, tile.Icon)).Select(group => group.First() with { Copies = group.Count() })];

    private static IEnumerable<T> ToSome<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}
