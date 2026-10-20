using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A weapon mod, as a step card names what it fired. A case is added when a test needs it.</summary>
[Union]
public partial record WeaponMod
{
    public partial record Synergy;
    public partial record SynergyEnhanced;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="WeaponMod"/>: the only place those strings live.</summary>
public static class WeaponModShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this WeaponMod value) =>
        value.Match(
            synergy => "Synergy",
            synergyEnhanced => "Synergy (enhanced)",
            unlisted => unlisted.Shown);
}
