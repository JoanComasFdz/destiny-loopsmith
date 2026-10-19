using System.Collections.Immutable;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The Subclass box of the designer's loadout (Components/LoadoutPanel.razor).</summary>
public sealed class SubclassBox(IPage page)
{
    private ILocator Box => page.Locator("[data-testid=subclass]");

    private ILocator Edit => Box.Locator("[data-action=edit-subclass]");

    private ILocator Aspects => Box.Locator("[data-testid=subclass-aspect]");

    private ILocator Fragments => Box.Locator("[data-testid=subclass-fragment]");

    // A plug's square is named for its plug.
    private ILocator FragmentSquare(Fragment fragment) =>
        Fragments.And(Box.Page.GetByRole(AriaRole.Button, new() { Name = fragment.ToShownText(), Exact = true }));

    /// <summary>Clicks <paramref name="fragment"/>'s square: its item details popup opens.</summary>
    public Task ClickFragmentAsync(Fragment fragment) => OnScreen.PressAsync(Box, FragmentSquare(fragment));

    /// <summary>Clicks ⋯ on the Subclass box: the subclass editor opens.</summary>
    public Task ClickEditAsync() => OnScreen.PressAsync(Box, Edit);

    /// <summary>What the Subclass box shows now.</summary>
    public Task<SubclassBoxSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    private async Task<SubclassBoxSnapshot> ReadShownAsync()
    {
        var shownClass = await Box.GetAttributeAsync("data-class") ?? "";
        var shownSubclass = await Box.GetAttributeAsync("data-subclass") ?? "";
        return new SubclassBoxSnapshot(
            GuardianClassShowing.ReadGuardianClass(shownClass),
            SubclassShowing.ReadSubclass(shownSubclass),
            [.. (await ReadNamesAsync(Aspects)).Select(AspectShowing.ReadAspect)],
            [.. (await ReadNamesAsync(Fragments)).Select(FragmentShowing.ReadFragment)]);
    }

    // A plug's square is named for its plug.
    private static async Task<ImmutableArray<string>> ReadNamesAsync(ILocator squares) =>
        [.. await Task.WhenAll((await squares.AllAsync()).Select(async square => (await square.GetAttributeAsync("aria-label") ?? "").Trim()))];
}

/// <summary>What the Subclass box shows: the build's class and subclass, its aspects and its fragments, in order.</summary>
public sealed record SubclassBoxSnapshot(GuardianClass Class, Subclass Subclass, ImmutableArray<Aspect> Aspects, ImmutableArray<Fragment> Fragments);
