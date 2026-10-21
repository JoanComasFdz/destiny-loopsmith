using System.Collections.Immutable;
using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Tests.Support;

/// <summary>The runs, posts, comments and assets the tests are made of.</summary>
internal static class Posts
{
    public const string Repo = "Owner/repo";

    /// <summary>A run of PR 1's V1 with its GIF on disk, no MP4 and no rule, in prs-resources.</summary>
    public static PlannedRun RunOf(string cls, string method, string screen, bool passed = true) =>
        RunOf(cls, method, screen, new RunSelection.Changed(), passed);

    public static PlannedRun RunOf(string cls, string method, string screen, RunSelection selection, bool passed = true) =>
        new(
            new ScenarioEntry(
                cls, method, $"{method} title", Optional.Some($"Scenario: {method}"), Optional.Some(screen),
                $"Loop/{method}-{screen}.gif", Optional.Some($"Loop/{method}-{screen}.mp4"), Optional.None<string>(),
                Optional.None<string>(), Optional.None<string>(), passed, selection),
            new RecordedFiles(HasGif: true, HasMp4: false, HasFlow: false),
            ReleaseNaming.PrsRelease,
            $"pr-1-v1-{method}-{screen}.gif",
            $"pr-1-v1-{method}-{screen}.mp4",
            Optional.None<string>());

    public static RunSelection Fixtures(params string[] paths) => new RunSelection.Fixtures([.. paths]);

    public static PlannedRun WithMp4(PlannedRun run, string release) =>
        run with { Files = run.Files with { HasMp4 = true }, Release = release };

    /// <summary>The run naming a rule whose flow the rule viewer drew, in the rule's release.</summary>
    public static PlannedRun WithFlow(PlannedRun run, string rule) =>
        run with
        {
            Entry = run.Entry with { Rule = Optional.Some(rule), Flow = Optional.Some($"Rules/Hunter/Arc/Aspects/{rule}.png") },
            Files = run.Files with { HasFlow = true },
            Release = "rules-hunter-arc-aspects-resources",
            FlowAsset = Optional.Some($"pr-1-v1-{rule}.png"),
        };

    public static string WritePost(params PlannedRun[] runs) => PostWriting.WritePost(Repo, 1, [.. runs], "Text.");

    /// <summary>
    /// A workflow post as PostWriting writes it: each title a closed &lt;details&gt;, its screens ####. With
    /// <paramref name="headings"/>, as it was written before them: titles ### (#### under a section), screens one level down.
    /// </summary>
    public static string PostOf(int version, string[] titles, bool sections = false, bool headings = false)
    {
        var level = sections ? "####" : "###";
        IEnumerable<string> lines = [$"## Scenarios (V{version})", "", "**What changed**", "", "Text.", ""];
        if (sections)
        {
            lines = lines.Concat(["### Added or changed by this PR", ""]);
        }

        foreach (var title in titles)
        {
            lines = headings
                ? lines.Concat([$"{level} {title}", "", "```gherkin", "Scenario: x", "```", "", $"{level}# 🖥️ Desktop", "", "![gif](u)", ""])
                : lines.Concat([$"<details><summary><b>{title}</b></summary>", "", "```gherkin", "Scenario: x", "```", "",
                    "#### 🖥️ Desktop", "", "![gif](u)", "", "</details>", ""]);
        }

        return string.Join('\n', lines.Concat(["---", PostVersioning.Footer, ""]));
    }

    public static PrComment CommentOf(long id, string body) => new(id, $"IC_{id}", PostVersioning.Bot, body);

    /// <summary>The post as the next version collapses it.</summary>
    public static PrComment CollapsedOf(long id, int version, params string[] titles) =>
        CommentOf(id, PostVersioning.PlanCollapses([CommentOf(id, PostOf(version, titles))], version + 1)[0].Body);

    /// <summary>The post as closing the PR shrinks it, a later version being the newest.</summary>
    public static PrComment ShrunkOf(long id, int version) =>
        CommentOf(id, PostVersioning.PlanShrinks([CommentOf(id, PostOf(version, ["A"])), CommentOf(id + 1, PostOf(version + 1, ["A"]))], false)[0].Body);

    public static ImmutableArray<ReleaseAsset> AssetsOf(params string[] names) =>
        [.. names.Select((name, at) => new ReleaseAsset(at, name))];

    public static ImmutableArray<string> ReadNames(ImmutableArray<ReleaseAsset> assets) =>
        [.. assets.Select(asset => asset.Name).Order(StringComparer.Ordinal)];

    public static int CountOf(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
