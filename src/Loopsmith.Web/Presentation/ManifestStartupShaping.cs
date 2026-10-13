using System.Collections.Immutable;
using System.Globalization;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// Where the tab's startup load of Bungie's manifest is: not begun, checking the manifest's version (and the copy the
/// browser saved), downloading its files, reading the saved copy instead (the bytes read of how many, and whether the
/// listing was saved with it), listing every weapon and armor piece from the files (the listing's question it is on, of
/// how many; whether the files came from the saved copy), failed (why), done, or skipped (the player went on without
/// it: the next read that needs it loads it then).
/// </summary>
[Union]
public partial record ManifestStartup
{
    partial record Waiting();
    partial record Checking();
    partial record Downloading(ManifestDownload Download);
    partial record ReadingSaved(long Bytes, Optional<long> Total, bool ListingSaved);
    partial record Listing(int Round, int Rounds, bool FromSaved);
    partial record Failed(string Reason);
    partial record Ready();
    partial record Skipped();
}

/// <summary>One bar of the startup screen: what it is for, where it is, how far along (percent), and whether it is the one moving.</summary>
public sealed record StartupBarView(string Label, string Detail, int Percent, bool Active);

/// <summary>The startup screen: its title, its bars (the files, then the listing; none once it failed), and why it failed when it did.</summary>
public sealed record StartupView(string Title, ImmutableArray<StartupBarView> Bars, Optional<string> Problem);

/// <summary>
/// Pure: the startup load of Bungie's manifest → the screen that stands in for every page until it is done (or the
/// player goes on without it): a bar for the files (their download, or the saved copy read instead) and one for the
/// listing of every weapon and armor piece (made, or read with the saved copy).
/// </summary>
public static class ManifestStartupShaping
{
    private const string CheckingTitle = "Checking Bungie's manifest";
    private const string DownloadingTitle = "Downloading Bungie's manifest";
    private const string ReadingTitle = "Reading Bungie's manifest";
    private const string FilesLabel = "Downloading its files";
    private const string SavedLabel = "Reading the saved copy";
    private const string ItemsLabel = "Listing every weapon and armor piece";
    private const string LoginPath = "login";

    public static readonly ManifestStartup NotBegun = new ManifestStartup.Waiting();

    /// <summary>The load begins (or begins again, after Try again): the manifest's version and the browser's saved copy first.</summary>
    public static readonly ManifestStartup CheckBegun = new ManifestStartup.Checking();

    /// <summary>The files download (there's no saved copy of the current version, or it didn't read).</summary>
    public static readonly ManifestStartup DownloadBegun = new ManifestStartup.Downloading(ManifestProgressShaping.NoDownload);

    /// <summary>The saved copy is read instead of a download: <paramref name="total"/> bytes, unpacked (when known); its listing with it when it was saved.</summary>
    public static ManifestStartup BeginReadingSaved(Optional<long> total, bool listingSaved) =>
        new ManifestStartup.ReadingSaved(0, total, listingSaved);

    /// <summary>The player went on without the manifest (after a failed load).</summary>
    public static readonly ManifestStartup Skipped = new ManifestStartup.Skipped();

    /// <summary>Whether the startup screen stands in for the page: until the load is done or skipped.</summary>
    public static bool IsBlocking(ManifestStartup startup) =>
        startup is not (ManifestStartup.Ready or ManifestStartup.Skipped);

    /// <summary>
    /// Whether a page's path (relative to the app's base: <c>login?code=…</c>) is Bungie's login redirect, which hands the
    /// login back to the tab that asked and closes: it neither loads the manifest nor waits for it.
    /// </summary>
    public static bool IsLoginRedirect(string relativePath)
    {
        var end = relativePath.IndexOfAny(['?', '#']);
        var path = (end < 0 ? relativePath : relativePath[..end]).Trim('/');
        return string.Equals(path, LoginPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>How far the files' download (or the saved copy's reading) got, while the load is at them (past them, it stays where it is).</summary>
    public static ManifestStartup ReportDownload(ManifestStartup startup, ManifestDownload download) =>
        startup switch
        {
            ManifestStartup.Downloading => new ManifestStartup.Downloading(download),
            ManifestStartup.ReadingSaved reading => reading with { Bytes = download.Bytes },
            _ => startup,
        };

    /// <summary>The listing's question the load is on (1 to <paramref name="rounds"/>), while the load runs (else it stays where it is).</summary>
    public static ManifestStartup ReportListing(ManifestStartup startup, int round, int rounds) =>
        startup switch
        {
            ManifestStartup.ReadingSaved => new ManifestStartup.Listing(round, rounds, true),
            ManifestStartup.Listing listing => listing with { Round = round, Rounds = rounds },
            ManifestStartup.Downloading => new ManifestStartup.Listing(round, rounds, false),
            _ => startup,
        };

    /// <summary>Whether the load is at the files (downloading them, reading the saved copy) or listing the items.</summary>
    public static bool IsLoading(ManifestStartup startup) =>
        startup is ManifestStartup.Downloading or ManifestStartup.ReadingSaved or ManifestStartup.Listing;

    /// <summary>Whether the load is at the files: the download, or the saved copy (they say how far they got).</summary>
    public static bool IsAtFiles(ManifestStartup startup) =>
        startup is ManifestStartup.Downloading or ManifestStartup.ReadingSaved;

    /// <summary>The load's end: done, or why it failed.</summary>
    public static ManifestStartup EndStartup<T>(Result<T, string> loaded) =>
        loaded.Match<ManifestStartup>(
            _ => new ManifestStartup.Ready(),
            failed => new ManifestStartup.Failed(failed.Failure));

    public static StartupView ShapeStartup(ManifestStartup startup) =>
        startup.Match(
            _ => new StartupView(CheckingTitle, [new(FilesLabel, "starting…", 0, true), WaitingItems], Optional.None<string>()),
            _ => new StartupView(CheckingTitle, [new(FilesLabel, "checking its version…", 0, true), WaitingItems], Optional.None<string>()),
            downloading => new StartupView(
                DownloadingTitle,
                [new(FilesLabel, ManifestProgressShaping.DescribeDownload(downloading.Download), ToDownloadPercent(downloading.Download), true), WaitingItems],
                Optional.None<string>()),
            reading => new StartupView(
                ReadingTitle,
                [new(SavedLabel, DescribeSavedReading(reading), ToSavedPercent(reading), true), reading.ListingSaved ? SavedItems : WaitingItems],
                Optional.None<string>()),
            listing => new StartupView(
                listing.FromSaved ? ReadingTitle : DownloadingTitle,
                [new(listing.FromSaved ? SavedLabel : FilesLabel, "done", 100, false), ShapeItems(listing)],
                Optional.None<string>()),
            failed => new StartupView(DownloadingTitle, [], Optional.Some($"Couldn't read Bungie's manifest ({failed.Reason}).")),
            _ => new StartupView(DownloadingTitle, [], Optional.None<string>()),
            _ => new StartupView(DownloadingTitle, [], Optional.None<string>()));

    private static readonly StartupBarView WaitingItems = new(ItemsLabel, "once the files are in", 0, false);

    private static readonly StartupBarView SavedItems = new(ItemsLabel, "from the saved copy", 100, false);

    /// <summary>The bytes read of the saved copy's ("84 of 199 MB"; just the bytes when its size wasn't saved).</summary>
    private static string DescribeSavedReading(ManifestStartup.ReadingSaved reading) =>
        reading.Total.Match(
            total => $"{ToMegabytes(Math.Min(reading.Bytes, total.Value))} of {ToMegabytes(total.Value)} MB",
            _ => $"{ToMegabytes(reading.Bytes)} MB");

    /// <summary>The saved copy's bar: by its saved size (else as a download's), up to 99 % until the listing begins.</summary>
    private static int ToSavedPercent(ManifestStartup.ReadingSaved reading) =>
        reading.Total.Match(
            total => (int)Math.Min(99, Math.Round(100.0 * reading.Bytes / total.Value)),
            _ => ToDownloadPercent(new ManifestDownload(reading.Bytes, Optional.None<ManifestRetry>())));

    private static string ToMegabytes(long bytes) =>
        (bytes / 1_000_000.0).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>Step n of m, the bar at the middle of that step (its question is being read and answered).</summary>
    private static StartupBarView ShapeItems(ManifestStartup.Listing listing) =>
        new(
            ItemsLabel,
            $"step {listing.Round.ToString(CultureInfo.InvariantCulture)} of {listing.Rounds.ToString(CultureInfo.InvariantCulture)}",
            (int)Math.Round(100.0 * (2 * listing.Round - 1) / (2 * Math.Max(1, listing.Rounds))),
            true);

    /// <summary>The download's bar: up to 99 % while files come (their size isn't known), full once they're all in.</summary>
    private static int ToDownloadPercent(ManifestDownload download) =>
        (int)Math.Min(99, Math.Round(100 * ManifestProgressShaping.ToDownloadedShare(download)));
}
