using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Helmet;

/// <summary>Dynamo's rules (rules/mods/helmet.yaml), one scenario per rule claim.</summary>
public sealed class DynamoTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Dynamo's class ability gives Super energy
          # Rule mods-helmet-dynamo-1: Class ability ➜ Dynamo equipped ➜ Super energy (?)
          Given the build "dynamo-marksmans-dodge" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And it reads "Class ability"
          # The amount depends on the build's class, which a rule can't read: unknown, so "?".
          And "Dynamo" fires "+?% super energy"
        """)]
    public async Task DynamosClassAbilityGivesSuperEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("dynamo-marksmans-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // The amount depends on the build's class, which a rule can't read: unknown, so "?".
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Reads("Class ability")
            .Fires(new ArmorMod.Dynamo(), "+?% super energy");
    }
}
