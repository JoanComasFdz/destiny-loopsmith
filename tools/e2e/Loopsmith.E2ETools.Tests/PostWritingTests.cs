using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;
using static Loopsmith.E2ETools.Tests.Support.Posts;

namespace Loopsmith.E2ETools.Tests;

/// <summary>The post: its sections, each scenario a closed &lt;details&gt;, its flow, its Gherkin, its screens and their links.</summary>
public sealed class PostWritingTests
{
    [Fact]
    public void Both_kinds_show_the_PRs_own_first_then_the_rerecorded_with_their_fixtures()
    {
        var post = PostWriting.WritePost(Repo, 2, [
            RunOf("EnemyCountTests", "Counts", "desktop", Fixtures("Scenarios/Loop/Weapons/a.build.yaml", "Scenarios/Loop/Weapons/b.build.yaml")),
            RunOf("BuildImportTests", "Imports", "desktop"),
            RunOf("BuildImportTests", "Imports", "mobile"),
        ], "Text.");

        var own = post.IndexOf("### Added or changed by this PR", StringComparison.Ordinal);
        var rerecorded = post.IndexOf("### Re-recorded: their fixtures changed", StringComparison.Ordinal);
        var fixtures = "_`Scenarios/Loop/Weapons/a.build.yaml`, `Scenarios/Loop/Weapons/b.build.yaml` changed._";
        Assert.True(own < post.IndexOf("<b>Imports title</b>", StringComparison.Ordinal));
        Assert.True(post.IndexOf("<b>Imports title</b>", StringComparison.Ordinal) < rerecorded);
        Assert.True(rerecorded < post.IndexOf("<b>Counts title</b>", StringComparison.Ordinal));
        Assert.Contains("\n#### 🖥️ Desktop\n", post);
        Assert.Contains("\n#### 📱 Mobile\n", post);
        Assert.True(post.IndexOf("<b>Counts title</b>", StringComparison.Ordinal) < post.IndexOf(fixtures, StringComparison.Ordinal));
        Assert.DoesNotContain("### Imports title", post);
    }

    [Fact]
    public void Each_scenario_is_a_closed_details_holding_its_Gherkin_and_screens_and_the_sections_stay_outside()
    {
        var post = WritePost(RunOf("EnemyCountTests", "Counts", "desktop", Fixtures("f.yaml")), RunOf("BuildImportTests", "Imports", "desktop"));

        var imports = post[post.IndexOf("<b>Imports title</b>", StringComparison.Ordinal)..post.IndexOf("### Re-recorded", StringComparison.Ordinal)];
        Assert.Equal(2, CountOf(post, "<details><summary>"));
        Assert.Equal(2, CountOf(post, "\n</details>\n"));
        Assert.DoesNotContain("<details open", post);
        Assert.Contains("\n<details><summary><b>Imports title</b></summary>\n\n```gherkin\nScenario: Imports\n```", post);
        Assert.EndsWith("</details>", imports.TrimEnd());
        Assert.Contains("#### 🖥️ Desktop", imports);
        Assert.True(post.IndexOf("### Added or changed by this PR", StringComparison.Ordinal) < post.IndexOf("<details>", StringComparison.Ordinal));
        Assert.Equal(2, PostVersioning.CountScenarios(post));
    }

    [Fact]
    public void A_title_is_escaped_in_its_summary()
    {
        var run = RunOf("ReloadTests", "Reloads", "desktop");

        var post = WritePost(run with { Entry = run.Entry with { Title = "Keeps <b> & <i> as text" } });

        Assert.Contains("<summary><b>Keeps &lt;b&gt; &amp; &lt;i&gt; as text</b></summary>", post);
    }

    [Fact]
    public void Only_rerecorded_scenarios_leave_out_the_PRs_own_heading()
    {
        var post = WritePost(RunOf("CodeLensTests", "Lens", "desktop", Fixtures("Workspace/x.cs")));

        Assert.DoesNotContain("Added or changed by this PR", post);
        Assert.Contains("### Re-recorded: their fixtures changed", post);
        Assert.Contains("<details><summary><b>Lens title</b></summary>", post);
        Assert.Contains("_`Workspace/x.cs` changed._", post);
    }

    [Fact]
    public void Only_the_PRs_own_scenarios_read_as_before_without_section_headings()
    {
        var post = WritePost(RunOf("BuildImportTests", "Imports", "desktop"));

        Assert.DoesNotContain("Added or changed by this PR", post);
        Assert.DoesNotContain("Re-recorded", post);
        Assert.Contains("\n<details><summary><b>Imports title</b></summary>\n", post);
        Assert.Contains("\n#### 🖥️ Desktop\n", post);
    }

    [Fact]
    public void A_mobile_run_reads_as_its_screen()
    {
        var post = WritePost(RunOf("ReloadTests", "Reloads", "mobile"));

        Assert.DoesNotContain("Re-recorded", post);
        Assert.Contains("\n<details><summary><b>Reloads title</b></summary>\n", post);
        Assert.Contains("\n#### 📱 Mobile\n", post);
    }

    [Fact]
    public void A_failing_scenario_keeps_its_mark_in_either_section()
    {
        var post = WritePost(
            RunOf("BuildImportTests", "Imports", "desktop", passed: false),
            RunOf("ReloadTests", "Reloads", "desktop", Fixtures("f.yaml"), passed: false));

        Assert.Contains("<details><summary><b>❌ Imports title</b></summary>", post);
        Assert.Contains("<details><summary><b>❌ Reloads title</b></summary>", post);
        Assert.Equal(2, CountOf(post, "#### ❌ 🖥️ Desktop"));
    }

    [Fact]
    public void A_rule_runs_GIF_and_player_link_point_at_its_rule_release()
    {
        var post = WritePost(WithMp4(RunOf("CombinationBlowTests", "Hits", "desktop"), "rules-hunter-arc-melees-resources"));

        Assert.Contains("https://github.com/Owner/repo/releases/download/rules-hunter-arc-melees-resources/pr-1-v1-Hits-desktop.gif", post);
        Assert.Contains("(https://owner.github.io/repo/e2e-player.html?v=pr-1-v1-Hits-desktop.mp4&r=rules-hunter-arc-melees-resources)", post);
    }

    [Fact]
    public void Any_other_runs_GIF_and_player_link_point_at_prs_resources_with_no_release_in_the_link()
    {
        var post = WritePost(WithMp4(RunOf("ReloadTests", "Reloads", "desktop"), ReleaseNaming.PrsRelease));

        Assert.Contains("/releases/download/prs-resources/pr-1-v1-Reloads-desktop.gif", post);
        Assert.Contains("(https://owner.github.io/repo/e2e-player.html?v=pr-1-v1-Reloads-desktop.mp4)", post);
        Assert.Contains("\n[▶ Play MP4](https://owner.github.io/repo/e2e-player.html?v=pr-1-v1-Reloads-desktop.mp4)\n", post);
    }

    [Fact]
    public void A_run_without_its_GIF_says_so_under_its_screen()
    {
        var run = RunOf("ReloadTests", "Reloads", "desktop");

        var post = WritePost(run with { Files = run.Files with { HasGif = false } });

        Assert.Contains("\n#### 🖥️ Desktop\n\n_No recording._\n", post);
        Assert.DoesNotContain("![", post);
    }

    [Fact]
    public void A_rule_scenario_reads_title_then_its_flow_then_its_Gherkin_then_its_screens()
    {
        var post = WritePost(
            WithFlow(RunOf("LethalCurrentTests", "Arms", "desktop"), "hunter-arc-aspects-lethal-current-2"),
            WithFlow(RunOf("LethalCurrentTests", "Arms", "mobile"), "hunter-arc-aspects-lethal-current-2"));

        var flow = "![Flow of hunter-arc-aspects-lethal-current-2](https://github.com/Owner/repo/releases/download/"
            + "rules-hunter-arc-aspects-resources/pr-1-v1-hunter-arc-aspects-lethal-current-2.png)";
        Assert.Equal(1, CountOf(post, flow));
        Assert.True(post.IndexOf("<b>Arms title</b>", StringComparison.Ordinal) < post.IndexOf(flow, StringComparison.Ordinal));
        Assert.True(post.IndexOf(flow, StringComparison.Ordinal) < post.IndexOf("```gherkin", StringComparison.Ordinal));
        Assert.True(post.IndexOf("```gherkin", StringComparison.Ordinal) < post.IndexOf("#### 🖥️ Desktop", StringComparison.Ordinal));
        Assert.True(post.IndexOf("#### 🖥️ Desktop", StringComparison.Ordinal) < post.IndexOf("#### 📱 Mobile", StringComparison.Ordinal));
    }

    [Fact]
    public void A_rule_the_viewer_couldnt_draw_says_why_where_its_flow_would_be()
    {
        var run = RunOf("SparkOfShockTests", "Jolts", "desktop");

        var post = WritePost(run with
        {
            Entry = run.Entry with
            {
                Rule = Optional.Some("all-classes-arc-fragments-spark-of-shock-1"),
                FlowError = Optional.Some("No rule is named 'all-classes-arc-fragments-spark-of-shock-1'."),
            },
        });

        var line = "_No flow drawn for `all-classes-arc-fragments-spark-of-shock-1`: No rule is named 'all-classes-arc-fragments-spark-of-shock-1'._";
        Assert.Contains(line, post);
        Assert.True(post.IndexOf("<b>Jolts title</b>", StringComparison.Ordinal) < post.IndexOf(line, StringComparison.Ordinal));
        Assert.True(post.IndexOf(line, StringComparison.Ordinal) < post.IndexOf("```gherkin", StringComparison.Ordinal));
        Assert.DoesNotContain("![Flow", post);
    }

    [Fact]
    public void A_scenario_that_names_no_rule_shows_no_flow()
    {
        var post = WritePost(RunOf("ReloadTests", "Reloads", "desktop"));

        Assert.DoesNotContain("Flow", post);
        Assert.DoesNotContain("No flow drawn", post);
    }

    [Fact]
    public void The_post_starts_with_its_version_and_what_changed_and_ends_with_the_footer()
    {
        var post = PostWriting.WritePost(Repo, 3, [RunOf("ReloadTests", "Reloads", "desktop")], "Ported the poster.");

        Assert.StartsWith("## Scenarios (V3)\n\n**What changed**\n\nPorted the poster.\n\n", post);
        Assert.EndsWith($"\n---\n{PostVersioning.Footer}\n", post);
    }
}
