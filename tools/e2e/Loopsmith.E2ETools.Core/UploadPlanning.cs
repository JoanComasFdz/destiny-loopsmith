using System.Collections.Immutable;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Core;

/// <summary>A file to upload: its path relative to record.sh's output folder, and the asset name it takes in its release.</summary>
public sealed record UploadFile(string Path, string Asset);

/// <summary>One release's upload: its files, and, for a rule release, one of its runs' GIF path (which names its rule file, for creating it).</summary>
public sealed record ReleaseUpload(string Release, Optional<string> RuleGif, ImmutableArray<UploadFile> Files);

/// <summary>What to upload, one upload per release, and a note for each run whose GIF or MP4 is missing.</summary>
public sealed record UploadPlan(ImmutableArray<string> Notes, ImmutableArray<ReleaseUpload> Uploads);

/// <summary>
/// The post's runs and their files: each run's release and asset names (<c>pr-&lt;n&gt;-v&lt;version&gt;-&lt;GIF name&gt;</c>, ReleaseNaming),
/// and each run's GIF and MP4, and each rule's flow once, uploaded to its release.
/// </summary>
public static class UploadPlanning
{
    public static ImmutableArray<PlannedRun> PlanRuns(int pr, int version, ImmutableArray<(ScenarioEntry Entry, RecordedFiles Files)> runs) =>
        [.. runs.Select(run => new PlannedRun(
            run.Entry,
            run.Files,
            ReleaseNaming.FindRelease(run.Entry.Gif),
            ReleaseNaming.NameAsset(pr, version, run.Entry.Gif, ".gif"),
            ReleaseNaming.NameAsset(pr, version, run.Entry.Gif, ".mp4"),
            ReleaseNaming.NameFlow(pr, version, run.Entry.Flow)))];

    /// <summary>
    /// Each release's files in the runs' order: a rule's flow before its first run's GIF, then each run's GIF and MP4; a
    /// release with nothing to upload has no upload. A note names a missing file by its path under <paramref name="outFolder"/>.
    /// </summary>
    public static UploadPlan PlanUploads(ImmutableArray<PlannedRun> runs, string outFolder)
    {
        var notes = runs.SelectMany(run => DescribeMissing(run, outFolder)).ToImmutableArray();
        var uploads = runs
            .GroupBy(run => run.Release)
            .Select(group => new ReleaseUpload(
                group.Key,
                ReleaseNaming.IsRuleRelease(group.Key) ? Optional.Some(group.First().Entry.Gif) : Optional.None<string>(),
                [.. group.SelectMany(ListFiles).Distinct()]))
            .Where(upload => !upload.Files.IsEmpty)
            .ToImmutableArray();
        return new UploadPlan(notes, uploads);
    }

    /// <summary>The notes a rule release carries when the workflow creates it.</summary>
    public static string DescribeRuleRelease(string gif)
    {
        var ruleFile = string.Join('/', gif.Split('/')[1..^1].Select(ReleaseNaming.ToKebabCase));
        return $"The latest e2e recordings (GIFs and MP4s) and flows (PNGs) of the rules of rules/{ruleFile}.yaml, "
            + "kept by the E2E videos workflow (docs/hosting.md, \"E2E videos on pull requests\"). Created by it: don't edit.";
    }

    private static IEnumerable<UploadFile> ListFiles(PlannedRun run)
    {
        var flow = run.Files.HasFlow && run.Entry.Flow is Optional<string>.Some path && run.FlowAsset is Optional<string>.Some asset
            ? [new UploadFile(path.Value, asset.Value)]
            : Array.Empty<UploadFile>();
        var gif = run.Files.HasGif ? [new UploadFile(run.Entry.Gif, run.GifAsset)] : Array.Empty<UploadFile>();
        var mp4 = run.Files.HasMp4 && run.Entry.Mp4 is Optional<string>.Some video
            ? [new UploadFile(video.Value, run.Mp4Asset)]
            : Array.Empty<UploadFile>();
        return flow.Concat(gif).Concat(mp4);
    }

    private static IEnumerable<string> DescribeMissing(PlannedRun run, string outFolder)
    {
        var gif = run.Files.HasGif ? [] : new[] { $"No GIF for {run.Entry.Title} ({outFolder}/{run.Entry.Gif})." };
        var mp4 = run.Files.HasMp4 ? [] : new[] { $"No MP4 for {run.Entry.Title} ({run.Entry.Mp4.Map(path => $"{outFolder}/{path}").UnwrapOr("none recorded")})." };
        return gif.Concat(mp4);
    }
}
