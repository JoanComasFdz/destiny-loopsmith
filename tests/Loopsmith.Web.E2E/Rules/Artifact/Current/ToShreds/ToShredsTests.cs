using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Artifact.Current;

/// <summary>To Shreds's rules (rules/artifact/current.yaml), one scenario per rule claim.</summary>
public sealed class ToShredsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: To Shreds unravels a severed target kept under fire
          # Rule artifact-current-to-shreds-1: Weapon sustained hit ➜ To Shreds equipped and Sever on the target ➜ Unravel
          Given the build "to-shreds-marksmans-dodge-slice" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its WEAPONS show only "Slice · Festival Flight"
          # keeping step #1: its Slice is what severs #2's target
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Festival Flight (🥊 hit)"
          And its DEBUFFS show only "Sever"
          # keeping step #2: its Sever on the target is what #3 keeps shooting
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Festival Flight (🥊 hit)"
          And it reads "Festival Flight 🥊 hit on Severed target"
          And "To Shreds" fires "Unravel target"
          And its DEBUFFS show only "Sever" and "Unravel"
        """)]
    public async Task ToShredsUnravelsASeveredTargetKeptUnderFire(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("to-shreds-marksmans-dodge-slice.build.yaml");
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
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var unraveled = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(unraveled)
            .HasHeading("#3 Festival Flight (🥊 hit)")
            .Reads("Festival Flight 🥊 hit on Severed target")
            .Fires(new ArtifactPerk.ToShreds(), "Unravel target")
            .Shows(new StateGroup.Debuffs(), "Sever", "Unravel");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: To Shreds heals and weaves mail when a severed target dies
          # Rule artifact-current-to-shreds-2: Any kill ➜ To Shreds equipped and Sever on the target ➜ Health (3 × 15 HP + allies), Woven Mail (10s)
          Given the build "to-shreds-marksmans-dodge-slice" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its WEAPONS show only "Slice · Festival Flight"
          # keeping step #1: its Slice is what severs #2's target
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Festival Flight (🥊 hit)"
          And its DEBUFFS show only "Sever"
          # keeping step #2: its Sever on the target is what #3 kills
          When I tap "Festival Flight" in the trigger panel
          And I tap "💀 kill"
          Then card #3's heading is "#3 Festival Flight (💀 kill)"
          And "To Shreds" fires "Woven Mail (10s)" and "heals you and allies 3 × 15 HP"
          And its BUFFS show only "Woven Mail"
        """)]
    public async Task ToShredsHealsAndWeavesMailWhenASeveredTargetDies(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("to-shreds-marksmans-dodge-slice.build.yaml");
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
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var killed = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#3 Festival Flight (💀 kill)")
            .Fires(new ArtifactPerk.ToShreds(), "Woven Mail (10s)", "heals you and allies 3 × 15 HP")
            .Shows(new StateGroup.Buffs(), "Woven Mail");
    }
}
