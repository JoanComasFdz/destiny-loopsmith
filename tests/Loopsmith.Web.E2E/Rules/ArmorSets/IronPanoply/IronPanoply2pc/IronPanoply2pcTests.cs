using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.ArmorSets.IronPanoply;

/// <summary>Iron Panoply's 2-piece bonus, Vigilant Watch (rules/armor-sets/iron-panoply.yaml), one scenario per rule claim.</summary>
public sealed class IronPanoply2pcTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Vigilant Watch's weapon kill at critical health heals you
          # Rule armor-sets-iron-panoply-iron-panoply-2pc-1: Weapon kill ➜ Vigilant Watch equipped and Critical health on you ➜ Heal
          Given the build "iron-panoply-2pc-festival-flight" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #1's heading is "#1 Critical health"
          And its BUFFS show only "Critical health"
          # keeping step #1: its critical health is what #2's kill needs
          When I tap "Festival Flight" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Festival Flight (💀 kill)"
          And "Vigilant Watch" fires "restores ? health"
          And its BUFFS show only "Critical health"
        """)]
    public async Task VigilantWatchsWeaponKillAtCriticalHealthHealsYou(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("iron-panoply-2pc-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var critical = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#1 Critical health")
            .Shows(new StateGroup.Buffs(), "Critical health");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Festival Flight (💀 kill)")
            .Fires(new ArmorSetBonus.VigilantWatch(), "restores ? health")
            .Shows(new StateGroup.Buffs(), "Critical health");
    }
}
