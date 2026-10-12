using System.Globalization;
using Loopsmith.Core.Functional;

namespace Loopsmith.Web.Presentation;

public enum ManifestStage { Downloading, Naming }

/// <summary>
/// How a read of Bungie's full manifest goes: for how many items, and whether its files are still downloading (the bytes
/// so far — their total isn't known: bungie.net sends them compressed) or Loopsmith is naming the items from them (the
/// round of questions it is on).
/// </summary>
public sealed record ManifestReadProgress(int Items, ManifestStage Stage, long Bytes, int Round);

/// <summary>
/// What the progress bar says: its title, where the read is ("downloading… 84 MB"), and how far along the bar is, in
/// percent: the downloading fills the bar to 85 % (four fifths of that by the size it is expected to have, then
/// ever more slowly: its size isn't known), each naming round half of what's left before 99 %.
/// </summary>
public sealed record ManifestProgressView(string Title, string Detail, int Percent);

/// <summary>Pure: a manifest read's progress → the Bungie login section's progress bar.</summary>
public static class ManifestProgressShaping
{
    /// <summary>The bytes the download is assumed to near (Bungie's item table and its companions, about 215 MB in 2026): the bar slows past it, it never stops.</summary>
    private const double ExpectedBytes = 215_000_000;

    public static ManifestReadProgress StartReading(int items) =>
        new(items, ManifestStage.Downloading, 0, 0);

    public static ManifestReadProgress ReportDownload(ManifestReadProgress reading, long bytes) =>
        reading.Stage == ManifestStage.Downloading ? reading with { Bytes = bytes } : reading;

    public static ManifestReadProgress ReportRound(ManifestReadProgress reading, int round) =>
        reading with { Stage = ManifestStage.Naming, Round = round };

    public static ManifestProgressView ShapeReading(ManifestReadProgress reading) =>
        new(
            reading.Items == 1 ? "Reading Bungie's manifest for 1 of your items" : $"Reading Bungie's manifest for {reading.Items.ToString(CultureInfo.InvariantCulture)} of your items",
            reading.Stage == ManifestStage.Downloading
                ? $"downloading… {DescribeMegabytes(reading.Bytes)}"
                : $"naming them (step {reading.Round.ToString(CultureInfo.InvariantCulture)})",
            ToPercent(reading));

    private static int ToPercent(ManifestReadProgress reading) =>
        reading.Stage == ManifestStage.Downloading
            ? (int)Math.Round(85 * (1 - Math.Exp(-1.6 * reading.Bytes / ExpectedBytes)))
            : (int)Math.Round(99 - 14 * Math.Pow(0.5, reading.Round));

    private static string DescribeMegabytes(long bytes) =>
        $"{(bytes / 1_000_000.0).ToString("0", CultureInfo.InvariantCulture)} MB";

    /// <summary>The bar's fill, as a CSS width.</summary>
    public static string ToWidth(int percent) =>
        $"{Math.Clamp(percent, 0, 100).ToString(CultureInfo.InvariantCulture)}%";

    /// <summary>A read in progress, when there is one.</summary>
    public static Optional<ManifestProgressView> ShapeReading(Optional<ManifestReadProgress> reading) =>
        reading.Map(ShapeReading);
}
