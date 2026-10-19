using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.ImportExport;

/// <summary>
/// A build file's weapon given by its hash (docs/rule-format.md, "Build file"): its slot, name and damage type are Bungie's
/// manifest's, read from the full manifest when the bundled excerpt lacks them; a hash no manifest knows is refused.
/// </summary>
public sealed class WeaponByHashTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A build file weapon given only by its hash shows the manifest's slot, name and damage type
          Given the app is open on Home
          When I import the build "festival-flight-by-hash"
          Then the Weapons box's Kinetic slot holds "Festival Flight", Strand
        """)]
    public async Task ABuildFileWeaponGivenOnlyByItsHashShowsTheManifestsSlotNameAndDamageType(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportBuildAsync("festival-flight-by-hash.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // Then
        var weapons = await Frontend.Designer.Weapons.ReadAsync();
        Asserting.That(weapons).Holds(new WeaponSlot.Kinetic(), new Weapon.FestivalFlight(), new DamageType.Strand());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The manifest wins over the slot, name and damage type a build file gives with a weapon's hash
          Given the app is open on Home
          When I import the build "festival-flight-misnamed"
          # The file says "Old Name", arc, in the energy slot; its hash is Festival Flight's, a Strand kinetic weapon.
          Then the Weapons box's Kinetic slot holds "Festival Flight", Strand
          And it holds no other weapon
        """)]
    public async Task TheManifestWinsOverTheSlotNameAndDamageTypeABuildFileGivesWithAWeaponsHash(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportBuildAsync("festival-flight-misnamed.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // Then
        // The file says "Old Name", arc, in the energy slot; its hash is Festival Flight's, a Strand kinetic weapon.
        var weapons = await Frontend.Designer.Weapons.ReadAsync();
        Asserting.That(weapons)
            .Holds(new WeaponSlot.Kinetic(), new Weapon.FestivalFlight(), new DamageType.Strand())
            .HoldsNoOtherWeapon(new WeaponSlot.Kinetic());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A build file weapon whose hash only Bungie's full manifest knows is read from it
          Given the app is open on Home
          When I import the build "ogma-pr6-by-hash"
          # Ogma PR6 isn't in Loopsmith's bundled manifest excerpt: the designer reads it from Bungie's full manifest.
          Then the Weapons box's Energy slot holds "Ogma PR6", Solar
        """)]
    public async Task ABuildFileWeaponWhoseHashOnlyBungiesFullManifestKnowsIsReadFromIt(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportBuildAsync("ogma-pr6-by-hash.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // Then
        // Ogma PR6 isn't in Loopsmith's bundled manifest excerpt: the designer reads it from Bungie's full manifest.
        var weapons = await Frontend.Designer.Weapons.ReadAsync();
        Asserting.That(weapons).Holds(new WeaponSlot.Energy(), new Weapon.OgmaPr6(), new DamageType.Solar());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A build file weapon whose hash no manifest knows is refused
          Given the app is open on Home
          When I import the build "unknown-weapon-hash"
          Then the notice reads "unknown-weapon-hash.build.yaml:6: weapon hash 1234567 isn't in the manifest, so the file can't be imported"
          And Home is still shown
        """)]
    public async Task ABuildFileWeaponWhoseHashNoManifestKnowsIsRefused(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportBuildAsync("unknown-weapon-hash.build.yaml");
        await Frontend.Home.Notice.WaitUntilShownAsync();

        // Then
        var notice = await Frontend.Home.Notice.ReadAsync();
        Asserting.That(notice).Reads("unknown-weapon-hash.build.yaml:6: weapon hash 1234567 isn't in the manifest, so the file can't be imported");

        var home = await Frontend.Home.ReadAsync();
        Asserting.That(home).IsShown();
    }
}
