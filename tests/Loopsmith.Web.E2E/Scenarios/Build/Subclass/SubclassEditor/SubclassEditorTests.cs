using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

namespace Loopsmith.Web.E2E.Scenarios.Build.Subclass;

/// <summary>
/// The subclass editor (docs/loop-format.md, "Editing the subclass"): it keeps the game's limit on fragments, and warns
/// when the build holds more fragments than its aspects open (a build file: validation only warns).
/// </summary>
public sealed class SubclassEditorTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The subclass editor warns when the build has more fragments than its aspects open
          Given the build "spark-of-shock-over-the-slots" is imported, with no steps
          When I tap "⋯" on the Subclass box
          Then the subclass editor warns "1 fragment equipped; the aspects open 0 slots. Save needs aspects that open enough slots, or fewer fragments."
        """)]
    public async Task TheSubclassEditorWarnsWhenTheBuildHasMoreFragmentsThanItsAspectsOpen(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-shock-over-the-slots.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.Subclass.ClickEditAsync();
        await Frontend.Designer.SubclassEditor.WaitUntilShownAsync();

        // Then
        var editor = await Frontend.Designer.SubclassEditor.ReadAsync();
        Asserting.That(editor).Warns("1 fragment equipped; the aspects open 0 slots. Save needs aspects that open enough slots, or fewer fragments.");
    }
}
