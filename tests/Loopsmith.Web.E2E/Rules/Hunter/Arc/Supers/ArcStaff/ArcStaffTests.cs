using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.Arc.Supers;

/// <summary>Arc Staff's rules and passive (rules/hunter/arc/supers.yaml), one scenario per rule claim.</summary>
public sealed class ArcStaffTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc Staff's kill gives Bolt Charge
          # Rule hunter-arc-supers-arc-staff-1: Super kill ➜ Arc Staff equipped ➜ Bolt Charge
          Given the build "arc-staff" is imported, with no steps
          When I tap "Arc Staff" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Super (💀 kill)"
          # A super's first kill casts it: "Super cast" comes before the kill.
          And it reads "Super cast"
          And "Arc Staff" fires "+1 Bolt Charge"
        """)]
    public async Task ArcStaffsKillGivesBoltCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-staff.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // A super's first kill casts it: "Super cast" comes before the kill.
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Super (💀 kill)")
            .Reads("Super cast")
            .Fires(new Ability.ArcStaff(), "+1 Bolt Charge");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc Staff gives damage resistance while its Super is on
          # Rule hunter-arc-supers-arc-staff-2: Always ➜ Arc Staff equipped and Super on you ➜ Damage resistance (90% [PvP 53%])
          Given the build "arc-staff" is imported, with no steps
          When I tap "Arc Staff" in the trigger panel
          And I tap "🥊 hit"
          And I tap "▸ DAMAGE RESISTANCE" on card #1
          Then card #1's heading is "#1 Super (🥊 hit)"
          # A super's first hit casts it: Super is on you from then on.
          And its BUFFS show only "Super · 1 effect"
          And its "All damage" resistance reads "93.45%" PvE and "53%" PvP
          And "Arc Staff" gives "90% · PvP 53%" of it
        """)]
    public async Task ArcStaffGivesDamageResistanceWhileItsSuperIsOn(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-staff.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.Loop.Step(1).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(1).WaitUntilResistanceSourcesShownAsync();

        // Then
        // A super's first hit casts it: Super is on you from then on.
        var cast = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(cast)
            .HasHeading("#1 Super (🥊 hit)")
            .Shows(new StateGroup.Buffs(), "Super · 1 effect")
            .HasResistance("All damage", "93.45%", "53%")
            .HasResistanceFrom(new Ability.ArcStaff(), "90% · PvP 53%");
    }
}
