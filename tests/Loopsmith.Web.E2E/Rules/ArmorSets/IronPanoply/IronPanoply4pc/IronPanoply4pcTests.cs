using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.ArmorSets.IronPanoply;

/// <summary>Iron Panoply's 4-piece bonus, Iron Conviction (rules/armor-sets/iron-panoply.yaml), one scenario per rule claim.</summary>
public sealed class IronPanoply4pcTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Iron Conviction's critical health gives its buff
          # Rule armor-sets-iron-panoply-iron-panoply-4pc-1: Critical health ➜ Iron Conviction equipped ➜ Iron Conviction
          Given the build "iron-panoply-4pc-festival-flight" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #1's heading is "#1 Critical health"
          And "Iron Conviction" fires "Iron Conviction (7s)"
          And its BUFFS show only "Critical health" and "Iron Conviction"
        """)]
    public async Task IronConvictionsCriticalHealthGivesItsBuff(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("iron-panoply-4pc-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var critical = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#1 Critical health")
            .Fires(new ArmorSetBonus.IronConviction(), "Iron Conviction (7s)")
            .Shows(new StateGroup.Buffs(), "Critical health", "Iron Conviction");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Iron Conviction's buff resists close-range combatant damage
          # Rule armor-sets-iron-panoply-iron-panoply-4pc-3: Always ➜ Iron Conviction equipped and Iron Conviction on you ➜ Damage resistance (15% vs combatants within 10 m)
          Given the build "iron-panoply-4pc-festival-flight" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          And I tap "▸ DAMAGE RESISTANCE" on card #1
          Then card #1's heading is "#1 Critical health"
          And its BUFFS show only "Critical health" and "Iron Conviction"
          And its "Within 10 m" resistance reads "15%" PvE and "0%" PvP
          And "Iron Conviction" gives "15% · PvP 0%" of it
        """)]
    public async Task IronConvictionsBuffResistsCloseRangeCombatantDamage(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("iron-panoply-4pc-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.Loop.Step(1).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(1).WaitUntilResistanceSourcesShownAsync();

        // Then
        var critical = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#1 Critical health")
            .Shows(new StateGroup.Buffs(), "Critical health", "Iron Conviction")
            .HasResistance("Within 10 m", "15%", "0%")
            .HasResistanceFrom(new ArmorSetBonus.IronConviction(), "15% · PvP 0%");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Iron Conviction's kill refreshes its buff
          # Rule armor-sets-iron-panoply-iron-panoply-4pc-2: Kill ➜ Iron Conviction equipped and Iron Conviction on you ➜ Iron Conviction
          Given the build "iron-panoply-4pc-festival-flight" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #1's heading is "#1 Critical health"
          And its BUFFS show only "Critical health" and "Iron Conviction"
          # keeping step #1: its Iron Conviction is what #2's kill refreshes
          When I tap "Festival Flight" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Festival Flight (💀 kill)"
          And "Iron Conviction" fires "Iron Conviction (7s)"
          And its BUFFS show only "Critical health" and "Iron Conviction"
        """)]
    public async Task IronConvictionsKillRefreshesItsBuff(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("iron-panoply-4pc-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var critical = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#1 Critical health")
            .Shows(new StateGroup.Buffs(), "Critical health", "Iron Conviction");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Festival Flight (💀 kill)")
            .Fires(new ArmorSetBonus.IronConviction(), "Iron Conviction (7s)")
            .Shows(new StateGroup.Buffs(), "Critical health", "Iron Conviction");
    }
}
