using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Harmonic Siphon's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class HarmonicSiphonTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Harmonic Siphon's two kills at once with a weapon of your Super's element drop an Orb of Power
          # Rule mods-armor-harmonic-siphon-1: Weapon of your Super's element 2+ kills ➜ Harmonic Siphon equipped ➜ Orb of Power
          Given the build "harmonic-siphon-arc-staff-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "2 in 3 s · Harmonic Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill 2)"
          And it reads "Crisis Inverted 💀 killed 2 enemies in 3 s"
          And "Harmonic Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task HarmonicSiphonsTwoKillsAtOnceWithAWeaponOfYourSupersElementDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("harmonic-siphon-arc-staff-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Harmonic Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill 2)")
            .Reads("Crisis Inverted 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.HarmonicSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Harmonic Siphon's single kill with a weapon of your Super's element may drop an Orb of Power
          # Rule mods-armor-harmonic-siphon-2: Weapon of your Super's element fewer than 2 kills ➜ Harmonic Siphon equipped ➜ Orb of Power (chance)
          Given the build "harmonic-siphon-arc-staff-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill)"
          And it reads "Crisis Inverted 💀 killed 1 enemy"
          And "Harmonic Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task HarmonicSiphonsSingleKillWithAWeaponOfYourSupersElementMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("harmonic-siphon-arc-staff-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill)")
            .Reads("Crisis Inverted 💀 killed 1 enemy")
            .Fires(new ArmorMod.HarmonicSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }
}
