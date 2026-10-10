using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

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
    ImmutableArray<LeftOutGroup> LeftOut,
    ImmutableArray<BuildIssue> Issues);

/// <summary>What a DIM loadout had that the build leaves out, by where the loadout listed it ("Armor mods": 3 hashes).</summary>
public sealed record LeftOutGroup(string Label, ImmutableArray<ItemHash> Hashes);

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
                new AbilityTile("Super", DescribeAbility(catalog, b.Abilities.Super)),
                new AbilityTile("Grenade", DescribeAbility(catalog, b.Abilities.Grenade)),
                new AbilityTile("Melee", DescribeAbility(catalog, b.Abilities.Melee)),
                new AbilityTile("Class", DescribeAbility(catalog, b.Abilities.ClassAbility)),
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
            [.. b.LeftOut.GroupBy(item => item.Part).OrderBy(group => group.Key).Select(group => new LeftOutGroup(group.Key.DescribeLoadoutParts(), [.. group.Select(item => item.Hash)]))],
            issues);
    }

    private static ImmutableArray<ElementTile> DescribeElements(RuleCatalog catalog, ImmutableArray<ElementId> ids) =>
        [.. ids.GroupBy(id => id).Select(group => DescribeElement(catalog, group.Key, group.Count()))];

    private static ElementTile DescribeElement(RuleCatalog catalog, ElementId id, int copies) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? new ElementTile(element.Name, element.Affinity, copies, element.Description)
            : new ElementTile(id.Value, Affinity.Neutral, copies, Optional.None<string>());

    /// <summary>"dim.gg/2jguuoq": a dim.gg share without its scheme and name, short enough for a card; "Open in DIM" otherwise.</summary>
    public static string DescribeShareLink(string link)
    {
        var parts = link.Split("://", 2)[^1].Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[0] == "dim.gg" ? $"dim.gg/{parts[1]}" : "Open in DIM";
    }

    /// <summary>An ability the build gives as "?": a tile that says so (unknown is data, ADRs D1).</summary>
    private static ElementTile DescribeAbility(RuleCatalog catalog, Optional<ElementId> id) =>
        id.Match(
            known => DescribeElement(catalog, known.Value, 1),
            _ => new ElementTile(DomainPhrasing.Unknown, Affinity.Neutral, 1, Optional.Some("Not known: the DIM link's ability isn't in the rule catalog yet, so it sets nothing off.")));

    private static Optional<int> ReadStat(Optional<StatValue> value) =>
        value.Map(stat => stat.Value);
}
