using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;

namespace Loopsmith.Web.E2E.Scenarios.ImportExport;

/// <summary>
/// Importing a build.yaml on Home (docs/rule-format.md, "Build file"): a file Loopsmith can't read is refused there, and
/// the problem shows under the "import a build.yaml" link, where the player tapped.
/// </summary>
public sealed class BuildImportTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A build file whose weapon has no archetype or hash is refused
          Given the app is open on Home
          When I import the build "crisis-inverted-without-archetype-or-hash"
          Then the import problem reads "crisis-inverted-without-archetype-or-hash.build.yaml:6: weapon 'Crisis Inverted' needs an `archetype` or a `hash`"
          And Home is still shown
        """)]
    public async Task ABuildFileWhoseWeaponHasNoArchetypeOrHashIsRefused(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportBuildAsync("crisis-inverted-without-archetype-or-hash.build.yaml");
        await Frontend.Home.ImportProblem.WaitUntilShownAsync();

        // Then
        var problem = await Frontend.Home.ImportProblem.ReadAsync();
        var home = await Frontend.Home.ReadAsync();
        Asserting.That(problem).Reads("crisis-inverted-without-archetype-or-hash.build.yaml:6: weapon 'Crisis Inverted' needs an `archetype` or a `hash`");
        Asserting.That(home).IsShown();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A refused build file's problem shows under the import link, inside the screen
          Given the app is open on Home
          When I import the build "crisis-inverted-without-archetype-or-hash"
          Then the import problem sits right under the "import a build.yaml" link
          And the import problem fits inside the screen, with no sideways scroll
          And the notice at the top of Home is not shown
        """)]
    public async Task ARefusedBuildFilesProblemShowsUnderTheImportLinkInsideTheScreen(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportBuildAsync("crisis-inverted-without-archetype-or-hash.build.yaml");
        await Frontend.Home.ImportProblem.WaitUntilShownAsync();

        // Then
        var layout = await Frontend.Home.ImportProblem.ReadLayoutAsync();
        var notice = await Frontend.Home.Notice.ReadAsync();
        Asserting.That(layout)
            .SitsRightUnderTheLink()
            .FitsInsideTheScreen();
        Asserting.That(notice).IsNotShown();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Importing again clears the previous build file's problem
          Given the app is open on Home
          And I imported the build "crisis-inverted-without-archetype-or-hash"
          When I import the build "crisis-inverted-without-archetype-or-hash" again
          Then exactly one import problem is shown
        """)]
    public async Task ImportingAgainClearsThePreviousBuildFilesProblem(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();
        await Frontend.Home.ImportBuildAsync("crisis-inverted-without-archetype-or-hash.build.yaml");
        await Frontend.Home.ImportProblem.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportBuildAsync("crisis-inverted-without-archetype-or-hash.build.yaml");
        await Frontend.Home.ImportProblem.WaitUntilShownAsync();

        // Then
        var home = await Frontend.Home.ReadAsync();
        Asserting.That(home).ShowsOneImportProblem();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Dismissing a refused build file's problem hides it
          Given the app is open on Home
          And I imported the build "crisis-inverted-without-archetype-or-hash"
          When I dismiss the import problem
          Then the import problem is not shown
        """)]
    public async Task DismissingARefusedBuildFilesProblemHidesIt(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();
        await Frontend.Home.ImportBuildAsync("crisis-inverted-without-archetype-or-hash.build.yaml");
        await Frontend.Home.ImportProblem.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportProblem.ClickDismissAsync();
        await Frontend.Home.ImportProblem.WaitUntilGoneAsync();

        // Then
        var home = await Frontend.Home.ReadAsync();
        Asserting.That(home).ShowsNoImportProblem();
    }
}
