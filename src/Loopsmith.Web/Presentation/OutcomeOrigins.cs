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
/// Ruin", "Exotic armor"; a keyword every build has, by what it is in the game ("Arc buff" for Bolt Charge, "Arc
/// elemental pickup" for Ionic Trace, "Pickup" for an Orb of Power) — with that item's icon (none when the build doesn't
/// show one).
/// </summary>
public sealed record OutcomeOrigin(string Label, Optional<string> Icon);

/// <summary>An origin and the elements an event set off from it, in the order they fired.</summary>
public sealed record OriginGroup(OutcomeOrigin Origin, ImmutableArray<SourceOutcomes> Sources);

/// <summary>
/// Pure: an event's elements grouped by where they come from, the groups in the order their first element fired; an
/// element whose rules fired apart (a debuff first, energy last) is one entry in its group, its outcomes in that order.
/// </summary>
public static class OutcomeOrigins
{
    public static ImmutableArray<OriginGroup> GroupByOrigin(ValidatedBuild build, EventOutcomes shaped) =>
    [
        .. shaped.Sources
            .Select(source => (Origin: FindOrigin(build, shaped, source), Source: source))
            .GroupBy(entry => entry.Origin)
            .Select(group => new OriginGroup(group.Key, JoinSameSource(group.Select(entry => entry.Source)))),
    ];

    /// <summary>The same element in a row (its rules fired apart, now in one group) as one entry.</summary>
    private static ImmutableArray<SourceOutcomes> JoinSameSource(IEnumerable<SourceOutcomes> sources) =>
        sources.Aggregate(ImmutableArray<SourceOutcomes>.Empty, (joined, next) =>
            !joined.IsEmpty && joined[^1].Source == next.Source
                ? joined.SetItem(joined.Length - 1, joined[^1] with
                {
                    Outcomes = joined[^1].Outcomes.AddRange(next.Outcomes),
                    Reasons = joined[^1].Reasons.AddRange(next.Reasons),
                    Caveats = joined[^1].Caveats.AddRange(next.Caveats),
                })
                : joined.Add(next));

    private static OutcomeOrigin FindOrigin(ValidatedBuild build, EventOutcomes shaped, SourceOutcomes source) =>
        source.Kind switch
        {
            ElementKind.Super or ElementKind.Grenade or ElementKind.Melee or ElementKind.ClassAbility or ElementKind.Aspect or ElementKind.Fragment =>
                DescribeSubclassOrigin(build, source.Kind),
            ElementKind.ExoticWeapon or ElementKind.WeaponPerk or ElementKind.Catalyst => DescribeWeaponOrigin(build, shaped, source),
            ElementKind.ArmorMod => DescribeModOrigin(build, source),
            ElementKind.ArmorSetBonus => DescribeSetOrigin(build.Catalog, source),
            ElementKind.ArtifactPerk => DescribeArtifactOrigin(build, source),
            ElementKind.ExoticArmor => new OutcomeOrigin("Exotic armor", Optional.None<string>()),
            _ => new OutcomeOrigin(LoopDesigning.DescribeElementType(build, source.Source), Optional.None<string>()),
        };

    /// <summary>An ability, an aspect or a fragment: the subclass it is part of (Arcstrider), with its icon.</summary>
    private static OutcomeOrigin DescribeSubclassOrigin(ValidatedBuild build, ElementKind kind)
    {
        var subclass = build.Catalog.FindSubclassItem(build.Build.Class, build.Build.Subclass);
        return new OutcomeOrigin(
            $"{kind.DescribeElementKind()} · {subclass.Map(item => item.Name).UnwrapOr("Subclass")}",
            subclass.Bind(item => item.Icon).Map(LoadoutShaping.ToIconUrl));
    }

    /// <summary>
    /// A weapon's perk, an exotic weapon's own or its catalyst: the build's weapon that holds it (Core says which: the build
    /// check's own mapping), with its icon; of two that hold it, the one whose hit or kill the event is.
    /// </summary>
    private static OutcomeOrigin DescribeWeaponOrigin(ValidatedBuild build, EventOutcomes shaped, SourceOutcomes source)
    {
        var label = source.Kind switch
        {
            ElementKind.ExoticWeapon => "Exotic weapon",
            ElementKind.Catalyst => "Catalyst",
            _ => "Weapon perk",
        };
        var holding = LoopDesigning.ListWeaponsHolding(build, source.Source).Select(index => build.Build.Weapons[index]).ToImmutableArray();
        return holding
            .OrderBy(weapon => shaped.Weapon == Optional.Some(weapon.Slot) ? 0 : 1)
            .Select(weapon => Optional.Some(new OutcomeOrigin(
                $"{label} · {weapon.Name}",
                weapon.Hash.Bind(hash => build.Catalog.Manifest.FindItem(hash)).Bind(item => item.Icon).Map(LoadoutShaping.ToIconUrl))))
            .FindFirstSome()
            .UnwrapOr(new OutcomeOrigin(label, Optional.None<string>()));
    }

    /// <summary>
    /// An armor mod: the armor piece in its slot that the build panel shows (its icon), else the slot; a mod with no slot is
    /// "General"; one the excerpt doesn't know, just "Armor mod".
    /// </summary>
    private static OutcomeOrigin DescribeModOrigin(ValidatedBuild build, SourceOutcomes source) =>
        build.Catalog.FindElementItem(source.Source).Match(
            item => item.Value.ReadArmorSlot().Match(
                slot => LoadoutShaping.FindArmorPiece(build, slot.Value).Match(
                    piece => new OutcomeOrigin($"Armor mod · {piece.Value.Name}", piece.Value.Icon.Map(LoadoutShaping.ToIconUrl)),
                    _ => new OutcomeOrigin($"Armor mod · {slot.Value.DescribeArmorSlot()}", Optional.None<string>())),
                _ => new OutcomeOrigin("Armor mod · General", Optional.None<string>())),
            _ => new OutcomeOrigin("Armor mod", Optional.None<string>()));

    /// <summary>An armor set bonus: its set, by name.</summary>
    private static OutcomeOrigin DescribeSetOrigin(RuleCatalog catalog, SourceOutcomes source) =>
        (catalog.Elements.TryGetValue(source.Source, out var element) ? element.Hashes : [])
            .Select(hash => catalog.Manifest.FindSetOfPerk(hash))
            .FindFirstSome()
            .Match(
                found => new OutcomeOrigin($"Armor set · {found.Value.Set.Name}", Optional.None<string>()),
                _ => new OutcomeOrigin("Armor set", Optional.None<string>()));

    /// <summary>An artifact perk: the build's artifact (LoopDesigning.FindArtifact) when it unlocks this perk, with its icon.</summary>
    private static OutcomeOrigin DescribeArtifactOrigin(ValidatedBuild build, SourceOutcomes source) =>
        LoopDesigning.FindArtifact(build)
            .Bind(found => found.Perks.Any(perk => perk.Element == Optional.Some(source.Source)) ? Optional.Some(found.Item) : Optional.None<ManifestItem>())
            .Match(
                artifact => new OutcomeOrigin($"Artifact perk · {artifact.Value.Name}", artifact.Value.Icon.Map(LoadoutShaping.ToIconUrl)),
                _ => new OutcomeOrigin("Artifact perk", Optional.None<string>()));
}
