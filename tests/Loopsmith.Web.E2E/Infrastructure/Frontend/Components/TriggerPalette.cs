using System.Collections.Immutable;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>
/// The trigger panel (Components/TriggerPalette.razor): the step HUD (Super, abilities, weapons) and the picked one's row
/// of buttons ("🥊 hit", "💀 kill", "🔄 reload") with its "×" enemy count.
/// </summary>
public sealed class TriggerPalette(IPage page)
{
    private ILocator Panel => page.GetByRole(AriaRole.Region, new() { Name = "Next trigger", Exact = true });

    private ILocator Row => Panel.Locator("[data-testid=trig-actions]");

    private ILocator CountedButtons => Row.Locator("[data-testid=trigger-buttons]");

    private ILocator CountedCaptions => CountedButtons.Locator("[data-testid=trigger-caption]");

    private ILocator ApartCaptions => Row.Locator("[data-testid=trigger-buttons-apart] [data-testid=trigger-caption]");

    private ILocator TargetCount => Row.Locator("[data-action=target-count]");

    private ILocator Weapon(WeaponSlot slot) => Panel.Locator($"[data-action=trigger-weapon][aria-label$='({slot.ToShownText()})']");

    private ILocator Trigger(StepTrigger trigger) =>
        Row.Locator("[data-action=trigger]")
            .Filter(new() { Has = page.Locator("[data-testid=trigger-caption]").GetByText(trigger.ToShownText(), new() { Exact = true }) });

    /// <summary>Clicks the weapon in <paramref name="slot"/> in the step HUD: its row of buttons shows.</summary>
    public Task ClickWeaponAsync(WeaponSlot slot) => OnScreen.ActAsync(Panel, Weapon(slot), weapon => weapon.ClickAsync());

    /// <summary>Selects the "×" enemy count whose text is <paramref name="shown"/> ("3 in 3 s · One for All").</summary>
    public Task SelectTargetCountAsync(string shown) =>
        OnScreen.ActAsync(Panel, TargetCount, count => count.SelectOptionAsync(new SelectOptionValue { Label = shown }));

    /// <summary>Clicks <paramref name="trigger"/> in the picked row: the loop gets a step.</summary>
    public Task ClickAsync(StepTrigger trigger) => OnScreen.ActAsync(Panel, Trigger(trigger), button => button.ClickAsync());

    /// <summary>Waits until the picked weapon's or ability's row of buttons shows.</summary>
    public Task WaitUntilRowShownAsync() => CountedButtons.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    /// <summary>What the picked row shows now.</summary>
    public Task<TriggerPaletteSnapshot> ReadAsync() => OnScreen.ReadAsync(Panel, ReadShownAsync);

    private async Task<TriggerPaletteSnapshot> ReadShownAsync() =>
        new(
            [.. (await CountedCaptions.AllInnerTextsAsync()).Select(shown => StepTriggerShowing.ReadStepTrigger(shown.Trim()))],
            await TargetCount.CountAsync() > 0,
            [.. (await ApartCaptions.AllInnerTextsAsync()).Select(shown => StepTriggerShowing.ReadStepTrigger(shown.Trim()))]);
}

/// <summary>
/// What the picked row shows: the buttons the "×" enemy count goes with, whether that count shows, and the buttons at the
/// row's right end, apart from it.
/// </summary>
public sealed record TriggerPaletteSnapshot(ImmutableArray<StepTrigger> WithTheCount, bool ShowsTheCount, ImmutableArray<StepTrigger> ApartFromTheCount);
