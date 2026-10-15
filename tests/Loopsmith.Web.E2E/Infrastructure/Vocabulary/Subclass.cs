using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A subclass's element, as the designer's Subclass box names it. A case is added when a test needs it.</summary>
[Union]
public partial record Subclass
{
    public partial record Arc;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Subclass"/>: the only place those strings live.</summary>
public static class SubclassShowing
{
    private static readonly Subclass[] Listed = [new Subclass.Arc()];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this Subclass value) =>
        value.Match(
            arc => "Arc",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="Subclass.Unlisted"/>.</summary>
    public static Subclass ReadSubclass(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new Subclass.Unlisted(shown);
}
