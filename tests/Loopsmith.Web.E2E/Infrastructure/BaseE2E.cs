using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Loopsmith.Web.E2E.Infrastructure.Frontend;
using Microsoft.Playwright;
using Xunit.v3;

namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>
/// Everything around a scenario that isn't the scenario (E2E-TEST-CODE-RULES.md, rule b):
/// - skips it unless <c>E2E_URL</c> points at a running site;
/// - one browser for the whole run (<see cref="BrowserRun"/>), a fresh context per scenario with its own storage, so
///   scenarios share nothing (it checks the site's IndexedDB and local storage are empty before opening Home);
/// - opens that context as the screen the scenario is played on (its <c>[PlayInDesktop]</c> / <c>[PlayInMobile]</c>):
///   a 1280×800 window with a mouse, its video the same size, or a 390×844 phone with touch, its video at the phone's
///   pixels (780×1688: <see cref="BrowserRun"/>);
/// - routes the context's bungie.net requests (<see cref="BungieRouting"/>);
/// - opens Home on a recorded page and waits until it is shown (behind the manifest's startup screen), noting when, so
///   record.sh trims the startup screen off the video;
/// - when the run records (<c>E2E_VIDEO_DIR</c>): draws a ring at each click or tap (and on desktop a big mouse pointer),
///   holds Home and the end on screen a moment, and saves the video, also when the scenario fails, with its record next
///   to it (title, Gherkin, screen, GIF name, domain folder, when Home was shown, whether it passed);
/// - gives the scenario its <see cref="Frontend"/>.
/// </summary>
public abstract class BaseE2E : IAsyncLifetime
{
    private static readonly TimeSpan HomeTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(2);
    private static readonly ViewportSize DesktopWindow = new() { Width = 1280, Height = 800 };
    private static readonly ViewportSize Phone = new() { Width = 390, Height = 844 };

    private IBrowserContext? context;
    private IPage? page;
    private string? videoFolder;
    private readonly Stopwatch sinceOpened = new();
    private double? homeShownAt;
    private Screen screen;

    /// <summary>The app as the user sees it, starting on Home.</summary>
    protected Frontend.Frontend Frontend { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Assert.SkipWhen(E2ESite.Url is null, "E2E_URL isn't set: end-to-end tests need a running site (tools/e2e/record.sh).");

        var run = (BrowserRun)(await TestContext.Current.GetFixture(typeof(BrowserRun)))!;
        screen = ReadScreen();
        var browser = await run.OpenBrowserAsync(screen);
        videoFolder = E2ESite.VideoDirectory is { } videos ? Path.Combine(videos, ".raw", Guid.NewGuid().ToString("N")) : null;
        context = await browser.NewContextAsync(OpenAs(screen, videoFolder));
        context.SetDefaultTimeout((float)ActionTimeout.TotalMilliseconds);
        await BungieRouting.RouteAsync(context, DescribeScenario());
        if (videoFolder is not null)
        {
            await context.AddInitScriptAsync(screen == Screen.Mobile
                ? RecordingOverlay.TapRingScript
                : RecordingOverlay.PointerScript + RecordingOverlay.TapRingScript);
        }

        // Started before the page (and its video): the time Home is noted at is never earlier than Home in the video.
        sinceOpened.Start();
        page = await context.NewPageAsync();
        OnScreen.NoteScreen(page, screen);
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

    // The screen the scenario's [PlayInDesktop] / [PlayInMobile] row plays it on (its one argument).
    private static Screen ReadScreen() =>
        TestContext.Current.Test is IXunitTest { TestMethodArguments: [Screen played] } ? played : Screen.Desktop;

    // A desktop window with a mouse, or a phone with touch (Chromium's phone emulation: the page is laid out for the
    // phone's width and matches (hover: none) and (pointer: coarse)), drawn at the phone's density. The video is the
    // window at its pixels: 1280×800, a phone's 780×1688 (its browser draws at that density: BrowserRun).
    private static BrowserNewContextOptions OpenAs(Screen screen, string? videoFolder)
    {
        var window = screen == Screen.Mobile ? Phone : DesktopWindow;
        var density = screen == Screen.Mobile ? BrowserRun.PhoneDensity : 1;
        return new()
        {
            ViewportSize = window,
            IsMobile = screen == Screen.Mobile,
            HasTouch = screen == Screen.Mobile,
            DeviceScaleFactor = density,
            RecordVideoDir = videoFolder,
            RecordVideoSize = videoFolder is null ? null : new RecordVideoSize { Width = window.Width * density, Height = window.Height * density },
            AcceptDownloads = true,
        };
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
        var gherkin = Gherkin.Read(GetType().GetMethod(method)?.GetCustomAttribute<TestDescriptionAttribute>()?.Gherkin ?? "");
        var domain = ScenarioNaming.ToDomainFolder(GetType());
        var gifName = ScenarioNaming.ToGifName(method, screen);
        var folder = Path.Combine(videos, domain);
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, gifName + ".webm");
        File.Move(rawVideo, target, overwrite: true);
        var record = new ScenarioRecording(
            domain,
            GetType().Name,
            method,
            gherkin.Title.Length > 0 ? gherkin.Title : method,
            gherkin.Text,
            screen.ToString().ToLowerInvariant(),
            gifName,
            homeShownAt,
            passed);
        await File.WriteAllTextAsync(Path.Combine(folder, gifName + ".json"), JsonSerializer.Serialize(record, RecordingJson));
    }

    private string DescribeScenario() => $"{GetType().Name}.{TestContext.Current.TestMethod?.MethodName} ({screen})";

    private static readonly JsonSerializerOptions RecordingJson = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}

