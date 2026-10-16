using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The banner over a page (Components/NoticeBanner.razor): an import that failed, a link that could not be opened.</summary>
public sealed class NoticeBanner(IPage page)
{
    private ILocator Box => page.Locator("[data-testid=notice]");

    private ILocator Text => Box.Locator("[data-testid=notice-text]");

    /// <summary>Waits until the notice is shown.</summary>
    public Task WaitUntilShownAsync() =>
        Box.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>What the notice says now.</summary>
    public Task<NoticeBannerSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    private async Task<NoticeBannerSnapshot> ReadShownAsync() =>
        new((await Text.InnerTextAsync()).Trim());
}

/// <summary>What the notice says.</summary>
public sealed record NoticeBannerSnapshot(string Text);
