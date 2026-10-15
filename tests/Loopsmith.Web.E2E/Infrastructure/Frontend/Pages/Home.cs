using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;

/// <summary>The start screen (Pages/Home.razor): start a loop from scratch, from a DIM link, a build or a loop file.</summary>
public sealed class Home(IPage page)
{
    private ILocator StartFromScratch => page.Locator("[data-action=scratch-start]");

    private ILocator HandWritten => page.Locator("[data-testid=hand-written]");

    private ILocator ImportBuild => HandWritten.Locator("[data-action=import-build]");

    /// <summary>Waits until Home is shown (after the manifest's startup screen).</summary>
    public Task WaitUntilShownAsync() => WaitUntilShownAsync(TimeSpan.FromSeconds(30));

    /// <summary>Waits up to <paramref name="timeout"/> until Home is shown (BaseE2E's first wait, behind the startup screen).</summary>
    public Task WaitUntilShownAsync(TimeSpan timeout) =>
        StartFromScratch.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = (float)timeout.TotalMilliseconds });

    /// <summary>Clicks "Start from scratch": a new loop for an Arc Hunter with nothing equipped.</summary>
    public Task ClickStartFromScratchAsync() => OnScreen.ActAsync(StartFromScratch, StartFromScratch, button => button.ClickAsync());

    /// <summary>Imports the build.yaml fixture <paramref name="fixture"/> (a file next to a scenario class): a new loop for it.</summary>
    public Task ImportBuildAsync(string fixture) =>
        OnScreen.ActAsync(HandWritten, ImportBuild, input => input.SetInputFilesAsync(FindFixture(fixture)));

    private static string FindFixture(string fixture) =>
        Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Scenarios"), fixture, SearchOption.AllDirectories).Single();
}
