using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Web.Presentation;

/// <summary>An equipped element as a DIM-like tile: catalog name, colour, copies (mods stack), description.</summary>
public sealed record ElementTile(string Name, Affinity Affinity, int Copies, Optional<string> Description);

public sealed record AbilityTile(string Slot, ElementTile Element);

public sealed record WeaponRow(string Slot, string Name, DamageType Type, Optional<string> Archetype, ImmutableArray<ElementTile> Perks);

public sealed record StatRow(string Name, Optional<int> Value);

/// <summary>What the build panel shows (the same facts as <c>BuildExplaining.RenderBuildSummary</c>, laid out as tiles).</summary>
public sealed record BuildSheet(
    string Name,
    GuardianClass Class,
    Subclass Subclass,
    Optional<string> Author,
    Optional<string> SourceUrl,
    ImmutableArray<AbilityTile> Abilities,
    ImmutableArray<ElementTile> Aspects,
    ImmutableArray<ElementTile> Fragments,
    Optional<ElementTile> Exotic,
    ImmutableArray<ElementTile> ArmorSet,
    ImmutableArray<ElementTile> Mods,
    ImmutableArray<ElementTile> Artifact,
    ImmutableArray<WeaponRow> Weapons,
    ImmutableArray<StatRow> Stats,
    ImmutableArray<BuildIssue> Issues);

/// <summary>
/// Pure: a validated build → its sheet. Names and colours come from the catalog; nothing is inferred. The issues are
/// the design's (<c>LoopDesigning.ListDesignIssues</c>: the build's own plus a catalog mismatch).
/// </summary>
public static class BuildSheetShaping
{
    public const int MaxStat = 200;

    public static BuildSheet ShapeSheet(ValidatedBuild build, ImmutableArray<BuildIssue> issues)
    {
        var b = build.Build;
        var catalog = build.Catalog;
        return new BuildSheet(
            b.Name,
            b.Class,
            b.Subclass,
            b.Author,
            b.SourceUrl,
            [
                new AbilityTile("Super", DescribeElement(catalog, b.Abilities.Super, 1)),
                new AbilityTile("Grenade", DescribeElement(catalog, b.Abilities.Grenade, 1)),
                new AbilityTile("Melee", DescribeElement(catalog, b.Abilities.Melee, 1)),
                new AbilityTile("Class", DescribeElement(catalog, b.Abilities.ClassAbility, 1)),
            ],
            DescribeElements(catalog, b.Aspects),
            DescribeElements(catalog, b.Fragments),
            b.ExoticArmor.Map(id => DescribeElement(catalog, id, 1)),
            DescribeElements(catalog, b.ArmorSetBonuses),
            DescribeElements(catalog, b.ArmorMods),
            DescribeElements(catalog, b.ArtifactPerks),
            [.. b.Weapons.Select(w => new WeaponRow(w.Slot.ToString(), w.Name, w.Type, w.Archetype, DescribeElements(catalog, w.Perks)))],
            [
                new StatRow("Weapons", ReadStat(b.Stats.Weapons)),
                new StatRow("Health", ReadStat(b.Stats.Health)),
                new StatRow("Class", ReadStat(b.Stats.Class)),
                new StatRow("Grenade", ReadStat(b.Stats.Grenade)),
                new StatRow("Super", ReadStat(b.Stats.Super)),
                new StatRow("Melee", ReadStat(b.Stats.Melee)),
            ],
            issues);
    }

    private static ImmutableArray<ElementTile> DescribeElements(RuleCatalog catalog, ImmutableArray<ElementId> ids) =>
        [.. ids.GroupBy(id => id).Select(group => DescribeElement(catalog, group.Key, group.Count()))];

    private static ElementTile DescribeElement(RuleCatalog catalog, ElementId id, int copies) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? new ElementTile(element.Name, element.Affinity, copies, element.Description)
            : new ElementTile(id.Value, Affinity.Neutral, copies, Optional.None<string>());

    private static Optional<int> ReadStat(Optional<StatValue> value) =>
        value.Map(stat => stat.Value);
}
