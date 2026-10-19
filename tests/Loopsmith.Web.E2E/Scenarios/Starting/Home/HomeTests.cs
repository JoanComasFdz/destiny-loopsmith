using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;

namespace Loopsmith.Web.E2E.Scenarios.Starting;

/// <summary>What Home offers to start a loop from: from scratch, a build.yaml or a loop file; no example loops, bundled builds or DIM link.</summary>
public sealed class HomeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Home offers no example loops or bundled builds
          Given the app is open on Home
          When I tap the Loopsmith logo in the header
          Then Home offers only "Start from scratch", "import a build.yaml" and "import a loop"
          And it shows no example loop
          And it shows no shared DIM build
          And it shows no build written by hand
          And it has no DIM link field
        """)]
    public async Task HomeOffersNoExampleLoopsOrBundledBuilds(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Header.ClickLogoAsync();
        await Frontend.Home.WaitUntilShownAsync();

        // Then
        var home = await Frontend.Home.ReadAsync();
        Asserting.That(home)
            .Offers("Start from scratch", "import a build.yaml", "import a loop")
            .ShowsNoExampleLoop()
            .ShowsNoSharedDimBuild()
            .ShowsNoBuildWrittenByHand()
            .HasNoDimLinkField();
    }
}
