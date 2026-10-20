using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Legs;

/// <summary>Stacks on Stacks's rules (rules/mods/legs.yaml), one scenario per rule claim.</summary>
public sealed class StacksOnStacksTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Stacks on Stacks adds a stack to every Armor Charge gained
          # Rule mods-legs-stacks-on-stacks-1: Always ➜ Stacks on Stacks equipped ➜ Armor Charge (+1 stack)
          Given the build "stacks-on-stacks-spark-of-amplitude" is imported, with no steps
          When I tick "caveats" above the loop
          And I tap "Disorienting Blow" in the trigger panel
          And I select "2 in 4 s · Spark of Amplitude"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Melee (💀 kill 2)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 picks up
          When I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          And "Orb of Power" has the caveats "+1 Armor Charge: +1 from Stacks on Stacks" and "+?% super energy: amount unknown"
          And its BUFFS show only "Amplified", "Bolt Charge" and "Armor Charge x2"
        """)]
    public async Task StacksOnStacksAddsAStackToEveryArmorChargeGained(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("stacks-on-stacks-spark-of-amplitude.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TickCaveatsAsync();
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 4 s · Spark of Amplitude");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Melee (💀 kill 2)")
            .Shows(new StateGroup.Ground(), "Orb of Power");

        // When
        await Frontend.Designer.TriggerPalette.ClickPickupAsync(new Keyword.OrbOfPower());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var pickedUp = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#2 Pick up Orb of Power")
            .HasCaveats(new Keyword.OrbOfPower(), "+1 Armor Charge: +1 from Stacks on Stacks", "+?% super energy: amount unknown")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge", "Armor Charge x2");
    }
}
