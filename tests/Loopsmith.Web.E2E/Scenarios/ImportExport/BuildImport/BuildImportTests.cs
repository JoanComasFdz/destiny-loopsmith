using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.ImportExport;

/// <summary>
/// Importing a build.yaml on Home (docs/rule-format.md, "Build file"): a file Loopsmith can't read is refused there, and
/// the notice floats under the header, where the player is; one the game wouldn't hold (more fragments than its aspects
/// open) opens with a warning.
/// </summary>
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
        var home = await Frontend.Home.ReadAsync();
        Asserting.That(notice).Reads("crisis-inverted-without-archetype-or-hash.build.yaml:6: weapon 'Crisis Inverted' needs an `archetype` or a `hash`");
        Asserting.That(home).IsShown();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A refused build file's notice floats right under the header, inside the screen
          Given the app is open on Home
          When I import the build "crisis-inverted-without-archetype-or-hash"
          Then the notice sits right under the header
          And the notice fits inside the screen, with no sideways scroll
          And the "import a build.yaml" link is still in view
        """)]
    public async Task ARefusedBuildFilesNoticeFloatsRightUnderTheHeaderInsideTheScreen(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportBuildAsync("crisis-inverted-without-archetype-or-hash.build.yaml");
        await Frontend.Home.Notice.WaitUntilShownAsync();

        // Then
        var layout = await Frontend.Home.Notice.ReadLayoutAsync();
        Asserting.That(layout)
            .SitsRightUnderTheHeader()
            .FitsInsideTheScreen()
            .KeepsTheImportLinkInView();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Importing again shows the build file's notice once
          Given the app is open on Home
          And I imported the build "crisis-inverted-without-archetype-or-hash"
          When I import the build "crisis-inverted-without-archetype-or-hash" again
          Then exactly one notice is shown
        """)]
    public async Task ImportingAgainShowsTheBuildFilesNoticeOnce(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();
        await Frontend.Home.ImportBuildAsync("crisis-inverted-without-archetype-or-hash.build.yaml");
        await Frontend.Home.Notice.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportBuildAsync("crisis-inverted-without-archetype-or-hash.build.yaml");
        await Frontend.Home.Notice.WaitUntilShownAsync();

        // Then
        var notice = await Frontend.Home.Notice.ReadAsync();
        Asserting.That(notice).IsShownOnce();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Dismissing a refused build file's notice hides it
          Given the app is open on Home
          And I imported the build "crisis-inverted-without-archetype-or-hash"
          When I dismiss the notice
          Then the notice is not shown
        """)]
    public async Task DismissingARefusedBuildFilesNoticeHidesIt(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();
        await Frontend.Home.ImportBuildAsync("crisis-inverted-without-archetype-or-hash.build.yaml");
        await Frontend.Home.Notice.WaitUntilShownAsync();

        // When
        await Frontend.Home.Notice.ClickDismissAsync();
        await Frontend.Home.Notice.WaitUntilGoneAsync();

        // Then
        var notice = await Frontend.Home.Notice.ReadAsync();
        Asserting.That(notice).IsNotShown();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A build file with a fragment and no aspect opens with a warning
          Given the app is open on Home
          When I import the build "spark-of-shock-alone"
          Then the Subclass box shows the fragment "Spark of Shock" and no aspect
          And the Validation list shows the warning "1 fragment equipped; the aspects grant 0 slots."
        """)]
    public async Task ABuildFileWithAFragmentAndNoAspectOpensWithAWarning(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ImportBuildAsync("spark-of-shock-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // Then
        var subclass = await Frontend.Designer.Subclass.ReadAsync();
        var validation = await Frontend.Designer.Validation.ReadAsync();
        Asserting.That(subclass).ShowsFragments(new Fragment.SparkOfShock()).ShowsNoAspect();
        Asserting.That(validation).ShowsWarning("1 fragment equipped; the aspects grant 0 slots.");
    }
}
