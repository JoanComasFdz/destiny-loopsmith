using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop;

/// <summary>A status's details opened from a step's state: what it does right then, after that step.</summary>
public sealed class StateDetailsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Amplified's details list what it switches on right then
          Given the build "state-details-amplified-spark-of-frequency" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Amplified · 1 effect" and "Bolt Charge"
          When I tap the "Amplified · 1 effect" chip in card #1's BUFFS
          Then the item details popup opens, titled "Amplified"
          And its "Right then, after #1" card reads only "15s" and "Spark of Frequency: +1 Bolt Charge per gain"
        """)]
    public async Task AmplifiedsDetailsListWhatItSwitchesOnRightThen(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("state-details-amplified-spark-of-frequency.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var finished = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(finished)
            .HasHeading("#1 Finisher")
            .Shows(new StateGroup.Buffs(), "Amplified · 1 effect", "Bolt Charge");

        // When
        await Frontend.Designer.Loop.Step(1).ClickChipAsync(new StateGroup.Buffs(), "Amplified · 1 effect");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new Keyword.Amplified())
            .HasRightThen("Right then, after #1", "15s", "Spark of Frequency: +1 Bolt Charge per gain");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Recharge's details from the build say nothing about a step
          Given the build "state-details-spark-of-recharge" is imported, with no steps
          When I tap Spark of Recharge's square in the Subclass box
          Then the item details popup opens, titled "Spark of Recharge"
          And it has no "Right then" card
        """)]
    public async Task SparkOfRechargesDetailsFromTheBuildSayNothingAboutAStep(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("state-details-spark-of-recharge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.Subclass.ClickFragmentAsync(new Fragment.SparkOfRecharge());
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new Fragment.SparkOfRecharge())
            .HasNoRightThen();
    }
}
