using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop;

/// <summary>A keyword's icon before its name (rules/glossary.yaml, a status's <c>trait</c>): in a step's lines, its state's chips and the trigger panel's "On you".</summary>
public sealed class KeywordIconsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Void Overshield shows Bungie's Overshield icon where the designer names it
          Given the build "bulwark-finisher-void-overshield" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          # Bungie has no icon for Armor Charge: a status without one shows its name alone
          And "Armor Charge" under "On you" in the trigger panel has no icon
          # keeping step #1: its Armor Charge is what #2 declares at max
          When I tap "at max (x3)" on "Armor Charge" under "On you" in the trigger panel
          Then card #2's heading is "#2 Armor Charge at max (x3)"
          # keeping step #2: its x3 Armor Charge is what #3 spends
          When I tap "Finisher" in the trigger panel
          Then card #3's heading is "#3 Finisher"
          And "Bulwark Finisher" fires "Void Overshield (8s)" with the Overshield icon before it
          And its BUFFS chip "Void Overshield" has the Overshield icon before it
          And "Void Overshield" under "On you" in the trigger panel has the Overshield icon before it
        """)]
    public async Task VoidOvershieldShowsBungiesOvershieldIconWhereTheDesignerNamesIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("bulwark-finisher-void-overshield.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        // Bungie has no icon for Armor Charge: a status without one shows its name alone
        var chargedPanel = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(charged).HasHeading("#1 Finisher");
        Asserting.That(chargedPanel).ShowsOnYouWithNoIcon(new Keyword.ArmorCharge());

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.ArmorCharge(), "at max (x3)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var maxed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(maxed).HasHeading("#2 Armor Charge at max (x3)");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var finished = await Frontend.Designer.Loop.Step(3).ReadAsync();
        var finishedPanel = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(finished)
            .HasHeading("#3 Finisher")
            .FiresWithIcon(new ArmorMod.BulwarkFinisher(), "Void Overshield (8s)", new KeywordIcon.Overshield())
            .ShowsWithIcon(new StateGroup.Buffs(), "Void Overshield", new KeywordIcon.Overshield());
        Asserting.That(finishedPanel).ShowsOnYouWithIcon(new Keyword.VoidOvershield(), new KeywordIcon.Overshield());
    }
}
