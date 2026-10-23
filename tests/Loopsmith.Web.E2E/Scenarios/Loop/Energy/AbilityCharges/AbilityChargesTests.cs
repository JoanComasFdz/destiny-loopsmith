using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Energy;

/// <summary>An ability with two charges (Shinobu's Vow's grenade, ADRs D48): a throw spends one, and a step needs one full.</summary>
public sealed class AbilityChargesTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: With Shinobu's Vow the grenade has two charges and each throw spends one
          Given the build "energy-shinobus-vow-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          # Shinobu's Vow's refunds go by the Grenade stat, which the build doesn't give: at least the charge left.
          Then card #1's ENERGY reads "Grenade 2 charges → ≥ 1 charge"
          And it has no "NOT READY" badge
          # keeping step #1: #2 throws the charge it left
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #2 has no "NOT READY" badge
          And its ENERGY reads "Grenade ≥ 1 charge → ?"
          # keeping step #2: what its refunds gave back is all #3's grenade has
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #3 has a "MAY NOT BE READY" badge
          And it shows the note "Grenade may not be ready: ? before #3"
        """)]
    public async Task WithShinobusVowTheGrenadeHasTwoChargesAndEachThrowSpendsOne(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("energy-shinobus-vow-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Shinobu's Vow's refunds go by the Grenade stat, which the build doesn't give: at least the charge left.
        var first = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(first)
            .ShowsEnergy(new AbilitySlot.Grenade(), "2 charges", "≥ 1 charge")
            .IsNotFlagged();

        // When
        // keeping step #1: #2 throws the charge it left
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var second = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(second)
            .IsNotFlagged()
            .ShowsEnergy(new AbilitySlot.Grenade(), "≥ 1 charge", "?");

        // When
        // keeping step #2: what its refunds gave back is all #3's grenade has
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var third = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(third)
            .IsFlagged("MAY NOT BE READY")
            .HasNote("Grenade may not be ready: ? before #3");
    }
}
