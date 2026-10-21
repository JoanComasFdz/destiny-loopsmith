using System.Collections.Immutable;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Core;

/// <summary>
/// The post of a PR's recorded scenarios. It starts with what changed; each scenario shows once, as a closed
/// &lt;details&gt; whose summary is its bold title (so the owner opens only the ones to look at), holding the flow PNG of the
/// rule its Gherkin names (or a line saying why none was drawn), its Gherkin, then a "🖥️ Desktop" and/or "📱 Mobile"
/// subtitle with that screen's GIF and, below it, "▶ Play MP4": both link to the site's player page, since GitHub shows
/// no player for a video it didn't store itself (a tap on a bare GIF would only download it).
/// </summary>
public static class PostWriting
{
    private static readonly ImmutableArray<(string Screen, string Label)> Screens = [("desktop", "🖥️ Desktop"), ("mobile", "📱 Mobile")];

    /// <summary>
    /// The post. The scenarios the PR added or changed come first, then those only re-recorded because a fixture file they
    /// use changed, each naming its changed fixtures, under ### headings. With only the first kind there are no section
    /// headings.
    /// </summary>
    public static string WritePost(string repo, int version, ImmutableArray<PlannedRun> runs, string changed)
    {
        var groups = GroupByScenario(runs);
        var own = groups.Where(group => !IsRerecorded(group[0])).ToImmutableArray();
        var rerecorded = groups.Where(group => IsRerecorded(group[0])).ToImmutableArray();
        IEnumerable<string> lines = [$"## Scenarios (V{version})", "", "**What changed**", "", changed, ""];
        if (rerecorded.IsEmpty)
        {
            lines = lines.Concat(own.SelectMany(group => WriteScenario(repo, group, "")));
        }
        else
        {
            if (!own.IsEmpty)
            {
                lines = lines.Concat(["### Added or changed by this PR", ""]).Concat(own.SelectMany(group => WriteScenario(repo, group, "")));
            }

            lines = lines
                .Concat(["### Re-recorded: their fixtures changed", "", "Their code didn't change, a fixture file they use did: they should behave as before.", ""])
                .Concat(rerecorded.SelectMany(group => WriteScenario(repo, group, DescribeFixtures(group[0]))));
        }

        return string.Join('\n', lines.Concat(["---", PostVersioning.Footer, ""]));
    }

    /// <summary>The site's player page: GitHub Pages serves &lt;owner&gt;/&lt;repo&gt; at https://&lt;owner, lowercase&gt;.github.io/&lt;repo&gt;/.</summary>
    public static string FindPlayer(string repo)
    {
        var parts = repo.Split('/', 2);
        return $"https://{parts[0].ToLowerInvariant()}.github.io/{parts[1]}/e2e-player.html";
    }

    /// <summary>The player page on an MP4: ?v=&lt;its name&gt;, and &amp;r=&lt;its release&gt; when that isn't prs-resources (the page's default).</summary>
    public static string LinkPlayer(string repo, string mp4Asset, string release) =>
        $"{FindPlayer(repo)}?v={mp4Asset}" + (release == ReleaseNaming.PrsRelease ? "" : $"&r={release}");

    /// <summary>The runs of each scenario (its class and method), in the order the scenarios first appear; desktop before mobile.</summary>
    private static ImmutableArray<ImmutableArray<PlannedRun>> GroupByScenario(ImmutableArray<PlannedRun> runs) =>
        [.. runs
            .GroupBy(run => (run.Entry.Class, run.Entry.Method))
            .Select(group => group.OrderBy(run => OrderScreen(run.Entry.Screen)).ToImmutableArray())];

    private static int OrderScreen(Optional<string> screen)
    {
        var at = Screens.Select(known => known.Screen).ToList().IndexOf(screen.UnwrapOr(""));
        return at < 0 ? Screens.Length : at;
    }

    private static bool IsRerecorded(PlannedRun run) => run.Entry.Selection is RunSelection.Fixtures;

    private static string DescribeFixtures(PlannedRun run)
    {
        var paths = run.Entry.Selection is RunSelection.Fixtures fixtures ? fixtures.Paths : [];
        return paths.IsEmpty ? "" : $"_{string.Join(", ", paths.Select(path => $"`{path}`"))} changed._";
    }

    /// <summary>
    /// A closed &lt;details&gt; whose summary is the scenario's title, holding why it is there, the flow of the rule it names
    /// (or why there is none), its Gherkin, then each screen as a #### subtitle with its GIF.
    /// </summary>
    private static IEnumerable<string> WriteScenario(string repo, ImmutableArray<PlannedRun> runs, string why)
    {
        var first = runs[0];
        var mark = runs.Any(run => !run.Entry.Passed) ? "❌ " : "";
        IEnumerable<string> lines = [$"<details><summary><b>{mark}{EscapeHtml(first.Entry.Title)}</b></summary>", ""];
        if (why.Length > 0)
        {
            lines = lines.Concat([why, ""]);
        }

        lines = lines.Concat(WriteFlow(repo, first));
        if (first.Entry.Gherkin is Optional<string>.Some gherkin && gherkin.Value.Length > 0)
        {
            lines = lines.Concat(["```gherkin", gherkin.Value, "```", ""]);
        }

        return lines.Concat(runs.SelectMany(run => WriteScreen(repo, first.Entry.Title, run))).Concat(["</details>", ""]);
    }

    private static IEnumerable<string> WriteScreen(string repo, string title, PlannedRun run)
    {
        var screen = DescribeScreen(run.Entry.Screen);
        var heading = $"#### {(run.Entry.Passed ? "" : "❌ ")}{screen}".TrimEnd();
        if (!run.Files.HasGif)
        {
            return [heading, "", "_No recording._", ""];
        }

        var url = $"https://github.com/{repo}/releases/download/{run.Release}/{run.GifAsset}";
        var image = $"![{title} · {screen}]({url})";
        if (!run.Files.HasMp4)
        {
            return [heading, "", image, ""];
        }

        var player = LinkPlayer(repo, run.Mp4Asset, run.Release);
        return [heading, "", $"[{image}]({player})", "", $"[▶ Play MP4]({player})", ""];
    }

    private static string DescribeScreen(Optional<string> screen)
    {
        var name = screen.UnwrapOr("");
        var known = Screens.Where(entry => entry.Screen == name).Select(entry => entry.Label).FirstOrDefault();
        return known ?? name;
    }

    /// <summary>
    /// The flow PNG of the rule a scenario's Gherkin names, from its release; a line saying why when the rule viewer
    /// couldn't draw it; nothing for a scenario that names no rule.
    /// </summary>
    private static IEnumerable<string> WriteFlow(string repo, PlannedRun run)
    {
        if (run.Entry.Rule is not Optional<string>.Some rule || rule.Value.Length == 0)
        {
            return [];
        }

        if (run.Files.HasFlow && run.FlowAsset is Optional<string>.Some asset)
        {
            return [$"![Flow of {rule.Value}](https://github.com/{repo}/releases/download/{run.Release}/{asset.Value})", ""];
        }

        var error = run.Entry.FlowError.UnwrapOr("");
        return [$"_No flow drawn for `{rule.Value}`: {(error.Length > 0 ? error : "not drawn")}_", ""];
    }

    /// <summary>A title as text inside the summary's HTML: &amp;, &lt; and &gt; escaped, quotes left as they are.</summary>
    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
}
