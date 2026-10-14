using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The designed loop, one card per step after the fresh spawn (Components/LoopTimeline.razor).</summary>
public sealed class LoopTimeline(IPage page)
{
    private ILocator Steps => page.Locator("[data-testid=loop-timeline] [data-testid=step]");

    /// <summary>What the loop shows now.</summary>
    public async Task<LoopTimelineSnapshot> ReadAsync() => new(await Steps.CountAsync());
}

/// <summary>What the loop shows: how many steps follow the fresh spawn.</summary>
public sealed record LoopTimelineSnapshot(int StepCount);
