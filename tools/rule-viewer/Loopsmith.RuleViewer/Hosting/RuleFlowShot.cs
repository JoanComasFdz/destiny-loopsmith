using Loopsmith.Core.Functional;
using Microsoft.Playwright;

namespace Loopsmith.RuleViewer.Hosting;

/// <summary>
/// Impure boundary that takes a rule's PNG: opens its page in Playwright's headless Chromium (the one the repository pins,
/// <c>Directory.Packages.props</c>; <c>/opt/pw-browsers</c> in a cloud session), waits for its icons and fonts, draws its
/// wires and saves a picture of the flows, at twice the pixels so it stays sharp on a phone.
/// </summary>
public static class RuleFlowShot
{
    private const string DrawWires = "() => document.fonts.ready.then(() => new Promise(done => requestAnimationFrame(() => { wireFlows(); done(); })))";

    public static async Task<Result<string, string>> SaveShotAsync(string html, string selector, string path)
    {
        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
            Directory.CreateDirectory(folder);
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1600, Height = 900 }, DeviceScaleFactor = 2, ColorScheme = ColorScheme.Light });
            await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.NetworkIdle });
            await page.EvaluateAsync(DrawWires);
            await page.Locator(selector).ScreenshotAsync(new() { Path = path });
            return new Result<string, string>.Ok(path);
        }
        catch (PlaywrightException exception)
        {
            return new Result<string, string>.Error($"Cannot take '{path}' with Playwright's Chromium: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<string, string>.Error($"Cannot write '{path}': {exception.Message}");
        }
    }
}
