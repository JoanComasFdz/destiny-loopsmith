using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The designed loop, one card per step after the fresh spawn (Components/LoopTimeline.razor).</summary>
public sealed class LoopTimeline(IPage page)
{
    private ILocator Steps => page.Locator("[data-testid=loop-timeline] [data-testid=step]");

    private ILocator Tokens => Steps.Locator("[data-testid=step-token]");

    private ILocator Step(int number) => page.Locator($"[data-testid=loop-timeline] [data-testid=step][data-step='{number}']");

    /// <summary>Waits until the loop shows step <paramref name="number"/> (#1 is the first after the fresh spawn).</summary>
    public Task WaitUntilStepShownAsync(int number) => Step(number).WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>What the loop shows now: each step's token, read with Core's own parser.</summary>
    public async Task<LoopTimelineSnapshot> ReadAsync() =>
        new([.. (await Tokens.AllInnerTextsAsync()).Select(token => ReadStep(token.Trim()))]);

    private static LoopStepShown ReadStep(string token) =>
        ActionTokenParsing.ParseActionToken(token) is Result<PlayerAction, string>.Ok parsed
            ? new LoopStepShown(token, parsed.Value)
            : new LoopStepShown(token, null);
}

/// <summary>What the loop shows: its steps after the fresh spawn, in order.</summary>
public sealed record LoopTimelineSnapshot(ImmutableArray<LoopStepShown> Steps);

/// <summary>A step as the loop shows it: its token and the action that token names (none when Core can't read it).</summary>
public sealed record LoopStepShown(string Token, PlayerAction? Action);
