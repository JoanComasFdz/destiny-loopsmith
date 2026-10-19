using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A buff or debuff the trigger panel offers to declare ("On you": Bolt Charge). A case is added when a test needs it.</summary>
[Union]
public partial record Status
{
    public partial record BoltCharge;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Status"/>: the only place those strings live.</summary>
public static class StatusShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this Status value) =>
        value.Match(
            boltCharge => "Bolt Charge",
            unlisted => unlisted.Shown);
}
