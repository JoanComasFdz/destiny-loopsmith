using System.Globalization;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// What a read of Bungie's manifest is for: the player's items (after their Bungie login), the open build's (a DIM
/// link's), every weapon and armor piece in the game (the pickers' "Every item"), or the item picked there (its perks,
/// catalyst or set, before it goes into the build).
/// </summary>
public enum ManifestReadFor { YourItems, BuildItems, EveryItem, PickedItem }

/// <summary>A file of the manifest broke while it came: it is downloaded again from its start, the <c>Attempt</c>-th time of at most <c>Limit</c>.</summary>
public sealed record ManifestRetry(int Attempt, int Limit);

/// <summary>
/// How far the download of the manifest's files got: the bytes that came, unpacked (their total isn't known: bungie.net
/// sends them compressed), and the retry the file coming is on, when it broke before.
/// </summary>
public sealed record ManifestDownload(long Bytes, Optional<ManifestRetry> Retry);

/// <summary>
/// Where a read of Bungie's manifest is: reading its files (when the tab doesn't hold them yet), about to name the items
/// from the files the tab holds, or naming them (the round of questions it is on, from 1).
/// </summary>
[Union]
public partial record ManifestReadStage
{
    partial record Reading(ManifestDownload Download);
    partial record Holding();
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

/// <summary>Pure: a manifest read's progress → the progress bar (the Bungie login section, the ⋯ pickers; the startup screen's download bar).</summary>
public static class ManifestProgressShaping
{
    /// <summary>The unpacked bytes the files are assumed to near (Bungie's item table and its companions, about 215 MB in 2026): the bar slows past it, it never stops.</summary>
    private const double ExpectedBytes = 215_000_000;

    /// <summary>The files nobody downloaded yet.</summary>
    public static readonly ManifestDownload NoDownload = new(0, Optional.None<ManifestRetry>());

    /// <summary>A read that downloads the manifest's files first (the tab doesn't have them: its startup load was skipped or failed).</summary>
    public static ManifestReadProgress StartReading(ManifestReadFor readFor, int items) =>
        new(readFor, items, new ManifestReadStage.Reading(NoDownload));

    /// <summary>A read from the files the tab keeps: it names the items at once.</summary>
    public static ManifestReadProgress StartNaming(ManifestReadFor readFor, int items) =>
        new(readFor, items, new ManifestReadStage.Holding());

    /// <summary>What manifest.js says of the download (the bytes, unpacked; 0 for a file's first try, else its retry of <paramref name="retries"/>).</summary>
    public static ManifestDownload ToDownload(long bytes, int attempt, int retries) =>
        new(bytes, attempt > 0 ? Optional.Some(new ManifestRetry(attempt, retries)) : Optional.None<ManifestRetry>());

    /// <summary>How far the files' download got; once the naming has begun, the read is past them.</summary>
    public static ManifestReadProgress ReportDownload(ManifestReadProgress progress, ManifestDownload download) =>
        progress.Stage is ManifestReadStage.Reading ? progress with { Stage = new ManifestReadStage.Reading(download) } : progress;

    /// <summary>The next round of naming: the first after the files, then one more each time.</summary>
    public static ManifestReadProgress ReportRound(ManifestReadProgress progress) =>
        progress with
        {
            Stage = progress.Stage.Match<ManifestReadStage>(
                _ => new ManifestReadStage.Naming(1),
                _ => new ManifestReadStage.Naming(1),
                naming => new ManifestReadStage.Naming(naming.Round + 1)),
        };

    /// <summary>The question a listing is on (its own count, 1 to 4), as the naming's round.</summary>
    public static ManifestReadProgress ReportRound(ManifestReadProgress progress, int round) =>
        progress with { Stage = new ManifestReadStage.Naming(round) };

    public static ManifestProgressView ShapeReading(ManifestReadProgress progress) =>
        new(
            DescribeTitle(progress),
            progress.Stage.Match(
                reading => $"reading its files… {DescribeDownload(reading.Download)}",
                _ => "naming them…",
                naming => $"naming them (step {naming.Round.ToString(CultureInfo.InvariantCulture)})"),
            progress.For switch
            {
                ManifestReadFor.YourItems => "You can keep designing meanwhile; ⋯ names your armor and weapons once it's done.",
                ManifestReadFor.EveryItem => "Only the first time in this tab: the list shows once it's done.",
                ManifestReadFor.PickedItem => "It goes into the build once it's done.",
                _ => "You can keep designing meanwhile; the build's items show by hash until it's done.",
            },
            progress.Stage.Match(
                reading => (int)Math.Round(85 * ToDownloadedShare(reading.Download)),
                _ => 85,
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
            ManifestReadFor.EveryItem => "Reading Bungie's manifest for every weapon and armor piece",
            ManifestReadFor.PickedItem => "Reading Bungie's manifest for the item you picked",
            _ => progress.Items == 1 ? "Reading Bungie's manifest for 1 of this build's items" : $"Reading Bungie's manifest for {count} of this build's items",
        };
    }

    /// <summary>The bytes so far ("84 MB"), and the retry when a file broke ("84 MB · connection lost, trying again: 2 of 3").</summary>
    public static string DescribeDownload(ManifestDownload download) =>
        download.Retry.Match(
            retry => $"{DescribeMegabytes(download.Bytes)} · connection lost, trying again: {retry.Value.Attempt.ToString(CultureInfo.InvariantCulture)} of {retry.Value.Limit.ToString(CultureInfo.InvariantCulture)}",
            _ => DescribeMegabytes(download.Bytes));

    /// <summary>How much of the files is in, from 0 to 1: four fifths by the size they are expected to have, then ever more slowly (their size isn't known).</summary>
    public static double ToDownloadedShare(ManifestDownload download) =>
        1 - Math.Exp(-1.6 * download.Bytes / ExpectedBytes);

    private static string DescribeMegabytes(long bytes) =>
        $"{(bytes / 1_000_000.0).ToString("0", CultureInfo.InvariantCulture)} MB";
}
