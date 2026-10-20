using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Artifact.Current;

/// <summary>Horde Shuttle's rules (rules/artifact/current.yaml), one scenario per rule claim.</summary>
public sealed class HordeShuttleTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Horde Shuttle spawns a Threadling from an unraveled target kept under fire
          # Rule artifact-current-horde-shuttle-1: Weapon sustained hit ➜ Horde Shuttle equipped and Unravel on the target ➜ Threadling
          Given the build "horde-shuttle-with-unraveling-orbs-and-attrition-orbs" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 picks up
          When I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          And its WEAPONS show only "Unraveling Rounds"
          # keeping step #2: its Unraveling Rounds are what unravel #3's target
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Festival Flight (🥊 hit)"
          And its DEBUFFS show only "Unravel"
          # keeping step #3: its Unravel on the target is what #4 keeps shooting
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #4's heading is "#4 Festival Flight (🥊 hit)"
          And it reads "Festival Flight 🥊 hit on Unraveled target"
          And on "Festival Flight 🥊 hit on Unraveled target" "Horde Shuttle" fires "Threadling"
        """)]
    public async Task HordeShuttleSpawnsAThreadlingFromAnUnraveledTargetKeptUnderFire(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("horde-shuttle-with-unraveling-orbs-and-attrition-orbs.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dropped = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dropped)
            .HasHeading("#1 Festival Flight (🥊 hit)")
            .Shows(new StateGroup.Ground(), "Orb of Power");

        // When
        await Frontend.Designer.TriggerPalette.ClickPickupAsync(new Keyword.OrbOfPower());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var pickedUp = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#2 Pick up Orb of Power")
            .Shows(new StateGroup.Weapons(), "Unraveling Rounds");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var unraveled = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(unraveled)
            .HasHeading("#3 Festival Flight (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Unravel");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(4);

        // Then
        var spawned = await Frontend.Designer.Loop.Step(4).ReadAsync();
        Asserting.That(spawned)
            .HasHeading("#4 Festival Flight (🥊 hit)")
            .Reads("Festival Flight 🥊 hit on Unraveled target")
            .FiresOn("Festival Flight 🥊 hit on Unraveled target", new ArtifactPerk.HordeShuttle(), "Threadling");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Horde Shuttle's Threadlings sever what they hit
          # Rule artifact-current-horde-shuttle-2: Threadling hit ➜ Horde Shuttle equipped ➜ Sever
          Given the build "horde-shuttle-hatchling-on-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · Hatchling"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill 3)"
          And "Hatchling" fires "Threadling"
          And on "↳ Threadling 🥊 hit" "Horde Shuttle" fires "Sever target (10s)"
          And its DEBUFFS show only "Sever"
        """)]
    public async Task HordeShuttlesThreadlingsSeverWhatTheyHit(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("horde-shuttle-hatchling-on-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Hatchling");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill 3)")
            .Fires(new WeaponPerk.Hatchling(), "Threadling")
            .FiresOn("↳ Threadling 🥊 hit", new ArtifactPerk.HordeShuttle(), "Sever target (10s)")
            .Shows(new StateGroup.Debuffs(), "Sever");
    }
}
