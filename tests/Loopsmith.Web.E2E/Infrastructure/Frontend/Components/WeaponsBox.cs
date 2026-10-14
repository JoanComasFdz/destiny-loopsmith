using System.Collections.Immutable;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The Weapons box of the designer's loadout (Components/LoadoutPanel.razor).</summary>
public sealed class WeaponsBox(IPage page)
{
    private ILocator Slots => page.Locator("[data-testid=weapon-slot]");

    /// <summary>What the Weapons box shows now.</summary>
    public async Task<WeaponsBoxSnapshot> ReadAsync()
    {
        var slots = await Slots.EvaluateAllAsync<SlotShown[]>("rows => rows.map(row => ({ slot: row.dataset.slot ?? '', empty: row.dataset.empty === 'true' }))");
        return new WeaponsBoxSnapshot(
            [.. slots.Where(shown => !shown.Empty).Select(shown => WeaponSlotShowing.ReadWeaponSlot(shown.Slot))],
            [.. slots.Where(shown => shown.Empty).Select(shown => WeaponSlotShowing.ReadWeaponSlot(shown.Slot))]);
    }

    // One slot as the page answers it (Playwright fills it through a parameterless constructor).
    private sealed class SlotShown
    {
        public string Slot { get; init; } = "";

        public bool Empty { get; init; }
    }
}

/// <summary>What the Weapons box shows: the slots holding a weapon and the empty ones.</summary>
public sealed record WeaponsBoxSnapshot(ImmutableArray<WeaponSlot> Equipped, ImmutableArray<WeaponSlot> Empty);
