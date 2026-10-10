using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

public sealed record StatRow(string Name, Optional<int> Value);

/// <summary>What a DIM loadout had that the build leaves out, one entry per hash (its manifest name when the excerpt has one) and its copies.</summary>
public sealed record LeftOutEntry(ItemHash Hash, string Name, int Copies);

/// <summary>The left-out entries of one part of the loadout ("Armor mods").</summary>
public sealed record LeftOutGroup(string Label, ImmutableArray<LeftOutEntry> Entries);

/// <summary>
/// What the build panel shows around the loadout (<see cref="LoadoutShaping"/>): the build's name, class, subclass,
/// author and source, its stats, what a DIM loadout left out, and the design's issues.
/// </summary>
public sealed record BuildSheet(
    string Name,
    GuardianClass Class,
    Subclass Subclass,
    Optional<string> Author,
    Optional<string> SourceUrl,
    Optional<string> SubclassIcon,
    ImmutableArray<StatRow> Stats,
    ImmutableArray<LeftOutGroup> LeftOut,
    ImmutableArray<BuildIssue> Issues);

/// <summary>
/// Pure: a validated build → its sheet. Names come from the catalog and its manifest excerpt; nothing is inferred. The
/// issues are the design's (<c>LoopDesigning.ListDesignIssues</c>: the build's own plus a catalog mismatch).
/// </summary>
public static class BuildSheetShaping
{
    public const int MaxStat = 200;

    public static BuildSheet ShapeSheet(ValidatedBuild build, ImmutableArray<BuildIssue> issues)
    {
        var b = build.Build;
        var manifest = build.Catalog.Manifest;
        return new BuildSheet(
            b.Name,
            b.Class,
            b.Subclass,
            b.Author,
            b.SourceUrl,
            LoadoutShaping.FindSubclassIcon(build.Catalog, b.Class, b.Subclass),
            [
                new StatRow("Weapons", ReadStat(b.Stats.Weapons)),
                new StatRow("Health", ReadStat(b.Stats.Health)),
                new StatRow("Class", ReadStat(b.Stats.Class)),
                new StatRow("Grenade", ReadStat(b.Stats.Grenade)),
                new StatRow("Super", ReadStat(b.Stats.Super)),
                new StatRow("Melee", ReadStat(b.Stats.Melee)),
            ],
            [
                .. b.LeftOut
                    .GroupBy(item => item.Part)
                    .OrderBy(group => group.Key)
                    .Select(group => new LeftOutGroup(
                        group.Key.DescribeLoadoutParts(),
                        [.. group.GroupBy(item => item.Hash).Select(copies => new LeftOutEntry(copies.Key, DescribeHash(manifest, copies.Key), copies.Count()))])),
            ],
            issues);
    }

    /// <summary>"dim.gg/2jguuoq": a dim.gg share without its scheme and name, short enough for a card; "Open in DIM" otherwise.</summary>
    public static string DescribeShareLink(string link) =>
        LoopDesigning.ReadDimLink(link) is Result<DimLink, string>.Ok { Value: DimLink.Shared shared }
            ? $"dim.gg/{shared.ShareId.Value}"
            : "Open in DIM";

    /// <summary>A hash by its manifest name, or the number when the excerpt doesn't have it.</summary>
    private static string DescribeHash(ManifestExcerpt manifest, ItemHash hash) =>
        manifest.Items.TryGetValue(hash, out var item) ? item.Name : hash.Value.ToString(CultureInfo.InvariantCulture);

    private static Optional<int> ReadStat(Optional<StatValue> value) =>
        value.Map(stat => stat.Value);
}
