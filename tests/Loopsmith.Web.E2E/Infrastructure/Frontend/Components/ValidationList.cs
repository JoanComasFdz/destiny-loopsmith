using System.Collections.Immutable;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The Validation list under the build (Components/BuildPanel.razor): its warnings (!) and notes (i).</summary>
public sealed class ValidationList(IPage page)
{
    private ILocator List => page.Locator("[data-testid=validation]");

    private ILocator Issues => List.Locator("[data-testid=issue]");

    /// <summary>What the Validation list shows now: each line's kind and text, in order.</summary>
    public Task<ValidationListSnapshot> ReadAsync() => OnScreen.ReadAsync(List, ReadShownAsync);

    private async Task<ValidationListSnapshot> ReadShownAsync() =>
        new([.. await Task.WhenAll((await Issues.AllAsync()).Select(async issue => new ValidationLine(
            await issue.GetAttributeAsync("data-severity") == "warning",
            (await issue.Locator("[data-testid=issue-message]").InnerTextAsync()).Trim())))]);
}

/// <summary>What the Validation list shows: its lines, in order.</summary>
public sealed record ValidationListSnapshot(ImmutableArray<ValidationLine> Lines);

/// <summary>A line of the Validation list: a warning (!) or a note (i), and its text.</summary>
public sealed record ValidationLine(bool IsWarning, string Text);
