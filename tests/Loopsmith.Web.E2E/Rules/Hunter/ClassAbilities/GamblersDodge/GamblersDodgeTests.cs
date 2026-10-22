using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.ClassAbilities;

/// <summary>Gambler's Dodge's rules (rules/hunter/class-abilities.yaml), one scenario per rule claim.</summary>
public sealed class GamblersDodgeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Gambler's Dodge gives melee energy on a dodge
          # Rule hunter-class-abilities-gamblers-dodge-1: Class ability ➜ Gambler's Dodge equipped ➜ Melee energy (2% per Melee point)
          Given the build "gamblers-dodge" is imported, with no steps
          When I tap "Gambler's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And it reads "Class ability"
          And "Gambler's Dodge" fires "+2% melee energy per Melee point"
        """)]
    public async Task GamblersDodgeGivesMeleeEnergyOnADodge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("gamblers-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.GamblersDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Reads("Class ability")
            .Fires(new Ability.GamblersDodge(), "+2% melee energy per Melee point");
    }
}
