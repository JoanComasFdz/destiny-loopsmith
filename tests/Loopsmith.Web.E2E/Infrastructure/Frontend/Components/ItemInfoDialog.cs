using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The item details popup (Components/ItemInfoDialog.razor), opened by an item's square.</summary>
public sealed class ItemInfoDialog(IPage page)
{
    private ILocator Dialog => page.GetByRole(AriaRole.Dialog).Filter(new() { Has = page.Locator("[data-testid=item-info]") });

    private ILocator TypeLine => Dialog.Locator("[data-testid=item-info-type]");

    /// <summary>Waits until the item details popup is open.</summary>
    public Task WaitUntilShownAsync() => Dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>What the item details popup shows now.</summary>
    public Task<ItemInfoDialogSnapshot> ReadAsync() => OnScreen.ReadAsync(Dialog, ReadShownAsync);

    private async Task<ItemInfoDialogSnapshot> ReadShownAsync() =>
        new(await Dialog.GetAttributeAsync("aria-label") ?? "", (await TypeLine.InnerTextAsync()).Trim());
}

/// <summary>What the item details popup shows: its title (the item's name) and the type line under it.</summary>
public sealed record ItemInfoDialogSnapshot(string Title, string TypeLine);
