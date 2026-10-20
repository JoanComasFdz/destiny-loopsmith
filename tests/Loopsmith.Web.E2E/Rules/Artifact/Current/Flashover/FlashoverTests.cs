using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Artifact.Current;

/// <summary>Flashover's rules (rules/artifact/current.yaml), one scenario per rule claim.</summary>
public sealed class FlashoverTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Flashover's Bolt Charge at max makes you Amplified
          # Rule artifact-current-flashover-1: Bolt Charge at max ➜ Flashover equipped ➜ Amplified (15s)
          Given the build "flashover-arc-staff-kills" is imported, with no steps
          When I tap "Arc Staff" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Super (💀 kill)"
          And its BUFFS show only "Super · 1 effect" and "Bolt Charge"
          # keeping step #1: its Bolt Charge is what #2 declares at max
          When I tap "at max (x10)" on "Bolt Charge" under "On you" in the trigger panel
          Then card #2's heading is "#2 Bolt Charge at max (x10)"
          And "Flashover" fires "Amplified (15s)"
          And its BUFFS show only "Super · 1 effect", "Bolt Charge (at max)" and "Amplified"
        """)]
    public async Task FlashoversBoltChargeAtMaxMakesYouAmplified(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("flashover-arc-staff-kills.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(charged)
            .HasHeading("#1 Super (💀 kill)")
            .Shows(new StateGroup.Buffs(), "Super · 1 effect", "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.BoltCharge(), "at max (x10)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var maxed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(maxed)
            .HasHeading("#2 Bolt Charge at max (x10)")
            .Fires(new ArtifactPerk.Flashover(), "Amplified (15s)")
            .Shows(new StateGroup.Buffs(), "Super · 1 effect", "Bolt Charge (at max)", "Amplified");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Flashover's Bolt Charge strikes always hit harder
          # Rule artifact-current-flashover-2: Always ➜ Flashover equipped ➜ Damage bonus (+50% vs Bolt Charge)
          Given the build "flashover-arc-staff-kills" is imported, with no steps
          When I tap "Arc Staff" in the trigger panel
          And I tap "💀 kill"
          And I tap "▸ DAMAGE BONUS" on card #1
          Then card #1's heading is "#1 Super (💀 kill)"
          And its "Bolt Charge" damage bonus reads "+50%"
          And "Flashover" gives "+50%" of it
        """)]
    public async Task FlashoversBoltChargeStrikesAlwaysHitHarder(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("flashover-arc-staff-kills.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.Loop.Step(1).ClickDamageBonusAsync();
        await Frontend.Designer.Loop.Step(1).WaitUntilDamageBonusSourcesShownAsync();

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Super (💀 kill)")
            .HasDamageBonus("Bolt Charge", "+50%")
            .HasDamageBonusFrom(new ArtifactPerk.Flashover(), "+50%");
    }
}
