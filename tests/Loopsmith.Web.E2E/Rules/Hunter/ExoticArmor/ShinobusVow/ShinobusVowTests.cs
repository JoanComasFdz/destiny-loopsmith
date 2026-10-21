using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.ExoticArmor;

/// <summary>Shinobu's Vow's rules (rules/hunter/exotic-armor.yaml), one scenario per rule claim.</summary>
public sealed class ShinobusVowTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Shinobu's Vow's grenade hit gives Bolt Charge and grenade energy
          # Rule hunter-exotic-armor-shinobus-vow-1: Grenade hit ➜ Shinobu's Vow equipped ➜ Grenade energy (4.2%), Bolt Charge
          Given the build "shinobus-vow-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And on "Grenade 🥊 hit" "Shinobu's Vow" fires "+1 Bolt Charge" and "+4.2% grenade energy"
          And its BUFFS show only "Bolt Charge"
        """)]
    public async Task ShinobusVowsGrenadeHitGivesBoltChargeAndGrenadeEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("shinobus-vow-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Grenade (🥊 hit)")
            .FiresOn("Grenade 🥊 hit", new ArmorPiece.ShinobusVow(), "+1 Bolt Charge", "+4.2% grenade energy")
            .Shows(new StateGroup.Buffs(), "Bolt Charge");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Shinobu's Vow gives grenade energy for every Bolt Charge gained
          # Rule hunter-exotic-armor-shinobus-vow-2: Bolt Charge gained ➜ Shinobu's Vow equipped ➜ Grenade energy (?)
          Given the build "shinobus-vow-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          # The energy per stack gained is unknown, so "?".
          And on "↳ Bolt Charge gained" "Shinobu's Vow" fires "+?% grenade energy"
          And its BUFFS show only "Bolt Charge"
        """)]
    public async Task ShinobusVowGivesGrenadeEnergyForEveryBoltChargeGained(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("shinobus-vow-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // The energy per stack gained is unknown, so "?".
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Grenade (🥊 hit)")
            .FiresOn("↳ Bolt Charge gained", new ArmorPiece.ShinobusVow(), "+?% grenade energy")
            .Shows(new StateGroup.Buffs(), "Bolt Charge");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Shinobu's Vow's Bolt Charge at max gives New Tricks, grenade energy and healing
          # Rule hunter-exotic-armor-shinobus-vow-3: Bolt Charge at max ➜ Shinobu's Vow equipped ➜ Grenade energy (~40%), Health (? + allies), New Tricks
          Given the build "shinobus-vow-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And its BUFFS show only "Bolt Charge"
          # keeping step #1: its Bolt Charge is what #2 declares at max
          When I tap "at max (x10)" on "Bolt Charge" under "On you" in the trigger panel
          Then card #2's heading is "#2 Bolt Charge at max (x10)"
          And "Shinobu's Vow" fires "New Tricks", "+~40% grenade energy" and "heals you and allies ?"
          And its BUFFS show only "Bolt Charge (at max)" and "New Tricks"
        """)]
    public async Task ShinobusVowsBoltChargeAtMaxGivesNewTricksGrenadeEnergyAndHealing(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("shinobus-vow-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Shows(new StateGroup.Buffs(), "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.BoltCharge(), "at max (x10)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var atMax = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(atMax)
            .HasHeading("#2 Bolt Charge at max (x10)")
            .Fires(new ArmorPiece.ShinobusVow(), "New Tricks", "+~40% grenade energy", "heals you and allies ?")
            .Shows(new StateGroup.Buffs(), "Bolt Charge (at max)", "New Tricks");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Shinobu's Vow's New Tricks is spent by the next grenade thrown
          # Rule hunter-exotic-armor-shinobus-vow-4: Grenade cast ➜ Shinobu's Vow equipped and New Tricks on you ➜ ends New Tricks
          Given the build "shinobus-vow-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And its BUFFS show only "Bolt Charge"
          # keeping step #1: its Bolt Charge is what #2 declares at max
          When I tap "at max (x10)" on "Bolt Charge" under "On you" in the trigger panel
          Then card #2's heading is "#2 Bolt Charge at max (x10)"
          And its BUFFS show only "Bolt Charge (at max)" and "New Tricks"
          # keeping step #2: its New Tricks is what #3's throw spends
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Grenade (🥊 hit)"
          # A grenade's hit throws it first: the card reads the throw first.
          And it reads "Grenade thrown"
          And on "Grenade thrown" "Shinobu's Vow" fires "consumes New Tricks"
          And its BUFFS show none
        """)]
    public async Task ShinobusVowsNewTricksIsSpentByTheNextGrenadeThrown(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("shinobus-vow-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Shows(new StateGroup.Buffs(), "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.BoltCharge(), "at max (x10)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var atMax = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(atMax)
            .HasHeading("#2 Bolt Charge at max (x10)")
            .Shows(new StateGroup.Buffs(), "Bolt Charge (at max)", "New Tricks");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        // A grenade's hit throws it first: the card reads the throw first.
        var thrown = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(thrown)
            .HasHeading("#3 Grenade (🥊 hit)")
            .Reads("Grenade thrown")
            .FiresOn("Grenade thrown", new ArmorPiece.ShinobusVow(), "consumes New Tricks")
            .ShowsNone(new StateGroup.Buffs());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Shinobu's Vow gives an extra grenade charge
          # Rule hunter-exotic-armor-shinobus-vow-5: Always ➜ Shinobu's Vow equipped ➜ Extra grenade charge (+1)
          Given the build "shinobus-vow-skip-grenade" is imported, with no steps
          When I tap "What this build does" above the loop
          Then its "Always" line shows "+1 grenade charge [Shinobu's Vow]"
        """)]
    public async Task ShinobusVowGivesAnExtraGrenadeCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("shinobus-vow-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();

        // Then
        var explained = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(explained).Shows("Always", "+1 grenade charge [Shinobu's Vow]");
    }
}
