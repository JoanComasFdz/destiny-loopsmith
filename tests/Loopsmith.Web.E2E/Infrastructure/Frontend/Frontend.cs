using Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend;

/// <summary>
/// The app as the user sees it: one object per page (Pages/*.razor) and per component it shows (Components/*.razor),
/// named and nested as the app names and nests them. Locators stay private to their class (data-action, data-testid,
/// or role + accessible name).
/// </summary>
public sealed class Frontend(IPage page)
{
    /// <summary>The start screen (Pages/Home.razor).</summary>
    public Home Home { get; } = new(page);

    /// <summary>The loop designer (Pages/Designer.razor).</summary>
    public Designer Designer { get; } = new(page);
}
