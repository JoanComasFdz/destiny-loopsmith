using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;

/// <summary>The start screen (Pages/Home.razor): start a loop from scratch, from a DIM link, a build or a loop file.</summary>
public sealed class Home(IPage page)
{
    private ILocator StartFromScratch => page.Locator("[data-action=scratch-start]");

    /// <summary>Waits until Home is shown (after the manifest's startup screen).</summary>
    public Task WaitUntilShownAsync() => WaitUntilShownAsync(TimeSpan.FromSeconds(30));

    /// <summary>Waits up to <paramref name="timeout"/> until Home is shown (BaseE2E's first wait, behind the startup screen).</summary>
    public Task WaitUntilShownAsync(TimeSpan timeout) =>
        StartFromScratch.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = (float)timeout.TotalMilliseconds });

    /// <summary>Clicks "Start from scratch": a new loop for an Arc Hunter with nothing equipped.</summary>
    public Task ClickStartFromScratchAsync() => StartFromScratch.ClickAsync();
}
