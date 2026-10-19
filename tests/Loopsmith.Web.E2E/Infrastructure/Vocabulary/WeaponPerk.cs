using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A weapon perk, as a step card names what it fired. A case is added when a test needs it.</summary>
[Union]
public partial record WeaponPerk
{
    public partial record OneForAll;
    public partial record StatsForAll;
    public partial record ReapersTithe;
    public partial record Hatchling;
    public partial record HatchlingEnhanced;
    public partial record Unrelenting;
    public partial record VeteransWisdom;
    public partial record RadiolariaTransposer;
    public partial record Demolitionist;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="WeaponPerk"/>: the only place those strings live.</summary>
public static class WeaponPerkShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this WeaponPerk value) =>
        value.Match(
            oneForAll => "One for All",
            statsForAll => "Stats for All",
            reapersTithe => "Reaper's Tithe",
            hatchling => "Hatchling",
            hatchlingEnhanced => "Hatchling (enhanced)",
            unrelenting => "Unrelenting",
            veteransWisdom => "Veteran's Wisdom",
            radiolariaTransposer => "Radiolaria Transposer",
            demolitionist => "Demolitionist",
            unlisted => unlisted.Shown);
}
