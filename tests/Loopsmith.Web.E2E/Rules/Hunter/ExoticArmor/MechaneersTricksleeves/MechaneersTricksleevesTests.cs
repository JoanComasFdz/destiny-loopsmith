using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.ExoticArmor;

/// <summary>Mechaneer's Tricksleeves's rules (rules/hunter/exotic-armor.yaml), one scenario per rule claim.</summary>
public sealed class MechaneersTricksleevesTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Mechaneer's Tricksleeves's sidearm ready at critical health gives Spring-Loaded Mounting
          # Rule hunter-exotic-armor-mechaneers-tricksleeves-2: Ready Sidearm ➜ Mechaneer's Tricksleeves equipped and Critical health on you ➜ Spring-Loaded Mounting
          Given the build "mechaneers-tricksleeves-buzzard" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #1's heading is "#1 Critical health"
          # keeping step #1: its critical health is what #2's ready needs
          When I tap "Buzzard" in the trigger panel
          And I tap "✋ ready"
          Then card #2's heading is "#2 Ready Buzzard"
          And "Mechaneer's Tricksleeves" fires "Spring-Loaded Mounting"
          And its BUFFS show only "Critical health" and "Spring-Loaded Mounting · 1 effect"
        """)]
    public async Task MechaneersTricksleevessSidearmReadyAtCriticalHealthGivesSpringLoadedMounting(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("mechaneers-tricksleeves-buzzard.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var critical = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(critical).HasHeading("#1 Critical health");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Buzzard());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Buzzard());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Ready());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var readied = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(readied)
            .HasHeading("#2 Ready Buzzard")
            .Fires(new ArmorPiece.MechaneersTricksleeves(), "Spring-Loaded Mounting")
            .Shows(new StateGroup.Buffs(), "Critical health", "Spring-Loaded Mounting · 1 effect");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Mechaneer's Tricksleeves's sidearm ready without critical health gives nothing
          # Rule hunter-exotic-armor-mechaneers-tricksleeves-2: Ready Sidearm ➜ Mechaneer's Tricksleeves equipped and Critical health on you ➜ Spring-Loaded Mounting
          Given the build "mechaneers-tricksleeves-buzzard" is imported, with no steps
          When I tap "Buzzard" in the trigger panel
          And I tap "✋ ready"
          Then card #1's heading is "#1 Ready Buzzard"
          And "Mechaneer's Tricksleeves" fires nothing
          And its BUFFS show none
        """)]
    public async Task MechaneersTricksleevessSidearmReadyWithoutCriticalHealthGivesNothing(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("mechaneers-tricksleeves-buzzard.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Buzzard());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Buzzard());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Ready());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var readied = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(readied)
            .HasHeading("#1 Ready Buzzard")
            .DoesNotFire(new ArmorPiece.MechaneersTricksleeves())
            .ShowsNone(new StateGroup.Buffs());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Mechaneer's Tricksleeves's ready of a weapon that isn't a sidearm gives nothing
          # Rule hunter-exotic-armor-mechaneers-tricksleeves-2: Ready Sidearm ➜ Mechaneer's Tricksleeves equipped and Critical health on you ➜ Spring-Loaded Mounting
          Given the build "mechaneers-tricksleeves-buzzard" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          And I tap "Thunderlord" in the trigger panel
          # Thunderlord is a machine gun: no rule of the build reacts to readying it, so its ready can't be tapped.
          Then "✋ ready" is disabled
        """)]
    public async Task MechaneersTricksleevessReadyOfAWeaponThatIsntASidearmGivesNothing(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("mechaneers-tricksleeves-buzzard.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Thunderlord());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Thunderlord());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).IsDisabled(new StepTrigger.Ready());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spring-Loaded Mounting gives +100% [PvP 10%] sidearm damage
          # Rule hunter-exotic-armor-mechaneers-tricksleeves-3: Always ➜ Mechaneer's Tricksleeves equipped and Spring-Loaded Mounting on you ➜ Damage bonus (+100% [PvP 10%] vs Sidearm)
          Given the build "mechaneers-tricksleeves-buzzard" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          And I tap "Buzzard" in the trigger panel
          And I tap "✋ ready"
          Then card #2's heading is "#2 Ready Buzzard"
          # keeping steps #1 and #2: Spring-Loaded Mounting on you is what makes the sidearm harder
          When I tap "▸ DAMAGE BONUS" on card #2
          Then its "Sidearm" damage bonus reads "+100% [PvP 10%]"
          And "Mechaneer's Tricksleeves" gives "+100% [PvP 10%]" of it
        """)]
    public async Task SpringLoadedMountingGives100PvP10SidearmDamage(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("mechaneers-tricksleeves-buzzard.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Buzzard());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Buzzard());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Ready());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var readied = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(readied).HasHeading("#2 Ready Buzzard");

        // When
        await Frontend.Designer.Loop.Step(2).ClickDamageBonusAsync();
        await Frontend.Designer.Loop.Step(2).WaitUntilDamageBonusSourcesShownAsync();

        // Then
        var opened = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(opened)
            .HasDamageBonus("Sidearm", "+100% [PvP 10%]")
            .HasDamageBonusFrom(new ArmorPiece.MechaneersTricksleeves(), "+100% [PvP 10%]");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Mechaneer's Tricksleeves gives sidearms +50 Airborne Effectiveness, +100 Handling and +100 Reload Speed
          # Rule hunter-exotic-armor-mechaneers-tricksleeves-1: Always ➜ Mechaneer's Tricksleeves equipped ➜ Weapon stats (airborne-effectiveness +50, handling +100, reload-speed +100)
          Given the build "mechaneers-tricksleeves-buzzard" is imported, with no steps
          When I tap "What this build does" above the loop
          Then under its "Always" heading it shows "Sidearms: +50 Airborne Effectiveness, +100 Handling, +100 Reload Speed [Mechaneer's Tricksleeves]"
        """)]
    public async Task MechaneersTricksleevesGivesSidearms50AirborneEffectiveness100HandlingAnd100ReloadSpeed(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("mechaneers-tricksleeves-buzzard.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();

        // Then
        var explained = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(explained).Shows("Always", "Sidearms: +50 Airborne Effectiveness, +100 Handling, +100 Reload Speed [Mechaneer's Tricksleeves]");
    }
}
