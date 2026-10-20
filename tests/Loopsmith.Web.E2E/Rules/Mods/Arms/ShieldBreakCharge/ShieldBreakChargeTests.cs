using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Arms;

/// <summary>Shield Break Charge's rules (rules/mods/arms.yaml), one scenario per rule claim.</summary>
public sealed class ShieldBreakChargeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Shield Break Charge's broken shield gives Armor Charge
          # Rule mods-arms-shield-break-charge-1: Shield broken ➜ Shield Break Charge equipped ➜ Armor Charge (×1 | ×2)
          Given the build "shield-break-charge-crisis-inverted-alone" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I tick "🛡 shield"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Crisis Inverted (🥊 hit · 🛡 shield)"
          And it reads "Crisis Inverted 🛡 broke a shield"
          And "Shield Break Charge" fires "+1 Armor Charge"
          And its BUFFS show only "Armor Charge x1"
        """)]
    public async Task ShieldBreakChargesBrokenShieldGivesArmorCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("shield-break-charge-crisis-inverted-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.TickOptionAsync(new RowOption.Shield());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var broke = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(broke)
            .HasHeading("#1 Crisis Inverted (🥊 hit · 🛡 shield)")
            .Reads("Crisis Inverted 🛡 broke a shield")
            .Fires(new ArmorMod.ShieldBreakCharge(), "+1 Armor Charge")
            .Shows(new StateGroup.Buffs(), "Armor Charge x1");
    }
}
