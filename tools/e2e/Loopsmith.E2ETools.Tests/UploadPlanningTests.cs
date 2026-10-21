using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;
using static Loopsmith.E2ETools.Tests.Support.Posts;

namespace Loopsmith.E2ETools.Tests;

/// <summary>Which files go to which release, and what a missing file says.</summary>
public sealed class UploadPlanningTests
{
    [Fact]
    public void Each_release_gets_one_upload_with_a_rules_flow_once_before_its_runs()
    {
        var desktop = WithMp4(WithFlow(RunOf("LethalCurrentTests", "Arms", "desktop"), "hunter-arc-aspects-lethal-current-2"), "rules-hunter-arc-aspects-resources");
        var mobile = WithFlow(RunOf("LethalCurrentTests", "Arms", "mobile"), "hunter-arc-aspects-lethal-current-2");
        var other = RunOf("ReloadTests", "Reloads", "desktop");

        var plan = UploadPlanning.PlanUploads([desktop, other, mobile], "out");

        Assert.Equal(["rules-hunter-arc-aspects-resources", ReleaseNaming.PrsRelease], plan.Uploads.Select(upload => upload.Release));
        Assert.Equal(
            ["pr-1-v1-hunter-arc-aspects-lethal-current-2.png", "pr-1-v1-Arms-desktop.gif", "pr-1-v1-Arms-desktop.mp4", "pr-1-v1-Arms-mobile.gif"],
            plan.Uploads[0].Files.Select(file => file.Asset));
        Assert.Equal(Optional.Some("Loop/Arms-desktop.gif"), plan.Uploads[0].RuleGif);
        Assert.Equal(Optional.None<string>(), plan.Uploads[1].RuleGif);
    }

    [Fact]
    public void A_missing_GIF_or_MP4_is_a_note_and_a_release_with_nothing_to_upload_has_no_upload()
    {
        var run = RunOf("ReloadTests", "Reloads", "desktop");

        var plan = UploadPlanning.PlanUploads([run with { Files = run.Files with { HasGif = false } }], "e2e-gifs");

        Assert.Equal(["No GIF for Reloads title (e2e-gifs/Loop/Reloads-desktop.gif).", "No MP4 for Reloads title (e2e-gifs/Loop/Reloads-desktop.mp4)."], plan.Notes);
        Assert.Empty(plan.Uploads);
    }

    [Fact]
    public void Runs_are_named_for_their_PR_and_version_in_their_release()
    {
        var entry = RunOf("CombinationBlowTests", "Hits", "desktop").Entry with { Gif = "Rules/Hunter/Arc/Melees/hits-desktop.gif" };

        var run = Assert.Single(UploadPlanning.PlanRuns(7, 3, [(entry, new RecordedFiles(true, true, false))]));

        Assert.Equal(("rules-hunter-arc-melees-resources", "pr-7-v3-hits-desktop.gif", "pr-7-v3-hits-desktop.mp4"), (run.Release, run.GifAsset, run.Mp4Asset));
    }

    [Fact]
    public void A_rule_release_names_its_rule_file_in_its_notes() =>
        Assert.StartsWith(
            "The latest e2e recordings (GIFs and MP4s) and flows (PNGs) of the rules of rules/all-classes/arc/fragments.yaml, ",
            UploadPlanning.DescribeRuleRelease("Rules/AllClasses/Arc/Fragments/jolts-desktop.gif"));
}
