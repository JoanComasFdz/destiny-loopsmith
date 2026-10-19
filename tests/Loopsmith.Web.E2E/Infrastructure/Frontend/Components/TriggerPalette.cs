using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>
/// The trigger panel (Components/TriggerPalette.razor): the step HUD (Super, abilities with their secondary uses, weapons)
/// and the picked one's row of buttons ("🥊 hit", "💀 kill", "🔄 reload") with its "×" enemy count.
/// </summary>
public sealed class TriggerPalette(IPage page)
{
    private ILocator Panel => page.GetByRole(AriaRole.Region, new() { Name = "Next trigger", Exact = true });

    private ILocator Row => Panel.Locator("[data-testid=trig-actions]");

    private ILocator DisabledNote => Panel.Locator("[data-testid=palette-disabled]");

    private ILocator RowTitle => Row.Locator("[data-testid=trig-actions-title]");

    private ILocator CountedButtons => Row.Locator("[data-testid=trigger-buttons]");

    private ILocator CountedCaptions => CountedButtons.Locator("[data-testid=trigger-caption]");

    private ILocator ApartCaptions => Row.Locator("[data-testid=trigger-buttons-apart] [data-testid=trigger-caption]");

    private ILocator DisabledCaptions => Row.Locator("[data-action=trigger]:disabled [data-testid=trigger-caption]");

    private ILocator TargetCount => Row.Locator("[data-action=target-count]");

    private ILocator TargetCountOptions => TargetCount.GetByRole(AriaRole.Option);

    private ILocator OptionCaptions => Row.Locator("[data-action=trigger-option] [data-testid=trigger-option-caption]");

    private ILocator RowOptionBox(RowOption option) =>
        Row.Locator("[data-action=trigger-option]")
            .Filter(new() { Has = page.Locator("[data-testid=trigger-option-caption]").GetByText(option.ToShownText(), new() { Exact = true }) });

    // A weapon's button is named "<its name> (<its slot>)".
    private ILocator WeaponButton(Weapon weapon) =>
        Panel.Locator("[data-action=trigger-weapon]")
            .And(page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex($"^{Regex.Escape(weapon.ToShownText())} \\(") }));

    // An ability's square is named "<its kind>: <its name>" ("Melee: Combination Blow"); the finisher's is named for the
    // equipped finisher (after a Bungie login), so it is found by its HUD slot.
    private ILocator AbilityButton(Ability ability) =>
        ability is Ability.Finisher
            ? Panel.Locator("[data-action=trigger-ability][data-hud-slot=finisher]")
            : Panel.Locator("[data-action=trigger-ability]")
                .And(page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex($"^[^:]+: {Regex.Escape(ability.ToShownText())}$") }));

    // A status's chip in the panel's "On you" group ("Bolt Charge", with its "at max (x10)" and "ends"), and one of its
    // declarations by its caption.
    private ILocator Declaration(Keyword status, string declaration) =>
        Panel.Locator("[data-testid=status-chip]")
            .Filter(new() { Has = page.Locator("[data-testid=status-name]").GetByText(status.ToShownText(), new() { Exact = true }) })
            .Locator("[data-action=trigger-at-once]")
            .Filter(new() { Has = page.Locator("[data-testid=trigger-caption]").GetByText(declaration, new() { Exact = true }) });

    // The panel's "On you" group: the buffs on you, each a chip with its declarations, and the states to declare beside them.
    private ILocator OnYou => Panel.Locator("[data-testid=trig-group][data-group=you]");

    // A state to declare in the "On you" group, outside the status chips ("Critical health"), by its caption.
    private ILocator State(Keyword state) =>
        OnYou.Locator(":scope > [data-action=trigger-at-once]")
            .Filter(new() { Has = page.Locator("[data-testid=trigger-caption]").GetByText(state.ToShownText(), new() { Exact = true }) });

    // A pickup's chip in the panel's "On the ground" group reads "Pick up <its name>".
    private ILocator Pickup(Keyword pickup) =>
        Panel.Locator("[data-testid=trig-group][data-group=ground] [data-action=trigger-at-once]")
            .Filter(new() { Has = page.Locator("[data-testid=trigger-caption]").GetByText($"Pick up {pickup.ToShownText()}", new() { Exact = true }) });

    // A secondary use's caption is drawn in capitals ("SLIDE"); its text may not be.
    private ILocator SecondaryButton(SecondaryUse use) =>
        Panel.Locator("[data-action=trigger-secondary]")
            .Filter(new() { Has = page.Locator("[data-testid=secondary-caption]").GetByText(new Regex($"^{Regex.Escape(use.ToShownText())}$", RegexOptions.IgnoreCase)) });

    private ILocator Trigger(StepTrigger trigger) =>
        Row.Locator("[data-action=trigger]")
            .Filter(new() { Has = page.Locator("[data-testid=trigger-caption]").GetByText(trigger.ToShownText(), new() { Exact = true }) });

    /// <summary>Clicks <paramref name="weapon"/> in the step HUD: its row of buttons shows.</summary>
    public Task ClickWeaponAsync(Weapon weapon) => OnScreen.PressAsync(Panel, WeaponButton(weapon));

    /// <summary>
    /// Clicks <paramref name="ability"/>'s square in the step HUD: its row of buttons shows, or, for an ability with one
    /// action (a class ability), the loop gets a step.
    /// </summary>
    public Task ClickAbilityAsync(Ability ability) => OnScreen.PressAsync(Panel, AbilityButton(ability));

    /// <summary>
    /// Clicks <paramref name="use"/> under its ability's square ("SLIDE", "AIR"): its row of buttons shows, or, for a use
    /// with one action (the air move), the loop gets a step.
    /// </summary>
    public Task ClickSecondaryAsync(SecondaryUse use) => OnScreen.PressAsync(Panel, SecondaryButton(use));

    /// <summary>
    /// Clicks <paramref name="declaration"/> ("at max (x10)", "ends") on <paramref name="status"/>'s chip under "On you": the
    /// loop gets a step that declares it.
    /// </summary>
    public Task ClickDeclarationAsync(Keyword status, string declaration) => OnScreen.PressAsync(Panel, Declaration(status, declaration));

    /// <summary>Clicks <paramref name="state"/> ("Critical health") under "On you", outside the status chips: the loop gets a step that declares it.</summary>
    public Task ClickStateAsync(Keyword state) => OnScreen.PressAsync(Panel, State(state));

    /// <summary>Clicks "Pick up <paramref name="pickup"/>" under "On the ground": the loop gets a step that picks it up.</summary>
    public Task ClickPickupAsync(Keyword pickup) => OnScreen.PressAsync(Panel, Pickup(pickup));

    /// <summary>Selects the "×" enemy count whose text is <paramref name="shown"/> ("3 in 3 s · One for All").</summary>
    public Task SelectTargetCountAsync(string shown) =>
        OnScreen.ActAsync(Panel, TargetCount, count => count.SelectOptionAsync(new SelectOptionValue { Label = shown }));

    /// <summary>Ticks <paramref name="option"/>'s box in the picked row ("🎯 precision"): its next steps carry it.</summary>
    public Task TickOptionAsync(RowOption option) => OnScreen.PressAsync(Panel, RowOptionBox(option));

    /// <summary>Clicks <paramref name="trigger"/> in the picked row: the loop gets a step.</summary>
    public Task ClickAsync(StepTrigger trigger) => OnScreen.PressAsync(Panel, Trigger(trigger));

    /// <summary>Waits until the picked row is <paramref name="weapon"/>'s: its title names it, its buttons show.</summary>
    public async Task WaitUntilRowShownAsync(Weapon weapon)
    {
        await RowTitle.GetByText(weapon.ToShownText(), new() { Exact = true }).WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await CountedButtons.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>Waits until the picked row is <paramref name="ability"/>'s: its title names it, its buttons show.</summary>
    public Task WaitUntilRowShownAsync(Ability ability) => WaitUntilRowShownAsync(ability.ToShownText());

    /// <summary>Waits until the picked row is the one titled <paramref name="title"/> ("Combination Blow", "Sliding melee · Tempest Strike"): its buttons show.</summary>
    public async Task WaitUntilRowShownAsync(string title)
    {
        await RowTitle.GetByText(title, new() { Exact = true }).WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await CountedButtons.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>Where the picked row's buttons and its "×" enemy count are on the screen, and how wide the screen and the page are.</summary>
    public Task<TriggerRowLayout> ReadLayoutAsync() => OnScreen.ReadAsync(Row, ReadLayoutShownAsync);

    private async Task<TriggerRowLayout> ReadLayoutShownAsync() =>
        (await Row.EvaluateAsync<JsonElement>(LayoutScript)).Deserialize<TriggerRowLayout>(ShownJson.Options)!;

    /// <summary>What the panel shows now: the picked row, and whether the panel is greyed out.</summary>
    public Task<TriggerPaletteSnapshot> ReadAsync() => OnScreen.ReadAsync(Panel, ReadShownAsync);

    private async Task<TriggerPaletteSnapshot> ReadShownAsync() =>
        new(
            [.. (await CountedCaptions.AllInnerTextsAsync()).Select(shown => StepTriggerShowing.ReadStepTrigger(shown.Trim()))],
            await TargetCount.CountAsync() > 0,
            [.. (await TargetCountOptions.AllInnerTextsAsync()).Select(shown => shown.Trim())],
            [.. (await ApartCaptions.AllInnerTextsAsync()).Select(shown => StepTriggerShowing.ReadStepTrigger(shown.Trim()))],
            [.. (await DisabledCaptions.AllInnerTextsAsync()).Select(shown => StepTriggerShowing.ReadStepTrigger(shown.Trim()))],
            [.. (await OptionCaptions.AllInnerTextsAsync()).Select(shown => RowOptionShowing.ReadRowOption(shown.Trim()))],
            await Panel.GetAttributeAsync("aria-disabled") == "true",
            await DisabledNote.CountAsync() > 0 ? (await DisabledNote.InnerTextAsync()).Trim() : "",
            await ReadOnYouAsync());

    private async Task<ImmutableArray<OnYouChip>> ReadOnYouAsync() =>
        await OnYou.CountAsync() == 0
            ? []
            : [.. (await OnYou.EvaluateAsync<JsonElement>(OnYouScript)).Deserialize<ImmutableArray<OnYouChip>>(ShownJson.Options)];

    // Each chip of the "On you" group, in order: a status's name with its buttons' captions, or a state to declare's caption
    // with no buttons (it is a button itself).
    private const string OnYouScript = """
        group => [...group.children]
          .filter(el => el.matches('[data-testid=status-chip], [data-action=trigger-at-once]'))
          .map(el => el.matches('[data-testid=status-chip]')
            ? { name: el.querySelector('[data-testid=status-name]').innerText.trim(), isButton: false,
                buttons: [...el.querySelectorAll('[data-testid=trigger-caption]')].map(c => c.innerText.trim()) }
            : { name: el.querySelector('[data-testid=trigger-caption]').innerText.trim(), isButton: true, buttons: [] })
        """;

    // The row's box and its content's right edge (its right padding left out), each button's box with its caption, each
    // option's box with its caption, the "×" enemy count's box, the screen's width and the page's (wider when it scrolls
    // sideways).
    private const string LayoutScript = """
        row => {
          const box = el => { const r = el.getBoundingClientRect(); return { left: r.left, top: r.top, right: r.right, bottom: r.bottom }; };
          const bounds = box(row);
          return {
            row: bounds,
            contentRight: bounds.right - parseFloat(getComputedStyle(row).paddingRight),
            buttons: [...row.querySelectorAll('[data-action=trigger]')].map(button => ({
              caption: button.querySelector('[data-testid=trigger-caption]').innerText.trim(),
              box: box(button),
            })),
            options: [...row.querySelectorAll('[data-action=trigger-option]')].map(option => ({
              caption: option.querySelector('[data-testid=trigger-option-caption]').innerText.trim(),
              box: box(option),
            })),
            count: row.querySelector('[data-testid=target-count]') ? box(row.querySelector('[data-testid=target-count]')) : null,
            screenWidth: window.innerWidth,
            pageWidth: document.documentElement.scrollWidth,
          };
        }
        """;
}

/// <summary>
/// What the panel shows: the picked row's buttons the "×" enemy count goes with, whether that count shows, the counts it
/// offers (as its options read, in order), the buttons at the row's right end, apart from it, the buttons shown but
/// disabled (no rule of the build reacts to them), and the boxes the player ticks (its options); whether the whole panel
/// is greyed out (nothing in it can be tapped), and the line that says why (empty when it isn't); and the chips of its
/// "On you" group, in order.
/// </summary>
public sealed record TriggerPaletteSnapshot(
    ImmutableArray<StepTrigger> WithTheCount,
    bool ShowsTheCount,
    ImmutableArray<string> TargetCounts,
    ImmutableArray<StepTrigger> ApartFromTheCount,
    ImmutableArray<StepTrigger> Disabled,
    ImmutableArray<RowOption> Options,
    bool IsGreyedOut,
    string DisabledNote,
    ImmutableArray<OnYouChip> OnYou);

/// <summary>
/// A chip of the "On you" group: a buff on you, by its name, with its buttons' captions ("at max (x10)", "ends"); or a
/// state to declare, by its caption, which is a button itself (<see cref="IsButton"/>, no buttons of its own).
/// </summary>
public sealed record OnYouChip(string Name, bool IsButton, ImmutableArray<string> Buttons);

/// <summary>
/// Where the picked row is on the screen, in CSS pixels: the row's box and its content's right edge, each button's box
/// with its caption, each option's box with its caption, the "×" enemy count's box (none when the row has no count), the
/// screen's width and the page's.
/// </summary>
public sealed record TriggerRowLayout(ScreenBox Row, double ContentRight, ImmutableArray<TriggerButtonBox> Buttons, ImmutableArray<TriggerButtonBox> Options, ScreenBox? Count, double ScreenWidth, double PageWidth);

/// <summary>A button or option box of the row, and its box.</summary>
public sealed record TriggerButtonBox(string Caption, ScreenBox Box);

/// <summary>A box on the screen, in CSS pixels from the window's top left.</summary>
public sealed record ScreenBox(double Left, double Top, double Right, double Bottom);
