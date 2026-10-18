using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.Arc.Aspects;

/// <summary>Ascension's rules (rules/hunter/arc/aspects.yaml), one scenario per rule claim.</summary>
public sealed class AscensionTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Ascension's air dodge amplifies you and jolts the target
          # Rule hunter-arc-aspects-ascension-1: Air class ability ➜ Ascension equipped ➜ Amplified, Jolt
          Given the build "ascension-marksmans-dodge" is imported, with no steps
          When I tap "AIR" in the trigger panel
          Then card #1's heading is "#1 Class ability (in the air)"
          And it reads "Class ability in the air"
          And "Ascension" fires "Jolt target (10s)" and "Amplified (15s)"
          And its BUFFS show only "Amplified"
          And its DEBUFFS show only "Jolt"
        """)]
    public async Task AscensionsAirDodgeAmplifiesYouAndJoltsTheTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("ascension-marksmans-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickSecondaryAsync(new SecondaryUse.Air());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability (in the air)")
            .Reads("Class ability in the air")
            .Fires(new Aspect.Ascension(), "Jolt target (10s)", "Amplified (15s)")
            .Shows(new StateGroup.Buffs(), "Amplified")
            .Shows(new StateGroup.Debuffs(), "Jolt");
    }
}
