using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Chest;

/// <summary>Emergency Reinforcement's rules (rules/mods/chest.yaml), one scenario per rule claim.</summary>
public sealed class EmergencyReinforcementTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Emergency Reinforcement spends three Armor Charges at critical health
          # Rule mods-chest-emergency-reinforcement-1: Critical health ➜ Emergency Reinforcement equipped and Armor Charge 3+ stacks ➜ Emergency Reinforcement (6s | 9s | 12s by stacks spent), ends Armor Charge
          Given the build "emergency-reinforcement-orbs" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I select "2 in 4 s · Spark of Amplitude"
          And I tap "💀 kill"
          And I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          And its BUFFS show only "Amplified", "Bolt Charge" and "Armor Charge x1"
          # keeping steps #1 and #2: their Armor Charge x1 is the first of the three #7 spends
          When I tap "Disorienting Blow" in the trigger panel
          And I select "2 in 4 s · Spark of Amplitude"
          And I tap "💀 kill"
          And I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #4's heading is "#4 Pick up Orb of Power"
          And its BUFFS show only "Amplified", "Bolt Charge" and "Armor Charge x2"
          # keeping steps #1 to #4: their Armor Charge x2 is two of the three #7 spends
          When I tap "Disorienting Blow" in the trigger panel
          And I select "2 in 4 s · Spark of Amplitude"
          And I tap "💀 kill"
          And I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #6's heading is "#6 Pick up Orb of Power"
          And its BUFFS show only "Amplified", "Bolt Charge" and "Armor Charge x3 (at max)"
          # keeping steps #1 to #6: their Armor Charge x3 is what #7 spends
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #7's heading is "#7 Critical health"
          And "Emergency Reinforcement" fires "consumes Armor Charge x3" and "Emergency Reinforcement (6s)"
          And its BUFFS show only "Amplified", "Bolt Charge", "Critical health" and "Emergency Reinforcement"
        """)]
    public async Task EmergencyReinforcementSpendsThreeArmorChargesAtCriticalHealth(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("emergency-reinforcement-orbs.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await KillTwoAndPickUpTheOrbAsync(2);

        // Then
        var first = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(first)
            .HasHeading("#2 Pick up Orb of Power")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge", "Armor Charge x1");

        // When
        await KillTwoAndPickUpTheOrbAsync(4);

        // Then
        var second = await Frontend.Designer.Loop.Step(4).ReadAsync();
        Asserting.That(second)
            .HasHeading("#4 Pick up Orb of Power")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge", "Armor Charge x2");

        // When
        await KillTwoAndPickUpTheOrbAsync(6);

        // Then
        var third = await Frontend.Designer.Loop.Step(6).ReadAsync();
        Asserting.That(third)
            .HasHeading("#6 Pick up Orb of Power")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge", "Armor Charge x3 (at max)");

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(7);

        // Then
        var critical = await Frontend.Designer.Loop.Step(7).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#7 Critical health")
            .Fires(new ArmorMod.EmergencyReinforcement(), "consumes Armor Charge x3", "Emergency Reinforcement (6s)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge", "Critical health", "Emergency Reinforcement");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Emergency Reinforcement gives damage resistance while it is on you
          # Rule mods-chest-emergency-reinforcement-2: Always ➜ Emergency Reinforcement equipped and Emergency Reinforcement on you ➜ Damage resistance (10%)
          Given the build "emergency-reinforcement-orbs" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I select "2 in 4 s · Spark of Amplitude"
          And I tap "💀 kill"
          And I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          And its BUFFS show only "Amplified", "Bolt Charge" and "Armor Charge x1"
          # keeping steps #1 and #2: their Armor Charge x1 is the first of the three #7 spends
          When I tap "Disorienting Blow" in the trigger panel
          And I select "2 in 4 s · Spark of Amplitude"
          And I tap "💀 kill"
          And I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #4's heading is "#4 Pick up Orb of Power"
          And its BUFFS show only "Amplified", "Bolt Charge" and "Armor Charge x2"
          # keeping steps #1 to #4: their Armor Charge x2 is two of the three #7 spends
          When I tap "Disorienting Blow" in the trigger panel
          And I select "2 in 4 s · Spark of Amplitude"
          And I tap "💀 kill"
          And I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #6's heading is "#6 Pick up Orb of Power"
          And its BUFFS show only "Amplified", "Bolt Charge" and "Armor Charge x3 (at max)"
          # keeping steps #1 to #6: their Armor Charge x3 is what #7 spends
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #7's heading is "#7 Critical health"
          And its BUFFS show only "Amplified", "Bolt Charge", "Critical health" and "Emergency Reinforcement"
          When I tap "▸ DAMAGE RESISTANCE" on card #7
          # 34.5% is every Guardian's own, 15% is Amplified's (Disorienting Blow's kills made you Amplified): with Emergency Reinforcement's 10%, 1 − (1 − 34.5%) × (1 − 15%) × (1 − 10%). The Compendium gives Emergency Reinforcement no PvP value: "?".
          Then its "All damage" resistance reads "49.89%" PvE and "?%" PvP
          And "Emergency Reinforcement" gives "10% · PvP ?%" of it
        """)]
    public async Task EmergencyReinforcementGivesDamageResistanceWhileItIsOnYou(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("emergency-reinforcement-orbs.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await KillTwoAndPickUpTheOrbAsync(2);

        // Then
        var first = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(first)
            .HasHeading("#2 Pick up Orb of Power")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge", "Armor Charge x1");

        // When
        await KillTwoAndPickUpTheOrbAsync(4);

        // Then
        var second = await Frontend.Designer.Loop.Step(4).ReadAsync();
        Asserting.That(second)
            .HasHeading("#4 Pick up Orb of Power")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge", "Armor Charge x2");

        // When
        await KillTwoAndPickUpTheOrbAsync(6);

        // Then
        var third = await Frontend.Designer.Loop.Step(6).ReadAsync();
        Asserting.That(third)
            .HasHeading("#6 Pick up Orb of Power")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge", "Armor Charge x3 (at max)");

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(7);

        // Then
        var critical = await Frontend.Designer.Loop.Step(7).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#7 Critical health")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge", "Critical health", "Emergency Reinforcement");

        // When
        await Frontend.Designer.Loop.Step(7).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(7).WaitUntilResistanceSourcesShownAsync();

        // Then
        // 34.5% is every Guardian's own, 15% is Amplified's (Disorienting Blow's kills made you Amplified): with Emergency Reinforcement's 10%, 1 − (1 − 34.5%) × (1 − 15%) × (1 − 10%). The Compendium gives Emergency Reinforcement no PvP value: "?".
        var resisting = await Frontend.Designer.Loop.Step(7).ReadAsync();
        Asserting.That(resisting)
            .HasResistance("All damage", "49.89%", "?%")
            .HasResistanceFrom(new ArmorMod.EmergencyReinforcement(), "10% · PvP ?%");
    }

    // Two Disorienting Blow kills at once drop an Orb of Power, and its pickup (step pickUpStep) gives an Armor Charge.
    private async Task KillTwoAndPickUpTheOrbAsync(int pickUpStep)
    {
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 4 s · Spark of Amplitude");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(pickUpStep - 1);
        await Frontend.Designer.TriggerPalette.ClickPickupAsync(new Keyword.OrbOfPower());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(pickUpStep);
    }
}
