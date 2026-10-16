using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>
/// The banner over a page (Components/NoticeBanner.razor): a link that could not be opened. With no notice, a read shows
/// <paramref name="pageTop"/>, the first part of the page under where the banner would be.
/// </summary>
public sealed class NoticeBanner(IPage page, ILocator pageTop)
{
    private ILocator Box => page.Locator("[data-testid=notice]");

    private ILocator Text => Box.Locator("[data-testid=notice-text]");

    /// <summary>Waits until the notice is shown.</summary>
    public Task WaitUntilShownAsync() =>
        Box.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>Whether the notice shows now, and what it says.</summary>
    public async Task<NoticeBannerSnapshot> ReadAsync() =>
        await Box.IsVisibleAsync()
            ? await OnScreen.ReadAsync(Box, ReadShownAsync)
            : await OnScreen.ReadAsync(pageTop, () => Task.FromResult(new NoticeBannerSnapshot(false, "")));

    private async Task<NoticeBannerSnapshot> ReadShownAsync() =>
        new(true, (await Text.InnerTextAsync()).Trim());
}

/// <summary>Whether the notice shows, and what it says (nothing when it doesn't show).</summary>
public sealed record NoticeBannerSnapshot(bool IsShown, string Text);
