using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Charged Up's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class ChargedUpTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Charged Up lets you hold four Armor Charges before they are at max
          # Rule mods-armor-charged-up-1: Always ➜ Armor Charge (max 4 | 5 | 6)
          Given the build "charged-up-crux-celeritas-strand-siphon" is imported, with no steps
          When I tap "Crux Celeritas IV" in the trigger panel
          And I select "2 in 3 s · Strand Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crux Celeritas IV (💀 kill 2)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 picks up
          When I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          And its BUFFS show only "Armor Charge x1"
          And "On you" in the trigger panel shows "Armor Charge" with "at max (x4)" and "ends"
        """)]
    public async Task ChargedUpLetsYouHoldFourArmorChargesBeforeTheyAreAtMax(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("charged-up-crux-celeritas-strand-siphon.build.yaml");
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
        await Frontend.Designer.TriggerPalette.ClickPickupAsync(new Keyword.OrbOfPower());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var pickedUp = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#2 Pick up Orb of Power")
            .Shows(new StateGroup.Buffs(), "Armor Charge x1");

        var panel = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(panel).ShowsOnYouWith(new Keyword.ArmorCharge(), "at max (x4)", "ends");
    }
}
