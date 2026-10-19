using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A subclass's fragment, as the Subclass box shows it and a step card names what it fired. A case is added when a test needs it.</summary>
[Union]
public partial record Fragment
{
    public partial record SparkOfShock;
    public partial record SparkOfVolts;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Fragment"/>: the only place those strings live.</summary>
public static class FragmentShowing
{
    private static readonly Fragment[] Listed = [new Fragment.SparkOfShock(), new Fragment.SparkOfVolts()];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this Fragment value) =>
        value.Match(
            sparkOfShock => "Spark of Shock",
            sparkOfVolts => "Spark of Volts",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="Fragment.Unlisted"/>.</summary>
    public static Fragment ReadFragment(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new Fragment.Unlisted(shown);
}
