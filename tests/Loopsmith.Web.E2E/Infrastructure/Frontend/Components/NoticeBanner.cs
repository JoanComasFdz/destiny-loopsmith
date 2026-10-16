using System.Text.Json;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>
/// The banner over a page (Components/NoticeBanner.razor): an import that failed, a link that could not be opened. On
/// Home it floats under the header. <paramref name="actedOn"/> is where the player acts on the page (Home: the
/// "import a build.yaml" link): a read with no notice shows it, and the layout says whether it is still in view.
/// </summary>
public sealed class NoticeBanner(IPage page, ILocator actedOn)
{
    private ILocator Box => page.Locator("[data-testid=notice]");

    private ILocator Text => Box.Locator("[data-testid=notice-text]");

    private ILocator Dismiss => Box.GetByRole(AriaRole.Button, new() { Name = "Dismiss", Exact = true });

    private ILocator Header => page.GetByRole(AriaRole.Banner);

    /// <summary>Waits until the notice is shown.</summary>
    public Task WaitUntilShownAsync() =>
        Box.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>Waits until the notice is gone.</summary>
    public Task WaitUntilGoneAsync() =>
        Box.WaitForAsync(new() { State = WaitForSelectorState.Detached });

    /// <summary>Clicks the notice's "✕": it goes.</summary>
    public Task ClickDismissAsync() => OnScreen.PressAsync(Box, Dismiss);

    /// <summary>How many notices show now, and what the first says.</summary>
    public async Task<NoticeBannerSnapshot> ReadAsync() =>
        await Box.CountAsync() > 0
            ? await OnScreen.ReadAsync(Box.First, ReadShownAsync)
            : await OnScreen.ReadAsync(actedOn, () => Task.FromResult(new NoticeBannerSnapshot(0, "")));

    private async Task<NoticeBannerSnapshot> ReadShownAsync() =>
        new(await Box.CountAsync(), (await Text.First.InnerTextAsync()).Trim());

    /// <summary>
    /// Where the notice, the header and where the player acted are on the screen. The notice floats, so the read doesn't
    /// scroll the page: it is measured where the player left it.
    /// </summary>
    public Task<NoticeBannerLayout> ReadLayoutAsync() => OnScreen.ReadAsync(Box, ReadLayoutShownAsync);

    private async Task<NoticeBannerLayout> ReadLayoutShownAsync()
    {
        var header = await Header.ElementHandleAsync();
        var acted = await actedOn.ElementHandleAsync();
        var layout = (await Box.EvaluateAsync<JsonElement>(LayoutScript, new[] { header, acted })).Deserialize<NoticeBannerLayout>(ShownJson.Options)!;
        await header.DisposeAsync();
        await acted.DisposeAsync();
        return layout;
    }

    private const string LayoutScript = """
        (notice, [header, acted]) => {
          const box = el => { const r = el.getBoundingClientRect(); return { left: r.left, top: r.top, right: r.right, bottom: r.bottom }; };
          return {
            notice: box(notice),
            header: box(header),
            actedOn: box(acted),
            screenWidth: window.innerWidth,
            screenHeight: window.innerHeight,
            pageWidth: document.documentElement.scrollWidth,
          };
        }
        """;
}

/// <summary>How many notices show (0 when none does), and what the first says.</summary>
public sealed record NoticeBannerSnapshot(int Shown, string Text);

/// <summary>
/// Where the notice is on the screen, in CSS pixels: its box, the header's, the box of where the player acted, the
/// screen's width and height, and the page's width.
/// </summary>
public sealed record NoticeBannerLayout(ScreenBox Notice, ScreenBox Header, ScreenBox ActedOn, double ScreenWidth, double ScreenHeight, double PageWidth);
