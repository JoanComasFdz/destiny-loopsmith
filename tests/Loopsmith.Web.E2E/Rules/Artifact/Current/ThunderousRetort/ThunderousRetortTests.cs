using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Artifact.Current;

/// <summary>Thunderous Retort's rules (rules/artifact/current.yaml), one scenario per rule claim.</summary>
public sealed class ThunderousRetortTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Thunderous Retort's Super at critical health hits harder
          # Rule artifact-current-thunderous-retort-1: Always ➜ Thunderous Retort equipped and Critical health on you ➜ +30% Super damage
          Given the build "thunderous-retort-arc-staff" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #1's heading is "#1 Critical health"
          And its BUFFS show only "Critical health"
          # keeping step #1: its critical health is what makes #2's Super harder
          When I tap "Arc Staff" in the trigger panel
          And I tap "🥊 hit"
          And I tap "▸ DAMAGE BONUS" on card #2
          Then card #2's heading is "#2 Super (🥊 hit)"
          And its "Super" damage bonus reads "+30%"
          And "Thunderous Retort" gives "+30%" of it
        """)]
    public async Task ThunderousRetortsSuperAtCriticalHealthHitsHarder(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("thunderous-retort-arc-staff.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var critical = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#1 Critical health")
            .Shows(new StateGroup.Buffs(), "Critical health");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);
        await Frontend.Designer.Loop.Step(2).ClickDamageBonusAsync();
        await Frontend.Designer.Loop.Step(2).WaitUntilDamageBonusSourcesShownAsync();

        // Then
        var super = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(super)
            .HasHeading("#2 Super (🥊 hit)")
            .HasDamageBonus("Super", "+30%")
            .HasDamageBonusFrom(new ArtifactPerk.ThunderousRetort(), "+30%");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Thunderous Retort's Super while Amplified hits harder
          # Rule artifact-current-thunderous-retort-2: Always ➜ Thunderous Retort equipped, Amplified on you and no Critical health ➜ +30% Super damage
          Given the build "thunderous-retort-disorienting-blow-arc-staff" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Melee (🥊 hit)"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Amplified is what makes #2's Super harder
          When I tap "Arc Staff" in the trigger panel
          And I tap "🥊 hit"
          And I tap "▸ DAMAGE BONUS" on card #2
          Then card #2's heading is "#2 Super (🥊 hit)"
          And its "Super" damage bonus reads "+30%"
          And "Thunderous Retort" gives "+30%" of it
        """)]
    public async Task ThunderousRetortsSuperWhileAmplifiedHitsHarder(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("thunderous-retort-disorienting-blow-arc-staff.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var amplified = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(amplified)
            .HasHeading("#1 Melee (🥊 hit)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);
        await Frontend.Designer.Loop.Step(2).ClickDamageBonusAsync();
        await Frontend.Designer.Loop.Step(2).WaitUntilDamageBonusSourcesShownAsync();

        // Then
        var super = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(super)
            .HasHeading("#2 Super (🥊 hit)")
            .HasDamageBonus("Super", "+30%")
            .HasDamageBonusFrom(new ArtifactPerk.ThunderousRetort(), "+30%");
    }
}
