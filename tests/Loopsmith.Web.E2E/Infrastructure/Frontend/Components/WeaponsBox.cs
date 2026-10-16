using System.Collections.Immutable;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>The Weapons box of the designer's loadout (Components/LoadoutPanel.razor).</summary>
public sealed class WeaponsBox(IPage page)
{
    private ILocator Box => page.GetByRole(AriaRole.Region, new() { Name = "Weapons", Exact = true });

    private ILocator Slots => Box.Locator("[data-testid=weapon-slot]");

    /// <summary>What the Weapons box shows now.</summary>
    public Task<WeaponsBoxSnapshot> ReadAsync() => OnScreen.ReadAsync(Box, ReadShownAsync);

    private async Task<WeaponsBoxSnapshot> ReadShownAsync()
    {
        var slots = await Slots.EvaluateAllAsync<SlotShown[]>("""
            rows => rows.map(row => ({
                slot: row.dataset.slot ?? '',
                empty: row.dataset.empty === 'true',
                name: row.querySelector('[data-testid=weapon-name]')?.textContent?.trim() ?? '',
                damage: row.querySelector('[data-testid=damage-type]')?.dataset.damage ?? '',
            }))
            """);
        return new WeaponsBoxSnapshot(
            [.. slots.Where(shown => !shown.Empty).Select(shown => WeaponSlotShowing.ReadWeaponSlot(shown.Slot))],
            [.. slots.Where(shown => shown.Empty).Select(shown => WeaponSlotShowing.ReadWeaponSlot(shown.Slot))],
            [.. slots.Where(shown => !shown.Empty).Select(shown => new WornWeapon(
                WeaponSlotShowing.ReadWeaponSlot(shown.Slot),
                WeaponShowing.ReadWeapon(shown.Name),
                DamageTypeShowing.ReadDamageType(shown.Damage)))]);
    }

    // One slot as the page answers it (Playwright fills it through a parameterless constructor).
    private sealed class SlotShown
    {
        public string Slot { get; init; } = "";

        public bool Empty { get; init; }

        public string Name { get; init; } = "";

        public string Damage { get; init; } = "";
    }
}

/// <summary>What the Weapons box shows: the slots holding a weapon and the empty ones, and each weapon it holds.</summary>
public sealed record WeaponsBoxSnapshot(ImmutableArray<WeaponSlot> Equipped, ImmutableArray<WeaponSlot> Empty, ImmutableArray<WornWeapon> Weapons);

/// <summary>A weapon the Weapons box shows: its slot, its name and the damage type its square shows.</summary>
public sealed record WornWeapon(WeaponSlot Slot, Weapon Weapon, DamageType Damage);
