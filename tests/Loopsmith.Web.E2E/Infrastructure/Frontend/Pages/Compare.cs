using System.Collections.Immutable;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;

/// <summary>The Compare screen (Pages/Compare.razor): a loop picked for each side, Left and Right, and their comparison.</summary>
public sealed class Compare(IPage page)
{
    private ILocator Box => page.GetByRole(AriaRole.Main);

    private ILocator Heading => Box.GetByRole(AriaRole.Heading, new() { Name = "Compare loops", Exact = true });

    private ILocator Help => Box.Locator("[data-testid=compare-help]");

    private ILocator NothingToCompare => Box.GetByRole(AriaRole.Heading, new() { Name = "Nothing to compare yet", Exact = true });

    private ILocator LeftPicker => Box.GetByRole(AriaRole.Combobox, new() { Name = "Left loop", Exact = true });

    private ILocator RightPicker => Box.GetByRole(AriaRole.Combobox, new() { Name = "Right loop", Exact = true });

    /// <summary>Waits until the Compare screen is shown.</summary>
    public Task WaitUntilShownAsync() => Heading.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>Waits until the Compare screen shows its loop pickers, Left and Right.</summary>
    public Task WaitUntilPickersShownAsync() => RightPicker.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>What the Compare screen shows: its help text, whether it says there is nothing to compare yet, and the loops each side's picker offers.</summary>
    public Task<CompareSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    private async Task<CompareSnapshot> ReadShownAsync() =>
        new(
            (await Help.InnerTextAsync()).Trim(),
            await NothingToCompare.IsVisibleAsync(),
            await ReadOffersAsync(LeftPicker),
            await ReadOffersAsync(RightPicker));

    private static async Task<ImmutableArray<string>> ReadOffersAsync(ILocator picker) =>
        await picker.CountAsync() == 0
            ? []
            : [.. (await picker.GetByRole(AriaRole.Option, new() { IncludeHidden = true }).AllTextContentsAsync()).Select(text => text.Trim())];
}

/// <summary>
/// What the Compare screen shows: its help text, whether it says "Nothing to compare yet", and the options each side's
/// loop picker offers (none when it shows no picker).
/// </summary>
public sealed record CompareSnapshot(string Help, bool ShowsNothingToCompare, ImmutableArray<string> LeftOffers, ImmutableArray<string> RightOffers);
