using Loopsmith.E2ETools.Core;
using static Loopsmith.E2ETools.Tests.Support.Posts;

namespace Loopsmith.E2ETools.Tests;

/// <summary>What each release keeps when the PR closes.</summary>
public sealed class ReleaseCleaningTests
{
    [Fact]
    public void Prs_resources_keeps_the_PRs_newest_version_only()
    {
        var held = AssetsOf("pr-7-v1-a-desktop.gif", "pr-7-v2-a-desktop.gif", "pr-7-v10-b-desktop.gif", "pr-70-v1-a-desktop.gif", "owners-own.png");

        Assert.Equal(["pr-7-v1-a-desktop.gif", "pr-7-v2-a-desktop.gif"], ReadNames(ReleaseCleaning.PlanPrDeletions(held, 7)));
    }

    [Fact]
    public void A_merged_PR_keeps_its_newest_recording_of_each_rule_and_deletes_older_ones_of_closed_PRs()
    {
        var held = AssetsOf(
            "pr-5-v1-hits-desktop.gif",        // a closed PR's, replaced
            "pr-6-v1-hits-desktop.gif",        // an open PR's, kept for its posts
            "pr-7-v1-hits-desktop.gif",        // this PR's older one
            "pr-7-v2-hits-desktop.gif",
            "pr-7-v1-only-in-v1-mobile.gif",   // a rule V2 didn't re-record: still its newest
            "pr-5-v1-other-rule-desktop.gif",  // a rule this PR didn't touch
            "owners-own.png");

        var deleted = ReleaseCleaning.PlanRuleDeletions(held, 7, merged: true, new HashSet<int> { 6 });

        Assert.Equal(["pr-5-v1-hits-desktop.gif", "pr-7-v1-hits-desktop.gif"], ReadNames(deleted));
    }

    [Fact]
    public void A_long_named_rule_recording_of_another_PR_is_the_same_run_by_its_hash()
    {
        var held = AssetsOf(NameLong(5, 1), NameLong(140, 12), NameLong(140, 9), NameLong(140, 12, ".mp4"));

        var runs = held.Take(3).Select(asset => ReleaseNaming.ReadAsset(asset.Name)).ToHashSet();
        var deleted = ReleaseCleaning.PlanRuleDeletions(held, 140, merged: true, new HashSet<int>());

        Assert.Single(runs.Select(run => run.Match(some => some.Value.Run, _ => "")).Distinct());
        Assert.Equal([.. new[] { NameLong(5, 1), NameLong(140, 9) }.Order(StringComparer.Ordinal)], ReadNames(deleted));
    }

    [Fact]
    public void A_merged_PR_keeps_its_newest_flow_of_each_rule_like_a_recording()
    {
        var held = AssetsOf(
            "pr-5-v1-hunter-arc-aspects-lethal-current-2.png", "pr-7-v1-hunter-arc-aspects-lethal-current-2.png",
            "pr-7-v2-hunter-arc-aspects-lethal-current-2.png", "pr-7-v1-arms-desktop.gif");

        Assert.Equal(
            ["pr-5-v1-hunter-arc-aspects-lethal-current-2.png", "pr-7-v1-hunter-arc-aspects-lethal-current-2.png"],
            ReadNames(ReleaseCleaning.PlanRuleDeletions(held, 7, merged: true, new HashSet<int>())));
    }

    [Fact]
    public void A_PR_closed_without_merging_deletes_all_its_rule_recordings()
    {
        var held = AssetsOf("pr-7-v1-hits-desktop.gif", "pr-7-v2-hits-desktop.mp4", "pr-5-v1-hits-desktop.gif", "pr-70-v1-x.gif");

        var deleted = ReleaseCleaning.PlanRuleDeletions(held, 7, merged: false, new HashSet<int>());

        Assert.Equal(["pr-7-v1-hits-desktop.gif", "pr-7-v2-hits-desktop.mp4"], ReadNames(deleted));
    }

    [Fact]
    public void Prs_resources_is_cleaned_first_and_no_other_tag_is()
    {
        var releases = ReleaseCleaning.PickReleases([new(1, "rules-mods-arms-resources"), new(2, "v1.0"), new(3, ReleaseNaming.PrsRelease), new(4, "rules-hunter-arc-melees-resources")]);

        Assert.Equal([3L, 1L, 4L], releases.Select(release => release.Id));
    }

    [Fact]
    public void A_cleaned_release_says_what_it_kept_and_warns_near_its_limit()
    {
        var held = AssetsOf([.. Enumerable.Range(0, 902).Select(at => $"pr-3-v1-x{at}.gif"), "pr-7-v1-a.gif", "pr-7-v2-a.gif"]);

        var lines = ReleaseCleaning.DescribeCleaned("prs-resources", held, [held[^2]], 7);

        Assert.Equal(
            ["prs-resources: kept 1 asset(s) of PR #7; it holds 903 of at most 1,000 assets.",
             "::warning::prs-resources holds 903 of at most 1,000 assets (docs/hosting.md, \"The releases' limit\")."],
            lines);
    }

    /// <summary>A name shortened to 200 characters, as NameAsset writes it for a long GIF name.</summary>
    private static string NameLong(int pr, int version, string extension = ".gif")
    {
        var stem = $"pr-{pr}-v{version}-" + string.Concat(Enumerable.Repeat("a-rule-whose-title-goes-on-", 8));
        return stem[..(ReleaseNaming.MaxName - $"-7cec2320{extension}".Length)].TrimEnd('-') + $"-7cec2320{extension}";
    }
}
