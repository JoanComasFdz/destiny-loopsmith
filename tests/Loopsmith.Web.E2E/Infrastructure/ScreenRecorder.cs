using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>
/// Films a page from screenshots at the device's pixels (a phone at its density: 780×1688 for a 390×844 phone at
/// 2×), which Playwright's recorder can't: Chromium's screencast hands it CSS pixels, and so does a screenshot unless
/// its clip asks for the device's scale (a clip is in the document's coordinates, so each shot first reads where the
/// window is scrolled to, <c>Page.getLayoutMetrics</c>, as Playwright's own screenshot does). Until stopped, it asks
/// Chromium for a JPEG (<c>Page.captureScreenshot</c>, over the page's own CDP session) every 40 ms without waiting for
/// the one before (one at a time, Chromium answers about 20 a second; a few at once, over 50), times each one on the
/// scenario's clock (the stopwatch Home's time is noted on) when its answer comes (while the app starts, an answer can
/// take seconds, and shows the page as it is then), and writes them with an ffmpeg concat list: each frame lasts until
/// the next one, the first one from 0, so the video's clock is the scenario's. tools/e2e/record.sh turns the list into
/// the run's 25 fps video. A screenshot that fails (a page between two documents) is skipped, and so is a tick while
/// three are still on their way: the frame before lasts longer.
/// </summary>
public sealed class ScreenRecorder
{
    private static readonly TimeSpan FrameGap = TimeSpan.FromMilliseconds(40);
    private const int MostOnTheirWay = 3;

    private readonly ICDPSession session;
    private readonly string folder;
    private readonly Stopwatch clock;
    private readonly ViewportSize window;
    private readonly float scale;
    private readonly List<(int Index, Task<double?> TakenAt)> shots = [];
    private readonly CancellationTokenSource stopping = new();
    private Task? filming;

    private ScreenRecorder(ICDPSession session, string folder, Stopwatch clock, ViewportSize window, float scale)
    {
        this.session = session;
        this.folder = folder;
        this.clock = clock;
        this.window = window;
        this.scale = scale;
    }

    /// <summary>Starts filming the page (its window drawn at the given scale) into its own folder, timed on the given clock.</summary>
    public static async Task<ScreenRecorder> StartAsync(IPage page, ViewportSize window, float scale, string folder, Stopwatch clock)
    {
        Directory.CreateDirectory(folder);
        var session = await page.Context.NewCDPSessionAsync(page);
        var recorder = new ScreenRecorder(session, folder, clock, window, scale);
        recorder.filming = Task.Run(recorder.FilmAsync);
        return recorder;
    }

    /// <summary>
    /// Stops filming and writes the concat list (<c>frames.ffconcat</c> in its folder); says how many frames a second
    /// it took from the given time on (Home shown: the app's startup screen is slower to film, and is trimmed off). The
    /// list's last frame lasts until now.
    /// </summary>
    public async Task<FilmedFrames> StopAsync(double from)
    {
        await stopping.CancelAsync();
        await filming!;
        await Task.WhenAll(shots.Select(shot => shot.TakenAt));
        var end = clock.Elapsed.TotalSeconds;
        var saved = shots.Where(shot => shot.TakenAt.Result is not null).Select(shot => (shot.Index, At: shot.TakenAt.Result!.Value)).OrderBy(shot => shot.At).ToList();
        var list = Path.Combine(folder, "frames.ffconcat");
        await File.WriteAllTextAsync(list, DescribeFrames(saved, end));
        var counted = saved.Count(shot => shot.At >= from);
        return new FilmedFrames(list, saved.Count, end > from ? counted / (end - from) : 0);
    }

    private async Task FilmAsync()
    {
        var next = clock.Elapsed;
        while (!stopping.IsCancellationRequested)
        {
            if (shots.Count(shot => !shot.TakenAt.IsCompleted) < MostOnTheirWay)
            {
                shots.Add((shots.Count, SaveShotAsync(shots.Count)));
            }

            next += FrameGap;
            var wait = next - clock.Elapsed;
            if (wait <= TimeSpan.Zero)
            {
                next = clock.Elapsed;
                continue;
            }

            try
            {
                await Task.Delay(wait, stopping.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    // A JPEG of the page as Chromium draws it, saved as the frame of that index; when Chromium answered (it draws the
    // picture just before), or nothing when it can't take one (the page is between documents, or closing).
    private async Task<double?> SaveShotAsync(int index)
    {
        try
        {
            var answer = await session.SendAsync("Page.captureScreenshot", await AskForWindowAsync());
            var takenAt = clock.Elapsed.TotalSeconds;
            if (answer is not { } json || !json.TryGetProperty("data", out var data))
            {
                return null;
            }

            await File.WriteAllBytesAsync(Path.Combine(folder, ToFrameName(index)), Convert.FromBase64String(data.GetString()!));
            return takenAt;
        }
        catch (PlaywrightException)
        {
            return null;
        }
    }

    // A JPEG of the window as it is scrolled now, at the device's scale.
    private async Task<Dictionary<string, object>> AskForWindowAsync()
    {
        var metrics = await session.SendAsync("Page.getLayoutMetrics");
        var scrolled = metrics!.Value.GetProperty("cssVisualViewport");
        return new()
        {
            ["format"] = "jpeg",
            ["quality"] = 90,
            ["optimizeForSpeed"] = true,
            ["clip"] = new Dictionary<string, object>
            {
                ["x"] = scrolled.GetProperty("pageX").GetDouble(),
                ["y"] = scrolled.GetProperty("pageY").GetDouble(),
                ["width"] = window.Width,
                ["height"] = window.Height,
                ["scale"] = scale,
            },
        };
    }

    // Each frame lasts until the next one (the first from 0, so the video starts with the page), the last one until the end.
    private static string DescribeFrames(List<(int Index, double At)> saved, double end)
    {
        var list = new StringBuilder("ffconcat version 1.0\n");
        for (var i = 0; i < saved.Count; i++)
        {
            var from = i == 0 ? 0 : saved[i].At;
            var until = i + 1 < saved.Count ? saved[i + 1].At : end;
            list.Append(CultureInfo.InvariantCulture, $"file '{ToFrameName(saved[i].Index)}'\nduration {Math.Max(until - from, 0.001):0.000}\n");
        }

        // The concat demuxer drops the last entry's duration unless the file is named again.
        if (saved.Count > 0)
        {
            list.Append(CultureInfo.InvariantCulture, $"file '{ToFrameName(saved[^1].Index)}'\n");
        }

        return list.ToString();
    }

    private static string ToFrameName(int index) => $"frame-{index:D6}.jpg";
}

/// <summary>A filmed run: its concat list, how many frames it holds and how many it took a second from Home on.</summary>
public sealed record FilmedFrames(string List, int Count, double FramesPerSecond);
