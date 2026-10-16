using System.Text.Json;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>
/// A problem shown where the player acted (Components/InlineProblem.razor): on Home, the import problem under the
/// "import a build.yaml" link (<paramref name="link"/>).
/// </summary>
public sealed class InlineProblem(IPage page, string id, ILocator link)
{
    private ILocator Box => page.Locator($"[data-testid={id}]");

    private ILocator Text => Box.Locator($"[data-testid={id}-text]");

    private ILocator Dismiss => Box.Locator($"[data-action=dismiss-{id}]");

    /// <summary>Waits until the problem is shown.</summary>
    public Task WaitUntilShownAsync() =>
        Box.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>Waits until the problem is gone.</summary>
    public Task WaitUntilGoneAsync() =>
        Box.WaitForAsync(new() { State = WaitForSelectorState.Detached });

    /// <summary>Clicks the problem's "✕": it goes.</summary>
    public Task ClickDismissAsync() => OnScreen.PressAsync(Box, Dismiss);

    /// <summary>What the problem says now.</summary>
    public Task<InlineProblemSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    private async Task<InlineProblemSnapshot> ReadShownAsync() =>
        new((await Text.InnerTextAsync()).Trim());

    /// <summary>Where the problem and the link it belongs under are on the screen.</summary>
    public Task<InlineProblemLayout> ReadLayoutAsync() => OnScreen.ReadAsync(Box, ReadLayoutShownAsync);

    private async Task<InlineProblemLayout> ReadLayoutShownAsync()
    {
        var linkElement = await link.ElementHandleAsync();
        var layout = (await Box.EvaluateAsync<JsonElement>(LayoutScript, linkElement)).Deserialize<InlineProblemLayout>(ShownJson.Options)!;
        await linkElement.DisposeAsync();
        return layout;
    }

    private const string LayoutScript = """
        (problem, link) => {
          const box = el => { const r = el.getBoundingClientRect(); return { left: r.left, top: r.top, right: r.right, bottom: r.bottom }; };
          return {
            problem: box(problem),
            link: box(link),
            screenWidth: window.innerWidth,
            screenHeight: window.innerHeight,
            pageWidth: document.documentElement.scrollWidth,
          };
        }
        """;
}

/// <summary>What the problem says.</summary>
public sealed record InlineProblemSnapshot(string Text);

/// <summary>
/// Where the problem is on the screen, in CSS pixels: its box, the box of the link it belongs under, the screen's width
/// and height, and the page's width.
/// </summary>
public sealed record InlineProblemLayout(ScreenBox Problem, ScreenBox Link, double ScreenWidth, double ScreenHeight, double PageWidth);
