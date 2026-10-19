using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The item details popup (Components/ItemInfoDialog.razor), opened by an item's square or a state chip.</summary>
public sealed class ItemInfoDialog(IPage page)
{
    private ILocator Dialog => page.GetByRole(AriaRole.Dialog).Filter(new() { Has = page.Locator("[data-testid=item-info]") });

    private ILocator Close => Dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true });

    /// <summary>Waits until the item details popup is open.</summary>
    public Task WaitUntilShownAsync() => Dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>Clicks the popup's "✕": it closes.</summary>
    public Task ClickCloseAsync() => OnScreen.PressAsync(Dialog, Close);

    /// <summary>Waits until the item details popup is closed.</summary>
    public Task WaitUntilClosedAsync() => Dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });

    /// <summary>What the item details popup shows now.</summary>
    public Task<ItemInfoDialogSnapshot> ReadAsync() => OnScreen.ReadAsync(Dialog, ReadShownAsync);

    private async Task<ItemInfoDialogSnapshot> ReadShownAsync()
    {
        var shown = (await Dialog.EvaluateAsync<JsonElement>(ReadScript)).Deserialize<ShownDialog>(ShownJson.Options)!;
        return new ItemInfoDialogSnapshot(
            shown.Title,
            shown.TypeLine,
            [.. shown.RightThen.Select(card => new RightThenCard(card.Title, [.. card.Lines]))]);
    }

    // The popup's texts, white space collapsed. A "Right then" card's title is read as written, not as the page
    // capitalises it.
    private const string ReadScript = """
        dialog => {
          const text = el => (el ? el.textContent : '').replace(/\s+/g, ' ').trim();
          return {
            title: dialog.getAttribute('aria-label') ?? '',
            typeLine: text(dialog.querySelector('[data-testid=item-info-type]')),
            rightThen: [...dialog.querySelectorAll('[data-testid=info-right-then]')].map(card => ({
              title: text(card.querySelector('[data-testid=info-right-then-title]')),
              lines: [...card.querySelectorAll('[data-testid=info-right-then-line]')].map(text),
            })),
          };
        }
        """;

    private sealed record ShownDialog(string Title, string TypeLine, ShownRightThen[] RightThen);

    private sealed record ShownRightThen(string Title, string[] Lines);
}

/// <summary>
/// What the item details popup shows: its title (the item's name), the type line under it and, opened from a state
/// chip, its "Right then, after #N" card.
/// </summary>
public sealed record ItemInfoDialogSnapshot(string Title, string TypeLine, ImmutableArray<RightThenCard> RightThen);

/// <summary>A "Right then, after #1" card: its title and its lines ("400% [PvP 200%] faster grenade and melee regeneration").</summary>
public sealed record RightThenCard(string Title, ImmutableArray<string> Lines);
