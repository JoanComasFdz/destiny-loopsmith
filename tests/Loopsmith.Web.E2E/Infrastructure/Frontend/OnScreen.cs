using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend;

/// <summary>
/// Every Act and Read member of a page object goes through here (E2E-TEST-CODE-RULES.md, rule c), so the video shows
/// what the scenario does, one step at a time:
/// - it brings the box the member acts on or reads into view first, so the video shows the whole box, not only the
///   element Playwright clicks (it scrolls that only just into view) or nothing at all (a read doesn't scroll). A box
///   that fits in the window is scrolled, as little as needed, until all of it shows below the sticky top bar; a taller
///   one until its top shows under the top bar, then, when the member uses an element inside it, until that shows too;
/// - when the run records, the scroll is smooth, a box that moved stays a moment on screen before the action or the
///   read, the drawn mouse pointer glides to the element an action uses before the action, and a step starts no sooner than <see cref="E2ESite.ActionGap"/> after the previous one ended (record.sh
///   <c>--action-gap</c>), so the GIF doesn't scroll and click faster than the eye follows. Without a recording it
///   jumps and goes on at once;
/// - on a phone (<see cref="Screen.Mobile"/>) a press is a tap, and no mouse pointer glides (a phone has none): when the run
///   records, a pink dot shows where the finger lands instead, held a moment before the action, so the video shows where
///   the tap happens before the screen changes (the tap's ring, BaseE2E's ring script, only shows once the app has
///   already re-rendered).
/// </summary>
public static class OnScreen
{
    private static readonly TimeSpan ShownBeforeStep = TimeSpan.FromMilliseconds(500);

    // As long as the drawn pointer's glide (BaseE2E's pointer script), and a moment to see it arrive.
    private static readonly TimeSpan PointerGlide = TimeSpan.FromMilliseconds(800);

    // How long the phone's dot shows where the finger lands before the tap (its fade-in included).
    private static readonly TimeSpan TapMarkerHold = TimeSpan.FromMilliseconds(600);

    // When each page's last step ended (one page per scenario, its steps one after another).
    private static readonly ConditionalWeakTable<IPage, Stopwatch> SinceLastStep = [];

    // The screen each page is played on (BaseE2E notes it when it opens the page).
    private static readonly ConditionalWeakTable<IPage, StrongBox<Screen>> Screens = [];

    private static bool Recording => E2ESite.VideoDirectory is not null;

    /// <summary>Notes that <paramref name="page"/> is played on <paramref name="screen"/> (BaseE2E, when it opens the page).</summary>
    public static void NoteScreen(IPage page, Screen screen) => Screens.AddOrUpdate(page, new StrongBox<Screen>(screen));

    /// <summary>Shows <paramref name="box"/> (and <paramref name="target"/> in it), then presses the target: a click, or a tap on a phone.</summary>
    public static Task PressAsync(ILocator box, ILocator target) =>
        ActAsync(box, target, pressed => IsPhone(pressed.Page) ? pressed.TapAsync() : pressed.ClickAsync());

    /// <summary>Shows <paramref name="box"/> (and <paramref name="target"/> in it), then does <paramref name="act"/> on the target.</summary>
    public static async Task ActAsync(ILocator box, ILocator target, Func<ILocator, Task> act)
    {
        await ShowAsync(box, target);
        if (Recording && IsPhone(box.Page))
        {
            await MarkTapAsync(target);
        }
        else if (Recording)
        {
            await GlideMouseToAsync(target);
        }

        await act(target);
        NoteStepEnded(box.Page);
    }

    /// <summary>Shows <paramref name="box"/>, then reads it with <paramref name="read"/>.</summary>
    public static async Task<T> ReadAsync<T>(ILocator box, Func<Task<T>> read)
    {
        await ShowAsync(box, null);
        var shown = await read();
        NoteStepEnded(box.Page);
        return shown;
    }

    private static async Task ShowAsync(ILocator box, ILocator? target)
    {
        if (Recording)
        {
            await WaitForActionGapAsync(box.Page);
        }

        var targetElement = target is null ? null : await target.ElementHandleAsync();
        var moved = await box.EvaluateAsync<bool>(ScrollScript, new Dictionary<string, object?>
        {
            ["target"] = targetElement,
            ["smooth"] = Recording,
        });
        if (targetElement is not null)
        {
            await targetElement.DisposeAsync();
        }

        if (Recording && moved)
        {
            await Task.Delay(ShownBeforeStep);
        }
    }

    // Moves the mouse to the target's centre (where Playwright clicks) and waits while the drawn pointer glides there
    // (BaseE2E's pointer script), so the video shows where the next action happens before it does.
    private static async Task GlideMouseToAsync(ILocator target)
    {
        if (await target.BoundingBoxAsync() is { } bounds)
        {
            await target.Page.Mouse.MoveAsync(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            await Task.Delay(PointerGlide);
        }
    }

    // Draws a pink dot where the finger lands (the target's centre, where Playwright taps) and holds it there, so the
    // video shows where the next action happens before it does. The dot goes when the finger goes down, as the tap's ring
    // takes its place (or a moment after the hold, for an action that is no tap).
    private static async Task MarkTapAsync(ILocator target)
    {
        if (await target.BoundingBoxAsync() is { } bounds)
        {
            await target.Page.EvaluateAsync(TapMarkerScript, new Dictionary<string, object?>
            {
                ["x"] = bounds.X + bounds.Width / 2,
                ["y"] = bounds.Y + bounds.Height / 2,
                ["hold"] = TapMarkerHold.TotalMilliseconds,
            });
            await Task.Delay(TapMarkerHold);
        }
    }

    private static async Task WaitForActionGapAsync(IPage page)
    {
        if (SinceLastStep.TryGetValue(page, out var since) && since.Elapsed < E2ESite.ActionGap)
        {
            await Task.Delay(E2ESite.ActionGap - since.Elapsed);
        }
    }

    private static bool IsPhone(IPage page) => Screens.TryGetValue(page, out var screen) && screen.Value == Screen.Mobile;

    private static void NoteStepEnded(IPage page) => SinceLastStep.AddOrUpdate(page, Stopwatch.StartNew());

    // The phone's dot: the tap ring's pink, filled, faded in over 150 ms.
    private const string TapMarkerScript = """
        ({ x, y, hold }) => {
          const dot = document.createElement('div');
          dot.style.cssText = 'position:fixed;z-index:2147483647;pointer-events:none;width:40px;height:40px;margin:-20px 0 0 -20px;'
            + 'border-radius:50%;background:rgba(255,61,127,.55);border:2px solid #ff3d7f;box-shadow:0 0 0 4px rgba(255,61,127,.3);'
            + `opacity:0;transition:opacity .15s;left:${x}px;top:${y}px`;
          document.documentElement.appendChild(dot);
          requestAnimationFrame(() => { dot.style.opacity = '1'; });
          const remove = () => { dot.remove(); window.removeEventListener('pointerdown', remove, true); };
          window.addEventListener('pointerdown', remove, true);
          setTimeout(remove, hold + 400);
        }
        """;

    // Scrolls the box (and the target) into view below the sticky top bar, waits until the page stops moving, and answers
    // whether it moved. The top bar is whatever sticky or fixed strip covers the top of the window (a dialog's backdrop,
    // which covers it all, is no top bar).
    private const string ScrollScript = """
        async (box, { target, smooth }) => {
          const margin = 8;
          const coveringTop = () => {
            for (let el = document.elementFromPoint(window.innerWidth / 2, 1); el && el !== document.documentElement; el = el.parentElement) {
              const position = getComputedStyle(el).position;
              const bounds = el.getBoundingClientRect();
              if ((position === 'sticky' || position === 'fixed') && bounds.top <= 0 && bounds.height < window.innerHeight / 3) {
                return bounds.bottom;
              }
            }
            return 0;
          };
          const top = coveringTop();
          const room = window.innerHeight - top - 2 * margin;
          const where = () => box.getBoundingClientRect().top;
          const before = where();
          const settle = async () => {
            let last = NaN, still = 0;
            const until = performance.now() + 3000;
            while (still < 5 && performance.now() < until) {
              await new Promise(requestAnimationFrame);
              const now = where();
              still = now === last ? still + 1 : 0;
              last = now;
            }
          };
          const scroll = async (el, block) => {
            const [marginTop, marginBottom] = [el.style.scrollMarginTop, el.style.scrollMarginBottom];
            el.style.scrollMarginTop = `${top + margin}px`;
            el.style.scrollMarginBottom = `${margin}px`;
            el.scrollIntoView({ block, inline: 'nearest', behavior: smooth ? 'smooth' : 'instant' });
            await settle();
            el.style.scrollMarginTop = marginTop;
            el.style.scrollMarginBottom = marginBottom;
          };
          if (box.getBoundingClientRect().height <= room) {
            await scroll(box, 'nearest');
          } else {
            await scroll(box, 'start');
            if (target) {
              await scroll(target, 'nearest');
            }
          }
          return where() !== before;
        }
        """;
}
