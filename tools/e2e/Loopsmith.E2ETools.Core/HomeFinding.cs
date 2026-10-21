using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Loopsmith.E2ETools.Core;

/// <summary>
/// When Home shows in a recorded run's video. The video's clock runs up to a second apart from the test's, so the time the
/// test noted only says where to look: ffmpeg scores each frame's change of picture from a second before it to three after,
/// and the first big change (the startup screen giving way to Home; the click comes at least two seconds after Home) is
/// where Home starts, a tenth of a second in; without one, two tenths after the noted time.
/// </summary>
public static partial class HomeFinding
{
    /// <summary>The least scene score that counts as the startup screen giving way to Home.</summary>
    private const double BigChange = 0.08;

    /// <summary>The second the probe starts at: a second before the noted time, never before the video's start.</summary>
    public static double FindProbeStart(double noted) => Math.Max(0.0, noted - 1.0);

    /// <summary>ffmpeg's arguments to print each frame's scene score, four seconds from <paramref name="start"/>.</summary>
    public static ImmutableArray<string> WriteProbeArguments(string video, double start) =>
        ["-nostdin", "-hide_banner", "-ss", FormatSeconds(start), "-t", "4", "-i", video,
         "-vf", "select='gte(scene,0)',metadata=print:key=lavfi.scene_score", "-f", "null", "-"];

    /// <summary>The second Home starts, from what the probe printed (its error output).</summary>
    public static double FindHome(double noted, string probe)
    {
        var start = FindProbeStart(noted);
        var times = CreateFrameTimePattern().Matches(probe).Select(found => ReadNumber(found.Groups[1].Value));
        var scores = CreateSceneScorePattern().Matches(probe).Select(found => ReadNumber(found.Groups[1].Value));
        var cuts = times.Zip(scores).Where(frame => frame.Second >= BigChange).Select(frame => start + frame.First + 0.1);
        return cuts.DefaultIfEmpty(noted + 0.2).First();
    }

    /// <summary>A second as ffmpeg and record.sh take it: three decimals.</summary>
    public static string FormatSeconds(double seconds) => seconds.ToString("F3", CultureInfo.InvariantCulture);

    /// <summary>The video next to a run's record: <c>&lt;name&gt;.json</c> → <c>&lt;name&gt;.webm</c>.</summary>
    public static string FindVideo(string record) =>
        (record.EndsWith(".json", StringComparison.Ordinal) ? record[..^".json".Length] : record) + ".webm";

    /// <summary>The line record.sh reads: domain folder, GIF name, screen and the second to trim the video from.</summary>
    public static string WriteHomeLine(RunRecord record, double home) => $"{record.Domain} {record.Gif} {record.Screen} {FormatSeconds(home)}";

    private static double ReadNumber(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"pts_time:([0-9]+(?:\.[0-9]*)?)")]
    private static partial Regex CreateFrameTimePattern();

    [GeneratedRegex(@"scene_score=([0-9]+(?:\.[0-9]*)?)")]
    private static partial Regex CreateSceneScorePattern();
}
