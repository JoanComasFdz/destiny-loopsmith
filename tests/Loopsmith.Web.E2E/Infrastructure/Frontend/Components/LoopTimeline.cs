using System.Collections.Immutable;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The designed loop, one card per step after the fresh spawn (Components/LoopTimeline.razor).</summary>
public sealed class LoopTimeline(IPage page)
{
    private ILocator Box => page.Locator("[data-testid=loop-timeline]");

    private ILocator Steps => Box.Locator("[data-testid=step]");

    private ILocator Tokens => Steps.Locator("[data-testid=step-token]");

    private ILocator StepBox(int number) => Box.Locator($"[data-testid=step][data-step='{number}']");

    private ILocator RemoveButton(int number) => StepBox(number).Locator("[data-action=step-remove]");

    /// <summary>Step <paramref name="number"/>'s card (#1 is the first after the fresh spawn).</summary>
    public StepCard Step(int number) => new(page, number);

    /// <summary>Waits until the loop shows step <paramref name="number"/> (#1 is the first after the fresh spawn).</summary>
    public Task WaitUntilStepShownAsync(int number) => StepBox(number).WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>Waits until the loop shows no step after the fresh spawn.</summary>
    public Task WaitUntilNoStepsAsync() => Steps.First.WaitForAsync(new() { State = WaitForSelectorState.Detached });

    /// <summary>Clicks the "✕" at the top right of step <paramref name="number"/>'s card ("Remove this step").</summary>
    public Task ClickRemoveStepAsync(int number) => OnScreen.PressAsync(StepBox(number), RemoveButton(number));

    /// <summary>What the loop shows now.</summary>
    public Task<LoopTimelineSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    private async Task<LoopTimelineSnapshot> ReadShownAsync() =>
        new([.. (await Tokens.AllInnerTextsAsync()).Select(token => LoopStepShowing.ReadLoopStep(token.Trim()))]);
}

/// <summary>What the loop shows: its steps after the fresh spawn, in order.</summary>
public sealed record LoopTimelineSnapshot(ImmutableArray<LoopStep> Steps);
