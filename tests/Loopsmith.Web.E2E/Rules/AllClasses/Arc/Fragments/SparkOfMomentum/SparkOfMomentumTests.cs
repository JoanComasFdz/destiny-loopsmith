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
          Given the build "spark-of-momentum-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I tap "📦 slide over ammo"
          Then card #1's heading is "#1 Slide over an ammo brick with Festival Flight"
          And "Spark of Momentum" fires "+3 Bolt Charge"
          And its BUFFS show only "Bolt Charge"
        """)]
    public async Task SparkOfMomentumsSlideOverAnAmmoBrickGivesX3BoltCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-momentum-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.SlideOverAmmo());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var slid = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(slid)
            .HasHeading("#1 Slide over an ammo brick with Festival Flight")
            .Fires(new Fragment.SparkOfMomentum(), "+3 Bolt Charge")
            .Shows(new StateGroup.Buffs(), "Bolt Charge");
    }
}
