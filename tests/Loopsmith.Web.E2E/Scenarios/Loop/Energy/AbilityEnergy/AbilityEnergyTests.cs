using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Energy;

/// <summary>
/// Each step card's ENERGY (ADRs D48): the grenade's, melee's and class ability's energy before the step and after it; a
/// throw spends the grenade, a refund fills it by its chunk scalar at 70 in its stat, and a step whose grenade isn't back
/// is flagged.
/// </summary>
public sealed class AbilityEnergyTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Throwing a grenade spends it and the card shows each ability's energy before and after
          Given the build "energy-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #1's ENERGY reads "Grenade 100% → 0%"
          And its ENERGY reads "Melee 100% → 100%"
          And its ENERGY reads "Class ability 100% → 100%"
          And it has no "NOT READY" badge
          # keeping step #1: its throw spent the grenade #2 throws again
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #2 has a "NOT READY" badge
          And it shows the note "Grenade not ready: 0% before #2"
          And its ENERGY reads "Grenade 0% → 0%"
          And its ENERGY row fits inside the screen, with no sideways scroll
        """)]
    public async Task ThrowingAGrenadeSpendsItAndTheCardShowsEachAbilitysEnergyBeforeAndAfter(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("energy-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var thrown = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(thrown)
            .ShowsEnergy(new AbilitySlot.Grenade(), "100%", "0%")
            .ShowsEnergy(new AbilitySlot.Melee(), "100%", "100%")
            .ShowsEnergy(new AbilitySlot.ClassAbility(), "100%", "100%")
            .IsNotFlagged();

        // When
        // keeping step #1: its throw spent the grenade #2 throws again
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var again = await Frontend.Designer.Loop.Step(2).ReadAsync();
        var layout = await Frontend.Designer.Loop.Step(2).ReadEnergyLayoutAsync();
        Asserting.That(again)
            .IsFlagged("NOT READY")
            .HasNote("Grenade not ready: 0% before #2")
            .ShowsEnergy(new AbilitySlot.Grenade(), "0%", "0%");
        Asserting.That(layout).FitsInsideTheScreen();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: At 70 Grenade an Ionic Trace refills the grenade by its chunk scalar
          Given the build "energy-skip-grenade-spark-of-ions-70-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          # 11.25% × Skip Grenade's 0.75 chunk scalar: exactly that at 70 Grenade.
          Then card #1's ENERGY reads "Grenade 100% → 8.44%"
          # keeping step #1: its trace's energy is all #2's grenade has
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #2 has a "NOT READY" badge
          And it shows the note "Grenade not ready: 8.44% before #2"
        """)]
    public async Task At70GrenadeAnIonicTraceRefillsTheGrenadeByItsChunkScalar(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("energy-skip-grenade-spark-of-ions-70-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // 11.25% × Skip Grenade's 0.75 chunk scalar: exactly that at 70 Grenade.
        var thrown = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(thrown).ShowsEnergy(new AbilitySlot.Grenade(), "100%", "8.44%");

        // When
        // keeping step #1: its trace's energy is all #2's grenade has
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var again = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(again)
            .IsFlagged("NOT READY")
            .HasNote("Grenade not ready: 8.44% before #2");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Without a Grenade stat an Ionic Trace's refill is unknown
          Given the build "energy-skip-grenade-spark-of-ions" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          # A refund goes by the Grenade stat (its listed value at 70), and the build gives none: "?", never 0.
          Then card #1's ENERGY reads "Grenade 100% → ?"
          # keeping step #1: its trace's energy is all #2's grenade has
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #2 has a "MAY NOT BE READY" badge
          And it shows the note "Grenade may not be ready: ? before #2"
        """)]
    public async Task WithoutAGrenadeStatAnIonicTracesRefillIsUnknown(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("energy-skip-grenade-spark-of-ions.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // A refund goes by the Grenade stat (its listed value at 70), and the build gives none: "?", never 0.
        var thrown = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(thrown).ShowsEnergy(new AbilitySlot.Grenade(), "100%", "?");

        // When
        // keeping step #1: its trace's energy is all #2's grenade has
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var again = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(again)
            .IsFlagged("MAY NOT BE READY")
            .HasNote("Grenade may not be ready: ? before #2");
    }
}
