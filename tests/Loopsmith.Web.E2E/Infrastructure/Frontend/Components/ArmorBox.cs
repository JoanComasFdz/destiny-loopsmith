using System.Collections.Immutable;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The Armor box of the designer's loadout (Components/LoadoutPanel.razor).</summary>
public sealed class ArmorBox(IPage page)
{
    private ILocator Box => page.GetByRole(AriaRole.Region, new() { Name = "Armor", Exact = true });

    private ILocator Slots => Box.Locator("[data-testid=armor-slot]");

    /// <summary>What the Armor box shows now.</summary>
    public Task<ArmorBoxSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    private async Task<ArmorBoxSnapshot> ReadShownAsync()
    {
        var slots = await Slots.EvaluateAllAsync<SlotShown[]>("pieces => pieces.map(piece => ({ slot: piece.dataset.slot ?? '', empty: piece.dataset.empty === 'true' }))");
        return new ArmorBoxSnapshot(
            [.. slots.Where(shown => !shown.Empty).Select(shown => ArmorSlotShowing.ReadArmorSlot(shown.Slot))],
            [.. slots.Where(shown => shown.Empty).Select(shown => ArmorSlotShowing.ReadArmorSlot(shown.Slot))]);
    }

    // One slot as the page answers it (Playwright fills it through a parameterless constructor).
    private sealed class SlotShown
    {
        public string Slot { get; init; } = "";

        public bool Empty { get; init; }
    }
}

/// <summary>What the Armor box shows: the slots wearing a piece and the empty ones.</summary>
public sealed record ArmorBoxSnapshot(ImmutableArray<ArmorSlot> Worn, ImmutableArray<ArmorSlot> Empty);
