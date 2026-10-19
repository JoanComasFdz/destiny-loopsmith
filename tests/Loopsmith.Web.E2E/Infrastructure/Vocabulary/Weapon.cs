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
    public partial record CrisisInverted;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Weapon"/>: the only place those strings live.</summary>
public static class WeaponShowing
{
    private static readonly Weapon[] Listed =
        [
            new Weapon.FestivalFlight(), new Weapon.OgmaPr6(), new Weapon.BellowingGiant(), new Weapon.CruxCeleritasIv(), new Weapon.Scathelocke(),
            new Weapon.Speleologist(), new Weapon.CrisisInverted(),
        ];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this Weapon value) =>
        value.Match(
            festivalFlight => "Festival Flight",
            ogmaPr6 => "Ogma PR6",
            bellowingGiant => "Bellowing Giant",
            cruxCeleritasIv => "Crux Celeritas IV",
            scathelocke => "Scathelocke",
            speleologist => "Speleologist",
            crisisInverted => "Crisis Inverted",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="Weapon.Unlisted"/>.</summary>
    public static Weapon ReadWeapon(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new Weapon.Unlisted(shown);
}
