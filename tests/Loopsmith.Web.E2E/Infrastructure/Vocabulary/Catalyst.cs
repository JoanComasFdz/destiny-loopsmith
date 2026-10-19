using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>An exotic weapon's catalyst, as a step card names what it fired. A case is added when a test needs it.</summary>
[Union]
public partial record Catalyst
{
    public partial record ReturnStroke;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Catalyst"/>: the only place those strings live.</summary>
public static class CatalystShowing
{
    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this Catalyst value) =>
        value.Match(
            returnStroke => "Return Stroke",
            unlisted => unlisted.Shown);
}
