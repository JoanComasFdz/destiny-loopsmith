using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.ArmorSets.OryxsMemory;

/// <summary>Oryx's Memory's 2-piece bonus, Iron Sharpens Iron (rules/armor-sets/oryxs-memory.yaml), one scenario per rule claim.</summary>
public sealed class OryxsMemory2pcTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Iron Sharpens Iron's Elite+ kill gives ammo progress
          # Rule armor-sets-oryxs-memory-oryxs-memory-2pc-1: Any kill (elite+) ➜ Iron Sharpens Iron equipped ➜ Ammo progress
          Given the build "oryxs-memory-2pc-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I pick "Elite" as the rank
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill · Elite)"
          And "Iron Sharpens Iron" fires "+12% Special, +7.5% Heavy ammo progress"
        """)]
    public async Task IronSharpensIronsEliteKillGivesAmmoProgress(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("oryxs-memory-2pc-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectRankAsync(new EnemyRank.Elite());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var kill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(kill)
            .HasHeading("#1 Crisis Inverted (💀 kill · Elite)")
            .Fires(new ArmorSetBonus.IronSharpensIron(), "+12% Special, +7.5% Heavy ammo progress");
    }
}
