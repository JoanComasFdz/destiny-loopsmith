using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;

namespace Loopsmith.Web.E2E.Scenarios.Loop;

/// <summary>The loops the Compare screen offers each side: the one in the designer and those imported there, no example loops.</summary>
public sealed class CompareTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Compare offers no example loops
          Given the app is open on Home
          When I tap "Compare" in the header
          # No loop is in the designer and none is imported, so no example loop fills the pickers: there are none.
          Then Compare shows "Nothing to compare yet"
          And it shows no loop picker
          And the help text reads "Pick a loop for each side — the one in the designer or a .loop.yaml you import."
          And the help text names no example loop
          When I tap the Loopsmith logo in the header
          And I tap "Start from scratch"
          And I tap "Compare" in the header
          Then the Left loop picker offers only "Arc Hunter loop (in the designer)"
          And the Right loop picker offers only "Arc Hunter loop (in the designer)"
        """)]
    public async Task CompareOffersNoExampleLoops(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Header.ClickCompareAsync();
        await Frontend.Compare.WaitUntilShownAsync();

        // Then
        var empty = await Frontend.Compare.ReadAsync();
        Asserting.That(empty)
            .ShowsNothingToCompare()
            .ShowsNoLoopPicker()
            .HelpReads("Pick a loop for each side — the one in the designer or a .loop.yaml you import.")
            .HelpNamesNoExample();

        // When
        await Frontend.Header.ClickLogoAsync();
        await Frontend.Home.WaitUntilShownAsync();
        await Frontend.Home.ClickStartFromScratchAsync();
        await Frontend.Designer.WaitUntilShownAsync();
        await Frontend.Header.ClickCompareAsync();
        await Frontend.Compare.WaitUntilPickersShownAsync();

        // Then
        var picking = await Frontend.Compare.ReadAsync();
        Asserting.That(picking)
            .OffersOnTheLeft("Arc Hunter loop (in the designer)")
            .OffersOnTheRight("Arc Hunter loop (in the designer)");
    }
}
