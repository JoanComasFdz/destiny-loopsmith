using System.Collections.Immutable;
using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Hosting;

/// <summary>
/// The PR closed: cleans the releases (ReleaseCleaning), logging each name deleted and each release's count, then shrinks
/// every post but the newest to its one line; the newest too when the PR closed without merging and lost rule recordings.
/// </summary>
public static class PrClosing
{
    public static Result<Unit, string> ClosePr(E2EToolsOptions options, E2ECommand.ClosePr command)
    {
        var cleaned = CleanReleases(options, command.Merged);   // impure
        if (cleaned is Result<bool, string>.Error notCleaned)
        {
            return new Result<Unit, string>.Error(notCleaned.Failure);
        }

        var unmergedRules = ((Result<bool, string>.Ok)cleaned).Value;
        return ShrinkPosts(options, unmergedRules);   // impure
    }

    /// <summary>True when it deleted rule recordings of a PR closed without merging (its newest post shrinks too).</summary>
    private static Result<bool, string> CleanReleases(E2EToolsOptions options, bool merged)
    {
        var pages = GitHubCli.CallGh(["api", "--paginate", "--slurp", $"repos/{options.Repo}/releases?per_page=100"], Optional.None<string>());   // impure
        var listed = pages.Bind(JsonReading.ReadReleases).Map(ReleaseCleaning.PickReleases);   // pure
        Result<ImmutableArray<int>, string> open = new Result<ImmutableArray<int>, string>.Ok([]);   // pure
        if (merged)
        {
            open = ReadOpenPrs(options);   // impure
        }

        if (listed is Result<ImmutableArray<Release>, string>.Error noReleases)
        {
            return new Result<bool, string>.Error(noReleases.Failure);
        }

        if (open is Result<ImmutableArray<int>, string>.Error noPrs)
        {
            return new Result<bool, string>.Error(noPrs.Failure);
        }

        var releases = ((Result<ImmutableArray<Release>, string>.Ok)listed).Value;
        var openPrs = ((Result<ImmutableArray<int>, string>.Ok)open).Value.ToImmutableHashSet();   // pure
        var deletedByRelease = ImmutableArray.CreateBuilder<(string Tag, ImmutableArray<ReleaseAsset> Deleted)>();
        foreach (var release in releases)
        {
            var cleaned = CleanRelease(options, release, merged, openPrs);   // impure
            if (cleaned is Result<ImmutableArray<ReleaseAsset>, string>.Error notCleaned)
            {
                return new Result<bool, string>.Error(notCleaned.Failure);
            }

            deletedByRelease.Add((release.Tag, ((Result<ImmutableArray<ReleaseAsset>, string>.Ok)cleaned).Value));
        }

        var hasPrsRelease = ReleaseCleaning.HasPrsRelease(releases);   // pure
        if (!hasPrsRelease)
        {
            E2EFiles.Report($"There is no {ReleaseNaming.PrsRelease} release: nothing to clean there.");   // impure
        }

        var unmergedRules = ReleaseCleaning.HasDeletedUnmergedRules(deletedByRelease.ToImmutable(), merged);   // pure
        return new Result<bool, string>.Ok(unmergedRules);
    }

    private static Result<ImmutableArray<int>, string> ReadOpenPrs(E2EToolsOptions options)
    {
        var pages = GitHubCli.CallGh(["api", "--paginate", "--slurp", $"repos/{options.Repo}/pulls?state=open&per_page=100"], Optional.None<string>());   // impure
        return pages.Bind(JsonReading.ReadPullNumbers);   // pure
    }

    /// <summary>Deletes what the release no longer keeps, and gives what it deleted.</summary>
    private static Result<ImmutableArray<ReleaseAsset>, string> CleanRelease(E2EToolsOptions options, Release release, bool merged, IReadOnlySet<int> openPrs)
    {
        var pages = GitHubCli.CallGh(["api", "--paginate", "--slurp", $"repos/{options.Repo}/releases/{release.Id}/assets?per_page=100"], Optional.None<string>());   // impure
        var read = pages.Bind(JsonReading.ReadAssets);   // pure
        if (read is Result<ImmutableArray<ReleaseAsset>, string>.Error noAssets)
        {
            return noAssets;
        }

        var assets = ((Result<ImmutableArray<ReleaseAsset>, string>.Ok)read).Value;
        var deleted = release.Tag == ReleaseNaming.PrsRelease
            ? ReleaseCleaning.PlanPrDeletions(assets, options.Pr)
            : ReleaseCleaning.PlanRuleDeletions(assets, options.Pr, merged, openPrs);   // pure
        foreach (var asset in deleted)
        {
            E2EFiles.Report($"{release.Tag}: deleting {asset.Name}");   // impure
            if (options.DryRun)
            {
                continue;
            }

            var gone = GitHubCli.CallGh(["api", "-X", "DELETE", $"repos/{options.Repo}/releases/assets/{asset.Id}"], Optional.None<string>());   // impure
            if (gone is Result<string, string>.Error notGone)
            {
                return new Result<ImmutableArray<ReleaseAsset>, string>.Error(notGone.Failure);
            }
        }

        var lines = ReleaseCleaning.DescribeCleaned(release.Tag, assets, deleted, options.Pr);   // pure
        E2EFiles.ReportAll(lines);   // impure
        return new Result<ImmutableArray<ReleaseAsset>, string>.Ok(deleted);
    }

    /// <summary>Every post but the newest becomes its one line, its recordings deleted with its version; the newest too when <paramref name="newestToo"/>.</summary>
    private static Result<Unit, string> ShrinkPosts(E2EToolsOptions options, bool newestToo)
    {
        var read = ScenarioPosting.ReadWorkflowComments(options);   // impure
        var edits = read.Map(comments => PostVersioning.PlanShrinks(comments, newestToo));   // pure
        if (edits is Result<ImmutableArray<CommentEdit>, string>.Error noComments)
        {
            return new Result<Unit, string>.Error(noComments.Failure);
        }

        var shrinks = ((Result<ImmutableArray<CommentEdit>, string>.Ok)edits).Value;
        if (shrinks.IsEmpty)
        {
            E2EFiles.Report($"No earlier post on #{options.Pr} to shrink.");   // impure
        }

        PostEditing.EditComments(options, shrinks);   // impure
        return new Result<Unit, string>.Ok(new Unit.Value());
    }
}
