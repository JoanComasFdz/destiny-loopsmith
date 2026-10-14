using Microsoft.Playwright;

[assembly: AssemblyFixture(typeof(Loopsmith.Web.E2E.Infrastructure.BrowserRun))]

namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>
/// One browser for the whole run (the Chromium the cloud image ships, found through <c>PLAYWRIGHT_BROWSERS_PATH</c>);
/// each scenario opens its own context in it. Started only when the run has a site to test.
/// </summary>
public sealed class BrowserRun : IAsyncLifetime
{
    private IPlaywright? playwright;
    private IBrowser? browser;

    /// <summary>The run's browser: started on first use, so a run without <c>E2E_URL</c> starts none.</summary>
    public async Task<IBrowser> OpenBrowserAsync()
    {
        if (browser is not null)
        {
            return browser;
        }

        await gate.WaitAsync();
        try
        {
            playwright ??= await Playwright.CreateAsync();
            browser ??= await playwright.Chromium.LaunchAsync(new() { Headless = true });
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
        if (browser is not null)
        {
            await browser.CloseAsync();
        }

        playwright?.Dispose();
        gate.Dispose();
    }
}
