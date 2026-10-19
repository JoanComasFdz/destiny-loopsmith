using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>
/// An exotic weapon's own perk (its intrinsic or its trait: rules/exotics/weapons.yaml), as a step card names what it
/// fired. A case is added when a test needs it.
/// </summary>
[Union]
public partial record ExoticWeaponPerk
{
    public partial record ReignHavoc;
    public partial record LightningRounds;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="ExoticWeaponPerk"/>: the only place those strings live.</summary>
public static class ExoticWeaponPerkShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this ExoticWeaponPerk value) =>
        value.Match(
            reignHavoc => "Reign Havoc",
            lightningRounds => "Lightning Rounds",
            unlisted => unlisted.Shown);
}
