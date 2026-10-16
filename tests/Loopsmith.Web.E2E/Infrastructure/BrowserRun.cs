using Microsoft.Playwright;

[assembly: AssemblyFixture(typeof(Loopsmith.Web.E2E.Infrastructure.BrowserRun))]

namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>
/// One browser per screen for the whole run (the Chromium the cloud image ships, found through
/// <c>PLAYWRIGHT_BROWSERS_PATH</c>); each scenario opens its own context in its screen's. Each is started on first use, so
/// a run without <c>E2E_URL</c> starts none. They show their scrollbars: Playwright starts headless Chromium with
/// <c>--hide-scrollbars</c>, so a video of a page taller than the window would end at the window's bottom edge with
/// nothing saying the page goes on. The phone's browser draws at the phone's density
/// (<c>--force-device-scale-factor=2</c>): an emulated density alone draws the page at twice the pixels but hands
/// Playwright's recorder (Chromium's screencast) its CSS pixels, a blurry 390×844; drawn at 2×, the screencast is the
/// phone's 780×1688.
/// </summary>
public sealed class BrowserRun : IAsyncLifetime
{
    /// <summary>A phone's pixels per CSS pixel: its browser draws at it, and its context emulates it.</summary>
    public const int PhoneDensity = 2;

    private IPlaywright? playwright;
    private readonly Dictionary<Screen, IBrowser> browsers = [];

    /// <summary>The run's browser for a screen: started on first use.</summary>
    public async Task<IBrowser> OpenBrowserAsync(Screen screen)
    {
        await gate.WaitAsync();
        try
        {
            if (browsers.TryGetValue(screen, out var open))
            {
                return open;
            }

            playwright ??= await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new()
            {
                Headless = true,
                IgnoreDefaultArgs = ["--hide-scrollbars"],
                Args = screen == Screen.Mobile ? [$"--force-device-scale-factor={PhoneDensity}"] : [],
            });
            browsers[screen] = browser;
            return browser;
        }
        finally
        {
            gate.Release();
        }
    }

    private readonly SemaphoreSlim gate = new(1, 1);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var browser in browsers.Values)
        {
            await browser.CloseAsync();
        }

        playwright?.Dispose();
        gate.Dispose();
    }
}
