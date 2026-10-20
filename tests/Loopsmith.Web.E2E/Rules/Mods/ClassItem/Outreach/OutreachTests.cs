using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.ClassItem;

/// <summary>Outreach's rule (rules/mods/class-item.yaml), one scenario per rule claim.</summary>
public sealed class OutreachTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Outreach gives melee energy on a class ability
          # Rule mods-class-item-outreach-1: Class ability ➜ Outreach equipped ➜ Melee energy (12% | 17% | 20%)
          Given the build "outreach-marksmans-dodge" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And "Outreach" fires "+12% melee energy"
        """)]
    public async Task OutreachGivesMeleeEnergyOnAClassAbility(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("outreach-marksmans-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Fires(new ArmorMod.Outreach(), "+12% melee energy");
    }
}
