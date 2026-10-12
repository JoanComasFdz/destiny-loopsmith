using System.Globalization;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// Whose items a read of Bungie's manifest names: the player's (after their Bungie login), the open build's (a DIM
/// link's), every weapon and armor piece of the game (the pickers without a login), or the one just picked from them.
/// </summary>
public enum ManifestReadFor { YourItems, BuildItems, GameItems, PickedItem }

/// <summary>
/// Where a read of Bungie's manifest is: reading its files (the bytes come unpacked, so their total isn't known — bungie.net
/// sends them compressed), or naming the items from them (the round of questions it is on).
/// </summary>
[Union]
public partial record ManifestReadStage
{
    partial record Reading(long Bytes);
    partial record Naming(int Round);
}

/// <summary>A read of Bungie's full manifest in progress: for whose items, how many, and where it is.</summary>
public sealed record ManifestReadProgress(ManifestReadFor For, int Items, ManifestReadStage Stage);

/// <summary>
/// What the progress bar says: its title, where the read is ("reading its files… 84 MB"), what to do meanwhile, and how
/// far along the bar is, in
/// percent: the files fill the bar to 85 % (four fifths of that by the size they are expected to have, then ever more
/// slowly: their size isn't known), each naming round half of what's left before 99 %.
/// </summary>
public sealed record ManifestProgressView(string Title, string Detail, string Hint, int Percent);

/// <summary>Pure: a manifest read's progress → the progress bar (the Bungie login section, the ⋯ pickers).</summary>
public static class ManifestProgressShaping
{
    /// <summary>The unpacked bytes the files are assumed to near (Bungie's item table and its companions, about 215 MB in 2026): the bar slows past it, it never stops.</summary>
    private const double ExpectedBytes = 215_000_000;

    public static ManifestReadProgress StartReading(ManifestReadFor readFor, int items) =>
        new(readFor, items, new ManifestReadStage.Reading(0));

    /// <summary>How many bytes of the files have come (unpacked); once the naming has begun, the read is past them.</summary>
    public static ManifestReadProgress ReportBytes(ManifestReadProgress progress, long bytes) =>
        progress.Stage is ManifestReadStage.Reading ? progress with { Stage = new ManifestReadStage.Reading(bytes) } : progress;

    /// <summary>The next round of naming: the first after the files, then one more each time.</summary>
    public static ManifestReadProgress ReportRound(ManifestReadProgress progress) =>
        progress with
        {
            Stage = progress.Stage.Match<ManifestReadStage>(
                _ => new ManifestReadStage.Naming(1),
                naming => new ManifestReadStage.Naming(naming.Round + 1)),
        };

    public static ManifestProgressView ShapeReading(ManifestReadProgress progress) =>
        new(
            DescribeTitle(progress),
            progress.Stage.Match(
                reading => $"reading its files… {DescribeMegabytes(reading.Bytes)}",
                naming => $"naming them (step {naming.Round.ToString(CultureInfo.InvariantCulture)})"),
            DescribeHint(progress.For),
            progress.Stage.Match(
                reading => (int)Math.Round(85 * (1 - Math.Exp(-1.6 * reading.Bytes / ExpectedBytes))),
                naming => (int)Math.Round(99 - 14 * Math.Pow(0.5, naming.Round))));

    /// <summary>A read in progress, when there is one.</summary>
    public static Optional<ManifestProgressView> ShapeReading(Optional<ManifestReadProgress> progress) =>
        progress.Map(ShapeReading);

    /// <summary>The bar's fill, as a CSS width.</summary>
    public static string ToWidth(int percent) =>
        $"{Math.Clamp(percent, 0, 100).ToString(CultureInfo.InvariantCulture)}%";

    private static string DescribeTitle(ManifestReadProgress progress)
    {
        var count = progress.Items.ToString(CultureInfo.InvariantCulture);
        return progress.For switch
        {
            ManifestReadFor.YourItems => progress.Items == 1 ? "Reading Bungie's manifest for 1 of your items" : $"Reading Bungie's manifest for {count} of your items",
            ManifestReadFor.GameItems => "Reading Bungie's manifest for every weapon and armor piece",
            ManifestReadFor.PickedItem => "Reading Bungie's manifest for the item you picked",
            _ => progress.Items == 1 ? "Reading Bungie's manifest for 1 of this build's items" : $"Reading Bungie's manifest for {count} of this build's items",
        };
    }

    private static string DescribeHint(ManifestReadFor readFor) =>
        readFor switch
        {
            ManifestReadFor.YourItems => "You can keep designing meanwhile; ⋯ names your armor and weapons once it's done.",
            ManifestReadFor.GameItems => "Only the first time in this tab: the list shows once it's done.",
            ManifestReadFor.PickedItem => "It goes into the build once it's done.",
            _ => "You can keep designing meanwhile; the build's items show by hash until it's done.",
        };

    private static string DescribeMegabytes(long bytes) =>
        $"{(bytes / 1_000_000.0).ToString("0", CultureInfo.InvariantCulture)} MB";
}
