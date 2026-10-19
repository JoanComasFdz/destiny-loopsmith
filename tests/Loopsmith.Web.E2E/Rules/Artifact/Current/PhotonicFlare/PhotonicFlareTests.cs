using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Artifact.Current;

/// <summary>Photonic Flare's rules (rules/artifact/current.yaml), one scenario per rule claim.</summary>
public sealed class PhotonicFlareTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Photonic Flare's Arc kill of a severed target blinds
          # Rule artifact-current-photonic-flare-1: Arc damage kill ➜ Photonic Flare equipped and Sever on the target ➜ Blind
          Given the build "photonic-flare-marksmans-dodge-slice-skip-grenade" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its WEAPONS show only "Slice · Festival Flight"
          # keeping step #1: its Slice is what severs #2's target
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Festival Flight (🥊 hit)"
          And its DEBUFFS show only "Sever"
          # keeping step #2: its Sever on the target is what #3 kills with Arc damage
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #3's heading is "#3 Grenade (💀 kill)"
          And it reads "Grenade 💀 kill on Severed target"
          And "Photonic Flare" fires "Blind target (10s)"
          And its DEBUFFS show only "Sever" and "Blind"
        """)]
    public async Task PhotonicFlaresArcKillOfASeveredTargetBlinds(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("photonic-flare-marksmans-dodge-slice-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Shows(new StateGroup.Weapons(), "Slice · Festival Flight");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var severed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(severed)
            .HasHeading("#2 Festival Flight (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Sever");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var killed = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#3 Grenade (💀 kill)")
            .Reads("Grenade 💀 kill on Severed target")
            .Fires(new ArtifactPerk.PhotonicFlare(), "Blind target (10s)")
            .Shows(new StateGroup.Debuffs(), "Sever", "Blind");
    }
}
