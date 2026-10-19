using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Build.Weapons;

/// <summary>A weapon's item details, opened by its square in the Weapons box: its type line names its ammo type.</summary>
public sealed class WeaponDetailsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A weapon's details show its ammo type
          Given the build "thunderlord-weapon-details" is imported, with no steps
          When I tap Thunderlord's square in the Weapons box
          Then the item details popup opens, titled "Thunderlord"
          And its type line reads "Exotic Machine Gun · Heavy"
        """)]
    public async Task AWeaponsDetailsShowItsAmmoType(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("thunderlord-weapon-details.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.Weapons.ClickSquareAsync(new Weapon.Thunderlord());
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new Weapon.Thunderlord())
            .HasTypeLine("Exotic Machine Gun · Heavy");
    }
}
