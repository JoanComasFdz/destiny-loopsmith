using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>
/// A secondary use of an ability, as its caption under the ability's square in the trigger panel names it (ADRs D9). A
/// case is added when a test needs it.
/// </summary>
[Union]
public partial record SecondaryUse
{
    public partial record Slide;
    public partial record Air;
    public partial record Combo;
    public partial record Guard;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="SecondaryUse"/>: the only place those strings live.</summary>
public static class SecondaryUseShowing
{
    /// <summary>What the UI shows for <paramref name="value"/> (in capitals, as the panel draws it).</summary>
    public static string ToShownText(this SecondaryUse value) =>
        value.Match(
            slide => "SLIDE",
            air => "AIR",
            combo => "COMBO",
            guard => "GUARD",
            unlisted => unlisted.Shown);
}
