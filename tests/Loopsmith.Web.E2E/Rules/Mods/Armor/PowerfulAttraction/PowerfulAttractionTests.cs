using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Powerful Attraction's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class PowerfulAttractionTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Powerful Attraction's dodge collects the Orb of Power on the ground
          # Rule mods-armor-powerful-attraction-1: Class ability ➜ Powerful Attraction equipped ➜ collects Orb of Power
          Given the build "powerful-attraction-marksmans-dodge-crux-celeritas-strand-siphon" is imported, with no steps
          When I tap "Crux Celeritas IV" in the trigger panel
          And I select "2 in 3 s · Strand Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crux Celeritas IV (💀 kill 2)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 pulls in
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #2's heading is "#2 Class ability"
          And "Powerful Attraction" fires "collects Orb of Power from the ground"
          And its GROUND shows none
        """)]
    public async Task PowerfulAttractionsDodgeCollectsTheOrbOfPowerOnTheGround(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("powerful-attraction-marksmans-dodge-crux-celeritas-strand-siphon.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CruxCeleritasIv());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CruxCeleritasIv());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Strand Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crux Celeritas IV (💀 kill 2)")
            .Shows(new StateGroup.Ground(), "Orb of Power");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#2 Class ability")
            .Fires(new ArmorMod.PowerfulAttraction(), "collects Orb of Power from the ground")
            .ShowsNone(new StateGroup.Ground());
    }
}
