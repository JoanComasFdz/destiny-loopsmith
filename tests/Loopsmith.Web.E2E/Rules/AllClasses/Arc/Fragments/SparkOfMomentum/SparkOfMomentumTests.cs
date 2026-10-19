using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>Spark of Momentum's rules (rules/all-classes/arc/fragments.yaml), one scenario per rule claim.</summary>
public sealed class SparkOfMomentumTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Momentum's slide over an ammo brick gives x3 Bolt Charge
          # Rule all-classes-arc-fragments-spark-of-momentum-1: Slide over an ammo brick ➜ Spark of Momentum equipped ➜ +3 Bolt Charge
          Given the build "spark-of-momentum-alone" is imported, with no steps
          When I tap "📦 Ammo brick" in the trigger panel
          And I tick "🛝 by sliding"
          And I tap "Special"
          Then card #1's heading is "#1 Slide over a Special ammo brick"
          And "Spark of Momentum" fires "+3 Bolt Charge"
          And its BUFFS show only "Bolt Charge"
        """)]
    public async Task SparkOfMomentumsSlideOverAnAmmoBrickGivesX3BoltCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-momentum-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAmmoBrickAsync();
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Ammo brick");
        await Frontend.Designer.TriggerPalette.TickOptionAsync(new RowOption.BySliding());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.PickUp(new Brick.Special()));
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var slid = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(slid)
            .HasHeading("#1 Slide over a Special ammo brick")
            .Fires(new Fragment.SparkOfMomentum(), "+3 Bolt Charge")
            .Shows(new StateGroup.Buffs(), "Bolt Charge");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Momentum gives nothing when the brick is picked up without sliding
          # Rule all-classes-arc-fragments-spark-of-momentum-1: Slide over an ammo brick ➜ Spark of Momentum equipped ➜ +3 Bolt Charge
          Given the build "spark-of-momentum-alone" is imported, with no steps
          When I tap "📦 Ammo brick" in the trigger panel
          And I tap "Special"
          Then card #1's heading is "#1 Pick up a Special ammo brick"
          And "Spark of Momentum" fires nothing
          And its BUFFS show none
        """)]
    public async Task SparkOfMomentumGivesNothingWhenTheBrickIsPickedUpWithoutSliding(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-momentum-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAmmoBrickAsync();
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Ammo brick");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.PickUp(new Brick.Special()));
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var picked = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(picked)
            .HasHeading("#1 Pick up a Special ammo brick")
            .DoesNotFire(new Fragment.SparkOfMomentum())
            .ShowsNone(new StateGroup.Buffs());
    }
}
