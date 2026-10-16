using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;

namespace Loopsmith.Web.E2E.Scenarios.Starting;

/// <summary>Importing a build.yaml on Home (docs/rule-format.md, "Build file"): a file Loopsmith can't read is refused there.</summary>
public sealed class BuildImportTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A build file whose weapon has no archetype or hash is refused
          Given the app is open on Home
          When I import the build "crisis-inverted-without-archetype-or-hash"
          Then the notice reads "crisis-inverted-without-archetype-or-hash.build.yaml:6: weapon 'Crisis Inverted' needs an `archetype` or a `hash`"
          And Home is still shown
        """)]
    public async Task ABuildFileWhoseWeaponHasNoArchetypeOrHashIsRefused(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportBuildAsync("crisis-inverted-without-archetype-or-hash.build.yaml");
        await Frontend.Home.Notice.WaitUntilShownAsync();

        // Then
        var notice = await Frontend.Home.Notice.ReadAsync();
        Asserting.That(notice).Reads("crisis-inverted-without-archetype-or-hash.build.yaml:6: weapon 'Crisis Inverted' needs an `archetype` or a `hash`");

        var home = await Frontend.Home.ReadAsync();
        Asserting.That(home).IsShown();
    }
}
