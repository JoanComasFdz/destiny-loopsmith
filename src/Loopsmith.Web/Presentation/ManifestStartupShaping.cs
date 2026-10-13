using System.Collections.Immutable;
using System.Globalization;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// Where the tab's startup load of Bungie's manifest is: not begun, downloading its files, listing every weapon and armor
/// piece from them (the listing's question it is on, of how many), failed (why), done, or skipped (the player went on
/// without it: the next read that needs it loads it then).
/// </summary>
[Union]
public partial record ManifestStartup
{
    partial record Waiting();
    partial record Downloading(ManifestDownload Download);
    partial record Listing(int Round, int Rounds);
    partial record Failed(string Reason);
    partial record Ready();
    partial record Skipped();
}

/// <summary>One bar of the startup screen: what it is for, where it is, how far along (percent), and whether it is the one moving.</summary>
public sealed record StartupBarView(string Label, string Detail, int Percent, bool Active);

/// <summary>The startup screen: its bars (the files, then the listing; none once it failed), and why it failed when it did.</summary>
public sealed record StartupView(ImmutableArray<StartupBarView> Bars, Optional<string> Problem);

/// <summary>
/// Pure: the startup load of Bungie's manifest → the screen that stands in for every page until it is done (or the
/// player goes on without it): a bar for the files' download and one for the listing of every weapon and armor piece.
/// </summary>
public static class ManifestStartupShaping
{
    public const string Title = "Downloading Bungie's manifest";

    private const string FilesLabel = "Downloading its files";
    private const string ItemsLabel = "Listing every weapon and armor piece";
    private const string LoginPath = "login";

    public static readonly ManifestStartup NotBegun = new ManifestStartup.Waiting();

    /// <summary>The load begins (or begins again, after Try again) with its files.</summary>
    public static readonly ManifestStartup DownloadBegun = new ManifestStartup.Downloading(ManifestProgressShaping.NoDownload);

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

    /// <summary>How far the files' download got, while the load downloads them (past them, it stays where it is).</summary>
    public static ManifestStartup ReportDownload(ManifestStartup startup, ManifestDownload download) =>
        startup is ManifestStartup.Downloading ? new ManifestStartup.Downloading(download) : startup;

    /// <summary>The listing's question the load is on (1 to <paramref name="rounds"/>), while the load runs (else it stays where it is).</summary>
    public static ManifestStartup ReportListing(ManifestStartup startup, int round, int rounds) =>
        IsLoading(startup) ? new ManifestStartup.Listing(round, rounds) : startup;

    /// <summary>Whether the load is downloading the files or listing the items.</summary>
    public static bool IsLoading(ManifestStartup startup) =>
        startup is ManifestStartup.Downloading or ManifestStartup.Listing;

    /// <summary>The load's end: done, or why it failed.</summary>
    public static ManifestStartup EndStartup<T>(Result<T, string> loaded) =>
        loaded.Match<ManifestStartup>(
            _ => new ManifestStartup.Ready(),
            failed => new ManifestStartup.Failed(failed.Failure));

    public static StartupView ShapeStartup(ManifestStartup startup) =>
        startup.Match(
            _ => new StartupView([ShapeFiles("starting…", 0, true), WaitingItems], Optional.None<string>()),
            downloading => new StartupView(
                [ShapeFiles(ManifestProgressShaping.DescribeDownload(downloading.Download), ToDownloadPercent(downloading.Download), true), WaitingItems],
                Optional.None<string>()),
            listing => new StartupView([ShapeFiles("done", 100, false), ShapeItems(listing)], Optional.None<string>()),
            failed => new StartupView([], Optional.Some($"Couldn't read Bungie's manifest ({failed.Reason}).")),
            _ => new StartupView([], Optional.None<string>()),
            _ => new StartupView([], Optional.None<string>()));

    private static readonly StartupBarView WaitingItems = new(ItemsLabel, "once the files are in", 0, false);

    private static StartupBarView ShapeFiles(string detail, int percent, bool active) =>
        new(FilesLabel, detail, percent, active);

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
