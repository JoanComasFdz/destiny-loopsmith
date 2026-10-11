using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// Where an element of a step comes from, as a heading: what kind of thing it is and the item it sits on —
/// "Fragment · Arcstrider", "Weapon perk · Festival Flight", "Armor mod · Luminopotent Mask", "Artifact perk · Tablet of
/// Ruin", "Exotic armor", "Keyword" — with that item's icon (none when the build doesn't show one).
/// </summary>
public sealed record OutcomeOrigin(string Label, Optional<string> Icon, Affinity Affinity);

/// <summary>An origin and the elements an event set off from it, in the order they fired.</summary>
public sealed record OriginGroup(OutcomeOrigin Origin, ImmutableArray<SourceOutcomes> Sources);

/// <summary>Pure: an event's elements grouped by where they come from, the groups in the order their first element fired.</summary>
public static class OutcomeOrigins
{
    public static ImmutableArray<OriginGroup> GroupByOrigin(ValidatedBuild build, ImmutableArray<SourceOutcomes> sources) =>
    [
        .. sources
            .Select(source => (Origin: FindOrigin(build, source), Source: source))
            .GroupBy(entry => entry.Origin)
            .Select(group => new OriginGroup(group.Key, [.. group.Select(entry => entry.Source)])),
    ];

    private static OutcomeOrigin FindOrigin(ValidatedBuild build, SourceOutcomes source) =>
        source.Kind switch
        {
            ElementKind.Super or ElementKind.Grenade or ElementKind.Melee or ElementKind.ClassAbility or ElementKind.Aspect or ElementKind.Fragment =>
                DescribeSubclassOrigin(build, source.Kind),
            ElementKind.ExoticWeapon or ElementKind.WeaponPerk or ElementKind.Catalyst => DescribeWeaponOrigin(build, source),
            ElementKind.ArmorMod => DescribeModOrigin(build, source),
            ElementKind.ArmorSetBonus => DescribeSetOrigin(build.Catalog, source),
            ElementKind.ArtifactPerk => DescribeArtifactOrigin(build),
            ElementKind.ExoticArmor => new OutcomeOrigin("Exotic armor", Optional.None<string>(), Affinity.Neutral),
            _ => new OutcomeOrigin("Keyword", Optional.None<string>(), Affinity.Neutral),
        };

    /// <summary>An ability, an aspect or a fragment: the subclass it is part of (Arcstrider), with its icon.</summary>
    private static OutcomeOrigin DescribeSubclassOrigin(ValidatedBuild build, ElementKind kind)
    {
        var subclass = build.Catalog.FindSubclassItem(build.Build.Class, build.Build.Subclass);
        return new OutcomeOrigin(
            $"{kind.DescribeElementKind()} · {subclass.Map(item => item.Name).UnwrapOr("Subclass")}",
            subclass.Bind(item => item.Icon).Map(LoadoutShaping.ToIconUrl),
            build.Build.Subclass.ToAffinity());
    }

    /// <summary>A weapon's perk, an exotic weapon's own or its catalyst: the build's weapon that holds it, with its icon.</summary>
    private static OutcomeOrigin DescribeWeaponOrigin(ValidatedBuild build, SourceOutcomes source)
    {
        var label = source.Kind switch
        {
            ElementKind.ExoticWeapon => "Exotic weapon",
            ElementKind.Catalyst => "Catalyst",
            _ => "Weapon perk",
        };
        return build.Build.Weapons
            .Where(weapon => LoopDesigning.ListPerkSlots(build, weapon).Any(slot => IsHeldBy(build.Catalog, slot, source.Source)))
            .Select(weapon => Optional.Some(new OutcomeOrigin(
                $"{label} · {weapon.Name}",
                weapon.Hash.Bind(hash => build.Catalog.Manifest.FindItem(hash)).Bind(item => item.Icon).Map(LoadoutShaping.ToIconUrl),
                weapon.Type.ToAffinity())))
            .FindFirstSome()
            .UnwrapOr(new OutcomeOrigin(label, Optional.None<string>(), Affinity.Neutral));
    }

    private static bool IsHeldBy(RuleCatalog catalog, WeaponPerkSlot slot, ElementId element)
    {
        var hashes = catalog.Elements.TryGetValue(element, out var found) ? found.Hashes : [];
        return slot.Match(
            intrinsic => hashes.Contains(intrinsic.Perk),
            named => named.Perk == element,
            fixedPerk => hashes.Contains(fixedPerk.Perk),
            rolling => rolling.Perk.Match(perk => hashes.Contains(perk.Value), _ => false),
            catalyst => catalyst.On && hashes.Contains(catalyst.Plug));
    }

    /// <summary>An armor mod: the armor piece of its slot the build shows (its icon), else the slot; a general mod is "General".</summary>
    private static OutcomeOrigin DescribeModOrigin(ValidatedBuild build, SourceOutcomes source) =>
        build.Catalog.FindElementItem(source.Source).Bind(item => item.ReadArmorSlot()).Match(
            slot => FindArmorPiece(build, slot.Value).Match(
                piece => new OutcomeOrigin($"Armor mod · {piece.Value.Name}", piece.Value.Icon.Map(LoadoutShaping.ToIconUrl), Affinity.Neutral),
                _ => new OutcomeOrigin($"Armor mod · {slot.Value.DescribeArmorSlot()}", Optional.None<string>(), Affinity.Neutral)),
            _ => new OutcomeOrigin("Armor mod · General", Optional.None<string>(), Affinity.Neutral));

    /// <summary>The build's armor piece in a slot, when the excerpt knows it: its exotic, else a piece the DIM loadout wore.</summary>
    private static Optional<ManifestItem> FindArmorPiece(ValidatedBuild build, ArmorSlot slot)
    {
        var catalog = build.Catalog;
        var exotic = build.Build.ExoticArmor.Bind(id => catalog.FindElementItem(id));
        var worn = build.Build.LeftOut.Where(entry => entry.Part == LoadoutPart.Item).Select(entry => catalog.Manifest.FindItem(entry.Hash));
        return worn.Prepend(exotic)
            .Select(item => item.Bind(found => found.Kind is ManifestKind.Armor armor && armor.Slot == slot ? Optional.Some(found) : Optional.None<ManifestItem>()))
            .FindFirstSome();
    }

    /// <summary>An armor set bonus: its set, by name.</summary>
    private static OutcomeOrigin DescribeSetOrigin(RuleCatalog catalog, SourceOutcomes source) =>
        (catalog.Elements.TryGetValue(source.Source, out var element) ? element.Hashes : [])
            .Select(hash => catalog.Manifest.FindSetOfPerk(hash))
            .FindFirstSome()
            .Match(
                found => new OutcomeOrigin($"Armor set · {found.Value.Set.Name}", Optional.None<string>(), Affinity.Neutral),
                _ => new OutcomeOrigin("Armor set", Optional.None<string>(), Affinity.Neutral));

    /// <summary>An artifact perk: the build's artifact (the one that unlocks the most of its perks), with its icon.</summary>
    private static OutcomeOrigin DescribeArtifactOrigin(ValidatedBuild build) =>
        LoadoutShaping.FindArtifact(build).Match(
            artifact => new OutcomeOrigin($"Artifact perk · {artifact.Value.Name}", artifact.Value.Icon.Map(LoadoutShaping.ToIconUrl), Affinity.Neutral),
            _ => new OutcomeOrigin("Artifact perk", Optional.None<string>(), Affinity.Neutral));
}
