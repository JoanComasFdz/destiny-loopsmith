using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>
/// One step's card in the loop (Components/LoopTimeline.razor): its heading, what it fired (Components/OutcomeTree.razor:
/// each event, the elements that fired from it and their lines, with "(chance)"; the "why:" reasons when the "why"
/// checkbox is ticked) and its "State after #N" (Components/StepStateHud.razor).
/// </summary>
public sealed class StepCard(IPage page, int number)
{
    private ILocator Card => page.Locator($"[data-testid=loop-timeline] [data-testid=step][data-step='{number}']");

    private ILocator Reasons => Card.Locator("[data-testid=outcome-reason]");

    /// <summary>Waits until the card shows its "why:" reasons (after the "why" checkbox is ticked).</summary>
    public Task WaitUntilReasonsShownAsync() => Reasons.First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>What the card shows now.</summary>
    public Task<StepCardSnapshot> ReadAsync() => OnScreen.ReadAsync(Card, ReadShownAsync);

    private async Task<StepCardSnapshot> ReadShownAsync()
    {
        var shown = (await Card.EvaluateAsync<JsonElement>(ReadScript)).Deserialize<ShownCard>(ShownJson.Options)!;
        return new StepCardSnapshot(
            shown.Heading,
            [.. shown.Events.Select(e => new FiredEvent(e.Text, [.. e.Sources.Select(s => new FiredSource(s.Name, [.. s.Lines], [.. s.Reasons]))]))],
            shown.NothingTriggers,
            new StepState(ToChips(shown.Buffs), ToChips(shown.Weapons), ToChips(shown.Debuffs), ToChips(shown.Ground)));
    }

    private static ImmutableArray<StateChip> ToChips(ShownChip[] chips) => [.. chips.Select(chip => new StateChip(chip.Text, chip.Tooltip))];

    // The card's texts as the user reads them, white space collapsed. A chip reads as its parts (its name, how many
    // effects it switches on, its weapons) joined by " · " ("One for All · Festival Flight"), as the card draws them
    // apart, and its tooltip is its title.
    private const string ReadScript = """
        card => {
          const text = el => (el ? el.innerText : '').replace(/\s+/g, ' ').trim();
          const chips = group => [...card.querySelectorAll(`[data-testid=step-state] [data-group=${group}] [data-testid=state-chip]`)].map(chip => ({
            text: [chip.querySelector('[data-testid=state-chip-name]'), chip.querySelector('[data-testid=switches-on]'), ...chip.querySelectorAll('[data-testid=buff-weapon]')]
              .map(text).filter(part => part.length > 0).join(' · '),
            tooltip: chip.getAttribute('title') || '',
          }));
          return {
            heading: `${text(card.querySelector('[data-testid=step-no]'))} ${text(card.querySelector('[data-testid=step-title]'))}`,
            events: [...card.querySelectorAll('[data-testid=outcome-event]')].map(event => ({
              text: text(event.querySelector('[data-testid=outcome-event-head]')),
              sources: [...event.querySelectorAll('[data-testid=outcome-source]')].map(source => ({
                name: text(source.querySelector('[data-testid=outcome-element-name]')),
                lines: [...source.querySelectorAll('[data-testid=outcome-line]')].map(text),
                reasons: [...source.querySelectorAll('[data-testid=outcome-reason]')].map(text),
              })),
            })),
            nothingTriggers: card.querySelector('[data-testid=outcome-none]') !== null,
            buffs: chips('buffs'),
            weapons: chips('weapons'),
            debuffs: chips('debuffs'),
            ground: chips('ground'),
          };
        }
        """;

    private sealed record ShownCard(string Heading, ShownEvent[] Events, bool NothingTriggers, ShownChip[] Buffs, ShownChip[] Weapons, ShownChip[] Debuffs, ShownChip[] Ground);

    private sealed record ShownEvent(string Text, ShownSource[] Sources);

    private sealed record ShownSource(string Name, string[] Lines, string[] Reasons);

    private sealed record ShownChip(string Text, string Tooltip);
}

/// <summary>
/// What a step card shows: its heading ("#1 Festival Flight (🥊 hit)"), the events it fired with what fired from each,
/// whether it reads "nothing triggers", and its "State after #N".
/// </summary>
public sealed record StepCardSnapshot(string Heading, ImmutableArray<FiredEvent> Events, bool NothingTriggers, StepState State);

/// <summary>An event the step fired ("Festival Flight 🥊 hit 1 enemy") and the elements that fired from it.</summary>
public sealed record FiredEvent(string Text, ImmutableArray<FiredSource> Sources);

/// <summary>An element that fired ("One for All"), its lines ("One for All (10s) (chance)") and its "why:" reasons.</summary>
public sealed record FiredSource(string Name, ImmutableArray<string> Lines, ImmutableArray<string> Reasons);

/// <summary>The card's "State after #N": the chips of each group.</summary>
public sealed record StepState(ImmutableArray<StateChip> Buffs, ImmutableArray<StateChip> Weapons, ImmutableArray<StateChip> Debuffs, ImmutableArray<StateChip> Ground);

/// <summary>A chip of the state ("One for All · Festival Flight") and its tooltip ("10s").</summary>
public sealed record StateChip(string Text, string Tooltip);
