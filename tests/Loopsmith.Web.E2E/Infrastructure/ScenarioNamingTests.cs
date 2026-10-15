namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>The scenario names record.sh and the PR's GIFs rely on (runs without a browser).</summary>
public sealed class ScenarioNamingTests
{
    [Fact]
    public void MethodName_IsTheTitlesWordsEachCapitalized() =>
        Assert.Equal(
            "ReapersTitheGives3StacksForAKillOf3",
            ScenarioNaming.ToMethodName("Reaper's Tithe gives 3 stacks for a kill of 3"));

    [Fact]
    public void MethodName_LeavesOutPunctuationAndEmoji() =>
        Assert.Equal(
            "OneForAllAChanceOn1HitSureOn3",
            ScenarioNaming.ToMethodName("One for All: a chance on 1 hit, sure on 3 · 🥊"));

    [Fact]
    public void GifName_IsTheMethodNameKebaberizedWithItsScreen() =>
        Assert.Equal(
            "start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile",
            ScenarioNaming.ToGifName("StartFromScratchOpensAnArcHunterWithNothingEquipped", Screen.Mobile));

    [Fact]
    public void DomainFolder_IsTheNamespaceUnderScenarios() =>
        Assert.Equal("Starting", ScenarioNaming.ToDomainFolder(typeof(Scenarios.Starting.StartFromScratchTests)));

    [Fact]
    public void Gherkin_ReadsItsTagsTitleAndDedentedText()
    {
        var gherkin = Gherkin.Read("""
            @desktop @mobile
            Scenario: Start from scratch opens an Arc Hunter
              Given the app is open on Home
            """);

        Assert.Equal(["@desktop", "@mobile"], gherkin.Tags);
        Assert.Equal("Start from scratch opens an Arc Hunter", gherkin.Title);
        Assert.Equal("@desktop @mobile\nScenario: Start from scratch opens an Arc Hunter\n  Given the app is open on Home", gherkin.Text);
    }
}
