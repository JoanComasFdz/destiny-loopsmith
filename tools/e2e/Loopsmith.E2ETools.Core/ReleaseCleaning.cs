using System.Collections.Immutable;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Core;

/// <summary>A release the workflow keeps recordings in: its id and its tag.</summary>
public sealed record Release(long Id, string Tag);

/// <summary>A file of a release: its id and its name.</summary>
public sealed record ReleaseAsset(long Id, string Name);

/// <summary>
/// What each release keeps when a PR closes. In <c>prs-resources</c> the PR's newest version; in each rule release the
/// latest recording of every rule. Only names <c>pr-&lt;n&gt;-v&lt;version&gt;-…</c> are ever deleted.
/// </summary>
public static class ReleaseCleaning
{
    public const int MaxAssets = 1000;
    public const int WarnAssets = 900;

    /// <summary>The releases to clean: prs-resources first, then the rule releases, in GitHub's order.</summary>
    public static ImmutableArray<Release> PickReleases(ImmutableArray<Release> releases) =>
        [.. releases
            .Where(release => release.Tag == ReleaseNaming.PrsRelease || ReleaseNaming.IsRuleRelease(release.Tag))
            .OrderBy(release => release.Tag != ReleaseNaming.PrsRelease)];

    /// <summary>The PR's assets of prs-resources to delete when it closes: every version older than its newest.</summary>
    public static ImmutableArray<ReleaseAsset> PlanPrDeletions(ImmutableArray<ReleaseAsset> assets, int pr)
    {
        var own = ReadOwn(assets).Where(asset => asset.Name.Pr == pr).ToImmutableArray();
        var newest = own.Select(asset => asset.Name.Version).DefaultIfEmpty(0).Max();
        return [.. own.Where(asset => asset.Name.Version < newest).Select(asset => asset.Asset)];
    }

    /// <summary>
    /// The assets of a rule release to delete when the PR closes, so each rule keeps its latest recording. Merged: for each
    /// run the PR recorded, its newest recording stays; its older ones go, and so do other PRs' recordings of that run,
    /// unless that PR is still open (its posts need them; the one merged last wins). Closed without merging: all of the
    /// PR's go.
    /// </summary>
    public static ImmutableArray<ReleaseAsset> PlanRuleDeletions(ImmutableArray<ReleaseAsset> assets, int pr, bool merged, IReadOnlySet<int> openPrs)
    {
        var named = ReadOwn(assets).ToImmutableArray();
        var own = named.Where(asset => asset.Name.Pr == pr).ToImmutableArray();
        if (!merged)
        {
            return [.. own.Select(asset => asset.Asset)];
        }

        var newest = own.GroupBy(asset => asset.Name.Run).ToImmutableDictionary(group => group.Key, group => group.Max(asset => asset.Name.Version));
        return [.. named
            .Where(asset => newest.TryGetValue(asset.Name.Run, out var latest)
                && ((asset.Name.Pr == pr && asset.Name.Version < latest) || (asset.Name.Pr != pr && !openPrs.Contains(asset.Name.Pr))))
            .Select(asset => asset.Asset)];
    }

    /// <summary>A release's lines once cleaned: how many of the PR's assets it kept, how many it holds, and a warning near its limit.</summary>
    public static ImmutableArray<string> DescribeCleaned(string tag, ImmutableArray<ReleaseAsset> assets, ImmutableArray<ReleaseAsset> deleted, int pr)
    {
        var left = assets.Length - deleted.Length;
        var kept = assets.Count(asset => !deleted.Contains(asset) && ReleaseNaming.ReadAsset(asset.Name) is Optional<AssetName>.Some name && name.Value.Pr == pr);
        var line = $"{tag}: kept {kept} asset(s) of PR #{pr}; it holds {left} of at most {MaxAssets:N0} assets.";
        return left > WarnAssets
            ? [line, $"::warning::{tag} holds {left} of at most {MaxAssets:N0} assets (docs/hosting.md, \"The releases' limit\")."]
            : [line];
    }

    private static IEnumerable<(ReleaseAsset Asset, AssetName Name)> ReadOwn(ImmutableArray<ReleaseAsset> assets) =>
        assets
            .Select(asset => (Asset: asset, Name: ReleaseNaming.ReadAsset(asset.Name)))
            .Where(asset => asset.Name is Optional<AssetName>.Some)
            .Select(asset => (asset.Asset, ((Optional<AssetName>.Some)asset.Name).Value));
}
