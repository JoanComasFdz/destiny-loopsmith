using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Layout;

/// <summary>The header over every page (Layout/MainLayout.razor): the Loopsmith logo, which leads Home, and the screen tabs.</summary>
public sealed class Header(IPage page)
{
    private ILocator Box => page.GetByRole(AriaRole.Banner);

    private ILocator Logo => Box.GetByRole(AriaRole.Link, new() { Name = "Loopsmith — home", Exact = true });

    /// <summary>Clicks the Loopsmith logo: Home.</summary>
    public Task ClickLogoAsync() => OnScreen.PressAsync(Box, Logo);
}
