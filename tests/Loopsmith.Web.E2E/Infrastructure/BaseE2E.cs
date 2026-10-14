using System.Diagnostics;
using System.Text.Json;
using Loopsmith.Web.E2E.Infrastructure.Frontend;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>
/// Everything around a scenario that isn't the scenario (E2E-TEST-CODE-RULES.md, rule b):
/// - skips it unless <c>E2E_URL</c> points at a running site;
/// - one browser for the whole run (<see cref="BrowserRun"/>), a fresh context per scenario with its own storage, so
///   scenarios share nothing (it checks the site's IndexedDB and local storage are empty before opening Home);
/// - routes the context's bungie.net requests (<see cref="BungieRouting"/>);
/// - opens Home on a recorded page and waits until it is shown (behind the manifest's startup screen), noting when, so
///   record.sh trims the startup screen off the video;
/// - when the run records (<c>E2E_VIDEO_DIR</c>): shows each click on the page, holds Home and the end on screen a
///   moment, and saves the video, also when the scenario fails, with its record next to it (title, GIF name, domain
///   folder, when Home was shown, whether it passed);
/// - gives the scenario its <see cref="Frontend"/>.
/// </summary>
public abstract class BaseE2E : IAsyncLifetime
{
    private static readonly TimeSpan HomeTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(2);
    private static readonly ViewportSize Screen = new() { Width = 1280, Height = 800 };

    private IBrowserContext? context;
    private IPage? page;
    private string? videoFolder;
    private readonly Stopwatch sinceOpened = new();
    private double? homeShownAt;

    /// <summary>The app as the user sees it, starting on Home.</summary>
    protected Frontend.Frontend Frontend { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Assert.SkipWhen(E2ESite.Url is null, "E2E_URL isn't set: end-to-end tests need a running site (tools/e2e/record.sh).");

        var run = (BrowserRun)(await TestContext.Current.GetFixture(typeof(BrowserRun)))!;
        var browser = await run.OpenBrowserAsync();
        videoFolder = E2ESite.VideoDirectory is { } videos ? Path.Combine(videos, ".raw", Guid.NewGuid().ToString("N")) : null;
        context = await browser.NewContextAsync(new()
        {
            ViewportSize = Screen,
            RecordVideoDir = videoFolder,
            RecordVideoSize = videoFolder is null ? null : new RecordVideoSize { Width = Screen.Width, Height = Screen.Height },
            AcceptDownloads = true,
        });
        context.SetDefaultTimeout((float)ActionTimeout.TotalMilliseconds);
        await BungieRouting.RouteAsync(context, DescribeScenario());
        if (videoFolder is not null)
        {
            await context.AddInitScriptAsync(ClickMarkerScript);
        }

        // Started before the page (and its video): the time Home is noted at is never earlier than Home in the video.
        sinceOpened.Start();
        page = await context.NewPageAsync();
        Frontend = new Frontend.Frontend(page);

        try
        {
            await CheckNothingSavedAsync(page);
            await page.GotoAsync(E2ESite.Url!);
            await Frontend.Home.WaitUntilShownAsync(HomeTimeout);
        }
        catch (Exception)
        {
            // xunit doesn't dispose a test whose start failed: its video is saved here, as a failure.
            await CloseAsync(passed: false);
            throw;
        }

        homeShownAt = sinceOpened.Elapsed.TotalSeconds;
        TestContext.Current.SendDiagnosticMessage($"{DescribeScenario()}: Home shown {homeShownAt:0.0} s after the page opened");
        if (videoFolder is not null)
        {
            await Task.Delay(Hold);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync(TestContext.Current.TestState?.Result == TestResult.Passed);
        GC.SuppressFinalize(this);
    }

    // Closes the context (once) and saves its video with the scenario's record.
    private async Task CloseAsync(bool passed)
    {
        if (context is null)
        {
            return;
        }

        var closing = context;
        context = null;
        if (videoFolder is not null && homeShownAt is not null)
        {
            await Task.Delay(Hold);
        }

        var video = page?.Video;
        await closing.CloseAsync();
        if (video is not null && E2ESite.VideoDirectory is { } videos)
        {
            await SaveRecordingAsync(videos, await video.PathAsync(), passed);
        }
    }

    // A new context holds nothing: the site's origin, opened on one of its static files (not the app), has no IndexedDB
    // database and no local storage.
    private static async Task CheckNothingSavedAsync(IPage page)
    {
        await page.GotoAsync(new Uri(new Uri(E2ESite.Url!), "favicon.svg").AbsoluteUri);
        var saved = await page.EvaluateAsync<string[]>(
            "async () => [...(await indexedDB.databases()).map(d => 'IndexedDB ' + d.name), ...Object.keys(localStorage).map(k => 'local storage ' + k)]");
        Assert.True(saved.Length == 0, $"A new browser context should start with nothing saved, but it has: {string.Join(", ", saved)}");
    }

    private async Task SaveRecordingAsync(string videos, string rawVideo, bool passed)
    {
        var method = TestContext.Current.TestMethod?.MethodName ?? "unnamed";
        var domain = ScenarioNaming.ToDomainFolder(GetType());
        var gifName = ScenarioNaming.ToGifName(method);
        var folder = Path.Combine(videos, domain);
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, gifName + ".webm");
        File.Move(rawVideo, target, overwrite: true);
        var record = new ScenarioRecording(
            domain,
            GetType().Name,
            method,
            ScenarioNaming.ToTitle(method),
            gifName,
            homeShownAt,
            passed);
        await File.WriteAllTextAsync(Path.Combine(folder, gifName + ".json"), JsonSerializer.Serialize(record, RecordingJson));
    }

    private string DescribeScenario() => $"{GetType().Name}.{TestContext.Current.TestMethod?.MethodName}";

    private static readonly JsonSerializerOptions RecordingJson = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // A ring where the pointer goes down, so a click shows in the video (headless Chromium draws no cursor).
    private const string ClickMarkerScript = """
        window.addEventListener('pointerdown', (event) => {
          const ring = document.createElement('div');
          ring.style.cssText = 'position:fixed;z-index:2147483647;pointer-events:none;width:36px;height:36px;margin:-18px 0 0 -18px;'
            + 'border:4px solid #ff3d7f;border-radius:50%;box-shadow:0 0 0 4px rgba(255,61,127,.35);transition:transform .6s,opacity .6s;'
            + `left:${event.clientX}px;top:${event.clientY}px`;
          document.documentElement.appendChild(ring);
          requestAnimationFrame(() => { ring.style.transform = 'scale(1.8)'; ring.style.opacity = '0'; });
          setTimeout(() => ring.remove(), 1200);
        }, true);
        """;
}

/// <summary>What record.sh reads next to a scenario's video: its names, when Home was shown and whether it passed.</summary>
public sealed record ScenarioRecording(
    string Domain,
    string Class,
    string Method,
    string Title,
    string Gif,
    double? HomeShownSeconds,
    bool Passed);
