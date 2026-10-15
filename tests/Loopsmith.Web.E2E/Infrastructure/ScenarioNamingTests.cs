namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>The scenario names record.sh and the PR's GIFs rely on (runs without a browser).</summary>
public sealed class ScenarioNamingTests
{
    private const string Method = "GivenAppIsOpenOnHome_WhenIClickStartFromScratch_ThenTheDesignerShowsAnArcHunterWithNothingEquipped";

    [Fact]
    public void Title_HumanizesEachPartAndJoinsThemWithCommas() =>
        Assert.Equal(
            "Given app is open on home, When I click start from scratch, Then the designer shows an arc hunter with nothing equipped",
            ScenarioNaming.ToTitle(Method));

    [Fact]
    public void GifName_IsTheMethodNameKebaberized() =>
        Assert.Equal(
            "given-app-is-open-on-home-when-i-click-start-from-scratch-then-the-designer-shows-an-arc-hunter-with-nothing-equipped",
            ScenarioNaming.ToGifName(Method));

    [Fact]
    public void DomainFolder_IsTheNamespaceUnderScenarios() =>
        Assert.Equal("Starting", ScenarioNaming.ToDomainFolder(typeof(Scenarios.Starting.StartFromScratchTests)));
}
