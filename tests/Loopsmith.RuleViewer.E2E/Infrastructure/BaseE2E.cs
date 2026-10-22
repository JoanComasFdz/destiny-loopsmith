using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;
using Microsoft.Playwright;
using Xunit.v3;

namespace Loopsmith.RuleViewer.E2E.Infrastructure;

/// <summary>
/// Everything around a scenario that isn't the scenario (E2E-TEST-CODE-RULES.md, rule b):
/// - skips it unless <c>E2E_RULE_VIEWER_URL</c> points at the rules page drawn from Fixtures/rules (tools/e2e/record.sh);
/// - one browser per screen for the whole run (<see cref="BrowserRun"/>), a fresh context per scenario, so scenarios share
///   nothing;
/// - opens that context as the screen the scenario is played on (its <c>[PlayInDesktop]</c> / <c>[PlayInMobile]</c>):
///   a 1280×800 window with a mouse, its video the same size, or a 390×844 phone with touch, its video at the phone's
///   pixels (780×1688: <see cref="BrowserRun"/>);
/// - serves the page's icons and fonts from the local cache (<see cref="OutsideRouting"/>), so it looks as it does for a
///   reader and no run depends on the network;
/// - opens the page on a recorded page and waits until its tree is shown, its icons and fonts are loaded and its flows
///   are wired, noting when, so
///   record.sh trims the blank start off the video;
/// - fails a scenario that passed when the page logged an error to its console or threw;
/// - when the run records (<c>E2E_VIDEO_DIR</c>): draws a ring at each click or tap (and on desktop a big mouse pointer),
///   holds the page and the end on screen a moment, and saves the video, also when the scenario fails, with its record
///   next to it (title, Gherkin, screen, GIF name, domain folder, when the page was shown, whether it passed);
/// - gives the scenario its <see cref="RulesPage.RulesPage"/>.
/// </summary>
public abstract class BaseE2E : IAsyncLifetime
{
    private static readonly TimeSpan ShownTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(2);
    private static readonly ViewportSize DesktopWindow = new() { Width = 1280, Height = 800 };
    private static readonly ViewportSize Phone = new() { Width = 390, Height = 844 };

    private IBrowserContext? context;
    private IPage? page;
    private string? videoFolder;
    private readonly Stopwatch sinceOpened = new();
    private readonly List<string> consoleErrors = [];
    private double? shownAt;
    private byte[]? shownScreenshot;
    private Screen screen;

    /// <summary>The rules page as the user sees it: its tree beside (or, on a phone, above) every rule's flows.</summary>
    protected RulesPage.RulesPage RulesPage { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Assert.SkipWhen(E2ERuleViewer.Url is null, "E2E_RULE_VIEWER_URL isn't set: these scenarios need the rules page drawn from Fixtures/rules (tools/e2e/record.sh).");

        var run = (BrowserRun)(await TestContext.Current.GetFixture(typeof(BrowserRun)))!;
        screen = ReadScreen();
        var browser = await run.OpenBrowserAsync(screen);
        videoFolder = E2ERuleViewer.VideoDirectory is { } videos ? Path.Combine(videos, ".raw", Guid.NewGuid().ToString("N")) : null;
        context = await browser.NewContextAsync(OpenAs(screen, videoFolder));
        context.SetDefaultTimeout((float)ActionTimeout.TotalMilliseconds);
        await OutsideRouting.RouteAsync(context);
        if (videoFolder is not null)
        {
            await context.AddInitScriptAsync(screen == Screen.Mobile
                ? RecordingOverlay.TapRingScript
                : RecordingOverlay.PointerScript + RecordingOverlay.TapRingScript);
        }

        // Started before the page (and its video). The video's clock runs behind it, so record.sh trims the video at the
        // first frame that looks like the screenshot taken when the page is noted (<name>.home.png; the e2e tool's find-home).
        sinceOpened.Start();
        page = await context.NewPageAsync();
        page.Console += (_, message) => NoteConsoleError(message);
        page.PageError += (_, error) => consoleErrors.Add(error);
        OnScreen.NoteScreen(page, screen);
        RulesPage = new RulesPage.RulesPage(page);

        try
        {
            await page.GotoAsync(E2ERuleViewer.Url!);
            await RulesPage.WaitUntilShownAsync(ShownTimeout);
        }
        catch (Exception)
        {
            // xunit doesn't dispose a test whose start failed: its video is saved here, as a failure.
            await CloseAsync(passed: false);
            throw;
        }

        shownAt = sinceOpened.Elapsed.TotalSeconds;
        TestContext.Current.SendDiagnosticMessage($"{DescribeScenario()}: the rules page shown {shownAt:0.0} s after it opened");
        if (videoFolder is not null)
        {
            shownScreenshot = await page.ScreenshotAsync();
            await Task.Delay(Hold);
        }
    }

    public async ValueTask DisposeAsync()
    {
        var passed = TestContext.Current.TestState?.Result == TestResult.Passed;
        await CloseAsync(passed && consoleErrors.Count == 0);
        GC.SuppressFinalize(this);
        Assert.True(
            !passed || consoleErrors.Count == 0,
            $"The rules page logged {consoleErrors.Count} error(s) to its console:{Environment.NewLine}{string.Join(Environment.NewLine, consoleErrors)}");
    }

    private void NoteConsoleError(IConsoleMessage message)
    {
        if (message.Type == "error")
        {
            consoleErrors.Add(message.Text);
        }
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
        if (videoFolder is not null && shownAt is not null)
        {
            await Task.Delay(Hold);
        }

        var video = page?.Video;
        await closing.CloseAsync();
        if (video is not null && E2ERuleViewer.VideoDirectory is { } videos)
        {
            await SaveRecordingAsync(videos, await video.PathAsync(), passed);
        }
    }

    // The screen the scenario's [PlayInDesktop] / [PlayInMobile] row plays it on (its one argument).
    private static Screen ReadScreen() =>
        TestContext.Current.Test is IXunitTest { TestMethodArguments: [Screen played] } ? played : Screen.Desktop;

    // A desktop window with a mouse, or a phone with touch (Chromium's phone emulation), drawn at the phone's density. The
    // video is the window at its pixels: 1280×800, a phone's 780×1688 (its browser draws at that density: BrowserRun).
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
        };
    }

    private async Task SaveRecordingAsync(string videos, string rawVideo, bool passed)
    {
        var method = TestContext.Current.TestMethod?.MethodName ?? "unnamed";
        var gherkin = Gherkin.Read(GetType().GetMethod(method)?.GetCustomAttribute<TestDescriptionAttribute>()?.Gherkin ?? "");
        var domain = ScenarioNaming.ToDomainFolder(GetType());
        var gifName = ScenarioNaming.ToGifName(method, screen);
        var folder = Path.Combine(videos, domain);
        Directory.CreateDirectory(folder);
        File.Move(rawVideo, Path.Combine(folder, gifName + ".webm"), overwrite: true);
        var record = new ScenarioRecording(
            domain,
            GetType().Name,
            method,
            gherkin.Title.Length > 0 ? gherkin.Title : method,
            gherkin.Text,
            screen.ToString().ToLowerInvariant(),
            gifName,
            shownAt,
            passed);
        await File.WriteAllTextAsync(Path.Combine(folder, gifName + ".json"), JsonSerializer.Serialize(record, RecordingJson));
        if (shownScreenshot is not null)
        {
            await File.WriteAllBytesAsync(Path.Combine(folder, gifName + ".home.png"), shownScreenshot);
        }
    }

    private string DescribeScenario() => $"{GetType().Name}.{TestContext.Current.TestMethod?.MethodName} ({screen})";

    private static readonly JsonSerializerOptions RecordingJson = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
