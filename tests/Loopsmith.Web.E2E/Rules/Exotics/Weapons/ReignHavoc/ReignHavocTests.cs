using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Exotics.Weapons;

/// <summary>Reign Havoc's rules (rules/exotics/weapons.yaml): Thunderlord's intrinsic, one scenario per rule claim.</summary>
public sealed class ReignHavocTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Reign Havoc's kill strike gives Bolt Charge
          # Rule exotics-weapons-reign-havoc-1: Power weapon kill ➜ Reign Havoc equipped ➜ Bolt Charge
          Given the build "reign-havoc-thunderlord" is imported, with no steps
          When I tap "Thunderlord" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Thunderlord (💀 kill)"
          And "Reign Havoc" fires "+1 Bolt Charge"
          And its BUFFS show only "Bolt Charge"
        """)]
    public async Task ReignHavocsKillStrikeGivesBoltCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("reign-havoc-thunderlord.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Thunderlord());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Thunderlord());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var kill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(kill)
            .HasHeading("#1 Thunderlord (💀 kill)")
            .Fires(new ExoticWeaponPerk.ReignHavoc(), "+1 Bolt Charge")
            .Shows(new StateGroup.Buffs(), "Bolt Charge");
    }
}
