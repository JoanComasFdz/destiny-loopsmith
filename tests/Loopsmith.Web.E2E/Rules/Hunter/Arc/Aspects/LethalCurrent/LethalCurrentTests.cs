using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.Arc.Aspects;

/// <summary>Lethal Current's rules (rules/hunter/arc/aspects.yaml), one scenario per rule claim.</summary>
public sealed class LethalCurrentTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Lethal Current's dodge arms the next melee hit
          # Rule hunter-arc-aspects-lethal-current-2: Class ability ➜ Lethal Current equipped ➜ Lethal Current
          Given the build "lethal-current-marksmans-dodge" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And it reads "Class ability"
          And "Lethal Current" fires "Lethal Current (10s)"
          And its BUFFS show only "Lethal Current"
        """)]
    public async Task LethalCurrentsDodgeArmsTheNextMeleeHit(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("lethal-current-marksmans-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Reads("Class ability")
            .Fires(new Aspect.LethalCurrent(), "Lethal Current (10s)")
            .Shows(new StateGroup.Buffs(), "Lethal Current");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Lethal Current's melee hit blinds a jolted target
          # Rule hunter-arc-aspects-lethal-current-1: Melee hit ➜ Lethal Current equipped and Jolt on the target ➜ Blind
          Given the build "lethal-current-spark-of-shock" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And its DEBUFFS show only "Jolt"
          # keeping step #1: its Jolt on the target is what #2 punches
          When I tap "Combination Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Melee (🥊 hit)"
          And it reads "Melee 🥊 hit on Jolted target"
          And "Lethal Current" fires "Blind target (10s)"
          And its DEBUFFS show only "Jolt" and "Blind"
        """)]
    public async Task LethalCurrentsMeleeHitBlindsAJoltedTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("lethal-current-spark-of-shock.build.yaml");
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
            .Fires(new Aspect.LethalCurrent(), "Blind target (10s)")
            .Shows(new StateGroup.Debuffs(), "Jolt", "Blind");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Lethal Current's armed melee hit jolts, strikes and is consumed
          # Rule hunter-arc-aspects-lethal-current-3: Melee hit ➜ Lethal Current equipped and Lethal Current on you ➜ Lethal Current strike, Jolt, ends Lethal Current
          Given the build "lethal-current-marksmans-dodge-combination-blow" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its BUFFS show only "Lethal Current"
          # keeping step #1: its Lethal Current is what #2 spends
          When I tap "Combination Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Melee (🥊 hit)"
          And it reads "Melee 🥊 hit"
          And "Lethal Current" fires "Jolt target (10s)", "consumes Lethal Current" and "Lethal Current strike"
          And its BUFFS show none
          And its DEBUFFS show only "Jolt"
        """)]
    public async Task LethalCurrentsArmedMeleeHitJoltsStrikesAndIsConsumed(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("lethal-current-marksmans-dodge-combination-blow.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var armed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(armed)
            .HasHeading("#1 Class ability")
            .Shows(new StateGroup.Buffs(), "Lethal Current");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var punched = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(punched)
            .HasHeading("#2 Melee (🥊 hit)")
            .Reads("Melee 🥊 hit")
            .Fires(new Aspect.LethalCurrent(), "Jolt target (10s)", "consumes Lethal Current", "Lethal Current strike")
            .ShowsNone(new StateGroup.Buffs())
            .Shows(new StateGroup.Debuffs(), "Jolt");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Lethal Current's armed sliding melee hit jolts, strikes and is consumed
          # Rule hunter-arc-aspects-lethal-current-3: Sliding melee hit ➜ Lethal Current equipped and Lethal Current on you ➜ Lethal Current strike, Jolt, ends Lethal Current
          Given the build "lethal-current-tempest-strike-marksmans-dodge" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its BUFFS show only "Lethal Current"
          # keeping step #1: its Lethal Current is what #2 spends
          When I tap "SLIDE" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Sliding melee (🥊 hit)"
          And it reads "Sliding melee 🥊 hit"
          And "Lethal Current" fires "Jolt target (10s)", "consumes Lethal Current" and "Lethal Current strike"
          And its BUFFS show none
          And its DEBUFFS show only "Jolt"
        """)]
    public async Task LethalCurrentsArmedSlidingMeleeHitJoltsStrikesAndIsConsumed(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("lethal-current-tempest-strike-marksmans-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var armed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(armed)
            .HasHeading("#1 Class ability")
            .Shows(new StateGroup.Buffs(), "Lethal Current");

        // When
        await Frontend.Designer.TriggerPalette.ClickSecondaryAsync(new SecondaryUse.Slide());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Sliding melee · Tempest Strike");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var slid = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(slid)
            .HasHeading("#2 Sliding melee (🥊 hit)")
            .Reads("Sliding melee 🥊 hit")
            .Fires(new Aspect.LethalCurrent(), "Jolt target (10s)", "consumes Lethal Current", "Lethal Current strike")
            .ShowsNone(new StateGroup.Buffs())
            .Shows(new StateGroup.Debuffs(), "Jolt");
    }
}
