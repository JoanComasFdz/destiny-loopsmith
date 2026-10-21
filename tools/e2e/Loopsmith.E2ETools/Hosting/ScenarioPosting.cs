using System.Collections.Immutable;
using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Hosting;

/// <summary>
/// Posts a PR's recorded scenarios: uploads each run's GIF and MP4, and each rule's flow, to its release, posts them as a
/// new comment, then collapses every earlier post still open and hides it as Outdated. A hide or an edit GitHub refuses is
/// a warning, never a failure: the new post is up. Nothing recorded posts nothing.
/// </summary>
public static class ScenarioPosting
{
    private const int MaxHiddenRead = 100;

    public static Result<Unit, string> PostScenarios(E2EToolsOptions options, E2ECommand.PostScenarios command)
    {
        var json = E2EFiles.ReadScenariosJson(command.Out);   // impure
        var entries = json.Bind(JsonReading.ReadScenarios);   // pure
        if (entries is Result<ImmutableArray<ScenarioEntry>, string>.Error unread)
        {
            return new Result<Unit, string>.Error(unread.Failure);
        }

        var recorded = ((Result<ImmutableArray<ScenarioEntry>, string>.Ok)entries).Value;
        if (recorded.IsEmpty)
        {
            E2EFiles.Report("No scenario recorded: nothing to post.");   // impure
            return new Result<Unit, string>.Ok(new Unit.Value());
        }

        var read = ReadWorkflowComments(options);   // impure
        if (read is Result<ImmutableArray<PrComment>, string>.Error noComments)
        {
            return new Result<Unit, string>.Error(noComments.Failure);
        }

        var comments = ((Result<ImmutableArray<PrComment>, string>.Ok)read).Value;
        var version = PostVersioning.FindLastVersion(comments) + 1;   // pure
        var files = E2EFiles.FindRecordedFiles(command.Out, recorded);   // impure
        var runs = UploadPlanning.PlanRuns(options.Pr, version, files);   // pure
        var plan = UploadPlanning.PlanUploads(runs, command.Out);   // pure
        var uploaded = UploadRuns(options, command.Out, plan);   // impure
        if (uploaded is Result<Unit, string>.Error notUploaded)
        {
            return notUploaded;
        }

        var changed = DescribeChanges(command);   // impure
        if (changed is Result<string, string>.Error noMessage)
        {
            return new Result<Unit, string>.Error(noMessage.Failure);
        }

        var post = PostWriting.WritePost(options.Repo, version, runs, ((Result<string, string>.Ok)changed).Value);   // pure
        var posted = AddComment(options, post);   // impure
        if (posted is Result<Unit, string>.Error notPosted)
        {
            return notPosted;
        }

        var collapses = PostVersioning.PlanCollapses(comments, version);   // pure
        PostEditing.EditComments(options, collapses);   // impure
        var toCheck = PostVersioning.PlanHides(comments, version, ImmutableHashSet<string>.Empty);   // pure
        var hidden = ReadHidden(options, toCheck);   // impure
        if (hidden is Result<ImmutableHashSet<string>, string>.Error && !options.DryRun)
        {
            return new Result<Unit, string>.Ok(new Unit.Value());
        }

        var alreadyHidden = hidden.Match(ok => ok.Value, _ => ImmutableHashSet<string>.Empty);   // pure
        var hides = PostVersioning.PlanHides(comments, version, alreadyHidden);   // pure
        HideComments(options, hides);   // impure
        return new Result<Unit, string>.Ok(new Unit.Value());
    }

    /// <summary>The workflow's comments on the PR (PostVersioning.PickWorkflowComments).</summary>
    public static Result<ImmutableArray<PrComment>, string> ReadWorkflowComments(E2EToolsOptions options)
    {
        var pages = GitHubCli.CallGh(["api", "--paginate", "--slurp", $"repos/{options.Repo}/issues/{options.Pr}/comments?per_page=100"], Optional.None<string>());   // impure
        return pages.Bind(JsonReading.ReadComments).Map(PostVersioning.PickWorkflowComments);   // pure
    }

    private static Result<Unit, string> UploadRuns(E2EToolsOptions options, string outFolder, UploadPlan plan)
    {
        E2EFiles.ReportAll(plan.Notes);   // impure
        foreach (var upload in plan.Uploads)
        {
            var ensured = upload.RuleGif.Match(
                gif => EnsureRuleRelease(options, upload.Release, gif.Value),
                _ => new Result<Unit, string>.Ok(new Unit.Value()));   // impure
            if (ensured is Result<Unit, string>.Error notEnsured)
            {
                return notEnsured;
            }

            var staging = E2EFiles.CreateStagingPath();   // impure
            try
            {
                var targets = E2EFiles.StageUpload(outFolder, staging, upload.Files);   // impure
                var uploading = upload.Files.Select(file => $"Uploading {file.Asset} to {upload.Release}").ToImmutableArray();   // pure
                E2EFiles.ReportAll(uploading);   // impure
                var sent = options.DryRun
                    ? new Result<string, string>.Ok("")
                    : GitHubCli.CallGh(["release", "upload", upload.Release, .. targets, "--clobber", "--repo", options.Repo], Optional.None<string>());   // impure
                if (sent is Result<string, string>.Error notSent)
                {
                    return new Result<Unit, string>.Error(notSent.Failure);
                }
            }
            finally
            {
                E2EFiles.DeleteStaging(staging);   // impure
            }
        }

        return new Result<Unit, string>.Ok(new Unit.Value());
    }

    /// <summary>
    /// Creates a rule release that doesn't exist yet, as a pre-release. Another PR's run may create it at the same time: a
    /// creation GitHub refuses is fine when the release is there after it.
    /// </summary>
    private static Result<Unit, string> EnsureRuleRelease(E2EToolsOptions options, string release, string gif)
    {
        if (HasRelease(options, release))   // impure
        {
            return new Result<Unit, string>.Ok(new Unit.Value());
        }

        E2EFiles.Report($"Creating the pre-release {release}");   // impure
        if (options.DryRun)
        {
            return new Result<Unit, string>.Ok(new Unit.Value());
        }

        var notes = UploadPlanning.DescribeRuleRelease(gif);   // pure
        var made = GitHubCli.RunGh(["release", "create", release, "--prerelease", "--title", release, "--notes", notes, "--repo", options.Repo], Optional.None<string>());   // impure
        if (made.ExitCode == 0 || HasRelease(options, release))   // impure
        {
            return new Result<Unit, string>.Ok(new Unit.Value());
        }

        return new Result<Unit, string>.Error($"Couldn't create the release {release}:\n{made.Errors.Trim()}");
    }

    private static bool HasRelease(E2EToolsOptions options, string release) =>
        GitHubCli.RunGh(["release", "view", release, "--repo", options.Repo, "--json", "tagName"], Optional.None<string>()).ExitCode == 0;

    private static Result<string, string> DescribeChanges(E2ECommand.PostScenarios command)
    {
        var given = E2EFiles.ReadWhatChanged(command.Out);   // impure
        if (given.Length > 0)
        {
            return new Result<string, string>.Ok(given);
        }

        var message = GitHubCli.ReadCommitMessage(command.Commit);   // impure
        return message.Map(CommitDescribing.DescribeCommit);   // pure
    }

    private static Result<Unit, string> AddComment(E2EToolsOptions options, string post)
    {
        E2EFiles.Report(post);   // impure
        if (!options.DryRun)
        {
            var body = JsonReading.WriteBody(post);   // pure
            var sent = GitHubCli.CallGh(["api", "-X", "POST", $"repos/{options.Repo}/issues/{options.Pr}/comments", "--input", "-"], Optional.Some(body));   // impure
            if (sent is Result<string, string>.Error notSent)
            {
                return new Result<Unit, string>.Error(notSent.Failure);
            }
        }

        E2EFiles.Report($"Commented on #{options.Pr}.");   // impure
        return new Result<Unit, string>.Ok(new Unit.Value());
    }

    /// <summary>
    /// The node ids of these posts already hidden (GraphQL's isMinimized: REST doesn't say); an error, with a warning, when
    /// GitHub refuses the read or answers something else, so nothing is hidden this time (a dry run prints them all).
    /// </summary>
    private static Result<ImmutableHashSet<string>, string> ReadHidden(E2EToolsOptions options, ImmutableArray<PrComment> comments)
    {
        var ids = comments.Select(comment => comment.NodeId).Take(MaxHiddenRead).ToImmutableArray();   // pure
        if (ids.IsEmpty)
        {
            return new Result<ImmutableHashSet<string>, string>.Ok(ImmutableHashSet<string>.Empty);
        }

        var query = JsonReading.WriteHiddenQuery(ids);   // pure
        var answer = GitHubCli.RunGh(["api", "graphql", "--input", "-"], Optional.Some(query));   // impure
        var hidden = answer.ExitCode == 0
            ? JsonReading.ReadHidden(ids, answer.Output)
            : new Result<ImmutableHashSet<string>, string>.Error(answer.Errors.Trim().Length > 0 ? answer.Errors.Trim() : answer.Output.Trim());   // pure
        if (hidden is Result<ImmutableHashSet<string>, string>.Error unread)
        {
            E2EFiles.Report($"::warning::Couldn't read which posts on #{options.Pr} are hidden, so none is hidden: {unread.Failure}");   // impure
        }

        return hidden;
    }

    /// <summary>Hides each post as Outdated; a refusal is a warning: the post is collapsed all the same.</summary>
    private static void HideComments(E2EToolsOptions options, ImmutableArray<PrComment> comments)
    {
        foreach (var comment in comments)
        {
            var firstLine = PostVersioning.ReadFirstLine(comment.Body);   // pure
            E2EFiles.Report($"Hiding comment {comment.Id} as outdated: {firstLine}");   // impure
            if (options.DryRun)
            {
                continue;
            }

            var mutation = JsonReading.WriteHideMutation(comment.NodeId);   // pure
            var answer = GitHubCli.RunGh(["api", "graphql", "--input", "-"], Optional.Some(mutation));   // impure
            if (answer.ExitCode != 0)
            {
                E2EFiles.Report($"::warning::Couldn't hide comment {comment.Id} on #{options.Pr} as outdated: {answer.Errors.Trim()}");   // impure
            }
        }
    }
}
