using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Void.Fragments;

/// <summary>Echo of Vigilance's rules (rules/all-classes/void/fragments.yaml), one scenario per rule claim.</summary>
public sealed class EchoOfVigilanceTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Echo of Vigilance's kill at critical health gives a Void Overshield
          # Rule all-classes-void-fragments-echo-of-vigilance-1: Kill ➜ Echo of Vigilance equipped and Critical health on you ➜ Void Overshield
          Given the build "echo-of-vigilance-scathelocke" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #1's heading is "#1 Critical health"
          And its BUFFS show only "Critical health"
          # keeping step #1: its critical health is what #2's kill needs
          When I tap "Scathelocke" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Scathelocke (💀 kill)"
          And "Echo of Vigilance" fires "Void Overshield"
          And its BUFFS show only "Critical health" and "Void Overshield"
        """)]
    public async Task EchoOfVigilancesKillAtCriticalHealthGivesAVoidOvershield(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("echo-of-vigilance-scathelocke.build.yaml");
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
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Scathelocke (💀 kill)")
            .Fires(new Fragment.EchoOfVigilance(), "Void Overshield")
            .Shows(new StateGroup.Buffs(), "Critical health", "Void Overshield");
    }
}
