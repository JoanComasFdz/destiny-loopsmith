using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A box the player ticks on the trigger palette's picked row (ADRs D36), as its caption names it. A case is added when a test needs it.</summary>
[Union]
public partial record RowOption
{
    public partial record Precision;
    public partial record BySliding;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="RowOption"/>: the only place those strings live.</summary>
public static class RowOptionShowing
{
    private static readonly RowOption[] Listed = [new RowOption.Precision(), new RowOption.BySliding()];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this RowOption value) =>
        value.Match(
            precision => "🎯 precision",
            bySliding => "🛝 by sliding",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="RowOption.Unlisted"/>.</summary>
    public static RowOption ReadRowOption(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new RowOption.Unlisted(shown);
}
