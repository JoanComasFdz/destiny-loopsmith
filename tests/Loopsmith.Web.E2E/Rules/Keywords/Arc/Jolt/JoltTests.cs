using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Keywords.Arc;

/// <summary>Jolt's rules (rules/keywords/arc.yaml), one scenario per rule claim.</summary>
public sealed class JoltTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Jolt chains lightning when a weapon hits a jolted target
          # Rule keywords-arc-jolt-1: Weapon hit ➜ Jolt on the target ➜ Jolt strike
          Given the build "jolt-spark-of-shock-festival-flight" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And its DEBUFFS show only "Jolt"
          # keeping step #1: its Jolt on the target is what #2 shoots
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Festival Flight (🥊 hit)"
          And it reads "Festival Flight 🥊 hit on Jolted target"
          And "Jolt" fires "Jolt strike"
        """)]
    public async Task JoltChainsLightningWhenAWeaponHitsAJoltedTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("jolt-spark-of-shock-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var jolted = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(jolted)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Jolt");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var shot = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(shot)
            .HasHeading("#2 Festival Flight (🥊 hit)")
            .Reads("Festival Flight 🥊 hit on Jolted target")
            .Fires(new Keyword.Jolt(), "Jolt strike");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Jolt chains lightning when a grenade hits a jolted target
          # Rule keywords-arc-jolt-2: Grenade hit ➜ Jolt on the target ➜ Jolt strike
          Given the build "jolt-spark-of-shock" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And its DEBUFFS show only "Jolt"
          # keeping step #1: its Jolt on the target is what #2 hits
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Grenade (🥊 hit)"
          And it reads "Grenade 🥊 hit on Jolted target"
          And "Jolt" fires "Jolt strike"
        """)]
    public async Task JoltChainsLightningWhenAGrenadeHitsAJoltedTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("jolt-spark-of-shock.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var jolted = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(jolted)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Jolt");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#2 Grenade (🥊 hit)")
            .Reads("Grenade 🥊 hit on Jolted target")
            .Fires(new Keyword.Jolt(), "Jolt strike");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Jolt chains lightning when a melee hits a jolted target
          # Rule keywords-arc-jolt-2: Melee hit ➜ Jolt on the target ➜ Jolt strike
          Given the build "jolt-spark-of-shock-combination-blow" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And its DEBUFFS show only "Jolt"
          # keeping step #1: its Jolt on the target is what #2 punches
          When I tap "Combination Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Melee (🥊 hit)"
          And it reads "Melee 🥊 hit on Jolted target"
          And "Jolt" fires "Jolt strike"
        """)]
    public async Task JoltChainsLightningWhenAMeleeHitsAJoltedTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("jolt-spark-of-shock-combination-blow.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var jolted = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(jolted)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Jolt");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var punched = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(punched)
            .HasHeading("#2 Melee (🥊 hit)")
            .Reads("Melee 🥊 hit on Jolted target")
            .Fires(new Keyword.Jolt(), "Jolt strike");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Jolt chains lightning when a sliding melee hits a jolted target
          # Rule keywords-arc-jolt-2: Sliding melee hit ➜ Jolt on the target ➜ Jolt strike
          Given the build "jolt-tempest-strike" is imported, with no steps
          When I tap "SLIDE" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Sliding melee (🥊 hit)"
          And its DEBUFFS show only "Jolt"
          # keeping step #1: its Jolt on the target is what #2 slides into
          When I tap "SLIDE" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Sliding melee (🥊 hit)"
          And it reads "Sliding melee 🥊 hit on Jolted target"
          And "Jolt" fires "Jolt strike"
        """)]
    public async Task JoltChainsLightningWhenASlidingMeleeHitsAJoltedTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("jolt-tempest-strike.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickSecondaryAsync(new SecondaryUse.Slide());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Sliding melee · Tempest Strike");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var jolted = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(jolted)
            .HasHeading("#1 Sliding melee (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Jolt");

        // When
        await Frontend.Designer.TriggerPalette.ClickSecondaryAsync(new SecondaryUse.Slide());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Sliding melee · Tempest Strike");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var slid = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(slid)
            .HasHeading("#2 Sliding melee (🥊 hit)")
            .Reads("Sliding melee 🥊 hit on Jolted target")
            .Fires(new Keyword.Jolt(), "Jolt strike");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Jolt chains lightning when a Super hits a jolted target
          # Rule keywords-arc-jolt-2: Super hit ➜ Jolt on the target ➜ Jolt strike
          Given the build "jolt-gathering-storm" is imported, with no steps
          When I tap "Gathering Storm" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Super (🥊 hit)"
          And its DEBUFFS show only "Jolt"
          # keeping step #1: its Jolt on the target is what #2 hits
          When I tap "Gathering Storm" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Super (🥊 hit)"
          And "Jolt" fires "Jolt strike"
        """)]
    public async Task JoltChainsLightningWhenASuperHitsAJoltedTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("jolt-gathering-storm.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.GatheringStorm());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.GatheringStorm());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var jolted = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(jolted)
            .HasHeading("#1 Super (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Jolt");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.GatheringStorm());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.GatheringStorm());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#2 Super (🥊 hit)")
            .Fires(new Keyword.Jolt(), "Jolt strike");
    }
}
