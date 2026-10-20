using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Reaper's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class ReaperTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Reaper's dodge arms it for 10 seconds
          # Rule mods-armor-reaper-1: Class ability ➜ Reaper equipped ➜ Reaper
          Given the build "reaper-marksmans-dodge" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And "Reaper" fires "Reaper (10s)"
          And its BUFFS show only "Reaper"
        """)]
    public async Task ReapersDodgeArmsItFor10Seconds(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("reaper-marksmans-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Fires(new ArmorMod.Reaper(), "Reaper (10s)")
            .Shows(new StateGroup.Buffs(), "Reaper");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Reaper's armed weapon kill drops an Orb of Power and is consumed
          # Rule mods-armor-reaper-2: Weapon kill ➜ Reaper equipped and Reaper on you ➜ Orb of Power, ends Reaper
          Given the build "reaper-crisis-inverted-marksmans-dodge" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And its BUFFS show only "Reaper"
          # keeping step #1: its Reaper is what #2 spends
          When I tap "Crisis Inverted" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #2's heading is "#2 Crisis Inverted (💀 kill)"
          And "Reaper" fires "consumes Reaper" and "Orb of Power"
          And its BUFFS show none
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task ReapersArmedWeaponKillDropsAnOrbOfPowerAndIsConsumed(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("reaper-crisis-inverted-marksmans-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var armed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(armed)
            .HasHeading("#1 Class ability")
            .Shows(new StateGroup.Buffs(), "Reaper");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Crisis Inverted (💀 kill)")
            .Fires(new ArmorMod.Reaper(), "consumes Reaper", "Orb of Power")
            .ShowsNone(new StateGroup.Buffs())
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }
}
