using System.Collections.Immutable;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>
/// The "What this build does" panel above the loop (Components/ExplainPanel.razor): one line per trigger, "Always -> … [source]
/// + … [source]", closed until its title is tapped.
/// </summary>
public sealed class BuildExplanation(IPage page)
{
    private ILocator Box => page.Locator("[data-testid=build-explanation]");

    private ILocator Toggle => Box.Locator("[data-action=build-explanation-toggle]");

    private ILocator Lines => Box.Locator("[data-testid=styled-line]");

    /// <summary>Clicks the panel's title, "What this build does", which opens it when it is closed.</summary>
    public Task ClickOpenAsync() => OnScreen.PressAsync(Box, Toggle);

    /// <summary>Waits until the panel shows its lines.</summary>
    public Task WaitUntilOpenAsync() => Lines.First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>What the panel shows now: its lines, in order.</summary>
    public Task<BuildExplanationSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    private async Task<BuildExplanationSnapshot> ReadShownAsync() =>
        new([.. (await Lines.AllInnerTextsAsync()).Select(line => line.Trim())]);
}

/// <summary>What the "What this build does" panel shows: its lines, in order ("Always -> 25% … [Spark of Resistance] + …").</summary>
public sealed record BuildExplanationSnapshot(ImmutableArray<string> Lines);
