using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>Radiolaria Transposer's rules (rules/weapons/perks.yaml), one scenario per rule claim.</summary>
public sealed class RadiolariaTransposerTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Radiolaria Transposer's 3 quick kills explode and leave a pool
          # Rule weapons-perks-radiolaria-transposer-1: This weapon 3+ kills within 8s ➜ Radiolaria Transposer equipped ➜ Extra strike and Extra strike
          Given the build "radiolaria-transposer-speleologist" is imported, with no steps
          When I tap "Speleologist" in the trigger panel
          And I select "3 in 8 s · Radiolaria Transposer"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Speleologist (💀 kill 3)"
          And it reads "Speleologist 💀 killed 3 enemies in 8 s"
          # Two strikes: the explosion, then the pool.
          And "Radiolaria Transposer" fires "heavy weapon strike, Arc damage" and "heavy weapon strike, Arc damage"
        """)]
    public async Task RadiolariaTransposers3QuickKillsExplodeAndLeaveAPool(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("radiolaria-transposer-speleologist.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 8 s · Radiolaria Transposer");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Two strikes: the explosion, then the pool.
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Speleologist (💀 kill 3)")
            .Reads("Speleologist 💀 killed 3 enemies in 8 s")
            .Fires(new WeaponPerk.RadiolariaTransposer(), "heavy weapon strike, Arc damage", "heavy weapon strike, Arc damage");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Radiolaria Transposer's fewer kills may explode and leave a pool
          # Rule weapons-perks-radiolaria-transposer-2: This weapon fewer than 3 kills ➜ Radiolaria Transposer equipped ➜ Extra strike and Extra strike (chance)
          Given the build "radiolaria-transposer-speleologist" is imported, with no steps
          When I tap "Speleologist" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Speleologist (💀 kill)"
          And it reads "Speleologist 💀 killed 1 enemy"
          # Two strikes: the explosion, then the pool.
          And "Radiolaria Transposer" fires "heavy weapon strike, Arc damage (chance)" and "heavy weapon strike, Arc damage (chance)"
        """)]
    public async Task RadiolariaTransposersFewerKillsMayExplodeAndLeaveAPool(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("radiolaria-transposer-speleologist.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Two strikes: the explosion, then the pool.
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Speleologist (💀 kill)")
            .Reads("Speleologist 💀 killed 1 enemy")
            .Fires(new WeaponPerk.RadiolariaTransposer(), "heavy weapon strike, Arc damage (chance)", "heavy weapon strike, Arc damage (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Radiolaria Transposer's 3 quick kills explode and leave a pool
          # Rule weapons-perks-radiolaria-transposer-1: This weapon 3+ kills within 8s ➜ Radiolaria Transposer (enhanced) equipped ➜ Extra strike and Extra strike
          Given the build "radiolaria-transposer-enhanced-speleologist" is imported, with no steps
          When I tap "Speleologist" in the trigger panel
          And I select "3 in 8 s · Radiolaria Transposer (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Speleologist (💀 kill 3)"
          And it reads "Speleologist 💀 killed 3 enemies in 8 s"
          # Two strikes: the explosion, then the pool.
          And "Radiolaria Transposer (enhanced)" fires "heavy weapon strike, Arc damage" and "heavy weapon strike, Arc damage"
        """)]
    public async Task EnhancedRadiolariaTransposers3QuickKillsExplodeAndLeaveAPool(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("radiolaria-transposer-enhanced-speleologist.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 8 s · Radiolaria Transposer (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Two strikes: the explosion, then the pool.
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Speleologist (💀 kill 3)")
            .Reads("Speleologist 💀 killed 3 enemies in 8 s")
            .Fires(new WeaponPerk.RadiolariaTransposerEnhanced(), "heavy weapon strike, Arc damage", "heavy weapon strike, Arc damage");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Radiolaria Transposer's fewer kills may explode and leave a pool
          # Rule weapons-perks-radiolaria-transposer-2: This weapon fewer than 3 kills ➜ Radiolaria Transposer (enhanced) equipped ➜ Extra strike and Extra strike (chance)
          Given the build "radiolaria-transposer-enhanced-speleologist" is imported, with no steps
          When I tap "Speleologist" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Speleologist (💀 kill)"
          And it reads "Speleologist 💀 killed 1 enemy"
          # Two strikes: the explosion, then the pool.
          And "Radiolaria Transposer (enhanced)" fires "heavy weapon strike, Arc damage (chance)" and "heavy weapon strike, Arc damage (chance)"
        """)]
    public async Task EnhancedRadiolariaTransposersFewerKillsMayExplodeAndLeaveAPool(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("radiolaria-transposer-enhanced-speleologist.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Speleologist());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Two strikes: the explosion, then the pool.
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Speleologist (💀 kill)")
            .Reads("Speleologist 💀 killed 1 enemy")
            .Fires(new WeaponPerk.RadiolariaTransposerEnhanced(), "heavy weapon strike, Arc damage (chance)", "heavy weapon strike, Arc damage (chance)");
    }
}
