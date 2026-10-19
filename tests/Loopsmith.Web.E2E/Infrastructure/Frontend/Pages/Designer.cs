using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;

/// <summary>
/// The loop designer (Pages/Designer.razor): the build's loadout (Components/LoadoutPanel.razor: its Subclass, Artifact,
/// Weapons and Armor boxes), the trigger panel (Components/TriggerPalette.razor) and the loop (Components/LoopTimeline.razor).
/// </summary>
public sealed class Designer(IPage page)
{
    private ILocator BuildIdentity => page.Locator("[data-testid=build-identity]");

    private ILocator ShowReasons => page.Locator("[data-action=show-reasons]");

    /// <summary>The Subclass box: the class, the subclass, its super, abilities, aspects and fragments.</summary>
    public SubclassBox Subclass { get; } = new(page);

    /// <summary>The Artifact box: the artifact and its perks.</summary>
    public ArtifactBox Artifact { get; } = new(page);

    /// <summary>The Weapons box: one row per weapon slot.</summary>
    public WeaponsBox Weapons { get; } = new(page);

    /// <summary>The Armor box: one piece per armor slot.</summary>
    public ArmorBox Armor { get; } = new(page);

    /// <summary>The subclass editor, opened by ⋯ on the Subclass box.</summary>
    public SubclassEditor SubclassEditor { get; } = new(page);

    /// <summary>The Validation list under the build: its warnings and notes.</summary>
    public ValidationList Validation { get; } = new(page);

    /// <summary>The trigger panel: the step HUD and the picked one's buttons (Components/TriggerPalette.razor).</summary>
    public TriggerPalette TriggerPalette { get; } = new(page);

    /// <summary>The loop's steps (Components/LoopTimeline.razor).</summary>
    public LoopTimeline Loop { get; } = new(page);

    /// <summary>Ticks the "why" checkbox above the loop (it stays ticked when it already is): each step card shows its "why:" reasons.</summary>
    public Task TickWhyAsync() => OnScreen.ActAsync(ShowReasons, ShowReasons, box => box.SetCheckedAsync(true));

    /// <summary>Waits until the designer shows an open loop.</summary>
    public Task WaitUntilShownAsync() => BuildIdentity.WaitForAsync(new() { State = WaitForSelectorState.Visible });
}
