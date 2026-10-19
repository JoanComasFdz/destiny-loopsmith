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
            await Dialog.GetAttributeAsync("aria-label") ?? "",
            shown.TypeLine,
            [.. shown.InBuild.Select(role => new InBuildRole(role.Heading, [.. role.Items.Select(item => new InBuildItem(item.Source, item.Text))]))],
            [.. shown.RightThen.Select(card => new RightThenCard(card.Title, [.. card.Lines]))]);
    }

    // The type line; the "In this build" section: each role's heading ("While Amplified") and its items, each the
    // element it comes from ("Flow State") and what it does there ("200% faster class ability regeneration"); and, opened
    // from a state chip, the "Right then" card: its title, read as written, not as the page capitalises it, and its lines.
    private const string ReadScript = """
        dialog => {
          const text = el => (el ? el.innerText : '').replace(/\s+/g, ' ').trim();
          const written = el => (el ? el.textContent : '').replace(/\s+/g, ' ').trim();
          return {
            typeLine: text(dialog.querySelector('[data-testid=item-info-type]')),
            inBuild: [...dialog.querySelectorAll('[data-testid=info-in-build] [data-testid=in-build-role]')].map(role => ({
              heading: text(role.querySelector('[data-testid=in-build-role-head]')),
              items: [...role.querySelectorAll('[data-testid=in-build-item]')].map(item => ({
                source: item.getAttribute('data-source') || '',
                text: text(item.querySelector('[data-testid=in-build-text]')),
              })),
            })),
            rightThen: [...dialog.querySelectorAll('[data-testid=info-right-then]')].map(card => ({
              title: written(card.querySelector('[data-testid=info-right-then-title]')),
              lines: [...card.querySelectorAll('[data-testid=info-right-then-line]')].map(written),
            })),
          };
        }
        """;

    private sealed record ShownDialog(string TypeLine, ShownRole[] InBuild, ShownRightThen[] RightThen);

    private sealed record ShownRole(string Heading, ShownItem[] Items);

    private sealed record ShownItem(string Source, string Text);

    private sealed record ShownRightThen(string Title, string[] Lines);
}

/// <summary>
/// What the item details popup shows: its title (the item's name), the type line under it, its "In this build"
/// section, role by role (none when it has no such section) and, opened from a state chip, its "Right then, after #N" card.
/// </summary>
public sealed record ItemInfoDialogSnapshot(string Title, string TypeLine, ImmutableArray<InBuildRole> InBuild, ImmutableArray<RightThenCard> RightThen);

/// <summary>A role of the "In this build" section: its heading ("While Amplified") and its items.</summary>
public sealed record InBuildRole(string Heading, ImmutableArray<InBuildItem> Items);

/// <summary>An item of a role: the element it comes from ("Flow State") and what it does ("200% faster class ability regeneration").</summary>
public sealed record InBuildItem(string Source, string Text);

/// <summary>A "Right then, after #1" card: its title and its lines ("400% [PvP 200%] faster grenade and melee regeneration").</summary>
public sealed record RightThenCard(string Title, ImmutableArray<string> Lines);
