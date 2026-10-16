using System.Collections.Immutable;
using Dunet;

namespace Loopsmith.VsCodeExtension.E2E.Infrastructure.Vocabulary;

/// <summary>
/// A scenario file of the workspace VS Code for the web opens (Workspace/Scenarios/): one scenario each. A case is added
/// when a test needs it.
/// </summary>
[Union]
public partial record ScenarioFile
{
    /// <summary>Scenarios/Starting/StartFromScratchTests.cs: a web scenario played on both screens, and a helper method.</summary>
    public partial record StartFromScratch;

    /// <summary>Scenarios/Loop/Weapons/ReloadTests.cs: a web scenario played on both screens.</summary>
    public partial record Reload;

    /// <summary>Scenarios/Loop/Weapons/UnrelentingTests.cs: a web scenario played on both screens.</summary>
    public partial record Unrelenting;

    /// <summary>Scenarios/Recordings/LongNameTests.cs: a desktop scenario whose asset names pass 200 characters.</summary>
    public partial record LongName;
}

/// <summary>Each <see cref="ScenarioFile"/>'s path, its scenario's method, domain folder and screens: the only place they live.</summary>
public static class ScenarioFileShowing
{
    /// <summary>"Scenarios/Starting/StartFromScratchTests.cs": its path in the workspace, as the explorer nests it.</summary>
    public static string ToPath(this ScenarioFile file) =>
        file.Match(
            startFromScratch => "Scenarios/Starting/StartFromScratchTests.cs",
            reload => "Scenarios/Loop/Weapons/ReloadTests.cs",
            unrelenting => "Scenarios/Loop/Weapons/UnrelentingTests.cs",
            longName => "Scenarios/Recordings/LongNameTests.cs");

    /// <summary>"StartFromScratchTests.cs": its name, as its editor tab shows it.</summary>
    public static string ToFileName(this ScenarioFile file) => file.ToPath().Split('/')[^1];

    /// <summary>Its scenario's method name.</summary>
    public static string ToMethod(this ScenarioFile file) =>
        file.Match(
            startFromScratch => "StartFromScratchOpensAnArcHunterWithNothingEquipped",
            reload => "ReloadStandsApartFromTheCountAndTakesNone",
            unrelenting => "UnrelentingHealsForSureOn3QuickKills",
            longName => "AScenarioWhoseTitleGoesOnAndOnPastTwoHundredCharactersStillFindsItsRecordingBecauseTheWorkflowShortensItsAssetNamesWithAHashAndTheExtensionShortensThemTheSameWay");

    /// <summary>Its scenario's domain folder: its namespace after ".Scenarios.".</summary>
    public static string ToDomain(this ScenarioFile file) =>
        file.Match(
            startFromScratch => "Starting",
            reload => "Loop/Weapons",
            unrelenting => "Loop/Weapons",
            longName => "Recordings");

    /// <summary>The screens its scenario is played on.</summary>
    public static ImmutableArray<Screen> ToScreens(this ScenarioFile file) =>
        file.Match(
            startFromScratch => ImmutableArray.Create(Screen.Desktop, Screen.Mobile),
            reload => ImmutableArray.Create(Screen.Desktop, Screen.Mobile),
            unrelenting => ImmutableArray.Create(Screen.Desktop, Screen.Mobile),
            longName => ImmutableArray.Create(Screen.Desktop));
}
