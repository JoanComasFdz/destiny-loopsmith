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

    private ILocator RowTitle => Row.Locator("[data-testid=trig-actions-title]");

    private ILocator CountedButtons => Row.Locator("[data-testid=trigger-buttons]");

    private ILocator CountedCaptions => CountedButtons.Locator("[data-testid=trigger-caption]");

    private ILocator ApartCaptions => Row.Locator("[data-testid=trigger-buttons-apart] [data-testid=trigger-caption]");

    private ILocator TargetCount => Row.Locator("[data-action=target-count]");

    private ILocator TargetCountOptions => TargetCount.GetByRole(AriaRole.Option);

    // A weapon's button is named "<its name> (<its slot>)".
    private ILocator WeaponButton(Weapon weapon) =>
        Panel.Locator("[data-action=trigger-weapon]")
            .And(page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex($"^{Regex.Escape(weapon.ToShownText())} \\(") }));

    // An ability's square is named "<its kind>: <its name>" ("Melee: Combination Blow").
    private ILocator AbilityButton(Ability ability) =>
        Panel.Locator("[data-action=trigger-ability]")
            .And(page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex($"^[^:]+: {Regex.Escape(ability.ToShownText())}$") }));

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

    /// <summary>Selects the "×" enemy count whose text is <paramref name="shown"/> ("3 in 3 s · One for All").</summary>
    public Task SelectTargetCountAsync(string shown) =>
        OnScreen.ActAsync(Panel, TargetCount, count => count.SelectOptionAsync(new SelectOptionValue { Label = shown }));

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

    /// <summary>What the picked row shows now.</summary>
    public Task<TriggerPaletteSnapshot> ReadAsync() => OnScreen.ReadAsync(Panel, ReadShownAsync);

    private async Task<TriggerPaletteSnapshot> ReadShownAsync() =>
        new(
            [.. (await CountedCaptions.AllInnerTextsAsync()).Select(shown => StepTriggerShowing.ReadStepTrigger(shown.Trim()))],
            await TargetCount.CountAsync() > 0,
            [.. (await TargetCountOptions.AllInnerTextsAsync()).Select(shown => shown.Trim())],
            [.. (await ApartCaptions.AllInnerTextsAsync()).Select(shown => StepTriggerShowing.ReadStepTrigger(shown.Trim()))]);

    // The row's box and its content's right edge (its right padding left out), each button's box with its caption, the
    // "×" enemy count's box, the screen's width and the page's (wider when it scrolls sideways).
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
            count: row.querySelector('[data-testid=target-count]') ? box(row.querySelector('[data-testid=target-count]')) : null,
            screenWidth: window.innerWidth,
            pageWidth: document.documentElement.scrollWidth,
          };
        }
        """;
}

/// <summary>
/// What the picked row shows: the buttons the "×" enemy count goes with, whether that count shows, the counts it offers
/// (as its options read, in order), and the buttons at the row's right end, apart from it.
/// </summary>
public sealed record TriggerPaletteSnapshot(
    ImmutableArray<StepTrigger> WithTheCount,
    bool ShowsTheCount,
    ImmutableArray<string> TargetCounts,
    ImmutableArray<StepTrigger> ApartFromTheCount);

/// <summary>
/// Where the picked row is on the screen, in CSS pixels: the row's box and its content's right edge, each button's box
/// with its caption, the "×" enemy count's box (none when the row has no count), the screen's width and the page's.
/// </summary>
public sealed record TriggerRowLayout(ScreenBox Row, double ContentRight, ImmutableArray<TriggerButtonBox> Buttons, ScreenBox? Count, double ScreenWidth, double PageWidth);

/// <summary>A button of the row and its box.</summary>
public sealed record TriggerButtonBox(string Caption, ScreenBox Box);

/// <summary>A box on the screen, in CSS pixels from the window's top left.</summary>
public sealed record ScreenBox(double Left, double Top, double Right, double Bottom);
