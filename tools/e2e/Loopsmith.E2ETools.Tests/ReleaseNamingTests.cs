using Loopsmith.E2ETools.Core;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Tests;

/// <summary>Which release each run goes to, and the names its recordings and its rule's flow take there.</summary>
public sealed class ReleaseNamingTests
{
    [Theory]
    [InlineData("Rules/Hunter/Arc/Melees/hits-desktop.gif", "rules-hunter-arc-melees-resources")]
    [InlineData("Rules/AllClasses/Arc/Fragments/x-mobile.gif", "rules-all-classes-arc-fragments-resources")]
    [InlineData("Rules/Hunter/ClassAbilities/x-desktop.gif", "rules-hunter-class-abilities-resources")]
    [InlineData("Rules/Mods/Arms/x-desktop.gif", "rules-mods-arms-resources")]
    public void A_rule_scenarios_release_is_its_folders_under_Rules_kebab_cased(string gif, string release) =>
        Assert.Equal(release, ReleaseNaming.FindRelease(gif));

    [Theory]
    [InlineData("Starting/x-desktop.gif")]
    [InlineData("Loop/Weapons/x-mobile.gif")]
    [InlineData("Recordings/x-desktop.gif")]
    public void Any_other_scenarios_release_is_prs_resources(string gif) =>
        Assert.Equal(ReleaseNaming.PrsRelease, ReleaseNaming.FindRelease(gif));

    [Theory]
    [InlineData("rules-hunter-arc-melees-resources", true)]
    [InlineData("prs-resources", false)]
    [InlineData("rules-resources", false)]
    [InlineData("v1.0", false)]
    public void Rule_releases_are_told_apart_from_any_other_tag(string tag, bool isRuleRelease) =>
        Assert.Equal(isRuleRelease, ReleaseNaming.IsRuleRelease(tag));

    [Fact]
    public void A_flow_is_named_by_its_rule_once_for_all_its_runs()
    {
        Assert.Equal(
            Optional.Some("pr-7-v3-hunter-arc-aspects-lethal-current-2.png"),
            ReleaseNaming.NameFlow(7, 3, Optional.Some("Rules/Hunter/Arc/Aspects/hunter-arc-aspects-lethal-current-2.png")));
        Assert.Equal(Optional.None<string>(), ReleaseNaming.NameFlow(7, 3, Optional.None<string>()));
    }

    [Fact]
    public void A_runs_GIF_and_MP4_share_their_stem()
    {
        Assert.Equal("pr-7-v2-hits-desktop.gif", ReleaseNaming.NameAsset(7, 2, "Rules/Hunter/Arc/Melees/hits-desktop.gif", ".gif"));
        Assert.Equal("pr-7-v2-hits-desktop.mp4", ReleaseNaming.NameAsset(7, 2, "Rules/Hunter/Arc/Melees/hits-desktop.gif", ".mp4"));
    }

    [Fact]
    public void A_name_past_200_characters_keeps_its_start_and_ends_with_a_hash_of_the_GIFs_path()
    {
        var gif = "Loop/Weapons/" + string.Concat(Enumerable.Repeat("a-rule-whose-title-goes-on-", 9)) + "desktop.gif";

        var name = ReleaseNaming.NameAsset(7, 2, gif, ".gif");
        var other = ReleaseNaming.NameAsset(140, 12, gif, ".mp4");

        Assert.Equal(ReleaseNaming.MaxName, name.Length);
        Assert.StartsWith("pr-7-v2-a-rule-whose-title-goes-on-", name);
        Assert.Matches("-[0-9a-f]{8}\\.gif$", name);
        Assert.Equal(name[^12..^4], other[^12..^4]);
    }

    [Theory]
    [InlineData("AllClasses", "all-classes")]
    [InlineData("ClassAbilities", "class-abilities")]
    [InlineData("HTMLPage", "html-page")]
    [InlineData("Arc", "arc")]
    public void A_folder_is_kebab_cased_like_Humanizers_Kebaberize(string folder, string kebab) =>
        Assert.Equal(kebab, ReleaseNaming.ToKebabCase(folder));
}
