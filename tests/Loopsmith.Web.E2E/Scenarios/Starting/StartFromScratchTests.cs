using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Starting;

/// <summary>Starting a loop from scratch on Home: an Arc Hunter with nothing equipped and no loop steps.</summary>
public sealed class StartFromScratchTests : BaseE2E
{
    [Fact]
    public async Task GivenAppIsOpenOnHome_WhenIClickStartFromScratch_ThenTheDesignerShowsAnArcHunterWithNothingEquipped()
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ClickStartFromScratchAsync();
        await Frontend.Designer.WaitUntilShownAsync();

        // Then
        var subclass = await Frontend.Designer.Subclass.ReadAsync();
        Asserting.That(subclass).HasClass(new GuardianClass.Unlisted("Titan")).HasSubclass(new Subclass.Arc());

        var weapons = await Frontend.Designer.Weapons.ReadAsync();
        Asserting.That(weapons).HasNothingEquipped();

        var armor = await Frontend.Designer.Armor.ReadAsync();
        Asserting.That(armor).HasNothingEquipped();

        var artifact = await Frontend.Designer.Artifact.ReadAsync();
        Asserting.That(artifact).IsNotSet();

        var loop = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(loop).HasNoSteps();
    }
}
