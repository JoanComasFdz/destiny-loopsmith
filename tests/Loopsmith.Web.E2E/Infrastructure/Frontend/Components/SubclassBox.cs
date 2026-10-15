using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The Subclass box of the designer's loadout (Components/LoadoutPanel.razor).</summary>
public sealed class SubclassBox(IPage page)
{
    private ILocator Box => page.Locator("[data-testid=subclass]");

    /// <summary>What the Subclass box shows now.</summary>
    public Task<SubclassBoxSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    private async Task<SubclassBoxSnapshot> ReadShownAsync()
    {
        var shownClass = await Box.GetAttributeAsync("data-class") ?? "";
        var shownSubclass = await Box.GetAttributeAsync("data-subclass") ?? "";
        return new SubclassBoxSnapshot(GuardianClassShowing.ReadGuardianClass(shownClass), SubclassShowing.ReadSubclass(shownSubclass));
    }
}

/// <summary>What the Subclass box shows: the build's class and subclass.</summary>
public sealed record SubclassBoxSnapshot(GuardianClass Class, Subclass Subclass);
