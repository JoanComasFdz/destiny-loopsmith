using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A weapon, as the trigger panel and the step cards name it. A case is added when a test needs it.</summary>
[Union]
public partial record Weapon
{
    public partial record FestivalFlight;
    public partial record OgmaPr6;
    public partial record BellowingGiant;
    public partial record CruxCeleritasIv;
    public partial record Scathelocke;
    public partial record Speleologist;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Weapon"/>: the only place those strings live.</summary>
public static class WeaponShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this Weapon value) =>
        value.Match(
            festivalFlight => "Festival Flight",
            ogmaPr6 => "Ogma PR6",
            bellowingGiant => "Bellowing Giant",
            cruxCeleritasIv => "Crux Celeritas IV",
            scathelocke => "Scathelocke",
            speleologist => "Speleologist",
            unlisted => unlisted.Shown);
}
