using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A weapon slot, as the designer's Weapons box names it. A case is added when a test needs it.</summary>
[Union]
public partial record WeaponSlot
{
    public partial record Kinetic;
    public partial record Energy;
    public partial record Power;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="WeaponSlot"/>: the only place those strings live.</summary>
public static class WeaponSlotShowing
{
    private static readonly WeaponSlot[] Listed = [new WeaponSlot.Kinetic(), new WeaponSlot.Energy(), new WeaponSlot.Power()];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this WeaponSlot value) =>
        value.Match(
            kinetic => "Kinetic",
            energy => "Energy",
            power => "Power",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="WeaponSlot.Unlisted"/>.</summary>
    public static WeaponSlot ReadWeaponSlot(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new WeaponSlot.Unlisted(shown);
}
