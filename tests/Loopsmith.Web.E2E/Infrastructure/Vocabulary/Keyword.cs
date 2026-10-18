using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>
/// A keyword (a status or a pickup every build has: rules/keywords/), as the trigger panel and a step card name it. A
/// case is added when a test needs it.
/// </summary>
[Union]
public partial record Keyword
{
    public partial record Jolt;
    public partial record BoltCharge;
    public partial record IonicTrace;
    public partial record Amplified;
    public partial record Super;
    public partial record OrbOfPower;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Keyword"/>: the only place those strings live.</summary>
public static class KeywordShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this Keyword value) =>
        value.Match(
            jolt => "Jolt",
            boltCharge => "Bolt Charge",
            ionicTrace => "Ionic Trace",
            amplified => "Amplified",
            super => "Super",
            orbOfPower => "Orb of Power",
            unlisted => unlisted.Shown);
}
