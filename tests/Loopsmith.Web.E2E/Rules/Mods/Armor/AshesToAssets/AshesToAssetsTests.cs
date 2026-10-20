using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Ashes to Assets's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class AshesToAssetsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Ashes to Assets' grenade kill gives Super energy of unknown amount
          # Rule mods-armor-ashes-to-assets-1: Grenade kill ➜ Ashes to Assets equipped ➜ Super energy ?
          Given the build "ashes-to-assets-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Grenade (💀 kill)"
          And "Ashes to Assets" fires "+?% super energy"
        """)]
    public async Task AshesToAssetsGrenadeKillGivesSuperEnergyOfUnknownAmount(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("ashes-to-assets-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Grenade (💀 kill)")
            .Fires(new ArmorMod.AshesToAssets(), "+?% super energy");
    }
}
