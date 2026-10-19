using Dunet;

namespace Loopsmith.Web.E2E.Infrastructure.Vocabulary;

/// <summary>A subclass's aspect, as a step card names what it fired. A case is added when a test needs it.</summary>
[Union]
public partial record Aspect
{
    public partial record LethalCurrent;
    public partial record Ascension;
    public partial record TempestStrike;
    public partial record FlowState;

    /// <summary>What the UI shows that no case names yet: a failing assertion names it as the UI shows it.</summary>
    public partial record Unlisted(string Shown);
}

/// <summary>What the UI shows for each <see cref="Aspect"/>: the only place those strings live.</summary>
public static class AspectShowing
{
    private static readonly Aspect[] Listed = [new Aspect.LethalCurrent(), new Aspect.Ascension(), new Aspect.TempestStrike(), new Aspect.FlowState()];

    /// <summary>What the UI shows for <paramref name="value"/>.</summary>
    public static string ToShownText(this Aspect value) =>
        value.Match(
            lethalCurrent => "Lethal Current",
            ascension => "Ascension",
            tempestStrike => "Tempest Strike",
            flowState => "Flow State",
            unlisted => unlisted.Shown);

    /// <summary>The case the UI's <paramref name="shown"/> text names, or <see cref="Aspect.Unlisted"/>.</summary>
    public static Aspect ReadAspect(string shown) =>
        Listed.FirstOrDefault(value => value.ToShownText() == shown) ?? new Aspect.Unlisted(shown);
}
