using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>
/// One step's card in the loop (Components/LoopTimeline.razor): its heading, what it fired (Components/OutcomeTree.razor:
/// each event, the elements that fired from it and their lines, with "(chance)"; the "why:" reasons when the "why"
/// checkbox is ticked, the caveats when the "caveats" one is) and its "State after #N" (Components/StepStateHud.razor).
/// </summary>
public sealed class StepCard(IPage page, int number)
{
    private ILocator Card => page.Locator($"[data-testid=loop-timeline] [data-testid=step][data-step='{number}']");

    private ILocator Reasons => Card.Locator("[data-testid=outcome-reason]");

    private ILocator State => Card.Locator("[data-testid=step-state]");

    private ILocator Resistance => Card.Locator("[data-testid=resistance]");

    private ILocator ResistanceToggle => Resistance.Locator("[data-action=resistance-toggle]");

    private ILocator ResistanceSources => Resistance.Locator("[data-testid=resistance-sources]");

    private ILocator Bonus => Card.Locator("[data-testid=damage-bonus]");

    private ILocator BonusToggle => Bonus.Locator("[data-action=damage-bonus-toggle]");

    private ILocator BonusSources => Bonus.Locator("[data-testid=damage-bonus-sources]");

    /// <summary>Waits until the card shows its "why:" reasons (after the "why" checkbox is ticked).</summary>
    public Task WaitUntilReasonsShownAsync() => Reasons.First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>Clicks the "▸ DAMAGE RESISTANCE" title of the card's state: where each resistance comes from shows below its rows.</summary>
    public Task ClickDamageResistanceAsync() => OnScreen.PressAsync(Resistance, ResistanceToggle);

    /// <summary>Waits until the card's damage resistance shows where each of its resistances comes from.</summary>
    public Task WaitUntilResistanceSourcesShownAsync() => ResistanceSources.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>Clicks the "▸ DAMAGE BONUS" title of the card's state: where each damage bonus comes from shows below its rows.</summary>
    public Task ClickDamageBonusAsync() => OnScreen.PressAsync(Bonus, BonusToggle);

    /// <summary>Waits until the card's damage bonus shows where each of its bonuses comes from.</summary>
    public Task WaitUntilDamageBonusSourcesShownAsync() => BonusSources.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>
    /// Clicks the chip <paramref name="chip"/> ("Amplified · 1 effect", as the card reads it) of the state's
    /// <paramref name="group"/>: its details open in the item details popup.
    /// </summary>
    public Task ClickStateChipAsync(StateGroup group, string chip) => OnScreen.PressAsync(State, FindStateChip(group, chip));

    /// <summary>
    /// Clicks <paramref name="keyword"/> ("Stats for All") in the line <paramref name="line"/> ("Stats for All (11s)") that
    /// <paramref name="source"/> fired: its details open in the item details popup.
    /// </summary>
    public Task ClickFiredKeywordAsync(WeaponPerk keyword, string line, WeaponPerk source) =>
        OnScreen.PressAsync(Card, FindFiredKeyword(keyword.ToShownText(), line, source.ToShownText()));

    /// <summary>What the card shows now.</summary>
    public Task<StepCardSnapshot> ReadAsync() => OnScreen.ReadAsync(Card, ReadShownAsync);

    private async Task<StepCardSnapshot> ReadShownAsync()
    {
        var shown = (await Card.EvaluateAsync<JsonElement>(ReadScript)).Deserialize<ShownCard>(ShownJson.Options)!;
        return new StepCardSnapshot(
            shown.Heading,
            [.. shown.Events.Select(e => new FiredEvent(e.Text, [.. e.Sources.Select(s => new FiredSource(s.Name, [.. s.Lines], [.. s.LineIcons.Select(ToIcons)], [.. s.Reasons], [.. s.Caveats]))]))],
            shown.NothingTriggers,
            new StepState(ToChips(shown.Buffs), ToChips(shown.Weapons), ToChips(shown.Debuffs), ToChips(shown.Ground)),
            new DamageResistance(
                [.. shown.Resistance.Select(row => new ResistanceRow(row.Name, row.Pve, row.Pvp))],
                [.. shown.ResistanceSources.Select(source => new ResistanceSource(source.Name, source.Value))]),
            new DamageBonus(
                [.. shown.Bonus.Select(row => new DamageBonusRow(row.Name, row.Value))],
                [.. shown.BonusSources.Select(source => new DamageBonusSource(source.Name, source.Value))]));
    }

    // A chip by its name, the part of its text before the first " · " (its effects and weapons come after).
    private ILocator FindStateChip(StateGroup group, string chip) =>
        State.Locator($"[data-testid=state-group][data-group={ToGroupKey(group)}] [data-testid=state-chip]")
            .Filter(new() { Has = page.Locator("[data-testid=state-chip-name]").Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(chip.Split(" · ")[0])}$") }) });

    // A keyword by its name, in the line of that text under the element of that name. The line's text holds the white
    // space between its parts as the page writes it (a line break and an indent), and the keyword's preview while the
    // pointer is on it, so anything between the line's words matches a space.
    private ILocator FindFiredKeyword(string keyword, string line, string source) =>
        Card.Locator("[data-testid=outcome-source]")
            .Filter(new() { Has = page.Locator("[data-testid=outcome-element-name]").Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(source)}$") }) })
            .Locator("[data-testid=outcome-line]")
            .Filter(new() { HasTextRegex = new Regex($@"^\s*{Regex.Escape(line).Replace("\\ ", @"[\s\S]+?", StringComparison.Ordinal)}\s*$") })
            .GetByRole(AriaRole.Button, new() { Name = keyword, Exact = true });

    private static string ToGroupKey(StateGroup group) =>
        group.Match(
            buffs => "buffs",
            weapons => "weapons",
            debuffs => "debuffs",
            ground => "ground",
            unlisted => unlisted.Shown);

    private static ImmutableArray<StateChip> ToChips(ShownChip[] chips) => [.. chips.Select(chip => new StateChip(chip.Text, ToIcons(chip.Icons)))];

    private static ImmutableArray<KeywordIcon> ToIcons(string[] icons) => [.. icons.Select(KeywordIconShowing.ReadKeywordIcon)];

    // The card's texts as the user reads them, white space collapsed. A chip reads as its parts (its name, how many
    // effects it switches on, its weapons) joined by " · " ("One for All · Festival Flight"), as the card draws them
    // apart. The damage resistance reads as its rows ("All damage", PvE, PvP) and, once its
    // title is clicked, the sources below them (each "<name>" and "<its values>"); the damage bonus the same way, its rows
    // ("Melee", "?") and, once its title is clicked, its sources ("Combination Blow", "?").
    private const string ReadScript = """
        card => {
          const text = el => (el ? el.innerText : '').replace(/\s+/g, ' ').trim();
          const icons = el => [...el.querySelectorAll('[data-testid=keyword-icon]')].map(icon => icon.getAttribute('src'));
          const chips = group => [...card.querySelectorAll(`[data-testid=step-state] [data-testid=state-group][data-group=${group}] [data-testid=state-chip]`)].map(chip => ({
            text: [chip.querySelector('[data-testid=state-chip-name]'), chip.querySelector('[data-testid=switches-on]'), ...chip.querySelectorAll('[data-testid=buff-weapon]')]
              .map(text).filter(part => part.length > 0).join(' · '),
            icons: icons(chip),
          }));
          return {
            heading: `${text(card.querySelector('[data-testid=step-no]'))} ${text(card.querySelector('[data-testid=step-title]'))}`,
            events: [...card.querySelectorAll('[data-testid=outcome-event]')].map(event => ({
              text: text(event.querySelector('[data-testid=outcome-event-head]')),
              sources: [...event.querySelectorAll('[data-testid=outcome-source]')].map(source => ({
                name: text(source.querySelector('[data-testid=outcome-element-name]')),
                lines: [...source.querySelectorAll('[data-testid=outcome-line]')].map(text),
                lineIcons: [...source.querySelectorAll('[data-testid=outcome-line]')].map(icons),
                reasons: [...source.querySelectorAll('[data-testid=outcome-reason]')].map(text),
                caveats: [...source.querySelectorAll('[data-testid=outcome-caveat]')].map(text),
              })),
            })),
            nothingTriggers: card.querySelector('[data-testid=outcome-none]') !== null,
            buffs: chips('buffs'),
            weapons: chips('weapons'),
            debuffs: chips('debuffs'),
            ground: chips('ground'),
            resistance: [...card.querySelectorAll('[data-testid=resistance] [data-testid=resistance-line]')].map(row => ({
              name: text(row.querySelector('[data-testid=resistance-line-name]')),
              pve: text(row.querySelector('[data-column=pve]')),
              pvp: text(row.querySelector('[data-column=pvp]')),
            })),
            resistanceSources: [...card.querySelectorAll('[data-testid=resistance-sources] [data-testid=resistance-source]')].map(source => ({
              name: text(source.querySelector('[data-testid=resistance-source-name]')),
              value: text(source.querySelector('[data-testid=resistance-source-value]')),
            })),
            bonus: [...card.querySelectorAll('[data-testid=damage-bonus] [data-testid=damage-bonus-line]')].map(row => ({
              name: text(row.querySelector('[data-testid=damage-bonus-line-name]')),
              value: text(row.querySelector('[data-testid=damage-bonus-line-value]')),
            })),
            bonusSources: [...card.querySelectorAll('[data-testid=damage-bonus-sources] [data-testid=damage-bonus-source]')].map(source => ({
              name: text(source.querySelector('[data-testid=damage-bonus-source-name]')),
              value: text(source.querySelector('[data-testid=damage-bonus-source-value]')),
            })),
          };
        }
        """;

    private sealed record ShownCard(
        string Heading, ShownEvent[] Events, bool NothingTriggers, ShownChip[] Buffs, ShownChip[] Weapons, ShownChip[] Debuffs, ShownChip[] Ground,
        ShownResistanceRow[] Resistance, ShownResistanceSource[] ResistanceSources, ShownBonus[] Bonus, ShownBonus[] BonusSources);

    private sealed record ShownBonus(string Name, string Value);

    private sealed record ShownResistanceRow(string Name, string Pve, string Pvp);

    private sealed record ShownResistanceSource(string Name, string Value);

    private sealed record ShownEvent(string Text, ShownSource[] Sources);

    private sealed record ShownSource(string Name, string[] Lines, string[][] LineIcons, string[] Reasons, string[] Caveats);

    private sealed record ShownChip(string Text, string[] Icons);
}

/// <summary>
/// What a step card shows: its heading ("#1 Festival Flight (🥊 hit)"), the events it fired with what fired from each,
/// whether it reads "nothing triggers", its "State after #N", its damage resistance and its damage bonus.
/// </summary>
public sealed record StepCardSnapshot(string Heading, ImmutableArray<FiredEvent> Events, bool NothingTriggers, StepState State, DamageResistance Resistance, DamageBonus Bonus);

/// <summary>An event the step fired ("Festival Flight 🥊 hit 1 enemy") and the elements that fired from it.</summary>
public sealed record FiredEvent(string Text, ImmutableArray<FiredSource> Sources);

/// <summary>
/// An element that fired ("One for All"), its lines ("One for All (10s) (chance)"), the keyword icons of each line, in
/// order (Void Overshield's before its name), its "why:" reasons and its caveats ("· +1 Bolt Charge: +1 from Spark of
/// Frequency", when the "caveats" checkbox is ticked).
/// </summary>
public sealed record FiredSource(
    string Name, ImmutableArray<string> Lines, ImmutableArray<ImmutableArray<KeywordIcon>> LineIcons, ImmutableArray<string> Reasons, ImmutableArray<string> Caveats);

/// <summary>The card's "State after #N": the chips of each group.</summary>
public sealed record StepState(ImmutableArray<StateChip> Buffs, ImmutableArray<StateChip> Weapons, ImmutableArray<StateChip> Debuffs, ImmutableArray<StateChip> Ground);

/// <summary>A chip of the state ("One for All · Festival Flight") and the keyword icon before its name (none when it has none).</summary>
public sealed record StateChip(string Text, ImmutableArray<KeywordIcon> Icons);

/// <summary>
/// The card's damage resistance: its rows ("All damage" and each against some combatants only) and, once its title is
/// clicked, the sources of each ("Arc Staff" "90% · PvP 53%"); none until then.
/// </summary>
public sealed record DamageResistance(ImmutableArray<ResistanceRow> Rows, ImmutableArray<ResistanceSource> Sources);

/// <summary>A row of the damage resistance: what it is against ("All damage"), its PvE and its PvP ("93.5%", "53%").</summary>
public sealed record ResistanceRow(string Name, string Pve, string Pvp);

/// <summary>A source of the damage resistance: its name ("Arc Staff") and its values ("90% · PvP 53%").</summary>
public sealed record ResistanceSource(string Name, string Value);

/// <summary>
/// The card's damage bonus: its rows (one per damage source a bonus makes harder) and, once its title is clicked, the
/// sources of each ("Combination Blow" "?"); none until then.
/// </summary>
public sealed record DamageBonus(ImmutableArray<DamageBonusRow> Rows, ImmutableArray<DamageBonusSource> Sources);

/// <summary>A row of the damage bonus: what it makes harder ("Melee") and each source's own value ("?", "+50% · +10%").</summary>
public sealed record DamageBonusRow(string Name, string Value);

/// <summary>A source of the damage bonus: its name ("Combination Blow") and its value ("?").</summary>
public sealed record DamageBonusSource(string Name, string Value);
